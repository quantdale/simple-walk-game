using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.ReadModels;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-176 (KI-027 a, b): the trail bridge shows ruined → partly rebuilt → rebuilt. Content v6 authors
/// 3 visible build stages (derived from invested Vitality, D-071 rule); every project has its own visual
/// key on its place; completion emits one <see cref="VisualChangeKind.ProjectCompleted"/> event, in the
/// replayed stream and the in-session diff alike. Old (v5) saves load unchanged.
/// </summary>
public sealed class BridgeBuildStageTests : IDisposable
{
    private const string Bridge = "proj.rebuild-trail-bridges";
    private const string Clear = "proj.clear-trailhead";
    private static readonly DateTimeOffset T0 = TestSessions.T0;
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private static NormalizedActivityRecord Walk(int n, DateTimeOffset end, long steps) =>
        new("bridge.provider", "walk-" + n, ActivityCategory.Walking, ActivityUnits.Steps, steps, end.AddMinutes(-30), end);

    [Fact]
    public void Bridge_authors_three_stages_without_changing_cost_or_prerequisites()
    {
        var p = Region1Catalog.Create().Projects.Single(x => x.Id.Value == Bridge);
        Assert.Equal(300L, p.VitalityCost);
        Assert.Equal(3, p.VisibleBuildStages);
        Assert.Equal(new[] { Clear }, p.Prerequisites.Select(x => x.Value));
        Assert.Equal(new[] { 0, 0, 1, 1, 2, 2, 2 }, new[] { 0L, 99L, 100L, 199L, 200L, 299L, 300L }.Select(p.BuildStageFor));
    }

    [Fact]
    public void Walking_shows_the_bridge_ruined_then_partly_rebuilt_then_rebuilt()
    {
        var clock = new ManualClock(T0);
        var s = TestSessions.Create(_temp.Path, clock);
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(42UL).Status);
        Assert.True(s.EnqueueProject(Clear).IsSuccess);

        var keys = new List<string>();
        var trailKeys = new List<string?>();
        var diffs = new List<VisualChangeEvent>();
        var streamed = new List<VisualChangeEvent>();
        var view = s.GetWorldView();
        Record(view);
        // 1,500 steps (15 Vitality) per walk: fine enough to see every 100-Vitality stage.
        for (int i = 1; i <= 120 && view.FindProject(Bridge)!.Status != ProjectStatus.Completed; i++)
        {
            var since = clock.UtcNow;
            clock.Advance(TimeSpan.FromHours(2));
            s.IngestActivityBatch(new[] { Walk(i, clock.UtcNow, 1_500L) });
            if (s.GetPendingReturnSummary() != null) s.AcknowledgeReturnSummary();
            if (s.GetProjects().Projects.Single(p => p.ProjectId == Clear).Status == ProjectStatus.Completed
                && s.GetProjects().Projects.Single(p => p.ProjectId == Bridge).Status == ProjectStatus.Available)
                Assert.True(s.EnqueueProject(Bridge).IsSuccess);
            var next = s.GetWorldView();
            diffs.AddRange(WorldViewDiff.Between(view, next));
            streamed.AddRange(s.GetVisualChangesSince(since));
            view = next;
            Record(view);
        }

        Assert.Equal(new[] { Bridge + "@ruined", Bridge + "@build-0of3", Bridge + "@build-1of3", Bridge + "@build-2of3", Bridge + "@done" },
            keys.Distinct().ToArray());
        Assert.Contains(Bridge + "@build-1of3", trailKeys);   // the trailhead row carries the construction key while it is built
        Assert.Contains(Bridge + "@build-2of3", trailKeys);
        Assert.Equal(2, diffs.Count(e => e.Kind == VisualChangeKind.ConstructionAdvanced && e.SubjectId == Bridge));

        var done = streamed.Where(e => e.Kind == VisualChangeKind.ProjectCompleted && e.SubjectId == Bridge).ToList();
        Assert.Single(done);
        Assert.Equal("lm.trailhead", done[0].LocationId);
        Assert.Equal(Bridge + "@done", done[0].ToKey);
        Assert.Null(done[0].FromKey);
        Assert.Equal(done[0].Shape, diffs.Single(e => e.Kind == VisualChangeKind.ProjectCompleted && e.SubjectId == Bridge).Shape);
        var batch = streamed.Where(e => e.AtUtc == done[0].AtUtc).ToList();
        Assert.Equal(VisualChangeKind.ProjectCompleted, batch.First().Kind); // the bridge transition plays before what it opens

