using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.Pacing;
using WalkGame.Application.Persistence;
using WalkGame.Application.ReadModels;
using WalkGame.Domain;
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;
using WalkGame.Infrastructure.Persistence;
using WalkGame.Infrastructure.Platform;

internal static class Cli
{
    private const ulong DefaultSeed = 42UL;
    private const int DefaultStepsPerDay = 6000;
    private const string PrimaryFileName = "save.json";
    private const string BackupFileName = "save.backup.json";

    // ---------------------------------------------------------------- verbs

    public static int New(string[] tokens)
    {
        var a = new SimArgs(tokens);
        ulong seed = a.Has("--seed") ? a.ULong("--seed") : DefaultSeed;
        DateTimeOffset at = a.Has("--at") ? a.Iso("--at") : DateTimeOffset.UtcNow;
        IClock clock = a.Has("--at") ? new ManualClock(at) : NewSystemClock();

        var session = CreateSession(clock, a.SaveDir);
        var created = session.StartNewGame(seed);
        if (created.Status != StartStatus.NewGameCreated)
        {
            Console.Error.WriteLine("ERROR: " + (created.Detail ?? $"could not create save ({created.Status})."));
            return 3;
        }

        Console.WriteLine($"created: seed={seed} at={Format(clock.UtcNow)}");
        foreach (var line in created.SummaryLines)
            Console.WriteLine(line);
        Console.WriteLine(Out.HomeLine(session.GetHome()));
        return 0;
    }

    public static int Credit(string[] tokens)
    {
        var a = new SimArgs(tokens);
        long vitality = a.RequireLong("--vitality");
        if (vitality < 0)
            throw new CliUsageException("--vitality cannot be negative");
        Guid id = a.Has("--id") ? a.Guid("--id") : Guid.NewGuid();
        string reason = a.Text("--reason");
        if (reason.Length == 0)
            reason = "manual-credit";
        var clock = new ManualClock(a.Has("--at") ? a.Iso("--at") : DateTimeOffset.UtcNow);

        var session = CreateSession(clock, a.SaveDir);
        var boot = Boot(session, allowFreshStart: false, freshSeed: 0);
        if (boot.ExitCode != 0)
            return boot.ExitCode;

        Console.WriteLine("id: " + id.ToString("D"));
        var credit = session.CreditActivity(id, clock.UtcNow, vitality, reason);
        if (credit.DuplicateIgnored)
            Console.WriteLine("WARN: duplicate transaction ignored; nothing credited.");
        foreach (var line in credit.SummaryLines)
            Console.WriteLine("  " + line);
        return 0;
    }

    public static int Advance(string[] tokens)
    {
        var a = new SimArgs(tokens);
        bool hasTo = a.Has("--to");
        bool hasDays = a.Has("--days");
        if (hasTo == hasDays)
            throw new CliUsageException("provide exactly one of --to ISO | --days N");

        long days = hasDays ? a.Long("--days") : 0;
        if (days < 0)
            throw new CliUsageException("--days cannot be negative");
        var target = hasTo
            ? a.Iso("--to")
            : TruncateToHour(DateTimeOffset.UtcNow).AddDays(days);

        var session = CreateSession(new ManualClock(target), a.SaveDir);
        var boot = Boot(session, allowFreshStart: false, freshSeed: 0);
        if (boot.ExitCode != 0)
            return boot.ExitCode;

        Console.WriteLine("[dev] advanced to " + Format(target));
        foreach (var line in boot.Start.SummaryLines)
            Console.WriteLine(line);
        return 0;
    }

    public static int Simulate(string[] tokens)
    {
        var a = new SimArgs(tokens);
        long days = a.RequireLong("--days");
        if (days is < 0 or > 1_000_000)
            throw new CliUsageException("--days must be between 0 and 1000000");
        int stepsPerDay = a.Has("--steps-per-day") ? a.Int("--steps-per-day") : DefaultStepsPerDay;
        if (stepsPerDay <= 0)
            throw new CliUsageException("--steps-per-day must be positive");
        ulong seed = a.Has("--seed") ? a.ULong("--seed") : DefaultSeed;

        var startInstant = a.Has("--start")
            ? a.Iso("--start")
            : TruncateToHour(DateTimeOffset.UtcNow.AddDays(-days));
        var clock = new ManualClock(startInstant);
        var session = CreateSession(clock, a.SaveDir);

        var boot = Boot(session, allowFreshStart: true, freshSeed: seed);
        if (boot.ExitCode != 0)
            return boot.ExitCode;

        var catalogOrder = Region1Catalog.Create().Projects.Select(p => p.Id.Value).ToList();
        long dailyVitality = Math.Min(100L, stepsPerDay / 100L);
        long totalCredited = 0L;

        Console.WriteLine("[dev] conversion: floor(steps/100) Vitality/day, capped at 100");
        for (long day = 1; day <= days; day++)
        {
            clock.Advance(TimeSpan.FromDays(1));
            Guid txId = new Guid(MD5.HashData(Encoding.UTF8.GetBytes($"{seed}:{day}")));
            var credit = session.CreditActivity(txId, clock.UtcNow, dailyVitality, $"dev-sim day {day}");
            if (!credit.DuplicateIgnored)
                totalCredited += dailyVitality;
            Console.WriteLine($"[dev] day={day} credited=+{dailyVitality}" + (credit.DuplicateIgnored ? " (duplicate ignored)" : string.Empty));

            AutoQueueIfIdle(session, catalogOrder);
        }

        Console.WriteLine("--- home after simulation ---");
        Out.HomeBlock(session.GetHome());
        Console.WriteLine($"days simulated: {days}");
        Console.WriteLine($"total vitality credited: {totalCredited}");
        return 0;
    }

