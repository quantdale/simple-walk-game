## 1. ReorderQueue coverage

- [x] 1.1 Add cases to
  `tests/WalkGame.Application.Tests/SessionQueueControlTests.cs` for a valid
  permutation of two or more queued projects: the persisted order matches the
  requested order, and every queued project is still present exactly once.
- [x] 1.2 A list with the wrong length is rejected and **changes nothing
  durable** — decode the save and compare to the pre-call state.
- [x] 1.3 A list containing an unknown project id is rejected with no durable
  change.
- [x] 1.4 A list containing a duplicate is rejected with no durable change.
- [x] 1.5 A list that omits a queued project is rejected with no durable change.
- [x] 1.6 A valid reorder survives a restart with the same order.
- [x] 1.7 Reordering does not alter the active project, the active project's
  invested progress, or any balance.
- [x] 1.8 If any of these reveals a genuine defect in `ReorderQueue`, **stop**,
  report it, and open a separate change. Do not patch production code here.

## 2. DequeueProject coverage

- [x] 2.1 Add cases for dequeuing a queued project: it leaves the queue, its
  status returns to `Available`, it is not the active project, and the state
  validates.
- [x] 2.2 Dequeuing an unknown project is rejected with no durable change.
- [x] 2.3 Dequeuing a project that is `Active` rather than `Queued` is covered
  against the current contract (entry removed from the queue; status unchanged).
  Do **not** change that behaviour in this change.
- [x] 2.4 Vitality already invested toward a dequeued project is neither lost
  nor redirected: it returns to the unallocated bank and is reported as banked
  when the queue is idle.
- [x] 2.5 A dequeue survives a restart.
- [x] 2.6 The resulting state passes `GameStateValidator` — in particular the
  queue/status consistency rule.
- [x] 2.7 If any of these reveals a genuine defect in `DequeueProject`, **stop**,
  report it, and open a separate change.

## 3. WithNotice coverage

- [x] 3.1 Add direct tests for
  `src/WalkGame.Application/Summaries/ReturnSummaryComposer.cs` `WithNotice`:
  the notice appears, its kind and position follow the documented priority
  order, it is deduped against an identical existing notice, and the result
  stays within `PendingReturnSummaryState.MaxItems`.
- [x] 3.2 Assert the notice text stays calm and non-technical: it states what
  happened and that progress is intact, without file paths, error codes or
  jargon. `SessionPersistenceHardeningTests` currently asserts only the item
  count.
- [x] 3.3 `WithNotice` on a null existing state produces a valid single-item
  summary.

## 4. No-leak sweep coverage

- [x] 4.1 In
  `tests/WalkGame.Application.Tests/M5H1ContractHardeningTests.cs`, add
  `GetDiscoveries()`, `GetExpeditions()` and `GetPendingReturnSummary()` to the
  existing explicit assertion list in
  `PlayerFacingSurfaces_NeverLeakRawExceptionTextOrHealthPayloads`.
- [x] 4.2 Confirm the extended sweep passes (these read models currently carry
  only content keys, so no marker can appear).
- [x] 4.3 Note in the change description that the durable, reflection-driven
  sweep lives in `surface-ingestion-rejection-reasons` task 5.1; do not
  implement it here.

## 5. Enumeration guard against future gaps

- [x] 5.1 Add a test that reflects over `WalkGame.Application.GameSession`'s
  public instance members and, for each one that is a public, parameterless,
  non-special, side-effect-free operation, asserts its name appears somewhere in
  the test project's source files.
- [x] 5.2 Exclude deliberately: constructors, property getters, backing
  fields, operators, and any member that mutates durable state when called
  (the guard must never invoke an operation to check it).
- [x] 5.3 Provide a small, visible allow-list with a required reason per entry,
  so a consciously untested operation is a reviewable act.
- [x] 5.4 Make the failure message name the uncovered member and the file
  expected to contain its test.
- [x] 5.5 Prove the guard works: temporarily add a public parameterless use case
  to `GameSession` with no test, assert the guard fails, then remove it. Do not
  ship an unproven guard.

## 6. Verification

- [x] 6.1 `dotnet build SimpleWalkGame.sln` — zero errors, no new warnings.
- [x] 6.2 `dotnet test` — all suites green, with the Application suite count
  increased by the new cases.
- [x] 6.3 Confirm **no production file is modified** by this change
      (`git diff --stat -- src tools` is empty).
- [x] 6.4 `scripts/verify-architecture.sh` — 11 passed, 0 failed.
- [x] 6.5 `tests/guards/run-guard-tests.sh` — green.
- [x] 6.6 `openspec validate close-application-use-case-test-gaps --strict`
      passes.
- [ ] 6.7 `git status` clean apart from intended files; writer lease released.

---

## Implementation record (added during apply)

1. **Task 2.3 described `DequeueProject` on an Active project incorrectly.** It said "entry
   removed from the queue; status unchanged". The real contract is that the call is
   **refused** with `NotQueued`, because `ActivateQueuedProject` has already removed the
   project from the queue. That is the better behaviour (an Active project holds invested
   Vitality; refusing the call is what stops that money being orphaned), so the test pins
   the real contract and the discrepancy is recorded in D-056 rather than "fixed" to match
   the prose. No production code was changed.

2. **Task 2.4's "returns to the unallocated bank" only holds once the queue is idle.**
   While a project is Active the Vitality is correctly held as that project's progress. The
   test asserts the real conservation explicitly: 120 invested + 200 more = 320 in, the
   300 cost consumed, 20 banked, nothing stranded.

3. **The enumeration guard's first proof attempt passed VACUOUSLY.** The guard test's own XML
   doc comment named the temporary probe, and the guard scans the test sources, so the
   probe "matched". A guard satisfiable by a comment about the guard is not a guard. The
   name was removed from the test file and the proof re-run, producing the real failure
   transcript recorded in D-056. The proof now lives in the decision log precisely so the
   hole cannot reopen.

4. **Region 1 has exactly one entry project**, so three projects cannot be enqueued through
   the public API without first completing a prerequisite chain. The queue tests seed
   canonical state directly instead. That keeps the tests about queue semantics and avoids
   minutes of simulation per case.
