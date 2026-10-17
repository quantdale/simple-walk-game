using System;
using System.Collections.Generic;
using WalkGame.Application;

namespace WalkGame.Shell.Presentation
{
    /// <summary>
    /// D-175 (KI-027 c): <see cref="IPresentationMemory"/> that survives a cold restart. It reads and
    /// writes through <see cref="GameSession.GetPresentationMemory"/> / <see cref="GameSession.SavePresentationMemory"/>,
    /// which keep it in its own small file outside the canonical save (erased by "Delete all local data",
    /// not in the privacy export). It always follows the loaded game: memory of another game is ignored.
    /// A damaged or missing file reads as "nothing shown yet", so the next open is a baseline with no
    /// replay. Writes happen only when something actually changes, and a failed write never throws.
    /// </summary>
    public sealed class SessionPresentationMemory : IPresentationMemory
    {
        private readonly GameSession _session;

        public SessionPresentationMemory(GameSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        /// <summary>Outcome of the last write (diagnostics and tests); null before any write.</summary>
        public PresentationMemorySaveStatus? LastSaveStatus { get; private set; }

        public IReadOnlyCollection<string> SeenLineIds => _session.GetPresentationMemory().SeenLineIds;

        public void MarkSeen(string lineId)
        {
            if (string.IsNullOrEmpty(lineId)) return;
            var m = _session.GetPresentationMemory();
            if (m.SeenLineIds.Contains(lineId)) return;
            var seen = new List<string>(m.SeenLineIds) { lineId };
            LastSaveStatus = _session.SavePresentationMemory(m.ChangesShownThroughUtc, seen);
        }

        public DateTimeOffset? ChangesShownThroughUtc
        {
            get => _session.GetPresentationMemory().ChangesShownThroughUtc;
            set
            {
                var m = _session.GetPresentationMemory();
                if (m.ChangesShownThroughUtc == value) return;
                LastSaveStatus = _session.SavePresentationMemory(value, m.SeenLineIds);
            }
        }
    }
}
