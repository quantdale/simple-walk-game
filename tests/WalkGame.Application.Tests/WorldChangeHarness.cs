using System;
using System.Linq;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Time;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>D-167 test harness: one live session, one walk per day, idle queue fills in content order.</summary>
internal sealed class WorldChangeHarness
{
    public static readonly DateTimeOffset T0 = new(2026, 2, 2, 0, 0, 0, TimeSpan.Zero);
    public ManualClock Clock { get; } = new(T0);
    public GameSession Session { get; }
    private readonly long _steps;

    public WorldChangeHarness(string dir, long stepsPerDay)
    {
        _steps = stepsPerDay;
        Session = TestSessions.Create(dir, Clock);
        Assert.Equal(StartStatus.NewGameCreated, Session.StartNewGame(42UL).Status);
        FillQueue();
    }

    public void PlayDay(int day)
    {
        var end = T0.AddDays(day);
        Clock.Set(end);
        Session.IngestFromSource(new SyntheticWalkingSource(_steps), end.AddDays(-1), end);
        if (Session.GetPendingReturnSummary() != null) Session.AcknowledgeReturnSummary();
        FillQueue();
    }

    private void FillQueue()
    {
        var home = Session.GetHome();
        if (home.ActiveProjectId != null || home.Queued.Count > 0) return;
        foreach (var id in Region1Catalog.Create().Projects.Select(p => p.Id.Value))
            if (Session.EnqueueProject(id).IsSuccess) break;
    }
}
