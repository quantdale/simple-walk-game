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
    /// Region 1 — Millbrook Valley: the authored M4 content contract (content version 2,
    /// D-036). Six interdependent restoration chains, six landmarks, three bounded
    /// producers, thirteen provenance-bearing discoveries, three deterministic expedition
    /// routes, region-level ecology/settlement arcs, and the Complete Valley Survey
    /// closure milestone with a stable post-completion evergreen state.
    ///
    /// The original M1/M3 development-seed definitions (projects, their costs and
    /// prerequisites, landmark stage triggers and the workshop salvage producer) are
    /// preserved verbatim: durable saves and the M3 acceptance proof depend on them.
    /// Presentation binds to canonical state through the keys below plus the documented
    /// bindings in docs/WORLD_AND_CONTENT.md §12; no Unity assets are defined here.
    /// </summary>
    public static class Region1Catalog
    {
        public const int AuthoredContentVersion = 2;

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

        public static RegionDefinition Create() => CachedInstance;

        private static RegionDefinition Build()
        {
            var projects = new List<ProjectDefinition>
            {
                // Trail access
                new ProjectDefinition(new ProjectId(ClearTrailHead), "Clear the old trailhead", 300L,
                    descriptionKey: "Cut back overgrowth and clear debris so the valley can be entered safely."),
                new ProjectDefinition(new ProjectId(RebuildTrailBridges), "Rebuild the trail bridges", 450L,
                    prerequisites: new[] { new ProjectId(ClearTrailHead) },
                    descriptionKey: "Replace collapsed crossings so the high routes are passable again."),
                new ProjectDefinition(new ProjectId(OpenLookout), "Open the ridge lookout", 650L,
                    prerequisites: new[] { new ProjectId(RebuildTrailBridges) },
                    descriptionKey: "Clear and secure the fire lookout above the treeline."),

                // Water system
                new ProjectDefinition(new ProjectId(RiverIntake), "Restore the river intake", 800L,
                    prerequisites: new[] { new ProjectId(ClearTrailHead) },
                    descriptionKey: "Free the intake works and get clean water moving again."),
                new ProjectDefinition(new ProjectId(ClearReservoir), "Clear the old reservoir", 700L,
                    prerequisites: new[] { new ProjectId(RiverIntake) },
                    descriptionKey: "Dredge silt and repair the reservoir basin."),
                new ProjectDefinition(new ProjectId(LayWaterLines), "Lay water lines to the hub", 850L,
                    prerequisites: new[] { new ProjectId(ClearReservoir) },
                    descriptionKey: "Run piping from the reservoir down to the settlement hub."),

                // Settlement community
                new ProjectDefinition(new ProjectId(BuildWorkshop), "Rebuild the settlement workshop", 1500L,
                    prerequisites: new[] { new ProjectId(RiverIntake) },
                    descriptionKey: "Roof, bench and tool the workshop that the valley builds with."),
                new ProjectDefinition(new ProjectId(RestoreMarketHall), "Restore the market hall", 950L,
                    prerequisites: new[] { new ProjectId(BuildWorkshop) },
                    descriptionKey: "Raise the market hall where the community trades and gathers."),
                new ProjectDefinition(new ProjectId(WireSettlementPower), "Wire settlement power", 1250L,
                    prerequisites: new[] { new ProjectId(RestoreMarketHall), new ProjectId(LayWaterLines) },
                    descriptionKey: "Run the mill race generator and light the streets warm."),

                // Wetland recovery
                new ProjectDefinition(new ProjectId(WetlandDrainage), "Drain and replant the east wetland", 2200L,
                    prerequisites: new[] { new ProjectId(RiverIntake) },
                    descriptionKey: "Undo the failed drainage scheme and let the marsh breathe."),
                new ProjectDefinition(new ProjectId(ReplantNativeSedges), "Replant native sedges", 500L,
                    prerequisites: new[] { new ProjectId(WetlandDrainage) },
                    descriptionKey: "Reinforce the banks with native sedge beds."),
                new ProjectDefinition(new ProjectId(BuildNestingIslets), "Build crane nesting islets", 650L,
                    prerequisites: new[] { new ProjectId(ReplantNativeSedges) },
                    descriptionKey: "Anchor safe nesting islands for returning cranes."),
                new ProjectDefinition(new ProjectId(WetlandBoardwalk), "Raise the wetland boardwalk", 800L,
                    prerequisites: new[] { new ProjectId(WetlandDrainage), new ProjectId(RebuildTrailBridges) },
                    descriptionKey: "Span the reeds with a quiet boardwalk route."),

                // Woodland
                new ProjectDefinition(new ProjectId(ClearFallenTimber), "Clear the fallen timber", 400L,
                    prerequisites: new[] { new ProjectId(ClearTrailHead) },
                    descriptionKey: "Open the storm-felled grove paths and make the wood safe."),
                new ProjectDefinition(new ProjectId(PlantWoodlandUnderstory), "Plant the woodland understory", 600L,
                    prerequisites: new[] { new ProjectId(ClearFallenTimber) },
                    descriptionKey: "Reintroduce ferns, shrubs and young hardwoods beneath the canopy."),

                // Canopy link (original seed definition, preserved verbatim incl. cost/prereqs)
                new ProjectDefinition(new ProjectId(CanopyWalkway), "Raise the canopy walkway", 4000L,
                    prerequisites: new[] { new ProjectId(BuildWorkshop), new ProjectId(WetlandDrainage) },
                    descriptionKey: "Bridge the grove crowns with the valley's signature walkway."),

                // Research
                new ProjectDefinition(new ProjectId(RefitObservatoryDome), "Refit the observatory dome", 900L,
                    prerequisites: new[] { new ProjectId(OpenLookout) },
                    descriptionKey: "Regear the dome and mount the survey telescope."),
                new ProjectDefinition(new ProjectId(CalibrateSurveyRig), "Calibrate the survey rig", 700L,
                    prerequisites: new[] { new ProjectId(RefitObservatoryDome), new ProjectId(BuildWorkshop) },
                    descriptionKey: "Align instruments and benches for a full valley survey."),
                new ProjectDefinition(new ProjectId(CompleteValleySurvey), "Complete the valley survey", 1200L,
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
                completionMilestoneProjectId: CompleteValleySurvey);
        }
    }
}
