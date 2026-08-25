using System;
using System.Collections.Generic;
using System.Linq;
using WalkGame.Domain.Randomness;

namespace WalkGame.Domain.Tests;

public class DeterministicRngTests
{
    [Fact]
    public void SameSeed_ProducesIdenticalSequenceAcrossInstances_For100Draws()
    {
        var first = new DeterministicRng(20260825UL);
        var second = new DeterministicRng(20260825UL);

        var seqA = Enumerable.Range(0, 100).Select(_ => first.NextUInt64()).ToArray();
        var seqB = Enumerable.Range(0, 100).Select(_ => second.NextUInt64()).ToArray();

        Assert.Equal(seqA, seqB);
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentSequences()
    {
        var first = new DeterministicRng(1UL);
        var second = new DeterministicRng(2UL);

        ulong[] seqA = Enumerable.Range(0, 16).Select(_ => first.NextUInt64()).ToArray();
        ulong[] seqB = Enumerable.Range(0, 16).Select(_ => second.NextUInt64()).ToArray();

        Assert.NotEqual(seqA, seqB);
    }

    [Fact]
    public void Snapshot_RestoredIntoNewInstance_ContinuesExactSequence()
    {
        var original = new DeterministicRng(777UL);
        for (int i = 0; i < 37; i++)
            original.NextUInt64();
        RngState saved = original.Snapshot();

        ulong[] expected = Enumerable.Range(0, 10).Select(_ => original.NextUInt64()).ToArray();

        var restored = new DeterministicRng(saved);
        ulong[] actual = Enumerable.Range(0, 10).Select(_ => restored.NextUInt64()).ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AllZeroState_IsNormalizedWithoutCrash_AndRemainsDeterministic()
    {
        var state = new RngState();
        var first = new DeterministicRng(state);
        var second = new DeterministicRng(state);

        ulong[] seqA = Enumerable.Range(0, 32).Select(_ => first.NextUInt64()).ToArray();
        ulong[] seqB = Enumerable.Range(0, 32).Select(_ => second.NextUInt64()).ToArray();

        Assert.Equal(seqA, seqB);
        Assert.Contains(seqA, value => value != 0UL);
    }

    [Fact]
    public void NextInt64_StaysWithinInclusiveExclusiveBounds_Over1000Draws()
    {
        var rng = new DeterministicRng(424242UL);
        var seen = new HashSet<long>();

        for (int i = 0; i < 1000; i++)
        {
            long value = rng.NextInt64(-5L, 6L);
            Assert.InRange(value, -5L, 5L);
            seen.Add(value);
        }

        Assert.True(seen.Count >= 2, "expected the range to be exercised with multiple distinct values");
    }

    [Fact]
    public void NextInt64_SingleValueRange_ReturnsBoundForAll1000Draws()
    {
        var rng = new DeterministicRng(9UL);

        for (int i = 0; i < 1000; i++)
            Assert.Equal(0L, rng.NextInt64(0L, 1L));
    }

    [Fact]
    public void NextInt64_EmptyRangeThrows()
    {
        var rng = new DeterministicRng(1UL);

        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt64(5L, 5L));
    }

    [Fact]
    public void NextDouble_AlwaysWithinHalfOpenUnitInterval_Over1000Draws()
    {
        var rng = new DeterministicRng(31337UL);

        for (int i = 0; i < 1000; i++)
        {
            double value = rng.NextDouble();
            Assert.True(value >= 0.0 && value < 1.0, $"value {value} outside [0,1)");
        }
    }
}
