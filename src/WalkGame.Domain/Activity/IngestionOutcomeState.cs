using System;
using System.Collections.Generic;

namespace WalkGame.Domain.Activity
{
    /// <summary>Durable last-batch outcome classification (persisted additively in GameState).</summary>
    public enum IngestionOutcomeKind
    {
        /// <summary>No batch has run yet on this profile.</summary>
        NeverRun = 0,

        Succeeded = 1,

        /// <summary>The adapter fetch threw before any record reached the trust pipeline.</summary>
        SourceFetchFailed = 2,
    }

    /// <summary>
    /// Bounded, privacy-safe aggregate of the most recent ingestion batch (M5-H1). This is
    /// diagnostic EVIDENCE about pipeline runs — never a second copy of ledger or world
    /// state. It is additive on save schema v2 (absent decodes to null = "never run"),
    /// updated inside the same atomic commit as the batch it describes, and permanently
    /// bounded to one row of counters.
    /// </summary>
    public sealed class IngestionOutcomeState
    {
        public IngestionOutcomeKind Outcome { get; set; } = IngestionOutcomeKind.Succeeded;

        public DateTimeOffset CompletedAtUtc { get; set; }

        public int TotalReceived { get; set; }

        public int Accepted { get; set; }

        public int Rejected { get; set; }

        public int DuplicatesIgnored { get; set; }

        public int CorrectionsApplied { get; set; }

        public int DeletionsApplied { get; set; }

        public long VitalityCredited { get; set; }

        public long UnappliedReversalVitality { get; set; }

        /// <summary>
        /// Stable error-category classification (e.g. exception TYPE name) for failed
        /// fetches. Deliberately never a raw exception message.
        /// </summary>
        public string? ErrorCategory { get; set; }

        /// <summary>
        /// Per-rejection-reason counts, keyed by the ORDINAL of
        /// <see cref="ActivityValidationStatus"/> (D-063). Persisting ordinals rather than
        /// member names keeps the durable shape stable under a rename and the payload small,
        /// at the cost of requiring a reader to map ordinals back through the enum — which is
        /// the right trade for evidence about a class of rejection, not about one record.
        ///
        /// NULL means "no breakdown recorded" (a save written before this field existed);
        /// EMPTY means "a batch ran and rejected nothing". Those are different facts — a
        /// player whose whole backlog was lost and a player whose source was merely quiet do
        /// not look alike — and collapsing them is exactly the gap that made
        /// <c>OutsideHorizon</c> indistinguishable from <c>ZeroQuantity</c> in support.
        ///
        /// Bounded by construction: one entry per eligibility reason, never per record.
        /// </summary>
        public Dictionary<int, int>? RejectionCountsByReason { get; set; }

        /// <summary>Deletion markers for records this pipeline never credited (ignored).</summary>
        public int DeletionsIgnored { get; set; }

        /// <summary>Redeliveries carrying an already-processed or older revision (ignored).</summary>
        public int StaleRevisionsIgnored { get; set; }

        /// <summary>Records whose requested quantity was above the bounded-quantity ceiling.</summary>
        public int QuantityClamped { get; set; }

        /// <summary>When a fetch was last ATTEMPTED, successful or not. Null = never attempted.</summary>
        public DateTimeOffset? LastFetchAttemptedUtc { get; set; }

        /// <summary>When a fetch last completed and its records were trusted. Null = never.</summary>
        public DateTimeOffset? LastFetchSucceededUtc { get; set; }
    }
}
