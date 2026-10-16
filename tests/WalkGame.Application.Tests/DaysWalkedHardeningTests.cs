using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using WalkGame.Application.Content;
using WalkGame.Application.Persistence;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-105: the days-walked surface (D-103) under tamper and fuzz. A save loads valid or fails
/// closed with its bytes untouched; a count larger than the profile's lifetime is rejected.
/// </summary>
public sealed class DaysWalkedHardeningTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 2, 1, 6, 0, 0, TimeSpan.Zero);
    private const int Days = 20;
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();
    private string Primary => Path.Combine(_temp.Path, "save.json");
    private DateTimeOffset End => T0.AddDays(Days + 1);

    private byte[] DatedV3Save()
    {
        var clock = new ManualClock(T0);
        var s = TestSessions.Create(_temp, clock);
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(seed: 9UL).Status);
        for (int d = 0; d < Days; d++)
        {
            var end = T0.AddDays(d).AddHours(2);
            clock.Set(end.AddMinutes(5));
            s.IngestActivityBatch(new[]
            {
                new NormalizedActivityRecord("p", "w" + d, ActivityCategory.Walking, ActivityUnits.Steps, 4000,
                    end.AddMinutes(-30), end, EndZoneOffsetSeconds: 8 * 3600),
            });
        }
        Assert.Equal(Days, s.GetDaysWalked().Days);
        return File.ReadAllBytes(Primary);
    }

    private static byte[] ToV2(byte[] v3)
    {
        var payload = PayloadSurgery.OpenPayload(v3);
        payload["schemaVersion"] = 2;
        var processed = (JsonObject)payload["processedRecords"]!;
        foreach (var n in new[] { "walkedLocalDays", "legacyDaysLowerBound", "legacyLastUtcDay", "isLegacyLowerBound", "hasUndatedCredits", "retiredThroughLocalDay" })
            processed.Remove(n);
        foreach (var row in ((JsonObject)processed["entries"]!).Select(p => p.Value).OfType<JsonObject>())
            row.Remove("localDay");
        return PayloadSurgery.WrapPayload(payload, 2, T0);
    }

    private StartStatus Load(byte[] bytes, out bool untouched)
    {
        File.WriteAllBytes(Primary, bytes);
        var backup = Path.Combine(_temp.Path, "save.backup.json");
        if (File.Exists(backup)) File.Delete(backup);
        var status = TestSessions.Create(_temp, new ManualClock(End)).Continue().Status;
        untouched = bytes.AsSpan().SequenceEqual(File.ReadAllBytes(Primary));
        return status;
    }

    private static byte[] Edit(byte[] save, Action<JsonObject> edit)
    {
        var payload = PayloadSurgery.OpenPayload(save);
        edit((JsonObject)payload["processedRecords"]!);
        return PayloadSurgery.WrapPayload(payload, payload["schemaVersion"]!.GetValue<int>(), T0);
    }

    [Fact]
    public void InflatedLegacyCount_IsRejected_AndTheSaveIsUntouched()
    {
        var bad = Edit(DatedV3Save(), p => p["legacyDaysLowerBound"] = 5000);
        Assert.Equal(StartStatus.StateInvalid, Load(bad, out bool untouched));
        Assert.True(untouched);
    }

    [Fact]
    public void FarFutureOrAncientDay_IsRejected()
    {
        var save = DatedV3Save();
        foreach (int day in new[] { LocalDayCalendar.UtcDayOf(End) + 400, 1 })
        {
            var bad = Edit(save, p =>
            {
                var arr = p["walkedLocalDays"]!.AsArray();
                var vals = arr.Select(n => n!.GetValue<int>()).Append(day).Distinct().OrderBy(x => x).ToList();
                p["walkedLocalDays"] = new JsonArray(vals.Select(v => (JsonNode)v).ToArray());
            });
            Assert.Equal(StartStatus.StateInvalid, Load(bad, out bool untouched));
            Assert.True(untouched);
        }
    }

    [Fact]
    public void HonestSaves_StayValid_AtTheEdgesOfTheRange()
    {
        // A +14 h offset late in the day lands on UTC date + 1; that must stay valid.
        var clock = new ManualClock(T0);
        var s = TestSessions.Create(_temp, clock);
        s.StartNewGame(seed: 2UL);
        var end = T0.Date.AddHours(23).AddMinutes(30);
        clock.Set(new DateTimeOffset(end, TimeSpan.Zero).AddMinutes(5));
        s.IngestActivityBatch(new[]
        {
            new NormalizedActivityRecord("p", "late", ActivityCategory.Walking, ActivityUnits.Steps, 4000,
                new DateTimeOffset(end, TimeSpan.Zero).AddMinutes(-20), new DateTimeOffset(end, TimeSpan.Zero), EndZoneOffsetSeconds: 14 * 3600),
        });
        var state = TestSessions.NewCodec().Decode(File.ReadAllBytes(Primary)).State!;
        Assert.Empty(GameStateValidator.Validate(state, Region1Catalog.Create()));
        Assert.Equal(1, state.ProcessedRecords.DaysWalkedCount);
    }

    [Theory]
    [InlineData(3, 2026101601)]
    [InlineData(2, 2026101602)]
    public void ProcessedRecordsFuzz_LoadsValid_OrFailsClosedUntouched(int schema, int seed)
    {
        byte[] v3 = DatedV3Save();
        byte[] baseSave = schema == 2 ? ToV2(v3) : v3;
        var rng = new Random(seed);
        var outcomes = new Dictionary<StartStatus, int>();
        for (int c = 0; c < 300; c++)
        {
            var payload = PayloadSurgery.OpenPayload(baseSave);
            var processed = (JsonObject)payload["processedRecords"]!;
            var slots = new List<(JsonNode parent, object key)>();
            void Walk(JsonNode n)
            {
                if (n is JsonObject o) foreach (var kv in o.ToList()) { slots.Add((o, kv.Key)); if (kv.Value != null) Walk(kv.Value); }
                else if (n is JsonArray a) for (int i = 0; i < a.Count; i++) { slots.Add((a, i)); if (a[i] != null) Walk(a[i]!); }
            }
            Walk(processed);
            var ops = new List<string>();
            for (int j = 0, n = 1 + rng.Next(3); j < n && slots.Count > 0; j++)
            {
                var (parent, key) = slots[rng.Next(slots.Count)];
                int op = rng.Next(7);
                JsonNode? v = op switch { 1 => null, 2 => -1, 3 => int.MaxValue, 4 => "x", 5 => new JsonArray(3, 3), 6 => new JsonObject(), _ => null };
                if (parent is JsonObject o) { if (op == 0) o.Remove((string)key); else o[(string)key] = v; }
                else { var a = (JsonArray)parent; int i = (int)key; if (i >= a.Count) continue; if (op == 0) a.RemoveAt(i); else a[i] = v; }
                ops.Add($"{key} op{op}");
            }
            byte[] damaged = PayloadSurgery.WrapPayload(payload, schema, T0);
            string ctx = $"schema {schema} case {c}: {string.Join("; ", ops)}";
            StartStatus status;
            bool untouched;
            try { status = Load(damaged, out untouched); }
            catch (Exception e) { throw new Xunit.Sdk.XunitException(ctx + " threw " + e); }
            outcomes[status] = outcomes.TryGetValue(status, out var k) ? k + 1 : 1;
            if (status == StartStatus.Loaded)
            {
                var st = TestSessions.NewCodec().Decode(File.ReadAllBytes(Primary)).State!;
                var violations = GameStateValidator.Validate(st, Region1Catalog.Create());
                Assert.True(violations.Count == 0, ctx + " loaded invalid: " + string.Join(" | ", violations));
                Assert.InRange(st.ProcessedRecords.DaysWalkedCount, 0, Days + 50);
            }
            else
            {
                Assert.True(status is StartStatus.StateInvalid or StartStatus.SaveUnreadable or StartStatus.ContentIncompatible, ctx + " unexpected " + status);
                Assert.True(untouched, ctx + " failed but rewrote the save");
            }
        }
        Assert.True(outcomes.ContainsKey(StartStatus.Loaded) && outcomes.Count > 1, "fuzz must exercise both outcomes");
    }
}