    /// <summary>
    /// M3 acceptance path: normalized synthetic walking records flow through the real
    /// IngestActivityBatch trust pipeline (never direct credit), with the game session
    /// recreated from disk between activity windows so persistence/boot logic is
    /// exercised exactly like an app-closed period.
    /// </summary>
    public static int Walk(string[] tokens)
    {
        var a = new SimArgs(tokens);
        long days = a.RequireLong("--days");
        if (days is < 0 or > 3650)
            throw new CliUsageException("--days must be between 0 and 3650");
        long stepsPerDay = a.Has("--steps-per-day") ? a.Long("--steps-per-day") : 12000L;
        if (stepsPerDay <= 0)
            throw new CliUsageException("--steps-per-day must be positive");

        DateTimeOffset endInstant = a.Has("--at") ? a.Iso("--at") : TruncateToHour(DateTimeOffset.UtcNow);
        DateTimeOffset startInstant = endInstant.AddDays(-days);
        bool replay = a.Has("--replay");
        if (replay && !a.Has("--at"))
            throw new CliUsageException("--replay requires --at so the original window can be repeated");

        long totalCredited = 0L;
        int duplicateRecords = 0;
        var catalogOrder = Region1Catalog.Create().Projects.Select(p => p.Id.Value).ToList();

        for (long day = 1; day <= days; day++)
        {
            DateTimeOffset windowStart = startInstant.AddDays(day - 1);
            DateTimeOffset windowEnd = startInstant.AddDays(day);

            // Fresh session from disk every window: app-closed between activity periods.
            var session = CreateSession(new ManualClock(windowEnd), a.SaveDir);
            var boot = Boot(session, allowFreshStart: !replay, freshSeed: DefaultSeed);
            if (boot.ExitCode != 0)
                return boot.ExitCode;

            var source = new SyntheticWalkingSource(stepsPerDay);
            var ingest = session.IngestFromSource(source, windowStart, windowEnd);
            totalCredited += Math.Max(0L, ingest.VitalityCredited);
            duplicateRecords += ingest.DuplicatesIgnored;

            Console.WriteLine(
                $"[walk] day={day} accepted={ingest.Accepted} rejected={ingest.Rejected}"
                + $" duplicates={ingest.DuplicatesIgnored} vitality=+{ingest.VitalityCredited}");
            foreach (string line in ingest.SummaryLines)
                Console.WriteLine("  " + line);

            AutoQueueIfIdle(session, catalogOrder);
        }

        Console.WriteLine(replay ? "--- replay proof ---" : "--- walk complete ---");
        Console.WriteLine($"windows: {days}  vitality credited: {totalCredited}  duplicate records ignored: {duplicateRecords}");
        if (replay && totalCredited != 0L)
        {
            Console.Error.WriteLine("ERROR: replay credited new vitality — exactly-once violation.");
            return 2;
        }
        return 0;
    }

