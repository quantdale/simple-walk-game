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
    }
}
