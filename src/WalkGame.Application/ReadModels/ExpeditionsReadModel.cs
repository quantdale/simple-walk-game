using System;
using System.Collections.Generic;
using WalkGame.Domain.Economy;

namespace WalkGame.Application.ReadModels
{
    /// <summary>
    /// Presentation contract for expedition routes (M4 boundary, D-037): stable
    /// definitions plus deterministic availability/completion state. There is no
    /// interactive start/claim mechanic at this milestone — routes resolve from canonical
    /// progression while the app is closed.
    /// </summary>
    public sealed class ExpeditionsReadModel
    {
        public IReadOnlyList<ExpeditionRow> Expeditions { get; }

        public int TotalExpeditions { get; }

        public int AvailableCount { get; }

        public int CompletedCount { get; }

        public ExpeditionsReadModel(
            IReadOnlyList<ExpeditionRow> expeditions,
            int totalExpeditions,
            int availableCount,
            int completedCount)
        {
            Expeditions = expeditions;
            TotalExpeditions = totalExpeditions;
            AvailableCount = availableCount;
            CompletedCount = completedCount;
        }

        public enum ExpeditionStatus
        {
            Locked = 0,
            Available = 1,
            Completed = 2,
        }

        public sealed class ExpeditionRow
        {
            public string ExpeditionId { get; }
            public string TitleKey { get; }
            public string DescriptionKey { get; }
            public ExpeditionStatus Status { get; }
            public IReadOnlyList<string> RequiredProjectIds { get; }
            public IReadOnlyList<string> RequiredStageKeys { get; }
            public ResourceType? RewardType { get; }
            public long RewardUnits { get; }
            public DateTimeOffset? CompletedAtUtc { get; }

            public ExpeditionRow(
                string expeditionId,
                string titleKey,
                string descriptionKey,
                ExpeditionStatus status,
                IReadOnlyList<string> requiredProjectIds,
                IReadOnlyList<string> requiredStageKeys,
                ResourceType? rewardType,
                long rewardUnits,
                DateTimeOffset? completedAtUtc)
            {
                ExpeditionId = expeditionId;
                TitleKey = titleKey;
                DescriptionKey = descriptionKey;
                Status = status;
                RequiredProjectIds = requiredProjectIds;
                RequiredStageKeys = requiredStageKeys;
                RewardType = rewardType;
                RewardUnits = rewardUnits;
                CompletedAtUtc = completedAtUtc;
            }
        }
    }
}
