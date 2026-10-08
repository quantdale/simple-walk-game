using System;
using System.Collections.Generic;
using System.Linq;
using WalkGame.Domain.Regions;

namespace WalkGame.Application.Pacing
{
    /// <summary>
    /// The activity profiles Region 1 is balanced against, and the verdict the pacing gate
    /// owes them (D-062 / GAME_SYSTEMS §13).
    ///
    /// This lives in the Application layer rather than in the CLI because it is a PRODUCT
    /// CONTRACT, not display code: "which profiles must close, inside how long, and what
    /// makes a run a balance defect" is the rule that decides whether a profile's outcome is
    /// acceptable evidence. While it lived only inside the CLI, nothing could test it — and
    /// the shortfall it was written to catch sat open for a whole milestone because the gate
    /// reported a failure and exited 0 anyway.\n    /// </summary>
    public static class PacingContract
    {
        /// <summary>
        /// Whether the profile MUST close, and its documented target in days.\n        ///
        /// Every profile is measured against an authored target rather than against whatever\n        /// number the current content happens to produce. The <c>low</c> profile is deliberately\n        /// EXEMPT: a sustained 3,000-steps-per-day walker closing Region 1 inside a year is\n        /// not reachable under the authored conversion rate, and D-039 records that long tail\n        /// as an accepted characteristic of a movement-proportional economy. An exemption is a\n        /// documented decision; it is not a silent pass.\n        /// </summary>
        public static (bool MustClose, int MaxDays) Target(string profileName) => profileName switch
        {
            "high" => (true, 120),
            "irregular" => (true, 250),
            "moderate" => (true, 300),
            "weekend-heavy" => (true, 300),
            "pathological" => (true, 400),
            "inactivity" => (true, 400),
            _ => (false, 0),
        };

        /// <summary>True when a profile's measured outcome is a balance defect rather than evidence.</summary>
        public static bool IsBalanceDefect(string profileName, bool completed, int daysSimulated) =>
            IsBalanceDefect(profileName, completed, daysSimulated, daysSimulated);

        /// <summary>
        /// True when a profile's measured outcome is a balance defect rather than evidence.
        ///
        /// The verdict requires the run to have REACHED the horizon the contract documents: a
        /// shorter run is a fixture generator that cannot certify closure either way, and
        /// treating it as a defect made `profile` unusable for the content-drift gate, which
        /// generates short saves with the same verb.
        /// </summary>
        public static bool IsBalanceDefect(string profileName, bool completed, int daysSimulated, int horizonDays)
        {
            var (mustClose, maxDays) = Target(profileName);
            if (!mustClose || horizonDays < maxDays)
                return false;
            return !completed || daysSimulated > maxDays;
        }

        /// <summary>
        /// Chains still unfinished at the horizon, by label. Named rather than counted: "two\n        /// chains are incomplete" is not actionable, "settlement-community, research" is.\n        /// </summary>
        public static IReadOnlyList<string> IncompleteChains(
            RegionDefinition content,
            IReadOnlyDictionary<string, int> projectCompletionDays)
        {
            var incomplete = new List<string>();
            foreach (var chain in content.Chains)
            {
                bool all = true;
                foreach (var projectId in chain.ProjectIds)
                {
                    if (!projectCompletionDays.TryGetValue(projectId, out int completion))
                    {
                        all = false;
                        break;
                    }
                }
                if (!all)
                    incomplete.Add(chain.Id);
            }
            return incomplete;
        }

        /// <summary>Report label for a chain identifier: <c>chain.trail-access</c> prints as <c>trail-access</c>.</summary>
        public static string Label(string chainId) =>
            chainId.StartsWith("chain.", StringComparison.Ordinal) ? chainId.Substring(6) : chainId;

        /// <summary>Report label for every unfinished chain, or "none named" when there are none.</summary>
        public static string IncompleteChainLabels(
            RegionDefinition content,
            IReadOnlyDictionary<string, int> projectCompletionDays)
        {
            var labels = IncompleteChains(content, projectCompletionDays)
                .Select(Label)
                .ToList();
            return labels.Count == 0 ? "none named" : string.Join(", ", labels);
        }
    }
}
