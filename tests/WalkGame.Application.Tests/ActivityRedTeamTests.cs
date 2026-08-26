using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.Persistence;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Time;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>
/// M8-H1 adversarial activity/reconciliation red-team (campaign Workstream D).
///
/// Every record — hostile or honest — enters through the SAME IngestActivityBatch trust
/// pipeline production adapters use; nothing mutates Vitality directly. A canonical
/// reference execution over clean ordered history is compared against semantically
/// equivalent hostile permutations across every durable economic surface, and the fully
/// replayed hostile history must be an exact no-op.
///
/// Correction/deletion semantics that deliberately depend on order (conservative
/// clawbacks bounded by the unspent balance) get dedicated exact-value scenarios
/// instead of cross-permutation equality.
/// </summary>
public sealed class ActivityRedTeamTests : IDisposable
{
    private static readonly DateTimeOffset T0 = TestSessions.T0;
    private const string Provider = "redteam.provider";

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    // ------------------------------------------------------------------
    // Convergence: equivalent histories → identical canonical state.
    // ------------------------------------------------------------------

    [Fact]
    public void HostilePermutations_ConvergeToReferenceState_AndFullReplayIsNoOp()
    {
        var reference = RunScenario(ScenarioStyle.ReferenceOrderedBatches);

        var variants = new[]
        {
            ("single-batch", ScenarioStyle.OneSingleBatch),
            ("reversed-order", ScenarioStyle.ReversedOrder),
            ("duplicated-records", ScenarioStyle.EveryRecordDuplicated),
            ("restart-between-records", ScenarioStyle.RestartBetweenEveryRecord),
            ("junk-mixed-in", ScenarioStyle.JunkMixedIn),
        };

        foreach (var (name, style) in variants)
        {
            using var dir = new TempDirectory();
            var snapshot = RunScenario(style, dir.Path);
            Assert.True(reference.Equals(snapshot),
                $"scenario '{name}' diverged from reference.\nreference: {reference}\nactual:    {snapshot}");
        }
    }

    [Fact]
    public void OverlappingQueryWindows_ThroughSourceSeam_AreExactlyOnce()
    {
        using var referenceDir = new TempDirectory();
        var reference = RunSyntheticReference(referenceDir.Path);

        using var dir = new TempDirectory();
        Bootstrap(dir.Path);
        var source = new SyntheticWalkingSource(20000L);

        // Each two-day query overlaps its predecessor by exactly one UTC day while the
        // UNION of covered days matches the reference history.
        for (int day = 1; day <= 7; day++)
        {
            var session = Session(dir.Path, WindowEnd(day + 1));
            Assert.Equal(StartStatus.Loaded, session.Continue().Status);
            session.IngestFromSource(source, WindowEnd(day - 1), WindowEnd(day + 1));
        }

        var actual = CanonicalSnapshot.Of(dir.Path);
        Assert.True(reference.Equals(actual),
            $"overlapping windows diverged.\nreference: {reference}\nactual:    {actual}");
    }

    [Fact]
    public void HugeDuplicateFlood_IsIgnoredWithoutCost_AndReplayStaysNoOp()
    {
        Bootstrap(_temp.Path);
        var batch = ReferenceRecords().ToList();
        var session = Session(_temp.Path, T0.AddDays(11));
        session.Continue();
        var first = session.IngestActivityBatch(batch);
        Assert.Equal(batch.Count, first.Accepted);
        long credited = first.VitalityCredited;

        // 5,000-record flood of the identical logical records.
        var flood = new List<NormalizedActivityRecord>();
        for (int i = 0; i < 5000 / batch.Count + 1; i++)
            flood.AddRange(batch);

        var flooded = session.IngestActivityBatch(flood);
        Assert.Equal(flood.Count, flooded.DuplicatesIgnored);
        Assert.Equal(0L, flooded.VitalityCredited);
        Assert.True(flooded.Saved);

        var after = CanonicalSnapshot.Of(_temp.Path);
        Assert.Equal(credited, after.LedgerTotal);
        Assert.Equal(batch.Count, after.ProcessedCount);
    }

    // ------------------------------------------------------------------
    // Corrections and deletions: documented conservative-clawback semantics.
    // ------------------------------------------------------------------

