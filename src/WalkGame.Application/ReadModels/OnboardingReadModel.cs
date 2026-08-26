using WalkGame.Application.Ux;

namespace WalkGame.Application.ReadModels
{
    /// <summary>What the shell should present as the next onboarding step.</summary>
    public enum OnboardingNextAction
    {
        /// <summary>Onboarding finished — no onboarding surface needed.</summary>
        None = 0,

        /// <summary>Show the premise ("your movement restores this world").</summary>
        ExplainPremise = 1,

        /// <summary>Show one clearly damaged landmark as the world baseline.</summary>
        ShowWorldBaseline = 2,

        /// <summary>Explain activity connection; permission may be granted or denied.</summary>
        OfferActivityConnection = 3,

        /// <summary>Route the player into the real project-selection operations.</summary>
        ChooseFirstProject = 4,

        /// <summary>Demonstrate that progression happens while away.</summary>
        DemonstrateProgression = 5,

        /// <summary>Tell the player they do not need to keep the app open.</summary>
        ShowExitMessage = 6,
    }

    /// <summary>How the activity-connection step treats the current permission state.</summary>
    public enum OnboardingActivityStepState
    {
        /// <summary>No activity connection port configured (headless/dev composition).</summary>
        NotAvailable = 0,

        NotYetRequested = 1,

        Granted = 2,

        /// <summary>Denied or revoked — onboarding must remain safely navigable regardless.</summary>
        Denied = 3,

        SourceUnavailable = 4,
    }

    /// <summary>
    /// Presentation-ready onboarding state. Purely derived from the durable local
    /// UX-preferences record plus canonical queue facts; reading it has no side effects
    /// and never mutates progression.
    /// </summary>
    public sealed class OnboardingReadModel
    {
        public OnboardingStage CurrentStage { get; }

        public bool IsComplete => CurrentStage == OnboardingStage.Complete;

        public OnboardingNextAction NextAction { get; }

        /// <summary>True once a first project exists in canonical queue/active/completed state.</summary>
        public bool FirstProjectChosen { get; }

        /// <summary>Whether the first-project step's canonical gate is satisfied.</summary>
        public bool CanCompleteFirstProjectStep => FirstProjectChosen;

        public OnboardingActivityStepState ActivityStep { get; }

        /// <summary>
        /// True when permission is denied/revoked but navigation remains available —
        /// proves the denial path does not trap the profile.
        /// </summary>
        public bool NavigableDespitePermissionDenied { get; }

        public OnboardingReadModel(
            OnboardingStage currentStage,
            OnboardingNextAction nextAction,
            bool firstProjectChosen,
            OnboardingActivityStepState activityStep,
            bool navigableDespitePermissionDenied)
        {
            CurrentStage = currentStage;
            NextAction = nextAction;
            FirstProjectChosen = firstProjectChosen;
            ActivityStep = activityStep;
            NavigableDespitePermissionDenied = navigableDespitePermissionDenied;
        }
    }
}
