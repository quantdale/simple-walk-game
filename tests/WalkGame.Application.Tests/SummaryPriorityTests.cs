using System;
using System.Collections.Generic;
using System.Linq;
using WalkGame.Application.Content;
using WalkGame.Application.ReadModels;
using WalkGame.Application.Summaries;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Simulation;
using WalkGame.Domain.Summaries;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-163 (KI-026): when the Transformation bucket (5) is full, a project's own completion line is
/// never retired in favour of a lower-value line from the same batch; older batches still rotate
/// out first (D-055). Plus the display-only pruning of lines that repeat a shown completion.
/// </summary>
public sealed class SummaryPriorityTests
{
    private static readonly RegionDefinition Content = Region1Catalog.Create();
    private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    private static List<string> Transformations(PendingReturnSummaryState s) =>
        s.Items.Where(i => i.Kind == SummaryItemKind.Transformation).Select(i => i.Text).ToList();

    /// <summary>The closure day seen in D-159 (cheapest-first): more facts than the 5 slots.</summary>
    private static SimulationEvent[] CrowdedClosureDay() => new SimulationEvent[]
    {
        new ProjectCompleted(T0, "proj.canopy-walkway"),
        new LandmarkStageReached(T0, "lm.river-intake", RestorationStage.Restored),
        new LandmarkStageReached(T0, "lm.canopy", RestorationStage.Flourishing),
        new RegionProgressionAdvanced(T0, RegionProgressionAxis.Ecology, 4),
        new StoryChapterUnlocked(T0, "story.above-the-trees", 7),
        new ProjectCompleted(T0, "proj.complete-valley-survey"),
        new LandmarkStageReached(T0, "lm.observatory", RestorationStage.Restored),
        new StoryChapterUnlocked(T0, "story.every-mile-mapped", 8),
        new RegionCompleted(T0, "proj.complete-valley-survey"),
    };

    [Fact]
    public void ACrowdedDay_KeepsEveryCompletionLine_AndDropsTheLandmarkAndArcLinesFirst()
    {
        var s = ReturnSummaryComposer.Compose(CrowdedClosureDay(), Content, null, T0, regionCompleted: true);
        var kept = Transformations(s);
        Assert.Equal(5, kept.Count);
        Assert.Contains("Raise the canopy walkway is complete.", kept);
        Assert.Contains("Complete the valley survey is complete.", kept);
        Assert.Contains("Millbrook Valley's restoration is complete.", kept);
        Assert.Contains("New chapter: Above the Trees.", kept);
        Assert.Contains("New chapter: Every Mile Mapped.", kept);
        Assert.DoesNotContain(kept, t => ReturnSummaryComposer.RetentionRank(t) >= 4);
    }

    [Fact]
    public void Composition_IsDeterministic_AndOrderIndependentOfRanking()
    {
        var a = ReturnSummaryComposer.Compose(CrowdedClosureDay(), Content, null, T0, true);
        var b = ReturnSummaryComposer.Compose(CrowdedClosureDay(), Content, null, T0, true);
        Assert.Equal(a.Items.Select(i => i.Kind + i.Text), b.Items.Select(i => i.Kind + i.Text));
        // Kept lines stay in composer order (D-055 presentation order is unchanged).
        var kept = Transformations(a);
        Assert.True(kept.IndexOf("Raise the canopy walkway is complete.") < kept.IndexOf("Complete the valley survey is complete."));
    }

