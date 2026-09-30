using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalkGame.Application.Persistence;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;

namespace WalkGame.Application.Tests;

/// <summary>
/// F-02 / F-03 / F-09 regression coverage: the activity acceptance window is anchored to
/// TRUSTED TIME (durable trusted progress), not to the raw device wall clock.
///
/// Before D-053 the window was "now minus 14 days .. now plus 10 minutes". Two failures
/// followed directly, and both are reproduced here as the cases that must now pass:
///
///   * a player returning after a long holiday had their ENTIRE provider backlog
///     rejected as OutsideHorizon, so the activity that earned the most goodwill
///     silently disappeared (the product is explicitly offline-first);
///   * a device clock merely MINUTES fast made every record look future-dated, so
///     crediting stopped entirely with no player-visible explanation.
///
/// Deletion markers previously bypassed validation altogether and could push the durable
/// watermark anywhere; they are now judged by the same eligibility rules first.
/// </summary>
public sealed class TrustedTimeWindowTests : IDisposable
{
    private const string Provider = "trust.provider";
    private static readonly DateTimeOffset T0 = TestSessions.T0;

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    // ==================================================================
    // 4.3 The headline defect: a 30-day absence with the whole backlog
    //     handed back in ONE batch on return.
    // ==================================================================

    [Fact]
    public void ThirtyDayAbsence_WholeBacklogDeliveredOnReturn_IsCreditedExactlyOnce()
    {
        Bootstrap();
        // Return a day after the last backlog day: a record for day 30 ends at
        // T0 + 30d + 23h, so booting at T0 + 30d would (correctly) call it future-dated.
        var returnDay = T0.AddDays(31);
        var returning = Session(returnDay);
        Assert.Equal(StartStatus.Loaded, returning.Continue().Status);

        // What the provider actually hands back: every day of the absence, in one batch.
        var backlog = Backlog(days: 30, firstDay: 1, stepsPerDay: 10_000L);
        Assert.Equal(30, backlog.Count);

        var result = returning.IngestActivityBatch(backlog);

        Assert.Equal(30, result.TotalReceived);
        Assert.Equal(30, result.Accepted);
        Assert.Equal(0, result.Rejected);
        Assert.Empty(result.RejectionCounts);
        Assert.Equal(30 * 100L, result.VitalityCredited);

        // Watermark advanced to the latest end in the batch.
        var afterFirst = Decode();
        Assert.Equal(T0.AddDays(30).AddHours(23), afterFirst.IngestionCheckpointUtc);
        Assert.Equal(30, afterFirst.ProcessedRecords.Count);

        // A full replay of the same batch credits nothing further and does not regress
        // the watermark: this is the exactly-once guarantee surviving a backlog replay.
        var replay = Session(returnDay.AddHours(1));
        Assert.Equal(StartStatus.Loaded, replay.Continue().Status);
        var replayed = replay.IngestActivityBatch(backlog);

        Assert.Equal(0, replayed.Accepted);
        Assert.Equal(30, replayed.DuplicatesIgnored);
        Assert.Equal(0L, replayed.VitalityCredited);
        Assert.Equal(T0.AddDays(30).AddHours(23), Decode().IngestionCheckpointUtc);
    }

    // ==================================================================
    // 4.4 45 days: past the OLD 14-day bound and past the 30-day acceptance
    //     case, with a stale temp file and a restarted session in the mix.
    // ==================================================================

