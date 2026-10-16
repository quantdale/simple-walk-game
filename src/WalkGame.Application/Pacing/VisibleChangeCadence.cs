using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalkGame.Application.ReadModels;

namespace WalkGame.Application.Pacing
{
    /// <summary>
    /// "Does walking visibly matter?" balance metric (D-070). A VISIBLE CHANGE is something the
    /// player can see on the Region screen or the return summary: a landmark stage advancing,
    /// a restoration finishing, or a discovery unlocking. Vitality totals alone do not count.
    /// Pure observation of read models; never mutates state.
    /// </summary>
    public sealed class VisibleChangeTracker
    {
        private string? _last;
        private readonly List<int> _days = new List<int>();

        public IReadOnlyList<int> ChangeDays => _days;

        /// <summary>Observe the end-of-day surfaces. Returns true when something visible changed.</summary>
        public bool Observe(int day, RegionReadModel region, DiscoveriesReadModel discoveries)
        {
            if (region == null) throw new ArgumentNullException(nameof(region));
            if (discoveries == null) throw new ArgumentNullException(nameof(discoveries));
            var sb = new StringBuilder();
            foreach (var l in region.Landmarks.OrderBy(l => l.LandmarkId, StringComparer.Ordinal))
                sb.Append(l.LandmarkId).Append('=').Append((int)l.Stage).Append(';');
            sb.Append("|done=").Append(region.CompletedProjects).Append("|disc=").Append(discoveries.UnlockedCount);
            string now = sb.ToString();
            bool changed = _last != null && now != _last;
            _last = now;
            if (changed) _days.Add(day);
            return changed;
        }
    }

    /// <summary>Summary of when a player saw the valley change.</summary>
    public sealed class CadenceReport
    {
        public CadenceReport(int firstChangeDay, int longestGapDays, int changes, int observedDays)
        {
            FirstChangeDay = firstChangeDay; LongestGapDays = longestGapDays; Changes = changes; ObservedDays = observedDays;
        }
        /// <summary>Day of the first visible change, or 0 if none.</summary>
        public int FirstChangeDay { get; }
        /// <summary>Longest run of days without any visible change, counted from day 0 and up to the last observed day.</summary>
        public int LongestGapDays { get; }
        public int Changes { get; }
        public int ObservedDays { get; }
        public double ChangesPer30Days => ObservedDays <= 0 ? 0 : Changes * 30.0 / ObservedDays;

        public static CadenceReport Analyze(IReadOnlyList<int> changeDays, int observedDays)
        {
            if (changeDays == null) throw new ArgumentNullException(nameof(changeDays));
            if (observedDays < 0) throw new ArgumentOutOfRangeException(nameof(observedDays));
            int prev = 0, longest = 0;
            foreach (int d in changeDays)
            {
                if (d < prev) throw new ArgumentException("change days must be ascending", nameof(changeDays));
                longest = Math.Max(longest, d - prev);
                prev = d;
            }
            longest = Math.Max(longest, observedDays - prev);
            return new CadenceReport(changeDays.Count > 0 ? changeDays[0] : 0, longest, changeDays.Count, observedDays);
        }

        public string ToLine() =>
            $"first visible change: day {FirstChangeDay}; longest gap without change: {LongestGapDays} days; "
            + $"visible changes: {Changes} in {ObservedDays} days ({ChangesPer30Days:0.0} per 30 days)";
    }
}
