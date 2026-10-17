using System;
using System.IO;
using System.Linq;
using WalkGame.Application.Content;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-178: a source that revises one record down and back up while the player has no unspent
/// Vitality must not mint credit. Before the fix every down/up cycle credited +1 forever,
/// because the clamped clawback left the row holding more Vitality than its steps and the
/// correction pool formula read that excess as carried steps.
/// </summary>
public sealed class CorrectionOscillationTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 3, 10, 8, 0, 0, TimeSpan.Zero);
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    [Fact]
    public void DownUpCycles_WhileBalanceIsSpent_AreBoundedAndStopMinting()
    {
        var s = StartSpending();
        Assert.Equal(1L, s.IngestActivityBatch(new[] { Rec("a", 150, 1) }).VitalityCredited);
        long lifetime = 0L;
        for (int cycle = 0; cycle < 10; cycle++)
        {
            s.IngestActivityBatch(new[] { Rec("a", 50, 2 + cycle * 2) });   // clawback clamped: nothing unspent
            s.IngestActivityBatch(new[] { Rec("a", 150, 3 + cycle * 2) });  // back up
            lifetime = State().ProcessedRecords.TotalVitalityCredited;
        }
        // Honest value of 150 steps is 1; the tolerated excess is at most one Vitality for the record.
        Assert.InRange(lifetime, 1L, 2L);
        AssertValid();
    }

    [Fact]
    public void DeleteAndReAdd_WhileBalanceIsSpent_AreBoundedAndStopMinting()
    {
        var s = StartSpending();
        s.IngestActivityBatch(new[] { Rec("a", 150, 1) });
        for (int cycle = 0; cycle < 10; cycle++)
        {
            s.IngestActivityBatch(new[] { Rec("a", 150, 2 + cycle * 2, deletion: true) });
            s.IngestActivityBatch(new[] { Rec("a", 150, 3 + cycle * 2) });
        }
        Assert.InRange(State().ProcessedRecords.TotalVitalityCredited, 1L, 2L);
        AssertValid();
    }

    [Fact]
    public void LargeRecordOscillation_NeverExceedsWhatItsStepsCanJustify()
    {
        var s = StartSpending();
        s.IngestActivityBatch(new[] { Rec("a", 10_000, 1) });
        for (int cycle = 0; cycle < 5; cycle++)
        {
            s.IngestActivityBatch(new[] { Rec("a", 50, 2 + cycle * 2) });
            s.IngestActivityBatch(new[] { Rec("a", 10_000, 3 + cycle * 2) });
        }
        Assert.InRange(State().ProcessedRecords.TotalVitalityCredited, 100L, 100L + 1L);
        AssertValid();
    }

    [Fact]
    public void WithUnspentBalance_CyclesAreExact()
    {
        var s = TestSessions.Create(_temp, new ManualClock(T0));
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(seed: 7UL).Status);
        s.IngestActivityBatch(new[] { Rec("a", 150, 1) });
        for (int cycle = 0; cycle < 5; cycle++)
        {
            s.IngestActivityBatch(new[] { Rec("a", 50, 2 + cycle * 2) });
            s.IngestActivityBatch(new[] { Rec("a", 150, 3 + cycle * 2) });
        }
        var st = State();
        Assert.Equal(1L, st.ProcessedRecords.TotalVitalityCredited);
        Assert.Equal(1L, st.Resources.Get(ResourceType.Vitality));
        Assert.Equal(50L, st.ProcessedRecords.GetCarry("p"));
        Assert.Equal(0L, st.ProcessedRecords.UnappliedReversalVitality);
        AssertValid();
    }

    [Fact]
    public void ReplayingTheWholeHistory_AfterOscillation_IsANoOp()
    {
        var s = StartSpending();
        var history = new[] { Rec("a", 150, 1), Rec("a", 50, 2), Rec("a", 150, 3) };
        foreach (var r in history) s.IngestActivityBatch(new[] { r });
        long before = State().ProcessedRecords.TotalVitalityCredited;
        var replay = s.IngestActivityBatch(history);
        Assert.Equal(0L, replay.VitalityCredited);
        Assert.Equal(before, State().ProcessedRecords.TotalVitalityCredited);
    }

    /// <summary>
    /// Property (150 seeded histories of 1–4 records, 30 random revisions each, deletions
    /// included, everything spent at once): a record never holds more lifetime credit than its
    /// largest revision can justify, floor((maxSteps + MaxCarry) / 100). It fails without the D-178 clamp.
    /// </summary>
    [Fact]
    public void RandomRevisionHistories_NeverCreditMoreThanTheLargestRevisionsJustify()
    {
        for (int seed = 0; seed < 150; seed++)
        {
            using var temp = new TempDirectory();
            var rng = new Random(seed);
            var s = TestSessions.Create(temp, new ManualClock(T0));
            Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(seed: 7UL).Status);
            Assert.True(s.EnqueueProject("proj.clear-trailhead").IsSuccess);
            int n = 1 + rng.Next(4);
            var rev = new int[n];
            var maxSteps = new long[n];
            for (int op = 0; op < 30; op++)
            {
                int i = rng.Next(n);
                bool del = rev[i] > 0 && rng.Next(5) == 0;
                long q = del ? 0 : rng.Next(1, 400);
                rev[i]++;
                maxSteps[i] = Math.Max(maxSteps[i], q);
                s.IngestActivityBatch(new[] { new NormalizedActivityRecord("p", "r" + i, ActivityCategory.Walking,
                    ActivityUnits.Steps, q, T0.AddMinutes(-40 - i), T0.AddMinutes(-20 - i), rev[i], del) });
            }
            var st = TestSessions.NewCodec().Decode(File.ReadAllBytes(Path.Combine(temp.Path, "save.json"))).State!;
            long bound = maxSteps.Sum(m => (m + StepConversionRuleV1.MaxCarry) / StepConversionRuleV1.StepsPerVitality);
            Assert.True(st.ProcessedRecords.TotalVitalityCredited <= bound,
                $"seed {seed}: credited {st.ProcessedRecords.TotalVitalityCredited} > bound {bound}");
            Assert.Empty(GameStateValidator.Validate(st, Region1Catalog.Create()));
        }
    }

    private GameSession StartSpending()
    {
        var s = TestSessions.Create(_temp, new ManualClock(T0));
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(seed: 7UL).Status);
        Assert.True(s.EnqueueProject("proj.clear-trailhead").IsSuccess); // credited Vitality is spent at once
        return s;
    }

    private static NormalizedActivityRecord Rec(string id, long qty, int revision, bool deletion = false) =>
        new NormalizedActivityRecord("p", id, ActivityCategory.Walking, ActivityUnits.Steps, deletion ? 0 : qty,
            T0.AddMinutes(-40), T0.AddMinutes(-20), revision, deletion);

    private void AssertValid()
    {
        var st = State();
        Assert.InRange(st.ProcessedRecords.GetCarry("p"), 0L, StepConversionRuleV1.MaxCarry);
        Assert.Empty(GameStateValidator.Validate(st, Region1Catalog.Create()));
    }

    private GameState State() =>
        TestSessions.NewCodec().Decode(File.ReadAllBytes(Path.Combine(_temp.Path, "save.json"))).State!;
}
