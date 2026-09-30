## Purpose

Defines the release-gate contract for authored region content: that identifiers
are unique and resolvable, that the dependency graph is acyclic and fully
reachable, that every authored number lies inside the bounds the production
simulation can actually represent, and that costs and rates are positive and
meaningful. Content that fails any of these SHALL be rejected before it can
create runtime corruption.

## ADDED Requirements

### Requirement: Every authored rate is inside the range the simulation can represent

The content validator SHALL accept a producer rate only when the production
simulation's own tick arithmetic is representable for that rate across the
maximum supported simulation interval, including the sub-day remainder term.
A rate that the validator accepts SHALL NOT be capable of producing a negative,
overflowed or otherwise unrepresentable production amount at runtime.

The bound SHALL be derived from the simulation's interval and representable
range rather than stated independently, so that the two cannot drift apart. If
the simulation's representable range or interval changes, the validator's bound
SHALL change with it or the build SHALL fail.

#### Scenario: A rate at the maximum accepted value is representable

- **WHEN** a producer is authored at exactly the maximum accepted rate
- **THEN** content validation reports zero violations
- **THEN** simulating the maximum supported interval produces a non-negative
  production amount
- **THEN** the resulting pending store is non-negative and within its bound

#### Scenario: A rate above the representable range is rejected

- **WHEN** a producer is authored with a rate above the maximum accepted value
- **THEN** content validation reports a violation naming the producer and the
  rate
- **THEN** the content is not accepted

#### Scenario: The bound tracks the simulation

- **WHEN** the simulation's maximum interval or its representable integer range
  is changed
- **THEN** either the validator's bound changes accordingly, or a check fails
  reporting that the two are inconsistent

#### Scenario: A sub-day remainder cannot overflow

- **WHEN** a producer is authored at the maximum accepted rate and only a
  fraction of a day elapses
- **THEN** the produced amount is non-negative and representable
- **THEN** the pending store remains non-negative

### Requirement: Capacity and cost bounds reflect real representability

Every authored capacity SHALL be accepted only when the pending-store capacity
derived from it is representable, and every authored cost SHALL be accepted only
when the region's total cost budget remains representable. A bound SHALL NOT be
accepted merely because it is a round number.

#### Scenario: An unrepresentable capacity is rejected

- **WHEN** a producer is authored with a capacity whose derived store capacity
  is not representable
- **THEN** content validation reports a violation naming the producer

#### Scenario: A total cost budget that cannot be represented is rejected

- **WHEN** the summed authored costs would exceed the representable budget
- **THEN** content validation reports a violation naming the project at which
  the budget was exceeded

#### Scenario: Current Region 1 content is unaffected

- **WHEN** the shipped region content is validated
- **THEN** zero violations are reported
- **THEN** no authored value is changed by the corrected bounds

### Requirement: The validator's bounds cannot be asserted without proof

Every bound the content validator enforces SHALL have at least one automated
case proving the behaviour at both edges: the largest accepted value is
accepted, and the first value above it is rejected with a violation that names
the offending content. A bound with only an accepted-value test SHALL be
considered insufficiently covered.

#### Scenario: Each bound has a negative edge test

- **WHEN** the content validation suite is enumerated
- **THEN** every bound enforced by the validator has a test proving the first
  out-of-range value is rejected

#### Scenario: A violation always names the offending content

- **WHEN** any bound is violated
- **THEN** the violation message names the specific project, producer, landmark,
  discovery or expedition at fault
