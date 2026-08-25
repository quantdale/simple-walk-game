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
