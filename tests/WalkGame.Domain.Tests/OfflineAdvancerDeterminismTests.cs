using System;
using System.Collections.Generic;
using System.Linq;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Simulation;
using LandmarkId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.LandmarkIdKind>;
using ProjectId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProjectIdKind>;
using ProducerId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProducerIdKind>;
using RegionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RegionIdKind>;
using RewardTransactionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RewardTransactionIdKind>;

namespace WalkGame.Domain.Tests;

public class OfflineAdvancerDeterminismTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 1, 6, 0, 0, TimeSpan.Zero);

    private static RegionDefinition CreateContent()
    {
        var a = new ProjectDefinition(new ProjectId("proj.a"), "A", 300L);
        var b = new ProjectDefinition(new ProjectId("proj.b"), "B", 500L, new[] { new ProjectId("proj.a") });
        var c = new ProjectDefinition(new ProjectId("proj.c"), "C", 200L, new[] { new ProjectId("proj.b") });
        var gate = new LandmarkDefinition(new LandmarkId("land.gate"), "Gate", new[]
        {
            new LandmarkStageDefinition(RestorationStage.Stabilized, "proj.a"),
            new LandmarkStageDefinition(RestorationStage.Functional, "proj.c"),
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

    private static void Credit(GameState game, RegionDefinition content, DateTimeOffset at, string txId, long vitality, List<SimulationEvent> events)
    {
        Assert.Equal(
            LedgerApplyOutcome.AppliedFirstTime,
            game.Ledger.Apply(new RewardTransaction(new RewardTransactionId(txId), at, vitality, "walk"), game.Resources));
        OfflineAdvancer.Advance(game, content, at, events);
    }

    private static (GameState Game, RegionDefinition Content) RunScript(List<SimulationEvent> events)
    {
        var content = CreateContent();
        var game = GameFactory.NewGame(content, T0, 2026UL);

        game.Region.FindProject("proj.a")!.Status = ProjectStatus.Queued;
        game.Region.FindProject("proj.b")!.Status = ProjectStatus.Queued;
        game.Queue.QueuedProjectIds.Add("proj.a");
        game.Queue.QueuedProjectIds.Add("proj.b");

        Credit(game, content, T0.AddHours(1), "tx-7001", 400L, events);
        Credit(game, content, T0.AddHours(2), "tx-7002", 250L, events);
        Credit(game, content, T0.AddHours(5), "tx-7003", 150L, events);
        OfflineAdvancer.Advance(game, content, T0.AddHours(9), events);

        return (game, content);
    }

    private static void AssertDeepEqual(GameState expected, GameState actual)
    {
        foreach (ResourceType type in Enum.GetValues<ResourceType>())
        {
            Assert.Equal(expected.Resources.Get(type), actual.Resources.Get(type));
            Assert.Equal(expected.Resources.GetCap(type), actual.Resources.GetCap(type));
        }

        var keys = expected.Region.Projects.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.Equal(keys, actual.Region.Projects.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList());
        foreach (var key in keys)
        {
            var e = expected.Region.Projects[key];
            var a = actual.Region.Projects[key];
            Assert.Equal(e.Status, a.Status);
            Assert.Equal(e.VitalityInvested, a.VitalityInvested);
            Assert.Equal(e.CompletedAtUtc, a.CompletedAtUtc);
            Assert.Equal(e.ProjectId, a.ProjectId);
        }

        Assert.Equal(
            expected.Region.LandmarkStages.OrderBy(p => p.Key, StringComparer.Ordinal).ToList(),
            actual.Region.LandmarkStages.OrderBy(p => p.Key, StringComparer.Ordinal).ToList());

        Assert.Equal(expected.Region.Producers.Count, actual.Region.Producers.Count);
        for (int i = 0; i < expected.Region.Producers.Count; i++)
        {
            var e = expected.Region.Producers[i];
            var a = actual.Region.Producers[i];
            Assert.Equal(e.ProducerId, a.ProducerId);
            Assert.Equal(e.Unlocked, a.Unlocked);
            Assert.Equal(e.StoredMilliUnits, a.StoredMilliUnits);
            Assert.Equal(e.TotalProducedMilliUnits, a.TotalProducedMilliUnits);
            Assert.Equal(e.LastTickUtc, a.LastTickUtc);
        }

        Assert.Equal(expected.Queue.QueuedProjectIds, actual.Queue.QueuedProjectIds);
        Assert.Equal(expected.Queue.ActiveProjectId, actual.Queue.ActiveProjectId);
        Assert.Equal(expected.Queue.AutoAdvance, actual.Queue.AutoAdvance);

        Assert.Equal(expected.Ledger.Records.Count, actual.Ledger.Records.Count);
        for (int i = 0; i < expected.Ledger.Records.Count; i++)
        {
            Assert.Equal(expected.Ledger.Records[i].TransactionId, actual.Ledger.Records[i].TransactionId);
            Assert.Equal(expected.Ledger.Records[i].VitalityAmount, actual.Ledger.Records[i].VitalityAmount);
        }
        Assert.Equal(expected.Ledger.TotalVitalityCredited, actual.Ledger.TotalVitalityCredited);

        Assert.Equal(expected.SchemaVersion, actual.SchemaVersion);
        Assert.Equal(expected.CreatedAtUtc, actual.CreatedAtUtc);
        Assert.Equal(expected.LastAdvancedUtc, actual.LastAdvancedUtc);
        Assert.Equal(expected.Rng, actual.Rng);
    }

    private static (long Materials, long ProducedMilli, DateTimeOffset LastAdvanced) Observe(GameState game) =>
        (
            game.Resources.Get(ResourceType.Materials),
            game.Region.Producers.Sum(p => p.TotalProducedMilliUnits),
            game.LastAdvancedUtc
        );

    [Fact]
    public void ScriptedScenario_RunTwiceFromFreshStates_ProducesDeepEqualStates()
    {
        var firstEvents = new List<SimulationEvent>();
        var (first, _) = RunScript(firstEvents);
        var secondEvents = new List<SimulationEvent>();
        var (second, _) = RunScript(secondEvents);

        AssertDeepEqual(first, second);

        Assert.Equal(ProjectStatus.Completed, first.Region.FindProject("proj.a")!.Status);
        Assert.Equal(ProjectStatus.Completed, first.Region.FindProject("proj.b")!.Status);
        Assert.Equal(ProjectStatus.Available, first.Region.FindProject("proj.c")!.Status);
        Assert.Equal(RestorationStage.Stabilized, first.Region.LandmarkStages["land.gate"]);
        Assert.True(first.Region.FindProducer("prod.mill")!.Unlocked);

        var mill = first.Region.FindProducer("prod.mill")!;
        long gainedMilli = firstEvents.OfType<ProducerProduced>().Sum(e => e.MilliUnitsGained);
        Assert.True(gainedMilli > 0);
        Assert.Equal(gainedMilli, mill.TotalProducedMilliUnits);
        Assert.Equal(gainedMilli / 1000L, first.Resources.Get(ResourceType.Materials));
        Assert.Equal(0L, mill.StoredMilliUnits);

        Assert.Equal(3, first.Ledger.Records.Count);
        Assert.Equal(800L, first.Ledger.TotalVitalityCredited);
    }

    [Fact]
    public void Advance_AtExactLastTimestamp_IsSafeNoOp()
    {
        var events = new List<SimulationEvent>();
        var (game, content) = RunScript(events);
        var before = Observe(game);

        OfflineAdvancer.Advance(game, content, game.LastAdvancedUtc, events);

        Assert.DoesNotContain(events, e => e is ClockSkewIgnored);
        Assert.Equal(before, Observe(game));
        Assert.Equal(T0.AddHours(9), game.LastAdvancedUtc);
    }

    [Fact]
    public void BackwardAdvance_EmitsClockSkew_RegressesNothing_AndForwardAdvanceResumes()
    {
        var events = new List<SimulationEvent>();
        var (game, content) = RunScript(events);
        var before = Observe(game);
        var skewEvents = new List<SimulationEvent>();
        var backMoment = game.LastAdvancedUtc.AddHours(-3);

        OfflineAdvancer.Advance(game, content, backMoment, skewEvents);

        var skew = Assert.Single(skewEvents.OfType<ClockSkewIgnored>());
        Assert.Equal(TimeSpan.FromHours(3), skew.AttemptedBackstep);
        Assert.Equal(backMoment, skew.AtUtc);
        Assert.Equal(before, Observe(game));

        var resumeEvents = new List<SimulationEvent>();
        var resumeMoment = backMoment.AddHours(6);
        OfflineAdvancer.Advance(game, content, resumeMoment, resumeEvents);

        Assert.DoesNotContain(resumeEvents, e => e is ClockSkewIgnored);
        Assert.Equal(resumeMoment, game.LastAdvancedUtc);
        Assert.Equal(before.Materials + 3L, game.Resources.Get(ResourceType.Materials));
        Assert.Equal(before.ProducedMilli + 3000L, game.Region.Producers.Sum(p => p.TotalProducedMilliUnits));
    }

    [Fact]
    public void RepeatedAdvance_AtResumeTimestamp_RemainsSafe()
    {
        var events = new List<SimulationEvent>();
        var (game, content) = RunScript(events);
        var backMoment = game.LastAdvancedUtc.AddHours(-3);
        OfflineAdvancer.Advance(game, content, backMoment, events);

        var resumeEvents = new List<SimulationEvent>();
        var resumeMoment = backMoment.AddHours(6);
        OfflineAdvancer.Advance(game, content, resumeMoment, resumeEvents);
        var afterResume = Observe(game);

        OfflineAdvancer.Advance(game, content, resumeMoment, resumeEvents);
        OfflineAdvancer.Advance(game, content, resumeMoment, resumeEvents);

        Assert.DoesNotContain(resumeEvents, e => e is ClockSkewIgnored);
        Assert.Equal(afterResume, Observe(game));
        Assert.Equal(resumeMoment, game.LastAdvancedUtc);
    }
}
