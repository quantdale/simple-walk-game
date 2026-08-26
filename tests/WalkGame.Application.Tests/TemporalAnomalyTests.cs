using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalkGame.Application.Content;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Application.Persistence;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>
/// M8-H1 clock and temporal anomaly hardening (campaign Workstream E): canonical
/// progression relies exclusively on UTC instants and the injected clock, so machine
/// locale/timezone can never enter the trust pipeline. Every anomaly degrades
/// deterministically without negative resources, duplicate advancement or fabricated
/// activity.
/// </summary>
public sealed class TemporalAnomalyTests : IDisposable
{
    private static readonly DateTimeOffset T0 = TestSessions.T0;
    private const string Provider = "temporal.provider";

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    // ------------------------------------------------------------------
    // Horizon/skew boundaries are inclusive exactly as documented (D-030).
    // ------------------------------------------------------------------

    [Fact]
    public void HorizonAndSkewBoundaries_AreDecidedExactlyAtTheDocumentedEdges()
    {
        Bootstrap();
        var session = Session(T0.AddDays(20));
        session.Continue();

        var results = session.IngestActivityBatch(new[]
        {
            // Ends EXACTLY at now − 14d: inside the documented inclusive boundary.
            Record("edge-horizon-exact", 100L,
                startUtc: T0.AddDays(5), endUtc: T0.AddDays(6)),
            // Ends EXACTLY at now + 10m: inside the documented skew allowance.
            Record("edge-future-exact", 200L,
                startUtc: T0.AddDays(19).AddMinutes(30), endUtc: T0.AddDays(20).AddMinutes(10)),
        });

        Assert.Equal(2, results.Accepted);
        Assert.Equal(0, results.Rejected);

        var rejected = session.IngestActivityBatch(new[]
        {
            // One tick past the horizon edge.
            Record("beyond-horizon", 300L,
                startUtc: T0.AddDays(5).AddSeconds(-1), endUtc: T0.AddDays(6).AddTicks(-1)),
            // One tick past the future-skew edge.
            Record("beyond-skew", 400L,
                startUtc: T0.AddDays(19).AddMinutes(31), endUtc: T0.AddDays(20).AddMinutes(10).AddTicks(1)),
        });

        Assert.Equal(0, rejected.Accepted);
        Assert.Equal(1, rejected.RejectionCounts[nameof(ActivityValidationStatus.OutsideHorizon)]);
        Assert.Equal(1, rejected.RejectionCounts[nameof(ActivityValidationStatus.FutureTimestamp)]);
        Assert.Equal(0L, rejected.VitalityCredited);
    }

    // ------------------------------------------------------------------
    // Identical content at identical timestamps IS one logical record.
    // ------------------------------------------------------------------

    [Fact]
    public void IdenticalFingerprintContent_IsOneLogicalRecord_CreditedOnce()
    {
        Bootstrap();
        var session = Session(T0.AddDays(2));
        session.Continue();

        var row = FingerprintRow("same", 5_000L, T0.AddHours(10));

        var result = session.IngestActivityBatch(new[] { row, CopyWithSameIdentity(row) });

        Assert.Equal(50L, result.VitalityCredited);
        Assert.Equal(1, result.Accepted);
        Assert.Equal(1, result.DuplicatesIgnored);
    }

