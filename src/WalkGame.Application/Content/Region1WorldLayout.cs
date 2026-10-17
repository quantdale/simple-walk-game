using System;
using System.Collections.Generic;

namespace WalkGame.Application.Content
{
    /// <summary>
    /// D-165 (OpenSpec add-interactive-world-presentation): presentation-only binding of each
    /// D-062 restoration chain to the Millbrook Valley landmark where its work happens, so a world
    /// view can say which place a project belongs to. Not canonical content: it never changes
    /// cost, prerequisites, the content version or the fingerprint, and no rule reads it.
    /// Style-neutral: it names places, never how they look.
    /// </summary>
    public static class Region1WorldLayout
    {
        /// <summary>Chain id → landmark id. Validated against the catalog by tests.</summary>
        public static readonly IReadOnlyDictionary<string, string> LocationOfChain = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["chain.trail-access"] = "lm.trailhead",
            ["chain.water-system"] = "lm.river-intake",
            ["chain.settlement-community"] = "lm.settlement",
            ["chain.wetland-recovery"] = "lm.wetland",
            ["chain.woodland"] = "lm.canopy",
            ["chain.research"] = "lm.observatory",
        };
    }
}
