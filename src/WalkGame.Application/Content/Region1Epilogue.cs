using System;
using System.Collections.Generic;

namespace WalkGame.Application.Content
{
    /// <summary>
    /// D-129: the Region 1 epilogue. After Millbrook's closure milestone, two closing letters
    /// from Nell Hartley, one final journal entry and eight quiet valley notes appear. It is a
    /// pure projection of <c>RegionCompletedAtUtc</c> and the trusted clock: no save field, no
    /// content-fingerprint change, no reward, count, streak or timer, and nothing can be missed
    /// (every note stays readable once shown). No Region 2 content or hook (D-011).
    /// </summary>
    public static class Region1Epilogue
    {
        public sealed class Entry
        {
            public Entry(string id, int dayAfterCompletion, string body) { Id = id; DayAfterCompletion = dayAfterCompletion; Body = body; }
            public string Id { get; }
            /// <summary>Whole days after the region was completed when this entry appears (0 = at once).</summary>
            public int DayAfterCompletion { get; }
            public string Body { get; }
        }

        public const int MaxBodyLength = 320;
        /// <summary>Days between valley notes. Notes then keep cycling, one "in the valley now" at a time.</summary>
        public const int NoteIntervalDays = 30;

        public static readonly Entry JournalEntry = new Entry("epilogue.journal", 0,
            "Final entry. The river runs where it wants to, the mill turns, and the cranes have the marsh again. Millbrook is not finished, because valleys never are, but it is whole. We will keep walking it, gently, for as long as we like.");

        public static readonly IReadOnlyList<Entry> ClosingLetters = new List<Entry>
        {
            new Entry("epilogue.letter.home", 0,
                "Tom, someone has done it. I heard the mill from the far road and had to sit down on the verge. There is nothing left you need to do for the valley now. It will keep itself, and it will keep a light on for you."),
            new Entry("epilogue.letter.after", 7,
                "I walked the whole loop today, trailhead to the survey stone, slowly. Nobody was counting. That is the best part of a mended place: you can simply be in it. Come whenever you like. I will be here."),
        }.AsReadOnly();

        public static readonly IReadOnlyList<Entry> ValleyNotes = new List<Entry>
        {
            new Entry("epilogue.note.1", 0, "Mist sits on the wetland this morning, and the first cranes are standing in it like they never left."),
            new Entry("epilogue.note.2", 1 * NoteIntervalDays, "Wild garlic along the trailhead path. Someone has left a jar by the millstone for anyone who wants some."),
            new Entry("epilogue.note.3", 2 * NoteIntervalDays, "The river is high and loud after the rain, and the intake gates are simply letting it through."),
            new Entry("epilogue.note.4", 3 * NoteIntervalDays, "Long light in the evenings. Children are racing leaf boats from the workshop to the bend."),
            new Entry("epilogue.note.5", 4 * NoteIntervalDays, "The canopy walkway smells of warm resin. Bees in the old oak, and a faded ribbon still tied on."),
            new Entry("epilogue.note.6", 5 * NoteIntervalDays, "Apples on the settlement trees. The workshop door is propped open and the forge is ticking as it cools."),
            new Entry("epilogue.note.7", 6 * NoteIntervalDays, "Fog in the hollows and woodsmoke over the roofs. The geese went south over the survey stone today."),
            new Entry("epilogue.note.8", 7 * NoteIntervalDays, "Frost on the footbridge, a clear sky, and the river quieter under thin ice. The valley is resting, not gone."),
        }.AsReadOnly();

        /// <summary>Whole days since completion, never negative (a backward clock shows the day-0 epilogue, it never hides it).</summary>
        public static int DaysSince(DateTimeOffset completedAtUtc, DateTimeOffset nowUtc)
        {
            var d = (nowUtc - completedAtUtc).TotalDays;
            if (double.IsNaN(d) || d <= 0) return 0;
            return d >= int.MaxValue ? int.MaxValue : (int)Math.Floor(d);
        }

        /// <summary>The note shown as "in the valley now": it cycles through all eight, forever, one per interval.</summary>
        public static int CurrentNoteIndex(int daysSince) => (Math.Max(0, daysSince) / NoteIntervalDays) % ValleyNotes.Count;
    }
}