    /// <summary>
    /// M4 pacing harness (campaign workstream F): runs Region 1 from clean state through
    /// the REAL trust pipeline using a representative activity profile and a deterministic
    /// low-decision player policy, then prints a stable machine-friendly pacing report.
    /// Never sets completion flags directly — everything flows through ingestion,
    /// allocation and canonical completion effects.
    /// </summary>
    public static int Profile(string[] tokens)
    {
        var a = new SimArgs(tokens);
        string profileName = a.Has("--profile") ? a.Text("--profile") : string.Empty;
        PacingProfile pacing = ResolvePacingProfile(profileName);
        int horizon = a.Has("--days") ? (int)a.Long("--days") : 400;
        if (horizon is < 1 or > 3650)
            throw new CliUsageException("--days must be between 1 and 3650");
        ulong seed = a.Has("--seed") ? a.ULong("--seed") : DefaultSeed;

        var content = Region1Catalog.Create();
        var catalogOrder = content.Projects.Select(p => p.Id.Value).ToList();
        var clockBase = a.Has("--at") ? a.Iso("--at") : new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

        var session = CreateSession(new ManualClock(clockBase), a.SaveDir);
        var boot = Boot(session, allowFreshStart: true, freshSeed: seed);
        if (boot.ExitCode != 0)
            return boot.ExitCode;

        var projectCompletionDay = new Dictionary<string, int>();
        var discoveryDay = new Dictionary<string, int>();
        var expeditionAvailableDay = new Dictionary<string, int>();
        var expeditionCompletedDay = new Dictionary<string, int>();
        int decisions = 0, queueEmptyDays = 0, cappedProducerDays = 0;
        int silentDays = 0;
        long totalSteps = 0, eligibleStepsTotal = 0, vitalityCredited = 0;
        int day = 0;
        bool completed = false;
        bool cadence = a.Has("--cadence");
        var tracker = new VisibleChangeTracker();
        var initial = CreateSession(new ManualClock(clockBase), a.SaveDir);
        if (Boot(initial, allowFreshStart: false, freshSeed: 0).ExitCode == 0)
            tracker.Observe(0, initial.GetRegion(), initial.GetDiscoveries());

        while (day < horizon && !completed)
        {
            day++;
            var windowStart = clockBase.AddDays(day - 1);
            var windowEnd = clockBase.AddDays(day);
            // A silent day models a real source returning no record at all (not a
            // zero-quantity record, which the trust pipeline rejects by design).
            long steps = pacing.StepsForDay(day, windowStart);

            var dailySession = CreateSession(new ManualClock(windowEnd), a.SaveDir);
            var dailyBoot = Boot(dailySession, allowFreshStart: false, freshSeed: 0);
            if (dailyBoot.ExitCode != 0)
                return dailyBoot.ExitCode;

            if (steps > 0L)
            {
                var ingest = dailySession.IngestFromSource(new SyntheticWalkingSource(steps), windowStart, windowEnd);
                totalSteps += steps;
                eligibleStepsTotal += ingest.EligibleSteps;
                vitalityCredited += Math.Max(0L, ingest.VitalityCredited);
            }
            else
            {
                silentDays++;
            }

            var home = dailySession.GetHome();
            if (home.ActiveProjectId == null && home.Queued.Count == 0)
            {
                bool queuedAny = false;
                foreach (var projectId in catalogOrder)
                    if (dailySession.EnqueueProject(projectId).IsSuccess) { queuedAny = true; break; }
                if (!queuedAny) queueEmptyDays++; else decisions++;
            }

            var projectsModel = dailySession.GetProjects();
            foreach (var row in projectsModel.Projects)
                if (row.Status == WalkGame.Domain.Projects.ProjectStatus.Completed)
                    TrackFirst(projectCompletionDay, row.ProjectId, day);

            var journal = dailySession.GetDiscoveries();
            foreach (var d in journal.Discoveries)
                if (d.Unlocked) TrackFirst(discoveryDay, d.DiscoveryId, day);

            var expeditions = dailySession.GetExpeditions();
            foreach (var e in expeditions.Expeditions)
            {
                if (e.Status != ExpeditionsReadModel.ExpeditionStatus.Locked)
                    TrackFirst(expeditionAvailableDay, e.ExpeditionId, day);
                if (e.Status == ExpeditionsReadModel.ExpeditionStatus.Completed)
                    TrackFirst(expeditionCompletedDay, e.ExpeditionId, day);
            }

            var region = dailySession.GetRegion();
            foreach (var p in region.Producers)
                if (p.Unlocked && p.StoredMilliUnits >= p.CapacityUnits * ProducerMilliUnitsPerUnit)
                    cappedProducerDays++;

            completed = region.RegionCompleted;
            tracker.Observe(day, region, journal);
        }

        var finalState = DecodeForInspection(a.SaveDir, NewCodec(), out _);
        var violations = finalState == null
            ? new List<string> { "save could not be decoded" }
            : GameStateValidator.Validate(finalState, content);

        string closureId = content.ClosureCriterion?.MilestoneProjectId ?? content.CompletionMilestoneProjectId ?? string.Empty;
        bool criterionMet = finalState != null && content.MeetsClosureCriterion(finalState.Region);
        int chainsComplete = finalState != null ? content.ChainsComplete(finalState.Region) : 0;
        Console.WriteLine($"--- profile report: {profileName} ---");
        if (pacing.PositionalPattern != null)
            Console.WriteLine($"pattern steps/day: {string.Join(",", pacing.PositionalPattern)}");
        else
            Console.WriteLine($"pattern steps/day: {pacing.Description}");

        // Closure is reported against the AUTHORED CRITERION, never inferred from a resource
        // total (D-062).
        Console.WriteLine($"region completed: {(completed ? "yes" : "NO (horizon reached)")}" +
            (projectCompletionDay.TryGetValue(closureId, out int closureDay) ? $" on day {closureDay}" : string.Empty));
        Console.WriteLine($"closure criterion: {(criterionMet ? "met" : "NOT MET")} ({chainsComplete}/{content.Chains.Count} chains)");
        Console.WriteLine($"days simulated: {day}  horizon: {horizon}");

        // Requested and eligible activity are DIFFERENT numbers whenever bounded-quantity
        // handling fires, and the ratio must be derivable from the two figures printed here.
        // The previous single "activity ingested" line carried the pre-clamp total, which is
        // how a documentation section came to state a conversion rate roughly seven times the
        // real one (D-062 / F-13).
        Console.WriteLine($"activity requested: {totalSteps:N0} steps");
        Console.WriteLine($"activity eligible: {eligibleStepsTotal:N0} steps (after bounded-quantity handling)");
        Console.WriteLine($"effective ratio: {RatioText(eligibleStepsTotal, vitalityCredited)} eligible steps/vitality (credited {vitalityCredited:N0} vitality)");
        if (pacing.AllowsSilentDays)
            Console.WriteLine($"silent days (no record returned): {silentDays}");
        Console.WriteLine($"foreground decisions: {decisions} (one queue choice per idle stop; auto-advance on)");
        Console.WriteLine($"queue-empty days: {queueEmptyDays}");
        Console.WriteLine($"producer capped-store days: {cappedProducerDays}");
        Console.WriteLine("chains (vitality): " + string.Join(", ", content.Chains.Select(c =>
            ChainLabel(c.Id) + "=" + c.ProjectIds.Sum(id => content.FindProject(id)?.VitalityCost ?? 0L))));
        Console.WriteLine("per-chain completion day: " + string.Join(", ", content.Chains.Select(c =>
        {
            int last = 0;
            bool all = true;
            foreach (var memberId in c.ProjectIds)
            {
                if (!projectCompletionDay.TryGetValue(memberId, out int memberDay)) { all = false; break; }
                last = Math.Max(last, memberDay);
            }
            return ChainLabel(c.Id) + "=" + (all ? last.ToString() : "incomplete");
        })));
        Console.WriteLine("discovery pacing: " + string.Join(", ", discoveryDay.OrderBy(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + "@d" + kv.Value)));
        Console.WriteLine("expeditions: " + string.Join(", ", content.Expeditions.Select(e =>
        {
            string id = e.Id.Value;
            string avail = expeditionAvailableDay.TryGetValue(id, out int av) ? av.ToString() : "-";
            string done = expeditionCompletedDay.TryGetValue(id, out int dn) ? dn.ToString() : "-";
            return id + "(avail d" + avail + ", done d" + done + ")";
        })));
        if (finalState != null)
            Console.WriteLine($"final arcs: ecology {finalState.Region.EcologyStage}/{content.EcologyProgression.Stages.Count}, settlement {finalState.Region.SettlementStage}/{content.SettlementProgression.Stages.Count}");
        Console.WriteLine("validator-clean: " + (violations.Count == 0 ? "yes" : "NO — " + string.Join("; ", violations)));
        // D-070: opt-in so the committed pacing evidence still reproduces byte-for-byte.
        if (cadence)
            Console.WriteLine("cadence: " + CadenceReport.Analyze(tracker.ChangeDays, day).ToLine());

        // A profile that must close and did not is a BALANCE FAILURE, not a report line. The
        // shortfall sat open for a whole milestone precisely because the harness reported it
        // and then exited 0 (D-062).
        if (PacingContract.IsBalanceDefect(profileName, completed, day, horizon))
        {
            Console.WriteLine(
                $"BALANCE DEFECT: profile '{profileName}' did not close within its documented horizon; "
                + "incomplete chains: "
                + PacingContract.IncompleteChainLabels(content, projectCompletionDay));
            return 2;
        }

        return violations.Count == 0 ? 0 : 2;
    }


