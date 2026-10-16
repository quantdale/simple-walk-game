using System;
using WalkGame.Domain.Activity;

namespace WalkGame.Domain.Tests;

/// <summary>D-103: local day numbers and the maintained days-walked aggregate.</summary>
public sealed class DaysWalkedTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 10, 23, 30, 0, TimeSpan.Zero);

    [Fact]
    public void MorningWalkInUtcPlus8_FallsOnTheNextUtcDate()
    {
        int? utc = LocalDayCalendar.DayOf(T0, 0);
        int? manila = LocalDayCalendar.DayOf(T0, 8 * 3600);
        Assert.Equal(utc + 1, manila);
        Assert.Equal(LocalDayCalendar.UtcDayOf(T0), utc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(18 * 3600 + 1)]
    [InlineData(-18 * 3600 - 1)]
    public void UnknownOrOutOfRangeOffset_HasNoDay(int? offset) =>
        Assert.Null(LocalDayCalendar.DayOf(T0, offset));

    [Fact]
    public void Credits_AddDistinctDays_UndatedCreditsMarkALowerBound()
    {
        var l = new ProcessedRecordLedgerState();
        l.Record(Row("a", 500, 100));
        l.Record(Row("b", 500, 100));
        l.Record(Row("c", 500, 101));
        l.Record(Row("z", 0, 102)); // zero steps never count a day
        Assert.Equal(new[] { 100, 101 }, l.WalkedLocalDays);
        Assert.Equal(2, l.DaysWalkedCount);
        Assert.False(l.DaysWalkedIsLowerBound);

        l.Record(Row("u", 500, null));
        Assert.Equal(2, l.DaysWalkedCount);
        Assert.True(l.DaysWalkedIsLowerBound);
    }

    [Fact]
    public void Deletion_RemovesADayOnlyWhenNoOtherRowCreditsIt()
    {
        var l = new ProcessedRecordLedgerState();
        l.Record(Row("a", 500, 100));
        l.Record(Row("b", 500, 100));
        l.Record(Row("c", 500, 101));
        l.Update(Row("a", 0, 100));
        Assert.Equal(new[] { 100, 101 }, l.WalkedLocalDays);
        l.Update(Row("c", 0, 101));
        Assert.Equal(new[] { 100 }, l.WalkedLocalDays);
    }

    [Fact]
    public void CorrectionThatMovesADay_MovesTheCount()
    {
        var l = new ProcessedRecordLedgerState();
        l.Record(Row("a", 500, 100));
        l.Update(Row("a", 600, 101));
        Assert.Equal(new[] { 101 }, l.WalkedLocalDays);
    }

    [Fact]
    public void Retirement_KeepsDays_AndProtectsRetiredDaysFromDeletion()
    {
        var l = new ProcessedRecordLedgerState();
        l.Record(Row("old", 500, 100, T0.AddDays(-60)));
        l.Record(Row("new", 500, 100, T0));
        Assert.Equal(1, l.Retire(T0.AddDays(-45), T0, 256));
        Assert.Equal(100, l.RetiredThroughLocalDay);
        l.Update(Row("new", 0, 100, T0));
        Assert.Equal(new[] { 100 }, l.WalkedLocalDays); // the retired row still walked that day
        Assert.Equal(1, l.DaysWalkedCount);
    }

    [Fact]
    public void LegacyLowerBound_IsAddedWithoutOverlappingTheMigrationBoundary()
    {
        var l = new ProcessedRecordLedgerState { LegacyDaysLowerBound = 5, LegacyLastUtcDay = 200, IsLegacyLowerBound = true };
        l.Record(Row("a", 500, 201)); // could be the same local date as the last legacy record
        Assert.Equal(5, l.DaysWalkedCount);
        l.Record(Row("b", 500, 202));
        Assert.Equal(6, l.DaysWalkedCount);
        Assert.True(l.DaysWalkedIsLowerBound);
    }

    private static ProcessedRecordEntry Row(string id, long steps, int? day, DateTimeOffset? end = null) =>
        new(id, 1, steps, steps / 100, (end ?? T0), 1, end ?? T0, day);
}
