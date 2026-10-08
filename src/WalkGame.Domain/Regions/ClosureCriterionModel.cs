using System;
using System.Collections.Generic;

namespace WalkGame.Domain.Regions
{
    /// <summary>
    /// One authored restoration chain: a named group of projects that progress together.
    ///
    /// Chains are authored content rather than a hardcoded table because the pacing
    /// harnesses, the region surface and the closure criterion all need the same grouping,
    /// and a second copy of "which projects form a chain" is exactly the kind of
    /// duplicated domain knowledge that drifts. The CLI used to carry that table; it now
    /// reads it from here.
    /// </summary>
    public sealed class RestorationChainDefinition
    {
        public string Id { get; }

        public string TitleKey { get; }

        /// <summary>Project ids that form the chain, in authored escalation order.</summary>
        public IReadOnlyList<string> ProjectIds { get; }

        public RestorationChainDefinition(string id, string titleKey, IEnumerable<string> projectIds)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Restoration chain requires an ID.", nameof(id));
            if (string.IsNullOrWhiteSpace(titleKey))
                throw new ArgumentException("Restoration chain requires a title.", nameof(titleKey));

            var members = new List<string>();
            if (projectIds != null)
            {
                foreach (var projectId in projectIds)
                {
                    if (string.IsNullOrWhiteSpace(projectId))
                        throw new ArgumentException(
                            "Restoration chain '" + id + "' lists an empty project ID.", nameof(projectIds));
                    members.Add(projectId);
                }
            }
            if (members.Count == 0)
                throw new ArgumentException("Restoration chain '" + id + "' has no projects.", nameof(projectIds));

            Id = id;
            TitleKey = titleKey;
            ProjectIds = members.AsReadOnly();
        }
    }

    /// <summary>
    /// The authored closure criterion for a region (D-062): what "finished" MEANS.
    ///
    /// Closure used to be defined implicitly — "the player spent the region's whole
    /// authored Vitality total on this exact roster", which is a property of arithmetic
    /// rather than of the product, and which made the ending brittle against any change to
    /// costs, conversion or content. A weekday-only player with a six-week break earns
    /// 16,420 Vitality against a 19,400-Vitality roster, and the finding sat open for a
    /// milestone because nothing in the repository stated the rule the numbers were
    /// silently being measured against.
    ///
    /// The criterion has two authored routes. The primary route is the closure milestone
    /// project itself — the narrative ending, whose prerequisite closure spans the whole
    /// roster. The alternative route is a majority of the authored chains, which is what a
    /// player who cannot fund the whole roster can still reach; it is deliberately a
    /// STRONG majority so that at most one chain may remain, and so that the ending cannot
    /// be reached cheaply.
    /// </summary>
    public sealed class ClosureCriterionDefinition
    {
        /// <summary>The project whose completion is the authored ending (the primary route).</summary>
        public string MilestoneProjectId { get; }

        /// <summary>
        /// Chains that must be complete for the alternative route. Must be at least a
        /// majority of the region's chains, and at most the chain count (which makes the
        /// alternative route equivalent to the primary one and therefore harmless).
        /// </summary>
        public int MinimumChainsComplete { get; }

        public string TitleKey { get; }

        public string DescriptionKey { get; }

        public ClosureCriterionDefinition(
            string milestoneProjectId,
            int minimumChainsComplete,
            string titleKey,
            string? descriptionKey = null)
        {
            if (string.IsNullOrWhiteSpace(milestoneProjectId))
                throw new ArgumentException(
                    "Closure criterion requires the closure milestone project.", nameof(milestoneProjectId));
            if (minimumChainsComplete <= 0)
                throw new ArgumentException("Closure minimum chains must be positive.", nameof(minimumChainsComplete));
            if (string.IsNullOrWhiteSpace(titleKey))
                throw new ArgumentException("Closure criterion requires a title.", nameof(titleKey));

            MilestoneProjectId = milestoneProjectId;
            MinimumChainsComplete = minimumChainsComplete;
            TitleKey = titleKey;
            DescriptionKey = descriptionKey ?? string.Empty;
        }
    }
}
