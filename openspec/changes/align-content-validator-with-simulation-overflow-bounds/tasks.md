## 1. Correct and self-checking bounds

- [ ] 1.1 In `src/WalkGame.Domain/Validation/ContentValidator.cs`, replace
  `MaxMilliUnitsPerDay = 1_000_000_000_000L` with a bound derived from
  representability: the largest rate for which
  `(subDayRemainderTicks) × MilliUnitsPerDay` stays within `long.MaxValue`, where
  `subDayRemainderTicks < TimeSpan.TicksPerDay`. Express it as a derived constant
  with the derivation in the comment, not as a round number.
- [ ] 1.2 Add a self-check inside `ContentValidator` that recomputes the bound
  from the simulation's maximum interval and the integer range, compares it to
  the published bound, and emits a content-validation violation naming both
  values if they disagree. The check must also assert which term of the tick
  formula is the binding constraint.
- [ ] 1.3 Reference the simulation's maximum interval as the single source of
  truth rather than restating its value, so changing the interval cannot leave
  the validator bound stale. If that requires a small shared arithmetic helper,
  add it in Domain and have both the simulation and the validator use it.
- [ ] 1.4 Keep the existing capacity check (`CapacityUnits > long.MaxValue /
  MilliUnitsPerUnit`) and the total-cost budget check; verify both are still
  correct against the same reasoning and tighten only if they are not.
- [ ] 1.5 Rewrite the misleading comment
  ("Producer rates multiply by up to MaxProducerInterval days; keep products
  representable") to state what the bound actually is and how it was derived.

## 2. Boundary coverage for every enforced bound

- [ ] 2.1 Add a table-driven test in
  `tests/WalkGame.Domain.Tests/ContentValidationTests.cs` covering every numeric
  bound the validator enforces: at the accepted maximum (zero violations) and at
  the first value above it (violation naming the offending content).
- [ ] 2.2 Cover producer rate, producer capacity, and total cost budget at
  minimum. Include any other numeric bound `ContentValidator` enforces.
- [ ] 2.3 Add a test proving the simulation cannot overflow at the accepted
  bound: author a producer at exactly the maximum accepted rate, simulate the
  maximum supported interval, and assert the produced amount is non-negative,
  representable, and that the resulting `StoredMilliUnits` is within
  `[0, CapacityUnits × 1000]`.
- [ ] 2.4 Add a sub-day-remainder case: a producer at the maximum accepted rate
  with a fractional day elapsed must also produce a non-negative amount.
- [ ] 2.5 Add a test for the self-check: simulate drift between the published
  bound and the derived bound (by temporarily perturbing one in a test-only
  construction) and assert a violation is reported. If the self-check cannot be
  exercised without production code changes, restructure it so it can.
- [ ] 2.6 Assert that every violation message names the specific offending
  project, producer, landmark, discovery or expedition.

## 3. Shipped-content re-verification

- [ ] 3.1 Confirm `M4ContentGraphTests.AuthoredRegion1Catalog_Validates_WithZeroViolations`
  still passes with zero violations and that no Region 1 value changed.
- [ ] 3.2 Confirm no Region 1 producer rate is within two orders of magnitude of
  the new bound. If one is, stop and investigate the arithmetic rather than
  raising the bound.
- [ ] 3.3 `scripts/verify-pacing-evidence.sh` — all seven committed reports
  reproduce byte-for-byte. This is the primary proof that the production
  formula was not touched.

## 4. Documentation

- [ ] 4.1 `docs/DECISIONS.md` — record the corrected bound, its derivation, and
  the self-check.
- [ ] 4.2 `docs/WORLD_AND_CONTENT.md` — document the authoring bounds any future
  content campaign must respect, with the derivation.
- [ ] 4.3 `docs/TECHNICAL_ARCHITECTURE.md` §23 — note that the content validator
  is the release gate for numeric representability, not only for references.

## 5. Coordination with sibling changes

- [ ] 5.1 Confirm this change is applied together with, or before,
  `bound-secondary-resource-economy` and
  `gate-saves-against-authored-content-version`, all of which edit
  `ContentValidator`.
- [ ] 5.2 If more than one is in flight, rebase so the final file contains the
  numeric bounds, the cap/requirement rules, and the tombstone rules
  simultaneously, and re-run `ContentValidationTests` in full.

## 6. Verification

- [ ] 6.1 `dotnet build SimpleWalkGame.sln` — zero errors, no new warnings.
- [ ] 6.2 `dotnet test` — all suites green.
- [ ] 6.3 `scripts/verify-architecture.sh` — 11 passed, 0 failed.
- [ ] 6.4 `scripts/verify-pacing-evidence.sh` — seven reproduced, 0 drifted.
- [ ] 6.5 `tests/guards/run-guard-tests.sh` — green.
- [ ] 6.6
  `openspec validate align-content-validator-with-simulation-overflow-bounds --strict`
  passes.
- [ ] 6.7 `git status` clean apart from intended files; writer lease released.
