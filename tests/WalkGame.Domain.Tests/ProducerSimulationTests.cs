using System;
using System.Collections.Generic;
using System.Linq;
using WalkGame.Domain;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Simulation;
using ProjectId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProjectIdKind>;
using ProducerId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProducerIdKind>;
using RegionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RegionIdKind>;

namespace WalkGame.Domain.Tests;

public class ProducerSimulationTests
{
    private const long MilliPerDay = 24000L;
    private static readonly DateTimeOffset T0 = new(2026, 6, 15, 0, 0, 0, TimeSpan.Zero);

    private static RegionDefinition CreateContent()
    {
        var unlocker = new ProjectDefinition(new ProjectId("proj.unlock"), "Unlock", 100L);
        var mill = new ProducerDefinition(
            new ProducerId("prod.mill"), "Mill", ResourceType.Materials,
            MilliPerDay, 1_000_000L, "proj.unlock");
        return new RegionDefinition(
            new RegionId("region.test"), "Test Region",
            new[] { unlocker },
            Array.Empty<LandmarkDefinition>(),
            new[] { mill });
    }

    private static (GameState Game, RegionDefinition Content) CreateUnlockedProducer()
    {
        var content = CreateContent();
        var game = GameFactory.NewGame(content, T0, 42UL);
        var runtime = game.Region.FindProducer("prod.mill")!;
        runtime.Unlocked = true;
        runtime.LastTickUtc = T0;
        return (game, content);
    }

    private static long ExpectedMilliUnits(long cappedTicks) =>
        (cappedTicks / TimeSpan.TicksPerDay) * MilliPerDay
        + (cappedTicks % TimeSpan.TicksPerDay) * MilliPerDay / TimeSpan.TicksPerDay;

    [Fact]
    public void Tick_HalfDay_ProducesExactIntegerShare_WithCarryPreserved()
    {
        var (game, content) = CreateUnlockedProducer();
        var events = new List<SimulationEvent>();
        var now = T0.AddHours(12);

        OfflineAdvancer.TickProducers(game, content, now, events);

        long elapsedTicks = now.UtcTicks - T0.UtcTicks;
        long cappedTicks = Math.Min(elapsedTicks, OfflineAdvancer.MaxProducerInterval.Ticks);
        long expectedMilli = ExpectedMilliUnits(cappedTicks);

        var runtime = game.Region.FindProducer("prod.mill")!;
        Assert.Equal(expectedMilli, runtime.TotalProducedMilliUnits);
        Assert.Equal(expectedMilli / 1000L, game.Resources.Get(ResourceType.Materials));
        Assert.Equal((runtime.CarryMilliUnits + expectedMilli) % 1000L, runtime.CarryMilliUnits);
        Assert.Equal(0L, runtime.CarryMilliUnits);
        Assert.Equal(now, runtime.LastTickUtc);

        var produced = Assert.Single(events.OfType<ProducerProduced>());
        Assert.Equal(12000L, produced.MilliUnitsGained);
        Assert.False(produced.HitCapacity);
    }

    [Fact]
    public void Tick_TwoHalfDays_MatchesOneFullDay_Total()
    {
        var (splitGame, splitContent) = CreateUnlockedProducer();
        var (fullGame, fullContent) = CreateUnlockedProducer();
        var splitEvents = new List<SimulationEvent>();
        var fullEvents = new List<SimulationEvent>();

        OfflineAdvancer.TickProducers(splitGame, splitContent, T0.AddHours(12), splitEvents);
        OfflineAdvancer.TickProducers(splitGame, splitContent, T0.AddHours(24), splitEvents);
        OfflineAdvancer.TickProducers(fullGame, fullContent, T0.AddHours(24), fullEvents);

        Assert.Equal(24000L, splitGame.Region.FindProducer("prod.mill")!.TotalProducedMilliUnits);
        Assert.Equal(
            fullGame.Region.FindProducer("prod.mill")!.TotalProducedMilliUnits,
            splitGame.Region.FindProducer("prod.mill")!.TotalProducedMilliUnits);
        Assert.Equal(24L, splitGame.Resources.Get(ResourceType.Materials));
        Assert.Equal(
            fullGame.Resources.Get(ResourceType.Materials),
            splitGame.Resources.Get(ResourceType.Materials));
        Assert.Equal(
            splitGame.Region.FindProducer("prod.mill")!.CarryMilliUnits,
            fullGame.Region.FindProducer("prod.mill")!.CarryMilliUnits);
    }

    [Fact]
    public void Tick_ZeroElapsed_NoProduction_AndNoEvents()
    {
        var (game, content) = CreateUnlockedProducer();
        var events = new List<SimulationEvent>();

        OfflineAdvancer.TickProducers(game, content, T0, events);

        var runtime = game.Region.FindProducer("prod.mill")!;
        Assert.Empty(events);
        Assert.Equal(0L, runtime.TotalProducedMilliUnits);
        Assert.Equal(0L, runtime.CarryMilliUnits);
        Assert.Equal(0L, game.Resources.Get(ResourceType.Materials));
        Assert.Equal(T0, runtime.LastTickUtc);
    }

    [Fact]
    public void Tick_BackwardClock_NoProduction_LastTickUpdatedToSkewedMoment()
    {
        var (game, content) = CreateUnlockedProducer();
        var events = new List<SimulationEvent>();
        var backward = T0.AddHours(-2);

        OfflineAdvancer.TickProducers(game, content, backward, events);

        var runtime = game.Region.FindProducer("prod.mill")!;
        Assert.Empty(events);
        Assert.Equal(0L, runtime.TotalProducedMilliUnits);
        Assert.Equal(0L, game.Resources.Get(ResourceType.Materials));
        Assert.Equal(backward, runtime.LastTickUtc);
    }

    [Fact]
    public void Tick_ResourceCapExhausted_AppliesOnlyRoom_AndReportsHitCapacity()
    {
        var (game, content) = CreateUnlockedProducer();
        game.Resources.SetCap(ResourceType.Materials, 5L);
        var events = new List<SimulationEvent>();

        OfflineAdvancer.TickProducers(game, content, T0.AddHours(12), events);

        var runtime = game.Region.FindProducer("prod.mill")!;
        Assert.Equal(5L, game.Resources.Get(ResourceType.Materials));
        Assert.Equal(5000L, runtime.TotalProducedMilliUnits);
        var produced = Assert.Single(events.OfType<ProducerProduced>());
        Assert.True(produced.HitCapacity);
        Assert.Equal(5000L, produced.MilliUnitsGained);
    }
}
