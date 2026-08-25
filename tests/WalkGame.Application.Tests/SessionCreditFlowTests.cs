using System;
using System.IO;
using WalkGame.Application.Persistence;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Time;

namespace WalkGame.Application.Tests;

public sealed class SessionCreditFlowTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void CreditEnqueueCompleteFlow_IsExactlyOnce()
    {
        var clock = new ManualClock(TestSessions.T0);
        var session = TestSessions.Create(_temp, clock);

        session.StartNewGame(seed: 7UL);

        var first = session.CreditActivity(TestSessions.Tx1, TestSessions.T0, 250L, "walk");
        Assert.False(first.DuplicateIgnored);
        Assert.True(first.Saved);

        var enqueue = session.EnqueueProject(TestSessions.EntryProjectId);
        Assert.True(enqueue.IsSuccess, enqueue.Error?.Message);

        var active = session.GetHome();
        Assert.Equal(TestSessions.EntryProjectId, active.ActiveProjectId);
        Assert.Equal(250L, active.ActiveProjectInvested);
        Assert.Equal(300L, active.ActiveProjectCost);
        Assert.Equal(0L, active.Vitality);

        var completionTime = TestSessions.T0.AddMinutes(5);
        var second = session.CreditActivity(TestSessions.Tx2, completionTime, 100L, "run");
        Assert.False(second.DuplicateIgnored);

        var completed = session.GetHome();
        Assert.Equal(1, completed.CompletedProjects);
        Assert.Null(completed.ActiveProjectId);
        Assert.Equal(50L, completed.Vitality);

        var persisted = DecodePersisted();
        Assert.Equal(CodecStatus.Ok, persisted.Status);
        var trailhead = persisted.State!.Region.FindProject(TestSessions.EntryProjectId)!;
        Assert.Equal(ProjectStatus.Completed, trailhead.Status);
        Assert.Equal(TestSessions.T0, trailhead.CompletedAtUtc);
        Assert.Equal(2, persisted.State.Ledger.Records.Count);

        var replay = session.CreditActivity(TestSessions.Tx2, completionTime, 100L, "run");
        Assert.True(replay.DuplicateIgnored);
        Assert.Equal(50L, session.GetHome().Vitality);

        var afterReplay = DecodePersisted();
        Assert.Equal(CodecStatus.Ok, afterReplay.Status);
        Assert.Equal(2, afterReplay.State!.Ledger.Records.Count);
    }

    [Fact]
    public void ExactlyOnce_Dedup_SurvivesRestartBetweenCredits()
    {
        var clock = new ManualClock(TestSessions.T0);
        var firstSession = TestSessions.Create(_temp, clock);
        firstSession.StartNewGame(seed: 7UL);
        firstSession.CreditActivity(TestSessions.Tx1, TestSessions.T0, 250L, "walk");

        var reloaded = TestSessions.Create(_temp, clock);
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);

        var replay = reloaded.CreditActivity(TestSessions.Tx1, TestSessions.T0, 250L, "walk");
        Assert.True(replay.DuplicateIgnored);

        var fresh = reloaded.CreditActivity(TestSessions.Tx2, TestSessions.T0.AddMinutes(1), 40L, "run");
        Assert.False(fresh.DuplicateIgnored);

        var persisted = DecodePersisted();
        Assert.Equal(CodecStatus.Ok, persisted.Status);
        Assert.Equal(2, persisted.State!.Ledger.Records.Count);
        Assert.Equal(290L, persisted.State.Resources.Get(ResourceType.Vitality));
    }

    private DecodeResult DecodePersisted() =>
        TestSessions.NewCodec().Decode(File.ReadAllBytes(_temp.FilePath("save.json")));
}
