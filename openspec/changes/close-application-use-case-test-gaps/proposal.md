# Close the untested public use-case surface

## Why

`GameSession` is the whole application contract the future Unity shell will
call. Two of its public mutating operations have **zero** test references
anywhere in the repository:

```text
$ grep -rl "ReorderQueue"   --include=*.cs tests   -> 0 test files
$ grep -rl "DequeueProject" --include=*.cs tests   -> 0 test files
```

Both mutate durable canonical state and both persist:

- `ReorderQueue` takes a caller-supplied ordering and rewrites
  `game.Queue.QueuedProjectIds`. Its validation (a permutation of the current
  queued set) is the only thing standing between a presentation-layer bug and a
  queue that silently drops or duplicates a player's projects. A bad ordering
  would also permanently corrupt the save.
- `DequeueProject` removes an entry and, when the project is still `Queued`,
  transitions its status back to `Available` — a state-machine edge that no
  other covered path exercises, since every covered path promotes straight
  through `Active`.

Nothing about them is exercised, so nothing about them is protected. Every
other public use case — `EnqueueProject`, `SetAutoAdvance`,
`ActivateQueuedProject`, `MarkDiscoveryReviewed`, `GetProjects`, `GetRegion`,
`GetDiscoveries`, `GetExpeditions`, `AcknowledgeReturnSummary` — has at least
one test. The two gaps are exactly the two operations a drag-to-reorder queue
UI and an "un-queue" button would call, i.e. the two most likely to be wired up
first when the shell lands.

Two smaller gaps in the same family:

- `ReturnSummaryComposer.WithNotice` has no direct test. It is reached only
  indirectly via the backup-recovery path, where its output is asserted as a
  count, not as content.
- The reflection-based no-leak sweep
  (`M5H1ContractHardeningTests.PlayerFacingSurfaces_NeverLeakRawExceptionTextOrHealthPayloads`)
  walks six of the nine player-facing read models. `GetDiscoveries()`,
  `GetExpeditions()` and `GetPendingReturnSummary()` are not swept. That is a
  coverage hole in the test whose entire purpose is to prevent a leak class.

## What Changes

- `ReorderQueue` gains coverage for: a valid permutation, a wrong-length list, a
  list containing an unknown project, a list containing a duplicate, a list
  that omits a queued project, persistence across restart, and the invariant
  that a rejected reorder changes nothing durable.
- `DequeueProject` gains coverage for: a queued project, an unknown project, a
  project that is `Active` rather than `Queued`, the resulting status
  transition, persistence across restart, and that Vitality previously
  banked toward the dequeued project is neither lost nor redirected.
- `ReturnSummaryComposer.WithNotice` gains direct coverage for content, ordering
  within the bound, and the calm, non-technical wording requirement.
- The no-leak sweep is extended to every remaining player-facing read model.
- A guard is added so a **new** public mutating use case cannot be added to the
  session without a test: the suite enumerates the session's public members and
  fails if a new one has no reference in the test sources.

**Non-goals:** no production behaviour change. Nothing here may alter what any
existing operation does; this change only proves the existing behaviour and
closes a coverage gate.

## Impact

**Evidence from the audit**

- `src/WalkGame.Application/GameSession.cs` — `ReorderQueue` and
  `DequeueProject` are public, mutate `game.Queue`, and call `PersistOrThrow()`.
- `grep` over `tests/` returns zero matches for either name.
- `src/WalkGame.Application/Summaries/ReturnSummaryComposer.cs` —
  `WithNotice` is public static; referenced only from `FinishBoot`.
- `tests/WalkGame.Application.Tests/M5H1ContractHardeningTests.cs` — the sweep
  calls `GetActivityStatus`, `GetOnboarding`, `GetSettings`, `GetHome`,
  `GetProjects`, `GetRegion` and nothing else.
- `tests/WalkGame.Application.Tests/SessionQueueControlTests.cs` (7 tests) —
  covers `SetAutoAdvance`, `ActivateQueuedProject`, `GetProjects`, `GetRegion`;
  not `ReorderQueue` or `DequeueProject`.

**Affected tests only.** No production file is modified by this change; the
enumeration guard lives in the test project.

**Affected existing tests**

None are modified except by addition. `SessionQueueControlTests` gains cases.

**Dependencies**

- Independent of every other change. Safe to land at any point in the sequence.
