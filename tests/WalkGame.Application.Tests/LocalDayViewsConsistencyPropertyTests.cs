using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalkGame.Application.Content;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Time;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-158: the two views derived from a record's local day must agree: days walked (D-103,
/// maintained <c>WalkedLocalDays</c>) and per-day earnings / "Today" (D-156, derived from rows).
/// Seeded random play: new walks with mixed source offsets (none, DST-like, non-hour, date line),
/// corrections that change quantity AND offset (a source re-labelling the walk's zone), deletions,
/// replays, clock advances past the 45-day retirement horizon, and reloads. After every step, on the
/// decoded save:
///   * every retained credited row's local day is in <c>WalkedLocalDays</c>;
///   * every walked day is backed by a retained credited row, or lies at/before the retired horizon;
///   * a day with Vitality earned is a walked day; per-day earnings are never negative;
///   * dated per-day earnings plus undated row Vitality equals the retained credited row Vitality,
///     which never exceeds lifetime credited Vitality;
///   * "Today" equals the derivation for the trusted clock's local day, for several device offsets;
///   * replaying everything already delivered credits 0 and moves no view;
///   * a reload reproduces every view.
/// </summary>
public sealed class LocalDayViewsConsistencyPropertyTests
{
    private static readonly int?[] SourceOffsets = { null, -18000, -14400, 0, 19800, 20700, 28800, 50400, -39600 };
    private static readonly int[] DeviceOffsets = { -39600, -18000, 0, 28800, 50400 };

    public static IEnumerable<object[]> Seeds() => Enumerable.Range(1, 10).Select(i => new object[] { i * 104729 });

    private sealed class Walk { public string Id = ""; public long Qty; public DateTimeOffset End; public int? Offset; public int Revision = 1; public bool Deleted; }

    private static NormalizedActivityRecord ToRecord(Walk w) =>
        new("p", w.Id, ActivityCategory.Walking, ActivityUnits.Steps, w.Deleted ? 0L : w.Qty, w.End.AddMinutes(-20), w.End, w.Revision, w.Deleted, w.Deleted ? null : w.Offset);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Days_walked_and_per_day_earnings_agree_under_random_play(int seed)
    {
        using var temp = new TempDirectory();
        var rng = new Random(seed);
        var clock = new ManualClock(TestSessions.T0);
        var s = TestSessions.Create(temp, clock);
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame((ulong)seed).Status);
        var walks = new List<Walk>();
        int next = 0;
        bool sawRetirement = false, sawMovedDay = false;

