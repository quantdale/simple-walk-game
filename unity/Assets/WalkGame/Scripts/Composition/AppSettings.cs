using UnityEngine;

namespace WalkGame.UnityShell.Composition
{
    public enum AppPreference
    {
        ReducedMotion = 0,
        HapticsEnabled = 1,
    }

    public static class AppSettings
    {
        public const string Version = "1";

        public static bool GetBool(AppPreference preference, bool fallback)
        {
            return PlayerPrefs.GetInt(Key(preference), fallback ? 1 : 0) == 1;
        }

        public static void SetBool(AppPreference preference, bool value)
        {
            PlayerPrefs.SetInt(Key(preference), value ? 1 : 0);
            PlayerPrefs.Save();
        }

        private static string Key(AppPreference preference)
        {
            return "walkgame.pref." + preference + ".v" + Version;
        }
    }
}