    [Fact]
    public void CorrectionUpThenDown_AcrossRestart_TracksNetAppliedAndCountsClamps()
    {
        Bootstrap(_temp.Path);
        var session = Session(_temp.Path, T0.AddDays(1));
        session.Continue();
        ActivateEntryWork(session);

        var original = Record("rec-corr", 10_000L); // 100 Vitality target
        var up = session.IngestActivityBatch(new[] { original });
        Assert.Equal(100L, up.VitalityCredited);

        // Revision 2 corrects upward (+5,000 steps → target 150 Vitality, delta +50).
        var reloaded = Session(_temp.Path, T0.AddDays(1).AddMinutes(5));
        reloaded.Continue();
        var correctedUp = reloaded.IngestActivityBatch(new[]
        {
            Record("rec-corr", 15_000L, revision: 2),
        });
        Assert.Equal(1, correctedUp.CorrectionsApplied);
        Assert.Equal(50L, correctedUp.VitalityCredited);

        long balanceBeforeDown = reloaded.GetHome().Vitality;

        // Revision 3 corrects back down toward the original value: the pipeline deltas
        // against the row's NET-APPLIED vitality (150 → target 100 ⇒ delta −50) and
        // claws back only what the unspent balance funds, counting any remainder.
        var reloaded2 = Session(_temp.Path, T0.AddDays(1).AddMinutes(10));
        reloaded2.Continue();
        var correctedDown = reloaded2.IngestActivityBatch(new[]
        {
            Record("rec-corr", 10_000L, revision: 3),
        });
        Assert.Equal(1, correctedDown.CorrectionsApplied);

        const long netAppliedBeforeDown = 150L;
        const long downTarget = 100L;
        long delta = downTarget - netAppliedBeforeDown; // −50
        long expectedClaw = Math.Max(delta, -balanceBeforeDown);

        Assert.Equal(expectedClaw, correctedDown.VitalityCredited);
        Assert.Equal(expectedClaw - delta, correctedDown.UnappliedReversalVitality);

        // The processed row tracks NET-APPLIED vitality, never outrunning the ledger.
        var state = CanonicalSnapshot.Of(_temp.Path);
        Assert.Equal(netAppliedBeforeDown + expectedClaw, state.ProcessedTotal);
        Assert.Equal(state.ProcessedTotal, state.LedgerTotal);
        Assert.Equal(correctedDown.UnappliedReversalVitality, state.UnappliedReversal);
    }

    [Fact]
    public void Deletion_ClampsToUnspentBalance_CountsUnclawedRemainder_Durably()
    {
        Bootstrap(_temp.Path);
        var session = Session(_temp.Path, T0.AddDays(1));
        session.Continue();
        ActivateEntryWork(session);

        const long creditedTarget = 200L;
        session.IngestActivityBatch(new[] { Record("rec-del", creditedTarget * 100) });
        long balanceBeforeDeletion = session.GetHome().Vitality;

        var deletion = session.IngestActivityBatch(new[]
        {
            Record("rec-del", creditedTarget * 100, revision: 2, isDeletion: true),
        });

        Assert.Equal(1, deletion.DeletionsApplied);
        Assert.Equal(-balanceBeforeDeletion, deletion.VitalityCredited);
        Assert.Equal(creditedTarget - balanceBeforeDeletion, deletion.UnappliedReversalVitality);
        long unapplied = deletion.UnappliedReversalVitality;

        // The counter and net-applied row are durable.
        var rebooted = Session(_temp.Path, T0.AddDays(1).AddMinutes(1));
        rebooted.Continue();
        var snapshot = CanonicalSnapshot.Of(_temp.Path);
        Assert.Equal(unapplied, snapshot.UnappliedReversal);
        Assert.Equal(creditedTarget - balanceBeforeDeletion, snapshot.ProcessedTotal);

        // A later positive correction converges toward earned value against net-applied
        // accounting (D-029 consequence).
        var restore = rebooted.IngestActivityBatch(new[]
        {
            Record("rec-del", creditedTarget * 100, revision: 3),
        });
        Assert.Equal(1, restore.CorrectionsApplied);
        Assert.Equal(snapshot.ProcessedTotal + restore.VitalityCredited,
            CanonicalSnapshot.Of(_temp.Path).ProcessedTotal);
    }

