using System;
using System.Collections.Generic;
using WalkGame.Application.Content;
using WalkGame.Application.ReadModels;
using WalkGame.Application.Summaries;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>D-140 (Region 1 narrative polish): ecology/settlement progress reads as authored story, not "stage N".</summary>
public sealed class ProgressionNarrativeTests
{
    [Fact]
    public void EveryAuthoredStage_HasACalmSentence_WithNoNumberOrStageWord()
    {
        var c = Region1Catalog.Create();
        Assert.Equal(c.EcologyProgression.Stages.Count, ReturnSummaryComposer.EcologyLines.Count);
        Assert.Equal(c.SettlementProgression.Stages.Count, ReturnSummaryComposer.SettlementLines.Count);
        foreach (var eco in new[] { true, false })
            for (int st = 1; st <= 4; st++)
            {
                var line = ReturnSummaryComposer.ProgressionLine(eco, st);
                Assert.DoesNotContain("stage", line, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotMatch(@"\d", line);
                Assert.EndsWith(".", line);
                Assert.True(line.Length <= 90, line);
                Assert.Equal(line, GlancePhrases.PlayerLeadFor(line)); // the glance shows it, not "The valley changed."
            }
    }

    [Fact]
    public void AStageOutsideTheTable_KeepsTheOldNumberedForm()
    {
        Assert.Equal("The settlement has reached stage 9.", ReturnSummaryComposer.ProgressionLine(false, 9));
        Assert.Equal("The valley's ecology has reached stage 0.", ReturnSummaryComposer.ProgressionLine(true, 0));
    }

    [Fact]
    public void AuthoredSentences_TakeWholeSentenceOverlayEntries_AndHistoricalLinesStillLocalize()
    {
        var d = new Dictionary<string, string>
        {
            [ContentText.Key(SummaryLineText.PatternId("ecology.2"), SummaryLineText.Field)] = "[xx] eco 2",
            [ContentText.Key(SummaryLineText.PatternId("settlement-stage"), SummaryLineText.Field)] = "[xx] settle {0}",
        };
        var t = new SummaryLineText(ContentText.ForLocale("xx", d), Region1Catalog.Create());
        Assert.Equal("[xx] eco 2", t.Localize(ReturnSummaryComposer.ProgressionLine(true, 2)));
        Assert.Equal(ReturnSummaryComposer.ProgressionLine(false, 1), t.Localize(ReturnSummaryComposer.ProgressionLine(false, 1)));
        Assert.Equal("[xx] settle 2", t.Localize("The settlement has reached stage 2."));
    }
}
