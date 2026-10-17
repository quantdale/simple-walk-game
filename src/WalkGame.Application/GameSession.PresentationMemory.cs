using System;
using System.Collections.Generic;
using System.IO;
using WalkGame.Application.Presentation;
using WalkGame.Application.ReadModels;
using WalkGame.Domain.Projects;

namespace WalkGame.Application
{
    public enum PresentationMemorySaveStatus
    {
        /// <summary>Written durably to the presentation-memory file.</summary>
        Saved = 0,
        /// <summary>No store is wired: kept for this session only.</summary>
        SessionOnly = 1,
        /// <summary>The file could not be written: kept for this session only; nothing else is affected.</summary>
        Failed = 2,
        /// <summary>No game is loaded: nothing to remember.</summary>
        NoGame = 3,
    }

    // D-175 (KI-027 c): presentation memory, outside the canonical save.
    public sealed partial class GameSession
    {
        private readonly IPresentationMemoryStore? _presentationMemoryStore;
        private PresentationMemoryState? _presentationMemory;
        private bool _presentationMemoryLoaded;
        private PresentationMemoryLoadOutcome _presentationMemoryLoadOutcome = PresentationMemoryLoadOutcome.NotFound;

        /// <summary>How the presentation-memory file loaded (diagnostics and tests). NotFound before the first read.</summary>
        public PresentationMemoryLoadOutcome PresentationMemoryLoadOutcome
        {
            get { EnsurePresentationMemoryLoaded(); return _presentationMemoryLoadOutcome; }
        }

        /// <summary>
        /// D-175: what the world presentation has already shown for the loaded game (a copy). Empty
        /// (nothing shown: the next open is a baseline and replays nothing) when no game is loaded, when
        /// the file is missing, damaged, unreadable or from a newer version, or when it belongs to a
        /// different game. Reading never touches canonical state or the save.
        /// </summary>
        public PresentationMemoryState GetPresentationMemory()
        {
            var created = _state?.CreatedAtUtc;
            EnsurePresentationMemoryLoaded();
            var m = _presentationMemory;
            if (_state == null || m == null || m.GameCreatedAtUtc != created)
            {
                var empty = PresentationMemoryState.Empty(created);
                empty.ShownCompletionIds = new List<string>(); // D-180: nothing shown for this game
                return empty;
            }
            // D-180: a v1 file did not record shown completions. Derive them ONCE from its timestamp mark
            // (completions at or before it count as shown) and keep the result for this session, so a
            // completion made later with an earlier (corrected-back) stamp is still unshown.
            if (m.ShownCompletionIds == null) m.ShownCompletionIds = DeriveShownCompletions(m.ChangesShownThroughUtc);
            return m.Clone();
        }

        /// <summary>
        /// D-180 (KI-028): the visual changes not yet shown for the loaded game, in stream order: every
        /// completion not yet recorded as shown, whatever its timestamp. Empty before the world was ever
        /// opened (that open is the baseline) and when no game is loaded. Reading changes nothing.
        /// </summary>
        public IReadOnlyList<VisualChangeEvent> GetUnshownVisualChanges()
        {
            if (_state == null) return Array.Empty<VisualChangeEvent>();
            var m = GetPresentationMemory();
            return PresentationReplay.Unshown(GetVisualChangesSince(DateTimeOffset.MinValue), m.ShownCompletionIds, m.ChangesShownThroughUtc);
        }

        /// <summary>
        /// D-180: records <paramref name="shownEvents"/> (their completions, and the latest instant) as shown,
        /// so they are never replayed again, including after a restart. Seen lines are kept.
        /// </summary>
        public PresentationMemorySaveStatus MarkVisualChangesShown(IEnumerable<VisualChangeEvent> shownEvents)
        {
            if (_state == null) return PresentationMemorySaveStatus.NoGame;
            var list = new List<VisualChangeEvent>(shownEvents ?? Array.Empty<VisualChangeEvent>());
            var m = GetPresentationMemory();
            var ids = new List<string>(m.ShownCompletionIds ?? new List<string>());
            ids.AddRange(PresentationReplay.CompletionIdsOf(list));
            var through = PresentationReplay.Latest(list, m.ChangesShownThroughUtc ?? DateTimeOffset.MinValue);
            return SavePresentationMemory(through, m.SeenLineIds, ids);
        }

        /// <summary>D-180: the whole current world counts as shown (the first open's baseline, or after in-session play).</summary>
        public PresentationMemorySaveStatus MarkAllVisualChangesShown()
        {
            if (_state == null) return PresentationMemorySaveStatus.NoGame;
            var all = GetVisualChangesSince(DateTimeOffset.MinValue);
            var m = GetPresentationMemory();
            var ids = new List<string>(m.ShownCompletionIds ?? new List<string>());
            ids.AddRange(PresentationReplay.CompletionIdsOf(all));
            ids.AddRange(CompletedIds());
            var through = PresentationReplay.Latest(all, m.ChangesShownThroughUtc ?? DateTimeOffset.MinValue);
            return SavePresentationMemory(through, m.SeenLineIds, ids);
        }