    [Fact]
    public void DuplicateAndUnknownDeletions_AreDeterministicDiagnostics()
    {
        Bootstrap(_temp.Path);
        var session = Session(_temp.Path, T0.AddDays(1));
        session.Continue();

        // Deletion for a logical record never credited → ignored diagnostic.
        var unknown = session.IngestActivityBatch(new[]
        {
            Record("rec-never-seen", 5_000L, isDeletion: true),
        });
        Assert.Equal(1, unknown.DeletionsIgnored);
        Assert.Equal(0, unknown.DeletionsApplied);

        session.IngestActivityBatch(new[] { Record("rec-x", 4_000L) });

        // First deletion at revision 2 applies; the duplicate at the same revision is stale.
        session.IngestActivityBatch(new[] { Record("rec-x", 4_000L, revision: 2, isDeletion: true) });
        var duplicate = session.IngestActivityBatch(new[]
        {
            Record("rec-x", 4_000L, revision: 2, isDeletion: true),
        });
        Assert.Equal(1, duplicate.StaleRevisionsIgnored);
        Assert.Equal(0, duplicate.DeletionsApplied);
    }

    // ------------------------------------------------------------------
    // Scenario engine.
    // ------------------------------------------------------------------

    private enum ScenarioStyle
    {
        ReferenceOrderedBatches,
        OneSingleBatch,
        ReversedOrder,
        EveryRecordDuplicated,
        RestartBetweenEveryRecord,
        JunkMixedIn,
    }

    private CanonicalSnapshot RunScenario(ScenarioStyle style, string? directory = null)
    {
        directory ??= _temp.Path;
        Bootstrap(directory);

        var records = ReferenceRecords().ToList();
        var expectedAccepted = records.Count;

        switch (style)
        {
            case ScenarioStyle.ReferenceOrderedBatches:
            {
                var session = Session(directory, T0.AddDays(10));
                session.Continue();
                foreach (var chunk in Chunk(records, 10))
                    session.IngestActivityBatch(chunk);
                break;
            }
            case ScenarioStyle.OneSingleBatch:
            {
                var session = Session(directory, T0.AddDays(10));
                session.Continue();
                session.IngestActivityBatch(records);
                break;
            }
            case ScenarioStyle.ReversedOrder:
            {
                var session = Session(directory, T0.AddDays(10));
                session.Continue();
                foreach (var chunk in Chunk(records.AsEnumerable().Reverse().ToList(), 7))
                    session.IngestActivityBatch(Enumerable.Reverse(chunk).ToList());
                break;
            }
            case ScenarioStyle.EveryRecordDuplicated:
            {
                var session = Session(directory, T0.AddDays(10));
                session.Continue();
                foreach (var chunk in Chunk(records, 9))
                {
                    var duplicated = chunk.Concat(chunk).ToList();
                    var result = session.IngestActivityBatch(duplicated);
                    Assert.Equal(chunk.Count, result.DuplicatesIgnored);
                }

                break;
            }
            case ScenarioStyle.RestartBetweenEveryRecord:
            {
                int index = 0;
                foreach (var record in records)
                {
                    var session = Session(directory, T0.AddDays(10).AddMinutes(index));
                    session.Continue();
                    var result = session.IngestActivityBatch(new[] { record });
                    Assert.Equal(1, result.Accepted);
                    index++;
                }

                break;
            }
            case ScenarioStyle.JunkMixedIn:
            {
                var session = Session(directory, T0.AddDays(10));
                session.Continue();
                int junkPerBatch = 0;
                foreach (var chunk in Chunk(records, 10))
                {
                    var mixed = new List<NormalizedActivityRecord>(chunk);
                    mixed.AddRange(JunkRecords(junkPerBatch++));
                    var result = session.IngestActivityBatch(mixed);
                    Assert.Equal(chunk.Count, result.Accepted);
                }

                break;
            }
        }

        var final = Session(directory, T0.AddDays(11));
        Assert.Equal(StartStatus.Loaded, final.Continue().Status);

        var snapshot = CanonicalSnapshot.Of(directory);
        if (style == ScenarioStyle.EveryRecordDuplicated || style == ScenarioStyle.RestartBetweenEveryRecord)
            Assert.Equal(expectedAccepted, snapshot.ProcessedCount);

        return snapshot;
    }

    /// <summary>Clean, ordered, valid history: two providers × stable IDs and fingerprint-only
    /// rows, spread across distinct windows.</summary>
    private static IEnumerable<NormalizedActivityRecord> ReferenceRecords()
    {
        var records = new List<NormalizedActivityRecord>();
        for (int i = 0; i < 30; i++)
        {
            DateTimeOffset start = T0.AddHours(i * 7);
            records.Add(Record("stable-" + i, 6_000L + (i % 5) * 1_000L, startUtc: start));
            records.Add(FingerprintRecord("fp-" + i, 3_500L, start.AddMinutes(90)));
        }

        return records;
    }

