## Context

See `proposal.md` (Why) for the reproduction and the evidence. What constrains
the design:

- The durable evidence needed to anchor the window already exists and is already
  invariant-checked: `GameState.IngestionCheckpointUtc` (the reconciliation
  watermark) and `ProcessedRecordLedgerState` (every accepted record, with its
  identity, eligible steps and credited Vitality). The validator already refuses
  to load a save where processed-record credited Vitality exceeds the reward
  ledger total, so this evidence is trustworthy.
- `GameSession.IngestActivityBatch` already computes a running
  `maxTrustedEndUtc` seeded from `game.IngestionCheckpointUtc` and advanced by
  each accepted record, then assigns it back to `game.IngestionCheckpointUtc`
  once at the end. That is exactly the "latest end durably trusted" quantity the
  window needs — it is simply not used as an input to validation today.
- Validation is currently a pure static function
  (`ActivityValidationPolicy.Validate(record, nowUtc)`) with no access to game
  state. The change must not turn the Domain's validation into something that
  needs the whole aggregate, because `scripts/verify-architecture.sh` rule 5
  keeps Domain free of anything ambient and the existing `ActivityIngestionTests`
  (13 tests) call `Validate` directly with two arguments.
- The per-record quantity clamp (`MaxStepsPerRecord = 250_000`) and the integer
  conversion rule (`StepConversionRuleV1`, 100 steps → 1 Vitality, floor) are
  deliberately unchanged; committed pacing evidence depends on both.
- Committed byte-reproducible evidence under `docs/evidence/m4/` is produced by
  `tools/simulation profile` and gated by `scripts/verify-pacing-evidence.sh`.
  Every profile ingests day-by-day, so widening the window must not perturb any
  of those reports.
- `M4Region1AcceptanceTests` and `M3AmbientProgressionAcceptanceTests` assert
  byte-identical canonical state across reruns. Any change here must preserve
  byte-identical output for the same inputs.

## Goals / Non-Goals

**Goals**

- Make a returning player's entire un-trusted backlog creditable, without
  removing the anti-abuse bound on unbounded old history.
- Stop the pipeline from being disabled wholesale by a device clock that is
  merely fast.
- Make deletion markers obey the same eligibility rules as ordinary records.
- Keep validation a pure, directly testable domain function.

**Non-Goals**

- Introducing a scheduled/periodic reconciliation pass that drives query windows
  from the watermark. `IngestionCheckpointUtc` is currently evidence, not a
  driver; making it a driver is the M7 adapter's job and is recorded in the
  master plan as deferred.
- Any change to conversion, clamping, dedup identity, correction policy or the
  save schema.
- Storing per-reason rejection counts durably — that is the separate
  diagnostics change, which consumes the per-reason breakdown this change makes
  readable.

## Decisions

### 1. Anchor to a `TrustedTime` value computed once per batch

Introduce a small immutable value in the Domain that carries the trusted
reference for a batch: the current time, the latest activity end already durably
trusted, and the reconciliation watermark. The acceptance window is then a pure
function of that value and the record.

*Why not just pass the watermark into `Validate`?* Because the upper bound also
needs "latest record end ever trusted", which is not a single stored field — it
is the maximum `EndUtc` implied by the processed-record ledger, and recomputing
that per record would be O(n) per record. Computing it once per batch and
threading it through keeps `Validate` O(1) per record and keeps it pure.

*Why not widen the constants?* Because a wider fixed window is still wrong when
the device clock is fast, and still too narrow for a 45-day absence. The problem
is the anchor, not the width.

### 2. The window's lower bound is `min(watermark, latestTrustedEnd) - floor`

The floor is a new, documented, still-bounded constant. It answers: "how far
before the last thing we trusted will we still believe a record?" Its value
should be chosen to comfortably exceed the longest absence the product
advertises (MASTER_PLAN §10 treats long absence as normal) while remaining far
below "the entire history of the device". The exact value is a balance decision;
`design.md` deliberately does not hard-code it, and the task list requires the
implementer to record the chosen value in `docs/DECISIONS.md` as a superseding
amendment to D-030.

The important structural property is that the lower bound **moves forward as
the player progresses**, so the window is a sliding, trust-anchored window
rather than a fixed "last 14 days of wall clock".

### 3. Keep the anti-abuse rejection, keep the distinct reason

