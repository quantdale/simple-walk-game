namespace WalkGame.Application.Ux
{
    /// <summary>
    /// Durable onboarding progress stages (UX_DESIGN §11 ordered flow). The value is a
    /// durable local UX-flow marker only: it never gates rewards, content, or canonical
    /// progression, and completing the first-project step must be earned through the real
    /// canonical project operations, never through this state alone.
    /// </summary>
    public enum OnboardingStage
    {
        NotStarted = 0,
        Premise = 1,
        WorldBaseline = 2,
        ActivityConnection = 3,
        FirstProject = 4,
        Simulation = 5,
        Exit = 6,
        Complete = 7,
    }

    /// <summary>Player-configurable notification categories (UX_DESIGN §13).</summary>
    public enum NotificationCategory
    {
        ProjectCompletions = 0,
        ExpeditionResults = 1,
        Discoveries = 2,
        DailyReminder = 3,
    }

    /// <summary>
    /// Versioned durable local UX preferences + onboarding state. This record is NOT
    /// canonical game state: it lives in its own local store, never inside GameState, and
    /// no preference value may influence progression math, reward processing, or world
    /// simulation (D-042). Kept deliberately small and free of domain types so it can be
    /// serialized without leaking game state into a settings surface.
    /// </summary>
    public sealed class UxPreferencesState
    {
        /// <summary>Current local-preferences schema version.</summary>
        public const int CurrentVersion = 1;

        public int SchemaVersion { get; set; } = CurrentVersion;

        // --- Onboarding flow ---------------------------------------------------------

        public OnboardingStage OnboardingStage { get; set; } = OnboardingStage.NotStarted;

        // --- Accessibility / presentation --------------------------------------------

        /// <summary>Reduced-motion option (UX_DESIGN §14): suppresses non-essential motion.</summary>
        public bool ReducedMotion { get; set; }

        /// <summary>Haptics feedback enabled (default on).</summary>
        public bool HapticsEnabled { get; set; } = true;

        /// <summary>Sound feedback enabled (default on); accessibility requires sound-independent channels.</summary>
        public bool SoundEnabled { get; set; } = true;

        // --- Notifications (local configuration only; delivery is out of M5-H1 scope) -

        /// <summary>Master opt-in. Effective category delivery = OptIn AND category flag.</summary>
        public bool NotificationsOptIn { get; set; }

        public bool NotifyProjectCompletions { get; set; } = true;

        public bool NotifyExpeditionResults { get; set; } = true;

        public bool NotifyDiscoveries { get; set; } = true;

        public bool DailyReminderEnabled { get; set; }

        /// <summary>Minutes since midnight (device-local wall clock) for the optional daily reminder.</summary>
        public int DailyReminderMinutesOfDay { get; set; } = DefaultReminderMinutesOfDay;

        // --- Support -----------------------------------------------------------------

        /// <summary>Whether the player opted into showing the support/diagnostics section.</summary>
        public bool DiagnosticsVisible { get; set; }

        /// <summary>Validation bounds for <see cref="DailyReminderMinutesOfDay"/> (00:00–23:59).</summary>
        public const int ReminderMinutesMin = 0;
        public const int ReminderMinutesMax = 24 * 60 - 1;
        public const int DefaultReminderMinutesOfDay = 8 * 60;

        /// <summary>Deterministic documented defaults for a fresh profile.</summary>
        public static UxPreferencesState CreateDefault() => new UxPreferencesState();

        public UxPreferencesState Clone() => new UxPreferencesState
        {
            SchemaVersion = SchemaVersion,
            OnboardingStage = OnboardingStage,
            ReducedMotion = ReducedMotion,
            HapticsEnabled = HapticsEnabled,
            SoundEnabled = SoundEnabled,
            NotificationsOptIn = NotificationsOptIn,
            NotifyProjectCompletions = NotifyProjectCompletions,
            NotifyExpeditionResults = NotifyExpeditionResults,
            NotifyDiscoveries = NotifyDiscoveries,
            DailyReminderEnabled = DailyReminderEnabled,
            DailyReminderMinutesOfDay = DailyReminderMinutesOfDay,
            DiagnosticsVisible = DiagnosticsVisible,
        };
    }
}
