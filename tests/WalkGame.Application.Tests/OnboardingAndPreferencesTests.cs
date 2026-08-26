using System;
using System.IO;
using WalkGame.Application.ReadModels;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Application.Ux;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Application.Tests;

/// <summary>
/// Durable local UX preferences + onboarding contract (Workstream B, D-042): restart
/// durability, forward-only progression with the canonical first-project gate, and total
/// isolation between preference writes and canonical save bytes.
/// </summary>
public sealed class OnboardingAndPreferencesTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ManualClock _clock = new(TestSessions.T0);

    public void Dispose() => _temp.Dispose();

    private GameSession NewSession() =>
        TestSessions.Create(_temp.Path, _clock, new LocalPreferencesStore(_temp.Path));

    [Fact]
    public void FreshProfile_HasDefaults_AndOnboardingStartsAtNotStarted()
    {
        var session = NewSession();
        session.StartNewGame(7UL);

        var onboarding = session.GetOnboarding();

        Assert.Equal(OnboardingStage.NotStarted, onboarding.CurrentStage);
        Assert.Equal(OnboardingNextAction.ExplainPremise, onboarding.NextAction);
        Assert.False(onboarding.FirstProjectChosen);
        Assert.False(onboarding.IsComplete);
        Assert.True(session.GetSettings().HapticsEnabled);
        Assert.False(session.GetSettings().ReducedMotion);
        Assert.False(session.GetSettings().NotificationsOptIn);
    }

    [Fact]
    public void AdvanceOnboarding_WalksTheDocumentedStageFlow()
    {
        var session = NewSession();
        session.StartNewGame(7UL);

        session.AdvanceOnboarding(OnboardingStage.Premise);
        Assert.Equal(OnboardingNextAction.ShowWorldBaseline, session.GetOnboarding().NextAction);

        session.AdvanceOnboarding(OnboardingStage.WorldBaseline);
        Assert.Equal(OnboardingNextAction.OfferActivityConnection, session.GetOnboarding().NextAction);

        session.AdvanceOnboarding(OnboardingStage.ActivityConnection);
        Assert.Equal(OnboardingNextAction.ChooseFirstProject, session.GetOnboarding().NextAction);

        session.AdvanceOnboarding(OnboardingStage.FirstProject);
        // First project not chosen yet: the projection keeps pointing at the choice.
        Assert.Equal(OnboardingNextAction.ChooseFirstProject, session.GetOnboarding().NextAction);
        Assert.False(session.GetOnboarding().CanCompleteFirstProjectStep);
    }

    [Fact]
    public void AdvanceOnwards_Complete_IsRejectedWithoutCanonicalFirstProject()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        Assert.True(session.AdvanceOnboarding(OnboardingStage.Exit).IsSuccess);

        var result = session.AdvanceOnboarding(OnboardingStage.Complete);

        Assert.False(result.IsSuccess);
        Assert.Equal(UxErrorCodes.OnboardingPrerequisite, result.Error!.Code);
        Assert.NotEqual(OnboardingStage.Complete, session.GetOnboarding().CurrentStage);
    }

    [Fact]
    public void Complete_SucceedsOnlyAfterRealProjectOperation_ChooseViaEnqueue()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        Assert.True(session.AdvanceOnboarding(OnboardingStage.FirstProject).IsSuccess);

        // The canonical route — the same operation a player uses outside onboarding.
        Assert.True(session.EnqueueProject(TestSessions.EntryProjectId).IsSuccess);
        Assert.True(session.GetOnboarding().FirstProjectChosen);

        var result = session.AdvanceOnboarding(OnboardingStage.Complete);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(session.GetOnboarding().IsComplete);
        Assert.Equal(OnboardingNextAction.None, session.GetOnboarding().NextAction);
    }

    [Fact]
    public void OnboardingProgress_IsForwardOnly_AndIdempotent()
    {
        var session = NewSession();
        session.StartNewGame(7UL);

        Assert.True(session.AdvanceOnboarding(OnboardingStage.ActivityConnection).IsSuccess);
        Assert.True(session.AdvanceOnboarding(OnboardingStage.Premise).IsSuccess);
        Assert.True(session.AdvanceOnboarding(OnboardingStage.ActivityConnection).IsSuccess);

        Assert.Equal(OnboardingStage.ActivityConnection, session.GetOnboarding().CurrentStage);
    }

    [Fact]
    public void AdvanceOnboarding_RejectsUnknownStageValue()
    {
        var session = NewSession();
        session.StartNewGame(7UL);

        var result = session.AdvanceOnboarding((OnboardingStage)42);

        Assert.False(result.IsSuccess);
        Assert.Equal(UxErrorCodes.InvalidOnboardingTarget, result.Error!.Code);
    }

    [Fact]
    public void Restart_ResumesDurableOnboardingStage_AndPreferences()
    {
        var writer = NewSession();
        writer.StartNewGame(7UL);
        Assert.True(writer.SetReducedMotion(true).IsSuccess);
        Assert.True(writer.SetDailyReminder(true, 1235).IsSuccess);
        Assert.True(writer.AdvanceOnboarding(OnboardingStage.Simulation).IsSuccess);

        var reloaded = NewSession();
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);

        Assert.Equal(OnboardingStage.Simulation, reloaded.GetOnboarding().CurrentStage);
        Assert.True(reloaded.GetSettings().ReducedMotion);
        Assert.True(reloaded.GetSettings().DailyReminderEnabled);
        Assert.Equal(1235, reloaded.GetSettings().DailyReminderMinutesOfDay);
    }

    [Fact]
    public void PreferenceWrites_NeverTouchCanonicalSaveBytes()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        Assert.True(session.EnqueueProject(TestSessions.EntryProjectId).IsSuccess);
        var before = File.ReadAllBytes(Path.Combine(_temp.Path, "save.json"));

        Assert.True(session.SetReducedMotion(true).IsSuccess);
        Assert.True(session.SetHapticsEnabled(false).IsSuccess);
        Assert.True(session.SetSoundEnabled(false).IsSuccess);
        Assert.True(session.SetNotificationsOptIn(true).IsSuccess);
        Assert.True(session.SetNotificationCategory(NotificationCategory.Discoveries, false).IsSuccess);
        Assert.True(session.SetDiagnosticsVisible(true).IsSuccess);

        var after = File.ReadAllBytes(Path.Combine(_temp.Path, "save.json"));
        Assert.Equal(before, after);

        // Canonical read models unchanged by preference churn.
        Assert.Equal(TestSessions.EntryProjectId, session.GetHome().ActiveProjectId);
        Assert.Equal(0L, session.GetHome().Vitality);
    }

    [Fact]
    public void PreferenceWrites_AreRepeatableAndIdempotent()
    {
        var session = NewSession();
        session.StartNewGame(7UL);

        for (int i = 0; i < 3; i++)
        {
            Assert.True(session.SetReducedMotion(true).IsSuccess);
            Assert.True(session.SetNotificationCategory(NotificationCategory.ProjectCompletions, false).IsSuccess);
        }

        Assert.True(session.GetSettings().ReducedMotion);
        Assert.False(session.GetSettings().NotificationCategories[0].Enabled);
    }

    [Fact]
    public void SetDailyReminder_ValidatesRangeExplicitly()
    {
        var session = NewSession();
        session.StartNewGame(7UL);

        var tooSmall = session.SetDailyReminder(true, -1);
        var tooLarge = session.SetDailyReminder(true, 24 * 60);
        var edgeOk = session.SetDailyReminder(true, 24 * 60 - 1);

        Assert.False(tooSmall.IsSuccess);
        Assert.Equal(UxErrorCodes.InvalidReminderTime, tooSmall.Error!.Code);
        Assert.False(tooLarge.IsSuccess);
        Assert.True(edgeOk.IsSuccess);
        Assert.Equal(24 * 60 - 1, session.GetSettings().DailyReminderMinutesOfDay);
        Assert.Equal(UxPreferencesState.ReminderMinutesMin, session.GetSettings().ReminderMinutesMin);
    }

    [Fact]
    public void EffectiveCategoryDelivery_RequiresMasterOptIn_AndCategoryToggle()
    {
        var session = NewSession();
        session.StartNewGame(7UL);

        // Categories default enabled but master opt-in off → nothing effective.
        Assert.All(session.GetSettings().NotificationCategories, c => Assert.False(c.Effective));

        Assert.True(session.SetNotificationsOptIn(true).IsSuccess);
        Assert.All(session.GetSettings().NotificationCategories, c => Assert.True(c.Effective));

        Assert.True(session.SetNotificationCategory(NotificationCategory.ExpeditionResults, false).IsSuccess);
        var expedition = session.GetSettings().NotificationCategories[1];
        Assert.Equal(NotificationCategory.ExpeditionResults, expedition.Category);
        Assert.False(expedition.Enabled);
        Assert.False(expedition.Effective);
    }

    [Fact]
    public void Setters_WithoutConfiguredStore_FailWithStableErrorCode_InsteadOfSilentVolatileBehavior()
    {
        var session = TestSessions.Create(_temp, _clock);
        session.StartNewGame(7UL);

        var result = session.SetReducedMotion(true);

        Assert.False(result.IsSuccess);
        Assert.Equal(UxErrorCodes.PreferencesStoreMissing, result.Error!.Code);
        Assert.False(session.GetSettings().ReducedMotion);
    }

    [Fact]
    public void DamagedPreferencesFile_DoesNotBlockBoot_GameplayContinuesWithDefaults()
    {
        var writer = NewSession();
        writer.StartNewGame(7UL);
        writer.CreditActivity(TestSessions.Tx1, TestSessions.T0, 250L, "walk");
        Assert.True(writer.SetReducedMotion(true).IsSuccess);

        File.WriteAllText(Path.Combine(_temp.Path, "ux-preferences.json"), "not-json-at-all");

        var reloaded = NewSession();
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);

        // Gameplay intact; preferences degraded to documented defaults.
        Assert.Equal(250L, reloaded.GetHome().Vitality);
        Assert.False(reloaded.GetSettings().ReducedMotion);
        Assert.Equal(OnboardingStage.NotStarted, reloaded.GetOnboarding().CurrentStage);
    }
}
