using System;
using System.Collections.Generic;
using System.Linq;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;

namespace WalkGame.Application.Summaries
{
    /// <summary>
    /// D-100: a calm, richer line for a project-completion moment in the return summary.
    /// Ties the finished project to the landmark stage it brings and to the storyline chapter
    /// it belongs to, using authored content only. Pure and read-only: no save, no content
    /// change, and stored summary text is never rewritten (the line is derived at render time).
    /// </summary>
    public static class ProjectCompletionMoments
    {
        /// <summary>Suffix of the composer's per-project completion line ("{Title} is complete.").</summary>
        public const string CompletionSuffix = " is complete.";

        /// <summary>
        /// Returns the follow-up line for a summary item, or null when the item is not a
        /// single-project completion or the project has no landmark or story link.
        /// </summary>
        /// <param name="unlockedChapterIds">Chapter ids already unlocked; used to say a chapter opened rather than is ahead.</param>
        public static string? Describe(RegionDefinition content, string itemText, ICollection<string>? unlockedChapterIds = null)
        {
            if (content == null || string.IsNullOrEmpty(itemText) || !itemText.EndsWith(CompletionSuffix, StringComparison.Ordinal))
                return null;
            string title = itemText.Substring(0, itemText.Length - CompletionSuffix.Length);
            ProjectDefinition? project = null;
            foreach (var p in content.Projects)
                if (p.TitleKey == title || p.Id.Value == title) { project = p; break; }
            if (project == null) return null;
            string id = project.Id.Value;

            var parts = new List<string>();
            var landmarks = new List<string>();
            foreach (var lm in content.Landmarks)
                foreach (var stage in lm.Stages)
                    if (stage.UnlockedByProjectId == id)
                        landmarks.Add(lm.TitleKey + " is now " + stage.Stage.ToString().ToLowerInvariant());
            if (landmarks.Count > 0)
                parts.Add(JoinList(landmarks) + ".");

            foreach (var ch in content.Chapters)
            {
                if (!ch.RequiredProjectIds.Contains(id)) continue;
                bool open = unlockedChapterIds != null && unlockedChapterIds.Contains(ch.Id);
                parts.Add(open
                    ? "It is part of the chapter \"" + ch.TitleKey + "\"."
                    : "The story moves toward \"" + ch.TitleKey + "\".");
                break;
            }
            return parts.Count == 0 ? null : string.Join(" ", parts);
        }

        /// <summary>D-162: "A", "A and B", "A, B, and C" (was "A, and B, and C").</summary>
        public static string JoinList(IReadOnlyList<string> items) => items.Count switch
        {
            0 => string.Empty,
            1 => items[0],
            2 => items[0] + " and " + items[1],
            _ => string.Join(", ", items.Take(items.Count - 1)) + ", and " + items[items.Count - 1],
        };
    }
}
