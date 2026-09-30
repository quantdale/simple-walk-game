## 1. Authored resource caps (no behaviour change for existing saves)

- [x] 1.1 In `src/WalkGame.Domain/Regions/RegionModel.cs`, add a read-only
  per-resource cap map to `RegionDefinition` with a constructor parameter
  defaulting to empty. Keep the type immutable and constructor-validated.
- [x] 1.2 In `src/WalkGame.Domain/GameState.cs`, apply the content caps to the
  fresh `ResourceBalances` in `GameFactory.NewGame`.
- [x] 1.3 In `src/WalkGame.Domain/Validation/ContentValidator.cs`, add: every
  resource any producer emits MUST have an authored cap; every cap MUST be
  positive; and the derived store capacity `cap × 1000` MUST be representable.
  Name the resource in every violation.
- [x] 1.4 In `src/WalkGame.Domain/Validation/GameStateValidator.cs`, add: no
  resource balance may exceed the cap declared by the content it is validated
  against. Do **not** clamp on load — report a violation and fail closed.
- [x] 1.5 Author Region 1 caps in `src/WalkGame.Application/Content/Region1Catalog.cs`
  for Materials, Knowledge and Vitality, chosen at or above the maximum balances
  the committed evidence produces. Record the chosen values in
  `docs/DECISIONS.md`.
- [x] 1.6 Add a red-team `ContentValidationTests` case: a producer whose output
  resource has no authored cap is reported.
- [x] 1.7 Add a `GameStateValidationTests` case: a balance above its content cap
  is reported and the state is not silently clamped.

## 2. Make parking reachable and observable

- [x] 2.1 Verify by test that the existing
  `OfflineAdvancer.DeliverStoredOutput` parking path and its zero-elapsed flush
  are now reachable in production. Add a test that drives Region 1 producers
  against the authored cap and asserts: balance stops at the cap, the remainder
  is in `ProducerRuntimeState.StoredMilliUnits`, and a later flush after
  headroom is created delivers it with no player action.
- [x] 2.2 Confirm the "no waste" property: a full pending store plus further
  elapsed time discards no fractional remainder. Extend the existing
  `ProducerSimulationTests` coverage rather than duplicating it.
- [x] 2.3 In `src/WalkGame.Application/ReadModels/RegionReadModel.cs`, add the
  resource cap and an explicit capacity-limited flag to `ProducerRow`.
- [x] 2.4 In `src/WalkGame.Application/Summaries/ReturnSummaryComposer.cs`, add
  a summary item for capped storage that states nothing was lost, and ensure it
  is only emitted when a producer actually hit a cap in that batch.
- [x] 2.5 Confirm `getRegion()`/`GetRegion()` still compiles and the existing
  `SessionQueueControlTests.GetRegion_ExposesCanonicalStagesAndProducerStoreState`
  keeps passing after the read-model change.

## 3. Secondary-resource requirements

- [x] 3.1 In `src/WalkGame.Domain/Projects/ProjectModel.cs`, add an optional
  per-resource cost map to `ProjectDefinition` (default empty), alongside
  `VitalityCost`.
- [x] 3.2 In `src/WalkGame.Domain/Simulation/OfflineAdvancer.cs`, consume the
  active project's requirement **at activation**, all-or-nothing via
  `TryConsume`. Consume before any Vitality is streamed so a project is never
  half-funded in a way that is hard to reason about.
- [x] 3.3 In `EnsureActiveSlot`, skip promotion when the head-of-queue project's
  requirement is unmet, and **stop the scan** rather than promoting a later
  project. Preserve current auto-advance behaviour in every other respect.
- [x] 3.4 In `src/WalkGame.Application/GameSession.cs`, add a dedicated error
  code to `ErrorCodes` for an unmet secondary requirement; return it from
  `ActivateQueuedProject` with a message naming the resource and the shortfall.
  Ensure nothing is consumed and the active slot is unchanged on failure.
- [x] 3.5 In `src/WalkGame.Domain/Validation/ContentValidator.cs`, add
  satisfiability validation: for each project requirement, the region's own
  producers of that resource must be able to supply it within the cap over the
  design horizon. Name project and resource in violations.
- [x] 3.6 Author the Region 1 requirement set in `Region1Catalog.cs`: 2–3
  projects across distinct chains, sized so the `moderate` and `high` profiles
  are unaffected and every chain stays completable under `moderate`. Record the
  choice and the reasoning in `docs/DECISIONS.md`.
- [x] 3.7 In `src/WalkGame.Application/ReadModels/ProjectsReadModel.cs`, add the
  requirement costs and an affordability flag to `ProjectRow`.

## 4. Tests

- [x] 4.1 New: a requirement is consumed exactly once on activation; a second
  activation does not double-consume.
- [x] 4.2 New: activating an unaffordable project returns the new error code,
  consumes nothing, and leaves the active slot unchanged.
- [x] 4.3 New: auto-advance stalls at an unaffordable head-of-queue project, the
  project stays queued, later queued projects are not promoted past it, and
  Vitality continues to bank.
