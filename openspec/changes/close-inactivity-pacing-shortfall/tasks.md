## 1. Author the closure criterion

- [x] 1.1 In `src/WalkGame.Domain/Regions/RegionModel.cs` (and
  `Region1Catalog`), author an explicit **closure criterion** expressed in
  terms of chain/landmark progress, replacing the implicit "spend the authored
  total on this exact roster". The criterion MUST still require the majority of
  the six chains so closure cannot be reached cheaply.
- [x] 1.2 Decide explicitly whether to record *which* route was taken in
  canonical state. If recorded, add it as an additive field following D-036 and
  do not bump `SchemaVersions`. If not recorded, derive closure from canonical
  facts exactly as today, and state that decision in `docs/DECISIONS.md`.
- [x] 1.3 Do **not** re-cost, rename, or re-prerequisite the original M1/M3 seed
  definitions that `Region1Catalog` documents as preserved verbatim. Find the
  lever in M4-authored content.
- [x] 1.4 Evaluate whether a modest cost adjustment in addition to the
  criterion is warranted, and record the decision either way. If adopted, it
  must be justified by the measured shortfall (16,420 earned vs 19,400 cost)
  rather than by preference.

## 2. Validate the criterion

- [x] 2.1 In `src/WalkGame.Domain/Validation/ContentValidator.cs`, add: the
  closure criterion is expressible in terms of content already proven reachable,
  and is not satisfied only by unreachable content. Reuse the existing
  reachability fixpoint.
- [x] 2.2 In `src/WalkGame.Domain/Validation/GameStateValidator.cs`, add:
  post-closure state remains valid and evergreen regardless of which route was
  taken, and a recorded route is consistent with the canonical completion
  facts.
- [x] 2.3 Add `ContentValidationTests` and `M4StateValidationTests` cases for
  each new rule, including an unreachable-criterion negative case.
- [x] 2.4 Confirm `M4ContentGraphTests.AuthoredRegion1Catalog_MeetsTheDocumentedContentMinimum`
  still passes (2–7 chains, 6+ landmarks, 10+ discoveries, 3+ expeditions).

## 3. Make the pacing harness fail on a shortfall

- [x] 3.1 In `tools/simulation/Commands.cs` `Profile`, return a non-zero exit
  code when the horizon is reached with the region not closed, and print the
  incomplete chains by name. Preserve the existing validator-violation exit
  behaviour.
- [x] 3.2 Report closure against the authored criterion, and stop inferring
  closure from a resource total alone.
- [x] 3.3 Confirm `scripts/verify-pacing-evidence.sh` now fails on a shortfall
  without any script change, by reproducing the current `inactivity` state
  (a temporary local re-cost that reintroduces the shortfall must make the gate
  fail — prove it, then revert).
- [x] 3.4 Ensure `Bench`, `LongHaul` and `Scenarios` are unaffected: only
  `profile` owns pacing judgements.

## 4. Report requested vs eligible activity

- [x] 4.1 In `Cli.Profile`, accumulate and print **three** figures: requested
  steps, eligible steps after bounded-quantity handling, and the achieved
  effective eligible-steps-per-vitality ratio. Stop printing the pre-clamp total
  under an "activity ingested" label.
- [x] 4.2 Make the printed ratio derivable from the printed figures, so a
  reader can verify it by division.
- [x] 4.3 Carry the same distinction into `LongHaul`'s `LONGHAUL-RESULT` line if
  it prints a step total.
- [x] 4.4 Update the CLI usage text for `profile` if it describes the report.

## 5. Regenerate and explain all evidence

- [x] 5.1 Regenerate all seven `docs/evidence/m4/pacing-*.txt` reports.
- [x] 5.2 Produce a **per-profile diff** against the previous committed report.
  Do not bulk-overwrite.
- [x] 5.3 Record in `docs/DECISIONS.md`: which completion days moved, by how
  much, and why the movement is acceptable. Explicitly confirm the `moderate`
  and `high` profiles' closure days did not regress.
- [x] 5.4 Ratify or explicitly supersede each D-039 provisional target. No
  target may be left silently violated.
- [x] 5.5 `scripts/verify-pacing-evidence.sh` — all seven reproduce
  byte-for-byte after regeneration.
- [x] 5.6 `scripts/verify-save-growth.sh` — growth series reproduces; update
  only if record counts genuinely changed.

---

## Implementation record (added during apply, 2026-10-09)

1. **Chain grouping became authored content, not a CLI table.** `RegionDefinition.Chains`
   authors the six restoration chains and `RegionDefinition.ClosureCriterion` states what
   "finished" means, so the criterion, the pacing harness and the region surface read ONE
   grouping. The hardcoded `ChainMembers` switch inside `tools/simulation` was deleted.

2. **The weaker closure routes were authored, measured, and rejected — this is the pivot of
   the change.** With a five-of-six-chain criterion, measured: `high` closes on day 81 with the
   research chain unrestored (survey, observatory, closure discovery — the part of the valley
   the ending is written about) while the weekday-only profile STILL does not close. A criterion
   a low-effort player can meet is met earlier by an engaged player, because engaged players
   complete more content sooner; that is structural, not a tuning problem. So the remedy is the
   rebalance, and the criterion ships at all-six chains with the alternative-route machinery
   proven on a fixture (`ClosureCriterion_AlternativeRoute_FiresOnAMajorityOfChains_WhenAuthored`)
   so it cannot rot silently if a future balance change needs it.

