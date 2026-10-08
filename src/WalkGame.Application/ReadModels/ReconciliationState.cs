namespace WalkGame.Application.ReadModels
{
    /// <summary>
    /// Classified source health for the activity pipeline (D-063).
    ///
    /// The classification is derived from DURABLE evidence — when a fetch was last attempted,
    /// when one last succeeded, and the durable reconciliation watermark — never from a
    /// comparison that trusts the wall clock. A backward clock makes a source look fresher
    /// than its own evidence, so when the clock and the durable record disagree the state
    /// fails toward STALE: "possibly stale, do not reassure the player" is the only honest
    /// answer to "which of these two is lying?".
    /// </summary>
    public enum ReconciliationState
    {
        /// <summary>No fetch has ever been attempted on this profile.</summary>
        NeverAttempted = 0,

        /// <summary>A fetch succeeded recently, and no fetch since that success has failed.</summary>
        Current = 1,

        /// <summary>The newest durable activity evidence is older than the staleness threshold.</summary>
        Stale = 2,

        /// <summary>A fetch was attempted after the last successful one and did not succeed.</summary>
        FailedSinceLastSuccess = 3,
    }
}
