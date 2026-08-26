using System;
using System.Collections.Generic;
using System.Linq;
using WalkGame.Application.Content;
using WalkGame.Domain.Common;
using WalkGame.Domain.Discoveries;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Expeditions;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Validation;
using ProjectId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProjectIdKind>;
using RegionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RegionIdKind>;
using DiscoveryId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.DiscoveryIdKind>;
using ExpeditionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ExpeditionIdKind>;

namespace WalkGame.Application.Tests;

/// <summary>
/// M4 workstream E evidence: the authored Region 1 graph is a valid release gate and
/// meets the documented content minimum, plus red-team coverage for malformed content.
/// </summary>
public class M4ContentGraphTests
{
    [Fact]
    public void AuthoredRegion1Catalog_Validates_WithZeroViolations()
    {
        var violations = ContentValidator.Validate(Region1Catalog.Create());

        Assert.Empty(violations);
    }

    [Fact]
    public void AuthoredRegion1Catalog_MeetsTheDocumentedContentMinimum()
    {
        var content = Region1Catalog.Create();

        int chainRoots = content.Projects.Count(p => p.Prerequisites.Count == 0);
        Assert.InRange(content.Projects.Count, 12, 20);          // meaningful project nodes
        Assert.InRange(chainRoots, 1, 7);                         // entry paths define chains
        Assert.True(content.Landmarks.Count >= 6);
        Assert.True(content.Producers.Count >= 2);
        Assert.True(content.Discoveries.Count >= 10);
        Assert.True(content.Expeditions.Count >= 3);

        Assert.NotNull(content.CompletionMilestoneProjectId);
        Assert.NotNull(content.FindProject(content.CompletionMilestoneProjectId!));
        Assert.Equal("region.millbrook-valley", content.Id.Value);
        Assert.True(content.EcologyProgression.Stages.Count >= 3);
        Assert.True(content.SettlementProgression.Stages.Count >= 3);

        // Every landmark stage and producer/discovery trigger resolves to a defined project.
        foreach (var landmark in content.Landmarks)
            foreach (var stage in landmark.Stages)
                Assert.NotNull(content.FindProject(stage.UnlockedByProjectId));
        foreach (var producer in content.Producers)
            Assert.NotNull(content.FindProject(producer.UnlockedByProjectId));
        foreach (var discovery in content.Discoveries)
            Assert.NotNull(content.FindProject(discovery.UnlockedByProjectId));
        foreach (var expedition in content.Expeditions)
            foreach (var required in expedition.RequiredProjectIds)
                Assert.NotNull(content.FindProject(required));

        // Preserved seed contract: original five projects keep their IDs, costs and edges.
        Assert.Equal(300L, content.FindProject("proj.clear-trailhead")!.VitalityCost);
        Assert.Equal(800L, content.FindProject("proj.river-intake")!.VitalityCost);
        Assert.Equal(1500L, content.FindProject("proj.build-workshop")!.VitalityCost);
        Assert.Equal(2200L, content.FindProject("proj.wetland-drainage")!.VitalityCost);
        Assert.Equal(4000L, content.FindProject("proj.canopy-walkway")!.VitalityCost);
        var canopy = content.FindProject("proj.canopy-walkway")!;
        Assert.Contains(canopy.Prerequisites, p => p.Value == "proj.build-workshop");
        Assert.Contains(canopy.Prerequisites, p => p.Value == "proj.wetland-drainage");
        var salvage = content.Producers.Single(p => p.Id.Value == "prd.workshop-salvage");
        Assert.Equal(2500L, salvage.MilliUnitsPerDay);
        Assert.Equal(500L, salvage.CapacityUnits);
    }

    [Fact]
    public void ForwardPrerequisiteReferences_ValidateCorrectly()
    {
        // Regression for the order-dependent validator defect flagged by the campaign:
        // a prerequisite declared BEFORE its dependency's own definition must validate.
        var content = new RegionDefinition(
            new RegionId("region.fwd"), "Forward Region",
            new[]
            {
                new ProjectDefinition(new ProjectId("proj.late"), "t.late", 100L,
                    prerequisites: new[] { new ProjectId("proj.early") }),
                new ProjectDefinition(new ProjectId("proj.early"), "t.early", 100L),
            },
            Array.Empty<LandmarkDefinition>(),
            Array.Empty<ProducerDefinition>());

        Assert.Empty(ContentValidator.Validate(content));
    }

    [Fact]
    public void DuplicateDiscoveryAndExpeditionIds_AreFlagged()
    {
        var baseContent = Region1Catalog.Create();
        var duplicated = new RegionDefinition(
            baseContent.Id, baseContent.TitleKey,
            baseContent.Projects,
            baseContent.Landmarks,
            baseContent.Producers,
            contentVersion: baseContent.ContentVersion,
            discoveries: new[]
            {
                baseContent.Discoveries[0],
                baseContent.Discoveries[0],
            },
            expeditions: new[]
            {
                baseContent.Expeditions[0],
                baseContent.Expeditions[0],
            });

        var violations = ContentValidator.Validate(duplicated);

        Assert.Contains(violations, v => v.Contains("Duplicate discovery ID"));
        Assert.Contains(violations, v => v.Contains("Duplicate expedition ID"));
    }

