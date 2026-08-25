using System;

namespace WalkGame.Domain.Randomness
{
    /// <summary>Persistable canonical RNG state. Save/reload must continue, never reroll.</summary>
    public struct RngState
    {
        public ulong S0;
        public ulong S1;
        public ulong S2;
        public ulong S3;
    }

    /// <summary>
    /// xoshiro256** generator with SplitMix64 seeding. Algorithm and seeding are fully
    /// specified here so results are identical across .NET and Unity runtimes.
    /// Canonical randomness must come from persisted <see cref="RngState"/>; rendering-side
    /// randomness must never decide rewards.
    /// </summary>
    public sealed class DeterministicRng
    {
        private RngState _state;

        public DeterministicRng(ulong seed)
        {
            _state = SeedWithSplitMix64(seed);
        }

        public DeterministicRng(RngState state)
        {
            _state = state;
            EnsureUsable();
        }

        public RngState Snapshot() => _state;

        public ulong NextUInt64()
        {
            ulong s0 = _state.S0;
            ulong s1 = _state.S1;
            ulong s2 = _state.S2;
            ulong s3 = _state.S3;

            ulong result = RotateLeft(s1 * 5UL, 7) * 9UL;

            ulong t = s1 << 17;
            s2 ^= s0;
            s3 ^= s1;
            s1 ^= s2;
            s0 ^= s3;
            s2 ^= t;
            s3 = RotateLeft(s3, 45);

            _state.S0 = s0;
            _state.S1 = s1;
            _state.S2 = s2;
            _state.S3 = s3;

            return result;
        }

        /// <summary>Uniform in [minInclusive, maxExclusive) using rejection sampling (no modulo bias).</summary>
        public long NextInt64(long minInclusive, long maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Range must be non-empty.");
            ulong range = (ulong)(maxExclusive - minInclusive);
            ulong limit = ulong.MaxValue - ulong.MaxValue % range;
            ulong r;
            do
            {
                r = NextUInt64();
            }
            while (r >= limit);
            return minInclusive + (long)(r % range);
        }

        /// <summary>Uniform double in [0, 1).</summary>
        public double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

        private static ulong RotateLeft(ulong value, int count) => (value << count) | (value >> (64 - count));

        private static RngState SeedWithSplitMix64(ulong seed)
        {
            ulong z = seed;
            var state = new RngState
            {
                S0 = SplitMix64Next(ref z),
                S1 = SplitMix64Next(ref z),
                S2 = SplitMix64Next(ref z),
                S3 = SplitMix64Next(ref z),
            };
            return state;
        }

        private static ulong SplitMix64Next(ref ulong z)
        {
            z += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        private void EnsureUsable()
        {
            ref var s = ref _state;
            if ((s.S0 | s.S1 | s.S2 | s.S3) == 0UL)
            {
                _state = SeedWithSplitMix64(0xA1B2C3D4E5F60718UL);
            }
        }
    }
}
