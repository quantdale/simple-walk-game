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

        /// <summary>
        /// The authored post-completion program and its durable progress (D-061): what
        /// activity earned after the ending is used for. Null only before completion or when
        /// the region authors no program, so a shell can tell "the valley is finished" from
        /// "there is nothing left to show" without guessing.
        /// </summary>
        public PostCompletionCareRow? PostCompletionCare { get; }

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
            int expeditionsCompleted = 0,
            PostCompletionCareRow? postCompletionCare = null)
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
            PostCompletionCare = postCompletionCare;
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

            /// <summary>Authored cap for the resource this producer emits (D-054).</summary>
            public long ResourceCapUnits { get; }

            /// <summary>True when this producer's resource is at its cap, so output is parked
            /// rather than delivered. Lets the shell tell "capped" apart from "idle": a
            /// capped producer has produced plenty and is holding it, not doing nothing.</summary>
            public bool CapacityLimited { get; }

            public ProducerRow(
                string producerId,
                string titleKey,
                ResourceType output,
                long milliUnitsPerDay,
                long capacityUnits,
                bool unlocked,
                long storedMilliUnits,
                long totalProducedMilliUnits,
                long resourceCapUnits = long.MaxValue,
                bool capacityLimited = false)
            {
                ProducerId = producerId;
                TitleKey = titleKey;
                Output = output;
                MilliUnitsPerDay = milliUnitsPerDay;
                CapacityUnits = capacityUnits;
                Unlocked = unlocked;
                StoredMilliUnits = storedMilliUnits;
                TotalProducedMilliUnits = totalProducedMilliUnits;
                ResourceCapUnits = resourceCapUnits;
                CapacityLimited = capacityLimited;
            }
        }
        /// <summary>
        /// One authored post-completion program with its durable progress. Presented through
        /// the region surface rather than the project roster on purpose: it is optional depth
        /// that gates nothing, and putting it among the projects would make a completed region
        /// read as unfinished work (D-061).
        /// </summary>
        public sealed class PostCompletionCareRow
        {
            public string ProgramId { get; }
            public string TitleKey { get; }
            public string DescriptionKey { get; }

            /// <summary>Completed cycles. Repeats indefinitely; never a completion flag.</summary>
            public long CyclesCompleted { get; }

            public long InvestedVitality { get; }

            public long VitalityCostPerCycle { get; }

            public PostCompletionCareRow(
                string programId,
                string titleKey,
                string descriptionKey,
                long cyclesCompleted,
                long investedVitality,
                long vitalityCostPerCycle)
            {
                ProgramId = programId;
                TitleKey = titleKey;
                DescriptionKey = descriptionKey;
                CyclesCompleted = cyclesCompleted;
                InvestedVitality = investedVitality;
                VitalityCostPerCycle = vitalityCostPerCycle;
            }
        }
    }
}