    /// <summary>
    /// M8-H1 long-horizon harness (campaign workstream F/H): drives Region 1 through
    /// months of deterministic app-closed days via the REAL trust pipeline under a
    /// named activity shape, then prints a machine-readable growth/performance record
    /// (processed-ledger and reward-ledger sizes, save bytes, wall time, validator
    /// state). Never mutates canonical state directly.
    /// </summary>
    public static int LongHaul(string[] tokens)
    {
        var a = new SimArgs(tokens);
        long days = a.RequireLong("--days");
        if (days is < 1 or > 3650)
            throw new CliUsageException("--days must be between 1 and 3650");

        string shape = a.Has("--shape") ? a.Text("--shape") : "flat";
        if (shape is not ("flat" or "irregular" or "absence"))
            throw new CliUsageException("--shape must be one of: flat | irregular | absence");

        long stepsPerDay = a.Has("--steps-per-day") ? a.Long("--steps-per-day") : 8000L;
        if (stepsPerDay <= 0)
            throw new CliUsageException("--steps-per-day must be positive");

        ulong seed = a.Has("--seed") ? a.ULong("--seed") : DefaultSeed;
        var clockBase = a.Has("--at") ? a.Iso("--at") : new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var content = Region1Catalog.Create();
        var catalogOrder = content.Projects.Select(p => p.Id.Value).ToList();

        long[] irregularPattern = { 26000L, 15000L, 2000L, 18000L, 6000L, 22000L, 9000L };
        const long AbsenceStartDay = 61;
        const long AbsenceEndDay = 240;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        long vitalityCredited = 0;
        int ingestionDays = 0;

        for (long day = 1; day <= days; day++)
        {
            long steps = shape == "irregular" ? irregularPattern[(day - 1) % irregularPattern.Length] : stepsPerDay;
            bool silentDay = shape == "absence" && day >= AbsenceStartDay && day <= AbsenceEndDay;

            var windowStart = clockBase.AddDays(day - 1);
            var windowEnd = clockBase.AddDays(day);

            var session = CreateSession(new ManualClock(windowEnd), a.SaveDir);
            var boot = Boot(session, allowFreshStart: day == 1, freshSeed: seed);
            if (boot.ExitCode != 0)
                return boot.ExitCode;

            if (!silentDay)
            {
                var ingest = session.IngestFromSource(new SyntheticWalkingSource(steps), windowStart, windowEnd);
                vitalityCredited += Math.Max(0L, ingest.VitalityCredited);
                ingestionDays++;
            }

            AutoQueueIfIdle(session, catalogOrder);
        }

        stopwatch.Stop();

        var finalState = DecodeForInspection(a.SaveDir, NewCodec(), out string sourceFile);
        var violations = finalState == null
            ? new List<string> { "save could not be decoded" }
            : GameStateValidator.Validate(finalState, content);

        int completedProjects = 0;
        if (finalState != null)
            foreach (var pair in finalState.Region.Projects)
                if (pair.Value.Status == WalkGame.Domain.Projects.ProjectStatus.Completed)
                    completedProjects++;

        string savePath = Path.Combine(a.SaveDir, sourceFile);
        long saveBytes = File.Exists(savePath) ? new FileInfo(savePath).Length : 0;

        Console.WriteLine("--- longhaul report ---");
        Console.WriteLine($"shape: {shape}  days: {days}  ingestion-days: {ingestionDays}  base-steps/day: {stepsPerDay}");
        Console.WriteLine($"vitality credited (exactly-once pipeline): {vitalityCredited:N0}");
        Console.WriteLine($"completed projects: {completedProjects}/{content.Projects.Count}  region completed: {(finalState?.Region.IsCompleted ?? false)}");
        Console.WriteLine($"validator-clean: {(violations.Count == 0 ? "yes" : "NO - " + string.Join("; ", violations))}");
        Console.WriteLine(
            $"LONGHAUL-RESULT days={days} shape={shape} processedLedger={(finalState?.ProcessedRecords.Count ?? -1)} " +
            $"ledgerRecords={(finalState?.Ledger.Records.Count ?? -1)} ledgerVitality={(finalState?.Ledger.TotalVitalityCredited ?? -1)} " +
            $"unappliedReversal={(finalState?.ProcessedRecords.UnappliedReversalVitality ?? -1)} saveBytes={saveBytes} " +
            $"completed={completedProjects} regionCompleted={(finalState?.Region.IsCompleted ?? false)} " +
            $"violations={violations.Count} wallMs={stopwatch.ElapsedMilliseconds}");

        return violations.Count == 0 ? 0 : 2;
    }

