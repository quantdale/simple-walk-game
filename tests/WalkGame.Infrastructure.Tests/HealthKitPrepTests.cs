using System;
using System.Linq;
using WalkGame.Infrastructure.Platform.HealthKit;

namespace WalkGame.Infrastructure.Tests;

/// <summary>D-113: seen-samples pruning and iPhone/Watch source dedup design (headless; not wired).</summary>
public sealed class HealthKitPrepTests
{
    private const long Day = 86_400_000L;
    private static readonly long Now = new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

    [Fact]
    public void Retention_covers_the_shell_read_window_plus_margin()
    {
        Assert.Equal(30, HealthKitSampleLedger.ReadWindowDays); // must equal ShellController.SyncWindowDays (D-107)
        Assert.True(HealthKitSampleLedger.Retention > TimeSpan.FromDays(HealthKitSampleLedger.ReadWindowDays));
    }

    [Fact]
    public void Prune_drops_only_entries_that_ended_before_the_retention_cutoff()
    {
        var l = new HealthKitSampleLedger();
        l.RecordReturned("old", Now - 40 * Day, Now - 38 * Day);
        l.RecordReturned("edge", Now - 37 * Day, Now - 37 * Day + 1);
        l.RecordReturned("new", Now - Day, Now - Day + 60_000);
        l.RecordReturned("delOld", Now - 50 * Day, Now - 49 * Day); l.MarkDeleted("delOld");
        l.RecordReturned("delNew", Now - 2 * Day, Now - 2 * Day + 1); l.MarkDeleted("delNew");
        Assert.Equal(2, l.Prune(Now));
        Assert.Equal(new[] { "edge", "new" }, l.Intervals.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal(new[] { "delNew" }, l.Deleted.Keys);
    }

    [Fact]
    public void Pruning_never_changes_what_any_shell_read_window_emits()
    {
        var l = new HealthKitSampleLedger();
        for (int i = 0; i < 120; i++) { l.RecordReturned("s" + i, Now - i * Day / 2, Now - i * Day / 2 + 600_000); if (i % 3 == 0) l.MarkDeleted("s" + i); }
        long ws = Now - HealthKitSampleLedger.ReadWindowDays * Day, we = Now + 60_000;
        var before = l.DeletionLines(ws, we).ToList();
        Assert.True(l.Prune(Now) > 0);
        Assert.Equal(before, l.DeletionLines(ws, we).ToList());
        // A clock moved back by up to the margin still sees every deletion it could see before.
        long back = Now - HealthKitSampleLedger.RetentionMarginDays * Day;
        var l2 = new HealthKitSampleLedger();
        for (int i = 0; i < 120; i++) { l2.RecordReturned("s" + i, Now - i * Day / 2, Now - i * Day / 2 + 600_000); if (i % 3 == 0) l2.MarkDeleted("s" + i); }
        var expect = l2.DeletionLines(back - 30 * Day, back).ToList();
        l2.Prune(Now);
        Assert.Equal(expect, l2.DeletionLines(back - 30 * Day, back).ToList());
    }

    [Fact]
    public void Cap_evicts_oldest_first_and_bounds_each_map()
    {
        var l = new HealthKitSampleLedger();
        int n = HealthKitSampleLedger.MaxEntriesPerMap + 5;
        for (int i = 0; i < n; i++) l.RecordReturned("x" + i.ToString("D6"), Now - Day + i, Now - Day + i + 1);
        Assert.Equal(5, l.Prune(Now));
        Assert.Equal(HealthKitSampleLedger.MaxEntriesPerMap, l.Intervals.Count);
        Assert.DoesNotContain("x000000", l.Intervals.Keys);
        Assert.Contains("x000005", l.Intervals.Keys);
    }

    [Fact]
    public void Deletion_of_an_unknown_or_pruned_uuid_is_a_no_op_and_deleted_ids_stay_deleted()
    {
        var l = new HealthKitSampleLedger();
        Assert.False(l.MarkDeleted("never"));
        l.RecordReturned("a", Now - 60 * Day, Now - 59 * Day);
        l.Prune(Now);
        Assert.False(l.MarkDeleted("a"));
        l.RecordReturned("b", Now - Day, Now); Assert.True(l.MarkDeleted("b"));
        l.RecordReturned("b", Now - Day, Now);
        Assert.False(l.Intervals.ContainsKey("b"));
        Assert.Single(l.DeletionLines(Now - 2 * Day, Now + 1));
    }

    [Theory]
    [InlineData("Watch6,1", "com.apple.health.81C1A9B3", HealthKitSourceKind.Watch)]
    [InlineData("iPhone15,2", "com.apple.health.5E3C1D7A", HealthKitSourceKind.Phone)]
    [InlineData("iPhone15,2", "com.example.stepapp", HealthKitSourceKind.Other)]
    [InlineData(null, "com.apple.Health", HealthKitSourceKind.Other)]
    [InlineData(null, null, HealthKitSourceKind.Other)]
    public void Classify_trusts_only_apple_collectors(string? model, string? bundle, HealthKitSourceKind expected) =>
        Assert.Equal(expected, HealthKitSourceDedup.Classify(model, bundle));

    private static HealthKitSourcedSample S(string id, int startMin, int endMin, long count, HealthKitSourceKind k) =>
        new(id, Now + startMin * 60_000L, Now + endMin * 60_000L, count, k);

    [Fact]
    public void Same_walk_from_phone_and_watch_counts_once_with_watch_winning()
    {
        var r = HealthKitSourceDedup.Resolve(new[] { S("p", 0, 10, 1200, HealthKitSourceKind.Phone), S("w", 0, 10, 1000, HealthKitSourceKind.Watch) });
        Assert.Equal(1000, r["w"]); Assert.Equal(0, r["p"]);
    }

    [Fact]
    public void Partial_overlap_prorates_the_lower_priority_sample()
    {
        var r = HealthKitSourceDedup.Resolve(new[] { S("w", 0, 10, 1000, HealthKitSourceKind.Watch), S("p", 5, 15, 999, HealthKitSourceKind.Phone) });
        Assert.Equal(1000, r["w"]); Assert.Equal(499, r["p"]); // floor(999 * 5/10)
    }

    [Fact]
    public void Non_overlapping_and_same_source_samples_are_untouched()
    {
        var r = HealthKitSourceDedup.Resolve(new[] {
            S("w1", 0, 10, 100, HealthKitSourceKind.Watch), S("w2", 5, 15, 100, HealthKitSourceKind.Watch),
            S("p", 20, 30, 300, HealthKitSourceKind.Phone) });
        Assert.Equal(100, r["w1"]); Assert.Equal(100, r["w2"]); Assert.Equal(300, r["p"]);
    }

    [Fact]
    public void Three_levels_and_point_samples()
    {
        var r = HealthKitSourceDedup.Resolve(new[] {
            S("w", 0, 10, 100, HealthKitSourceKind.Watch), S("p", 10, 20, 100, HealthKitSourceKind.Phone),
            S("o", 0, 20, 400, HealthKitSourceKind.Other), S("pt", 5, 5, 7, HealthKitSourceKind.Phone), S("pt2", 25, 25, 7, HealthKitSourceKind.Phone) });
        Assert.Equal(0, r["o"]); Assert.Equal(0, r["pt"]); Assert.Equal(7, r["pt2"]); Assert.Equal(100, r["p"]);
    }

    [Fact]
    public void Result_is_order_independent_never_exceeds_input_and_never_negative()
    {
        var rng = new Random(113);
        for (int round = 0; round < 200; round++)
        {
            var xs = Enumerable.Range(0, 12).Select(i =>
            {
                int s = rng.Next(0, 100); int len = rng.Next(0, 30);
                return S("id" + i, s, s + len, rng.Next(-5, 2000), (HealthKitSourceKind)rng.Next(0, 3));
            }).ToList();
            var a = HealthKitSourceDedup.Resolve(xs);
            var b = HealthKitSourceDedup.Resolve(xs.OrderBy(_ => rng.Next()).ToList());
            Assert.Equal(a.OrderBy(k => k.Key), b.OrderBy(k => k.Key));
            foreach (var x in xs) Assert.InRange(a[x.Id], 0, Math.Max(0, x.Count));
        }
    }
}
