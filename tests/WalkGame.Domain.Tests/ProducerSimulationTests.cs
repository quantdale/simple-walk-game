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

    private static RegionDefinition CreateContent(long capacityUnits = 1_000_000L)
    {
        var unlocker = new ProjectDefinition(new ProjectId("proj.unlock"), "Unlock", 100L);
        var mill = new ProducerDefinition(
            new ProducerId("prod.mill"), "Mill", ResourceType.Materials,
            MilliPerDay, capacityUnits, "proj.unlock");
        return new RegionDefinition(
            new RegionId("region.test"), "Test Region",
            new[] { unlocker },
            Array.Empty<LandmarkDefinition>(),
            new[] { mill });
    }

    private static (GameState Game, RegionDefinition Content) CreateUnlockedProducer(long capacityUnits = 1_000_000L)
    {
        var content = CreateContent(capacityUnits);
        var game = GameFactory.NewGame(content, T0, 42UL);
        var runtime = game.Region.FindProducer("prod.mill")!;
        runtime.Unlocked = true;
        runtime.LastTickUtc = T0;
        return (game, content);
    }

    [Fact]
    public void Tick_HalfDay_ProducesExactIntegerShare_AndEmptiesStore()
    {
        var (game, content) = CreateUnlockedProducer();
        var events = new List<SimulationEvent>();
        var now = T0.AddHours(12);

        OfflineAdvancer.TickProducers(game, content, now, events);

        var runtime = game.Region.FindProducer("prod.mill")!;
        Assert.Equal(12000L, runtime.TotalProducedMilliUnits);
        Assert.Equal(12L, game.Resources.Get(ResourceType.Materials));
        Assert.Equal(0L, runtime.StoredMilliUnits);
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
            fullGame.Resources.Get(ResourceType.Materials));
        Assert.Equal(
            splitGame.Region.FindProducer("prod.mill")!.StoredMilliUnits,
            fullGame.Region.FindProducer("prod.mill")!.StoredMilliUnits);
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
        Assert.Equal(0L, runtime.StoredMilliUnits);
        Assert.Equal(0L, game.Resources.Get(ResourceType.Materials));
        Assert.Equal(T0, runtime.LastTickUtc);
    }

    [Fact]
    public void Tick_BackwardClock_NoProduction_CheckpointNotRegressed_SkewReported()
    {
        var (game, content) = CreateUnlockedProducer();
        var events = new List<SimulationEvent>();
        var backward = T0.AddHours(-2);

        OfflineAdvancer.TickProducers(game, content, backward, events);

        var runtime = game.Region.FindProducer("prod.mill")!;
        Assert.Empty(events.OfType<ProducerProduced>());
        Assert.Equal(0L, runtime.TotalProducedMilliUnits);
        Assert.Equal(0L, game.Resources.Get(ResourceType.Materials));
        // Monotonic checkpoint defense: a skewed clock may never backdate a producer,
        // because a later forward tick would otherwise mint from the regressed instant.
        Assert.Equal(T0, runtime.LastTickUtc);
        Assert.Single(events.OfType<ClockSkewIgnored>());

        // A subsequent correct tick produces only from the original checkpoint onward.
        var later = new List<SimulationEvent>();
        OfflineAdvancer.TickProducers(game, content, T0.AddHours(24), later);
        Assert.Equal(24000L, game.Region.FindProducer("prod.mill")!.TotalProducedMilliUnits);
    }

    [Fact]
    public void Tick_ResourceCapExhausted_ParksSurplus_InBoundedStore()
    {
        var (game, content) = CreateUnlockedProducer();
        game.Resources.SetCap(ResourceType.Materials, 5L);
        var events = new List<SimulationEvent>();

        OfflineAdvancer.TickProducers(game, content, T0.AddHours(12), events);

        var runtime = game.Region.FindProducer("prod.mill")!;
        Assert.Equal(5L, game.Resources.Get(ResourceType.Materials));
        Assert.Equal(5000L, runtime.TotalProducedMilliUnits);
        // The 7 surplus units are parked in the producer's bounded store, not destroyed.
        Assert.Equal(7000L, runtime.StoredMilliUnits);
        var produced = Assert.Single(events.OfType<ProducerProduced>());
        Assert.True(produced.HitCapacity);
        Assert.Equal(5000L, produced.MilliUnitsGained);
    }

    [Fact]
    public void ParkedUnits_FlushOnZeroElapsedTick_WhenResourceSpaceFrees()
    {
        var (game, content) = CreateUnlockedProducer();
        game.Resources.SetCap(ResourceType.Materials, 5L);
        OfflineAdvancer.TickProducers(game, content, T0.AddHours(12), new List<SimulationEvent>());
        Assert.Equal(7000L, game.Region.FindProducer("prod.mill")!.StoredMilliUnits);

        game.Resources.SetCap(ResourceType.Materials, 1000L);
        var events = new List<SimulationEvent>();
        // No time passes — delivery alone must move the parked units downstream.
        OfflineAdvancer.TickProducers(game, content, T0.AddHours(12), events);

        var runtime = game.Region.FindProducer("prod.mill")!;
        Assert.Equal(0L, runtime.StoredMilliUnits);
        Assert.Equal(12L, game.Resources.Get(ResourceType.Materials));
        Assert.Equal(12000L, runtime.TotalProducedMilliUnits);
        var produced = Assert.Single(events.OfType<ProducerProduced>());
        Assert.Equal(7000L, produced.MilliUnitsGained);
        Assert.False(produced.HitCapacity);
    }

    [Fact]
    public void Tick_StoreCapacityBoundsProduction_PerTick_EvenOverLongAbsence()
    {
        long capacityUnits = 20L;
        var (game, content) = CreateUnlockedProducer(capacityUnits);
        var events = new List<SimulationEvent>();

        // Ten days would earn 240 units unbounded; the store caps each tick's minting
        // to its remaining room, so exactly one store's worth can ever appear per tick.
        OfflineAdvancer.TickProducers(game, content, T0.AddDays(10), events);

        var runtime = game.Region.FindProducer("prod.mill")!;
        Assert.Equal(capacityUnits, game.Resources.Get(ResourceType.Materials));
        Assert.Equal(0L, runtime.StoredMilliUnits);
        Assert.True(events.OfType<ProducerProduced>().Single().HitCapacity);

        // A further 30-day absence yields at most one more bounded store, never the raw rate.
        OfflineAdvancer.TickProducers(game, content, T0.AddDays(40), events);
        Assert.Equal(capacityUnits * 2L, game.Resources.Get(ResourceType.Materials));
        Assert.Equal(0L, runtime.StoredMilliUnits);
    }

    [Fact]
    public void UnlockTimestamp_PreventsRetroactiveProduction()
    {
        var content = CreateContent();
        var game = GameFactory.NewGame(content, T0, 42UL);

        // The unlock project completes at T0+5d through normal allocation.
        game.Region.FindProject("proj.unlock")!.Status = ProjectStatus.Active;
        game.Queue.ActiveProjectId = "proj.unlock";
        game.Resources.Add(ResourceType.Vitality, 100L);

        var events = new List<SimulationEvent>();
        OfflineAdvancer.Advance(game, content, T0.AddDays(5), events);

        Assert.Contains(events, e => e is ProjectCompleted);
        Assert.Contains(events, e => e is ProducerUnlocked);

        var runtime = game.Region.FindProducer("prod.mill")!;
        Assert.True(runtime.Unlocked);
        // Checkpoint starts at the unlock instant: no production is retroactively
        // credited for the five days before the producer existed.
        Assert.Equal(T0.AddDays(5), runtime.LastTickUtc);
    }

    [Fact]
    public void LockedProducer_NeverMints_EvenOverLongAbsence()
    {
        var content = CreateContent();
        var game = GameFactory.NewGame(content, T0, 42UL);

        OfflineAdvancer.TickProducers(game, content, T0.AddDays(30), new List<SimulationEvent>());

        var runtime = game.Region.FindProducer("prod.mill")!;
        Assert.False(runtime.Unlocked);
        Assert.Equal(0L, runtime.TotalProducedMilliUnits);
        Assert.Equal(0L, runtime.StoredMilliUnits);
    }
}
