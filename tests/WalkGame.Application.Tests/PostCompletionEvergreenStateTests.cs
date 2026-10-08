using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.Persistence;
using WalkGame.Application.ReadModels;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Simulation;
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-061 post-completion acceptance: the audited defect was that a COMPLETED region kept
/// telling Home the player must queue restoration work, forever, while banked Vitality grew
/// without bound. Every test here drives real state through the real trust pipeline (fresh
/// session from disk per window), then asserts what the shell-facing surfaces report.
///
/// The mechanical properties are what can be proven headlessly: no unsatisfiable
/// instruction, a defined sink for post-completion progress, truthful surfaces, and
/// permanence. Whether the ending feels satisfying is a UX judgement outside what this
/// suite can evidence, and is recorded as such rather than claimed.
/// </summary>
public sealed class PostCompletionEvergreenStateTests : IDisposable
{
    private const ulong Seed = 11UL;
    private const long StepsPerDay = 20000L; // 200 Vitality/day through the real pipeline
    private const int HorizonDays = 160;
    private static readonly DateTimeOffset T0 = TestSessions.T0;

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    // ------------------------------------------------------------------
    // Driver: a clean profile through the real trust pipeline to closure.
    // ------------------------------------------------------------------

    /// <summary>
    /// Drives a fresh profile until Region 1's closure milestone, using the same
    /// deterministic low-decision player policy as the M4 acceptance proof. Returns the day
    /// the region completed, or null when the horizon ran out (a failed test result).
    /// </summary>
    private int? DriveToCompletion()
    {
        var content = Region1Catalog.Create();

        var bootstrap = TestSessions.Create(_temp.Path, new ManualClock(T0));
        Assert.Equal(StartStatus.NewGameCreated, bootstrap.StartNewGame(Seed).Status);
        Assert.True(bootstrap.EnqueueProject(TestSessions.EntryProjectId).IsSuccess);

        for (int day = 1; day <= HorizonDays; day++)
        {
            var session = SessionAt(WindowEnd(day));
            Assert.Equal(StartStatus.Loaded, session.Continue().Status);
            var ingest = session.IngestFromSource(
                new SyntheticWalkingSource(StepsPerDay), WindowStart(day), WindowEnd(day));
            Assert.Equal(1, ingest.Accepted);
            Assert.True(ingest.Saved);

            var home = session.GetHome();
            if (home.ActiveProjectId == null && home.Queued.Count == 0)
            {
                foreach (var definition in content.Projects.Select(p => p.Id.Value))
                    if (session.EnqueueProject(definition).IsSuccess)
                        break;
            }

            if (session.GetRegion().RegionCompleted)
            {
                // The player read the glance; dismissing it is the normal completion beat.
                Assert.True(session.AcknowledgeReturnSummary().IsSuccess);
                return day;
            }
        }

        return null;
    }

    private GameSession SessionAt(DateTimeOffset now) => TestSessions.Create(_temp.Path, new ManualClock(now));

    /// <summary>A fresh session that boots from disk at <paramref name="now"/>, like an app restart.</summary>
    private GameSession BootAt(DateTimeOffset now)
    {
        var session = SessionAt(now);
        Assert.Equal(StartStatus.Loaded, session.Continue().Status);
        return session;
    }

    private static DateTimeOffset WindowStart(int day) => T0.AddDays(day - 1);

    private static DateTimeOffset WindowEnd(int day) => T0.AddDays(day);

    private static GameState ReadState(string directory)
    {
        var decoded = TestSessions.NewCodec()
            .Decode(File.ReadAllBytes(Path.Combine(directory, "save.json")));
        Assert.Equal(CodecStatus.Ok, decoded.Status);
        return decoded.State!;
    }

    // ------------------------------------------------------------------
    // 1. The audited defect: a completed region demanded impossible work.
    // ------------------------------------------------------------------

    [Fact]
    public void CompletedRegion_ReportsAConclusion_NotAnImpossibleInstruction()
    {
        int? completedOn = DriveToCompletion();
        Assert.True(completedOn.HasValue, "region did not complete inside the horizon");

        var home = BootAt(WindowEnd(completedOn!.Value)).GetHome();

        Assert.False(home.RequiresAttention,
            "a completed region is concluded, not outstanding: it must stop demanding attention");
        Assert.Equal(HomeAttentionReason.RegionComplete, home.AttentionReason);
        Assert.True(home.RegionComplete);
        Assert.NotNull(home.RegionCompletedAtUtc);

        // Nothing anywhere instructs the player to queue work that no longer exists.
        Assert.Null(home.PrimaryNextAction);
        Assert.DoesNotContain("Queue the next restoration project.",
            home.PrimaryNextAction ?? string.Empty);
    }

