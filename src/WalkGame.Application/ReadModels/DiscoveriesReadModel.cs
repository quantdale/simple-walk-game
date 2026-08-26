using System;
using System.Collections.Generic;

namespace WalkGame.Application.ReadModels
{
    /// <summary>
    /// Presentation contract for the discovery journal (M4): every authored discovery with
    /// its unlocked/reviewed flags. Presentation binds titles/bodies/provenance via keys —
    /// canonical state never stores player-facing copy.
    /// </summary>
    public sealed class DiscoveriesReadModel
    {
        public IReadOnlyList<DiscoveryRow> Discoveries { get; }

        /// <summary>Total authored discoveries in the region content.</summary>
        public int TotalDiscoveries { get; }

        public int UnlockedCount { get; }

        public int UnreviewedCount { get; }

        public DiscoveriesReadModel(
            IReadOnlyList<DiscoveryRow> discoveries,
            int totalDiscoveries,
            int unlockedCount,
            int unreviewedCount)
        {
            Discoveries = discoveries;
            TotalDiscoveries = totalDiscoveries;
            UnlockedCount = unlockedCount;
            UnreviewedCount = unreviewedCount;
        }

        public sealed class DiscoveryRow
        {
            public string DiscoveryId { get; }
            public string Category { get; }
            public string TitleKey { get; }
            public string BodyKey { get; }
            public string ProvenanceKey { get; }
            public string? LocationKey { get; }
            public bool Unlocked { get; }
            public DateTimeOffset? DiscoveredAtUtc { get; }
            public bool Reviewed { get; }

            public DiscoveryRow(
                string discoveryId,
                string category,
                string titleKey,
                string bodyKey,
                string provenanceKey,
                string? locationKey,
                bool unlocked,
                DateTimeOffset? discoveredAtUtc,
                bool reviewed)
            {
                DiscoveryId = discoveryId;
                Category = category;
                TitleKey = titleKey;
                BodyKey = bodyKey;
                ProvenanceKey = provenanceKey;
                LocationKey = locationKey;
                Unlocked = unlocked;
                DiscoveredAtUtc = discoveredAtUtc;
                Reviewed = reviewed;
            }
        }
    }
}