    private const long ProducerMilliUnitsPerUnit = 1000L;

    /// <summary>
    /// A named pacing profile from GAME_SYSTEMS §13 "Simulation testing" / R-013.
    /// A profile resolves a day index to a synthetic walking quantity, or 0 to model a
    /// day on which the source returned no record at all. Resolution depends only on
    /// the day index and the window start instant, so reports stay byte-reproducible.
    /// </summary>
    private sealed class PacingProfile
    {
        private readonly Func<int, DateTimeOffset, long> _resolve;

        private PacingProfile(
            Func<int, DateTimeOffset, long> resolve,
            long[]? positionalPattern,
            string? description,
            bool allowsSilentDays)
        {
            _resolve = resolve;
            PositionalPattern = positionalPattern;
            Description = description;
            AllowsSilentDays = allowsSilentDays;
        }

        /// <summary>Fixed rotation by day position; printed verbatim in the report.</summary>
        public long[]? PositionalPattern { get; }

        /// <summary>Human-readable rule for profiles that are not a fixed rotation.</summary>
        public string? Description { get; }

        public bool AllowsSilentDays { get; }

        public long StepsForDay(int day, DateTimeOffset windowStartUtc) => _resolve(day, windowStartUtc);

        public static PacingProfile Rotating(params long[] pattern) =>
            new(
                (day, _) => pattern[(day - 1) % pattern.Length],
                positionalPattern: pattern,
                description: null,
                allowsSilentDays: false);

        public static PacingProfile Ruled(
            Func<int, DateTimeOffset, long> resolve,
            string description,
            bool allowsSilentDays) =>
            new(
                resolve,
                positionalPattern: null,
                description: description,
                allowsSilentDays: allowsSilentDays);
    }

    /// <summary>
    /// Resolves the seven profiles named by GAME_SYSTEMS §13. The first four are the
    /// M4 profiles and keep their original positional semantics so the committed
    /// evidence under docs/evidence/m4/ still reproduces byte-for-byte.
    /// </summary>
    private static PacingProfile ResolvePacingProfile(string name) => name switch
    {
        "low" => PacingProfile.Rotating(3000L),
        "moderate" => PacingProfile.Rotating(8000L),
        "high" => PacingProfile.Rotating(20000L),
        "irregular" => PacingProfile.Rotating(26000L, 15000L, 2000L, 18000L, 6000L, 22000L, 9000L),

        // Weekend-heavy: concentrated Saturday/Sunday effort, quiet weekdays. Driven by
        // the real calendar weekday rather than array position so it stays correct for
        // any --at start instant.
        "weekend-heavy" => PacingProfile.Ruled(
            (day, start) => start.DayOfWeek switch
            {
                DayOfWeek.Saturday => 22000L,
                DayOfWeek.Sunday => 24000L,
                DayOfWeek.Friday => 6000L,
                DayOfWeek.Thursday => 4500L,
                DayOfWeek.Wednesday => 4000L,
                DayOfWeek.Tuesday => 3500L,
                _ => 3000L,
            },
            description: "calendar weekday (Sat 22000, Sun 24000, Fri 6000, Thu 4500, Wed 4000, Tue 3500, Mon 3000)",
            allowsSilentDays: false),

        // Multi-day inactivity: five active weekdays, silent weekends, then a long
        // 45-day total absence. Proves the anti-quitting-spiral mechanics (indefinite
        // vitality banking, no-waste producer parking, durable return summary) keep a
        // returning player on a viable pacing curve rather than a stalled one.
        "inactivity" => PacingProfile.Ruled(
            (day, _) =>
            {
                if (day >= 100 && day <= 144) return 0L;
                if ((day - 1) % 7 is 5 or 6) return 0L;
                return (day % 2 == 0) ? 9000L : 4000L;
            },
            description: "weekdays 4000/9000 alternating, weekends silent, days 100-144 entirely silent",
            allowsSilentDays: true),

        // Extreme/pathological source values: alternates implausible volumes with
        // near-zero volumes to exercise D-030 quantity clamping and the
        // integer-floor conversion rule. Every day stays positive so the run measures
        // clamping, not record rejection.
        "pathological" => PacingProfile.Rotating(250000L, 100L, 40000L, 50L, 500000L, 10L, 8000L, 1L),

        _ => throw new CliUsageException(
            "--profile must be one of: low | moderate | high | irregular | weekend-heavy | inactivity | pathological"),
    };

    private static void TrackFirst(Dictionary<string, int> target, string key, int day)
    {
        if (!target.ContainsKey(key))
            target[key] = day;
    }

    /// <summary>Chain label for reports: "chain.trail-access" prints as "trail-access".</summary>
    private static string ChainLabel(string chainId) => PacingContract.Label(chainId);

    /// <summary>Effective eligible-steps-per-vitality, or "n/a" when nothing was credited.</summary>
    private static string RatioText(long eligibleSteps, long vitalityCredited)
    {
        if (vitalityCredited <= 0L)
            return "n/a";
        return (eligibleSteps / (double)vitalityCredited).ToString("0.0");
    }

    /// <summary>Acknowledges (dismisses) the pending return summary; idempotent.</summary>
    public static int Ack(string[] tokens)
    {
        var a = new SimArgs(tokens);
        // A path whose PARENT cannot exist is a usage mistake, not "no save found"
        // (D-060). `new`, `scenarios` and `bench` deliberately do not take this path.
        string saveDir = a.RequireReadableSaveDir();
        var session = CreateSession(NewSystemClock(), saveDir);
        var boot = Boot(session, allowFreshStart: false, freshSeed: 0);
        if (boot.ExitCode != 0)
            return boot.ExitCode;

        var pending = session.GetPendingReturnSummary();
        if (pending == null)
        {
            Console.WriteLine("ack: nothing pending (already a no-op).");
            return 0;
        }

        var result = session.AcknowledgeReturnSummary();
        if (!result.IsSuccess)
        {
            Console.Error.WriteLine("ERROR: " + result.Error!.Message);
            return 2;
        }
        Console.WriteLine($"ack: dismissed {pending.Items.Count} item(s).");
        return 0;
    }

