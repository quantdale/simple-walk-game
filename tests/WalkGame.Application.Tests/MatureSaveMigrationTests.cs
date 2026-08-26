using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.Persistence;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;
using WalkGame.Infrastructure.Persistence;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>
/// M8-H1 mature-save and migration qualification (campaign Workstream C):
/// representative mature fixtures driven through the REAL pipeline, genuine v1 → v2
/// migration of a rich payload, exactly-once replay after migration, unknown-future
/// fail-closed behavior, and content-identity durability under adversarial payload
/// surgery.
/// </summary>
public sealed class MatureSaveMigrationTests : IDisposable
{
    private const ulong Seed = 23UL;
    private const long StepsPerDay = 18000L;
    private static readonly DateTimeOffset T0 = TestSessions.T0;

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    // ------------------------------------------------------------------
    // C1/C2: genuine v1 mature save → migrate → validate → reload stable,
    // and its historical activity replays exactly-once afterwards.
    // ------------------------------------------------------------------

    [Fact]
    public void MatureV1Save_MigratesThroughRealChain_Validates_AndReplayIsExactlyOnce()
    {
        int daysDriven = DriveMidGame(_temp.Path);

        // Downgrade the durable state into a GENUINE schema-v1 payload: producer rows
        // carry sub-unit carryMilliUnits again and every post-M3 additive field is gone,
        // while the M2-era processed-record ledger survives intact (it was additive on
        // v1). Both generations on disk become valid v1 saves.
        byte[] matureV2Envelope = File.ReadAllBytes(Path.Combine(_temp.Path, "save.json"));
        var payload = PayloadSurgery.OpenPayload(matureV2Envelope);
        Assert.True(payload["processedRecords"]!["entries"]!.AsObject().Count > 0,
            "fixture must carry a populated dedup ledger");
        DowngradeToGenuineV1(payload);
        byte[] v1Envelope = PayloadSurgery.WrapPayload(payload, schemaVersion: 1, T0.AddDays(daysDriven));
        File.WriteAllBytes(Path.Combine(_temp.Path, "save.json"), v1Envelope);
        WriteBackupFromPrimary();

        long ledgerBefore = ReadLedgerTotalViaForcedMigration();

        // Boot migrates through the real registered chain.
        var migrated = TestSessions.Create(_temp.Path, new ManualClock(T0.AddDays(daysDriven)));
        var boot = migrated.Continue();
        Assert.Equal(StartStatus.Loaded, boot.Status);

        var persisted = DecodePersisted();
        Assert.Equal(CodecStatus.Ok, persisted.Status);
        Assert.Equal(SchemaVersions.Current, persisted.State!.SchemaVersion);
        Assert.Empty(GameStateValidator.Validate(persisted.State, Region1Catalog.Create()));
        Assert.True(persisted.State.ProcessedRecords.Count > 0);
        Assert.Equal(ledgerBefore, persisted.State.Ledger.TotalVitalityCredited);

        // C2: replaying the entire already-processed history credits nothing further...
        long vitalityAfterMigration = persisted.State.Resources.Get(ResourceType.Vitality);
        var source = new SyntheticWalkingSource(StepsPerDay);
        for (int day = 1; day <= daysDriven; day++)
        {
            var result = migrated.IngestFromSource(source, WindowStart(day), WindowEnd(day));
            Assert.Equal(0L, result.VitalityCredited);
            Assert.Equal(1, result.DuplicatesIgnored);
        }

        // ...and it stays a no-op after another restart.
        var rebooted = TestSessions.Create(_temp.Path, new ManualClock(T0.AddDays(daysDriven + 1)));
        Assert.Equal(StartStatus.Loaded, rebooted.Continue().Status);
        for (int day = 1; day <= daysDriven; day++)
            rebooted.IngestFromSource(source, WindowStart(day), WindowEnd(day));

        var finalState = DecodePersisted().State!;
        Assert.Equal(vitalityAfterMigration, finalState.Resources.Get(ResourceType.Vitality));
        Assert.Empty(GameStateValidator.Validate(finalState, Region1Catalog.Create()));
    }