    private static IEnumerable<NormalizedActivityRecord> JunkRecords(int variant)
    {
        var start = T0.AddHours(variant * 3 + 1);
        return new List<NormalizedActivityRecord>
        {
            Record("junk-negative", -500L, startUtc: start),
            Record("junk-zero", 0L, startUtc: start),
            new NormalizedActivityRecord(Provider, "junk-unit", ActivityCategory.Walking, "meters", 100L, start, start.AddMinutes(30)),
            Record("junk-future", 900L, startUtc: T0.AddHours(24 * 40), endUtc: T0.AddHours(24 * 40).AddMinutes(30)),
            Record("junk-stale", 900L, startUtc: T0.AddDays(-40), endUtc: T0.AddDays(-40).AddMinutes(30)),
            Record("junk-window", 900L, startUtc: start.AddHours(2), endUtc: start), // end <= start
        };
    }

    private static IEnumerable<List<T>> Chunk<T>(IReadOnlyList<T> source, int size)
    {
        for (int i = 0; i < source.Count; i += size)
            yield return source.Skip(i).Take(size).ToList();
    }

    private static NormalizedActivityRecord Record(
        string sourceId, long steps, int revision = 1, bool isDeletion = false,
        DateTimeOffset? startUtc = null, DateTimeOffset? endUtc = null) =>
        new NormalizedActivityRecord(
            Provider,
            sourceId,
            ActivityCategory.Walking,
            ActivityUnits.Steps,
            steps,
            startUtc ?? T0.AddMinutes(-50),
            endUtc ?? (startUtc ?? T0.AddMinutes(-50)).AddMinutes(30),
            revision,
            isDeletion);

    private static NormalizedActivityRecord FingerprintRecord(
        string tag, long steps, DateTimeOffset start)
    {
        // No SourceRecordId → deterministic content fingerprint identity. Distinct
        // second-offset makes each row logically unique.
        return new NormalizedActivityRecord(
            "fingerprint." + tag,
            null,
            ActivityCategory.Walking,
            ActivityUnits.Steps,
            steps,
            start,
            start.AddMinutes(25));
    }

    private CanonicalSnapshot RunSyntheticReference(string directory)
    {
        Bootstrap(directory);
        var source = new SyntheticWalkingSource(20000L);
        for (int day = 1; day <= 8; day++)
        {
            var session = Session(directory, WindowEnd(day));
            Assert.Equal(StartStatus.Loaded, session.Continue().Status);
            session.IngestFromSource(source, WindowStart(day), WindowEnd(day));
        }

        return CanonicalSnapshot.Of(directory);
    }

    private void Bootstrap(string directory)
    {
        var session = TestSessions.Create(directory, new ManualClock(T0));
        Assert.Equal(StartStatus.NewGameCreated, session.StartNewGame(seed: 5UL).Status);
    }

    /// <summary>Turns on auto-advance and queues the entry project so ingested Vitality
    /// flows into restoration work instead of staying banked — making clawback clamping
    /// observable.</summary>
    private static void ActivateEntryWork(GameSession session)
    {
        Assert.True(session.SetAutoAdvance(true).IsSuccess);
        string entryId = TestSessions.EntryProjectId;
        Assert.True(session.EnqueueProject(entryId).IsSuccess);
    }

    private static GameSession Session(string directory, DateTimeOffset now) =>
        TestSessions.Create(directory, new ManualClock(now));

    private static DateTimeOffset WindowStart(int day) => T0.AddDays(day - 1);

    private static DateTimeOffset WindowEnd(int day) => T0.AddDays(day);

