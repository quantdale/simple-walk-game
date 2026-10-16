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
/// D-125: read-model titles are display text by explicit typing, never sniffed with a dot
/// heuristic. A translated title containing '.' must render as itself.
/// </summary>
public sealed class TitleTypingTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private static ContentText Dotted()
    {
        var c = Region1Catalog.Create();
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in c.Projects) d[ContentText.Key(p.Id.Value, ContentText.Fields.Title)] = "St. " + p.TitleKey + " Nr. 2";
        return ContentText.ForLocale("xx", d);
    }

    private GameSession Session()
    {
        var s = TestSessions.Create(_temp.Path, new ManualClock(T0));
        s.StartNewGame(42UL);
        foreach (var p in s.GetProjects().Projects.Where(p => p.Status == WalkGame.Domain.Projects.ProjectStatus.Available))
            s.EnqueueProject(p.ProjectId);
        return s;
    }

    [Fact]
    public void ADottedTranslatedTitle_IsShownInTheGlance_NotReplacedByTheUntitledPhrase()
    {
        var s = Session();
        s.ContentLocale = Dotted();
        var home = s.GetHome();
        Assert.NotNull(home.ActiveProjectTitleKey);
        Assert.StartsWith("St. ", home.ActiveProjectTitleKey);
        var g = s.GetGlance();
        Assert.Contains(home.ActiveProjectTitleKey!, g.RestoringNowSentence);
        Assert.DoesNotContain("A restoration project", g.RestoringNowSentence);
    }

    [Fact]
    public void ReadModelTitles_NeverCarryAStableId()
    {
        var s = Session();
        var c = Region1Catalog.Create();
        var ids = new HashSet<string>(c.Projects.Select(p => p.Id.Value).Concat(c.Landmarks.Select(l => l.Id.Value)), StringComparer.Ordinal);
        var home = s.GetHome();
        Assert.DoesNotContain(home.ActiveProjectTitleKey ?? "", ids);
        Assert.All(home.Queued, q => Assert.DoesNotContain(q.TitleKey, ids));
        Assert.All(home.Landmarks, l => Assert.DoesNotContain(l.TitleKey, ids));
        Assert.All(s.GetProjects().Projects, p => Assert.DoesNotContain(p.TitleKey, ids));
    }
}
