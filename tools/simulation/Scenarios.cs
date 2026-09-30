using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.Persistence;
using WalkGame.Domain;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;
using WalkGame.Infrastructure.Persistence;

/// <summary>
/// PERFORMANCE_BUDGETS section 19 regression scenarios, restricted to the subset that
/// is honestly measurable without a Unity editor or a device.
///
/// The ten named scenarios split into three groups:
///   - headless-measurable: cold boot with mature save, return after 7 days with a large
///     activity batch, mature region with maximum unlocked content, long-running
///     save/ledger growth, and the canonical cost of a landmark stage transition;
///   - headless-surrogate: the background/resume loop, proxied by repeated boot-from-disk
///     plus advance. This is NOT real backgrounding and is labelled as a surrogate;
///   - device-only: Home idle, entering/traversing/exiting Visit World. These are NOT
///     simulated here. Fabricating a number for them would be worse than omitting them.
///
/// Every line is machine-readable SCENARIO-RESULT / SCENARIO-SIZES so the deterministic
/// half (byte sizes, ledger counts, credited totals) can be CI-gated, while wall-clock
/// figures stay evidence-only. Timing assertions are deliberately NOT gated: they are
/// machine-dependent, and a flaky timing gate is a defect (TESTING_AND_RELEASE section 17).
/// </summary>
internal static class Scenarios
{
    private const long StepsPerDay = 8000L;

