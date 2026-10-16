using System;
using System.IO;
using WalkGame.Application;
using WalkGame.Application.Activity;
using WalkGame.Application.Content;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;
using WalkGame.Infrastructure.Platform.HealthConnect;
using WalkGame.Infrastructure.Tests.TestSupport;

namespace WalkGame.Infrastructure.Tests;

/// <summary>
/// D-093: Health Connect bridge failure states as the PLAYER sees them, end to end through the
/// real adapter, session and projector (fake bridge; device behaviour UNVERIFIED, KI-002):
/// provider uninstalled, provider outdated, permission revoked mid-read, and clock anomalies
/// between reads. Every case: progress is never lost and the next action is one the player can take.
/// </summary>
public sealed class HealthConnectFailureStateTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private sealed class Bridge : IHealthConnectBridge
    {
        public string Sdk = "available", Permission = "granted", Response = "ok";
        public int Reads;
        public string GetSdkStatus() => Sdk;
        public string GetPermissionStatus() => Permission;
        public string ReadSteps(long s, long e) { Reads++; return Response; }
    }

    private sealed class Clock : IClock { public DateTimeOffset UtcNow { get; set; } }

    private static string Line(string id, DateTimeOffset s, long count) =>
        $"{id}\t{s.ToUnixTimeMilliseconds()}\t{s.AddHours(1).ToUnixTimeMilliseconds()}\t{count}\t{s.AddHours(2).ToUnixTimeMilliseconds()}";

    private (GameSession, HealthConnectActivityAdapter, Bridge, Clock) Start()
    {
        var clock = new Clock { UtcNow = T0.AddDays(2) };
        var bridge = new Bridge { Response = "ok\n" + Line("hc-1", T0, 5000) };
        var adapter = new HealthConnectActivityAdapter(bridge, () => clock.UtcNow);
        var session = new GameSession(new AtomicFileSaveStore(_temp.Path),
            new SaveCodec(new MigrationRunner(DefaultMigrations.All)), clock,
            Region1Catalog.Create(), new LocalPreferencesStore(_temp.Path), adapter);
        session.StartNewGame(7UL);
        Assert.Equal(50L, session.IngestFromSource(adapter, T0, T0.AddDays(2)).VitalityCredited);
        return (session, adapter, bridge, clock);
    }

    [Fact]
    public void Outdated_provider_asks_for_an_update_not_a_retry_and_never_reads()
    {
        var (session, adapter, bridge, _) = Start();
        long vitality = session.GetHome().Vitality;
        bridge.Sdk = "update_required";
        int reads = bridge.Reads;

        Assert.Throws<InvalidOperationException>(() => session.IngestFromSource(adapter, T0, T0.AddDays(2)));
        var status = session.GetActivityStatus();
        Assert.Equal(ActivityPlayerStatus.ProviderUpdateRequired, status.Status);
        Assert.Equal(ActivityRecommendedAction.UpdateProvider, status.RecommendedAction);
        Assert.Equal(reads, bridge.Reads);
        Assert.Equal(vitality, session.GetHome().Vitality);
    }

    [Fact]
    public void After_the_provider_is_updated_new_walks_credit_and_old_ones_do_not_repeat()
    {
        var (session, adapter, bridge, _) = Start();
        bridge.Sdk = "update_required";
        Assert.ThrowsAny<Exception>(() => session.IngestFromSource(adapter, T0, T0.AddDays(2)));

        bridge.Sdk = "available";
        bridge.Response = "ok\n" + Line("hc-1", T0, 5000) + "\n" + Line("hc-2", T0.AddDays(1), 3000);
        var r = session.IngestFromSource(adapter, T0, T0.AddDays(2));
        Assert.Equal(30L, r.VitalityCredited);
        Assert.Equal(1, r.DuplicatesIgnored);
        Assert.Equal(ActivityPlayerStatus.ConnectedCurrent, session.GetActivityStatus().Status);
    }

    [Fact]
    public void Provider_uninstalled_mid_session_keeps_progress_and_offers_no_dead_end_action()
    {
        var (session, adapter, bridge, _) = Start();
        long vitality = session.GetHome().Vitality;
        bridge.Sdk = "unsupported";
        Assert.Throws<InvalidOperationException>(() => session.IngestFromSource(adapter, T0, T0.AddDays(2)));
        var status = session.GetActivityStatus();
        Assert.Equal(ActivityPlayerStatus.SourceUnavailable, status.Status);
        Assert.NotEqual(ActivityRecommendedAction.Connect, status.RecommendedAction);
        Assert.Equal(vitality, session.GetHome().Vitality);
    }

    [Fact]
    public void Permission_revoked_between_the_status_check_and_the_read_fails_whole_and_credits_nothing()
    {
        var (session, adapter, bridge, _) = Start();
        long vitality = session.GetHome().Vitality;
        // Status still says granted, but the provider refuses the read itself (revoked mid-sync).
        bridge.Response = "error:permission";
        Assert.Throws<UnauthorizedAccessException>(() => session.IngestFromSource(adapter, T0, T0.AddDays(3)));
        Assert.Equal(vitality, session.GetHome().Vitality);
        Assert.NotEqual(ActivityPlayerStatus.ConnectedCurrent, session.GetActivityStatus().Status);

        bridge.Permission = "revoked";
        var status = session.GetActivityStatus();
        Assert.Equal(ActivityPlayerStatus.PermissionDenied, status.Status);
        Assert.Equal(ActivityRecommendedAction.OpenSettings, status.RecommendedAction);
    }

    [Fact]
    public void Clock_moved_backwards_between_reads_neither_fails_nor_double_credits()
    {
        var (session, adapter, bridge, clock) = Start();
        long vitality = session.GetHome().Vitality;
        clock.UtcNow = T0.AddDays(-3); // user set the device clock back
        var r = session.IngestFromSource(adapter, T0, T0.AddDays(2));
        Assert.Equal(0L, r.VitalityCredited);
        Assert.Equal(vitality, session.GetHome().Vitality);
        Assert.NotEqual(ActivityPlayerStatus.RefreshTemporarilyFailed, session.GetActivityStatus().Status);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(-10)]
    [InlineData(14)]
    public void Timezone_change_rewrites_no_instant_so_the_same_record_is_not_credited_again(int offsetHours)
    {
        var (session, adapter, bridge, clock) = Start();
        long vitality = session.GetHome().Vitality;
        // Travelling changes the local offset only; the bridge protocol is epoch-ms, so the
        // same Health Connect record arrives with the same identity and instants.
        clock.UtcNow = clock.UtcNow.ToOffset(TimeSpan.FromHours(offsetHours));
        var r = session.IngestFromSource(adapter, T0.ToOffset(TimeSpan.FromHours(offsetHours)), clock.UtcNow);
        Assert.Equal(0L, r.VitalityCredited);
        Assert.Equal(vitality, session.GetHome().Vitality);
    }

    [Fact]
    public void Long_offline_gap_then_failure_then_success_credits_the_backlog_exactly_once()
    {
        var (session, adapter, bridge, clock) = Start();
        long vitality = session.GetHome().Vitality;
        clock.UtcNow = T0.AddDays(40);
        bridge.Response = "error:timeout";
        Assert.Throws<IOException>(() => session.IngestFromSource(adapter, T0, clock.UtcNow));
        Assert.Equal(ActivityRecommendedAction.RetryLater, session.GetActivityStatus().RecommendedAction);

        bridge.Response = "ok\n" + Line("hc-1", T0, 5000) + "\n" + Line("hc-30", T0.AddDays(30), 2000) + "\n" + Line("hc-39", T0.AddDays(39), 1000);
        Assert.Equal(30L, session.IngestFromSource(adapter, T0, clock.UtcNow).VitalityCredited);
        Assert.Equal(0L, session.IngestFromSource(adapter, T0, clock.UtcNow).VitalityCredited);
        Assert.True(session.GetHome().Vitality >= vitality);
    }
}
