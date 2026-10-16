using WalkGame.Application.Activity;

namespace WalkGame.Shell
{
    /// <summary>
    /// Connection port for every non-Android player (the editor, desktop, and iOS until a HealthKit
    /// adapter exists, KI-022; Android uses the Health Connect adapter, D-068). Development builds
    /// report the synthetic source as connected; release builds honestly report the source as
    /// unsupported rather than pretending a permission was granted (D-108 parity check).
    /// </summary>
    public sealed class ShellConnectionPort : IActivityConnectionPort
    {
        /// <summary>Support-diagnostics detail (never player-facing, UX_DESIGN §5).</summary>
        public const string UnsupportedDetail = "no walking data provider on this platform (HealthKit adapter not implemented, KI-022)";
        private readonly bool _development;
        public ShellConnectionPort(bool development) { _development = development; }

        public ActivityConnectionSnapshot SnapshotConnection() => _development
            ? new ActivityConnectionSnapshot(ActivityPermissionState.Granted, ActivitySourceAvailability.Available, technicalDetail: "synthetic development source")
            : new ActivityConnectionSnapshot(ActivityPermissionState.NotRequested, ActivitySourceAvailability.Unsupported, technicalDetail: UnsupportedDetail);
    }
}
