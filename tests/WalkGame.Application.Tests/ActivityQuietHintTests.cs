using System;
using WalkGame.Application.Activity;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Time;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-196 (KI-031): on HealthKit (a refused read is invisible), a connected player whose walks have not
/// arrived for 3 local calendar days gets a gentle hint; the status stays Connected. Threshold is
/// (today − arrival day) ≥ 3 in the device's current local calendar. headless-tested.
/// </summary>
public sealed class ActivityQuietHintTests
{
    private const int Manila = 8 * 3600;
    private const int NewYork = -5 * 3600;

    private static ActivityStatusReadModel Status(DateTimeOffset? lastArrival, bool hidden = true,
        ActivityPermissionState perm = ActivityPermissionState.Granted, bool processed = true,
        IngestionOutcomeKind last = IngestionOutcomeKind.Succeeded,
        ActivitySourceAvailability avail = ActivitySourceAvailability.Available) =>
        ActivityStatusProjector.Project(new ActivityConnectionSnapshot(perm, avail, readDenialHidden: hidden),
            processed, last, 0L, null, lastArrival);

    // 2026-10-14 10:00 Manila = 02:00 UTC
    private static readonly DateTimeOffset Arrived = new(2026, 10, 14, 2, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset ManilaLocal(int day, int hour, int minute = 0) =>
        new DateTimeOffset(2026, 10, day, hour, minute, 0, TimeSpan.FromHours(8)).ToUniversalTime();

    [Theory]
    [InlineData(14, 23, false)] // same day
    [InlineData(16, 23, false)] // two local days later: not yet
    [InlineData(17, 0, true)]   // third local day begins: hint
    [InlineData(20, 12, true)]
    public void Threshold_is_three_local_calendar_days(int day, int hour, bool expected) =>
        Assert.Equal(expected, ActivityQuietHint.Applies(Status(Arrived), ManilaLocal(day, hour), Manila));

    [Fact]
    public void The_edge_is_the_local_midnight_not_seventy_two_hours()
    {
        var lateArrival = ManilaLocal(14, 23, 59);
        Assert.True(ActivityQuietHint.Applies(Status(lateArrival), ManilaLocal(17, 0, 0), Manila), "48 h + 1 min, but the third local day");
        var earlyArrival = ManilaLocal(14, 0, 1);
        Assert.False(ActivityQuietHint.Applies(Status(earlyArrival), ManilaLocal(16, 23, 59), Manila), "almost 72 h, still the second local day");
    }

    [Fact]
    public void Local_days_follow_the_device_zone_not_utc()
    {
        // Arrival 2026-10-14 23:00 UTC = 15th in Manila, 14th 18:00 in New York.
        var arrival = new DateTimeOffset(2026, 10, 14, 23, 0, 0, TimeSpan.Zero);
        var now = new DateTimeOffset(2026, 10, 17, 17, 0, 0, TimeSpan.Zero); // 18th 01:00 Manila; 17th 12:00 New York
        Assert.True(ActivityQuietHint.Applies(Status(arrival), now, Manila));   // 18 − 15 = 3
        Assert.True(ActivityQuietHint.Applies(Status(arrival), now, NewYork));  // 17 − 14 = 3
        var earlier = now.AddHours(-2); // 17th 23:00 Manila; 17th 10:00 New York
        Assert.False(ActivityQuietHint.Applies(Status(arrival), earlier, Manila)); // 17 − 15 = 2
        Assert.True(ActivityQuietHint.Applies(Status(arrival), earlier, NewYork)); // 17 − 14 = 3
    }

    [Fact]
    public void Health_connect_never_gets_the_hint() =>
        Assert.False(ActivityQuietHint.Applies(Status(Arrived, hidden: false), ManilaLocal(30, 12), Manila));

    [Fact]
    public void Only_a_connected_player_with_a_walk_on_record_gets_it()
    {
        var now = ManilaLocal(30, 12);
        Assert.Equal(ActivityPlayerStatus.ConnectedCurrent, Status(Arrived).Status);
        Assert.False(ActivityQuietHint.Applies(Status(null), now, Manila), "no walk has ever arrived");
        Assert.False(ActivityQuietHint.Applies(Status(null, processed: false), now, Manila), "waiting for the first walk");
        Assert.False(ActivityQuietHint.Applies(Status(Arrived, last: IngestionOutcomeKind.SourceFetchFailed), now, Manila), "refresh failure has its own copy");
        Assert.False(ActivityQuietHint.Applies(Status(Arrived, perm: ActivityPermissionState.NotRequested), now, Manila));
        Assert.False(ActivityQuietHint.Applies(Status(Arrived, perm: ActivityPermissionState.Revoked), now, Manila));
        Assert.False(ActivityQuietHint.Applies(Status(Arrived, avail: ActivitySourceAvailability.Unsupported), now, Manila));
    }

    [Fact]
    public void A_clock_that_moved_back_never_shows_it() =>
        Assert.False(ActivityQuietHint.Applies(Status(ManilaLocal(20, 12)), ManilaLocal(14, 12), Manila));

    [Fact]
    public void Hostile_offsets_never_show_it() =>
        Assert.False(ActivityQuietHint.Applies(Status(Arrived), ManilaLocal(30, 12), 19 * 3600));

    [Fact]
    public void Null_status_is_false() => Assert.False(ActivityQuietHint.Applies(null!, ManilaLocal(30, 12), Manila));

    // ---- GameSession: the arrival instant is derived from the ledger the save already holds ----

    private sealed class HiddenPort : IActivityConnectionPort
    {
        public ActivityConnectionSnapshot SnapshotConnection() =>
            new(ActivityPermissionState.Granted, ActivitySourceAvailability.Available, readDenialHidden: true);
    }

    private static NormalizedActivityRecord Rec(string id, long steps, int revision, DateTimeOffset end, bool deleted = false) =>
        new("p", id, ActivityCategory.Walking, ActivityUnits.Steps, steps, end.AddMinutes(-30), end, revision, deleted, Manila);

    [Fact]
    public void Session_reports_when_the_last_sample_arrived_and_a_new_one_clears_the_hint()
    {
        using var dir = new TempDirectory();
        var t0 = ManilaLocal(14, 10);
        var clock = new ManualClock(t0);
        var s = TestSessions.Create(dir.Path, clock, null, new HiddenPort());
        s.StartNewGame(seed: 7UL);
        Assert.Null(s.GetActivityStatus().LastSampleArrivedUtc);

        s.IngestActivityBatch(new[] { Rec("a", 3000, 1, t0.AddHours(-1)), Rec("c", 1500, 1, t0.AddHours(-3)) });
        Assert.Equal(t0, s.GetActivityStatus().LastSampleArrivedUtc);
        long vitality = s.GetDiagnostics().LifetimeVitalityCredited;

        clock.Set(ManilaLocal(17, 9));
        s.IngestActivityBatch(Array.Empty<NormalizedActivityRecord>()); // a quiet read
        var quiet = s.GetActivityStatus();
        Assert.Equal(ActivityPlayerStatus.ConnectedCurrent, quiet.Status);
        Assert.True(ActivityQuietHint.Applies(quiet, clock.UtcNow, Manila));
        Assert.Equal(vitality, s.GetDiagnostics().LifetimeVitalityCredited); // no progress effect

        // A deletion (processed now) is not a walk arriving; the remaining walk keeps its arrival.
        s.IngestActivityBatch(new[] { Rec("a", 0, 2, t0.AddHours(-1), deleted: true) });
        Assert.Equal(t0, s.GetActivityStatus().LastSampleArrivedUtc);

        // A late watch sync with an OLD walk still counts as a sample arriving now.
        s.IngestActivityBatch(new[] { Rec("b", 2500, 1, ManilaLocal(13, 18)) });
        var after = s.GetActivityStatus();
        Assert.Equal(clock.UtcNow, after.LastSampleArrivedUtc);
        Assert.False(ActivityQuietHint.Applies(after, clock.UtcNow, Manila));

        // Survives a restart: derived from the save, nothing new stored.
        var again = TestSessions.Create(dir.Path, clock, null, new HiddenPort());
        Assert.Equal(StartStatus.Loaded, again.Continue().Status);
        Assert.Equal(clock.UtcNow, again.GetActivityStatus().LastSampleArrivedUtc);
    }
}
