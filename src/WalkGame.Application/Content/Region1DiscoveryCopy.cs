using System;
using System.Collections.Generic;

namespace WalkGame.Application.Content
{
    /// <summary>
    /// Authored player copy for Region 1 discovery body and origin keys (D-086). The catalog
    /// keeps keys (canonical state never stores copy, and the content fingerprint is
    /// unchanged); presentation resolves them here. Storyline-consistent with the D-076
    /// Millbrook chapters: the floods, the failed drainage scheme, the heron-over-wheel mark,
    /// the people who come back. An unknown key returns null so a raw key never reaches a screen.
    /// </summary>
    public static class Region1DiscoveryCopy
    {
        private static readonly Dictionary<string, string> Copy = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["disc.old-millstone.body"] = "A grooved millstone half sunk in the trailhead mud, the heron-over-wheel mark cut into its rim. It ground the valley's grain for a century before the floods carried it off its spindle.",
            ["disc.old-millstone.provenance"] = "Found where the old trail meets the road. The miller's family rolled it uphill to mark the way in when they left, so anyone who came back would know where to start.",
            ["disc.intake-plate-stamp.body"] = "A brass plate from the intake valve house, stamped with the date the river gates were first hung and a line of tally marks: one for every flood season the gates held.",
            ["disc.intake-plate-stamp.provenance"] = "Prised loose when the gates were freed. The last tally stops the year the drainage scheme failed and the river took the low fields.",
            ["disc.workshop-ledger.body"] = "A water-stained ledger of jobs: hinges for the mill, a cradle, three ploughshares, lamp brackets for the square. Each customer is written by first name, as if everyone knew everyone.",
            ["disc.workshop-ledger.provenance"] = "Wrapped in oilcloth in the workshop's back room. The final entry is unpaid and unsigned: \"Gate hinge, heron mark. Settle when we're back.\"",
            ["disc.reservoir-time-capsule.body"] = "A sealed tin of letters, a school photograph and a pressed crane feather, addressed \"to whoever drinks from this again\".",
            ["disc.reservoir-time-capsule.provenance"] = "Uncovered in the silt when the reservoir basin was cleared. The schoolchildren buried it the summer the reservoir was dug, a generation before the valley went quiet.",
            ["disc.lookout-fire-lens.body"] = "The lookout's fire lens, a heavy glass ring that once threw a warning light across the whole valley. It still splits the morning sun into a thin rainbow.",
            ["disc.lookout-fire-lens.provenance"] = "Left wrapped in a blanket in the lamp room. The last keeper took the bulb but would not take the lens: it belonged to the ridge, the note says.",
            ["disc.grid-archive-map.body"] = "A hand-inked map of Millbrook's power lines, from the mill race generator to every lamp on the square. Pencilled notes mark which poles leaned after each flood.",
            ["disc.grid-archive-map.provenance"] = "Rolled inside a pipe in the settlement substation. It is the same map someone copied by hand for the market notice board.",
            ["disc.market-mural.body"] = "Under peeling whitewash, a painted mural of market day: stalls, a brass band, cranes over the marsh, and the mill wheel turning with a heron perched on top.",
            ["disc.market-mural.provenance"] = "Revealed when the market hall's inner wall was scraped back. Painted by the stallholders together the year the hall opened; each of them signed one painted apple.",
            ["disc.sedge-first-flush.body"] = "A green flush of native sedge along the wetland bank, the first in years. Its roots hold the soft edge so the water can rise without washing it away.",
            ["disc.sedge-first-flush.provenance"] = "Grown from seed that waited in the old bank mud through every dry season after the drains were dug. It only needed the water back.",
            ["disc.crane-return.body"] = "A pair of cranes nesting on the new islets, dancing at dawn the way the old mural shows them. Their call carries all the way to the square.",
            ["disc.crane-return.provenance"] = "First seen the spring after the islets were built. The oldest returning neighbour says the last pair left the year the marsh was drained.",
            ["disc.heron-roost-boards.body"] = "A row of weathered roost boards along the boardwalk, scratched with heron footprints, and on one board the heron-over-wheel mark itself.",
            ["disc.heron-roost-boards.provenance"] = "Salvaged from the old marsh path and set back up when the boardwalk went in. The mark may be where the valley's sign came from in the first place.",
            ["disc.understory-orchids.body"] = "Small pale orchids in the shade of the grove floor. They only flower where the soil has been left undisturbed and damp for several seasons.",
            ["disc.understory-orchids.provenance"] = "Found near the oak with the ribbon. A note in the workshop ledger mentions picking them for a wedding in the square, long before the floods.",
            ["disc.dome-star-chart.body"] = "A star chart drawn on the inside of the observatory dome, with the valley's ridge line traced along the bottom edge so every star has a landmark beneath it.",
            ["disc.dome-star-chart.provenance"] = "Revealed when the dome's shutters were refitted and light reached the plaster again. The surveyors drew it on clear nights while they mapped the valley below.",
            ["disc.survey-baseline-stone.body"] = "The stone the first survey measured every distance from. A fresh line has been cut beside the old one, with today's date and a small heron over a wheel.",
            ["disc.survey-baseline-stone.provenance"] = "Set on the valley floor by the original surveyors and found again by the new survey. Every mile mapped starts here, then and now.",
        };

        /// <summary>Copy for a body or provenance key, or null when none is authored.</summary>
        public static string? Text(string? key) =>
            key != null && Copy.TryGetValue(key, out var text) ? text : null;

        /// <summary>Every authored key (for coverage tests).</summary>
        public static IEnumerable<string> Keys => Copy.Keys;
    }
}
