## 1. Trusted-time reference in the domain

- [ ] 1.1 In `src/WalkGame.Domain/Activity/ActivityIngestion.cs`, add an
  immutable trusted-time value carrying: the injected current time, the latest
  activity end already durably trusted, and the reconciliation watermark.
  Keep it in Domain with no dependency on `GameState` so `Validate` stays pure.
- [ ] 1.2 Change `ActivityValidationPolicy.Validate` to take that value instead of
  a bare `nowUtc`. Keep the method `static`, side-effect free and O(1) per
  record, and keep every existing `ActivityValidationStatus` member.
- [ ] 1.3 Define the acceptance window as: lower bound = the earlier of the
  watermark and the latest trusted end, minus a new documented floor constant;
  upper bound = the later of the watermark, the latest trusted end, and the
  current time, plus the skew allowance. Choose and record the floor value so
  that it comfortably exceeds the longest absence the product advertises
  (MASTER_PLAN §10) and remains far below unbounded history.
- [ ] 1.4 Keep the `OutsideHorizon` and `FutureTimestamp` status names and their
  distinct meanings. The status a record receives for being outside the trusted
  window is still `OutsideHorizon`.
- [ ] 1.5 Add an XML doc comment on both constants stating, in one sentence
  each, what reference point they are compared against and why.

## 2. Batch loop and watermark

- [ ] 2.1 In `src/WalkGame.Application/GameSession.cs`
  `IngestActivityBatch`, compute the latest trusted record end once before the
  loop by taking the maximum end implied by `game.ProcessedRecords` combined
  with `game.IngestionCheckpointUtc`. Do not compute it per record.
- [ ] 2.2 Build the trusted-time value once per batch and pass it to every
  `ActivityValidationPolicy.Validate` call, including for deletion markers.
- [ ] 2.3 Restructure the loop so eligibility is evaluated for **every** record
  first, and the deletion handler is dispatched only for records that passed
  eligibility. Preserve the existing post-eligibility policy checks inside
  `ProcessDeletion` (empty namespace, unknown logical record, stale revision).
- [ ] 2.4 Ensure a rejected deletion marker cannot reach
  `game.ProcessedRecords.Update` and cannot advance `maxTrustedEndUtc`.
- [ ] 2.5 Preserve the existing exactly-once guarantees: a correction and a
  deletion must still derive distinct stable transaction identities, and the
  processed-record row must still track net **applied** vitality so dedup state
  can never outrun reward state.

## 3. Batch diagnostics consistency

- [ ] 3.1 Make `IngestResult.RejectionCounts` complete and readable on the
  immutable result for every reason present in the batch.
- [ ] 3.2 Verify and assert the accounting identity: total received equals
  accepted plus the sum of all rejection reasons plus duplicates plus
  corrections plus applied deletions plus ignored deletions plus stale
  revisions. If the current counters cannot satisfy this without double
  counting, adjust the counter definitions and document the change here.
- [ ] 3.3 Do not add persistence of the per-reason breakdown in this change;
  that belongs to the follow-up diagnostics change.

## 4. Tests

- [ ] 4.1 Re-scope
  `SessionIngestionTests.RecordBeyondReconciliationHorizon_IsRejectedWithoutSideEffects`
  to a record genuinely outside the new trusted window, preserving the original
  intent (rejection, no state change, no watermark move). Do not delete it.
- [ ] 4.2 Re-scope
  `SessionPersistenceHardeningTests.DeletionMarker_WithoutIdentifiableNamespace_IsCountedInDiagnostics`
  for the new eligibility-then-policy split: an empty namespace is still an
  ignored deletion; a malformed timestamp is now a rejected deletion. Cover
  both.
- [ ] 4.3 New: 30-day absence, provider returns the full 30-day backlog in one
  batch on return → all accepted, credited exactly once, watermark at the last
  end, and a full replay of the same batch credits nothing further.
- [ ] 4.4 New: 45-day absence with a stale `save.tmp` and a restarted session
  → same outcome (crosses the old 14-day bound and the store's crash window).
- [ ] 4.5 New: device clock 2 hours fast, records not ahead of already-trusted
  progress → accepted, and `FutureTimestamp` is not reported.
- [ ] 4.6 New: record genuinely ahead of the trusted reference by more than the
  skew allowance → rejected with `FutureTimestamp`, nothing credited, watermark
  unchanged.
- [ ] 4.7 New: forward clock jump during absence → backlog credited on normal
  terms, and the pipeline never mints Vitality the source did not report.
- [ ] 4.8 New: backward clock movement → upper bound never moves below the
  already-trusted end, watermark never regresses. Extend the existing
  `TemporalAnomalyTests.BackwardWallClock_Ignored_WithoutCheckpointOrProgressRegression`
  rather than duplicating it.
- [ ] 4.9 New: hostile far-old dump mixed with in-window records → old records
  rejected under `OutsideHorizon`, in-window records still processed.
- [ ] 4.10 New: deletion markers with a malformed timestamp window, an empty
  provider namespace, and a far-future end → each rejected or ignored per the
  split above, none reverses value, none moves the watermark.
- [ ] 4.11 New: an entire batch rejected → one entry per distinct reason, zero
  credited, and the accounting identity holds.
- [ ] 4.12 Extend `ActivityIngestionTests` (13 tests) to call `Validate` with the
  new trusted-time value and cover the boundary at exactly the floor and
  exactly the skew allowance, mirroring the existing
  `TemporalAnomalyTests.HorizonAndSkewBoundaries_AreDecidedExactlyAtTheDocumentedEdges`
  style.

## 5. Evidence and documentation

- [ ] 5.1 `scripts/verify-pacing-evidence.sh` — all seven
  `docs/evidence/m4/pacing-*.txt` reports must still reproduce byte-for-byte.
  If any changes, stop and rework the window change; do not regenerate evidence.
- [ ] 5.2 `scripts/verify-save-growth.sh` — the growth series must reproduce
  exactly; commit no new numbers unless the implementation genuinely changed
  record counts.
- [ ] 5.3 Add a `docs/DECISIONS.md` entry superseding D-030's constants and
  reference point, recording the trusted-time anchor, the chosen floor, and the
  reason.
- [ ] 5.4 Update `docs/ACTIVITY_PIPELINE.md` §5, §10 and §12 to describe the
  trusted anchor instead of "14 days before now" / "10 minutes after now".
- [ ] 5.5 Update `docs/KNOWN_ISSUES.md` (close the KI entry for the R-003
  long-absence loss) and add an evidence paragraph to `docs/RISK_REGISTER.md`
  R-003 stating exactly which scenario is now covered headlessly and which
  remain device-blocked.

## 6. Verification

- [ ] 6.1 `dotnet build SimpleWalkGame.sln` — zero errors, no new warnings.
- [ ] 6.2 `dotnet test` — all suites green; confirm
  `M3AmbientProgressionAcceptanceTests` and `M4Region1AcceptanceTests` still
  assert and achieve byte-identical deterministic state.
- [ ] 6.3 `scripts/verify-architecture.sh` — 11 passed, 0 failed (Domain gained
  no dependency and no filesystem access).
- [ ] 6.4 `tests/guards/run-guard-tests.sh` — green.
- [ ] 6.5 `openspec validate anchor-activity-acceptance-window-to-trusted-time --strict`
  passes.
- [ ] 6.6 `git status` clean apart from intended files; writer lease released.
