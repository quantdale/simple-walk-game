using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalkGame.Application.Persistence;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;
using RewardTransactionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RewardTransactionIdKind>;

namespace WalkGame.Application.Tests;

/// <summary>
/// F-07 / D-066: bounded ledger retirement keeps exactly-once intact. Every scenario drives
/// the real pipeline and re-reads the durable save, so "retired" is proven at rest.
/// </summary>
public sealed class LedgerRetirementTests : IDisposable
{
    private const string Provider = "retire.provider";
    private static readonly DateTimeOffset T0 = TestSessions.T0;
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private GameSession Session(DateTimeOffset now) => TestSessions.Create(_temp, new ManualClock(now));

    private static NormalizedActivityRecord Day(int day, long steps = 10_000L, int revision = 1, bool deletion = false)
    {
        var start = T0.AddDays(day);
        return new NormalizedActivityRecord(Provider, "day-" + day, ActivityCategory.Walking,
            ActivityUnits.Steps, deletion ? 0L : steps, start, start.AddHours(23), revision, deletion);
    }

    private GameState Decode()
    {
        var decoded = TestSessions.NewCodec().Decode(File.ReadAllBytes(_temp.FilePath("save.json")));
        Assert.Equal(CodecStatus.Ok, decoded.Status);
        return decoded.State!;
    }

    /// <summary>Ingests one record per day for <paramref name="days"/> days, one session per day.</summary>
    private void DriveDays(int days)
    {
        Assert.Equal(StartStatus.NewGameCreated, Session(T0).StartNewGame(7UL).Status);
        for (int day = 1; day <= days; day++)
        {
            var s = Session(T0.AddDays(day + 1));
            Assert.Equal(StartStatus.Loaded, s.Continue().Status);
            s.IngestActivityBatch(new[] { Day(day) });
        }
    }

    [Fact]
    public void LongHorizon_RetainedHistoryConvergesToTheWindow_LifetimeTotalsIntact()
    {
        DriveDays(120);
        var state = Decode();
        // Retention bound: rows ending within TrustedWindowFloor (45d) of trusted progress.
        Assert.InRange(state.ProcessedRecords.Count, 40, 47);
        Assert.Equal(120 - state.ProcessedRecords.Count, state.ProcessedRecords.RetiredCount);
        Assert.Equal(120 * 100L, state.ProcessedRecords.TotalVitalityCredited);
        Assert.Equal(120 * 100L, state.Ledger.TotalVitalityCredited);
        Assert.True(state.Ledger.RetiredCount > 0);
        Assert.Equal(state.ProcessedRecords.RecomputeLifetimeVitality(), state.ProcessedRecords.MaintainedVitalityCredited);
        Assert.Equal(state.Ledger.RecomputeLifetimeVitality(), state.Ledger.MaintainedVitalityCredited);
        Assert.Empty(GameStateValidator.Validate(state, Session(T0.AddDays(121)).Content));
    }

    [Fact]
    public void RetiredRecordRedelivery_CreditsNothing_AndIsClassifiedAsRetired()
    {
        DriveDays(100);
        Assert.True(Decode().ProcessedRecords.IsRetired(ActivityIdentity.Compute(Day(1))));
        var s = Session(T0.AddDays(102));
        Assert.Equal(StartStatus.Loaded, s.Continue().Status);
        long before = Decode().Ledger.TotalVitalityCredited;

        var result = s.IngestActivityBatch(new[] { Day(1), Day(2) });

        Assert.Equal(0L, result.VitalityCredited);
        Assert.Equal(0, result.Accepted);
        Assert.Equal(0, result.Rejected); // not a refused activity
        var after = Decode();
        Assert.Equal(2, after.LastIngestionOutcome!.RetiredRecordsIgnored);
        Assert.Equal(2, after.LastIngestionOutcome.DuplicatesIgnored);
        Assert.Equal(before, after.Ledger.TotalVitalityCredited);
    }

