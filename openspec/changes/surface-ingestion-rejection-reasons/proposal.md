# Surface why activity was rejected, durably, in support diagnostics

## Why

A support engineer looking at a player who says "my steps stopped counting" has
almost no information today. The pipeline computes exactly the right thing and
then throws it away at the end of the call.

`IngestResult.RejectionCounts` is a `SortedDictionary<string, int>` of
per-reason rejection counts, produced for every batch, correct in every red-team
test. But:

- it is a property of a short-lived result object with no persistence path;
- `GameState` persists only `LastIngestionOutcome`, whose
  `DiagnosticsIngestionRow` carries the **aggregate** `Rejected` count and no
  per-reason breakdown;
- so `OutsideHorizon` (the player's whole backlog was older than the acceptance
  window) and `ZeroQuantity` (a source returned a zero record, entirely normal)
  are indistinguishable in the only durable evidence that survives a restart.

That distinction is the difference between "this is a product bug losing a
player their activity" and "this is normal source noise", and the two failure
modes the repository cares most about — R-003 (valid activity silently lost) and
R-002 (double crediting) — are exactly the ones that need it.

A second gap sits next to it. `IngestionCheckpointUtc` is durable evidence that
the pipeline records and **never uses** for anything except reporting an age
days-old integer. `ACTIVITY_PIPELINE.md` §14 lists "whether reconciliation is
pending" as a source-health field the adapter should expose, and no such
derivation exists. A support engineer cannot tell "the source has not been
polled in three weeks" from "the source was polled and returned nothing new",
because the diagnostics report a single watermark age in whole days and nothing
about whether a fetch has been attempted since.

## What Changes

- The durable ingestion outcome retains the **per-reason rejection breakdown**,
  so the reason a player's activity was not credited survives a restart.
- The durable outcome records whether a fetch has been **attempted** since the
  last successful fetch, so a stale source is distinguishable from a silent one.
- A bounded, privacy-safe reconciliation-pending derivation is added to the
  support diagnostics projection, so a support engineer can see at a glance
  whether the pipeline is waiting for data it has not received.
- Rejection reasons are exposed as classified enums and counts only, never as
  raw provider text, preserving the D-044 privacy contract.
- The reflection-based no-leak sweep is extended to cover every
  player-facing and support-facing read model, so the new fields cannot become
  a leak path.

**Non-goals:** adding a logging framework, adding any network upload, exposing
raw record contents, or changing which records are accepted (that is the
trusted-time anchor change).

## Capabilities

### New Capabilities

- `support-diagnostics`: the bounded, privacy-safe operational projection —
  boot and recovery evidence, schema and content versions, durable ingestion
  outcomes with their per-reason rejection breakdown, reconciliation-watermark
  and pending-reconciliation state, and the guarantee that no raw record,
  payload or exception text can reach it.

### Modified Capabilities

<!-- None. `openspec/specs/` is empty at HEAD; `support-diagnostics` is
     introduced here. It is a separate capability from
     `activity-trust-pipeline` because it is a read-side contract: the
     pipeline decides, diagnostics reports. -->

## Impact

**Evidence from the audit**

- `src/WalkGame.Application/GameSession.cs` `IngestActivityBatch` — builds
  `rejectionCounts` and returns it on `IngestResult`; writes
  `game.LastIngestionOutcome` with only aggregate counts.
- `src/WalkGame.Domain/Activity/IngestionOutcomeState.cs` — has
  `TotalReceived`, `Accepted`, `Rejected`, `DuplicatesIgnored`,
  `CorrectionsApplied`, `DeletionsApplied`, `VitalityCredited`,
  `UnappliedReversalVitality`, `ErrorCategory`; no per-reason breakdown and no
  "deletion markers ignored" or "stale revisions ignored" counts even though
  `IngestResult` computes them.
- `src/WalkGame.Application/ReadModels/DiagnosticsReadModel.cs` —
  `DiagnosticsIngestionRow.FromState` copies the aggregate fields only.
- `src/WalkGame.Application/GameSession.cs` `GetDiagnostics()` — computes
  `checkpointWatermarkAgeDays` as `(long)checkpointAge.TotalDays`, which floors
  to 0 for anything under 24 hours, so "3 hours stale" and "just polled" both
  read as 0.
- `src/WalkGame.Application/GameSession.cs` `IngestFromSource` — records
  `ErrorCategory = ex.GetType().Name` on a fetch failure, but nothing records
  *when* a fetch was last attempted.
- `tests/WalkGame.Application.Tests/DiagnosticsReadModelTests.cs` (8 tests) —
  covers counters, migration evidence, no-leak; not the per-reason breakdown.
- `tests/WalkGame.Application.Tests/M5H1ContractHardeningTests.cs`
  `PlayerFacingSurfaces_NeverLeakRawExceptionTextOrHealthPayloads` — walks 6
  read models; `GetDiscoveries()`, `GetExpeditions()` and
  `GetPendingReturnSummary()` are **not** in the sweep.

**Affected code**

- `src/WalkGame.Domain/Activity/IngestionOutcomeState.cs` — additive durable
  fields.
- `src/WalkGame.Application/GameSession.cs` — populate the new fields; refine
  the watermark reporting.
- `src/WalkGame.Application/ReadModels/DiagnosticsReadModel.cs` — expose them.
- `src/WalkGame.Domain/Validation/GameStateValidator.cs` — validate the new
  durable fields (counts non-negative, breakdown sums consistently).
- `tests/.../M5H1ContractHardeningTests.cs` — extend the sweep.

**Schema impact**

Additive, following D-036: absent decodes to "no breakdown recorded yet", which
is correct for every existing save. `SchemaVersions` is not bumped. A bounded
per-reason breakdown is a small fixed-size map (one entry per eligibility
reason), not a growing collection, so it does not interact with the
`state-durability` change.

**Dependencies**

- Apply **after** `anchor-activity-acceptance-window-to-trusted-time`, whose spec
  requires the per-reason breakdown to be retained and readable; this change is
  what makes it durable.
- Independent of the save-recovery and content-compatibility changes.
