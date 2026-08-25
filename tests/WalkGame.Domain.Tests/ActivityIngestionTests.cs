using System;
using System.Collections.Generic;
using WalkGame.Domain.Activity;

namespace WalkGame.Domain.Tests;

public sealed class ActivityIngestionTests
{
    private static readonly DateTimeOffset Now =
        new DateTimeOffset(2026, 3, 10, 8, 0, 0, TimeSpan.Zero);

    private static NormalizedActivityRecord Valid(
        long quantity = 6400L,
        string? sourceRecordId = "rec-1",
        string provider = "fixture",
        string unit = "steps",
        DateTimeOffset? start = null,
        DateTimeOffset? end = null) =>
        new NormalizedActivityRecord(
            provider,
            sourceRecordId,
            ActivityCategory.Walking,
            unit,
            quantity,
            start ?? new DateTimeOffset(2026, 3, 10, 7, 0, 0, TimeSpan.Zero),
            end ?? new DateTimeOffset(2026, 3, 10, 7, 45, 0, TimeSpan.Zero));

    [Fact]
    public void Validate_AcceptsWellFormedWalkingRecord()
    {
        Assert.Equal(ActivityValidationStatus.Valid, ActivityValidationPolicy.Validate(Valid(), Now));
    }

    [Fact]
    public void Validate_RejectsEmptyOrWhitespaceProvider()
    {
        Assert.Equal(ActivityValidationStatus.EmptyProvider, ActivityValidationPolicy.Validate(Valid(provider: ""), Now));
        Assert.Equal(ActivityValidationStatus.EmptyProvider, ActivityValidationPolicy.Validate(Valid(provider: "   "), Now));
    }

    [Fact]
    public void Validate_RejectsUnsupportedCategoryAndUnit()
    {
        Assert.Equal(
            ActivityValidationStatus.UnsupportedCategory,
            ActivityValidationPolicy.Validate(Valid() with { Category = (ActivityCategory)9 }, Now));
        Assert.Equal(
            ActivityValidationStatus.UnsupportedUnit,
            ActivityValidationPolicy.Validate(Valid(unit: "kilometers"), Now));
        Assert.Equal(
            ActivityValidationStatus.UnsupportedUnit,
            ActivityValidationPolicy.Validate(Valid(unit: "Steps"), Now));
    }

    [Theory]
    [InlineData(0L, ActivityValidationStatus.ZeroQuantity)]
    [InlineData(-1L, ActivityValidationStatus.NegativeQuantity)]
    public void Validate_RejectsNonPositiveQuantities(long quantity, ActivityValidationStatus expected)
    {
        Assert.Equal(expected, ActivityValidationPolicy.Validate(Valid(quantity: quantity), Now));
    }

    [Fact]
    public void Validate_RejectsMalformedWindows()
    {
        var reversed = Valid(start: Now.AddMinutes(-5), end: Now.AddMinutes(-15));
        Assert.Equal(ActivityValidationStatus.MalformedTimestamps, ActivityValidationPolicy.Validate(reversed, Now));

        var defaultStart = Valid() with { StartUtc = default };
        Assert.Equal(ActivityValidationStatus.MalformedTimestamps, ActivityValidationPolicy.Validate(defaultStart, Now));

        var zeroEnd = Valid() with { EndUtc = default };
        Assert.Equal(ActivityValidationStatus.MalformedTimestamps, ActivityValidationPolicy.Validate(zeroEnd, Now));
    }

    [Fact]
    public void Validate_FutureEndBeyondSkew_IsRejected_WithinSkew_IsAccepted()
    {
        var slightlyFuture = Valid(end: Now.AddMinutes(9));
        Assert.Equal(ActivityValidationStatus.Valid, ActivityValidationPolicy.Validate(slightlyFuture, Now));

        var farFuture = Valid(end: Now.AddMinutes(11));
        Assert.Equal(ActivityValidationStatus.FutureTimestamp, ActivityValidationPolicy.Validate(farFuture, Now));

        // The policy is clock-relative: advancing the clock makes yesterday's
        // "future" record acceptable without any code change.
        Assert.Equal(
            ActivityValidationStatus.Valid,
            ActivityValidationPolicy.Validate(farFuture, Now.AddHours(1)));
    }

    [Theory]
    [InlineData(249999L, 249999L)]
    [InlineData(250000L, 250000L)]
    [InlineData(250001L, 250000L)]
    [InlineData(900000L, 250000L)]
    public void ClampQuantity_BoundsPathologicalStepInputs(long input, long expected)
    {
        Assert.Equal(expected, ActivityValidationPolicy.ClampQuantity(ActivityCategory.Walking, input));
    }

