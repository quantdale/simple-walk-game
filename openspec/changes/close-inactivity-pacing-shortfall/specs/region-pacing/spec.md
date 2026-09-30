## Purpose

Defines how authored restoration content paces against real-world activity: the
activity profiles the region is balanced against, the closure criterion that
defines completion, the documented targets each profile is measured against, and
the rule that every balance change is re-measured with its evidence regenerated
rather than assumed.

## ADDED Requirements

### Requirement: Every named activity profile reaches region closure

For each activity profile the region is balanced against, a player following
that profile's activity pattern SHALL be able to reach region closure within the
documented horizon. A profile that cannot reach closure SHALL NOT be an accepted
balance outcome; it SHALL be treated as a defect in the balance or in the
closure criterion.

This applies to every profile, including low-frequency and intermittent profiles
and profiles containing long absences. "A player who walks rarely can never
finish" is a product defect, not an acceptable difficulty outcome.

#### Scenario: A weekday-only player with a long absence reaches closure

- **WHEN** a profile models activity on weekdays only, with silent weekends and
  a multi-week absence
- **THEN** region closure is reached within the documented horizon
- **THEN** every authored chain reaches completion
- **THEN** no chain is left permanently unreachable

#### Scenario: A very low-activity profile reaches closure

- **WHEN** a profile models sustained low daily activity
- **THEN** region closure is reached within the documented horizon
- **THEN** the progression remains legible rather than becoming an opaque wait

#### Scenario: A pathological profile reaches closure without becoming trivial

- **WHEN** a profile models implausible source values
- **THEN** region closure is reached
- **THEN** bounded quantity handling remains observable and is reported

#### Scenario: A profile that cannot reach closure is reported as a defect

- **WHEN** a pacing run reaches the horizon with one or more chains incomplete
  and the region not closed
- **THEN** the run is reported as a balance failure
- **THEN** the incomplete chains are named
- **THEN** the report is not committed as acceptable evidence

### Requirement: Closure is defined by a criterion, not by a raw effort total

Region closure SHALL be defined by an explicit, authored closure criterion that
is reachable by every profile the region is balanced against. Closure SHALL NOT
be implicitly equivalent to "the player spent a fixed total amount of a single
resource on exactly the authored roster", because that makes the game's ending
brittle against any change to costs, conversion or content.

The closure criterion SHALL be authored content, validated with the rest of the
content graph, and reported by the pacing harnesses.

#### Scenario: Closure is reported against the authored criterion

- **WHEN** a pacing run completes
- **THEN** the report states whether the authored closure criterion was met
- **THEN** the report does not infer closure from a resource total alone

#### Scenario: An unreachable closure criterion is a content failure

- **WHEN** the authored closure criterion cannot be met by a profile that the
  region is balanced against
- **THEN** content validation or the pacing gate reports a failure naming the
  criterion

### Requirement: Pacing reports state both requested and eligible activity

A pacing report SHALL state the activity the source **requested** and the
activity that was **eligible** after bounded-quantity handling, and SHALL state
the effective conversion ratio actually achieved. A report that presents only a
requested total SHALL NOT be used to characterise the conversion rate.

#### Scenario: Bounded handling is visible in the report

- **WHEN** a profile triggers bounded-quantity handling
- **THEN** the report states both the requested total and the eligible total
- **THEN** the report states the effective eligible-steps-per-vitality ratio

#### Scenario: The reported ratio is reproducible from the report

- **WHEN** a reader divides the eligible step total by the credited vitality
- **THEN** the result matches the ratio the report prints

#### Scenario: The pathological profile's report no longer implies a reduced
conversion rate

- **WHEN** the pathological pacing report is regenerated
- **THEN** the printed effective ratio matches the ratio computed from its own
  eligible and credited figures
- **THEN** no documentation states a conversion rate inconsistent with it

### Requirement: Every balance change regenerates all pacing evidence

Any change to authored costs, conversion behaviour, bounded handling, or the
closure criterion SHALL regenerate the deterministic pacing report for every
balanced profile in the same change, and the committed evidence SHALL be updated
to match. Evidence SHALL NOT be regenerated silently: each report's diff against
the previous committed version SHALL be reviewed and its significance explained
in the decision log.

#### Scenario: A cost change is only accepted with regenerated evidence

- **WHEN** authored content numbers change
- **THEN** the pacing verification fails until the committed reports are
  regenerated
- **THEN** the regenerated reports are committed alongside the content change

#### Scenario: A diff is explained, not just regenerated

- **WHEN** pacing evidence is regenerated
- **THEN** the decision log states which completion days moved, by how much, and
  why the movement is acceptable against the documented targets

#### Scenario: Documented targets are ratified or superseded explicitly

- **WHEN** a balance change alters a profile's measured outcome relative to its
  documented target
- **THEN** the target is either reaffirmed or explicitly superseded in the
  decision log
- **THEN** no target is left silently violated

### Requirement: Interim progress is meaningful even before closure

A player who cannot yet reach closure SHALL still experience understandable,
legible progress toward it. Progress toward a chain that cannot complete within
the horizon SHALL NOT be silent, and the player-facing surfaces SHALL remain
consistent with the canonical state that produced them.

#### Scenario: Partial chain progress is observable

- **WHEN** a profile leaves a chain incomplete at the horizon
- **THEN** the canonical state records that chain's partial progress
- **THEN** the surfaces report the chain's real progress rather than a stalled
  or reset state
