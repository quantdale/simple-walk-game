namespace WalkGame.Application.ReadModels
{
    /// <summary>
    /// The three answers a player needs for a glance, in bounded sentences (UX_DESIGN §3):
    /// what changed, what is being restored now, and whether anything must be done.
    ///
    /// Purpose-built and separate from <see cref="HomeReadModel"/> on purpose: Home stays the
    /// fact snapshot the attention tests pin, while this model is the derived sentence layer
    /// a shell can render directly. Reading one is always side-effect free and never advances,
    /// acknowledges, or credits anything.
    /// </summary>
    public sealed class GlanceReadModel
    {
        /// <summary>
        /// D-145: how far the active project is and how far the next visible change is
        /// ("Clear the old trailhead: 4% restored. About 28,800 more steps to finish it.").
        /// Null when nothing is funded yet. Display-time only; derived from canonical state.
        /// </summary>
        public string? PayoffSentence { get; }

        /// <summary>What changed: one lead sentence, never a list of every pending item.</summary>
        public string WhatChangedSentence { get; }

        /// <summary>
        /// How many further CURRENT changes are waiting beyond the lead, so the shell can
        /// offer detail without the player having to read everything.
        /// </summary>
        public int FurtherChangeCount { get; }

        /// <summary>What is being restored right now, named by display title rather than id.</summary>
        public string RestoringNowSentence { get; }

        /// <summary>
        /// Copied from <see cref="HomeReadModel.RequiresAttention"/> — never recomputed. The
        /// glance follows the shell's existing attention classification rather than inventing
        /// a second one.
        /// </summary>
        public bool RequiresAttention { get; }

        /// <summary>Whether the player must do anything, in one sentence.</summary>
        public string AttentionSentence { get; }

        /// <summary>At most one primary next action, or null when nothing needs attention.</summary>
        public string? PrimaryNextAction { get; }

        /// <summary>Player-safe sentence for the current activity connection status.</summary>
        public string ActivitySentence { get; }

        /// <summary>
        /// Sentence for the current onboarding step, or null once onboarding is complete.
        /// </summary>
        public string? OnboardingSentence { get; }

        public GlanceReadModel(
            string whatChangedSentence,
            int furtherChangeCount,
            string restoringNowSentence,
            bool requiresAttention,
            string attentionSentence,
            string? primaryNextAction,
            string activitySentence,
            string? onboardingSentence,
            string? payoffSentence = null)
        {
            PayoffSentence = payoffSentence;
            WhatChangedSentence = whatChangedSentence;
            FurtherChangeCount = furtherChangeCount;
            RestoringNowSentence = restoringNowSentence;
            RequiresAttention = requiresAttention;
            AttentionSentence = attentionSentence;
            PrimaryNextAction = primaryNextAction;
            ActivitySentence = activitySentence;
            OnboardingSentence = onboardingSentence;
        }

        /// <summary>
        /// The bounded extra sentence for further current changes, or null when the lead was
        /// the only current change. Presented as an offer of detail, not a list.
        /// </summary>
        public string? FurtherChangesSentence =>
            FurtherChangeCount > 0 ? FurtherChangeCount + " more changes are waiting." : null;
    }
}
