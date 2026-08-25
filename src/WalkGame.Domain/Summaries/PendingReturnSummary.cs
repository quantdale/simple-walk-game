using System;
using System.Collections.Generic;

namespace WalkGame.Domain.Summaries
{
    /// <summary>
    /// Canonical priority classes for return-summary items (GAME_SYSTEMS §11, UX_DESIGN §4):
    /// transformation first, then actionable decisions, then production/notice, then
    /// concise aggregates. Ordering is deterministic and enforced by the composer.
    /// </summary>
    public enum SummaryItemKind
    {
        Transformation = 0,
        ActionableDecision = 1,
        Production = 2,
        Notice = 3,
        Aggregate = 4,
    }

    /// <summary>One bounded, presentation-ready summary line derived from committed events.</summary>
    public sealed class PendingSummaryItemState
    {
        public SummaryItemKind Kind { get; set; }

        public string Text { get; set; } = string.Empty;
    }

    /// <summary>
    /// Durable re-entry contract: the summary of already-committed progress that survives
    /// a crash between committing and presenting. Progress never depends on this being
    /// read; acknowledging it is an idempotent presentation convenience that may not alter
    /// earned progression. Absent state means "nothing pending".
    /// </summary>
    public sealed class PendingReturnSummaryState
    {
        /// <summary>Hard bound keeping the summary inside the 5–15 second glance budget.</summary>
        public const int MaxItems = 12;

        public List<PendingSummaryItemState> Items { get; } = new List<PendingSummaryItemState>();

        /// <summary>Single primary next action for the player, or null when nothing needs attention.</summary>
        public string? PrimaryNextAction { get; set; }

        public DateTimeOffset GeneratedAtUtc { get; set; }
    }
}
