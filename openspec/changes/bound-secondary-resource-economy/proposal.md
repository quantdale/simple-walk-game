# Bound the secondary resource economy so Materials and Knowledge have a purpose

## Why

Region 1 ships two secondary currencies, Materials and Knowledge, that the
game's own design documents call "optional". In practice they are neither
optional nor bounded: nothing consumes them, nothing caps them, and the
production cap logic that is written to bound them is dead code.

- No production path ever calls `ResourceBalances.SetCap`. A grep over
  `src/` and `tools/` finds the method defined and zero call sites outside
  tests, so `GetCap` always returns `long.MaxValue`.
- No production path ever consumes Materials or Knowledge. `TryConsume` is
  called for Vitality only, in `RewardLedger.ApplyCorrection` and
  `OfflineAdvancer.AllocateVitality`.
- Consequently the `hitResourceCap` branch in
  `OfflineAdvancer.DeliverStoredOutput`, and the entire "blocked units stay
  parked in the store and flush on a later tick" mechanism that
  `ProducerSimulationTests.ParkedUnits_FlushOnZeroElapsedTick_WhenResourceSpaceFrees`
  proves works, is unreachable in production.
- `GAME_SYSTEMS.md` §6 lists the required resource invariants and states "cap
  behavior must be explicit". It is not.
- `GAME_SYSTEMS.md` §3 lists "optional secondary-resource requirements" as a
  project schema field. `ProjectDefinition` has no such field.

Meanwhile Risk R-014 ("Too many currencies obscure the activity-to-world
relationship") is recorded as Open, and the unmitigated form of it is now in
the build: two currencies a player accumulates forever and can do nothing
with. Project costs are Vitality-only, so there is no resource-allocation
decision anywhere in the game.

## What Changes

- Authored secondary-resource **caps** become a first-class, validated part of
  the region content contract, and are applied when a new game is created. Caps
  are explicit, per resource, and validated for representability.
- Producer output that a cap refuses is **parked in the producer's bounded
  pending store** and flushes automatically when space frees, with no manual
  claiming step, preserving the anti-busywork rule.
- A **secondary-resource sink** is added so the currencies carry a real
  decision: projects gain an optional, authored secondary-resource requirement
  that is consumed when the project is activated, and refunded-conservatively
  under the same rules as Vitality correction.
- Read models and the return summary surface parked output and cap pressure, so
  a player can see *why* a store is full without it being a silent loss.
- Validator coverage for: cap representability, requirement satisfiability,
  requirement cost bounds, and conservation (nothing can be authored that
  makes a required project permanently unreachable).

**Non-goals:** adding new currencies, adding floating-point math, changing
Vitality's role or the steps-to-Vitality conversion, adding a shop, or adding
any resource-spending UI. Presentation of the new fields is the shell's job and
stays in read models only.

## Capabilities

### New Capabilities

- `economy-resources`: the canonical resource model — which resources exist,
  what produces them, what consumes them, how caps bound them, how parked
  output is released without manual claiming, and the invariants that make the
  economy deterministic and non-negative.

### Modified Capabilities

<!-- None. `openspec/specs/` is empty at HEAD; `economy-resources` is
     introduced here. Note that `content-authoring` and `state-durability`
     changes touch overlapping validator code and are sequenced after this one
     (see Dependencies). -->

## Impact

**Evidence from the audit**

- `src/WalkGame.Domain/Economy/ResourceBalances.cs` — `SetCap` exists;
  `GetCap` defaults to `long.MaxValue`; no cap is ever set in production.
- `src/WalkGame.Domain/Simulation/OfflineAdvancer.cs` —
  `DeliverStoredOutput` computes `hitResourceCap` from
  `balances.Add(...)`'s return value, which can never be short because the cap
  is `long.MaxValue`.
- `src/WalkGame.Domain/Projects/ProjectModel.cs` — `ProjectDefinition` has
  `VitalityCost` and `Prerequisites` but no secondary-resource requirement,
  contrary to `GAME_SYSTEMS.md` §3.
- `src/WalkGame.Application/GameSession.cs` `GetHome()` and
  `src/WalkGame.Application/ReadModels/HomeReadModel.cs` — Materials and
  Knowledge are displayed but not explainable.
- `Region1Catalog.cs` — all three producers emit Materials or Knowledge; two of
  three expeditions reward Materials or Knowledge. There is no consumer.
- Tests that already prove the mechanism but not its reachability:
  `ProducerSimulationTests.Tick_ResourceCapExhausted_ParksSurplus_InBoundedStore`,
  `ProducerSimulationTests.ParkedUnits_FlushOnZeroElapsedTick_WhenResourceSpaceFrees`,
  `M4ProgressionMechanicsTests.ExpeditionReward_IsClampedByResourceCap`.

**Affected code**

- `src/WalkGame.Domain/Economy/ResourceBalances.cs` — cap application and
  observability.
- `src/WalkGame.Domain/Regions/RegionModel.cs` — `RegionDefinition` gains
  authored caps; `ProducerDefinition` unchanged in shape.
- `src/WalkGame.Domain/Projects/ProjectModel.cs` — `ProjectDefinition` gains
  optional secondary requirements.
- `src/WalkGame.Domain/Simulation/OfflineAdvancer.cs` — requirement
  consumption at activation; parked-output release already implemented.
- `src/WalkGame.Domain/GameState.cs` — no new durable field expected; caps live
  in content, not state.
- `src/WalkGame.Domain/Validation/ContentValidator.cs` — new validation rules.
- `src/WalkGame.Domain/Validation/GameStateValidator.cs` — balance-within-cap
  invariant on load.
- `src/WalkGame.Application/Content/Region1Catalog.cs` — author the caps and the
  requirements.
- Read models: `HomeReadModel`, `RegionReadModel`, `ProjectsReadModel`.

**Affected tests and committed evidence**

- `ProducerSimulationTests` (9), `M4ProgressionMechanicsTests` (5),
  `ResourceBalancesTests` (8), `M4ContentGraphTests` (9),
  `ContentValidationTests` (8), `QueueAllocationTests` (3),
  `M4Region1AcceptanceTests`, `M3AmbientProgressionAcceptanceTests`.
- **All seven `docs/evidence/m4/pacing-*.txt` reports will change** if Region 1
  requirements are authored, because project costs and completion days appear in
  the report. `scripts/verify-pacing-evidence.sh` is the gate, and regenerating
  that evidence is an explicit, reviewed step in this change — not something to
  paper over.

**Dependencies**

- `align-content-validator-with-simulation-overflow-bounds` must be applied
  first or together: both edit `ContentValidator`, and applying them in the
  wrong order risks losing one rule set.
- `close-inactivity-pacing-shortfall` depends on this change, because that
  change's balance remedy needs secondary costs to exist before it can re-tune
  pacing.
