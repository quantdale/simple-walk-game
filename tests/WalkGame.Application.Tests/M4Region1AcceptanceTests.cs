using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.Persistence;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Simulation;
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;
using WalkGame.Application.ReadModels;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Application.Tests;

/// <summary>
/// THE M4 Region 1 acceptance proof (campaign workstream G): drives a clean profile
/// through the REAL trust/progression stack — normalized synthetic records via
/// IngestActivityBatch, deterministic auto-player queue decisions, fresh GameSession from
/// disk between every app-closed day — until the Region 1 closure milestone is reached,
/// then proves replay/exactly-once safety, post-completion stability and validator-clean
/// final canonical state, plus byte-identical determinism for identical inputs.
/// </summary>
public sealed class M4Region1AcceptanceTests : IDisposable
{
    private const ulong Seed = 11UL;
    private const long StepsPerDay = 20000L; // high-ish profile: 200 Vitality/day
    private const int HorizonDays = 220;
    private static readonly DateTimeOffset T0 = TestSessions.T0;

    private readonly TempDirectory _tempA = new();
    private readonly TempDirectory _tempB = new();

    public void Dispose()
    {
        _tempA.Dispose();
        _tempB.Dispose();
    }

    [Fact]
    public void Region1_CompletesHeadlessly_ThroughTheRealPipeline_IsExactlyOnceAndDeterministic()
    {
        // ---- (1) Initial graph is valid and exposes reachable entry work. ----
        var content = Region1Catalog.Create();
        Assert.Empty(ContentValidator.Validate(content));

        var bootstrap = Session(_tempA.Path, WindowEnd(0));
        Assert.Equal(StartStatus.NewGameCreated, bootstrap.StartNewGame(Seed).Status);
        var home0 = bootstrap.GetHome();
        Assert.Equal(ProjectStatus.Available,
            bootstrap.GetProjects().Projects.Single(p => p.ProjectId == TestSessions.EntryProjectId).Status);

        // ---- (2)(3)(10) Daily app-closed windows through the trust pipeline with an
        //      automatic low-decision player; session recreated from disk every day. ----
        int decisions = 0;
        int day = 0;
        bool completed = false;
        while (day < HorizonDays && !completed)
        {
            day++;
            var session = Session(_tempA.Path, WindowEnd(day));
            Assert.Equal(StartStatus.Loaded, session.Continue().Status);

            var ingest = session.IngestFromSource(new SyntheticWalkingSource(StepsPerDay),
                WindowStart(day), WindowEnd(day));
            Assert.Equal(1, ingest.Accepted);
            Assert.True(ingest.Saved);

            // Deterministic player policy: one decision when the pipeline is fully idle.
            var home = session.GetHome();
            if (home.ActiveProjectId == null && home.Queued.Count == 0)
            {
                foreach (var definition in content.Projects.Select(p => p.Id.Value))
                {
                    if (session.EnqueueProject(definition).IsSuccess)
                    {
                        decisions++;
                        break;
                    }
                }
            }

            completed = session.GetRegion().RegionCompleted;
        }

        // ---- (9) The closure milestone was reached inside the horizon. ----
        Assert.True(completed, $"region did not complete within {HorizonDays} days");
        Assert.InRange(decisions, content.Projects.Count, content.Projects.Count); // one per project, no more

        // Determinism baseline captured BEFORE review/idle/replay mutations below.
        byte[] completionBytesA = File.ReadAllBytes(Path.Combine(_tempA.Path, "save.json"));

        var finalA = ReadState(_tempA.Path);
        Assert.True(finalA.Region.IsCompleted);
        Assert.NotNull(finalA.Region.RegionCompletedAtUtc);
        Assert.Equal(content.CompletionMilestoneProjectId,
            content.Projects.Single(p => p.Id.Value == content.CompletionMilestoneProjectId).Id.Value);

        // All restoration work finished: every project completed.
        Assert.All(finalA.Region.Projects.Values, p => Assert.Equal(ProjectStatus.Completed, p.Status));

        // ---- (4) Several landmark stages changed; key landmarks reached their tops. ----
        Assert.Equal(RestorationStage.Restored, finalA.Region.LandmarkStages["lm.trailhead"]);
        Assert.Equal(RestorationStage.Restored, finalA.Region.LandmarkStages["lm.river-intake"]);
        Assert.Equal(RestorationStage.Flourishing, finalA.Region.LandmarkStages["lm.canopy"]);
        Assert.Equal(RestorationStage.Restored, finalA.Region.LandmarkStages["lm.settlement"]);
        Assert.Equal(RestorationStage.Flourishing, finalA.Region.LandmarkStages["lm.wetland"]);
        Assert.Equal(RestorationStage.Restored, finalA.Region.LandmarkStages["lm.observatory"]);

        // ---- (5) Both additional producers unlocked and stayed within documented bounds. ----
        foreach (var producerId in new[] { "prd.workshop-salvage", "prd.nursery-greenhouse", "prd.observatory-archive" })
        {
            var definition = content.Producers.Single(p => p.Id.Value == producerId);
            var runtime = finalA.Region.FindProducer(producerId)!;
            Assert.True(runtime.Unlocked);
            Assert.InRange(runtime.StoredMilliUnits, 0L, definition.CapacityUnits * ProducerDefinition.MilliUnitsPerUnit);
            Assert.True(runtime.TotalProducedMilliUnits >= 0L);
        }

        // ---- (6) All discoveries unlocked exactly once; review state independent. ----
        Assert.Equal(content.Discoveries.Count, finalA.Region.Discoveries.Count);
        var discoverySession = Session(_tempA.Path, WindowEnd(day));
        Assert.Equal(StartStatus.Loaded, discoverySession.Continue().Status);
        var journalBeforeReview = discoverySession.GetDiscoveries();
        Assert.Equal(journalBeforeReview.TotalDiscoveries, journalBeforeReview.UnlockedCount);
        Assert.Contains(journalBeforeReview.Discoveries, d => d.DiscoveryId == "disc.old-millstone" && !d.Reviewed);
        Assert.True(discoverySession.MarkDiscoveryReviewed("disc.old-millstone").IsSuccess);

        var reloadedAfterReview = Session(_tempA.Path, WindowEnd(day));
        reloadedAfterReview.Continue();
        Assert.True(reloadedAfterReview.GetDiscoveries().Discoveries.Single(d => d.DiscoveryId == "disc.old-millstone").Reviewed);
        // Reviewing again is idempotent.
        Assert.True(reloadedAfterReview.MarkDiscoveryReviewed("disc.old-millstone").IsSuccess);

        // ---- (7) Expedition hooks became available and completed deterministically. ----
        var expeditions = reloadedAfterReview.GetExpeditions();
        Assert.Equal(3, expeditions.CompletedCount);
        Assert.All(expeditions.Expeditions, e => Assert.Equal(ExpeditionsReadModel.ExpeditionStatus.Completed, e.Status));

        // ---- (8) Ecological and settlement arcs progressed to their final stages. ----
        Assert.Equal(content.EcologyProgression.Stages.Count, finalA.Region.EcologyStage);
        Assert.Equal(content.SettlementProgression.Stages.Count, finalA.Region.SettlementStage);

        // ---- (12) Post-completion stability across further app-closed days. ----
        long ledgerAtCompletion = finalA.Ledger.TotalVitalityCredited;
        for (int extra = 1; extra <= 5; extra++)
        {
            var idleSession = Session(_tempA.Path, WindowEnd(day + extra));
            Assert.Equal(StartStatus.Loaded, idleSession.Continue().Status);
        }
        var afterIdle = ReadState(_tempA.Path);
        Assert.True(afterIdle.Region.IsCompleted);
        Assert.Equal(finalA.Region.RegionCompletedAtUtc, afterIdle.Region.RegionCompletedAtUtc);
        Assert.Equal(ProjectStatus.Completed, afterIdle.Region.FindProject(content.CompletionMilestoneProjectId!)!.Status);
        Assert.Equal(content.Discoveries.Count, afterIdle.Region.Discoveries.Count);
        Assert.Equal(ledgerAtCompletion, afterIdle.Ledger.TotalVitalityCredited);

        // ---- (11) Replaying the ENTIRE activity window is an exactly-once no-op. ----
        var source = new SyntheticWalkingSource(StepsPerDay);
        for (int replayDay = 1; replayDay <= day; replayDay++)
        {
            var replaySession = Session(_tempA.Path, WindowEnd(replayDay));
            Assert.Equal(StartStatus.Loaded, replaySession.Continue().Status);
            var result = replaySession.IngestFromSource(source, WindowStart(replayDay), WindowEnd(replayDay));
            Assert.Equal(0L, result.VitalityCredited);
            Assert.Equal(1, result.DuplicatesIgnored);
        }
        var afterReplay = ReadState(_tempA.Path);
        Assert.Equal(ledgerAtCompletion, afterReplay.Ledger.TotalVitalityCredited);
        Assert.Equal(finalA.Region.Projects.Values.Count(p => p.Status == ProjectStatus.Completed),
            afterReplay.Region.Projects.Values.Count(p => p.Status == ProjectStatus.Completed));

        // ---- (13) Final canonical state validates cleanly against authored content. ----
        Assert.Empty(GameStateValidator.Validate(afterReplay, content));

        // ---- Determinism: the identical script from fresh state is byte-identical. ----
        RunIdenticalScript(_tempB.Path);
        byte[] bytesB = File.ReadAllBytes(Path.Combine(_tempB.Path, "save.json"));
        if (!bytesB.AsSpan().SequenceEqual(completionBytesA))
            Assert.True(false, "identical inputs must produce byte-identical saves");
    }

