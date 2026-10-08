## 1. Durable per-reason rejection breakdown

- [x] 1.1 In `src/WalkGame.Domain/Activity/IngestionOutcomeState.cs`, add an
  additive bounded breakdown: a map from rejection-reason **ordinal** to count.
  Do not persist enum member names.
- [x] 1.2 Add an explicit representation distinguishing "no rejections
  occurred" (empty breakdown, recorded) from "breakdown not recorded"
  (absent on an older save).
- [x] 1.3 Add the three dispositions the batch result already computes but the
  durable outcome omits: deletion markers ignored, stale revisions ignored, and
  records whose quantity was bounded.
- [x] 1.4 Do **not** bump `SchemaVersions.Current` and do **not** add an
  `ISaveMigration`: every field is additive and absent-decodes to the correct
  older meaning.
- [x] 1.5 In `src/WalkGame.Application/GameSession.cs` `IngestActivityBatch` and
  `IngestFromSource`, populate the new fields on every path that writes
  `LastIngestionOutcome` — including the fetch-failure path, which must still
  record the failure classification and must not clear prior credited progress.

## 2. Reconciliation state

- [x] 2.1 Keep the existing whole-day `CheckpointWatermarkAgeDays` unchanged;
  `M5H1ShellAcceptanceTests.Scenario05` asserts it is in `[28, 31]` after a
  30-day absence and that assertion must keep passing.
- [x] 2.2 Add a classified reconciliation state to
  `DiagnosticsReadModel`: never attempted / current / stale /
  failed-since-last-success. Derive it from durable evidence, not from a
  comparison against a possibly-skewed wall clock.
- [x] 2.3 Choose and document the staleness threshold: comfortably above any
  normal polling interval, comfortably below the delay at which a player would
  notice missing activity. Record it in `docs/ACTIVITY_PIPELINE.md` §14.
- [x] 2.4 Ensure a backward clock cannot make a stale source appear current.

## 3. Validator

- [x] 3.1 In `src/WalkGame.Domain/Validation/GameStateValidator.cs`, add: every
  durable ingestion counter is non-negative; the breakdown's sum equals the
  recorded rejection total; the accepted count does not exceed the total
  received; a recorded breakdown's keys are within the defined reason range or
  are tolerated as unknown on read.
- [x] 3.2 Report each violation with enough detail to identify the offending
  durable record.
- [x] 3.3 Add `GameStateValidationTests` cases for each violation.

## 4. Read models

- [x] 4.1 Expose the breakdown and the classified reconciliation state on
  `DiagnosticsReadModel` and `DiagnosticsIngestionRow` as enums and counts
  only. No strings from provider payloads.
- [x] 4.2 Confirm the projection's length-bounding of adapter/preferences
  technical detail (`GameSession.BoundDetail`) still applies to every new
  string-bearing field, and bound any new one.
- [x] 4.3 Add a test asserting the projection's rejection-reason set is stable,
  so a future enum reorder or removal is caught.

## 5. Exhaustive no-leak sweep

- [x] 5.1 In
  `tests/WalkGame.Application.Tests/M5H1ContractHardeningTests.cs`, replace the
  hand-maintained list of six read models with a reflection-driven sweep that
  enumerates every parameterless, side-effect-free read-model-producing member
  of a session. Skip primitives, collections of primitives, and any member that
  mutates.
- [x] 5.2 Add an explicit allow-list for the two support-only surfaces
  (diagnostics, adapter technical detail) where a bounded, gated string may
  legitimately appear.
- [x] 5.3 Prove the sweep fails: introduce a deliberately-leaking read model in
  a scratch fixture, assert the sweep catches it, then remove it. Do not ship
  the sweep unproven.
- [x] 5.4 Also add the three currently-missing read models explicitly to the
  existing assertion list (`GetDiscoveries()`, `GetExpeditions()`,
  `GetPendingReturnSummary()`), as the sibling backup-recovery change also
  requires.

## 6. Tests

- [x] 6.1 New: the per-reason breakdown survives a restart unchanged.
- [x] 6.2 New: benign-only rejections report exactly those reasons and no
  bounded-window or clock-related reason.
- [x] 6.3 New: the breakdown is bounded in size regardless of batch size.
- [x] 6.4 New: "no rejections" is distinguishable from "not recorded".
- [x] 6.5 New: a save with no recorded breakdown decodes and reports "not
  recorded".
- [x] 6.6 New: durable counters reconcile against the batch total received.
- [x] 6.7 New: reconciliation state is `never attempted` on a fresh profile,
  `current` after a recent success, `stale` after a long gap, and
  `failed-since-last-success` after a failed fetch that was not yet followed by
  a success.