    [Fact]
    public void FortyFiveDayAbsence_AcrossAStoreCrashWindow_IsCredited()
    {
        Bootstrap();
        // Booted a day after the last backlog day, so every backlog record is genuinely in
        // the past relative to the injected clock and the test isolates the WINDOW.
        var returnDay = T0.AddDays(46);
        var returning = Session(returnDay);
        Assert.Equal(StartStatus.Loaded, returning.Continue().Status);

        // A leftover temp file from a crashed commit must not confuse recovery.
        File.WriteAllText(_temp.FilePath("save.tmp"), "half-written garbage");

        var backlog = Backlog(days: 45, firstDay: 1, stepsPerDay: 8_000L);
        var result = returning.IngestActivityBatch(backlog);

        Assert.Equal(45, result.Accepted);
        Assert.Equal(0, result.Rejected);
        Assert.Equal(45 * 80L, result.VitalityCredited);
        // The last backlog day ends at 23:00 on day 45, so the watermark lands there.
        Assert.Equal(T0.AddDays(45).AddHours(23), Decode().IngestionCheckpointUtc);

        // Still valid canonical state, and the temp file was never promoted as data.
        Assert.Empty(GameStateValidator.Validate(Decode(), Session(returnDay).Content));
    }

    // ==================================================================
    // 4.5 F-03: a device clock two hours FAST must not disable crediting.
    // ==================================================================

    [Fact]
    public void DeviceClockTwoHoursFast_StillCredits_AndDoesNotReportFutureTimestamp()
    {
        Bootstrap();

        // The device thinks it is 08:00; the real walking actually ended at 06:00.
        var deviceNow = T0.AddHours(2);
        var session = Session(deviceNow);
        Assert.Equal(StartStatus.Loaded, session.Continue().Status);

        var record = Row("clock-fast", 12_000L, endUtc: T0.AddHours(-2));
        var result = session.IngestActivityBatch(new[] { record });

        Assert.Equal(1, result.Accepted);
        Assert.Equal(0, result.Rejected);
        Assert.DoesNotContain(
            nameof(ActivityValidationStatus.FutureTimestamp), result.RejectionCounts.Keys);
        Assert.Equal(120L, result.VitalityCredited);
    }

    [Fact]
    public void DeviceClockDaysFast_StillCredits_BecauseTheAnchorTracksTrustedProgress()
    {
        // The severe form of F-03: minutes are routine, but a phone that has been off for
        // a week can resynchronise badly. Records still behind already-trusted progress
        // must never be refused for being in the "future" relative to a wrong clock.
        Bootstrap();
        var day3 = Session(T0.AddDays(3));
        day3.Continue();
        day3.IngestActivityBatch(new[] { Row("anchor", 10_000L, endUtc: T0.AddDays(3)) });

        // Now the clock jumps forward by 3 days, and the provider replays real history
        // that is behind the trusted anchor.
        var jumped = Session(T0.AddDays(6));
        Assert.Equal(StartStatus.Loaded, jumped.Continue().Status);
        var result = jumped.IngestActivityBatch(new[]
        {
            Row("real-day-4", 10_000L, endUtc: T0.AddDays(4)),
            Row("real-day-5", 10_000L, endUtc: T0.AddDays(5)),
        });

        Assert.Equal(2, result.Accepted);
        Assert.Equal(0, result.Rejected);
        Assert.Equal(200L, result.VitalityCredited);
    }

    // ==================================================================
    // 4.6 A genuinely future-dated record is STILL rejected, and the
    //     watermark does not move.
    // ==================================================================

    [Fact]
    public void GenuinelyFutureRecord_IsRejected_AndDoesNotMoveTheWatermark()
    {
        Bootstrap();
        var now = T0.AddDays(2);
        var session = Session(now);
        session.Continue();
        session.IngestActivityBatch(new[] { Row("trusted", 5_000L, endUtc: T0.AddDays(1)) });

        var watermarkBefore = Decode().IngestionCheckpointUtc;

        // Ahead of every trusted reference by far more than the skew allowance.
        var result = session.IngestActivityBatch(new[]
        {
            Row("pre-minted", 9_000L, endUtc: T0.AddDays(40)),
        });

        Assert.Equal(1, result.Rejected);
        Assert.Equal(1, result.RejectionCounts[nameof(ActivityValidationStatus.FutureTimestamp)]);
        Assert.Equal(0, result.Accepted);
        Assert.Equal(0L, result.VitalityCredited);
        Assert.Equal(watermarkBefore, Decode().IngestionCheckpointUtc);
    }

