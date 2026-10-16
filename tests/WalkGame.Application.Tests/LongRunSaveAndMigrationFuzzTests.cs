using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.Persistence;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Validation;
using WalkGame.Domain.Time;
using Xunit;
using Xunit.Abstractions;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-098 Region 1 hardening: (1) a year-plus daily-play profile keeps the save bounded
/// (ledger retirement, D-066, holds size flat once the trusted window is full); (2) a
/// deterministic migration fuzz over a genuine schema-v1 mature save: every mutated
/// payload either loads into a fully valid current-schema state or fails closed without
/// touching the file. Never an exception, never a partially-loaded world.
/// </summary>
public sealed class LongRunSaveAndMigrationFuzzTests : IDisposable
{
    private const ulong Seed = 31UL;
    private const long StepsPerDay = 9000L;
    private static readonly DateTimeOffset T0 = TestSessions.T0;
    private readonly TempDirectory _temp = new();
    private readonly ITestOutputHelper _out;

    public LongRunSaveAndMigrationFuzzTests(ITestOutputHelper output) => _out = output;

    public void Dispose() => _temp.Dispose();

    private string Primary => Path.Combine(_temp.Path, "save.json");
    private string Backup => Path.Combine(_temp.Path, "save.backup.json");

    /// <summary>Plays <paramref name="days"/> app-open days and returns save sizes sampled at each checkpoint day.</summary>
    private Dictionary<int, long> PlayDays(int days, params int[] checkpoints)
    {
        var content = Region1Catalog.Create();
        var boot = TestSessions.Create(_temp.Path, new ManualClock(T0));
        Assert.Equal(StartStatus.NewGameCreated, boot.StartNewGame(Seed).Status);
        var sizes = new Dictionary<int, long>();
        var source = new SyntheticWalkingSource(StepsPerDay);
        for (int day = 1; day <= days; day++)
        {
            var s = TestSessions.Create(_temp.Path, new ManualClock(T0.AddDays(day)));
            Assert.Equal(StartStatus.Loaded, s.Continue().Status);
            Assert.True(s.IngestFromSource(source, T0.AddDays(day - 1), T0.AddDays(day)).Saved, "day " + day);
            if (s.GetPendingReturnSummary() != null) s.AcknowledgeReturnSummary();
            var home = s.GetHome();
            if (home.ActiveProjectId == null && home.Queued.Count == 0)
                foreach (var id in content.Projects.Select(p => p.Id.Value))
                    if (s.EnqueueProject(id).IsSuccess) break;
            if (checkpoints.Contains(day)) sizes[day] = new FileInfo(Primary).Length;
        }
        return sizes;
    }

    [Fact]
    public void FourHundredDaysOfDailyPlay_SaveSizePlateaus_AndStaysValid()
    {
        var sizes = PlayDays(400, 60, 120, 200, 300, 400);
        foreach (var kv in sizes) _out.WriteLine($"day {kv.Key}: {kv.Value} bytes");

        // Absolute budget (PERFORMANCE_BUDGETS: mature save well under 1 MB).
        Assert.True(sizes[400] < 256 * 1024, $"save at day 400 is {sizes[400]} bytes");
        // Retained detail plateaus at the 45-day window (D-066); only the identity-only
        // retired set keeps growing, ~60 B/day (PERFORMANCE_BUDGETS). Gate it at 100 B/day
        // and require late growth to be no faster than early growth.
        double latePerDay = (sizes[400] - sizes[200]) / 200.0;
        double earlyPerDay = (sizes[120] - sizes[60]) / 60.0;
        Assert.True(latePerDay <= 100.0, $"late growth {latePerDay:F1} B/day exceeds 100 B/day");
        Assert.True(latePerDay <= earlyPerDay * 1.25, $"late growth {latePerDay:F1} B/day outpaces early {earlyPerDay:F1} B/day");

        var decoded = TestSessions.NewCodec().Decode(File.ReadAllBytes(Primary));
        Assert.Equal(CodecStatus.Ok, decoded.Status);
        Assert.Empty(GameStateValidator.Validate(decoded.State!, Region1Catalog.Create()));
        Assert.True(decoded.State!.Ledger.TotalVitalityCredited >= 400L * (StepsPerDay / 100L) - 1L,
            "lifetime Vitality must survive retirement of old ledger rows");
    }

    // ------------------------------------------------------------------
    // Migration fuzz.
    // ------------------------------------------------------------------

