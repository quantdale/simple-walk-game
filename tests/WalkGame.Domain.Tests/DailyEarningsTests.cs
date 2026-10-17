using System;
using WalkGame.Domain.Activity;
using Xunit;

namespace WalkGame.Domain.Tests;

/// <summary>D-156: per-day earnings derived from processed-record rows (no persisted ledger).</summary>
public sealed class DailyEarningsTests
{
    private static readonly DateTimeOffset T = new(2026, 3, 10, 8, 0, 0, TimeSpan.Zero);

    private static ProcessedRecordEntry Row(string id, long steps, long vitality, int? day) =>
        new(id, 1, steps, vitality, T, 1, T, day);

    [Fact]
    public void Sums_net_applied_vitality_of_rows_on_that_day_only()
    {
        var p = new ProcessedRecordLedgerState();
        p.Record(Row("a", 2000, 20, 100));
        p.Record(Row("b", 550, 5, 100));
        p.Record(Row("c", 9000, 90, 101));
        Assert.Equal(25L, p.VitalityOnLocalDay(100));
        Assert.Equal(90L, p.VitalityOnLocalDay(101));
        Assert.Equal(0L, p.VitalityOnLocalDay(102));
    }

    [Fact]
    public void Undated_rows_never_count()
    {
        var p = new ProcessedRecordLedgerState();
        p.Record(Row("a", 2000, 20, null));
        Assert.Equal(0L, p.VitalityOnLocalDay(100));
    }

    [Fact]
    public void Deleted_row_counts_zero_even_when_its_clawback_was_clamped()
    {
        var p = new ProcessedRecordLedgerState();
        p.Record(Row("a", 2000, 20, 100));
        p.Update(new ProcessedRecordEntry("a", 1, 0L, 7L, T, 2, T, 100)); // 13 reversed, 7 clamped by balance
        Assert.Equal(0L, p.VitalityOnLocalDay(100));
    }

    [Fact]
    public void Correction_down_reduces_the_day_and_a_moved_row_moves_its_credit()
    {
        var p = new ProcessedRecordLedgerState();
        p.Record(Row("a", 3000, 30, 100));
        p.Record(Row("b", 1000, 10, 100));
        p.Update(new ProcessedRecordEntry("a", 1, 1200L, 12L, T, 2, T, 100));
        Assert.Equal(22L, p.VitalityOnLocalDay(100));
        p.Update(new ProcessedRecordEntry("b", 1, 1000L, 10L, T, 2, T, 101));
        Assert.Equal(12L, p.VitalityOnLocalDay(100));
        Assert.Equal(10L, p.VitalityOnLocalDay(101));
    }

    [Fact]
    public void A_day_that_may_include_retired_rows_is_unknown()
    {
        var p = new ProcessedRecordLedgerState();
        p.Record(new ProcessedRecordEntry("old", 1, 2000, 20, T.AddDays(-60), 1, T.AddDays(-60), 50));
        p.Record(Row("new", 2000, 20, 100));
        Assert.Equal(1, p.Retire(T.AddDays(-45), T, 256));
        Assert.Null(p.VitalityOnLocalDay(50));
        Assert.Null(p.VitalityOnLocalDay(49));
        Assert.Equal(20L, p.VitalityOnLocalDay(100));
    }
}