    /// <summary>Order-independent projection of every durable economic surface.</summary>
    internal readonly struct CanonicalSnapshot : IEquatable<CanonicalSnapshot>
    {
        public long Balance { get; }
        public long LedgerTotal { get; }
        public int LedgerCount { get; }
        public int ProcessedCount { get; }
        public long ProcessedTotal { get; }
        public long UnappliedReversal { get; }
        public int CompletedProjects { get; }
        public long LandmarkStageSum { get; }
        public long ProducerTotalMilli { get; }
        public int Discoveries { get; }
        public int ExpeditionsCompleted { get; }
        public int EcologyStage { get; }
        public int SettlementStage { get; }
        public bool RegionCompleted { get; }
        public DateTimeOffset CheckpointUtc { get; }

        public CanonicalSnapshot(
            long balance, long ledgerTotal, int ledgerCount, int processedCount,
            long processedTotal, long unappliedReversal, int completedProjects,
            long landmarkStageSum, long producerTotalMilli, int discoveries,
            int expeditionsCompleted, int ecologyStage, int settlementStage,
            bool regionCompleted, DateTimeOffset checkpointUtc)
        {
            Balance = balance;
            LedgerTotal = ledgerTotal;
            LedgerCount = ledgerCount;
            ProcessedCount = processedCount;
            ProcessedTotal = processedTotal;
            UnappliedReversal = unappliedReversal;
            CompletedProjects = completedProjects;
            LandmarkStageSum = landmarkStageSum;
            ProducerTotalMilli = producerTotalMilli;
            Discoveries = discoveries;
            ExpeditionsCompleted = expeditionsCompleted;
            EcologyStage = ecologyStage;
            SettlementStage = settlementStage;
            RegionCompleted = regionCompleted;
            CheckpointUtc = checkpointUtc;
        }

        public static CanonicalSnapshot Of(string directory)
        {
            var decoded = TestSessions.NewCodec().Decode(File.ReadAllBytes(Path.Combine(directory, "save.json")));
            Assert.Equal(CodecStatus.Ok, decoded.Status);
            var s = decoded.State!;
            var content = Region1Catalog.Create();

            int completed = 0;
            foreach (var pair in s.Region.Projects)
                if (pair.Value.Status == WalkGame.Domain.Projects.ProjectStatus.Completed)
                    completed++;

            long stageSum = 0;
            foreach (var pair in s.Region.LandmarkStages)
                stageSum += (long)pair.Value;

            long producerMilli = 0;
            int expeditionsCompleted = 0;
            foreach (var producer in s.Region.Producers)
                producerMilli += producer.TotalProducedMilliUnits;
            foreach (var expedition in s.Region.Expeditions.Values)
                if (expedition.CompletedAtUtc != null)
                    expeditionsCompleted++;

            Assert.Empty(WalkGame.Domain.Validation.GameStateValidator.Validate(s, content));

            return new CanonicalSnapshot(
                s.Resources.Get(ResourceType.Vitality),
                s.Ledger.TotalVitalityCredited,
                s.Ledger.Records.Count,
                s.ProcessedRecords.Count,
                s.ProcessedRecords.TotalVitalityCredited,
                s.ProcessedRecords.UnappliedReversalVitality,
                completed,
                stageSum,
                producerMilli,
                s.Region.Discoveries.Count,
                expeditionsCompleted,
                s.Region.EcologyStage,
                s.Region.SettlementStage,
                s.Region.IsCompleted,
                s.IngestionCheckpointUtc);
        }

        public bool Equals(CanonicalSnapshot other) =>
            Balance == other.Balance &&
            LedgerTotal == other.LedgerTotal &&
            LedgerCount == other.LedgerCount &&
            ProcessedCount == other.ProcessedCount &&
            ProcessedTotal == other.ProcessedTotal &&
            UnappliedReversal == other.UnappliedReversal &&
            CompletedProjects == other.CompletedProjects &&
            LandmarkStageSum == other.LandmarkStageSum &&
            ProducerTotalMilli == other.ProducerTotalMilli &&
            Discoveries == other.Discoveries &&
            ExpeditionsCompleted == other.ExpeditionsCompleted &&
            EcologyStage == other.EcologyStage &&
            SettlementStage == other.SettlementStage &&
            RegionCompleted == other.RegionCompleted &&
            CheckpointUtc == other.CheckpointUtc;

        public override bool Equals(object? obj) => obj is CanonicalSnapshot other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(
            Balance, LedgerTotal, ProcessedCount, ProcessedTotal, CompletedProjects, CheckpointUtc);

        public override string ToString() =>
            $"balance={Balance} ledger={LedgerTotal}/{LedgerCount} processed={ProcessedTotal}/{ProcessedCount} "
            + $"unapplied={UnappliedReversal} projects={CompletedProjects} stages={LandmarkStageSum} "
            + $"producerMilli={ProducerTotalMilli} discoveries={Discoveries} expeditions={ExpeditionsCompleted} "
            + $"ecology={EcologyStage} settlement={SettlementStage} completed={RegionCompleted} ckpt={CheckpointUtc:O}";
    }
}
