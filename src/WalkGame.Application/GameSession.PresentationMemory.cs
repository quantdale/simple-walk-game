using System;
using System.Collections.Generic;
using System.IO;
using WalkGame.Application.Presentation;

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
            if (_state == null || m == null || m.GameCreatedAtUtc != created) return PresentationMemoryState.Empty(created);
            return m.Clone();
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
            var next = new PresentationMemoryState
            {
                GameCreatedAtUtc = _state.CreatedAtUtc,
                ChangesShownThroughUtc = changesShownThroughUtc,
                SeenLineIds = PresentationMemoryState.NormalizeLineIds(seenLineIds),
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