        for (int step = 0; step < 400; step++)
        {
            int op = rng.Next(100);
            if (op < 45 || walks.Count == 0)
            {
                var w = new Walk
                {
                    Id = "w" + next++,
                    Qty = rng.Next(4) == 0 ? rng.Next(1, 99) : rng.Next(100, 12000), // some sub-unit walks
                    End = clock.UtcNow.AddMinutes(-rng.Next(0, 36 * 60)),
                    Offset = SourceOffsets[rng.Next(SourceOffsets.Length)],
                };
                walks.Add(w);
                s.IngestActivityBatch(new[] { ToRecord(w) });
            }
            else if (op < 65)
            {
                var w = walks[rng.Next(walks.Count)];
                if (w.Deleted) continue;
                int? before = w.Offset;
                w.Qty = Math.Max(1, w.Qty + rng.Next(-3000, 3000));
                if (rng.Next(2) == 0) w.Offset = SourceOffsets[rng.Next(SourceOffsets.Length)];
                if (before != null && w.Offset != null && LocalDayCalendar.DayOf(w.End, before) != LocalDayCalendar.DayOf(w.End, w.Offset)) sawMovedDay = true;
                w.Revision++;
                s.IngestActivityBatch(new[] { ToRecord(w) });
            }
            else if (op < 75)
            {
                var w = walks[rng.Next(walks.Count)];
                if (w.Deleted) continue;
                w.Deleted = true;
                w.Revision++;
                s.IngestActivityBatch(new[] { ToRecord(w) });
            }
            else if (op < 85)
            {
                clock.Advance(TimeSpan.FromHours(rng.Next(1, 120))); // ~100 days per run: past the 45-day retirement horizon
            }
            else
            {
                AssertReplayIsNoOp(s, walks, clock);
            }

            var state = Decode(temp);
            if (state.ProcessedRecords.RetiredThroughLocalDay != null) sawRetirement = true;
            AssertViewsAgree(s, state, clock, $"seed {seed} step {step}");

            if (step % 50 == 49)
            {
                var before = Views(s, clock);
                s = TestSessions.Create(temp, clock);
                Assert.Equal(StartStatus.Loaded, s.Continue().Status);
                Assert.Equal(before, Views(s, clock));
            }
        }
        AssertReplayIsNoOp(s, walks, clock);
        // The run must actually reach the interesting paths, or the property proves little.
        Assert.True(sawRetirement, "no retirement reached");
        Assert.True(sawMovedDay, "no correction moved a walk to another local day");
    }

    private static void AssertReplayIsNoOp(GameSession s, List<Walk> walks, ManualClock clock)
    {
        var before = Views(s, clock);
        var replay = s.IngestActivityBatch(walks.Select(ToRecord).ToArray());
        Assert.Equal(0L, replay.VitalityCredited);
        Assert.Equal(before, Views(s, clock));
    }

    private static string Views(GameSession s, ManualClock clock)
    {
        var days = s.GetDaysWalked();
        var today = string.Join(",", DeviceOffsets.Select(o => { var t = s.GetTodayEarnings(o); return $"{t.LocalDay}:{t.Vitality}:{t.IsKnown}"; }));
        return $"{days.Days}/{days.IsLowerBound}/{s.GetDiagnostics().LifetimeVitalityCredited}/{today}";
    }

    private static void AssertViewsAgree(GameSession s, GameState state, ManualClock clock, string where)
    {
        var ledger = state.ProcessedRecords;
        var credited = ledger.Entries.Values.Where(e => e.EligibleSteps > 0L).ToList();
        var walked = new HashSet<int>(ledger.WalkedLocalDays);
        Assert.True(ledger.WalkedLocalDays.SequenceEqual(ledger.WalkedLocalDays.Distinct().OrderBy(d => d)), where + ": walked days sorted/distinct");

        foreach (var e in credited)
            if (e.LocalDay is int d && !(ledger.RetiredThroughLocalDay is int r0 && d <= r0))
                Assert.True(walked.Contains(d), $"{where}: credited row {e.IdentityKey} day {d} missing from walked days");
        foreach (int d in walked)
            Assert.True(credited.Any(e => e.LocalDay == d) || (ledger.RetiredThroughLocalDay is int r && d <= r),
                $"{where}: walked day {d} has no retained credited row");

        long dated = 0L;
        foreach (int d in credited.Where(e => e.LocalDay != null).Select(e => e.LocalDay!.Value).Distinct())
        {
            long? v = ledger.VitalityOnLocalDay(d);
            if (v == null) { Assert.True(ledger.RetiredThroughLocalDay is int r && d <= r, where); continue; }
            Assert.True(v.Value >= 0L, where);
            if (v.Value > 0L) Assert.Contains(d, walked);
            dated += v.Value;
        }
        long retainedAfterHorizon = credited.Where(e => e.LocalDay is int d && !(ledger.RetiredThroughLocalDay is int r && d <= r)).Sum(e => Math.Max(0L, e.VitalityCredited));
        Assert.Equal(retainedAfterHorizon, dated);
        long all = credited.Sum(e => Math.Max(0L, e.VitalityCredited));
        Assert.True(all <= s.GetDiagnostics().LifetimeVitalityCredited, $"{where}: rows {all} exceed lifetime");

        foreach (int off in DeviceOffsets)
        {
            var t = s.GetTodayEarnings(off);
            int today = LocalDayCalendar.DayOf(clock.UtcNow, off)!.Value;
            Assert.Equal(today, t.LocalDay);
            long? expected = ledger.VitalityOnLocalDay(today);
            Assert.Equal(expected != null, t.IsKnown);
            Assert.Equal(expected ?? 0L, t.Vitality);
        }
    }

    private static GameState Decode(TempDirectory temp) =>
        TestSessions.NewCodec().Decode(File.ReadAllBytes(Path.Combine(temp.Path, "save.json"))).State!;
}
