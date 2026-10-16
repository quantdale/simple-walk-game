using System;
using System.Collections.Generic;

namespace WalkGame.Application.ReadModels
{
    /// <summary>
    /// D-118: Nell's letters, derived from completed projects. Unfound letters expose no text
    /// (the shell cannot spoil them); found letters never become unfound (completion is monotonic).
    /// </summary>
    public sealed class LettersReadModel
    {
        public LettersReadModel(IReadOnlyList<LetterRow> letters)
        {
            Letters = letters ?? throw new ArgumentNullException(nameof(letters));
            TotalLetters = letters.Count;
            int found = 0;
            foreach (var l in letters) if (l.Found) found++;
            FoundCount = found;
        }

        public IReadOnlyList<LetterRow> Letters { get; }
        public int TotalLetters { get; }
        public int FoundCount { get; }

        public sealed class LetterRow
        {
            public LetterRow(string letterId, int order, string place, string? body, bool found, DateTimeOffset? foundAtUtc)
            { LetterId = letterId; Order = order; Place = place; Body = body; Found = found; FoundAtUtc = foundAtUtc; }
            public string LetterId { get; }
            public int Order { get; }
            public string Place { get; }
            /// <summary>Null until found.</summary>
            public string? Body { get; }
            public bool Found { get; }
            public DateTimeOffset? FoundAtUtc { get; }
        }
    }
}
