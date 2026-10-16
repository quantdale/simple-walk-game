using System;
using System.Linq;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.Pacing;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Time;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-070 balance cadence: how long low / irregular / high players wait to SEE the valley change.
/// Bounds are the measured 2026-10-16 baseline (regression guards), not ratified product targets;
/// the low-player gap is an open balance finding (KI-017).
/// </summary>
public sealed class VisibleChangeCadenceTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    // ---- analyzer ---------------------------------------------------------------------

    [Fact]
    public void Analyze_counts_leading_inner_and_trailing_gaps()
    {
        var r = CadenceReport.Analyze(new[] { 3, 10, 12 }, 30);
        Assert.Equal(3, r.FirstChangeDay);
        Assert.Equal(18, r.LongestGapDays); // 12 -> 30
        Assert.Equal(3, r.Changes);
        Assert.Equal(3.0, r.ChangesPer30Days, 3);
    }

    [Fact]
    public void Analyze_with_no_change_reports_the_whole_period_as_a_gap()
    {
        var r = CadenceReport.Analyze(Array.Empty<int>(), 40);
        Assert.Equal(0, r.FirstChangeDay);
        Assert.Equal(40, r.LongestGapDays);
        Assert.Equal(0.0, r.ChangesPer30Days);
    }

    [Fact]
    public void Analyze_rejects_unordered_days()
    {
        Assert.Throws<ArgumentException>(() => CadenceReport.Analyze(new[] { 5, 2 }, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => CadenceReport.Analyze(new[] { 1 }, -1));
    }

    [Fact]
    public void Tracker_ignores_the_baseline_and_vitality_only_days()
    {
        var session = TestSessions.Create(_temp.Path, new ManualClock(T0));
        session.StartNewGame(42UL);
        var t = new VisibleChangeTracker();
        Assert.False(t.Observe(0, session.GetRegion(), session.GetDiscoveries()));
        Assert.False(t.Observe(1, session.GetRegion(), session.GetDiscoveries()), "nothing visible changed");
        Assert.Empty(t.ChangeDays);
    }

    // ---- profiles through the real trust pipeline ---------------------------------------

    private CadenceReport Drive(Func<int, long> steps, int horizon)
    {
        var content = Region1Catalog.Create();
        var start = TestSessions.Create(_temp.Path, new ManualClock(T0));
        Assert.Equal(StartStatus.NewGameCreated, start.StartNewGame(42UL).Status);
        var tracker = new VisibleChangeTracker();
        tracker.Observe(0, start.GetRegion(), start.GetDiscoveries());
        int day = 0;
        bool completed = false;
        while (day < horizon && !completed)
        {
            day++;
            var ws = T0.AddDays(day - 1); var we = T0.AddDays(day);
            var s = TestSessions.Create(_temp.Path, new ManualClock(we));
            Assert.Equal(StartStatus.Loaded, s.Continue().Status);
            s.IngestFromSource(new SyntheticWalkingSource(steps(day)), ws, we);
            var home = s.GetHome();
            if (home.ActiveProjectId == null && home.Queued.Count == 0)
                foreach (var id in content.Projects.Select(p => p.Id.Value))
                    if (s.EnqueueProject(id).IsSuccess) break;
            var region = s.GetRegion();
            completed = region.RegionCompleted;
            tracker.Observe(day, region, s.GetDiscoveries());
        }
        return CadenceReport.Analyze(tracker.ChangeDays, day);
    }

    private static readonly long[] Irregular = { 26_000L, 15_000L, 2_000L, 18_000L, 6_000L, 22_000L, 9_000L };

    [Fact]
    public void High_player_sees_change_within_days_and_never_waits_three_weeks()
    {
        var r = Drive(_ => 20_000L, 120);
        Assert.InRange(r.FirstChangeDay, 1, 3);
        Assert.InRange(r.LongestGapDays, 1, 21); // baseline 20
    }

    [Fact]
    public void Irregular_player_never_waits_more_than_a_month()
    {
        var r = Drive(d => Irregular[(d - 1) % 7], 250);
        Assert.InRange(r.FirstChangeDay, 1, 3);
        Assert.InRange(r.LongestGapDays, 1, 30); // baseline 29
    }

    [Fact]
    public void Low_player_cadence_does_not_regress_beyond_the_recorded_baseline()
    {
        // KI-017: 78 days without a visible change is a real balance gap; this bound only stops it getting worse.
        var r = Drive(_ => 3_000L, 400);
        Assert.InRange(r.FirstChangeDay, 1, 10);
        Assert.InRange(r.LongestGapDays, 1, 78);
        Assert.True(r.Changes >= 15);
    }
}
