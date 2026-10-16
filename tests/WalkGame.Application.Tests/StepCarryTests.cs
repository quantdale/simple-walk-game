using System;
using System.IO;
using System.Linq;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;

namespace WalkGame.Application.Tests;

/// <summary>D-079 / KI-019: sub-unit step remainders carry per provider.</summary>
public sealed class StepCarryTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 3, 10, 8, 0, 0, TimeSpan.Zero);
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    [Fact]
    public void ManySmallRecords_CreditTheSameAsOneLargeRecord()
    {
        var session = Start();
        var batch = Enumerable.Range(0, 100).Select(i => Rec("p", "r" + i, 150)).ToList();
        var result = session.IngestActivityBatch(batch);
        Assert.Equal(150L, result.VitalityCredited);
    }

    [Fact]
    public void RecordsUnder100Steps_EventuallyCredit_AndCarrySurvivesReload()
    {
        var session = Start();
        Assert.Equal(0L, session.IngestActivityBatch(new[] { Rec("p", "a", 60) }).VitalityCredited);
        var reloaded = Continue();
        Assert.Equal(1L, reloaded.IngestActivityBatch(new[] { Rec("p", "b", 60) }).VitalityCredited);
        Assert.Equal(20L, State().ProcessedRecords.GetCarry("p"));
    }

    [Fact]
    public void CarryIsPerProvider()
    {
        var session = Start();
        var r = session.IngestActivityBatch(new[] { Rec("p", "a", 60), Rec("q", "a", 60) });
        Assert.Equal(0L, r.VitalityCredited);
    }

    [Fact]
    public void Replay_DoesNotDoubleCountCarry()
    {
        var session = Start();
        session.IngestActivityBatch(new[] { Rec("p", "a", 60) });
        session.IngestActivityBatch(new[] { Rec("p", "a", 60) });
        Assert.Equal(60L, State().ProcessedRecords.GetCarry("p"));
    }

    [Fact]
    public void Deletion_NeverCreatesCredit_AndLeavesStateValid()
    {
        var session = Start();
        session.IngestActivityBatch(new[] { Rec("p", "a", 60), Rec("p", "b", 60) }); // 1 credited, carry 20
        var del = session.IngestActivityBatch(new[] { Rec("p", "b", 60, revision: 2, deletion: true) });
        Assert.True(del.VitalityCredited <= 0L);
        var state = State();
        Assert.InRange(state.ProcessedRecords.GetCarry("p"), 0L, 99L);
        Assert.Empty(GameStateValidator.Validate(state, Region1Catalog.Create()));
    }

    [Fact]
    public void Correction_ReattributesFromThePool()
    {
        var session = Start();
        session.IngestActivityBatch(new[] { Rec("p", "a", 150) }); // 1, carry 50
        var corr = session.IngestActivityBatch(new[] { Rec("p", "a", 250, revision: 2) });
        Assert.Equal(1L, corr.VitalityCredited); // 250 -> 2 total, carry 50
        Assert.Equal(50L, State().ProcessedRecords.GetCarry("p"));
    }

    [Fact]
    public void Validator_RejectsOutOfRangeCarry()
    {
        Start().IngestActivityBatch(new[] { Rec("p", "a", 60) });
        var state = State();
        state.ProcessedRecords.StepCarryByProvider["p"] = 100L;
        Assert.Contains(GameStateValidator.Validate(state, Region1Catalog.Create()), v => v.Contains("Step carry"));
    }

    private static NormalizedActivityRecord Rec(string provider, string id, long qty, int revision = 1, bool deletion = false) =>
        new NormalizedActivityRecord(provider, id, ActivityCategory.Walking, ActivityUnits.Steps, qty,
            T0.AddMinutes(-40), T0.AddMinutes(-20), revision, deletion);

    private GameSession Start()
    {
        var s = TestSessions.Create(_temp, new ManualClock(T0));
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(seed: 7UL).Status);
        return s;
    }

    private GameSession Continue()
    {
        var s = TestSessions.Create(_temp, new ManualClock(T0));
        Assert.Equal(StartStatus.Loaded, s.Continue().Status);
        return s;
    }

    private GameState State() =>
        TestSessions.NewCodec().Decode(File.ReadAllBytes(Path.Combine(_temp.Path, "save.json"))).State!;
}
