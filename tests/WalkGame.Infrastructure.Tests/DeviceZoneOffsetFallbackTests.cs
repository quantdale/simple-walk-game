using System;
using System.IO;
using System.Linq;
using WalkGame.Application;
using WalkGame.Application.Activity;
using WalkGame.Application.Content;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;
using WalkGame.Infrastructure.Platform;
using WalkGame.Infrastructure.Platform.HealthConnect;
using WalkGame.Infrastructure.Platform.HealthKit;
using WalkGame.Infrastructure.Tests.TestSupport;

namespace WalkGame.Infrastructure.Tests;

/// <summary>
/// D-157: a walk whose source supplied no zone offset gets a local day from the DEVICE zone's offset
/// at the record's end instant. Pins: a source offset always wins; deletions never take the fallback;
/// a failing fallback leaves the walk undated without failing the fetch; DST and date-line edges use
/// the offset in force at the record's time; crediting, exactly-once and the save schema are unchanged.
/// Zones are built with <see cref="TimeZoneInfo.CreateCustomTimeZone(string, TimeSpan, string, string, string, TimeZoneInfo.AdjustmentRule[])"/>
/// so the tests do not depend on the host's tz database.
/// </summary>
public sealed class DeviceZoneOffsetFallbackTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    // US-Eastern-like rules: DST from the 2nd Sunday of March 02:00 to the 1st Sunday of November 02:00.
    private static readonly TimeZoneInfo Eastern = TimeZoneInfo.CreateCustomTimeZone(
        "Test/Eastern", TimeSpan.FromHours(-5), "Test Eastern", "EST", "EDT",
        new[]
        {
            TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                new DateTime(2007, 1, 1), DateTime.MaxValue.Date, TimeSpan.FromHours(1),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 2, DayOfWeek.Sunday),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 11, 1, DayOfWeek.Sunday)),
        });

    private static TimeZoneInfo Fixed(string id, TimeSpan offset) => TimeZoneInfo.CreateCustomTimeZone(id, offset, id, id);

    private sealed class HcBridge : IHealthConnectBridge
    {
        public string Response = "ok";
        public string GetSdkStatus() => "available";
        public string GetPermissionStatus() => "granted";
        public string ReadSteps(long s, long e) => Response;
    }

    private sealed class HkBridge : IHealthKitBridge
    {
        public string Response = "ok";
        public string GetAvailability() => "available";
        public string GetRequestStatus() => "unnecessary";
        public string ReadSteps(long s, long e) => Response;
    }

    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow { get; set; } }

    private static string Hc(string id, DateTimeOffset s, DateTimeOffset e, long count, string? offset = null) =>
        $"{id}\t{s.ToUnixTimeMilliseconds()}\t{e.ToUnixTimeMilliseconds()}\t{count}\t{e.AddMinutes(1).ToUnixTimeMilliseconds()}"
        + (offset == null ? "" : "\t" + offset);

    private static string HcDel(string id, DateTimeOffset s, DateTimeOffset e) =>
        $"del\t{id}\t{s.ToUnixTimeMilliseconds()}\t{e.ToUnixTimeMilliseconds()}\t{e.AddMinutes(1).ToUnixTimeMilliseconds()}\t{e.AddMinutes(5).ToUnixTimeMilliseconds()}";

    private static string Hk(string id, DateTimeOffset s, DateTimeOffset e, long count, string offset = "") =>
        $"{id}\t{s.ToUnixTimeMilliseconds()}\t{e.ToUnixTimeMilliseconds()}\t{count}\t{offset}";

    private static NormalizedActivityRecord FetchOneHc(string line, Func<DateTimeOffset, int?>? fallback, DateTimeOffset from, DateTimeOffset to) =>
        new HealthConnectActivityAdapter(new HcBridge { Response = "ok\n" + line }, () => to, fallback).FetchRecords(from, to).Single();

    private GameSession Session(IActivityConnectionPort port, IClock clock) => new(
        new AtomicFileSaveStore(_temp.Path),
        new SaveCodec(new MigrationRunner(DefaultMigrations.All)),
        clock, Region1Catalog.Create(), new LocalPreferencesStore(_temp.Path), port);

    // ---- the helper --------------------------------------------------------------------------

    [Fact]
    public void Offset_is_the_zone_offset_in_force_at_the_instant_across_both_DST_transitions()
    {
        // 2026-03-08: 02:00 EST = 07:00Z springs forward. 2026-11-01: 02:00 EDT = 06:00Z falls back.
        Assert.Equal(-18000, DeviceZoneOffsets.OffsetSecondsAt(Eastern, new DateTimeOffset(2026, 3, 8, 6, 59, 59, TimeSpan.Zero)));
        Assert.Equal(-14400, DeviceZoneOffsets.OffsetSecondsAt(Eastern, new DateTimeOffset(2026, 3, 8, 7, 0, 0, TimeSpan.Zero)));
        // The repeated 01:30 local hour on 2026-11-01 is ambiguous as a local time but not as an instant.
        Assert.Equal(-14400, DeviceZoneOffsets.OffsetSecondsAt(Eastern, new DateTimeOffset(2026, 11, 1, 5, 30, 0, TimeSpan.Zero)));
        Assert.Equal(-18000, DeviceZoneOffsets.OffsetSecondsAt(Eastern, new DateTimeOffset(2026, 11, 1, 6, 30, 0, TimeSpan.Zero)));
        // Non-hour and date-line offsets.
        Assert.Equal(20700, DeviceZoneOffsets.OffsetSecondsAt(Fixed("Test/Kathmandu", new TimeSpan(5, 45, 0)), T0));
        Assert.Equal(50400, DeviceZoneOffsets.OffsetSecondsAt(Fixed("Test/Kiritimati", TimeSpan.FromHours(14)), T0));
        Assert.Null(DeviceZoneOffsets.OffsetSecondsAt(null!, T0));
    }

    [Fact]
    public void Missing_source_offset_takes_the_device_offset_at_the_record_end_and_a_present_one_wins()
    {
        int calls = 0;
        Func<DateTimeOffset, int?> manila = t => { calls++; return DeviceZoneOffsets.OffsetSecondsAt(Fixed("Test/Manila", TimeSpan.FromHours(8)), t); };

        var missing = FetchOneHc(Hc("hc-1", T0, T0.AddHours(1), 4200), manila, T0, T0.AddDays(1));
        Assert.Equal(28800, missing.EndZoneOffsetSeconds);
        var emptySixth = FetchOneHc(Hc("hc-2", T0, T0.AddHours(1), 4200, ""), manila, T0, T0.AddDays(1));
        Assert.Equal(28800, emptySixth.EndZoneOffsetSeconds);
        Assert.Equal(2, calls);

        var present = FetchOneHc(Hc("hc-3", T0, T0.AddHours(1), 4200, "-18000"), manila, T0, T0.AddDays(1));
        Assert.Equal(-18000, present.EndZoneOffsetSeconds);
        Assert.Equal(2, calls); // never consulted when Health Connect supplied an offset

        // Without a fallback (pre-D-157 composition) the walk stays undated.
        Assert.Null(FetchOneHc(Hc("hc-4", T0, T0.AddHours(1), 4200), null, T0, T0.AddDays(1)).EndZoneOffsetSeconds);
        // Identity never includes the offset, so inferring one cannot re-credit a record.
        Assert.Equal(ActivityIdentity.Compute(missing with { EndZoneOffsetSeconds = null }), ActivityIdentity.Compute(missing));
    }

    [Fact]
    public void The_offset_is_taken_at_the_record_end_not_at_sync_time_across_a_DST_change()
    {
        // Walk ends 2026-11-01 04:30Z = 00:30 EDT on Nov 1. Synced after fall-back (now EST, -5 h):
        // the current offset would date it Oct 31 23:30; the offset at the record's time keeps Nov 1.
        var end = new DateTimeOffset(2026, 11, 1, 4, 30, 0, TimeSpan.Zero);
        var r = FetchOneHc(Hc("hc-dst", end.AddMinutes(-30), end, 3000), DeviceZoneOffsets.For(Eastern), end.AddDays(-1), end.AddDays(1));
        Assert.Equal(-14400, r.EndZoneOffsetSeconds);
        Assert.Equal(LocalDayCalendar.DayOf(new DateTimeOffset(2026, 11, 1, 0, 30, 0, TimeSpan.FromHours(-4)), -14400), LocalDayCalendar.DayOf(r.EndUtc, r.EndZoneOffsetSeconds));
        Assert.NotEqual(LocalDayCalendar.DayOf(r.EndUtc, -18000), LocalDayCalendar.DayOf(r.EndUtc, r.EndZoneOffsetSeconds));
    }

    [Fact]
    public void Date_line_zones_put_the_same_instant_on_different_local_days()
    {
        var end = new DateTimeOffset(2026, 10, 12, 10, 30, 0, TimeSpan.Zero);
        var east = FetchOneHc(Hc("e", end.AddHours(-1), end, 1000), DeviceZoneOffsets.For(Fixed("Test/P14", TimeSpan.FromHours(14))), T0, T0.AddDays(4));
        var west = FetchOneHc(Hc("w", end.AddHours(-1), end, 1000), DeviceZoneOffsets.For(Fixed("Test/M11", TimeSpan.FromHours(-11))), T0, T0.AddDays(4));
        int dayEast = LocalDayCalendar.DayOf(east.EndUtc, east.EndZoneOffsetSeconds)!.Value;
        int dayWest = LocalDayCalendar.DayOf(west.EndUtc, west.EndZoneOffsetSeconds)!.Value;
        Assert.Equal(2, dayEast - dayWest); // Oct 13 00:30 vs Oct 11 23:30
    }

    [Theory]
    [InlineData("throws")]
    [InlineData("null")]
    [InlineData("out-of-range")]
    public void A_failing_fallback_leaves_the_walk_undated_and_never_fails_the_fetch(string mode)
    {
        Func<DateTimeOffset, int?> bad = mode switch
        {
            "throws" => _ => throw new TimeZoneNotFoundException("no zone"),
            "null" => _ => null,
            _ => _ => LocalDayCalendar.MaxOffsetSeconds + 1,
        };
        var r = FetchOneHc(Hc("hc-1", T0, T0.AddHours(1), 4200), bad, T0, T0.AddDays(1));
        Assert.Null(r.EndZoneOffsetSeconds);
        Assert.Equal(4200, r.Quantity);
    }

    [Fact]
    public void Deletions_never_take_the_fallback()
    {
        int calls = 0;
        Func<DateTimeOffset, int?> spy = _ => { calls++; return 28800; };
        var adapter = new HealthConnectActivityAdapter(new HcBridge { Response = "ok\n" + HcDel("hc-1", T0, T0.AddHours(1)) }, () => T0.AddDays(1), spy);
        var del = adapter.FetchRecords(T0, T0.AddDays(1)).Single();
        Assert.True(del.IsDeletion);
        Assert.Null(del.EndZoneOffsetSeconds);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void HealthKit_samples_without_a_time_zone_take_the_same_fallback_and_a_tagged_one_wins()
    {
        var bridge = new HkBridge { Response = "ok\n" + Hk("untagged", T0, T0.AddHours(1), 2000) + "\n" + Hk("tagged", T0.AddHours(2), T0.AddHours(3), 2000, "-18000") };
        var rs = new HealthKitActivityAdapter(bridge, () => T0.AddDays(1), DeviceZoneOffsets.For(Fixed("Test/Manila", TimeSpan.FromHours(8))))
            .FetchRecords(T0, T0.AddDays(1)).ToDictionary(r => r.SourceRecordId!);
        Assert.Equal(28800, rs["untagged"].EndZoneOffsetSeconds);
        Assert.Equal(-18000, rs["tagged"].EndZoneOffsetSeconds);
        Assert.Null(new HealthKitActivityAdapter(new HkBridge { Response = "ok\n" + Hk("u", T0, T0.AddHours(1), 2000) }, () => T0.AddDays(1))
            .FetchRecords(T0, T0.AddDays(1)).Single().EndZoneOffsetSeconds);
    }

    // ---- end to end through the real session --------------------------------------------------

    [Fact]
    public void Today_line_appears_for_offsetless_walks_and_crediting_and_exactly_once_are_unchanged()
    {
        // Walk ends 2026-10-10 17:00Z = 2026-10-11 01:00 in UTC+8. "Now" is 2026-10-11 12:00 local.
        var clock = new FixedClock { UtcNow = new DateTimeOffset(2026, 10, 11, 4, 0, 0, TimeSpan.Zero) };
        string response = "ok\n" + Hc("hc-a", T0.AddHours(16), T0.AddHours(17), 6000);
        var manila = DeviceZoneOffsets.For(Fixed("Test/Manila", TimeSpan.FromHours(8)));

        // Baseline without the fallback: same credit, but no local day so "Today" stays 0.
        long baselineCredit;
        using (var other = new TempDirectory())
        {
            var plainAdapter = new HealthConnectActivityAdapter(new HcBridge { Response = response }, () => clock.UtcNow);
            var plain = new GameSession(new AtomicFileSaveStore(other.Path), new SaveCodec(new MigrationRunner(DefaultMigrations.All)),
                clock, Region1Catalog.Create(), new LocalPreferencesStore(other.Path), plainAdapter);
            plain.StartNewGame(7UL);
            baselineCredit = plain.IngestFromSource(plainAdapter, T0, T0.AddDays(2)).VitalityCredited;
            Assert.Equal(0L, plain.GetTodayEarnings(28800).Vitality);
            Assert.True(plain.GetDaysWalked().IsLowerBound); // undated credit
        }

        var adapter = new HealthConnectActivityAdapter(new HcBridge { Response = response }, () => clock.UtcNow, manila);
        var session = Session(adapter, clock);
        session.StartNewGame(7UL);
        var first = session.IngestFromSource(adapter, T0, T0.AddDays(2));
        Assert.Equal(baselineCredit, first.VitalityCredited);
        Assert.Equal(60L, first.VitalityCredited);
        Assert.Equal(60L, session.GetTodayEarnings(28800).Vitality);
        Assert.Equal(1, session.GetDaysWalked().Days);
        Assert.False(session.GetDaysWalked().IsLowerBound);

        // Replay through an adapter whose device zone is now UTC-5 (the player flew): still a
        // duplicate, nothing re-credited and the stored local day is not re-labelled.
        var flown = new HealthConnectActivityAdapter(new HcBridge { Response = response }, () => clock.UtcNow, DeviceZoneOffsets.For(Fixed("Test/M5", TimeSpan.FromHours(-5))));
        var replay = session.IngestFromSource(flown, T0, T0.AddDays(2));
        Assert.Equal(0L, replay.VitalityCredited);
        Assert.Equal(1, replay.DuplicatesIgnored);
        Assert.Equal(60L, session.GetTodayEarnings(28800).Vitality);

        // Save schema unchanged; the day survives a reload.
        string json = File.ReadAllText(Directory.GetFiles(_temp.Path, "*.json", SearchOption.AllDirectories)
            .First(p => File.ReadAllText(p).Contains("\"schemaVersion\"", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains("\"schemaVersion\":" + SchemaVersions.Current, json.Replace(" ", ""), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, SchemaVersions.Current);
        var reloaded = Session(adapter, clock);
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);
        Assert.Equal(60L, reloaded.GetTodayEarnings(28800).Vitality);
    }
}
