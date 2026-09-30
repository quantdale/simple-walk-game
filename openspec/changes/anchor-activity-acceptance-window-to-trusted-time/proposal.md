# Anchor the activity acceptance window to trusted time

## Why

The trust pipeline decides whether a record is acceptable by comparing the
record's timestamps against the **raw device wall clock**: anything ending more
than 14 days before "now" is rejected, and anything ending more than 10 minutes
after "now" is rejected. Both anchors are wrong for the two situations the
product is explicitly built for.

1. A player who returns after a two-week holiday has a provider backlog that is
   entirely older than 14 days. **Every record in that backlog is rejected**, so
   the activity that earned the most goodwill silently disappears.
2. A device whose clock runs even slightly fast (minutes, not days — routine on
   phones that resynchronise late) makes **every** record look future-dated.
   Activity stops crediting entirely, with no player-visible explanation.

A third, smaller asymmetry compounds both: deletion markers skip validation
altogether and so can push the durable reconciliation watermark anywhere.

## What Changes

- The acceptance window becomes **anchored to trusted time** rather than to raw
  wall clock. The trusted reference is the greater of the injected clock and the
  durable evidence the pipeline already keeps: the ingestion watermark and the
  latest record end the pipeline has already trusted.
- A returning player's backlog is accepted. The lower bound of the window is
  derived from the durable watermark (and a bounded floor), so a player cannot be
  re-credited for records already past the watermark *and* cannot lose the
  records that arrived during the absence.
- The upper bound keeps rejecting genuinely future-dated records, and the skew
  allowance is widened only as far as the trusted anchor requires, so
  anti-abuse behaviour is preserved for a device whose clock jumps forward while
  it is offline.
- Deletion markers are validated by the **same** eligibility rules as ordinary
  records before they may affect durable state.
- The accepted/rejected boundary is covered by new tests for: a 30-day backlog
  after absence, a fast device clock, a forward clock jump during absence, a
  hostile old dump, and malformed deletion markers.

**Non-goals:** changing the steps-to-Vitality conversion, the per-record quantity
clamp, the exactly-once dedup ledger, correction/clawback policy, or the save
schema. Those are unchanged by this proposal.

## Capabilities

### New Capabilities

- `activity-trust-pipeline`: the contract that turns externally sourced
  activity into exactly-once progression — the eligibility and bounding rules,
  the time window within which records are acceptable and what that window is
  anchored to, durable record identity and deduplication, the correction and
  deletion policy, the durable reconciliation watermark, and the per-batch
  trust diagnostics that account for every received record.

### Modified Capabilities

<!-- None. `openspec/specs/` is empty at HEAD; `activity-trust-pipeline` is
     introduced here and later changes reuse this path. -->

## Impact

**Evidence from the audit**

- `src/WalkGame.Domain/Activity/ActivityIngestion.cs` —
  `ReconciliationHorizon = TimeSpan.FromDays(14)` and
  `MaxFutureSkew = TimeSpan.FromMinutes(10)`, both compared against the
  injected `nowUtc`.
- `src/WalkGame.Application/GameSession.cs` — the `if (record.IsDeletion)`
  branch is handled **before** `ActivityValidationPolicy.Validate` is ever
  called, and `ProcessDeletion` advances `maxTrustedEndUtc` from the
  unvalidated `record.EndUtc`.
- `tests/WalkGame.Application.Tests/SessionIngestionTests.cs` —
  `RecordBeyondReconciliationHorizon_IsRejectedWithoutSideEffects` pins the
  15-day rejection (and is therefore the test that must be re-scoped by this
  change, not deleted silently).
- `M5H1ShellAcceptanceTests.Scenario05_ThirtyDayReturn_BoundedCalmLongAbsence`
  returns after 30 days but deliberately ingests **nothing** during the absence,
  so the realistic "provider hands back the whole backlog on return" case has no
  coverage anywhere in the suite.
- `docs/DECISIONS.md` D-030 records the 14-day/10-minute constants as an
  accepted decision, with the consequence "genuinely older backfills need an
  explicit, separately-designed import path". That import path does not exist,
  and the D-003/D-004 offline-first thesis is what it was meant to protect.

**Affected code**

- `src/WalkGame.Domain/Activity/ActivityIngestion.cs` — window policy and its
  inputs.
- `src/WalkGame.Application/GameSession.cs` — batch loop, deletion path,
  watermark advance.
- `src/WalkGame.Domain/GameState.cs` — no shape change expected; the watermark
  field already exists.
- `src/WalkGame.Domain/Validation/GameStateValidator.cs` — only if new durable
  evidence is added, which this proposal avoids.

**Affected tests**

- `SessionIngestionTests` (15 tests, one of which is re-scoped),
  `TemporalAnomalyTests` (7 tests — boundary cases that encode the current
  constants), `ActivityRedTeamTests` (6 tests), `ActivityIngestionTests`
  (13 tests), `M5H1ShellAcceptanceTests` (11 tests),
  `M4Region1AcceptanceTests` and `M3AmbientProgressionAcceptanceTests` (both
  assert byte-identical determinism and must stay byte-identical).

**Dependencies**

- None. Lands independently of the save-recovery change; both are foundational
  and neither depends on the other.
- The rejection-reason diagnostics change
  (`surface-ingestion-rejection-reasons`) builds on this one and should follow it.
