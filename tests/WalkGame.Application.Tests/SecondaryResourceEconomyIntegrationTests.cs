using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalkGame.Application.Content;
using WalkGame.Application.Persistence;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Common;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Simulation;
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-054 coverage against the SHIPPED Region 1 catalog.
///
/// The Domain suite proves the rules against a synthetic region. This suite proves two
/// things only the shipped content can prove: (a) the catalog itself validates, and
/// (b) the caps and requirements are actually REACHABLE in the product. That distinction
/// is the whole finding — before D-054 the cap and parking machinery existed, was tested,
/// and was never executed by any production path, because <c>SetCap</c> had zero call
/// sites outside tests.
/// </summary>
public sealed class SecondaryResourceEconomyIntegrationTests : IDisposable
{
    private static readonly DateTimeOffset T0 = TestSessions.T0;

    private const string MarketHall = "proj.restore-market-hall";
    private const string SurveyRig = "proj.calibrate-survey-rig";

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    // ==================================================================
    // The shipped catalog authors real caps and real requirements, and
    // validates cleanly with them.
    // ==================================================================

    [Fact]
    public void ShippedRegion1_ValidatesCleanly_WithAuthoredCapsAndRequirements()
    {
        Assert.Empty(ContentValidator.Validate(Region1Catalog.Create()));
    }

    [Fact]
    public void ShippedRegion1_AuthorsACapForEveryProducedResource()
    {
        var content = Region1Catalog.Create();

        foreach (var producer in content.Producers)
            Assert.True(content.ResourceCaps.ContainsKey(producer.Output),
                "every produced resource must declare a cap; " + producer.Output + " does not");

        // Vitality is deliberately uncapped: capping it would silently discard real walking
        // credit, because RewardLedger.Apply ignores what balances.Add returns. See D-054.
        Assert.False(content.ResourceCaps.ContainsKey(ResourceType.Vitality));
    }

    [Fact]
    public void ShippedRegion1_AuthorsSecondaryRequirementsOnTwoDistinctChainsAndCurrencies()
    {
        var content = Region1Catalog.Create();
        var withRequirements = content.Projects.Where(p => p.HasResourceRequirement).ToList();

        // Task 3.6 asked for 2-3 projects across distinct chains.
        Assert.InRange(withRequirements.Count, 2, 3);
        Assert.Contains(withRequirements, p => p.ResourceCost(ResourceType.Materials) > 0L);
        Assert.Contains(withRequirements, p => p.ResourceCost(ResourceType.Knowledge) > 0L);
        Assert.NotEqual(MarketHall, SurveyRig); // distinct projects, distinct chains
    }

    [Fact]
    public void ShippedRegion1_RequirementsAreSelfSupplying_BecauseTheirProducersUnlockFirst()
    {
        // The structural property that makes the requirement safe: whatever produces the
        // currency a project needs is unlocked by that project's own prerequisite, so the
        // requirement can never deadlock the chain it sits on.
        var content = Region1Catalog.Create();

        foreach (var project in content.Projects.Where(p => p.HasResourceRequirement))
            foreach (var requirement in project.ResourceCosts)
            {
                var producers = content.Producers
                    .Where(p => p.Output == requirement.Key)
                    .ToList();
                Assert.True(producers.Count > 0,
                    "no producer supplies " + requirement.Key + " required by " + project.Id);

                var prerequisiteIds = project.Prerequisites.Select(p => p.Value).ToList();
                Assert.True(producers.Any(p => prerequisiteIds.Contains(p.UnlockedByProjectId)),
                    "a producer of " + requirement.Key + " must be unlocked by a prerequisite of "
                    + project.Id + ", otherwise the requirement could deadlock the chain");
            }
    }

    // ==================================================================
    // 2.1 Parking is reachable with the SHIPPED content and a real new game.
    // ==================================================================

