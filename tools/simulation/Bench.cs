using System;
using System.Diagnostics;
using System.IO;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.Persistence;
using WalkGame.Domain;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;
using WalkGame.Infrastructure.Persistence;

internal static class Bench
{
    /// <summary>
    /// Deterministic phase-level performance harness for the critical paths
    /// (content construction/validation, save encode/decode, session construction,
    /// boot, and full app-closed ingest days). Machine-readable BENCH-RESULT lines
    /// make before/after optimization comparisons reproducible. Measurement only:
    /// it never mutates canonical semantics and its save directory is disposable.
    /// </summary>
    public static int Run(string[] tokens)
    {
        var a = new SimArgs(tokens);
        int iterations = a.Has("--iterations") ? a.Int("--iterations") : 200;
        if (iterations is < 1 or > 100_000)
            throw new CliUsageException("--iterations must be between 1 and 100000");
        int days = a.Has("--days") ? a.Int("--days") : 30;
        if (days is < 0 or > 3650)
            throw new CliUsageException("--days must be between 0 and 3650");

        string benchDir = Path.Combine(a.SaveDir, "bench-work");
        if (Directory.Exists(benchDir))
            Directory.Delete(benchDir, recursive: true);
        Directory.CreateDirectory(benchDir);

        // ---- Phase: catalog + content validation (pure duplicate work when uncached) ----
        var sw = Stopwatch.StartNew();
        RegionDefinition? last = null;
        for (int i = 0; i < iterations; i++)
            last = Region1Catalog.Create();
        sw.Stop();
        double catalogMsPerOp = sw.Elapsed.TotalMilliseconds / iterations;
        Console.WriteLine($"BENCH catalog-create x{iterations}: {catalogMsPerOp:F4} ms/op");

        RegionDefinition content = last!;
        sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
            ContentValidator.Validate(content);
        sw.Stop();
        double validateMsPerOp = sw.Elapsed.TotalMilliseconds / iterations;
        Console.WriteLine($"BENCH content-validate x{iterations}: {validateMsPerOp:F4} ms/op");

        ISaveCodec NewCodec() => new SaveCodec(new MigrationRunner(DefaultMigrations.All));
        AtomicFileSaveStore NewStore() => new(benchDir);

        // ---- Prepare one real save grown to representative maturity for codec phases. ----
        var clock = new ManualClock(new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero));
        var prepSession = new GameSession(NewStore(), NewCodec(), clock, content);
        prepSession.StartNewGame(seed: 42UL);
        for (long day = 1; day <= days; day++)
        {
            clock.Advance(TimeSpan.FromDays(1));
            _ = prepSession.IngestFromSource(new SyntheticWalkingSource(8000), clock.UtcNow.AddDays(-1), clock.UtcNow);
        }

        byte[] envelope = File.ReadAllBytes(Path.Combine(benchDir, "save.json"));
        GameState liveState = DecodePrimary(NewCodec(), envelope);

        sw = Stopwatch.StartNew();
        long encodeBytes = 0L;
        for (int i = 0; i < iterations; i++)
            encodeBytes = NewCodec().Encode(liveState, clock.UtcNow).LongLength;
        sw.Stop();
        double encodeMsPerOp = sw.Elapsed.TotalMilliseconds / iterations;
        Console.WriteLine($"BENCH encode x{iterations}: {encodeMsPerOp:F4} ms/op, {encodeBytes} bytes/op");

        sw = Stopwatch.StartNew();
        int decodeOk = 0;
        for (int i = 0; i < iterations; i++)
        {
            if (NewCodec().Decode(envelope).Status == CodecStatus.Ok)
                decodeOk++;
        }
        sw.Stop();
        double decodeMsPerOp = sw.Elapsed.TotalMilliseconds / iterations;
        Console.WriteLine($"BENCH decode x{iterations}: {decodeMsPerOp:F4} ms/op ok={decodeOk}/{iterations}");

        sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
            _ = new GameSession(NewStore(), NewCodec(), clock, content);
        sw.Stop();
        double ctorMsPerOp = sw.Elapsed.TotalMilliseconds / iterations;
        Console.WriteLine($"BENCH session-ctor x{iterations}: {ctorMsPerOp:F4} ms/op");

        double dayMsPerOp = -1.0;
        if (days > 0)
        {
            int dayIterations = Math.Max(5, Math.Min(iterations, 50));
            sw = Stopwatch.StartNew();
            for (int i = 0; i < dayIterations; i++)
            {
                clock.Advance(TimeSpan.FromDays(1));
                var dailySession = new GameSession(NewStore(), NewCodec(), clock, content);
                var start = dailySession.Continue();
                if (start.Status != StartStatus.Loaded && start.Status != StartStatus.NewGameCreated)
                    throw new InvalidOperationException("bench boot failed: " + start.Status);
                _ = dailySession.IngestFromSource(new SyntheticWalkingSource(8000), clock.UtcNow.AddDays(-1), clock.UtcNow);
            }
            sw.Stop();
            dayMsPerOp = sw.Elapsed.TotalMilliseconds / dayIterations;
            Console.WriteLine($"BENCH app-closed-day x{dayIterations}: {dayMsPerOp:F4} ms/day");
        }

        Console.WriteLine(
            $"BENCH-RESULT iterations={iterations} days={days} catalogMsPerOp={catalogMsPerOp:F4} " +
            $"validateMsPerOp={validateMsPerOp:F4} encodeMsPerOp={encodeMsPerOp:F4} " +
            $"decodeMsPerOp={decodeMsPerOp:F4} ctorMsPerOp={ctorMsPerOp:F4} dayMsPerOp={dayMsPerOp:F4}");
        return 0;
    }

    private static GameState DecodePrimary(ISaveCodec codec, byte[] envelope) =>
        codec.Decode(envelope).State ?? throw new InvalidOperationException("bench could not decode prepared save");
}
