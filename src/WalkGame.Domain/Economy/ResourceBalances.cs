using System;
using System.Collections.Generic;

namespace WalkGame.Domain.Economy
{
    public enum ResourceType
    {
        Vitality = 1,
        Materials = 2,
        Knowledge = 3,
    }

    /// <summary>
    /// Canonical resource accounting. Integer units only (no floating-point currency).
    /// Invariants: balances never negative; failed spends consume nothing; adds clamp
    /// explicitly to caps and report how much was actually applied.
    /// </summary>
    public sealed class ResourceBalances
    {
        public Dictionary<ResourceType, long> Amounts { get; } = new Dictionary<ResourceType, long>();

        public Dictionary<ResourceType, long> Caps { get; } = new Dictionary<ResourceType, long>();

        public long Get(ResourceType type) => Amounts.TryGetValue(type, out var value) ? value : 0L;

        public long GetCap(ResourceType type) => Caps.TryGetValue(type, out var cap) ? cap : long.MaxValue;

        public void SetCap(ResourceType type, long capUnits)
        {
            if (capUnits < 0)
                throw new ArgumentOutOfRangeException(nameof(capUnits), "Caps cannot be negative.");
            Caps[type] = capUnits;
            if (Get(type) > capUnits)
                Amounts[type] = capUnits;
        }

        /// <summary>Adds amount, clamped to the cap. Returns the amount actually applied.</summary>
        public long Add(ResourceType type, long amountUnits)
        {
            if (amountUnits < 0)
                throw new ArgumentOutOfRangeException(nameof(amountUnits), "Use TryConsume for spending; negative adds are forbidden.");

            long current = Get(type);
            long cap = GetCap(type);
            long headroom = cap > current ? cap - current : 0L;
            long roomByOverflow = long.MaxValue - current;
            long applied = Math.Min(Math.Min(amountUnits, headroom), roomByOverflow);
            if (applied <= 0L)
                return 0L;

            Amounts[type] = current + applied;
            return applied;
        }

        /// <summary>All-or-nothing consumption. Returns false (consuming nothing) when insufficient.</summary>
        public bool TryConsume(ResourceType type, long amountUnits)
        {
            if (amountUnits < 0)
                throw new ArgumentOutOfRangeException(nameof(amountUnits), "Cannot consume negative amounts.");
            if (amountUnits == 0L)
                return true;

            long current = Get(type);
            if (current < amountUnits)
                return false;

            Amounts[type] = current - amountUnits;
            return true;
        }
    }
}
