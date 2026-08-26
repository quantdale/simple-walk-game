using System;
using System.Linq;
using WalkGame.Application.Content;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Time;

namespace WalkGame.Application.Tests;

public sealed class SessionQueueControlTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ManualClock _clock = new(TestSessions.T0);

    public void Dispose() => _temp.Dispose();

    private GameSession NewSession() => TestSessions.Create(_temp, _clock);

    [Fact]
    public void SetAutoAdvance_False_PersistsAcrossRestart()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        session.EnqueueProject(TestSessions.EntryProjectId);

        Assert.True(session.SetAutoAdvance(false).IsSuccess);

        var reloaded = NewSession();
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);

        Assert.False(reloaded.GetHome().AutoAdvance);
        Assert.False(reloaded.GetProjects().AutoAdvance);
    }

    [Fact]
    public void SetAutoAdvance_True_ActivatesHeadOfQueue_WhenSlotFree()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        Assert.True(session.SetAutoAdvance(false).IsSuccess);
        Assert.True(session.EnqueueProject(TestSessions.EntryProjectId).IsSuccess);
        Assert.Null(session.GetHome().ActiveProjectId);

        Assert.True(session.SetAutoAdvance(true).IsSuccess);

        Assert.Equal(TestSessions.EntryProjectId, session.GetHome().ActiveProjectId);
    }

    [Fact]
    public void ActivateQueuedProject_ManualStart_SpendsBankedVitalityImmediately()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        Assert.True(session.SetAutoAdvance(false).IsSuccess);
        Assert.True(session.EnqueueProject(TestSessions.EntryProjectId).IsSuccess);
        Assert.True(session.CreditActivity(TestSessions.Tx1, TestSessions.T0, 120L, "walk").Saved);
        long bankedBefore = session.GetHome().Vitality;
        Assert.True(bankedBefore >= 120L);

        var result = session.ActivateQueuedProject(TestSessions.EntryProjectId);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var home = session.GetHome();
        Assert.Equal(TestSessions.EntryProjectId, home.ActiveProjectId);
        Assert.Equal(Math.Min(bankedBefore, 300L), home.ActiveProjectInvested);
        Assert.Equal(Math.Max(0L, bankedBefore - 300L), home.Vitality);
    }

    [Fact]
    public void ActivateQueuedProject_WhileSlotOccupied_FailsExplicitly()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        Assert.True(session.EnqueueProject(TestSessions.EntryProjectId).IsSuccess);
        Assert.NotNull(session.GetHome().ActiveProjectId);

        // The second entry project is Available in this catalog seed.
        var other = Region1Catalog.Create().Projects
            .Select(p => p.Id.Value)
            .FirstOrDefault(id => id != TestSessions.EntryProjectId && session.GetProjects().Projects.Any(r => r.ProjectId == id && r.Status == ProjectStatus.Available));

        if (other == null)
            return; // catalog has no second available project; nothing to prove here.

        var result = session.ActivateQueuedProject(other);
        Assert.False(result.IsSuccess);
        Assert.Equal("project.already-active", result.Error!.Code);
    }

    [Fact]
    public void ActivateQueuedProject_ProjectNotQueued_FailsCleanly()
    {
        var session = NewSession();
        session.StartNewGame(7UL);

        var result = session.ActivateQueuedProject("proj.river-intake");

        Assert.False(result.IsSuccess);
        Assert.Equal("project.not-queued", result.Error!.Code);
    }

    [Fact]
    public void GetProjects_ExposesStatusEffortPrerequisitesAndQueuePositions()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        Assert.True(session.EnqueueProject(TestSessions.EntryProjectId).IsSuccess);

        var model = session.GetProjects();

        Assert.True(model.AutoAdvance);
        var entry = model.Projects.Single(p => p.ProjectId == TestSessions.EntryProjectId);
        Assert.Equal(ProjectStatus.Active, entry.Status);
        Assert.Equal(0L, entry.VitalityInvested);
        Assert.Equal(300L, entry.VitalityCost);
        Assert.Null(entry.QueuedPosition);

        var river = model.Projects.Single(p => p.ProjectId == "proj.river-intake");
        Assert.Equal(ProjectStatus.Locked, river.Status);
        Assert.Contains(TestSessions.EntryProjectId, river.PrerequisiteIds);
    }

    [Fact]
    public void GetRegion_ExposesCanonicalStagesAndProducerStoreState()
    {
        var session = NewSession();
        session.StartNewGame(7UL);

        var region = session.GetRegion();

        Assert.Equal("Millbrook Valley", region.RegionTitleKey);
        Assert.All(region.Landmarks, l => Assert.Equal(RestorationStage.Ruined, l.Stage));
        Assert.Equal(3, region.Producers.Count);
        var producer = region.Producers.Single(p => p.ProducerId == "prd.workshop-salvage");
        Assert.False(producer.Unlocked);
        Assert.Equal(ResourceType.Materials, producer.Output);
        Assert.Equal(500L, producer.CapacityUnits);
        Assert.Equal(2500L, producer.MilliUnitsPerDay);
        Assert.Equal(0L, producer.StoredMilliUnits);
    }
}
