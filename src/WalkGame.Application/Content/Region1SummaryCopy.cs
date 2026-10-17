using System;
using System.Collections.Generic;

namespace WalkGame.Application.Content
{
    /// <summary>
    /// D-162: display-time completion sentences for Region 1 projects. Project titles are
    /// imperative ("Complete the valley survey"), so the stored composer line "{Title} is complete."
    /// reads badly ("Complete the valley survey is complete."). The stored line is unchanged (saves,
    /// dedup and shell parsing keep using it); at display time it is replaced by the sentence here,
    /// keyed by stable project id. A locale may override it with the overlay entry
    /// "&lt;project id&gt;#completed" (<see cref="ContentText.Fields.Completed"/>). Presentation only:
    /// the catalog, its content fingerprint and saves never see this table.
    /// </summary>
    public static class Region1SummaryCopy
    {
        private static readonly Dictionary<string, string> Completed = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["proj.clear-trailhead"] = "The old trailhead is clear.",
            ["proj.rebuild-trail-bridges"] = "The trail bridges are rebuilt.",
            ["proj.open-lookout"] = "The ridge lookout is open.",
            ["proj.river-intake"] = "The river intake is restored.",
            ["proj.clear-reservoir"] = "The old reservoir is clear.",
            ["proj.lay-water-lines"] = "Water lines now run to the hub.",
            ["proj.build-workshop"] = "The settlement workshop is rebuilt.",
            ["proj.restore-market-hall"] = "The market hall is restored.",
            ["proj.wire-settlement-power"] = "Power is wired through the settlement.",
            ["proj.wetland-drainage"] = "The east wetland is drained and replanted.",
            ["proj.replant-native-sedges"] = "Native sedges are replanted.",
            ["proj.build-nesting-islets"] = "The crane nesting islets are built.",
            ["proj.wetland-boardwalk"] = "The wetland boardwalk is raised.",
            ["proj.clear-fallen-timber"] = "The fallen timber is cleared.",
            ["proj.plant-woodland-understory"] = "The woodland understory is planted.",
            ["proj.canopy-walkway"] = "The canopy walkway is raised.",
            ["proj.refit-observatory-dome"] = "The observatory dome is refitted.",
            ["proj.calibrate-survey-rig"] = "The survey rig is calibrated.",
            ["proj.complete-valley-survey"] = "The valley survey is complete.",
        };

        /// <summary>The authored completion sentence for a project id, or null when none is authored.</summary>
        public static string? CompletedLine(string? projectId) =>
            projectId != null && Completed.TryGetValue(projectId, out var line) ? line : null;

        /// <summary>Every project id with an authored completion sentence (for coverage tests).</summary>
        public static IEnumerable<string> ProjectIds => Completed.Keys;
    }
}