    // ------------------------------------------------------------------
    // Locale/timezone independence: only the UTC instant may matter.
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(5.5)]
    [InlineData(-8.0)]
    [InlineData(0.0)]
    public void OffsetExpressedRecords_CreditIdenticallyToUtcInstants(double offsetHours)
    {
        long steps = 12_345L;

        long RunWith(double off)
        {
            using var dir = new TempDirectory();
            var bootstrap = TestSessions.Create(dir.Path, new ManualClock(T0.AddDays(4)));
            Assert.Equal(StartStatus.NewGameCreated, bootstrap.StartNewGame(3UL).Status);

            var offset = TimeSpan.FromHours(off);
            var start = new DateTimeOffset(2026, 3, 12, 18, 15, 0, offset);
            var end = start.AddMinutes(47);
            var session = TestSessions.Create(dir.Path, new ManualClock(T0.AddDays(4)));
            session.Continue();
            var result = session.IngestActivityBatch(new[]
            {
                new NormalizedActivityRecord(Provider, "offset-rec", ActivityCategory.Walking,
                    ActivityUnits.Steps, steps, start, end),
            });
            Assert.Equal(1, result.Accepted);
            return CanonicalVitality(dir.Path);
        }

        Assert.Equal(RunWith(0.0), RunWith(offsetHours));
    }

    // ------------------------------------------------------------------
    // Calendar boundaries and leap days are ordinary UTC instants.
    // ------------------------------------------------------------------

    [Fact]
    public void YearMonthAndLeapDayBoundaries_CreditDeterministically()
    {
        static List<NormalizedActivityRecord> Around(DateTimeOffset anchor) => new()
        {
            new NormalizedActivityRecord("cal.provider", "a", ActivityCategory.Walking,
                ActivityUnits.Steps, 8_000L, anchor.AddHours(-2), anchor.AddMinutes(-30)),
            new NormalizedActivityRecord("cal.provider", "b", ActivityCategory.Walking,
                ActivityUnits.Steps, 9_000L, anchor.AddMinutes(30), anchor.AddHours(3)),
        };

        DateTimeOffset leapEve = new(2028, 2, 28, 23, 0, 0, TimeSpan.Zero);
        DateTimeOffset yearEnd = new(2027, 12, 31, 23, 0, 0, TimeSpan.Zero);
        DateTimeOffset monthEnd = new(2026, 4, 30, 23, 0, 0, TimeSpan.Zero);

        foreach (var anchor in new[] { leapEve, yearEnd, monthEnd })
        {
            using var dir = new TempDirectory();
            var bootstrap = TestSessions.Create(dir.Path, new ManualClock(anchor.AddDays(3)));
            Assert.Equal(StartStatus.NewGameCreated, bootstrap.StartNewGame(9UL).Status);

            var session = TestSessions.Create(dir.Path, new ManualClock(anchor.AddDays(3)));
            session.Continue();
            var result = session.IngestActivityBatch(Around(anchor));

            Assert.Equal(2, result.Accepted);
            Assert.Equal(170L, result.VitalityCredited);
            Assert.Empty(GameStateValidator.Validate(DecodeState(dir.Path), Region1Catalog.Create()));
        }
    }

    // ------------------------------------------------------------------
    // Repeated boots with zero elapsed canonical time fabricate nothing.
    // ------------------------------------------------------------------

    [Fact]
    public void RepeatedZeroElapsedBoots_FabricateNoProgress()
    {
        Bootstrap();
        var writer = Session(T0.AddDays(1));
        writer.Continue();
        writer.IngestActivityBatch(new[] { Record("zero-elapsed", 10_000L, startUtc: T0, endUtc: T0.AddHours(1)) });

        var baseline = DecodeState(_temp.Path);

        for (int i = 0; i < 5; i++)
        {
            var session = Session(T0.AddDays(1));
            Assert.Equal(StartStatus.Loaded, session.Continue().Status);
        }

        var after = DecodeState(_temp.Path);
        Assert.Equal(baseline.Ledger.TotalVitalityCredited, after.Ledger.TotalVitalityCredited);
        Assert.Equal(baseline.LastAdvancedUtc, after.LastAdvancedUtc);
        Assert.Equal(
            baseline.Region.Projects.Values.Count(p => p.Status == ProjectStatus.Completed),
            after.Region.Projects.Values.Count(p => p.Status == ProjectStatus.Completed));
    }

    // ------------------------------------------------------------------
    // Very long absence: production stays bounded by the store/cap model.
    // ------------------------------------------------------------------

    [Fact]
    public void FourThousandDayAbsence_ProductionBounded_StateRemainsValid()
    {
        var content = Region1Catalog.Create();
        var bootstrap = TestSessions.Create(_temp.Path, new ManualClock(T0));
        bootstrap.StartNewGame(17UL);

        // Fixture setup through the real codec: complete the first producer's unlock
        // project and unlock it so the long-absence boot has production to bound.
        var state = DecodeState(_temp.Path);
        var producer = state.Region.Producers[0];
        string unlockerId = content.FindProducer(producer.ProducerId)!.UnlockedByProjectId;
        var unlocker = state.Region.FindProject(unlockerId)!;
        unlocker.Status = ProjectStatus.Completed;
        unlocker.CompletedAtUtc = T0;
        producer.Unlocked = true;
        producer.LastTickUtc = T0;
        var store = new WalkGame.Infrastructure.Persistence.AtomicFileSaveStore(_temp.Path);
        byte[] envelope = TestSessions.NewCodec().Encode(state, T0);
        store.WriteAtomic(envelope);

        var farFuture = Session(T0.AddDays(4000));
        Assert.Equal(StartStatus.Loaded, farFuture.Continue().Status);

        var after = DecodeState(_temp.Path);
        Assert.True(after.LastAdvancedUtc > T0);
        Assert.Equal(T0.AddDays(4000), after.LastAdvancedUtc);

        foreach (var runtime in after.Region.Producers)
        {
            var definition = content.FindProducer(runtime.ProducerId)!;
            Assert.InRange(runtime.StoredMilliUnits, 0L, definition.CapacityUnits * 1000L);
            Assert.True(runtime.TotalProducedMilliUnits >= 0L);
        }

        Assert.All(after.Resources.Amounts.Values, v => Assert.True(v >= 0L));
        Assert.Empty(GameStateValidator.Validate(after, content));
    }

    // ------------------------------------------------------------------
    // Backward wall clock mid-history: skew ignored, nothing regresses.
    // ------------------------------------------------------------------

    [Fact]
    public void BackwardWallClock_Ignored_WithoutCheckpointOrProgressRegression()
    {
        Bootstrap();
        var forward = Session(T0.AddDays(5));
        forward.Continue();
        forward.IngestActivityBatch(new[]
        {
            Record("backward-guard", 20_000L, startUtc: T0.AddDays(4), endUtc: T0.AddDays(4).AddHours(6)),
        });
        long ledgerForward = DecodeState(_temp.Path).Ledger.TotalVitalityCredited;
        var checkpointForward = DecodeState(_temp.Path).IngestionCheckpointUtc;

        // Wall clock moves backward; the record still fits the (now smaller) window.
        var backward = Session(T0.AddDays(3));
        Assert.Equal(StartStatus.Loaded, backward.Continue().Status);
        backward.IngestActivityBatch(new[]
        {
            Record("backward-new", 5_000L, startUtc: T0.AddDays(2), endUtc: T0.AddDays(2).AddHours(4)),
        });

        var state = DecodeState(_temp.Path);
        Assert.Equal(checkpointForward, state.IngestionCheckpointUtc); // watermark never regresses
        Assert.True(state.LastAdvancedUtc >= checkpointForward - TimeSpan.FromDays(14));

        // Forward again: the skipped-time world resumes; the replayed old record stays
        // an exactly-once no-op while the legitimately new record keeps its credit.
        var resumed = Session(T0.AddDays(6));
        Assert.Equal(StartStatus.Loaded, resumed.Continue().Status);
        var replay = resumed.IngestActivityBatch(new[]
        {
            Record("backward-guard", 20_000L, startUtc: T0.AddDays(4), endUtc: T0.AddDays(4).AddHours(6)),
        });
        Assert.Equal(1, replay.DuplicatesIgnored);
        Assert.Equal(0L, replay.VitalityCredited);
        Assert.Empty(GameStateValidator.Validate(DecodeState(_temp.Path), Region1Catalog.Create()));
    }

    // ------------------------------------------------------------------
    // Helpers.
    // ------------------------------------------------------------------

    private void Bootstrap()
    {
        var session = TestSessions.Create(_temp.Path, new ManualClock(T0));
        Assert.Equal(StartStatus.NewGameCreated, session.StartNewGame(7UL).Status);
    }

    private GameSession Session(DateTimeOffset now) =>
        TestSessions.Create(_temp.Path, new ManualClock(now));

    private static NormalizedActivityRecord Record(
        string id, long steps, int revision = 1, DateTimeOffset? startUtc = null, DateTimeOffset? endUtc = null) =>
        new NormalizedActivityRecord(Provider, id, ActivityCategory.Walking, ActivityUnits.Steps,
            steps, startUtc ?? T0.AddMinutes(-90), endUtc ?? (startUtc ?? T0.AddMinutes(-90)).AddMinutes(45),
            revision, false);

    private static NormalizedActivityRecord FingerprintRow(string tag, long steps, DateTimeOffset start) =>
        new NormalizedActivityRecord("fp." + tag, null, ActivityCategory.Walking, ActivityUnits.Steps,
            steps, start, start.AddMinutes(40));

    private static NormalizedActivityRecord CopyWithSameIdentity(NormalizedActivityRecord row) =>
        row with { };

    private long CanonicalVitality(string directory) =>
        DecodeState(directory).Resources.Get(ResourceType.Vitality);

    private GameState DecodeState(string directory)
    {
        var decoded = TestSessions.NewCodec().Decode(File.ReadAllBytes(Path.Combine(directory, "save.json")));
        Assert.Equal(CodecStatus.Ok, decoded.Status);
        return decoded.State!;
    }
}
