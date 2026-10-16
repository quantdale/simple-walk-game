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
using WalkGame.Infrastructure.Tests.TestSupport;

namespace WalkGame.Infrastructure.Tests;

/// <summary>
/// Headless conformance of the Android Health Connect adapter (D-068) against the existing
/// activity-source (D-034) and connection-port (D-043) contracts. The native Java bridge is
/// replaced by a fake speaking the same string protocol; device behaviour is UNVERIFIED (KI-002).
/// </summary>
public sealed class HealthConnectActivityAdapterTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private sealed class FakeBridge : IHealthConnectBridge
    {
        public string Sdk = "available";
        public string Permission = "granted";
        public string Response = "ok";
        public Exception? Throw;
        public int Reads;
        public (long, long) LastWindow;
        public string GetSdkStatus() => Sdk;
        public string GetPermissionStatus() => Permission;
        public string ReadSteps(long s, long e)
        {
            Reads++; LastWindow = (s, e);
            if (Throw != null) throw Throw;
            return Response;
        }
    }

    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow { get; set; } }

    private static string Line(string id, DateTimeOffset s, DateTimeOffset e, long count, DateTimeOffset modified) =>
        $"{id}\t{s.ToUnixTimeMilliseconds()}\t{e.ToUnixTimeMilliseconds()}\t{count}\t{modified.ToUnixTimeMilliseconds()}";

    private static HealthConnectActivityAdapter Adapter(IHealthConnectBridge b) => new(b, () => T0.AddDays(4));

    private GameSession Session(IActivityConnectionPort port, IClock clock) => new(
        new AtomicFileSaveStore(_temp.Path),
        new SaveCodec(new MigrationRunner(DefaultMigrations.All)),
        clock,
        Region1Catalog.Create(),
        new LocalPreferencesStore(_temp.Path),
        port);

    // ---- record mapping ------------------------------------------------------------

    [Fact]
    public void Ok_response_maps_to_normalized_walking_step_records_with_provenance()
    {
        var b = new FakeBridge { Response = "ok\n" + Line("hc-1", T0, T0.AddHours(1), 4200, T0.AddHours(2)) };
        var r = Adapter(b).FetchRecords(T0, T0.AddDays(1)).Single();

        Assert.Equal(HealthConnectActivityAdapter.Namespace, r.ProviderNamespace);
        Assert.Equal("hc-1", r.SourceRecordId);
        Assert.Equal(ActivityCategory.Walking, r.Category);
        Assert.Equal(ActivityUnits.Steps, r.Unit);
        Assert.Equal(4200, r.Quantity);
        Assert.Equal(T0, r.StartUtc);
        Assert.Equal(T0.AddHours(1), r.EndUtc);
        Assert.False(r.IsDeletion);
        Assert.Equal((T0.ToUnixTimeMilliseconds(), T0.AddDays(1).ToUnixTimeMilliseconds()), b.LastWindow);
    }

    [Fact]
    public void Records_outside_the_window_are_dropped_and_output_is_deterministically_ordered()
    {
        var b = new FakeBridge
        {
            Response = "ok\n" +
                Line("b", T0.AddHours(3), T0.AddHours(4), 10, T0) + "\n" +
                Line("straddle", T0.AddHours(-1), T0.AddHours(1), 99, T0) + "\n" +
                Line("a", T0.AddHours(3), T0.AddHours(4), 20, T0) + "\n" +
                Line("first", T0.AddHours(1), T0.AddHours(2), 30, T0) + "\n" +
                Line("late", T0.AddHours(23), T0.AddDays(1).AddMinutes(1), 5, T0) + "\n",
        };
        var ids = Adapter(b).FetchRecords(T0, T0.AddDays(1)).Select(r => r.SourceRecordId).ToArray();
        Assert.Equal(new[] { "first", "a", "b" }, ids);
    }

    [Fact]
    public void A_later_edit_of_the_same_record_gets_a_higher_revision()
    {
        Assert.True(HealthConnectActivityAdapter.RevisionFor(T0.AddMinutes(5)) > HealthConnectActivityAdapter.RevisionFor(T0));
        Assert.Equal(1, HealthConnectActivityAdapter.RevisionFor(new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        Assert.Equal(int.MaxValue, HealthConnectActivityAdapter.RevisionFor(DateTimeOffset.MaxValue));
    }

    [Theory]
    [InlineData("ok\nid-only")]
    [InlineData("ok\nx\tnot-a-number\t1\t1\t1")]
    [InlineData("ok\n\t1\t2\t3\t4")]
    [InlineData("ok\nx\t1\t2\t3")]
    [InlineData("surprise")]
    [InlineData("")]
    public void Malformed_bridge_output_fails_the_whole_fetch(string response)
    {
        var b = new FakeBridge { Response = response };
        Assert.Throws<InvalidOperationException>(() => Adapter(b).FetchRecords(T0, T0.AddDays(1)));
    }

    [Fact]
    public void Empty_or_inverted_window_never_calls_the_provider()
    {
        var b = new FakeBridge();
        Assert.Empty(Adapter(b).FetchRecords(T0, T0));
        Assert.Empty(Adapter(b).FetchRecords(T0.AddDays(1), T0));
        Assert.Equal(0, b.Reads);
    }

    // ---- permission / availability / failure states --------------------------------

    [Theory]
    [InlineData("granted", ActivityPermissionState.Granted)]
    [InlineData("not_requested", ActivityPermissionState.NotRequested)]
    [InlineData("denied", ActivityPermissionState.Denied)]
    [InlineData("revoked", ActivityPermissionState.Revoked)]
    [InlineData("GRANTED", ActivityPermissionState.NotRequested)]
    [InlineData("whatever", ActivityPermissionState.NotRequested)]
    public void Permission_strings_map_fail_closed(string raw, ActivityPermissionState expected)
    {
        var snap = Adapter(new FakeBridge { Permission = raw }).SnapshotConnection();
        Assert.Equal(expected, snap.Permission);
        Assert.Equal(ActivitySourceAvailability.Available, snap.Availability);
    }

    [Theory]
    [InlineData("unsupported", ActivitySourceAvailability.Unsupported)]
    [InlineData("update_required", ActivitySourceAvailability.ProviderUpdateRequired)] // D-093
    [InlineData("garbage", ActivitySourceAvailability.TemporarilyUnavailable)]
    public void Sdk_status_maps_to_availability_and_never_claims_a_grant(string sdk, ActivitySourceAvailability expected)
    {
        var snap = Adapter(new FakeBridge { Sdk = sdk, Permission = "granted" }).SnapshotConnection();
        Assert.Equal(expected, snap.Availability);
        Assert.Equal(ActivityPermissionState.NotRequested, snap.Permission);
    }

    [Fact]
    public void A_throwing_bridge_status_call_is_contained()
    {
        var snap = Adapter(new ThrowingStatusBridge()).SnapshotConnection();
        Assert.Equal(ActivitySourceAvailability.TemporarilyUnavailable, snap.Availability);
    }

    private sealed class ThrowingStatusBridge : IHealthConnectBridge
    {
        public string GetSdkStatus() => throw new InvalidOperationException("jni");
        public string GetPermissionStatus() => throw new InvalidOperationException("jni");
        public string ReadSteps(long s, long e) => throw new InvalidOperationException("jni");
    }

    [Theory]
    [InlineData("not_requested")]
    [InlineData("denied")]
    [InlineData("revoked")]
    public void Fetch_without_permission_throws_unauthorized_and_does_not_read(string permission)
    {
        var b = new FakeBridge { Permission = permission };
        Assert.Throws<UnauthorizedAccessException>(() => Adapter(b).FetchRecords(T0, T0.AddDays(1)));
        Assert.Equal(0, b.Reads);
    }

    [Theory]
    [InlineData("error:permission", typeof(UnauthorizedAccessException))]
    [InlineData("error:unavailable", typeof(InvalidOperationException))]
    [InlineData("error:timeout", typeof(IOException))]
    [InlineData("error:io", typeof(IOException))]
    public void Bridge_error_codes_map_to_the_exceptions_the_session_records(string code, Type expected)
    {
        var b = new FakeBridge { Response = code };
        Assert.Throws(expected, () => Adapter(b).FetchRecords(T0, T0.AddDays(1)));
    }

    [Fact]
    public void Native_exception_during_read_becomes_io_failure()
    {
        var b = new FakeBridge { Throw = new Exception("AndroidJavaException") };
        Assert.Throws<IOException>(() => Adapter(b).FetchRecords(T0, T0.AddDays(1)));
    }

    [Fact]
    public void Transient_failure_shows_temporarily_unavailable_until_the_next_success()
    {
        var b = new FakeBridge { Response = "error:timeout" };
        var a = Adapter(b);
        Assert.Throws<IOException>(() => a.FetchRecords(T0, T0.AddDays(1)));
        var failed = a.SnapshotConnection();
        Assert.Equal(ActivitySourceAvailability.TemporarilyUnavailable, failed.Availability);
        Assert.NotNull(failed.LastAttemptUtc);
        Assert.Null(failed.LastSuccessfulRefreshUtc);

        b.Response = "ok";
        a.FetchRecords(T0, T0.AddDays(1));
        var ok = a.SnapshotConnection();
        Assert.Equal(ActivitySourceAvailability.Available, ok.Availability);
        Assert.Equal(ok.LastAttemptUtc, ok.LastSuccessfulRefreshUtc);
    }

    // ---- end to end through the real trust pipeline --------------------------------

    [Fact]
    public void Health_connect_records_credit_once_through_the_unchanged_trust_pipeline()
    {
        var clock = new FixedClock { UtcNow = T0.AddDays(2) };
        var b = new FakeBridge
        {
            Response = "ok\n" +
                Line("hc-a", T0, T0.AddHours(8), 6000, T0.AddHours(9)) + "\n" +
                Line("hc-b", T0.AddDays(1), T0.AddDays(1).AddHours(8), 4000, T0.AddDays(1).AddHours(9)),
        };
        var adapter = Adapter(b);
        var session = Session(adapter, clock);
        Assert.Equal(StartStatus.NewGameCreated, session.StartNewGame(7UL).Status);

        var first = session.IngestFromSource(adapter, T0, T0.AddDays(2));
        Assert.Equal(2, first.Accepted);
        Assert.Equal(100L, first.VitalityCredited); // (6000 + 4000) / 100
        Assert.Equal(ActivityPlayerStatus.ConnectedCurrent, session.GetActivityStatus().Status);

        var replay = session.IngestFromSource(adapter, T0, T0.AddDays(2));
        Assert.Equal(0, replay.Accepted);
        Assert.Equal(0L, replay.VitalityCredited);
        Assert.Equal(2, replay.DuplicatesIgnored);
    }

    [Fact]
    public void Revocation_mid_session_keeps_progress_and_shows_denied_not_an_error()
    {
        var clock = new FixedClock { UtcNow = T0.AddDays(2) };
        var b = new FakeBridge { Response = "ok\n" + Line("hc-a", T0, T0.AddHours(8), 6000, T0) };
        var adapter = Adapter(b);
        var session = Session(adapter, clock);
        session.StartNewGame(7UL);
        session.IngestFromSource(adapter, T0, T0.AddDays(2));
        long vitality = session.GetHome().Vitality;

        b.Permission = "revoked";
        Assert.Throws<UnauthorizedAccessException>(() => session.IngestFromSource(adapter, T0, T0.AddDays(2)));

        Assert.Equal(vitality, session.GetHome().Vitality);
        Assert.Equal(ActivityPermissionState.Revoked, adapter.SnapshotConnection().Permission);
        Assert.Equal(ActivityPlayerStatus.PermissionDenied, session.GetActivityStatus().Status);
    }

    [Fact]
    public void Unsupported_device_reports_source_unavailable_and_navigation_stays_open()
    {
        var clock = new FixedClock { UtcNow = T0 };
        var adapter = Adapter(new FakeBridge { Sdk = "unsupported" });
        var session = Session(adapter, clock);
        session.StartNewGame(7UL);
        Assert.Equal(ActivityPlayerStatus.SourceUnavailable, session.GetActivityStatus().Status);
        Assert.NotNull(session.GetHome());
    }

    // ---- D-069: change-tracking deletions through the existing deletion path ---------

    private static string Del(string id, DateTimeOffset s, DateTimeOffset e, DateTimeOffset modified, DateTimeOffset deleted) =>
        $"del\t{id}\t{s.ToUnixTimeMilliseconds()}\t{e.ToUnixTimeMilliseconds()}\t{modified.ToUnixTimeMilliseconds()}\t{deleted.ToUnixTimeMilliseconds()}";

    [Fact]
    public void Deletion_line_maps_to_a_marker_with_the_original_identity_and_interval()
    {
        var b = new FakeBridge { Response = "ok\n" + Del("hc-1", T0, T0.AddHours(1), T0.AddHours(2), T0.AddDays(1)) };
        var r = Adapter(b).FetchRecords(T0, T0.AddDays(2)).Single();
        Assert.True(r.IsDeletion);
        Assert.Equal("hc-1", r.SourceRecordId);
        Assert.Equal(HealthConnectActivityAdapter.Namespace, r.ProviderNamespace);
        Assert.Equal(0L, r.Quantity);
        Assert.Equal(T0, r.StartUtc);
        Assert.Equal(T0.AddHours(1), r.EndUtc);
        Assert.Equal(ActivityIdentity.Compute(r),
            ActivityIdentity.Compute(new NormalizedActivityRecord(HealthConnectActivityAdapter.Namespace, "hc-1", ActivityCategory.Walking, ActivityUnits.Steps, 5, T0.AddHours(5), T0.AddHours(6))));
    }

    [Fact]
    public void Deletion_revision_is_always_above_the_credited_revision()
    {
        Assert.True(HealthConnectActivityAdapter.DeletionRevisionFor(T0, T0.AddDays(1)) > HealthConnectActivityAdapter.RevisionFor(T0));
        // device clock ran backwards: deletion stamped before the last edit
        Assert.Equal(HealthConnectActivityAdapter.RevisionFor(T0) + 1, HealthConnectActivityAdapter.DeletionRevisionFor(T0, T0.AddDays(-3)));
        Assert.Equal(int.MaxValue, HealthConnectActivityAdapter.DeletionRevisionFor(DateTimeOffset.MaxValue, T0));
    }

    [Theory]
    [InlineData("ok\ndel\tid\t1\t2\t3")]
    [InlineData("ok\ndel\t\t1\t2\t3\t4")]
    [InlineData("ok\ndel\tid\tx\t2\t3\t4")]
    public void Malformed_deletion_lines_fail_the_whole_fetch(string response)
    {
        Assert.Throws<InvalidOperationException>(() => Adapter(new FakeBridge { Response = response }).FetchRecords(T0, T0.AddDays(1)));
    }

    [Fact]
    public void Deletions_outside_the_window_are_dropped_and_sort_after_credits()
    {
        var b = new FakeBridge
        {
            Response = "ok\n" +
                Del("d-in", T0, T0.AddHours(1), T0, T0.AddDays(1)) + "\n" +
                Del("d-out", T0.AddDays(-2), T0.AddDays(-2).AddHours(1), T0, T0.AddDays(1)) + "\n" +
                Line("c", T0.AddHours(5), T0.AddHours(6), 10, T0),
        };
        var rs = Adapter(b).FetchRecords(T0, T0.AddDays(1));
        Assert.Equal(new[] { "c", "d-in" }, rs.Select(r => r.SourceRecordId).ToArray());
    }

    [Fact]
    public void Deleting_a_credited_record_reverses_its_credit_exactly_once_across_restarts()
    {
        var clock = new FixedClock { UtcNow = T0.AddDays(2) };
        var b = new FakeBridge
        {
            Response = "ok\n" +
                Line("keep", T0, T0.AddHours(8), 6000, T0.AddHours(9)) + "\n" +
                Line("gone", T0.AddDays(1), T0.AddDays(1).AddHours(8), 4000, T0.AddDays(1).AddHours(9)),
        };
        var adapter = Adapter(b);
        var session = Session(adapter, clock);
        session.StartNewGame(7UL);
        Assert.Equal(100L, session.IngestFromSource(adapter, T0, T0.AddDays(2)).VitalityCredited);
        long before = session.GetHome().Vitality;

        // Health Connect no longer returns "gone"; the bridge re-emits its deletion on every read.
        b.Response = "ok\n" + Line("keep", T0, T0.AddHours(8), 6000, T0.AddHours(9)) + "\n" +
            Del("gone", T0.AddDays(1), T0.AddDays(1).AddHours(8), T0.AddDays(1).AddHours(9), T0.AddDays(1).AddHours(20));
        var del = session.IngestFromSource(adapter, T0, T0.AddDays(2));
        Assert.Equal(-40L, del.VitalityCredited);
        Assert.Equal(before - 40L, session.GetHome().Vitality);

        // Same deletion re-delivered, in this session and after a restart: no further change.
        Assert.Equal(0L, session.IngestFromSource(adapter, T0, T0.AddDays(2)).VitalityCredited);
        var reloaded = Session(adapter, clock);
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);
        Assert.Equal(0L, reloaded.IngestFromSource(adapter, T0, T0.AddDays(2)).VitalityCredited);
        Assert.Equal(before - 40L, reloaded.GetHome().Vitality);
    }

    [Fact]
    public void Deleting_a_record_never_credited_changes_nothing()
    {
        var clock = new FixedClock { UtcNow = T0.AddDays(2) };
        var b = new FakeBridge { Response = "ok\n" + Del("never", T0, T0.AddHours(1), T0, T0.AddDays(1)) };
        var adapter = Adapter(b);
        var session = Session(adapter, clock);
        session.StartNewGame(7UL);
        var r = session.IngestFromSource(adapter, T0, T0.AddDays(2));
        Assert.Equal(0L, r.VitalityCredited);
        Assert.Equal(0L, session.GetHome().Vitality);
    }

    [Fact]
    public void A_deleted_record_reappearing_with_a_stale_edit_is_not_recredited()
    {
        var clock = new FixedClock { UtcNow = T0.AddDays(2) };
        var b = new FakeBridge { Response = "ok\n" + Line("x", T0, T0.AddHours(8), 6000, T0.AddHours(9)) };
        var adapter = Adapter(b);
        var session = Session(adapter, clock);
        session.StartNewGame(7UL);
        session.IngestFromSource(adapter, T0, T0.AddDays(2));
        b.Response = "ok\n" + Del("x", T0, T0.AddHours(8), T0.AddHours(9), T0.AddHours(10));
        session.IngestFromSource(adapter, T0, T0.AddDays(2));
        long afterDelete = session.GetHome().Vitality;
        b.Response = "ok\n" + Line("x", T0, T0.AddHours(8), 6000, T0.AddHours(9)); // stale re-delivery
        Assert.Equal(0L, session.IngestFromSource(adapter, T0, T0.AddDays(2)).VitalityCredited);
        Assert.Equal(afterDelete, session.GetHome().Vitality);
    }
}
