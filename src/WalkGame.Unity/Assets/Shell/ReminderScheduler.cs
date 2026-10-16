namespace WalkGame.Shell
{
    /// <summary>OS notification permission as the reminder sees it (D-119).</summary>
    public enum ReminderPermission { Granted, NotGranted }

    /// <summary>
    /// D-119: delivery port for the optional, opt-in, local-only gentle daily reminder. The
    /// controller owns policy (off by default, calm copy, only when the OS allows it); an
    /// implementation only schedules or cancels one inexact, non-waking daily notification.
    /// Tests use a fake; Android uses <see cref="AndroidReminderScheduler"/>.
    /// </summary>
    public interface IReminderScheduler
    {
        ReminderPermission Permission { get; }
        /// <summary>Asks the OS once; the answer is read again on the next resume.</summary>
        void RequestPermission();
        /// <summary>Replaces any scheduled reminder with one near minutesOfDay (device-local), daily.</summary>
        bool ScheduleDaily(int minutesOfDay, string title, string body);
        void Cancel();
    }

    /// <summary>D-119 reminder copy: an invitation, never a streak, count or guilt line.</summary>
    public static class ReminderCopy
    {
        public static string Title => PlayerStrings.T("reminder.title");
        public static string Body => PlayerStrings.T("reminder.body");
    }
}