    [Fact]
    public void ContinuingToEarnAfterCompletion_IsAppliedToTheAuthoredProgram_AndNeverAlarms()
    {
        int completedOn = AssertCompleted();

        // 25 further days of real walking through the pipeline, glance dismissed each day.
        for (int day = completedOn + 1; day <= completedOn + 25; day++)
        {
            var session = SessionAt(WindowEnd(day));
            Assert.Equal(StartStatus.Loaded, session.Continue().Status);
            var ingest = session.IngestFromSource(
                new SyntheticWalkingSource(StepsPerDay), WindowStart(day), WindowEnd(day));
            Assert.Equal(1, ingest.Accepted);
            Assert.True(session.AcknowledgeReturnSummary().IsSuccess);
        }

        var home = BootAt(WindowEnd(completedOn + 25)).GetHome();

        Assert.False(home.RequiresAttention);
        Assert.Equal(HomeAttentionReason.RegionComplete, home.AttentionReason);

        // The bank is not an ever-growing unused number: it is spent on the authored program.
        Assert.Equal(0L, home.BankedVitality);
        Assert.Equal(0L, home.Vitality);
        Assert.Equal(1000L, home.PostCompletionCareCostPerCycle);
        Assert.True(home.PostCompletionCareCycles >= 5L,
            "25 days at 200 Vitality/day must complete several tending cycles");
        Assert.InRange(home.PostCompletionCareInvested, 0L, 999L);

        // Lifetime totals still reconcile with what was actually earned: nothing was lost,
        // and every unit of post-completion progress has a home.
        var state = ReadState(_temp.Path);
        Assert.Equal(0L, state.Resources.Get(ResourceType.Vitality));

        long authoredRegionCost = Region1Catalog.Create().Projects.Sum(p => p.VitalityCost);
        long expeditionGrant = state.Region.Expeditions.TryGetValue("exp.valley-transect", out var transect)
            && transect!.CompletedAtUtc != null
                ? 50L
                : 0L;
        long appliedToProgram = (state.Region.PostCompletionCyclesCompleted * 1000L)
            + state.Region.PostCompletionInvestedVitality;

        Assert.Equal(
            state.Ledger.TotalVitalityCredited + expeditionGrant - authoredRegionCost,
            appliedToProgram);
    }

    private int AssertCompleted()
    {
        int? completedOn = DriveToCompletion();
        Assert.True(completedOn.HasValue, "region did not complete inside the horizon");
        return completedOn!.Value;
    }

    // ------------------------------------------------------------------
    // 2. The pre-completion contract is untouched.
    // ------------------------------------------------------------------

    [Fact]
    public void BeforeCompletion_EmptyQueueWithBankedProgress_StillDemandsAttention()
    {
        var session = TestSessions.Create(_temp.Path, new ManualClock(T0));
        session.StartNewGame(Seed);

        // Bank real activity without queueing anything: the queue stays empty by choice.
        // The record ends AT the injected now, so the only reason it could be rejected is a
        // future timestamp — which would make this test prove nothing.
        session.IngestActivityBatch(new[]
        {
            Normalized(T0.AddHours(-2), T0, 9000L, "pre-completion.bank"),
        });
        session.AcknowledgeReturnSummary();

        var home = session.GetHome();
        Assert.False(home.RegionComplete);
        Assert.True(home.RequiresAttention,
            "before completion an empty queue with banked progress IS an actionable state");
        Assert.Equal(HomeAttentionReason.QueueEmptyWithBankedVitality, home.AttentionReason);
        Assert.True(home.BankedVitality > 0L);
    }

    // ------------------------------------------------------------------
    // 3. Every surface reports the completed state truthfully.
    // ------------------------------------------------------------------

