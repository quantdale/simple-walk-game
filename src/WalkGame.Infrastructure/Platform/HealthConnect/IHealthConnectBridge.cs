namespace WalkGame.Infrastructure.Platform.HealthConnect
{
    /// <summary>
    /// String-protocol seam to the native Android bridge
    /// (<c>com.quantdale.walkgame.health.HealthConnectBridge</c>, Java). Plain strings keep the
    /// JNI surface trivial and let every mapping rule be tested headless with a fake.
    ///
    /// Protocol (all values lower-case ASCII):
    /// <list type="bullet">
    /// <item><see cref="GetSdkStatus"/>: <c>available</c> | <c>unsupported</c> | <c>update_required</c>.</item>
    /// <item><see cref="GetPermissionStatus"/>: <c>granted</c> | <c>not_requested</c> | <c>denied</c> | <c>revoked</c>.</item>
    /// <item><see cref="ReadSteps"/>: first line <c>ok</c>, then one line per Health Connect
    /// StepsRecord: <c>id \t startEpochMs \t endEpochMs \t count \t lastModifiedEpochMs</c>,
    /// and one line per record deleted in Health Connect after we returned it (D-069):
    /// <c>del \t id \t startEpochMs \t endEpochMs \t lastModifiedEpochMs \t deletedEpochMs</c>.
    /// The bridge re-emits a deletion on every read while its interval is inside the window, so
    /// the pipeline's stale-revision rule (not bridge bookkeeping) makes it exactly-once;
    /// or a single line <c>error:permission</c> | <c>error:unavailable</c> | <c>error:timeout</c> | <c>error:io</c>.</item>
    /// </list>
    /// Unknown values are treated fail-closed by <see cref="HealthConnectActivityAdapter"/>.
    /// </summary>
    public interface IHealthConnectBridge
    {
        string GetSdkStatus();
        string GetPermissionStatus();
        string ReadSteps(long startEpochMs, long endEpochMs);
    }
}
