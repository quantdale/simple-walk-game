using System;
using WalkGame.Domain.Activity;

namespace WalkGame.Application.Activity
{
    /// <summary>
    /// Player-safe standing classification of the activity connection (UX_DESIGN §5).
    /// Deliberately excludes "data processed successfully" from the standing enum: that is
    /// a LAST-OUTCOME fact carried separately so both can be presented together without
    /// conflating state and event.
    /// </summary>
    public enum ActivityPlayerStatus
    {
        /// <summary>Permission granted, source available, records have been processed.</summary>
        ConnectedCurrent = 0,

        /// <summary>Permission not yet requested — the connect action is relevant.</summary>
        PermissionNeeded = 1,

        /// <summary>Denied or externally revoked. Never traps navigation; progress preserved.</summary>
        PermissionDenied = 2,

        /// <summary>No usable provider exists on this device/platform.</summary>
        SourceUnavailable = 3,

        /// <summary>Connected but zero logical records processed so far.</summary>
        WaitingForFirstData = 4,

        /// <summary>Last refresh attempt failed transiently; prior progress fully preserved.</summary>
        RefreshTemporarilyFailed = 5,

        /// <summary>The provider is installed but outdated; the player must update it (D-093).</summary>
        ProviderUpdateRequired = 6,
    }

    /// <summary>Bounded next-action classification for the current status.</summary>
    public enum ActivityRecommendedAction
    {
        None = 0,

        /// <summary>Show the OS permission/connect flow.</summary>
        Connect = 1,

        /// <summary>Route to OS settings to re-enable a denied/revoked permission.</summary>
        OpenSettings = 2,

        /// <summary>Suggest retrying the refresh later; nothing is lost by waiting.</summary>
        RetryLater = 3,

        /// <summary>Route to the store/system to update the activity provider (D-093).</summary>
        UpdateProvider = 4,
    }

    /// <summary>
    /// Player-facing activity status projection (Workstream C). Pure function of the
    /// adapter snapshot + canonical facts + durable last outcome — deterministic, side-
    /// effect free, and identical for every caller. Raw exceptions and raw health payloads
    /// can never appear here: only classified enums, counts, and timestamps cross this
    /// boundary (D-043, D-016).
    ///
    /// Classification precedence (documented contract for adapters and tests):
    ///   0. Availability ProviderUpdateRequired → ProviderUpdateRequired (D-093: waiting or
    ///      connecting cannot fix an outdated provider, so it outranks permission)
    ///   1. Availability Unsupported  → SourceUnavailable, whatever the permission (D-068, D-146)
    ///   2. Denied/Revoked            → PermissionDenied
    ///   3. NotRequested              → PermissionNeeded
    ///   4. TemporarilyUnavailable or last fetch failed → RefreshTemporarilyFailed
    ///   5. Connected, zero processed → WaitingForFirstData
    ///   6. otherwise                 → ConnectedCurrent
    /// </summary>
    public static class ActivityStatusProjector
    {
        public static ActivityStatusReadModel Project(
            ActivityConnectionSnapshot? snapshot,
            bool hasProcessedAnyRecord,
            IngestionOutcomeKind lastOutcome,
            long lastBatchVitalityCredited,
            DateTimeOffset? lastProcessedAtUtc)
        {
            if (snapshot == null)
                return new ActivityStatusReadModel(
                    ActivityPlayerStatus.SourceUnavailable,
                    ActivityRecommendedAction.None,
                    permissionGranted: false,
                    partiallyGranted: false,
                    hasProcessedAnyRecord: hasProcessedAnyRecord,
                    lastOutcome: lastOutcome,
                    lastBatchVitalityCredited: lastBatchVitalityCredited,
                    lastProcessedAtUtc: lastProcessedAtUtc,
                    lastSuccessfulRefreshUtc: null,
                    lastAttemptUtc: null);

            var status = Classify(snapshot, hasProcessedAnyRecord, lastOutcome);
            return new ActivityStatusReadModel(
                status,
                Recommend(status),
                IsGranted(snapshot.Permission),
                snapshot.Permission == ActivityPermissionState.PartiallyGranted,
                hasProcessedAnyRecord,
                lastOutcome,
                lastBatchVitalityCredited,
                lastProcessedAtUtc,
                snapshot.LastSuccessfulRefreshUtc,
                snapshot.LastAttemptUtc);
        }

        private static ActivityPlayerStatus Classify(
            ActivityConnectionSnapshot snapshot, bool hasProcessedAnyRecord, IngestionOutcomeKind lastOutcome)
        {
            if (snapshot.Availability == ActivitySourceAvailability.ProviderUpdateRequired)
                return ActivityPlayerStatus.ProviderUpdateRequired;

            // D-068: with no provider on the device, permission is meaningless; asking the
            // player to "connect" something that cannot exist is a dead end.
            // D-146: the same holds after a prior Denied/Revoked: "Open Settings" cannot grant
            // access to a provider that is not installed, so the missing source wins.
            if (snapshot.Availability == ActivitySourceAvailability.Unsupported)
                return ActivityPlayerStatus.SourceUnavailable;

            switch (snapshot.Permission)
            {
                case ActivityPermissionState.Denied:
                case ActivityPermissionState.Revoked:
                    return ActivityPlayerStatus.PermissionDenied;
                case ActivityPermissionState.NotRequested:
                    return ActivityPlayerStatus.PermissionNeeded;
            }

            if (snapshot.Availability == ActivitySourceAvailability.TemporarilyUnavailable
                || lastOutcome == IngestionOutcomeKind.SourceFetchFailed)
                return ActivityPlayerStatus.RefreshTemporarilyFailed;

            if (!hasProcessedAnyRecord)
                return ActivityPlayerStatus.WaitingForFirstData;

            return ActivityPlayerStatus.ConnectedCurrent;
        }

        private static ActivityRecommendedAction Recommend(ActivityPlayerStatus status)
        {
            switch (status)
            {
                case ActivityPlayerStatus.PermissionNeeded: return ActivityRecommendedAction.Connect;
                case ActivityPlayerStatus.PermissionDenied: return ActivityRecommendedAction.OpenSettings;
                case ActivityPlayerStatus.RefreshTemporarilyFailed: return ActivityRecommendedAction.RetryLater;
                case ActivityPlayerStatus.ProviderUpdateRequired: return ActivityRecommendedAction.UpdateProvider;
                default: return ActivityRecommendedAction.None;
            }
        }

        private static bool IsGranted(ActivityPermissionState permission) =>
            permission == ActivityPermissionState.Granted || permission == ActivityPermissionState.PartiallyGranted;
    }
}
