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
    /// The numeric limits a producer's authored values must respect, DERIVED from the
    /// production tick's real arithmetic rather than chosen as round numbers (D-058).
    ///
    /// <c>OfflineAdvancer.TickProducers</c> computes earned output as:
    ///
    /// <code>
    /// (cappedTicks / TicksPerDay) * MilliUnitsPerDay
    ///     + (cappedTicks % TicksPerDay) * MilliUnitsPerDay / TicksPerDay
    /// </code>
    ///
    /// Two products must stay representable, and C# arithmetic here is UNCHECKED, so an
    /// overflow does not throw — it wraps, and a wrapped value can drive
    /// <c>StoredMilliUnits</c> NEGATIVE, which <c>GameStateValidator</c> then rejects on
    /// the next boot. The player's world becomes unloadable from a number content
    /// validation explicitly approved.
    ///
    /// <list type="bullet">
    /// <item>the <b>sub-day remainder</b> term: <c>cappedTicks % TicksPerDay</c> is
    /// strictly less than <see cref="TimeSpan.TicksPerDay"/>, so
    /// <c>rate ≤ long.MaxValue / (TicksPerDay − 1)</c>.</item>
    /// <item>the <b>whole-day</b> term: <c>cappedTicks / TicksPerDay</c> is at most
    /// <see cref="MaxInterval"/>.TotalDays, so
    /// <c>rate ≤ long.MaxValue / MaxInterval.TotalDays</c>.</item>
    /// </list>
    ///
    /// The sub-day term is by far the binding constraint, and
    /// <see cref="SubDayRemainderBinds"/> records which one actually binds so the
    /// validator's self-check can say so rather than assume it.
    /// </summary>
    public static class ProducerArithmetic
    {
        /// <summary>
        /// Suspicious elapsed intervals are capped so a wildly wrong clock cannot mint
        /// unbounded production in one tick. THIS IS THE SINGLE SOURCE OF TRUTH:
        /// <c>OfflineAdvancer.MaxProducerInterval</c> references it, so changing the cap
        /// cannot leave the validator's bound stale.
        /// </summary>
        public static readonly TimeSpan MaxInterval = TimeSpan.FromDays(3650L);

        /// <summary><c>cappedTicks % TicksPerDay</c> is strictly less than this.</summary>
        private static readonly long MaxSubDayRemainderTicks = TimeSpan.TicksPerDay - 1L;

        /// <summary>Bound implied by the sub-day remainder term of the tick formula.</summary>
        public static long SubDayTermBound => long.MaxValue / MaxSubDayRemainderTicks;

        /// <summary>Bound implied by the whole-day term of the tick formula.</summary>
        public static long WholeDayTermBound =>
            MaxInterval.Ticks / TimeSpan.TicksPerDay == 0L
                ? long.MaxValue
                : long.MaxValue / (MaxInterval.Ticks / TimeSpan.TicksPerDay);

        /// <summary>
        /// The largest authored rate the production tick can represent without wrapping.
        /// </summary>
        public static long MaxRepresentableMilliUnitsPerDay { get; } =
            Math.Min(SubDayTermBound, WholeDayTermBound);

        /// <summary>
        /// True when the sub-day remainder term is the binding constraint — the case this
        /// content is authored for, asserted rather than assumed so a future change to
        /// <see cref="MaxInterval"/> cannot silently flip which term matters.
        /// </summary>
        public static bool SubDayRemainderBinds => SubDayTermBound < WholeDayTermBound;

        /// <summary>Re-derives the bound from first principles, for the validator self-check.</summary>
        public static long DeriveMaxRepresentableMilliUnitsPerDay() =>
            Math.Min(
                long.MaxValue / (TimeSpan.TicksPerDay - 1L),
                long.MaxValue / (MaxInterval.Ticks / TimeSpan.TicksPerDay));
    }

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
    /// <summary>
    /// An authored content identifier that has been retired, and why (D-059).
    ///
    /// A tombstone is the explicit, reviewable record that a live definition was removed or
    /// renamed. Existing saves that still reference the old identifier resolve to the
    /// tombstone rather than failing closed, which is the difference between a content
    /// change and the destruction of a mature profile.
    ///
    /// The tombstone grants nothing. It exists purely so a reference stays RESOLVABLE.
    /// </summary>
    public sealed class ContentTombstone
    {
        /// <summary>The retired identifier, exactly as older saves recorded it.</summary>
        public string Id { get; }

        /// <summary>Why it was retired. Required, non-empty, and reviewable.</summary>
        public string Reason { get; }

        /// <summary>Content version at which the identifier was retired, when known.</summary>
        public int? RetiredInContentVersion { get; }

        public ContentTombstone(string id, string reason, int? retiredInContentVersion = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A content tombstone requires a stable identifier.", nameof(id));
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException(
                    "Content tombstone '" + id + "' requires a non-empty reason.", nameof(reason));

            Id = id;
            Reason = reason;
            RetiredInContentVersion = retiredInContentVersion;
        }

        public override string ToString() => "tombstone:" + Id + " (" + Reason + ")";
    }

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

        /// <summary>
        /// Authored post-completion programs (D-061): the repeatable use for progress earned
        /// after the closure milestone. Deliberately a separate collection from
        /// <see cref="Projects"/> — a program is never restoration work, is never queued,
        /// and must not make the region's roster look unfinished.
        ///
        /// Absent (empty) is valid and authored content decides it: a region with no program
        /// leaves post-completion progress banked and says so in its surface contract.
        /// </summary>
        public IReadOnlyList<PostCompletionProgramDefinition> PostCompletionPrograms { get; }

        /// <summary>Region-level ecological recovery arc.</summary>
        public RegionProgressionDefinition EcologyProgression { get; }

        /// <summary>Region-level settlement/hub arc.</summary>
        public RegionProgressionDefinition SettlementProgression { get; }

        /// <summary>The project whose completion is the explicit region closure milestone; null when the region has no closure yet.</summary>
        public string? CompletionMilestoneProjectId { get; }

        /// <summary>
        /// Authored per-resource storage caps, in whole units (D-054). Every resource a
        /// producer in this region can emit MUST appear here with a positive, representable
        /// cap; a missing cap is a content-validation failure rather than an unbounded
        /// runtime default. Read-only, and never inferred: an absent entry means "no cap
        /// authored", which validation rejects.
        /// </summary>
        public IReadOnlyDictionary<Economy.ResourceType, long> ResourceCaps { get; }

        /// <summary>Authored cap for a resource, or null when the region authors none.</summary>
        public long? ResourceCap(Economy.ResourceType type) =>
            ResourceCaps.TryGetValue(type, out var cap) ? cap : (long?)null;

        /// <summary>
        /// Identifiers that have been RETIRED from authored content, with the reason they
        /// were retired (D-059). A save that still references a retired identifier resolves
        /// to its tombstone instead of failing closed, so a content rename or removal does
        /// not destroy a mature profile.
        ///
        /// A tombstone grants NOTHING: a retired project cannot complete, a retired producer
        /// cannot unlock, and a retired discovery or expedition cannot award. It only stops
        /// the reference from becoming unresolvable.
        ///
        /// Region 1's set is deliberately EMPTY: the mechanism and its guards are introduced
        /// here, and nothing is retired.
        /// </summary>
        public IReadOnlyDictionary<string, ContentTombstone> Tombstones { get; }

        /// <summary>Declared post-completion identifier set, collected once at construction.</summary>
        private readonly HashSet<string> _postCompletionProgramIds;

        /// <summary>Identifier set of the declared post-completion programs.</summary>
        public IReadOnlyCollection<string> PostCompletionProgramIds => _postCompletionProgramIds;

        /// <summary>Authored post-completion program for an identifier, or null when none is declared.</summary>
        public PostCompletionProgramDefinition? FindPostCompletionProgram(string programId) =>
            string.IsNullOrEmpty(programId) ? null : FindPostCompletionProgramUnchecked(programId);

        private PostCompletionProgramDefinition? FindPostCompletionProgramUnchecked(string programId)
        {
            foreach (var program in PostCompletionPrograms)
                if (string.Equals(program.Id, programId, StringComparison.Ordinal))
                    return program;
            return null;
        }

        /// <summary>True when the identifier is a declared post-completion program.</summary>
        public bool IsPostCompletionProgram(string programId) =>
            !string.IsNullOrEmpty(programId) && _postCompletionProgramIds.Contains(programId);

        /// <summary>The tombstone for an identifier, or null when it is not retired.</summary>
        public ContentTombstone? FindTombstone(string id) =>
            id != null && Tombstones.TryGetValue(id, out var tombstone) ? tombstone : null;

        /// <summary>True when the identifier is retired rather than live.</summary>
        public bool IsTombstoned(string id) => FindTombstone(id) != null;

        public RegionDefinition(RegionId id, string titleKey,
            IEnumerable<ProjectDefinition> projects,
            IEnumerable<LandmarkDefinition> landmarks,
            IEnumerable<ProducerDefinition> producers,
            int contentVersion = 1,
            IEnumerable<DiscoveryDefinition>? discoveries = null,
            IEnumerable<ExpeditionDefinition>? expeditions = null,
            RegionProgressionDefinition? ecologyProgression = null,
            RegionProgressionDefinition? settlementProgression = null,
            string? completionMilestoneProjectId = null,
            IEnumerable<KeyValuePair<Economy.ResourceType, long>>? resourceCaps = null,
            IEnumerable<ContentTombstone>? tombstones = null,
            IEnumerable<PostCompletionProgramDefinition>? postCompletionPrograms = null)
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

            var caps = new Dictionary<Economy.ResourceType, long>();
            if (resourceCaps != null)
            {
                foreach (var pair in resourceCaps)
                {
                    if (pair.Value <= 0L)
                        throw new ArgumentException(
                            "Resource cap for " + pair.Key + " must be positive.", nameof(resourceCaps));
                    caps[pair.Key] = pair.Value;
                }
            }
            ResourceCaps = new Dictionary<Economy.ResourceType, long>(caps);

            var retired = new Dictionary<string, ContentTombstone>(StringComparer.Ordinal);
            if (tombstones != null)
            {
                foreach (var tombstone in tombstones)
                {
                    if (tombstone == null)
                        continue;
                    if (string.IsNullOrWhiteSpace(tombstone.Id))
                        throw new ArgumentException("A content tombstone requires a stable identifier.", nameof(tombstones));
                    // A tombstone with no reason is an unattributed silent removal, which is
                    // exactly the class of change this mechanism exists to make reviewable.
                    if (string.IsNullOrWhiteSpace(tombstone.Reason))
                        throw new ArgumentException(
                            "Content tombstone '" + tombstone.Id + "' requires a non-empty reason.", nameof(tombstones));
                    retired[tombstone.Id] = tombstone;
                }
            }
            Tombstones = new Dictionary<string, ContentTombstone>(retired);

            var programs = new List<PostCompletionProgramDefinition>();
            if (postCompletionPrograms != null)
            {
                foreach (var program in postCompletionPrograms)
                {
                    if (program == null)
                        throw new ArgumentException("A post-completion program entry is null.", nameof(postCompletionPrograms));
                    programs.Add(program);
                }
            }
            PostCompletionPrograms = programs.AsReadOnly();
            _postCompletionProgramIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var program in PostCompletionPrograms)
                _postCompletionProgramIds.Add(program.Id);
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
    /// bump or migration is required. The same rule covers the D-061 post-completion counters.
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

        /// <summary>
        /// Vitality already applied to the in-progress cycle of the region's authored
        /// post-completion program (D-061). Always below that program's cycle cost: reaching
        /// the cost completes a cycle and leaves only the remainder here.
        ///
        /// Additive on schema v2 exactly like the fields above: an older payload decodes with
        /// 0, which means "nothing invested yet", so no migration is required.
        /// </summary>
        public long PostCompletionInvestedVitality { get; set; }

        /// <summary>Completed post-completion program cycles. Additive on schema v2 (absent = 0).</summary>
        public long PostCompletionCyclesCompleted { get; set; }

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