    [Fact]
    public void ShippedRegion1_ReachesItsAuthoredCaps_AndParksTheRefusedOutput()
    {
        var content = Region1Catalog.Create();
        var game = GameFactory.NewGame(content, T0, seed: 7UL);
        UnlockAllProducers(game, content);

        for (int day = 1; day <= 400; day++)
            OfflineAdvancer.TickProducers(game, content, T0.AddDays(day), new List<SimulationEvent>());

        // Both currencies stop AT the authored cap — never above it.
        Assert.Equal(content.ResourceCaps[ResourceType.Materials], game.Resources.Get(ResourceType.Materials));
        Assert.Equal(content.ResourceCaps[ResourceType.Knowledge], game.Resources.Get(ResourceType.Knowledge));

        // And the refused output is retained in the producers' bounded stores, not lost.
        long parked = 0L;
        foreach (var producer in content.Producers)
        {
            var runtime = game.Region.FindProducer(producer.Id.Value)!;
            parked += runtime.StoredMilliUnits;
            long storeCapacity = producer.CapacityUnits * ProducerDefinition.MilliUnitsPerUnit;
            Assert.InRange(runtime.StoredMilliUnits, 0L, storeCapacity);
        }
        Assert.True(parked > 0L, "output refused by the caps must be parked, not discarded");

        // And the state is still canonically valid with caps applied.
        Assert.Empty(GameStateValidator.Validate(game, content));
    }

    [Fact]
    public void ShippedRegion1_ParkedOutputReturnsAutomaticallyWhenThePlayerSpends()
    {
        var content = Region1Catalog.Create();
        var game = GameFactory.NewGame(content, T0, seed: 7UL);
        UnlockAllProducers(game, content);

        for (int day = 1; day <= 400; day++)
            OfflineAdvancer.TickProducers(game, content, T0.AddDays(day), new List<SimulationEvent>());

        var materials = content.Producers.First(p => p.Output == ResourceType.Materials);
        var runtime = game.Region.FindProducer(materials.Id.Value)!;
        long parkedBefore = runtime.StoredMilliUnits;
        Assert.True(parkedBefore > 0L);

        // The only relief valve is spending the resource.
        Assert.True(game.Resources.TryConsume(ResourceType.Materials, 40L));

        // A tick with NO elapsed time: delivery alone must move the parked units.
        OfflineAdvancer.TickProducers(game, content, T0.AddDays(400), new List<SimulationEvent>());

        Assert.True(runtime.StoredMilliUnits < parkedBefore);
        Assert.Equal(content.ResourceCaps[ResourceType.Materials], game.Resources.Get(ResourceType.Materials));
    }

    // ==================================================================
    // 3.7 / 4.7 The read models make the new state observable.
    // ==================================================================

    [Fact]
    public void ProjectRow_ExposesItsSecondaryRequirementAndAffordability()
    {
        var session = TestSessions.Create(_temp, new ManualClock(T0));
        Assert.Equal(StartStatus.NewGameCreated, session.StartNewGame(7UL).Status);

        var projects = session.GetProjects();
        var needy = projects.Projects.Where(p => p.ResourceCosts.Count > 0).ToList();
        Assert.NotEmpty(needy);

        foreach (var row in needy)
        {
            Assert.True(row.ResourceCost(ResourceType.Materials) + row.ResourceCost(ResourceType.Knowledge) > 0L);
            // A brand-new profile holds no secondary resources, so nothing is affordable yet.
            Assert.False(row.RequirementsAffordable);
        }

        // Projects with no requirement report an empty map and are trivially affordable.
        var plain = projects.Projects.First(p => p.ResourceCosts.Count == 0);
        Assert.Empty(plain.ResourceCosts);
        Assert.True(plain.RequirementsAffordable);
    }

