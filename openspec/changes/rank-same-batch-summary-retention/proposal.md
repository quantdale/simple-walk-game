# Rank same-batch summary retention so a project's completion line survives (KI-026)

**Status:** Approved for implementation by the executing agent (D-163), under the design authority Michael granted for this campaign. Region 1 scope: a refinement of the `rotate-durable-return-summary` contract, not a Region 2 item. The D-011 / ROADMAP gate is untouched.

## Why

`rotate-durable-return-summary` retires "the oldest item of that same kind" when a kind is at its bound. One composition commits all its lines at the same instant, so on a crowded day "oldest" meant "composed first". That was the project's own "is complete." line, which comes before the landmark, arc, chapter and region lines it caused. D-159's playthroughs saw the canopy walkway's and settlement power's completion lines dropped this way (KI-026).

## What Changes

- When a kind is at its bound, the retired item is from the **oldest committed batch** (earliest first-commit time). This is unchanged across commits, so newer commits still displace older ones.
- Within that batch, the **lowest retention rank** goes first. Transformation ranks: region complete, project completion, new chapter, expedition, "N projects are complete", arc lines, landmark stage. The incoming line competes when it is in that batch. Other kinds have one rank, so they behave exactly as before.
- Shell display only: the summary screen skips a landmark line that a shown completion's D-100 detail repeats, and "ready" lines for projects already restored.
- No save-schema, content or fingerprint change. Stored summaries load unchanged; only newly composed summaries differ. Pacing evidence reproduces.

## Impact

- `Region1PlaythroughTests`: 0 completion lines dropped in all 9 playthroughs (was up to 2). An expedition line can yield to chapter and project lines on a saturated day; Discoveries and the celebration recap still count it.
