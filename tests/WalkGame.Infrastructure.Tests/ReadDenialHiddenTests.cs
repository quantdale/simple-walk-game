using System;
using WalkGame.Infrastructure.Platform.HealthConnect;
using WalkGame.Infrastructure.Platform.HealthKit;

namespace WalkGame.Infrastructure.Tests;

/// <summary>D-196: HealthKit snapshots say a refused read is invisible; Health Connect snapshots never do.</summary>
public sealed class ReadDenialHiddenTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 17, 0, 0, 0, TimeSpan.Zero);

    private sealed class Hc : IHealthConnectBridge
    {
        public string Sdk = "available", Permission = "granted";
        public string GetSdkStatus() => Sdk;
        public string GetPermissionStatus() => Permission;
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
    [InlineData("available", "unnecessary")]
    [InlineData("available", "should_request")]
    [InlineData("unavailable", "unnecessary")]
    [InlineData("garbled", "unnecessary")]
    public void Every_healthkit_snapshot_marks_read_denial_hidden(string availability, string request) =>
        Assert.True(new HealthKitActivityAdapter(new Hk { Availability = availability, Request = request }, () => T0).SnapshotConnection().ReadDenialHidden);

    [Theory]
    [InlineData("available", "granted")]
    [InlineData("available", "revoked")]
    [InlineData("unavailable", "granted")]
    [InlineData("garbled", "granted")]
    public void No_health_connect_snapshot_marks_read_denial_hidden(string sdk, string permission) =>
        Assert.False(new HealthConnectActivityAdapter(new Hc { Sdk = sdk, Permission = permission }, () => T0).SnapshotConnection().ReadDenialHidden);
}
