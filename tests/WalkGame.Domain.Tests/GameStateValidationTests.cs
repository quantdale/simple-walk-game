using System;
using WalkGame.Domain;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Validation;
using LandmarkId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.LandmarkIdKind>;
using ProjectId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProjectIdKind>;
using ProducerId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProducerIdKind>;
using RegionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RegionIdKind>;

namespace WalkGame.Domain.Tests;

public class GameStateValidationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);

    private static RegionDefinition CreateContent()
    {
        var a = new ProjectDefinition(new ProjectId("proj.a"), "A", 300L);
        var b = new ProjectDefinition(new ProjectId("proj.b"), "B", 500L, new[] { new ProjectId("proj.a") });
        var c = new ProjectDefinition(new ProjectId("proj.c"), "C", 200L, new[] { new ProjectId("proj.b") });
        var gate = new LandmarkDefinition(new LandmarkId("land.gate"), "Gate", new[]
        {
            new LandmarkStageDefinition(RestorationStage.Ruined, "proj.a"),
            new LandmarkStageDefinition(RestorationStage.Stabilized, "proj.b"),
        });
        var mill = new ProducerDefinition(
            new ProducerId("prod.mill"), "Mill", ResourceType.Materials,
            24000L, 1_000_000L, "proj.b");
        return new RegionDefinition(
            new RegionId("region.test"), "Test Region",
            new[] { a, b, c },
            new[] { gate },
            new[] { mill });
    }

    private static GameState NewTamperedGame(out RegionDefinition content)
    {
        content = CreateContent();
        return GameFactory.NewGame(content, T0, 42UL);
    }

    [Fact]
    public void FreshFactoryState_ValidatesWithZeroViolations()
    {
        var content = CreateContent();
        var game = GameFactory.NewGame(content, T0, 42UL);

        var violations = GameStateValidator.Validate(game, content);

        Assert.Empty(violations);
    }

    [Fact]
    public void MissingProducerRuntimeRow_IsFlagged()
    {
        // D-041: producer rows are created for the full content set at game start;
        // a missing row is corruption that would silently disable the producer.
        var game = NewTamperedGame(out var content);
        game.Region.Producers.RemoveAt(0);

        var violations = GameStateValidator.Validate(game, content);

        Assert.Contains(violations, v => v.Contains("Missing runtime state for producer", StringComparison.Ordinal));
    }

    [Fact]
    public void NegativeResourceBalance_IsFlagged()
    {
        var game = NewTamperedGame(out var content);
        game.Resources.Amounts[ResourceType.Vitality] = -10L;

        var violations = GameStateValidator.Validate(game, content);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Contains("negative"));
    }

    [Fact]
    public void InvestedExceedingCost_IsFlagged()
    {
        var game = NewTamperedGame(out var content);
        game.Region.FindProject("proj.a")!.VitalityInvested = 301L;

        var violations = GameStateValidator.Validate(game, content);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Contains("invested vitality out of bounds"));
    }

    [Fact]
    public void QueuedId_WhoseRuntimeStatusIsNotQueued_IsFlagged()
    {
        var game = NewTamperedGame(out var content);
        game.Region.FindProject("proj.b")!.Status = ProjectStatus.Locked;
        game.Queue.QueuedProjectIds.Add("proj.b");

        var violations = GameStateValidator.Validate(game, content);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Contains("inconsistent status"));
    }

    [Fact]
    public void ActiveProjectId_PointingAtNonActiveRuntime_IsFlagged()
    {
        var game = NewTamperedGame(out var content);
        game.Queue.ActiveProjectId = "proj.c";

        var violations = GameStateValidator.Validate(game, content);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Contains("has inconsistent status"));
    }

    [Fact]
    public void UnknownRuntimeProject_IsFlagged()
    {
        var game = NewTamperedGame(out var content);
        game.Region.Projects["proj.ghost"] = new ProjectState { ProjectId = "proj.ghost" };

        var violations = GameStateValidator.Validate(game, content);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Contains("unknown to content definitions"));
    }

    [Fact]
    public void PendingStoreAboveCapacity_IsFlagged()
    {
        var game = NewTamperedGame(out var content);
        var definition = content.FindProducer("prod.mill")!;
        game.Region.FindProducer("prod.mill")!.StoredMilliUnits =
            definition.CapacityUnits * ProducerDefinition.MilliUnitsPerUnit + 1L;

        var violations = GameStateValidator.Validate(game, content);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Contains("pending store out of bounds"));
    }

    [Fact]
    public void NegativePendingStore_IsFlagged()
    {
        var game = NewTamperedGame(out var content);
        game.Region.FindProducer("prod.mill")!.StoredMilliUnits = -5L;

        var violations = GameStateValidator.Validate(game, content);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Contains("pending store out of bounds"));
    }

    [Fact]
    public void LockedProducerWithProducedOutput_IsFlagged()
    {
        var game = NewTamperedGame(out var content);
        game.Region.FindProducer("prod.mill")!.TotalProducedMilliUnits = 4000L;

        var violations = GameStateValidator.Validate(game, content);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Contains("Locked producer"));
    }
}
