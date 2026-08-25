using System;
using System.Collections.Generic;
using WalkGame.Domain.Economy;
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

    /// <summary>
    /// Immutable content for one region. Definitions carry authoring data only;
    /// player-specific values live in <see cref="RegionState"/>.
    /// </summary>
    public sealed class RegionDefinition
    {
        public RegionId Id { get; }
        public string TitleKey { get; }
        public IReadOnlyList<ProjectDefinition> Projects { get; }
        public IReadOnlyList<LandmarkDefinition> Landmarks { get; }
        public IReadOnlyList<ProducerDefinition> Producers { get; }

        public RegionDefinition(RegionId id, string titleKey,
            IEnumerable<ProjectDefinition> projects,
            IEnumerable<LandmarkDefinition> landmarks,
            IEnumerable<ProducerDefinition> producers)
        {
            if (!id.IsValid)
                throw new ArgumentException("Region definition requires a valid ID.", nameof(id));
            if (string.IsNullOrWhiteSpace(titleKey))
                throw new ArgumentException("Region definition requires a title key.", nameof(titleKey));

            Id = id;
            TitleKey = titleKey;
            Projects = new List<ProjectDefinition>(projects).AsReadOnly();
            Landmarks = new List<LandmarkDefinition>(landmarks).AsReadOnly();
            Producers = new List<ProducerDefinition>(producers).AsReadOnly();
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
    /// Canonical per-player state of a region: project progress, landmark stages and
    /// producer runtimes. Visuals bind to this state; it is never derived from scenes.
    /// </summary>
    public sealed class RegionState
    {
        public string RegionId { get; set; } = string.Empty;

        public Dictionary<string, ProjectState> Projects { get; } = new Dictionary<string, ProjectState>();

        public Dictionary<string, RestorationStage> LandmarkStages { get; } = new Dictionary<string, RestorationStage>();

        public List<ProducerRuntimeState> Producers { get; } = new List<ProducerRuntimeState>();

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
