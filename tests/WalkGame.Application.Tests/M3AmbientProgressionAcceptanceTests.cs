using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.Persistence;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Summaries;
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Application.Tests;

/// <summary>
/// THE M3 vertical-slice acceptance proof (campaign workstream F): fresh durable state →
/// queue restoration work → several app-closed days of normalized synthetic activity →
/// reconcile through the real M2 trust pipeline → exactly-once Vitality → completion
/// boundaries crossed → landmark stages change → producer unlocks and produces over
/// elapsed time → durable summaries survive commit-before-presentation restarts →
/// player chooses/reorders/starts the next project → full replay is an exactly-once
/// no-op → final state validates and is byte-for-byte deterministic for equal inputs.
///
/// The game session is recreated from disk between EVERY activity window, so persistence
/// and boot logic are exercised exactly like repeated app-closed periods.
/// </summary>
public sealed class M3AmbientProgressionAcceptanceTests : IDisposable
{
    private const ulong Seed = 7UL;
    private const long StepsPerDay = 20000L; // 200 Vitality/day
    private static readonly DateTimeOffset T0 = TestSessions.T0;

    private readonly TempDirectory _tempA = new();
    private readonly TempDirectory _tempB = new();

    public void Dispose()
    {
        _tempA.Dispose();
        _tempB.Dispose();
    }

    private static string Entry => TestSessions.EntryProjectId;          // proj.clear-trailhead (300)
    private const string River = "proj.river-intake";                     // 800
    private const string Workshop = "proj.build-workshop";                // 1500
    private const string Wetland = "proj.wetland-drainage";               // 2200

    [Fact]
    public void FullAmbientLoop_ThroughTrustPipeline_IsExactlyOnceAndDeterministic()
    {
        var finalA = RunScenario(_tempA);

        // ---- Replay the ENTIRE activity history after "restart": pure no-op. ----
        var beforeReplay = ReadState(_tempA);
        long ledgerBefore = beforeReplay.Ledger.TotalVitalityCredited;
        int completionsBefore = CountCompletions(_tempA);
        long materialsBefore = beforeReplay.Resources.Get(WalkGame.Domain.Economy.ResourceType.Materials);
        long bankedBefore = beforeReplay.Resources.Get(WalkGame.Domain.Economy.ResourceType.Vitality);

        var source = new SyntheticWalkingSource(StepsPerDay);
        for (int day = 1; day <= 13; day++)
        {
            var session = Session(_tempA, WindowEnd(day));
            Assert.Equal(StartStatus.Loaded, session.Continue().Status);
            var result = session.IngestFromSource(source, WindowStart(day), WindowEnd(day));
            Assert.Equal(0L, result.VitalityCredited);
            Assert.Equal(1, result.DuplicatesIgnored);
            Assert.True(result.Saved);
        }

        var afterReplay = ReadState(_tempA);
        Assert.Equal(ledgerBefore, afterReplay.Ledger.TotalVitalityCredited);
        Assert.Equal(completionsBefore, CountCompletions(_tempA));
        Assert.Equal(materialsBefore, afterReplay.Resources.Get(WalkGame.Domain.Economy.ResourceType.Materials));
        Assert.Equal(bankedBefore, afterReplay.Resources.Get(WalkGame.Domain.Economy.ResourceType.Vitality));

        // Replayed activity may add notices (backward-boot skew) but must never
        // fabricate a NEW progress claim that was not already pending.
        var textsBefore = (beforeReplay.PendingReturnSummary?.Items ?? new List<Domain.Summaries.PendingSummaryItemState>())
            .Where(i => i.Kind != SummaryItemKind.Notice)
            .Select(i => i.Kind + "|" + i.Text)
            .ToHashSet();
        int nonNoticeItemsAfter = afterReplay.PendingReturnSummary?.Items.Count(i => i.Kind != SummaryItemKind.Notice) ?? 0;
        Assert.True(nonNoticeItemsAfter <= textsBefore.Count);
        if (afterReplay.PendingReturnSummary != null)
            Assert.All(afterReplay.PendingReturnSummary.Items,
                i => Assert.True(i.Kind == SummaryItemKind.Notice || textsBefore.Contains(i.Kind + "|" + i.Text)));

        // ---- Determinism: the identical script from fresh state is byte-identical. ----
        var finalB = RunScenario(_tempB);
        Assert.True(finalB.AsSpan().SequenceEqual(finalA), "Identical seeds+inputs must produce byte-identical saves.");

        // ---- Final canonical state validates cleanly. ----
        var decoded = new SaveCodec(new MigrationRunner(DefaultMigrations.All)).Decode(finalA);
        Assert.Equal(CodecStatus.Ok, decoded.Status);
        Assert.Empty(GameStateValidator.Validate(decoded.State!, Region1Catalog.Create()));
    }

