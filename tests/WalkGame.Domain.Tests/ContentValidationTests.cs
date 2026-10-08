using System;
using System.Collections.Generic;
using System.Linq;
using WalkGame.Domain;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Simulation;
using WalkGame.Domain.Validation;
using LandmarkId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.LandmarkIdKind>;
using ProjectId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProjectIdKind>;
using ProducerId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProducerIdKind>;
using RegionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RegionIdKind>;

namespace WalkGame.Domain.Tests;

public class ContentValidationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);
    private static ProjectDefinition P(string id, long cost, params string[] prerequisiteIds) =>
        new(new ProjectId(id), $"title.{id}", cost,
            prerequisiteIds.Select(p => new ProjectId(p)));

    private static LandmarkDefinition L(string id, params LandmarkStageDefinition[] stages) =>
        new(new LandmarkId(id), $"landmark.{id}", stages);

    private static ProducerDefinition Pr(string id, string unlockedByProjectId) =>
        new(new ProducerId(id), $"producer.{id}", ResourceType.Materials, 24000L, 1000L, unlockedByProjectId);

    private static RegionDefinition Region(
        ProjectDefinition[] projects,
        LandmarkDefinition[]? landmarks = null,
        ProducerDefinition[]? producers = null,
        IEnumerable<KeyValuePair<ResourceType, long>>? resourceCaps = null) =>
        new(new RegionId("region.test"), "Test Region",
            projects, landmarks ?? Array.Empty<LandmarkDefinition>(), producers ?? Array.Empty<ProducerDefinition>(),
            resourceCaps: resourceCaps);

    /// <summary>Cap every resource any of the given producers emits, so a test fixture that
    /// is not testing caps still satisfies the "every produced resource declares a cap" rule.</summary>
    private static IEnumerable<KeyValuePair<ResourceType, long>> CapsFor(params ResourceType[] resources) =>
        resources.Select(r => new KeyValuePair<ResourceType, long>(r, 10_000L));

    [Fact]
    public void ValidGraph_HasZeroViolations()
    {
        var content = Region(
            new[] { P("proj.a", 300L), P("proj.b", 500L, "proj.a"), P("proj.c", 200L, "proj.b") },
            new[] { L("land.gate",
                new LandmarkStageDefinition(RestorationStage.Stabilized, "proj.a"),
                new LandmarkStageDefinition(RestorationStage.Functional, "proj.c")) },
            new[] { Pr("prod.mill", "proj.b") },
            CapsFor(ResourceType.Materials));

        var violations = ContentValidator.Validate(content);

        var unexpected = violations.Where(v => !v.Contains("non-positive cost")).ToList();
        Assert.Empty(unexpected);
    }

    [Fact]
    public void ProjectDefinition_PreservesConstructorVitalityCost()
    {
        var definition = new ProjectDefinition(new ProjectId("proj.cost"), "title.cost", 300L);

        Assert.Equal(300L, definition.VitalityCost);
    }

    [Fact]
    public void DuplicateProjectId_IsFlagged()
    {
        var content = Region(new[]
        {
            P("proj.a", 300L),
            P("proj.b", 200L, "proj.a"),
            P("proj.b", 100L, "proj.a"),
        });

        var violations = ContentValidator.Validate(content);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Contains("Duplicate project ID"));
    }

    [Fact]
    public void MissingPrerequisiteReference_IsFlagged()
    {
        var content = Region(new[] { P("proj.a", 300L, "proj.ghost") });

        var violations = ContentValidator.Validate(content);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Contains("missing prerequisite"));
    }

    [Fact]
    public void PrerequisiteCycle_IsFlagged()
    {
        var content = Region(new[]
        {
            P("proj.x", 100L, "proj.y"),
            P("proj.y", 100L, "proj.x"),
        });

        var violations = ContentValidator.Validate(content);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Contains("cycle"));
    }

    [Fact]
    public void MissingLandmarkUnlockProjectReference_IsFlagged()
    {
        var content = Region(
            new[] { P("proj.a", 300L) },
            new[] { L("land.gate", new LandmarkStageDefinition(RestorationStage.Stabilized, "proj.ghost")) });

        var violations = ContentValidator.Validate(content);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Contains("references missing project"));
    }

    [Fact]
    public void UnknownProducerUnlockProjectReference_IsFlagged()
    {
        var content = Region(
            new[] { P("proj.a", 300L) },
            null,
            new[] { Pr("prod.mill", "proj.ghost") });

        var violations = ContentValidator.Validate(content);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Contains("missing unlock project"));
    }

    [Fact]
    public void NoEntryProject_IsFlagged()
    {
        var content = Region(new[]
        {
            P("proj.x", 100L, "proj.y"),
            P("proj.y", 100L, "proj.x"),
        });

        var violations = ContentValidator.Validate(content);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Contains("no entry project"));
    }

    // ==================================================================
    // D-058: every numeric bound the validator enforces, at the accepted
    // maximum and at the FIRST value above it. A bound nobody pins is a
    // bound nobody can trust.
    // ==================================================================

    private static RegionDefinition ProducerRegion(long milliUnitsPerDay, long capacityUnits, long projectCost = 100L) =>
        new(
            new RegionId("region.bounds"), "Bounds",
            new[] { new ProjectDefinition(new ProjectId("proj.a"), "A", projectCost) },
            Array.Empty<LandmarkDefinition>(),
            new[]
            {
                new ProducerDefinition(new ProducerId("prod.p"), "P",
                    ResourceType.Materials, milliUnitsPerDay, capacityUnits, "proj.a"),
            },
            resourceCaps: new[] { new KeyValuePair<ResourceType, long>(ResourceType.Materials, 1_000_000L) });

    [Fact]
    public void ProducerRate_AtTheAcceptedMaximum_IsAccepted()
    {
        long max = ContentValidator.MaxMilliUnitsPerDay;

        var violations = ContentValidator.Validate(ProducerRegion(max, 1_000L));

        Assert.DoesNotContain(violations, v => v.Contains("rate"));
    }

    [Fact]
    public void ProducerRate_OneTickAboveTheAcceptedMaximum_IsRejected_NamingTheProducer()
    {
        long tooHigh = ContentValidator.MaxMilliUnitsPerDay + 1L;

        var violations = ContentValidator.Validate(ProducerRegion(tooHigh, 1_000L));

        Assert.Contains(violations, v => v.Contains("prod.p") && v.Contains("rate"));
    }

    [Fact]
    public void TheAcceptedRate_IsTheDerivedBound_NotARoundNumber()
    {
        // The old bound was 1e12, wrong by ~5 orders of magnitude. Pin the DERIVATION.
        Assert.Equal(ProducerArithmetic.MaxRepresentableMilliUnitsPerDay,
            ContentValidator.MaxMilliUnitsPerDay);
        Assert.Equal(ProducerArithmetic.DeriveMaxRepresentableMilliUnitsPerDay(),
            ContentValidator.MaxMilliUnitsPerDay);

        // The sub-day remainder is the binding term, and that is asserted, not assumed.
        Assert.True(ProducerArithmetic.SubDayRemainderBinds);
        Assert.Equal(ProducerArithmetic.SubDayTermBound, ContentValidator.MaxMilliUnitsPerDay);

        // And the bound really is where the arithmetic says: one above it overflows.
        Assert.Equal(long.MaxValue / (TimeSpan.TicksPerDay - 1L),
            ContentValidator.MaxMilliUnitsPerDay);
    }

    [Fact]
    public void ProducerCapacity_AtAndAboveTheAcceptedMaximum_BehavesAsDocumented()
    {
        long maxCapacity = long.MaxValue / ProducerDefinition.MilliUnitsPerUnit;

        var atBound = ContentValidator.Validate(ProducerRegion(1_000L, maxCapacity));
        Assert.DoesNotContain(atBound, v => v.Contains("capacity"));

        var above = ContentValidator.Validate(ProducerRegion(1_000L, maxCapacity + 1L));
        Assert.Contains(above, v => v.Contains("prod.p") && v.Contains("capacity"));
    }

    [Fact]
    public void ProducerCapacity_MustBePositive()
    {
        // `ProducerDefinition` rejects a non-positive capacity at CONSTRUCTION, so the
        // validator's own `capacityUnits <= 0` rule can only be reached by a definition
        // that bypasses the constructor. Prove the guard that actually runs, and note the
        // validator rule as defence in depth rather than pretend it is reachable.
        Assert.Throws<ArgumentException>(() => new ProducerDefinition(
            new ProducerId("prod.zero"), "Z", ResourceType.Materials, 1_000L, 0L, "proj.a"));
    }

    [Fact]
    public void Vitality_MayNotBeCapped_AndTheViolationNamesTheResource()
    {
        // D-054: Vitality is the activity-derived progression resource and is deliberately
        // exempt from the cap rule. GameFactory applies EVERY authored cap to a fresh
        // balance, so an authored Vitality cap would validate clean and then silently clamp
        // real walking credit away -- the exact data-loss defect the exemption prevents.
        // Fail closed at authoring time instead of trusting a future campaign to remember.
        var capped = Region(
            new[] { P("proj.a", 100L) },
            producers: new[]
            {
                new ProducerDefinition(new ProducerId("prod.p"), "P",
                    ResourceType.Materials, 1_000L, 100L, "proj.a"),
            },
            resourceCaps: new[]
            {
                new KeyValuePair<ResourceType, long>(ResourceType.Materials, 500L),
                new KeyValuePair<ResourceType, long>(ResourceType.Vitality, 10_000L),
            });

        var violations = ContentValidator.Validate(capped);
        Assert.Contains(violations, v => v.Contains("Vitality") && v.Contains("forbidden"));

        // Control: the same region WITHOUT the Vitality cap is clean, so the rule above is
        // what rejects it and not some unrelated violation.
        var uncapped = Region(
            new[] { P("proj.a", 100L) },
            producers: new[]
            {
                new ProducerDefinition(new ProducerId("prod.p"), "P",
                    ResourceType.Materials, 1_000L, 100L, "proj.a"),
            },
            resourceCaps: new[] { new KeyValuePair<ResourceType, long>(ResourceType.Materials, 500L) });
        Assert.Empty(ContentValidator.Validate(uncapped));
    }

    [Fact]
    public void ResourceCap_MustBePositive_AndEveryViolationNamesTheResource()
    {
        var badCap = new RegionDefinition(
            new RegionId("region.cap"), "Cap",
            new[] { new ProjectDefinition(new ProjectId("proj.a"), "A", 100L) },
            Array.Empty<LandmarkDefinition>(),
            new[]
            {
                new ProducerDefinition(new ProducerId("prod.p"), "P",
                    ResourceType.Materials, 1_000L, 100L, "proj.a"),
            },
            // Constructed directly rather than through the constructor guard, because the
            // constructor REJECTS a non-positive cap -- the validator must still be correct
            // for any RegionDefinition that reaches it.
            resourceCaps: null);

        // No cap at all: the produced resource is unbounded, and is named.
        var violations = ContentValidator.Validate(badCap);
        Assert.Contains(violations, v => v.Contains("Materials") && v.Contains("no cap"));

        // A non-positive cap cannot be constructed, so prove the guard that enforces it.
        Assert.Throws<ArgumentException>(() => new RegionDefinition(
            new RegionId("region.cap2"), "Cap2",
            new[] { new ProjectDefinition(new ProjectId("proj.a"), "A", 100L) },
            Array.Empty<LandmarkDefinition>(), Array.Empty<ProducerDefinition>(),
            resourceCaps: new[] { new KeyValuePair<ResourceType, long>(ResourceType.Materials, -1L) }));
    }

    [Fact]
    public void SelfCheck_ReportsAViolation_WhenThePublishedBoundDriftsFromTheDerivedOne()
    {
        // Exercise the self-check itself: a hand-edited published bound that no longer
        // matches the simulation must be reported, naming BOTH values.
        var problems = ContentValidator.CheckNumericBoundsAgainstSimulation(
            publishedMaxMilliUnitsPerDay: 1_000_000_000_000L);

        Assert.NotEmpty(problems);
        Assert.Contains(problems, v =>
            v.Contains("out of step with the simulation")
            && v.Contains("1000000000000")
            && v.Contains(ProducerArithmetic.MaxRepresentableMilliUnitsPerDay.ToString()));
    }

    [Fact]
    public void SelfCheck_IsSilent_WhenThePublishedBoundAgreesWithTheDerivation()
    {
        Assert.Empty(ContentValidator.CheckNumericBoundsAgainstSimulation(
            ContentValidator.MaxMilliUnitsPerDay));
    }

    [Fact]
    public void TheCorrectedBound_IsOrdersOfMagnitudeAboveShippedRates_AndBelowTheOldOne()
    {
        // Task 3.2. Region 1's three authored rates are 1200, 1500 and 2500 milli-units/day.
        // The corrected bound must sit far above all of them -- and, critically, FAR BELOW
        // the 1e12 the validator used to accept, which is the whole defect.
        const long shippedRate = 2_500L;
        long bound = ContentValidator.MaxMilliUnitsPerDay;

        Assert.True(bound > shippedRate * 1_000L,
            "the corrected bound (" + bound + ") must be far above the largest shipped rate ("
            + shippedRate + "); a bound near real content would mean the arithmetic is suspect");
        Assert.True(bound < 1_000_000_000_000L,
            "the corrected bound (" + bound + ") must be far below the old 1e12 bound");
    }

    // ==================================================================
    // Task 2.3 / 2.4: the simulation genuinely cannot overflow at the bound.
    // ==================================================================

    [Fact]
    public void Simulation_DoesNotOverflow_AtTheAcceptedRateOverTheMaximumInterval()
    {
        var content = ProducerRegion(ContentValidator.MaxMilliUnitsPerDay, capacityUnits: 1_000_000L);
        Assert.DoesNotContain(ContentValidator.Validate(content), v => v.Contains("rate"));

        var game = GameFactory.NewGame(content, T0, seed: 1UL);
        var unlocker = game.Region.FindProject("proj.a")!;
        unlocker.Status = ProjectStatus.Completed;
        unlocker.CompletedAtUtc = T0;
        game.Region.FindProducer("prod.p")!.Unlocked = true;

        // The maximum interval the simulation will ever process in one tick.
        OfflineAdvancer.TickProducers(
            game, content, T0.Add(OfflineAdvancer.MaxProducerInterval), new List<SimulationEvent>());

        var runtime = game.Region.FindProducer("prod.p")!;
        long storeCapacity = 1_000_000L * ProducerDefinition.MilliUnitsPerUnit;

        // The whole point: non-negative and inside the store, at the maximum authored rate.
        Assert.True(runtime.StoredMilliUnits >= 0L,
            "the tick must not wrap to a negative store at the accepted rate");
        Assert.InRange(runtime.StoredMilliUnits, 0L, storeCapacity);
        Assert.Empty(GameStateValidator.Validate(game, content));
    }

    [Fact]
    public void Simulation_DoesNotOverflow_AtTheAcceptedRateWithAFractionalDay()
    {
        // The sub-day remainder is the binding term, so a FRACTIONAL day at the maximum
        // rate is the single most overflow-prone input the tick can receive.
        var content = ProducerRegion(ContentValidator.MaxMilliUnitsPerDay, capacityUnits: 1_000_000L);
        var game = GameFactory.NewGame(content, T0, seed: 1UL);
        game.Region.FindProject("proj.a")!.Status = ProjectStatus.Completed;
        game.Region.FindProducer("prod.p")!.Unlocked = true;

        foreach (var elapsed in new[]
                 {
                     TimeSpan.FromTicks(1),
                     TimeSpan.FromHours(23),
                     TimeSpan.FromDays(1) - TimeSpan.FromTicks(1),
                     TimeSpan.FromDays(1),
                 })
        {
            var fresh = GameFactory.NewGame(content, T0, seed: 1UL);
            fresh.Region.FindProject("proj.a")!.Status = ProjectStatus.Completed;
            fresh.Region.FindProducer("prod.p")!.Unlocked = true;

            OfflineAdvancer.TickProducers(fresh, content, T0.Add(elapsed), new List<SimulationEvent>());

            long stored = fresh.Region.FindProducer("prod.p")!.StoredMilliUnits;
            Assert.True(stored >= 0L, "negative store after " + elapsed);
            Assert.InRange(stored, 0L, 1_000_000L * ProducerDefinition.MilliUnitsPerUnit);
        }
    }

    // ==================================================================
    // D-061: authored post-completion programs are validated as content.
    //
    // The rule that matters most is the one that is deliberately absent: entry
    // reachability. A program is reachable only from the closure milestone, so a naive
    // authoring pass would be flagged as a hidden deadlock — and the tempting "fix" is to
    // delete the node, which silently removes the ending's sink. These tests pin the
    // exemption AND every rule that still applies to the new content kind.
    // ==================================================================

    private static PostCompletionProgramDefinition Care(
        string id = "care.evergreen-tending",
        long costPerCycle = 1000L,
        string? enabledByMilestoneProjectId = null) =>
        new(id, "title.care", costPerCycle, enabledByMilestoneProjectId ?? "proj.closure", "description.care");

    /// <summary>A minimal region whose closure milestone is <c>proj.closure</c>.</summary>
    private static RegionDefinition RegionWithProgram(
        PostCompletionProgramDefinition program,
        string closureProjectId = "proj.closure") =>
        new(new RegionId("region.test"), "Test Region",
            new[] { P("proj.a", 300L), P("proj.closure", 500L, "proj.a") },
            Array.Empty<LandmarkDefinition>(), Array.Empty<ProducerDefinition>(),
            completionMilestoneProjectId: closureProjectId,
            postCompletionPrograms: new[] { program });

    [Fact]
    public void AuthoredPostCompletionProgram_ValidatesWithZeroViolations()
    {
        Assert.Empty(ContentValidator.Validate(RegionWithProgram(Care())));
    }

    [Fact]
    public void RegionWithoutAPostCompletionProgram_IsStillValidContent()
    {
        var content = new RegionDefinition(
            new RegionId("region.test"), "Test Region",
            new[] { P("proj.a", 300L), P("proj.closure", 500L, "proj.a") },
            Array.Empty<LandmarkDefinition>(), Array.Empty<ProducerDefinition>(),
            completionMilestoneProjectId: "proj.closure");

        Assert.Empty(ContentValidator.Validate(content));
    }

    /// <summary>
    /// The exemption that makes the whole change possible: the program is NOT reachable
    /// from any entry project, and that is correct rather than a hidden deadlock.
    /// </summary>
    [Fact]
    public void PostCompletionProgram_IsExemptFromEntryReachability_ButNothingElse()
    {
        var violations = ContentValidator.Validate(RegionWithProgram(Care()));

        Assert.DoesNotContain(violations, v => v.Contains("unreachable"));
        Assert.DoesNotContain(violations, v => v.Contains("hidden deadlock"));

        // Still an ordinary content node for every other rule: adding a referential defect
        // must still be reported.
        var dangling = RegionWithProgram(Care(enabledByMilestoneProjectId: "proj.does-not-exist"));
        Assert.Contains(ContentValidator.Validate(dangling),
            v => v.Contains("proj.does-not-exist") && v.Contains("closure milestone"));
    }

    [Fact]
    public void PostCompletionProgram_EnabledByANonMilestoneProject_IsFlagged()
    {
        var violations = ContentValidator.Validate(RegionWithProgram(Care(enabledByMilestoneProjectId: "proj.a")));

        Assert.Contains(violations, v => v.Contains("care.evergreen-tending"));
        Assert.Contains(violations, v => v.Contains("enabled by project"));
    }

    [Fact]
    public void PostCompletionProgram_WithUnrepresentableCost_IsFlagged()
    {
        // The non-positive case cannot even be constructed (the definition guards it), so the
        // validator's own non-positive rule is defence-in-depth; what is testable here is an
        // authored cost the simulation could never represent.
        var huge = ContentValidator.Validate(
            RegionWithProgram(Care(costPerCycle: long.MaxValue)));
        Assert.Contains(huge, v => v.Contains("unrepresentably large"));
    }

    [Fact]
    public void DuplicatePostCompletionProgramId_IsFlagged()
    {
        var content = new RegionDefinition(
            new RegionId("region.test"), "Test Region",
            new[] { P("proj.a", 300L), P("proj.closure", 500L, "proj.a") },
            Array.Empty<LandmarkDefinition>(), Array.Empty<ProducerDefinition>(),
            completionMilestoneProjectId: "proj.closure",
            postCompletionPrograms: new[] { Care(), Care() });

        Assert.Contains(ContentValidator.Validate(content), v => v.Contains("Duplicate post-completion program ID"));
    }

    /// <summary>
    /// A node authored as ordinary restoration content is NOT a post-completion program: it
    /// gets the ordinary rules, entry reachability included. Declaring a program is what
    /// makes it one — an id that is both is an authoring error, not a bonus.
    /// </summary>
    [Fact]
    public void PostCompletionProgramId_ThatIsAlsoOrdinaryContent_IsFlagged()
    {
        var content = new RegionDefinition(
            new RegionId("region.test"), "Test Region",
            new[] { P("proj.a", 300L), P("care.evergreen-tending", 500L, "proj.a"), P("proj.closure", 500L, "proj.a") },
            Array.Empty<LandmarkDefinition>(), Array.Empty<ProducerDefinition>(),
            completionMilestoneProjectId: "proj.closure",
            postCompletionPrograms: new[] { Care() });

        Assert.Contains(ContentValidator.Validate(content),
            v => v.Contains("authored both as ordinary content and as a post-completion program"));
    }

    [Fact]
    public void PostCompletionProgram_UnderARegionWithNoClosureMilestone_IsFlagged()
    {
        var content = new RegionDefinition(
            new RegionId("region.test"), "Test Region",
            new[] { P("proj.a", 300L) },
            Array.Empty<LandmarkDefinition>(), Array.Empty<ProducerDefinition>(),
            completionMilestoneProjectId: null,
            postCompletionPrograms: new[] { Care() });

        Assert.Contains(ContentValidator.Validate(content), v => v.Contains("defines no completion milestone"));
    }

    // ==================================================================
    // D-061: the post-completion STATE invariants.
    // ==================================================================

    [Fact]
    public void PostCompletionProgress_BeforeCompletion_IsAStateViolation()
    {
        var content = RegionWithProgram(Care());
        var game = GameFactory.NewGame(content, T0, seed: 1UL);
        game.Region.PostCompletionCyclesCompleted = 1L;

        Assert.Contains(GameStateValidator.Validate(game, content),
            v => v.Contains("before the region was completed"));
    }

    [Fact]
    public void PostCompletionProgress_AboveOneCycleCost_IsAStateViolation()
    {
        var content = RegionWithProgram(Care());
        var game = GameFactory.NewGame(content, T0, seed: 1UL);
        game.Region.IsCompleted = true;
        game.Region.RegionCompletedAtUtc = T0;
        var milestone = game.Region.FindProject("proj.closure")!;
        milestone.Status = ProjectStatus.Completed;
        milestone.CompletedAtUtc = T0;
        game.Region.PostCompletionInvestedVitality = 1000L; // exactly one cycle: it must have completed

        Assert.Contains(GameStateValidator.Validate(game, content),
            v => v.Contains("reaches the authored cycle cost"));
    }

    [Fact]
    public void PostCompletionProgress_WithoutAnAuthoredProgram_IsAStateViolation()
    {
        var content = new RegionDefinition(
            new RegionId("region.test"), "Test Region",
            new[] { P("proj.a", 300L), P("proj.closure", 500L, "proj.a") },
            Array.Empty<LandmarkDefinition>(), Array.Empty<ProducerDefinition>(),
            completionMilestoneProjectId: "proj.closure");

        var game = GameFactory.NewGame(content, T0, seed: 1UL);
        game.Region.IsCompleted = true;
        game.Region.RegionCompletedAtUtc = T0;
        var milestone = game.Region.FindProject("proj.closure")!;
        milestone.Status = ProjectStatus.Completed;
        milestone.CompletedAtUtc = T0;
        game.Region.PostCompletionInvestedVitality = 10L;

        Assert.Contains(GameStateValidator.Validate(game, content),
            v => v.Contains("authors no post-completion program"));
    }

    [Fact]
    public void PostCompletionProgress_WithARemainderBelowOneCycle_IsValid()
    {
        var content = RegionWithProgram(Care());
        var game = GameFactory.NewGame(content, T0, seed: 1UL);
        game.Region.IsCompleted = true;
        game.Region.RegionCompletedAtUtc = T0;
        var milestone = game.Region.FindProject("proj.closure")!;
        milestone.Status = ProjectStatus.Completed;
        milestone.CompletedAtUtc = T0;
        game.Region.PostCompletionCyclesCompleted = 60L;
        game.Region.PostCompletionInvestedVitality = 650L;

        Assert.Empty(GameStateValidator.Validate(game, content));
    }
}
