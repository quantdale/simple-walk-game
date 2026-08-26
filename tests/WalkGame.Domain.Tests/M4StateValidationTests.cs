using System;
using System.Collections.Generic;
using WalkGame.Domain.Discoveries;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Expeditions;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Validation;
using ProjectId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProjectIdKind>;
using RegionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RegionIdKind>;

namespace WalkGame.Domain.Tests;

/// <summary>Red-team coverage for the M4 canonical state integrity checks.</summary>
public class M4StateValidationTests
{
    private static readonly DateTimeOffset T0 =
        new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);

    private static RegionDefinition Content() =>
        new(new RegionId("region.v"), "Validation Region",
            new[]
            {
                new ProjectDefinition(new ProjectId("proj.a"), "t.a", 100L),
                new ProjectDefinition(new ProjectId("proj.b"), "t.b", 100L,
                    prerequisites: new[] { new ProjectId("proj.a") }),
            },
            Array.Empty<LandmarkDefinition>(),
            Array.Empty<Regions.ProducerDefinition>(),
            discoveries: new[]
            {
                new DiscoveryDefinition(
                    new Common.Id<Common.DiscoveryIdKind>("disc.known"),
                    "artifact", "k.title", "k.body", "k.prov", unlockedByProjectId: "proj.a"),
            },
            expeditions: new[]
            {
                new ExpeditionDefinition(
                    new Common.Id<Common.ExpeditionIdKind>("exp.known"),
                    "e.title", "e.desc", requiredProjectIds: new[] { "proj.a" }),
            });

    private static GameState FreshState(RegionDefinition content) =>
        GameFactory.NewGame(content, T0, seed: 3UL);

    [Fact]
    public void UnknownDiscoveryRuntime_IsFlagged()
    {
        var content = Content();
        var state = FreshState(content);
        state.Region.Discoveries["disc.ghost"] = new DiscoveryRuntimeState
        {
            DiscoveryId = "disc.ghost",
            DiscoveredAtUtc = T0,
        };

        var violations = GameStateValidator.Validate(state, content);

        Assert.Contains(violations, v => v.Contains("Discovery runtime 'disc.ghost' is unknown"));
    }

    [Fact]
    public void InconsistentDiscoveryReviewState_IsFlagged()
    {
        var content = Content();
        var state = FreshState(content);
        state.Region.Discoveries["disc.known"] = new DiscoveryRuntimeState
        {
            DiscoveryId = "disc.known",
            DiscoveredAtUtc = T0,
            Reviewed = true,
            ReviewedAtUtc = null,
        };

        var violations = GameStateValidator.Validate(state, content);

        Assert.Contains(violations, v => v.Contains("reviewed flag and timestamp are inconsistent"));
    }

    [Fact]
    public void ReviewBeforeDiscovery_IsFlagged()
    {
        var content = Content();
        var state = FreshState(content);
        state.Region.Discoveries["disc.known"] = new DiscoveryRuntimeState
        {
            DiscoveryId = "disc.known",
            DiscoveredAtUtc = T0.AddDays(2),
            Reviewed = true,
            ReviewedAtUtc = T0.AddDays(1),
        };

        var violations = GameStateValidator.Validate(state, content);

        Assert.Contains(violations, v => v.Contains("reviewed before it was discovered"));
    }

    [Fact]
    public void UnknownExpeditionRuntime_IsFlagged()
    {
        var content = Content();
        var state = FreshState(content);
        state.Region.Expeditions["exp.ghost"] = new ExpeditionRuntimeState
        {
            ExpeditionId = "exp.ghost",
            AvailableAtUtc = T0,
        };

        var violations = GameStateValidator.Validate(state, content);

        Assert.Contains(violations, v => v.Contains("Expedition runtime 'exp.ghost' is unknown"));
    }

    [Fact]
    public void ExpeditionCompletedBeforeAvailable_IsFlagged()
    {
        var content = Content();
        var state = FreshState(content);
        state.Region.Expeditions["exp.known"] = new ExpeditionRuntimeState
        {
            ExpeditionId = "exp.known",
            AvailableAtUtc = T0.AddDays(5),
            CompletedAtUtc = T0,
        };

        var violations = GameStateValidator.Validate(state, content);

        Assert.Contains(violations, v => v.Contains("completed before it became available"));
    }

    [Fact]
    public void ProgressionStageOutOfBounds_IsFlagged()
    {
        var content = Content();
        var state = FreshState(content);
        state.Region.EcologyStage = 99;

        var violations = GameStateValidator.Validate(state, content);

        Assert.Contains(violations, v => v.Contains("Ecology stage 99 is outside its content arc"));
    }

    [Fact]
    public void RegionCompletionInconsistencies_AreFlagged()
    {
        var content = Content();

        var completedWithoutMilestoneProject = FreshState(content);
        completedWithoutMilestoneProject.Region.IsCompleted = true;
        completedWithoutMilestoneProject.Region.RegionCompletedAtUtc = T0;
        var violationsA = GameStateValidator.Validate(completedWithoutMilestoneProject, content);
        // Content defines no milestone here; completion claim must be rejected.
        Assert.Contains(violationsA, v => v.Contains("defines no completion milestone"));

        var timestampWithoutFlag = FreshState(content);
        timestampWithoutFlag.Region.RegionCompletedAtUtc = T0;
        var violationsB = GameStateValidator.Validate(timestampWithoutFlag, content);
        Assert.Contains(violationsB, v => v.Contains("completion timestamp but is not marked completed"));
    }
}
