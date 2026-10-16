# Design: restoration storyline

## Model
`StoryChapterDefinition(id, number, titleKey, bodyKey, hintKey, requiredProjectIds[], minCareCycles = 0)` lives on `RegionDefinition.Chapters` (optional; empty for regions without a story).

`StoryProgress.Evaluate(RegionDefinition, RegionState, DateTimeOffset createdAtUtc)` → per chapter `(unlocked, unlockedAtUtc?)`:
- `own(n)` = every required project is Completed and `PostCompletionCyclesCompleted ≥ minCareCycles`;
- `unlocked(n)` = `own(n) ∧ (n == 0 ∨ unlocked(n−1))`;
- `unlockedAt(n)` = max(`unlockedAt(n−1)`, max `CompletedAtUtc` of the required projects). The prologue uses the game's creation time. A care-gated chapter uses the time it was observed unlocked, which is null when unknown; the surface then says "recently".

Pure function. No clock reads, no state writes.

## Why derived
Project completion is monotonic and already durable, so unlock state can never regress, and no new persisted field needs a codec, validator rule or migration. The cost is that a care-gated epilogue's time is approximate. That is acceptable for an epilogue.

## Return-summary moments
`GameSession` evaluates the story before and after each committed batch and passes newly unlocked chapter titles to `ReturnSummaryComposer.Compose`. The composer emits one `Transformation` item per chapter ("New chapter: <title>"). This reuses the existing kind, so the persisted summary shape is unchanged, and the existing per-kind bound keeps it inside the glance budget.

## Region 1 chapters (Millbrook Valley)
| # | Title | Unlocked by |
|---|---|---|
| 0 | The Quiet Valley | start (prologue) |
| 1 | The Way In | Clear the old trailhead |
| 2 | Water Remembers | Restore the river intake |
| 3 | Hands at the Bench | Rebuild the settlement workshop |
| 4 | The Marsh Breathes | Drain and replant the east wetland |
| 5 | Lights in the Square | Wire settlement power |
| 6 | Under the Canopy | Raise the canopy walkway |
| 7 | Every Mile Mapped | Complete the valley survey (closure) |
| 8 | The Long Watch (epilogue) | 1 evergreen tending cycle after completion |

## Cadence
Measured per profile with the real pipeline. The gate lives in `StoryCadenceTests`. Bounds are set from the measured first run and recorded in D-076.