    // ==================================================================
    // 4.7 A forward clock jump during absence never mints Vitality the
    //     source did not report.
    // ==================================================================

    [Fact]
    public void ForwardClockJumpDuringAbsence_CreditsOnlyWhatTheSourceReported()
    {
        Bootstrap();
        var beforeJump = Session(T0.AddDays(2));
        beforeJump.Continue();
        beforeJump.IngestActivityBatch(new[] { Row("pre-jump", 10_000L, endUtc: T0.AddDays(2)) });
        long creditedBefore = Decode().Ledger.TotalVitalityCredited;

        // Clock jumps a month forward while the app is closed.
        var afterJump = Session(T0.AddDays(32));
        Assert.Equal(StartStatus.Loaded, afterJump.Continue().Status);

        // The source hands back only two real days.
        var result = afterJump.IngestActivityBatch(new[]
        {
            Row("post-jump-a", 7_000L, endUtc: T0.AddDays(3)),
            Row("post-jump-b", 7_000L, endUtc: T0.AddDays(4)),
        });

        Assert.Equal(2, result.Accepted);
        Assert.Equal(140L, result.VitalityCredited); // exactly 70 + 70, nothing invented
        Assert.Equal(creditedBefore + 140L, Decode().Ledger.TotalVitalityCredited);
    }

    // ==================================================================
    // 4.9 A hostile far-old dump is rejected, while the in-window records
    //     in the SAME batch are still processed normally.
    // ==================================================================

    [Fact]
    public void HostileOldDumpMixedWithInWindowRecords_RejectsOnlyTheOldOnes()
    {
        Bootstrap();
        var now = T0.AddDays(5);
        var session = Session(now);
        session.Continue();
        session.IngestActivityBatch(new[] { Row("trust-anchor", 5_000L, endUtc: T0.AddDays(1)) });

        var mixed = new List<NormalizedActivityRecord>
        {
            Row("hostile-1", 90_000L, endUtc: T0.AddDays(-400)),
            Row("hostile-2", 90_000L, endUtc: T0.AddDays(-800)),
            Row("legit-1", 10_000L, endUtc: T0.AddDays(4)),
            Row("legit-2", 10_000L, endUtc: T0.AddDays(5)),
        };

        var result = session.IngestActivityBatch(mixed);

        Assert.Equal(4, result.TotalReceived);
        Assert.Equal(2, result.Rejected);
        Assert.Equal(2, result.RejectionCounts[nameof(ActivityValidationStatus.OutsideHorizon)]);
        Assert.Equal(2, result.Accepted);
        Assert.Equal(200L, result.VitalityCredited); // the 180,000 hostile steps minted nothing
    }

    // ==================================================================
    // 4.10 F-09: deletion markers obey the same eligibility rules. Each of
    //      these must be rejected (not silently honoured) and must move
    //      nothing.
    // ==================================================================

    [Fact]
    public void MalformedDeletionMarkers_AreRejected_AndChangeNothing()
    {
        Bootstrap();
        var now = T0.AddDays(2);
        var session = Session(now);
        session.Continue();
        session.IngestActivityBatch(new[] { Row("to-delete", 20_000L, endUtc: T0.AddDays(1)) });

        long vitalityBefore = session.GetHome().Vitality;
        var watermarkBefore = Decode().IngestionCheckpointUtc;
        long reversalBefore = Decode().ProcessedRecords.UnappliedReversalVitality;

        // Far-future deletion: previously this bypassed validation entirely and could
        // push the durable watermark forward by an arbitrary amount.
        var future = Deletion("del-future", 2, endUtc: T0.AddDays(60));
        // Malformed temporal extent (end before start).
        var malformed = Deletion("del-malformed", 2, startUtc: T0.AddDays(1), endUtc: T0.AddDays(0));
        // Far outside the trusted window.
        var outside = Deletion("del-outside", 2, endUtc: T0.AddDays(-400));

        var result = session.IngestActivityBatch(new[] { future, malformed, outside });

        Assert.Equal(3, result.Rejected);
        Assert.Equal(1, result.RejectionCounts[nameof(ActivityValidationStatus.FutureTimestamp)]);
        Assert.Equal(1, result.RejectionCounts[nameof(ActivityValidationStatus.MalformedTimestamps)]);
        Assert.Equal(1, result.RejectionCounts[nameof(ActivityValidationStatus.OutsideHorizon)]);
        Assert.Equal(0, result.DeletionsApplied);
        Assert.Equal(0L, result.VitalityCredited);

        // Nothing durable moved: the 200 Vitality is still there, the watermark is
        // unchanged, and no reversal was recorded.
        Assert.Equal(vitalityBefore, session.GetHome().Vitality);
        Assert.Equal(watermarkBefore, Decode().IngestionCheckpointUtc);
        Assert.Equal(reversalBefore, Decode().ProcessedRecords.UnappliedReversalVitality);
    }

