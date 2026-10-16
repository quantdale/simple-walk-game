using System;
using System.Collections.Generic;
using System.Linq;

namespace WalkGame.Infrastructure.Platform.HealthKit
{
    /// <summary>Source priority, highest first (D-113). Lower value wins an overlap.</summary>
    public enum HealthKitSourceKind { Watch = 0, Phone = 1, Other = 2 }

    /// <summary>A step sample with its HKSource/HKDevice classification.</summary>
    public sealed class HealthKitSourcedSample
    {
        public HealthKitSourcedSample(string id, long startMs, long endMs, long count, HealthKitSourceKind kind)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            StartMs = startMs; EndMs = endMs; Count = count; Kind = kind;
        }
        public string Id { get; }
        public long StartMs { get; }
        public long EndMs { get; }
        public long Count { get; }
        public HealthKitSourceKind Kind { get; }
    }

    /// <summary>
    /// D-113 design for KI-022's iPhone + Apple Watch double counting. HealthKit returns both
    /// devices' step samples for the same walk; summing them double-credits. Health.app resolves
    /// this with a source priority list; this mirrors it with a fixed order Watch &gt; Phone &gt; Other.
    ///
    /// Rule: a sample keeps the share of its steps that falls outside the time already covered by
    /// strictly higher-priority samples (linear proration, floored). Same-priority samples are not
    /// merged (one device does not double-count itself). The result is independent of input order.
    /// D-114: wired into <see cref="HealthKitActivityAdapter"/> through the bridge's optional sixth
    /// field. The adapter itself is still NOT composed (D-112), so no shipped build runs this.
    /// </summary>
    public static class HealthKitSourceDedup
    {
        /// <summary>Classifies from HKDevice.model / HKSource.bundleIdentifier. Only Apple's own collectors get Watch/Phone; anything else → Other.</summary>
        public static HealthKitSourceKind Classify(string? deviceModel, string? bundleIdentifier)
        {
            string m = (deviceModel ?? "").Trim();
            string b = (bundleIdentifier ?? "").Trim();
            // Third-party apps (manual entries, other trackers) are Other even on an iPhone.
            bool apple = b.StartsWith("com.apple.", StringComparison.Ordinal);
            if (apple && m.StartsWith("Watch", StringComparison.Ordinal)) return HealthKitSourceKind.Watch;
            if (apple && m.StartsWith("iPhone", StringComparison.Ordinal)) return HealthKitSourceKind.Phone;
            return HealthKitSourceKind.Other;
        }

        /// <summary>Returns id → credited steps (≤ the sample's count, ≥ 0) for every input sample.</summary>
        public static IReadOnlyDictionary<string, long> Resolve(IEnumerable<HealthKitSourcedSample> samples)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            var ordered = samples.OrderBy(s => s.Kind).ThenBy(s => s.StartMs).ThenBy(s => s.Id, StringComparer.Ordinal).ToList();
            var result = new Dictionary<string, long>(StringComparer.Ordinal);
            var covered = new List<(long S, long E)>();   // union of strictly higher-priority intervals
            var pending = new List<(long S, long E)>();   // current priority level, added after the level ends
            HealthKitSourceKind? level = null;
            foreach (var s in ordered)
            {
                if (level != s.Kind) { covered = Union(covered.Concat(pending)); pending.Clear(); level = s.Kind; }
                long count = Math.Max(0, s.Count);
                long credited;
                if (s.EndMs <= s.StartMs)
                    credited = covered.Any(c => s.StartMs >= c.S && s.StartMs < c.E) ? 0 : count;
                else
                {
                    long dur = s.EndMs - s.StartMs;
                    long overlap = covered.Sum(c => Math.Max(0, Math.Min(c.E, s.EndMs) - Math.Max(c.S, s.StartMs)));
                    credited = (long)Math.Floor((decimal)count * (dur - overlap) / dur);
                    pending.Add((s.StartMs, s.EndMs));
                }
                result[s.Id] = result.TryGetValue(s.Id, out var prev) ? Math.Min(prev, credited) : credited;
            }
            return result;
        }

        private static List<(long S, long E)> Union(IEnumerable<(long S, long E)> intervals)
        {
            var merged = new List<(long S, long E)>();
            foreach (var iv in intervals.OrderBy(i => i.S))
            {
                if (merged.Count > 0 && iv.S <= merged[merged.Count - 1].E)
                    merged[merged.Count - 1] = (merged[merged.Count - 1].S, Math.Max(merged[merged.Count - 1].E, iv.E));
                else merged.Add(iv);
            }
            return merged;
        }
    }
}
