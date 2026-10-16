using System;
using System.Collections.Generic;

namespace WalkGame.Application.ReadModels
{
    /// <summary>
    /// D-129: the Region 1 epilogue. <see cref="Available"/> is false until the region is
    /// complete, and then every list is empty (no spoilers, no hints). Entries already shown
    /// stay shown; nothing expires.
    /// </summary>
    public sealed class EpilogueReadModel
    {
        public static readonly EpilogueReadModel None = new EpilogueReadModel(false, null, Array.Empty<string>(), Array.Empty<string>(), null);

        public EpilogueReadModel(bool available, string? journalEntry, IReadOnlyList<string> closingLetters, IReadOnlyList<string> valleyNotes, string? currentNote)
        {
            Available = available;
            JournalEntry = journalEntry;
            ClosingLetters = closingLetters ?? throw new ArgumentNullException(nameof(closingLetters));
            ValleyNotes = valleyNotes ?? throw new ArgumentNullException(nameof(valleyNotes));
            CurrentNote = currentNote;
        }

        public bool Available { get; }
        public string? JournalEntry { get; }
        /// <summary>Closing letters from Nell already arrived, in order.</summary>
        public IReadOnlyList<string> ClosingLetters { get; }
        /// <summary>Valley notes already shown, oldest first.</summary>
        public IReadOnlyList<string> ValleyNotes { get; }
        /// <summary>The note for "in the valley now" (cycles); null before completion.</summary>
        public string? CurrentNote { get; }
    }
}
