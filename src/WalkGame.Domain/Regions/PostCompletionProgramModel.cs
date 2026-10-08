using System;

namespace WalkGame.Domain.Regions
{
    /// <summary>
    /// Authored post-completion content: the declared, repeatable use for progress earned
    /// AFTER a region's closure milestone has been reached (D-061).
    ///
    /// Before this content kind existed, region completion was a permanent dead end: every
    /// project was complete, the queue was empty, and Home kept reporting
    /// <c>QueueEmptyWithBankedVitality</c> — an instruction to queue restoration work that
    /// no longer existed — while the banked balance grew without bound. A program here is
    /// what makes continued activity mean something after the ending instead of nagging.
    ///
    /// A program is deliberately NOT a <see cref="ProjectDefinition"/>: it must never appear
    /// in the restoration roster, never be queued, never gate anything, and never make the
    /// region look unfinished ("19 of 20 complete"). It is authored content — same shape,
    /// same validation discipline — but reachable only from the completed region, which is
    /// why it is a separate collection rather than another project.
    /// </summary>
    public sealed class PostCompletionProgramDefinition
    {
        /// <summary>Stable content identifier. Convention: <c>care.*</c>.</summary>
        public string Id { get; }

        public string TitleKey { get; }

        public string DescriptionKey { get; }

        /// <summary>
        /// Vitality consumed by one cycle. Cycles repeat indefinitely, so this is also the
        /// bound on how long banked progress can sit unspent after completion.
        /// </summary>
        public long VitalityCostPerCycle { get; }

        /// <summary>
        /// The closure milestone that enables this program. It MUST be the region's own
        /// <see cref="RegionDefinition.CompletionMilestoneProjectId"/>: a program enabled by
        /// anything else would consume progress before the region is actually finished.
        /// </summary>
        public string EnabledByMilestoneProjectId { get; }

        public PostCompletionProgramDefinition(
            string id,
            string titleKey,
            long vitalityCostPerCycle,
            string enabledByMilestoneProjectId,
            string? descriptionKey = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Post-completion program requires an ID.", nameof(id));
            if (string.IsNullOrWhiteSpace(titleKey))
                throw new ArgumentException("Post-completion program requires a title.", nameof(titleKey));
            if (vitalityCostPerCycle <= 0L)
                throw new ArgumentException("Post-completion program cycle cost must be positive.", nameof(vitalityCostPerCycle));
            if (string.IsNullOrWhiteSpace(enabledByMilestoneProjectId))
                throw new ArgumentException(
                    "Post-completion program must name the closure milestone that enables it.", nameof(enabledByMilestoneProjectId));

            Id = id;
            TitleKey = titleKey;
            DescriptionKey = descriptionKey ?? string.Empty;
            VitalityCostPerCycle = vitalityCostPerCycle;
            EnabledByMilestoneProjectId = enabledByMilestoneProjectId;
        }
    }
}