- [x] 4.4 New: making the requirement affordable (an expedition grant) lets
  auto-advance proceed on the next tick with no player action.
- [x] 4.5 New: `ContentValidator` flags an unsatisfiable requirement; and flags
  a missing cap.
- [x] 4.6 New: `ResourceBalances` invariants — balance never above cap, never
  negative, saturating grant, zero-grant no-op. Extend
  `ResourceBalancesTests` rather than duplicating its 8 existing cases.
- [x] 4.7 New: a capped producer is reported as capacity-limited in
  `RegionReadModel` with its cap and parked amount; a non-capped producer is not.
- [x] 4.8 New: the return summary emits the capped-storage item only when a cap
  was actually hit.
- [x] 4.9 Confirm unchanged: `ProducerSimulationTests` (9),
  `M4ProgressionMechanicsTests` (5), `QueueAllocationTests` (3),
  `M4ContentGraphTests` (9), `M4Region1AcceptanceTests`,
  `M3AmbientProgressionAcceptanceTests`.

## 5. Regenerate committed evidence (required)

- [x] 5.1 Run `tools/simulation profile` for all seven profiles and capture the
  new reports.
- [x] 5.2 Diff each new report against `docs/evidence/m4/pacing-*.txt` and record
  in `docs/DECISIONS.md` exactly which completion days moved and why the change
  is acceptable. Do not regenerate silently.
- [x] 5.3 `scripts/verify-pacing-evidence.sh` — all seven reproduce
  byte-for-byte **after** regeneration.
- [x] 5.4 `scripts/verify-save-growth.sh` — growth series reproduces; update the
  committed series only if record counts genuinely changed.

## 6. Documentation

- [x] 6.1 `docs/GAME_SYSTEMS.md` §3 and §6 — document authored caps, requirement
  consumption at activation, and the parking-and-flush contract.
- [x] 6.2 `docs/RISK_REGISTER.md` R-014 — add the evidence that Materials and
  Knowledge now carry a real decision, and state what remains unmitigated.
- [x] 6.3 `docs/KNOWN_ISSUES.md` — no new entry expected; confirm none of the
  existing entries are invalidated.
- [x] 6.4 `docs/WORLD_AND_CONTENT.md` — record the authored requirement and cap
  values alongside the content graph.

## 7. Verification

- [x] 7.1 `dotnet build SimpleWalkGame.sln` — zero errors, no new warnings.
- [x] 7.2 `dotnet test` — all suites green.
- [x] 7.3 `scripts/verify-architecture.sh` — 11 passed, 0 failed.
- [x] 7.4 `tests/guards/run-guard-tests.sh` — green.
- [x] 7.5 `openspec validate bound-secondary-resource-economy --strict` passes.
- [ ] 7.6 `git status` clean apart from intended files; writer lease released.

---

## Implementation record (added during apply)

Three places where the artifacts were wrong or incomplete. Recorded here rather than
diverging silently, per `docs/IMPLEMENTATION_MASTER_PLAN.md` §9 rule 11.

1. **Task 1.5 asked for a Vitality cap, and implementing it would have been a data-loss
   bug.** `RewardLedger.Apply` ignores the value `balances.Add` returns and records the
   **full** transaction amount in the ledger; Vitality also has no producer store to park
   refused units in. A Vitality cap would therefore silently discard real walking credit —
   the exact defect class this campaign exists to remove. **Vitality is deliberately left
   uncapped**, and the validation rule is scoped to resources a producer can emit (no
   producer emits Vitality). See D-054.

2. **The committed pacing evidence did NOT need regenerating.** The proposal expected all
   seven `docs/evidence/m4/pacing-*.txt` reports to change. They did at the first attempt
   (60 Materials / 40 Knowledge drifted five of seven profiles; `high` research chain
   97→101, `pathological` 29→38). Task 3.6's sizing constraint was applied empirically and
   the requirements set to **40 Materials / 20 Knowledge**, after which **all seven reports
   reproduce byte-for-byte**. So tasks 5.1–5.3 are satisfied without regenerating anything,
   which is strictly better than the change anticipated. The sizing search and the drift it
   caused are recorded in D-054 rather than papered over.

3. **`ProducerProduced.HitCapacity` was a conflated flag** carrying both "the producer's
   own store filled" and "the resource is at its cap". The spec requires the read model and
   summary to distinguish "no production happened" from "production happened but is parked",
   which one boolean cannot express. The event now carries `HitCapacity` (store) and
   `HitResourceCap` (balance) separately, and only the latter is relieved by the player
   spending something. `ProducerSimulationTests.Tick_ResourceCapExhausted_ParksSurplus_InBoundedStore`
   was updated to assert `HitResourceCap`; its parking and flush assertions are unchanged.

Task 2.1's proof that parking is reachable was deliberately split across two suites: the
Domain suite proves the rules against a synthetic region, and the Application suite proves
the SHIPPED catalog reaches its caps. That split matters — the original defect was
precisely that a mechanism was tested in isolation and never in the product.
