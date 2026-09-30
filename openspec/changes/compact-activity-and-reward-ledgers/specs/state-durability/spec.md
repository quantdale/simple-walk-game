## Purpose

Defines the durability contract for canonical state that grows across a
profile's lifetime: how much activity history must be retained to keep
exactly-once crediting true, how that history is bounded without ever
re-introducing a double credit, which aggregates are maintained incrementally
rather than recomputed, and the cost characteristics of committing the state.

## ADDED Requirements

### Requirement: Durable record history is bounded by an explicit retention policy

The durable processed-record history and the durable reward-transaction history
SHALL each be bounded by an explicit, documented retention policy rather than
growing without limit. The policy SHALL state what is retained, what becomes
eligible for retirement, and what durable evidence records that retirement
happened.

A profile's total canonical state size SHALL converge toward a documented bound
instead of growing linearly for the lifetime of the profile.

#### Scenario: A very long profile does not grow without bound

- **WHEN** a profile is driven through a long simulated horizon with daily
  activity
- **THEN** the canonical state size stops growing materially once the retention
  policy has taken effect
- **THEN** the size does not exceed the documented convergence bound
- **THEN** the resulting state still validates and still boots

#### Scenario: Retirement is durably recorded

- **WHEN** records have been retired from the durable history
- **THEN** the state records how many were retired and over what instant
- **THEN** a profile that retires nothing records that nothing was retired

#### Scenario: Retirement happens on ordinary commits, not only in bulk

- **WHEN** a commit pushes the history past its retention bound
- **THEN** that same commit performs the retirement
- **THEN** the resulting persisted state is already within the bound

### Requirement: A retired record can never be credited again

Retirement SHALL NOT make any record re-creditable. A record that is no longer
retained SHALL remain permanently non-creditable, and a re-delivery of that
record SHALL be detected and refused rather than credited.

Refusal SHALL be classified distinctly from "seen and deliberately ignored",
so that diagnostics can tell a retired-record re-delivery from an ordinary
duplicate, and neither may be reported as new credit.

#### Scenario: Re-delivery of a retired record credits nothing

- **WHEN** a record that has been retired from the durable history is delivered
  again
- **THEN** no Vitality is credited
- **THEN** the batch reports the record under a retired-record classification
- **THEN** the durable totals are unchanged

#### Scenario: Retirement never reverses or re-credits anything

- **WHEN** retirement occurs
- **THEN** no balance changes
- **THEN** no project's invested progress changes
- **THEN** the lifetime credited total is unchanged by the act of retiring

#### Scenario: A correction for a retired record is handled conservatively

- **WHEN** a higher-revision correction arrives for a record whose row has been
  retired
- **THEN** no additional credit is created
- **THEN** any reversal that would be required is not applied against a
  completed world
- **THEN** the outcome is classified and diagnosable

### Requirement: Lifetime aggregates are maintained, not recomputed on every read

The durable lifetime totals that appear in support diagnostics and in the
load-time cross-check SHALL be maintained as part of the state transition that
changes them, so that reading them is O(1) and does not depend on the size of
the history.

The maintained totals SHALL be cross-checked against a full recomputation during
load validation. A maintained total that disagrees with the recomputation SHALL
be reported as a validation violation naming the mismatch.

#### Scenario: Reading lifetime totals is independent of history size

- **WHEN** lifetime totals are read
- **THEN** the result does not depend on how many records are retained

#### Scenario: A tampered aggregate is caught on load

- **WHEN** a decoded state carries a maintained lifetime total that disagrees
  with a full recomputation from the retained history
- **THEN** validation reports a violation naming the aggregate
- **THEN** the state is not silently corrected on load

#### Scenario: Retirement preserves the aggregate

- **WHEN** records are retired
- **THEN** the maintained lifetime credited total is unchanged
- **THEN** the recomputation over the retained history still agrees with the
  maintained total, accounting for the durably recorded retired totals

### Requirement: Commit cost is bounded by a documented growth model

The documentation of performance budgets SHALL state the expected growth model
for canonical state, including what is expected to grow linearly, what is
expected to plateau, and the documented convergence bound.

The deterministic growth series that the build already produces SHALL remain
reproducible, and the bound SHALL be expressed in a way the existing gate can
assert.

#### Scenario: The growth series is still byte-reproducible

- **WHEN** the save-growth verification runs
- **THEN** the recorded series reproduces exactly for the existing checkpoints
- **THEN** the series demonstrates convergence rather than unbounded linear
  growth

#### Scenario: The documented bound is enforced by the existing gate

- **WHEN** the save-growth verification runs
- **THEN** a final state exceeding the documented convergence bound fails the
  gate with a message naming the bound and the observed value

### Requirement: In-memory lookup structures survive reload without rebuild cost

After a save is loaded, the structures used to answer "have I seen this record"
and "have I applied this transaction" SHALL be available without a full
recomputation proportional to the retained history on the first query.

#### Scenario: The first duplicate check after load does not scan the history

- **WHEN** a state with a large retained history is loaded and the first
  duplicate check is performed
- **THEN** the check does not require iterating every retained record

#### Scenario: Index state is never authoritative over the durable rows

- **WHEN** the in-memory index is inconsistent with the durable rows
- **THEN** the durable rows win
- **THEN** the inconsistency is not observable as a false duplicate or a lost
  credit
