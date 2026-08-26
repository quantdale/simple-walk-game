using System;
using ProjectId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProjectIdKind>;

namespace WalkGame.Domain.Discoveries
{
    /// <summary>
    /// M4 discovery trigger boundary (D-037): a discovery unlocks deterministically when
    /// one designated restoration project completes. Canonical, replay-safe and
    /// idempotent; richer trigger models (activity thresholds, expedition completions,
    /// visit interactions) remain future work per GAME_SYSTEMS §7 and must reuse this
    /// durable unlocked/reviewed state shape.
    /// </summary>
    public sealed class DiscoveryDefinition
    {
        public Common.Id<Common.DiscoveryIdKind> Id { get; }
        public string Category { get; }
        public string TitleKey { get; }
        public string BodyKey { get; }

        /// <summary>Localization/presentation key for provenance text or data.</summary>
        public string ProvenanceKey { get; }

        public string UnlockedByProjectId { get; }

        /// <summary>Optional world-location key for later Visit World binding.</summary>
        public string? LocationKey { get; }

        public DiscoveryDefinition(
            Common.Id<Common.DiscoveryIdKind> id,
            string category,
            string titleKey,
            string bodyKey,
            string provenanceKey,
            string unlockedByProjectId,
            string? locationKey = null)
        {
            if (!id.IsValid)
                throw new ArgumentException("Discovery definition requires a valid ID.", nameof(id));
            if (string.IsNullOrWhiteSpace(category))
                throw new ArgumentException("Discovery definition requires a category.", nameof(category));
            if (string.IsNullOrWhiteSpace(titleKey))
                throw new ArgumentException("Discovery definition requires a title key.", nameof(titleKey));
            if (string.IsNullOrWhiteSpace(bodyKey))
                throw new ArgumentException("Discovery definition requires a body key.", nameof(bodyKey));
            if (string.IsNullOrWhiteSpace(provenanceKey))
                throw new ArgumentException("Discovery definition requires a provenance key.", nameof(provenanceKey));
            if (string.IsNullOrWhiteSpace(unlockedByProjectId))
                throw new ArgumentException("Discovery definitions must reference the project that unlocks them.", nameof(unlockedByProjectId));

            Id = id;
            Category = category;
            TitleKey = titleKey;
            BodyKey = bodyKey;
            ProvenanceKey = provenanceKey;
            UnlockedByProjectId = unlockedByProjectId;
            LocationKey = locationKey;
        }
    }

    /// <summary>
    /// Player-specific discovery state. An entry exists in canonical state only after the
    /// discovery has been unlocked; absence always means "not yet discovered", which keeps
    /// legacy saves and fresh saves on identical semantics without a schema migration.
    /// Reviewed is a presentation convenience (GAME_SYSTEMS §7): it never gates
    /// progression and transitions at most once.
    /// </summary>
    public sealed class DiscoveryRuntimeState
    {
        public string DiscoveryId { get; set; } = string.Empty;

        public DateTimeOffset DiscoveredAtUtc { get; set; }

        public bool Reviewed { get; set; }

        public DateTimeOffset? ReviewedAtUtc { get; set; }
    }
}
