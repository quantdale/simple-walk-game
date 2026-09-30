## 1. Canonical randomness: decide and record (no new consumer)

- [ ] 1.1 Record the decision in `docs/DECISIONS.md`: keep
  `DeterministicRng` and `GameState.Rng` as persisted infrastructure for a
  future canonical consumer, with **no consumer existing today**. Correct D-017
  to state this explicitly rather than describing a live mechanism.
- [ ] 1.2 In `src/WalkGame.Domain/Randomness/DeterministicRng.cs`, add a class
  doc statement that no canonical consumer exists today, and that the first
  consumer must add an end-to-end determinism test covering save/load.
- [ ] 1.3 In `src/WalkGame.Domain/GameState.cs`, document the `Rng` field's
  current status the same way.
- [ ] 1.4 **Do not** wire the RNG into any progression, content or simulation
  path. Adding a random mechanic to justify existing code is out of scope and
  is explicitly forbidden.
- [ ] 1.5 If the implementer decides removal is preferable, that requires a
  decision record, an `ISaveMigration` for the removed field, and a
  mature-save fixture test. Do not remove the field silently.

## 2. Honesty test for the seed parameter

- [ ] 2.1 Add a test asserting that two profiles created with **different**
  `StartNewGame` seeds currently reach identical canonical state (driven
  through the real trust pipeline, with canonical state compared deeply).
- [ ] 2.2 Add a comment on that test explaining its purpose: it pins the
  current honest property so the first canonical consumer must revisit seed
  semantics deliberately rather than silently changing them.
- [ ] 2.3 Confirm the test is deterministic and does not depend on wall-clock
  ordering.

## 3. Make the test-data RNG obviously not-canonical

- [ ] 3.1 In `tests/WalkGame.Domain.Tests/SeededProgressionPropertyTests.cs`,
  rename the local `DeterministicRng` usage so its role as a **property-test
  data generator** is evident from the identifier, and add a one-line comment
  stating it is test input, never canonical game randomness.
- [ ] 3.2 Confirm the test's assertions are unchanged by the rename.

## 4. Resolve `DomainResult<T>`

- [ ] 4.1 Before removing, grep for any consumer of `DomainResult<` in `src/`,
  `tools/`, `tests/`, `docs/` and scripts, and confirm the only references are
  the declaration and its own unit tests.
- [ ] 4.2 Remove `DomainResult<T>` from
  `src/WalkGame.Domain/Common/Results.cs`.
- [ ] 4.3 Remove or rewrite the two cases in
  `tests/WalkGame.Domain.Tests/IdsAndResultsTests.cs` that exercise it. Do not
  weaken the surrounding non-generic `DomainResult` coverage.
- [ ] 4.4 Record in `docs/DECISIONS.md` that a value-returning use case, when it
  arrives, chooses its result type against a real requirement.
- [ ] 4.5 If the implementer decides to retain it instead, record that decision
  with its rationale. Either outcome is acceptable; an unrecorded one is not.

## 5. Unused error codes and a guard

- [ ] 5.1 Confirm `ErrorCodes.NotAvailable`, `ErrorCodes.QueueEmpty` and
  `ErrorCodes.InvalidArgument` have no consumers, and that no persisted value,
  wire format, or localization table keys on any error-code string (grep
  `docs/`, `scripts/`, and the save/JSON code paths before removing).
- [ ] 5.2 Remove the three unused codes.
- [ ] 5.3 Add a guard test reflecting over `ErrorCodes` and asserting every
  declared constant is referenced somewhere under `src/`. Report **all** unused
  codes in one failure message, not just the first.
- [ ] 5.4 Provide a small, visible reserved-codes allow-list with a required
  reason per entry, so platform adapters can declare codes ahead of the
  operations that return them without the guard blocking legitimate work.
- [ ] 5.5 Prove the guard works: add a deliberately-unused code in a scratch
  edit, assert the guard fails and names it, then remove it.

## 6. Documentation

- [ ] 6.1 `docs/AGENT_EXECUTION_GUIDE.md` — add a short "dormant surface"
  note: canonical randomness is infrastructure with no consumer today; a
  property-test's use of the generator is not canonical randomness.
- [ ] 6.2 `docs/DECISIONS.md` — D-017 correction, the `DomainResult<T>`
  decision, the error-code decision, and the guard's allow-list policy.
- [ ] 6.3 `docs/RISK_REGISTER.md` — no change expected; confirm no risk item
  claimed these mechanisms were live.

## 7. Verification

- [ ] 7.1 `dotnet build SimpleWalkGame.sln` — zero errors, no new warnings.
- [ ] 7.2 `dotnet test` — all suites green; the honesty test passes (different
  seeds → identical state).
- [ ] 7.3 `scripts/verify-architecture.sh` — 11 passed, 0 failed.
- [ ] 7.4 `scripts/verify-save-growth.sh` and
  `scripts/verify-pacing-evidence.sh` — unchanged and green (no payload or
  content change).
- [ ] 7.5 `tests/guards/run-guard-tests.sh` — green.
- [ ] 7.6 `openspec validate resolve-unused-domain-surface --strict` passes.
- [ ] 7.7 `git status` clean apart from intended files; writer lease released.
