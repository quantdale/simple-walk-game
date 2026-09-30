## 1. ReorderQueue coverage

- [ ] 1.1 Add cases to
  `tests/WalkGame.Application.Tests/SessionQueueControlTests.cs` for a valid
  permutation of two or more queued projects: the persisted order matches the
  requested order, and every queued project is still present exactly once.
- [ ] 1.2 A list with the wrong length is rejected and **changes nothing
  durable** — decode the save and compare to the pre-call state.
- [ ] 1.3 A list containing an unknown project id is rejected with no durable
  change.
- [ ] 1.4 A list containing a duplicate is rejected with no durable change.
- [ ] 1.5 A list that omits a queued project is rejected with no durable change.
- [ ] 1.6 A valid reorder survives a restart with the same order.
- [ ] 1.7 Reordering does not alter the active project, the active project's
  invested progress, or any balance.
- [ ] 1.8 If any of these reveals a genuine defect in `ReorderQueue`, **stop**,
  report it, and open a separate change. Do not patch production code here.

## 2. DequeueProject coverage

- [ ] 2.1 Add cases for dequeuing a queued project: it leaves the queue, its
  status returns to `Available`, it is not the active project, and the state
  validates.
- [ ] 2.2 Dequeuing an unknown project is rejected with no durable change.
- [ ] 2.3 Dequeuing a project that is `Active` rather than `Queued` is covered
  against the current contract (entry removed from the queue; status unchanged).
  Do **not** change that behaviour in this change.
- [ ] 2.4 Vitality already invested toward a dequeued project is neither lost
  nor redirected: it returns to the unallocated bank and is reported as banked
  when the queue is idle.
- [ ] 2.5 A dequeue survives a restart.
- [ ] 2.6 The resulting state passes `GameStateValidator` — in particular the
  queue/status consistency rule.
- [ ] 2.7 If any of these reveals a genuine defect in `DequeueProject`, **stop**,
  report it, and open a separate change.

## 3. WithNotice coverage

- [ ] 3.1 Add direct tests for
  `src/WalkGame.Application/Summaries/ReturnSummaryComposer.cs` `WithNotice`:
  the notice appears, its kind and position follow the documented priority
  order, it is deduped against an identical existing notice, and the result
  stays within `PendingReturnSummaryState.MaxItems`.
- [ ] 3.2 Assert the notice text stays calm and non-technical: it states what
  happened and that progress is intact, without file paths, error codes or
  jargon. `SessionPersistenceHardeningTests` currently asserts only the item
  count.
- [ ] 3.3 `WithNotice` on a null existing state produces a valid single-item
  summary.

## 4. No-leak sweep coverage

- [ ] 4.1 In
  `tests/WalkGame.Application.Tests/M5H1ContractHardeningTests.cs`, add
  `GetDiscoveries()`, `GetExpeditions()` and `GetPendingReturnSummary()` to the
  existing explicit assertion list in
  `PlayerFacingSurfaces_NeverLeakRawExceptionTextOrHealthPayloads`.
- [ ] 4.2 Confirm the extended sweep passes (these read models currently carry
  only content keys, so no marker can appear).
- [ ] 4.3 Note in the change description that the durable, reflection-driven
  sweep lives in `surface-ingestion-rejection-reasons` task 5.1; do not
  implement it here.

## 5. Enumeration guard against future gaps

- [ ] 5.1 Add a test that reflects over `WalkGame.Application.GameSession`'s
  public instance members and, for each one that is a public, parameterless,
  non-special, side-effect-free operation, asserts its name appears somewhere in
  the test project's source files.
- [ ] 5.2 Exclude deliberately: constructors, property getters, backing
  fields, operators, and any member that mutates durable state when called
  (the guard must never invoke an operation to check it).
- [ ] 5.3 Provide a small, visible allow-list with a required reason per entry,
  so a consciously untested operation is a reviewable act.
- [ ] 5.4 Make the failure message name the uncovered member and the file
  expected to contain its test.
- [ ] 5.5 Prove the guard works: temporarily add a public parameterless use case
  to `GameSession` with no test, assert the guard fails, then remove it. Do not
  ship an unproven guard.

## 6. Verification

- [ ] 6.1 `dotnet build SimpleWalkGame.sln` — zero errors, no new warnings.
- [ ] 6.2 `dotnet test` — all suites green, with the Application suite count
  increased by the new cases.
- [ ] 6.3 Confirm **no production file is modified** by this change
      (`git diff --stat -- src tools` is empty).
- [ ] 6.4 `scripts/verify-architecture.sh` — 11 passed, 0 failed.
- [ ] 6.5 `tests/guards/run-guard-tests.sh` — green.
- [ ] 6.6 `openspec validate close-application-use-case-test-gaps --strict`
      passes.
- [ ] 6.7 `git status` clean apart from intended files; writer lease released.
