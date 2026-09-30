## Purpose

Defines the trust contract that turns externally sourced activity records into
exactly-once game progression: which records are eligible, what time window they
must fall inside and what that window is anchored to, how a logical record is
identified and deduplicated, how corrections and deletions are applied
conservatively, what durable watermark records how far the pipeline has trusted
sources, and how every received record is accounted for in per-batch
diagnostics.

## ADDED Requirements

### Requirement: Activity records are accepted across a player absence

A record whose end falls before the current wall-clock time SHALL still be
accepted when it lies within the durable window the pipeline has not yet trusted,
so that activity accumulated while the app was closed is credited. The lower
bound of the acceptance window SHALL be derived from durable trusted progress
(most recent trusted record end and the reconciliation watermark) together with a
bounded floor, and SHALL NOT be the raw current time minus a fixed short
interval.

Rejecting a record outside the window SHALL remain possible and SHALL be
reported with a distinct reason, so a hostile or misconfigured source dumping
enormously old history remains bounded and observable.

#### Scenario: A returning player's whole backlog is credited

- **WHEN** a profile ingests a batch of records whose ends span the whole period
  since the last trusted record end, and the current time is well beyond a
  short fixed interval
- **THEN** every record in that backlog is accepted
- **THEN** each record's Vitality is credited exactly once
- **THEN** the reconciliation watermark advances to the latest end in the batch

#### Scenario: A record older than the trusted window is still rejected

- **WHEN** a record's end precedes the start of the trusted window by more than
  the bounded floor
- **THEN** the record is rejected
- **THEN** the rejection is reported under a distinct "outside the trusted
  window" reason and not under any other rejection reason
- **THEN** no durable state changes and the reconciliation watermark does not
  move

#### Scenario: A hostile old dump cannot re-open arbitrary history

- **WHEN** a single batch contains records that are far older than the trusted
  window
- **THEN** those records are rejected and credited nothing
- **THEN** records in the same batch that do fall inside the trusted window are
  still processed normally

#### Scenario: Replaying a previously accepted backlog credits nothing further

- **WHEN** a previously accepted batch is delivered again
- **THEN** every record in it is recognised as a duplicate
- **THEN** no additional Vitality is credited
- **THEN** the reconciliation watermark does not regress

### Requirement: The acceptance window is anchored to trusted time, not to raw device time

The upper bound of the acceptance window SHALL be evaluated against a trusted
time reference that is never earlier than the latest activity end the pipeline
has already durably trusted, and never earlier than the reconciliation
watermark. A record that is not meaningfully in the future relative to that
trusted reference SHALL be accepted even when the device's own clock is ahead of
the true time.

A record that is genuinely in the future relative to the trusted reference SHALL
still be rejected, so a source that pre-mints future activity remains bounded.

#### Scenario: A device clock running fast does not stop crediting

- **WHEN** the device clock is ahead of the true source time by more than the
  minimal skew allowance, and the records themselves are not ahead of the
  pipeline's already-trusted progress
- **THEN** the records are accepted and credited
- **THEN** the rejection reason "future timestamp" is not reported for them

#### Scenario: Genuinely future-dated records are still rejected

- **WHEN** a record's end is ahead of the pipeline's trusted reference by more
  than the skew allowance
- **THEN** the record is rejected with the "future timestamp" reason
- **THEN** no Vitality is credited and no watermark advance occurs

#### Scenario: A forward clock jump during absence does not create credit

- **WHEN** the device clock jumps far forward while the app is closed, and the
  source's records remain in the past relative to the pre-jump trusted progress
- **THEN** the records are accepted on the same terms as any other backlog
- **THEN** the pipeline never mints Vitality that the source did not report

#### Scenario: A backward clock movement never widens the window

- **WHEN** the current time moves earlier than the latest trusted record end
- **THEN** the accepted window's upper bound does not move earlier than the
  already-trusted end
- **THEN** the durable watermark never regresses

### Requirement: Every received record is judged by the same eligibility rules

Deletion markers SHALL be subject to the same eligibility rules as ordinary
activity records before they may alter durable state. A deletion marker that
fails any eligibility rule SHALL be rejected with a distinct reason and SHALL
NOT reverse value, SHALL NOT change the processed-record row, and SHALL NOT
advance the reconciliation watermark.

A deletion marker that is eligible SHALL advance the reconciliation watermark
only to its own end, consistent with the rule that the watermark records the
latest activity end durably trusted and never moves backwards.

#### Scenario: A malformed deletion marker changes nothing

- **WHEN** a deletion marker has a missing provider namespace, a malformed
  timestamp window, or a non-positive temporal extent
- **THEN** it is rejected under the corresponding eligibility reason
- **THEN** no credited value is reversed
- **THEN** the reconciliation watermark is unchanged by that marker

#### Scenario: A future-dated deletion marker cannot move the watermark

- **WHEN** a deletion marker is otherwise identifiable and eligible but its end
  is far in the future relative to the trusted reference
- **THEN** it is rejected
- **THEN** the reconciliation watermark is unchanged

#### Scenario: A well-formed deletion marker still reverses conservatively

- **WHEN** a deletion marker is eligible and identifies a logical record the
  pipeline has credited
- **THEN** the previously credited value is reversed, clamped to the unspent
  balance
- **THEN** any unclamped remainder is durably counted
- **THEN** the reconciliation watermark advances to that marker's end

#### Scenario: A repeated deletion marker is a no-op

- **WHEN** the same deletion marker is delivered more than once
- **THEN** no additional value is reversed
- **THEN** the batch diagnostics classify it as an ignored or stale revision

### Requirement: Per-batch trust diagnostics account for every received record

Every ingestion batch SHALL report, at minimum: total records received; records
accepted; records rejected grouped by their individual eligibility reason;
records recognised as duplicates; records applied as corrections; deletion
markers applied; deletion markers ignored; stale-revision redeliveries ignored;
records whose quantity was bounded; Vitality credited; and the cumulative
unapplied reversal total. The counts SHALL sum consistently to the total
received.

Rejected-record reasons SHALL be retained in the batch result in a form that a
caller can persist, so that the reason a player's activity was not credited is
recoverable after the process that produced the batch is gone.

#### Scenario: Rejection reasons sum to the total received

- **WHEN** any ingestion batch completes
- **THEN** accepted plus every rejection-reason count plus duplicates plus
  corrections plus applied deletions equals the total received

#### Scenario: A batch that is entirely rejected still reports why

- **WHEN** every record in a batch is rejected
- **THEN** the result reports one entry per distinct eligibility reason present
- **THEN** the result reports zero Vitality credited

#### Scenario: Rejection reasons survive the call that produced them

- **WHEN** a caller keeps the batch result beyond the end of the call
- **THEN** the per-reason rejection breakdown is still readable from it
