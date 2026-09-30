using System;
using System.Collections.Generic;
using System.Linq;
using WalkGame.Domain;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Simulation;
using WalkGame.Domain.Validation;
using ProjectId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProjectIdKind>;
using ProducerId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProducerIdKind>;
using RegionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RegionIdKind>;

namespace WalkGame.Domain.Tests;

/// <summary>
/// D-054 regression coverage for the bounded secondary-resource economy, PURE DOMAIN.
///
/// The audit that produced this change found the cap/parking machinery fully
/// implemented, fully tested, and **completely unreachable in production**:
/// <c>SetCap</c> had zero call sites outside tests, so <c>GetCap</c> always returned
/// <c>long.MaxValue</c>, so the <c>hitResourceCap</c> branch in
/// <c>OfflineAdvancer.DeliverStoredOutput</c> never fired and Materials and Knowledge
/// grew forever with nothing to spend them on.
///
/// These tests use a self-contained region fixture rather than the shipped catalog,
/// because the Domain test project references only Domain. The shipped-catalog
/// reachability proof lives in the Application suite
/// (<c>SecondaryResourceEconomyIntegrationTests</c>), where <c>Region1Catalog</c>
/// is actually reachable.
/// </summary>
public sealed class SecondaryResourceEconomyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);

    // ==================================================================
    // 2.1 / 2.2 Parking is REACHABLE and lossless, through the real tick.
    // ==================================================================

    [Fact]
    public void ProducerAtItsResourceCap_ParksSurplusInItsBoundedStore()
    {
        // Without this, the whole "park and flush" mechanism is dead code again.
        var content = Region(capUnits: 10L);
        var game = GameFactory.NewGame(content, T0, seed: 7UL);
        UnlockAllProducers(game, content);

        for (int day = 1; day <= 60; day++)
            OfflineAdvancer.TickProducers(game, content, T0.AddDays(day), new List<SimulationEvent>());

        var producer = content.Producers[0];
        var runtime = game.Region.FindProducer(producer.Id.Value)!;

        // The balance stops AT the cap, never above it.
        Assert.Equal(10L, game.Resources.Get(ResourceType.Materials));

        // The refused output is waiting in the producer's own bounded store, not lost.
        Assert.True(runtime.StoredMilliUnits > 0L,
            "output refused by the cap must be retained in the producer's pending store");

        long storeCapacity = producer.CapacityUnits * ProducerDefinition.MilliUnitsPerUnit;
        Assert.InRange(runtime.StoredMilliUnits, 0L, storeCapacity);
    }

    [Fact]
    public void ParkedOutput_FlushesAutomaticallyOnATickWithNoElapsedTime_NoPlayerClaim()
    {
        var content = Region(capUnits: 10L);
        var game = GameFactory.NewGame(content, T0, seed: 7UL);
        UnlockAllProducers(game, content);

        for (int day = 1; day <= 60; day++)
            OfflineAdvancer.TickProducers(game, content, T0.AddDays(day), new List<SimulationEvent>());

        var producer = content.Producers[0];
        var runtime = game.Region.FindProducer(producer.Id.Value)!;
        long parkedBefore = runtime.StoredMilliUnits;
        Assert.True(parkedBefore / ProducerDefinition.MilliUnitsPerUnit > 0L, "precondition: units must be parked");

        // The ONLY relief valve is the player spending the resource.
        Assert.True(game.Resources.TryConsume(ResourceType.Materials, 10L));

        // A tick with NO elapsed time at all: delivery alone must move the parked units.
        OfflineAdvancer.TickProducers(game, content, T0.AddDays(60), new List<SimulationEvent>());

        Assert.True(runtime.StoredMilliUnits < parkedBefore,
            "parked units must flush downward as soon as headroom exists, with no player action");
        Assert.Equal(10L, game.Resources.Get(ResourceType.Materials)); // refilled to the cap
    }

    [Fact]
    public void FullPendingStore_DiscardsNoFractionalRemainder()
    {
        // 2.2 "no waste": with both the store and the resource full, the fractional
        // remainder already parked must survive rather than being re-lost each tick.
        var content = Region(capUnits: 10L);
        var game = GameFactory.NewGame(content, T0, seed: 7UL);
        UnlockAllProducers(game, content);

        for (int day = 1; day <= 500; day++)
            OfflineAdvancer.TickProducers(game, content, T0.AddDays(day), new List<SimulationEvent>());

        var producer = content.Producers[0];
        var runtime = game.Region.FindProducer(producer.Id.Value)!;
        long remainderBefore = runtime.StoredMilliUnits % ProducerDefinition.MilliUnitsPerUnit;

        for (int day = 501; day <= 600; day++)
            OfflineAdvancer.TickProducers(game, content, T0.AddDays(day), new List<SimulationEvent>());

        long storeCapacity = producer.CapacityUnits * ProducerDefinition.MilliUnitsPerUnit;
        Assert.InRange(runtime.StoredMilliUnits, 0L, storeCapacity);
        Assert.True(runtime.StoredMilliUnits >= 0L);
        Assert.Equal(remainderBefore, runtime.StoredMilliUnits % ProducerDefinition.MilliUnitsPerUnit);
    }

    // ==================================================================
    // 4.1 / 4.2 Secondary requirements gate ACTIVATION, all-or-nothing.
    // ==================================================================

    [Fact]
    public void Requirement_IsConsumedExactlyOnceOnActivation()
    {
        var content = Region(capUnits: 10_000L, requirement: 40L);
        var game = GameFactory.NewGame(content, T0, seed: 7UL);

        var needy = content.FindProject("proj.needy")!;
        Assert.Equal(40L, needy.ResourceCost(ResourceType.Materials));

        // Queued but NOT funded: nothing may be consumed and it may not activate.
        game.Region.FindProject(needy.Id.Value)!.Status = ProjectStatus.Queued;
        game.Queue.QueuedProjectIds.Add(needy.Id.Value);
        game.Resources.Add(ResourceType.Materials, 39L);

        Assert.False(OfflineAdvancer.CanAffordRequirements(game, needy));
        Assert.False(OfflineAdvancer.ConsumeRequirements(game, needy));
        Assert.Equal(39L, game.Resources.Get(ResourceType.Materials));

        OfflineAdvancer.AllocateVitality(game, content, T0, new List<SimulationEvent>());
        Assert.Null(game.Queue.ActiveProjectId);
        Assert.Equal(39L, game.Resources.Get(ResourceType.Materials));

        // Fund the last unit; the next tick activates it and consumes exactly the cost.
        game.Resources.Add(ResourceType.Materials, 1L);
        OfflineAdvancer.AllocateVitality(game, content, T0.AddHours(1), new List<SimulationEvent>());

        Assert.Equal(needy.Id.Value, game.Queue.ActiveProjectId);
        Assert.Equal(0L, game.Resources.Get(ResourceType.Materials));

        // Further ticks must not charge the requirement a second time.
        for (int hour = 2; hour <= 5; hour++)
            OfflineAdvancer.AllocateVitality(game, content, T0.AddHours(hour), new List<SimulationEvent>());
        Assert.Equal(0L, game.Resources.Get(ResourceType.Materials));
    }

    [Fact]
    public void ConsumeRequirements_IsAllOrNothing_WhenOneOfSeveralCostsIsShort()
    {
        var content = Region(capUnits: 10_000L);
        var game = GameFactory.NewGame(content, T0, seed: 7UL);

        var twoCost = new ProjectDefinition(
            new ProjectId("proj.two-cost"), "Two Cost", 100L,
            resourceCosts: new[]
            {
                new KeyValuePair<ResourceType, long>(ResourceType.Materials, 10L),
                new KeyValuePair<ResourceType, long>(ResourceType.Knowledge, 50L),
            });

        game.Resources.Add(ResourceType.Materials, 100L);
        game.Resources.Add(ResourceType.Knowledge, 5L); // short on the second cost only

        Assert.False(OfflineAdvancer.CanAffordRequirements(game, twoCost));
        Assert.False(OfflineAdvancer.ConsumeRequirements(game, twoCost));

        // A partial deduction must not be representable: neither cost was touched.
        Assert.Equal(100L, game.Resources.Get(ResourceType.Materials));
        Assert.Equal(5L, game.Resources.Get(ResourceType.Knowledge));
    }

    // ==================================================================
    // 4.3 Auto-advance stalls at an unaffordable head of queue and never
    //     skips past it; Vitality keeps banking.
    // ==================================================================

    [Fact]
    public void AutoAdvance_StallsAtAnUnaffordableHeadOfQueue_AndDoesNotSkipPastIt()
    {
        var content = Region(capUnits: 10_000L, requirement: 40L);
        var game = GameFactory.NewGame(content, T0, seed: 7UL);
        game.Queue.AutoAdvance = true;

        var needy = content.FindProject("proj.needy")!;
        var entry = content.FindProject("proj.entry")!;

        // Requirement-bearing project FIRST, an ordinary one behind it.
        game.Region.FindProject(needy.Id.Value)!.Status = ProjectStatus.Queued;
        game.Region.FindProject(entry.Id.Value)!.Status = ProjectStatus.Queued;
        game.Queue.QueuedProjectIds.Add(needy.Id.Value);
        game.Queue.QueuedProjectIds.Add(entry.Id.Value);

        Assert.Equal(0L, game.Resources.Get(ResourceType.Materials));

        OfflineAdvancer.AllocateVitality(game, content, T0, new List<SimulationEvent>());

        Assert.Null(game.Queue.ActiveProjectId);
        Assert.Equal(ProjectStatus.Queued, game.Region.FindProject(needy.Id.Value)!.Status);
        Assert.Contains(needy.Id.Value, game.Queue.QueuedProjectIds);
        Assert.Contains(entry.Id.Value, game.Queue.QueuedProjectIds);

        // Vitality BANKS rather than being spent on the project behind it.
        game.Resources.Add(ResourceType.Vitality, 5_000L);
        OfflineAdvancer.AllocateVitality(game, content, T0.AddHours(1), new List<SimulationEvent>());

        Assert.Null(game.Queue.ActiveProjectId);
        Assert.Equal(ProjectStatus.Queued, game.Region.FindProject(entry.Id.Value)!.Status);
        Assert.Equal(5_000L, game.Resources.Get(ResourceType.Vitality));
    }

    [Fact]
    public void AutoAdvance_ProceedsOnceTheRequirementIsFundable_NoPlayerAction()
    {
        var content = Region(capUnits: 10_000L, requirement: 40L);
        var game = GameFactory.NewGame(content, T0, seed: 7UL);
        game.Queue.AutoAdvance = true;

        var needy = content.FindProject("proj.needy")!;
        game.Region.FindProject(needy.Id.Value)!.Status = ProjectStatus.Queued;
        game.Queue.QueuedProjectIds.Add(needy.Id.Value);

        OfflineAdvancer.AllocateVitality(game, content, T0, new List<SimulationEvent>());
        Assert.Null(game.Queue.ActiveProjectId);

        // The resources arrive on their own, the way producer output would.
        game.Resources.Add(ResourceType.Materials, 40L);
        OfflineAdvancer.AllocateVitality(game, content, T0.AddHours(1), new List<SimulationEvent>());

        Assert.Equal(needy.Id.Value, game.Queue.ActiveProjectId);
        Assert.Equal(0L, game.Resources.Get(ResourceType.Materials));
    }

    // ==================================================================
    // 4.5 Content validation for caps and requirement satisfiability.
    // ==================================================================

    [Fact]
    public void ContentValidator_AcceptsAWellFormedBoundedRegion()
    {
        Assert.Empty(ContentValidator.Validate(Region(capUnits: 10_000L, requirement: 40L)));
    }

    [Fact]
    public void ContentValidator_RejectsAProducedResourceWithNoAuthoredCap()
    {
        var content = Region(capUnits: 1L, withCaps: false);
        var violations = ContentValidator.Validate(content);

        Assert.Contains(violations, v => v.Contains("Materials") && v.Contains("no cap"));
    }

    [Fact]
    public void ContentValidator_RejectsAnUnsatisfiableRequirement()
    {
        // The fixture producer makes 2 whole units/day (2400 milli-units), i.e. ~800 units
        // across the 400-day design horizon. A 900-unit requirement is BELOW the authored
        // cap (so this is not the "exceeds the cap" rule) yet above what the region can
        // ever produce, so the project would be permanently unactivatable.
        var content = Region(capUnits: 10_000L, requirement: 900L);
        var violations = ContentValidator.Validate(content);

        Assert.Contains(violations, v => v.Contains("proj.needy") && v.Contains("Materials"));
    }

    [Fact]
    public void ContentValidator_RejectsARequirementAboveTheAuthoredCap()
    {
        var content = Region(capUnits: 10L, requirement: 500L);
        var violations = ContentValidator.Validate(content);

        Assert.Contains(violations, v => v.Contains("proj.needy") && v.Contains("exceeds"));
    }

    [Fact]
    public void ContentValidator_RejectsARequirementForAResourceNobodyProduces()
    {
        var content = Region(capUnits: 10_000L, requirement: 5L, resource: ResourceType.Knowledge);
        var violations = ContentValidator.Validate(content);

        Assert.Contains(violations, v => v.Contains("proj.needy") && v.Contains("no producer"));
    }

    [Fact]
    public void ProjectDefinition_RejectsANonPositiveSecondaryCost()
    {
        Assert.Throws<ArgumentException>(() => new ProjectDefinition(
            new ProjectId("proj.bad"), "Bad", 100L,
            resourceCosts: new[] { new KeyValuePair<ResourceType, long>(ResourceType.Materials, 0L) }));

        Assert.Throws<ArgumentException>(() => new ProjectDefinition(
            new ProjectId("proj.bad"), "Bad", 100L,
            resourceCosts: new[] { new KeyValuePair<ResourceType, long>(ResourceType.Materials, -5L) }));
    }

    [Fact]
    public void RegionDefinition_RejectsANonPositiveAuthoredCap()
    {
        Assert.Throws<ArgumentException>(() => new RegionDefinition(
            new RegionId("region.bad"), "Bad",
            new[] { new ProjectDefinition(new ProjectId("proj.a"), "A", 100L) },
            Array.Empty<LandmarkDefinition>(), Array.Empty<ProducerDefinition>(),
            resourceCaps: new[] { new KeyValuePair<ResourceType, long>(ResourceType.Materials, 0L) }));
    }

    // ==================================================================
    // 1.4 / 4.6 / 4.7 Balances, caps and the load-time invariant.
    // ==================================================================

    [Fact]
    public void GameStateValidator_ReportsABalanceAboveTheContentCap_WithoutClampingIt()
    {
        var content = Region(capUnits: 10L);
        var game = GameFactory.NewGame(content, T0, seed: 7UL);
        Assert.Empty(GameStateValidator.Validate(game, content));

        // Force an over-cap balance, bypassing ResourceBalances.Add the way corrupt or
        // hand-edited content would.
        game.Resources.Amounts[ResourceType.Materials] = 11L;

        var violations = GameStateValidator.Validate(game, content);

        Assert.Contains(violations, v => v.Contains("exceeds the content cap"));
        Assert.Equal(11L, game.Resources.Get(ResourceType.Materials)); // reported, not repaired
    }

    [Fact]
    public void GameFactory_AppliesAuthoredCapsToAFreshGame()
    {
        var content = Region(capUnits: 10L);
        var game = GameFactory.NewGame(content, T0, seed: 7UL);

        Assert.Equal(10L, game.Resources.GetCap(ResourceType.Materials));
    }

    [Fact]
    public void ResourceBalances_Invariants_HoldUnderCaps()
    {
        // 4.6: extends the existing ResourceBalancesTests cases, does not duplicate them.
        var balances = new ResourceBalances();
        balances.SetCap(ResourceType.Materials, 10L);

        // A zero grant is a no-op.
        Assert.Equal(0L, balances.Add(ResourceType.Materials, 0L));
        Assert.Equal(0L, balances.Get(ResourceType.Materials));

        // A grant beyond the cap applies only up to the cap and REPORTS the shortfall, so
        // the caller can park the remainder rather than silently losing it.
        Assert.Equal(10L, balances.Add(ResourceType.Materials, 25L));
        Assert.Equal(10L, balances.Get(ResourceType.Materials));
        Assert.Equal(0L, balances.Add(ResourceType.Materials, 1L));

        // Applying a cap lowers an existing balance exactly to the cap, never below zero.
        balances.Add(ResourceType.Vitality, 500L);
        balances.SetCap(ResourceType.Vitality, 50L);
        Assert.Equal(50L, balances.Get(ResourceType.Vitality));
        Assert.True(balances.Get(ResourceType.Vitality) >= 0L);

        // Spending more than the balance consumes nothing at all.
        Assert.False(balances.TryConsume(ResourceType.Materials, 11L));
        Assert.Equal(10L, balances.Get(ResourceType.Materials));

        // Extremely large grants saturate rather than overflow, and stay non-negative.
        var unbounded = new ResourceBalances();
        Assert.True(unbounded.Add(ResourceType.Vitality, long.MaxValue) > 0L);
        Assert.True(unbounded.Get(ResourceType.Vitality) > 0L);
        Assert.True(balances.Add(ResourceType.Materials, long.MaxValue) >= 0L);
    }

    // ==================================================================
    // Helpers
    // ==================================================================

    private static void UnlockAllProducers(GameState game, RegionDefinition content)
    {
        foreach (var producer in content.Producers)
        {
            var unlocker = game.Region.FindProject(producer.UnlockedByProjectId);
            if (unlocker != null)
            {
                unlocker.Status = ProjectStatus.Completed;
                unlocker.CompletedAtUtc = T0;
            }
            game.Region.FindProducer(producer.Id.Value)!.Unlocked = true;
        }
    }

    /// <summary>
    /// Minimal region with one Materials producer, used to exercise the new rules.
    /// 2400 milli-units/day = 2.4 units/day, so the cap binds within a handful of days.
    /// </summary>
    private static RegionDefinition Region(
        long capUnits,
        long? requirement = null,
        ResourceType resource = ResourceType.Materials,
        bool withCaps = true)
    {
        var projects = new List<ProjectDefinition>
        {
            new ProjectDefinition(new ProjectId("proj.entry"), "Entry", 100L),
            new ProjectDefinition(
                new ProjectId("proj.needy"), "Needy", 100L,
                prerequisites: new[] { new ProjectId("proj.entry") },
                resourceCosts: requirement.HasValue
                    ? new[] { new KeyValuePair<ResourceType, long>(resource, requirement.Value) }
                    : null),
        };

        var producers = new List<ProducerDefinition>
        {
            new ProducerDefinition(new ProducerId("prod.mill"), "Mill",
                ResourceType.Materials, 2_400L, 1_000L, "proj.entry"),
        };

        var caps = withCaps
            ? new List<KeyValuePair<ResourceType, long>>
            {
                new KeyValuePair<ResourceType, long>(ResourceType.Materials, capUnits),
            }
            : null;

        return new RegionDefinition(
            new RegionId("region.econ"), "Economy Test",
            projects, new List<LandmarkDefinition>(), producers,
            resourceCaps: caps);
    }
}
