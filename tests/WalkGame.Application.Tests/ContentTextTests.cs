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

/// <summary>D-122: the content-copy overlay changes presentation only, never content or saves.</summary>
public sealed class ContentTextTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private static Dictionary<string, string> PseudoLocale() =>
        Region1Letters.All.SelectMany(l => new[]
        {
            new KeyValuePair<string, string>(ContentText.Key(l.Id, ContentText.Fields.Body), "[xx] " + l.Body),
            new KeyValuePair<string, string>(ContentText.Key(l.Id, ContentText.Fields.Place), "[xx] " + l.PlaceKey),
        }).ToDictionary(p => p.Key, p => p.Value);


    [Fact]
    public void English_ResolvesToTheAuthoredText_Exactly()
    {
        foreach (var l in Region1Letters.All)
        {
            Assert.Same(l.Body, ContentText.English.Resolve(l.Id, ContentText.Fields.Body, l.Body));
            Assert.Same(l.PlaceKey, ContentText.English.Resolve(l.Id, ContentText.Fields.Place, l.PlaceKey));
        }
    }

    [Fact]
    public void MissingOrBlankEntries_FallBackToEnglish_NeverARawKey()
    {
        var l = Region1Letters.All[0];
        var partial = ContentText.ForLocale("xx", new Dictionary<string, string> { [ContentText.Key(l.Id, "body")] = "  " });
        Assert.Equal(l.Body, partial.Resolve(l.Id, ContentText.Fields.Body, l.Body));
        Assert.Equal(l.PlaceKey, partial.Resolve(l.Id, ContentText.Fields.Place, l.PlaceKey));
        Assert.Throws<ArgumentException>(() => ContentText.ForLocale(" ", new Dictionary<string, string>()));
    }

    [Fact]
    public void Overlay_IsCopiedAtConstruction_SoLaterMutationCannotLeak()
    {
        var l = Region1Letters.All[0];
        var d = new Dictionary<string, string>();
        var t = ContentText.ForLocale("xx", d);
        d[ContentText.Key(l.Id, "body")] = "late";
        Assert.Equal(l.Body, t.Resolve(l.Id, "body", l.Body));
    }

    [Fact]
    public void ALocale_ChangesLetterText_ButNotSaveBytes_ContentVersion_OrCatalog()
    {
        var content = Region1Catalog.Create();
        int version = content.ContentVersion;
        var titlesBefore = content.Projects.Select(p => p.TitleKey).ToArray();
        var s = TestSessions.Create(_temp.Path, new ManualClock(T0));
        s.StartNewGame(42UL);
        s.IngestFromSource(new SyntheticWalkingSource(20_000L), T0, T0.AddHours(12));
        var files = System.IO.Directory.GetFiles(_temp.Path).OrderBy(f => f).ToArray();
        var before = files.Select(System.IO.File.ReadAllBytes).ToArray();

        var english = s.GetLetters();
        s.ContentLocale = ContentText.ForLocale("xx", PseudoLocale());
        var local = s.GetLetters();
        Assert.Equal(english.FoundCount, local.FoundCount);
        for (int i = 0; i < english.Letters.Count; i++)
        {
            Assert.Equal("[xx] " + english.Letters[i].Place, local.Letters[i].Place);
            Assert.Equal(english.Letters[i].Body == null ? null : "[xx] " + english.Letters[i].Body, local.Letters[i].Body);
            Assert.Equal(english.Letters[i].LetterId, local.Letters[i].LetterId);
        }
        s.ContentLocale = ContentText.English;
        Assert.Equal(english.Letters.Select(l => l.Place), s.GetLetters().Letters.Select(l => l.Place));

        for (int i = 0; i < files.Length; i++) Assert.Equal(before[i], System.IO.File.ReadAllBytes(files[i]));
        Assert.Equal(version, Region1Catalog.Create().ContentVersion);
        Assert.Equal(titlesBefore, Region1Catalog.Create().Projects.Select(p => p.TitleKey).ToArray());
    }
}
