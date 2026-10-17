using System;
using System.Collections.Generic;
using WalkGame.Domain.Discoveries;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Expeditions;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using DiscoveryId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.DiscoveryIdKind>;
using ExpeditionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ExpeditionIdKind>;
using LandmarkId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.LandmarkIdKind>;
using ProducerId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProducerIdKind>;
using ProjectId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProjectIdKind>;
using RegionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RegionIdKind>;

namespace WalkGame.Application.Content
{
    /// <summary>
    /// Region 1 — Millbrook Valley: the authored M4 content contract.
    ///
    /// Content version 3 (D-062): the M4-authored costs were rebalanced to close the measured
    /// weekday-only shortfall, and closure is now authored as a criterion rather than inferred
    /// from the roster's total cost. The five original M1/M3 development-seed definitions are
    /// still preserved VERBATIM (their costs, prerequisites, the landmark stage triggers and
    /// the workshop salvage producer) — the rebalance found its lever entirely in the
    /// M4-authored projects. Content version 2 remains readable: a save recorded against it
    /// classifies as CompatibleOlder (D-059) and keeps its whole history.
    ///
    /// Content version 4 (D-071): the three longest projects (workshop, wetland drainage,
    /// canopy walkway) author VISIBLE BUILD STAGES so the valley visibly advances while they
    /// are funded. Costs, prerequisites and ids are unchanged (the seeds stay verbatim); the
    /// stages are derived from invested Vitality, so v3 saves need no content migration.
    ///
    /// Content version 5 (D-076): the restoration storyline, nine chapters that unlock from
    /// restoration milestones only and are derived from completion state (no save change).
    ///
    /// Six interdependent restoration chains, six landmarks, three bounded producers,
    /// thirteen provenance-bearing discoveries, three deterministic expedition routes,
    /// region-level ecology/settlement arcs, the Complete Valley Survey closure criterion,
    /// the authored post-completion program that gives continued activity a use (D-061), and
    /// the six named restoration chains the criterion is measured against (D-062).
    /// Presentation binds to canonical state through the keys below plus the documented
    /// bindings in docs/WORLD_AND_CONTENT.md §12; no Unity assets are defined here.
    /// </summary>
    public static class Region1Catalog
    {
        public const int AuthoredContentVersion = 5;

        /// <summary>
        /// Authored per-resource storage caps for Region 1, in whole units (D-054).
        ///
        /// These caps BIND. Measured over a 730-day `longhaul` run with this content, the
        /// balances settle at exactly Materials 400 and Knowledge 300 — i.e. both resources
        /// reach their cap and stay there, because production outruns consumption. That is
        /// the point: before this change `GetCap` returned <c>long.MaxValue</c> for every
        /// resource, so both currencies grew without bound and the whole
        /// "park the surplus and flush it later" mechanism in `OfflineAdvancer` was
        /// unreachable dead code. A bound that a real profile actually reaches is a real
        /// bound; the surplus is not lost, it waits in the producer's pending store and
        /// flushes automatically when the player spends some.
        ///
        /// The values are chosen round and well below the region total Vitality cost, so
        /// the currencies read as scarce maintenance inputs rather than as a second
        /// progress bar.
        ///
        /// Vitality is deliberately ABSENT. Capping it would be a data-loss bug, not a
        /// balance: <c>RewardLedger.Apply</c> ignores the value <c>balances.Add</c> returns
        /// and records the FULL transaction amount in the ledger, and Vitality has no
        /// producer store to park refused units in. A Vitality cap would therefore
        /// silently discard real walking credit. The cap requirement is scoped to resources
        /// a producer can emit, and no producer emits Vitality. See D-054.
        /// </summary>
        private static readonly KeyValuePair<ResourceType, long>[] AuthoredResourceCaps =
        {
            new KeyValuePair<ResourceType, long>(ResourceType.Materials, 400L),
            new KeyValuePair<ResourceType, long>(ResourceType.Knowledge, 300L),
        };

