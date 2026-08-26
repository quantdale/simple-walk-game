using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.Persistence;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;
using WalkGame.Infrastructure.Persistence;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>
/// M8-H1 hardening acceptance scenario (campaign Workstream I): one named end-to-end
/// story traversing REAL production boundaries — trust-pipeline ingestion across
/// app-closed windows with session recreation, a persisted pending summary, an
/// interrupted commit followed by real recovery, corrections and deletions, full
/// history replay (twice), a very long absence, Region 1 closure, and final
/// serialize/reload equivalence.
///
/// Migration from historical fixtures lives in the dedicated
/// MatureSaveMigrationTests scenario, as the campaign specifies.
/// Nothing hand-edits canonical state: every mutation flows through GameSession use
/// cases over durable saves.
/// </summary>
public sealed class M8H1HardeningAcceptanceTests : IDisposable
{
    private const ulong Seed = 31UL;
    private const long StepsPerDay = 20000L;
    private static readonly DateTimeOffset T0 = TestSessions.T0;

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void FullHardeningStory_ThroughRealBoundaries_IsRecoverableExactlyOnceAndStable()
    {
        var content = Region1Catalog.Create();

        // ---- (1) Clean profile. -------------------------------------------------
        var bootstrap = Session(WindowEnd(0));
        Assert.Equal(StartStatus.NewGameCreated, bootstrap.StartNewGame(Seed).Status);
        bootstrap.SetAutoAdvance(true);

        // ---- (2)(3)(4) Days of valid activity, app closed between every window,
        //      reaching meaningful world state. -----------------------------------
        int day = 0;
        int completedAtPhaseOne = 0;
        for (day = 1; day <= 25; day++)
        {
            var session = Session(WindowEnd(day));
            Assert.Equal(StartStatus.Loaded, session.Continue().Status);
            IngestDaily(session, day);
            AutoQueue(session, content);
        }

        var phaseOneState = DecodePersisted();
        completedAtPhaseOne = phaseOneState.Region.Projects.Values.Count(p => p.Status == ProjectStatus.Completed);
        Assert.True(completedAtPhaseOne >= 2, "phase one should complete entry restoration work");

        // ---- (5) A later boot composes a durable pending return summary. --------
        var summarizingBoot = Session(WindowEnd(day));
        Assert.Equal(StartStatus.Loaded, summarizingBoot.Continue().Status);
        var pendingSummary = summarizingBoot.GetPendingReturnSummary();
        Assert.NotNull(pendingSummary);
        Assert.True(pendingSummary!.Items.Count > 0, "the pending summary must retain committed changes");

        long ledgerBeforeInterruption = DecodePersisted().Ledger.TotalVitalityCredited;

        // ---- (6) Persistence interruption mid-ingestion: the commit fails AFTER
        //      the state mutated in memory. The caller sees an IOException. -------
        var flakyInner = new AtomicFileSaveStore(_temp.Path);
        var flaky = new InterruptingStore(flakyInner);
        using (var interruptScope = new TempDirectory()) { } // keep naming symmetry; interruption uses same dir
        var interruptSession = new GameSession(
            flaky, TestSessions.NewCodec(), new ManualClock(WindowEnd(day + 1)), content);
        Assert.Equal(StartStatus.Loaded, interruptSession.Continue().Status);

        day++;
        flaky.FailNextWrites = 1;
        Assert.Throws<IOException>(() => IngestDaily(interruptSession, day));

        // ---- (7)(8) Boot through the real recovery path over the untouched
        //      durable generation; canonical state remains valid. -----------------
        var recovered = Session(WindowEnd(day));
        Assert.Equal(StartStatus.Loaded, recovered.Continue().Status);
        var recoveredState = DecodePersisted();
        Assert.Empty(GameStateValidator.Validate(recoveredState, content));
        Assert.Equal(ledgerBeforeInterruption, recoveredState.Ledger.TotalVitalityCredited);

        // The interrupted window's records were NOT durably committed; re-ingesting
        // them now credits exactly once.
        AutoQueue(recovered, content);
        var retryIngest = IngestDaily(recovered, day);
        Assert.True(retryIngest.VitalityCredited > 0L);

        // ---- (9)(10) More activity plus correction and deletion history. --------
        day += 3;
        var correctingSession = Session(WindowEnd(day));
        Assert.Equal(StartStatus.Loaded, correctingSession.Continue().Status);
        var correctionBatch = new List<NormalizedActivityRecord>
        {
            Record("acc-corr", 10_000L, startUtc: T0.AddDays(day - 1).AddHours(6)),
            Record("acc-del", 8_000L, startUtc: T0.AddDays(day - 1).AddHours(7)),
        };
        var firstPass = correctingSession.IngestActivityBatch(correctionBatch);
        Assert.Equal(2, firstPass.Accepted);

        var corrected = correctingSession.IngestActivityBatch(new List<NormalizedActivityRecord>
        {
            Record("acc-corr", 14_000L, revision: 2, startUtc: T0.AddDays(day - 1).AddHours(6)),
            Record("acc-del", 8_000L, revision: 2, isDeletion: true, startUtc: T0.AddDays(day - 1).AddHours(7)),
        });
        Assert.Equal(1, corrected.CorrectionsApplied);
        Assert.Equal(1, corrected.DeletionsApplied);
        Assert.True(corrected.Saved);

        // Keep daily continuity through the correction/interruption phase so that
        // "replay all known provider history" below means "already-processed history".
        for (int fill = 26; fill <= day; fill++)
        {
            var filler = Session(WindowEnd(fill));
            Assert.Equal(StartStatus.Loaded, filler.Continue().Status);
            IngestDaily(filler, fill);
            AutoQueue(filler, content);
        }

        // ---- (12)(13) Replay ALL known provider history: zero new reward. -------
        long ledgerBeforeReplay = DecodePersisted().Ledger.TotalVitalityCredited;
        var source = new SyntheticWalkingSource(StepsPerDay);
        for (int replayDay = 1; replayDay <= day; replayDay++)
        {
            var replaySession = Session(WindowEnd(replayDay));
            Assert.Equal(StartStatus.Loaded, replaySession.Continue().Status);
            replaySession.IngestFromSource(source, WindowStart(replayDay), WindowEnd(replayDay));
            replaySession.IngestActivityBatch(new[]
            {
                Record("acc-corr", 14_000L, revision: 2, startUtc: T0.AddDays(day - 1).AddHours(6)),
                Record("acc-del", 8_000L, revision: 2, isDeletion: true, startUtc: T0.AddDays(day - 1).AddHours(7)),
                Record("acc-del", 8_000L, startUtc: T0.AddDays(day - 1).AddHours(7)),
            });
        }
        Assert.Equal(ledgerBeforeReplay, DecodePersisted().Ledger.TotalVitalityCredited);

        // ---- (14)(15) Additional long absence, then drive to closure. -----------
        int lastProcessedBeforeAbsence = day;
        day += 200; // app fully closed for ~6.5 months: provider saw nothing new
        var afterAbsence = Session(WindowEnd(day));
        Assert.Equal(StartStatus.Loaded, afterAbsence.Continue().Status);
        var postAbsenceState = DecodePersisted();
        Assert.Empty(GameStateValidator.Validate(postAbsenceState, content));

        while (day < 400)
        {
            day++;
            var session = Session(WindowEnd(day));
            Assert.Equal(StartStatus.Loaded, session.Continue().Status);
            IngestDaily(session, day);
            AutoQueue(session, content);
            if (DecodePersisted().Region.IsCompleted)
                break;
        }

        var completionBytes = File.ReadAllBytes(Path.Combine(_temp.Path, "save.json"));
        var completedState = DecodePersisted();
        Assert.True(completedState.Region.IsCompleted);
        Assert.NotNull(completedState.Region.RegionCompletedAtUtc);
        Assert.All(completedState.Region.Projects.Values,
            p => Assert.Equal(ProjectStatus.Completed, p.Status));

        // ---- (16)(17)(18) Restart; replay everything already processed; closure
        //      and the economic core stay stable. ---------------------------------
        long ledgerAtCompletion = completedState.Ledger.TotalVitalityCredited;
        var restarted = Session(WindowEnd(day + 1));
        Assert.Equal(StartStatus.Loaded, restarted.Continue().Status);

        void ReplayProcessedRange(int firstDay, int lastDay)
        {
            for (int replayDay = firstDay; replayDay <= lastDay; replayDay++)
            {
                var replaySession = Session(WindowEnd(replayDay));
                replaySession.Continue();
                replaySession.IngestFromSource(source, WindowStart(replayDay), WindowEnd(replayDay));
            }
        }

        ReplayProcessedRange(1, lastProcessedBeforeAbsence);
        ReplayProcessedRange(lastProcessedBeforeAbsence + 201, day); // absence days had no provider data

        var afterFullReplay = DecodePersisted();
        Assert.Equal(ledgerAtCompletion, afterFullReplay.Ledger.TotalVitalityCredited);
        Assert.True(afterFullReplay.Region.IsCompleted);
        Assert.Equal(completedState.Region.RegionCompletedAtUtc, afterFullReplay.Region.RegionCompletedAtUtc);
        Assert.Empty(GameStateValidator.Validate(afterFullReplay, content));

        // ---- (20) Serialize/reload once more and prove equivalence. -------------
        byte[] finalBytes = File.ReadAllBytes(Path.Combine(_temp.Path, "save.json"));
        var codec = TestSessions.NewCodec();
        var redecoded = codec.Decode(finalBytes);
        Assert.Equal(CodecStatus.Ok, redecoded.Status);
        byte[] reencoded = codec.Encode(redecoded.State!, WindowEnd(day));
        Assert.True(reencoded.AsSpan().SequenceEqual(finalBytes),
            "encode(decode(final)) must reproduce the exact durable bytes");
    }

