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
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-103: v2 → v3 seeds days walked with a proven lower bound ("at least N"). The mature
/// fixture is a real 80-day profile (one walk a day, so retirement has run) rewritten to the
/// exact v2 shape: no local day on any row and no aggregate fields.
/// </summary>
public sealed class MigrationV2ToV3Tests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
    private const int Days = 80;
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    [Fact]
    public void MatureV2Save_MigratesToALowerBound_NeverAnOvercount()
    {
        byte[] v2 = MatureV2Envelope(out int retained, out long retiredCount);
        Assert.True(retiredCount > 0, "fixture must exercise retirement");

        var result = TestSessions.NewCodec().Decode(v2);
        Assert.Equal(CodecStatus.Ok, result.Status);
        Assert.Equal(2, result.SourceSchemaVersion);
        Assert.Equal(new[] { MigrationV2ToV3.Id }, result.AppliedMigrations);

        var p = result.State!.ProcessedRecords;
        // Retained rows are one per UTC day at the same hour: a 50 h window covers at most 3.
        Assert.Equal((retained + 2) / 3, p.LegacyDaysLowerBound);
        Assert.InRange(p.DaysWalkedCount, 1, Days);
        Assert.True(p.DaysWalkedIsLowerBound);
        Assert.Empty(p.WalkedLocalDays);
        Assert.Empty(GameStateValidator.Validate(result.State!, Region1Catalog.Create()));
    }

    [Fact]
    public void MigratedSave_ReEncodes_Stably_AndLaterWalksAddDaysAfterTheBoundary()
    {
        File.WriteAllBytes(Path.Combine(_temp.Path, "save.json"), MatureV2Envelope(out _, out _));
        var now = T0.AddDays(Days + 3);
        var s = TestSessions.Create(_temp, new ManualClock(now));
        Assert.Equal(StartStatus.Loaded, s.Continue().Status);
        int before = s.GetDaysWalked().Days;
        Assert.True(s.GetDaysWalked().IsLowerBound);

        s.IngestActivityBatch(new[]
        {
            new NormalizedActivityRecord("p", "post-1", ActivityCategory.Walking, ActivityUnits.Steps, 2000,
                now.AddHours(-2), now.AddHours(-1), EndZoneOffsetSeconds: 0),
        });
        Assert.Equal(before + 1, s.GetDaysWalked().Days);

        var codec = TestSessions.NewCodec();
        var again = codec.Decode(File.ReadAllBytes(Path.Combine(_temp.Path, "save.json")));
        Assert.Empty(again.AppliedMigrations);
        Assert.Equal(before + 1, again.State!.ProcessedRecords.DaysWalkedCount);
    }

    [Fact]
    public void EmptyV2Save_MigratesToAnExactZero()
    {
        var s = TestSessions.Create(_temp, new ManualClock(T0));
        s.StartNewGame(seed: 3UL);
        var payload = PayloadSurgery.OpenPayload(File.ReadAllBytes(Path.Combine(_temp.Path, "save.json")));
        StripToV2(payload);
        var result = TestSessions.NewCodec().Decode(PayloadSurgery.WrapPayload(payload, 2, T0));
        Assert.Equal(CodecStatus.Ok, result.Status);
        Assert.Equal(0, result.State!.ProcessedRecords.DaysWalkedCount);
        Assert.False(result.State!.ProcessedRecords.DaysWalkedIsLowerBound);
    }

    private byte[] MatureV2Envelope(out int retained, out long retiredCount)
    {
        var clock = new ManualClock(T0);
        var s = TestSessions.Create(_temp, clock);
        s.StartNewGame(seed: 11UL);
        for (int d = 0; d < Days; d++)
        {
            var end = T0.AddDays(d).AddHours(1);
            clock.Set(end.AddMinutes(5));
            s.IngestActivityBatch(new[]
            {
                new NormalizedActivityRecord("p", "day-" + d, ActivityCategory.Walking, ActivityUnits.Steps, 3000,
                    end.AddMinutes(-30), end),
            });
        }
        byte[] v3 = File.ReadAllBytes(Path.Combine(_temp.Path, "save.json"));
        var state = TestSessions.NewCodec().Decode(v3).State!;
        retained = state.ProcessedRecords.Entries.Values.Count(e => e.EligibleSteps > 0);
        retiredCount = state.ProcessedRecords.RetiredCount;

        var payload = PayloadSurgery.OpenPayload(v3);
        StripToV2(payload);
        return PayloadSurgery.WrapPayload(payload, 2, T0.AddDays(Days));
    }

    private static void StripToV2(JsonNode payload)
    {
        payload["schemaVersion"] = 2;
        var processed = (JsonObject)payload["processedRecords"]!;
        foreach (var name in new[] { "walkedLocalDays", "legacyDaysLowerBound", "legacyLastUtcDay",
                     "isLegacyLowerBound", "hasUndatedCredits", "retiredThroughLocalDay" })
            processed.Remove(name);
        foreach (var row in ((JsonObject)processed["entries"]!).Select(p => p.Value).OfType<JsonObject>())
            row.Remove("localDay");
    }
}
