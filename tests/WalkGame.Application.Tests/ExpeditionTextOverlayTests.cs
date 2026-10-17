using System;
using System.Collections.Generic;
using System.Linq;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Summaries;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Time;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>D-141 (KI-023 last remainder): reward-suffixed expedition lines and expedition/care
/// descriptions are display-localized; English is byte-identical and the save never changes.</summary>
public sealed class ExpeditionTextOverlayTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private static string Line(string title, long units, string res) => title + " expedition complete" + $" (+{units:N0} {res}).";

    [Fact]
    public void RewardLine_English_IsStoredBytes_AndUnknownLocaleFallsBack()
    {
        var c = Region1Catalog.Create();
        var e = c.Expeditions[0];
        string stored = Line(e.TitleKey, 1234, "Materials");
        Assert.Same(stored, new SummaryLineText(ContentText.English, c).Localize(stored));
        var empty = ContentText.ForLocale("xx", new Dictionary<string, string>());
        Assert.Equal(stored, new SummaryLineText(empty, c).Localize(stored));
    }

    [Fact]
    public void RewardLine_IsTranslated_WithTitleUnitsAndResource()
    {
        var c = Region1Catalog.Create();
        var e = c.Expeditions[0];
        var d = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ContentText.Key(e.Id.Value, ContentText.Fields.Title)] = "[xx] T",
            [ContentText.Key(SummaryLineText.ResourceId("Materials"), ContentText.Fields.Title)] = "[xx] M",
            [ContentText.Key(SummaryLineText.PatternId(SummaryLineText.ExpeditionRewardName), SummaryLineText.Field)] = "{0} / {2} +{1}",
        };
        var t = new SummaryLineText(ContentText.ForLocale("xx", d), c);
        Assert.Equal("[xx] T / [xx] M +1,234", t.Localize(Line(e.TitleKey, 1234, "Materials")));
        // Only the title translated: the English pattern is used with the localized title.
        var onlyTitle = new SummaryLineText(ContentText.ForLocale("xx", new Dictionary<string, string> { [ContentText.Key(e.Id.Value, ContentText.Fields.Title)] = "Z" }), c);
        Assert.Equal("Z expedition complete (+40 Materials).", onlyTitle.Localize(Line(e.TitleKey, 40, "Materials")));
        // A broken pattern falls back to the English form, never throws or shows a key.
        var broken = new SummaryLineText(ContentText.ForLocale("xx", new Dictionary<string, string> { [ContentText.Key(SummaryLineText.PatternId(SummaryLineText.ExpeditionRewardName), SummaryLineText.Field)] = "{7}" }), c);
        Assert.Equal(Line(e.TitleKey, 40, "Materials"), broken.Localize(Line(e.TitleKey, 40, "Materials")));
    }

    [Fact]
    public void ExpeditionDescriptions_FollowTheLocale_EnglishFallback()
    {
        var c = Region1Catalog.Create();
        var s = TestSessions.Create(_temp.Path, new ManualClock(T0));
        s.StartNewGame(42UL);
        var english = s.GetExpeditions().Expeditions.Select(r => r.DescriptionKey).ToList();
        Assert.Equal(c.Expeditions.Select(x => x.DescriptionKey), english);
        var first = c.Expeditions[0];
        s.ContentLocale = ContentText.ForLocale("xx", new Dictionary<string, string>
        { [ContentText.Key(first.Id.Value, ContentText.Fields.Description)] = "[xx] desc" });
        var local = s.GetExpeditions().Expeditions.Select(r => r.DescriptionKey).ToList();
        Assert.Equal("[xx] desc", local[0]);
        Assert.Equal(english.Skip(1), local.Skip(1));
    }
}
