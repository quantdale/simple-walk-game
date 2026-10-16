using System.Runtime.InteropServices;
using WalkGame.Infrastructure.Platform.HealthKit;

namespace WalkGame.Shell
{
    /// <summary>
    /// P/Invoke half of the HealthKit seam (KI-022, D-112): forwards the string protocol to
    /// <c>Assets/Plugins/iOS/WalkGameHealthKitBridge.swift</c>. NOT composed by <see cref="ShellBootstrap"/>
    /// yet: the Swift side has never been compiled or run, so iOS players keep the honest
    /// unsupported state (D-108) until a macOS build and a device check exist. UNVERIFIED.
    /// </summary>
    public sealed class IosHealthKitBridge : IHealthKitBridge
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern string wg_hk_availability();
        [DllImport("__Internal")] private static extern string wg_hk_request_status();
        [DllImport("__Internal")] private static extern void wg_hk_request_authorization();
        [DllImport("__Internal")] private static extern string wg_hk_read_steps(long startMs, long endMs);

        public static bool IsIosPlayer => true;
        public string GetAvailability() => wg_hk_availability();
        public string GetRequestStatus() => wg_hk_request_status();
        public string ReadSteps(long startEpochMs, long endEpochMs) => wg_hk_read_steps(startEpochMs, endEpochMs);
        public static void RequestAuthorization() => wg_hk_request_authorization();
#else
        public static bool IsIosPlayer => false;
        public string GetAvailability() => "unsupported";
        public string GetRequestStatus() => "unknown";
        public string ReadSteps(long startEpochMs, long endEpochMs) => "error:unavailable";
        public static void RequestAuthorization() { }
#endif
    }
}
