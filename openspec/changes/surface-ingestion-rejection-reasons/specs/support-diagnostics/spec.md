## Purpose

Defines the bounded, privacy-safe operational projection used for support: boot
and recovery evidence, schema and content versions, the durable outcome of the
most recent ingestion batch including its per-reason rejection breakdown,
reconciliation-watermark and pending-reconciliation state, and the guarantee
that no raw record, payload or provider text can ever reach it.

## ADDED Requirements

### Requirement: The durable ingestion outcome explains every rejected record

The durable record of the most recent ingestion batch SHALL retain the
per-reason breakdown of rejected records, so that the reason a player's
activity was not credited remains knowable after the process that performed the
ingestion has exited.

The breakdown SHALL be carried as classified reasons and counts only, SHALL be
bounded in size, and SHALL remain readable across restarts.

#### Scenario: The reason a record was rejected survives a restart

- **WHEN** a batch containing rejected records completes
- **THEN** the durable outcome records the count per rejection reason
- **WHEN** the profile is restarted and diagnostics are read
- **THEN** the per-reason breakdown is still present and unchanged

#### Scenario: Normal source noise is distinguishable from lost activity

- **WHEN** records were rejected only for reasons that indicate a benign source
  condition
- **THEN** the breakdown reports exactly those reasons
- **THEN** no reason indicating a bounded-window or clock-related loss is
  reported

#### Scenario: The breakdown is bounded

- **WHEN** a batch rejects records of many distinct reasons
- **THEN** the durable breakdown contains at most one entry per defined
  rejection reason
- **THEN** the stored size does not grow with the number of records in the batch

#### Scenario: An empty breakdown is represented unambiguously

- **WHEN** no record in a batch was rejected
- **THEN** the durable outcome records an empty breakdown
- **THEN** this is distinguishable from a breakdown that was never recorded

#### Scenario: A save with no recorded breakdown decodes cleanly

- **WHEN** a save written before breakdown recording is loaded
- **THEN** it decodes successfully
- **THEN** the breakdown is reported as not recorded rather than as zero
  rejections

### Requirement: The durable outcome accounts for every disposition of a batch

The durable outcome SHALL record every disposition the batch result computes:
accepted, duplicates ignored, corrections applied, deletions applied, deletions
ignored, stale revisions ignored, and records whose quantity was bounded. A
support engineer SHALL be able to reconcile the durable counters against the
total received without the batch result in hand.

#### Scenario: Every computed disposition is durable

- **WHEN** any batch completes
- **THEN** every disposition the batch computed is present in the durable
  outcome
- **THEN** the dispositions plus rejections plus accepted account for the total
  received

#### Scenario: Counters are validated on load

- **WHEN** a decoded state carries durable ingestion counters
- **THEN** a negative counter is reported as a validation violation
- **THEN** a breakdown that does not reconcile with the recorded rejection total
  is reported as a validation violation

### Requirement: Reconciliation pending state is derivable and reportable

The projection SHALL make it possible to distinguish a source that has not been
polled recently from a source that was polled and returned nothing new, and a
successful batch from a failed fetch. The distinction SHALL be derivable from
durable evidence that survives a restart.

The reported reconciliation state SHALL be a classified value, not a raw
timestamp, and SHALL be accompanied by a watermark age that is precise enough to
distinguish "recent" from "stale" without a reader doing arithmetic.

#### Scenario: A never-polled profile is distinguishable from a stale one

- **WHEN** no fetch has ever been attempted
- **THEN** the reconciliation state reports that no fetch has been attempted
- **AND** this is distinct from a state where a fetch was attempted and failed

#### Scenario: A stale source is distinguishable from a current one

- **WHEN** the last successful fetch is recent
- **THEN** the reconciliation state reports that the source is current
- **WHEN** the last successful fetch is older than the documented staleness
  threshold
- **THEN** the reconciliation state reports that reconciliation is pending
- **THEN** the reported age is precise enough to distinguish the two cases
  without a reader converting it

#### Scenario: A failed fetch is durably classified

- **WHEN** a fetch attempt fails before any record reaches the trust pipeline
- **THEN** the durable outcome records the failure with a classified category
- **THEN** prior credited progress is unchanged
- **THEN** a later successful batch replaces the failure classification

### Requirement: The projection cannot leak sensitive content

The projection SHALL carry classified enums, bounded counters, timestamps, and
adapter-owned technical strings only. It SHALL NOT carry raw activity records,
provider payloads, or exception text generated by this codebase. Every string
the projection includes SHALL be length-bounded.

Every player-facing and support-facing read model reachable from a session SHALL
be covered by an automated no-leak sweep that plants a marker in a provider
error and adapter technical detail and asserts the marker appears in no
read model, and appears in the support projection only in bounded, gated form.

#### Scenario: A planted marker never reaches a player-facing read model

- **WHEN** a provider throws an error containing a distinctive marker
- **THEN** no player-facing read model contains that marker

#### Scenario: A planted marker never reaches the support projection

- **WHEN** an adapter supplies technical detail containing a distinctive marker
- **THEN** the support projection reports the failure only as a classified
  category
- **THEN** any adapter-supplied technical string is length-bounded
- **THEN** the support projection is the only surface where such a string may
  appear

#### Scenario: The sweep covers every reachable read model

- **WHEN** the no-leak sweep is enumerated
- **THEN** every read model reachable from a session is included in it
- **THEN** adding a new read model without adding it to the sweep fails the
  suite
