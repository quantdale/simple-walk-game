using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalkGame.Application.Content;
using WalkGame.Application.Persistence;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>
/// M8-H1 boot/recovery end-to-end hostile-path evidence (campaign Workstream B, session
/// level): recovery preserves the last healthy generation, unrecoverable saves fail
/// closed without fabricating a fresh world, interrupted persistence never reports
/// success, and replay after an interrupted commit is exactly-once.
/// </summary>
public sealed class SessionPersistenceHardeningTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void RecoveryFromBackup_RepeatedBoots_StableAndNeverRepeatedNotice()
    {
        var clock = new ManualClock(TestSessions.T0);
        var writer = TestSessions.Create(_temp, clock);
        writer.StartNewGame(seed: 7UL);
        writer.CreditActivity(TestSessions.Tx1, TestSessions.T0, 250L, "walk");
        writer.CreditActivity(TestSessions.Tx2, TestSessions.T0.AddMinutes(1), 10L, "run");
        // Disk now: primary = 260 Vitality (corrupted below), backup = 250 Vitality.

        CorruptPrimary();

        var first = TestSessions.Create(_temp, clock).Continue();
        Assert.Equal(StartStatus.RecoveredFromBackup, first.Status);
        Assert.Contains(first.SummaryLines, l => l.Contains("backup", StringComparison.Ordinal));

        // The recovered generation is durable in the primary slot...
        var second = TestSessions.Create(_temp, clock).Continue();
        Assert.Equal(StartStatus.Loaded, second.Status);
        Assert.Equal(250L, ReadPersistedVitality());

        // ...and repeated boots stay stable.
        var third = TestSessions.Create(_temp, clock).Continue();
        Assert.Equal(StartStatus.Loaded, third.Status);

        // The backup slot must still hold a decodable generation after recovery.
        var store = new AtomicFileSaveStore(_temp.Path);
        Assert.Equal(SaveReadOutcome.Success, store.ReadBackup().Outcome);
    }

    [Fact]
    public void UnrecoverableSaves_ContinueFailsClosed_AndNeverFabricatesAFreshWorld()
    {
        var clock = new ManualClock(TestSessions.T0);
        var writer = TestSessions.Create(_temp, clock);
        writer.StartNewGame(seed: 7UL);
        writer.CreditActivity(TestSessions.Tx1, TestSessions.T0, 250L, "walk");

        File.WriteAllText(_temp.FilePath("save.json"), "mature-progress-now-unreadable");
        File.WriteAllText(_temp.FilePath("save.backup.json"), "{also-broken");
        var primaryBefore = File.ReadAllBytes(_temp.FilePath("save.json"));
        var backupBefore = File.ReadAllBytes(_temp.FilePath("save.backup.json"));

        var result = TestSessions.Create(_temp, clock).Continue();

        Assert.Equal(StartStatus.SaveUnreadable, result.Status);
        Assert.False(string.IsNullOrEmpty(result.Detail));
        Assert.Null(result.Summary);

        // Boot must not silently replace mature progress with a fresh profile.
        Assert.True(primaryBefore.SequenceEqual(File.ReadAllBytes(_temp.FilePath("save.json"))));
        Assert.True(backupBefore.SequenceEqual(File.ReadAllBytes(_temp.FilePath("save.backup.json"))));
    }

    [Fact]
    public void FutureSchemaSave_ContinueReportsUnsupported_NeverOverwritesOrResets()
    {
        var clock = new ManualClock(TestSessions.T0);
        var writer = TestSessions.Create(_temp, clock);
        writer.StartNewGame(seed: 7UL);
        writer.CreditActivity(TestSessions.Tx1, TestSessions.T0, 250L, "walk");

        // Both durable generations claim an unsupported future schema: recovery cannot
        // help either, so boot must fail closed without rewriting or resetting anything.
        RewriteEnvelopeSchemaVersion(_temp.FilePath("save.json"), 99);
        RewriteEnvelopeSchemaVersion(_temp.FilePath("save.backup.json"), 99);
        var primaryBefore = File.ReadAllBytes(_temp.FilePath("save.json"));
        var backupBefore = File.ReadAllBytes(_temp.FilePath("save.backup.json"));

        var result = TestSessions.Create(_temp, clock).Continue();

        Assert.Equal(StartStatus.SaveUnreadable, result.Status);
        Assert.Contains("newer", result.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.True(primaryBefore.SequenceEqual(File.ReadAllBytes(_temp.FilePath("save.json"))),
            "an unsupported future save must be left exactly as found");
        Assert.True(backupBefore.SequenceEqual(File.ReadAllBytes(_temp.FilePath("save.backup.json"))));
    }

    [Fact]
    public void InterruptedCommit_DuringIngest_NoPartialCredit_ExactlyOnceOnRetry()
    {
        var clock = new ManualClock(TestSessions.T0);
        var flaky = new FailOnceStore(_temp.Path);
        var session = new GameSession(flaky, TestSessions.NewCodec(), clock, Region1Catalog.Create());
        Assert.Equal(StartStatus.NewGameCreated, session.StartNewGame(seed: 7UL).Status);

        var batch = new List<NormalizedActivityRecord>
        {
            Record("rec-a", 5_000L),
            Record("rec-b", 7_500L),
        };

        flaky.FailNextWrites = 1;
        Assert.Throws<IOException>(() => session.IngestActivityBatch(batch));

        // A fresh boot over the same directory sees only the last durable generation:
        // no partial credit, no checkpoint advance, no half-written ledger.
        var reloaded = TestSessions.Create(_temp, new ManualClock(TestSessions.T0));
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);
        var homeAfterCrash = reloaded.GetHome();
        Assert.Equal(0L, homeAfterCrash.Vitality);

        // Retrying the identical batch credits exactly once.
        var retry = reloaded.IngestActivityBatch(batch);
        Assert.True(retry.Saved);
        Assert.Equal(125L, retry.VitalityCredited);
        Assert.Equal(2, retry.Accepted);

        var replay = TestSessions.Create(_temp, new ManualClock(TestSessions.T0));
        replay.Continue();
        var replayResult = replay.IngestActivityBatch(batch);
        Assert.Equal(2, replayResult.DuplicatesIgnored);
        Assert.Equal(0L, replayResult.VitalityCredited);
        Assert.Equal(125L, replay.GetHome().Vitality);
    }

    [Fact]
    public void DeletionMarker_WithoutIdentifiableNamespace_IsCountedInDiagnostics()
    {
        var session = StartNewSession();

        var result = session.IngestActivityBatch(new List<NormalizedActivityRecord>
        {
            new NormalizedActivityRecord(
                ProviderNamespace: "",
                SourceRecordId: null,
                Category: ActivityCategory.Walking,
                Unit: ActivityUnits.Steps,
                Quantity: 100L,
                StartUtc: TestSessions.T0.AddMinutes(-30),
                EndUtc: TestSessions.T0.AddMinutes(-10),
                Revision: 1,
                IsDeletion: true),
        });

        Assert.Equal(1, result.DeletionsIgnored);
        Assert.Equal(0L, result.VitalityCredited);
    }

    // ------------------------------------------------------------------
    // Helpers.
    // ------------------------------------------------------------------

    private GameSession StartNewSession()
    {
        var session = TestSessions.Create(_temp, new ManualClock(TestSessions.T0));
        Assert.Equal(StartStatus.NewGameCreated, session.StartNewGame(seed: 7UL).Status);
        return session;
    }

    private long ReadPersistedVitality()
    {
        var decoded = TestSessions.NewCodec().Decode(File.ReadAllBytes(_temp.FilePath("save.json")));
        Assert.Equal(CodecStatus.Ok, decoded.Status);
        return decoded.State!.Resources.Get(ResourceType.Vitality);
    }

    private void CorruptPrimary()
    {
        var bytes = File.ReadAllBytes(_temp.FilePath("save.json"));
        bytes[3] ^= 0xFF;
        File.WriteAllBytes(_temp.FilePath("save.json"), bytes);
    }

    /// <summary>Rewrites one envelope frame's schemaVersion to an unsupported value.</summary>
    private static void RewriteEnvelopeSchemaVersion(string path, int version)
    {
        var json = System.Text.Json.JsonSerializer.Deserialize<
            System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>>(
            File.ReadAllText(path));

        var rewritten = new Dictionary<string, object>();
        foreach (var pair in json!)
            rewritten[pair.Key] =
                pair.Key == "schemaVersion"
                    ? (object)version
                    : (object)pair.Value.ToString();

        File.WriteAllText(path,
            System.Text.Json.JsonSerializer.Serialize(rewritten));
    }

    private static NormalizedActivityRecord Record(string sourceId, long steps) =>
        new NormalizedActivityRecord(
            ProviderNamespace: "test.hardening",
            SourceRecordId: sourceId,
            Category: ActivityCategory.Walking,
            Unit: ActivityUnits.Steps,
            Quantity: steps,
            StartUtc: TestSessions.T0.AddMinutes(-50),
            EndUtc: TestSessions.T0.AddMinutes(-20));

    /// <summary>Injects IOException at the atomic-commit boundary.</summary>
    private sealed class FailOnceStore : ISaveStore
    {
        private readonly AtomicFileSaveStore _inner;

        public FailOnceStore(string directory) => _inner = new AtomicFileSaveStore(directory);

        public int FailNextWrites { get; set; }

        public void WriteAtomic(byte[] envelopeBytes)
        {
            if (FailNextWrites > 0)
            {
                FailNextWrites--;
                throw new IOException("Injected storage failure.");
            }
            _inner.WriteAtomic(envelopeBytes);
        }

        public void WriteAtomicPreservingBackup(byte[] envelopeBytes) =>
            _inner.WriteAtomicPreservingBackup(envelopeBytes);

        public SaveReadResult ReadPrimary() => _inner.ReadPrimary();

        public SaveReadResult ReadBackup() => _inner.ReadBackup();
    }
}
