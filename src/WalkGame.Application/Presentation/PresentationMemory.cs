using System;
using System.Collections.Generic;

namespace WalkGame.Application.Presentation
{
    /// <summary>
    /// D-175 (KI-027 c): what the world presentation has already shown the player, so "changes since
    /// last open" survives a cold restart (bible §22) and seen lines stay seen. Display memory only:
    /// it is never canonical state, never read by progression, kept outside the save envelope in its
    /// own small versioned file, erased by "Delete all local data" and not part of the privacy export.
    /// </summary>
    public sealed class PresentationMemoryState
    {
        public const int CurrentVersion = 1;
        /// <summary>Bound on remembered line ids (Region 1 authors a few dozen); extra ids are dropped on save and load.</summary>
        public const int MaxSeenLineIds = 512;
        /// <summary>Longest accepted line id; longer (damaged or hand-edited) ids are dropped.</summary>
        public const int MaxLineIdLength = 128;

        public int SchemaVersion { get; set; } = CurrentVersion;

        /// <summary>
        /// The game this memory belongs to (<c>GameState.CreatedAtUtc</c>). Memory from another game
        /// (after a new game replaced the save) is ignored, never applied.
        /// </summary>
        public DateTimeOffset? GameCreatedAtUtc { get; set; }

        /// <summary>Latest canonical change time already shown; null = nothing shown yet (first open: baseline, no replay).</summary>
        public DateTimeOffset? ChangesShownThroughUtc { get; set; }

        /// <summary>Dialogue line ids already marked seen, ordinal-sorted and unique.</summary>
        public List<string> SeenLineIds { get; set; } = new List<string>();

        public static PresentationMemoryState Empty(DateTimeOffset? gameCreatedAtUtc) =>
            new PresentationMemoryState { GameCreatedAtUtc = gameCreatedAtUtc };

        public PresentationMemoryState Clone() => new PresentationMemoryState
        {
            SchemaVersion = SchemaVersion,
            GameCreatedAtUtc = GameCreatedAtUtc,
            ChangesShownThroughUtc = ChangesShownThroughUtc,
            SeenLineIds = new List<string>(SeenLineIds),
        };

        /// <summary>Unique, non-empty, length-bounded, ordinal-sorted and count-bounded ids.</summary>
        public static List<string> NormalizeLineIds(IEnumerable<string>? ids)
        {
            var set = new SortedSet<string>(StringComparer.Ordinal);
            if (ids != null)
                foreach (var id in ids)
                    if (!string.IsNullOrWhiteSpace(id) && id.Length <= MaxLineIdLength) set.Add(id);
            var list = new List<string>(Math.Min(set.Count, MaxSeenLineIds));
            foreach (var id in set) { if (list.Count == MaxSeenLineIds) break; list.Add(id); }
            return list;
        }
    }

    public enum PresentationMemoryLoadOutcome
    {
        Success = 0,
        /// <summary>No file yet: nothing shown so far.</summary>
        NotFound = 1,
        /// <summary>Damaged, empty or invalid: treated as nothing shown (no replay); never touches the save.</summary>
        Malformed = 2,
        /// <summary>Written by a newer schema: never interpreted; treated as nothing shown.</summary>
        FutureVersion = 3,
        /// <summary>Could not be read: treated as nothing shown for this session.</summary>
        IoFailure = 4,
    }

    public sealed class PresentationMemoryLoadResult
    {
        public PresentationMemoryLoadResult(PresentationMemoryLoadOutcome outcome, PresentationMemoryState? state = null, string? detail = null)
        { Outcome = outcome; State = state; Detail = detail; }

        public PresentationMemoryLoadOutcome Outcome { get; }
        public PresentationMemoryState? State { get; }
        /// <summary>Bounded technical detail for diagnostics only.</summary>
        public string? Detail { get; }
    }

    /// <summary>
    /// Durable local storage port for presentation memory (D-175). Implementations commit atomically,
    /// store no canonical state, and must also implement <see cref="Persistence.ILocalDataEraser"/>
    /// so "Delete all local data" removes the file. Save failures throw IOException; load problems
    /// are classified outcomes, so a broken file can never block boot or gameplay.
    /// </summary>
    public interface IPresentationMemoryStore
    {
        PresentationMemoryLoadResult Load();
        void Save(PresentationMemoryState state);
    }
}