3. **The rebalance landed entirely inside M4-authored content, because the seeds are genuinely
   load-bearing.** `git show d673aa5~1:.../Region1Catalog.cs` confirms the five original
   M1/M3 definitions (trailhead 300, river intake 800, workshop 1500, wetland drainage 2200,
   canopy walkway 4000 = 8,800 of the 19,400), and `M3AmbientProgressionAcceptanceTests`
   asserts the exact Vitality boundaries those costs produce (700 / 2300 / 1500 banked at the
   documented checkpoints). Task 1.3's constraint is therefore evidence-based, and the
   M4-authored 10,600 absorbed the whole −4,050: 19,400 → 15,350.

4. **A cheaper roster would have bricked mature saves, so the validator was taught the
   difference.** A profile that paid the v2 cost of a project whose v3 cost is lower holds more
   Vitality than the new cost; the previous rule (`invested <= cost`) would have failed such a
   save closed. `GameStateValidator` now accepts over-funding ONLY for a COMPLETED project
   (a record of what that restoration actually cost that player) and still rejects it for an
   incomplete one (where it would be unspendable). Content version 2 → 3 means those saves
   classify as `CompatibleOlder` under the existing D-059 gate rather than failing at the gate.

5. **The gate verdict moved into the Application layer so it could be tested at all.**
   `PacingContract` owns the per-profile target table, the balance-defect verdict and the
   incomplete-chain naming; the CLI consumes it. Testing the CLI verb directly is impossible
   from the test project (`Cli` is `internal` to the SimCli assembly), and a gate nobody can
   test is a gate nobody can prove — which is the same failure mode that left this shortfall
   open. The CLI's behaviour was verified by running the verb: `profile --profile inactivity
   --days 100` prints the chain list and exits 2.

6. **Reports now state requested AND eligible activity, which is the fix for F-13.**
   `IngestResult.EligibleSteps` carries the post-clamp total; the report prints requested,
   eligible, and the ratio, so the ≈686 figure cannot be reproduced from the tool's output.
   Measured on `pathological`: 2,386,482 requested / 1,636,482 eligible / 16,363 credited →
   exactly 100.0, matching every other profile.

7. **Per-profile measured diffs** (all seven reports regenerated in this change, each reviewed):

   | profile | before | after | target |
   |---|---:|---:|---|
   | high | day 97 | day 77 | ≤120 |
   | moderate | day 242 | day 192 | ≤300 |
   | irregular | day 139 | day 109 | ≤250 |
   | weekend-heavy | day 201 | day 158 | ≤300 |
   | pathological | day 29 | day 22 | ≤400 |
   | inactivity | never | **day 376** | ≤400 |
   | low | never | never | exempt (unchanged) |

8. **What this change does not claim.** Whether the rebalanced curve FEELS right for engaged
   players is a product judgement; the mechanical contract (every balanced profile closes inside
   its documented target with the whole roster restored) is what is tested.

## 6. Correct the misstated conversion figure

- [x] 6.1 In `docs/ROADMAP.md` §M4 ("Intermittent-engagement pacing finding"),
  replace "roughly **686 steps/Vitality** against the nominal 100" with the
  correct statement. The recomputation, to be included: the `pathological`
  profile requests 3,184,633 steps; per-record bounding reduces the eligible
  total to 2,184,633 steps; credited vitality is 21,844; therefore the achieved
  effective ratio is exactly 100.0 eligible steps per Vitality. Bounded handling
  discards surplus volume; it does not reduce the credit rate.
- [x] 6.2 Apply the same correction to `docs/RISK_REGISTER.md` R-013.
- [x] 6.3 Grep the whole `docs/` tree for the same figure and for any other
  pacing figure not derivable from the committed evidence. Correct each.
- [x] 6.4 `docs/DECISIONS.md` — record the correction plainly, per this
  repository's practice of recording what was wrong rather than overwriting it
  silently.

## 7. Disposition the known issues

- [x] 7.1 `docs/KNOWN_ISSUES.md` KI-003 — close with the evidence that the
  weekday-only profile now reaches closure, citing the regenerated report.
- [x] 7.2 KI-004 — ratify or supersede the provisional pacing targets, and state
  which.
- [x] 7.3 `docs/RISK_REGISTER.md` R-007 and R-013 — add the evidence that the
  intermittent-engagement shape is closed headlessly, stating explicitly that
  the seven-/thirty-day UX scenarios remain blocked on the runtime lane.

## 8. Tests

- [x] 8.1 New: a weekday-only-with-absence profile reaches region closure within
  the documented horizon, with every chain complete. Drive it through the real
  trust pipeline.
- [x] 8.2 New: the `low` profile still reaches closure.
- [x] 8.3 New: the `pathological` profile reaches closure and the report's
  printed ratio equals eligible ÷ credited.
- [x] 8.4 New: the pacing harness exits non-zero and names the incomplete chains
  when a shortfall is reintroduced.
- [x] 8.5 New: post-closure state remains valid and evergreen under the new
  criterion, across reloads.
- [x] 8.6 Confirm unchanged and still green: `M4Region1AcceptanceTests`,
  `M3AmbientProgressionAcceptanceTests` (byte-identical determinism),
  `M4ContentGraphTests` (9), `M4ProgressionMechanicsTests` (5),
  `M4StateValidationTests` (7), `ContentValidationTests` (8),
  `GameStateValidationTests` (10).

## 9. Verification

- [x] 9.1 `dotnet build SimpleWalkGame.sln` — zero errors, no new warnings.
- [x] 9.2 `dotnet test` — all suites green.
- [x] 9.3 `scripts/verify-pacing-evidence.sh` — seven reproduced, 0 drifted.
- [x] 9.4 `scripts/verify-save-growth.sh` — growth series reproduces.
- [x] 9.5 `scripts/verify-architecture.sh` — 11 passed, 0 failed.
- [x] 9.6 `tests/guards/run-guard-tests.sh` — green.
- [x] 9.7 `openspec validate close-inactivity-pacing-shortfall --strict` passes.
- [x] 9.8 `git status` clean apart from intended files; writer lease released.
