using System;
using System.IO;
using System.Linq;
using WalkGame.Application;
using WalkGame.Application.Activity;
using WalkGame.Application.Content;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;
using WalkGame.Infrastructure.Platform.HealthConnect;
using WalkGame.Infrastructure.Platform.HealthKit;
using WalkGame.Infrastructure.Tests.TestSupport;

namespace WalkGame.Infrastructure.Tests;

/// <summary>
/// KI-022 / D-112: headless conformance of the iOS HealthKit adapter against the activity-source
/// (D-034) and connection-port (D-043) contracts, using a fake bridge that speaks the documented
/// string protocol. The Swift bridge has never been compiled or run: device behaviour UNVERIFIED.
/// </summary>
public sealed class HealthKitActivityAdapterTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private sealed class FakeBridge : IHealthKitBridge
    {
        public string Availability = "available";
        public string Request = "unnecessary";
        public string Response = "ok";
        public Exception? Throw;
        public int Reads;
        public (long, long) LastWindow;
        public string GetAvailability() => Availability;
        public string GetRequestStatus() => Request;
        public string ReadSteps(long s, long e)
        {
            Reads++; LastWindow = (s, e);
            if (Throw != null) throw Throw;
            return Response;
        }
    }

    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow { get; set; } }

    private static string Line(string id, DateTimeOffset s, DateTimeOffset e, long count, string offset = "") =>
        $"{id}\t{s.ToUnixTimeMilliseconds()}\t{e.ToUnixTimeMilliseconds()}\t{count}\t{offset}";
    private static string Del(string id, DateTimeOffset s, DateTimeOffset e) =>
        $"del\t{id}\t{s.ToUnixTimeMilliseconds()}\t{e.ToUnixTimeMilliseconds()}";

    private static HealthKitActivityAdapter Adapter(IHealthKitBridge b) => new(b, () => T0.AddDays(4));

    private GameSession Session(IActivityConnectionPort port, IClock clock) => new(
        new AtomicFileSaveStore(_temp.Path),
        new SaveCodec(new MigrationRunner(DefaultMigrations.All)),
        clock, Region1Catalog.Create(), new LocalPreferencesStore(_temp.Path), port);

    [Fact]
    public void Sample_maps_to_a_walking_step_record_in_its_own_namespace()
    {
        var b = new FakeBridge { Response = "ok\n" + Line("A1B2", T0, T0.AddHours(1), 4200, "28800") };
        var r = Adapter(b).FetchRecords(T0, T0.AddDays(1)).Single();
        Assert.Equal("ios.healthkit", r.ProviderNamespace);
        Assert.NotEqual(HealthConnectActivityAdapter.Namespace, r.ProviderNamespace);
        Assert.Equal("A1B2", r.SourceRecordId);
        Assert.Equal(ActivityCategory.Walking, r.Category);
        Assert.Equal(ActivityUnits.Steps, r.Unit);
        Assert.Equal(4200, r.Quantity);
        Assert.Equal(28800, r.EndZoneOffsetSeconds);
        Assert.Equal(HealthKitActivityAdapter.CreditRevision, r.Revision);
        Assert.False(r.IsDeletion);
        Assert.Equal((T0.ToUnixTimeMilliseconds(), T0.AddDays(1).ToUnixTimeMilliseconds()), b.LastWindow);
    }

    [Fact]
    public void Empty_offset_means_unknown_local_day()
    {
        var b = new FakeBridge { Response = "ok\n" + Line("x", T0, T0.AddHours(1), 10) };
        Assert.Null(Adapter(b).FetchRecords(T0, T0.AddDays(1)).Single().EndZoneOffsetSeconds);
    }

    [Theory]
    [InlineData("ok\nid-only")]
    [InlineData("ok\nx\tnot-a-number\t1\t1\t")]
    [InlineData("ok\n\t1\t2\t3\t")]
    [InlineData("ok\nx\t1\t2\t3")]
    [InlineData("ok\nx\t1\t2\t3\t4\t5")]
    [InlineData("ok\nx\t1\t2\t3\tabc")]
    [InlineData("ok\nx\t1\t2\t3\t64801")]
    [InlineData("ok\nx\t1\t2\t3\t\nx\t1\t2\t3\t")]
    [InlineData("ok\ndel\tid\t1")]
    [InlineData("ok\ndel\t\t1\t2")]
    [InlineData("ok\ndel\tid\tx\t2")]
    [InlineData("ok\nx\t99999999999999999\t2\t3\t")]
    [InlineData("surprise")]
    [InlineData("")]
    public void Malformed_bridge_output_fails_the_whole_fetch(string response)
    {
        Assert.Throws<InvalidOperationException>(() => Adapter(new FakeBridge { Response = response }).FetchRecords(T0, T0.AddDays(1)));
    }

    [Fact]
    public void Oversized_or_control_character_ids_fail_closed()
    {
        Assert.Throws<InvalidOperationException>(() => Adapter(new FakeBridge { Response = "ok\n" + Line(new string('a', 129), T0, T0.AddHours(1), 1) }).FetchRecords(T0, T0.AddDays(1)));
        Assert.Throws<InvalidOperationException>(() => Adapter(new FakeBridge { Response = "ok\n" + Line("a\u0001b", T0, T0.AddHours(1), 1) }).FetchRecords(T0, T0.AddDays(1)));
    }

    [Fact]
    public void Window_filter_and_deterministic_order_credits_before_deletions()
    {
        var b = new FakeBridge
        {
            Response = "ok\n" + Del("d", T0, T0.AddHours(1)) + "\n" + Line("b", T0.AddHours(3), T0.AddHours(4), 10) + "\n"
                + Line("straddle", T0.AddHours(-1), T0.AddHours(1), 99) + "\n" + Line("a", T0.AddHours(3), T0.AddHours(4), 20) + "\n"
                + Del("old", T0.AddDays(-2), T0.AddDays(-2).AddHours(1)),
        };
        var rs = Adapter(b).FetchRecords(T0, T0.AddDays(1));
        Assert.Equal(new[] { "a", "b", "d" }, rs.Select(r => r.SourceRecordId).ToArray());
        Assert.Equal(HealthKitActivityAdapter.DeletionRevision, rs.Last().Revision);
        Assert.True(rs.Last().IsDeletion);
    }

    [Theory]
    [InlineData("unnecessary", ActivityPermissionState.Granted)]
    [InlineData("should_request", ActivityPermissionState.NotRequested)]
    [InlineData("unknown", ActivityPermissionState.NotRequested)]
    [InlineData("granted", ActivityPermissionState.NotRequested)]
    [InlineData("", ActivityPermissionState.NotRequested)]
    public void Request_status_maps_without_claiming_an_unseen_grant(string status, ActivityPermissionState expected)
    {
        Assert.Equal(expected, Adapter(new FakeBridge { Request = status }).SnapshotConnection().Permission);
    }

    [Theory]
    [InlineData("unsupported", ActivitySourceAvailability.Unsupported)]
    [InlineData("weird", ActivitySourceAvailability.TemporarilyUnavailable)]
    public void Unavailable_healthkit_is_reported_and_never_read(string availability, ActivitySourceAvailability expected)
    {
        var b = new FakeBridge { Availability = availability };
        var a = Adapter(b);
        Assert.Equal(expected, a.SnapshotConnection().Availability);
        Assert.Throws<InvalidOperationException>(() => a.FetchRecords(T0, T0.AddDays(1)));
        Assert.Equal(0, b.Reads);
    }

    [Fact]
    public void Not_yet_asked_is_a_permission_failure_without_a_read()
    {
        var b = new FakeBridge { Request = "should_request" };
        Assert.Throws<UnauthorizedAccessException>(() => Adapter(b).FetchRecords(T0, T0.AddDays(1)));
        Assert.Equal(0, b.Reads);
    }

    [Theory]
    [InlineData("error:permission", typeof(UnauthorizedAccessException))]
    [InlineData("error:unavailable", typeof(InvalidOperationException))]
    [InlineData("error:timeout", typeof(IOException))]
    [InlineData("error:io", typeof(IOException))]
    public void Bridge_errors_map_to_the_failure_policy(string response, Type expected)
    {
        Assert.Throws(expected, () => Adapter(new FakeBridge { Response = response }).FetchRecords(T0, T0.AddDays(1)));
    }

    [Fact]
    public void A_throwing_bridge_is_an_io_failure_and_a_bridge_crash_in_status_never_escapes()
    {
        Assert.Throws<IOException>(() => Adapter(new FakeBridge { Throw = new Exception("native") }).FetchRecords(T0, T0.AddDays(1)));
        var snap = Adapter(new ThrowingStatusBridge()).SnapshotConnection();
        Assert.Equal(ActivitySourceAvailability.TemporarilyUnavailable, snap.Availability);
    }

    private sealed class ThrowingStatusBridge : IHealthKitBridge
    {
        public string GetAvailability() => throw new Exception("native");
        public string GetRequestStatus() => throw new Exception("native");
        public string ReadSteps(long s, long e) => throw new Exception("native");
    }

    [Fact]
    public void Read_access_is_confirmed_only_by_data()
    {
        var b = new FakeBridge { Response = "ok" };
        var a = Adapter(b);
        a.FetchRecords(T0, T0.AddDays(1));
        Assert.False(a.ReadAccessConfirmed);
        Assert.Contains("not yet confirmed", a.SnapshotConnection().TechnicalDetail);
        b.Response = "ok\n" + Del("d", T0, T0.AddHours(1));
        a.FetchRecords(T0, T0.AddDays(1));
        Assert.False(a.ReadAccessConfirmed);
        b.Response = "ok\n" + Line("s", T0, T0.AddHours(1), 5);
        a.FetchRecords(T0, T0.AddDays(1));
        Assert.True(a.ReadAccessConfirmed);
    }

    [Fact]
    public void One_fetch_is_exactly_one_bridge_read_and_an_empty_window_none()
    {
        var b = new FakeBridge();
        var a = Adapter(b);
        a.FetchRecords(T0, T0);
        Assert.Equal(0, b.Reads);
        a.FetchRecords(T0, T0.AddDays(30));
        Assert.Equal(1, b.Reads);
    }

    [Fact]
    public void Samples_credit_once_and_a_deletion_reverses_once_across_restarts()
    {
        var clock = new FixedClock { UtcNow = T0.AddDays(2) };
        var b = new FakeBridge
        {
            Response = "ok\n" + Line("keep", T0, T0.AddHours(8), 6000, "0") + "\n" + Line("gone", T0.AddDays(1), T0.AddDays(1).AddHours(8), 4000, "0"),
        };
        var adapter = Adapter(b);
        var session = Session(adapter, clock);
        Assert.Equal(StartStatus.NewGameCreated, session.StartNewGame(7UL).Status);
        Assert.Equal(100L, session.IngestFromSource(adapter, T0, T0.AddDays(2)).VitalityCredited);
        Assert.Equal(0L, session.IngestFromSource(adapter, T0, T0.AddDays(2)).VitalityCredited);
        long before = session.GetHome().Vitality;

        b.Response = "ok\n" + Line("keep", T0, T0.AddHours(8), 6000, "0") + "\n" + Del("gone", T0.AddDays(1), T0.AddDays(1).AddHours(8));
        Assert.Equal(-40L, session.IngestFromSource(adapter, T0, T0.AddDays(2)).VitalityCredited);
        Assert.Equal(0L, session.IngestFromSource(adapter, T0, T0.AddDays(2)).VitalityCredited);
        var reloaded = Session(adapter, clock);
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);
        Assert.Equal(0L, reloaded.IngestFromSource(adapter, T0, T0.AddDays(2)).VitalityCredited);
        Assert.Equal(before - 40L, reloaded.GetHome().Vitality);
    }

    [Fact]
    public void Same_uuid_from_both_providers_is_two_distinct_records()
    {
        var hk = new NormalizedActivityRecord(HealthKitActivityAdapter.Namespace, "same", ActivityCategory.Walking, ActivityUnits.Steps, 10, T0, T0.AddHours(1));
        var hc = hk with { ProviderNamespace = HealthConnectActivityAdapter.Namespace };
        Assert.NotEqual(ActivityIdentity.Compute(hk), ActivityIdentity.Compute(hc));
    }
}
