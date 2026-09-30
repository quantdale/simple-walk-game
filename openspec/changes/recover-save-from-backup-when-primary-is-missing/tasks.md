## 1. Boot-flow recovery in the application layer

- [ ] 1.1 In `src/WalkGame.Application/GameSession.cs`, restructure
  `Continue()` so the backup generation is probed whenever the primary is
  unavailable, and `StartStatus.NoSaveFound` is returned only when the primary
  probe reports absent **and** the backup probe reports absent. Keep the
  existing integrity/version/migration/validation ordering for the primary
  unchanged.
- [ ] 1.2 In the same method, return `StartStatus.RecoveredFromBackup` when the
  primary is absent and the backup decodes and validates, and capture the cause
  separately from the existing `recoveredFromBackup` boolean.
- [ ] 1.3 Keep the unreadable-save reason string composed from the primary
  detail first, then the backup detail, so an absent-primary-plus-corrupt-backup
  still reports the backup's specific failure rather than a generic message.
- [ ] 1.4 Confirm `FinishBoot` routes a missing-primary recovery through
  `ISaveStore.WriteAtomicPreservingBackup` (it already does for
  `recoveredFromBackup: true`); do not add a new store method.
- [ ] 1.5 Parameterise the calm recovery notice in `FinishBoot` so its wording
  matches the cause (damaged primary vs absent primary) and both stay calm and
  non-technical. Keep it inside the existing 12-item summary bound.

## 2. Complete the boot outcome taxonomy

- [ ] 2.1 Produce `StartStatus.StateInvalid` from `Continue()` when a save
  decodes with `CodecStatus.Ok` but `GameStateValidator.Validate` returns
  violations, instead of folding it into `StartStatus.SaveUnreadable`.
- [ ] 2.2 Produce `DiagnosticsBootOutcome.StateInvalid` for the same condition,
  and add a new `DiagnosticsBootOutcome` member for "primary absent, backup
  used" (distinct from corrupt-primary recovery).
- [ ] 2.3 Add the matching property to `DiagnosticsReadModel` so
  `GetDiagnostics()` reports the new class; keep every string in the projection
  bounded and keep it free of raw save content.
- [ ] 2.4 In `tools/simulation/Commands.cs`, make the
  `start.Status == StartStatus.StateInvalid ? 2 : 3` branch reachable and add a
  specific stderr message for it; update the usage text if it enumerates the
  exit codes for that case.
- [ ] 2.5 Audit the whole tree for other consumers of `StartStatus` /
  `DiagnosticsBootOutcome` (`tools/simulation/Commands.cs`,
  `src/WalkGame.Application/ReadModels/DiagnosticsReadModel.cs`, the test
  support fakes) and update any exhaustive switch so no outcome is
  mishandled.
- [ ] 2.6 Cross-reference the new behaviour in the XML doc comment on
  `AtomicFileSaveStore` so the store's recovery guarantee and the application
  boot flow are described consistently.

## 3. Regression coverage at the application layer

- [ ] 3.1 New test in
  `tests/WalkGame.Application.Tests/SessionRecoveryAndSummaryTests.cs`:
  primary slot deleted, stale `save.tmp` present, healthy backup present →
  `Continue()` returns `StartStatus.RecoveredFromBackup`, loaded state matches
  the backup's state, and `save.tmp` is not promoted as data.
- [ ] 3.2 New test: primary absent **and** backup absent → `NoSaveFound`, and no
  file is created by the boot.
- [ ] 3.3 New test: primary absent and backup present but corrupt →
  `StartStatus.StateInvalid` is *not* used; the result is the unreadable class
  with the backup's specific reason, and neither slot is overwritten.
- [ ] 3.4 New test: primary absent and backup present but failing
  `GameStateValidator` → `StartStatus.StateInvalid`, and the diagnostics
  projection reports the invalid-state class with the violations.
- [ ] 3.5 New test: the cause recorded in diagnostics distinguishes
  missing-primary recovery from corrupt-primary recovery.
- [ ] 3.6 New test: repeated boots of a missing-primary profile recover the same
  generation and do not accumulate duplicate recovery notices.
- [ ] 3.7 New test in `tests/WalkGame.Application.Tests/SessionBootTests.cs`
  asserting that after a missing-primary boot, the healthy backup generation is
  still readable (never orphaned) and a subsequent normal commit rotates it
  exactly once.
- [ ] 3.8 Extend `M5H1ContractHardeningTests.PlayerFacingSurfaces_NeverLeakRawExceptionTextOrHealthPayloads`
  to also walk `GetDiscoveries()`, `GetExpeditions()` and
  `GetPendingReturnSummary()`, which are currently missing from the sweep.

## 4. Verification

- [ ] 4.1 `dotnet build SimpleWalkGame.sln` — zero errors, zero new warnings.
- [ ] 4.2 `dotnet test` — all suites green, with the Domain 105 /
  Infrastructure 48 / Application 142 baseline now higher. Explicitly confirm
  `SessionBootTests`, `SessionPersistenceHardeningTests`,
  `DiagnosticsReadModelTests`, `M5H1ShellAcceptanceTests` and
  `M8H1HardeningAcceptanceTests` still pass.
- [ ] 4.3 Reproduce the proposal's CLI scenario end to end and confirm the
  second `walk` invocation reports recovery rather than `[dev] started fresh
  game`, and that `dump` shows the pre-loss world.
- [ ] 4.4 `scripts/verify-architecture.sh` — still 11 passed, 0 failed
  (confirms no filesystem access leaked into Application).
- [ ] 4.5 `scripts/verify-save-growth.sh` and
  `scripts/verify-pacing-evidence.sh` — unchanged and still green (this change
  must not perturb any committed byte-reproducible evidence).
- [ ] 4.6 `tests/guards/run-guard-tests.sh` — green.
- [ ] 4.7 Update `docs/KNOWN_ISSUES.md` (remove or close the entry for this
  finding), `docs/RISK_REGISTER.md` R-005, and add a `docs/DECISIONS.md` entry
  recording the application-layer recovery rule and the exhaustiveness
  requirement on the outcome taxonomy.

## 5. Final checks

- [ ] 5.1 `openspec validate recover-save-from-backup-when-primary-is-missing --strict`
  passes.
- [ ] 5.2 `git status` is clean apart from the intended files, and
  `scripts/writer-lease.sh release` has been run.
