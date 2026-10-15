## 1. Maintained lifetime aggregates (retirement still disabled)

- [x] 1.1 In `src/WalkGame.Domain/Activity/ActivityIngestion.cs`, give
  `ProcessedRecordLedgerState` a durable maintained lifetime-credited total
  updated inside `Record` and `Update`. Make `TotalVitalityCredited` read the
  maintained value in O(1).
- [x] 1.2 In `src/WalkGame.Domain/Activity/RewardLedger.cs`, do the same for
  `RewardLedgerState.TotalVitalityCredited`, updated inside `Apply` and
  `ApplyCorrection`.
- [x] 1.3 In `src/WalkGame.Domain/Validation/GameStateValidator.cs`, add a
  cross-check: each maintained total MUST equal a full recomputation over the
  retained history plus the durably recorded retired total. A mismatch is a
  violation naming the aggregate. Keep the existing
  `ProcessedRecordCredited <= LedgerTotal` check.
- [x] 1.4 Add a `GameStateValidationTests` case that tampers a maintained
  aggregate and asserts the violation is reported (use the existing
  `PayloadSurgery` test helper in
  `tests/WalkGame.Application.Tests/TestSupport/`).
- [x] 1.5 At this point retirement is still disabled. Run the full suite: this
  step alone must not change any observed value in any existing test.

## 2. In-memory index without a full rescan

- [x] 2.1 In `RewardLedgerState`, keep the transaction index warm across commits
  within a session so a commit does not force a rebuild, while remaining a pure
  accelerator over the durable rows.
- [x] 2.2 Ensure an inconsistent index can never present as a false duplicate or
  a lost credit: the durable rows remain authoritative, and the index is only
  ever mutated alongside them.
- [x] 2.3 (index stays warm across commits/retirement; first check after a load scans only the retained window, independent of lifetime history — LastIndexBuildSize test) Add a test that loads a large state and asserts the first duplicate
  check does not require iterating every retained record (for example, by
  asserting a counter exposed for the test, or by asserting the operation is
  independent of retained size).

## 3. Retirement policy (still disabled)

- [x] 3.1 Add durable retirement bookkeeping to
  `ProcessedRecordLedgerState`: the count retired and the instant of the last
  retirement. Absent decodes to "nothing retired".
- [x] 3.2 Add a compact durable set of retired identities so "was this retired?"
  is O(1) and a retired record is permanently non-creditable.
- [x] 3.3 Implement the selection rule: candidates are entries whose record end
  lies behind the reconciliation window's lower bound, ordered
  deterministically (by the entry's committed instant, with a stable
  identity-key tiebreak), retired in bounded batches from within an ordinary
  commit.
- [x] 3.4 Retirement MUST NOT change any balance, any project's invested
  progress, or the maintained lifetime-credited totals. Only the retained detail
  and the retired-identity set change.
- [x] 3.5 Retire the reward ledger on the **same** watermark-relative rule so
  the existing `ProcessedRecordCredited <= LedgerTotal` invariant stays trivially
  satisfied. Record this coupling in the PR description as a review point: the
  two histories were previously independent.
- [x] 3.6 Handle a correction or deletion whose record row has been retired:
  no additional credit, no destructive reversal, outcome classified and
  diagnosable.

## 4. Enable retirement and set the bound

- [x] 4.1 Choose the retention bound and the per-commit retirement batch size so
  that the existing 730-day committed series converges, and record both values
  in `docs/DECISIONS.md`.
- [x] 4.2 Enable retirement on ordinary commits (not a bulk migration).
- [x] 4.3 Confirm convergence empirically: run `tools/simulation longhaul` at
  365 and 730 days and record the state sizes; growth in the second half must be
  materially smaller than in the first.

## 5. Tests

- [x] 5.1 New: re-delivery of a **retired** record credits nothing and is
  reported under a retired-record classification, distinct from an ordinary
  duplicate and distinct from new credit.
- [x] 5.2 New: retirement changes no balance, no invested progress, and no
  maintained lifetime total.
- [x] 5.3 New: a correction for a retired record creates no credit and applies
  no destructive reversal.
- [x] 5.4 New: retirement bookkeeping is durably recorded and survives restart.
- [x] 5.5 New: a state with no retirement bookkeeping decodes and validates
  (absent means "nothing retired").
- [x] 5.6 New: a very long horizon converges — the final state is within the
  documented bound.
- [x] 5.7 Confirm unchanged and still green: `ActivityIngestionTests` (13),
  `RewardLedgerTests` (4), `ActivityRedTeamTests` (6),
  `SessionIngestionTests` (15), `TemporalAnomalyTests` (7),
  `SaveCodecRoundtripTests` (5), `M4BackwardDecodingTests` (2),
  `MatureSaveMigrationTests` (4), `M3AmbientProgressionAcceptanceTests`,
  `M4Region1AcceptanceTests`.
  **If any of these need modification to pass, stop and redesign** — a
  trust-property regression is a design failure, not a test to update.
- [x] 5.8 Extend `SaveCodecRoundtripTests.Encode_Decode_PreservesEveryField` for
  the new fields.

## 6. Gate and documentation

- [x] 6.1 In `scripts/verify-save-growth.sh`, add a convergence assertion: a
  growth-**model** check (second-half growth materially smaller than
  first-half), not a brittle byte count. Print the bound and the observed
  value in the failure message.
- [x] 6.2 Keep the existing byte-reproducibility assertion of the series.
- [x] 6.3 `docs/PERFORMANCE_BUDGETS.md` — document the growth model, the
  convergence bound, and which structures are expected to plateau.
- [x] 6.4 `docs/DECISIONS.md` — record the retention rule, the bound, the batch
  size, and the record/reward ledger coupling.
- [x] 6.5 `docs/TECHNICAL_ARCHITECTURE.md` — document the retirement lifecycle
  alongside the existing dedup description.
- [x] 6.6 `docs/PRIVACY_REVIEW.md` §3 — the data inventory changes (retained
  history is bounded and retired records keep an identity only). Re-run the
  review's mechanical commands.

## 7. Verification

- [x] 7.1 `dotnet build SimpleWalkGame.sln` — zero errors, no new warnings.
- [x] 7.2 `dotnet test` — all suites green.
- [x] 7.3 `scripts/verify-save-growth.sh` — reproduces the committed series and
  passes the new convergence assertion.
- [~] 7.4 (content reproduces for all 7; 4 reports fail byte compare on Linux due to committed CRLF — KI-014, pre-existing) `scripts/verify-pacing-evidence.sh` — seven reports reproduce
  byte-for-byte.
- [x] 7.5 `scripts/verify-architecture.sh` — 11 passed, 0 failed.
- [x] 7.6 `tests/guards/run-guard-tests.sh` — green.
- [x] 7.7 `openspec validate compact-activity-and-reward-ledgers --strict`
  passes.
- [x] 7.8 `git status` clean apart from intended files; writer lease released.