    [Fact]
    public void DiscoveryTriggerToUnknownProject_IsFlagged()
    {
        var baseContent = Region1Catalog.Create();
        var broken = new RegionDefinition(
            baseContent.Id, baseContent.TitleKey,
            baseContent.Projects,
            baseContent.Landmarks,
            baseContent.Producers,
            discoveries: new[]
            {
                new DiscoveryDefinition(
                    new DiscoveryId("disc.bad"),
                    "artifact", "b.title", "b.body", "b.prov",
                    unlockedByProjectId: "proj.does-not-exist"),
            });

        var violations = ContentValidator.Validate(broken);

        Assert.Contains(violations, v => v.Contains("Discovery 'disc.bad' references missing unlock project"));
    }

    [Fact]
    public void ExpeditionRequiringUnreachableStage_IsFlagged()
    {
        var baseContent = Region1Catalog.Create();
        var broken = new RegionDefinition(
            baseContent.Id, baseContent.TitleKey,
            baseContent.Projects,
            baseContent.Landmarks,
            baseContent.Producers,
            expeditions: new[]
            {
                new ExpeditionDefinition(
                    new ExpeditionId("exp.bad"),
                    "e.title", "e.desc",
                    requiredProjectIds: new[] { "proj.clear-trailhead" },
                    requiredStages: new[]
                    {
                        new WalkGame.Domain.Expeditions.ExpeditionStageRequirement(
                            "lm.river-intake", RestorationStage.Flourishing),
                    }),
            });

        var violations = ContentValidator.Validate(broken);

        Assert.Contains(violations, v =>
            v.Contains("'lm.river-intake' at stage Flourishing, which its content never reaches"));
    }

    [Fact]
    public void NonAscendingArcStages_AreFlagged()
    {
        var baseContent = Region1Catalog.Create();
        var broken = new RegionDefinition(
            baseContent.Id, baseContent.TitleKey,
            baseContent.Projects,
            baseContent.Landmarks,
            baseContent.Producers,
            ecologyProgression: new RegionProgressionDefinition(new[]
            {
                new ProgressionStageDefinition(2, "proj.clear-fallen-timber"),
                new ProgressionStageDefinition(1, "proj.clear-reservoir"),
            }));

        var violations = ContentValidator.Validate(broken);

        Assert.Contains(violations, v => v.Contains("ecology progression stages are not strictly ascending"));
    }

    [Fact]
    public void UnreachableProject_IsFlaggedAsHiddenDeadlock()
    {
        var baseContent = Region1Catalog.Create();
        // A cyclic island nothing else can reach, plus a project hanging off it.
        var islandBase = new ProjectDefinition(new ProjectId("proj.island-base"), "t.base", 100L,
            prerequisites: new[] { new ProjectId("proj.island-closure") });
        var islandClosure = new ProjectDefinition(new ProjectId("proj.island-closure"), "t.island", 100L,
            prerequisites: new[] { new ProjectId("proj.island-base") });
        var orphan = new ProjectDefinition(new ProjectId("proj.orphan"), "t.orphan", 100L,
            prerequisites: new[] { new ProjectId("proj.island-closure") });
        var projects = new List<ProjectDefinition>(baseContent.Projects) { islandBase, islandClosure, orphan };
        var broken = new RegionDefinition(
            baseContent.Id, baseContent.TitleKey,
            projects, baseContent.Landmarks, baseContent.Producers);

        var violations = ContentValidator.Validate(broken);

        Assert.Contains(violations, v => v.Contains("Project 'proj.orphan' is unreachable from any entry project"));
    }

    [Fact]
    public void UnreachableClosureMilestone_IsFlagged()
    {
        var baseContent = Region1Catalog.Create();
        var islandBase = new ProjectDefinition(new ProjectId("proj.island-base"), "t.base", 100L,
            prerequisites: new[] { new ProjectId("proj.island-closure") });
        var islandClosure = new ProjectDefinition(new ProjectId("proj.island-closure"), "t.island", 100L,
            prerequisites: new[] { new ProjectId("proj.island-base") });
        var projects = new List<ProjectDefinition>(baseContent.Projects) { islandBase, islandClosure };
        var broken = new RegionDefinition(
            baseContent.Id, baseContent.TitleKey,
            projects, baseContent.Landmarks, baseContent.Producers,
            completionMilestoneProjectId: "proj.island-closure");

        var violations = ContentValidator.Validate(broken);

        Assert.Contains(violations, v => v.Contains("cycle"));
        Assert.Contains(violations, v => v.Contains("'proj.island-closure' is not reachable from an entry project"));
    }
}