    public static int Run(string[] tokens)
    {
        var a = new SimArgs(tokens);
        int maturityDays = a.Has("--days") ? a.Int("--days") : 200;
        if (maturityDays is < 1 or > 3650)
            throw new CliUsageException("--days must be between 1 and 3650");

        ulong seed = a.Has("--seed") ? a.ULong("--seed") : 42UL;
        var baseInstant = a.Has("--at")
            ? a.Iso("--at")
            : new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

        var content = Region1Catalog.Create();
        string root = Path.Combine(a.SaveDir, "scenario-work");
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        Directory.CreateDirectory(root);

        ISaveCodec NewCodec() => new SaveCodec(new MigrationRunner(DefaultMigrations.All));
        GameSession NewSession(IClock c, string dir) => new(new AtomicFileSaveStore(dir), NewCodec(), c, content);

        // ------------------------------------------------------------------
        // Scenario 1: cold boot with a mature save.
        // ------------------------------------------------------------------
        string matureDir = Path.Combine(root, "mature");
        Directory.CreateDirectory(matureDir);
        var growClock = new ManualClock(baseInstant);
        var grower = NewSession(growClock, matureDir);
        var started = grower.StartNewGame(seed: seed);
        if (started.Status != StartStatus.NewGameCreated && started.Status != StartStatus.Loaded)
            throw CliScenarioException.FromBoot("scenario 1 setup", started.Status);
        for (int day = 1; day <= maturityDays; day++)
        {
            growClock.Advance(TimeSpan.FromDays(1));
            _ = grower.IngestFromSource(new SyntheticWalkingSource(StepsPerDay), growClock.UtcNow.AddDays(-1), growClock.UtcNow);
        }
        string matureSave = Path.Combine(matureDir, "save.json");
        long matureBytes = new FileInfo(matureSave).Length;

        // Time cold boot: a brand new session, clock at a later instant, loading from disk.
        var coldClock = new ManualClock(growClock.UtcNow.AddDays(3));
        var coldSw = Stopwatch.StartNew();
        var coldSession = NewSession(coldClock, matureDir);
        var coldStart = coldSession.Continue();
        coldSw.Stop();
        if (coldStart.Status != StartStatus.Loaded)
            throw CliScenarioException.FromBoot("scenario 1 cold boot", coldStart.Status);
        Console.WriteLine(
            $"SCENARIO-RESULT id=cold-boot-mature-save days={maturityDays} bootMs={coldSw.Elapsed.TotalMilliseconds:F2} " +
            $"status={coldStart.Status} saveBytes={matureBytes}");

        // ------------------------------------------------------------------
        // Scenario 2: return after 7 days with a large activity batch.
        // ------------------------------------------------------------------
        string returnDir = Path.Combine(root, "return7");
        Directory.CreateDirectory(returnDir);
        var returnClock = new ManualClock(baseInstant);
        var returnSession = NewSession(returnClock, returnDir);
        _ = returnSession.StartNewGame(seed: seed);
        // A week away with no interaction at all.
        var awayClockUtc = returnClock.UtcNow.AddDays(7);
        var returnSw = Stopwatch.StartNew();
        var resumed = NewSession(new ManualClock(awayClockUtc), returnDir);
        var resumedStart = resumed.Continue();
        if (resumedStart.Status != StartStatus.Loaded)
            throw CliScenarioException.FromBoot("scenario 2 resume", resumedStart.Status);
        // One aggregated 7-day batch: 7 records, ~7x a normal day.
        var sevenDays = returnClock.UtcNow.AddDays(7);
        var batchIngest = resumed.IngestFromSource(new SyntheticWalkingSource(StepsPerDay), returnClock.UtcNow, sevenDays);
        returnSw.Stop();
        Console.WriteLine(
            $"SCENARIO-RESULT id=return-7-days-large-batch ingestMs={returnSw.Elapsed.TotalMilliseconds:F2} " +
            $"credited={batchIngest.VitalityCredited} accepted={batchIngest.Accepted} " +
            $"duplicates={batchIngest.DuplicatesIgnored} expectedSteps={StepsPerDay * 7}");

        // ------------------------------------------------------------------
        // Scenario 6 (canonical half): landmark stage transition cost.
        // Headless measures the canonical effect, never the rendered frame cost.
        // ------------------------------------------------------------------
        string landmarkDir = Path.Combine(root, "landmark");
        Directory.CreateDirectory(landmarkDir);
        var landmarkClock = new ManualClock(baseInstant);
        var landmarkSession = NewSession(landmarkClock, landmarkDir);
        _ = landmarkSession.StartNewGame(seed: seed);
        var landmarkSw = Stopwatch.StartNew();
        // Clear the old trailhead is the first entry project and drives the trailhead stage.
        var enqueue = landmarkSession.EnqueueProject("proj.clear-trailhead");
        landmarkSw.Stop();
        var beforeStages = landmarkSession.GetRegion().Landmarks.Count(l => l.Stage > 0);
        for (int day = 1; day <= 60 && !landmarkSession.GetRegion().RegionCompleted; day++)
        {
            landmarkClock.Advance(TimeSpan.FromDays(1));
            var daily = NewSession(new ManualClock(landmarkClock.UtcNow), landmarkDir);
            if (daily.Continue().Status != StartStatus.Loaded) break;
            _ = daily.IngestFromSource(new SyntheticWalkingSource(StepsPerDay), landmarkClock.UtcNow.AddDays(-1), landmarkClock.UtcNow);
            var home = daily.GetHome();
            if (home.ActiveProjectId == null && home.Queued.Count == 0)
                foreach (var pid in content.Projects.Select(p => p.Id.Value).ToList())
                    if (daily.EnqueueProject(pid).IsSuccess) break;
            var stages = daily.GetRegion().Landmarks.Count(l => l.Stage > 0);
            if (stages > beforeStages) { beforeStages = stages; break; }
        }
        var finalRegion = landmarkSession.GetRegion();
        Console.WriteLine(
            $"SCENARIO-RESULT id=landmark-stage-transition enqueueMs={landmarkSw.Elapsed.TotalMilliseconds:F2} " +
            $"enqueueOk={enqueue.IsSuccess} advancedLandmarks={beforeStages} " +
            $"note=canonical-only;frame-cost-unmeasured");

        // ------------------------------------------------------------------
        // Scenario 8 (surrogate): background/resume loop, proxied by repeated
        // boot-from-disk + advance. Explicitly a surrogate, not real backgrounding.
        // ------------------------------------------------------------------
        string resumeDir = Path.Combine(root, "resume");
        Directory.CreateDirectory(resumeDir);
        var resumeClock = new ManualClock(baseInstant);
        var resumeSession = NewSession(resumeClock, resumeDir);
        _ = resumeSession.StartNewGame(seed: seed);
        var resumeSw = Stopwatch.StartNew();
        int cycles = 0;
        for (int cycle = 1; cycle <= 20; cycle++)
        {
            resumeClock.Advance(TimeSpan.FromDays(1));
            var again = NewSession(new ManualClock(resumeClock.UtcNow), resumeDir);
            if (again.Continue().Status != StartStatus.Loaded)
                throw new CliScenarioException(3, "scenario 8 resume cycle failed at cycle " + cycle + ".");
            _ = again.IngestFromSource(new SyntheticWalkingSource(StepsPerDay), resumeClock.UtcNow.AddDays(-1), resumeClock.UtcNow);
            var homeState = again.GetHome();
            if (homeState.ActiveProjectId == null && homeState.Queued.Count == 0)
                foreach (var pid in content.Projects.Select(p => p.Id.Value).ToList())
                    if (again.EnqueueProject(pid).IsSuccess) break;
            cycles++;
        }
        resumeSw.Stop();
        var resumedBytes = new FileInfo(Path.Combine(resumeDir, "save.json")).Length;
        Console.WriteLine(
            $"SCENARIO-RESULT id=background-resume-surrogate cycles={cycles} " +
            $"totalMs={resumeSw.Elapsed.TotalMilliseconds:F2} perCycleMs={resumeSw.Elapsed.TotalMilliseconds / cycles:F2} " +
            $"saveBytes={resumedBytes} note=surrogate-not-real-backgrounding");

        // ------------------------------------------------------------------
        // Scenarios 9 + 15: mature region at maximum unlocked content, and the
        // long-running save/ledger growth series (§15 asks for this in CI).
        // ------------------------------------------------------------------
        string growthDir = Path.Combine(root, "growth");
        Directory.CreateDirectory(growthDir);
        var growthClock = new ManualClock(baseInstant);
        var growthSession = NewSession(growthClock, growthDir);
        _ = growthSession.StartNewGame(seed: seed);

        var catalogOrder = content.Projects.Select(p => p.Id.Value).ToList();
        var checkpoints = new SortedSet<int>();
        foreach (int mark in new[] { 7, 30, 90, 180, 365, 730 }) checkpoints.Add(mark);

        string growthSave = Path.Combine(growthDir, "save.json");
        int growthDay = 0;
        bool regionComplete = false;
        foreach (int mark in checkpoints)
        {
            while (growthDay < mark)
            {
                growthDay++;
                growthClock.Advance(TimeSpan.FromDays(1));
                var daily = NewSession(new ManualClock(growthClock.UtcNow), growthDir);
                if (daily.Continue().Status != StartStatus.Loaded) break;
                _ = daily.IngestFromSource(new SyntheticWalkingSource(StepsPerDay), growthClock.UtcNow.AddDays(-1), growthClock.UtcNow);
                var homeState = daily.GetHome();
                if (homeState.ActiveProjectId == null && homeState.Queued.Count == 0)
                    foreach (var pid in catalogOrder)
                        if (daily.EnqueueProject(pid).IsSuccess) break;
            }

            var state = DecodeState(NewCodec(), growthSave);
            regionComplete = state?.Region.IsCompleted ?? false;
            long bytes = new FileInfo(growthSave).Length;
            // These numbers are deterministic for a fixed seed/content: safe to CI-gate.
            Console.WriteLine(
                $"SCENARIO-SIZES day={growthDay} saveBytes={bytes} " +
                $"processedLedger={state?.ProcessedRecords.Count ?? -1} " +
                $"ledgerRecords={state?.Ledger.Records.Count ?? -1} " +
                $"ledgerVitality={state?.Ledger.TotalVitalityCredited ?? -1} " +
                $"materials={state?.Resources.Get(ResourceType.Materials) ?? -1} " +
                $"knowledge={state?.Resources.Get(ResourceType.Knowledge) ?? -1} " +
                $"completedProjects={state?.Region.Projects.Count(p => p.Value.Status == WalkGame.Domain.Projects.ProjectStatus.Completed) ?? -1} " +
                $"regionCompleted={regionComplete}");
        }

        // Scenario 9: the mature/max-content state must still be valid and bootable.
        var finalClock = new ManualClock(growthClock.UtcNow.AddDays(1));
        var matureSw = Stopwatch.StartNew();
        var maxContentSession = NewSession(finalClock, growthDir);
        var maxStart = maxContentSession.Continue();
        matureSw.Stop();
        var maxState = maxContentSession.GetHome();
        var violations = GameStateValidator.Validate(
            DecodeState(NewCodec(), growthSave)!, content);
        Console.WriteLine(
            $"SCENARIO-RESULT id=mature-region-max-content bootMs={matureSw.Elapsed.TotalMilliseconds:F2} " +
            $"status={maxStart.Status} activeProject={maxState.ActiveProjectId ?? "none"} " +
            $"queued={maxState.Queued.Count} requiresAttention={maxState.RequiresAttention} " +
            $"validatorViolations={violations.Count}");

        Console.WriteLine(
            $"SCENARIO-RESULT id=summary measurable=5 surrogate=1 deviceOnly=4 " +
            $"maturityDays={maturityDays} stepsPerDay={StepsPerDay} seed={seed} " +
            $"note=device-only-scenarios(3,4,5,7)-NOT-simulated;timings-not-gated");

        return violations.Count == 0 ? 0 : 2;
    }

    private static GameState? DecodeState(ISaveCodec codec, string savePath)
    {
        if (!File.Exists(savePath)) return null;
        return codec.Decode(File.ReadAllBytes(savePath)).State;
    }
}