    [Fact]
    public void OlderBatches_StillRotateOutFirst_EvenWhenTheyAreCompletionLines()
    {
        // Yesterday: five completion lines fill the bucket.
        var day1 = new SimulationEvent[]
        {
            new ProjectCompleted(T0, "proj.clear-trailhead"), new ProjectCompleted(T0, "proj.rebuild-trail-bridges"),
            new ProjectCompleted(T0, "proj.river-intake"), new ProjectCompleted(T0, "proj.clear-reservoir"),
            new ProjectCompleted(T0, "proj.build-workshop"),
        };
        var s1 = ReturnSummaryComposer.Compose(day1, Content, null, T0);
        Assert.Equal(5, Transformations(s1).Count(t => ReturnSummaryComposer.RetentionRank(t) == 1));

        // Today: a single landmark line still appears, retiring yesterday's oldest line (D-055).
        var t1 = T0.AddDays(1);
        var s2 = ReturnSummaryComposer.Compose(new SimulationEvent[] { new LandmarkStageReached(t1, "lm.wetland", RestorationStage.Stabilized) }, Content, s1, t1);
        var kept = Transformations(s2);
        Assert.Contains("East Wetland is safe to approach.", kept);
        Assert.DoesNotContain("Clear the old trailhead is complete.", kept);
        Assert.Equal(5, kept.Count);
    }

    [Fact]
    public void OtherKinds_RotateExactlyAsBefore()
    {
        var events = Enumerable.Range(0, 3).Select(i => (SimulationEvent)new ProducerUnlocked(T0, Content.Producers[i].Id.Value)).ToArray();
        var s = ReturnSummaryComposer.Compose(events, Content, null, T0);
        var production = s.Items.Where(i => i.Kind == SummaryItemKind.Production).Select(i => i.Text).ToList();
        Assert.Equal(2, production.Count);
        Assert.DoesNotContain(Content.Producers[0].TitleKey + " is now operating.", production); // oldest retired
    }

    [Theory]
    [InlineData("Millbrook Valley's restoration is complete.", 0)]
    [InlineData("Clear the old trailhead is complete.", 1)]
    [InlineData("New chapter: The Way In.", 2)]
    [InlineData("Valley Transect expedition complete (+50 Vitality).", 3)]
    [InlineData("3 restoration projects are complete.", 4)]
    [InlineData("The settlement is lit: the square glows after dark.", 5)]
    [InlineData("The valley's ecology has reached stage 9.", 5)]
    [InlineData("Old Trailhead is safe to approach.", 6)]
    [InlineData("Something else.", 3)]
    public void RetentionRanks(string text, int rank) => Assert.Equal(rank, ReturnSummaryComposer.RetentionRank(text));

    // ---- display-only pruning ----

    private static SummaryDisplayFilter.Item I(string t, bool current = true) => new SummaryDisplayFilter.Item(t, current);

    [Fact]
    public void ALandmarkLine_RepeatedByAShownCompletionDetail_IsNotRendered()
    {
        var items = new[]
        {
            I("Complete the valley survey is complete."),
            I(GlancePhrases.FormatLandmark("Ridge Observatory", RestorationStage.Restored)),
            I(GlancePhrases.FormatLandmark("East Wetland", RestorationStage.Stabilized)),
        };
        var hidden = SummaryDisplayFilter.Redundant(Content, items, null);
        Assert.Equal(new HashSet<int> { 1 }, hidden);
        // The detail line under the completion still names it, so nothing is lost.
        Assert.StartsWith("Ridge Observatory is now restored.", ProjectCompletionMoments.Describe(Content, items[0].Text));
    }

    [Fact]
    public void AnEarlierCompletion_DoesNotHideTheLandmarkLine()
    {
        var items = new[] { I("Complete the valley survey is complete.", current: false), I("Ridge Observatory is restored.") };
        Assert.Empty(SummaryDisplayFilter.Redundant(Content, items, null));
    }

    [Fact]
    public void AReadyLine_ForAnAlreadyRestoredProject_IsNotRendered()
    {
        var items = new[] { I("Complete the valley survey is ready to queue.", false), I("Calibrate the survey rig is ready to queue.") };
        var hidden = SummaryDisplayFilter.Redundant(Content, items, new HashSet<string> { "proj.complete-valley-survey" });
        Assert.Equal(new HashSet<int> { 0 }, hidden);
    }
}