    [Theory]
    [InlineData(0L, 0L)]
    [InlineData(99L, 0L)]
    [InlineData(100L, 1L)]
    [InlineData(101L, 1L)]
    [InlineData(199L, 1L)]
    [InlineData(200L, 2L)]
    [InlineData(6400L, 64L)]
    [InlineData(12345L, 123L)]
    [InlineData(250000L, 2500L)]
    public void ConversionRuleV1_IsIntegerFloorDivision(long steps, long expectedVitality)
    {
        Assert.Equal(expectedVitality, StepConversionRuleV1.ConvertSteps(steps));
        Assert.Equal(1, StepConversionRuleV1.RuleVersion);
        Assert.Equal(100L, StepConversionRuleV1.StepsPerVitality);
    }

    [Fact]
    public void Identity_SourceRecordIdIsPreferredAndStable()
    {
        string key = ActivityIdentity.Compute(Valid());

        // Same logical record re-emitted with unrelated window noise still matches
        // because platform identity is authoritative.
        var noisyWindow = Valid(sourceRecordId: " rec-1 ") with
        {
            StartUtc = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
        };
        Assert.Equal(key, ActivityIdentity.Compute(noisyWindow));
        Assert.StartsWith("rec1|", key);

        // A different platform record is a different identity even with identical fields.
        Assert.NotEqual(key, ActivityIdentity.Compute(Valid(sourceRecordId: "rec-2")));
    }

    [Fact]
    public void Identity_FingerprintFallbackIsDeterministicAndOffsetIndependent()
    {
        var a = Valid(sourceRecordId: null);
        var b = Valid(sourceRecordId: null) with
        {
            StartUtc = new DateTimeOffset(2026, 3, 10, 9, 0, 0, TimeSpan.FromHours(2)),
            EndUtc = new DateTimeOffset(2026, 3, 10, 9, 45, 0, TimeSpan.FromHours(2)),
        };

        string keyA = ActivityIdentity.Compute(a);
        string keyB = ActivityIdentity.Compute(b);
        Assert.Equal(keyA, keyB);
        Assert.StartsWith("fpt1|", keyA);

        Assert.NotEqual(keyA, ActivityIdentity.Compute(Valid(sourceRecordId: null, quantity: 6401L)));
        Assert.NotEqual(keyA, ActivityIdentity.Compute(Valid(sourceRecordId: null, provider: "other")));
    }

    [Fact]
    public void RewardIds_AreDerivedStablyFromIdentityPlusRuleVersion()
    {
        var identity = ActivityIdentity.Compute(Valid());
        Guid first = ActivityRewardIds.DeriveTransactionGuid(identity, StepConversionRuleV1.RuleVersion);
        Guid second = ActivityRewardIds.DeriveTransactionGuid(identity, StepConversionRuleV1.RuleVersion);

        Assert.Equal(first, second);
        Assert.NotEqual(Guid.Empty, first);
        Assert.NotEqual(
            first,
            ActivityRewardIds.DeriveTransactionGuid(identity, StepConversionRuleV1.RuleVersion + 1));
        Assert.NotEqual(
            first,
            ActivityRewardIds.DeriveTransactionGuid(ActivityIdentity.Compute(Valid(sourceRecordId: "rec-2")), StepConversionRuleV1.RuleVersion));
    }

    [Fact]
    public void ProcessedLedger_RecordsExactlyOnceAndRejectsRepeats()
    {
        var ledger = new ProcessedRecordLedgerState();
        string key = ActivityIdentity.Compute(Valid());

        Assert.False(ledger.HasProcessed(key));
        ledger.Record(new ProcessedRecordEntry(key, StepConversionRuleV1.RuleVersion, 6400L, 64L, Now));
        Assert.True(ledger.HasProcessed(key));
        Assert.Throws<InvalidOperationException>(
            () => ledger.Record(new ProcessedRecordEntry(key, StepConversionRuleV1.RuleVersion, 6400L, 64L, Now)));
        Assert.Equal(64L, ledger.TotalVitalityCredited);
        Assert.Equal(1, ledger.Count);
    }

    [Fact]
    public void ProcessedLedger_TracksZeroCreditEntriesForReplaySafety()
    {
        var ledger = new ProcessedRecordLedgerState();
        string subUnitKey = ActivityIdentity.Compute(Valid(quantity: 99L, sourceRecordId: null));

        ledger.Record(new ProcessedRecordEntry(subUnitKey, StepConversionRuleV1.RuleVersion, 99L, 0L, Now));
        Assert.Equal(0L, ledger.TotalVitalityCredited);
        Assert.True(ledger.HasProcessed(subUnitKey));
    }
}
