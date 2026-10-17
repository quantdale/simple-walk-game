using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Persistence;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-156: "Today: N Vitality earned" through the real ingestion pipeline and save. Derived from
/// processed-record rows; the schema stays v3 and nothing new is persisted.
/// </summary>
public sealed class DailyEarningsSessionTests : IDisposable
{
    // 2026-03-10 04:00 UTC = 12:00 in Manila (UTC+8) = 23:00 on Mar 9 in UTC-5.
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 4, 0, 0, TimeSpan.Zero);
    private const int Manila = 8 * 3600;
    private const int Eastern = -5 * 3600;
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Fresh_game_has_a_known_zero()
    {
        var t = Start().GetTodayEarnings(Manila);
        Assert.True(t.IsKnown);
        Assert.Equal(0L, t.Vitality);
        Assert.Equal(LocalDayCalendar.DayOf(Now, Manila), t.LocalDay);
    }

    [Fact]
    public void Todays_dated_walks_count_and_yesterdays_do_not()
    {
        var s = Start();
        s.IngestActivityBatch(new[]
        {
            Rec("y", 5000, Now.AddHours(-13), Manila), // 23:00 Mar 9 Manila: yesterday
            Rec("a", 2000, Now.AddHours(-3), Manila), // 09:00 Mar 10 Manila
            Rec("b", 1000, Now.AddHours(-1), Manila), // 11:00
        });
        Assert.Equal(30L, s.GetTodayEarnings(Manila).Vitality);
        Assert.Equal(s.GetDiagnostics().LifetimeVitalityCredited - 50L, s.GetTodayEarnings(Manila).Vitality);
    }

    [Fact]
    public void Day_boundary_follows_the_local_offset()
    {
        var s = Start();
        // 03:30 UTC Mar 10: Mar 10 in Manila, Mar 9 in UTC-5. Same instant, different local days.
        s.IngestActivityBatch(new[] { Rec("m", 2000, Now.AddMinutes(-30), Manila), Rec("e", 4000, Now.AddMinutes(-30).AddSeconds(1), Eastern) });
        Assert.Equal(20L, s.GetTodayEarnings(Manila).Vitality);
        Assert.Equal(40L, s.GetTodayEarnings(Eastern).Vitality);
    }

    [Fact]
    public void Replays_are_exactly_once()
    {
        var s = Start();
        var batch = new[] { Rec("a", 2000, Now.AddHours(-1), Manila) };
        s.IngestActivityBatch(batch);
        s.IngestActivityBatch(batch);
        s.IngestActivityBatch(batch.Concat(batch).ToArray());
        Assert.Equal(20L, s.GetTodayEarnings(Manila).Vitality);
    }

    [Fact]
    public void Correction_down_and_deletion_reduce_today()
    {
        var s = Start();
        s.IngestActivityBatch(new[] { Rec("a", 3000, Now.AddHours(-2), Manila), Rec("b", 2000, Now.AddHours(-1), Manila) });
        Assert.Equal(50L, s.GetTodayEarnings(Manila).Vitality);
        s.IngestActivityBatch(new[] { Rec("a", 1000, Now.AddHours(-2), Manila, revision: 2) });
        Assert.Equal(30L, s.GetTodayEarnings(Manila).Vitality);
        s.IngestActivityBatch(new[] { Rec("b", 0, Now.AddHours(-1), Manila, revision: 2, deletion: true) });
        Assert.Equal(10L, s.GetTodayEarnings(Manila).Vitality);
        Assert.Empty(GameStateValidator.Validate(State(), Region1Catalog.Create()));
    }

    [Fact]
    public void Clamped_deletion_still_removes_the_walk_from_today()
    {
        var s = Start();
        s.IngestActivityBatch(new[] { Rec("a", 3000, Now.AddHours(-2), Manila) });
        // Spend the balance so the deletion's clawback is clamped (completed content is never destroyed).
        foreach (var p in s.GetProjects().Projects.Where(p => p.Status == WalkGame.Domain.Projects.ProjectStatus.Available)) s.EnqueueProject(p.ProjectId);
        s.IngestActivityBatch(new[] { Rec("a", 0, Now.AddHours(-2), Manila, revision: 2, deletion: true) });
        Assert.True(State().ProcessedRecords.UnappliedReversalVitality > 0L, "fixture must clamp the clawback");
        Assert.Equal(0L, s.GetTodayEarnings(Manila).Vitality);
    }

    [Fact]
    public void Undated_walks_never_count()
    {
        var s = Start();
        s.IngestActivityBatch(new[] { Rec("a", 2000, Now.AddHours(-1), null) });
        Assert.Equal(0L, s.GetTodayEarnings(Manila).Vitality);
    }

    [Fact]
    public void Offset_out_of_range_is_unknown()
    {
        var s = Start();
        Assert.False(s.GetTodayEarnings(19 * 3600).IsKnown);
        Assert.False(s.GetTodayEarnings(-19 * 3600).IsKnown);
    }

    [Fact]
    public void Round_trip_reload_reads_the_same_and_writes_no_new_field()
    {
        var s = Start();
        s.IngestActivityBatch(new[] { Rec("a", 2500, Now.AddHours(-1), Manila) });
        var reloaded = TestSessions.Create(_temp, new ManualClock(Now));
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);
        byte[] before = File.ReadAllBytes(SavePath);
        Assert.Equal(25L, reloaded.GetTodayEarnings(Manila).Vitality);
        Assert.Equal(before, File.ReadAllBytes(SavePath)); // reading never writes
        var payload = PayloadSurgery.OpenPayload(before).ToJsonString();
        Assert.DoesNotContain("daily", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, SchemaVersions.Current);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(1)]
    public void Old_saves_load_and_never_invent_credit(int oldSchema)
    {
        var s = Start();
        s.IngestActivityBatch(new[] { Rec("a", 2000, Now.AddHours(-1), Manila) });
        Assert.Equal(20L, s.GetTodayEarnings(Manila).Vitality);
        var payload = PayloadSurgery.OpenPayload(File.ReadAllBytes(SavePath));
        Downgrade(payload, oldSchema);
        File.WriteAllBytes(SavePath, PayloadSurgery.WrapPayload(payload, oldSchema, Now));

        var old = TestSessions.Create(_temp, new ManualClock(Now));
        Assert.Equal(StartStatus.Loaded, old.Continue().Status);
        var t = old.GetTodayEarnings(Manila);
        Assert.True(t.IsKnown);
        Assert.Equal(0L, t.Vitality); // the legacy row has no local day: understated, never invented
        Assert.Equal(20L, old.GetDiagnostics().LifetimeVitalityCredited);

        old.IngestActivityBatch(new[] { Rec("a", 2000, Now.AddHours(-1), Manila), Rec("b", 1500, Now.AddMinutes(-10), Manila) });
        Assert.Equal(15L, old.GetTodayEarnings(Manila).Vitality); // replayed legacy row still not re-credited
    }

    [Fact]
    public void Hostile_v2_save_with_injected_local_days_never_shows_invented_earnings()
    {
        var s = Start();
        s.IngestActivityBatch(new[] { Rec("a", 2000, Now.AddHours(-1), Manila) });
        var payload = PayloadSurgery.OpenPayload(File.ReadAllBytes(SavePath));
        Downgrade(payload, 2);
        int today = LocalDayCalendar.DayOf(Now, Manila)!.Value;
        foreach (var row in ((JsonObject)payload["processedRecords"]!["entries"]!).Select(p => p.Value).OfType<JsonObject>())
            row["localDay"] = today; // a v2 writer never emitted this
        File.WriteAllBytes(SavePath, PayloadSurgery.WrapPayload(payload, 2, Now));

        var old = TestSessions.Create(_temp, new ManualClock(Now));
        var status = old.Continue().Status;
        if (status == StartStatus.Loaded)
            Assert.Equal(0L, old.GetTodayEarnings(Manila).Vitality);
        else
            Assert.NotEqual(StartStatus.NewGameCreated, status); // fails closed; the save is never reset
    }

    [Fact]
    public void Tampered_negative_row_fails_closed()
    {
        var s = Start();
        s.IngestActivityBatch(new[] { Rec("a", 2000, Now.AddHours(-1), Manila) });
        var state = State();
        var key = state.ProcessedRecords.Entries.Keys.Single();
        state.ProcessedRecords.Entries[key] = state.ProcessedRecords.Entries[key] with { VitalityCredited = -500L };
        Assert.NotEmpty(GameStateValidator.Validate(state, Region1Catalog.Create()));
    }

    private static void Downgrade(JsonNode payload, int schema)
    {
        payload["schemaVersion"] = schema;
        var processed = (JsonObject)payload["processedRecords"]!;
        foreach (var name in new[] { "walkedLocalDays", "legacyDaysLowerBound", "legacyLastUtcDay",
                     "isLegacyLowerBound", "hasUndatedCredits", "retiredThroughLocalDay" })
            processed.Remove(name);
        foreach (var row in ((JsonObject)processed["entries"]!).Select(p => p.Value).OfType<JsonObject>())
            row.Remove("localDay");
        if (schema >= 2) return;
        ((JsonObject)payload).Remove("pendingReturnSummary");
        var region = (JsonObject)payload["region"]!;
        foreach (var f in new[] { "discoveries", "expeditions", "ecologyStage", "settlementStage", "isCompleted", "regionCompletedAtUtc" })
            region.Remove(f);
        foreach (var producer in region["producers"]!.AsArray())
        {
            if (producer == null) continue;
            long stored = producer["storedMilliUnits"]?.GetValue<long>() ?? 0L;
            producer["carryMilliUnits"] = Math.Min(Math.Max(stored, 0L), 999L);
            ((JsonObject)producer).Remove("storedMilliUnits");
        }
    }

    private string SavePath => Path.Combine(_temp.Path, "save.json");

    private static NormalizedActivityRecord Rec(string id, long qty, DateTimeOffset end, int? offset, int revision = 1, bool deletion = false) =>
        new("p", id, ActivityCategory.Walking, ActivityUnits.Steps, qty, end.AddMinutes(-20), end, revision, deletion, offset);

    private GameSession Start()
    {
        var s = TestSessions.Create(_temp, new ManualClock(Now));
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(seed: 7UL).Status);
        return s;
    }

    private GameState State() => TestSessions.NewCodec().Decode(File.ReadAllBytes(SavePath)).State!;
}
