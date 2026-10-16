using WalkGame.Application.Tests.TestSupport;
using WalkGame.Application.Ux;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Application.Tests;

/// <summary>D-092: a fresh-device start never inherits onboarding progress from an erased world.</summary>
public sealed class StaleOnboardingResetTests : System.IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ManualClock _clock = new(TestSessions.T0);
    public void Dispose() => _temp.Dispose();

    private GameSession NewSession() => TestSessions.Create(_temp.Path, _clock, new LocalPreferencesStore(_temp.Path));

    private GameSession Reloaded() { var r = NewSession(); r.Continue(); return r; }

    private void LeaveStalePreferences(OnboardingStage stage)
    {
        var s = NewSession();
        s.StartNewGame(1UL);
        for (var t = OnboardingStage.Premise; t <= stage && t < OnboardingStage.Complete; t++) Assert.True(s.AdvanceOnboarding(t).IsSuccess);
        if (stage == OnboardingStage.Complete)
        {
            var first = s.GetProjects().Projects.First(p => p.Status == WalkGame.Domain.Projects.ProjectStatus.Available);
            Assert.True(s.EnqueueProject(first.ProjectId).IsSuccess);
            Assert.True(s.AdvanceOnboarding(OnboardingStage.Complete).IsSuccess);
        }
        Assert.True(s.SetReducedMotion(true).IsSuccess);
        // Kill between the save erase and the preferences erase of delete-all.
        foreach (var f in Directory.GetFiles(_temp.Path).Where(f => !Path.GetFileName(f).StartsWith("ux-preferences")))
            File.Delete(f);
    }

    [Theory]
    [InlineData(OnboardingStage.FirstProject)]
    [InlineData(OnboardingStage.Complete)]
    public void FreshDeviceStart_ResetsOnlyTheOnboardingStage(OnboardingStage stale)
    {
        LeaveStalePreferences(stale);
        var s = NewSession();
        Assert.Equal(StartStatus.NoSaveFound, s.Continue().Status);

        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(2UL, overwriteExisting: false).Status);
        Assert.Equal(OnboardingStage.NotStarted, s.GetOnboarding().CurrentStage);
        Assert.True(s.GetSettings().ReducedMotion, "player choices survive");
        Assert.Equal(OnboardingStage.NotStarted, Reloaded().GetOnboarding().CurrentStage);
    }

    [Fact]
    public void RefusedStart_LeavesOnboardingUntouched()
    {
        var s = NewSession();
        s.StartNewGame(1UL);
        s.AdvanceOnboarding(OnboardingStage.WorldBaseline);
        Assert.Equal(StartStatus.ExistingSaveRefused, NewSession().StartNewGame(2UL, overwriteExisting: false).Status);
        Assert.Equal(OnboardingStage.WorldBaseline, Reloaded().GetOnboarding().CurrentStage);
    }

    [Fact]
    public void LegacyOverwriteStart_KeepsItsDocumentedBehaviour()
    {
        var s = NewSession();
        s.StartNewGame(1UL);
        s.AdvanceOnboarding(OnboardingStage.WorldBaseline);
        s.StartNewGame(2UL);
        Assert.Equal(OnboardingStage.WorldBaseline, s.GetOnboarding().CurrentStage);
    }

    [Fact]
    public void PermissionDeniedThenGranted_MidOnboarding_KeepsTheStage()
    {
        var port = new FakeActivityConnectionPort();
        var s = TestSessions.Create(_temp.Path, _clock, new LocalPreferencesStore(_temp.Path), port);
        s.StartNewGame(1UL, overwriteExisting: false);
        s.AdvanceOnboarding(OnboardingStage.ActivityConnection);
        port.Permission = WalkGame.Application.Activity.ActivityPermissionState.Denied;
        Assert.Equal(WalkGame.Application.ReadModels.OnboardingActivityStepState.Denied, s.GetOnboarding().ActivityStep);
        Assert.True(s.GetOnboarding().NavigableDespitePermissionDenied);
        port.Permission = WalkGame.Application.Activity.ActivityPermissionState.Granted;
        Assert.Equal(WalkGame.Application.ReadModels.OnboardingActivityStepState.Granted, s.GetOnboarding().ActivityStep);
        Assert.Equal(OnboardingStage.ActivityConnection, s.GetOnboarding().CurrentStage);
    }
}
