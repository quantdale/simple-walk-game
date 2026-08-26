using System;
using System.Collections.Generic;
using WalkGame.Domain.Discoveries;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Expeditions;
using WalkGame.Domain.Projects;
using LandmarkId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.LandmarkIdKind>;
using ProducerId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProducerIdKind>;
using ProjectId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProjectIdKind>;
using RegionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RegionIdKind>;

namespace WalkGame.Domain.Regions
{
    /// <summary>
    /// Canonical restoration stages. Not every landmark uses all five, but content must
    /// define its progression explicitly; visuals derive from this state via bindings.
    /// </summary>
    public enum RestorationStage
    {
        Ruined = 0,
        Stabilized = 1,
        Functional = 2,
        Restored = 3,
        Flourishing = 4,
    }

    public sealed class LandmarkStageDefinition
    {
        public RestorationStage Stage { get; }
        public string UnlockedByProjectId { get; }

        public LandmarkStageDefinition(RestorationStage stage, string unlockedByProjectId)
        {
            if (string.IsNullOrWhiteSpace(unlockedByProjectId))
                throw new ArgumentException("Landmark stages must reference the project that unlocks them.", nameof(unlockedByProjectId));
            Stage = stage;
            UnlockedByProjectId = unlockedByProjectId;
        }
    }

    public sealed class LandmarkDefinition
    {
        public LandmarkId Id { get; }
        public string TitleKey { get; }
        public IReadOnlyList<LandmarkStageDefinition> Stages { get; }

        public LandmarkDefinition(LandmarkId id, string titleKey, IEnumerable<LandmarkStageDefinition> stages)
        {
            if (!id.IsValid)
                throw new ArgumentException("Landmark definition requires a valid ID.", nameof(id));
            if (string.IsNullOrWhiteSpace(titleKey))
                throw new ArgumentException("Landmark definition requires a title key.", nameof(titleKey));

            Id = id;
            TitleKey = titleKey;
            Stages = new List<LandmarkStageDefinition>(stages).AsReadOnly();
        }
    }

    /// <summary>
    /// Producer rate model uses integer milli-units per day so offline simulation stays
    /// deterministic across platforms with no floating point in canonical math.
    /// </summary>
    public sealed class ProducerDefinition
    {
        public const long MilliUnitsPerUnit = 1000L;

        public ProducerId Id { get; }
        public string TitleKey { get; }
        public Economy.ResourceType Output { get; }
        public long MilliUnitsPerDay { get; }
        public long CapacityUnits { get; }
        public string UnlockedByProjectId { get; }

        public ProducerDefinition(ProducerId id, string titleKey, Economy.ResourceType output, long milliUnitsPerDay, long capacityUnits, string unlockedByProjectId)
        {
            if (!id.IsValid)
                throw new ArgumentException("Producer definition requires a valid ID.", nameof(id));
            if (string.IsNullOrWhiteSpace(titleKey))
                throw new ArgumentException("Producer definition requires a title key.", nameof(titleKey));
            if (milliUnitsPerDay <= 0L)
                throw new ArgumentException("Producer rate must be positive.", nameof(milliUnitsPerDay));
            if (capacityUnits <= 0L)
                throw new ArgumentException("Producer capacity must be positive.", nameof(capacityUnits));
            if (string.IsNullOrWhiteSpace(unlockedByProjectId))
                throw new ArgumentException("Producers must reference the project that unlocks them.", nameof(unlockedByProjectId));

            Id = id;
            TitleKey = titleKey;
            Output = output;
            MilliUnitsPerDay = milliUnitsPerDay;
            CapacityUnits = capacityUnits;
            UnlockedByProjectId = unlockedByProjectId;
        }
    }

    /// <summary>One discrete stage of a region-level progression arc (D-038).</summary>
    public sealed class ProgressionStageDefinition
    {
        /// <summary>1-based, strictly ascending within an arc.</summary>
        public int Stage { get; }

        public string UnlockedByProjectId { get; }

