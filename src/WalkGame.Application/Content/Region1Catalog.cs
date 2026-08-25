using System;
using System.Collections.Generic;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using LandmarkId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.LandmarkIdKind>;
using ProducerId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProducerIdKind>;
using ProjectId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProjectIdKind>;
using RegionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RegionIdKind>;

namespace WalkGame.Application.Content
{
    /// <summary>
    /// Development content for Region 1. This is a minimal authoring seed for the M1/M3
    /// vertical slice — the final Region 1 graph is an M4 deliverable and will move to
    /// explicit content definitions with validators.
    /// </summary>
    public static class Region1Catalog
    {
        private const string ClearTrailHead = "proj.clear-trailhead";
        private const string RiverIntake = "proj.river-intake";
        private const string BuildWorkshop = "proj.build-workshop";
        private const string WetlandDrainage = "proj.wetland-drainage";
        private const string CanopyWalkway = "proj.canopy-walkway";

        public static RegionDefinition Create()
        {
            var projects = new List<ProjectDefinition>
            {
                new ProjectDefinition(new ProjectId(ClearTrailHead), "Clear the old trailhead", 300L),
                new ProjectDefinition(new ProjectId(RiverIntake), "Restore the river intake", 800L,
                    prerequisites: new[] { new ProjectId(ClearTrailHead) }),
                new ProjectDefinition(new ProjectId(BuildWorkshop), "Rebuild the settlement workshop", 1500L,
                    prerequisites: new[] { new ProjectId(RiverIntake) }),
                new ProjectDefinition(new ProjectId(WetlandDrainage), "Drain and replant the east wetland", 2200L,
                    prerequisites: new[] { new ProjectId(RiverIntake) }),
                new ProjectDefinition(new ProjectId(CanopyWalkway), "Raise the canopy walkway", 4000L,
                    prerequisites: new[] { new ProjectId(BuildWorkshop), new ProjectId(WetlandDrainage) }),
            };

            var landmarks = new List<LandmarkDefinition>
            {
                new LandmarkDefinition(new LandmarkId("lm.trailhead"), "Old Trailhead",
                    stages: new[]
                    {
                        new LandmarkStageDefinition(RestorationStage.Stabilized, ClearTrailHead),
                        new LandmarkStageDefinition(RestorationStage.Functional, BuildWorkshop),
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
                        new LandmarkStageDefinition(RestorationStage.Flourishing, CanopyWalkway),
                    }),
            };

            var producers = new List<ProducerDefinition>
            {
                new ProducerDefinition(new ProducerId("prd.workshop-salvage"), "Workshop Salvage Crew",
                    output: ResourceType.Materials,
                    milliUnitsPerDay: 2500L,
                    capacityUnits: 500L,
                    unlockedByProjectId: BuildWorkshop),
            };

            return new RegionDefinition(new RegionId("region.millbrook-valley"), "Millbrook Valley",
                projects, landmarks, producers);
        }
    }
}