        private static KeyValuePair<ResourceType, long> Cost(ResourceType resource, long units) =>
            new KeyValuePair<ResourceType, long>(resource, units);

        /// <summary>
        /// The authored catalog is a deterministic immutable content graph (every
        /// definition is constructor-frozen), so building it once per process removes
        /// pure duplicate work from session construction — sessions are created on every
        /// boot and once per app-closed day in the simulation harnesses.
        /// </summary>
        private static readonly RegionDefinition CachedInstance = Build();

        // ---- Chain 1: Trail access (access → stabilization → restored route) ----
        private const string ClearTrailHead = "proj.clear-trailhead";
        private const string RebuildTrailBridges = "proj.rebuild-trail-bridges";
        private const string OpenLookout = "proj.open-lookout";

        // ---- Chain 2: Water system (intake → reservoir → distribution) ----
        private const string RiverIntake = "proj.river-intake";
        private const string ClearReservoir = "proj.clear-reservoir";
        private const string LayWaterLines = "proj.lay-water-lines";

        // ---- Chain 3: Settlement community (workshop → market → power) ----
        private const string BuildWorkshop = "proj.build-workshop";
        private const string RestoreMarketHall = "proj.restore-market-hall";
        private const string WireSettlementPower = "proj.wire-settlement-power";

        // ---- Chain 4: Wetland recovery (drainage → replanting → habitat) ----
        private const string WetlandDrainage = "proj.wetland-drainage";
        private const string ReplantNativeSedges = "proj.replant-native-sedges";
        private const string BuildNestingIslets = "proj.build-nesting-islets";
        private const string WetlandBoardwalk = "proj.wetland-boardwalk";

        // ---- Chain 5: Woodland (clearance → understory → canopy link) ----
        private const string ClearFallenTimber = "proj.clear-fallen-timber";
        private const string PlantWoodlandUnderstory = "proj.plant-woodland-understory";
        private const string CanopyWalkway = "proj.canopy-walkway";

        // ---- Chain 6: Research (observatory → calibration → valley survey/closure) ----
        private const string RefitObservatoryDome = "proj.refit-observatory-dome";
        private const string CalibrateSurveyRig = "proj.calibrate-survey-rig";
        private const string CompleteValleySurvey = "proj.complete-valley-survey";

        // ---- Post-completion content (D-061) ----
        private const string EvergreenTending = "care.evergreen-tending";

        // ---- Restoration chains (D-062) ----
        private const string TrailChain = "chain.trail-access";
        private const string WaterChain = "chain.water-system";
        private const string SettlementChain = "chain.settlement-community";
        private const string WetlandChain = "chain.wetland-recovery";
        private const string ForestChain = "chain.woodland";
        private const string ResearchChain = "chain.research";

        public static RegionDefinition Create() => CachedInstance;

        /// <summary>
        /// Builds the shipped Region 1 content with an ADDITIONAL tombstone set layered on.
        /// The parameterless overload above returns a shared cached instance and is what
        /// production uses; this one exists so tests can prove the tombstone mechanism
        /// against the real content graph without mutating the shipped catalog.
        ///
        /// Additive: passing no tombstones is exactly the shipped content.
        /// </summary>
        public static RegionDefinition Create(IEnumerable<ContentTombstone>? tombstones)
        {
            if (tombstones == null)
                return CachedInstance;

            var authored = CachedInstance;
            var all = new List<ContentTombstone>(authored.Tombstones.Values);
            foreach (var tombstone in tombstones)
                all.Add(tombstone);

            return new RegionDefinition(
                authored.Id, authored.TitleKey,
                authored.Projects, authored.Landmarks, authored.Producers,
                contentVersion: authored.ContentVersion,
                discoveries: authored.Discoveries,
                expeditions: authored.Expeditions,
                ecologyProgression: authored.EcologyProgression,
                settlementProgression: authored.SettlementProgression,
                completionMilestoneProjectId: authored.CompletionMilestoneProjectId,
                resourceCaps: authored.ResourceCaps,
                tombstones: all,
                postCompletionPrograms: authored.PostCompletionPrograms,
                chains: authored.Chains,
                closureCriterion: authored.ClosureCriterion,
                chapters: authored.Chapters);
        }

