using WalkGame.Application.Activity;

namespace WalkGame.Shell
{
    /// <summary>
    /// M5-U1 stand-in until the Health Connect adapter (M5-A) exists. Development builds report
    /// the synthetic source as connected; release builds honestly report the source as
    /// unsupported rather than pretending a permission was granted.
    /// </summary>
    public sealed class ShellConnectionPort : IActivityConnectionPort
    {
        private readonly bool _development;
        public ShellConnectionPort(bool development) { _development = development; }

        public ActivityConnectionSnapshot SnapshotConnection() => _development
            ? new ActivityConnectionSnapshot(ActivityPermissionState.Granted, ActivitySourceAvailability.Available, technicalDetail: "synthetic development source")
            : new ActivityConnectionSnapshot(ActivityPermissionState.NotRequested, ActivitySourceAvailability.Unsupported, technicalDetail: "health connect adapter not yet implemented (M5-A)");
    }
}
