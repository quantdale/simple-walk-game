## Context

The storage layer already guarantees that a healthy generation survives. See
proposal.md (Why) for the reproduction.

What exists today and constrains the fix:

- `AtomicFileSaveStore.ReplaceFile` is a delete-then-move pair, because
  `netstandard2.1` has no `File.Move(src, dst, overwrite)`. Its own comment
  states: "a crash between the two steps leaves the previous generation in
  backup". The store author therefore already knew the primary slot can be
  transiently absent, and relied on the backup covering it.
- `AtomicFileSaveStore.ReadFile` deliberately distinguishes "absent" from
  "present but inaccessible" via `ClassifyInaccessible`, and the comment there
  states the rule: "'no save found' must mean the save is really not there,
  never 'we could not look'". The store therefore also already knows absence
  and unreadability are different facts.
- `GameSession.Continue()` violates both of those store-level invariants. Its
  first statement after reading the primary is an early return on
  `SaveReadOutcome.NotFound`, and `SafeReadBackup()` is not reached on that
  path.
- `FinishBoot(state, recoveredFromBackup: true)` already routes to
  `ISaveStore.WriteAtomicPreservingBackup` and already prepends the calm
  "Your latest save was damaged; the most recent healthy backup was restored."
  notice. The missing-primary path is the same situation with a different
  cause, so the existing recovery machinery is directly reusable.
- `PersistenceFaultInjectionTests.CrashDuringPromotion_DeletedPrimary_StaleTempAndValidBackup_RecoverFromBackup`
  already reproduces and asserts the exact store-level state (primary deleted,
  stale temp present, healthy backup readable). The gap is that no
  **application**-layer test exercises `GameSession.Continue()` against that
  state, which is why the defect survived a hardening campaign that
  specifically covered crash-during-promotion.
- `StartStatus` already declares `StateInvalid = 5` and
  `DiagnosticsBootOutcome` already declares `StateInvalid = 6`, but nothing
  produces either. `Continue()` folds a `GameStateValidator` failure into
  `SaveUnreadable`, and `tools/simulation/Commands.cs` has an unreachable
  `start.Status == StartStatus.StateInvalid ? 2 : 3` branch that consequently
  returns exit code 3 (documented as "save missing/unreadable") for what is
  actually a data-integrity failure.

## Goals / Non-Goals

**Goals**

- Make the application boot flow honour the store's own recovery guarantee for
  the absent-primary case, with no change to the store's write sequence.
- Give each boot failure class its own reachable, machine-readable outcome, so
  composition roots, diagnostics and the CLI stop collapsing distinct failures.
- Close the application-layer coverage hole that let this defect survive the
  existing persistence hardening campaign.

**Non-Goals**

- Changing the write sequence, adding a second backup generation, or adding a
  write-ahead journal. Those reduce the probability of the state; this change
  makes the state recoverable. Journal design is a separate, larger decision.
- Redesigning diagnostics. The existing bounded, privacy-safe projection
  (`DiagnosticsReadModel`) is correct; it gains one enum member and one
  property.
- Any change to save schema, envelope framing, checksum algorithm or migration
  chain.

## Decisions

### 1. Read the backup whenever the primary is unavailable, including when absent

The early return in `Continue()` becomes a two-stage probe: read the primary;
if it yields bytes, decode and validate it; then, independently, read the
backup and decode and validate it. `NoSaveFound` is produced only when the
primary probe classified as absent **and** the backup probe classified as
absent.

*Alternative considered:* leave `Continue()` alone and have every composition
root probe the backup itself. Rejected: it duplicates the recovery protocol in
N call sites, the `Simulation`/`Bench` harnesses already get it wrong by
defaulting to fresh start, and it puts a data-loss decision outside the one
place that owns recovery policy. The `ISaveStore` port comment already says the
application layer owns recovery policy.

### 2. Reuse `WriteAtomicPreservingBackup` for the missing-primary path

Recovery from an absent primary must not rotate anything into the backup slot.
There is no primary to rotate, so the current `WriteAtomic` would simply skip
the rotation — but relying on that is fragile: a later `WriteAtomic` after the
primary has been re-created would then rotate the recovered generation into
backup, which is correct. Routing through the explicit
`WriteAtomicPreservingBackup` keeps the intent legible and keeps the D-040
invariant uniform across both recovery causes.

