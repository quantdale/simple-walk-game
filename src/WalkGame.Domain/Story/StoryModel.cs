using System;
using System.Collections.Generic;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;

namespace WalkGame.Domain.Story
{
    /// <summary>
    /// One authored storyline chapter (D-076). Unlocks only from RESTORATION MILESTONES:
    /// completed projects and, for an epilogue, completed post-completion care cycles.
    /// Never raw steps, streaks or elapsed time. Immutable.
    /// </summary>
    public sealed class StoryChapterDefinition
    {
        /// <summary>Glance budget for a chapter body (characters).</summary>
        public const int MaxBodyLength = 420;

        public string Id { get; }
        public int Number { get; }
        public string TitleKey { get; }
        public string BodyKey { get; }
        /// <summary>Gentle "what brings this chapter" hint shown while locked. Never mentions time or streaks.</summary>
        public string HintKey { get; }
        public IReadOnlyList<string> RequiredProjectIds { get; }
        public long MinCareCycles { get; }

        public StoryChapterDefinition(string id, int number, string titleKey, string bodyKey, string hintKey,
                                      IEnumerable<string>? requiredProjectIds = null, long minCareCycles = 0)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Chapter requires an id.", nameof(id));
            if (number < 0) throw new ArgumentOutOfRangeException(nameof(number));
            if (string.IsNullOrWhiteSpace(titleKey)) throw new ArgumentException("Chapter '" + id + "' requires a title.", nameof(titleKey));
            if (string.IsNullOrWhiteSpace(bodyKey)) throw new ArgumentException("Chapter '" + id + "' requires a body.", nameof(bodyKey));
            if (minCareCycles < 0) throw new ArgumentOutOfRangeException(nameof(minCareCycles));
            Id = id; Number = number; TitleKey = titleKey; BodyKey = bodyKey; HintKey = hintKey ?? string.Empty;
            RequiredProjectIds = new List<string>(requiredProjectIds ?? Array.Empty<string>()).AsReadOnly();
            MinCareCycles = minCareCycles;
        }
    }

    /// <summary>Evaluated state of one chapter.</summary>
    public readonly struct ChapterProgress
    {
        public ChapterProgress(StoryChapterDefinition chapter, bool unlocked, DateTimeOffset? unlockedAtUtc)
        { Chapter = chapter; Unlocked = unlocked; UnlockedAtUtc = unlockedAtUtc; }
        public StoryChapterDefinition Chapter { get; }
        public bool Unlocked { get; }
        /// <summary>Latest milestone time in the chapter's chain; null when not unlocked or not knowable (care-gated).</summary>
        public DateTimeOffset? UnlockedAtUtc { get; }
    }

    /// <summary>
    /// Pure derivation of storyline progress from canonical state (D-076). No clock, no writes.
    /// Cumulative: chapter n unlocks only when its own milestones hold AND chapter n-1 is unlocked.
    /// Monotonic because project completion and care cycles never regress.
    /// </summary>
    public static class StoryProgress
    {
        public static IReadOnlyList<ChapterProgress> Evaluate(RegionDefinition content, RegionState region, DateTimeOffset gameCreatedAtUtc)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (region == null) throw new ArgumentNullException(nameof(region));
            var result = new List<ChapterProgress>(content.Chapters.Count);
            bool previousUnlocked = true;
            DateTimeOffset? previousAt = gameCreatedAtUtc;
            foreach (var chapter in content.Chapters)
            {
                bool own = previousUnlocked;
                DateTimeOffset? at = previousAt;
                if (own && chapter.Number == 0 && chapter.RequiredProjectIds.Count == 0 && chapter.MinCareCycles == 0)
                    at = gameCreatedAtUtc;
                foreach (var id in chapter.RequiredProjectIds)
                {
                    if (!own) break;
                    var p = region.FindProject(id);
                    if (p == null || p.Status != ProjectStatus.Completed) { own = false; break; }
                    if (p.CompletedAtUtc == null) at = null;
                    else if (at != null && p.CompletedAtUtc.Value > at.Value) at = p.CompletedAtUtc.Value;
                }
                if (own && chapter.MinCareCycles > 0)
                {
                    if (region.PostCompletionCyclesCompleted < chapter.MinCareCycles) own = false;
                    else at = null; // cycle completion times are not stored; surfaces say "recently"
                }
                result.Add(new ChapterProgress(chapter, own, own ? at : null));
                previousUnlocked = own;
                previousAt = own ? at : null;
            }
            return result;
        }

        public static int UnlockedCount(IReadOnlyList<ChapterProgress> progress)
        {
            int n = 0;
            foreach (var p in progress) if (p.Unlocked) n++;
            return n;
        }
    }
}
