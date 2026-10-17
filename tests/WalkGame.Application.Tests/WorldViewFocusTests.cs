using System;
using System.Linq;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.ReadModels;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Time;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>
/// KI-027(d): <see cref="WorldViewReadModel.LocationRow.FocusProjectId"/> names the project a place is
/// "about" right now (bible §15–§16: Inspect / Help restore). Pure projection; reading it changes nothing.
/// </summary>
public sealed class WorldViewFocusTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private GameSession Fresh(ManualClock clock)
    {
        var s = TestSessions.Create(_temp.Path, clock);
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(42UL).Status);
        return s;
    }

    [Fact]
    public void FreshValley_TrailheadFocusesItsFirstGoal_LockedPlacesFocusNothing()
    {
        var view = Fresh(new ManualClock(T0)).GetWorldView();
        Assert.Equal("proj.clear-trailhead", view.Find("lm.trailhead")!.FocusProjectId);
        Assert.All(view.Locations.Where(l => !l.IsUnlocked), l => Assert.Null(l.FocusProjectId));
    }

    [Fact]
    public void Trailhead_FocusMovesFromClearing_ToTheBridge_AndStaysOnTheActiveProject()
    {
        var clock = new ManualClock(T0);
        var s = Fresh(clock);
        Assert.True(s.EnqueueProject("proj.clear-trailhead").IsSuccess);
        for (int d = 0; d < 30 && s.GetProjects().Projects.Single(p => p.ProjectId == "proj.clear-trailhead").Status != ProjectStatus.Completed; d++)
        {
            var from = clock.UtcNow; clock.Advance(TimeSpan.FromDays(1));
            s.IngestFromSource(new SyntheticWalkingSource(9000), from, clock.UtcNow);
        }
        var trail = s.GetWorldView().Find("lm.trailhead")!;
        Assert.Equal("proj.rebuild-trail-bridges", trail.FocusProjectId); // bible §16: Mira by the damaged bridge

        Assert.True(s.EnqueueProject("proj.rebuild-trail-bridges").IsSuccess);
        var active = s.GetWorldView().Find("lm.trailhead")!;
        if (active.ActiveProjectId != null) Assert.Equal(active.ActiveProjectId, active.FocusProjectId);
    }

    [Fact]
    public void Over_a_whole_playthrough_focus_is_always_a_project_of_that_place_and_prefers_the_active_one()
    {
        var content = Region1Catalog.Create();
        var clock = new ManualClock(T0);
        var s = Fresh(clock);
        for (int day = 0; day < 140; day++)
        {
            if (s.GetHome().ActiveProjectId == null)
                foreach (var p in content.Projects) if (s.EnqueueProject(p.Id.Value).IsSuccess) break;
            var from = clock.UtcNow; clock.Advance(TimeSpan.FromDays(1));
            s.IngestFromSource(new SyntheticWalkingSource(15000), from, clock.UtcNow);
            if (s.GetPendingReturnSummary() != null) s.AcknowledgeReturnSummary();

            var before = s.ExportSaveEnvelope();
            foreach (var row in s.GetWorldView().Locations)
            {
                if (row.FocusProjectId == null) { Assert.Empty(row.SelectableProjectIds); Assert.Null(row.ActiveProjectId); continue; }
                Assert.Contains(row.FocusProjectId, row.ProjectIds);
                if (row.ActiveProjectId != null) Assert.Equal(row.ActiveProjectId, row.FocusProjectId);
                else if (row.SelectableProjectIds.Count > 0) Assert.Equal(row.SelectableProjectIds[0], row.FocusProjectId);
                else Assert.Equal(ProjectStatus.Completed, s.GetProjects().Projects.Single(p => p.ProjectId == row.FocusProjectId).Status);
            }
            Assert.Equal(before, s.ExportSaveEnvelope());
        }
    }
}
