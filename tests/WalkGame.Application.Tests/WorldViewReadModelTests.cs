using System;
using System.Collections.Generic;
using System.Linq;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.ReadModels;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Time;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>D-165: the world view and dialogue are pure, truthful projections of canonical state.</summary>
public sealed class WorldViewReadModelTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private GameSession Fresh()
    {
        var s = TestSessions.Create(_temp.Path, new ManualClock(T0));
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(42UL).Status);
        return s;
    }

    /// <summary>Plays <paramref name="days"/> days at 20k steps, queueing the first available project when idle.</summary>
    private GameSession PlayDays(int days, Action<GameSession, int>? eachDay = null)
    {
        var content = Region1Catalog.Create();
        Fresh();
        GameSession d = null!;
        for (int day = 1; day <= days; day++)
        {
            var end = T0.AddDays(day);
            d = TestSessions.Create(_temp.Path, new ManualClock(end));
            Assert.Equal(StartStatus.Loaded, d.Continue().Status);
            d.IngestFromSource(new SyntheticWalkingSource(20_000L), end.AddDays(-1), end);
            var home = d.GetHome();
            if (home.ActiveProjectId == null && home.Queued.Count == 0)
                foreach (var id in content.Projects.Select(p => p.Id.Value))
                    if (d.EnqueueProject(id).IsSuccess) break;
            if (d.GetPendingReturnSummary() != null) d.AcknowledgeReturnSummary();
            eachDay?.Invoke(d, day);
        }
        return d;
    }

    [Fact]
    public void FreshValley_OnlyTheTrailheadIsOpen_WithWrenWaiting()
    {
        var view = Fresh().GetWorldView();
        Assert.Equal(6, view.Locations.Count);
        Assert.All(view.Locations, l => { Assert.Equal(RestorationStage.Ruined, l.Stage); Assert.Equal(l.LocationId + "@ruined", l.VariantKey); });
        var trail = view.Find("lm.trailhead")!;
        Assert.True(trail.IsUnlocked);
        Assert.True(trail.IsInteractable);
        Assert.Equal(new[] { TestSessions.EntryProjectId }, trail.SelectableProjectIds);
        Assert.Equal(new[] { "npc.wren" }, trail.CharacterIds);
        Assert.Equal("line.wren.1", Assert.Single(trail.PendingDialogue).LineId);
        Assert.All(view.Locations.Where(l => l.LocationId != "lm.trailhead"), l => { Assert.False(l.IsUnlocked); Assert.False(l.IsInteractable); });
        Assert.Equal("region@eco0-set0", view.AtmosphereKey);
        Assert.False(view.RegionCompleted);
    }

    [Fact]
    public void SeenLines_AreNotPending_AndANewMilestoneMakesThemPendingAgain()
    {
        var s = Fresh();
        Assert.False(s.GetWorldView(new[] { "line.wren.1" }).Find("lm.trailhead")!.HasPendingDialogue);
        var later = PlayDays(2);
        Assert.Equal(ProjectStatus.Completed, later.GetProjects().Projects.Single(p => p.ProjectId == TestSessions.EntryProjectId).Status);
        var trail = later.GetWorldView(new[] { "line.wren.1" }).Find("lm.trailhead")!;
        Assert.Equal("line.wren.2", Assert.Single(trail.PendingDialogue).LineId);
        var wren = later.GetCharacters().Single(c => c.CharacterId == "npc.wren");
        Assert.Equal(new[] { "line.wren.1", "line.wren.2" }, wren.ReachedLines.Take(2).Select(l => l.LineId));
    }

    [Fact]
    public void AlongAWholePlaythrough_TheViewMatchesCanonicalStateEveryDay()
    {
        var content = Region1Catalog.Create();
        var locationOf = WorldPresentationProjector.LocationOfProject(content);
        var everUnlocked = new HashSet<string>();
        var lastStage = new Dictionary<string, RestorationStage>();
        var lastLines = new Dictionary<string, int>();
        var final = PlayDays(120, (d, day) =>
        {
            var view = d.GetWorldView();
            var region = d.GetRegion();
            var projects = d.GetProjects().Projects.ToDictionary(p => p.ProjectId);
            foreach (var lm in region.Landmarks)
            {
                var row = view.Find(lm.LandmarkId)!;
                Assert.Equal(lm.Stage, row.Stage);
                Assert.Equal(lm.TitleKey, row.Title);
                // Monotonic: a place never relocks or un-restores.
                if (everUnlocked.Contains(row.LocationId)) Assert.True(row.IsUnlocked, $"day {day}: {row.LocationId} relocked");
                if (row.IsUnlocked) everUnlocked.Add(row.LocationId);
                if (lastStage.TryGetValue(row.LocationId, out var prev)) Assert.True(row.Stage >= prev);
                lastStage[row.LocationId] = row.Stage;
                foreach (var pid in row.SelectableProjectIds)
                    Assert.Contains(projects[pid].Status, new[] { ProjectStatus.Available, ProjectStatus.Queued });
                foreach (var pid in row.ProjectIds) Assert.Equal(row.LocationId, locationOf[pid]);
                if (row.ActiveProjectId != null) Assert.Equal(d.GetHome().ActiveProjectId, row.ActiveProjectId);
                if (row.IsInteractable) Assert.True(row.IsUnlocked);
            }
            // Every selectable project in the game is selectable at exactly one place.
            var selectable = view.Locations.SelectMany(l => l.SelectableProjectIds).ToList();
            Assert.Equal(selectable.Count, selectable.Distinct().Count());
            Assert.Equal(projects.Values.Where(p => p.Status is ProjectStatus.Available or ProjectStatus.Queued).Select(p => p.ProjectId).OrderBy(x => x),
                selectable.OrderBy(x => x));
            // Dialogue only grows; a reached line is never lost.
            foreach (var c in d.GetCharacters())
            {
                if (lastLines.TryGetValue(c.CharacterId, out var n)) Assert.True(c.ReachedLines.Count >= n);
                lastLines[c.CharacterId] = c.ReachedLines.Count;
            }
        });
        Assert.True(final.GetRegion().CompletedProjects >= 10, "the playthrough should get well into the valley");
        Assert.True(everUnlocked.Count >= 4);
    }

    [Fact]
    public void Reading_IsSideEffectFree_AndDeterministic()
    {
        var s = PlayDays(30);
        var before = s.ExportSaveEnvelope()!;
        var a = s.GetWorldView(new[] { "line.wren.1" });
        var chars = s.GetCharacters();
        for (int i = 0; i < 5; i++) { s.GetWorldView(); s.GetCharacters(); }
        Assert.Equal(before, s.ExportSaveEnvelope()!);
        var b = s.GetWorldView(new[] { "line.wren.1" });
        Assert.Equal(Describe(a), Describe(b));
        // A fresh session loading the same save sees the same world.
        var reloaded = TestSessions.Create(_temp.Path, new ManualClock(T0.AddDays(30)));
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);
        Assert.Equal(Describe(a), Describe(reloaded.GetWorldView(new[] { "line.wren.1" })));
        Assert.Equal(chars.Select(c => c.CharacterId + ":" + c.ReachedLines.Count), reloaded.GetCharacters().Select(c => c.CharacterId + ":" + c.ReachedLines.Count));
    }

    [Fact]
    public void NeverReadingDialogue_ProgressesExactlyLikeReadingItAll()
    {
        using var other = new TempDirectory();
        var readEverything = PlayDays(60, (d, _) => { d.GetCharacters(); d.GetWorldView(); });
        var readerSave = readEverything.ExportSaveEnvelope()!;

        var content = Region1Catalog.Create();
        var s = TestSessions.Create(other.Path, new ManualClock(T0));
        s.StartNewGame(42UL);
        GameSession d = null!;
        for (int day = 1; day <= 60; day++)
        {
            var end = T0.AddDays(day);
            d = TestSessions.Create(other.Path, new ManualClock(end));
            d.Continue();
            d.IngestFromSource(new SyntheticWalkingSource(20_000L), end.AddDays(-1), end);
            var home = d.GetHome();
            if (home.ActiveProjectId == null && home.Queued.Count == 0)
                foreach (var id in content.Projects.Select(p => p.Id.Value))
                    if (d.EnqueueProject(id).IsSuccess) break;
            if (d.GetPendingReturnSummary() != null) d.AcknowledgeReturnSummary();
        }
        Assert.Equal(readerSave, d.ExportSaveEnvelope()!);
    }

    [Fact]
    public void Dialogue_AndNames_LocalizeThroughContentText_WithEnglishFallback()
    {
        var s = Fresh();
        s.ContentLocale = ContentText.ForLocale("xx", new Dictionary<string, string>
        {
            [ContentText.Key("npc.wren", ContentText.Fields.Name)] = "Roitelet",
            [ContentText.Key("line.wren.1", ContentText.Fields.Line)] = "Bonjour, marcheur.",
            [ContentText.Key("lm.trailhead", ContentText.Fields.Title)] = "Vieux sentier",
        });
        var wren = s.GetCharacters().Single(c => c.CharacterId == "npc.wren");
        Assert.Equal("Roitelet", wren.Name);
        Assert.Equal("Bonjour, marcheur.", wren.CurrentLine!.Text);
        Assert.Equal("Vieux sentier", s.GetWorldView().Find("lm.trailhead")!.Title);
        var arlo = s.GetCharacters().Single(c => c.CharacterId == "npc.arlo");
        Assert.Equal("Arlo", arlo.Name); // fallback to authored English
        Assert.False(arlo.IsPresent);
        Assert.Empty(arlo.ReachedLines);
    }

    [Fact]
    public void ActiveLongProject_ShowsItsConstructionStageAtItsLocation()
    {
        var content = Region1Catalog.Create();
        var locationOf = WorldPresentationProjector.LocationOfProject(content);
        bool sawConstruction = false;
        PlayDays(120, (d, _) =>
        {
            foreach (var row in d.GetWorldView().Locations)
            {
                if (row.ConstructionKey == null) continue;
                sawConstruction = true;
                Assert.NotNull(row.ActiveProjectId);
                Assert.StartsWith(row.ActiveProjectId + "@build-", row.ConstructionKey);
                Assert.Equal(row.LocationId, locationOf[row.ActiveProjectId!]);
                var region = d.GetRegion();
                Assert.Equal(WorldViewReadModel.ConstructionKeyFor(row.ActiveProjectId!, region.ActiveBuildStage, region.ActiveBuildStages), row.ConstructionKey);
            }
        });
        Assert.True(sawConstruction, "a multi-stage project should show construction at some point");
    }

    private static string Describe(WorldViewReadModel v) => string.Join("|", v.Locations.Select(l =>
        $"{l.VariantKey},{l.IsUnlocked},{l.IsInteractable},{string.Join("+", l.SelectableProjectIds)},{l.ActiveProjectId},{l.ConstructionKey},{l.DiscoveriesFound},{string.Join("+", l.CharacterIds)},{string.Join("+", l.PendingDialogue.Select(p => p.LineId))}"))
        + "|" + v.AtmosphereKey + "|" + v.RegionCompleted;
}