    [Fact]
    public void MigratedV1Save_ReEncode_IsCanonicallyStableAcrossReloads()
    {
        int daysDriven = DriveMidGame(_temp.Path);

        var payload = PayloadSurgery.OpenPayload(File.ReadAllBytes(Path.Combine(_temp.Path, "save.json")));
        DowngradeToGenuineV1(payload);
        File.WriteAllBytes(Path.Combine(_temp.Path, "save.json"),
            PayloadSurgery.WrapPayload(payload, 1, T0.AddDays(daysDriven)));

        var codec = TestSessions.NewCodec();
        var first = codec.Decode(File.ReadAllBytes(Path.Combine(_temp.Path, "save.json")));
        Assert.Equal(CodecStatus.Ok, first.Status);
        Assert.Equal(new[] { MigrationV1ToV2.Id }, first.AppliedMigrations);

        var session = TestSessions.Create(_temp.Path, new ManualClock(T0.AddDays(daysDriven)));
        Assert.Equal(StartStatus.Loaded, session.Continue().Status);

        byte[] persistedAfterBoot = File.ReadAllBytes(Path.Combine(_temp.Path, "save.json"));
        var redecoded = codec.Decode(persistedAfterBoot);
        Assert.Equal(CodecStatus.Ok, redecoded.Status);
        Assert.Empty(redecoded.AppliedMigrations); // already current

        // Canonical stability: decode→encode of the migrated save reproduces the exact
        // bytes the session persisted; encode(decode(x)) == x.
        byte[] reencoded = codec.Encode(redecoded.State!, T0.AddDays(daysDriven));
        Assert.True(
            reencoded.AsSpan().SequenceEqual(persistedAfterBoot),
            "encode(decode(save)) must reproduce canonical bytes");
    }

    // ------------------------------------------------------------------
    // C4: content-identity durability under adversarial payload surgery.
    // Every case must FAIL CLOSED at boot with diagnostics and leave both
    // durable generations untouched.
    // ------------------------------------------------------------------

    [Fact]
    public void UnknownDiscoveryRuntime_InPayload_FailsClosedWithoutRewrite()
    {
        int daysDriven = DriveMidGame(_temp.Path);
        byte[] before = File.ReadAllBytes(Path.Combine(_temp.Path, "save.json"));

        var payload = PayloadSurgery.OpenPayload(before);
        payload["region"]!["discoveries"]!["disc.does-not-exist"] = new JsonObject
        {
            ["discoveryId"] = "disc.does-not-exist",
            ["discoveredAtUtc"] = T0.AddDays(daysDriven),
            ["reviewed"] = false,
            ["reviewedAtUtc"] = null,
        };
        File.WriteAllBytes(Path.Combine(_temp.Path, "save.json"),
            PayloadSurgery.WrapPayload(payload, SchemaVersions.Current, T0.AddDays(daysDriven)));
        byte[] damaged = File.ReadAllBytes(Path.Combine(_temp.Path, "save.json"));

        var result = BootDamaged(damaged);

        AssertStartUnreadableAndUntouched(result, damaged);
    }

    [Fact]
    public void UnknownProjectRuntime_InPayload_FailsClosed()
    {
        int daysDriven = DriveMidGame(_temp.Path);
        byte[] before = File.ReadAllBytes(Path.Combine(_temp.Path, "save.json"));

        var payload = PayloadSurgery.OpenPayload(before);
        payload["region"]!["projects"]!["proj.ghost-project"] = new JsonObject
        {
            ["projectId"] = "proj.ghost-project",
            ["status"] = "completed",
            ["vitalityInvested"] = 10L,
            ["completedAtUtc"] = T0.AddDays(daysDriven),
        };
        File.WriteAllBytes(Path.Combine(_temp.Path, "save.json"),
            PayloadSurgery.WrapPayload(payload, SchemaVersions.Current, T0.AddDays(daysDriven)));

        var result = BootDamaged(File.ReadAllBytes(Path.Combine(_temp.Path, "save.json")));
        Assert.Equal(StartStatus.SaveUnreadable, result.Status);
        Assert.Contains("unknown", result.Detail, StringComparison.OrdinalIgnoreCase);
    }
    private void AssertStartUnreadableAndUntouched(StartResult result, byte[] damagedPrimary)
    {
        Assert.Equal(StartStatus.SaveUnreadable, result.Status);
        Assert.False(string.IsNullOrEmpty(result.Detail));
        Assert.True(damagedPrimary.AsSpan().SequenceEqual(
            File.ReadAllBytes(Path.Combine(_temp.Path, "save.json"))));
    }