    [Fact]
    public void WellFormedDeletionMarker_StillReversesConservatively_AndAdvancesTheWatermark()
    {
        // The other half: making deletion markers validate must not disable the correction
        // policy, which is the entire reason the marker exists.
        Bootstrap();
        var now = T0.AddDays(2);
        var session = Session(now);
        session.Continue();
        session.IngestActivityBatch(new[] { Row("to-delete", 20_000L, endUtc: T0.AddDays(1)) });
        Assert.Equal(200L, session.GetHome().Vitality);

        var watermarkBefore = Decode().IngestionCheckpointUtc;
        var result = session.IngestActivityBatch(new[]
        {
            Deletion("to-delete", 2, endUtc: T0.AddDays(2)),
        });

        Assert.Equal(1, result.DeletionsApplied);
        Assert.Equal(0, result.Rejected);
        Assert.Equal(-200L, result.VitalityCredited);
        Assert.Equal(0L, session.GetHome().Vitality);
        Assert.Equal(T0.AddDays(2), Decode().IngestionCheckpointUtc);
        Assert.True(Decode().IngestionCheckpointUtc >= watermarkBefore);

        // A repeat is a no-op, not a second clawback.
        var repeat = session.IngestActivityBatch(new[] { Deletion("to-delete", 2, endUtc: T0.AddDays(2)) });
        Assert.Equal(0, repeat.DeletionsApplied);
        Assert.Equal(1, repeat.StaleRevisionsIgnored);
        Assert.Equal(0L, repeat.VitalityCredited);
    }

    // ==================================================================
    // 4.11 An entirely-rejected batch: one entry per distinct reason, zero
    //      credited, and the accounting identity holds.
    // ==================================================================

    [Fact]
    public void EntirelyRejectedBatch_ReportsOneEntryPerDistinctReason_AndTheIdentityHolds()
    {
        Bootstrap();
        var session = Session(T0.AddDays(2));
        session.Continue();

        var allBad = new List<NormalizedActivityRecord>
        {
            Row("bad-unit", 1_000L, endUtc: T0.AddDays(1)) with { Unit = "kilometers" },
            Row("bad-zero", 0L, endUtc: T0.AddDays(1)),
            Row("bad-negative", -5L, endUtc: T0.AddDays(1)),
            Row("bad-window", 1_000L, startUtc: T0.AddDays(1), endUtc: T0),
            Row("bad-future", 1_000L, endUtc: T0.AddDays(30)),
            Row("bad-old", 1_000L, endUtc: T0.AddDays(-400)),
            Row("bad-provider", 1_000L, endUtc: T0.AddDays(1)) with { ProviderNamespace = "" },
        };

        var result = session.IngestActivityBatch(allBad);

        Assert.Equal(0, result.Accepted);
        Assert.Equal(0L, result.VitalityCredited);
        Assert.Equal(allBad.Count, result.Rejected);
        Assert.Equal(7, result.RejectionCounts.Count);
        Assert.Contains(nameof(ActivityValidationStatus.EmptyProvider), result.RejectionCounts.Keys);
        Assert.Contains(nameof(ActivityValidationStatus.UnsupportedUnit), result.RejectionCounts.Keys);
        Assert.Contains(nameof(ActivityValidationStatus.ZeroQuantity), result.RejectionCounts.Keys);
        Assert.Contains(nameof(ActivityValidationStatus.NegativeQuantity), result.RejectionCounts.Keys);
        Assert.Contains(nameof(ActivityValidationStatus.MalformedTimestamps), result.RejectionCounts.Keys);
        Assert.Contains(nameof(ActivityValidationStatus.FutureTimestamp), result.RejectionCounts.Keys);
        Assert.Contains(nameof(ActivityValidationStatus.OutsideHorizon), result.RejectionCounts.Keys);

        // The per-reason breakdown survives the call that produced it, and it sums right.
        Assert.Equal(result.Rejected, result.RejectionCounts.Values.Sum());
        AssertAccountingIdentity(result);
    }