        public ProgressionStageDefinition(int stage, string unlockedByProjectId)
        {
            if (stage < 1)
                throw new ArgumentException("Progression stages are 1-based.", nameof(stage));
            if (string.IsNullOrWhiteSpace(unlockedByProjectId))
                throw new ArgumentException("Progression stages must reference the project that unlocks them.", nameof(unlockedByProjectId));
            Stage = stage;
            UnlockedByProjectId = unlockedByProjectId;
        }
    }

    /// <summary>
    /// A region-level progression axis (ecology or settlement): discrete, monotonic,
    /// explainable stages driven by project completion — deliberately not a continuous
    /// simulation (GAME_SYSTEMS §10).
    /// </summary>
    public sealed class RegionProgressionDefinition
    {
        public IReadOnlyList<ProgressionStageDefinition> Stages { get; }

        public RegionProgressionDefinition(IEnumerable<ProgressionStageDefinition> stages)
        {
            Stages = new List<ProgressionStageDefinition>(stages).AsReadOnly();
        }

        public static RegionProgressionDefinition Empty() =>
            new RegionProgressionDefinition(Array.Empty<ProgressionStageDefinition>());
    }

    /// <summary>
    /// Immutable content for one region. Definitions carry authoring data only;
    /// player-specific values live in <see cref="RegionState"/>.
    /// </summary>
    public sealed class RegionDefinition
    {
        public RegionId Id { get; }
        public string TitleKey { get; }

        /// <summary>Authored content contract version; bumps whenever authored content changes meaning.</summary>
        public int ContentVersion { get; }

        public IReadOnlyList<ProjectDefinition> Projects { get; }
        public IReadOnlyList<LandmarkDefinition> Landmarks { get; }
        public IReadOnlyList<ProducerDefinition> Producers { get; }
        public IReadOnlyList<DiscoveryDefinition> Discoveries { get; }
        public IReadOnlyList<ExpeditionDefinition> Expeditions { get; }

        /// <summary>Region-level ecological recovery arc.</summary>
        public RegionProgressionDefinition EcologyProgression { get; }

        /// <summary>Region-level settlement/hub arc.</summary>
        public RegionProgressionDefinition SettlementProgression { get; }

        /// <summary>The project whose completion is the explicit region closure milestone; null when the region has no closure yet.</summary>
        public string? CompletionMilestoneProjectId { get; }

        public RegionDefinition(RegionId id, string titleKey,
            IEnumerable<ProjectDefinition> projects,
            IEnumerable<LandmarkDefinition> landmarks,
            IEnumerable<ProducerDefinition> producers,
            int contentVersion = 1,
            IEnumerable<DiscoveryDefinition>? discoveries = null,
            IEnumerable<ExpeditionDefinition>? expeditions = null,
            RegionProgressionDefinition? ecologyProgression = null,
            RegionProgressionDefinition? settlementProgression = null,
            string? completionMilestoneProjectId = null)
        {
            if (!id.IsValid)
                throw new ArgumentException("Region definition requires a valid ID.", nameof(id));
            if (string.IsNullOrWhiteSpace(titleKey))
                throw new ArgumentException("Region definition requires a title key.", nameof(titleKey));
            if (contentVersion < 1)
                throw new ArgumentException("Content version must be positive.", nameof(contentVersion));

            Id = id;
            TitleKey = titleKey;
            ContentVersion = contentVersion;
            Projects = new List<ProjectDefinition>(projects).AsReadOnly();
            Landmarks = new List<LandmarkDefinition>(landmarks).AsReadOnly();
            Producers = new List<ProducerDefinition>(producers).AsReadOnly();
            Discoveries = discoveries != null
                ? new List<DiscoveryDefinition>(discoveries).AsReadOnly()
                : (IReadOnlyList<DiscoveryDefinition>)new List<DiscoveryDefinition>().AsReadOnly();
            Expeditions = expeditions != null
                ? new List<ExpeditionDefinition>(expeditions).AsReadOnly()
                : (IReadOnlyList<ExpeditionDefinition>)new List<ExpeditionDefinition>().AsReadOnly();
            EcologyProgression = ecologyProgression ?? RegionProgressionDefinition.Empty();
            SettlementProgression = settlementProgression ?? RegionProgressionDefinition.Empty();
            CompletionMilestoneProjectId = completionMilestoneProjectId;
        }

