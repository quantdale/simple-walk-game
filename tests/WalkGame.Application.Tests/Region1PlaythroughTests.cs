using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.Pacing;
using WalkGame.Application.Persistence;
using WalkGame.Application.ReadModels;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Application.Ux;
using WalkGame.Domain;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-159: a whole Region 1 playthrough, onboarding to epilogue, as a player experiences it.
///
/// The pacing CLI (D-039/D-062) proves closure and per-chain timing for one fixed queue
/// policy. This suite closes the gaps it leaves: it starts at the first onboarding screen,
/// plays every day through a fresh session loaded from disk, reads and dismisses every return
/// summary, and keeps walking after completion until the last care chapter and the last
/// epilogue note. It then asserts that every chapter, discovery, letter, expedition and
/// project choice was REACHED and ANNOUNCED, for low, irregular and high walkers under three
/// different project-choice policies, and that closure lands inside the documented targets.
/// Nothing writes canonical state directly; every change flows through the session API.
/// </summary>
public sealed class Region1PlaythroughTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private const ulong Seed = 42UL;

    /// <summary>
    /// D-159 reachability tripwire for the D-039-exempt low profile (3,000 steps/day). It is
    /// NOT a balance target (low has none; its long tail is accepted). It only catches content
    /// that makes the region unreachable for a sustained light walker, measured at day 510.
    /// </summary>
    private const int LowReachabilityTripwireDays = 730;

    /// <summary>Last epilogue note reveals 7 × 30 days after completion (Region1Epilogue).</summary>
    /// <summary>D-055 per-kind bound for Transformation lines in one return summary.</summary>
    private const int TransformationBound = 5;

    private static readonly int EpilogueFullDays =
        Region1Epilogue.ValleyNotes.Max(n => n.DayAfterCompletion);

    private readonly List<TempDirectory> _temps = new();
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public Region1PlaythroughTests(Xunit.Abstractions.ITestOutputHelper output) => _output = output;

    public void Dispose()
    {
        foreach (var t in _temps) t.Dispose();
    }

    public enum Policy { Catalog, Cheapest, DeepestFirst }

    private static long[] Pattern(string profile) => profile switch
    {
        "low" => new[] { 3000L },
        "high" => new[] { 20000L },
        "irregular" => new[] { 26000L, 15000L, 2000L, 18000L, 6000L, 22000L, 9000L },
        _ => throw new ArgumentOutOfRangeException(nameof(profile)),
    };

    public static IEnumerable<object[]> Cases()
    {
        foreach (var profile in new[] { "low", "irregular", "high" })
            foreach (Policy policy in Enum.GetValues(typeof(Policy)))
                yield return new object[] { profile, policy };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Region1_FromOnboardingToEpilogue_ReachesAndAnnouncesEverything(string profile, Policy policy)
    {
        var content = Region1Catalog.Create();
        var run = Play(profile, policy, content);

        // ---- Closure inside the documented target (D-039 / D-062 PacingContract). ----
        Assert.True(run.CompletionDay > 0, $"{profile}/{policy}: region never completed");
        var (mustClose, maxDays) = PacingContract.Target(profile);
        if (mustClose)
            Assert.True(run.CompletionDay <= maxDays,
                $"{profile}/{policy}: closed on day {run.CompletionDay}, target ≤ {maxDays}");
        else
            Assert.True(run.CompletionDay <= LowReachabilityTripwireDays,
                $"{profile}/{policy}: closed on day {run.CompletionDay}, tripwire {LowReachabilityTripwireDays}");

        // ---- Every project was a real choice: offered, chosen, completed. ----
        Assert.Equal(content.Projects.Count, run.Offered.Count);
        Assert.Equal(content.Projects.Count, run.Chosen.Count);
        Assert.Equal(content.Projects.Count, run.Completed.Count);
        // Announced by its own line, unless that day's summary hit the D-055 Transformation
        // bound and rotated the line out in favour of the same day's later facts (the
        // chapter, arc and landmark lines that tell the same story; KI-026).
        foreach (var p in content.Projects)
        {
            string line = Title(p.TitleKey) + " is complete.";
            int doneDay = run.CompletedDay[p.Id.Value];
            Assert.True(run.SummaryLines.Contains(line) || run.TransformationsOnDay[doneDay] == TransformationBound,
                $"{profile}/{policy}: '{line}' was never shown and day {doneDay} was not saturated");
            if (!run.SummaryLines.Contains(line)) run.RotatedOutCompletions.Add(p.Id.Value);
        }
        _output.WriteLine($"PLAYTHROUGH {profile}/{policy}: closed day {run.CompletionDay}, "
            + $"care chapter day {run.FirstSeenDay.GetValueOrDefault("New chapter: The Long Watch.")}, "
            + $"completion lines rotated out: {(run.RotatedOutCompletions.Count == 0 ? "none" : string.Join(",", run.RotatedOutCompletions))}");
        Assert.True(run.RotatedOutCompletions.Count <= 2,
            $"{profile}/{policy}: too many completions rotated out: {string.Join(",", run.RotatedOutCompletions)}");

        // ---- Every chapter unlocked and announced; the care chapter only after closure. ----
        var story = run.FinalStory;
        Assert.Equal(story.TotalChapters, story.UnlockedCount);
        foreach (var chapter in story.Chapters.Where(c => c.Number > 0))
            Assert.Contains("New chapter: " + chapter.TitleKey + ".", run.SummaryLines);
        var care = story.Chapters.Single(c => c.MinCareCycles > 0);
        Assert.True(run.FirstSeenDay["New chapter: " + care.TitleKey + "."] > run.CompletionDay,
            "the care chapter must follow the region's completion");

        // ---- Every discovery recorded and announced. ----
        Assert.Equal(content.Discoveries.Count, run.FinalDiscoveries.UnlockedCount);
        foreach (var d in content.Discoveries)
            Assert.Contains("Discovery recorded: " + Title(d.TitleKey) + ".", run.SummaryLines);

        // ---- Every expedition offered and completed, both announced. ----
        Assert.Equal(content.Expeditions.Count, run.FinalExpeditions.CompletedCount);
        foreach (var e in content.Expeditions)
        {
            Assert.Contains(Title(e.TitleKey) + " route is ready.", run.SummaryLines);
            Assert.Contains(run.SummaryLines, l => l.StartsWith(Title(e.TitleKey) + " expedition complete", StringComparison.Ordinal));
        }

        // ---- Every letter found; the full epilogue revealed. ----
        Assert.Equal(run.FinalLetters.TotalLetters, run.FinalLetters.FoundCount);
        Assert.True(run.FinalEpilogue.Available);
        Assert.NotNull(run.FinalEpilogue.JournalEntry);
        Assert.Equal(Region1Epilogue.ClosingLetters.Count, run.FinalEpilogue.ClosingLetters.Count);
        Assert.Equal(Region1Epilogue.ValleyNotes.Count, run.FinalEpilogue.ValleyNotes.Count);
        Assert.Contains("Millbrook Valley's restoration is complete.", run.SummaryLines);

        // ---- Both arcs topped out, nothing left to do, canonical state clean. ----
        Assert.Equal(content.EcologyProgression.Stages.Count, run.FinalState.Region.EcologyStage);
        Assert.Equal(content.SettlementProgression.Stages.Count, run.FinalState.Region.SettlementStage);
        Assert.True(run.FinalState.Region.PostCompletionCyclesCompleted >= care.MinCareCycles);
        Assert.Empty(GameStateValidator.Validate(run.FinalState, content));
        Assert.True(run.OnboardingCompletedOnDay0);
    }

    /// <summary>
    /// The policies are different routes through the same valley: they must not all collapse
    /// into the catalog order, or the "every choice" claim proves nothing.
    /// </summary>
    [Fact]
    public void ChoicePolicies_TakeGenuinelyDifferentRoutes()
    {
        var content = Region1Catalog.Create();
        var orders = Enum.GetValues(typeof(Policy)).Cast<Policy>()
            .Select(p => string.Join(",", Play("high", p, content).Chosen))
            .ToList();
        Assert.Equal(orders.Count, orders.Distinct().Count());
    }

    /// <summary>
    /// D-159 regression: the survey is not the whole valley. A player who funds the survey
    /// before the canopy walkway reaches the survey with a chain unbuilt; the region must NOT
    /// close there (it used to: celebration, epilogue and care with three chapters locked).
    /// </summary>
    [Fact]
    public void SurveyBeforeCanopy_DoesNotCloseTheRegion_UntilEveryChainIsRestored()
    {
        var content = Region1Catalog.Create();
        var temp = new TempDirectory();
        _temps.Add(temp);
        var survey = content.ClosureCriterion!.MilestoneProjectId;
        const string Canopy = "proj.canopy-walkway";

        PlayUntilSurveyWithoutCanopy(temp.Path, content, out int day);

        var session = Session(temp.Path, T0.AddDays(day));
        Assert.Equal(StartStatus.Loaded, session.Continue().Status);
        Assert.Equal(ProjectStatus.Completed, session.GetProjects().Projects.Single(r => r.ProjectId == survey).Status);
        Assert.False(session.GetHome().RegionComplete);
        Assert.False(session.GetEpilogue().Available);
        Assert.Equal(ProjectStatus.Available, session.GetProjects().Projects.Single(r => r.ProjectId == Canopy).Status);
        Assert.Equal(content.Chains.Count - 1, content.ChainsComplete(ReadState(temp.Path).Region));
        Assert.Empty(GameStateValidator.Validate(ReadState(temp.Path), content));
    }

    /// <summary>
    /// D-159 save compatibility: a save that closed under the pre-D-159 milestone-only rule
    /// (survey done, canopy unbuilt, region recorded complete) still loads, stays complete,
    /// and can still fund the canopy walkway and reach every chapter.
    /// </summary>
    [Fact]
    public void PreD159ClosedSave_WithCanopyUnbuilt_StillLoads_AndCanFinishTheValley()
    {
        var content = Region1Catalog.Create();
        var temp = new TempDirectory();
        _temps.Add(temp);
        const string Canopy = "proj.canopy-walkway";

        PlayUntilSurveyWithoutCanopy(temp.Path, content, out int day);

        // Emulate the old closure: the advancer of that build set the flag on the survey alone.
        var legacy = ReadState(temp.Path);
        legacy.Region.IsCompleted = true;
        legacy.Region.RegionCompletedAtUtc = T0.AddDays(day);
        var bytes = TestSessions.NewCodec().Encode(legacy, T0.AddDays(day));
        File.WriteAllBytes(Path.Combine(temp.Path, "save.json"), bytes);
        Assert.Empty(GameStateValidator.Validate(ReadState(temp.Path), content));

        var session = Session(temp.Path, T0.AddDays(day));
        Assert.Equal(StartStatus.Loaded, session.Continue().Status);
        Assert.True(session.GetHome().RegionComplete);
        Assert.True(session.EnqueueProject(Canopy).IsSuccess);

        for (int i = 1; i <= 120 && session.GetStory().UnlockedCount < session.GetStory().TotalChapters; i++)
        {
            day++;
            session = Session(temp.Path, T0.AddDays(day));
            Assert.Equal(StartStatus.Loaded, session.Continue().Status);
            session.IngestFromSource(new SyntheticWalkingSource(20000L), T0.AddDays(day - 1), T0.AddDays(day));
        }

        Assert.Equal(ProjectStatus.Completed, session.GetProjects().Projects.Single(r => r.ProjectId == Canopy).Status);
        Assert.Equal(session.GetStory().TotalChapters, session.GetStory().UnlockedCount);
        Assert.True(session.GetHome().RegionComplete);
        Assert.Empty(GameStateValidator.Validate(ReadState(temp.Path), content));
    }

    /// <summary>High walker, cheapest-first, never choosing the canopy: stops once the survey completes.</summary>
    private static void PlayUntilSurveyWithoutCanopy(string directory, WalkGame.Domain.Regions.RegionDefinition content, out int day)
    {
        var survey = content.ClosureCriterion!.MilestoneProjectId;
        var first = Session(directory, T0);
        Assert.Equal(StartStatus.NewGameCreated, first.StartNewGame(Seed, overwriteExisting: false).Status);
        day = 0;
        while (true)
        {
            day++;
            Assert.True(day <= 200, "survey never completed");
            var session = Session(directory, T0.AddDays(day));
            Assert.Equal(StartStatus.Loaded, session.Continue().Status);
            session.IngestFromSource(new SyntheticWalkingSource(20000L), T0.AddDays(day - 1), T0.AddDays(day));
            var rows = session.GetProjects().Projects;
            if (rows.Single(r => r.ProjectId == survey).Status == ProjectStatus.Completed)
            {
                Assert.NotEqual(ProjectStatus.Completed, rows.Single(r => r.ProjectId == "proj.canopy-walkway").Status);
                return;
            }
            var home = session.GetHome();
            if (home.ActiveProjectId != null || home.Queued.Count > 0) continue;
            foreach (var row in rows.Where(r => r.Status == ProjectStatus.Available && r.ProjectId != "proj.canopy-walkway")
                         .OrderBy(r => r.VitalityCost).ThenBy(r => r.ProjectId, StringComparer.Ordinal))
                if (session.EnqueueProject(row.ProjectId).IsSuccess) break;
        }
    }

    // ------------------------------------------------------------------

    private sealed class Run
    {
        public int CompletionDay;
        public bool OnboardingCompletedOnDay0;
        public readonly HashSet<string> Offered = new(StringComparer.Ordinal);
        public readonly List<string> Chosen = new();
        public readonly HashSet<string> Completed = new(StringComparer.Ordinal);
        public readonly HashSet<string> SummaryLines = new(StringComparer.Ordinal);
        public readonly Dictionary<string, int> FirstSeenDay = new(StringComparer.Ordinal);
        public readonly Dictionary<string, int> CompletedDay = new(StringComparer.Ordinal);
        public readonly Dictionary<int, int> TransformationsOnDay = new();
        public readonly List<string> RotatedOutCompletions = new();
        public StoryReadModel FinalStory = null!;
        public DiscoveriesReadModel FinalDiscoveries = null!;
        public ExpeditionsReadModel FinalExpeditions = null!;
        public LettersReadModel FinalLetters = null!;
        public EpilogueReadModel FinalEpilogue = null!;
        public HomeReadModel FinalHome = null!;
        public GameState FinalState = null!;
    }

    private Run Play(string profile, Policy policy, WalkGame.Domain.Regions.RegionDefinition content)
    {
        var temp = new TempDirectory();
        _temps.Add(temp);
        var pattern = Pattern(profile);
        var run = new Run();

        // ---- Day 0: onboarding, exactly as the shell drives it. ----
        var first = Session(temp.Path, T0);
        Assert.Equal(StartStatus.NewGameCreated, first.StartNewGame(Seed, overwriteExisting: false).Status);
        foreach (var stage in new[] { OnboardingStage.Premise, OnboardingStage.WorldBaseline,
                     OnboardingStage.ActivityConnection, OnboardingStage.FirstProject })
            Assert.True(first.AdvanceOnboarding(stage).IsSuccess);
        Assert.Equal(OnboardingNextAction.ChooseFirstProject, first.GetOnboarding().NextAction);
        Observe(first, run, 0);
        Choose(first, policy, run);
        Assert.True(first.GetOnboarding().FirstProjectChosen);
        foreach (var stage in new[] { OnboardingStage.Simulation, OnboardingStage.Exit, OnboardingStage.Complete })
            Assert.True(first.AdvanceOnboarding(stage).IsSuccess);
        run.OnboardingCompletedOnDay0 = first.GetOnboarding().IsComplete;

        // ---- Daily play until closure, then until the care chapter and the last note. ----
        int completionCap = Math.Max(PacingContract.Target(profile).MaxDays, LowReachabilityTripwireDays);
        int day = 0;
        GameSession session = first;
        while (true)
        {
            day++;
            var windowStart = T0.AddDays(day - 1);
            var windowEnd = T0.AddDays(day);
            session = Session(temp.Path, windowEnd);
            Assert.Equal(StartStatus.Loaded, session.Continue().Status);
            Assert.True(session.GetOnboarding().IsComplete);

            long steps = pattern[(day - 1) % pattern.Length];
            var ingest = session.IngestFromSource(new SyntheticWalkingSource(steps), windowStart, windowEnd);
            Assert.True(ingest.Saved);

            Observe(session, run, day);
            var home = session.GetHome();
            if (!home.RegionComplete && home.ActiveProjectId == null && home.Queued.Count == 0)
                Choose(session, policy, run);

            home = session.GetHome();
            if (home.RegionComplete && run.CompletionDay == 0)
                run.CompletionDay = day;

            if (run.CompletionDay == 0)
            {
                Assert.True(day <= completionCap, $"{profile}/{policy}: no closure by day {day}");
                continue;
            }

            bool careDone = session.GetStory().UnlockedCount == session.GetStory().TotalChapters;
            bool epilogueDone = day - run.CompletionDay >= EpilogueFullDays + 1;
            if (careDone && epilogueDone)
                break;
            Assert.True(day - run.CompletionDay <= 400, $"{profile}/{policy}: post-completion content stalled: " + string.Join(",", session.GetStory().Chapters.Where(c => !c.Unlocked).Select(c => c.ChapterId)));
        }

        run.FinalStory = session.GetStory();
        run.FinalDiscoveries = session.GetDiscoveries();
        run.FinalExpeditions = session.GetExpeditions();
        run.FinalLetters = session.GetLetters();
        run.FinalEpilogue = session.GetEpilogue();
        run.FinalHome = session.GetHome();
        run.FinalState = ReadState(temp.Path);
        return run;
    }

    /// <summary>Records offers and completions, then reads and dismisses the return summary.</summary>
    private static void Observe(GameSession session, Run run, int day)
    {
        foreach (var row in session.GetProjects().Projects)
        {
            if (row.Status != ProjectStatus.Locked) run.Offered.Add(row.ProjectId);
            if (row.Status == ProjectStatus.Completed && run.Completed.Add(row.ProjectId))
                run.CompletedDay[row.ProjectId] = day;
        }

        var summary = session.GetPendingReturnSummary();
        run.TransformationsOnDay[day] = summary?.Items.Count(i => i.Kind == WalkGame.Domain.Summaries.SummaryItemKind.Transformation) ?? 0;
        if (summary == null) return;
        foreach (var item in summary.Items)
        {
            // Announcement is checked on the stored line (what shells parse); D-162: what the
            // player actually reads is the display form, and every one of those must read well.
            run.SummaryLines.Add(item.Text);
            run.FirstSeenDay.TryAdd(item.Text, day);
            var problems = SummaryCopyQualityTests.Problems(item.DisplayText);
            Assert.True(problems.Count == 0, $"day {day}: \"{item.DisplayText}\" ({string.Join("; ", problems)})");
        }
        Assert.True(session.AcknowledgeReturnSummary().IsSuccess);
    }

    private static void Choose(GameSession session, Policy policy, Run run)
    {
        var available = session.GetProjects().Projects
            .Where(r => r.Status == ProjectStatus.Available)
            .ToList();
        IEnumerable<ProjectsReadModel.ProjectRow> ordered = policy switch
        {
            Policy.Catalog => available,
            Policy.Cheapest => available.OrderBy(r => r.VitalityCost).ThenBy(r => r.ProjectId, StringComparer.Ordinal),
            Policy.DeepestFirst => Enumerable.Reverse(available),
            _ => throw new ArgumentOutOfRangeException(nameof(policy)),
        };
        foreach (var row in ordered)
        {
            if (session.EnqueueProject(row.ProjectId).IsSuccess)
            {
                run.Chosen.Add(row.ProjectId);
                return;
            }
        }
    }

    private static string Title(string titleKey) => titleKey;

    private static GameSession Session(string directory, DateTimeOffset now) =>
        TestSessions.Create(directory, new ManualClock(now), new LocalPreferencesStore(directory));

    private static GameState ReadState(string directory)
    {
        var decoded = TestSessions.NewCodec().Decode(File.ReadAllBytes(Path.Combine(directory, "save.json")));
        Assert.Equal(CodecStatus.Ok, decoded.Status);
        return decoded.State!;
    }
}
