## 1. Correct and self-checking bounds

- [x] 1.1 In `src/WalkGame.Domain/Validation/ContentValidator.cs`, replace
  `MaxMilliUnitsPerDay = 1_000_000_000_000L` with a bound derived from
  representability: the largest rate for which
  `(subDayRemainderTicks) × MilliUnitsPerDay` stays within `long.MaxValue`, where
  `subDayRemainderTicks < TimeSpan.TicksPerDay`. Express it as a derived constant
  with the derivation in the comment, not as a round number.
- [x] 1.2 Add a self-check inside `ContentValidator` that recomputes the bound
  from the simulation's maximum interval and the integer range, compares it to
  the published bound, and emits a content-validation violation naming both
  values if they disagree. The check must also assert which term of the tick
  formula is the binding constraint.
- [x] 1.3 Reference the simulation's maximum interval as the single source of
  truth rather than restating its value, so changing the interval cannot leave
  the validator bound stale. If that requires a small shared arithmetic helper,
  add it in Domain and have both the simulation and the validator use it.
- [x] 1.4 Keep the existing capacity check (`CapacityUnits > long.MaxValue /
  MilliUnitsPerUnit`) and the total-cost budget check; verify both are still
  correct against the same reasoning and tighten only if they are not.
- [x] 1.5 Rewrite the misleading comment
  ("Producer rates multiply by up to MaxProducerInterval days; keep products
  representable") to state what the bound actually is and how it was derived.

## 2. Boundary coverage for every enforced bound

- [x] 2.1 Add a table-driven test in
  `tests/WalkGame.Domain.Tests/ContentValidationTests.cs` covering every numeric
  bound the validator enforces: at the accepted maximum (zero violations) and at
  the first value above it (violation naming the offending content).
- [x] 2.2 Cover producer rate, producer capacity, and total cost budget at
  minimum. Include any other numeric bound `ContentValidator` enforces.
- [x] 2.3 Add a test proving the simulation cannot overflow at the accepted
  bound: author a producer at exactly the maximum accepted rate, simulate the
  maximum supported interval, and assert the produced amount is non-negative,
  representable, and that the resulting `StoredMilliUnits` is within
  `[0, CapacityUnits × 1000]`.
- [x] 2.4 Add a sub-day-remainder case: a producer at the maximum accepted rate
  with a fractional day elapsed must also produce a non-negative amount.
- [x] 2.5 Add a test for the self-check: simulate drift between the published
  bound and the derived bound (by temporarily perturbing one in a test-only
  construction) and assert a violation is reported. If the self-check cannot be
  exercised without production code changes, restructure it so it can.
- [x] 2.6 Assert that every violation message names the specific offending
  project, producer, landmark, discovery or expedition.

## 3. Shipped-content re-verification

- [x] 3.1 Confirm `M4ContentGraphTests.AuthoredRegion1Catalog_Validates_WithZeroViolations`
  still passes with zero violations and that no Region 1 value changed.
- [x] 3.2 Confirm no Region 1 producer rate is within two orders of magnitude of
  the new bound. If one is, stop and investigate the arithmetic rather than
  raising the bound.
- [x] 3.3 `scripts/verify-pacing-evidence.sh` — all seven committed reports
  reproduce byte-for-byte. This is the primary proof that the production
  formula was not touched.

## 4. Documentation

- [x] 4.1 `docs/DECISIONS.md` — record the corrected bound, its derivation, and
  the self-check.
- [x] 4.2 `docs/WORLD_AND_CONTENT.md` — document the authoring bounds any future
  content campaign must respect, with the derivation.
- [x] 4.3 `docs/TECHNICAL_ARCHITECTURE.md` §23 — note that the content validator
  is the release gate for numeric representability, not only for references.

## 5. Coordination with sibling changes

- [x] 5.1 Confirm this change is applied together with, or before,
  `bound-secondary-resource-economy` and
  `gate-saves-against-authored-content-version`, all of which edit
  `ContentValidator`.
- [x] 5.2 If more than one is in flight, rebase so the final file contains the
  numeric bounds, the cap/requirement rules, and the tombstone rules
  simultaneously, and re-run `ContentValidationTests` in full.

## 6. Verification

- [x] 6.1 `dotnet build SimpleWalkGame.sln` — zero errors, no new warnings.
- [x] 6.2 `dotnet test` — all suites green.
- [x] 6.3 `scripts/verify-architecture.sh` — 11 passed, 0 failed.
- [x] 6.4 `scripts/verify-pacing-evidence.sh` — seven reproduced, 0 drifted.
- [x] 6.5 `tests/guards/run-guard-tests.sh` — green.
- [x] 6.6
  `openspec validate align-content-validator-with-simulation-overflow-bounds --strict`
  passes.
- [ ] 6.7 `git status` clean apart from intended files; writer lease released.

---

## Implementation record (added during apply)

1. **`MaxMilliUnitsPerDay` became `static readonly`, not `const`.** A compile-time constant
   cannot reference `ProducerArithmetic.MaxRepresentableMilliUnitsPerDay`, and hardcoding
   the number is exactly the failure this change removes. The self-check takes the published
   bound as a **parameter** rather than a default (default parameter values must be
   compile-time constants), which is also what makes the self-check directly testable with
   a perturbed value — no production-code change needed to exercise it, as task 2.5 required.

2. **`MaxProducerInterval` moved into `ProducerArithmetic`.** The simulation now reads
   `ProducerArithmetic.MaxInterval`, so there is a single source of truth and changing the
   cap cannot leave the validator's bound stale (task 1.3). The validator is in
   `Domain.Validation` and the helper in `Domain.Regions`; the dependency runs helper →
   simulation → validator, with no cycle.

3. **Two validator rules are unreachable through their constructors.** `ProducerDefinition`
   already rejects a non-positive `capacityUnits`, and `RegionDefinition` a non-positive
   cap, so `ContentValidator`'s own checks for those can only fire on a definition that
   bypasses its constructor. They are kept as defence in depth, and the tests prove the
   CONSTRUCTOR guard that actually runs. This is recorded rather than left as a test that
   pretends to exercise a live path.

4. **Task 3.2 confirmed by measurement, not by raising the bound.** Region 1's shipped rates
   are 1200 / 1500 / 2500 milli-units/day; the corrected bound is `long.MaxValue /
   (TicksPerDay - 1)` ≈ 1.07e7, i.e. about 2500x above the largest shipped rate and far
   below the old 1e12. No investigation of the arithmetic was warranted.