    [Fact]
    public void CorrectionOrDeletionForRetiredRecord_NoCredit_NoDestructiveReversal()
    {
        DriveDays(100);
        var s = Session(T0.AddDays(102));
        Assert.Equal(StartStatus.Loaded, s.Continue().Status);
        var before = Decode();

        var result = s.IngestActivityBatch(new[] { Day(1, 50_000L, revision: 2), Day(2, revision: 2, deletion: true) });

        Assert.Equal(0L, result.VitalityCredited);
        var after = Decode();
        Assert.Equal(2, after.LastIngestionOutcome!.RetiredRecordsIgnored);
        Assert.Equal(0, after.LastIngestionOutcome.CorrectionsApplied);
        Assert.Equal(0, after.LastIngestionOutcome.DeletionsApplied);
        Assert.Equal(before.Ledger.TotalVitalityCredited, after.Ledger.TotalVitalityCredited);
        Assert.Equal(before.ProcessedRecords.UnappliedReversalVitality, after.ProcessedRecords.UnappliedReversalVitality);
    }

    [Fact]
    public void InWindowRetiredRecord_IsStillNonCreditable()
    {
        // Retire a record that is still inside the acceptance window (defence in depth: the
        // identity set, not the window, is what guarantees exactly-once).
        DriveDays(5);
        var state = Decode();
        Assert.Equal(5, state.ProcessedRecords.Retire(DateTimeOffset.MaxValue, T0.AddDays(6), 256));
        File.WriteAllBytes(_temp.FilePath("save.json"), TestSessions.NewCodec().Encode(state, T0.AddDays(6)));

        var s = Session(T0.AddDays(6).AddHours(1));
        Assert.Equal(StartStatus.Loaded, s.Continue().Status);
        var result = s.IngestActivityBatch(new[] { Day(5), Day(4, 90_000L, revision: 3) });

        Assert.Equal(0L, result.VitalityCredited);
        Assert.Equal(2, Decode().LastIngestionOutcome!.RetiredRecordsIgnored);
    }

    [Fact]
    public void Retirement_ChangesNoBalance_NoInvestedProgress_NoLifetimeTotal()
    {
        DriveDays(10);
        var state = Decode();
        long vitality = state.Resources.Get(ResourceType.Vitality);
        var invested = state.Region.Projects.ToDictionary(p => p.Key, p => p.Value.VitalityInvested);
        long processedTotal = state.ProcessedRecords.TotalVitalityCredited;
        long ledgerTotal = state.Ledger.TotalVitalityCredited;

        Assert.Equal(10, state.ProcessedRecords.Retire(DateTimeOffset.MaxValue, T0.AddDays(11), 256));
        Assert.True(state.Ledger.RetireActivityRows(DateTimeOffset.MaxValue, 256) > 0);

        Assert.Equal(vitality, state.Resources.Get(ResourceType.Vitality));
        Assert.Equal(invested, state.Region.Projects.ToDictionary(p => p.Key, p => p.Value.VitalityInvested));
        Assert.Equal(processedTotal, state.ProcessedRecords.TotalVitalityCredited);
        Assert.Equal(ledgerTotal, state.Ledger.TotalVitalityCredited);
        Assert.Equal(processedTotal, state.ProcessedRecords.RecomputeLifetimeVitality());
        Assert.Equal(ledgerTotal, state.Ledger.RecomputeLifetimeVitality());
    }

    [Fact]
    public void RetirementBookkeeping_IsDurable_AcrossRestart()
    {
        DriveDays(70);
        var first = Decode();
        Assert.True(first.ProcessedRecords.RetiredCount > 0);
        Assert.NotNull(first.ProcessedRecords.LastRetiredAtUtc);

        var restarted = Session(T0.AddDays(72));
        Assert.Equal(StartStatus.Loaded, restarted.Continue().Status);
        var again = Decode();
        Assert.Equal(first.ProcessedRecords.RetiredCount, again.ProcessedRecords.RetiredCount);
        Assert.Equal(first.ProcessedRecords.RetiredIdentities, again.ProcessedRecords.RetiredIdentities);
        Assert.Equal(first.ProcessedRecords.LastRetiredAtUtc, again.ProcessedRecords.LastRetiredAtUtc);
        Assert.Equal(first.ProcessedRecords.RetiredVitalityCredited, again.ProcessedRecords.RetiredVitalityCredited);
    }

