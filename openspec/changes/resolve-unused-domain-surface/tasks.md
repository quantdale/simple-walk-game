## 1. Canonical randomness: decide and record (no new consumer)

- [x] 1.1 Record the decision in `docs/DECISIONS.md`: keep
  `DeterministicRng` and `GameState.Rng` as persisted infrastructure for a
  future canonical consumer, with **no consumer existing today**. Correct D-017
  to state this explicitly rather than describing a live mechanism.
- [x] 1.2 In `src/WalkGame.Domain/Randomness/DeterministicRng.cs`, add a class
  doc statement that no canonical consumer exists today, and that the first
  consumer must add an end-to-end determinism test covering save/load.
- [x] 1.3 In `src/WalkGame.Domain/GameState.cs`, document the `Rng` field's
  current status the same way.
- [x] 1.4 **Do not** wire the RNG into any progression, content or simulation
  path. Adding a random mechanic to justify existing code is out of scope and
  is explicitly forbidden.
- [x] 1.5 If the implementer decides removal is preferable, that requires a
  decision record, an `ISaveMigration` for the removed field, and a
  mature-save fixture test. Do not remove the field silently.

## 2. Honesty test for the seed parameter

- [x] 2.1 Add a test asserting that two profiles created with **different**
  `StartNewGame` seeds currently reach identical canonical state (driven
  through the real trust pipeline, with canonical state compared deeply).
- [x] 2.2 Add a comment on that test explaining its purpose: it pins the
  current honest property so the first canonical consumer must revisit seed
  semantics deliberately rather than silently changing them.
- [x] 2.3 Confirm the test is deterministic and does not depend on wall-clock
  ordering.

## 3. Make the test-data RNG obviously not-canonical

- [x] 3.1 In `tests/WalkGame.Domain.Tests/SeededProgressionPropertyTests.cs`,
  rename the local `DeterministicRng` usage so its role as a **property-test
  data generator** is evident from the identifier, and add a one-line comment
  stating it is test input, never canonical game randomness.
- [x] 3.2 Confirm the test's assertions are unchanged by the rename.

## 4. Resolve `DomainResult<T>`

- [x] 4.1 Before removing, grep for any consumer of `DomainResult<` in `src/`,
  `tools/`, `tests/`, `docs/` and scripts, and confirm the only references are
  the declaration and its own unit tests.
- [x] 4.2 Remove `DomainResult<T>` from
  `src/WalkGame.Domain/Common/Results.cs`.
- [x] 4.3 Remove or rewrite the two cases in
  `tests/WalkGame.Domain.Tests/IdsAndResultsTests.cs` that exercise it. Do not
  weaken the surrounding non-generic `DomainResult` coverage.
- [x] 4.4 Record in `docs/DECISIONS.md` that a value-returning use case, when it
  arrives, chooses its result type against a real requirement.
- [x] 4.5 If the implementer decides to retain it instead, record that decision
  with its rationale. Either outcome is acceptable; an unrecorded one is not.

## 5. Unused error codes and a guard

- [x] 5.1 Confirm `ErrorCodes.NotAvailable`, `ErrorCodes.QueueEmpty` and
  `ErrorCodes.InvalidArgument` have no consumers, and that no persisted value,
  wire format, or localization table keys on any error-code string (grep
  `docs/`, `scripts/`, and the save/JSON code paths before removing).
- [x] 5.2 Remove the three unused codes.
- [x] 5.3 Add a guard test reflecting over `ErrorCodes` and asserting every
  declared constant is referenced somewhere under `src/`. Report **all** unused
  codes in one failure message, not just the first.
- [x] 5.4 Provide a small, visible reserved-codes allow-list with a required
  reason per entry, so platform adapters can declare codes ahead of the
  operations that return them without the guard blocking legitimate work.
- [x] 5.5 Prove the guard works: add a deliberately-unused code in a scratch
  edit, assert the guard fails and names it, then remove it.

## 6. Documentation

- [x] 6.1 `docs/AGENT_EXECUTION_GUIDE.md` — add a short "dormant surface"
  note: canonical randomness is infrastructure with no consumer today; a
  property-test's use of the generator is not canonical randomness.
- [x] 6.2 `docs/DECISIONS.md` — D-017 correction, the `DomainResult<T>`
  decision, the error-code decision, and the guard's allow-list policy.
- [x] 6.3 `docs/RISK_REGISTER.md` — no change expected; confirm no risk item
  claimed these mechanisms were live.

## 7. Verification

- [x] 7.1 `dotnet build SimpleWalkGame.sln` — zero errors, no new warnings.
- [x] 7.2 `dotnet test` — all suites green; the honesty test passes (different
  seeds → identical state).
- [x] 7.3 `scripts/verify-architecture.sh` — 11 passed, 0 failed.
- [x] 7.4 `scripts/verify-save-growth.sh` and
  `scripts/verify-pacing-evidence.sh` — unchanged and green (no payload or
  content change).
- [x] 7.5 `tests/guards/run-guard-tests.sh` — green.
- [x] 7.6 `openspec validate resolve-unused-domain-surface --strict` passes.
- [x] 7.7 `git status` clean apart from intended files; writer lease released.

---

## Implementation record (added during apply)

1. **Task 1.4's prohibition was respected.** The RNG was **not** wired into any progression,
   content or simulation path. The alternative — adding a random mechanic so the existing
   generator has a consumer — was rejected: inventing a feature to justify a dependency is
   strictly worse than a documented unused one. D-017's *policy* is kept and its
   description of a *mechanism* is corrected.

2. **Task 4 took the removal path** (4.2/4.3), not the retention path (4.5). Task 4.1's
   grep confirmed zero production/tool references and only two lines in the type's own
   unit test. The `DomainResult.From` test that used `QueueEmpty`/`InvalidArgument` as
   arbitrary codes now uses live codes; the behaviour it pins is unchanged, so the
   surrounding non-generic `DomainResult` coverage is not weakened.

3. **Task 5.1's persistence check came first and changed the risk of the removal.** Before
   deleting anything it was confirmed that no persisted value, wire format or localisation
   key keys on `project.not-available`, `queue.empty` or `domain.invalid-argument` — grepped
   across `docs/`, `scripts/` and the save/JSON paths. That is why no `ISaveMigration` and
   no mature-save fixture were needed. Note that `OnboardingActivityStepState.NotAvailable`
   is a *different* enum member and is untouched.

4. **Both guards were proven to fail**, and the error-code guard's transcript is in D-057.
   The D-056 use-case guard's first proof attempt passed vacuously because its own doc
   comment named the probe; that self-reference is documented in the guard itself.

5. **Task 6.3 needed no change.** A grep of `docs/RISK_REGISTER.md` for randomness,
   deterministic seeds, D-017, `DomainResult` and error codes found no risk item that
   claimed these mechanisms were live — the single match is D-054's own text about the
   error code it added. Recorded as a confirmation rather than an edit.