    /// <summary>Prints the player glance. Boots the shared way; the persist it
    /// performs is the same one <c>ack</c>, <c>dump</c> and <c>validate</c> already did.</summary>
    public static int Glance(string[] tokens)
    {
        var a = new SimArgs(tokens);
        // A path whose PARENT cannot exist is a usage mistake, not "no save found"
        // (D-060). Same contract as `ack`/`dump`/`validate`.
        string saveDir = a.RequireReadableSaveDir();

        // Optional clock pin. Without it the glance reads at wall-clock time, which is what a
        // real device does. With it, evidence runs are deterministic and a pending summary's
        // currency (D-055) can be reproduced exactly instead of depending on the run date.
        IClock clock = a.Has("--at") ? new ManualClock(a.Iso("--at")) : NewSystemClock();

        var session = CreateSession(clock, saveDir);
        var boot = Boot(session, allowFreshStart: false, freshSeed: 0);
        if (boot.ExitCode != 0)
            return boot.ExitCode;

        Out.GlanceBlock(session.GetGlance());
        return 0;
    }

    public static int Dump(string[] tokens)
    {
        var a = new SimArgs(tokens);
        // A path whose PARENT cannot exist is a usage mistake, not "no save found"
        // (D-060). `new`, `scenarios` and `bench` deliberately do not take this path.
        string saveDir = a.RequireReadableSaveDir();
        var session = CreateSession(NewSystemClock(), saveDir);
        var boot = Boot(session, allowFreshStart: false, freshSeed: 0);
        if (boot.ExitCode != 0)
            return boot.ExitCode;

        var codec = NewCodec();
        GameState? state = DecodeForInspection(saveDir, codec, out string sourceFile);
        if (state == null)
        {
            Console.Error.WriteLine("ERROR: neither primary nor backup save could be decoded.");
            return 2;
        }
        if (sourceFile != PrimaryFileName)
            Console.WriteLine($"WARN: primary save did not decode; showing {sourceFile} contents.");

        // D-059: the authored content version the profile was created against, so a
        // reviewer can see drift without decoding the payload by hand.
        int buildVersion = Region1Catalog.Create().ContentVersion;
        Console.WriteLine("authored content version: "
            + (state.AuthoredContentVersion?.ToString() ?? "unrecorded")
            + " (this build: " + buildVersion + ", "
            + DescribeContentCompatibility(state.AuthoredContentVersion, buildVersion) + ")");

        Out.Dump(state);
        return 0;
    }

    public static int Validate(string[] tokens)
    {
        var a = new SimArgs(tokens);
        // A path whose PARENT cannot exist is a usage mistake, not "no save found"
        // (D-060). `new`, `scenarios` and `bench` deliberately do not take this path.
        string saveDir = a.RequireReadableSaveDir();
        var session = CreateSession(NewSystemClock(), saveDir);
        var boot = Boot(session, allowFreshStart: false, freshSeed: 0);
        if (boot.ExitCode != 0)
            return boot.ExitCode;

        var codec = NewCodec();
        byte[] primaryBytes;
        try
        {
            primaryBytes = File.ReadAllBytes(Path.Combine(saveDir, PrimaryFileName));
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine("ERROR: could not read primary save: " + ex.Message);
            return 3;
        }

        var decoded = codec.Decode(primaryBytes);
        if (decoded.Status != CodecStatus.Ok)
        {
            Console.Error.WriteLine(
                "ERROR: primary save decode failed: " + decoded.Status +
                (decoded.Detail == null ? string.Empty : " - " + decoded.Detail));
            return 2;
        }

        var content = Region1Catalog.Create();
        var violations = GameStateValidator.Validate(decoded.State!, content);
        foreach (var violation in violations)
            Console.WriteLine("VIOLATION: " + violation);

        // D-059: the authored content version is a SEPARATE axis from the payload schema
        // version, and reporting only the schema made a retired-identifier failure look
        // like a schema problem. A release reviewer must see both.
        int? saveContent = decoded.State!.AuthoredContentVersion;
        string contentVerdict = DescribeContentCompatibility(saveContent, content.ContentVersion);

        Console.WriteLine(
            $"validate: schema=v{decoded.SourceSchemaVersion} violations={violations.Count} " +
            $"contentVersion={(saveContent?.ToString() ?? "unrecorded")}/{content.ContentVersion} " +
            $"content={contentVerdict}");
        if (contentVerdict == "NEWER-THAN-BUILD")
            Console.Error.WriteLine(
                "ERROR: save was written against a newer content version; this build cannot load it safely.");
        foreach (var tombstone in content.Tombstones.Values)
            Console.WriteLine($"content-tombstone: {tombstone}");

        bool selftestOk = true;
        if (a.Has("--selftest"))
        {
            selftestOk = IntegritySelfTest(codec, primaryBytes);
            if (selftestOk)
                Console.WriteLine("integrity-selftest PASS");
            else
                Console.Error.WriteLine("ERROR: integrity-selftest FAIL: expected ChecksumMismatch after payload tamper.");
        }

        return violations.Count > 0 || !selftestOk ? 2 : 0;
    }

    /// <summary>Human-readable content-compatibility verdict for the validate/dump output.</summary>
    private static string DescribeContentCompatibility(int? saveVersion, int buildVersion)
    {
        if (saveVersion == null || saveVersion.Value == buildVersion) return "EXACT-MATCH";
        return saveVersion.Value < buildVersion ? "COMPATIBLE-OLDER" : "NEWER-THAN-BUILD";
    }