- [x] 6.8 New: a backward clock does not make a stale source appear current.
- [x] 6.9 New: a planted provider-error marker reaches no read model, and
  adapter technical detail appears only in the support projection and is
  length-bounded.
- [x] 6.10 Confirm unchanged and still green: `DiagnosticsReadModelTests` (8),
  `ActivityStatusProjectionTests` (9), `SessionIngestionTests` (15),
  `SessionPersistenceHardeningTests` (5), `M4StateValidationTests` (7),
  `M4BackwardDecodingTests` (2), `MatureSaveMigrationTests` (4),
  `M5H1ShellAcceptanceTests` (11).
- [x] 6.11 Extend `SaveCodecRoundtripTests.Encode_Decode_PreservesEveryField`
  for the new durable fields.

## 7. Documentation

- [x] 7.1 `docs/ACTIVITY_PIPELINE.md` §14 — document the per-reason breakdown and
  the classified reconciliation state, including the staleness threshold.
- [x] 7.2 `docs/ACTIVITY_PIPELINE.md` §19 — add the new fields to the
  diagnostics field list.
- [x] 7.3 `docs/DECISIONS.md` — record ordinal-keyed rejection reasons, the
  classified reconciliation state, and the sweep-by-reflection approach.
- [x] 7.4 `docs/PRIVACY_REVIEW.md` — update the data inventory for the new
  durable fields and re-run the review's mechanical commands, confirming the
  zero-network grep still returns nothing.
- [x] 7.5 `docs/RISK_REGISTER.md` R-003 — add the evidence that a lost-activity
  report is now diagnosable from durable state, stating what remains
  device-blocked.

## 8. Verification

- [x] 8.1 `dotnet build SimpleWalkGame.sln` — zero errors, no new warnings.
- [x] 8.2 `dotnet test` — all suites green.
- [x] 8.3 `scripts/verify-architecture.sh` — 11 passed, 0 failed.
- [x] 8.4 `scripts/verify-pacing-evidence.sh` — seven reproduced, 0 drifted.
- [x] 8.5 `scripts/verify-save-growth.sh` — growth series reproduces.
- [x] 8.6 `tests/guards/run-guard-tests.sh` — green.
- [x] 8.7 `openspec validate surface-ingestion-rejection-reasons --strict`
  passes.
- [x] 8.8 `git status` clean apart from intended files; writer lease released.

---

## Implementation record (added during apply, 2026-10-09)

1. **Ordinal keying, with an unmapped bucket.** `IngestionOutcomeState.RejectionCountsByReason`
   maps the ORDINAL of `ActivityValidationStatus` to a count, as specified. A rejection name the
   current enum does not define keeps its count under an explicit unmapped ordinal (`-1`) rather
   than being dropped, so the breakdown always sums to the recorded rejection total — the
   invariant the validator enforces.

2. **`null` vs empty is load-bearing, and it cost one test to get right.** STJ cannot distinguish
   an absent dictionary from an empty one, so the field is NULLABLE: `null` = "not recorded"
   (a save written before this change), empty = "a batch ran and rejected nothing".
   `SupportDiagnosticsTests` proves both readings on the same profile by surgically clearing the
   field in a saved payload.

3. **A failed fetch preserves the previous batch's facts.** The original code replaced the whole
   row on a fetch failure, which would have zeroed counters and credited progress that were
   never in question. The new row carries the prior counters and credited total forward and adds
   `LastFetchAttemptedUtc` with a stale `LastFetchSucceededUtc` — which is also what makes
   `FailedSinceLastSuccess` derivable.

4. **One validator rule was written, bricked a valid correction, and was removed.**
   `VitalityCredited` was first checked for non-negativity. A negative correction legitimately
   claws value back, so `SessionIngestionTests.CorrectionDown_ClampsToUnspentBalance_...` failed
   closed at the next boot (`RecoveredFromBackup` instead of `Loaded`) — a mature profile
   rejected by an invariant that did not understand its own field. The rule is gone and the
   reason is recorded in D-063 so it does not come back.

5. **The sweep's surface filter had to learn one namespace.** Reflection over
   `WalkGame.Application.ReadModels` found 9 of the 10 surfaces: `GetActivityStatus` returns
   `ActivityStatusReadModel`, which lives in `WalkGame.Application.Activity`. The filter covers
   both namespaces and the test asserts the ten expected names by name, so a surface that moves
   namespace is noticed rather than silently dropped from the sweep.

6. **The verdict-in-the-harness interaction, recorded because it bit once already.**
   `PacingContract` (D-062) made `profile` exit 2 on an unclosed profile. `verify-content-
   compatibility.sh` uses the same verb to GENERATE short saves, so those runs began failing as
   "balance defects". The verdict now requires the run to have reached the contracted horizon;
   `SupportDiagnosticsTests` covers that boundary.