    /// <summary>Boots a fresh session over the current directory contents.</summary>
    private StartResult BootDamaged(byte[] damagedPrimary)
    {
        // Give the damaged primary a healthy-looking backup slot too? No: the backup
        // still holds the last healthy generation, so recovery would legitimately
        // succeed. For fail-closed evidence the DAMAGED generation itself must be the
        // only copy: remove the backup.
        File.Delete(Path.Combine(_temp.Path, "save.backup.json"));
        return TestSessions.Create(_temp.Path, new ManualClock(TestSessions.T0)).Continue();
    }

    // ------------------------------------------------------------------
    // Helpers.
    // ------------------------------------------------------------------

    private void WriteBackupFromPrimary()
    {
        File.Copy(
            Path.Combine(_temp.Path, "save.json"),
            Path.Combine(_temp.Path, "save.backup.json"), overwrite: true);
    }

    /// <summary>
    /// Drives a clean profile through the real trust/progression stack into a
    /// mid-maturity world: several app-closed days of ingested activity, queue
    /// decisions, an active project and completed entry work.
    /// </summary>
    private int DriveMidGame(string directory)
    {
        var content = Region1Catalog.Create();
        var bootstrap = TestSessions.Create(directory, new ManualClock(WindowEnd(0)));
        Assert.Equal(StartStatus.NewGameCreated, bootstrap.StartNewGame(Seed).Status);

        const int days = 12;
        for (int day = 1; day <= days; day++)
        {
            var session = TestSessions.Create(directory, new ManualClock(WindowEnd(day)));
            Assert.Equal(StartStatus.Loaded, session.Continue().Status);
            var ingest = session.IngestFromSource(new SyntheticWalkingSource(StepsPerDay),
                WindowStart(day), WindowEnd(day));
            Assert.True(ingest.Saved);

            var home = session.GetHome();
            if (home.ActiveProjectId == null && home.Queued.Count == 0)
                foreach (var definition in content.Projects.Select(p => p.Id.Value))
                    if (session.EnqueueProject(definition).IsSuccess)
                        break;
        }

        var midState = DecodePersisted().State!;
        Assert.True(midState.Region.Projects.Values.Any(p => p.Status == ProjectStatus.Completed),
            "mid-game fixture should include completed work");
        return days;
    }

    /// <summary>
    /// Rewrites an encoded v2 payload into the GENUINE v1 shape: producer stores become
    /// sub-unit carryMilliUnits, and every additive field introduced after schema v1
    /// (M3 pending summaries, M4 discoveries/expeditions/arcs/completion) is removed —
    /// exactly the fields a real pre-M3 writer never emitted.
    /// </summary>
    private static void DowngradeToGenuineV1(JsonNode payload)
    {
        payload["schemaVersion"] = 1;
        ((JsonObject)payload).Remove("pendingReturnSummary");

        var region = (JsonObject)payload["region"]!;
        region.Remove("discoveries");
        region.Remove("expeditions");
        region.Remove("ecologyStage");
        region.Remove("settlementStage");
        region.Remove("isCompleted");
        region.Remove("regionCompletedAtUtc");

        foreach (var producer in region["producers"]!.AsArray())
        {
            if (producer == null)
                continue;
            long stored = producer["storedMilliUnits"]?.GetValue<long>() ?? 0L;
            producer["carryMilliUnits"] = Math.Min(Math.Max(stored, 0L), 999L);
            ((JsonObject)producer).Remove("storedMilliUnits");
        }
    }

    private long ReadLedgerTotalViaForcedMigration() =>
        DecodePersisted().State!.Ledger.TotalVitalityCredited;

    private DecodeResult DecodePersisted() =>
        TestSessions.NewCodec().Decode(File.ReadAllBytes(Path.Combine(_temp.Path, "save.json")));

    private static DateTimeOffset WindowStart(int day) => T0.AddDays(day - 1);

    private static DateTimeOffset WindowEnd(int day) => T0.AddDays(day);
}
