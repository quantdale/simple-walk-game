using System;
using System.Linq;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Time;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-155: the Story and Discoveries read models carry the canonical "what brings this" rule ids
/// (chapter RequiredProjectIds / MinCareCycles, discovery UnlockedByProjectId) straight from the
/// catalog, so the shell's "what this leads to" lines are derived, never re-authored. Ids only:
/// a locked chapter still exposes no body.
/// </summary>
public sealed class LeadsToReadModelTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private GameSession Fresh()
    {
        var s = TestSessions.Create(_temp.Path, new ManualClock(T0));
        s.StartNewGame(42UL);
        return s;
    }

    [Fact]
    public void Chapter_rows_carry_the_catalog_requirements_exactly()
    {
        var c = Region1Catalog.Create();
        var story = Fresh().GetStory();
        Assert.Equal(c.Chapters.Count, story.Chapters.Count);
        for (int i = 0; i < c.Chapters.Count; i++)
        {
            Assert.Equal(c.Chapters[i].RequiredProjectIds, story.Chapters[i].RequiredProjectIds);
            Assert.Equal(c.Chapters[i].MinCareCycles, story.Chapters[i].MinCareCycles);
        }
        Assert.All(story.Chapters.Where(r => !r.Unlocked), r => Assert.Null(r.BodyKey));
    }

    [Fact]
    public void Discovery_rows_carry_the_unlocking_project_exactly()
    {
        var c = Region1Catalog.Create();
        var d = Fresh().GetDiscoveries();
        Assert.Equal(c.Discoveries.Select(x => x.UnlockedByProjectId), d.Discoveries.Select(x => x.UnlockedByProjectId));
        Assert.All(d.Discoveries, r => Assert.False(string.IsNullOrEmpty(r.UnlockedByProjectId)));
    }

    [Fact]
    public void Completing_the_named_project_turns_up_exactly_the_discoveries_that_named_it()
    {
        var content = Region1Catalog.Create();
        var before = Fresh().GetDiscoveries();
        GameSession s = null!;
        for (int day = 1; day <= 30; day++)
        {
            s = TestSessions.Create(_temp.Path, new ManualClock(T0.AddDays(day)));
            s.Continue();
            s.IngestFromSource(new SyntheticWalkingSource(20_000L), T0.AddDays(day - 1), T0.AddDays(day));
            var home = s.GetHome();
            if (home.ActiveProjectId == null && home.Queued.Count == 0)
                foreach (var pid in content.Projects.Select(p => p.Id.Value))
                    if (s.EnqueueProject(pid).IsSuccess) break;
        }
        var completed = s.GetProjects().Projects.Where(p => p.Status == WalkGame.Domain.Projects.ProjectStatus.Completed)
            .Select(p => p.ProjectId).ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(completed);
        var after = s.GetDiscoveries();
        foreach (var row in after.Discoveries)
            Assert.Equal(completed.Contains(row.UnlockedByProjectId), row.Unlocked);
        Assert.True(after.UnlockedCount > before.UnlockedCount);
    }
}
