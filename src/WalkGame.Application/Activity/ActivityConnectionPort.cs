using System;

namespace WalkGame.Application.Activity
{
    /// <summary>OS-level permission posture for the activity source, as seen by an adapter.</summary>
    public enum ActivityPermissionState
    {
        /// <summary>The app never asked / OS reports undecided.</summary>
        NotRequested = 0,

        Granted = 1,

        /// <summary>Granted but only for a subset of requested scopes.</summary>
        PartiallyGranted = 2,

        /// <summary>Actively refused.</summary>
        Denied = 3,

        /// <summary>Was granted before and has been withdrawn outside the app.</summary>
        Revoked = 4,
    }

    /// <summary>Whether the underlying activity provider can currently serve data at all.</summary>
    public enum ActivitySourceAvailability
    {
        Available = 0,

        /// <summary>Transient failure (provider busy, temporary error) — retry may succeed.</summary>
        TemporarilyUnavailable = 1,

        /// <summary>No provider on this device/platform; not a transient condition.</summary>
        Unsupported = 2,
    }

    /// <summary>
    /// Adapter-reported platform state for the activity connection. This is EPHEMERAL
    /// PLATFORM STATE owned by the adapter — it is projected through
    /// <see cref="ActivityStatusProjector"/> into player-safe/diagnostic read models and is
    /// never persisted by this application (D-043). <see cref="TechnicalDetail"/> may carry
    /// adapter-chosen technical text; it must only ever be surfaced through the separate
    /// support diagnostics projection, never as ordinary player copy.
    /// </summary>
    public sealed class ActivityConnectionSnapshot
    {
        public ActivityPermissionState Permission { get; }

        public ActivitySourceAvailability Availability { get; }

        /// <summary>UTC time of the adapter's last successful refresh attempt, if known.</summary>
        public DateTimeOffset? LastSuccessfulRefreshUtc { get; }

        /// <summary>UTC time of the adapter's last refresh attempt of any kind, if known.</summary>
        public DateTimeOffset? LastAttemptUtc { get; }

        /// <summary>Adapter-owned technical detail for diagnostics only. Never player copy.</summary>
        public string? TechnicalDetail { get; }

        public ActivityConnectionSnapshot(
            ActivityPermissionState permission,
            ActivitySourceAvailability availability,
            DateTimeOffset? lastSuccessfulRefreshUtc = null,
            DateTimeOffset? lastAttemptUtc = null,
            string? technicalDetail = null)
        {
            Permission = permission;
            Availability = availability;
            LastSuccessfulRefreshUtc = lastSuccessfulRefreshUtc;
            LastAttemptUtc = lastAttemptUtc;
            TechnicalDetail = technicalDetail;
        }
    }

    /// <summary>
    /// THE narrow platform-neutral seam future Health Connect/HealthKit native adapters
    /// implement (D-043). Test doubles and development providers sit behind exactly the
    /// same interface. Implementations only REPORT platform truth; they may never award,
    /// revoke, replay, or mutate game progression.
    /// </summary>
    public interface IActivityConnectionPort
    {
        ActivityConnectionSnapshot SnapshotConnection();
    }
}
