using System.Collections.Generic;

namespace WalkGame.UnityShell.Presentation
{
    public static class CopyTable
    {
        private static readonly Dictionary<string, string> Entries = new Dictionary<string, string>
        {
            ["region.millbrook-valley"] = "Millbrook Valley",

            ["proj.clear-trailhead"] = "Clear the Trailhead",
            ["proj.rebuild-trail-bridges"] = "Rebuild the Trail Bridges",
            ["proj.open-lookout"] = "Open the Lookout",
            ["proj.river-intake"] = "Restore the River Intake",
            ["proj.clear-reservoir"] = "Clear the Reservoir",
            ["proj.lay-water-lines"] = "Lay Water Lines",
            ["proj.build-workshop"] = "Build the Workshop",
            ["proj.restore-market-hall"] = "Restore the Market Hall",
            ["proj.wire-settlement-power"] = "Wire Settlement Power",
            ["proj.wetland-drainage"] = "Undo the Wetland Drainage",
            ["proj.replant-native-sedges"] = "Replant Native Sedges",
            ["proj.build-nesting-islets"] = "Build Nesting Islets",
            ["proj.wetland-boardwalk"] = "Build the Wetland Boardwalk",
            ["proj.clear-fallen-timber"] = "Clear Fallen Timber",
            ["proj.plant-woodland-understory"] = "Plant the Woodland Understory",
            ["proj.canopy-walkway"] = "Build the Canopy Walkway",
            ["proj.refit-observatory-dome"] = "Refit the Observatory Dome",
            ["proj.calibrate-survey-rig"] = "Calibrate the Survey Rig",
            ["proj.complete-valley-survey"] = "Complete the Valley Survey",

            ["lm.trailhead"] = "Trailhead",
            ["lm.river-intake"] = "River Intake",
            ["lm.settlement"] = "Old Settlement",
            ["lm.wetland"] = "Wetlands",
            ["lm.canopy"] = "Forest Canopy",
            ["lm.observatory"] = "Observatory",

            ["prd.workshop-salvage"] = "Workshop Salvage Store",
            ["prd.nursery-greenhouse"] = "Nursery Greenhouse Store",
            ["prd.observatory-archive"] = "Observatory Archive Store",

            ["exp.source-to-sound.description"] =
                "Walk the full route from the high trailhead down to the water.",
            ["exp.river-run.description"] =
                "Follow the river from the restored intake down to the wetlands.",
            ["exp.valley-transect.description"] =
                "A full ecological crossing of the valley, canopy to wetland.",

            ["disc.old-millstone.title"] = "The Old Millstone",
            ["disc.old-millstone.body"] =
                "An old millstone half-buried beside the trail, its grooves still holding the memory of the valley's working years.",
            ["disc.old-millstone.provenance"] =
                "Found near the trailhead during initial clearing.",
            ["disc.intake-plate-stamp.title"] = "Intake Plate Stamp",
            ["disc.intake-plate-stamp.body"] =
                "A stamped maker's plate on the intake valve house: the foundry, the year, and a small pressed ivy leaf.",
            ["disc.intake-plate-stamp.provenance"] =
                "Recorded at the intake works while freeing the mechanism.",
            ["disc.workshop-ledger.title"] = "The Workshop Ledger",
            ["disc.workshop-ledger.body"] =
                "A water-stained ledger listing every tool the workshop once lent out. Most of them came back.",
            ["disc.workshop-ledger.provenance"] =
                "Recovered from the workshop backroom during rebuilding.",
            ["disc.reservoir-time-capsule.title"] = "Reservoir Time Capsule",
            ["disc.reservoir-time-capsule.body"] =
                "A sealed jar sunk in the basin silt, holding coins, a photograph, and a note promising to return.",
            ["disc.reservoir-time-capsule.provenance"] =
                "Lifted from the reservoir basin during dredging.",
            ["disc.lookout-fire-lens.title"] = "The Lookout Fire Lens",
            ["disc.lookout-fire-lens.body"] =
                "The fire lens of the old lookout, clouded but unbroken. Polished, it throws sunlight across the whole ridge.",
            ["disc.lookout-fire-lens.provenance"] =
                "Documented in the lamp room when the lookout was reopened.",
            ["disc.grid-archive-map.title"] = "Grid Archive Map",
            ["disc.grid-archive-map.body"] =
                "A hand-inked map of the valley power grid, annotated with every pole and fuse in three different hands.",
            ["disc.grid-archive-map.provenance"] =
                "Found filed inside the settlement substation panel.",
            ["disc.market-mural.title"] = "The Market Mural",
            ["disc.market-mural.body"] =
                "Under the whitewash, a mural of market day: carts, kites, and a dog mid-leap.",
            ["disc.market-mural.provenance"] =
                "Uncovered on the market hall wall during restoration.",
            ["disc.sedge-first-flush.title"] = "First Sedge Flush",
            ["disc.sedge-first-flush.body"] =
                "Bright green shoots of replanted sedge where the bank was bare mud only weeks ago.",
            ["disc.sedge-first-flush.provenance"] =
                "Observed along the wetland bank after replanting.",
            ["disc.crane-return.title"] = "The Cranes Return",
            ["disc.crane-return.body"] =
                "Two cranes circling the new nesting islets at dusk, then landing as if they had always meant to.",
            ["disc.crane-return.provenance"] =
                "Recorded over the wetlands after the islets were built.",
            ["disc.heron-roost-boards.title"] = "Heron Roost Boards",
            ["disc.heron-roost-boards.body"] =
                "Fresh heron tracks on the new roost boards: someone approved of the carpentry.",
            ["disc.heron-roost-boards.provenance"] =
                "Noted from the boardwalk roost platform.",
            ["disc.understory-orchids.title"] = "Understory Orchids",
            ["disc.understory-orchids.body" ] = 
                "Orchids surfacing in the replanted understory, delicate and entirely unbothered by the plans that put them there.",
            ["disc.understory-orchids.provenance"] =
                "Found on the grove floor during understory planting.",
            ["disc.dome-star-chart.title"] = "Dome Star Chart",
            ["disc.dome-star-chart.body"] =
                "A star chart pinned inside the dome, one constellation circled repeatedly, as if it once mattered very much.",
            ["disc.dome-star-chart.provenance"] =
                "Documented when the observatory dome was refitted.",
            ["disc.survey-baseline-stone.title"] = "The Survey Baseline Stone",
            ["disc.survey-baseline-stone.body"] =
                "The cut stone from which every historical survey of the valley began, still true to the day it was set.",
            ["disc.survey-baseline-stone.provenance"] =
                "Verified at the valley baseline during the final survey.",

            ["loc.trailhead.millstone"] = "Trailhead",
            ["loc.intake.valve-house"] = "Intake valve house",
            ["loc.workshop.backroom"] = "Workshop backroom",
            ["loc.reservoir.basin"] = "Reservoir basin",
            ["loc.lookout.lamp-room"] = "Lookout lamp room",
            ["loc.settlement.substation"] = "Settlement substation",
            ["loc.market.hall-wall"] = "Market hall wall",
            ["loc.wetland.bank"] = "Wetland bank",
            ["loc.wetland.islets"] = "Nesting islets",
            ["loc.boardwalk.roost"] = "Boardwalk roost",
            ["loc.grove.floor"] = "Grove floor",
            ["loc.observatory.dome"] = "Observatory dome",
            ["loc.valley.baseline"] = "Valley baseline",
        };

        public static string Text(string key)
        {
            return key != null && Entries.TryGetValue(key, out var text) ? text : key ?? string.Empty;
        }
    }
}
