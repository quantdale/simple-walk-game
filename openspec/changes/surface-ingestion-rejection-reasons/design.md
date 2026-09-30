## Context

See `proposal.md` (Why) for the evidence.

Constraints that shape the approach:

- The D-044 privacy contract is explicit and test-enforced: the projection may
  carry "classified enums, bounded counters, timestamps, and adapter-owned
  technical strings" and nothing else. Anything added here must fit inside that
  envelope, or it does not go in.
- `IngestionOutcomeState` is **already** persisted inside `GameState` and is
  documented as "bounded privacy-safe aggregate of the most recent ingestion
  batch ... never a second copy of ledger/world state". A per-reason breakdown
  is a fixed-size map keyed by an enum — bounded, and not a second copy of
  anything. It belongs here, not in a new store.
- The per-reason data already exists and is already correct:
  `GameSession.IngestActivityBatch` builds `rejectionCounts` as a
  `SortedDictionary<string, int>` and returns it on `IngestResult`. The change is
  almost entirely a *persistence and projection* change, not a computation
  change. That is what makes it low-risk and fast.
- The reason keys are currently `ActivityValidationStatus` enum names as
  strings. Persisting strings is fragile: a renamed enum member would silently
  orphan historical rows. Persisting the **ordinal** and projecting to the enum
  on read avoids that, at the cost of needing an explicit "unknown ordinal"
  handling. Since the persisted value is in the same save as the code that reads
  it, and the save is versioned, the trade is cheap either way — but ordinal
  storage is strictly more robust to a rename.
- The reflection-based no-leak sweep in
  `M5H1ContractHardeningTests.PlayerFacingSurfaces_NeverLeakRawExceptionTextOrHealthPayloads`
  currently walks six read models by explicit call. It cannot fail when a new
  read model is added, which is a coverage gap rather than a bug.
- The watermark age is floored to whole days:
  `watermarkAgeDays = (long)checkpointAge.TotalDays`. Anything under 24 hours
  reads as `0`, and a negative age is clamped to `0` as well. A support engineer
  therefore cannot distinguish "polled 5 minutes ago" from "polled 20 hours
  ago", and cannot distinguish either from "clock moved backwards" — both read 0.

## Goals / Non-Goals

**Goals**

- Make a lost-activity report diagnosable from durable evidence alone.
- Make "the source is stale" distinguishable from "the source is quiet".
- Preserve the D-044 privacy envelope exactly.
- Make the no-leak sweep fail when a read model is added and not covered.

**Non-Goals**

- Any logging framework, telemetry sink, or network path. The repository has no
  logging seam by design and the privacy review asserts zero network usage;
  adding one would invalidate that review.
- Deriving a *reconciliation scheduler*. This change makes pending-reconciliation
  state observable; it does not make the pipeline poll. Driving query windows
  from the watermark is M7 adapter work and is recorded in the master plan as
  deferred.
- Exposing per-player activity detail of any kind.
- Changing which records are accepted.

## Decisions

### 1. Persist the breakdown as an ordinal-keyed bounded map

`IngestionOutcomeState` gains a small map from rejection-reason ordinal to count,
plus a flag distinguishing "no rejections occurred" from "breakdown not
recorded". Ordinals rather than names, because a renamed enum member must not
orphan historical rows; an out-of-range ordinal on read projects as a classified
`Unknown` reason rather than throwing.

*Alternative considered:* persist the string keys as produced today. Rejected:
strings couple the save format to enum member names, which is exactly the kind of
implicit coupling R-018 warns about. The map is also already sorted, so ordinal
keying costs nothing.

### 2. Add the missing dispositions rather than only the breakdown

`IngestionResult` computes `DeletionsIgnored`, `StaleRevisionsIgnored` and
`QuantityClamped`, and the durable outcome does not record them. The spec
requires a support engineer to reconcile the durable counters against the total
received, which is impossible without them. These are three integers and they
complete the accounting.

### 3. Replace the whole-day watermark age with a classified reconciliation state

