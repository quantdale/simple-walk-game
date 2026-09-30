## 1. Trusted-time reference in the domain

- [x] 1.1 In `src/WalkGame.Domain/Activity/ActivityIngestion.cs`, add an
  immutable trusted-time value carrying: the injected current time, the latest
  activity end already durably trusted, and the reconciliation watermark.
  Keep it in Domain with no dependency on `GameState` so `Validate` stays pure.
- [x] 1.2 Change `ActivityValidationPolicy.Validate` to take that value instead of
  a bare `nowUtc`. Keep the method `static`, side-effect free and O(1) per
  record, and keep every existing `ActivityValidationStatus` member.
- [x] 1.3 Define the acceptance window as: lower bound = the earlier of the
  watermark and the latest trusted end, minus a new documented floor constant;
  upper bound = the later of the watermark, the latest trusted end, and the
  current time, plus the skew allowance. Choose and record the floor value so
  that it comfortably exceeds the longest absence the product advertises
  (MASTER_PLAN §10) and remains far below unbounded history.
- [x] 1.4 Keep the `OutsideHorizon` and `FutureTimestamp` status names and their
  distinct meanings. The status a record receives for being outside the trusted
  window is still `OutsideHorizon`.
- [x] 1.5 Add an XML doc comment on both constants stating, in one sentence
  each, what reference point they are compared against and why.

## 2. Batch loop and watermark

- [x] 2.1 In `src/WalkGame.Application/GameSession.cs`
  `IngestActivityBatch`, compute the latest trusted record end once before the
  loop by taking the maximum end implied by `game.ProcessedRecords` combined
  with `game.IngestionCheckpointUtc`. Do not compute it per record.
- [x] 2.2 Build the trusted-time value once per batch and pass it to every
  `ActivityValidationPolicy.Validate` call, including for deletion markers.
- [x] 2.3 Restructure the loop so eligibility is evaluated for **every** record
  first, and the deletion handler is dispatched only for records that passed
  eligibility. Preserve the existing post-eligibility policy checks inside
  `ProcessDeletion` (empty namespace, unknown logical record, stale revision).
- [x] 2.4 Ensure a rejected deletion marker cannot reach
  `game.ProcessedRecords.Update` and cannot advance `maxTrustedEndUtc`.
- [x] 2.5 Preserve the existing exactly-once guarantees: a correction and a
  deletion must still derive distinct stable transaction identities, and the
  processed-record row must still track net **applied** vitality so dedup state
  can never outrun reward state.

## 3. Batch diagnostics consistency

- [x] 3.1 Make `IngestResult.RejectionCounts` complete and readable on the
  immutable result for every reason present in the batch.
- [x] 3.2 Verify and assert the accounting identity: total received equals
  accepted plus the sum of all rejection reasons plus duplicates plus
  corrections plus applied deletions plus ignored deletions plus stale
  revisions. If the current counters cannot satisfy this without double
  counting, adjust the counter definitions and document the change here.
- [x] 3.3 Do not add persistence of the per-reason breakdown in this change;
  that belongs to the follow-up diagnostics change.

## 4. Tests

- [x] 4.1 Re-scope
  `SessionIngestionTests.RecordBeyondReconciliationHorizon_IsRejectedWithoutSideEffects`
  to a record genuinely outside the new trusted window, preserving the original
  intent (rejection, no state change, no watermark move). Do not delete it.
- [x] 4.2 Re-scope
  `SessionPersistenceHardeningTests.DeletionMarker_WithoutIdentifiableNamespace_IsCountedInDiagnostics`
  for the new eligibility-then-policy split: an empty namespace is still an
  ignored deletion; a malformed timestamp is now a rejected deletion. Cover
  both.
- [x] 4.3 New: 30-day absence, provider returns the full 30-day backlog in one
  batch on return → all accepted, credited exactly once, watermark at the last
  end, and a full replay of the same batch credits nothing further.