        private static RegionDefinition Build()
        {
            var projects = new List<ProjectDefinition>
            {
                // Trail access
                new ProjectDefinition(new ProjectId(ClearTrailHead), "Clear the old trailhead", 300L,
                    descriptionKey: "Cut back overgrowth and clear debris so the valley can be entered safely."),
                new ProjectDefinition(new ProjectId(RebuildTrailBridges), "Rebuild the trail bridges", 300L,
                    prerequisites: new[] { new ProjectId(ClearTrailHead) },
                    descriptionKey: "Replace collapsed crossings so the high routes are passable again."),
                new ProjectDefinition(new ProjectId(OpenLookout), "Open the ridge lookout", 400L,
                    prerequisites: new[] { new ProjectId(RebuildTrailBridges) },
                    descriptionKey: "Clear and secure the fire lookout above the treeline."),

                // Water system
                new ProjectDefinition(new ProjectId(RiverIntake), "Restore the river intake", 800L,
                    prerequisites: new[] { new ProjectId(ClearTrailHead) },
                    descriptionKey: "Free the intake works and get clean water moving again."),
                new ProjectDefinition(new ProjectId(ClearReservoir), "Clear the old reservoir", 400L,
                    prerequisites: new[] { new ProjectId(RiverIntake) },
                    descriptionKey: "Dredge silt and repair the reservoir basin."),
                new ProjectDefinition(new ProjectId(LayWaterLines), "Lay water lines to the hub", 550L,
                    prerequisites: new[] { new ProjectId(ClearReservoir) },
                    descriptionKey: "Run piping from the reservoir down to the settlement hub."),

                // Settlement community
                new ProjectDefinition(new ProjectId(BuildWorkshop), "Rebuild the settlement workshop", 1500L,
                    prerequisites: new[] { new ProjectId(RiverIntake) },
                    descriptionKey: "Roof, bench and tool the workshop that the valley builds with.",
                    visibleBuildStages: 2),
                new ProjectDefinition(new ProjectId(RestoreMarketHall), "Restore the market hall", 650L,
                    prerequisites: new[] { new ProjectId(BuildWorkshop) },
                    descriptionKey: "Raise the market hall where the community trades and gathers.",
                    // D-054: first secondary-resource requirement. Materials come from the
                    // workshop-salvage crew, which this project itself unlocks, so the
                    // requirement is always self-supplying from the point the player can
                    // reach it. It teaches the mechanic without ever gating the chain.
                    // Sized empirically: 40 is the largest value that leaves ALL SEVEN
                    // committed pacing profiles byte-identical (60 pushed the settlement
                    // chain out by up to 6 days on the pathological profile). See D-054.
                    resourceCosts: new[] { Cost(ResourceType.Materials, 40L) }),
                new ProjectDefinition(new ProjectId(WireSettlementPower), "Wire settlement power", 750L,
                    prerequisites: new[] { new ProjectId(RestoreMarketHall), new ProjectId(LayWaterLines) },
                    descriptionKey: "Run the mill race generator and light the streets warm."),

                // Wetland recovery
                new ProjectDefinition(new ProjectId(WetlandDrainage), "Drain and replant the east wetland", 2200L,
                    prerequisites: new[] { new ProjectId(RiverIntake) },
                    descriptionKey: "Undo the failed drainage scheme and let the marsh breathe.",
                    visibleBuildStages: 3),
                new ProjectDefinition(new ProjectId(ReplantNativeSedges), "Replant native sedges", 300L,
                    prerequisites: new[] { new ProjectId(WetlandDrainage) },
                    descriptionKey: "Reinforce the banks with native sedge beds."),
                new ProjectDefinition(new ProjectId(BuildNestingIslets), "Build crane nesting islets", 400L,
                    prerequisites: new[] { new ProjectId(ReplantNativeSedges) },
                    descriptionKey: "Anchor safe nesting islands for returning cranes."),
                new ProjectDefinition(new ProjectId(WetlandBoardwalk), "Raise the wetland boardwalk", 550L,
                    prerequisites: new[] { new ProjectId(WetlandDrainage), new ProjectId(RebuildTrailBridges) },
                    descriptionKey: "Span the reeds with a quiet boardwalk route."),

                // Woodland
                new ProjectDefinition(new ProjectId(ClearFallenTimber), "Clear the fallen timber", 200L,
                    prerequisites: new[] { new ProjectId(ClearTrailHead) },
                    descriptionKey: "Open the storm-felled grove paths and make the wood safe."),
                new ProjectDefinition(new ProjectId(PlantWoodlandUnderstory), "Plant the woodland understory", 350L,
                    prerequisites: new[] { new ProjectId(ClearFallenTimber) },
                    descriptionKey: "Reintroduce ferns, shrubs and young hardwoods beneath the canopy."),

                // Canopy link (original seed definition, preserved verbatim incl. cost/prereqs)
                new ProjectDefinition(new ProjectId(CanopyWalkway), "Raise the canopy walkway", 4000L,
                    prerequisites: new[] { new ProjectId(BuildWorkshop), new ProjectId(WetlandDrainage) },
                    descriptionKey: "Bridge the grove crowns with the valley's signature walkway.",
                    visibleBuildStages: 5),

                // Research
                new ProjectDefinition(new ProjectId(RefitObservatoryDome), "Refit the observatory dome", 550L,
                    prerequisites: new[] { new ProjectId(OpenLookout) },
                    descriptionKey: "Regear the dome and mount the survey telescope."),
                new ProjectDefinition(new ProjectId(CalibrateSurveyRig), "Calibrate the survey rig", 500L,
                    prerequisites: new[] { new ProjectId(RefitObservatoryDome), new ProjectId(BuildWorkshop) },
                    descriptionKey: "Align instruments and benches for a full valley survey.",
                    // D-054: second secondary-resource requirement, on a DIFFERENT chain
                    // and a DIFFERENT currency from the market hall. The observatory archive
                    // (the region's only Knowledge producer) is unlocked by this project's
                    // own prerequisite, so the requirement is self-supplying too — it makes
                    // Knowledge a decision without ever making the region uncompletable.
                    // Sized empirically alongside the Materials requirement: 20 is the
                    // largest value that leaves all seven committed pacing profiles
                    // byte-identical (40 pushed the research chain out by up to 9 days).
                    resourceCosts: new[] { Cost(ResourceType.Knowledge, 20L) }),
                new ProjectDefinition(new ProjectId(CompleteValleySurvey), "Complete the valley survey", 650L,
                    prerequisites: new[] { new ProjectId(CalibrateSurveyRig), new ProjectId(WetlandBoardwalk), new ProjectId(PlantWoodlandUnderstory) },
                    descriptionKey: "Map every recovered mile: the act that closes Region 1's restoration."),
            };

            var landmarks = new List<LandmarkDefinition>
            {
                new LandmarkDefinition(new LandmarkId("lm.trailhead"), "Old Trailhead",
                    stages: new[]
                    {
                        new LandmarkStageDefinition(RestorationStage.Stabilized, ClearTrailHead),
                        new LandmarkStageDefinition(RestorationStage.Functional, BuildWorkshop),
                        new LandmarkStageDefinition(RestorationStage.Restored, OpenLookout),
                    }),
                new LandmarkDefinition(new LandmarkId("lm.river-intake"), "River Intake",
                    stages: new[]
                    {
                        new LandmarkStageDefinition(RestorationStage.Stabilized, RiverIntake),
                        new LandmarkStageDefinition(RestorationStage.Functional, WetlandDrainage),
                        new LandmarkStageDefinition(RestorationStage.Restored, CanopyWalkway),
                    }),
                new LandmarkDefinition(new LandmarkId("lm.canopy"), "Canopy Grove",
                    stages: new[]
                    {
                        new LandmarkStageDefinition(RestorationStage.Stabilized, WetlandDrainage),
                        new LandmarkStageDefinition(RestorationStage.Functional, PlantWoodlandUnderstory),
                        new LandmarkStageDefinition(RestorationStage.Flourishing, CanopyWalkway),
                    }),
                new LandmarkDefinition(new LandmarkId("lm.settlement"), "Millbrook Settlement",
                    stages: new[]
                    {
                        new LandmarkStageDefinition(RestorationStage.Stabilized, BuildWorkshop),
                        new LandmarkStageDefinition(RestorationStage.Functional, RestoreMarketHall),
                        new LandmarkStageDefinition(RestorationStage.Restored, WireSettlementPower),
                    }),
                new LandmarkDefinition(new LandmarkId("lm.wetland"), "East Wetland",
                    stages: new[]
                    {
                        new LandmarkStageDefinition(RestorationStage.Stabilized, WetlandDrainage),
                        new LandmarkStageDefinition(RestorationStage.Functional, ReplantNativeSedges),
                        new LandmarkStageDefinition(RestorationStage.Flourishing, BuildNestingIslets),
                    }),
                new LandmarkDefinition(new LandmarkId("lm.observatory"), "Ridge Observatory",
                    stages: new[]
                    {
                        new LandmarkStageDefinition(RestorationStage.Functional, RefitObservatoryDome),
                        new LandmarkStageDefinition(RestorationStage.Restored, CompleteValleySurvey),
                    }),
            };

            var producers = new List<ProducerDefinition>
            {
                new ProducerDefinition(new ProducerId("prd.workshop-salvage"), "Workshop Salvage Crew",
                    output: ResourceType.Materials,
                    milliUnitsPerDay: 2500L,
                    capacityUnits: 500L,
                    unlockedByProjectId: BuildWorkshop),
                new ProducerDefinition(new ProducerId("prd.nursery-greenhouse"), "Nursery Greenhouse",
                    output: ResourceType.Materials,
                    milliUnitsPerDay: 1500L,
                    capacityUnits: 300L,
                    unlockedByProjectId: ReplantNativeSedges),
                new ProducerDefinition(new ProducerId("prd.observatory-archive"), "Observatory Archive",
                    output: ResourceType.Knowledge,
                    milliUnitsPerDay: 1200L,
                    capacityUnits: 250L,
                    unlockedByProjectId: RefitObservatoryDome),
            };

            var discoveries = new List<DiscoveryDefinition>
            {
                new DiscoveryDefinition(new DiscoveryId("disc.old-millstone"), "artifact",
                    "The Old Millstone", "disc.old-millstone.body", "disc.old-millstone.provenance",
                    ClearTrailHead, locationKey: "loc.trailhead.millstone"),
                new DiscoveryDefinition(new DiscoveryId("disc.intake-plate-stamp"), "infrastructure-history",
                    "Intake Plate Stamp", "disc.intake-plate-stamp.body", "disc.intake-plate-stamp.provenance",
                    RiverIntake, locationKey: "loc.intake.valve-house"),
                new DiscoveryDefinition(new DiscoveryId("disc.workshop-ledger"), "settlement-story",
                    "The Workshop Ledger", "disc.workshop-ledger.body", "disc.workshop-ledger.provenance",
                    BuildWorkshop, locationKey: "loc.workshop.backroom"),
                new DiscoveryDefinition(new DiscoveryId("disc.reservoir-time-capsule"), "artifact",
                    "Reservoir Time Capsule", "disc.reservoir-time-capsule.body", "disc.reservoir-time-capsule.provenance",
                    ClearReservoir, locationKey: "loc.reservoir.basin"),
                new DiscoveryDefinition(new DiscoveryId("disc.lookout-fire-lens"), "artifact",
                    "The Lookout Fire Lens", "disc.lookout-fire-lens.body", "disc.lookout-fire-lens.provenance",
                    OpenLookout, locationKey: "loc.lookout.lamp-room"),
                new DiscoveryDefinition(new DiscoveryId("disc.grid-archive-map"), "archive-fragment",
                    "Grid Archive Map", "disc.grid-archive-map.body", "disc.grid-archive-map.provenance",
                    WireSettlementPower, locationKey: "loc.settlement.substation"),
                new DiscoveryDefinition(new DiscoveryId("disc.market-mural"), "settlement-story",
                    "The Market Mural", "disc.market-mural.body", "disc.market-mural.provenance",
                    RestoreMarketHall, locationKey: "loc.market.hall-wall"),
                new DiscoveryDefinition(new DiscoveryId("disc.sedge-first-flush"), "flora",
                    "First Sedge Flush", "disc.sedge-first-flush.body", "disc.sedge-first-flush.provenance",
                    ReplantNativeSedges, locationKey: "loc.wetland.bank"),
                new DiscoveryDefinition(new DiscoveryId("disc.crane-return"), "wildlife",
                    "The Cranes Return", "disc.crane-return.body", "disc.crane-return.provenance",
                    BuildNestingIslets, locationKey: "loc.wetland.islets"),
                new DiscoveryDefinition(new DiscoveryId("disc.heron-roost-boards"), "wildlife",
                    "Heron Roost Boards", "disc.heron-roost-boards.body", "disc.heron-roost-boards.provenance",
                    WetlandBoardwalk, locationKey: "loc.boardwalk.roost"),
                new DiscoveryDefinition(new DiscoveryId("disc.understory-orchids"), "flora",
                    "Understory Orchids", "disc.understory-orchids.body", "disc.understory-orchids.provenance",
                    PlantWoodlandUnderstory, locationKey: "loc.grove.floor"),
                new DiscoveryDefinition(new DiscoveryId("disc.dome-star-chart"), "science-record",
                    "Dome Star Chart", "disc.dome-star-chart.body", "disc.dome-star-chart.provenance",
                    RefitObservatoryDome, locationKey: "loc.observatory.dome"),
                new DiscoveryDefinition(new DiscoveryId("disc.survey-baseline-stone"), "science-record",
                    "The Survey Baseline Stone", "disc.survey-baseline-stone.body", "disc.survey-baseline-stone.provenance",
                    CompleteValleySurvey, locationKey: "loc.valley.baseline"),
            };

            var expeditions = new List<ExpeditionDefinition>
            {
                new ExpeditionDefinition(
                    new ExpeditionId("exp.source-to-sound"), "Source-to-Sound Route",
                    "exp.source-to-sound.description",
                    requiredProjectIds: new[] { RebuildTrailBridges },
                    requiredStages: new[] { new ExpeditionStageRequirement("lm.trailhead", RestorationStage.Restored) },
                    reward: new ExpeditionReward(ResourceType.Materials, 40L)),
                new ExpeditionDefinition(
                    new ExpeditionId("exp.river-run"), "The River Run",
                    "exp.river-run.description",
                    requiredProjectIds: new[] { LayWaterLines },
                    requiredStages: new[] { new ExpeditionStageRequirement("lm.river-intake", RestorationStage.Restored) },
                    reward: new ExpeditionReward(ResourceType.Knowledge, 25L)),
                new ExpeditionDefinition(
                    new ExpeditionId("exp.valley-transect"), "Valley Transect",
                    "exp.valley-transect.description",
                    requiredProjectIds: new[] { CalibrateSurveyRig },
                    requiredStages: new[]
                    {
                        new ExpeditionStageRequirement("lm.canopy", RestorationStage.Flourishing),
                        new ExpeditionStageRequirement("lm.wetland", RestorationStage.Flourishing),
                    },
                    reward: new ExpeditionReward(ResourceType.Vitality, 50L)),
            };

            var ecologyArc = new RegionProgressionDefinition(new[]
            {
                new ProgressionStageDefinition(1, ClearFallenTimber),
                new ProgressionStageDefinition(2, ClearReservoir),
                new ProgressionStageDefinition(3, ReplantNativeSedges),
                new ProgressionStageDefinition(4, BuildNestingIslets),
            });

            var settlementArc = new RegionProgressionDefinition(new[]
            {
                new ProgressionStageDefinition(1, BuildWorkshop),
                new ProgressionStageDefinition(2, LayWaterLines),
                new ProgressionStageDefinition(3, RestoreMarketHall),
                new ProgressionStageDefinition(4, WireSettlementPower),
            });

            var postCompletionPrograms = new List<PostCompletionProgramDefinition>
            {
                // Evergreen Tending (D-061): the authored use for activity earned AFTER the
                // valley's restoration is complete. One repeatable cycle, no prerequisites,
                // nothing gated, and it cannot be mistaken for restoration work because it is
                // not a project: the roster still reads 19 of 19 complete.
                //
                // Cost per cycle is 1,000 Vitality — about 5% of the region's whole authored
                // cost — sized so a moderate walker (≈100 Vitality/day) banks a cycle roughly
                // every ten days and a low walker still sees progress within a season. It is
                // deliberately NOT tuned to make post-completion activity feel productive
                // quickly: the ending is a conclusion, not a second game.
                new PostCompletionProgramDefinition(
                    EvergreenTending,
                    "Evergreen Tending",
                    vitalityCostPerCycle: 1000L,
                    enabledByMilestoneProjectId: CompleteValleySurvey,
                    descriptionKey: "Keep the restored valley flourishing: every completed tending cycle is one more season the cranes, the water and the woodlane hold."),
            };

            // ---- Restoration chains and the closure criterion (D-062) ----
            var chains = new List<RestorationChainDefinition>
            {
                new RestorationChainDefinition(TrailChain, "Trail Access",
                    new[] { ClearTrailHead, RebuildTrailBridges, OpenLookout }),
                new RestorationChainDefinition(WaterChain, "Water System",
                    new[] { RiverIntake, ClearReservoir, LayWaterLines }),
                new RestorationChainDefinition(SettlementChain, "Settlement Community",
                    new[] { BuildWorkshop, RestoreMarketHall, WireSettlementPower }),
                new RestorationChainDefinition(WetlandChain, "Wetland Recovery",
                    new[] { WetlandDrainage, ReplantNativeSedges, BuildNestingIslets, WetlandBoardwalk }),
                new RestorationChainDefinition(ForestChain, "Woodland",
                    new[] { ClearFallenTimber, PlantWoodlandUnderstory, CanopyWalkway }),
                new RestorationChainDefinition(ResearchChain, "Research",
                    new[] { RefitObservatoryDome, CalibrateSurveyRig, CompleteValleySurvey }),
            };

            // Closure is authored as a criterion, not inferred from the roster's total cost.
            //
            // The criterion requires the whole valley: the survey milestone is the primary
            // route and the authored minimum is all six chains. The survey's prerequisite
            // closure touches every chain but does NOT contain every project (the canopy
            // walkway is no prerequisite of it), so the chain minimum is enforced on the
            // milestone route too (D-159), not assumed from the prerequisite graph. A WEAKER alternative (five of six, or four of six) was
            // authored, measured, and rejected: with the five-seed definitions frozen and the
            // conversion rate fixed, a weekday-only player can fund the whole rebalanced
            // roster, and a five-chain route closes the region BEFORE the survey for players
            // who can afford it — the ending the contract is written about, skipped.
            //
            // Measured at the time of authoring (see docs/DECISIONS.md D-062): the
            // weekday-only profile completes the sixth chain the same week the fifth one lands,
            // so the weaker route buys nothing and costs the survey.
            var closureCriterion = new ClosureCriterionDefinition(
                CompleteValleySurvey,
                minimumChainsComplete: chains.Count,
                "Complete the valley survey",
                descriptionKey: "Map every recovered mile: the act that closes Region 1's restoration.");

            return new RegionDefinition(
                new RegionId("region.millbrook-valley"),
                "Millbrook Valley",
                projects,
                landmarks,
                producers,
                contentVersion: AuthoredContentVersion,
                discoveries: discoveries,
                expeditions: expeditions,
                ecologyProgression: ecologyArc,
                settlementProgression: settlementArc,
                completionMilestoneProjectId: CompleteValleySurvey,
                resourceCaps: AuthoredResourceCaps,
                postCompletionPrograms: postCompletionPrograms,
                chains: chains,
                closureCriterion: closureCriterion,
                chapters: Chapters());
        }