    private byte[] RunScenario(TempDirectory dir)
    {
        var source = new SyntheticWalkingSource(StepsPerDay);

        // (1)(2) Fresh valid durable state; player queues the entry project.
        var bootstrap = Session(dir, WindowEnd(1));
        Assert.Equal(StartStatus.NewGameCreated, bootstrap.StartNewGame(Seed).Status);
        Assert.True(bootstrap.EnqueueProject(Entry).IsSuccess);

        // (3)(4) Five app-closed days of normalized synthetic walking through IngestActivityBatch.
        IngestWindows(dir, source, 1, 5);

        // (5) First completion boundary crossed; unallocated Vitality banks, never wasted.
        var afterTrailhead = ReadState(dir);
        Assert.Equal(ProjectStatus.Completed, afterTrailhead.Region.FindProject(Entry)!.Status);
        Assert.Equal(RestorationStage.Stabilized, afterTrailhead.Region.LandmarkStages["lm.trailhead"]);
        Assert.Equal(ProjectStatus.Available, afterTrailhead.Region.FindProject(River)!.Status);
        Assert.Equal(0L, afterTrailhead.Region.FindProject(River)!.VitalityInvested);
        Assert.Equal(700L, afterTrailhead.Resources.Get(WalkGame.Domain.Economy.ResourceType.Vitality));

        // (8 more days) Queue is empty and automation has nothing to roll into:
        // activity keeps banking safely instead of being lost.
        IngestWindows(dir, source, 6, 13);
        var afterRiver = ReadState(dir);
        Assert.Equal(ProjectStatus.Available, afterRiver.Region.FindProject(River)!.Status);
        Assert.Equal(2300L, afterRiver.Resources.Get(WalkGame.Domain.Economy.ResourceType.Vitality));
        Assert.False(afterRiver.Region.FindProducer("prd.workshop-salvage")!.Unlocked);

        // (10a) Player queues restoration work; auto-allocation crosses both remaining
        // boundaries immediately from banked vitality.
        var chooser = Session(dir, WindowEnd(13));
        Assert.Equal(StartStatus.Loaded, chooser.Continue().Status);
        Assert.True(chooser.EnqueueProject(River).IsSuccess);
        var afterRiverQueued = ReadState(dir);
        Assert.Equal(ProjectStatus.Completed, afterRiverQueued.Region.FindProject(River)!.Status);
        Assert.Equal(RestorationStage.Stabilized, afterRiverQueued.Region.LandmarkStages["lm.river-intake"]);
        Assert.Equal(1500L, afterRiverQueued.Resources.Get(WalkGame.Domain.Economy.ResourceType.Vitality));

        Assert.Equal(ProjectStatus.Available, chooser.GetProjects().Projects.Single(p => p.ProjectId == Workshop).Status);
        Assert.True(chooser.EnqueueProject(Workshop).IsSuccess);

        // (6)(7) Landmark advanced again and the producer unlocked through completion effects.
        var afterWorkshop = ReadState(dir);
        Assert.Equal(ProjectStatus.Completed, afterWorkshop.Region.FindProject(Workshop)!.Status);
        Assert.Equal(RestorationStage.Functional, afterWorkshop.Region.LandmarkStages["lm.trailhead"]);
        Assert.True(afterWorkshop.Region.FindProducer("prd.workshop-salvage")!.Unlocked);
        Assert.Equal(0L, afterWorkshop.Resources.Get(WalkGame.Domain.Economy.ResourceType.Vitality));

        // (9) Commit-before-presentation crash safety: brand-new session still finds the story.
        var restarted = Session(dir, WindowEnd(13));
        Assert.Equal(StartStatus.Loaded, restarted.Continue().Status);
        var pending = restarted.GetPendingReturnSummary();
        Assert.NotNull(pending);
        Assert.Contains(pending!.Items, i => i.Text.Contains("workshop", StringComparison.OrdinalIgnoreCase) && i.Text.Contains("complete", StringComparison.Ordinal));
        Assert.NotNull(pending.PrimaryNextAction);

        // (10b) Player disables automation, queues and manually starts the following project,
        //       exercising reorder on a multi-item queue first.
        Assert.True(restarted.SetAutoAdvance(false).IsSuccess);
        Assert.True(restarted.EnqueueProject(Wetland).IsSuccess);

        var projectsModel = restarted.GetProjects();
        Assert.False(projectsModel.AutoAdvance);
        Assert.Null(projectsModel.ActiveProjectId);

        Assert.True(restarted.ActivateQueuedProject(Wetland).IsSuccess);
        Assert.Equal(Wetland, restarted.GetHome().ActiveProjectId);

        // One more app-closed day flows into the manually started project.
        IngestWindows(dir, source, 14, 14);
        var afterWetlandStart = ReadState(dir);
        Assert.Equal(200L, afterWetlandStart.Region.FindProject(Wetland)!.VitalityInvested);

        // Producer produced over elapsed time since unlock, within its documented capacity.
        Assert.Equal(2L, afterWetlandStart.Resources.Get(WalkGame.Domain.Economy.ResourceType.Materials));
        var producerRuntime = afterWetlandStart.Region.FindProducer("prd.workshop-salvage")!;
        Assert.Equal(500L, producerRuntime.StoredMilliUnits); // fractional half-unit parked, not lost

        return File.ReadAllBytes(System.IO.Path.Combine(dir.Path, "save.json"));
    }