    // ==================================================================
    // 4.12 Pure-domain boundary coverage at exactly the floor and exactly
    //      the skew allowance (no session, no filesystem).
    // ==================================================================

    [Fact]
    public void TrustedTimeWindow_EdgesAreInclusiveAtExactlyTheFloorAndSkew()
    {
        var now = T0.AddDays(100);
        var floor = ActivityValidationPolicy.TrustedWindowFloor;
        var skew = ActivityValidationPolicy.MaxFutureSkew;
        var trusted = new TrustedTime(now, default, default);

        Assert.Equal(now - floor, trusted.WindowStartUtc);
        Assert.Equal(now + skew, trusted.WindowEndUtc);

        Assert.Equal(ActivityValidationStatus.Valid,
            ActivityValidationPolicy.Validate(Row("at-floor", 1L, endUtc: now - floor), trusted));
        Assert.Equal(ActivityValidationStatus.OutsideHorizon,
            ActivityValidationPolicy.Validate(Row("below-floor", 1L, endUtc: (now - floor).AddTicks(-1)), trusted));
        Assert.Equal(ActivityValidationStatus.Valid,
            ActivityValidationPolicy.Validate(Row("at-skew", 1L, endUtc: now + skew), trusted));
        Assert.Equal(ActivityValidationStatus.FutureTimestamp,
            ActivityValidationPolicy.Validate(Row("above-skew", 1L, endUtc: (now + skew).AddTicks(1)), trusted));
    }

    [Fact]
    public void TrustedTime_AnchorsToDurableProgress_NotToWallClock()
    {
        var now = T0.AddDays(200);
        var watermark = T0.AddDays(190);

        // Anchored: the lower bound is the trusted progress less the floor, NOT now − floor.
        var trusted = new TrustedTime(now, watermark, watermark);
        Assert.Equal(watermark - floor(), trusted.WindowStartUtc);
        Assert.Equal(now + ActivityValidationPolicy.MaxFutureSkew, trusted.WindowEndUtc);

        // A record 50 days before "now" is hopelessly outside the old wall-clock window
        // (14 days) and only 40 days before the trusted progress, so it is inside the
        // TRUSTED window. That is the F-02 fix expressed as a single pure assertion.
        Assert.Equal(ActivityValidationStatus.Valid,
            ActivityValidationPolicy.Validate(Row("old-but-trusted", 1L, endUtc: T0.AddDays(150)), trusted));

        // And a record genuinely before the floor relative to trusted progress is still out.
        Assert.Equal(ActivityValidationStatus.OutsideHorizon,
            ActivityValidationPolicy.Validate(Row("before-the-floor", 1L, endUtc: T0.AddDays(140)), trusted));
    }

