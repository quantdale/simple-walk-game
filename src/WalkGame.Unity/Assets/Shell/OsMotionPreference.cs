using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace WalkGame.Shell
{
    /// <summary>
    /// D-186: the phone's own "reduce motion" accessibility setting, read through a platform
    /// adapter. The effective flag is the in-game setting OR this (<see cref="MotionPreference.Effective"/>).
    /// Implementations must be cheap and side-effect free; a throw is treated as "not requested".
    /// </summary>
    public interface IOsMotionPreference
    {
        bool ReduceMotionRequested { get; }
    }

    /// <summary>Editor, tests and unsupported platforms: the OS never asks for reduced motion.</summary>
    public sealed class NullOsMotionPreference : IOsMotionPreference
    {
        public static readonly NullOsMotionPreference Instance = new NullOsMotionPreference();
        public bool ReduceMotionRequested => false;
    }

    /// <summary>Combines the in-game setting with the OS preference.</summary>
    public static class MotionPreference
    {
        /// <summary>
        /// In-game setting OR OS preference. The OS side can only turn reduced motion on, never off,
        /// and any failure reading it falls back to the in-game setting alone.
        /// </summary>
        public static bool Effective(bool inGameSetting, IOsMotionPreference? os)
        {
            if (inGameSetting) return true;
            if (os == null) return false;
            try { return os.ReduceMotionRequested; }
            catch (Exception) { return false; }
        }

        /// <summary>The adapter for the running platform; the null adapter in the editor.</summary>
        public static IOsMotionPreference ForCurrentPlatform()
        {
            if (AndroidOsMotionPreference.IsAvailable) return new AndroidOsMotionPreference();
            if (IosOsMotionPreference.IsAvailable) return new IosOsMotionPreference();
            return NullOsMotionPreference.Instance;
        }
    }

    /// <summary>
    /// Android: "Remove animations" sets <c>Settings.Global.ANIMATOR_DURATION_SCALE</c> to 0.
    /// Compiled only for Android players; device behaviour UNVERIFIED (KI-002).
    /// </summary>
    public sealed class AndroidOsMotionPreference : IOsMotionPreference
    {
        /// <summary>True when the scale means animations are off (0 or below). NaN means "unknown" and is not off.</summary>
        public static bool IsRemoveAnimations(float animatorDurationScale) =>
            !float.IsNaN(animatorDurationScale) && animatorDurationScale <= 0f;

#if UNITY_ANDROID && !UNITY_EDITOR
        public static bool IsAvailable => true;

        public bool ReduceMotionRequested
        {
            get
            {
                try
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
                    using (var global = new AndroidJavaClass("android.provider.Settings$Global"))
                    {
                        float scale = global.CallStatic<float>("getFloat", resolver, "animator_duration_scale", 1f);
                        return IsRemoveAnimations(scale);
                    }
                }
                catch (Exception) { return false; }
            }
        }
#else
        public static bool IsAvailable => false;
        public bool ReduceMotionRequested => false;
#endif
    }

    /// <summary>
    /// iOS: <c>UIAccessibility.isReduceMotionEnabled</c> via <c>WalkGameAccessibilityBridge.swift</c>.
    /// Compiled only for iOS players; device behaviour UNVERIFIED (KI-002).
    /// </summary>
    public sealed class IosOsMotionPreference : IOsMotionPreference
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int wg_reduce_motion_enabled();

        public static bool IsAvailable => true;

        public bool ReduceMotionRequested
        {
            get
            {
                try { return wg_reduce_motion_enabled() != 0; }
                catch (Exception) { return false; }
            }
        }
#else
        public static bool IsAvailable => false;
        public bool ReduceMotionRequested => false;
#endif
    }
}
