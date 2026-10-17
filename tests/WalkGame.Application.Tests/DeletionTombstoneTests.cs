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
/// D-183 (KI-029, amends D-029): a valid deletion for a record never seen is remembered as a
/// zero-credit tombstone row (existing row shape, no save-format change). Before, it was a
/// counted diagnostic only, so a lower revision of that record delivered in a LATER batch was
/// credited (+5 Vitality and a walked day for a deleted walk), and re-delivering the deletion
/// then reversed it (-5): the outcome depended on delivery order and count. headless-tested.
/// </summary>
public sealed class DeletionTombstoneTests
{
    private static readonly DateTimeOffset T0 = new(2026, 3, 10, 8, 0, 0, TimeSpan.Zero);
    private const int Manila = 8 * 3600;

    private static NormalizedActivityRecord Rec(string id, long steps, int revision) =>
        new("p", id, ActivityCategory.Walking, ActivityUnits.Steps, steps,
            T0.AddHours(-2), T0.AddHours(-1), revision, false, Manila);

    private static NormalizedActivityRecord Del(string id, int revision) =>
        new("p", id, ActivityCategory.Walking, ActivityUnits.Steps, 0L,
            T0.AddHours(-2), T0.AddHours(-1), revision, true, Manila);

    private sealed class World : IDisposable
    {
        private readonly TempDirectory _dir = new();
        public GameSession Session { get; private set; }
        public World() { Session = TestSessions.Create(_dir, new ManualClock(T0)); Session.StartNewGame(seed: 7UL); }
        public GameState State() =>
            TestSessions.NewCodec().Decode(File.ReadAllBytes(Path.Combine(_dir.Path, "save.json"))).State!;
        public void Restart() { Session = TestSessions.Create(_dir, new ManualClock(T0)); Assert.Equal(StartStatus.Loaded, Session.Continue().Status); }
        public long Lifetime => Session.GetDiagnostics().LifetimeVitalityCredited;
        public void Dispose() => _dir.Dispose();
    }

    [Fact]
    public void A_deletion_batch_before_the_record_batch_leaves_no_credit_and_no_walked_day()
    {
        using var w = new World();
        var d = w.Session.IngestActivityBatch(new[] { Del("x", 2) });
        Assert.Equal(0L, d.VitalityCredited);
        Assert.Equal(1, d.DeletionsIgnored); // nothing to reverse: still reported as an ignored deletion

        var r = w.Session.IngestActivityBatch(new[] { Rec("x", 500, 1) });
        Assert.Equal(0L, r.VitalityCredited);
        Assert.Equal(1, r.DuplicatesIgnored);
        Assert.Equal(0L, w.Lifetime);
        Assert.Equal(0, w.Session.GetDaysWalked().Days);

        var row = w.State().ProcessedRecords.Entries.Values.Single();
        Assert.Equal((0L, 0L, 2), (row.EligibleSteps, row.VitalityCredited, row.LastRevision));
        Assert.Empty(GameStateValidator.Validate(w.State(), Region1Catalog.Create()));
    }

    [Fact]
    public void Any_order_and_count_of_redeliveries_nets_zero_credit()
    {
        var batches = new[]
        {
            new[] { Del("x", 2) },
            new[] { Rec("x", 500, 1) },
            new[] { Del("x", 2), Rec("x", 500, 1) },
        };
        var rng = new Random(183);
        for (int trial = 0; trial < 30; trial++)
        {
            using var w = new World();
            int deliveries = rng.Next(2, 9);
            var seq = new List<NormalizedActivityRecord[]> { batches[0] }; // the deletion arrives first at least once
            for (int i = 1; i < deliveries; i++) seq.Add(batches[rng.Next(batches.Length)]);
            foreach (var b in seq)
            {
                w.Session.IngestActivityBatch(b);
                if (rng.Next(4) == 0) w.Restart();
                Assert.Equal(0L, w.Lifetime);
            }
            Assert.Equal(0, w.Session.GetDaysWalked().Days);
            Assert.Empty(GameStateValidator.Validate(w.State(), Region1Catalog.Create()));
        }
    }

    [Fact]
    public void A_higher_revision_after_the_tombstone_is_a_genuine_re_add_and_credits_once()
    {
        using var w = new World();
        w.Session.IngestActivityBatch(new[] { Del("x", 2) });
        var readd = w.Session.IngestActivityBatch(new[] { Rec("x", 700, 3) });
        Assert.Equal(7L, readd.VitalityCredited);
        Assert.Equal(1, w.Session.GetDaysWalked().Days);
        var again = w.Session.IngestActivityBatch(new[] { Del("x", 2), Rec("x", 500, 1), Rec("x", 700, 3) });
        Assert.Equal(0L, again.VitalityCredited);
        Assert.Equal(7L, w.Lifetime);
    }

    [Fact]
    public void A_tombstone_never_moves_the_trusted_watermark()
    {
        using var w = new World();
        var before = w.State().IngestionCheckpointUtc;
        w.Session.IngestActivityBatch(new[] { Del("x", 2) });
        Assert.Equal(before, w.State().IngestionCheckpointUtc);
    }

    [Fact]
    public void Deletions_without_a_source_id_or_outside_the_window_leave_no_row()
    {
        using var w = new World();
        var fingerprint = new NormalizedActivityRecord("p", null, ActivityCategory.Walking, ActivityUnits.Steps, 0L,
            T0.AddHours(-2), T0.AddHours(-1), 2, true, Manila);
        var future = new NormalizedActivityRecord("p", "f", ActivityCategory.Walking, ActivityUnits.Steps, 0L,
            T0.AddDays(2), T0.AddDays(3), 2, true, Manila);
        w.Session.IngestActivityBatch(new[] { fingerprint, future });
        Assert.Empty(w.State().ProcessedRecords.Entries);
    }
}
