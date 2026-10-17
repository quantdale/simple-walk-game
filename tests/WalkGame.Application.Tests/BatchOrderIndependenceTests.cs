using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalkGame.Application.Content;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-181: within one ingestion batch the outcome must not depend on the order in which the
/// source listed revisions of the same logical record. Before the fix a deletion listed before
/// the record it deletes was ignored as "unknown", and the record was then credited: a walk the
/// source says never happened earned Vitality and a walked day. The shipped Health Connect and
/// HealthKit adapters already sort deletions last, so this was reachable only through other
/// <c>IActivityRecordSource</c>s or a direct caller; the trust pipeline no longer relies on it.
/// headless-tested.
/// </summary>
public sealed class BatchOrderIndependenceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 3, 10, 8, 0, 0, TimeSpan.Zero);
    private const int Manila = 8 * 3600;

    private static NormalizedActivityRecord Rec(string id, long steps, int revision, int hoursAgo = 1) =>
        new("p", id, ActivityCategory.Walking, ActivityUnits.Steps, steps,
            T0.AddHours(-hoursAgo - 1), T0.AddHours(-hoursAgo), revision, false, Manila);

    private static NormalizedActivityRecord Del(string id, int revision, int hoursAgo = 1) =>
        new("p", id, ActivityCategory.Walking, ActivityUnits.Steps, 0L,
            T0.AddHours(-hoursAgo - 1), T0.AddHours(-hoursAgo), revision, true, Manila);

    private sealed class World : IDisposable
    {
        private readonly TempDirectory _dir = new();
        public GameSession Session { get; }
        public World()
        {
            Session = TestSessions.Create(_dir, new ManualClock(T0));
            Session.StartNewGame(seed: 7UL);
        }
        public GameState State() =>
            TestSessions.NewCodec().Decode(File.ReadAllBytes(Path.Combine(_dir.Path, "save.json"))).State!;
        public void Dispose() => _dir.Dispose();
    }

    [Fact]
    public void A_deletion_listed_before_its_record_in_the_same_batch_still_deletes_it()
    {
        using var w = new World();
        var r = w.Session.IngestActivityBatch(new[] { Del("x", 2), Rec("x", 500, 1) });

        Assert.Equal(0L, r.VitalityCredited);
        Assert.Equal(0L, w.Session.GetDiagnostics().LifetimeVitalityCredited);
        Assert.Equal(0, w.Session.GetDaysWalked().Days);
        Assert.Equal(1, r.DeletionsApplied);
        Assert.Equal(0, r.DeletionsIgnored);
        var row = w.State().ProcessedRecords.Entries.Values.Single();
        Assert.Equal(0L, row.EligibleSteps);
        Assert.Equal(2, row.LastRevision);
        Assert.Empty(GameStateValidator.Validate(w.State(), Region1Catalog.Create()));
    }

    [Fact]
    public void A_correction_listed_before_the_original_is_not_undone_by_it()
    {
        using var w = new World();
        var r = w.Session.IngestActivityBatch(new[] { Rec("x", 800, 2), Rec("x", 300, 1) });
        Assert.Equal(8L, r.VitalityCredited);
        Assert.Equal(800L, w.State().ProcessedRecords.Entries.Values.Single().EligibleSteps);
    }

    [Fact]
    public void Every_order_of_one_records_revisions_gives_the_same_outcome()
    {
        var revisions = new[] { Rec("x", 300, 1), Del("x", 2), Rec("x", 650, 3) };
        (long, long, int, int)? expected = null;
        foreach (var order in Permutations(revisions))
        {
            using var w = new World();
            w.Session.IngestActivityBatch(order);
            var row = w.State().ProcessedRecords.Entries.Values.Single();
            var outcome = (w.Session.GetDiagnostics().LifetimeVitalityCredited, row.EligibleSteps,
                row.LastRevision, w.Session.GetDaysWalked().Days);
            expected ??= outcome;
            Assert.Equal(expected, outcome);
        }
        Assert.Equal((6L, 650L, 3, 1), expected);
    }

    [Fact]
    public void Shuffled_mixed_batches_settle_every_record_on_its_highest_revision()
    {
        var batch = new List<NormalizedActivityRecord>
        {
            Rec("a", 300, 1, 9), Rec("a", 1_200, 2, 9),
            Rec("b", 450, 1, 8), Del("b", 2, 8),
            Rec("c", 999, 1, 7),
            Rec("d", 250, 1, 6), Rec("d", 50, 2, 6), Rec("d", 700, 3, 6),
            Del("e", 4, 5), Rec("e", 5_000, 1, 5), Rec("e", 5_100, 2, 5),
        };
        var expectedSteps = new Dictionary<string, (long Steps, int Revision)>
        {
            ["a"] = (1_200, 2), ["b"] = (0, 2), ["c"] = (999, 1), ["d"] = (700, 3), ["e"] = (0, 4),
        };
        var rng = new Random(181);
        for (int trial = 0; trial < 40; trial++)
        {
            var order = batch.OrderBy(_ => rng.Next()).ToList();
            using var w = new World();
            w.Session.IngestActivityBatch(order);
            var rows = w.State().ProcessedRecords.Entries.Values.ToDictionary(e => e.IdentityKey.Split('|').Last());
            foreach (var (id, want) in expectedSteps)
            {
                Assert.Equal(want.Steps, rows[id].EligibleSteps);
                Assert.Equal(want.Revision, rows[id].LastRevision);
            }
            Assert.Equal(1, w.Session.GetDaysWalked().Days); // a, c and d all end on Mar 10, Manila time
            Assert.Empty(GameStateValidator.Validate(w.State(), Region1Catalog.Create()));
        }
    }

    [Fact]
    public void Replaying_the_same_out_of_order_batch_is_an_exactly_once_no_op()
    {
        using var w = new World();
        var batch = new[] { Del("x", 2), Rec("x", 500, 1), Rec("y", 400, 1) };
        w.Session.IngestActivityBatch(batch);
        long lifetime = w.Session.GetDiagnostics().LifetimeVitalityCredited;
        var again = w.Session.IngestActivityBatch(batch);
        Assert.Equal(0L, again.VitalityCredited);
        Assert.Equal(lifetime, w.Session.GetDiagnostics().LifetimeVitalityCredited);
        Assert.Equal(4L, lifetime);
    }

    private static IEnumerable<NormalizedActivityRecord[]> Permutations(NormalizedActivityRecord[] items)
    {
        if (items.Length <= 1) { yield return items; yield break; }
        for (int i = 0; i < items.Length; i++)
        {
            var rest = items.Where((_, j) => j != i).ToArray();
            foreach (var p in Permutations(rest))
                yield return new[] { items[i] }.Concat(p).ToArray();
        }
    }
}