- [x] 4.4 New: 45-day absence with a stale `save.tmp` and a restarted session
  → same outcome (crosses the old 14-day bound and the store's crash window).
- [x] 4.5 New: device clock 2 hours fast, records not ahead of already-trusted
  progress → accepted, and `FutureTimestamp` is not reported.
- [x] 4.6 New: record genuinely ahead of the trusted reference by more than the
  skew allowance → rejected with `FutureTimestamp`, nothing credited, watermark
  unchanged.
- [x] 4.7 New: forward clock jump during absence → backlog credited on normal
  terms, and the pipeline never mints Vitality the source did not report.
- [x] 4.8 New: backward clock movement → upper bound never moves below the
  already-trusted end, watermark never regresses. Extend the existing
  `TemporalAnomalyTests.BackwardWallClock_Ignored_WithoutCheckpointOrProgressRegression`
  rather than duplicating it.
- [x] 4.9 New: hostile far-old dump mixed with in-window records → old records
  rejected under `OutsideHorizon`, in-window records still processed.
- [x] 4.10 New: deletion markers with a malformed timestamp window, an empty
  provider namespace, and a far-future end → each rejected or ignored per the
  split above, none reverses value, none moves the watermark.
- [x] 4.11 New: an entire batch rejected → one entry per distinct reason, zero
  credited, and the accounting identity holds.
- [x] 4.12 Extend `ActivityIngestionTests` (13 tests) to call `Validate` with the
  new trusted-time value and cover the boundary at exactly the floor and
  exactly the skew allowance, mirroring the existing
  `TemporalAnomalyTests.HorizonAndSkewBoundaries_AreDecidedExactlyAtTheDocumentedEdges`
  style.

## 5. Evidence and documentation

- [x] 5.1 `scripts/verify-pacing-evidence.sh` — all seven
  `docs/evidence/m4/pacing-*.txt` reports must still reproduce byte-for-byte.
  If any changes, stop and rework the window change; do not regenerate evidence.
- [x] 5.2 `scripts/verify-save-growth.sh` — the growth series must reproduce
  exactly; commit no new numbers unless the implementation genuinely changed
  record counts.
- [x] 5.3 Add a `docs/DECISIONS.md` entry superseding D-030's constants and
  reference point, recording the trusted-time anchor, the chosen floor, and the
  reason.
- [x] 5.4 Update `docs/ACTIVITY_PIPELINE.md` §5, §10 and §12 to describe the
  trusted anchor instead of "14 days before now" / "10 minutes after now".
- [x] 5.5 Update `docs/KNOWN_ISSUES.md` (close the KI entry for the R-003
  long-absence loss) and add an evidence paragraph to `docs/RISK_REGISTER.md`
  R-003 stating exactly which scenario is now covered headlessly and which
  remain device-blocked.

## 6. Verification

- [x] 6.1 `dotnet build SimpleWalkGame.sln` — zero errors, no new warnings.
- [x] 6.2 `dotnet test` — all suites green; confirm
  `M3AmbientProgressionAcceptanceTests` and `M4Region1AcceptanceTests` still
  assert and achieve byte-identical deterministic state.
- [x] 6.3 `scripts/verify-architecture.sh` — 11 passed, 0 failed (Domain gained
  no dependency and no filesystem access).
- [x] 6.4 `tests/guards/run-guard-tests.sh` — green.
- [x] 6.5 `openspec validate anchor-activity-acceptance-window-to-trusted-time --strict`
  passes.
- [x] 6.6 `git status` clean apart from intended files; writer lease released.

---

## Implementation record (added during apply)

Two places where the artifacts disagreed, or where reality was more specific than the
artifacts. Recorded here rather than diverging silently, per `docs/IMPLEMENTATION_MASTER_PLAN.md`
§9 rule 11.

1. **Spec vs design on a deletion marker's empty provider namespace.** Task 4.2 and
   `design.md` said an empty namespace would remain an "ignored" *policy* outcome. The
   **spec** says a deletion marker with "a missing provider namespace" is "rejected under
   the corresponding eligibility reason". The spec is normative, so the spec was followed:
   such a marker is now **rejected** with `EmptyProvider`. Both classifications are equally
   safe — neither reverses value nor moves the watermark — and the spec's is strictly more
   informative because it names the reason. `SessionPersistenceHardeningTests` was updated
   accordingly and a companion test added for the policy half (an eligible marker naming an
   uncredited record remains `DeletionsIgnored`), so the two classes stay distinguishable.

2. **A deletion marker must skip the quantity eligibility rules.** Task 2.3 says every
   record is validated first, and the spec says "the same eligibility rules". Taken
   literally that applies `ZeroQuantity` to deletion markers — but every well-formed marker
   in this repository carries `Quantity == 0`, because a marker has no activity by
   definition. Doing so would reject every deletion ever issued and silently disable the
   correction policy, which is the opposite of the requirement's intent (a marker that must
   be rejected for being ineligible is a marker that can never reverse anything). The
   quantity checks are therefore skipped **for deletion markers only**; provenance, unit,
   temporal extent and both window bounds still apply. Found by the first test run of the
   restructure, not by inspection. See D-053.

3. **`ReconciliationHorizon` is retained as a name only.** Task 1.4 asks to keep the
   `OutsideHorizon` / `FutureTimestamp` status names; both are kept and keep their distinct
   meanings. The *constant* `ReconciliationHorizon = 14 days` is also kept, but is no longer
   read by validation, so the historical decision (D-030) stays locatable in review while
   its value is not silently resurrected. The live constant is `TrustedWindowFloor = 45 days`.

4. **An already-trusted out-of-window record is a duplicate, not a rejection.** Not
   anticipated by the artifacts, and required by two of them at once: the spec's "replaying
   a previously accepted backlog credits nothing further … every record in it is recognised
   as a duplicate", and the master plan's "preserve working functionality … the exactly-once
   guarantees are not to be relaxed for convenience". Without it, replaying a long horizon
   reported the oldest days as `OutsideHorizon` and `M4Region1AcceptanceTests` — the
   flagship exactly-once proof — read as refused activity. Guarded by a revision check so
   the carve-out cannot mint value: an out-of-window record with a **higher** revision is
   still rejected and can never reach the correction path.

5. **A real defect found by the new tests.** `DateTimeOffset` arithmetic does not saturate;
   `DateTimeOffset.MinValue − 45 days` throws. `TrustedTime` now guards both bound
   calculations against the distance available to the representable limit. See
   `docs/evidence/trust-window/CAMPAIGN-2026-09-30.md` §6.
