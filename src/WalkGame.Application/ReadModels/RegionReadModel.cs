using System;
using System.Collections.Generic;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Regions;

namespace WalkGame.Application.ReadModels
{
    /// <summary>
    /// Lightweight, non-3D region status read model: canonical landmark stages, producer
    /// unlock/output/store state and overall progress. Damaged vs restored must be
    /// distinguishable without color alone — presentation derives that from Stage values.
    /// </summary>
    public sealed class RegionReadModel
    {
        public string RegionTitleKey { get; }

        public IReadOnlyList<LandmarkRow> Landmarks { get; }

        public IReadOnlyList<ProducerRow> Producers { get; }

        public int CompletedProjects { get; }

        public int TotalProjects { get; }

        public string? ActiveProjectId { get; }

        /// <summary>Highest reached region-level ecology arc stage; 0 = baseline.</summary>
        public int EcologyStage { get; }

        /// <summary>Highest reached region-level settlement arc stage; 0 = baseline.</summary>
        public int SettlementStage { get; }

        /// <summary>True once the closure milestone has been reached; never resets.</summary>
        public bool RegionCompleted { get; }

        public DateTimeOffset? RegionCompletedAtUtc { get; }

        public int DiscoveriesUnlocked { get; }

        public int ExpeditionsAvailable { get; }

        public int ExpeditionsCompleted { get; }

        public RegionReadModel(
            string regionTitleKey,
            IReadOnlyList<LandmarkRow> landmarks,
            IReadOnlyList<ProducerRow> producers,
            int completedProjects,
            int totalProjects,
            string? activeProjectId,
            int ecologyStage = 0,
            int settlementStage = 0,
            bool regionCompleted = false,
            DateTimeOffset? regionCompletedAtUtc = null,
            int discoveriesUnlocked = 0,
            int expeditionsAvailable = 0,
            int expeditionsCompleted = 0)
        {
            RegionTitleKey = regionTitleKey;
            Landmarks = landmarks;
            Producers = producers;
            CompletedProjects = completedProjects;
            TotalProjects = totalProjects;
            ActiveProjectId = activeProjectId;
            EcologyStage = ecologyStage;
            SettlementStage = settlementStage;
            RegionCompleted = regionCompleted;
            RegionCompletedAtUtc = regionCompletedAtUtc;
            DiscoveriesUnlocked = discoveriesUnlocked;
            ExpeditionsAvailable = expeditionsAvailable;
            ExpeditionsCompleted = expeditionsCompleted;
        }

        public sealed class LandmarkRow
        {
            public string LandmarkId { get; }
            public string TitleKey { get; }
            public RestorationStage Stage { get; }

            public LandmarkRow(string landmarkId, string titleKey, RestorationStage stage)
            {
                LandmarkId = landmarkId;
                TitleKey = titleKey;
                Stage = stage;
            }
        }

        public sealed class ProducerRow
        {
            public string ProducerId { get; }
            public string TitleKey { get; }
            public ResourceType Output { get; }
            public long MilliUnitsPerDay { get; }
            public long CapacityUnits { get; }
            public bool Unlocked { get; }

            /// <summary>Units currently held in the producer's bounded pending store.</summary>
            public long StoredMilliUnits { get; }

            /// <summary>Total units ever delivered to canonical balances.</summary>
            public long TotalProducedMilliUnits { get; }

            public ProducerRow(
                string producerId,
                string titleKey,
                ResourceType output,
                long milliUnitsPerDay,
                long capacityUnits,
                bool unlocked,
                long storedMilliUnits,
                long totalProducedMilliUnits)
            {
                ProducerId = producerId;
                TitleKey = titleKey;
                Output = output;
                MilliUnitsPerDay = milliUnitsPerDay;
                CapacityUnits = capacityUnits;
                Unlocked = unlocked;
                StoredMilliUnits = storedMilliUnits;
                TotalProducedMilliUnits = totalProducedMilliUnits;
            }
        }
    }
}