The calm recovery notice text is parameterised on the cause so the player-facing
message stays truthful ("your latest save was damaged" is inaccurate when the
primary file was simply missing) while remaining calm and non-technical.

*Alternative considered:* a new `ISaveStore` method for "no primary at all".
Rejected: the existing method's contract ("commit primary WITHOUT rotating the
current primary into the backup slot; existing backup bytes stay untouched")
already matches exactly, and adding a port method for a case it already covers
widens the surface every store implementation must satisfy.

### 3. Model the cause, not the symptom, in diagnostics

Add a single `DiagnosticsBootOutcome` value meaning "the primary generation was
absent and the backup generation was used". `StartStatus` needs no new value:
from the caller's point of view, recovering a world is recovering a world, and
`StartStatus.RecoveredFromBackup` already says so. The finer distinction belongs
in diagnostics, which is the surface documented for exactly this purpose
(`ACTIVITY_PIPELINE` §14, D-044).

### 4. Make the declared outcome taxonomy exhaustive and reachable

Because the spec now requires every declared outcome to be reachable, this
change also wires up `StartStatus.StateInvalid` and
`DiagnosticsBootOutcome.StateInvalid` for the state-validation path, and fixes
the CLI exit code. This is deliberately bundled with the recovery fix rather
than deferred: the spec requirement is about the taxonomy as a whole, and
splitting it would leave the spec temporarily untrue. The change is small —
one branch in `Continue()`, one enum value in each enum, one branch in
`Commands.Boot`.

*Alternative considered:* delete the unused enum members instead of wiring them.
Rejected: `StateInvalid` is genuinely a distinct and useful class — "the file
is fine, the contents are not" — and it is what a future support tool needs to
tell a player apart from a corrupt download. Deleting it would also leave the
CLI's existing exit-code-2 branch permanently dead.

### 5. Boot-outcome evidence is in-memory, and that is correct

Boot evidence (`_lastBootOutcome`, `_lastBootCodecFailure`,
`_lastBootAppliedMigrations`) stays in the session object and is not persisted
into `GameState`. Persisting it would mean a second write during a boot that is
already recovering, and would make a "damage happened" fact durable in a way
that no other session would ever read. It is available through
`GetDiagnostics()` on the session that performed the boot, which is the only
caller that can need it.

## Risks / Trade-offs

- **[A missing primary is now silently recovered, so a genuinely deleted save
  looks like a crash]**
  → The distinct `DiagnosticsBootOutcome` value plus the existing
  `StartStatus.RecoveredFromBackup` and `DiagnosticsReadModel.RecoveredFromBackup`
  flag make the distinction available to support. Recovery is the safe default
  for a game whose core promise is "your world is never destroyed".

- **[Recovering from a backup can lose the newest generation]**
  → Unavoidable and already the accepted trade for the corrupt-primary path
  (D-040). The backup is by construction the previous successful commit, so the
  worst case is losing the changes since that commit, not the world.

- **[New `Continue()` branching could break existing boot tests]**
  → `SessionBootTests.Continue_WithoutAnySave_ReturnsNoSaveFound` keeps passing
  because it provisions no files at all, so both slots are absent. The
  corrupt-primary and both-corrupt cases already expect backup fallback and
  keep passing. Task 3.1 requires running the full suite before merge.

- **[Wiring `StateInvalid` changes CLI exit codes]**
  → Only in the previously-unreachable state-validation case, which today
  reports exit 3 ("save missing/unreadable"). Exit 2 ("domain/validation
  failure") is the documented meaning for exactly this. Documented in
  `tools/simulation` usage text and `TESTING_AND_RELEASE` §16 if the exit-code
  table is restated there.

## Migration Plan

No data migration. No save schema change. The change is entirely inside the boot
decision logic plus one diagnostics enum member and its projection property.

Rollback is a plain revert; no state written by the fixed build needs
interpretation by the previous build, because nothing about the persisted shape
changes.

## Open Questions

None. Every choice above is settled; the remaining decisions in this area
(multi-generation history, journal-based commit) are explicitly out of scope
and are recorded in the master plan's deferred list rather than as open
questions here.
