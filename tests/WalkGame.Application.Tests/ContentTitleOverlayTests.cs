using System;
using System.Collections.Generic;
using System.Linq;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Time;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-122 slice 2 (KI-023): project, landmark, region and chapter text plus discovery titles and
/// copy resolve through <see cref="GameSession.ContentLocale"/>. English is byte-identical; a
/// locale changes presentation only, never save bytes, content version or the catalog.
/// </summary>
public sealed class ContentTitleOverlayTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private const string X = "[xx] ";

    private static ContentText Pseudo()
    {
        var c = Region1Catalog.Create();
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        void Add(string id, string field, string? en) { if (en != null) d[ContentText.Key(id, field)] = X + en; }
        Add(c.Id.Value, ContentText.Fields.Title, c.TitleKey);
        foreach (var p in c.Projects) Add(p.Id.Value, ContentText.Fields.Title, p.TitleKey);
        foreach (var l in c.Landmarks) Add(l.Id.Value, ContentText.Fields.Title, l.TitleKey);
        foreach (var ch in c.Chapters)
        {
            Add(ch.Id, ContentText.Fields.Title, ch.TitleKey);
            Add(ch.Id, ContentText.Fields.Body, ch.BodyKey);
            Add(ch.Id, ContentText.Fields.Hint, ch.HintKey);
        }
        foreach (var disc in c.Discoveries)
        {
            Add(disc.Id.Value, ContentText.Fields.Title, disc.TitleKey);
            Add(disc.Id.Value, ContentText.Fields.Body, Region1DiscoveryCopy.Text(disc.BodyKey));
            Add(disc.Id.Value, ContentText.Fields.Provenance, Region1DiscoveryCopy.Text(disc.ProvenanceKey));
        }
        return ContentText.ForLocale("xx", d);
    }

    private GameSession Played()
    {
        var s = TestSessions.Create(_temp.Path, new ManualClock(T0));
        s.StartNewGame(42UL);
        s.IngestFromSource(new SyntheticWalkingSource(20_000L), T0, T0.AddDays(30));
        return s;
    }

    [Fact]
    public void English_ReadModels_CarryTheAuthoredCatalogText()
    {
        var c = Region1Catalog.Create();
        var s = Played();
        Assert.Equal(c.TitleKey, s.GetHome().RegionTitleKey);
        Assert.Equal(c.TitleKey, s.GetRegion().RegionTitleKey);
        Assert.Equal(c.Projects.Select(p => p.TitleKey), s.GetProjects().Projects.Select(r => r.TitleKey));
        Assert.Equal(c.Landmarks.Select(l => l.TitleKey), s.GetHome().Landmarks.Select(r => r.TitleKey));
        Assert.Equal(c.Landmarks.Select(l => l.TitleKey), s.GetRegion().Landmarks.Select(r => r.TitleKey));
        Assert.Equal(c.Chapters.Select(ch => ch.TitleKey), s.GetStory().Chapters.Select(r => r.TitleKey));
        Assert.Equal(c.Chapters.Select(ch => ch.HintKey), s.GetStory().Chapters.Select(r => r.HintKey));
        Assert.Equal(c.Discoveries.Select(d => d.TitleKey), s.GetDiscoveries().Discoveries.Select(r => r.TitleKey));
        foreach (var d in c.Discoveries)
            Assert.Equal(Region1DiscoveryCopy.Text(d.BodyKey), s.DiscoveryCopy(d.Id.Value, ContentText.Fields.Body, d.BodyKey));
        Assert.Null(s.DiscoveryCopy("disc.nope", ContentText.Fields.Body, "disc.nope.body"));
    }

    [Fact]
    public void ALocale_ChangesEveryRoutedText_ButNotIds_SaveBytes_ContentVersion_OrCatalog()
    {
        var c = Region1Catalog.Create();
        int version = c.ContentVersion;
        var s = Played();
        var files = System.IO.Directory.GetFiles(_temp.Path, "*", System.IO.SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal).ToArray();
        var before = files.Select(System.IO.File.ReadAllBytes).ToArray();

        var enHome = s.GetHome(); var enRegion = s.GetRegion(); var enProjects = s.GetProjects();
        var enStory = s.GetStory(); var enDisc = s.GetDiscoveries();
        Assert.True(enStory.Chapters.Any(ch => ch.Unlocked), "fixture should unlock at least one chapter");

        s.ContentLocale = Pseudo();
        var home = s.GetHome(); var region = s.GetRegion(); var projects = s.GetProjects();
        var story = s.GetStory(); var disc = s.GetDiscoveries();

        Assert.Equal(X + enHome.RegionTitleKey, home.RegionTitleKey);
        Assert.Equal(X + enRegion.RegionTitleKey, region.RegionTitleKey);
        if (enHome.ActiveProjectTitleKey != null) Assert.Equal(X + enHome.ActiveProjectTitleKey, home.ActiveProjectTitleKey);
        if (enRegion.ActiveProjectTitleKey != null) Assert.Equal(X + enRegion.ActiveProjectTitleKey, region.ActiveProjectTitleKey);
        Assert.Equal(enHome.Queued.Select(q => X + q.TitleKey), home.Queued.Select(q => q.TitleKey));
        Assert.Equal(enHome.Landmarks.Select(l => X + l.TitleKey), home.Landmarks.Select(l => l.TitleKey));
        Assert.Equal(enRegion.Landmarks.Select(l => X + l.TitleKey), region.Landmarks.Select(l => l.TitleKey));
        Assert.Equal(enProjects.Projects.Select(p => X + p.TitleKey), projects.Projects.Select(p => p.TitleKey));
        Assert.Equal(enProjects.Projects.Select(p => p.ProjectId), projects.Projects.Select(p => p.ProjectId));
        for (int i = 0; i < enStory.Chapters.Count; i++)
        {
            Assert.Equal(X + enStory.Chapters[i].TitleKey, story.Chapters[i].TitleKey);
            Assert.Equal(X + enStory.Chapters[i].HintKey, story.Chapters[i].HintKey);
            Assert.Equal(enStory.Chapters[i].BodyKey == null ? null : X + enStory.Chapters[i].BodyKey, story.Chapters[i].BodyKey);
            Assert.Equal(enStory.Chapters[i].ChapterId, story.Chapters[i].ChapterId);
        }
        Assert.Equal(enStory.NextHintKey == null ? null : X + enStory.NextHintKey, story.NextHintKey);
        Assert.Equal(enDisc.Discoveries.Select(d => X + d.TitleKey), disc.Discoveries.Select(d => d.TitleKey));
        Assert.Equal(enDisc.Discoveries.Select(d => d.BodyKey), disc.Discoveries.Select(d => d.BodyKey));
        foreach (var d in c.Discoveries)
            Assert.Equal(X + Region1DiscoveryCopy.Text(d.ProvenanceKey), s.DiscoveryCopy(d.Id.Value, ContentText.Fields.Provenance, d.ProvenanceKey));

        s.ContentLocale = ContentText.English;
        Assert.Equal(enHome.RegionTitleKey, s.GetHome().RegionTitleKey);

        for (int i = 0; i < files.Length; i++) Assert.Equal(before[i], System.IO.File.ReadAllBytes(files[i]));
        var after = Region1Catalog.Create();
        Assert.Equal(version, after.ContentVersion);
        Assert.Equal(c.Projects.Select(p => p.TitleKey), after.Projects.Select(p => p.TitleKey));
        Assert.Equal(c.Chapters.Select(p => p.BodyKey), after.Chapters.Select(p => p.BodyKey));
    }
}