Keep the existing whole-day age (it is a useful coarse figure and
`M5H1ShellAcceptanceTests.Scenario05` asserts it is in `[28, 31]` after a 30-day
absence, so it must stay), and **add** a classified state derived from durable
evidence:

- `NeverAttempted` — no fetch has ever been attempted.
- `Current` — a successful fetch is newer than the staleness threshold.
- `Stale` — the last successful fetch is older than the threshold.
- `FailedSinceLastSuccess` — a fetch failed after the last success.

`NeverAttempted` is distinguishable from `Stale` because "no successful fetch
has ever happened" is a different fact from "the last one is old", and the
existing `IngestionOutcomeKind.NeverRun` already encodes the former. The
distinction matters: one is a fresh profile, the other is a broken integration.

The backward-clock case is deliberately **not** folded into `Current`: a negative
age is clamped for reporting, and the classified state is computed from the
durable last-success fact rather than from a comparison against a possibly-skewed
clock, so it cannot be fooled by a clock jump.

*Alternative considered:* report an exact `TimeSpan` age and let the reader
decide. Rejected: that pushes a policy decision (what counts as stale?) into
every consumer, and this repository's pattern is to project policy into
classified read models — the same reasoning as `ActivityStatusProjector`.

### 4. Make the no-leak sweep exhaustive by construction

Rather than a hand-maintained list of six read models, the sweep should
enumerate the session's read-model-producing members by reflection and assert
each one's string properties are marker-free, with an explicit allow-list for
the two support-only surfaces. That way adding a read model without a leak test
fails the suite rather than silently going uncovered.

*Alternative considered:* keep the explicit list and just add the three missing
models. Rejected: it fixes today's gap and leaves the same gap for the next
read model, which is a recurring failure mode in exactly the file whose whole
purpose is preventing leaks. The task list in the sibling backup-recovery change
also adds the three models explicitly; both should be done, with the
reflection-driven sweep being the durable fix.

### 5. Validate the new durable counters on load

`GameStateValidator` gains: counters are non-negative; the breakdown's sum equals
the recorded rejection total; the accepted count does not exceed the total
received. This is the same fail-closed posture the validator already takes for
every other durable field, and it means a tampered counter is caught at boot
rather than reported as fact.

## Risks / Trade-offs

- **[Persisting a map adds a little size to every save]**
  → Bounded and tiny: at most one entry per defined rejection reason, and the
  map is empty in the overwhelmingly common all-accepted case. This does not
  interact with the `state-durability` convergence work.

- **[A classified "stale" state needs a threshold, and thresholds are
  product judgement]**
  → The threshold is a single documented constant assigned explicitly in the
  task list, chosen well above any normal polling interval and well below the
  staleness at which a player would notice missing activity. It is recorded in
  `docs/ACTIVITY_PIPELINE.md` §14, which already names this field.

- **[Reflection-driven sweep could be brittle]**
  → It enumerates public, parameterless, readable members on the session that
  return a read model, and skips primitives. It must be written to ignore
  operations with side effects. Mitigation: the sweep runs against a
  fully-composed session with mature history, and an explicit allow-list keeps
  the two support-only surfaces green. The task list requires the sweep to be
  proven to fail when a deliberately-leaking read model is introduced.

- **[Ordinal-keyed reasons could drift from the enum]**
  → The enum is append-only in practice; the change must not reorder or remove
  existing members. A test asserting the ordinal set is stable for the
  persisted reasons prevents an accidental reorder. Out-of-range ordinals on read
  project as `Unknown` rather than throwing.

- **[More durable fields mean more to keep in sync]**
  → Mitigated by the validator checks and by `PRIVACY_REVIEW.md` being updated
  as a task, so the data inventory stays honest.

## Migration Plan

No migration. Additive fields on `IngestionOutcomeState`, which already decodes
as absent-means-`NeverRun` per D-036. Every existing save loads with "breakdown
not recorded" and "no fetch attempted", both of which are truthful for a save
written before this change existed.

Rollback: a plain revert. Extra fields are ignored by the old build.

## Open Questions

None. The staleness threshold and the ordinal-stability test are explicit task
items; neither changes the shape of this design.
