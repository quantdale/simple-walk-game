using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Persistence;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Fixtures;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Application.Tests;

public sealed class SessionIngestionTests : IDisposable
{
    private static readonly DateTimeOffset T0 =
        new DateTimeOffset(2026, 3, 10, 8, 0, 0, TimeSpan.Zero);

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void MixedFixtureBatch_ProducesExactTrustDiagnostics()
    {
        var session = StartNewSession();
        var batch = ActivityFixtures.LoadBatch("walking-mixed-batch.json");

        var result = session.IngestActivityBatch(batch);

        Assert.Equal(7, result.TotalReceived);
        Assert.Equal(3, result.Accepted);
        Assert.Equal(3, result.Rejected);
        Assert.Equal(1, result.DuplicatesIgnored);
        Assert.Equal(1, result.QuantityClamped);
        Assert.Equal(0, result.DuplicateTransactionsIgnored);
        // 64 (6400 steps) + 123 (12345) + 2500 (900000 clamped to 250000)
        Assert.Equal(2687L, result.VitalityCredited);
        Assert.True(result.Saved);
        Assert.Equal(1, result.RejectionCounts["ZeroQuantity"]);
        Assert.Equal(1, result.RejectionCounts["MalformedTimestamps"]);
        Assert.Equal(1, result.RejectionCounts["FutureTimestamp"]);

        var persisted = DecodePersisted();
        Assert.Equal(CodecStatus.Ok, persisted.Status);
        Assert.Equal(3, persisted.State!.ProcessedRecords.Count);
        Assert.Equal(2687L, persisted.State.Ledger.TotalVitalityCredited);
        Assert.Equal(
            new DateTimeOffset(2026, 3, 10, 7, 50, 0, TimeSpan.Zero),
            persisted.State.IngestionCheckpointUtc);
        Assert.Equal(2687L, session.GetHome().Vitality);
    }

    [Fact]
    public void ReplaySameBatchAfterRestart_IsAFullNoOp()
    {
        var first = StartNewSession();
        first.EnqueueProject(TestSessions.EntryProjectId);
        var initial = first.IngestActivityBatch(ActivityFixtures.LoadBatch("walking-clean-batch.json"));
        Assert.Equal(20L, initial.VitalityCredited);

        var reloaded = ContinueSession();
        var replay = reloaded.IngestActivityBatch(ActivityFixtures.LoadBatch("walking-replay-clean.json"));

        Assert.Equal(2, replay.TotalReceived);
        Assert.Equal(0, replay.Accepted);
        Assert.Equal(0, replay.Rejected);
        Assert.Equal(2, replay.DuplicatesIgnored);
        Assert.Equal(0L, replay.VitalityCredited);

        var home = reloaded.GetHome();
        // 20 credited were fully invested into the still-active entry project.
        Assert.Equal(0L, home.Vitality);
        Assert.Equal(TestSessions.EntryProjectId, home.ActiveProjectId);
        Assert.Equal(20L, home.ActiveProjectInvested);

        var persisted = DecodePersisted();
        Assert.Equal(1, persisted.State!.Ledger.Records.Count);
        Assert.Equal(2, persisted.State.ProcessedRecords.Count);
    }

    [Fact]
    public void NewValidRecordAfterFullReplay_AddsOnlyItsDelta()
    {
        var first = StartNewSession();
        first.IngestActivityBatch(ActivityFixtures.LoadBatch("walking-clean-batch.json"));

        var reloaded = ContinueSession();
        reloaded.IngestActivityBatch(ActivityFixtures.LoadBatch("walking-replay-clean.json"));

        var freshRecord = new NormalizedActivityRecord(
            "fixture", "fresh-after-replay", ActivityCategory.Walking,
            ActivityUnits.Steps, 500L,
            T0.AddMinutes(-30), T0.AddMinutes(-5));
        var delta = reloaded.IngestActivityBatch(new[] { freshRecord });

        Assert.Equal(1, delta.Accepted);
        Assert.Equal(5L, delta.VitalityCredited);

        var persisted = DecodePersisted();
        Assert.Equal(25L, persisted.State!.Resources.Get(ResourceType.Vitality));
        Assert.Equal(2, persisted.State.Ledger.Records.Count);
        Assert.Equal(3, persisted.State.ProcessedRecords.Count);
    }

