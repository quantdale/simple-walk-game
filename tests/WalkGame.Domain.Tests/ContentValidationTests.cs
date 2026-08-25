using System;
using System.Linq;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Validation;
using LandmarkId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.LandmarkIdKind>;
using ProjectId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProjectIdKind>;
using ProducerId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProducerIdKind>;
using RegionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RegionIdKind>;

namespace WalkGame.Domain.Tests;

public class ContentValidationTests
{
    private static ProjectDefinition P(string id, long cost, params string[] prerequisiteIds) =>
        new(new ProjectId(id), $"title.{id}", cost,
            prerequisiteIds.Select(p => new ProjectId(p)));

    private static LandmarkDefinition L(string id, params LandmarkStageDefinition[] stages) =>
        new(new LandmarkId(id), $"landmark.{id}", stages);

    private static ProducerDefinition Pr(string id, string unlockedByProjectId) =>
        new(new ProducerId(id), $"producer.{id}", ResourceType.Materials, 24000L, 1000L, unlockedByProjectId);

    private static RegionDefinition Region(
        ProjectDefinition[] projects,
        LandmarkDefinition[]? landmarks = null,
        ProducerDefinition[]? producers = null) =>
        new(new RegionId("region.test"), "Test Region",
            projects, landmarks ?? Array.Empty<LandmarkDefinition>(), producers ?? Array.Empty<ProducerDefinition>());

    [Fact]
    public void ValidGraph_HasZeroViolations()
    {
        var content = Region(
            new[] { P("proj.a", 300L), P("proj.b", 500L, "proj.a"), P("proj.c", 200L, "proj.b") },
            new[] { L("land.gate",
                new LandmarkStageDefinition(RestorationStage.Stabilized, "proj.a"),
                new LandmarkStageDefinition(RestorationStage.Functional, "proj.c")) },
            new[] { Pr("prod.mill", "proj.b") });

        var violations = ContentValidator.Validate(content);

        var unexpected = violations.Where(v => !v.Contains("non-positive cost")).ToList();
        Assert.Empty(unexpected);
    }

    [Fact]
    public void ProjectDefinition_PreservesConstructorVitalityCost()
    {
        var definition = new ProjectDefinition(new ProjectId("proj.cost"), "title.cost", 300L);

        Assert.Equal(300L, definition.VitalityCost);
    }

    [Fact]
    public void DuplicateProjectId_IsFlagged()
    {
        var content = Region(new[]
        {
            P("proj.a", 300L),
            P("proj.b", 200L, "proj.a"),
            P("proj.b", 100L, "proj.a"),
        });

        var violations = ContentValidator.Validate(content);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Contains("Duplicate project ID"));
    }

    [Fact]
    public void MissingPrerequisiteReference_IsFlagged()
    {
        var content = Region(new[] { P("proj.a", 300L, "proj.ghost") });

        var violations = ContentValidator.Validate(content);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Contains("missing prerequisite"));
    }

    [Fact]
    public void PrerequisiteCycle_IsFlagged()
    {
        var content = Region(new[]
        {
            P("proj.x", 100L, "proj.y"),
            P("proj.y", 100L, "proj.x"),
        });

        var violations = ContentValidator.Validate(content);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Contains("cycle"));
    }

    [Fact]
    public void MissingLandmarkUnlockProjectReference_IsFlagged()
    {
        var content = Region(
            new[] { P("proj.a", 300L) },
            new[] { L("land.gate", new LandmarkStageDefinition(RestorationStage.Stabilized, "proj.ghost")) });

        var violations = ContentValidator.Validate(content);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Contains("references missing project"));
    }

    [Fact]
    public void UnknownProducerUnlockProjectReference_IsFlagged()
    {
        var content = Region(
            new[] { P("proj.a", 300L) },
            null,
            new[] { Pr("prod.mill", "proj.ghost") });

        var violations = ContentValidator.Validate(content);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Contains("missing unlock project"));
    }

    [Fact]
    public void NoEntryProject_IsFlagged()
    {
        var content = Region(new[]
        {
            P("proj.x", 100L, "proj.y"),
            P("proj.y", 100L, "proj.x"),
        });

        var violations = ContentValidator.Validate(content);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Contains("no entry project"));
    }
}
