using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.Summaries;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Time;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>D-138 (KI-023): stored summary lines are localized for display only; the save stays English.</summary>
public sealed class SummaryLineOverlayTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private static readonly string[] Names =
    {
        "restorations-complete", "more-ready", "producers-delivered", "vitality-credited", "duplicates-ignored",
        "ecology-stage", "settlement-stage", "new-chapter", "discovery-recorded", "region-complete", "producer-capped",
        "producer-store-filled", "project-complete", "project-ready", "producer-operating", "expedition-ready",
        "expedition-complete", "next-action-queue",
    };

    private static ContentText Pseudo()
    {
        var c = Region1Catalog.Create();
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in c.Projects) d[ContentText.Key(p.Id.Value, ContentText.Fields.Title)] = "[xx] " + p.TitleKey;
        foreach (var l in c.Landmarks) d[ContentText.Key(l.Id.Value, ContentText.Fields.Title)] = "[xx] " + l.TitleKey;
        foreach (var ch in c.Chapters) d[ContentText.Key(ch.Id, ContentText.Fields.Title)] = "[xx] " + ch.TitleKey;
        foreach (var n in Names) d[ContentText.Key(SummaryLineText.PatternId(n), SummaryLineText.Field)] = "[xx:" + n + "] {0}";
        return ContentText.ForLocale("xx", d);
    }

    private GameSession Session()
    {
        var s = TestSessions.Create(_temp.Path, new ManualClock(T0.AddDays(3)));
        s.StartNewGame(42UL);
        foreach (var p in Region1Catalog.Create().Projects)
            if (s.EnqueueProject(p.Id.Value).IsSuccess) break;
        s.IngestFromSource(new SyntheticWalkingSource(20_000L), T0, T0.AddDays(3));
        Assert.NotNull(s.GetPendingReturnSummary());
        return s;
    }

    private byte[] SaveBytes() =>
        Directory.GetFiles(_temp.Path, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal)
            .SelectMany(File.ReadAllBytes).ToArray();

    [Fact]
    public void English_IsByteIdentical_AndPseudoLocaleTranslatesEveryRecognisedLine_WithoutTouchingTheSave()
    {
        var s = Session();
        var english = s.GetPendingReturnSummary()!;
        Assert.NotEmpty(english.Items);
        var before = SaveBytes();

        s.ContentLocale = Pseudo();
        var local = s.GetPendingReturnSummary()!;
        Assert.Equal(english.Items.Count, local.Items.Count);
        for (int i = 0; i < english.Items.Count; i++)
        {
            Assert.Equal(english.Items[i].Kind, local.Items[i].Kind);
            Assert.Equal(english.Items[i].Text, local.Items[i].Text); // canonical stays English
            Assert.False(string.IsNullOrWhiteSpace(local.Items[i].DisplayText));
            Assert.DoesNotContain("summary.", local.Items[i].DisplayText);
        }
        Assert.Contains(local.Items, r => r.DisplayText.StartsWith("[xx", StringComparison.Ordinal));
        Assert.Equal(before, SaveBytes());

        // D-162: English display is the stored line except for the reworded project/expedition forms.
        s.ContentLocale = ContentText.English;
        var display = new SummaryLineText(ContentText.English, Region1Catalog.Create());
        Assert.Equal(english.Items.Select(r => display.Display(r.Text)), s.GetPendingReturnSummary()!.Items.Select(r => r.DisplayText));
        Assert.Equal(english.Items.Select(r => r.Text), s.GetPendingReturnSummary()!.Items.Select(r => display.Localize(r.Text)));
    }

    [Theory]
    [InlineData("3 restoration projects are complete.", "[xx:restorations-complete] 3")]
    [InlineData("Vitality credited: 1,234.", "[xx:vitality-credited] 1,234")]
    [InlineData("The settlement has reached stage 2.", "[xx:settlement-stage] 2")]
    [InlineData("Queue the next restoration project.", "[xx:next-action-queue] {0}")]
    [InlineData("Something nobody composed.", "Something nobody composed.")]
    [InlineData("Unknown Place is complete.", "[xx:project-complete] Unknown Place")]
    public void Forms_MapToPatterns_AndUnknownLinesFallBackToStoredEnglish(string stored, string expected)
    {
        var t = new SummaryLineText(Pseudo(), Region1Catalog.Create());
        var got = t.Localize(stored);
        if (expected.Contains("{0}")) Assert.StartsWith("[xx:next-action-queue]", got);
        else Assert.Equal(expected, got);
    }

    [Fact]
    public void ProjectTitles_AreMappedBackToTheirStableId_AndLocalized()
    {
        var c = Region1Catalog.Create();
        var p = c.Projects[0];
        var t = new SummaryLineText(Pseudo(), c);
        Assert.Equal("[xx:project-complete] [xx] " + p.TitleKey, t.Localize(p.TitleKey + " is complete."));
    }

    [Fact]
    public void BrokenPattern_FallsBackToEnglishForm_AndMissingPatternKeepsStoredBytes()
    {
        var c = Region1Catalog.Create();
        var d = new Dictionary<string, string> { [ContentText.Key(SummaryLineText.PatternId("vitality-credited"), SummaryLineText.Field)] = "{9}" };
        var t = new SummaryLineText(ContentText.ForLocale("xx", d), c);
        Assert.Equal("Vitality credited: 5.", t.Localize("Vitality credited: 5."));
        Assert.Equal("Producers delivered 7 units.", t.Localize("Producers delivered 7 units."));
    }
}