        // ---- Storyline (D-076): Millbrook Valley, told by what the player brings back. ----
        private static IEnumerable<WalkGame.Domain.Story.StoryChapterDefinition> Chapters()
        {
            WalkGame.Domain.Story.StoryChapterDefinition Ch(int n, string id, string title, string body, string hint, long care = 0, params string[] projects) =>
                new WalkGame.Domain.Story.StoryChapterDefinition("story." + id, n, title, body, hint, projects, care);
            return new[]
            {
                Ch(0, "quiet-valley", "The Quiet Valley",
                    "Millbrook was a working valley once: a mill on the river, a market on the square, cranes in the east marsh. Then the floods came, the drainage scheme failed, and people drifted away one season at a time. Nobody decided to abandon it. It simply went quiet. Your walks are the first footsteps on the old trail in years.",
                    ""),
                Ch(1, "way-in", "The Way In",
                    "With the trailhead cleared, the valley can be entered again. Someone carved a mark into the gatepost long ago: a heron over a wheel. You find it twice more on the way down, as if the valley meant to be found.",
                    "Clear the old trailhead to open the way in.", 0, ClearTrailHead),
                Ch(2, "water-remembers", "Water Remembers",
                    "The intake gates groan open and the river finds its old channel. By evening the leat is running past the mill for the first time since the floods. Water remembers the shape of a place even when people forget it.",
                    "Restore the river intake to let the water back in.", 0, RiverIntake),
                Ch(3, "hands-at-the-bench", "Hands at the Bench",
                    "A roof, a bench, a rack of tools: the workshop is small, but it changes what is possible. A salvage crew from the next valley stops by, then stays. They say they heard there was work here again.",
                    "Rebuild the settlement workshop.", 0, BuildWorkshop),
                Ch(4, "lights-in-the-square", "Lights in the Square",
                    "The mill race turns the generator and the street lamps come on one by one. The market hall has stalls again and a notice board, and someone has pinned a hand-drawn map of the valley to it, with your trail marked in pencil.",
                    "Wire settlement power to light the square.", 0, WireSettlementPower),
                Ch(5, "marsh-breathes", "The Marsh Breathes",
                    "The old drains are broken open and the east wetland fills slowly, like a held breath let go. Frogs first, then dragonflies. Late one afternoon a single crane circles twice and lands.",
                    "Drain the east wetland.", 0, WetlandDrainage),
                Ch(6, "under-the-canopy", "Under the Canopy",
                    "Fallen timber cleared, the understory planted: the old grove starts to fill in from the ground up. Bird calls return before the leaves do. Someone ties a ribbon to the oldest oak so nobody fells it by mistake.",
                    "Replant the woodland understory.", 0, PlantWoodlandUnderstory),
                Ch(7, "above-the-trees", "Above the Trees",
                    "The walkway threads the crowns of the grove, and from up here the valley makes sense: the river, the marsh, the square, the ridge. Children run the boards at dusk. The heron-and-wheel mark is carved here too, freshly, by someone new.",
                    "Raise the canopy walkway through the grove.", 0, CanopyWalkway),
                Ch(8, "every-mile-mapped", "Every Mile Mapped",
                    "The survey is finished: every path, spring and field boundary recorded at the observatory. The valley is whole again, not as it was, but as the people now living in it want it to be. Millbrook is not quiet anymore.",
                    "Complete the valley survey to close the story of the restoration.", 0, CompleteValleySurvey),
                Ch(9, "long-watch", "The Long Watch",
                    "Restored is not finished. Each season of tending keeps the cranes returning and the water clear. Your walks still matter here. They always did.",
                    "Complete a season of evergreen tending after the valley is restored.", 1),
            };
        }
    }
}