    [Fact]
    public void SaveWithoutRetirementBookkeeping_DecodesAsNothingRetired_AndValidates()
    {
        DriveDays(3);
        var payload = PayloadSurgery.OpenPayload(File.ReadAllBytes(_temp.FilePath("save.json")));
        var processed = payload["processedRecords"]!.AsObject();
        foreach (var key in new[] { "retiredIdentities", "retiredCount", "lastRetiredAtUtc", "retiredVitalityCredited", "maintainedVitalityCredited" })
            processed.Remove(key);
        var ledger = payload["ledger"]!.AsObject();
        foreach (var key in new[] { "retiredCount", "retiredVitalityCredited", "maintainedVitalityCredited" })
            ledger.Remove(key);
        foreach (var entry in processed["entries"]!.AsObject())
            entry.Value!.AsObject().Remove("recordEndUtc");

        var decoded = TestSessions.NewCodec().Decode(PayloadSurgery.WrapPayload(payload, SchemaVersions.Current, T0.AddDays(4)));

        Assert.Equal(CodecStatus.Ok, decoded.Status);
        var state = decoded.State!;
        Assert.Equal(0L, state.ProcessedRecords.RetiredCount);
        Assert.Empty(state.ProcessedRecords.RetiredIdentities);
        Assert.Null(state.ProcessedRecords.MaintainedVitalityCredited);
        Assert.Equal(300L, state.ProcessedRecords.TotalVitalityCredited);
        Assert.Equal(300L, state.Ledger.TotalVitalityCredited);
        Assert.Empty(GameStateValidator.Validate(state, Session(T0.AddDays(4)).Content));
    }

    [Fact]
    public void TamperedMaintainedAggregate_IsAViolationNamingTheAggregate()
    {
        DriveDays(3);
        var payload = PayloadSurgery.OpenPayload(File.ReadAllBytes(_temp.FilePath("save.json")));
        payload["processedRecords"]!["maintainedVitalityCredited"] = 99_999L;
        payload["ledger"]!["maintainedVitalityCredited"] = 12_345L;
        var decoded = TestSessions.NewCodec().Decode(PayloadSurgery.WrapPayload(payload, SchemaVersions.Current, T0.AddDays(4)));
        Assert.Equal(CodecStatus.Ok, decoded.Status);

        var violations = GameStateValidator.Validate(decoded.State!, Session(T0.AddDays(4)).Content);
        Assert.Contains(violations, v => v.Contains("ProcessedRecords.MaintainedVitalityCredited"));
        Assert.Contains(violations, v => v.Contains("Ledger.MaintainedVitalityCredited"));
    }

    [Fact]
    public void RewardLedgerIndex_StaysWarmAcrossCommitsAndRetirement()
    {
        var ledger = new RewardLedgerState();
        var balances = new ResourceBalances();
        for (int i = 0; i < 5_000; i++)
            ledger.Apply(new RewardTransaction(RewardTransactionId.FromGuid(Guid.NewGuid()), T0.AddMinutes(i), 1L, "ingest:x"), balances);
        Assert.Equal(1, ledger.IndexBuildCount);
        Assert.Equal(256, ledger.RetireActivityRows(DateTimeOffset.MaxValue, 256));
        var known = RewardTransactionId.FromGuid(new Guid(ledger.Records[0].TransactionId));
        Assert.True(ledger.HasTransaction(known));
        Assert.Equal(1, ledger.IndexBuildCount); // no rebuild: the index is mutated alongside the rows
        Assert.Equal(5_000L, ledger.TotalVitalityCredited);
    }

    [Fact]
    public void FirstDuplicateCheckAfterLoad_ScansOnlyRetainedRows_IndependentOfLifetimeHistory()
    {
        DriveDays(200);
        var loaded = Decode();
        Assert.Equal(0, loaded.Ledger.IndexBuildCount);
        string anyId = loaded.Ledger.Records[0].TransactionId;

        Assert.True(loaded.Ledger.HasTransaction(RewardTransactionId.FromGuid(new Guid(anyId))));

        Assert.Equal(1, loaded.Ledger.IndexBuildCount);
        // 200 days of lifetime history, but the first check touches only the bounded
        // retained window (D-066): cost is a function of the window, not of play time.
        Assert.InRange(loaded.Ledger.LastIndexBuildSize, 1, 50);
        Assert.True(loaded.Ledger.RetiredCount >= 150);
    }
}
