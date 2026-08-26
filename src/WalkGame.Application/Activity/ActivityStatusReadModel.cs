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
            DateTimeOffset? lastAttemptUtc)
        {
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
