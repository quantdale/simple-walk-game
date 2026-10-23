using System;
using WalkGame.Domain.Activity;

namespace WalkGame.Application.Activity
{
    /// <summary>
    /// Player-facing activity status read model: classified enums, bounded counts and
    /// timestamps only. By construction it cannot carry raw exception text or health
    /// payloads; adapter technical detail belongs exclusively to the separate diagnostics
    /// projection. Reading is side-effect free.
    /// </summary>
    public sealed class ActivityStatusReadModel
    {
        public ActivityPlayerStatus Status { get; }

        public ActivityRecommendedAction RecommendedAction { get; }

        public bool PermissionGranted { get; }

        /// <summary>True when only a subset of requested scopes is granted.</summary>
        public bool PartiallyGranted { get; }

        public bool HasProcessedAnyRecord { get; }

        /// <summary>Durable outcome of the last ingestion batch.</summary>
        public IngestionOutcomeKind LastOutcome { get; }

        /// <summary>Vitality credited by the last successful batch (bounded aggregate).</summary>
        public long LastBatchVitalityCredited { get; }

        public DateTimeOffset? LastProcessedAtUtc { get; }

        public DateTimeOffset? LastSuccessfulRefreshUtc { get; }

        public DateTimeOffset? LastAttemptUtc { get; }

        /// <summary>D-196: the adapter cannot report a refused read (HealthKit). From the snapshot; never persisted.</summary>
        public bool ReadDenialHidden { get; }

        /// <summary>
        /// D-196: when a walk sample last ARRIVED (latest processing instant of a retained ledger row
        /// with steps), derived from existing save data; null when none is retained. Not a new field.
        /// </summary>
        public DateTimeOffset? LastSampleArrivedUtc { get; }

        public ActivityStatusReadModel(
            ActivityPlayerStatus status,
            ActivityRecommendedAction recommendedAction,
            bool permissionGranted,
            bool partiallyGranted,
            bool hasProcessedAnyRecord,
            IngestionOutcomeKind lastOutcome,
            long lastBatchVitalityCredited,
            DateTimeOffset? lastProcessedAtUtc,
            DateTimeOffset? lastSuccessfulRefreshUtc,
            DateTimeOffset? lastAttemptUtc,
            bool readDenialHidden = false,
            DateTimeOffset? lastSampleArrivedUtc = null)
        {
            ReadDenialHidden = readDenialHidden;
            LastSampleArrivedUtc = lastSampleArrivedUtc;
            Status = status;
            RecommendedAction = recommendedAction;
            PermissionGranted = permissionGranted;
            PartiallyGranted = partiallyGranted;
            HasProcessedAnyRecord = hasProcessedAnyRecord;
            LastOutcome = lastOutcome;
            LastBatchVitalityCredited = lastBatchVitalityCredited;
            LastProcessedAtUtc = lastProcessedAtUtc;
            LastSuccessfulRefreshUtc = lastSuccessfulRefreshUtc;
            LastAttemptUtc = lastAttemptUtc;
        }
    }
}
