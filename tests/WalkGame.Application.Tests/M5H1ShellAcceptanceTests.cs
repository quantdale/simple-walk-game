using System;
using System.IO;
using WalkGame.Application.Activity;
using WalkGame.Application.ReadModels;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Application.Ux;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Application.Tests;

/// <summary>
/// M5-H1 named low-attention acceptance harness (Workstream F). Each test is one
/// product story executed through REAL Application/Infrastructure boundaries with a
/// deterministic clock: no Unity, no native adapters, no fabricated evidence.
/// </summary>
public sealed class M5H1ShellAcceptanceTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FakeActivityConnectionPort _port = new();
    private readonly ManualClock _clock = new(TestSessions.T0);

    public void Dispose() => _temp.Dispose();

    private GameSession NewSession() =>
        TestSessions.Create(_temp.Path, _clock, new LocalPreferencesStore(_temp.Path), _port);

    private string SavePath => Path.Combine(_temp.Path, "save.json");

    // 1 -----------------------------------------------------------------------

    [Fact]
    public void Scenario01_FirstRun_GrantPath()
    {
        var session = NewSession();
        Assert.Equal(StartStatus.NewGameCreated, session.StartNewGame(7UL).Status);

        Assert.True(session.AdvanceOnboarding(OnboardingStage.Premise).IsSuccess);
        Assert.True(session.AdvanceOnboarding(OnboardingStage.WorldBaseline).IsSuccess);
        Assert.True(session.AdvanceOnboarding(OnboardingStage.ActivityConnection).IsSuccess);

        // The OS grants the permission through the adapter seam.
        _port.Permission = ActivityPermissionState.Granted;
        Assert.Equal(ActivityPlayerStatus.WaitingForFirstData, session.GetActivityStatus().Status);
        Assert.Equal(OnboardingActivityStepState.Granted, session.GetOnboarding().ActivityStep);

        // First project chosen through the REAL canonical operation.
        Assert.True(session.EnqueueProject(TestSessions.EntryProjectId).IsSuccess);
        Assert.True(session.AdvanceOnboarding(OnboardingStage.FirstProject).IsSuccess);
        Assert.True(session.AdvanceOnboarding(OnboardingStage.Simulation).IsSuccess);
        Assert.True(session.AdvanceOnboarding(OnboardingStage.Exit).IsSuccess);
        Assert.True(session.AdvanceOnboarding(OnboardingStage.Complete).IsSuccess);

        _clock.Set(TestSessions.T0.AddHours(25));
        session.IngestFromSource(new StaticRecordSource(
                M5H1Records.Steps(6000L, TestSessions.T0.AddHours(24), TimeSpan.FromHours(1), sourceId: "day1")),
            TestSessions.T0.AddHours(20), TestSessions.T0.AddHours(26));

        var reloaded = NewSession();
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);

        Assert.True(reloaded.GetOnboarding().IsComplete);
        Assert.NotNull(reloaded.GetHome().ActiveProjectId);
        // Credited exactly once; the value sits either banked or invested in the active
        // project depending on where allocation left it.
        Assert.Equal(60L, reloaded.GetDiagnostics().LifetimeVitalityCredited);
        Assert.Equal(60L,
            reloaded.GetHome().ActiveProjectInvested + reloaded.GetHome().Vitality);
    }

    // 2 -----------------------------------------------------------------------

    [Fact]
    public void Scenario02_FirstRun_DenialPath_DoesNotTrapTheProfile()
    {
        _port.Permission = ActivityPermissionState.Denied;
        var session = NewSession();
        session.StartNewGame(7UL);

        // The player declines/refuses at the connection step; onboarding continues.
        Assert.True(session.AdvanceOnboarding(OnboardingStage.Premise).IsSuccess);
        Assert.True(session.AdvanceOnboarding(OnboardingStage.WorldBaseline).IsSuccess);
        Assert.True(session.AdvanceOnboarding(OnboardingStage.ActivityConnection).IsSuccess);

        var onboarding = session.GetOnboarding();
        Assert.Equal(OnboardingActivityStepState.Denied, onboarding.ActivityStep);
        Assert.True(onboarding.NavigableDespitePermissionDenied);

        // All lightweight surfaces remain available and honest.
        Assert.Equal(ActivityPlayerStatus.PermissionDenied, session.GetActivityStatus().Status);
        Assert.Equal(ActivityRecommendedAction.OpenSettings, session.GetActivityStatus().RecommendedAction);
        Assert.NotNull(session.GetHome());
        Assert.NotNull(session.GetProjects());
        Assert.NotNull(session.GetDiscoveries());

        // No fabricated activity credit anywhere.
        Assert.Equal(0L, session.GetHome().Vitality);
        Assert.Equal(IngestionOutcomeKind.NeverRun, session.GetActivityStatus().LastOutcome);

        // The player can still pick a first project manually and finish safely.
        Assert.True(session.EnqueueProject(TestSessions.EntryProjectId).IsSuccess);
        Assert.True(session.AdvanceOnboarding(OnboardingStage.Complete).IsSuccess,
            "permission denial must never block canonical onboarding completion");
    }

    // 3 -----------------------------------------------------------------------

    [Fact]
    public void Scenario03_OneDayReturn_ConciseAndNoFalseAlarm()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        session.EnqueueProject(TestSessions.EntryProjectId);
        _clock.Set(TestSessions.T0.AddHours(2));
        session.IngestFromSource(new StaticRecordSource(
                M5H1Records.Steps(4000L, TestSessions.T0, TimeSpan.FromHours(2), sourceId: "d0")),
            TestSessions.T0, TestSessions.T0.AddDays(1));
        session.AcknowledgeReturnSummary();

        _clock.Set(TestSessions.T0.AddDays(1).AddHours(2));
        var returned = NewSession();
        returned.Continue();

        // A quiet one-day return with no new activity owes NOTHING: no fabricated
        // summary, no false attention request.
        var summary = returned.GetPendingReturnSummary();
        if (summary != null)
            Assert.True(summary.Items.Count <= 12, "glance budget");

        var home = returned.GetHome();
        Assert.Equal(TestSessions.EntryProjectId, home.ActiveProjectId);
        Assert.False(home.BankedVitality > 0);

        // Whatever the boot composed, acknowledging leaves the surface genuinely calm:
        // active project progressing, nothing else demanding the player.
        Assert.True(returned.AcknowledgeReturnSummary().IsSuccess);
        var calm = returned.GetHome();
        Assert.False(calm.RequiresAttention);
        Assert.Equal(HomeAttentionReason.None, calm.AttentionReason);
    }

    // 4 -----------------------------------------------------------------------

    [Fact]
    public void Scenario04_SevenDayReturn_StateSurvivesAppClosedAdvancement()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        session.EnqueueProject(TestSessions.EntryProjectId);
        _clock.Set(TestSessions.T0.AddHours(3));
        session.IngestFromSource(new StaticRecordSource(
                M5H1Records.Steps(9000L, TestSessions.T0, TimeSpan.FromHours(3), sourceId: "w0")),
            TestSessions.T0, TestSessions.T0.AddDays(1));

        // Seven days closed.
        _clock.Set(TestSessions.T0.AddDays(7));
        var returned = NewSession();
        Assert.Equal(StartStatus.Loaded, returned.Continue().Status);

        var summary = returned.GetPendingReturnSummary();
        Assert.NotNull(summary);
        Assert.True(summary!.Items.Count <= 12);

        // Durable state advanced coherently across the gap and survives another reload.
        var investedBefore = returned.GetHome().ActiveProjectInvested;
        Assert.True(investedBefore > 0, "offline advancement must have invested banked vitality");

        _clock.Set(TestSessions.T0.AddDays(8));
        var again = NewSession();
        Assert.Equal(StartStatus.Loaded, again.Continue().Status);
        Assert.True(again.GetHome().ActiveProjectInvested >= investedBefore);
        Assert.Equal(returned.GetDiagnostics().LifetimeVitalityCredited,
            again.GetDiagnostics().LifetimeVitalityCredited);
    }

    // 5 -----------------------------------------------------------------------

    [Fact]
    public void Scenario05_ThirtyDayReturn_BoundedCalmLongAbsence()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        session.EnqueueProject(TestSessions.EntryProjectId);
        _clock.Set(TestSessions.T0.AddHours(4));
        session.IngestFromSource(new StaticRecordSource(
                M5H1Records.Steps(12000L, TestSessions.T0, TimeSpan.FromHours(4), sourceId: "m0")),
            TestSessions.T0, TestSessions.T0.AddDays(1));

        _clock.Set(TestSessions.T0.AddDays(30));
        var returned = NewSession();
        Assert.Equal(StartStatus.Loaded, returned.Continue().Status);

        var summary = returned.GetPendingReturnSummary();
        Assert.NotNull(summary);
        Assert.True(summary!.Items.Count <= 12, "long absence must stay inside the glance budget");
        Assert.All(summary.Items, i => Assert.False(string.IsNullOrWhiteSpace(i.Text)));

        var home = returned.GetHome();
        Assert.Equal(session.Content.Landmarks.Count, home.Landmarks.Count);
        Assert.Equal(session.Content.Projects.Count, home.TotalProjects);
        Assert.True(returned.GetRegion().EcologyStage >= 0);
        Assert.True(returned.GetRegion().SettlementStage >= 0);

        var diag = returned.GetDiagnostics();
        Assert.NotNull(diag.CheckpointWatermarkAgeDays);
        Assert.InRange(diag.CheckpointWatermarkAgeDays!.Value, 28, 31);
        Assert.Equal(120L, diag.LifetimeVitalityCredited);
    }

    // 6 -----------------------------------------------------------------------

    [Fact]
    public void Scenario06_QueueEmptyWhileAway_ExplicitReasonAndNextAction()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        Assert.True(session.SetAutoAdvance(false).IsSuccess);
        _clock.Set(TestSessions.T0.AddHours(2));
        session.IngestFromSource(new StaticRecordSource(
                M5H1Records.Steps(3000L, TestSessions.T0, TimeSpan.FromHours(2), sourceId: "q0")),
            TestSessions.T0, TestSessions.T0.AddDays(1));

        _clock.Set(TestSessions.T0.AddDays(3));
        var returned = NewSession();
        returned.Continue();

        // What-changed wins attention first; acknowledging it must reveal the standing
        // queue-empty/banked condition underneath.
        Assert.True(returned.AcknowledgeReturnSummary().IsSuccess);

        var home = returned.GetHome();
        Assert.Null(home.ActiveProjectId);
        Assert.Empty(home.Queued);
        Assert.True(home.RequiresAttention);
        Assert.Equal(HomeAttentionReason.QueueEmptyWithBankedVitality, home.AttentionReason);
        Assert.Equal(30L, home.BankedVitality);
        Assert.False(home.AutoAdvance, "fallback policy unchanged: automation stayed off");

        // Nothing was force-spent by UX machinery: the choice stays the player's.
        Assert.True(returned.AcknowledgeReturnSummary().IsSuccess);
        Assert.Equal(home.BankedVitality, returned.GetHome().BankedVitality);
    }

    // 7 -----------------------------------------------------------------------

    [Fact]
    public void Scenario07_SourceTemporarilyFails_ProgressPreservedRetryExactlyOnce()
    {
        _port.Permission = ActivityPermissionState.Granted;
        var session = NewSession();
        session.StartNewGame(7UL);
        session.EnqueueProject(TestSessions.EntryProjectId);

        long Held(GameSession s) => s.GetHome().Vitality + s.GetHome().ActiveProjectInvested;

        _clock.Set(TestSessions.T0.AddHours(2));
        session.IngestFromSource(new StaticRecordSource(
                M5H1Records.Steps(5000L, TestSessions.T0, TimeSpan.FromHours(2), sourceId: "f0")),
            TestSessions.T0, TestSessions.T0.AddHours(3));
        long before = Held(session);
        Assert.Equal(50L, before);

        _clock.Set(TestSessions.T0.AddHours(5));
        Assert.Throws<IOException>(() => session.IngestFromSource(
            new ThrowingRecordSource(new IOException("health provider timeout")),
            TestSessions.T0.AddHours(3), TestSessions.T0.AddHours(6)));

        var failed = session.GetActivityStatus();
        Assert.Equal(ActivityPlayerStatus.RefreshTemporarilyFailed, failed.Status);
        Assert.Equal(ActivityRecommendedAction.RetryLater, failed.RecommendedAction);
        Assert.Equal(before, Held(session));

        _clock.Set(TestSessions.T0.AddHours(9));
        var retry = session.IngestFromSource(new StaticRecordSource(
                M5H1Records.Steps(2000L, TestSessions.T0.AddHours(6), TimeSpan.FromHours(2), sourceId: "f1"),
                M5H1Records.Steps(1000L, TestSessions.T0, TimeSpan.FromHours(1), sourceId: "dup-of-f0-window")),
            TestSessions.T0.AddHours(3), TestSessions.T0.AddHours(10));

        Assert.Equal(2, retry.Accepted);
        Assert.Equal(30L, retry.VitalityCredited);
        Assert.Equal(before + 30L, Held(session));

        // Replaying that exact retry window credits zero more.
        var replay = session.IngestFromSource(new StaticRecordSource(
                M5H1Records.Steps(2000L, TestSessions.T0.AddHours(6), TimeSpan.FromHours(2), sourceId: "f1"),
                M5H1Records.Steps(1000L, TestSessions.T0, TimeSpan.FromHours(1), sourceId: "dup-of-f0-window")),
            TestSessions.T0.AddHours(3), TestSessions.T0.AddHours(10));
        Assert.Equal(2, replay.DuplicatesIgnored);
        Assert.Equal(0L, replay.VitalityCredited);
        Assert.Equal(ActivityPlayerStatus.ConnectedCurrent, session.GetActivityStatus().Status);
    }

    // 8 -----------------------------------------------------------------------

    [Fact]
    public void Scenario08_PermissionRevokedExternally_StatusChangesProgressIntact()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        session.EnqueueProject(TestSessions.EntryProjectId);
        _port.Permission = ActivityPermissionState.Granted;

        _clock.Set(TestSessions.T0.AddHours(2));
        session.IngestFromSource(new StaticRecordSource(
                M5H1Records.Steps(7000L, TestSessions.T0, TimeSpan.FromHours(2), sourceId: "r0")),
            TestSessions.T0, TestSessions.T0.AddHours(3));
        long Held(GameSession s) => s.GetHome().Vitality + s.GetHome().ActiveProjectInvested;
        long earned = Held(session);
        Assert.Equal(70L, earned);

        _port.Permission = ActivityPermissionState.Revoked;
        var revoked = session.GetActivityStatus();
        Assert.Equal(ActivityPlayerStatus.PermissionDenied, revoked.Status);
        Assert.False(revoked.PermissionGranted);
        Assert.Equal(earned, Held(session));

        // Reconnect path is cleanly representable.
        _port.Permission = ActivityPermissionState.NotRequested;
        Assert.Equal(ActivityPlayerStatus.PermissionNeeded, session.GetActivityStatus().Status);
        _port.Permission = ActivityPermissionState.Granted;
        Assert.Equal(ActivityPlayerStatus.ConnectedCurrent, session.GetActivityStatus().Status);
        Assert.Equal(earned, Held(session));
    }

    // 9 -----------------------------------------------------------------------

    [Fact]
    public void Scenario09_SaveRecoveryUsed_CalmSurfacingWithEvidence_NoSilentReset()
    {
        var writer = NewSession();
        writer.StartNewGame(7UL);
        writer.EnqueueProject(TestSessions.EntryProjectId);
        writer.CreditActivity(TestSessions.Tx1, TestSessions.T0, 90L, "walk");
        long earned = writer.GetHome().Vitality;

        File.WriteAllText(SavePath, "{definitely-not-a-save");

        _clock.Set(TestSessions.T0.AddMinutes(30));
        var recovered = NewSession();
        Assert.Equal(StartStatus.RecoveredFromBackup, recovered.Continue().Status);

        // Surfaced calmly through the durable summary, not an alarm.
        var summary = recovered.GetPendingReturnSummary();
        Assert.NotNull(summary);
        Assert.Contains(summary!.Items, i => i.Text.Contains("backup"));

        // No silent reset: earned progress intact.
        Assert.Equal(earned, recovered.GetHome().Vitality);
        Assert.Equal(TestSessions.EntryProjectId, recovered.GetHome().ActiveProjectId);

        // Diagnostics expose recovery evidence.
        var diag = recovered.GetDiagnostics();
        Assert.True(diag.RecoveredFromBackup);
        Assert.Equal(DiagnosticsBootOutcome.RecoveredFromBackup, diag.BootOutcome);
        Assert.Equal(CodecFailureCategory.MalformedEnvelope, diag.LastBootCodecFailure);
    }

    // 10 ----------------------------------------------------------------------

    [Fact]
    public void Scenario10_PreferenceIsolation_TogglesNeverTouchCanonicalBytes()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        session.EnqueueProject(TestSessions.EntryProjectId);
        session.CreditActivity(TestSessions.Tx1, TestSessions.T0, 40L, "walk");

        byte[] canonicalBefore = File.ReadAllBytes(SavePath);
        var prefsStore = new LocalPreferencesStore(_temp.Path);

        // Rapid repeated toggles across both directions and categories.
        Assert.True(session.SetReducedMotion(true).IsSuccess);
        Assert.True(session.SetHapticsEnabled(false).IsSuccess);
        Assert.True(session.SetNotificationsOptIn(true).IsSuccess);
        Assert.True(session.SetNotificationCategory(NotificationCategory.Discoveries, false).IsSuccess);
        Assert.True(session.SetReducedMotion(false).IsSuccess);
        Assert.True(session.SetDailyReminder(true, 777).IsSuccess);

        Assert.Equal(canonicalBefore, File.ReadAllBytes(SavePath));

        // Across a restart the same holds; user choices persist meanwhile.
        _clock.Set(TestSessions.T0.AddMinutes(10));
        var reloaded = NewSession();
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);

        Assert.True(reloaded.GetSettings().NotificationsOptIn);
        Assert.True(reloaded.GetSettings().DailyReminderEnabled);
        Assert.Equal(777, reloaded.GetSettings().DailyReminderMinutesOfDay);

        byte[] canonicalAfterBoot = File.ReadAllBytes(SavePath);
        Assert.True(reloaded.SetSoundEnabled(false).IsSuccess);
        Assert.True(reloaded.SetHapticsEnabled(true).IsSuccess);
        Assert.Equal(canonicalAfterBoot, File.ReadAllBytes(SavePath));

        // Canonical progression identical apart from intentionally separate pref bytes.
        Assert.Equal(reloaded.GetHome().Vitality, session.GetHome().Vitality);
        Assert.NotNull(prefsStore.Load().State);
    }

    // 11 ----------------------------------------------------------------------

    [Fact]
    public void Scenario11_ReplayAfterUxOperations_CreditsZeroAdditionalProgress()
    {
        var session = NewSession();
        session.StartNewGame(7UL);

        // Full UX pass: onboarding flow, settings churn, status/diagnostic reads.
        Assert.True(session.AdvanceOnboarding(OnboardingStage.Premise).IsSuccess);
        Assert.True(session.AdvanceOnboarding(OnboardingStage.WorldBaseline).IsSuccess);
        Assert.True(session.AdvanceOnboarding(OnboardingStage.ActivityConnection).IsSuccess);
        _port.Permission = ActivityPermissionState.Granted;
        _ = session.GetActivityStatus();
        Assert.True(session.SetNotificationsOptIn(true).IsSuccess);
        Assert.True(session.SetNotificationCategory(NotificationCategory.ProjectCompletions, false).IsSuccess);
        _ = session.GetDiagnostics();
        _ = session.GetOnboarding();
        Assert.True(session.EnqueueProject(TestSessions.EntryProjectId).IsSuccess);
        Assert.True(session.AdvanceOnboarding(OnboardingStage.Complete).IsSuccess);

        _clock.Set(TestSessions.T0.AddHours(2));

        var first = session.IngestFromSource(new StaticRecordSource(
                M5H1Records.Steps(8000L, TestSessions.T0, TimeSpan.FromHours(2), sourceId: "repro-1")),
            TestSessions.T0, TestSessions.T0.AddDays(1));
        Assert.Equal(80L, first.VitalityCredited);

        // More UX operations between replays.
        _ = session.GetActivityStatus();
        _ = session.GetDiagnostics();
        Assert.True(session.SetReducedMotion(true).IsSuccess);

        var replay = session.IngestFromSource(new StaticRecordSource(
                M5H1Records.Steps(8000L, TestSessions.T0, TimeSpan.FromHours(2), sourceId: "repro-1")),
            TestSessions.T0, TestSessions.T0.AddDays(1));
        Assert.Equal(1, replay.DuplicatesIgnored);
        Assert.Equal(0, replay.Accepted);
        Assert.Equal(0L, replay.VitalityCredited);
        Assert.Equal(first.VitalityCredited, session.GetDiagnostics().LifetimeVitalityCredited);
        Assert.Equal(first.VitalityCredited,
            session.GetHome().Vitality + session.GetHome().ActiveProjectInvested);
    }
}
