## Context

This change is verification-only. No production file is modified, so the
design question is not "how do I implement this" but "how do I make the
coverage gap impossible to reopen".

What exists today:

- `SessionQueueControlTests` covers four of the six queue-related use cases.
  The two uncovered ones are the two with non-trivial input validation
  (`ReorderQueue`) and the only one that performs a reverse state transition
  (`DequeueProject`).
- The no-leak sweep is an explicit list of six method calls followed by a
  reflection walk over the returned model's string properties. It is a good
  test with a structural weakness: nothing connects it to the set of read models
  that exist.

## Goals / Non-Goals

**Goals**

- Cover both untested mutating use cases across their full input space,
  including the negative cases that protect durable state.
- Close the sweep's coverage hole for the three missing read models.
- Make "a new public use case with no test" a suite failure rather than a
  reviewer's memory.

**Non-Goals**

- No production behaviour change, not even a refactor. If writing a test
  reveals a defect in `ReorderQueue` or `DequeueProject`, that defect must be
  reported and fixed in a separate change, not silently patched here.
- No coverage-percentage target. The enumeration guard is about
  *reachability of the test*, not a number.
- Not extending the enumeration guard to production behaviour; it enumerates
  the API surface only.

## Decisions

### 1. Enumerate the session's public API, not a hand-written list

The guard reflects over `GameSession`'s public instance members and, for each
one that is a parameterless, non-special, side-effect-free operation returning a
read model or result, asserts that its name appears in the test project's
sources. A new public mutating use case added without a test therefore fails the
suite.

The reference check is a source-text search over the test tree, which is crude
but exactly right for the question being asked: "is this operation named
anywhere in the tests?". It has no false-positive problem (a name present in the
tests is present for a reason) and no false-negative problem for the case that
matters.

*Alternative considered:* reflection-invoke each operation and assert it does not
throw. Rejected: that would break on operations with required arguments and
would actively *call* mutating operations, changing durable state as a side
effect of a coverage check.

*Alternative considered:* a code-coverage tool with a threshold. Rejected as
disproportionate: the repository already ships `coverlet.collector` but no
gate, and the gap here is not line coverage — `ReorderQueue` and
`DequeueProject` are already executed incidentally by the simulation harnesses
(they are not, but the point stands) without their *contracts* being asserted.
A percentage would not have caught this.

### 2. Assert the durable-state invariant on every rejected input

For both use cases, the important assertion is not just "it returns failure" but
"it changed nothing". A rejected reorder that had already partially rewritten
the queue, or a failed dequeue that had already removed the entry, would be a
silent state corruption. Each negative case therefore decodes the persisted save
and compares it to the pre-call state.

This is the pattern the existing persistence-hardening tests already use
(`SessionPersistenceHardeningTests`), so it is consistent rather than novel.

### 3. Cover the reverse-transition edge explicitly

`DequeueProject` is the only path that moves a project **backwards** in the state
machine (`Queued → Available`). Every other covered path is forward. The test
must assert the resulting status, that the project is not in the queue, that it
is not the active project, and that the state validates.

It must also cover the case where the project is `Active` rather than `Queued`:
the current implementation removes it from the queue without touching the
status. Whether that is the *right* behaviour is a product question, not a test
question — the test asserts the current contract and the change explicitly
forbids "fixing" it here.

### 4. Extend the sweep by adding the three missing call sites

`GetDiscoveries()`, `GetExpeditions()` and `GetPendingReturnSummary()` are added
to the existing explicit assertion list. They currently carry no untrusted
strings (only content keys), so the sweep will pass; the point is that the
invariant is now enforced for them if a future change introduces one.

The reflection-driven sweep is the durable fix, but it lives in the diagnostics
change (`surface-ingestion-rejection-reasons`, task 5.1). Both are done: the
explicit additions here are correct regardless, and the reflection sweep makes
the next read model automatic.

## Risks / Trade-offs

- **[The enumeration guard may flag legitimate operations]**
  → The exclusion rules are explicit and the failure message names the member
  and the file to add a test to. If a new operation is genuinely
  not-worth-testing, it must be added to a visible allow-list with a reason,
  which is a reviewable act rather than a silent omission.

- **[Writing these tests may reveal a real defect]**
  → That is a success of the change, not a failure of it. The instruction is
  explicit: report it, do not fix it here. `ReorderQueue`'s validation is
  correct on inspection (it checks count, membership and duplicates), so the
  expectation is that the tests pass, but the process must be honoured
  regardless.

- **[The source-text reference check depends on the test tree layout]**
  → It scans the test project directory, which is stable. If the layout changes,
  the guard fails loudly rather than silently passing, which is the safe
  direction.

## Migration Plan

None. Test-only change; no production file, no schema, no evidence.

## Open Questions

None.
