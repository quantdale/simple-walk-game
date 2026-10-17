## 1. Composer
- [x] 1.1 `ReturnSummaryComposer.PickRetired` / `RetentionRank`: oldest batch first, then lowest rank, then oldest held item; the incoming item competes.
## 2. Shell display
- [x] 2.1 `SummaryDisplayFilter` + `ShellController.Summary`: skip landmark lines repeated by a shown completion's detail and "ready" lines for restored projects.
## 3. Tests
- [x] 3.1 `SummaryPriorityTests` (crowded closure day, cross-batch rotation, other kinds unchanged, rank table, display pruning).
- [x] 3.2 `Region1PlaythroughTests` requires 0 rotated-out completion lines.
- [x] 3.3 EditMode `RegionCelebrationTests.Celebration_keeps_the_final_completion_line_and_skips_repeats`.
## 4. Verify
- [x] 4.1 .NET suite, pacing evidence (7 reproduced, 0 drifted), content compatibility, save growth; EditMode.
