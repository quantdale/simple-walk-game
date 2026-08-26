using System.Collections.Generic;
using WalkGame.Application.Ux;

namespace WalkGame.Application.ReadModels
{
    public sealed class NotificationCategoryRow
    {
        public NotificationCategory Category { get; }

        /// <summary>Player-facing toggle value persisted locally.</summary>
        public bool Enabled { get; }

        /// <summary>Effective delivery flag: master opt-in AND category toggle.</summary>
        public bool Effective { get; }

        public NotificationCategoryRow(NotificationCategory category, bool enabled, bool effective)
        {
            Category = category;
            Enabled = enabled;
            Effective = effective;
        }
    }

    /// <summary>
    /// Durable local preference snapshot for the Settings screen. Values come ONLY from
    /// the local preferences store. Canonical presentation-affecting state that is NOT a
    /// preference (auto-advance) is surfaced separately and marked canonical so
    /// presentation can never confuse the two ownership classes.
    /// Reading this model is side-effect free.
    /// </summary>
    public sealed class SettingsReadModel
    {
        // Accessibility / presentation (local).
        public bool ReducedMotion { get; }
        public bool HapticsEnabled { get; }
        public bool SoundEnabled { get; }

        // Notifications (local configuration only; delivery itself remains out of scope).
        public bool NotificationsOptIn { get; }
        public IReadOnlyList<NotificationCategoryRow> NotificationCategories { get; }
        public bool DailyReminderEnabled { get; }
        public int DailyReminderMinutesOfDay { get; }

        /// <summary>Inclusive validation bounds for <see cref="DailyReminderMinutesOfDay"/>.</summary>
        public int ReminderMinutesMin { get; }
        public int ReminderMinutesMax { get; }

        // Support.
        public bool DiagnosticsVisible { get; }

        /// <summary>
        /// Canonical automation flag from ProjectQueueState — deliberately NOT part of the
        /// local preferences record; shown here so one screen can present both ownership
        /// classes without presentation reaching into canonical state.
        /// </summary>
        public bool CanonicalAutoAdvance { get; }

        public SettingsReadModel(
            bool reducedMotion,
            bool hapticsEnabled,
            bool soundEnabled,
            bool notificationsOptIn,
            IReadOnlyList<NotificationCategoryRow> notificationCategories,
            bool dailyReminderEnabled,
            int dailyReminderMinutesOfDay,
            int reminderMinutesMin,
            int reminderMinutesMax,
            bool diagnosticsVisible,
            bool canonicalAutoAdvance)
        {
            ReducedMotion = reducedMotion;
            HapticsEnabled = hapticsEnabled;
            SoundEnabled = soundEnabled;
            NotificationsOptIn = notificationsOptIn;
            NotificationCategories = notificationCategories;
            DailyReminderEnabled = dailyReminderEnabled;
            DailyReminderMinutesOfDay = dailyReminderMinutesOfDay;
            ReminderMinutesMin = reminderMinutesMin;
            ReminderMinutesMax = reminderMinutesMax;
            DiagnosticsVisible = diagnosticsVisible;
            CanonicalAutoAdvance = canonicalAutoAdvance;
        }
    }
}
