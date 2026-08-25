using System;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Economy;
using RewardTransactionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RewardTransactionIdKind>;

namespace WalkGame.Domain.Tests;

public class RewardLedgerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private static RewardTransaction Tx(string transactionId, long vitalityAmount) =>
        new(new RewardTransactionId(transactionId), T0, vitalityAmount, "walk-session");

    [Fact]
    public void Apply_UnknownTransaction_CreditsVitalityOnce_AndReturnsAppliedFirstTime()
    {
        var ledger = new RewardLedgerState();
        var balances = new ResourceBalances();

        var outcome = ledger.Apply(Tx("tx-0001", 125L), balances);

        Assert.Equal(LedgerApplyOutcome.AppliedFirstTime, outcome);
        Assert.Equal(125L, balances.Get(ResourceType.Vitality));
        Assert.Equal(125L, ledger.TotalVitalityCredited);
        Assert.Single(ledger.Records);
        Assert.True(ledger.HasTransaction(new RewardTransactionId("tx-0001")));
    }

    [Fact]
    public void Apply_SameTransactionIdReapplied_ReturnsDuplicateIgnored_WithoutSecondCredit()
    {
        var ledger = new RewardLedgerState();
        var balances = new ResourceBalances();
        var transaction = Tx("tx-0002", 90L);

        Assert.Equal(LedgerApplyOutcome.AppliedFirstTime, ledger.Apply(transaction, balances));
        Assert.Equal(LedgerApplyOutcome.DuplicateIgnored, ledger.Apply(transaction, balances));

        Assert.Equal(90L, balances.Get(ResourceType.Vitality));
        Assert.Equal(90L, ledger.TotalVitalityCredited);
        Assert.Single(ledger.Records);
    }

    [Fact]
    public void Apply_DifferentTransactionIds_BothApplyAndAccumulate()
    {
        var ledger = new RewardLedgerState();
        var balances = new ResourceBalances();

        Assert.Equal(LedgerApplyOutcome.AppliedFirstTime, ledger.Apply(Tx("tx-a", 10L), balances));
        Assert.Equal(LedgerApplyOutcome.AppliedFirstTime, ledger.Apply(Tx("tx-b", 15L), balances));

        Assert.Equal(25L, balances.Get(ResourceType.Vitality));
        Assert.Equal(25L, ledger.TotalVitalityCredited);
        Assert.Equal(2, ledger.Records.Count);
        Assert.True(ledger.HasTransaction(new RewardTransactionId("tx-a")));
        Assert.True(ledger.HasTransaction(new RewardTransactionId("tx-b")));
    }

    [Fact]
    public void Apply_NegativeAmountThrows_AndLeavesStateUntouched()
    {
        var ledger = new RewardLedgerState();
        var balances = new ResourceBalances();

        Assert.Throws<ArgumentOutOfRangeException>(() => ledger.Apply(Tx("tx-neg", -1L), balances));

        Assert.Empty(ledger.Records);
        Assert.Equal(0L, balances.Get(ResourceType.Vitality));
        Assert.False(ledger.HasTransaction(new RewardTransactionId("tx-neg")));
    }
}
