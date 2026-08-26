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
        long[] pattern = profileName switch
        {
            "low" => new[] { 3000L },
            "moderate" => new[] { 8000L },
            "high" => new[] { 20000L },
            "irregular" => new[] { 26000L, 15000L, 2000L, 18000L, 6000L, 22000L, 9000L },
            _ => throw new CliUsageException("--profile must be one of: low | moderate | high | irregular"),
        };
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
        long totalSteps = 0, vitalityCredited = 0;
        int day = 0;
        bool completed = false;

        while (day < horizon && !completed)
        {
            day++;
            long steps = pattern[(day - 1) % pattern.Length];
            var windowStart = clockBase.AddDays(day - 1);
            var windowEnd = clockBase.AddDays(day);

            var dailySession = CreateSession(new ManualClock(windowEnd), a.SaveDir);
            var dailyBoot = Boot(dailySession, allowFreshStart: false, freshSeed: 0);
            if (dailyBoot.ExitCode != 0)
                return dailyBoot.ExitCode;

            var ingest = dailySession.IngestFromSource(new SyntheticWalkingSource(steps), windowStart, windowEnd);
            totalSteps += steps;
            vitalityCredited += Math.Max(0L, ingest.VitalityCredited);

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
        }

        var finalState = DecodeForInspection(a.SaveDir, NewCodec(), out _);
        var violations = finalState == null
            ? new List<string> { "save could not be decoded" }
            : GameStateValidator.Validate(finalState, content);

        string closureId = content.CompletionMilestoneProjectId ?? string.Empty;
        Console.WriteLine($"--- profile report: {profileName} ---");
        Console.WriteLine($"pattern steps/day: {string.Join(",", pattern)}");
        Console.WriteLine($"region completed: {(completed ? "yes" : "NO (horizon reached)")}" +
            (projectCompletionDay.TryGetValue(closureId, out int closureDay) ? $" on day {closureDay}" : string.Empty));
        Console.WriteLine($"days simulated: {day}  horizon: {horizon}");
        Console.WriteLine($"activity ingested: {totalSteps:N0} steps / {vitalityCredited:N0} vitality (exactly-once pipeline)");
        Console.WriteLine($"foreground decisions: {decisions} (one queue choice per idle stop; auto-advance on)");
        Console.WriteLine($"queue-empty days: {queueEmptyDays}");
        Console.WriteLine($"producer capped-store days: {cappedProducerDays}");
        Console.WriteLine("chains (vitality): " + string.Join(", ", ChainVitality(content).Select(kv => kv.Key + "=" + kv.Value)));
        Console.WriteLine("per-chain completion day: " + string.Join(", ", ChainVitality(content).Select(kv =>
        {
            int last = 0;
            bool all = true;
            foreach (var memberId in ChainMembers(kv.Key))
            {
                if (!projectCompletionDay.TryGetValue(memberId, out int memberDay)) { all = false; break; }
                last = Math.Max(last, memberDay);
            }
            return kv.Key + "=" + (all ? last.ToString() : "incomplete");
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

    private static void TrackFirst(Dictionary<string, int> target, string key, int day)
    {
        if (!target.ContainsKey(key))
            target[key] = day;
    }

    private static string[] ChainMembers(string chain) => chain switch
    {
        "trail" => new[] { "proj.clear-trailhead", "proj.rebuild-trail-bridges", "proj.open-lookout" },
        "water" => new[] { "proj.river-intake", "proj.clear-reservoir", "proj.lay-water-lines" },
        "settlement" => new[] { "proj.build-workshop", "proj.restore-market-hall", "proj.wire-settlement-power" },
        "wetland" => new[] { "proj.wetland-drainage", "proj.replant-native-sedges", "proj.build-nesting-islets", "proj.wetland-boardwalk" },
        "forest" => new[] { "proj.clear-fallen-timber", "proj.plant-woodland-understory", "proj.canopy-walkway" },
        "research" => new[] { "proj.refit-observatory-dome", "proj.calibrate-survey-rig", "proj.complete-valley-survey" },
        _ => Array.Empty<string>(),
    };

    private static List<KeyValuePair<string, long>> ChainVitality(WalkGame.Domain.Regions.RegionDefinition content)
    {
        var rows = new List<KeyValuePair<string, long>>();
        foreach (var chain in new[] { "trail", "water", "settlement", "wetland", "forest", "research" })
        {
            long sum = 0L;
            foreach (var memberId in ChainMembers(chain))
                sum += content.FindProject(memberId)?.VitalityCost ?? 0L;
            rows.Add(new KeyValuePair<string, long>(chain, sum));
        }
        return rows;
    }

    /// <summary>Acknowledges (dismisses) the pending return summary; idempotent.</summary>
    public static int Ack(string[] tokens)
    {
        var a = new SimArgs(tokens);
        var session = CreateSession(NewSystemClock(), a.SaveDir);
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

    public static int Dump(string[] tokens)
    {
        var a = new SimArgs(tokens);
        var session = CreateSession(NewSystemClock(), a.SaveDir);
        var boot = Boot(session, allowFreshStart: false, freshSeed: 0);
        if (boot.ExitCode != 0)
            return boot.ExitCode;

        var codec = NewCodec();
        GameState? state = DecodeForInspection(a.SaveDir, codec, out string sourceFile);
        if (state == null)
        {
            Console.Error.WriteLine("ERROR: neither primary nor backup save could be decoded.");
            return 2;
        }
        if (sourceFile != PrimaryFileName)
            Console.WriteLine($"WARN: primary save did not decode; showing {sourceFile} contents.");

        Out.Dump(state);
        return 0;
    }

    public static int Validate(string[] tokens)
    {
        var a = new SimArgs(tokens);
        var session = CreateSession(NewSystemClock(), a.SaveDir);
        var boot = Boot(session, allowFreshStart: false, freshSeed: 0);
        if (boot.ExitCode != 0)
            return boot.ExitCode;

        var codec = NewCodec();
        byte[] primaryBytes;
        try
        {
            primaryBytes = File.ReadAllBytes(Path.Combine(a.SaveDir, PrimaryFileName));
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

        var violations = GameStateValidator.Validate(decoded.State!, Region1Catalog.Create());
        foreach (var violation in violations)
            Console.WriteLine("VIOLATION: " + violation);
        Console.WriteLine($"validate: schema=v{decoded.SourceSchemaVersion} violations={violations.Count}");

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
              profile   --save <dir> --profile low|moderate|high|irregular [--days N] [--seed N] [--at ISO8601]
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
              ack       --save <dir>
                  acknowledge (dismiss) the pending return summary; idempotent
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
                Console.WriteLine("WARN: latest primary save was damaged; recovered from backup.");
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
            _ => "ERROR: " + (start.Detail ?? "save state invalid."),
        });
        return new BootOutcome(start.Status == StartStatus.StateInvalid ? 2 : 3, start);
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
        JsonNode? root = JsonNode.Parse(primaryEnvelope);
        JsonNode? payloadNode = root?["payloadBase64"] ?? root?["PayloadBase64"];
        if (payloadNode == null)
            return false;

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(payloadNode.GetValue<string>());
        }
        catch (FormatException)
        {
            return false;
        }
        if (payload.Length == 0)
            return false;

        payload[0] ^= 0xFF;
        root!["payloadBase64"] = JsonValue.Create(Convert.ToBase64String(payload));
        byte[] tamperedEnvelope = Encoding.UTF8.GetBytes(root.ToJsonString());

        return codec.Decode(tamperedEnvelope).Status == CodecStatus.ChecksumMismatch;
    }

    private static DateTimeOffset TruncateToHour(DateTimeOffset instant) =>
        DateTimeOffset.FromUnixTimeSeconds(instant.ToUnixTimeSeconds() - instant.ToUnixTimeSeconds() % 3600);

    internal static string Format(DateTimeOffset instant) => instant.ToString("O");
}