    [Fact]
    public void RecordOrderingDoesNotChangeFinalCanonicalState()
    {
        using var tempA = new TempDirectory();
        using var tempB = new TempDirectory();

        var forward = StartNewSession(tempA);
        forward.IngestActivityBatch(ActivityFixtures.LoadBatch("walking-clean-batch.json"));

        var reversedSession = StartNewSession(tempB);
        var reversed = ActivityFixtures.LoadBatch("walking-clean-batch.json");
        reversed.Reverse();
        reversedSession.IngestActivityBatch(reversed);

        var stateA = DecodeFrom(tempA.Path);
        var stateB = DecodeFrom(tempB.Path);

        Assert.Equal(
            stateA.Resources.Get(ResourceType.Vitality),
            stateB.Resources.Get(ResourceType.Vitality));
        Assert.Equal(stateA.IngestionCheckpointUtc, stateB.IngestionCheckpointUtc);
        Assert.Equal(stateA.ProcessedRecords.Count, stateB.ProcessedRecords.Count);
        Assert.Equal(stateA.Ledger.Records.Count, stateB.Ledger.Records.Count);

        var ledgerA = stateA.Ledger.Records.OrderBy(r => r.TransactionId).ToList();
        var ledgerB = stateB.Ledger.Records.OrderBy(r => r.TransactionId).ToList();
        for (int i = 0; i < ledgerA.Count; i++)
        {
            Assert.Equal(ledgerA[i].TransactionId, ledgerB[i].TransactionId);
            Assert.Equal(ledgerA[i].VitalityAmount, ledgerB[i].VitalityAmount);
        }
    }

    [Fact]
    public void SaveFailureMidIngest_LeavesDiskConsistent_AndRetryCreditsExactlyOnce()
    {
        var store = new FlakySaveStore(_temp.Path);
        var clock = new ManualClock(T0);
        var session = new GameSession(store, TestSessions.NewCodec(), clock, Region1Catalog.Create());
        session.StartNewGame(seed: 7UL);

        store.FailNextWrites = 1;
        Assert.Throws<IOException>(() =>
            session.IngestActivityBatch(ActivityFixtures.LoadBatch("walking-clean-batch.json")));

        // Nothing partially advanced on disk: old valid state, zero ingestion side effects.
        var afterFailure = DecodeFrom(_temp.Path);
        Assert.Equal(0, afterFailure.ProcessedRecords.Count);
        Assert.Equal(0, afterFailure.Ledger.Records.Count);
        Assert.Equal(default, afterFailure.IngestionCheckpointUtc);
        Assert.Equal(0L, afterFailure.Resources.Get(ResourceType.Vitality));

        var reloaded = ContinueSession();
        var retry = reloaded.IngestActivityBatch(ActivityFixtures.LoadBatch("walking-replay-clean.json"));

        Assert.Equal(2, retry.Accepted);
        Assert.Equal(20L, retry.VitalityCredited);

        var final = DecodePersisted();
        Assert.Equal(CodecStatus.Ok, final.Status);
        Assert.Equal(20L, final.State!.Resources.Get(ResourceType.Vitality));
        Assert.Equal(1, final.State.Ledger.Records.Count);
        Assert.Equal(2, final.State.ProcessedRecords.Count);
    }

    [Fact]
    public void CheckpointNeverExceedsMaxTrustedEndUtc()
    {
        var session = StartNewSession();

        var earlyOnly = new NormalizedActivityRecord(
            "fixture", "early-record", ActivityCategory.Walking,
            ActivityUnits.Steps, 1000L,
            T0.AddHours(-2), T0.AddHours(-1));
        session.IngestActivityBatch(new[] { earlyOnly });
        Assert.Equal(T0.AddHours(-1), DecodePersisted().State!.IngestionCheckpointUtc);

        session.IngestActivityBatch(ActivityFixtures.LoadBatch("walking-mixed-batch.json"));
        Assert.Equal(
            new DateTimeOffset(2026, 3, 10, 7, 50, 0, TimeSpan.Zero),
            DecodePersisted().State!.IngestionCheckpointUtc);
    }

    [Fact]
    public void SubUnitRecord_IsTrustedWithoutRewardNoise()
    {
        var session = StartNewSession();

        var tiny = new NormalizedActivityRecord(
            "fixture", "tiny-99", ActivityCategory.Walking,
            ActivityUnits.Steps, 99L,
            T0.AddMinutes(-30), T0.AddMinutes(-25));
        var result = session.IngestActivityBatch(new[] { tiny });

        Assert.Equal(1, result.Accepted);
        Assert.Equal(0L, result.VitalityCredited);

        var persisted = DecodePersisted();
        Assert.Equal(0, persisted.State!.Ledger.Records.Count);
        Assert.Equal(1, persisted.State.ProcessedRecords.Count);

        var replay = session.IngestActivityBatch(new[] { tiny });
        Assert.Equal(1, replay.DuplicatesIgnored);
        Assert.Equal(0, replay.Accepted);
    }

