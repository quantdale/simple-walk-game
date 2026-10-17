using System;
using System.IO;
using WalkGame.Application.Activity;
using WalkGame.Application.Content;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-143: adverse lifecycle in ONE live process. Earlier suites model process boundaries
/// (M8LifecycleHeadlessTests) or a backward clock across reloads (TemporalAnomalyTests).
/// These keep the same <see cref="GameSession"/> while the player crosses time zones, the
/// device clock jumps, and permission is revoked between two syncs.
/// Automated verified (headless). Device behaviour stays UNVERIFIED (KI-002).
/// </summary>
public sealed class TimezoneTravelLifecycleTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 3, 10, 8, 0, 0, TimeSpan.Zero);
    private const int Manila = 8 * 3600;
    private const int LosAngeles = -7 * 3600;
    private const int Auckland = 13 * 3600;
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    [Fact]
    public void FlyingWest_EachWalkCountsOnItsOwnLocalDay_AndVitalityIsOffsetIndependent()
    {
        var clock = new ManualClock(T0);
        var s = Start(clock);
        // Mar 10 15:00 Manila, then after the flight Mar 10 09:00 Los Angeles (= Mar 11 00:00 Manila).
        var r1 = s.IngestActivityBatch(new[] { Rec("manila", 4000, T0.AddHours(-1), Manila) });
        clock.Set(T0.AddHours(9));
        var r2 = s.IngestActivityBatch(new[] { Rec("la", 4000, T0.AddHours(8), LosAngeles) });

        // Both walks are on local Mar 10 for the walker: one day, not two.
        Assert.Equal(1, s.GetDaysWalked().Days);

        // A control world walking the same UTC instants without travel credits identically.
        using var control = new TempDirectory();
        var cs = TestSessions.Create(control, new ManualClock(T0.AddHours(9)));
        cs.StartNewGame(seed: 7UL);
        var c = cs.IngestActivityBatch(new[] { Rec("manila", 4000, T0.AddHours(-1), 0), Rec("la", 4000, T0.AddHours(8), 0) });
        Assert.Equal(c.VitalityCredited, r1.VitalityCredited + r2.VitalityCredited);
        AssertValid();
    }

    [Fact]
    public void CrossingTheDateLineEast_DoesNotInventOrLoseDays_AcrossReload()
    {
        var clock = new ManualClock(T0);
        var s = Start(clock);
        clock.Set(T0.AddHours(13));
        s.IngestActivityBatch(new[]
        {
            Rec("a", 3000, T0.AddHours(-2), Manila),     // Mar 10 Manila
            Rec("b", 3000, T0.AddHours(12), Auckland),   // Mar 11 09:00 Auckland
        });
        Assert.Equal(2, s.GetDaysWalked().Days);
        var reloaded = TestSessions.Create(_temp, clock);
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);
        Assert.Equal(2, reloaded.GetDaysWalked().Days);
        AssertValid();
    }

    [Fact]
    public void ReplayAfterTravel_WithTheProviderReportingANewOffset_IsAnExactlyOnceNoOp()
    {
        var clock = new ManualClock(T0);
        var s = Start(clock);
        var first = s.IngestActivityBatch(new[] { Rec("walk", 6000, T0.AddHours(-1), Manila) });
        Assert.True(first.VitalityCredited > 0);
        long lifetime = s.GetDiagnostics().LifetimeVitalityCredited;

        // Same record, same revision, re-read after travel; the provider re-labels its offset.
        clock.Set(T0.AddHours(20));
        var replay = s.IngestActivityBatch(new[] { Rec("walk", 6000, T0.AddHours(-1), LosAngeles) });
        Assert.Equal(0L, replay.VitalityCredited);
        Assert.Equal(lifetime, s.GetDiagnostics().LifetimeVitalityCredited);
        AssertValid();
    }

    [Fact]
    public void ClockJumpsBackAndForwardInOneSession_NeverDoubleCreditsOrRegressesTheCheckpoint()
    {
        var clock = new ManualClock(T0);
        var s = Start(clock);
        s.IngestActivityBatch(new[] { Rec("a", 5000, T0.AddHours(-1), Manila) });
        var checkpoint = State().IngestionCheckpointUtc;
        long lifetime = s.GetDiagnostics().LifetimeVitalityCredited;

        clock.Set(T0.AddDays(-2)); // user sets the clock back two days, app still open
        var back = s.IngestActivityBatch(new[] { Rec("a", 5000, T0.AddHours(-1), Manila) });
        Assert.Equal(0L, back.VitalityCredited);
        Assert.True(State().IngestionCheckpointUtc >= checkpoint);
        Assert.Equal(lifetime, s.GetDiagnostics().LifetimeVitalityCredited);

        clock.Set(T0.AddDays(1)); // clock restored
        var fwd = s.IngestActivityBatch(new[] { Rec("a", 5000, T0.AddHours(-1), Manila), Rec("b", 5000, T0.AddHours(20), Manila) });
        Assert.Equal(1, fwd.DuplicatesIgnored);
        Assert.True(fwd.VitalityCredited > 0);
        Assert.True(State().IngestionCheckpointUtc >= checkpoint);
        AssertValid();
    }

    [Fact]
    public void PermissionRevokedBetweenTwoSyncsInOneSession_KeepsProgress_AndRegrantCreditsOnlyTheGap()
    {
        var clock = new ManualClock(T0);
        var port = new FakeActivityConnectionPort { Permission = ActivityPermissionState.Granted };
        var s = TestSessions.Create(_temp.Path, clock, null, port);
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(seed: 7UL).Status);
        s.IngestActivityBatch(new[] { Rec("a", 5000, T0.AddHours(-1), Manila) });
        long lifetime = s.GetDiagnostics().LifetimeVitalityCredited;

        port.Permission = ActivityPermissionState.Revoked; // revoked from the system shade
        clock.Set(T0.AddHours(3));
        Assert.Equal(ActivityPlayerStatus.PermissionDenied, s.GetActivityStatus().Status);
        Assert.Equal(lifetime, s.GetDiagnostics().LifetimeVitalityCredited);
        Assert.Empty(GameStateValidator.Validate(State(), Region1Catalog.Create()));

        port.Permission = ActivityPermissionState.Granted;
        clock.Set(T0.AddHours(6));
        var back = s.IngestActivityBatch(new[] { Rec("a", 5000, T0.AddHours(-1), Manila), Rec("b", 5000, T0.AddHours(5), Manila) });
        Assert.Equal(1, back.Accepted);
        Assert.Equal(1, back.DuplicatesIgnored);
        AssertValid();
    }

    private static NormalizedActivityRecord Rec(string id, long qty, DateTimeOffset end, int? offset) =>
        new("p", id, ActivityCategory.Walking, ActivityUnits.Steps, qty, end.AddMinutes(-20), end, 1, false, offset);

    private GameSession Start(ManualClock clock)
    {
        var s = TestSessions.Create(_temp, clock);
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(seed: 7UL).Status);
        return s;
    }

    private GameState State() =>
        TestSessions.NewCodec().Decode(File.ReadAllBytes(Path.Combine(_temp.Path, "save.json"))).State!;

    private void AssertValid() => Assert.Empty(GameStateValidator.Validate(State(), Region1Catalog.Create()));
}
