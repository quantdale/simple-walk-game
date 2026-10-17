# Enforce the closure criterion's chain minimum on the milestone route

**Status:** Approved for implementation by the executing agent (D-159), under the design authority Michael granted for this campaign. Region 1 scope; a fix to an existing contract, not a Region 2 item (the D-011 / ROADMAP gate is untouched).

## Why

The authored Region 1 closure criterion (D-062) is "the valley survey AND all six chains". `RegionDefinition.MeetsClosureCriterion` returned true as soon as the survey milestone was complete, on the assumption that the survey's prerequisite closure restores the whole roster. It does not: the canopy walkway (4,000 Vitality, the woodland chain's last project) is no prerequisite of the survey.

The D-159 whole-playthrough simulation found the consequence: a player who chooses the cheapest open project each time funds the survey before the canopy walkway. The region then closed with a chain unbuilt. The player got the celebration, the epilogue and Evergreen Tending (which absorbs every later Vitality), while chapters 7–9 ("Above the Trees", "Every Mile Mapped", "The Long Watch") and Nell's canopy letter stayed locked behind a project the queue no longer prompted for. The catalog-order auto-player used for the pacing evidence never hits this, because it funds the canopy before the survey.

## What Changes

- Domain: `MeetsClosureCriterion` requires `ChainsComplete >= MinimumChainsComplete` on every route; the milestone route additionally requires the milestone. For Region 1 that is the survey plus all 19 projects.
- Domain: new `ExplainsRecordedCompletion` (the current criterion OR the pre-D-159 milestone-only rule) is what `GameStateValidator` uses for a region already marked completed. Completion is set exactly once and never reset, so a save that closed under the old rule stays loadable and complete. It can still queue and fund the canopy walkway and reach every chapter (tested).
- No content change, no content-version or fingerprint change, no save-schema change. The committed pacing evidence reproduces byte-for-byte (catalog order is unaffected).

## Impact

- Cheapest-first players close the region on the day the canopy walkway completes (high 77, irregular 110, low 512), not on the survey day.
- `Region1PlaythroughTests` (D-159) pins onboarding → closure → care chapter → full epilogue for low/irregular/high × three choice policies.