    // ------------------------------------------------------------------
    // Helpers.
    // ------------------------------------------------------------------

    private IngestResult IngestDaily(GameSession session, int day)
    {
        var result = session.IngestFromSource(new SyntheticWalkingSource(StepsPerDay),
            WindowStart(day), WindowEnd(day));
        Assert.True(result.Saved || result.DuplicatesIgnored > 0 || result.Rejected > 0,
            "every daily ingestion must be durably committed unless it was a pure no-op");
        return result;
    }

    private static void AutoQueue(GameSession session, RegionDefinition content)
    {
        var home = session.GetHome();
        if (home.ActiveProjectId != null || home.Queued.Count > 0)
            return;
        foreach (var definition in content.Projects)
            if (session.EnqueueProject(definition.Id.Value).IsSuccess)
                return;
    }

    private GameSession Session(DateTimeOffset now) =>
        TestSessions.Create(_temp.Path, new ManualClock(now));

    private GameState DecodePersisted()
    {
        var decoded = TestSessions.NewCodec().Decode(File.ReadAllBytes(Path.Combine(_temp.Path, "save.json")));
        Assert.Equal(CodecStatus.Ok, decoded.Status);
        return decoded.State!;
    }

    private static NormalizedActivityRecord Record(
        string id, long steps, int revision = 1, bool isDeletion = false, DateTimeOffset? startUtc = null) =>
        new NormalizedActivityRecord(
            ProviderNamespace: "acceptance.provider",
            SourceRecordId: id,
            Category: ActivityCategory.Walking,
            Unit: ActivityUnits.Steps,
            Quantity: steps,
            StartUtc: startUtc ?? T0.AddMinutes(-60),
            EndUtc: (startUtc ?? T0.AddMinutes(-60)).AddMinutes(40),
            Revision: revision,
            IsDeletion: isDeletion);

    /// <summary>Injects IOException at the atomic-commit boundary.</summary>
    private sealed class InterruptingStore : ISaveStore
    {
        private readonly ISaveStore _inner;

        public InterruptingStore(ISaveStore inner) => _inner = inner;

        public int FailNextWrites { get; set; }

        public void WriteAtomic(byte[] envelopeBytes)
        {
            if (FailNextWrites > 0)
            {
                FailNextWrites--;
                throw new IOException("Injected persistence interruption.");
            }
            _inner.WriteAtomic(envelopeBytes);
        }

        public void WriteAtomicPreservingBackup(byte[] envelopeBytes) =>
            _inner.WriteAtomicPreservingBackup(envelopeBytes);

        public SaveReadResult ReadPrimary() => _inner.ReadPrimary();

        public SaveReadResult ReadBackup() => _inner.ReadBackup();
    }

    private static DateTimeOffset WindowStart(int day) => T0.AddDays(day - 1);

    private static DateTimeOffset WindowEnd(int day) => T0.AddDays(day);
}
