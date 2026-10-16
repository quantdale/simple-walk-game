using UnityEngine;

namespace WalkGame.Shell
{
    /// <summary>
    /// D-119: Android delivery for the gentle daily reminder through the
    /// <c>com.quantdale.walkgame.reminder.ReminderBridge</c> Java class (AlarmManager inexact
    /// repeating RTC alarm, so it never wakes the device; low-importance channel). Requires
    /// POST_NOTIFICATIONS on Android 13+. Compiled only for Android players; device behaviour
    /// UNVERIFIED (KI-002).
    /// </summary>
    public sealed class AndroidReminderScheduler : IReminderScheduler
    {
        private const string Bridge = "com.quantdale.walkgame.reminder.ReminderBridge";
#if UNITY_ANDROID && !UNITY_EDITOR
        public static bool IsAvailable => true;

        private static T WithActivity<T>(System.Func<AndroidJavaClass, AndroidJavaObject, T> f, T fallback)
        {
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var bridge = new AndroidJavaClass(Bridge))
                    return f(bridge, activity);
            }
            catch (System.Exception) { return fallback; }
        }

        public ReminderPermission Permission =>
            WithActivity((b, a) => b.CallStatic<bool>("canNotify", a), false) ? ReminderPermission.Granted : ReminderPermission.NotGranted;

        public void RequestPermission() => WithActivity((b, a) => { b.CallStatic("requestPermission", a); return true; }, false);

        public bool ScheduleDaily(int minutesOfDay, string title, string body) =>
            WithActivity((b, a) => b.CallStatic<bool>("scheduleDaily", a, minutesOfDay, title, body), false);

        public void Cancel() => WithActivity((b, a) => { b.CallStatic("cancel", a); return true; }, false);
#else
        public static bool IsAvailable => false;
        public ReminderPermission Permission => ReminderPermission.NotGranted;
        public void RequestPermission() { }
        public bool ScheduleDaily(int minutesOfDay, string title, string body) => false;
        public void Cancel() { }
#endif
    }
}