    private static void RunIdenticalScript(string directory)
    {
        var content = Region1Catalog.Create();
        var bootstrap = Session(directory, WindowEnd(0));
        Assert.Equal(StartStatus.NewGameCreated, bootstrap.StartNewGame(Seed).Status);

        int day = 0;
        bool completed = false;
        while (day < HorizonDays && !completed)
        {
            day++;
            var session = Session(directory, WindowEnd(day));
            Assert.Equal(StartStatus.Loaded, session.Continue().Status);
            session.IngestFromSource(new SyntheticWalkingSource(StepsPerDay), WindowStart(day), WindowEnd(day));

            var home = session.GetHome();
            if (home.ActiveProjectId == null && home.Queued.Count == 0)
            {
                foreach (var definition in content.Projects.Select(p => p.Id.Value))
                    if (session.EnqueueProject(definition).IsSuccess)
                        break;
            }

            completed = session.GetRegion().RegionCompleted;
        }
    }

    private static GameSession Session(string directory, DateTimeOffset now)
    {
        var session = TestSessions.Create(directory, new ManualClock(now));
        return session;
    }

    private static GameState ReadState(string directory)
    {
        var codec = TestSessions.NewCodec();
        var decoded = codec.Decode(File.ReadAllBytes(Path.Combine(directory, "save.json")));
        Assert.Equal(CodecStatus.Ok, decoded.Status);
        return decoded.State!;
    }

    private static DateTimeOffset WindowStart(int day) => T0.AddDays(day - 1);

    private static DateTimeOffset WindowEnd(int day) => T0.AddDays(day);
}