    private byte[] BuildGenuineV1Mature()
    {
        PlayDays(14);
        var payload = PayloadSurgery.OpenPayload(File.ReadAllBytes(Primary));
        payload["schemaVersion"] = 1;
        ((JsonObject)payload).Remove("pendingReturnSummary");
        var region = (JsonObject)payload["region"]!;
        foreach (var k in new[] { "discoveries", "expeditions", "ecologyStage", "settlementStage", "isCompleted", "regionCompletedAtUtc" })
            region.Remove(k);
        foreach (var producer in region["producers"]!.AsArray())
        {
            if (producer == null) continue;
            long stored = producer["storedMilliUnits"]?.GetValue<long>() ?? 0L;
            producer["carryMilliUnits"] = Math.Min(Math.Max(stored, 0L), 999L);
            ((JsonObject)producer).Remove("storedMilliUnits");
        }
        return PayloadSurgery.WrapPayload(payload, 1, T0.AddDays(14));
    }

    private byte[] BuildCurrentMature()
    {
        PlayDays(14);
        return File.ReadAllBytes(Primary);
    }

    private static List<(JsonNode parent, object key)> Slots(JsonNode root)
    {
        var all = new List<(JsonNode, object)>();
        void Walk(JsonNode n)
        {
            if (n is JsonObject o) foreach (var kv in o.ToList()) { all.Add((o, kv.Key)); if (kv.Value != null) Walk(kv.Value); }
            else if (n is JsonArray a) for (int i = 0; i < a.Count; i++) { all.Add((a, i)); if (a[i] != null) Walk(a[i]!); }
        }
        Walk(root);
        return all;
    }

    private static string Mutate(JsonNode payload, Random rng)
    {
        var slots = Slots(payload);
        var (parent, key) = slots[rng.Next(slots.Count)];
        int op = rng.Next(6);
        JsonNode? replacement = op switch
        {
            1 => null,
            2 => JsonValue.Create(-1L),
            3 => JsonValue.Create(long.MaxValue),
            4 => JsonValue.Create("x"),
            5 => new JsonObject(),
            _ => null,
        };
        if (parent is JsonObject o)
        {
            string k = (string)key;
            if (op == 0) { o.Remove(k); return "remove ." + k; }
            o[k] = replacement; return $"set .{k} op{op}";
        }
        var a = (JsonArray)parent; int i = (int)key;
        if (op == 0) { a.RemoveAt(i); return $"remove [{i}]"; }
        a[i] = replacement; return $"set [{i}] op{op}";
    }

    [Theory]
    [InlineData(true, 20261016)]
    [InlineData(false, 20261017)]
    public void MigrationFuzz_EveryMutatedSave_LoadsValid_OrFailsClosedUntouched(bool genuineV1, int fuzzSeed)
    {
        byte[] v1 = genuineV1 ? BuildGenuineV1Mature() : BuildCurrentMature();
        var rng = new Random(fuzzSeed);
        var outcomes = new Dictionary<StartStatus, int>();
        const int cases = 400;
        for (int c = 0; c < cases; c++)
        {
            var payload = PayloadSurgery.OpenPayload(v1);
            int n = 1 + rng.Next(3);
            var ops = new List<string>();
            for (int j = 0; j < n; j++) ops.Add(Mutate(payload, rng));
            int fallback = genuineV1 ? 1 : SchemaVersions.Current;
            int schema = payload["schemaVersion"] is JsonValue sv && sv.TryGetValue<int>(out var s) ? s : fallback;
            byte[] damaged = PayloadSurgery.WrapPayload(payload, schema, T0.AddDays(14));
            File.WriteAllBytes(Primary, damaged);
            if (File.Exists(Backup)) File.Delete(Backup);

            string ctx = $"case {c}: {string.Join("; ", ops)}";
            StartResult result;
            try { result = TestSessions.Create(_temp.Path, new ManualClock(T0.AddDays(14))).Continue(); }
            catch (Exception e) { throw new Xunit.Sdk.XunitException(ctx + " threw " + e); }
            outcomes[result.Status] = outcomes.TryGetValue(result.Status, out var k) ? k + 1 : 1;

            if (result.Status == StartStatus.Loaded)
            {
                var decoded = TestSessions.NewCodec().Decode(File.ReadAllBytes(Primary));
                Assert.True(decoded.Status == CodecStatus.Ok, ctx + " loaded but persisted save does not decode");
                Assert.Equal(SchemaVersions.Current, decoded.State!.SchemaVersion);
                var violations = GameStateValidator.Validate(decoded.State, Region1Catalog.Create());
                Assert.True(violations.Count == 0, ctx + " loaded an invalid state: " + string.Join(" | ", violations));
            }
            else
            {
                Assert.True(result.Status is StartStatus.StateInvalid or StartStatus.SaveUnreadable or StartStatus.ContentIncompatible,
                    ctx + " unexpected outcome " + result.Status);
                Assert.True(damaged.AsSpan().SequenceEqual(File.ReadAllBytes(Primary)), ctx + " failed but rewrote the save");
            }
        }
        foreach (var kv in outcomes.OrderBy(k => k.Key)) _out.WriteLine($"{kv.Key}: {kv.Value}");
        Assert.True(outcomes.Values.Sum() == cases);
    }
}
