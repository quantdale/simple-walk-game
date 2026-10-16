using System;
using System.Text;
using WalkGame.Application;
using WalkGame.Application.Activity;
using WalkGame.Application.Content;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;
using WalkGame.Infrastructure.Platform.HealthConnect;
using WalkGame.Infrastructure.Tests.TestSupport;

namespace WalkGame.Infrastructure.Tests;

/// <summary>
/// Security review 2026-10-16: hostile or corrupted Health Connect bridge output, driven through
/// the REAL trust pipeline and save. Every case must stay bounded, never throw past the session
/// except as a contained fetch failure, and leave a save that reloads.
/// </summary>
public sealed class HealthConnectBridgeTamperTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset M = T0.AddHours(9);

    private sealed class Bridge : IHealthConnectBridge
    {
        public string Response = "ok";
        public string GetSdkStatus() => "available";
        public string GetPermissionStatus() => "granted";
        public string ReadSteps(long s, long e) => Response;
    }
    private sealed class Clock : IClock { public DateTimeOffset UtcNow { get; set; } }

    private static string L(string id, DateTimeOffset s, DateTimeOffset e, long n, DateTimeOffset m) =>
        $"{id}\t{s.ToUnixTimeMilliseconds()}\t{e.ToUnixTimeMilliseconds()}\t{n}\t{m.ToUnixTimeMilliseconds()}";

    private sealed class Outcome
    {
        public IngestResult? Result;
        public Exception? Thrown;
        public long Vitality;
        public StartStatus Reload;
    }

    private static Outcome Run(string response)
    {
        using var t = new TempDirectory();
        var b = new Bridge { Response = response };
        var a = new HealthConnectActivityAdapter(b, () => T0.AddDays(2));
        GameSession Make() => new(new AtomicFileSaveStore(t.Path), new SaveCodec(new MigrationRunner(DefaultMigrations.All)),
            new Clock { UtcNow = T0.AddDays(2) }, Region1Catalog.Create(), new LocalPreferencesStore(t.Path), a);
        var s = Make();
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(7UL).Status);
        var o = new Outcome();
        try { o.Result = s.IngestFromSource(a, T0, T0.AddDays(2)); }
        catch (Exception ex) { o.Thrown = ex; }
        o.Vitality = s.GetHome().Vitality;
        o.Reload = Make().Continue().Status;
        return o;
    }

    [Theory]
    [InlineData(long.MaxValue)]
    [InlineData(10_000_000L)]
    public void Absurd_step_counts_are_clamped_to_the_per_record_ceiling(long count)
    {
        var o = Run("ok\n" + L("x", T0, T0.AddHours(8), count, M));
        Assert.Null(o.Thrown);
        Assert.Equal(1, o.Result!.Accepted);
        Assert.Equal(2_500L, o.Vitality);   // plausibility ceiling, no overflow
        Assert.Equal(StartStatus.Loaded, o.Reload);
    }

    [Theory]
    [InlineData(-5000L, 0, 8)]   // negative count
    [InlineData(5000L, 8, 0)]    // end before start
    [InlineData(5000L, 0, 0)]    // zero-length interval
    public void Impossible_records_are_rejected_without_credit(long count, int startHours, int endHours)
    {
        var o = Run("ok\n" + L("x", T0.AddHours(startHours), T0.AddHours(endHours), count, M));
        Assert.Null(o.Thrown);
        Assert.Equal(0, o.Result!.Accepted);
        Assert.Equal(1, o.Result.Rejected);
        Assert.Equal(0L, o.Vitality);
        Assert.Equal(StartStatus.Loaded, o.Reload);
    }

    [Fact]
    public void The_same_id_twice_in_one_response_credits_once()
    {
        var o = Run("ok\n" + L("x", T0, T0.AddHours(8), 5000, M) + "\n" + L("x", T0, T0.AddHours(8), 90000, M));
        Assert.Equal(1, o.Result!.Accepted);
        Assert.Equal(1, o.Result.DuplicatesIgnored);
        Assert.Equal(50L, o.Vitality);
    }

    [Theory]
    [InlineData(129)]
    [InlineData(100_000)]
    public void Oversized_record_ids_fail_closed_and_are_never_persisted(int length)
    {
        var o = Run("ok\n" + L(new string('a', length), T0, T0.AddHours(8), 5000, M));
        Assert.IsType<InvalidOperationException>(o.Thrown);
        Assert.Equal(0L, o.Vitality);
        Assert.Equal(StartStatus.Loaded, o.Reload);
    }

    [Theory]
    [InlineData("a\u0000b")]
    [InlineData("a\u001b[31mred")]
    [InlineData("a\u0085b")]
    public void Control_characters_in_ids_fail_closed(string id)
    {
        var o = Run("ok\n" + L(id, T0, T0.AddHours(8), 5000, M));
        Assert.IsType<InvalidOperationException>(o.Thrown);
        Assert.Equal(0L, o.Vitality);
    }

    [Fact]
    public void An_id_at_the_length_limit_is_accepted()
    {
        var o = Run("ok\n" + L(new string('a', HealthConnectActivityAdapter.MaxRecordIdLength), T0, T0.AddHours(8), 5000, M));
        Assert.Null(o.Thrown);
        Assert.Equal(50L, o.Vitality);
    }

    [Theory]
    [InlineData("\uFEFFok")]   // BOM before the header
    [InlineData("OK")]
    [InlineData("ok\textra")]
    public void Unrecognized_headers_fail_the_whole_fetch(string header)
    {
        var o = Run(header + "\n" + L("x", T0, T0.AddHours(8), 5000, M));
        Assert.IsType<InvalidOperationException>(o.Thrown);
        Assert.Equal(0L, o.Vitality);
        Assert.Equal(StartStatus.Loaded, o.Reload);
    }

    [Fact]
    public void Extreme_last_modified_times_stay_bounded()
    {
        foreach (var mod in new[] { DateTimeOffset.UnixEpoch, DateTimeOffset.MaxValue.AddDays(-1) })
        {
            var o = Run("ok\n" + L("x", T0, T0.AddHours(8), 5000, mod));
            Assert.Null(o.Thrown);
            Assert.Equal(50L, o.Vitality);
        }
    }

    [Fact]
    public void A_deletion_for_a_record_never_credited_changes_nothing()
    {
        var o = Run("ok\ndel\tq\t" + T0.ToUnixTimeMilliseconds() + "\t" + T0.AddHours(1).ToUnixTimeMilliseconds() + "\t1\t2");
        Assert.Null(o.Thrown);
        Assert.Equal(0L, o.Vitality);
    }

    [Fact]
    public void A_fifty_thousand_record_response_is_processed_within_budget()
    {
        var sb = new StringBuilder("ok\n");
        for (int i = 0; i < 50_000; i++) sb.Append(L("r" + i, T0.AddSeconds(i), T0.AddSeconds(i + 1), 120, M)).Append('\n');
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var o = Run(sb.ToString());
        sw.Stop();
        Assert.Null(o.Thrown);
        Assert.Equal(50_000, o.Result!.Accepted);
        Assert.Equal(StartStatus.Loaded, o.Reload);
        Assert.True(sw.ElapsedMilliseconds < 20_000, "took " + sw.ElapsedMilliseconds + " ms");
    }
}
