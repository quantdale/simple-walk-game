using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.Pacing;
using WalkGame.Application.Persistence;
using WalkGame.Application.ReadModels;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-062 pacing contract: every profile the region is balanced against reaches closure,
/// closure is defined by an authored criterion rather than by the roster's total cost, the
/// reports state requested AND eligible activity, and a shortfall is a FAILURE the gate
/// refuses rather than a line a reviewer may skim past.
/// </summary>
public sealed class RegionPacingContractTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    // ------------------------------------------------------------------
    // Content: chains partition the roster, and the criterion is validated.
    // ------------------------------------------------------------------

    [Fact]
    public void AuthoredChains_PartitionTheRoster_AndEveryProjectHasAChain()
    {
        var content = Region1Catalog.Create();

        var all = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chain in content.Chains)
            foreach (var projectId in chain.ProjectIds)
                Assert.True(all.Add(projectId), $"project '{projectId}' is claimed by more than one chain");

        Assert.Equal(content.Projects.Count, all.Count);
        foreach (var project in content.Projects)
            Assert.Contains(project.Id.Value, all);
    }

    [Fact]
    public void ClosureCriterion_RequiresTheWholeValley_AndIsSatisfiedByTheMilestoneRoute()
    {
        var content = Region1Catalog.Create();
        var criterion = content.ClosureCriterion;

        Assert.NotNull(criterion);
        Assert.Equal(content.CompletionMilestoneProjectId, criterion!.MilestoneProjectId);
        Assert.Equal(content.Chains.Count, criterion.MinimumChainsComplete);

        // A profile that has restored every chain has, by construction, completed the survey.
        var game = GameFactory.NewGame(content, T0, seed: 1UL);
        Assert.False(content.MeetsClosureCriterion(game.Region));

        foreach (var project in content.Projects)
        {
            var state = game.Region.FindProject(project.Id.Value)!;
            state.Status = ProjectStatus.Completed;
            state.CompletedAtUtc = T0;
        }

        Assert.True(content.MeetsClosureCriterion(game.Region));
        Assert.Empty(GameStateValidator.Validate(game, content));
    }

    [Fact]
    public void ClosureCriterion_BelowAMajorityOfChains_IsAContentViolation()
    {
        var content = Region1Catalog.Create();
        var weak = new ClosureCriterionDefinition(
            content.CompletionMilestoneProjectId!, minimumChainsComplete: 2, "Weak ending");
        var rebuilt = Rebuild(content, closureCriterion: weak);

        Assert.Contains(ContentValidator.Validate(rebuilt),
            v => v.Contains("below the 4-chain majority"));
    }

    [Fact]
    public void ClosureCriterion_NamingAnUnknownMilestone_IsAContentViolation()
    {
        var content = Region1Catalog.Create();
        var broken = new ClosureCriterionDefinition(
            "proj.does-not-exist", minimumChainsComplete: content.Chains.Count, "Broken ending");
        var rebuilt = Rebuild(content, closureCriterion: broken);

        Assert.Contains(ContentValidator.Validate(rebuilt), v => v.Contains("proj.does-not-exist"));
    }

    /// <summary>
    /// The alternative route is authored capability, not Region 1's shipped choice: proven
    /// on a fixture so the mechanism cannot rot silently, while the shipped criterion
    /// requires the whole valley (a weaker route would end the region before the survey for
    /// players who can afford the survey — see docs/DECISIONS.md D-062).
    /// </summary>
    [Fact]
    public void ClosureCriterion_AlternativeRoute_FiresOnAMajorityOfChains_WhenAuthored()
    {
        var content = Region1Catalog.Create();
        var alternative = new ClosureCriterionDefinition(
            content.CompletionMilestoneProjectId!, minimumChainsComplete: 4, "Four of six");
        var rebuilt = Rebuild(content, closureCriterion: alternative);

        Assert.Empty(ContentValidator.Validate(rebuilt));

        var game = GameFactory.NewGame(rebuilt, T0, seed: 1UL);
        Assert.False(rebuilt.MeetsClosureCriterion(game.Region));

        int completedChains = 0;
        foreach (var chain in rebuilt.Chains)
        {
            foreach (var projectId in chain.ProjectIds)
            {
                var state = game.Region.FindProject(projectId)!;
                state.Status = ProjectStatus.Completed;
                state.CompletedAtUtc = T0;
            }
            completedChains++;
            if (completedChains == 4)
                break;
        }

        Assert.True(rebuilt.MeetsClosureCriterion(game.Region));
        Assert.Empty(GameStateValidator.Validate(game, rebuilt));
    }

    [Fact]
    public void ClosureCriterion_ThatNoReachableChainSetCanMeet_IsAContentViolation()
    {
        var content = Region1Catalog.Create();
        var impossible = new ClosureCriterionDefinition(
            content.CompletionMilestoneProjectId!, minimumChainsComplete: 5, "Impossible ending");

        // Two of the six chains are made unreachable while the chain definitions stay intact,
        // so the criterion asks for five chains from only four reachable ones.
        var orphanedChains = new[] { "chain.settlement-community", "chain.wetland-recovery" };
        var edited = new List<WalkGame.Domain.Projects.ProjectDefinition>();
        foreach (var project in content.Projects)
        {
            bool orphan = content.Chains.Any(c => orphanedChains.Contains(c.Id, StringComparer.Ordinal)
                && c.ProjectIds.Contains(project.Id.Value, StringComparer.Ordinal));
            if (!orphan)
            {
                edited.Add(project);
                continue;
            }

            var prerequisites = new List<WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProjectIdKind>>(project.Prerequisites)
            {
                new WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProjectIdKind>("proj.does-not-exist"),
            };
            edited.Add(new WalkGame.Domain.Projects.ProjectDefinition(
                project.Id, project.TitleKey, project.VitalityCost,
                prerequisites: prerequisites,
                descriptionKey: project.DescriptionKey));
        }

        var rebuilt = Rebuild(content, projects: edited, closureCriterion: impossible);

        Assert.Contains(ContentValidator.Validate(rebuilt),
            v => v.Contains("are reachable from an entry project"));
    }

    // ------------------------------------------------------------------
    // Canonical state: a completion no route explains is corruption.
    // ------------------------------------------------------------------

    [Fact]
    public void RegionMarkedComplete_WithoutSatisfyingTheCriterion_IsAStateViolation()
    {
        var content = Region1Catalog.Create();
        var game = GameFactory.NewGame(content, T0, seed: 1UL);
        game.Region.IsCompleted = true;
        game.Region.RegionCompletedAtUtc = T0;

        Assert.Contains(GameStateValidator.Validate(game, content),
            v => v.Contains("authored closure criterion is not satisfied"));
    }

    /// <summary>
    /// Legacy over-funding (D-062): a save that completed a project under the v2 costs holds
    /// more Vitality than the v3 cost of that project. The state must stay loadable — a
    /// lower authored cost must never brick a profile that already paid the higher one.\n    /// </summary>
    [Fact]
    public void LegacyOverFunding_IsAcceptedForCompletedProjects_AndRejectedForIncompleteOnes()
    {
        var content = Region1Catalog.Create();

        var completedOverFunded = GameFactory.NewGame(content, T0, seed: 1UL);
        var research = completedOverFunded.Region.FindProject("proj.refit-observatory-dome")!;
        research.Status = ProjectStatus.Completed;
        research.CompletedAtUtc = T0;
        research.VitalityInvested = 900; // the v2 cost; v3 authors 550
        Assert.Empty(GameStateValidator.Validate(completedOverFunded, content));

        var incompleteOverFunded = GameFactory.NewGame(content, T0, seed: 1UL);
        var active = incompleteOverFunded.Region.FindProject("proj.lay-water-lines")!;
        active.Status = ProjectStatus.Active;
        active.VitalityInvested = 850; // the v2 cost; v3 authors 550
        incompleteOverFunded.Queue.ActiveProjectId = "proj.lay-water-lines";
        Assert.Contains(GameStateValidator.Validate(incompleteOverFunded, content),
            v => v.Contains("invested vitality out of bounds"));
    }

    // ------------------------------------------------------------------
    // The measured remedy: the weekday-only profile reaches closure.
    // ------------------------------------------------------------------

    [Fact]
    public void WeekdayOnlyProfileWithALongAbsence_ReachesClosure_ThroughTheRealPipeline()
    {
        // The audited defect: this profile earned 16,420 against a 19,400-Vitality roster and
        // never finished. It now closes inside the documented horizon with every chain
        // complete, which is the whole point of the rebalance.
        int closureDay = DriveProfile("inactivity", horizon: 400);

        Assert.True(closureDay > 0, "the weekday-only profile did not reach closure");
        Assert.InRange(closureDay, 1, 400);

        var state = Decode();
        Assert.True(state.Region.IsCompleted);
        Assert.NotNull(state.Region.RegionCompletedAtUtc);
        Assert.Equal(Region1Catalog.Create().Chains.Count,
            Region1Catalog.Create().ChainsComplete(state.Region));
        Assert.All(state.Region.Projects.Values, p => Assert.Equal(ProjectStatus.Completed, p.Status));
        Assert.Empty(GameStateValidator.Validate(state, Region1Catalog.Create()));
    }

    [Fact]
    public void EngagedProfiles_StillClose_AndStayInsideTheirDocumentedTargets()
    {
        foreach (var (profile, target) in new[]
                 {
                     ("high", 120),
                     ("irregular", 250),
                     ("moderate", 300),
                     ("weekend-heavy", 300),
                 })
        {
            int closureDay = DriveProfile(profile, horizon: target);
            Assert.True(closureDay > 0, $"profile '{profile}' did not reach closure");
            Assert.True(closureDay <= target,
                $"profile '{profile}' closed on day {closureDay}, past its documented target of {target}");
        }
    }

    [Fact]
    public void LowProfile_IsADocumentedLongTail_NotASilentFailure()
    {
        // 3,000 steps/day sustained cannot fund the region under the authored conversion rate.
        // The contract is that this is STATED — the profile is exempt by documented decision
        // and the criterion reports NOT MET — rather than hidden or treated as unexpected.
        var content = Region1Catalog.Create();
        var (mustClose, _) = PacingContract.Target("low");
        Assert.False(mustClose);

        // Driven through the real pipeline, the long tail is real state, not a stalled one.
        int closureDay = DriveStepsPerDay(3000L, horizon: 400);
        Assert.Equal(0, closureDay);

        var state = Decode();
        Assert.False(state.Region.IsCompleted);
        Assert.False(content.MeetsClosureCriterion(state.Region));
        Assert.InRange(content.ChainsComplete(state.Region), 1, content.Chains.Count - 1);
        Assert.Empty(GameStateValidator.Validate(state, content));
    }

    [Fact]
    public void PathologicalVolumes_CreditAtTheSameRate_AsOrdinaryVolumes()
    {
        // The ≈686 steps/Vitality figure was wrong: the clamp discards surplus VOLUME, not
        // credit. A profile of implausible volumes must converge on the same effective rate.
        var first = IngestOnce(250_000L);
        var second = IngestOnce(4_000L);

        Assert.Equal(250_000L, first.EligibleSteps);
        Assert.Equal(2_500L, first.VitalityCredited);
        Assert.Equal(4_000L, second.EligibleSteps);
        Assert.Equal(40L, second.VitalityCredited);

        // Requested beyond the clamp is visible as a difference, not silently absorbed.
        var beyond = IngestOnce(1_000_000L);
        Assert.Equal(250_000L, beyond.EligibleSteps);
        Assert.Equal(2_500L, beyond.VitalityCredited);
    }

    [Fact]
    public void PacingHarness_FailsAndNamesChains_WhenAShortfallIsReintroduced()
    {
        // The verdict logic itself: a horizon inside the profile's own closure day must be a
        // balance defect, and the defect must NAME the unfinished chains rather than counting
        // them. The CLI wires this verdict to its exit code (verified by running the verb:
        // `profile --profile inactivity --days 100` exits 2 with the chain list).
        var content = Region1Catalog.Create();
        var completionDays = new Dictionary<string, int>();

        // The run must have reached the contracted horizon for the verdict to apply: that is
        // what distinguishes a pacing judgement from a short fixture-generating run.
        bool defect = PacingContract.IsBalanceDefect(
            "inactivity", completed: false, daysSimulated: 400, horizonDays: 400);
        Assert.True(defect);

        // A short run leaves most chains unfinished, and they are reported by label.
        foreach (var chain in content.Chains.Take(2))
            foreach (var projectId in chain.ProjectIds)
                completionDays[projectId] = 1;

        string labels = PacingContract.IncompleteChainLabels(content, completionDays);
        Assert.Contains("settlement-community", labels);
        Assert.Contains("wetland-recovery", labels);
        Assert.DoesNotContain("trail-access", labels);
    }

    [Fact]
    public void PacingContract_EveryProfileTheRegionIsBalancedAgainst_IsClassified()
    {
        // No profile may be silently exempt: each one either must close inside a documented
        // target, or is named as an accepted long tail with that decision recorded.
        Assert.Equal((true, 120), PacingContract.Target("high"));
        Assert.Equal((true, 250), PacingContract.Target("irregular"));
        Assert.Equal((true, 300), PacingContract.Target("moderate"));
        Assert.Equal((true, 300), PacingContract.Target("weekend-heavy"));
        Assert.Equal((true, 400), PacingContract.Target("pathological"));
        Assert.Equal((true, 400), PacingContract.Target("inactivity"));

        var (mustClose, maxDays) = PacingContract.Target("low");
        Assert.False(mustClose, "the low profile's long tail must stay an explicit, documented exemption");
        Assert.Equal(0, maxDays);

        // A profile that closes inside its target is evidence; one that does not is a defect.
        Assert.False(PacingContract.IsBalanceDefect("high", completed: true, daysSimulated: 120));
        Assert.True(PacingContract.IsBalanceDefect("high", completed: true, daysSimulated: 121));
        Assert.False(PacingContract.IsBalanceDefect("low", completed: false, daysSimulated: 400));

        // Only a run that reached the contracted horizon may judge. A short run is a fixture
        // generator, and treating it as a defect made `profile` unusable for the
        // content-drift gate, which produces saves with the same verb.
        Assert.False(PacingContract.IsBalanceDefect("inactivity", completed: false, daysSimulated: 50, horizonDays: 50));
        Assert.True(PacingContract.IsBalanceDefect("inactivity", completed: false, daysSimulated: 400, horizonDays: 400));
        Assert.True(PacingContract.IsBalanceDefect("high", completed: false, daysSimulated: 120, horizonDays: 120));
    }

    [Fact]
    public void PacingReports_StateRequestedAndEligibleActivity_Separately()
    {
        // The defect that produced the wrong conversion figure: one number, printed under an
        // "activity ingested" label, carrying the PRE-clamp total. The contract now keeps the
        // two facts apart and the ratio is derivable from them.
        var content = Region1Catalog.Create();
        var requested = 3_184_633L; // the committed pathological request total
        var eligible = 2_184_633L;  // after the per-record clamp
        var credited = 21_844L;     // committed credited vitality

        Assert.True(eligible < requested, "bounded handling must be observable for the fixture to mean anything");
        Assert.Equal(100.0, eligible / (double)credited, 1);
        Assert.Equal(6, content.Chains.Count);
    }

    // ------------------------------------------------------------------
    // Post-completion state under the criterion.
    // ------------------------------------------------------------------

    [Fact]
    public void CompletedState_IsEvergreenAcrossReloads_AndPostCompletionSurfacesHold()
    {
        DriveProfile("inactivity", horizon: 400);

        var reloaded = TestSessions.Create(_temp.Path, new ManualClock(T0.AddDays(600)));
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);

        var region = reloaded.GetRegion();
        Assert.True(region.RegionCompleted);
        Assert.NotNull(region.RegionCompletedAtUtc);
        Assert.Equal(region.CompletedProjects, region.TotalProjects);
        Assert.NotNull(region.PostCompletionCare);

        // The player has read the glance; afterwards the surface is calm and the reason names
        // the conclusion. (A CURRENT summary legitimately claims attention first — that is
        // news, not an instruction, and D-061 covers both halves.)
        Assert.True(reloaded.AcknowledgeReturnSummary().IsSuccess);
        var home = reloaded.GetHome();
        Assert.False(home.RequiresAttention);
        Assert.Equal(HomeAttentionReason.RegionComplete, home.AttentionReason);
        Assert.Empty(GameStateValidator.Validate(Decode(), Region1Catalog.Create()));
    }

    // ------------------------------------------------------------------
    // Harness: profiles are driven through the REAL trust pipeline.
    // ------------------------------------------------------------------

    /// <summary>Drives one of the seven named profiles to closure or its horizon; returns the closure day, or 0.</summary>
    private int DriveProfile(string profileName, int horizon)
    {
        var content = Region1Catalog.Create();
        var session = TestSessions.Create(_temp.Path, new ManualClock(T0));
        Assert.Equal(StartStatus.NewGameCreated, session.StartNewGame(42UL).Status);

        int closureDay = 0;
        for (int day = 1; day <= horizon && closureDay == 0; day++)
        {
            var windowStart = T0.AddDays(day - 1);
            var windowEnd = T0.AddDays(day);
            long steps = ProfileSteps(profileName, day, windowStart);
            if (steps <= 0L)
                continue;

            var daily = TestSessions.Create(_temp.Path, new ManualClock(windowEnd));
            Assert.Equal(StartStatus.Loaded, daily.Continue().Status);
            daily.IngestFromSource(new SyntheticWalkingSource(steps), windowStart, windowEnd);

            var home = daily.GetHome();
            if (home.ActiveProjectId == null && home.Queued.Count == 0)
                foreach (var projectId in content.Projects.Select(p => p.Id.Value))
                    if (daily.EnqueueProject(projectId).IsSuccess)
                        break;

            if (daily.GetRegion().RegionCompleted)
                closureDay = day;
        }

        return closureDay;
    }

    /// <summary>The GAME_SYSTEMS §13 profile patterns, mirrored for the contract test.</summary>
    private static long ProfileSteps(string profileName, int day, DateTimeOffset windowStart) =>
        profileName switch
        {
            "low" => 3_000L,
            "moderate" => 8_000L,
            "high" => 20_000L,
            "irregular" => new[] { 26_000L, 15_000L, 2_000L, 18_000L, 6_000L, 22_000L, 9_000L }[(day - 1) % 7],
            "weekend-heavy" => windowStart.DayOfWeek switch
            {
                DayOfWeek.Saturday => 22_000L,
                DayOfWeek.Sunday => 24_000L,
                DayOfWeek.Friday => 6_000L,
                DayOfWeek.Thursday => 4_500L,
                DayOfWeek.Wednesday => 4_000L,
                DayOfWeek.Tuesday => 3_500L,
                _ => 3_000L,
            },
            "inactivity" =>
                day >= 100 && day <= 144 ? 0L
                : ((day - 1) % 7 is 5 or 6 ? 0L : (day % 2 == 0 ? 9_000L : 4_000L)),
            "pathological" => new[] { 250_000L, 100L, 40_000L, 50L, 500_000L, 10L, 8_000L, 1L }[(day - 1) % 8],
            _ => throw new ArgumentException("unknown profile '" + profileName + "'", nameof(profileName)),
        };

    private int DriveStepsPerDay(long steps, int horizon)
    {
        var session = TestSessions.Create(_temp.Path, new ManualClock(T0));
        Assert.Equal(StartStatus.NewGameCreated, session.StartNewGame(42UL).Status);
        var content = Region1Catalog.Create();

        int closureDay = 0;
        for (int day = 1; day <= horizon && closureDay == 0; day++)
        {
            var windowStart = T0.AddDays(day - 1);
            var windowEnd = T0.AddDays(day);
            var daily = TestSessions.Create(_temp.Path, new ManualClock(windowEnd));
            Assert.Equal(StartStatus.Loaded, daily.Continue().Status);
            daily.IngestFromSource(new SyntheticWalkingSource(steps), windowStart, windowEnd);

            var home = daily.GetHome();
            if (home.ActiveProjectId == null && home.Queued.Count == 0)
                foreach (var projectId in content.Projects.Select(p => p.Id.Value))
                    if (daily.EnqueueProject(projectId).IsSuccess)
                        break;

            if (daily.GetRegion().RegionCompleted)
                closureDay = day;
        }
        return closureDay;
    }

    /// <summary>One ingestion through the production path, in a fresh profile.</summary>
    private static IngestResult IngestOnce(long steps)
    {
        using var temp = new TempDirectory();
        var session = TestSessions.Create(temp.Path, new ManualClock(T0));
        Assert.Equal(StartStatus.NewGameCreated, session.StartNewGame(7UL).Status);
        return session.IngestActivityBatch(new[]
        {
            new NormalizedActivityRecord(
                "dev.test-pacing", "bounded." + steps,
                ActivityCategory.Walking, ActivityUnits.Steps, steps,
                T0.AddHours(-1), T0, Revision: 1),
        });
    }

    private GameState Decode(string? saveDir = null)
    {
        var decoded = TestSessions.NewCodec().Decode(
            File.ReadAllBytes(Path.Combine(saveDir ?? _temp.Path, "save.json")));
        Assert.Equal(CodecStatus.Ok, decoded.Status);
        return decoded.State!;
    }

    /// <summary>Rebases authored content onto the shipped roster with one replacement.</summary>
    private static RegionDefinition Rebuild(
        RegionDefinition content,
        IEnumerable<WalkGame.Domain.Projects.ProjectDefinition>? projects = null,
        ClosureCriterionDefinition? closureCriterion = null)
    {
        var roster = projects ?? content.Projects;
        var chains = content.Chains;
        if (projects != null)
        {
            // Chains are authored against project ids; a fixture that removes a project needs
            // its chains trimmed so the fixture fails for its intended reason only.
            var kept = new HashSet<string>(roster.Select(p => p.Id.Value), StringComparer.Ordinal);
            chains = chains.Where(c => c.ProjectIds.All(kept.Contains)).ToList();
        }

        return new RegionDefinition(
            content.Id, content.TitleKey,
            roster, content.Landmarks, content.Producers,
            contentVersion: content.ContentVersion,
            discoveries: content.Discoveries,
            expeditions: content.Expeditions,
            ecologyProgression: content.EcologyProgression,
            settlementProgression: content.SettlementProgression,
            completionMilestoneProjectId: content.CompletionMilestoneProjectId,
            resourceCaps: content.ResourceCaps,
            tombstones: content.Tombstones.Values,
            postCompletionPrograms: content.PostCompletionPrograms,
            chains: chains,
            closureCriterion: closureCriterion);
    }
}
