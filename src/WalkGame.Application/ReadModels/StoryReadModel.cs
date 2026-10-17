using System;
using System.Collections.Generic;

namespace WalkGame.Application.ReadModels
{
    /// <summary>
    /// Presentation contract for the story journal (D-076). Chapters are derived from
    /// restoration state, so this is a pure projection: locked chapters expose only their
    /// title and a forward-looking hint (never the body), and unlocked ones never relock.
    /// </summary>
    public sealed class StoryReadModel
    {
        public IReadOnlyList<ChapterRow> Chapters { get; }
        public int TotalChapters { get; }
        public int UnlockedCount { get; }

        /// <summary>The first locked chapter's hint, or null once the story is fully read.</summary>
        public string? NextHintKey { get; }

        public StoryReadModel(IReadOnlyList<ChapterRow> chapters, int unlockedCount, string? nextHintKey)
        {
            Chapters = chapters ?? throw new ArgumentNullException(nameof(chapters));
            TotalChapters = chapters.Count;
            UnlockedCount = unlockedCount;
            NextHintKey = nextHintKey;
        }

        public sealed class ChapterRow
        {
            public string ChapterId { get; }
            public int Number { get; }
            public string TitleKey { get; }

            /// <summary>Null while locked so the shell cannot leak future narrative.</summary>
            public string? BodyKey { get; }

            public string HintKey { get; }
            public bool Unlocked { get; }
            public DateTimeOffset? UnlockedAtUtc { get; }

            /// <summary>D-155: the authored restoration milestones (project ids) this chapter needs,
            /// straight from <c>StoryChapterDefinition.RequiredProjectIds</c>. Ids only, never copy.</summary>
            public IReadOnlyList<string> RequiredProjectIds { get; }

            /// <summary>D-155: completed care cycles the chapter needs (0 for restoration chapters).</summary>
            public long MinCareCycles { get; }

            public ChapterRow(string chapterId, int number, string titleKey, string? bodyKey, string hintKey,
                bool unlocked, DateTimeOffset? unlockedAtUtc,
                IReadOnlyList<string>? requiredProjectIds = null, long minCareCycles = 0)
            {
                RequiredProjectIds = requiredProjectIds ?? Array.Empty<string>();
                MinCareCycles = minCareCycles;
                ChapterId = chapterId;
                Number = number;
                TitleKey = titleKey;
                BodyKey = bodyKey;
                HintKey = hintKey;
                Unlocked = unlocked;
                UnlockedAtUtc = unlockedAtUtc;
            }
        }
    }
}
