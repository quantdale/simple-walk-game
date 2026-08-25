using System;
using WalkGame.Domain.Economy;

namespace WalkGame.Domain.Tests;

public class ResourceBalancesTests
{
    [Fact]
    public void Get_MissingEntriesDefaultToZero_AndCapDefaultsToUnbounded()
    {
        var balances = new ResourceBalances();

        Assert.Equal(0L, balances.Get(ResourceType.Vitality));
        Assert.Equal(0L, balances.Get(ResourceType.Materials));
        Assert.Equal(long.MaxValue, balances.GetCap(ResourceType.Knowledge));
    }

    [Fact]
    public void Add_ClampsToCap_AndReturnsOnlyAppliedPortion()
    {
        var balances = new ResourceBalances();
        balances.SetCap(ResourceType.Materials, 100L);

        Assert.Equal(70L, balances.Add(ResourceType.Materials, 70L));
        Assert.Equal(30L, balances.Add(ResourceType.Materials, 70L));
        Assert.Equal(0L, balances.Add(ResourceType.Materials, 25L));
        Assert.Equal(100L, balances.Get(ResourceType.Materials));
    }

    [Fact]
    public void Add_WithoutCap_SaturatesAtLongMaxValue()
    {
        var balances = new ResourceBalances();

        Assert.Equal(long.MaxValue, balances.Add(ResourceType.Vitality, long.MaxValue));
        Assert.Equal(long.MaxValue, balances.Get(ResourceType.Vitality));
        Assert.Equal(0L, balances.Add(ResourceType.Vitality, 1L));
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    public void Add_NegativeAmountThrows(long amount)
    {
        var balances = new ResourceBalances();

        Assert.Throws<ArgumentOutOfRangeException>(() => balances.Add(ResourceType.Vitality, amount));
    }

    [Fact]
    public void TryConsume_IsAllOrNothing_OnInsufficientBalance()
    {
        var balances = new ResourceBalances();
        balances.Add(ResourceType.Vitality, 50L);

        Assert.False(balances.TryConsume(ResourceType.Vitality, 51L));
        Assert.Equal(50L, balances.Get(ResourceType.Vitality));

        Assert.True(balances.TryConsume(ResourceType.Vitality, 50L));
        Assert.Equal(0L, balances.Get(ResourceType.Vitality));
    }

    [Fact]
    public void TryConsume_ZeroAlwaysSucceeds_EvenAtZeroBalance()
    {
        var balances = new ResourceBalances();

        Assert.True(balances.TryConsume(ResourceType.Materials, 0L));
        Assert.Equal(0L, balances.Get(ResourceType.Materials));
    }

    [Fact]
    public void TryConsume_NegativeAmountThrows()
    {
        var balances = new ResourceBalances();

        Assert.Throws<ArgumentOutOfRangeException>(() => balances.TryConsume(ResourceType.Knowledge, -1L));
    }

    [Fact]
    public void SetCap_ClampsExistingBalanceDown_AndRejectsNegativeCaps()
    {
        var balances = new ResourceBalances();
        balances.Add(ResourceType.Materials, 80L);

        balances.SetCap(ResourceType.Materials, 30L);

        Assert.Equal(30L, balances.GetCap(ResourceType.Materials));
        Assert.Equal(30L, balances.Get(ResourceType.Materials));
        Assert.Throws<ArgumentOutOfRangeException>(() => balances.SetCap(ResourceType.Materials, -1L));
    }
}
