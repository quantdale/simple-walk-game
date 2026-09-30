## Purpose

Defines the canonical resource model: which resources exist, what produces
them, what consumes them, how storage caps bound them, how output refused by a
cap is released without the player ever having to claim it, and the invariants
that keep the economy deterministic, integral and non-negative.

## ADDED Requirements

### Requirement: Every produced resource has an explicit, authored, validated
cap

Every resource a region content can produce SHALL have an explicit cap authored
in the region content definition. A resource with no authored cap SHALL be a
content validation failure at authoring time, not an unbounded default at
runtime.

The activity-derived progression resource is EXEMPT and SHALL NOT be capped.
Activity-derived credit is earned by the player's real movement and is spent
against authored project costs; it has no producer store to park refused units
in, so a cap would silently discard earned walking credit. The cap requirement
is scoped to producer-fed and expedition-fed resources. (D-054 records this as a
deliberate departure from the original task list.)

Caps SHALL be positive and representable, and applying a cap SHALL never leave a
balance above that cap.

#### Scenario: A produced resource without an authored cap is rejected at
validation

- **WHEN** region content is validated and a producer-fed or expedition-fed
  resource it can emit has no authored cap
- **THEN** validation reports a violation naming that resource
- **THEN** the content is not accepted

#### Scenario: Capping the activity-derived resource is rejected

- **WHEN** region content declares a cap for the activity-derived progression
  resource
- **THEN** validation reports a violation naming that resource and the reason it
  must not be capped

#### Scenario: A non-positive or unrepresentable cap is rejected at validation

- **WHEN** region content declares a cap that is not positive, or is large
  enough that the producer store capacity derived from it would overflow
- **THEN** validation reports a violation naming that cap

#### Scenario: Applying a cap lowers an existing balance to the cap

- **WHEN** a cap is applied to a resource whose balance already exceeds it
- **THEN** the balance becomes exactly the cap
- **THEN** the balance is never negative

#### Scenario: A balance can never exceed its cap through a grant

- **WHEN** a producer or an expedition grants a resource beyond its cap
- **THEN** only the portion up to the cap is applied
- **THEN** the remainder is not silently discarded; it is reported

### Requirement: Output refused by a cap is parked, never lost, and never claimed

Output that a resource cap refuses SHALL be held in the producing unit's own
bounded pending-output store, SHALL be released automatically into the balance
as soon as space frees, and SHALL never require a manual claiming action from
the player. Surplus elapsed time beyond the pending store's capacity SHALL
create no waste and SHALL NOT destroy the fractional remainder already parked.

#### Scenario: Output beyond the cap is retained and later released

- **WHEN** a producer generates output while its resource is at its cap
- **THEN** the excess is held in the producer's pending store
- **THEN** the resource balance does not exceed its cap
- **WHEN** the player later consumes resource headroom and a tick occurs
- **THEN** the parked units are delivered into the balance automatically
- **THEN** no player action was required to release them

#### Scenario: A full pending store still creates no waste

- **WHEN** a producer's pending store is full and additional elapsed time passes
- **THEN** no fractional remainder is discarded
- **THEN** the store remains within its bound

#### Scenario: A player never has to claim production

- **WHEN** any amount of production occurs while the app is closed
- **THEN** the resulting state contains that production without any claiming
  step having been taken
- **THEN** re-opening the app and advancing time is sufficient to realise it

### Requirement: Secondary resources carry a real consumption decision

At least one authored restoration project SHALL require a secondary resource in
addition to Vitality, so that a player must decide how to spend a scarce
secondary resource rather than accumulate it indefinitely. A requirement SHALL
be consumed when the project is activated, SHALL be all-or-nothing, and a
project whose requirement cannot be met SHALL NOT be activatable.

A requirement SHALL be satisfiable within the resource's cap under the pacing
profile the region is balanced for; a requirement that makes a required project
permanently unreachable is a content validation failure.

#### Scenario: A requirement is consumed on activation

- **WHEN** a project with a secondary-resource requirement becomes active
- **THEN** the required units are deducted from the balance
- **THEN** the deduction is all-or-nothing

#### Scenario: An unmet requirement blocks activation explicitly

- **WHEN** a player attempts to activate a project whose secondary-resource
  requirement is not met
- **THEN** the operation fails with a specific, stable error code
- **THEN** nothing is consumed
- **THEN** the active project slot is unchanged

#### Scenario: Auto-advance never activates an unaffordable project

- **WHEN** auto-advance reaches the head of the queue and the next project has an
  unmet secondary-resource requirement
- **THEN** auto-advance does not activate it
- **THEN** the project remains queued
- **THEN** Vitality continues to bank rather than being consumed

#### Scenario: A requirement that can never be satisfied is a content failure

- **WHEN** region content declares a secondary-resource requirement whose cost
  cannot be met within the resource's cap and the region's own production
- **THEN** validation reports a violation naming the project and the resource

### Requirement: Resource balances are integral, non-negative, and bounded

All resource arithmetic SHALL use integers with no floating-point authoritative
value. A balance SHALL never become negative through any valid operation. A
balance SHALL never exceed its authored cap. Loading a state whose balance
exceeds the cap of the content it is being validated against SHALL be reported
as a validation violation rather than silently corrected.

#### Scenario: Spending more than the balance consumes nothing

- **WHEN** a consumption of a resource greater than the balance is attempted
- **THEN** nothing is consumed
- **THEN** the balance is unchanged

#### Scenario: Adding zero is a no-op

- **WHEN** a grant of zero units is applied
- **THEN** the balance is unchanged

#### Scenario: A save whose balance exceeds the content cap fails validation

- **WHEN** a decoded state holds a balance above the cap declared by the content
  it is validated against
- **THEN** validation reports a violation naming the resource
- **THEN** the state is not silently clamped on load

#### Scenario: Extremely large grants saturate rather than overflow

- **WHEN** a grant near the representable maximum is applied
- **THEN** the balance saturates at the representable maximum
- **THEN** no arithmetic overflow occurs and the balance stays non-negative

### Requirement: Resource pressure is explainable to the player

The player-facing read models and the return summary SHALL make it possible to
distinguish "no production happened" from "production happened but is parked
because storage is full", and SHALL report the cap and the parked amount for each
producing unit.

#### Scenario: A capped producer is reported as capped, not as idle

- **WHEN** a producer has refused output because its resource is at its cap
- **THEN** the region read model reports the producer as capacity-limited
- **THEN** the region read model reports its cap and its parked amount

#### Scenario: The return summary explains parked output

- **WHEN** a committed batch resulted in output being parked by a cap
- **THEN** the pending return summary contains an item explaining the parking
- **THEN** the item states that nothing was lost

#### Scenario: A non-producing profile is not misreported as capped

- **WHEN** no producer is unlocked or no output was generated
- **THEN** no capped-storage item appears in the summary
