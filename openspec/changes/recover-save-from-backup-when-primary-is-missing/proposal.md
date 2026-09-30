# Recover a save from the backup generation when the primary slot is missing

## Why

`GameSession.Continue()` reports "no save found" the moment the primary save
slot is absent, without ever reading the healthy backup generation that sits
beside it. Any composition root that treats "no save found" as a fresh profile
then starts a brand new empty world and overwrites the only surviving copy of
the player's progress. This is a total-loss path in the flagship product
invariant (progress must never be silently destroyed), and it is reproducible
today with the committed CLI.

## What Changes

- `GameSession.Continue()` consults the backup generation when the primary slot
  is **absent**, not only when the primary slot is present but undecodable.
- `StartStatus.NoSaveFound` is returned only when **both** generations are
  absent. A missing primary with a decodable backup returns
  `StartStatus.RecoveredFromBackup`.
- The boot path records the missing-primary condition as a distinct, durable
  evidence value in the support diagnostics projection so a support engineer can
  tell "crash between delete and move" apart from "user deleted the file" and
  apart from "primary was corrupt".
- A recovery commit made from a backup-only boot must not rotate the (absent)
  primary over the recovered backup: the existing
  `ISaveStore.WriteAtomicPreservingBackup` contract already covers this and is
  already used by the corrupt-primary recovery path; it is now used by the
  missing-primary path too.
- Regression coverage at the **application** layer for the exact state the store
  can produce: primary slot deleted, stale `save.tmp` present, healthy backup
  present.

**Non-goals:** changing the `AtomicFileSaveStore` write sequence, adding a
second backup generation, adding a journal, or changing save schema/envelope
framing. Those are separate concerns and are not required to close this defect.

## Capabilities

### New Capabilities

- `save-recovery`: the durable save store's read/commit protocol as observed
  through the application boot flow: primary and backup generation addressing,
  integrity classification, backup fallback (including the absent-primary case),
  fail-closed behaviour when no generation is readable, and the durable boot
  evidence that makes each recovery class diagnosable.

### Modified Capabilities

<!-- None. `openspec/specs/` is empty at HEAD; `save-recovery` is introduced here. -->

## Impact

**Affected code**

- `src/WalkGame.Application/GameSession.cs` — `Continue()`, `FinishBoot()`,
  `CaptureBootOutcome()`, `DiagnosticsBootOutcome` enum.
- `src/WalkGame.Application/ReadModels/DiagnosticsReadModel.cs` — new
  `DiagnosticsBootOutcome` member and the corresponding row property.
- `src/WalkGame.Infrastructure/Persistence/AtomicFileSaveStore.cs` — no
  behaviour change; only documentation cross-reference.

**Affected tests**

- New: `tests/WalkGame.Application.Tests/SessionRecoveryAndSummaryTests.cs`
  (or a new dedicated file if the file grows unwieldy) for the missing-primary
  recovery path and the both-absent path.
- New: a case in `tests/WalkGame.Application.Tests/SessionBootTests.cs`
  asserting that a healthy backup is never orphaned.
- Must keep passing unchanged: `SessionBootTests` (5 tests),
  `SessionPersistenceHardeningTests` (5 tests),
  `DiagnosticsReadModelTests` (8 tests),
  `M5H1ShellAcceptanceTests.Scenario09_SaveRecoveryUsed_*`,
  `M8H1HardeningAcceptanceTests`,
  `PersistenceFaultInjectionTests` (10 tests).

**Evidence reproduced during the audit**

```text
$ dotnet run --project tools/simulation -- walk --save $T --days 8 \
      --steps-per-day 12000 --at 2026-08-20T08:00:00Z
# 8 days of world progress committed; save.json + save.backup.json present
$ rm $T/save.json          # the exact Delete/Move window documented in
                           # AtomicFileSaveStore.ReplaceFile
$ dotnet run --project tools/simulation -- dump --save $T
ERROR: no save found (run 'new' first).        # exit 3; backup never read
$ dotnet run --project tools/simulation -- walk --save $T --days 2 \
      --steps-per-day 12000 --at 2026-08-28T08:00:00Z
[dev] started fresh game seed=42               # total loss of the 8-day world
```

**Dependencies**

- None. This is the first change to land.
- `classify-boot-outcome-taxonomy` (sibling proposal) touches the same enum and
  `Continue()`; apply this change first, then the taxonomy cleanup on top.