        void Record(WorldViewReadModel v)
        {
            keys.Add(v.FindProject(Bridge)!.VisualKey);
            trailKeys.Add(v.Find("lm.trailhead")!.ConstructionKey);
        }
    }

    [Fact]
    public void Every_project_has_a_visual_at_its_place_and_completion_is_streamed_once_each()
    {
        var h = new WorldChangeHarness(_temp.Path, 20_000L);
        for (int day = 1; day <= 200 && !h.Session.GetRegion().RegionCompleted; day++) h.PlayDay(day);
        var content = Region1Catalog.Create();
        var view = h.Session.GetWorldView();
        Assert.Equal(content.Projects.Select(p => p.Id.Value).OrderBy(x => x), view.Locations.SelectMany(l => l.ProjectVisuals).Select(p => p.ProjectId).OrderBy(x => x));
        foreach (var l in view.Locations)
            Assert.All(l.ProjectVisuals, p => Assert.Equal(l.LocationId, p.LocationId));

        var all = h.Session.GetVisualChangesSince(DateTimeOffset.MinValue);
        var completed = h.Session.GetProjects().Projects.Where(p => p.Status == ProjectStatus.Completed).Select(p => p.ProjectId).OrderBy(x => x).ToList();
        Assert.Equal(completed, all.Where(e => e.Kind == VisualChangeKind.ProjectCompleted).Select(e => e.SubjectId!).OrderBy(x => x).ToList());
        Assert.All(all.Where(e => e.Kind == VisualChangeKind.ProjectCompleted), e => Assert.Equal(e.SubjectId + "@done", e.ToKey));
        Assert.All(view.Locations.SelectMany(l => l.ProjectVisuals).Where(p => p.IsCompleted), p => Assert.EndsWith("@done", p.VisualKey));
    }

    [Fact]
    public void The_bridge_choice_previews_its_own_ruined_and_rebuilt_images()
    {
        var clock = new ManualClock(T0);
        var s = TestSessions.Create(_temp.Path, clock);
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(42UL).Status);
        Assert.True(s.EnqueueProject(Clear).IsSuccess);
        for (int i = 1; i <= 40 && s.GetRestorationChoices().Find(Bridge) == null; i++)
        {
            clock.Advance(TimeSpan.FromHours(2));
            s.IngestActivityBatch(new[] { Walk(i, clock.UtcNow, 1_500L) });
        }
        var c = s.GetRestorationChoices().Find(Bridge)!;
        Assert.Equal(Bridge + "@ruined", c.ProjectVisualKey);
        Assert.Equal(Bridge + "@done", c.DoneVisualKey);
        Assert.Equal(c.BeforeVariantKey, c.AfterVariantKey); // the bridge changes no landmark stage: the prop keys carry the picture
    }

    [Fact]
    public void A_content_v5_save_with_the_bridge_half_built_loads_unchanged_and_shows_its_stage()
    {
        var clock = new ManualClock(T0);
        var s = TestSessions.Create(_temp.Path, clock);
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(42UL).Status);
        Assert.True(s.EnqueueProject(Clear).IsSuccess);
        for (int i = 1; i <= 80; i++)
        {
            clock.Advance(TimeSpan.FromHours(2));
            s.IngestActivityBatch(new[] { Walk(i, clock.UtcNow, 1_500L) });
            if (s.GetProjects().Projects.Single(p => p.ProjectId == Bridge).Status == ProjectStatus.Available) s.EnqueueProject(Bridge);
            var b = s.GetProjects().Projects.Single(p => p.ProjectId == Bridge);
            if (b.Status == ProjectStatus.Active && b.VitalityInvested >= 150) break;
        }
        var codec = TestSessions.NewCodec();
        var game = codec.Decode(File.ReadAllBytes(_temp.FilePath("save.json"))).State!;
        Assert.Equal(ProjectStatus.Active, game.Region.FindProject(Bridge)!.Status);
        game.AuthoredContentVersion = 5; // as the previous build wrote it
        new AtomicFileSaveStore(_temp.Path).WriteAtomic(codec.Encode(game, clock.UtcNow));
        var invested = game.Region.FindProject(Bridge)!.VitalityInvested;

        var reopened = TestSessions.Create(_temp.Path, new ManualClock(clock.UtcNow));
        Assert.Equal(StartStatus.Loaded, reopened.Continue().Status);
        Assert.Equal(ContentCompatibility.CompatibleOlder, reopened.GetDiagnostics().ContentCompatibility);
        Assert.Equal(5, reopened.GetDiagnostics().SaveContentVersion);
        var row = reopened.GetProjects().Projects.Single(p => p.ProjectId == Bridge);
        Assert.Equal(ProjectStatus.Active, row.Status);
        Assert.Equal(invested, row.VitalityInvested);
        int stage = Region1Catalog.Create().Projects.Single(p => p.Id.Value == Bridge).BuildStageFor(invested);
        Assert.True(stage >= 1);
        Assert.Equal(Bridge + "@build-" + stage + "of3", reopened.GetWorldView().Find("lm.trailhead")!.ConstructionKey);
        Assert.Equal(5, codec.Decode(File.ReadAllBytes(_temp.FilePath("save.json"))).State!.AuthoredContentVersion); // recorded version is never rewritten
    }
}