    [Fact]
    public void EverySurface_ReportsTheCompletedState_WithoutImplyingUnfinishedWork()
    {
        int completedOn = AssertCompleted();

        // Six more days of real walking after the ending: the program that reports the use of
        // continued activity has to actually move.
        for (int day = completedOn + 1; day <= completedOn + 6; day++)
        {
            var later = SessionAt(WindowEnd(day));
            Assert.Equal(StartStatus.Loaded, later.Continue().Status);
            later.IngestFromSource(
                new SyntheticWalkingSource(StepsPerDay), WindowStart(day), WindowEnd(day));
        }

        var region = BootAt(WindowEnd(completedOn + 6)).GetRegion();
        Assert.True(region.RegionCompleted);
        Assert.NotNull(region.RegionCompletedAtUtc);
        Assert.Equal(19, region.CompletedProjects);
        Assert.Equal(19, region.TotalProjects);
        Assert.Null(region.ActiveProjectId);

        // The authored post-completion program is reported through the region surface.
        Assert.NotNull(region.PostCompletionCare);
        Assert.Equal("care.evergreen-tending", region.PostCompletionCare!.ProgramId);
        Assert.True(region.PostCompletionCare.CyclesCompleted > 0L,
            "continued activity after the ending must visibly advance the authored program");
        Assert.InRange(region.PostCompletionCare.InvestedVitality, 0L, region.PostCompletionCare.VitalityCostPerCycle - 1);

        // Producers still operate — that is the evergreen property that already worked.
        Assert.All(region.Producers, p => Assert.True(p.Unlocked));

        var projects = BootAt(WindowEnd(completedOn + 6)).GetProjects();
        Assert.Null(projects.ActiveProjectId);
        Assert.DoesNotContain(projects.Projects, p => p.Status != ProjectStatus.Completed);
        Assert.DoesNotContain(projects.Projects, p => p.QueuedPosition.HasValue);

        var expeditions = BootAt(WindowEnd(completedOn + 6)).GetExpeditions();
        Assert.DoesNotContain(expeditions.Expeditions,
            e => e.Status != ExpeditionsReadModel.ExpeditionStatus.Completed);

        var discoveries = BootAt(WindowEnd(completedOn + 6)).GetDiscoveries();
        Assert.DoesNotContain(discoveries.Discoveries, d => !d.Unlocked);
    }

    // ------------------------------------------------------------------
    // 4. Permanence.
    // ------------------------------------------------------------------

    [Fact]
    public void Completion_SurvivesReloadRestartAndLongAbsence_AndIsNeverRedetected()
    {
        int completedOn = AssertCompleted();
        var before = ReadState(_temp.Path);
        var completionInstant = before.Region.RegionCompletedAtUtc;

        // A long absence, then a restart from disk.
        var session = SessionAt(WindowEnd(completedOn + 400));
        Assert.Equal(StartStatus.Loaded, session.Continue().Status);
        Assert.True(session.GetRegion().RegionCompleted);
        Assert.Equal(completionInstant, session.GetRegion().RegionCompletedAtUtc);

        // Nothing completed was undone, and the region did not regress a stage.
        var after = ReadState(_temp.Path);
        Assert.Equal(19, after.Region.Projects.Count(p => p.Value.Status == ProjectStatus.Completed));
        Assert.Equal(completionInstant, after.Region.RegionCompletedAtUtc);
        Assert.Empty(GameStateValidator.Validate(after, Region1Catalog.Create()));

        // Continued activity after the long absence still lands in the authored program.
        Assert.True(after.Region.PostCompletionCyclesCompleted >= before.Region.PostCompletionCyclesCompleted);
    }

    // ------------------------------------------------------------------
    // 5. The post-completion program is optional depth: it gates nothing.
    // ------------------------------------------------------------------

