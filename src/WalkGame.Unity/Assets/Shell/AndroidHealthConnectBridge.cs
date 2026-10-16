using UnityEngine;
using WalkGame.Infrastructure.Platform.HealthConnect;

namespace WalkGame.Shell
{
    /// <summary>
    /// JNI half of the Health Connect seam (D-068): forwards the string protocol to
    /// <c>com.quantdale.walkgame.health.HealthConnectBridge</c>. Only constructed by
    /// <see cref="ShellBootstrap"/> on an Android player; device behaviour is UNVERIFIED (KI-002).
    /// </summary>
    public sealed class AndroidHealthConnectBridge : IHealthConnectBridge
    {
        private const string BridgeClass = "com.quantdale.walkgame.health.HealthConnectBridge";

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject Activity()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                return player.GetStatic<AndroidJavaObject>("currentActivity");
        }

        private static T Call<T>(string method, params object[] args)
        {
            using (var bridge = new AndroidJavaClass(BridgeClass))
            using (var activity = Activity())
            {
                var all = new object[args.Length + 1];
                all[0] = activity;
                args.CopyTo(all, 1);
                return bridge.CallStatic<T>(method, all);
            }
        }

        public string GetSdkStatus() => Call<string>("getSdkStatus");
        public string GetPermissionStatus() => Call<string>("getPermissionStatus");
        public string ReadSteps(long startEpochMs, long endEpochMs) => Call<string>("readSteps", startEpochMs, endEpochMs);

        public static void RequestPermission()
        {
            using (var bridge = new AndroidJavaClass(BridgeClass))
            using (var activity = Activity())
                bridge.CallStatic("requestPermission", activity);
        }
#else
        public string GetSdkStatus() => "unsupported";
        public string GetPermissionStatus() => "not_requested";
        public string ReadSteps(long startEpochMs, long endEpochMs) => "error:unavailable";
        public static void RequestPermission() { }
#endif

        public static bool IsAndroidPlayer =>
#if UNITY_ANDROID && !UNITY_EDITOR
            true;
#else
            false;
#endif
    }
}
