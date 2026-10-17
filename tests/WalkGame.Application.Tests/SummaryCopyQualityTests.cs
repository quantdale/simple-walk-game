using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WalkGame.Application.Content;
using WalkGame.Application.ReadModels;
using WalkGame.Application.Summaries;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Simulation;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-162: copy-quality sweep. Every generated line that interpolates a content title is rendered
/// for every title of every kind it can carry (19 projects, 13 discoveries, 3 expeditions, the
/// chapters, landmarks, producers and the region), through the real composer and the display
/// path the player sees, and checked against known-bad patterns: a doubled word ("Route route"),
/// an imperative project title used as a sentence subject ("Complete the valley survey is
/// complete."), doubled punctuation, raw ids and unfilled placeholders.
/// </summary>
public sealed class SummaryCopyQualityTests
{
    private static readonly RegionDefinition Content = Region1Catalog.Create();
    private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);
    private static readonly SummaryLineText EnglishDisplay = new SummaryLineText(ContentText.English, Content);

    private static readonly Regex DoubledWord = new Regex(@"\b(\w+)\s+\1\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex DoubledPunctuation = new Regex(@"[.!?,;:]\s*[.!?,;:]|\s[.,;:!?]|\s{2,}", RegexOptions.CultureInvariant);
    private static readonly Regex RawId = new Regex(@"\b(proj|exp|disc|lm|prd|story|region|care|chain)\.[a-z]|[{}]", RegexOptions.CultureInvariant);

    /// <summary>Every problem found in one rendered line (empty when it reads well).</summary>
    public static List<string> Problems(string line)
    {
        var found = new List<string>();
        if (string.IsNullOrWhiteSpace(line)) { found.Add("blank"); return found; }
        var d = DoubledWord.Match(line);
        if (d.Success) found.Add("doubled word '" + d.Value + "'");
        var p = DoubledPunctuation.Match(line);
        if (p.Success) found.Add("bad punctuation/spacing '" + p.Value + "'");
        if (RawId.IsMatch(line)) found.Add("raw id or placeholder");
        foreach (var project in Content.Projects)
            foreach (var verb in new[] { " is ", " are ", " opens ", " continues ", " was " })
                if (line.IndexOf(project.TitleKey + verb, StringComparison.Ordinal) >= 0)
                    found.Add("imperative title as subject: '" + project.TitleKey + verb.TrimEnd() + "'");
        return found;
    }

    private static IEnumerable<string> Composed(SimulationEvent evt) =>
        ReturnSummaryComposer.Compose(new[] { evt }, Content, null, T0).Items.Select(i => i.Text);

    /// <summary>Every composer line for every title it can carry, as stored (before display).</summary>
    public static IEnumerable<(string Template, string Title, string Stored)> StoredLines()
    {
        foreach (var p in Content.Projects)
        {
            foreach (var l in Composed(new ProjectCompleted(T0, p.Id.Value))) yield return ("project-complete", p.TitleKey, l);
            foreach (var l in Composed(new ProjectBecameAvailable(T0, p.Id.Value))) yield return ("project-ready", p.TitleKey, l);
        }
        foreach (var e in Content.Expeditions)
        {
            foreach (var l in Composed(new ExpeditionAvailable(T0, e.Id.Value))) yield return ("expedition-ready", e.TitleKey, l);
            foreach (var l in Composed(new ExpeditionCompleted(T0, e.Id.Value, null, 0L))) yield return ("expedition-complete", e.TitleKey, l);
            foreach (var l in Composed(new ExpeditionCompleted(T0, e.Id.Value, ResourceType.Materials, 40L))) yield return ("expedition-reward", e.TitleKey, l);
        }
        foreach (var d in Content.Discoveries)
            foreach (var l in Composed(new DiscoveryUnlocked(T0, d.Id.Value))) yield return ("discovery-recorded", d.TitleKey, l);
        foreach (var c in Content.Chapters)
            foreach (var l in Composed(new StoryChapterUnlocked(T0, c.Id, c.Number))) yield return ("new-chapter", c.TitleKey, l);
        foreach (var lm in Content.Landmarks)
            foreach (RestorationStage stage in Enum.GetValues(typeof(RestorationStage)))
                foreach (var l in Composed(new LandmarkStageReached(T0, lm.Id.Value, stage))) yield return ("landmark-" + stage, lm.TitleKey, l);
        foreach (var prd in Content.Producers)
        {
            foreach (var l in Composed(new ProducerUnlocked(T0, prd.Id.Value))) yield return ("producer-operating", prd.TitleKey, l);
            foreach (var l in Composed(new ProducerProduced(T0, prd.Id.Value, 0L, false, hitResourceCap: true))) yield return ("producer-capped", prd.TitleKey, l);
            foreach (var l in Composed(new ProducerProduced(T0, prd.Id.Value, 0L, true))) yield return ("producer-store-filled", prd.TitleKey, l);
        }
        foreach (var l in Composed(new RegionCompleted(T0, Content.CompletionMilestoneProjectId ?? "proj.complete-valley-survey"))) yield return ("region-complete", Content.TitleKey, l);
    }

    [Fact]
    public void TheChecker_RejectsTheKnownBadLines()
    {
        Assert.NotEmpty(Problems("Complete the valley survey is complete."));
        Assert.NotEmpty(Problems("Source-to-Sound Route route is ready."));
        Assert.NotEmpty(Problems("Clear the old trailhead is ready to queue."));
        Assert.NotEmpty(Problems("New chapter: The Way In.."));
        Assert.NotEmpty(Problems("Rebuild the trail bridges is underway (1 of 2)."));
        Assert.Empty(Problems("The valley survey is complete."));
    }

    [Fact]
    public void EveryTemplate_ForEveryTitle_ReadsWell_InTheDisplayForm()
    {
        var lines = StoredLines().ToList();
        // Coverage: every title of every kind was rendered (19 projects x2, 3 expeditions x3, 13 discoveries, ...).
        Assert.Equal(Content.Projects.Count * 2, lines.Count(l => l.Template.StartsWith("project-", StringComparison.Ordinal)));
        Assert.Equal(Content.Expeditions.Count * 3, lines.Count(l => l.Template.StartsWith("expedition-", StringComparison.Ordinal)));
        Assert.Equal(Content.Discoveries.Count, lines.Count(l => l.Template == "discovery-recorded"));
        Assert.Equal(Content.Chapters.Count, lines.Count(l => l.Template == "new-chapter"));
        Assert.Contains(lines, l => l.Template == "region-complete");

        var bad = new List<string>();
        foreach (var (template, title, stored) in lines)
        {
            string shown = EnglishDisplay.Display(stored);
            foreach (var problem in Problems(shown)) bad.Add($"{template} x \"{title}\": \"{shown}\" ({problem})");
        }
        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }

    [Fact]
    public void StoredLines_AreUnchanged_SoSavesAndParsingKeepWorking()
    {
        // The display form never leaks into storage: the composer still writes the D-055 forms.
        var survey = Content.Projects.Single(p => p.Id.Value == "proj.complete-valley-survey");
        Assert.Contains("Complete the valley survey is complete.", Composed(new ProjectCompleted(T0, survey.Id.Value)));
        Assert.Equal("The valley survey is complete.", EnglishDisplay.Display("Complete the valley survey is complete."));
        // Landmark-stage detail still keys off the stored line.
        Assert.NotNull(ProjectCompletionMoments.Describe(Content, "Complete the valley survey is complete."));
    }

    [Fact]
    public void EveryProject_HasAnAuthoredCompletionSentence()
    {
        var authored = Region1SummaryCopy.ProjectIds.ToHashSet();
        foreach (var p in Content.Projects)
        {
            Assert.Contains(p.Id.Value, authored);
            string line = Region1SummaryCopy.CompletedLine(p.Id.Value)!;
            Assert.EndsWith(".", line);
            Assert.True(char.IsUpper(line[0]), line);
        }
        Assert.Equal(Content.Projects.Count, authored.Count);
    }

    [Fact]
    public void UnknownTitles_AndNotices_StayAsStored()
    {
        Assert.Equal("Backup recovery is complete.", EnglishDisplay.Display("Backup recovery is complete."));
        Assert.Equal("Unknown Place is ready to queue.", EnglishDisplay.Display("Unknown Place is ready to queue."));
        Assert.Equal("Millbrook Valley's restoration is complete.", EnglishDisplay.Display("Millbrook Valley's restoration is complete."));
    }

    [Fact]
    public void ALocale_CanOverrideACompletionSentence_AndKeepsItsOwnPatterns()
    {
        var overlay = new Dictionary<string, string>
        {
            [ContentText.Key("proj.clear-trailhead", ContentText.Fields.Completed)] = "[xx] trailhead done",
            [ContentText.Key(SummaryLineText.PatternId("project-ready"), SummaryLineText.Field)] = "[xx] ready {0}",
        };
        var t = new SummaryLineText(ContentText.ForLocale("xx", overlay), Content);
        Assert.Equal("[xx] trailhead done", t.Display("Clear the old trailhead is complete."));
        Assert.Equal("[xx] ready Clear the old trailhead", t.Display("Clear the old trailhead is ready to queue."));
        // No per-project entry and no pattern: the English authored sentence.
        Assert.Equal("The old reservoir is clear.", t.Display("Clear the old reservoir is complete."));
    }

    [Fact]
    public void GlancePhrases_ForEveryProject_ReadWell()
    {
        var bad = new List<string>();
        foreach (var p in Content.Projects)
            foreach (var line in new[]
            {
                string.Format(GlanceProjector.UnderwayEnglish, p.TitleKey, 40, 300),
                string.Format(GlanceProjector.NextUpEnglish, p.TitleKey),
                p.TitleKey + ": 40% restored. About 1,000 more steps to finish it.",
            })
                foreach (var problem in Problems(line)) bad.Add($"\"{line}\" ({problem})");
        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }

    [Fact]
    public void CompletionMoments_ForEveryProject_ReadWell()
    {
        var all = new HashSet<string>(Content.Chapters.Select(c => c.Id));
        var bad = new List<string>();
        foreach (var p in Content.Projects)
            foreach (var unlocked in new[] { null, all })
            {
                var line = ProjectCompletionMoments.Describe(Content, p.TitleKey + " is complete.", unlocked);
                if (line == null) continue;
                foreach (var problem in Problems(line)) bad.Add($"{p.Id.Value}: \"{line}\" ({problem})");
                if (line.Split(", and ").Length > 2) bad.Add($"{p.Id.Value}: \"{line}\" (repeated ', and')");
            }
        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }

    [Theory]
    [InlineData(new[] { "A" }, "A")]
    [InlineData(new[] { "A", "B" }, "A and B")]
    [InlineData(new[] { "A", "B", "C" }, "A, B, and C")]
    public void LandmarkLists_JoinNaturally(string[] items, string expected) =>
        Assert.Equal(expected, ProjectCompletionMoments.JoinList(items));
}