    // ------------------------------------------------------------- usage

    public static int Usage(string? error)
    {
        if (!string.IsNullOrEmpty(error))
            Console.Error.WriteLine("ERROR: " + error);
        Console.Error.WriteLine(
            """
            WalkGame.SimCli - headless developer CLI for the deterministic game core

            usage: WalkGame.SimCli <verb> [flags]

            verbs:
              new       --save <dir> [--seed N] [--at ISO8601]
                  create a fresh save; clock pinned at --at (default wall clock), seed default 42
              credit    --save <dir> --vitality N [--id GUID] [--reason text] [--at ISO8601]
                  LOW-LEVEL dev diagnostic: apply one raw reward, bypassing ingestion
              advance   --save <dir> (--to ISO8601 | --days N)
                  load the save and advance offline systems to the target instant
              simulate  --save <dir> --days N [--start ISO8601] [--steps-per-day N] [--seed N]
                  LOW-LEVEL dev loop with direct daily credits and automatic queueing
              walk      --save <dir> --days N [--at ISO8601] [--steps-per-day N] [--replay]
                   M3 ACCEPTANCE PATH: normalized synthetic records through the real
                   IngestActivityBatch pipeline; session recreated from disk every window;
                   --replay repeats the same window (--at required) and must credit nothing
               profile   --save <dir> --profile low|moderate|high|irregular|weekend-heavy|inactivity|pathological [--days N] [--seed N] [--at ISO8601] [--cadence]
                   M4 PACING REPORT: deterministic Region 1 playthrough of the real
                   pipeline with a representative activity profile; prints completion
                   day, decisions, queue-empty days, producer caps, discovery/expedition
                   pacing and final validator state
              longhaul  --save <dir> --days N [--shape flat|irregular|absence] [--steps-per-day N] [--seed N] [--at ISO8601]
                   M8-H1 LONG-HORIZON HARNESS: months of app-closed days through the real
                   trust pipeline; prints machine-readable growth/performance record
                   (processed/ledger sizes, save bytes, validator state, wall time)
               bench     --save <dir> [--iterations N] [--days N]
                    PHASE TIMING HARNESS: catalog/validation/encode/decode/ctor/boot+ingest
                    day costs as BENCH-RESULT lines for optimization comparisons
               scenarios --save <dir> [--days N] [--seed N] [--at ISO8601]
                    PERFORMANCE_BUDGETS 19 REGRESSION SCENARIOS (headless subset):
                    cold boot with mature save, return after 7 days with a large batch,
                    canonical landmark stage transition, background/resume surrogate, and
                    mature-region max content. Emits SCENARIO-SIZES lines whose byte/count
                    values are deterministic and CI-gatable; wall-clock is evidence-only.
                    Visit World / Home-idle scenarios are NOT simulated (no editor).
               ack       --save <dir>
                    acknowledge (dismiss) the pending return summary; idempotent
               glance    --save <dir> [--at ISO8601]
                    PLAYER GLANCE (D-065): print the three glance sentences, the activity
                    sentence and the onboarding sentence. Reads at wall-clock time by default;
                    --at pins the clock so a pending summary's currency is reproducible.
                    Never writes the save.
              dump      --save <dir>
                  print aligned internals decoded straight from the save file
              validate  --save <dir> [--selftest]
                  decode + validate state against Region1Catalog; --selftest tampers an in-memory
                  copy of the payload and requires ChecksumMismatch

            exit codes: 0 ok | 1 usage | 2 domain/validation failure | 3 save missing/unreadable | 4 unexpected error
            """);
        return 1;
    }

    // ------------------------------------------------------------ shared

    private readonly record struct BootOutcome(int ExitCode, StartResult Start);

    private static SystemClock NewSystemClock() => new();

    private static ISaveCodec NewCodec() => new SaveCodec(new MigrationRunner(DefaultMigrations.All));

    private static GameSession CreateSession(IClock clock, string saveDir) =>
        new(
            new AtomicFileSaveStore(saveDir),
            NewCodec(),
            clock,
            Region1Catalog.Create());