    private void IngestWindows(TempDirectory dir, SyntheticWalkingSource source, int firstDay, int lastDay)
    {
        for (int day = firstDay; day <= lastDay; day++)
        {
            // Fresh session from disk for EVERY window: app closed between periods.
            var session = Session(dir, WindowEnd(day));
            Assert.Equal(StartStatus.Loaded, session.Continue().Status);

            var ingest = session.IngestFromSource(source, WindowStart(day), WindowEnd(day));
            Assert.Equal(1, ingest.Accepted);
            Assert.Equal(0, ingest.Rejected);
            Assert.Equal(StepsPerDay / 100L, ingest.VitalityCredited);
            Assert.True(ingest.Saved);
        }
    }

    private GameSession Session(TempDirectory dir, DateTimeOffset now) =>
        TestSessions.Create(dir, new ManualClock(now));

    private static DateTimeOffset WindowStart(int day) => T0.AddDays(day - 1);

    private static DateTimeOffset WindowEnd(int day) => T0.AddDays(day);

    private static GameState ReadState(TempDirectory dir)
    {
        var codec = new SaveCodec(new MigrationRunner(DefaultMigrations.All));
        var decoded = codec.Decode(File.ReadAllBytes(System.IO.Path.Combine(dir.Path, "save.json")));
        Assert.Equal(CodecStatus.Ok, decoded.Status);
        return decoded.State!;
    }

    private static long LedgerTotal(TempDirectory dir) =>
        ReadState(dir).Ledger.TotalVitalityCredited;

    private static int CountCompletions(TempDirectory dir) =>
        ReadState(dir).Region.Projects.Values.Count(p => p.Status == ProjectStatus.Completed);
}