Records older than the floor are still rejected, and the reason code stays
distinct. `ReconciliationHorizon` keeps existing as the *name* of that concept
so the 13 `ActivityIngestionTests` cases and the `SessionIngestionTests` case
named `RecordBeyondReconciliationHorizon_IsRejectedWithoutSideEffects` keep
compiling and keep their meaning; the constant's value and its reference point
change, and that test is re-scoped to use a record genuinely outside the new
trusted window (a record far older than the watermark/floor) rather than a
record that is merely "fifteen days ago on a fresh profile".

Re-scoping rather than deleting matters: the anti-abuse requirement from D-030
is still valid and must stay covered by an automated test.

### 4. Validate deletions through the same policy, then dispatch

Restructure the batch loop so that eligibility is evaluated for **every** record
including deletion markers, and only then dispatched to the deletion handler. The
deletion handler keeps its own additional checks (is the provider identifiable,
does the processed-record row exist, is the revision newer), which are *policy*
decisions, not *eligibility* decisions.

Consequence: a malformed deletion marker that was previously counted as
"ignored" is now counted as "rejected" under a specific reason. That is a
deliberate, documented change in diagnostic classification — it is strictly more
informative, and `SessionPersistenceHardeningTests.DeletionMarker_WithoutIdentifiableNamespace_IsCountedInDiagnostics`
must be re-scoped accordingly (a record with an empty namespace is still
"ignored" as a policy outcome; a record with a malformed timestamp is now
"rejected" as an eligibility outcome).

### 5. Compute the trusted reference once, before the loop

`maxTrustedEndUtc` is already threaded through the loop and already seeded from
the watermark. The change adds one pass over the processed-record ledger to
compute the latest trusted record end before the loop starts, and derives the
upper-bound anchor as the max of that, the watermark, and the injected now. The
pass is O(entries) once per batch, against a ledger that the save-growth evidence
shows reaching ~19,000 entries at day 730 — cheap relative to the single
whole-state serialisation that already happens once per batch.

### 6. Per-reason rejections stay in the batch result, uncapped but bounded by construction

`IngestResult.RejectionCounts` already exists as a
`SortedDictionary<string, int>`. The change makes the counts complete and
self-consistent (the spec now requires accepted + every rejection + duplicates +
corrections + deletions == total received) and guarantees the dictionary is
exposed on the immutable result object rather than discarded. A caller that wants
to persist it is the diagnostics change.

## Risks / Trade-offs

- **[A wider window accepts records the player genuinely already had credited
  under a different device's clock]**
  → Deduplication is by stable source record ID or content fingerprint, not by
  time, so a re-delivery is still a no-op. The window widens *what may be
  considered*, not *what may be credited twice*.

- **[A hostile source could now submit a large batch of old-but-within-window
  records and mint Vitality]**
  → The window is anchored to progress the player has already made, is bounded
  by a floor, is still subject to the per-record quantity clamp and the integer
  conversion rule, and the watermark advances monotonically. This is the same
  trust posture as before, just anchored to the right reference.

- **[Changing the window's reference point could perturb committed pacing
  evidence]**
  → All seven profiles ingest day-by-day, inside any window, so their reports
  must be byte-identical. `scripts/verify-pacing-evidence.sh` is the gate and is
  a mandatory verification task; if any report changes, the window change is
  wrong and must be reworked, not the evidence regenerated.

- **[Re-classifying malformed deletion markers from "ignored" to "rejected"
  changes diagnostic counters]**
  → Deliberate and more informative. Two existing tests must be re-scoped with
  their intent preserved, and the change is listed explicitly in the task list
  so it is a reviewable decision rather than a silent regression.

- **[A fast device clock means the watermark can be ahead of true time]**
  → Accepted deliberately. The watermark records "latest activity end durably
  trusted", not "true current time". Anchoring to it means a fast clock degrades
  the *anti-future* bound rather than disabling crediting entirely, which is the
  correct trade for a trust-first product.

## Migration Plan

No save schema change, no migration. The durable evidence needed already exists
in schema v1 and v2 saves. Older saves that were written while a fast clock
inflated the watermark will simply have a more conservative window until real
progress advances it; that is a safe direction.

Rollback is a plain revert.

## Open Questions

None blocking. The exact floor value for the lower bound is a documented
calibration decision that the task list assigns explicitly; it does not change
the shape of this design, only one constant plus its committed evidence.
