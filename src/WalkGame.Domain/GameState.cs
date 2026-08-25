using System;
using System.Collections.Generic;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Randomness;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Time;

namespace WalkGame.Domain
{
    public static class SchemaVersions
    {
        /// <summary>Current canonical save schema version. Bump requires a registered migration.</summary>
        public const int Current = 1;

        public const int MinimumSupported = 1;
    }

    /// <summary>
    /// Canonical game state aggregate. Owned and mutated only through domain services and
    /// application use cases — never by presentation code.
    /// </summary>
    public sealed class GameState
    {
        public int SchemaVersion { get; set; } = SchemaVersions.Current;

        public DateTimeOffset CreatedAtUtc { get; set; }

        public DateTimeOffset LastAdvancedUtc { get; set; }

        public ResourceBalances Resources { get; set; } = new ResourceBalances();

        public RewardLedgerState Ledger { get; set; } = new RewardLedgerState();

        /// <summary>
        /// Durable dedup ledger of already-trusted logical activity records. Additive on
        /// schema v1: older payloads decode with an empty ledger, which is the correct
        /// semantics (nothing processed yet) without a migration.
        /// </summary>
        public ProcessedRecordLedgerState ProcessedRecords { get; set; } = new ProcessedRecordLedgerState();

        /// <summary>
        /// Reconciliation watermark: latest activity EndUtc durably trusted so far.
        /// Advanced only in the same state transition as the rewards it represents and
        /// persisted atomically together with them, so it can never outrun durable
        /// reward/ledger state.
        /// </summary>
        public DateTimeOffset IngestionCheckpointUtc { get; set; }

        public RegionState Region { get; set; } = new RegionState();

        public ProjectQueueState Queue { get; set; } = new ProjectQueueState();

        public RngState Rng { get; set; }
    }

    /// <summary>Deterministic factory for fresh games.</summary>
    public static class GameFactory
    {
        public static GameState NewGame(RegionDefinition content, DateTimeOffset nowUtc, ulong seed)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));

            var game = new GameState
            {
                CreatedAtUtc = nowUtc,
                LastAdvancedUtc = nowUtc,
                Rng = new DeterministicRng(seed).Snapshot(),
            };

            game.Region.RegionId = content.Id.Value;

            foreach (var project in content.Projects)
            {
                bool entryProject = project.Prerequisites.Count == 0;
                game.Region.Projects[project.Id.Value] = new ProjectState
                {
                    ProjectId = project.Id.Value,
                    Status = entryProject ? ProjectStatus.Available : ProjectStatus.Locked,
                    VitalityInvested = 0L,
                    CompletedAtUtc = null,
                };
            }

            foreach (var landmark in content.Landmarks)
                game.Region.LandmarkStages[landmark.Id.Value] = RestorationStage.Ruined;

            foreach (var producer in content.Producers)
            {
                game.Region.Producers.Add(new ProducerRuntimeState
                {
                    ProducerId = producer.Id.Value,
                    Unlocked = false,
                    CarryMilliUnits = 0L,
                    TotalProducedMilliUnits = 0L,
                    LastTickUtc = nowUtc,
                });
            }

            return game;
        }
    }
}
