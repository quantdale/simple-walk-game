namespace WalkGame.Infrastructure.Platform.HealthKit
{
    /// <summary>
    /// String-protocol seam to the native iOS bridge (<c>Assets/Plugins/iOS/WalkGameHealthKitBridge.swift</c>,
    /// D-112). Same shape as the Health Connect seam (D-068) so every mapping rule is tested headless
    /// with a fake. The native side is UNVERIFIED: it has never been compiled or run (no macOS host, KI-022).
    ///
    /// Protocol (lower-case ASCII):
    /// <list type="bullet">
    /// <item><see cref="GetAvailability"/>: <c>available</c> | <c>unsupported</c> (HKHealthStore.isHealthDataAvailable).</item>
    /// <item><see cref="GetRequestStatus"/>: <c>should_request</c> | <c>unnecessary</c> | <c>unknown</c>
    /// (HKHealthStore.getRequestStatusForAuthorization). HealthKit never reveals whether READ access
    /// was granted; <c>unnecessary</c> only means the player has answered the dialog.</item>
    /// <item><see cref="ReadSteps"/>: first line <c>ok</c>, then one line per HKQuantitySample of
    /// stepCount: <c>uuid \t startEpochMs \t endEpochMs \t count \t tzOffsetSeconds</c> (offset empty
    /// when the sample has no HKMetadataKeyTimeZone), optionally followed by <c>\t source</c> (D-114:
    /// <c>watch</c> | <c>phone</c> | <c>other</c>, classified natively with the
    /// <see cref="HealthKitSourceDedup.Classify"/> rule; absent → other), and one line per deleted sample the bridge
    /// returned before: <c>del \t uuid \t startEpochMs \t endEpochMs</c> (the bridge remembers each
    /// returned sample's interval because HKDeletedObject carries only the UUID). A lost anchor is
    /// handled natively by a full re-read of the window; replays are exactly-once no-ops in the
    /// pipeline. Or a single line <c>error:permission</c> | <c>error:unavailable</c> |
    /// <c>error:timeout</c> | <c>error:io</c>.</item>
    /// </list>
    /// </summary>
    public interface IHealthKitBridge
    {
        string GetAvailability();
        string GetRequestStatus();
        string ReadSteps(long startEpochMs, long endEpochMs);
    }
}
