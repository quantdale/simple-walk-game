## 1. Author the closure criterion

- [ ] 1.1 In `src/WalkGame.Domain/Regions/RegionModel.cs` (and
  `Region1Catalog`), author an explicit **closure criterion** expressed in
  terms of chain/landmark progress, replacing the implicit "spend the authored
  total on this exact roster". The criterion MUST still require the majority of
  the six chains so closure cannot be reached cheaply.
- [ ] 1.2 Decide explicitly whether to record *which* route was taken in
  canonical state. If recorded, add it as an additive field following D-036 and
  do not bump `SchemaVersions`. If not recorded, derive closure from canonical
  facts exactly as today, and state that decision in `docs/DECISIONS.md`.
- [ ] 1.3 Do **not** re-cost, rename, or re-prerequisite the original M1/M3 seed
  definitions that `Region1Catalog` documents as preserved verbatim. Find the
  lever in M4-authored content.
- [ ] 1.4 Evaluate whether a modest cost adjustment in addition to the
  criterion is warranted, and record the decision either way. If adopted, it
  must be justified by the measured shortfall (16,420 earned vs 19,400 cost)
  rather than by preference.

## 2. Validate the criterion

- [ ] 2.1 In `src/WalkGame.Domain/Validation/ContentValidator.cs`, add: the
  closure criterion is expressible in terms of content already proven reachable,
  and is not satisfied only by unreachable content. Reuse the existing
  reachability fixpoint.
- [ ] 2.2 In `src/WalkGame.Domain/Validation/GameStateValidator.cs`, add:
  post-closure state remains valid and evergreen regardless of which route was
  taken, and a recorded route is consistent with the canonical completion
  facts.
- [ ] 2.3 Add `ContentValidationTests` and `M4StateValidationTests` cases for
  each new rule, including an unreachable-criterion negative case.
- [ ] 2.4 Confirm `M4ContentGraphTests.AuthoredRegion1Catalog_MeetsTheDocumentedContentMinimum`
  still passes (2–7 chains, 6+ landmarks, 10+ discoveries, 3+ expeditions).

## 3. Make the pacing harness fail on a shortfall

- [ ] 3.1 In `tools/simulation/Commands.cs` `Profile`, return a non-zero exit
  code when the horizon is reached with the region not closed, and print the
  incomplete chains by name. Preserve the existing validator-violation exit
  behaviour.
- [ ] 3.2 Report closure against the authored criterion, and stop inferring
  closure from a resource total alone.
- [ ] 3.3 Confirm `scripts/verify-pacing-evidence.sh` now fails on a shortfall
  without any script change, by reproducing the current `inactivity` state
  (a temporary local re-cost that reintroduces the shortfall must make the gate
  fail — prove it, then revert).
- [ ] 3.4 Ensure `Bench`, `LongHaul` and `Scenarios` are unaffected: only
  `profile` owns pacing judgements.

## 4. Report requested vs eligible activity

- [ ] 4.1 In `Cli.Profile`, accumulate and print **three** figures: requested
  steps, eligible steps after bounded-quantity handling, and the achieved
  effective eligible-steps-per-vitality ratio. Stop printing the pre-clamp total
  under an "activity ingested" label.
- [ ] 4.2 Make the printed ratio derivable from the printed figures, so a
  reader can verify it by division.
- [ ] 4.3 Carry the same distinction into `LongHaul`'s `LONGHAUL-RESULT` line if
  it prints a step total.
- [ ] 4.4 Update the CLI usage text for `profile` if it describes the report.

## 5. Regenerate and explain all evidence

- [ ] 5.1 Regenerate all seven `docs/evidence/m4/pacing-*.txt` reports.
- [ ] 5.2 Produce a **per-profile diff** against the previous committed report.
  Do not bulk-overwrite.
- [ ] 5.3 Record in `docs/DECISIONS.md`: which completion days moved, by how
  much, and why the movement is acceptable. Explicitly confirm the `moderate`
  and `high` profiles' closure days did not regress.
- [ ] 5.4 Ratify or explicitly supersede each D-039 provisional target. No
  target may be left silently violated.
- [ ] 5.5 `scripts/verify-pacing-evidence.sh` — all seven reproduce
  byte-for-byte after regeneration.
- [ ] 5.6 `scripts/verify-save-growth.sh` — growth series reproduces; update
  only if record counts genuinely changed.

## 6. Correct the misstated conversion figure

- [ ] 6.1 In `docs/ROADMAP.md` §M4 ("Intermittent-engagement pacing finding"),
  replace "roughly **686 steps/Vitality** against the nominal 100" with the
  correct statement. The recomputation, to be included: the `pathological`
  profile requests 3,184,633 steps; per-record bounding reduces the eligible
  total to 2,184,633 steps; credited vitality is 21,844; therefore the achieved
  effective ratio is exactly 100.0 eligible steps per Vitality. Bounded handling
  discards surplus volume; it does not reduce the credit rate.
- [ ] 6.2 Apply the same correction to `docs/RISK_REGISTER.md` R-013.
- [ ] 6.3 Grep the whole `docs/` tree for the same figure and for any other
  pacing figure not derivable from the committed evidence. Correct each.
- [ ] 6.4 `docs/DECISIONS.md` — record the correction plainly, per this
  repository's practice of recording what was wrong rather than overwriting it
  silently.

## 7. Disposition the known issues

- [ ] 7.1 `docs/KNOWN_ISSUES.md` KI-003 — close with the evidence that the
  weekday-only profile now reaches closure, citing the regenerated report.
- [ ] 7.2 KI-004 — ratify or supersede the provisional pacing targets, and state
  which.
- [ ] 7.3 `docs/RISK_REGISTER.md` R-007 and R-013 — add the evidence that the
  intermittent-engagement shape is closed headlessly, stating explicitly that
  the seven-/thirty-day UX scenarios remain blocked on the runtime lane.

## 8. Tests

- [ ] 8.1 New: a weekday-only-with-absence profile reaches region closure within
  the documented horizon, with every chain complete. Drive it through the real
  trust pipeline.
- [ ] 8.2 New: the `low` profile still reaches closure.
- [ ] 8.3 New: the `pathological` profile reaches closure and the report's
  printed ratio equals eligible ÷ credited.
- [ ] 8.4 New: the pacing harness exits non-zero and names the incomplete chains
  when a shortfall is reintroduced.
- [ ] 8.5 New: post-closure state remains valid and evergreen under the new
  criterion, across reloads.
- [ ] 8.6 Confirm unchanged and still green: `M4Region1AcceptanceTests`,
  `M3AmbientProgressionAcceptanceTests` (byte-identical determinism),
  `M4ContentGraphTests` (9), `M4ProgressionMechanicsTests` (5),
  `M4StateValidationTests` (7), `ContentValidationTests` (8),
  `GameStateValidationTests` (10).

## 9. Verification

- [ ] 9.1 `dotnet build SimpleWalkGame.sln` — zero errors, no new warnings.
- [ ] 9.2 `dotnet test` — all suites green.
- [ ] 9.3 `scripts/verify-pacing-evidence.sh` — seven reproduced, 0 drifted.
- [ ] 9.4 `scripts/verify-save-growth.sh` — growth series reproduces.
- [ ] 9.5 `scripts/verify-architecture.sh` — 11 passed, 0 failed.
- [ ] 9.6 `tests/guards/run-guard-tests.sh` — green.
- [ ] 9.7 `openspec validate close-inactivity-pacing-shortfall --strict` passes.
- [ ] 9.8 `git status` clean apart from intended files; writer lease released.