        public ProjectDefinition? FindProject(string projectId)
        {
            foreach (var project in Projects)
                if (project.Id.Value == projectId)
                    return project;
            return null;
        }

        public ProducerDefinition? FindProducer(string producerId)
        {
            foreach (var producer in Producers)
                if (producer.Id.Value == producerId)
                    return producer;
            return null;
        }

        public LandmarkDefinition? FindLandmark(string landmarkId)
        {
            foreach (var landmark in Landmarks)
                if (landmark.Id.Value == landmarkId)
                    return landmark;
            return null;
        }

        public DiscoveryDefinition? FindDiscovery(string discoveryId)
        {
            foreach (var discovery in Discoveries)
                if (discovery.Id.Value == discoveryId)
                    return discovery;
            return null;
        }

        public ExpeditionDefinition? FindExpedition(string expeditionId)
        {
            foreach (var expedition in Expeditions)
                if (expedition.Id.Value == expeditionId)
                    return expedition;
            return null;
        }
    }

    /// <summary>Runtime producer state owned by the region.</summary>
    /// <remarks>
    /// Schema v2: the v1 sub-unit carry field was promoted to the bounded pending-output
    /// store (<see cref="StoredMilliUnits"/>); migration m1-to-v2 maps v1 carries across.
    /// </remarks>
    public sealed class ProducerRuntimeState
    {
        public string ProducerId { get; set; } = string.Empty;

        public bool Unlocked { get; set; }

        /// <summary>
        /// Bounded pending output in milli-units (fractional remainders plus whole units
        /// parked while a downstream resource cap refuses delivery). Never exceeds the
        /// definition's CapacityUnits × 1000; surplus time beyond it produces nothing and
        /// creates no waste. Whole units auto-deliver into canonical balances — claiming
        /// is never required.
        /// </summary>
        public long StoredMilliUnits { get; set; }

        public long TotalProducedMilliUnits { get; set; }

        public DateTimeOffset LastTickUtc { get; set; }
    }

    /// <summary>
    /// Canonical per-player state of a region: project progress, landmark stages, producer
    /// runtimes, discovery/expedition progress and region-level progression axes. Visuals
    /// bind to this state; it is never derived from scenes.
    /// </summary>
    /// <remarks>
    /// Schema v2 additive fields (D-036): discoveries/expeditions entries appear only after
    /// their first canonical transition and stage counters default to 0/false, so payloads
    /// written before M4 decode with exactly "nothing unlocked yet" semantics — no schema
    /// bump or migration is required.
    /// </remarks>
    public sealed class RegionState
    {
        public string RegionId { get; set; } = string.Empty;

        public Dictionary<string, ProjectState> Projects { get; } = new Dictionary<string, ProjectState>();

        public Dictionary<string, RestorationStage> LandmarkStages { get; } = new Dictionary<string, RestorationStage>();

        public List<ProducerRuntimeState> Producers { get; } = new List<ProducerRuntimeState>();

        /// <summary>Unlocked discoveries only; absence means not-yet-discovered.</summary>
        public Dictionary<string, DiscoveryRuntimeState> Discoveries { get; } = new Dictionary<string, DiscoveryRuntimeState>();

        /// <summary>Available-or-completed expeditions only; absence means locked.</summary>
        public Dictionary<string, ExpeditionRuntimeState> Expeditions { get; } = new Dictionary<string, ExpeditionRuntimeState>();

        /// <summary>Highest reached ecology arc stage; 0 = baseline.</summary>
        public int EcologyStage { get; set; }

        /// <summary>Highest reached settlement arc stage; 0 = baseline.</summary>
        public int SettlementStage { get; set; }

        /// <summary>True once the closure milestone completed. Never resets (post-completion evergreen).</summary>
        public bool IsCompleted { get; set; }

        public DateTimeOffset? RegionCompletedAtUtc { get; set; }

        public ProjectState? FindProject(string projectId) =>
            Projects.TryGetValue(projectId, out var state) ? state : null;

        public ProducerRuntimeState? FindProducer(string producerId)
        {
            foreach (var producer in Producers)
                if (producer.ProducerId == producerId)
                    return producer;
            return null;
        }
    }
}
