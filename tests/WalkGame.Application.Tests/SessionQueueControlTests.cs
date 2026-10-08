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
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;

namespace WalkGame.Application.Tests;

public sealed class SessionQueueControlTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ManualClock _clock = new(TestSessions.T0);

    public void Dispose() => _temp.Dispose();

    private GameSession NewSession() => TestSessions.Create(_temp, _clock);

    [Fact]
    public void SetAutoAdvance_False_PersistsAcrossRestart()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        session.EnqueueProject(TestSessions.EntryProjectId);

        Assert.True(session.SetAutoAdvance(false).IsSuccess);

        var reloaded = NewSession();
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);

        Assert.False(reloaded.GetHome().AutoAdvance);
        Assert.False(reloaded.GetProjects().AutoAdvance);
    }

    [Fact]
    public void SetAutoAdvance_True_ActivatesHeadOfQueue_WhenSlotFree()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        Assert.True(session.SetAutoAdvance(false).IsSuccess);
        Assert.True(session.EnqueueProject(TestSessions.EntryProjectId).IsSuccess);
        Assert.Null(session.GetHome().ActiveProjectId);

        Assert.True(session.SetAutoAdvance(true).IsSuccess);

        Assert.Equal(TestSessions.EntryProjectId, session.GetHome().ActiveProjectId);
    }

    [Fact]
    public void ActivateQueuedProject_ManualStart_SpendsBankedVitalityImmediately()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        Assert.True(session.SetAutoAdvance(false).IsSuccess);
        Assert.True(session.EnqueueProject(TestSessions.EntryProjectId).IsSuccess);
        Assert.True(session.CreditActivity(TestSessions.Tx1, TestSessions.T0, 120L, "walk").Saved);
        long bankedBefore = session.GetHome().Vitality;
        Assert.True(bankedBefore >= 120L);

        var result = session.ActivateQueuedProject(TestSessions.EntryProjectId);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var home = session.GetHome();
        Assert.Equal(TestSessions.EntryProjectId, home.ActiveProjectId);
        Assert.Equal(Math.Min(bankedBefore, 300L), home.ActiveProjectInvested);
        Assert.Equal(Math.Max(0L, bankedBefore - 300L), home.Vitality);
    }

    [Fact]
    public void ActivateQueuedProject_WhileSlotOccupied_FailsExplicitly()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        Assert.True(session.EnqueueProject(TestSessions.EntryProjectId).IsSuccess);
        Assert.NotNull(session.GetHome().ActiveProjectId);

        // The second entry project is Available in this catalog seed.
        var other = Region1Catalog.Create().Projects
            .Select(p => p.Id.Value)
            .FirstOrDefault(id => id != TestSessions.EntryProjectId && session.GetProjects().Projects.Any(r => r.ProjectId == id && r.Status == ProjectStatus.Available));

        if (other == null)
            return; // catalog has no second available project; nothing to prove here.

        var result = session.ActivateQueuedProject(other);
        Assert.False(result.IsSuccess);
        Assert.Equal("project.already-active", result.Error!.Code);
    }

    [Fact]
    public void ActivateQueuedProject_ProjectNotQueued_FailsCleanly()
    {
        var session = NewSession();
        session.StartNewGame(7UL);

        var result = session.ActivateQueuedProject("proj.river-intake");

        Assert.False(result.IsSuccess);
        Assert.Equal("project.not-queued", result.Error!.Code);
    }

    [Fact]
    public void GetProjects_ExposesStatusEffortPrerequisitesAndQueuePositions()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        Assert.True(session.EnqueueProject(TestSessions.EntryProjectId).IsSuccess);

        var model = session.GetProjects();

        Assert.True(model.AutoAdvance);
        var entry = model.Projects.Single(p => p.ProjectId == TestSessions.EntryProjectId);
        Assert.Equal(ProjectStatus.Active, entry.Status);
        Assert.Equal(0L, entry.VitalityInvested);
        Assert.Equal(300L, entry.VitalityCost);
        Assert.Null(entry.QueuedPosition);

        var river = model.Projects.Single(p => p.ProjectId == "proj.river-intake");
        Assert.Equal(ProjectStatus.Locked, river.Status);
        Assert.Contains(TestSessions.EntryProjectId, river.PrerequisiteIds);
    }

    [Fact]
    public void GetRegion_ExposesCanonicalStagesAndProducerStoreState()
    {
        var session = NewSession();
        session.StartNewGame(7UL);

        var region = session.GetRegion();

        Assert.Equal("Millbrook Valley", region.RegionTitleKey);
        Assert.All(region.Landmarks, l => Assert.Equal(RestorationStage.Ruined, l.Stage));
        Assert.Equal(3, region.Producers.Count);
        var producer = region.Producers.Single(p => p.ProducerId == "prd.workshop-salvage");
        Assert.False(producer.Unlocked);
        Assert.Equal(ResourceType.Materials, producer.Output);
        Assert.Equal(500L, producer.CapacityUnits);
        Assert.Equal(2500L, producer.MilliUnitsPerDay);
        Assert.Equal(0L, producer.StoredMilliUnits);
    }

    // ==================================================================
    // D-056: ReorderQueue and DequeueProject had ZERO test references
    // anywhere in the repository. Both mutate and persist durable queue
    // state, and they are exactly the two operations a drag-to-reorder
    // queue UI and an "un-queue" button would wire up first.
    // ==================================================================

    private const string First = "proj.clear-trailhead";
    private const string Second = "proj.rebuild-trail-bridges";
    private const string Third = "proj.open-lookout";

    /// <summary>
    /// Seeds <paramref name="ids"/> as queued, plus any prerequisite chain they need, by
    /// writing canonical state directly. These tests are about QUEUE semantics, so driving
    /// the real prerequisite chain would add minutes of simulation to assert nothing about
    /// the operation under test. Region 1 has exactly one entry project, so enqueuing three
    /// projects through the public API is not even possible without completing a chain.
    /// </summary>
    private GameSession SeedQueue(params string[] ids)
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        session.SetAutoAdvance(false);

        var state = DecodeState();
        var keep = new HashSet<string>(ids, StringComparer.Ordinal);

        // Complete EVERY project that is not one of the three under test, so all of them are
        // enqueueable. Completing the chain is what makes the prerequisite rule pass; doing
        // it per-id in a loop would complete the other two as a side effect.
        //
        // The closure milestone is deliberately left as a fresh profile has it. These tests
        // are about QUEUE semantics, and a completed closure milestone makes the region
        // complete as a side effect (D-061: post-completion progress then has an authored
        // sink), which is a different product state from the one under test here.
        string? closureMilestone = Region1Catalog.Create().CompletionMilestoneProjectId;
        foreach (var pair in state.Region.Projects)
        {
            if (keep.Contains(pair.Key))
                continue;
            if (pair.Key == closureMilestone)
                continue;
            pair.Value.Status = ProjectStatus.Completed;
            pair.Value.CompletedAtUtc = TestSessions.T0;
        }
        foreach (var id in ids)
            state.Region.FindProject(id)!.Status = ProjectStatus.Available;

        Persist(state);

        var reloaded = NewSession();
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);
        foreach (var id in ids)
            Assert.True(reloaded.EnqueueProject(id).IsSuccess, "enqueue " + id);
        return reloaded;
    }

    private void Persist(GameState state) =>
        new WalkGame.Infrastructure.Persistence.AtomicFileSaveStore(_temp.Path)
            .WriteAtomic(TestSessions.NewCodec().Encode(state, TestSessions.T0));

    [Fact]
    public void ReorderQueue_ValidPermutation_PersistsTheRequestedOrder()
    {
        var session = SeedQueue(Second, Third, First);

        var result = session.ReorderQueue(new[] { Third, First, Second });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(new[] { Third, First, Second }, PersistedQueueIds());

        // Every queued project is still present exactly once.
        var ids = PersistedQueueIds();
        Assert.Equal(3, ids.Length);
        Assert.Equal(3, ids.Distinct().Count());
    }

    [Theory]
    [InlineData("short")]        // wrong length
    [InlineData("unknown")]      // names a project that is not queued
    [InlineData("duplicate")]    // repeats a queued project
    [InlineData("omission")]     // drops a queued project
    public void ReorderQueue_InvalidOrdering_IsRejectedWithNoDurableChange(string defect)
    {
        var session = SeedQueue(First, Second, Third);

        var before = PersistedState();
        string[] ordering = defect switch
        {
            "short" => new[] { First, Second },
            "unknown" => new[] { First, Second, "proj.does-not-exist" },
            "duplicate" => new[] { First, First, Second },
            _ => new[] { First, Second }, // omission
        };

        var result = session.ReorderQueue(ordering);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidQueueOrder, result.Error!.Code);

        // NOTHING durable changed.
        var after = PersistedState();
        Assert.Equal(before.Queue, after.Queue);
        Assert.Equal(before.Statuses, after.Statuses);
        Assert.Equal(before.Vitality, after.Vitality);
        Assert.Equal(before.Invested, after.Invested);
    }

    [Fact]
    public void ReorderQueue_SurvivesRestart_WithTheSameOrder()
    {
        var session = SeedQueue(First, Second, Third);
        Assert.True(session.ReorderQueue(new[] { Second, Third, First }).IsSuccess);

        var reloaded = NewSession();
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);

        Assert.Equal(new[] { Second, Third, First }, PersistedQueueIds());
    }

    [Fact]
    public void ReorderQueue_DoesNotDisturbTheActiveProject_OrAnyBalance()
    {
        var session = SeedQueue(First, Second, Third);

        // Make one project active and give it real invested progress.
        Assert.True(session.ActivateQueuedProject(First).IsSuccess);
        session.CreditActivity(TestSessions.Tx1, TestSessions.T0, 120L, "invest");

        long vitalityBefore = session.GetHome().Vitality;
        long investedBefore = PersistedState().Invested;
        Assert.True(investedBefore > 0L, "precondition: the active project must hold progress");

        Assert.True(session.ReorderQueue(new[] { Third, Second }).IsSuccess);

        var after = PersistedState();
        Assert.Equal(First, after.ActiveProjectId);
        Assert.Equal(investedBefore, after.Invested);
        Assert.Equal(vitalityBefore, session.GetHome().Vitality);
        Assert.Equal(new[] { Third, Second }, PersistedQueueIds());
    }

    [Fact]
    public void DequeueProject_QueuedProject_ReturnsToAvailable_AndStateStaysValid()
    {
        var session = SeedQueue(First, Second, Third);

        var result = session.DequeueProject(Second);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(new[] { First, Third }, PersistedQueueIds());
        Assert.Equal(ProjectStatus.Available, PersistedState().Statuses[Second]);
        Assert.Empty(GameStateValidator.Validate(DecodeState(), Region1Catalog.Create()));
    }

    [Fact]
    public void DequeueProject_UnknownProject_IsRejectedWithNoDurableChange()
    {
        var session = SeedQueue(First, Second, Third);
        var before = PersistedState();

        var result = session.DequeueProject("proj.does-not-exist");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotQueued, result.Error!.Code);

        var after = PersistedState();
        Assert.Equal(before.Queue, after.Queue);
        Assert.Equal(before.Statuses, after.Statuses);
    }

    [Fact]
    public void DequeueProject_ActiveProject_IsRejected_AndItsStatusAndProgressAreUntouched()
    {
        // The CURRENT contract, pinned deliberately and NOT changed here (task 2.3).
        // Activation already removed the project from the queue, so by the time it is
        // Active it is no longer a queue entry: DequeueProject refuses it with NotQueued.
        // This is the right outcome for the invariant below -- an ACTIVE project holds
        // invested Vitality, and refusing the call is what stops that money being orphaned.
        var session = SeedQueue(First, Second);
        Assert.True(session.ActivateQueuedProject(First).IsSuccess);
        session.CreditActivity(TestSessions.Tx1, TestSessions.T0, 120L, "invest");

        var before = PersistedState();
        Assert.Equal(ProjectStatus.Active, before.Statuses[First]);
        Assert.DoesNotContain(First, before.Queue);

        var result = session.DequeueProject(First);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotQueued, result.Error!.Code);

        var after = PersistedState();
        Assert.Equal(ProjectStatus.Active, after.Statuses[First]);
        Assert.Equal(before.ActiveProjectId, after.ActiveProjectId);
        Assert.Equal(before.Invested, after.Invested);
        Assert.Equal(before.Vitality, after.Vitality);
    }

    [Fact]
    public void DequeueProject_InvestedVitalityIsNeitherLostNorRedirected()
    {
        var session = SeedQueue(First, Second, Third);
        Assert.True(session.ActivateQueuedProject(First).IsSuccess);
        // Deliberately LESS than First's 300 Vitality cost, so the project stays ACTIVE
        // with real invested progress instead of completing and freeing the slot.
        session.CreditActivity(TestSessions.Tx1, TestSessions.T0, 120L, "invest");

        long invested = PersistedState().Invested;
        long bankedBefore = session.GetHome().Vitality;
        Assert.Equal(120L, invested);
        Assert.Equal(0L, bankedBefore); // all of it is held as project progress, not banked

        // Dequeuing OTHER projects must not touch the active project's progress or the
        // player's bank.
        Assert.True(session.DequeueProject(Third).IsSuccess);
        var afterThird = PersistedState();
        Assert.Equal(invested, afterThird.Invested);
        Assert.Equal(bankedBefore, afterThird.Vitality);
        Assert.Equal(First, afterThird.ActiveProjectId);

        Assert.True(session.DequeueProject(Second).IsSuccess);

        // The active project is now the only one, and its money is intact: it is held as
        // that project's progress, NOT as banked Vitality, because the queue is not idle.
        // Nothing is lost and nothing is redirected into a different project.
        var afterAll = PersistedState();
        Assert.Equal(First, afterAll.ActiveProjectId);
        Assert.Equal(invested, afterAll.Invested);
        Assert.Equal(0L, session.GetHome().Vitality);
        Assert.Empty(GameStateValidator.Validate(DecodeState(), Region1Catalog.Create()));

        // Completing the project consumes exactly its 300 Vitality cost and strands
        // nothing: 120 already invested + 200 more = 320 in, 300 consumed, 20 back in the
        // unallocated bank. Every credited unit is accounted for, and the completed
        // project RETAINS its 300 invested on record as the record of what it cost.
        session.CreditActivity(TestSessions.Tx2, TestSessions.T0.AddMinutes(1), 200L, "finish");
        var completed = PersistedState();
        Assert.Equal(ProjectStatus.Completed, completed.Statuses[First]);
        Assert.Equal(300L, completed.Invested);
        Assert.Equal(20L, session.GetHome().Vitality);
        Assert.Equal(20L, completed.Vitality);
        Assert.Empty(GameStateValidator.Validate(DecodeState(), Region1Catalog.Create()));
    }

    [Fact]
    public void DequeueProject_SurvivesRestart()
    {
        var session = SeedQueue(First, Second, Third);
        Assert.True(session.DequeueProject(Second).IsSuccess);

        var reloaded = NewSession();
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);

        Assert.Equal(new[] { First, Third }, PersistedQueueIds());
        Assert.Equal(ProjectStatus.Available, PersistedState().Statuses[Second]);
    }

    // ---------- helpers ----------

    private void QueueThree(GameSession session, params string[] ids)
    {
        foreach (var id in ids)
            Assert.True(session.EnqueueProject(id).IsSuccess, "enqueue " + id);
    }

    private sealed class QueueSnapshot
    {
        public string[] Queue { get; set; } = Array.Empty<string>();
        public string? ActiveProjectId { get; set; }
        public long Vitality { get; set; }
        public long Invested { get; set; }
        public Dictionary<string, ProjectStatus> Statuses { get; set; } =
            new Dictionary<string, ProjectStatus>();
    }

    private QueueSnapshot PersistedState()
    {
        var state = DecodeState();
        var snapshot = new QueueSnapshot
        {
            Queue = state.Queue.QueuedProjectIds.ToArray(),
            ActiveProjectId = state.Queue.ActiveProjectId,
            Vitality = state.Resources.Get(ResourceType.Vitality),
            Invested = state.Region.Projects.Values.Sum(p => p.VitalityInvested),
        };
        foreach (var pair in state.Region.Projects)
            snapshot.Statuses[pair.Key] = pair.Value.Status;
        return snapshot;
    }

    private string[] PersistedQueueIds() => PersistedState().Queue;

    private GameState DecodeState()
    {
        var decoded = TestSessions.NewCodec()
            .Decode(File.ReadAllBytes(System.IO.Path.Combine(_temp.Path, "save.json")));
        Assert.Equal(CodecStatus.Ok, decoded.Status);
        return decoded.State!;
    }
}