        /// <summary>
        /// D-180: as <see cref="SavePresentationMemory(DateTimeOffset?, IEnumerable{string})"/>, and also records
        /// which completions were shown. Ids of completions the loaded game does not have are pruned, so the
        /// list is bounded by the content (and by <see cref="PresentationMemoryState.MaxShownCompletionIds"/>).
        /// </summary>
        public PresentationMemorySaveStatus SavePresentationMemory(DateTimeOffset? changesShownThroughUtc,
            IEnumerable<string>? seenLineIds, IEnumerable<string>? shownCompletionIds)
        {
            if (_state == null) return PresentationMemorySaveStatus.NoGame;
            List<string>? pruned = null;
            if (shownCompletionIds != null)
            {
                var completed = new HashSet<string>(CompletedIds(), StringComparer.Ordinal);
                pruned = new List<string>();
                foreach (var id in shownCompletionIds) if (id != null && completed.Contains(id)) pruned.Add(id);
            }
            return Persist(changesShownThroughUtc, seenLineIds, pruned);
        }

        private List<string> DeriveShownCompletions(DateTimeOffset? through)
        {
            var ids = new List<string>();
            if (_state == null || through == null) return ids;
            foreach (var p in _state.Region.Projects.Values)
                if (p.Status == ProjectStatus.Completed && p.CompletedAtUtc != null && p.CompletedAtUtc.Value <= through.Value)
                    ids.Add(p.ProjectId);
            if (_state.Region.IsCompleted && _state.Region.RegionCompletedAtUtc != null && _state.Region.RegionCompletedAtUtc.Value <= through.Value)
                ids.Add(VisualChangeEvent.RegionCompletionId);
            return PresentationMemoryState.NormalizeCompletionIds(ids)!;
        }

        private IEnumerable<string> CompletedIds()
        {
            if (_state == null) yield break;
            foreach (var p in _state.Region.Projects.Values)
                if (p.Status == ProjectStatus.Completed) yield return p.ProjectId;
            if (_state.Region.IsCompleted) yield return VisualChangeEvent.RegionCompletionId;
        }

        /// <summary>
        /// D-175: remembers what has been shown for the loaded game and writes it to its own small file
        /// (atomic). Never writes the save, never marks the game dirty, never changes canonical state. A
        /// write failure keeps the memory for this session and is reported, never thrown.
        /// </summary>
        public PresentationMemorySaveStatus SavePresentationMemory(DateTimeOffset? changesShownThroughUtc, IEnumerable<string>? seenLineIds)
        {
            if (_state == null) return PresentationMemorySaveStatus.NoGame;
            EnsurePresentationMemoryLoaded();
            // D-180: keep whatever shown completions are already recorded for this game (null stays null).
            var stored = _presentationMemory != null && _presentationMemory.GameCreatedAtUtc == _state.CreatedAtUtc
                ? _presentationMemory.ShownCompletionIds : null;
            return Persist(changesShownThroughUtc, seenLineIds, stored);
        }

        private PresentationMemorySaveStatus Persist(DateTimeOffset? changesShownThroughUtc, IEnumerable<string>? seenLineIds, IEnumerable<string>? shownCompletionIds)
        {
            if (_state == null) return PresentationMemorySaveStatus.NoGame;
            EnsurePresentationMemoryLoaded();
            var next = new PresentationMemoryState
            {
                GameCreatedAtUtc = _state.CreatedAtUtc,
                ChangesShownThroughUtc = changesShownThroughUtc,
                SeenLineIds = PresentationMemoryState.NormalizeLineIds(seenLineIds),
                ShownCompletionIds = PresentationMemoryState.NormalizeCompletionIds(shownCompletionIds),
            };
            _presentationMemory = next;
            if (_presentationMemoryStore == null) return PresentationMemorySaveStatus.SessionOnly;
            try
            {
                _presentationMemoryStore.Save(next.Clone());
                return PresentationMemorySaveStatus.Saved;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return PresentationMemorySaveStatus.Failed;
            }
        }

        private void EnsurePresentationMemoryLoaded()
        {
            if (_presentationMemoryLoaded) return;
            _presentationMemoryLoaded = true;
            if (_presentationMemoryStore == null) return;
            PresentationMemoryLoadResult result;
            try { result = _presentationMemoryStore.Load(); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                result = new PresentationMemoryLoadResult(PresentationMemoryLoadOutcome.IoFailure, detail: ex.GetType().Name);
            }
            _presentationMemoryLoadOutcome = result.Outcome;
            _presentationMemory = result.Outcome == PresentationMemoryLoadOutcome.Success ? result.State : null;
        }

        private void ResetPresentationMemoryAfterErase()
        {
            _presentationMemory = null;
            _presentationMemoryLoaded = true; // the file is gone: nothing to read back
            _presentationMemoryLoadOutcome = PresentationMemoryLoadOutcome.NotFound;
        }
    }
}
