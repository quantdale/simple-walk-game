## Context

See `proposal.md` (Why) for the evidence.

What makes this change unusually cheap to implement:

- The parking-and-flush mechanism **already exists and is already tested**.
  `OfflineAdvancer.DeliverStoredOutput` subtracts only the units `balances.Add`
  actually accepted, leaving the refused remainder in
  `ProducerRuntimeState.StoredMilliUnits`, and `TickProducers` calls
  `DeliverStoredOutput` on every tick including zero-elapsed ones precisely so
  parked units flush without new production.
  `ProducerSimulationTests` proves both halves — it calls `SetCap` directly to
  create the condition. The production gap is only that no cap is ever set.
- `GameStateValidator` already bounds `StoredMilliUnits` against
  `CapacityUnits × 1000`, so a parked store is already a validated invariant.
- `ResourceBalances.Add` already returns "the amount actually applied", which
  is the exact primitive the "not silently discarded, but reported" requirement
  needs.

What constrains the design:

- Caps must live in **content**, not in state. `RegionDefinition` is immutable
  authored data; a cap is a balance of the region, not of the player. Putting
  caps in `GameState` would require a schema bump and would make them
  player-editable by migration.
- Content changes invalidate committed pacing evidence. `docs/evidence/m4/` is
  byte-reproducible and gated; `Region1Catalog` costs and the per-chain
  completion days printed by `tools/simulation profile` will change if project
  requirements are authored.
- `scripts/verify-architecture.sh` keeps Domain free of filesystem access and
  free of project references, so any helper used for "can this requirement
  ever be met" arithmetic must be pure domain code.
- `OfflineAdvancer.AllocateVitality` is the single place Vitality streams into
  the active project. Adding a second resource there must not disturb the
  existing rollover semantics that `QueueAllocationTests` pins, including the
  behaviour that surplus Vitality banks when the queue is empty.
- `EnsureActiveSlot` promotes the head of the queue unconditionally when
  auto-advance is on. Adding an affordability gate there is the load-bearing
  part of this change.

## Goals / Non-Goals

**Goals**

- Give every produced resource an explicit, validated, applied cap.
- Make the existing parking mechanism reachable in production.
- Give Materials and Knowledge at least one real consumption decision.
- Keep the economy integral, non-negative, and free of any manual claim step.

**Non-Goals**

- New currencies, a shop, an upgrade tree, or any resource-spending UI.
- Changing Vitality's role, the steps-to-Vitality conversion, or the
  steps-per-Vitality rate.
- Continuous-time or fractional resource math.
- Re-tuning Region 1's Vitality costs. That is the separate pacing change and
  doing both at once would make the pacing evidence unreviewable.

## Decisions

### 1. Caps are authored on `RegionDefinition`

`RegionDefinition` gains a read-only cap map keyed by resource type. A producer's
output resource must have a cap; a resource with no cap is a validation failure.

`GameFactory.NewGame` applies the caps to the fresh `ResourceBalances`.
Decoded saves do **not** re-apply caps on load: if a later content version
lowers a cap, `GameStateValidator` reports a violation and the boot fails
closed, which is the repository's established posture for "the state and the
content disagree" (the same posture as unknown content IDs in
`MatureSaveMigrationTests`).

*Alternative considered:* clamp on load instead of failing closed. Rejected: it
would silently destroy a player's accumulated balance the first time a cap is
retuned, which is exactly the "destructive" outcome R-005/R-007 forbid.

### 2. Requirements live on `ProjectDefinition` as a cost map

