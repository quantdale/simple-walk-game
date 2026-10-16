using System;
using System.Collections.Generic;
using System.Linq;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.ReadModels;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Time;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-122 slice 3 (KI-023): glance phrases plus producer, expedition and care-program titles
/// resolve through <see cref="GameSession.ContentLocale"/>. English is byte-identical; stored
/// summary lines stay English (a locale never reaches a save).
/// </summary>
public sealed class ContentGlanceOverlayTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private static readonly string[] PhraseNames =
    {
        "nothing-changed", "nothing-restoring", "underway", "next-up", "care-continues", "untitled-project",
        "opaque-change", "paused", "choose-project-action", "choose-first", "nothing-needed", "region-complete",
        "this-valley", "any-landmark-baseline", "landmark-sentence",
    };

    private static ContentText Pseudo(bool brokenPattern = false)
    {
        var c = Region1Catalog.Create();
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in c.Producers) d[ContentText.Key(p.Id.Value, ContentText.Fields.Title)] = "[xx] " + p.TitleKey;
        foreach (var e in c.Expeditions) d[ContentText.Key(e.Id.Value, ContentText.Fields.Title)] = "[xx] " + e.TitleKey;
        foreach (var pr in c.PostCompletionPrograms) d[ContentText.Key(pr.Id, ContentText.Fields.Title)] = "[xx] " + pr.TitleKey;
        foreach (var pj in c.Projects) d[ContentText.Key(pj.Id.Value, ContentText.Fields.Title)] = "[xx] " + pj.TitleKey;
        foreach (var n in PhraseNames) d[ContentText.Key(GlancePhrases.PhraseId(n), GlancePhrases.PhraseField)] = "[xx] " + n;
        d[ContentText.Key(GlancePhrases.PhraseId("underway"), GlancePhrases.PhraseField)] = brokenPattern ? "[xx] {7}" : "[xx] {0} ~ {1}/{2}";
        d[ContentText.Key(GlancePhrases.PhraseId("next-up"), GlancePhrases.PhraseField)] = "[xx] next {0}";
        foreach (RestorationStage st in Enum.GetValues(typeof(RestorationStage)))
            d[ContentText.Key(GlancePhrases.PhraseId("stage." + st.ToString().ToLowerInvariant()), GlancePhrases.PhraseField)] = "[xx] stage";
        return ContentText.ForLocale("xx", d);
    }

    private GameSession Session(long steps, int days)
    {
        var s = TestSessions.Create(_temp.Path, new ManualClock(T0));
        s.StartNewGame(42UL);
        if (steps > 0) s.IngestFromSource(new SyntheticWalkingSource(steps), T0, T0.AddDays(days));
        return s;
    }

    [Fact]
    public void English_IsTheExplicitDefault_AndMatchesTheNullOverlayProjection()
    {
        var s = Session(20_000L, 30);
        var a = s.GetGlance();
        s.ContentLocale = ContentText.English;
        var b = s.GetGlance();
        Assert.Equal(a.RestoringNowSentence, b.RestoringNowSentence);
        Assert.Equal(a.AttentionSentence, b.AttentionSentence);
        Assert.Equal(a.ActivitySentence, b.ActivitySentence);
        Assert.Equal(a.WhatChangedSentence, b.WhatChangedSentence);
        var c = Region1Catalog.Create();
        Assert.Equal(c.Producers.Select(p => p.TitleKey), s.GetRegion().Producers.Select(r => r.TitleKey));
        Assert.Equal(c.Expeditions.Select(e => e.TitleKey), s.GetExpeditions().Expeditions.Select(r => r.TitleKey));
    }

    [Fact]
    public void ALocale_RoutesProducerAndExpeditionTitles_AndGlancePhrases_WithoutTouchingSaves()
    {
        var s = Session(20_000L, 30);
        string before = System.IO.File.ReadAllText(System.IO.Directory.GetFiles(_temp.Path, "*", System.IO.SearchOption.AllDirectories).First(f => f.EndsWith(".json", StringComparison.Ordinal)));
        s.ContentLocale = Pseudo();
        Assert.All(s.GetRegion().Producers, r => Assert.StartsWith("[xx] ", r.TitleKey));
        Assert.All(s.GetExpeditions().Expeditions, r => Assert.StartsWith("[xx] ", r.TitleKey));
        var g = s.GetGlance();
        Assert.StartsWith("[xx] ", g.RestoringNowSentence);
        Assert.All(s.GetRegion().Producers, r => Assert.DoesNotContain("[xx] [xx]", r.TitleKey));
        string after = System.IO.File.ReadAllText(System.IO.Directory.GetFiles(_temp.Path, "*", System.IO.SearchOption.AllDirectories).First(f => f.EndsWith(".json", StringComparison.Ordinal)));
        Assert.Equal(before, after);
    }

    [Fact]
    public void FreshGame_GlanceUsesLocalizedPhrases()
    {
        var s = Session(0L, 0);
        s.ContentLocale = Pseudo();
        var g = s.GetGlance();
        Assert.Equal("[xx] nothing-changed", g.WhatChangedSentence);
        Assert.StartsWith("[xx] ", g.RestoringNowSentence);
    }

    [Fact]
    public void ABrokenLocalizedPattern_FallsBackToTheEnglishPattern()
    {
        Assert.Equal("Old Trailhead is underway (1 of 2).",
            GlancePhrases.Format(Pseudo(brokenPattern: true), "underway", "{0} is underway ({1} of {2}).", "Old Trailhead", "1", "2"));
    }

    [Fact]
    public void StageSentences_EnglishByteIdentical_LocaleUsesPattern()
    {
        foreach (RestorationStage st in Enum.GetValues(typeof(RestorationStage)))
        {
            Assert.Equal(GlancePhrases.FormatLandmark("Mill", st), GlancePhrases.FormatLandmark("Mill", st, null));
            Assert.Equal(GlancePhrases.FormatLandmark("Mill", st), GlancePhrases.FormatLandmark("Mill", st, ContentText.English));
        }
        Assert.Equal("[xx] landmark-sentence", GlancePhrases.FormatLandmark("Mill", RestorationStage.Ruined, Pseudo()));
    }
}
