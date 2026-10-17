## 1. Domain
- [x] 1.1 `RegionDefinition.MeetsClosureCriterion` enforces the chain minimum on the milestone route; `ExplainsRecordedCompletion` for validation of recorded completions.
- [x] 1.2 `GameStateValidator` validates a recorded completion with `ExplainsRecordedCompletion`.
## 2. Tests
- [x] 2.1 `Region1PlaythroughTests`: 9 profile × policy playthroughs, distinct-routes check, survey-before-canopy regression, pre-D-159 closed-save compatibility.
- [x] 2.2 Mutation check: reverting 1.1 fails the three cheapest-first playthroughs, the distinct-routes check and the regression test.
## 3. Verify
- [x] 3.1 .NET suite, pacing evidence (7 reproduced, 0 drifted), content compatibility, architecture; EditMode.
