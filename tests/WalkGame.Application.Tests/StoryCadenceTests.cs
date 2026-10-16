using System;
using System.Collections.Generic;
using System.Linq;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Domain.Time;
using WalkGame.Application.Tests.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-076 storyline: chapters unlock from restoration milestones through the REAL pipeline,
/// in order, never relock, surface as return-summary moments, and land at measured
/// intervals for every balanced profile (the cadence contract).
/// </summary>
public sealed class StoryCadenceTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 5, 0, 0, 0, TimeSpan.Zero); // a Monday
    private readonly TempDirectory _temp = new();
    private readonly ITestOutputHelper _out;
    public StoryCadenceTests(ITestOutputHelper output) => _out = output;
    public void Dispose() => _temp.Dispose();

    internal static long ProfileSteps(string profileName, int day, DateTimeOffset windowStart) =>
        profileName switch
        {
            "low" => 3_000L,
            "moderate" => 8_000L,
            "high" => 20_000L,
            "irregular" => new[] { 26_000L, 15_000L, 2_000L, 18_000L, 6_000L, 22_000L, 9_000L }[(day - 1) % 7],
            "weekend-heavy" => windowStart.DayOfWeek switch
            {
                DayOfWeek.Saturday => 22_000L,
                DayOfWeek.Sunday => 24_000L,
                DayOfWeek.Friday => 6_000L,
                DayOfWeek.Thursday => 4_500L,
                DayOfWeek.Wednesday => 4_000L,
                DayOfWeek.Tuesday => 3_500L,
                _ => 3_000L,
            },
            "inactivity" =>
                day >= 100 && day <= 144 ? 0L
                : ((day - 1) % 7 is 5 or 6 ? 0L : (day % 2 == 0 ? 9_000L : 4_000L)),
            _ => throw new ArgumentException("unknown profile " + profileName),
        };

    /// <summary>Drives a profile; returns the day each chapter first became readable (0 = never).</summary>
    private int[] Drive(string profile, int horizon, List<string>? violations = null)
    {
        var content = Region1Catalog.Create();
        var session = TestSessions.Create(_temp.Path, new ManualClock(T0));
        Assert.Equal(StartStatus.NewGameCreated, session.StartNewGame(42UL).Status);

        var first = session.GetStory();
        var unlockDay = new int[first.TotalChapters];
        int previous = first.UnlockedCount;
        for (int i = 0; i < previous; i++) unlockDay[i] = 0;
        for (int i = previous; i < unlockDay.Length; i++) unlockDay[i] = -1;

        for (int day = 1; day <= horizon && previous < first.TotalChapters; day++)
        {
            var windowStart = T0.AddDays(day - 1);
            var windowEnd = T0.AddDays(day);
            var daily = TestSessions.Create(_temp.Path, new ManualClock(windowEnd));
            Assert.Equal(StartStatus.Loaded, daily.Continue().Status);
            long steps = ProfileSteps(profile, day, windowStart);
            if (steps > 0L)
                daily.IngestFromSource(new SyntheticWalkingSource(steps), windowStart, windowEnd);

            var home = daily.GetHome();
            if (home.ActiveProjectId == null && home.Queued.Count == 0)
                foreach (var projectId in content.Projects.Select(p => p.Id.Value))
                    if (daily.EnqueueProject(projectId).IsSuccess)
                        break;

            var story = daily.GetStory();
            // Cumulative + monotonic: unlocked chapters are always a prefix and never shrink.
            for (int i = 0; i < story.Chapters.Count; i++)
                if (story.Chapters[i].Unlocked != (i < story.UnlockedCount))
                    violations?.Add($"{profile} day {day}: chapter {i} breaks prefix order");
            if (story.UnlockedCount < previous)
                violations?.Add($"{profile} day {day}: story regressed {previous}->{story.UnlockedCount}");

            if (story.UnlockedCount > previous)
            {
                var summary = daily.GetPendingReturnSummary();
                for (int i = previous; i < story.UnlockedCount; i++)
                {
                    unlockDay[i] = day;
                    string expected = "New chapter: " + story.Chapters[i].TitleKey + ".";
                    if (summary == null || !summary.Items.Any(it => it.Text == expected))
                        violations?.Add($"{profile} day {day}: summary lacks '{expected}'");
                }
                daily.AcknowledgeReturnSummary();
                previous = story.UnlockedCount;
            }
        }
        return unlockDay;
    }

    /// <summary>
    /// Cadence contract (D-076), measured through the real pipeline on 2026-10-16:
    /// first milestone chapter within 10 days for every profile, never two chapters on one
    /// day, and a per-profile ceiling on the longest gap between consecutive chapters.
    /// Engaged profiles must read the whole story (epilogue included) within 400 days;
    /// "low" is the documented long tail and "inactivity" includes a 45-day absence.
    /// </summary>
    public static IEnumerable<object[]> Profiles() => new[]
    {
        //            profile          maxGap  mustFinish
        new object[] { "high",          25,    true  },
        new object[] { "irregular",     35,    true  },
        new object[] { "moderate",      55,    true  },
        new object[] { "weekend-heavy", 50,    true  },
        new object[] { "inactivity",    90,    true  },
        new object[] { "low",           90,    false },
    };

    [Theory]
    [MemberData(nameof(Profiles))]
    public void Chapters_unlock_in_order_with_summary_moments_at_a_sensible_cadence(string profile, int maxGap, bool mustFinish)
    {
        var violations = new List<string>();
        var days = Drive(profile, 400, violations);
        _out.WriteLine(profile + ": " + string.Join(",", days));
        Assert.Empty(violations);

        Assert.Equal(0, days[0]);                       // the prologue is readable from the first launch
        Assert.InRange(days[1], 1, 10);                 // the first milestone chapter lands fast

        var reached = days.Where(d => d >= 0).ToArray();
        for (int i = 2; i < reached.Length; i++)
        {
            int gap = reached[i] - reached[i - 1];
            Assert.True(gap >= 1, $"{profile}: chapters {i - 1} and {i} unlocked on the same day ({reached[i]})");
            Assert.True(gap <= maxGap, $"{profile}: {gap}-day gap before chapter {i} exceeds {maxGap}");
        }

        if (mustFinish)
            Assert.DoesNotContain(-1, days);
        else
            Assert.True(reached.Length >= 6, $"{profile}: only {reached.Length} chapters within 400 days");
    }

    [Fact]
    public void Locked_chapters_expose_only_title_and_hint_and_the_next_hint_points_forward()
    {
        var session = TestSessions.Create(_temp.Path, new ManualClock(T0));
        Assert.Equal(StartStatus.NewGameCreated, session.StartNewGame(42UL).Status);
        var story = session.GetStory();

        Assert.Equal(10, story.TotalChapters);
        Assert.Equal(1, story.UnlockedCount);
        Assert.NotNull(story.Chapters[0].BodyKey);
        Assert.All(story.Chapters.Skip(1), c =>
        {
            Assert.False(c.Unlocked);
            Assert.Null(c.BodyKey);
            Assert.Null(c.UnlockedAtUtc);
            Assert.False(string.IsNullOrWhiteSpace(c.HintKey));
        });
        Assert.Equal(story.Chapters[1].HintKey, story.NextHintKey);
    }

    [Fact]
    public void Steps_alone_unlock_nothing_without_a_restoration_milestone()
    {
        // Chapters read only completion state and care cycles; there is no step or
        // resource input at all, so a region with nothing completed shows the prologue only
        // however much has been walked.
        var content = Region1Catalog.Create();
        var region = new WalkGame.Domain.Regions.RegionState { RegionId = content.Id.Value };
        var progress = WalkGame.Domain.Story.StoryProgress.Evaluate(content, region, T0);
        Assert.Equal(1, WalkGame.Domain.Story.StoryProgress.UnlockedCount(progress));
        Assert.Equal(T0, progress[0].UnlockedAtUtc);
    }

    [Fact]
    public void Story_is_derived_so_a_reloaded_save_reads_the_same_chapters_and_times()
    {
        var content = Region1Catalog.Create();
        var session = TestSessions.Create(_temp.Path, new ManualClock(T0));
        Assert.Equal(StartStatus.NewGameCreated, session.StartNewGame(42UL).Status);
        GameSession last = session;
        for (int day = 1; day <= 30; day++)
        {
            last = TestSessions.Create(_temp.Path, new ManualClock(T0.AddDays(day)));
            Assert.Equal(StartStatus.Loaded, last.Continue().Status);
            last.IngestFromSource(new SyntheticWalkingSource(20_000L), T0.AddDays(day - 1), T0.AddDays(day));
            var home = last.GetHome();
            if (home.ActiveProjectId == null && home.Queued.Count == 0)
                foreach (var pid in content.Projects.Select(p => p.Id.Value))
                    if (last.EnqueueProject(pid).IsSuccess) break;
        }
        var before = last.GetStory();
        Assert.True(before.UnlockedCount >= 3);

        var reloaded = TestSessions.Create(_temp.Path, new ManualClock(T0.AddDays(30)));
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);
        var after = reloaded.GetStory();
        Assert.Equal(before.UnlockedCount, after.UnlockedCount);
        for (int i = 0; i < before.TotalChapters; i++)
            Assert.Equal(before.Chapters[i].UnlockedAtUtc, after.Chapters[i].UnlockedAtUtc);
    }
}
