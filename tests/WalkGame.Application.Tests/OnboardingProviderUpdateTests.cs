using WalkGame.Application.Activity;
using WalkGame.Application.ReadModels;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Application.Ux;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Application.Tests;

/// <summary>D-094: onboarding shows the D-093 update-required state instead of offering Connect.</summary>
public sealed class OnboardingProviderUpdateTests : System.IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ManualClock _clock = new(TestSessions.T0);
    public void Dispose() => _temp.Dispose();

    private (GameSession s, FakeActivityConnectionPort port) AtActivityStep()
    {
        var port = new FakeActivityConnectionPort();
        var s = TestSessions.Create(_temp.Path, _clock, new LocalPreferencesStore(_temp.Path), port);
        s.StartNewGame(1UL, overwriteExisting: false);
        for (var t = OnboardingStage.Premise; t <= OnboardingStage.WorldBaseline; t++) Assert.True(s.AdvanceOnboarding(t).IsSuccess);
        return (s, port);
    }

    [Theory]
    [InlineData(ActivityPermissionState.NotRequested)]
    [InlineData(ActivityPermissionState.Denied)]
    [InlineData(ActivityPermissionState.Granted)]
    public void OutdatedProvider_OutranksEveryPermissionState(ActivityPermissionState permission)
    {
        var (s, port) = AtActivityStep();
        port.Permission = permission;
        port.Availability = ActivitySourceAvailability.ProviderUpdateRequired;
        var ob = s.GetOnboarding();
        Assert.Equal(OnboardingActivityStepState.ProviderUpdateRequired, ob.ActivityStep);
        Assert.Equal(OnboardingNextAction.OfferActivityConnection, ob.NextAction);
    }

    [Fact]
    public void Glance_AsksForAnUpdate_NotAConnect_AndStaysNoGuilt()
    {
        var (s, port) = AtActivityStep();
        port.Availability = ActivitySourceAvailability.ProviderUpdateRequired;
        var sentence = s.GetGlance().OnboardingSentence;
        Assert.NotNull(sentence);
        Assert.Contains("update", sentence!);
        Assert.DoesNotContain("Connect", sentence);
        Assert.Contains("nothing is locked", sentence);
    }

    [Fact]
    public void AfterUpdate_TheStepOffersConnectAgain_AndTheStageNeverMoved()
    {
        var (s, port) = AtActivityStep();
        port.Availability = ActivitySourceAvailability.ProviderUpdateRequired;
        Assert.Equal(OnboardingActivityStepState.ProviderUpdateRequired, s.GetOnboarding().ActivityStep);
        port.Availability = ActivitySourceAvailability.Available;
        Assert.Equal(OnboardingActivityStepState.NotYetRequested, s.GetOnboarding().ActivityStep);
        Assert.Equal(OnboardingStage.WorldBaseline, s.GetOnboarding().CurrentStage);
        Assert.StartsWith("Connect", s.GetGlance().OnboardingSentence);
    }
}
