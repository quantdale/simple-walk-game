using System;
using System.Collections.Generic;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Regions;
using ExpeditionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ExpeditionIdKind>;
using LandmarkId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.LandmarkIdKind>;

namespace WalkGame.Domain.Expeditions
{
    /// <summary>
    /// M4 expedition boundary (D-037): stable route definition plus deterministic
    /// availability/completion hooks — not a foreground interactive mechanic. Availability
    /// requires every <see cref="RequiredProjectIds"/> completed; completion requires every
    /// required landmark stage reached. Both derive only from canonical state, continue
    /// while the app is closed, and cannot double-fire.
    /// </summary>
    public sealed class ExpeditionDefinition
    {
        public ExpeditionId Id { get; }
        public string TitleKey { get; }
        public string DescriptionKey { get; }

        public IReadOnlyList<string> RequiredProjectIds { get; }

        public IReadOnlyList<ExpeditionStageRequirement> RequiredStages { get; }

        /// <summary>One-time clamped resource grant applied exactly once at completion; null = none.</summary>
        public ExpeditionReward? Reward { get; }

        public ExpeditionDefinition(
            ExpeditionId id,
            string titleKey,
            string descriptionKey,
            IEnumerable<string> requiredProjectIds,
            IEnumerable<ExpeditionStageRequirement>? requiredStages = null,
            ExpeditionReward? reward = null)
        {
            if (!id.IsValid)
                throw new ArgumentException("Expedition definition requires a valid ID.", nameof(id));
            if (string.IsNullOrWhiteSpace(titleKey))
                throw new ArgumentException("Expedition definition requires a title key.", nameof(titleKey));
            if (string.IsNullOrWhiteSpace(descriptionKey))
                throw new ArgumentException("Expedition definition requires a description key.", nameof(descriptionKey));

            Id = id;
            TitleKey = titleKey;
            DescriptionKey = descriptionKey;
            RequiredProjectIds = new List<string>(requiredProjectIds).AsReadOnly();
            RequiredStages = requiredStages != null
                ? new List<ExpeditionStageRequirement>(requiredStages).AsReadOnly()
                : (IReadOnlyList<ExpeditionStageRequirement>)new List<ExpeditionStageRequirement>().AsReadOnly();
            Reward = reward;
        }
    }

    /// <summary>A landmark stage that must be reached before an expedition can complete.</summary>
    public sealed class ExpeditionStageRequirement
    {
        public string LandmarkId { get; }
        public RestorationStage Stage { get; }

        public ExpeditionStageRequirement(string landmarkId, RestorationStage stage)
        {
            if (string.IsNullOrWhiteSpace(landmarkId))
                throw new ArgumentException("Stage requirement requires a landmark ID.", nameof(landmarkId));
            if (stage < RestorationStage.Stabilized)
                throw new ArgumentException("Stage requirement must target Stabilized or beyond.", nameof(stage));
            LandmarkId = landmarkId;
            Stage = stage;
        }
    }

    /// <summary>Deterministic one-time expedition reward. Integer units, cap-clamped on apply.</summary>
    public sealed class ExpeditionReward
    {
        public ResourceType Type { get; }
        public long Units { get; }

        public ExpeditionReward(ResourceType type, long units)
        {
            if (units <= 0L)
                throw new ArgumentException("Expedition rewards must be positive.", nameof(units));
            Type = type;
            Units = units;
        }
    }

    /// <summary>
    /// Player-specific expedition state. Entry appears when the route becomes available;
    /// absence means "not yet available". Completion happens at most once and its reward
    /// is applied in the same state transition as the completion timestamp.
    /// </summary>
    public sealed class ExpeditionRuntimeState
    {
        public string ExpeditionId { get; set; } = string.Empty;

        public DateTimeOffset AvailableAtUtc { get; set; }

        public DateTimeOffset? CompletedAtUtc { get; set; }
    }
}
