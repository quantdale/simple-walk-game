using System;
using System.Collections.Generic;
using System.Linq;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Simulation;
using ProjectId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProjectIdKind>;
using RegionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RegionIdKind>;
using RewardTransactionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RewardTransactionIdKind>;

namespace WalkGame.Domain.Tests;

public class QueueAllocationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static RegionDefinition CreateContent()
    {
        var a = new ProjectDefinition(new ProjectId("proj.a"), "A", 300L);
        var b = new ProjectDefinition(new ProjectId("proj.b"), "B", 500L, new[] { new ProjectId("proj.a") });
        var c = new ProjectDefinition(new ProjectId("proj.c"), "C", 200L, new[] { new ProjectId("proj.b") });
        return new RegionDefinition(
            new RegionId("region.test"), "Test Region",
            new[] { a, b, c },
            Array.Empty<LandmarkDefinition>(),
            Array.Empty<ProducerDefinition>());
    }

    private static GameState CreateQueuedGame(out RegionDefinition content)
    {
        content = CreateContent();
        var game = GameFactory.NewGame(content, T0, 42UL);
        game.Region.FindProject("proj.a")!.Status = ProjectStatus.Queued;
        game.Region.FindProject("proj.b")!.Status = ProjectStatus.Queued;
        game.Queue.QueuedProjectIds.Add("proj.a");
        game.Queue.QueuedProjectIds.Add("proj.b");
        return game;
    }

    private static void CreditAndAllocate(
        GameState game, RegionDefinition content,
        DateTimeOffset at, string transactionId, long vitality,
        List<SimulationEvent> events)
    {
        var outcome = game.Ledger.Apply(
            new RewardTransaction(new RewardTransactionId(transactionId), at, vitality, "walk"),
            game.Resources);
        Assert.Equal(LedgerApplyOutcome.AppliedFirstTime, outcome);
        OfflineAdvancer.Advance(game, content, at, events);
    }

    [Fact]
    public void Allocation_CompletesHeadProject_ThenRollsSurplusIntoNextQueued()
    {
        var game = CreateQueuedGame(out var content);
        var events = new List<SimulationEvent>();

        CreditAndAllocate(game, content, T0, "tx-0100", 400L, events);

        var a = game.Region.FindProject("proj.a")!;
        var b = game.Region.FindProject("proj.b")!;
        Assert.Equal(ProjectStatus.Completed, a.Status);
        Assert.Equal(300L, a.VitalityInvested);
        Assert.NotNull(a.CompletedAtUtc);
        Assert.Equal(ProjectStatus.Active, b.Status);
        Assert.Equal(100L, b.VitalityInvested);
        Assert.Equal("proj.b", game.Queue.ActiveProjectId);
        Assert.Equal(0L, game.Resources.Get(ResourceType.Vitality));

        Assert.Single(events.OfType<ProjectCompleted>(), e => e.ProjectId == "proj.a");
        Assert.Single(events.OfType<ProjectBecameActive>(), e => e.ProjectId == "proj.a");
        Assert.Single(events.OfType<ProjectBecameActive>(), e => e.ProjectId == "proj.b");
        Assert.DoesNotContain(events, e => e is ProjectCompleted done && done.ProjectId == "proj.b");
    }

    [Fact]
    public void Allocation_SecondCredit_CompletesNextAtExactlyCost_NothingLeftBanked()
    {
        var game = CreateQueuedGame(out var content);
        var events = new List<SimulationEvent>();

        CreditAndAllocate(game, content, T0, "tx-0100", 400L, events);
        CreditAndAllocate(game, content, T0.AddMinutes(30), "tx-0101", 400L, events);

        var b = game.Region.FindProject("proj.b")!;
        Assert.Equal(ProjectStatus.Completed, b.Status);
        Assert.Equal(500L, b.VitalityInvested);
        Assert.Null(game.Queue.ActiveProjectId);
        Assert.Empty(game.Queue.QueuedProjectIds);
        Assert.Equal(0L, game.Resources.Get(ResourceType.Vitality));

        Assert.Equal(2, events.OfType<ProjectCompleted>().Count());
        Assert.Single(events.OfType<ProjectCompleted>(), e => e.ProjectId == "proj.a");
        Assert.Single(events.OfType<ProjectCompleted>(), e => e.ProjectId == "proj.b");
        Assert.Single(events.OfType<ProjectBecameActive>(), e => e.ProjectId == "proj.b");
        Assert.Single(events.OfType<ProjectBecameAvailable>(), e => e.ProjectId == "proj.c");
        Assert.Equal(ProjectStatus.Available, game.Region.FindProject("proj.c")!.Status);
    }

    [Fact]
    public void Allocation_AutoAdvanceDisabled_NextStaysQueued_AndSurplusStaysBanked()
    {
        var content = CreateContent();
        var game = GameFactory.NewGame(content, T0, 42UL);
        game.Region.FindProject("proj.a")!.Status = ProjectStatus.Active;
        game.Queue.ActiveProjectId = "proj.a";
        game.Region.FindProject("proj.b")!.Status = ProjectStatus.Queued;
        game.Queue.QueuedProjectIds.Add("proj.b");
        game.Queue.AutoAdvance = false;

        var events = new List<SimulationEvent>();
        CreditAndAllocate(game, content, T0, "tx-0200", 400L, events);

        Assert.Equal(ProjectStatus.Completed, game.Region.FindProject("proj.a")!.Status);
        Assert.Equal(300L, game.Region.FindProject("proj.a")!.VitalityInvested);
        Assert.Equal(100L, game.Resources.Get(ResourceType.Vitality));
        Assert.Null(game.Queue.ActiveProjectId);
        Assert.Equal(new[] { "proj.b" }, game.Queue.QueuedProjectIds);
        Assert.Equal(ProjectStatus.Queued, game.Region.FindProject("proj.b")!.Status);
        Assert.DoesNotContain(events, e => e is ProjectBecameActive);
        Assert.Single(events.OfType<ProjectCompleted>());
    }
}