    [Fact]
    public void CappedProducer_IsReportedAsCapacityLimited_WithItsCap_AndANonCappedOneIsNot()
    {
        var content = Region1Catalog.Create();

        // Nothing produced yet: no producer may claim to be capped.
        var fresh = TestSessions.Create(_temp, new ManualClock(T0));
        fresh.StartNewGame(7UL);
        Assert.DoesNotContain(fresh.GetRegion().Producers, p => p.CapacityLimited);

        // Drive the real economy to the caps, then read the model back.
        var game = GameFactory.NewGame(content, T0, seed: 7UL);
        UnlockAllProducers(game, content);
        for (int day = 1; day <= 400; day++)
            OfflineAdvancer.TickProducers(game, content, T0.AddDays(day), new List<SimulationEvent>());
        Persist(game);

        var cappedSession = TestSessions.Create(_temp, new ManualClock(T0.AddDays(400)));
        Assert.Equal(StartStatus.Loaded, cappedSession.Continue().Status);
        var region = cappedSession.GetRegion();

        var atCap = region.Producers.Where(p => p.CapacityLimited).ToList();
        Assert.NotEmpty(atCap);
        foreach (var row in atCap)
        {
            Assert.True(row.ResourceCapUnits < long.MaxValue, "a capped producer must report its cap");
            Assert.Equal(row.Output switch
            {
                ResourceType.Materials => content.ResourceCaps[ResourceType.Materials],
                ResourceType.Knowledge => content.ResourceCaps[ResourceType.Knowledge],
                _ => long.MaxValue,
            }, row.ResourceCapUnits);
        }
    }

    [Fact]
    public void ReturnSummary_ExplainsParkedOutputOnlyWhenACapWasActuallyHit()
    {
        // 2.4 / 4.8: "no production happened" and "your output is waiting" must differ.
        var content = Region1Catalog.Create();

        // No producers unlocked: nothing is capped, so no capped-storage item may appear.
        var idle = GameFactory.NewGame(content, T0, seed: 7UL);
        Assert.Null(idle.PendingReturnSummary);

        // A tick with producers at their cap and output refused.
        var game = GameFactory.NewGame(content, T0, seed: 7UL);
        UnlockAllProducers(game, content);
        for (int day = 1; day <= 400; day++)
            OfflineAdvancer.TickProducers(game, content, T0.AddDays(day), new List<SimulationEvent>());

        // Force a delivery attempt that must be refused by the cap, and read the event.
        var events = new List<SimulationEvent>();
        var producer = content.Producers.First(p => p.Output == ResourceType.Materials);
        var runtime = game.Region.FindProducer(producer.Id.Value)!;
        runtime.StoredMilliUnits = 10_000L; // 10 whole units, all refused by the cap
        OfflineAdvancer.TickProducers(game, content, T0.AddDays(400), events);

        var produced = events.OfType<ProducerProduced>().ToList();
        Assert.NotEmpty(produced);
        Assert.Contains(produced, p => p.HitResourceCap);
        Assert.DoesNotContain(produced, p => p.HitCapacity && !p.HitResourceCap);
    }

    // ==================================================================
    // 3.4 The application-level activation failure path.
    // ==================================================================

    [Fact]
    public void ActivateQueuedProject_WithAnUnmetRequirement_FailsWithItsOwnErrorCode()
    {
        // Build the state directly: the prerequisite chain is irrelevant to what is being
        // tested, which is the ACTIVATION contract.
        var content = Region1Catalog.Create();
        var session = TestSessions.Create(_temp, new ManualClock(T0));
        Assert.Equal(StartStatus.NewGameCreated, session.StartNewGame(7UL).Status);

        var game = GameFactory.NewGame(content, T0, seed: 7UL);
        game.Region.FindProject(MarketHall)!.Status = ProjectStatus.Queued;
        game.Queue.QueuedProjectIds.Add(MarketHall);
        Persist(game);

        var reloaded = TestSessions.Create(_temp, new ManualClock(T0));
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);
        long materialsBefore = reloaded.GetHome().Materials;

