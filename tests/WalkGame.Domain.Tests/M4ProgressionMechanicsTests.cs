using System;
using System.Collections.Generic;
using System.Linq;
using WalkGame.Domain.Discoveries;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Expeditions;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Simulation;
using WalkGame.Domain.Validation;
using LandmarkId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.LandmarkIdKind>;
using ProjectId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProjectIdKind>;
using RegionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RegionIdKind>;

namespace WalkGame.Domain.Tests;

/// <summary>
/// M4 canonical mechanics over minimal local content: discovery unlock idempotency,
/// deterministic expedition hooks, region progression arcs, closure milestone and
/// post-completion evergreen stability.
/// </summary>
public class M4ProgressionMechanicsTests
{
    private const string EntryA = "proj.a";
    private const string MidB = "proj.b";
    private const string ClosureC = "proj.c";

    private static readonly DateTimeOffset T0 =
        new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);

    private static RegionDefinition Content(
        IEnumerable<DiscoveryDefinition>? discoveries = null,
        IEnumerable<ExpeditionDefinition>? expeditions = null,
        RegionProgressionDefinition? ecology = null,
        string? closure = null)
    {
        var projects = new List<ProjectDefinition>
        {
            new(new ProjectId(EntryA), "t.a", 100L),
            new(new ProjectId(MidB), "t.b", 150L, prerequisites: new[] { new ProjectId(EntryA) }),
            new(new ProjectId(ClosureC), "t.c", 200L, prerequisites: new[] { new ProjectId(MidB) }),
        };
        var landmarks = new[]
        {
            new LandmarkDefinition(new LandmarkId("lm.gate"), "lm.gate.title", new[]
            {
                new LandmarkStageDefinition(RestorationStage.Stabilized, EntryA),
                new LandmarkStageDefinition(RestorationStage.Functional, MidB),
            }),
        };
        return new RegionDefinition(
            new RegionId("region.mech"), "Mechanics Region",
            projects, landmarks, Array.Empty<Regions.ProducerDefinition>(),
            discoveries: discoveries,
            expeditions: expeditions,
            ecologyProgression: ecology ?? RegionProgressionDefinition.Empty(),
            completionMilestoneProjectId: closure);
    }

    private static List<SimulationEvent> Complete(GameState game, RegionDefinition content, string projectId)
    {
        var events = new List<SimulationEvent>();
        var state = game.Region.FindProject(projectId)!;
        state.Status = ProjectStatus.Active;
        game.Queue.ActiveProjectId = projectId;
        game.Resources.Add(ResourceType.Vitality, content.FindProject(projectId)!.VitalityCost);
        OfflineAdvancer.AllocateVitality(game, content, T0, events);
        return events;
    }

    private static void AdvanceIdle(GameState game, RegionDefinition content, DateTimeOffset at)
    {
        var events = new List<SimulationEvent>();
        OfflineAdvancer.Advance(game, content, at, events);
    }

    [Fact]
    public void Discovery_UnlocksExactlyOnce_FromCanonicalCompletion()
    {
        var content = Content(discoveries: new[]
        {
            new DiscoveryDefinition(
                new Common.Id<Common.DiscoveryIdKind>("disc.test"),
                "artifact", "d.title", "d.body", "d.prov",
                unlockedByProjectId: EntryA),
        });
        var game = GameFactory.NewGame(content, T0, seed: 7UL);

        var events = Complete(game, content, EntryA);

        Assert.Single(events.OfType<DiscoveryUnlocked>());
        Assert.True(game.Region.Discoveries.ContainsKey("disc.test"));
        Assert.False(game.Region.Discoveries["disc.test"].Reviewed);

        // Replays/reloads/repeated advancement never duplicate the unlock.
        var firstSeenAt = game.Region.Discoveries["disc.test"].DiscoveredAtUtc;
        for (int day = 1; day <= 3; day++)
            AdvanceIdle(game, content, T0.AddDays(day));
        Complete(game, content, MidB);

        Assert.Single(game.Region.Discoveries);
        Assert.Equal(firstSeenAt, game.Region.Discoveries["disc.test"].DiscoveredAtUtc);
    }

    [Fact]
    public void Expedition_AvailabilityAndCompletion_AreDeterministicOneShot()
    {
        var content = Content(expeditions: new[]
        {
            new ExpeditionDefinition(
                new Common.Id<Common.ExpeditionIdKind>("exp.route"),
                "exp.title", "exp.desc",
                requiredProjectIds: new[] { EntryA },
                requiredStages: new[] { new ExpeditionStageRequirement("lm.gate", RestorationStage.Functional) },
                reward: new ExpeditionReward(ResourceType.Materials, 40L)),
        });
        var game = GameFactory.NewGame(content, T0, seed: 7UL);
        game.Resources.SetCap(ResourceType.Materials, 1_000L);

        // Locked until its required project completes.
        AdvanceIdle(game, content, T0.AddDays(1));
        Assert.False(game.Region.Expeditions.ContainsKey("exp.route"));

        var eventsA = Complete(game, content, EntryA);
        Assert.Single(eventsA.OfType<ExpeditionAvailable>());
        Assert.Null(game.Region.Expeditions["exp.route"].CompletedAtUtc);
        Assert.Equal(0L, game.Resources.Get(ResourceType.Materials)); // stage gate not yet met

        var eventsB = Complete(game, content, MidB);
        var completion = Assert.Single(eventsB.OfType<ExpeditionCompleted>());
        Assert.Equal(ResourceType.Materials, completion.RewardType);
        Assert.Equal(40L, completion.UnitsGranted);
        Assert.Equal(40L, game.Resources.Get(ResourceType.Materials));

        DateTimeOffset completedAt = game.Region.Expeditions["exp.route"].CompletedAtUtc!.Value;

        // Repeated advancement cannot complete twice or re-grant the reward.
        for (int day = 2; day <= 5; day++)
            AdvanceIdle(game, content, T0.AddDays(day));
        Complete(game, content, ClosureC);

        Assert.Single(game.Region.Expeditions);
        Assert.Equal(completedAt, game.Region.Expeditions["exp.route"].CompletedAtUtc);
        Assert.Equal(40L, game.Resources.Get(ResourceType.Materials));
    }

    [Fact]
    public void ExpeditionReward_IsClampedByResourceCap()
    {
        var content = Content(expeditions: new[]
        {
            new ExpeditionDefinition(
                new Common.Id<Common.ExpeditionIdKind>("exp.route"),
                "exp.title", "exp.desc",
                requiredProjectIds: Array.Empty<string>(),
                requiredStages: null,
                reward: new ExpeditionReward(ResourceType.Materials, 40L)),
        });
        var game = GameFactory.NewGame(content, T0, seed: 7UL);
        game.Resources.SetCap(ResourceType.Materials, 10L);

        var events = Complete(game, content, EntryA);

        var completion = Assert.Single(events.OfType<ExpeditionCompleted>());
        Assert.Equal(10L, completion.UnitsGranted);
        Assert.Equal(10L, game.Resources.Get(ResourceType.Materials));
    }

    [Fact]
    public void ProgressionArcs_AdvanceMonotonically()
    {
        var ecology = new RegionProgressionDefinition(new[]
        {
            new Regions.ProgressionStageDefinition(1, EntryA),
            new Regions.ProgressionStageDefinition(2, MidB),
            new Regions.ProgressionStageDefinition(3, ClosureC),
        });
        var content = Content(ecology: ecology);
        var game = GameFactory.NewGame(content, T0, seed: 7UL);

        Assert.Equal(0, game.Region.EcologyStage);
        Complete(game, content, EntryA);
        Assert.Equal(1, game.Region.EcologyStage);
        Complete(game, content, MidB);
        Assert.Equal(2, game.Region.EcologyStage);

        // Idle advancement never regresses or duplicates stages.
        AdvanceIdle(game, content, T0.AddDays(9));
        Assert.Equal(2, game.Region.EcologyStage);

        Complete(game, content, ClosureC);
        Assert.Equal(3, game.Region.EcologyStage);
    }

    [Fact]
    public void Closure_CompletesOnce_PostCompletionStaysEvergreen()
    {
        var content = Content(closure: ClosureC);
        var game = GameFactory.NewGame(content, T0, seed: 7UL);

        Complete(game, content, EntryA);
        Complete(game, content, MidB);
        Assert.False(game.Region.IsCompleted);

        Complete(game, content, ClosureC);
        Assert.True(game.Region.IsCompleted);
        Assert.NotNull(game.Region.RegionCompletedAtUtc);

        // Post-completion: repeated boots never reset the region or refire the milestone.
        DateTimeOffset completedAt = game.Region.RegionCompletedAtUtc!.Value;
        AdvanceIdle(game, content, T0.AddDays(30));
        AdvanceIdle(game, content, T0.AddDays(60));

        Assert.True(game.Region.IsCompleted);
        Assert.Equal(completedAt, game.Region.RegionCompletedAtUtc);
        Assert.Equal(ProjectStatus.Completed, game.Region.FindProject(ClosureC)!.Status);
    }
}
