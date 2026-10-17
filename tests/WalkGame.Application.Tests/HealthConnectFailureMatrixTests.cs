using System;
using System.Collections.Generic;
using WalkGame.Application.Activity;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-144: Health Connect failure-state UX as an EXHAUSTIVE matrix. The precedence table in
/// <see cref="ActivityStatusProjectionTests"/> samples one row per status; this enumerates every
/// permission x availability x last-outcome x processed combination and pins the player-facing
/// invariants: never "connected" without a grant and a working source, no dead end (every
/// fixable state offers an action), update-required dominates, and no failure state touches
/// earned progress. Automated verified (headless); real Health Connect UI UNVERIFIED (KI-002).
/// </summary>
public sealed class HealthConnectFailureMatrixTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    public static IEnumerable<object[]> Matrix()
    {
        foreach (var p in Enum.GetValues<ActivityPermissionState>())
        foreach (var a in Enum.GetValues<ActivitySourceAvailability>())
        foreach (var o in Enum.GetValues<IngestionOutcomeKind>())
        foreach (var processed in new[] { false, true })
            yield return new object[] { p, a, o, processed };
    }

    private static bool Granted(ActivityPermissionState p) =>
        p == ActivityPermissionState.Granted || p == ActivityPermissionState.PartiallyGranted;

    [Theory]
    [MemberData(nameof(Matrix))]
    public void EveryCombination_ProjectsCalmly_WithTheContractedActionAndNoFalseConnectedClaim(
        ActivityPermissionState p, ActivitySourceAvailability a, IngestionOutcomeKind o, bool processed)
    {
        var snap = new ActivityConnectionSnapshot(p, a, null, null, "raw provider detail");
        var m = ActivityStatusProjector.Project(snap, processed, o, 0L, null);

        Assert.True(Enum.IsDefined(m.Status));
        Assert.Equal(Granted(p), m.PermissionGranted);

        // Deterministic: the same inputs always give the same projection.
        var again = ActivityStatusProjector.Project(snap, processed, o, 0L, null);
        Assert.Equal(m.Status, again.Status);
        Assert.Equal(m.RecommendedAction, again.RecommendedAction);

        // Update-required dominates; waiting or reconnecting cannot fix it.
        if (a == ActivitySourceAvailability.ProviderUpdateRequired)
            Assert.Equal(ActivityPlayerStatus.ProviderUpdateRequired, m.Status);

        // Never claim the walk is flowing unless it really can.
        if (m.Status == ActivityPlayerStatus.ConnectedCurrent)
        {
            Assert.True(Granted(p));
            Assert.Equal(ActivitySourceAvailability.Available, a);
            Assert.True(processed);
            Assert.NotEqual(IngestionOutcomeKind.SourceFetchFailed, o);
        }
        if (!Granted(p))
            Assert.DoesNotContain(m.Status, new[]
            {
                ActivityPlayerStatus.ConnectedCurrent, ActivityPlayerStatus.WaitingForFirstData,
                ActivityPlayerStatus.RefreshTemporarilyFailed,
            });

        // No dead end: a fixable state always offers exactly its action; the rest offer none.
        var expected = m.Status switch
        {
            ActivityPlayerStatus.PermissionNeeded => ActivityRecommendedAction.Connect,
            ActivityPlayerStatus.PermissionDenied => ActivityRecommendedAction.OpenSettings,
            ActivityPlayerStatus.RefreshTemporarilyFailed => ActivityRecommendedAction.RetryLater,
            ActivityPlayerStatus.ProviderUpdateRequired => ActivityRecommendedAction.UpdateProvider,
            _ => ActivityRecommendedAction.None,
        };
        Assert.Equal(expected, m.RecommendedAction);

        // D-068 / D-146: with no provider, never ask to connect or to open settings, even
        // after a prior denial or revocation; the missing source is the thing to fix.
        if (a == ActivitySourceAvailability.Unsupported)
        {
            Assert.Equal(ActivityPlayerStatus.SourceUnavailable, m.Status);
            Assert.Equal(ActivityRecommendedAction.None, m.RecommendedAction);
        }
    }

    [Theory]
    [InlineData(ActivityPermissionState.Denied)]
    [InlineData(ActivityPermissionState.Revoked)]
    public void D146_UninstalledProvider_AfterPriorDenial_ShowsSourceUnavailable_NotOpenSettings(ActivityPermissionState p)
    {
        var snap = new ActivityConnectionSnapshot(p, ActivitySourceAvailability.Unsupported, null, null, null);
        var m = ActivityStatusProjector.Project(snap, true, IngestionOutcomeKind.NeverRun, 0L, null);
        Assert.Equal(ActivityPlayerStatus.SourceUnavailable, m.Status);
        Assert.NotEqual(ActivityRecommendedAction.OpenSettings, m.RecommendedAction);
    }

    [Theory]
    [InlineData(ActivityPermissionState.Denied)]
    [InlineData(ActivityPermissionState.Revoked)]
    public void D146_InstalledProvider_AfterDenial_StillOffersOpenSettings(ActivityPermissionState p)
    {
        var snap = new ActivityConnectionSnapshot(p, ActivitySourceAvailability.Available, null, null, null);
        var m = ActivityStatusProjector.Project(snap, true, IngestionOutcomeKind.NeverRun, 0L, null);
        Assert.Equal(ActivityPlayerStatus.PermissionDenied, m.Status);
        Assert.Equal(ActivityRecommendedAction.OpenSettings, m.RecommendedAction);
    }

    [Fact]
    public void EveryFailureState_LeavesEarnedProgressUntouched_AndRecoveryCreditsOnlyNewWalks()
    {
        var t0 = TestSessions.T0;
        var clock = new ManualClock(t0);
        var port = new FakeActivityConnectionPort { Permission = ActivityPermissionState.Granted };
        var s = TestSessions.Create(_temp.Path, clock, new LocalPreferencesStore(_temp.Path), port);
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(seed: 7UL).Status);
        s.EnqueueProject(TestSessions.EntryProjectId);
        s.IngestActivityBatch(new[] { Rec("a", 8000, t0.AddHours(-1)) });
        long lifetime = s.GetDiagnostics().LifetimeVitalityCredited;
        long invested = s.GetHome().ActiveProjectInvested;
        Assert.True(lifetime > 0);

        foreach (var p in Enum.GetValues<ActivityPermissionState>())
        foreach (var a in Enum.GetValues<ActivitySourceAvailability>())
        {
            port.Permission = p;
            port.Availability = a;
            var status = s.GetActivityStatus();
            Assert.True(Enum.IsDefined(status.Status));
            Assert.Equal(lifetime, s.GetDiagnostics().LifetimeVitalityCredited);
            Assert.Equal(invested, s.GetHome().ActiveProjectInvested);

            // A restart in that state loads the same world.
            var reboot = TestSessions.Create(_temp.Path, clock, new LocalPreferencesStore(_temp.Path), port);
            Assert.Equal(StartStatus.Loaded, reboot.Continue().Status);
            Assert.Equal(lifetime, reboot.GetDiagnostics().LifetimeVitalityCredited);
        }

        port.Permission = ActivityPermissionState.Granted;
        port.Availability = ActivitySourceAvailability.Available;
        clock.Set(t0.AddHours(4));
        var back = s.IngestActivityBatch(new[] { Rec("a", 8000, t0.AddHours(-1)), Rec("b", 2000, t0.AddHours(3)) });
        Assert.Equal(1, back.Accepted);
        Assert.Equal(1, back.DuplicatesIgnored);
        Assert.Equal(ActivityPlayerStatus.ConnectedCurrent, s.GetActivityStatus().Status);
    }

    private static NormalizedActivityRecord Rec(string id, long qty, DateTimeOffset end) =>
        new("p", id, ActivityCategory.Walking, ActivityUnits.Steps, qty, end.AddMinutes(-20), end, 1, false, 8 * 3600);
}
