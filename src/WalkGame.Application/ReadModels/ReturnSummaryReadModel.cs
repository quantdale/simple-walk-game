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
        /// <summary>
        /// How old an item must be before it counts as HISTORICAL rather than current
        /// (D-055). Chosen at 7 days: long enough that a player who opens the app most
        /// days still sees "something is new", and short enough that a summary left
        /// unacknowledged for a week stops shouting for attention. The player who engages
        /// LEAST is the one this threshold exists to protect.
        /// </summary>
        public static readonly TimeSpan CurrencyThreshold = TimeSpan.FromDays(7);

        public IReadOnlyList<ItemRow> Items { get; }

        /// <summary>Single primary next action, or null when nothing needs attention.</summary>
        public string? PrimaryNextAction { get; }

        public DateTimeOffset GeneratedAtUtc { get; }

        /// <summary>D-138: <see cref="PrimaryNextAction"/> for display (localized; English when no overlay).</summary>
        public string? PrimaryNextActionDisplay { get; private set; }

        /// <summary>True when at least one item is still CURRENT (see <see cref="ItemRow.IsCurrent"/>).</summary>
        public bool HasCurrentItems
        {
            get
            {
                foreach (var item in Items)
                    if (item.IsCurrent)
                        return true;
                return false;
            }
        }

        public bool HasMeaningfulChange => Items.Count > 0;

        public ReturnSummaryReadModel(IReadOnlyList<ItemRow> items, string? primaryNextAction, DateTimeOffset generatedAtUtc)
        {
            Items = items;
            PrimaryNextAction = primaryNextAction;
            PrimaryNextActionDisplay = primaryNextAction;
            GeneratedAtUtc = generatedAtUtc;
        }

        /// <summary>
        /// Projects durable summary state into a presentation snapshot.
        ///
        /// <paramref name="nowUtc"/> is the instant the snapshot is taken, and it — NOT the
        /// summary's own <c>GeneratedAtUtc</c> — is the reference for item currency. That
        /// distinction is the whole point: a summary that has not been recomposed keeps its
        /// original generation time, so measuring against it would report a month-old item
        /// as brand new forever and the attention signal could never expire.
        /// </summary>
        public static ReturnSummaryReadModel FromState(
            PendingReturnSummaryState state, DateTimeOffset? nowUtc = null, Func<string?, string>? display = null)
        {
            var now = nowUtc ?? state.GeneratedAtUtc;
            var rows = new List<ItemRow>(state.Items.Count);
            foreach (var item in state.Items)
            {
                // A pre-D-055 save carries no currency; such an item is treated as current
                // because it genuinely is the most recent thing that happened.
                var committedAt = item.FirstCommittedAtUtc == default
                    ? state.GeneratedAtUtc
                    : item.FirstCommittedAtUtc;
                rows.Add(new ItemRow(item.Kind, item.Text, committedAt,
                    now - committedAt <= CurrencyThreshold, display == null ? null : display(item.Text)));
            }
            return new ReturnSummaryReadModel(rows, state.PrimaryNextAction, state.GeneratedAtUtc)
            {
                PrimaryNextActionDisplay = display == null || state.PrimaryNextAction == null ? state.PrimaryNextAction : display(state.PrimaryNextAction),
            };
        }

        public sealed class ItemRow
        {
            public SummaryItemKind Kind { get; }
            /// <summary>The stored English line (canonical; shells parse this, never DisplayText).</summary>
            public string Text { get; }

            /// <summary>D-138: the line for display through the content overlay (equals Text in English).</summary>
            public string DisplayText { get; }

            /// <summary>When this item was first committed to the durable summary.</summary>
            public DateTimeOffset FirstCommittedAtUtc { get; }

            /// <summary>False once the item is older than <see cref="CurrencyThreshold"/>.
            /// A shell can render these dimmed/collapsed; the Home surface stops claiming
            /// attention for a summary whose items are all historical.</summary>
            public bool IsCurrent { get; }

            public ItemRow(SummaryItemKind kind, string text, DateTimeOffset firstCommittedAtUtc, bool isCurrent, string? displayText = null)
            {
                Kind = kind;
                Text = text;
                DisplayText = string.IsNullOrEmpty(displayText) ? text : displayText!;
                FirstCommittedAtUtc = firstCommittedAtUtc;
                IsCurrent = isCurrent;
            }
        }
    }
}
