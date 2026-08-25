using System;
using System.Collections.Generic;
using System.Linq;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.Summaries;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Summaries;
using WalkGame.Domain.Time;

namespace WalkGame.Application.Tests;

/// <summary>
/// Durable return-summary contract: committed progress must remain explainable after a
/// crash between commit and presentation; acknowledgement is idempotent and never alters
/// progression; replayed activity cannot regenerate a false "new progress" summary.
/// </summary>
public sealed class ReturnSummaryDurabilityTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private static readonly DateTimeOffset WindowEnd = TestSessions.T0.AddDays(3);

    public void Dispose() => _temp.Dispose();

    private static GameSession BootFresh(TempDirectory dir, ManualClock clock)
    {
        var session = TestSessions.Create(dir, clock);
        Assert.Equal(StartStatus.NewGameCreated, session.StartNewGame(7UL).Status);
        return session;
    }

    [Fact]
    public void Summary_CommittedBeforeCrash_IsStillThere_AfterRestart()
    {
        var clock = new ManualClock(WindowEnd);
        var first = TestSessions.Create(_temp, clock);
        Assert.Equal(StartStatus.NoSaveFound, first.Continue().Status);
        Assert.Equal(StartStatus.NewGameCreated, first.StartNewGame(7UL).Status);
        Assert.True(first.EnqueueProject(TestSessions.EntryProjectId).IsSuccess);

        var ingest = first.IngestFromSource(new SyntheticWalkingSource(20000L), WindowEnd.AddDays(-3), WindowEnd);
        Assert.True(ingest.Saved);
        Assert.True(ingest.VitalityCredited > 0L);

        // "Crash": no acknowledgement, brand-new session reads from disk only.
        var second = TestSessions.Create(_temp, clock);
        Assert.Equal(StartStatus.Loaded, second.Continue().Status);

        var pending = second.GetPendingReturnSummary();
        Assert.NotNull(pending);
        Assert.True(pending!.HasMeaningfulChange);
        Assert.Contains(pending.Items, item => item.Text.Contains("Clear the old trailhead", StringComparison.Ordinal));
    }

    [Fact]
    public void Acknowledge_IsIdempotent_NeverAltersProgression()
    {
        var clock = new ManualClock(WindowEnd);
        var session = BootFresh(_temp, clock);
        Assert.True(session.EnqueueProject(TestSessions.EntryProjectId).IsSuccess);
        session.IngestFromSource(new SyntheticWalkingSource(20000L), WindowEnd.AddDays(-3), WindowEnd);

        long vitalityBefore = session.GetHome().Vitality;
        int completedBefore = session.GetHome().CompletedProjects;

        Assert.True(session.AcknowledgeReturnSummary().IsSuccess);
        Assert.Null(session.GetPendingReturnSummary());
        Assert.True(session.AcknowledgeReturnSummary().IsSuccess); // idempotent no-op

        Assert.Equal(vitalityBefore, session.GetHome().Vitality);
        Assert.Equal(completedBefore, session.GetHome().CompletedProjects);

        // The dismissal is durable.
        var reloaded = TestSessions.Create(_temp, clock);
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);
        Assert.Null(reloaded.GetPendingReturnSummary());
    }

    [Fact]
    public void ReplayedActivity_AfterAcknowledgement_RegeneratesNothingFalse()
    {
        var clock = new ManualClock(WindowEnd);
        var source = new SyntheticWalkingSource(20000L);
        var windowStart = WindowEnd.AddDays(-3);

        var first = TestSessions.Create(_temp, clock);
        Assert.Equal(StartStatus.NewGameCreated, first.StartNewGame(7UL).Status);
        Assert.True(first.EnqueueProject(TestSessions.EntryProjectId).IsSuccess);
        var ingestA = first.IngestFromSource(source, windowStart, WindowEnd);
        Assert.True(first.AcknowledgeReturnSummary().IsSuccess);

        var replaySession = TestSessions.Create(_temp, clock);
        Assert.Equal(StartStatus.Loaded, replaySession.Continue().Status);
        var ingestB = replaySession.IngestFromSource(source, windowStart, WindowEnd);

        Assert.Equal(0L, ingestB.VitalityCredited);
        Assert.Equal(ingestA.Accepted, ingestB.DuplicatesIgnored);
        Assert.Null(replaySession.GetPendingReturnSummary());
    }

    [Fact]
    public void IdleOperations_DoNotTouch_TheExistingPendingSummary()
    {
        var clock = new ManualClock(WindowEnd);
        var session = BootFresh(_temp, clock);
        Assert.True(session.EnqueueProject(TestSessions.EntryProjectId).IsSuccess);
        session.IngestFromSource(new SyntheticWalkingSource(20000L), WindowEnd.AddDays(-3), WindowEnd);

        var before = session.GetPendingReturnSummary()!;
        Assert.NotNull(before.PrimaryNextAction);

        // A committing-but-idle operation (no simulation events) must not touch it.
        Assert.True(session.SetAutoAdvance(session.GetHome().AutoAdvance).IsSuccess);
        var after = session.GetPendingReturnSummary();

        Assert.Equal(before.GeneratedAtUtc, after!.GeneratedAtUtc);
        Assert.Equal(before.Items.Count, after.Items.Count);
    }

    [Fact]
    public void Composer_BoundsOutput_AndPrioritizesTransformationOverAggregates()
    {
        var content = Region1Catalog.Create();
        var events = new List<WalkGame.Domain.Simulation.SimulationEvent>();
        for (int i = 0; i < 30; i++)
        {
            events.Add(new WalkGame.Domain.Simulation.ProjectCompleted(WindowEnd, "proj.filler-" + i.ToString("00")));
            events.Add(new WalkGame.Domain.Simulation.LandmarkStageReached(WindowEnd, "lm.filler-" + i.ToString("00"), RestorationStage.Functional));
            events.Add(new WalkGame.Domain.Simulation.ActivityCredited(WindowEnd, "tx-" + i, 5L));
            events.Add(new WalkGame.Domain.Simulation.ActivityDuplicate(WindowEnd, "tx-dup-" + i));
        }

        var composed = ReturnSummaryComposer.Compose(events, content, null, WindowEnd);

        Assert.True(composed.Items.Count <= PendingReturnSummaryState.MaxItems);
        Assert.Equal(SummaryItemKind.Transformation, composed.Items[0].Kind);
        Assert.True(composed.Items.Max(i => (int)i.Kind) >= composed.Items.Min(i => (int)i.Kind));
        Assert.All(composed.Items, i => Assert.False(string.IsNullOrWhiteSpace(i.Text)));
    }
}
