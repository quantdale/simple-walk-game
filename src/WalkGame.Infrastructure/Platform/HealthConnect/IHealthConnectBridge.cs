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
    /// StepsRecord: <c>id \t startEpochMs \t endEpochMs \t count \t lastModifiedEpochMs</c>;
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