`ProjectDefinition` gains an optional per-resource cost map alongside
`VitalityCost`, defaulting to empty. This is the field
`GAME_SYSTEMS.md` §3 already promises ("optional secondary-resource
requirements").

Requirement semantics, chosen to match the existing Vitality semantics as
closely as possible so the simulation stays predictable:

- Consumed **at activation**, not at enqueue, mirroring how Vitality is
  streamed rather than pre-paid. This keeps "queueing is free" true, which the
  low-attention product thesis depends on.
- All-or-nothing, via the existing `TryConsume`.
- Refund behaviour on a failed activation: none is needed, because activation
  validates affordability before consuming.

*Alternative considered:* consume the requirement progressively as the project
advances, like Vitality. Rejected: it multiplies the number of ways a project
can be half-funded and half-abandoned, and it makes auto-advance's rollover
harder to reason about, for no player-visible benefit at this scale.

### 3. Affordability is a gate inside `EnsureActiveSlot`, not a caller check

`EnsureActiveSlot` currently promotes `QueuedProjectIds[0]` whenever auto-advance
is on. It must additionally verify the next project's secondary requirements are
met. If they are not, the promotion is skipped and the project stays queued.

Critically, the loop must then **stop**, not skip to the next project: promoting
a later project past an unaffordable earlier one would silently reorder the
player's own priorities, which is exactly the agency R-015 asks to preserve.

*Alternative considered:* scan forward for the first affordable queued project.
Rejected: it changes queue semantics the player configured. A player who queued
A then B expects B to wait for A.

*Alternative considered:* filter the queue at enqueue time. Rejected: the queue
is durable player state and a later cap change or expedition grant could make a
previously-unaffordable project affordable; filtering durably would need its own
migration and would silently drop player intent.

### 4. Unmet requirements block auto-advance and produce a specific error on manual activation

`ActivateQueuedProject` already returns `DomainResult` with stable error codes.
An unmet requirement returns a new, dedicated code in the existing `ErrorCodes`
family, distinct from `NotQueued` and `ProjectAlreadyActive`, so a shell can
show "you need 40 Materials" rather than a generic failure.

### 5. Authored requirements must be satisfiable, and validation proves it

`ContentValidator` gains a satisfiability check: for each project, for each
required resource, the region's own producers of that resource must be able to
supply the requirement within the cap over the region's design horizon. This is
pure arithmetic over authored definitions, so it belongs in Domain validation
with no simulation.

*Alternative considered:* leave satisfiability to the pacing profiles.
Rejected: the pacing profiles are a CI evidence gate that regenerates when
content changes, which is too late and too expensive a place to discover that a
project can never be afforded.

### 6. The Region 1 requirement set is deliberately small

Two to three projects across distinct chains get a secondary requirement, sized
so that the `moderate` and `high` pacing profiles are unaffected in completion
day and the `low` profile's documented long tail is not materially worsened.
The `inactivity` profile is expected to shift and its evidence must be
regenerated and reviewed — see Risks.

The choice of *which* projects is a balance decision recorded in
`docs/DECISIONS.md` by the implementer, constrained by the rule that every
chain stays completable under the `moderate` profile.

### 7. Observability is added to read models, not to a new logging path

`RegionReadModel.ProducerRow` gains the resource cap and an explicit
capacity-limited flag; `ProjectsReadModel.ProjectRow` gains the requirement
costs and an affordability flag. The return-summary composer gains one item
kind for capped storage. No logging framework is introduced — this repository
has no logging seam by design (its observability surface is the bounded
diagnostics read model), and adding one is out of scope here.

## Risks / Trade-offs

- **[Authoring requirements changes committed pacing evidence]**
  → This is the largest real cost of the change and it is unavoidable if the
  currencies are to mean anything. Mitigation: keep the requirement set small
  and skewed to later chains, regenerate all seven reports in the same change,
  and have the implementer state in `docs/DECISIONS.md` exactly which
  completion days moved and why that is acceptable. The alternative — shipping
  requirements without regenerating evidence — is forbidden; the gate exists to
  catch exactly that.

- **[Auto-advance stalling on an unaffordable project could surprise players]**
  → Mitigation is visibility, not silent skipping: the Projects read model
  reports the requirement and the current balance, the return summary explains
  the stall, and Vitality keeps banking rather than being wasted. A player with
  nothing to spend sees "waiting for Materials", which is a legible state, not
  a dead end.

- **[Lowering a cap below an existing balance turns a mature save into a
  fail-closed boot]**
  → Mitigation: cap values for Region 1 are chosen at or above the balances the
  existing committed evidence produces, and a task requires running
  `tools/simulation profile` for all seven profiles to confirm no balance
  exceeds its cap at any point. If one would, raise the cap rather than adding
  a load-time clamp.

- **[A cap that is too tight turns production into a silent loss]**
  → The parking mechanism means it is not lost, only deferred, and
  `ProducerSimulationTests`' existing two cap tests become production-reachable
  coverage rather than a synthetic scenario. The observability requirement
  ensures the player can tell the difference.

- **[Region-level requirement satisfiability is approximated by authored
  arithmetic]**
  → Documented as an approximation. The pacing profiles remain the empirical
  check; this rule exists to catch obvious impossibilities cheaply, not to
  replace measurement.

## Migration Plan

No save schema change. `GameState` gains no new field: caps are content,
requirements are content, and parked output already exists in
`ProducerRuntimeState.StoredMilliUnits` (schema v2, D-032).

Ordering for safe rollout:

1. Land caps and validation **without** authoring any project requirement.
   Existing saves are unaffected; only production parking becomes reachable.
2. Then author requirements and regenerate pacing evidence.

This two-step split is in the task list deliberately, so that if the evidence
regeneration turns out to be contentious the caps can ship independently.

Rollback of step 2 is safe: removing an authored requirement makes state valid
again, since requirements are never persisted.

## Open Questions

None. The specific projects that receive a requirement, and the cap values, are
balance decisions assigned explicitly in the task list with the constraint that
every chain remains completable under the `moderate` profile.
