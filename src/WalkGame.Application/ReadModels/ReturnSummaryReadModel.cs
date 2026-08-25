using System;
using System.Collections.Generic;
using WalkGame.Domain.Summaries;

namespace WalkGame.Application.ReadModels
{
    /// <summary>
    /// Immutable snapshot of the durable pending return summary for presentation.
    /// Bounded, priority-ordered, deterministic; safe to render in a 5–15 second glance.
    /// </summary>
    public sealed class ReturnSummaryReadModel
    {
        public IReadOnlyList<ItemRow> Items { get; }

        /// <summary>Single primary next action, or null when nothing needs attention.</summary>
        public string? PrimaryNextAction { get; }

        public DateTimeOffset GeneratedAtUtc { get; }

        public bool HasMeaningfulChange => Items.Count > 0;

        public ReturnSummaryReadModel(IReadOnlyList<ItemRow> items, string? primaryNextAction, DateTimeOffset generatedAtUtc)
        {
            Items = items;
            PrimaryNextAction = primaryNextAction;
            GeneratedAtUtc = generatedAtUtc;
        }

        public static ReturnSummaryReadModel FromState(PendingReturnSummaryState state)
        {
            var rows = new List<ItemRow>(state.Items.Count);
            foreach (var item in state.Items)
                rows.Add(new ItemRow(item.Kind, item.Text));
            return new ReturnSummaryReadModel(rows, state.PrimaryNextAction, state.GeneratedAtUtc);
        }

        public sealed class ItemRow
        {
            public SummaryItemKind Kind { get; }
            public string Text { get; }

            public ItemRow(SummaryItemKind kind, string text)
            {
                Kind = kind;
                Text = text;
            }
        }
    }
}