    [Fact]
    public void TrustedTime_BackwardClockNeverMovesTheUpperBoundBelowTrustedEnd()
    {
        var trustedEnd = T0.AddDays(50);
        // Current time has moved BACKWARD, well before the already-trusted end.
        var trusted = new TrustedTime(T0.AddDays(20), trustedEnd, trustedEnd);

        Assert.True(trusted.WindowEndUtc >= trustedEnd);
        Assert.Equal(trustedEnd + ActivityValidationPolicy.MaxFutureSkew, trusted.WindowEndUtc);

        // The player's own recent history is still accepted despite the backward jump.
        Assert.Equal(ActivityValidationStatus.Valid,
            ActivityValidationPolicy.Validate(Row("recent", 1L, endUtc: T0.AddDays(49)), trusted));
    }

    [Fact]
    public void TrustedTime_WithoutDurableProgress_AnchorsToNow_AndStaysBounded()
    {
        // A brand-new profile must not inherit the device's entire walking history.
        var now = T0.AddDays(1);
        var trusted = new TrustedTime(now, default, default);

        Assert.Equal(now - ActivityValidationPolicy.TrustedWindowFloor, trusted.WindowStartUtc);
        Assert.Equal(ActivityValidationStatus.Valid,
            ActivityValidationPolicy.Validate(Row("recent", 1L, endUtc: T0.AddDays(1)), trusted));
        Assert.Equal(ActivityValidationStatus.OutsideHorizon,
            ActivityValidationPolicy.Validate(Row("ancient", 1L, endUtc: T0.AddDays(-400)), trusted));
    }

    [Fact]
    public void TrustedTime_ClampsInsteadOfThrowing_NearTheRepresentableMinimum()
    {
        // DateTimeOffset arithmetic is not saturating. A profile whose trusted progress
        // sits at the representable minimum must not throw during validation.
        var trusted = new TrustedTime(
            DateTimeOffset.MinValue,
            DateTimeOffset.MinValue,
            DateTimeOffset.MinValue);

        Assert.Equal(DateTimeOffset.MinValue, trusted.WindowStartUtc);
        var status = ActivityValidationPolicy.Validate(
            Row("edge", 1L, startUtc: T0.AddDays(-3), endUtc: T0.AddDays(-2)), trusted);
        Assert.NotEqual(ActivityValidationStatus.Valid, status);
    }

    [Fact]
    public void TrustedTime_TakesTheLaterUpperAnchor_WhenTheClockIsAheadOfTrustedProgress()
    {
        var trusted = new TrustedTime(T0.AddDays(10), T0.AddDays(40), T0.AddDays(40));
        Assert.Equal(T0.AddDays(40) + ActivityValidationPolicy.MaxFutureSkew, trusted.WindowEndUtc);
    }

    // ==================================================================
    // Accounting identity (task 3.2), asserted on a batch that exercises
    // every counter at once.
    // ==================================================================

    [Fact]
    public void BatchAccounting_SumsToTotalReceived_WithEveryCounterExercised()
    {
        Bootstrap();
        // Seed first: two credited rows, one to be corrected and one to be deleted. The
        // measuring session is created AFTER the seed commits, because a session holds its
        // own in-memory snapshot from Continue() and would not observe the seed's rows.
        var seed = Session(T0.AddDays(1));
        seed.Continue();
        seed.IngestActivityBatch(new[]
        {
            Row("correction-target", 20_000L, endUtc: T0.AddDays(1)),
            Row("deletion-target", 20_000L, endUtc: T0.AddDays(1)),
        });

        var session = Session(T0.AddDays(4));
        Assert.Equal(StartStatus.Loaded, session.Continue().Status);

        var batch = new List<NormalizedActivityRecord>
        {
            Row("brand-new", 10_000L, endUtc: T0.AddDays(3)),                                  // accepted
            Row("brand-new-2", 10_000L, endUtc: T0.AddDays(4)),                                // accepted
            Row("correction-target", 5_000L, revision: 2, endUtc: T0.AddDays(2)),              // correction
            Deletion("deletion-target", 2, endUtc: T0.AddDays(2)),                             // deletion applied
            Deletion("never-seen", 2, endUtc: T0.AddDays(2)),                                  // deletion ignored
            Row("bad-future", 10_000L, endUtc: T0.AddDays(90)),                                 // rejected
            Row("bad-old", 10_000L, endUtc: T0.AddDays(-400)),                                 // rejected
        };

        var result = session.IngestActivityBatch(batch);

        Assert.Equal(batch.Count, result.TotalReceived);
        Assert.Equal(2, result.Accepted);
        Assert.Equal(1, result.CorrectionsApplied);
        Assert.Equal(1, result.DeletionsApplied);
        Assert.Equal(1, result.DeletionsIgnored);
        Assert.Equal(2, result.Rejected);
        AssertAccountingIdentity(result);

        // Then replay the whole batch: every already-trusted row is a no-op.
        var replay = session.IngestActivityBatch(batch);
        Assert.Equal(0L, replay.VitalityCredited);
        AssertAccountingIdentity(replay);
    }