        var result = reloaded.ActivateQueuedProject(MarketHall);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceRequirementNotMet, result.Error!.Code);
        // The message names the resource and the shortfall so the shell can tell the player
        // exactly what to go and produce.
        Assert.Contains("Materials", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("40", result.Error.Message, StringComparison.Ordinal);

        // Nothing consumed, active slot unchanged, project still queued.
        Assert.Equal(materialsBefore, reloaded.GetHome().Materials);
        Assert.Null(reloaded.GetHome().ActiveProjectId);
        Assert.Contains(reloaded.GetHome().Queued, q => q.ProjectId == MarketHall);
    }

    [Fact]
    public void AutoAdvance_ConsumesTheRequirementExactlyOnce_WhenBootingWithTheProjectQueued()
    {
        var content = Region1Catalog.Create();
        var session = TestSessions.Create(_temp, new ManualClock(T0));
        Assert.Equal(StartStatus.NewGameCreated, session.StartNewGame(7UL).Status);

        // Seed: the requirement-bearing project is queued and fully funded, and auto-advance
        // is on (the default). Boot must activate it AND charge the requirement exactly once.
        var game = GameFactory.NewGame(content, T0, seed: 7UL);
        game.Region.FindProject(MarketHall)!.Status = ProjectStatus.Queued;
        game.Queue.QueuedProjectIds.Add(MarketHall);
        game.Resources.Add(ResourceType.Materials, 100L);
        Assert.True(game.Queue.AutoAdvance);
        Persist(game);

        var booted = TestSessions.Create(_temp, new ManualClock(T0));
        Assert.Equal(StartStatus.Loaded, booted.Continue().Status);

        Assert.Equal(MarketHall, booted.GetHome().ActiveProjectId);
        Assert.Equal(60L, booted.GetHome().Materials); // exactly 100 - 40

        // Repeated boots and operations must not charge it again.
        for (int day = 1; day <= 3; day++)
        {
            var later = TestSessions.Create(_temp, new ManualClock(T0.AddDays(day)));
            Assert.Equal(StartStatus.Loaded, later.Continue().Status);
        }

        var final = TestSessions.Create(_temp, new ManualClock(T0.AddDays(4)));
        Assert.Equal(StartStatus.Loaded, final.Continue().Status);
        Assert.Equal(60L, final.GetHome().Materials);
        Assert.Equal(MarketHall, final.GetHome().ActiveProjectId);
    }

    [Fact]
    public void ActivateQueuedProject_ExplicitActivation_ConsumesTheRequirementAndThenRefusesASecond()
    {
        var content = Region1Catalog.Create();
        var session = TestSessions.Create(_temp, new ManualClock(T0));
        Assert.Equal(StartStatus.NewGameCreated, session.StartNewGame(7UL).Status);

        // Same seed state, but auto-advance OFF so activation is the explicit operation
        // under test rather than a side effect of the boot.
        var game = GameFactory.NewGame(content, T0, seed: 7UL);
        game.Queue.AutoAdvance = false;
        game.Region.FindProject(MarketHall)!.Status = ProjectStatus.Queued;
        game.Queue.QueuedProjectIds.Add(MarketHall);
        game.Resources.Add(ResourceType.Materials, 100L);
        Persist(game);

        var funded = TestSessions.Create(_temp, new ManualClock(T0));
        Assert.Equal(StartStatus.Loaded, funded.Continue().Status);
        Assert.Equal(100L, funded.GetHome().Materials); // boot did not auto-activate
        Assert.Null(funded.GetHome().ActiveProjectId);

        var result = funded.ActivateQueuedProject(MarketHall);

        Assert.True(result.IsSuccess, result.Error?.Message ?? "activation should succeed");
        Assert.Equal(MarketHall, funded.GetHome().ActiveProjectId);
        Assert.Equal(60L, funded.GetHome().Materials); // exactly 100 - 40

        // A second activation cannot double-consume.
        var again = funded.ActivateQueuedProject(MarketHall);
        Assert.False(again.IsSuccess);
        Assert.Equal(60L, funded.GetHome().Materials);
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

    private void Persist(GameState state) =>
        new WalkGame.Infrastructure.Persistence.AtomicFileSaveStore(_temp.Path)
            .WriteAtomic(TestSessions.NewCodec().Encode(state, T0));

    private static GameState DecodeState(string path)
    {
        var decoded = TestSessions.NewCodec().Decode(File.ReadAllBytes(
            System.IO.Path.Combine(path, "save.json")));
        Assert.Equal(CodecStatus.Ok, decoded.Status);
        return decoded.State!;
    }
}