    /// <summary>Runs the boot flow. ExitCode 0 leaves a usable loaded state behind.</summary>
    private static BootOutcome Boot(GameSession session, bool allowFreshStart, ulong freshSeed)
    {
        var start = session.Continue();
        switch (start.Status)
        {
            case StartStatus.Loaded:
                return new BootOutcome(0, start);
            case StartStatus.RecoveredFromBackup:
                // Name the cause: "the primary file was missing" and "the primary file was
                // damaged" are different support stories that both land here.
                Console.WriteLine(session.GetDiagnostics().BootRecoveryCause
                    == SaveRecoveryCause.PrimaryMissing
                    ? "WARN: latest primary save was missing; recovered from backup."
                    : "WARN: latest primary save was damaged; recovered from backup.");
                return new BootOutcome(0, start);
            default:
                break;
        }

        if (start.Status == StartStatus.NoSaveFound && allowFreshStart)
        {
            var fresh = session.StartNewGame(freshSeed);
            if (fresh.Status == StartStatus.NewGameCreated)
            {
                Console.WriteLine("[dev] started fresh game seed=" + freshSeed.ToString());
                return new BootOutcome(0, fresh);
            }
            Console.Error.WriteLine("ERROR: " + (fresh.Detail ?? "could not create save."));
            return new BootOutcome(3, fresh);
        }

        Console.Error.WriteLine(start.Status switch
        {
            StartStatus.NoSaveFound => "ERROR: no save found (run 'new' first).",
            StartStatus.SaveUnreadable => "ERROR: save unreadable: " + (start.Detail ?? "unknown reason"),
            StartStatus.StateInvalid => "ERROR: save state failed validation: " + (start.Detail ?? "unknown reason"),
            StartStatus.ContentIncompatible => "ERROR: save is incompatible with this build's content: "
                + (start.Detail ?? "content version mismatch"),
            // NewGameCreated is handled above; Loaded and RecoveredFromBackup return early.
            // These arms exist so that adding a StartStatus member COMPILES LOUDLY rather
            // than silently falling into a generic message (D-060). A documented value with
            // no arm is a defect, not a default.
            StartStatus.NewGameCreated => "ERROR: internal: NewGameCreated reached the boot failure path.",
            StartStatus.Loaded => "ERROR: internal: Loaded reached the boot failure path.",
            StartStatus.RecoveredFromBackup => "ERROR: internal: RecoveredFromBackup reached the boot failure path.",
            _ => "ERROR: unclassified boot outcome: " + start.Status + ".",
        });
        // Exit 2 is the documented "domain/validation failure" code. Before D-052
        // StartStatus.StateInvalid was never produced, so a data-integrity failure exited 3
        // ("save missing/unreadable") and the 2 branch was dead code.
                // Exit 2 is the documented "domain/validation failure" code: a state that violates a
        // documented invariant, or content this build cannot represent. Everything else is
        // exit 3, "save missing/unreadable".
        int exitCode = start.Status == StartStatus.StateInvalid
            || start.Status == StartStatus.ContentIncompatible
            ? 2
            : 3;
        return new BootOutcome(exitCode, start);
    }

    private static void AutoQueueIfIdle(GameSession session, IReadOnlyList<string> catalogOrder)
    {
        HomeReadModel home = session.GetHome();
        if (home.ActiveProjectId != null || home.Queued.Count > 0)
            return;

        foreach (var projectId in catalogOrder)
        {
            var attempt = session.EnqueueProject(projectId);
            if (attempt.IsSuccess)
            {
                Console.WriteLine("[auto] queued " + projectId);
                return;
            }
        }
    }

    private static GameState? DecodeForInspection(string saveDir, ISaveCodec codec, out string sourceFile)
    {
        sourceFile = PrimaryFileName;
        GameState? primary = TryDecode(codec, Path.Combine(saveDir, PrimaryFileName));
        if (primary != null)
            return primary;

        sourceFile = BackupFileName;
        return TryDecode(codec, Path.Combine(saveDir, BackupFileName));
    }

    private static GameState? TryDecode(ISaveCodec codec, string path)
    {
        try
        {
            var decoded = codec.Decode(File.ReadAllBytes(path));
            return decoded.Status == CodecStatus.Ok ? decoded.State : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static bool IntegritySelfTest(ISaveCodec codec, byte[] primaryEnvelope)
    {
        // Read the payload through WHICHEVER key is actually present, then write it back
        // through THAT SAME KEY. The previous version read `payloadBase64 ?? PayloadBase64`
        // but always assigned `payloadBase64`, so a PascalCase envelope gained a SECOND
        // key differing only in case. That is a malformed document, and the self-test
        // reported MalformedEnvelope -- a false release-blocker from the tool whose whole
        // job is to prove integrity (D-060).
        JsonNode? root = JsonNode.Parse(primaryEnvelope);
        string? payloadKey = null;
        JsonNode? payloadNode = null;
        if (root != null)
        {
            foreach (string candidate in new[] { "payloadBase64", "PayloadBase64" })
            {
                JsonNode? found = root[candidate];
                if (found != null)
                {
                    payloadKey = candidate;
                    payloadNode = found;
                    break;
                }
            }
        }

        if (root == null || payloadKey == null || payloadNode == null)
        {
            Console.Error.WriteLine("  integrity-selftest: envelope has no recognisable payload property.");
            return false;
        }

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(payloadNode.GetValue<string>());
        }
        catch (FormatException)
        {
            Console.Error.WriteLine("  integrity-selftest: payload property is not valid base64.");
            return false;
        }
        if (payload.Length == 0)
        {
            Console.Error.WriteLine("  integrity-selftest: payload is empty; nothing to tamper with.");
            return false;
        }

        // Minimal, deterministic tamper: flip exactly one byte of the DECODED payload and
        // re-encode it. The stored checksum is deliberately left alone, which is what the
        // integrity check is supposed to notice.
        payload[0] ^= 0xFF;
        root[payloadKey] = JsonValue.Create(Convert.ToBase64String(payload));
        byte[] tamperedEnvelope = Encoding.UTF8.GetBytes(root.ToJsonString());

        var observed = codec.Decode(tamperedEnvelope);

        // Report what was observed AND what was expected. "Tamper not detected" and "tamper
        // detected but classified as something else" are completely different failures, and
        // a bare false does not distinguish them.
        if (observed.Status != CodecStatus.ChecksumMismatch)
        {
            Console.Error.WriteLine(
                "  integrity-selftest: expected " + CodecStatus.ChecksumMismatch
                + " after tampering '" + payloadKey + "', but the codec reported "
                + observed.Status + (observed.Detail == null ? "" : " (" + observed.Detail + ")") + ".");
            return false;
        }

        Console.WriteLine("  integrity-selftest: tampering '" + payloadKey
            + "' produced " + CodecStatus.ChecksumMismatch + " as expected.");
        return true;
    }

    private static DateTimeOffset TruncateToHour(DateTimeOffset instant) =>
        DateTimeOffset.FromUnixTimeSeconds(instant.ToUnixTimeSeconds() - instant.ToUnixTimeSeconds() % 3600);

    internal static string Format(DateTimeOffset instant) => instant.ToString("O");
}