    [Fact]
    public void InvalidOnlyBatch_ChangesNothingDurable()
    {
        var session = StartNewSession();

        var invalid = new List<NormalizedActivityRecord>
        {
            Valid("zero", 0L),
            Valid("negative", -50L),
            Valid("future", 100L, endUtcOverride: T0.AddHours(2)),
        };
        var result = session.IngestActivityBatch(invalid);

        Assert.Equal(3, result.TotalReceived);
        Assert.Equal(0, result.Accepted);
        Assert.Equal(3, result.Rejected);

        var persisted = DecodePersisted();
        Assert.Equal(0, persisted.State!.ProcessedRecords.Count);
        Assert.Equal(0, persisted.State.Ledger.Records.Count);
        Assert.Equal(default, persisted.State.IngestionCheckpointUtc);
        Assert.Equal(0L, persisted.State.Resources.Get(ResourceType.Vitality));
    }

    [Fact]
    public void IngestedVitality_CompletesQueuedProject_AndAdvancesRestorationState()
    {
        var session = StartNewSession();
        var enqueued = session.EnqueueProject(TestSessions.EntryProjectId);
        Assert.True(enqueued.IsSuccess, enqueued.Error?.Message);

        var result = session.IngestActivityBatch(ActivityFixtures.LoadBatch("walking-mixed-batch.json"));

        var home = session.GetHome();
        Assert.Equal(1, home.CompletedProjects);
        Assert.Null(home.ActiveProjectId);
        // 2687 credited − 300 invested into trailhead.
        Assert.Equal(2387L, home.Vitality);

        var trailheadStage = home.Landmarks.Single(row => row.LandmarkId == "lm.trailhead");
        Assert.Equal(RestorationStage.Stabilized, trailheadStage.Stage);

        var persisted = DecodePersisted();
        var runtime = persisted.State!.Region.FindProject(TestSessions.EntryProjectId)!;
        Assert.Equal(ProjectStatus.Completed, runtime.Status);
        Assert.Equal(T0, runtime.CompletedAtUtc);
        Assert.Equal(RestorationStage.Stabilized, persisted.State.Region.LandmarkStages["lm.trailhead"]);
    }

    [Fact]
    public void FingerprintIdentity_DedupSurvivesPersistenceRoundtrip()
    {
        var first = StartNewSession();
        var fingerprinted = new NormalizedActivityRecord(
            "fixture", null, ActivityCategory.Walking,
            ActivityUnits.Steps, 3000L,
            T0.AddMinutes(-60), T0.AddMinutes(-10));
        first.IngestActivityBatch(new[] { fingerprinted });

        var reloaded = ContinueSession();
        var replay = reloaded.IngestActivityBatch(new[] { fingerprinted });

        Assert.Equal(1, replay.DuplicatesIgnored);
        Assert.Equal(0L, replay.VitalityCredited);
        Assert.Equal(30L, reloaded.GetHome().Vitality);
    }

    private static NormalizedActivityRecord Valid(
        string sourceRecordId, long quantity, DateTimeOffset? endUtcOverride = null) =>
        new NormalizedActivityRecord(
            "fixture",
            sourceRecordId,
            ActivityCategory.Walking,
            ActivityUnits.Steps,
            quantity,
            T0.AddMinutes(-40),
            endUtcOverride ?? T0.AddMinutes(-20));

    private GameSession StartNewSession(TempDirectory? directory = null)
    {
        var session = TestSessions.Create(directory ?? _temp, new ManualClock(T0));
        Assert.Equal(StartStatus.NewGameCreated, session.StartNewGame(seed: 7UL).Status);
        return session;
    }

    private GameSession ContinueSession()
    {
        var reloaded = TestSessions.Create(_temp, new ManualClock(T0));
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);
        return reloaded;
    }

    private DecodeResult DecodePersisted() => DecodeFrom(_temp.Path);

    private static DecodeResult DecodeFrom(string directory) =>
        TestSessions.NewCodec().Decode(File.ReadAllBytes(Path.Combine(directory, "save.json")));

    /// <summary>Injects IOException at the atomic-commit boundary to prove interruption safety.</summary>
    private sealed class FlakySaveStore : ISaveStore
    {
        private readonly AtomicFileSaveStore _inner;

        public FlakySaveStore(string directory) => _inner = new AtomicFileSaveStore(directory);

        public int FailNextWrites { get; set; }

        public void WriteAtomic(byte[] envelopeBytes)
        {
            if (FailNextWrites > 0)
            {
                FailNextWrites--;
                throw new IOException("Injected write interruption.");
            }
            _inner.WriteAtomic(envelopeBytes);
        }

        public SaveReadResult ReadPrimary() => _inner.ReadPrimary();

        public SaveReadResult ReadBackup() => _inner.ReadBackup();
    }
}