    [Fact]
    public void RegionCompletion_DoesNotRequireThePostCompletionProgram()
    {
        // Same real pipeline, but the region authors NO post-completion program. Closure is
        // reached exactly the same way, and the suppression of the impossible instruction is
        // not an accident of the sink being present.
        var contentWithoutPrograms = ContentWithoutPrograms();

        var bootstrap = TestSessions.Create(_temp.Path, new ManualClock(T0), contentWithoutPrograms);
        Assert.Equal(StartStatus.NewGameCreated, bootstrap.StartNewGame(Seed).Status);
        Assert.True(bootstrap.EnqueueProject(TestSessions.EntryProjectId).IsSuccess);

        int? completedOn = null;
        for (int day = 1; day <= HorizonDays && completedOn == null; day++)
        {
            var session = TestSessions.Create(_temp.Path, new ManualClock(WindowEnd(day)), contentWithoutPrograms);
            Assert.Equal(StartStatus.Loaded, session.Continue().Status);
            session.IngestFromSource(
                new SyntheticWalkingSource(StepsPerDay), WindowStart(day), WindowEnd(day));

            var home = session.GetHome();
            if (home.ActiveProjectId == null && home.Queued.Count == 0)
                foreach (var definition in contentWithoutPrograms.Projects.Select(p => p.Id.Value))
                    if (session.EnqueueProject(definition).IsSuccess)
                        break;

            if (session.GetRegion().RegionCompleted)
            {
                completedOn = day;
                session.AcknowledgeReturnSummary();
            }
        }

        Assert.True(completedOn.HasValue, "closure must not depend on the post-completion program");

        var afterCompletion = TestSessions.Create(_temp.Path, new ManualClock(WindowEnd(completedOn!.Value)), contentWithoutPrograms);
        Assert.Equal(StartStatus.Loaded, afterCompletion.Continue().Status);
        var completed = afterCompletion.GetHome();
        Assert.False(completed.RequiresAttention);
        Assert.Equal(HomeAttentionReason.RegionComplete, completed.AttentionReason);
        Assert.Equal(0L, completed.PostCompletionCareCostPerCycle);
        Assert.Equal(0L, completed.PostCompletionCareCycles);
    }

    [Fact]
    public void PostCompletionProgress_IsBounded_OneCycleAtMost_SitsUnspent()
    {
        int completedOn = AssertCompleted();

        // A burst far larger than any single cycle: the unbounded bank is the audited defect.
        // Ten records at the per-record clamp (250,000 steps) plus five ordinary ones, all
        // inside the acceptance window, so the credit is real rather than rejected.
        var session = SessionAt(WindowEnd(completedOn + 1));
        Assert.Equal(StartStatus.Loaded, session.Continue().Status);

        var windowStart = WindowStart(completedOn + 1);
        var records = new List<NormalizedActivityRecord>();
        for (int i = 0; i < 10; i++)
            records.Add(Normalized(windowStart, windowStart.AddMinutes(30), 250_000L, "post.clamped." + i));
        for (int i = 0; i < 5; i++)
            records.Add(Normalized(windowStart, windowStart.AddMinutes(45), 4_000L, "post.ordinary." + i));

        var beforeBurst = session.GetHome();
        long appliedBefore = (beforeBurst.PostCompletionCareCycles * 1000L) + beforeBurst.PostCompletionCareInvested;

        var ingest = session.IngestActivityBatch(records);
        Assert.Equal(15, ingest.Accepted);

        var home = session.GetHome();

        // Every unit of the burst (25,000 clamped + 200 ordinary) has a home.
        long appliedAfter = appliedBefore + (25_000L + 200L);
        Assert.Equal(appliedAfter / 1000L, home.PostCompletionCareCycles);
        Assert.Equal(appliedAfter % 1000L, home.PostCompletionCareInvested);

        // The audited defect was the balance, not the counter: nothing sits unspent.
        Assert.Equal(0L, home.Vitality);
        Assert.Equal(0L, home.BankedVitality);
        Assert.InRange(home.PostCompletionCareInvested, 0L, home.PostCompletionCareCostPerCycle - 1);
    }

    /// <summary>The shipped Region 1 content with the post-completion program removed.</summary>
    private static RegionDefinition ContentWithoutPrograms()
    {
        var authored = Region1Catalog.Create();
        return new RegionDefinition(
            authored.Id, authored.TitleKey,
            authored.Projects, authored.Landmarks, authored.Producers,
            contentVersion: authored.ContentVersion,
            discoveries: authored.Discoveries,
            expeditions: authored.Expeditions,
            ecologyProgression: authored.EcologyProgression,
            settlementProgression: authored.SettlementProgression,
            completionMilestoneProjectId: authored.CompletionMilestoneProjectId,
            resourceCaps: authored.ResourceCaps,
            tombstones: authored.Tombstones.Values,
            postCompletionPrograms: null);
    }

    /// <summary>One normalized record through the production ingestion shape.</summary>
    private static NormalizedActivityRecord Normalized(
        DateTimeOffset startUtc, DateTimeOffset endUtc, long steps, string sourceRecordId) =>
        new(
            "dev.test",
            sourceRecordId,
            ActivityCategory.Walking,
            ActivityUnits.Steps,
            steps,
            startUtc,
            endUtc,
            Revision: 1);
}
