using System;
using WalkGame.Application.Activity;
using WalkGame.Infrastructure.Platform.HealthConnect;
using WalkGame.Infrastructure.Platform.HealthKit;

namespace WalkGame.Infrastructure.Tests;

/// <summary>
/// D-192: both adapters remember the last permission they actually READ (this process only) and
/// attach it to an unreadable-status snapshot, while still never claiming the unseen grant.
/// Health Connect and HealthKit behave the same way.
/// </summary>
public sealed class LastKnownPermissionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 17, 0, 0, 0, TimeSpan.Zero);

    private sealed class Hc : IHealthConnectBridge
    {
        public string Sdk = "available", Permission = "granted";
        public string GetSdkStatus() => Sdk;
        public string GetPermissionStatus() => Permission == "throw" ? throw new InvalidOperationException("jni") : Permission;
        public string ReadSteps(long s, long e) => "ok";
    }

    private sealed class Hk : IHealthKitBridge
    {
        public string Availability = "available", Request = "unnecessary";
        public string GetAvailability() => Availability;
        public string GetRequestStatus() => Request;
        public string ReadSteps(long s, long e) => "ok";
    }

    [Theory]
    [InlineData("granted", ActivityPermissionState.Granted)]
    [InlineData("revoked", ActivityPermissionState.Revoked)]
    [InlineData("denied", ActivityPermissionState.Denied)]
    [InlineData("not_requested", ActivityPermissionState.NotRequested)]
    public void Health_connect_carries_the_last_read_permission_into_an_unreadable_snapshot(string raw, ActivityPermissionState expected)
    {
        var b = new Hc { Permission = raw };
        var a = new HealthConnectActivityAdapter(b, () => T0);
        Assert.Null(a.SnapshotConnection().LastKnownPermission); // readable snapshots need no memory
        b.Sdk = "garbled";
        var snap = a.SnapshotConnection();
        Assert.Equal(ActivitySourceAvailability.TemporarilyUnavailable, snap.Availability);
        Assert.Equal(ActivityPermissionState.NotRequested, snap.Permission); // still never claims a grant
        Assert.Equal(expected, snap.LastKnownPermission);
    }

    [Fact]
    public void Health_connect_with_nothing_read_yet_has_no_last_known_permission()
    {
        var a = new HealthConnectActivityAdapter(new Hc { Sdk = "garbled" }, () => T0);
        Assert.Null(a.SnapshotConnection().LastKnownPermission);
    }

    [Fact]
    public void Health_connect_ignores_an_unrecognised_or_failed_permission_read()
    {
        var b = new Hc();
        var a = new HealthConnectActivityAdapter(b, () => T0);
        a.SnapshotConnection(); // learns Granted
        b.Permission = "throw"; a.SnapshotConnection();
        b.Permission = "something-new"; a.SnapshotConnection();
        b.Sdk = "garbled";
        Assert.Equal(ActivityPermissionState.Granted, a.SnapshotConnection().LastKnownPermission);
    }

    [Fact]
    public void Health_connect_learns_a_revocation_seen_during_a_read()
    {
        var b = new Hc();
        var a = new HealthConnectActivityAdapter(b, () => T0);
        a.SnapshotConnection();
        b.Permission = "revoked";
        Assert.Throws<UnauthorizedAccessException>(() => a.FetchRecords(T0.AddDays(-1), T0));
        b.Sdk = "garbled";
        Assert.Equal(ActivityPermissionState.Revoked, a.SnapshotConnection().LastKnownPermission);
    }

    [Theory]
    [InlineData("unnecessary", ActivityPermissionState.Granted)]
    [InlineData("should_request", ActivityPermissionState.NotRequested)]
    public void HealthKit_carries_the_last_read_request_status_into_an_unreadable_snapshot(string raw, ActivityPermissionState expected)
    {
        var b = new Hk { Request = raw };
        var a = new HealthKitActivityAdapter(b, () => T0);
        a.SnapshotConnection();
        b.Availability = "weird";
        var snap = a.SnapshotConnection();
        Assert.Equal(ActivitySourceAvailability.TemporarilyUnavailable, snap.Availability);
        Assert.Equal(ActivityPermissionState.NotRequested, snap.Permission);
        Assert.Equal(expected, snap.LastKnownPermission);
    }

    [Fact]
    public void HealthKit_with_nothing_read_yet_or_an_unknown_answer_has_no_last_known_permission()
    {
        var b = new Hk { Request = "unknown" };
        var a = new HealthKitActivityAdapter(b, () => T0);
        a.SnapshotConnection();
        b.Availability = "weird";
        Assert.Null(a.SnapshotConnection().LastKnownPermission);
    }

    [Fact]
    public void An_unreadable_snapshot_after_a_grant_projects_to_a_refresh_failure_for_both_providers()
    {
        var hc = new Hc(); var hcA = new HealthConnectActivityAdapter(hc, () => T0); hcA.SnapshotConnection(); hc.Sdk = "garbled";
        var hk = new Hk(); var hkA = new HealthKitActivityAdapter(hk, () => T0); hkA.SnapshotConnection(); hk.Availability = "weird";
        foreach (var snap in new[] { hcA.SnapshotConnection(), hkA.SnapshotConnection() })
        {
            var m = ActivityStatusProjector.Project(snap, true, WalkGame.Domain.Activity.IngestionOutcomeKind.Succeeded, 0L, null);
            Assert.Equal(ActivityPlayerStatus.RefreshTemporarilyFailed, m.Status);
            Assert.False(m.PermissionGranted);
        }
    }
}
