using System;
using System.Collections.Generic;
using WalkGame.Application.ReadModels;
using WalkGame.Domain.Regions;

namespace WalkGame.Application.Summaries
{
    /// <summary>
    /// D-163 (KI-026): display-only pruning of return-summary lines that say nothing new on the
    /// summary screen. Stored items are never changed; the shell simply does not render these:
    /// <list type="bullet">
    /// <item>a landmark-stage line ("Ridge Observatory is restored.") when a CURRENT completion line
    /// for the project that brought that stage is shown, because the D-100 follow-up line under it
    /// already says "Ridge Observatory is now restored.";</item>
    /// <item>a "… is ready to queue." line for a project that is already restored.</item>
    /// </list>
    /// </summary>
    public static class SummaryDisplayFilter
    {
        public readonly struct Item
        {
            public Item(string text, bool isCurrent) { Text = text ?? string.Empty; IsCurrent = isCurrent; }
            public string Text { get; }
            public bool IsCurrent { get; }
        }

        private const string ReadySuffix = " is ready to queue.";

        /// <summary>Indexes of items the summary screen need not render.</summary>
        public static HashSet<int> Redundant(RegionDefinition content, IReadOnlyList<Item> items, ICollection<string>? completedProjectIds)
        {
            var hidden = new HashSet<int>();
            if (content == null || items == null) return hidden;

            // Landmark stage lines repeated by a shown completion line's D-100 detail.
            var repeated = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (!item.IsCurrent || !item.Text.EndsWith(ProjectCompletionMoments.CompletionSuffix, StringComparison.Ordinal)) continue;
                string title = item.Text.Substring(0, item.Text.Length - ProjectCompletionMoments.CompletionSuffix.Length);
                foreach (var p in content.Projects)
                {
                    if (p.TitleKey != title) continue;
                    foreach (var lm in content.Landmarks)
                        foreach (var stage in lm.Stages)
                            if (stage.UnlockedByProjectId == p.Id.Value)
                                repeated.Add(GlancePhrases.FormatLandmark(lm.TitleKey, stage.Stage));
                }
            }

            for (int i = 0; i < items.Count; i++)
            {
                string t = items[i].Text;
                if (repeated.Contains(t)) { hidden.Add(i); continue; }
                if (completedProjectIds != null && t.EndsWith(ReadySuffix, StringComparison.Ordinal))
                {
                    string title = t.Substring(0, t.Length - ReadySuffix.Length);
                    foreach (var p in content.Projects)
                        if (p.TitleKey == title && completedProjectIds.Contains(p.Id.Value)) { hidden.Add(i); break; }
                }
            }
            return hidden;
        }
    }
}
