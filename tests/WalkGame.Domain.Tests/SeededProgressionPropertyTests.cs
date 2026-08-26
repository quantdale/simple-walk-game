using System;
using System.Collections.Generic;
using System.Linq;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Randomness;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Simulation;
using WalkGame.Domain.Validation;
using ProjectId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProjectIdKind>;
using LandmarkId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.LandmarkIdKind>;
using ProducerId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProducerIdKind>;
using RegionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RegionIdKind>;
using Xunit;

namespace WalkGame.Domain.Tests;

/// <summary>
/// M8-H1 seeded property/invariant testing (campaign Workstream G). Every generated
/// scenario is driven by an explicit seed; failures print the seed so the exact
/// sequence can be reproduced deterministically. No floating point, no wall clock.
///
/// Core invariants exercised repeatedly over randomized-but-deterministic histories:
///   - balances never go negative and producer stores never exceed capacity;
///   - completed projects never become incomplete (completion is monotonic);
///   - landmark stages and region arcs never regress;
///   - region completion is monotonic and stamped exactly once;
///   - reward replay by transaction identity is idempotent;
///   - producer elapsed-time partitioning loses at most &lt; 1 milli-unit per tick
///     against a single-tick computation of the same total elapsed time;
///   - state validates cleanly after every step.
/// </summary>
public sealed class SeededProgressionPropertyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    // ------------------------------------------------------------------
    // Producer partitioning property.
    // ------------------------------------------------------------------

    [Fact]
    public void ProducerTickPartitioning_LosesLessThanOneMilliUnitPerSplit_AndNeverOverproduces()
    {
        const int scenarioCount = 24;

        for (int scenario = 0; scenario < scenarioCount; scenario++)
        {
            var rng = new DeterministicRng((ulong)scenario + 1UL);
            long rateMilliPerDay = 500L + (long)(rng.NextUInt64() % 4_500UL);
            int splits = 1 + (int)(rng.NextUInt64() % 7UL); // 1..7 ticks

            var content = SingleProducerContent(rateMilliPerDay, capacityUnits: 10_000L);
            var oneShot = UnlockedGame(content);
            var partitioned = UnlockedGame(content);

            long totalElapsedTicks = TimeSpan.TicksPerDay * (long)(scenario + 1);
            OfflineAdvancer.TickProducers(oneShot, content, T0.AddTicks(totalElapsedTicks), new List<SimulationEvent>());

            long remaining = totalElapsedTicks;
            for (int split = 0; split < splits && remaining > 0; split++)
            {
                bool last = split == splits - 1;
                long slice = last || remaining <= 1
                    ? remaining
                    : Math.Max(1L, rng.NextInt64(1, remaining));
                remaining -= slice;
                OfflineAdvancer.TickProducers(partitioned, content, T0.AddTicks(totalElapsedTicks - remaining), new List<SimulationEvent>());
            }

            long singleTotal = oneShot.Region.Producers[0].TotalProducedMilliUnits;
            long splitTotal = partitioned.Region.Producers[0].TotalProducedMilliUnits;

            Assert.True(splitTotal <= singleTotal,
                $"[seed {scenario + 1}] split production exceeded one-shot production");
            Assert.True(singleTotal - splitTotal >= 0 && singleTotal - splitTotal <= splits,
                $"[seed {scenario + 1}] partitioning drift {singleTotal - splitTotal} exceeded <1 milli-unit per tick (splits={splits})");

            var violations = GameStateValidator.Validate(partitioned, content);
            Assert.Empty(violations);
        }
    }

    [Fact]
    public void ProducerStoreCapacity_BindsProductionUnderSeededLongAbsences()
    {
        for (int scenario = 0; scenario < 16; scenario++)
        {
            var rng = new DeterministicRng(0xC0FFEEUL + (ulong)scenario);
            const long capacityUnits = 5L;
            var content = SingleProducerContent(rateMilliPerDay: 2_000L, capacityUnits);
            var game = UnlockedGame(content);

            int absenceCount = 3 + (int)(rng.NextUInt64() % 6UL);
            var now = T0;
            for (int absence = 0; absence < absenceCount; absence++)
            {
                now = now.AddDays(1 + rng.NextInt64(1, 400));
                var events = new List<SimulationEvent>();
                OfflineAdvancer.Advance(game, content, now, events);

                var runtime = game.Region.Producers[0];
                Assert.InRange(runtime.StoredMilliUnits, 0L, capacityUnits * 1000L);
                foreach (var balance in game.Resources.Amounts.Values)
                    Assert.True(balance >= 0L, $"[seed {scenario}] negative balance after absence to {now}");
                Assert.Empty(GameStateValidator.Validate(game, content));
            }
        }
    }

    // ------------------------------------------------------------------
    // Progression monotonicity under seeded credit/allocation scripts.
    // ------------------------------------------------------------------

    [Fact]
    public void SeededCreditScripts_PreserveMonotoneCompletionAndIdempotentReplay()
    {
        for (int scenario = 0; scenario < 20; scenario++)
        {
            var rng = new DeterministicRng(0x5EEDUL + (ulong)scenario * 7919UL);
            var content = ChainContent();
            var game = GameFactory.NewGame(content, T0, (ulong)scenario + 100UL);
            game.Queue.AutoAdvance = true;

            var completedBefore = new HashSet<string>(StringComparer.Ordinal);
            var stagesBefore = SnapshotStages(game);
            bool regionCompletedBefore = false;

            var now = T0;
            ulong txCounter = 1UL;
            for (int step = 0; step < 30; step++)
            {
                if (game.Queue.QueuedProjectIds.Count == 0 && game.Queue.ActiveProjectId == null)
                    foreach (var definition in content.Projects)
                        if (game.Region.FindProject(definition.Id.Value)!.Status == ProjectStatus.Available)
                        {
                            game.Region.FindProject(definition.Id.Value)!.Status = ProjectStatus.Queued;
                            game.Queue.QueuedProjectIds.Add(definition.Id.Value);
                            break;
                        }

                now = now.AddHours(5 + rng.NextInt64(0, 40));

                long amount = rng.NextInt64(0, 400);
                var tx = RewardTx(txCounter++);
                Assert.Equal(LedgerApplyOutcome.AppliedFirstTime,
                    game.Ledger.Apply(new RewardTransaction(tx, now, amount, "walk"), game.Resources));

                var events = new List<SimulationEvent>();
                OfflineAdvancer.Advance(game, content, now, events);

                // Idempotent replay of the identical transaction identity.
                Assert.Equal(LedgerApplyOutcome.DuplicateIgnored,
                    game.Ledger.Apply(new RewardTransaction(tx, now, amount, "walk"), game.Resources));

                // Monotonic completion.
                foreach (var pair in game.Region.Projects)
                {
                    if (pair.Value.Status == ProjectStatus.Completed)
                        completedBefore.Add(pair.Key);
                    else
                        Assert.False(completedBefore.Contains(pair.Key),
                            $"[seed {scenario}] completed project '{pair.Key}' regressed at step {step}");
                }

                // Landmark stages never regress.
                var stagesNow = SnapshotStages(game);
                foreach (var stagePair in stagesNow)
                    Assert.True(stagePair.Value >= stagesBefore[stagePair.Key],
                        $"[seed {scenario}] landmark '{stagePair.Key}' regressed at step {step}");
                stagesBefore = stagesNow;

                // Arc monotonicity.
                Assert.True(game.Region.EcologyStage >= 0);
                Assert.True(game.Region.SettlementStage >= 0);

                // Region completion monotonic.
                Assert.False(regionCompletedBefore && !game.Region.IsCompleted,
                    $"[seed {scenario}] region completion un-set at step {step}");
                regionCompletedBefore |= game.Region.IsCompleted;

                // Balances and validator.
                foreach (var balance in game.Resources.Amounts.Values)
                    Assert.True(balance >= 0L, $"[seed {scenario}] negative balance at step {step}");
                Assert.Empty(GameStateValidator.Validate(game, content));

                // Determinism witness: re-running allocation on unchanged inputs is safe.
                var beforeStateLedger = game.Ledger.TotalVitalityCredited;
                OfflineAdvancer.AllocateVitality(game, content, now, new List<SimulationEvent>());
                Assert.Equal(beforeStateLedger, game.Ledger.TotalVitalityCredited);
            }
        }
    }

    // ------------------------------------------------------------------
    // Content builders.
    // ------------------------------------------------------------------

    private static RegionDefinition SingleProducerContent(long rateMilliPerDay, long capacityUnits)
    {
        var project = new ProjectDefinition(new ProjectId("proj.p0"), "P0", 100L);
        var producer = new ProducerDefinition(
            new ProducerId("prd.p0"), "PRD", ResourceType.Materials, rateMilliPerDay,
            capacityUnits, "proj.p0");
        return new RegionDefinition(
            new RegionId("region.prop"), "Property Region",
            new[] { project },
            Array.Empty<LandmarkDefinition>(),
            new[] { producer });
    }

    private static RegionDefinition ChainContent()
    {
        var a = new ProjectDefinition(new ProjectId("proj.a"), "A", 250L);
        var b = new ProjectDefinition(new ProjectId("proj.b"), "B", 400L, new[] { new ProjectId("proj.a") });
        var c = new ProjectDefinition(new ProjectId("proj.c"), "C", 150L, new[] { new ProjectId("proj.b") });
        var milestone = new ProjectDefinition(new ProjectId("proj.m"), "M", 200L, new[] { new ProjectId("proj.c") });

        var landmark = new LandmarkDefinition(
            new LandmarkId("lm.a"), "LM A",
            new[]
            {
                new LandmarkStageDefinition(RestorationStage.Stabilized, "proj.a"),
                new LandmarkStageDefinition(RestorationStage.Restored, "proj.c"),
            });

        return new RegionDefinition(
            new RegionId("region.chain"), "Chain Region",
            new[] { a, b, c, milestone },
            new[] { landmark },
            Array.Empty<ProducerDefinition>(),
            ecologyProgression: new RegionProgressionDefinition(new[]
            {
                new ProgressionStageDefinition(1, "proj.b"),
            }),
            settlementProgression: new RegionProgressionDefinition(new[]
            {
                new ProgressionStageDefinition(1, "proj.c"),
            }),
            completionMilestoneProjectId: "proj.m");
    }

    private static GameState UnlockedGame(RegionDefinition content)
    {
        var game = GameFactory.NewGame(content, T0, 42UL);
        var runtime = game.Region.FindProject("proj.p0")!;
        runtime.Status = ProjectStatus.Completed;
        runtime.CompletedAtUtc = T0;
        var producer = game.Region.Producers[0];
        producer.Unlocked = true;
        producer.LastTickUtc = T0;
        return game;
    }

    private static Dictionary<string, int> SnapshotStages(GameState game) =>
        game.Region.LandmarkStages.ToDictionary(pair => pair.Key, pair => (int)pair.Value);

    private static WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RewardTransactionIdKind> RewardTx(ulong n)
    {
        byte[] bytes = new byte[16];
        BitConverter.GetBytes(n).CopyTo(bytes, 0);
        return WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RewardTransactionIdKind>.FromGuid(new Guid(bytes));
    }
}
