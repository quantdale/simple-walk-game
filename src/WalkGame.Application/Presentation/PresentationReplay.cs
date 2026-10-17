using System;
using System.Collections.Generic;
using WalkGame.Application.ReadModels;

namespace WalkGame.Application.Presentation
{
    /// <summary>
    /// D-180 (KI-028): which visual changes still have to be shown. A change belongs to a completion
    /// (<see cref="VisualChangeEvent.CompletionId"/>); a completion is shown once, whatever its timestamp,
    /// so a device clock corrected backward can no longer hide a later completion behind the last shown
    /// instant. Pure and deterministic; the Unity controller and <see cref="GameSession"/> share it.
    /// </summary>
    public static class PresentationReplay
    {
        /// <summary>
        /// The events of <paramref name="all"/> (the full change stream, in stream order) not yet shown.
        /// <paramref name="shownThroughUtc"/> null = the world was never opened: the current world is the
        /// baseline and nothing is replayed. <paramref name="shownCompletionIds"/> null = not recorded (old
        /// memory): fall back to the timestamp rule (only events after <paramref name="shownThroughUtc"/>).
        /// </summary>
        public static IReadOnlyList<VisualChangeEvent> Unshown(
            IReadOnlyList<VisualChangeEvent> all, IReadOnlyCollection<string>? shownCompletionIds, DateTimeOffset? shownThroughUtc)
        {
            if (all == null) throw new ArgumentNullException(nameof(all));
            if (shownThroughUtc == null) return Array.Empty<VisualChangeEvent>();
            var shown = shownCompletionIds == null ? null : new HashSet<string>(shownCompletionIds, StringComparer.Ordinal);
            var result = new List<VisualChangeEvent>();
            foreach (var e in all)
            {
                bool include = shown != null && e.CompletionId != null
                    ? !shown.Contains(e.CompletionId)
                    : e.AtUtc.HasValue && e.AtUtc.Value > shownThroughUtc.Value;
                if (include) result.Add(e);
            }
            return result;
        }

        /// <summary>Distinct completion ids of <paramref name="events"/>, in stream order.</summary>
        public static List<string> CompletionIdsOf(IEnumerable<VisualChangeEvent> events)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var ids = new List<string>();
            if (events != null)
                foreach (var e in events)
                    if (e.CompletionId != null && seen.Add(e.CompletionId)) ids.Add(e.CompletionId);
            return ids;
        }

        /// <summary>The later of <paramref name="floor"/> and every event time.</summary>
        public static DateTimeOffset Latest(IEnumerable<VisualChangeEvent> events, DateTimeOffset floor)
        {
            var latest = floor;
            if (events != null)
                foreach (var e in events) if (e.AtUtc.HasValue && e.AtUtc.Value > latest) latest = e.AtUtc.Value;
            return latest;
        }
    }
}
