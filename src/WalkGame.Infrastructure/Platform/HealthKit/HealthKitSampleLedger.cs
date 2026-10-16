using System;
using System.Collections.Generic;
using System.Linq;

namespace WalkGame.Infrastructure.Platform.HealthKit
{
    /// <summary>
    /// D-113: C# model of the two UUID maps the Swift bridge keeps in <c>UserDefaults</c>
    /// (<c>wg.healthkit.intervals</c>: samples already returned; <c>wg.healthkit.deleted</c>: returned
    /// samples later deleted). Before D-113 both maps grew without bound. <see cref="Prune"/> is the
    /// pruning rule; the Swift bridge mirrors it (UNVERIFIED, never compiled). Not composed: the
    /// adapter stays unwired (D-112).
    ///
    /// Why pruning is safe: the shell never reads further back than <see cref="ReadWindowDays"/>
    /// (ShellController.SyncWindowDays, D-107), and the bridge only emits a <c>del</c> line whose
    /// interval lies inside the read window. An entry that ended before now − <see cref="Retention"/>
    /// can therefore never be emitted again. The extra <see cref="RetentionMarginDays"/> absorbs a
    /// device clock moved back by up to a week. Beyond that a deletion may be missed, which leaves a
    /// credit in place (never a double credit).
    /// </summary>
    public sealed class HealthKitSampleLedger
    {
        public const int ReadWindowDays = 30;
        public const int RetentionMarginDays = 7;
        /// <summary>Hard cap per map, so a pathological source cannot grow UserDefaults without bound.</summary>
        public const int MaxEntriesPerMap = 50_000;
        public static readonly TimeSpan Retention = TimeSpan.FromDays(ReadWindowDays + RetentionMarginDays);

        private readonly Dictionary<string, (long StartMs, long EndMs)> _intervals = new Dictionary<string, (long, long)>(StringComparer.Ordinal);
        private readonly Dictionary<string, (long StartMs, long EndMs)> _deleted = new Dictionary<string, (long, long)>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, (long StartMs, long EndMs)> Intervals => _intervals;
        public IReadOnlyDictionary<string, (long StartMs, long EndMs)> Deleted => _deleted;

        /// <summary>A sample was returned by a read (bridge step 1).</summary>
        public void RecordReturned(string id, long startMs, long endMs)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            if (_deleted.ContainsKey(id)) return; // HealthKit never resurrects a deleted UUID
            _intervals[id] = (startMs, endMs);
        }

        /// <summary>HKDeletedObject seen (bridge step 2). False when the UUID was never returned
        /// (or was already pruned): there is no interval to emit, so nothing to reverse.</summary>
        public bool MarkDeleted(string id)
        {
            if (id == null || !_intervals.TryGetValue(id, out var iv)) return false;
            _intervals.Remove(id);
            _deleted[id] = iv;
            return true;
        }

        /// <summary>The <c>del</c> lines the bridge emits for a read window, ordered by UUID.</summary>
        public IEnumerable<string> DeletionLines(long windowStartMs, long windowEndMs) =>
            _deleted.Where(kv => kv.Value.StartMs >= windowStartMs && kv.Value.EndMs <= windowEndMs)
                    .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                    .Select(kv => "del\t" + kv.Key + "\t" + kv.Value.StartMs + "\t" + kv.Value.EndMs);

        /// <summary>Drops entries that can never be emitted again, then enforces the cap by
        /// evicting the oldest end times (ties by UUID). Returns how many entries were removed.</summary>
        public int Prune(long nowMs)
        {
            long cutoff = nowMs - (long)Retention.TotalMilliseconds;
            return PruneMap(_intervals, cutoff) + PruneMap(_deleted, cutoff);
        }

        private static int PruneMap(Dictionary<string, (long StartMs, long EndMs)> map, long cutoffMs)
        {
            var drop = map.Where(kv => kv.Value.EndMs < cutoffMs).Select(kv => kv.Key).ToList();
            int over = map.Count - drop.Count - MaxEntriesPerMap;
            if (over > 0)
            {
                var keep = new HashSet<string>(drop, StringComparer.Ordinal);
                drop.AddRange(map.Where(kv => !keep.Contains(kv.Key))
                    .OrderBy(kv => kv.Value.EndMs).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                    .Take(over).Select(kv => kv.Key));
            }
            foreach (var k in drop) map.Remove(k);
            return drop.Count;
        }
    }
}