    // ==================================================================
    // Helpers
    // ==================================================================

    private static void AssertAccountingIdentity(IngestResult r)
    {
        int accounted = r.Accepted + r.Rejected + r.DuplicatesIgnored
            + r.CorrectionsApplied + r.DeletionsApplied + r.DeletionsIgnored
            + r.StaleRevisionsIgnored;

        Assert.True(
            accounted == r.TotalReceived,
            "accounting identity broken: accepted(" + r.Accepted + ") + rejected(" + r.Rejected
            + ") + duplicates(" + r.DuplicatesIgnored + ") + corrections(" + r.CorrectionsApplied
            + ") + deletionsApplied(" + r.DeletionsApplied + ") + deletionsIgnored(" + r.DeletionsIgnored
            + ") + staleRevisions(" + r.StaleRevisionsIgnored + ") = " + accounted
            + " but total received = " + r.TotalReceived);
    }

    private static TimeSpan floor() => ActivityValidationPolicy.TrustedWindowFloor;

    private void Bootstrap() =>
        Assert.Equal(StartStatus.NewGameCreated,
            Session(T0).StartNewGame(7UL).Status);

    private GameSession Session(DateTimeOffset now) =>
        TestSessions.Create(_temp, new ManualClock(now));

    private static NormalizedActivityRecord Row(
        string id, long steps, int revision = 1,
        DateTimeOffset? startUtc = null, DateTimeOffset? endUtc = null)
    {
        var start = startUtc ?? (endUtc ?? T0).AddMinutes(-30);
        var end = endUtc ?? start.AddMinutes(20);
        return new NormalizedActivityRecord(Provider, id, ActivityCategory.Walking,
            ActivityUnits.Steps, steps, start, end, revision, false);
    }

    private static NormalizedActivityRecord Deletion(
        string id, int revision, DateTimeOffset? startUtc = null, DateTimeOffset? endUtc = null)
    {
        var start = startUtc ?? (endUtc ?? T0).AddMinutes(-30);
        var end = endUtc ?? start.AddMinutes(20);
        return new NormalizedActivityRecord(Provider, id, ActivityCategory.Walking,
            ActivityUnits.Steps, 0L, start, end, revision, true);
    }

    /// <summary>One walking record per UTC day of the absence, as a provider would return them.</summary>
    private static List<NormalizedActivityRecord> Backlog(int days, int firstDay, long stepsPerDay)
    {
        var list = new List<NormalizedActivityRecord>(days);
        for (int day = firstDay; day < firstDay + days; day++)
        {
            var start = T0.AddDays(day);
            list.Add(new NormalizedActivityRecord(Provider, "day-" + day, ActivityCategory.Walking,
                ActivityUnits.Steps, stepsPerDay, start, start.AddHours(23)));
        }
        return list;
    }

    private GameState Decode()
    {
        var decoded = TestSessions.NewCodec()
            .Decode(File.ReadAllBytes(_temp.FilePath("save.json")));
        Assert.Equal(CodecStatus.Ok, decoded.Status);
        return decoded.State!;
    }
}
