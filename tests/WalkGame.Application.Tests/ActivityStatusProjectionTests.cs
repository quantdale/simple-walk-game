using System;
using WalkGame.Application.Activity;
using WalkGame.Application.ReadModels;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Application.Tests;

/// <summary>
/// Workstream C contract: the player-safe activity connection classification, its
/// documented precedence, rapid-transition determinism, and the durable refresh-failure
/// path — all exactly-once safe and progression-neutral.
/// </summary>
public sealed class ActivityStatusProjectionTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ManualClock _clock = new(TestSessions.T0);

    public void Dispose() => _temp.Dispose();

    private GameSession NewSession(FakeActivityConnectionPort port) =>
        TestSessions.Create(_temp.Path, _clock, new LocalPreferencesStore(_temp.Path), port);

    [Fact]
    public void Projector_DocumentPrecedenceTable_ClassifiesEveryCombination()
    {
        DateTimeOffset? noTime = null;

        (ActivityPermissionState perm, ActivitySourceAvailability avail, IngestionOutcomeKind last, bool processed, ActivityPlayerStatus expect)[] cases =
        {
            (ActivityPermissionState.Denied, ActivitySourceAvailability.Available, IngestionOutcomeKind.NeverRun, false, ActivityPlayerStatus.PermissionDenied),
            (ActivityPermissionState.Revoked, ActivitySourceAvailability.Available, IngestionOutcomeKind.Succeeded, true, ActivityPlayerStatus.PermissionDenied),
            (ActivityPermissionState.NotRequested, ActivitySourceAvailability.Available, IngestionOutcomeKind.NeverRun, false, ActivityPlayerStatus.PermissionNeeded),
            (ActivityPermissionState.Granted, ActivitySourceAvailability.Unsupported, IngestionOutcomeKind.Succeeded, true, ActivityPlayerStatus.SourceUnavailable),
            (ActivityPermissionState.Granted, ActivitySourceAvailability.TemporarilyUnavailable, IngestionOutcomeKind.NeverRun, true, ActivityPlayerStatus.RefreshTemporarilyFailed),
            (ActivityPermissionState.Granted, ActivitySourceAvailability.Available, IngestionOutcomeKind.SourceFetchFailed, true, ActivityPlayerStatus.RefreshTemporarilyFailed),
            (ActivityPermissionState.Granted, ActivitySourceAvailability.Available, IngestionOutcomeKind.NeverRun, false, ActivityPlayerStatus.WaitingForFirstData),
            (ActivityPermissionState.Granted, ActivitySourceAvailability.Available, IngestionOutcomeKind.Succeeded, true, ActivityPlayerStatus.ConnectedCurrent),
            (ActivityPermissionState.PartiallyGranted, ActivitySourceAvailability.Available, IngestionOutcomeKind.Succeeded, true, ActivityPlayerStatus.ConnectedCurrent),
        };

        foreach (var c in cases)
        {
            var snapshot = new ActivityConnectionSnapshot(c.perm, c.avail);
            var model = ActivityStatusProjector.Project(snapshot, c.processed, c.last, 0L, noTime);
            Assert.Equal(c.expect, model.Status);
        }
    }

    [Fact]
    public void Projector_RecommendedActionMatchesStatus()
    {
        Assert.Equal(ActivityRecommendedAction.Connect,
            RecommendFor(ActivityPlayerStatus.PermissionNeeded));
        Assert.Equal(ActivityRecommendedAction.OpenSettings,
            RecommendFor(ActivityPlayerStatus.PermissionDenied));
        Assert.Equal(ActivityRecommendedAction.RetryLater,
            RecommendFor(ActivityPlayerStatus.RefreshTemporarilyFailed));
        Assert.Equal(ActivityRecommendedAction.None,
            RecommendFor(ActivityPlayerStatus.ConnectedCurrent));
        Assert.Equal(ActivityRecommendedAction.None,
            RecommendFor(ActivityPlayerStatus.WaitingForFirstData));
        Assert.Equal(ActivityRecommendedAction.None,
            RecommendFor(ActivityPlayerStatus.SourceUnavailable));
    }

    private static ActivityRecommendedAction RecommendFor(ActivityPlayerStatus status)
    {
        var snapshot = new ActivityConnectionSnapshot(
            status == ActivityPlayerStatus.PermissionNeeded ? ActivityPermissionState.NotRequested
            : status == ActivityPlayerStatus.PermissionDenied ? ActivityPermissionState.Denied
            : ActivityPermissionState.Granted,
            status == ActivityPlayerStatus.SourceUnavailable ? ActivitySourceAvailability.Unsupported
            : status == ActivityPlayerStatus.RefreshTemporarilyFailed ? ActivitySourceAvailability.TemporarilyUnavailable
            : ActivitySourceAvailability.Available);

        bool processed = status != ActivityPlayerStatus.WaitingForFirstData;
        return ActivityStatusProjector.Project(snapshot, processed, IngestionOutcomeKind.NeverRun, 0L, null)
            .RecommendedAction;
    }

    [Fact]
    public void RapidTransitions_ConvergeDeterministically()
    {
        var port = new FakeActivityConnectionPort();
        var session = NewSession(port);
        session.StartNewGame(7UL);

        // needed → denied → connected(granted) → unavailable → connected again.
        port.Permission = ActivityPermissionState.NotRequested;
        Assert.Equal(ActivityPlayerStatus.PermissionNeeded, session.GetActivityStatus().Status);

        port.Permission = ActivityPermissionState.Denied;
        var denied = session.GetActivityStatus();
        Assert.Equal(ActivityPlayerStatus.PermissionDenied, denied.Status);
        Assert.True(denied.Equals(denied));

        port.Permission = ActivityPermissionState.Granted;
        Assert.Equal(ActivityPlayerStatus.WaitingForFirstData, session.GetActivityStatus().Status);

        port.Availability = ActivitySourceAvailability.TemporarilyUnavailable;
        Assert.Equal(ActivityPlayerStatus.RefreshTemporarilyFailed, session.GetActivityStatus().Status);

        port.Permission = ActivityPermissionState.Granted;
        port.Availability = ActivitySourceAvailability.Available;

        // Records must already be complete relative to the injected clock.
        _clock.Set(TestSessions.T0.AddHours(2));
        session.IngestFromSource(new StaticRecordSource(
            M5H1Records.Steps(5000L, TestSessions.T0, TimeSpan.FromHours(1), sourceId: "s1")),
            TestSessions.T0, TestSessions.T0.AddDays(1));

        var current = session.GetActivityStatus();
        Assert.Equal(ActivityPlayerStatus.ConnectedCurrent, current.Status);
        Assert.True(current.HasProcessedAnyRecord);
        Assert.Equal(IngestionOutcomeKind.Succeeded, current.LastOutcome);
        Assert.Equal(50L, current.LastBatchVitalityCredited);
        Assert.NotNull(current.LastProcessedAtUtc);
    }

    [Fact]
    public void ZeroRecords_WithMatureHistory_StaysConnectedCurrent()
    {
        var port = new FakeActivityConnectionPort { Permission = ActivityPermissionState.Granted };
        var session = NewSession(port);
        session.StartNewGame(7UL);

        _clock.Set(TestSessions.T0.AddDays(2).AddHours(-1));
        session.IngestFromSource(new StaticRecordSource(
                M5H1Records.Steps(1000L, TestSessions.T0.AddDays(-2), TimeSpan.FromHours(2), sourceId: "old")),
            TestSessions.T0.AddDays(-3), TestSessions.T0.AddDays(-1));

        // A later empty window must not regress the standing classification, even though
        // it replaces the last-batch aggregate with an empty one.
        _clock.Set(TestSessions.T0.AddDays(2));
        session.IngestFromSource(new StaticRecordSource(), TestSessions.T0, TestSessions.T0.AddDays(1));

        var status = session.GetActivityStatus();
        Assert.Equal(ActivityPlayerStatus.ConnectedCurrent, status.Status);
        Assert.Equal(0L, status.LastBatchVitalityCredited);
        Assert.True(status.HasProcessedAnyRecord);
        Assert.Equal(10L, session.GetDiagnostics().LifetimeVitalityCredited);
    }

    [Fact]
    public void PermissionDenied_DoesNotTrapNavigation_ReadModelsRemainAvailable()
    {
        var port = new FakeActivityConnectionPort { Permission = ActivityPermissionState.Denied };
        var session = NewSession(port);
        session.StartNewGame(7UL);

        Assert.NotNull(session.GetHome());
        Assert.NotNull(session.GetProjects());
        Assert.NotNull(session.GetRegion());
        Assert.NotNull(session.GetDiscoveries());
        Assert.NotNull(session.GetExpeditions());

        var onboarding = session.GetOnboarding();
        Assert.Equal(OnboardingActivityStepState.Denied, onboarding.ActivityStep);
        Assert.True(onboarding.NavigableDespitePermissionDenied);

        // Denial never fabricates credit and never mutates canonical state.
        Assert.Equal(0L, session.GetHome().Vitality);
    }

    [Fact]
    public void ExternalRevocation_IsRepresentableWithoutMutatingEarnedProgress()
    {
        var port = new FakeActivityConnectionPort { Permission = ActivityPermissionState.Granted };
        var session = NewSession(port);
        session.StartNewGame(7UL);

        _clock.Set(TestSessions.T0.AddHours(6));
        session.IngestFromSource(new StaticRecordSource(
                M5H1Records.Steps(8000L, TestSessions.T0, TimeSpan.FromHours(3), sourceId: "r1")),
            TestSessions.T0, TestSessions.T0.AddHours(6));
        long earned = session.GetHome().Vitality;
        Assert.Equal(80L, earned);

        // The OS revoked permission outside the app.
        port.Permission = ActivityPermissionState.Revoked;

        Assert.Equal(ActivityPlayerStatus.PermissionDenied, session.GetActivityStatus().Status);
        Assert.Equal(ActivityRecommendedAction.OpenSettings, session.GetActivityStatus().RecommendedAction);
        Assert.Equal(earned, session.GetHome().Vitality);

        // Reconnect is representable: grant returns a usable state, progress intact.
        port.Permission = ActivityPermissionState.Granted;
        Assert.Equal(ActivityPlayerStatus.ConnectedCurrent, session.GetActivityStatus().Status);
        Assert.Equal(earned, session.GetHome().Vitality);
    }

    [Fact]
    public void SourceFetchFailure_IsDurablyClassified_AndRetryStaysExactlyOnce()
    {
        var port = new FakeActivityConnectionPort { Permission = ActivityPermissionState.Granted };
        var session = NewSession(port);
        session.StartNewGame(7UL);

        _clock.Set(TestSessions.T0.AddHours(2));
        session.IngestFromSource(new StaticRecordSource(
                M5H1Records.Steps(3000L, TestSessions.T0, TimeSpan.FromHours(1), sourceId: "ok-1")),
            TestSessions.T0, TestSessions.T0.AddHours(2));

        _clock.Set(TestSessions.T0.AddHours(4));
        Assert.Throws<IOException>(() => session.IngestFromSource(
            new ThrowingRecordSource(new IOException("provider socket exploded")),
            TestSessions.T0.AddHours(2), TestSessions.T0.AddHours(4)));

        Assert.Equal(ActivityPlayerStatus.RefreshTemporarilyFailed, session.GetActivityStatus().Status);

        // Failure evidence survives restart.
        var reloaded = TestSessions.Create(_temp.Path, _clock, new LocalPreferencesStore(_temp.Path), port);
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);
        Assert.Equal(ActivityPlayerStatus.RefreshTemporarilyFailed, reloaded.GetActivityStatus().Status);
        Assert.Equal(30L, reloaded.GetHome().Vitality);

        // A later successful retry processes only genuinely new records exactly once.
        var retry = reloaded.IngestFromSource(new StaticRecordSource(
                M5H1Records.Steps(2000L, TestSessions.T0.AddHours(2), TimeSpan.FromHours(1), sourceId: "after-fail")),
            TestSessions.T0.AddHours(2), TestSessions.T0.AddHours(5));
        Assert.Equal(1, retry.Accepted);
        Assert.Equal(20L, retry.VitalityCredited);
        Assert.Equal(ActivityPlayerStatus.ConnectedCurrent, reloaded.GetActivityStatus().Status);
        Assert.Equal(50L, reloaded.GetHome().Vitality);
    }

    [Fact]
    public void GetActivityStatus_WithoutConfiguredPort_ThrowsWithStableCode()
    {
        var session = TestSessions.Create(_temp, _clock);
        session.StartNewGame(7UL);

        Assert.Contains(UxErrorCodes.ConnectionPortMissing,
            Assert.Throws<InvalidOperationException>(() => session.GetActivityStatus()).Message);
    }

    [Fact]
    public void StatusReads_AreSideEffectFree_SaveBytesUnchanged()
    {
        var port = new FakeActivityConnectionPort { Permission = ActivityPermissionState.Granted };
        var session = NewSession(port);
        session.StartNewGame(7UL);
        session.IngestFromSource(new StaticRecordSource(
                M5H1Records.Steps(4000L, TestSessions.T0, TimeSpan.FromHours(1), sourceId: "fx")),
            TestSessions.T0, TestSessions.T0.AddHours(3));

        System.IO.File.Copy(System.IO.Path.Combine(_temp.Path, "save.json"),
            System.IO.Path.Combine(_temp.Path, "snapshot.json"), overwrite: true);

        for (int i = 0; i < 5; i++)
        {
            _ = session.GetActivityStatus();
            _ = session.GetDiagnostics();
            _ = session.GetOnboarding();
            _ = session.GetSettings();
            _ = session.GetHome();
        }

        Assert.Equal(
            System.IO.File.ReadAllBytes(System.IO.Path.Combine(_temp.Path, "snapshot.json")),
            System.IO.File.ReadAllBytes(System.IO.Path.Combine(_temp.Path, "save.json")));
    }
}
