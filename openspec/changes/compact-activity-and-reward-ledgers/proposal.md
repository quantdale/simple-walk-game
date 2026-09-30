# Bound durable ledger growth so long-lived saves stay cheap to commit

## Why

Two durable structures grow without bound, forever, one entry per ingested
record, and both are re-serialized on **every** commit. The repository already
measures the consequence and gates it, so the numbers are not in dispute:

`scripts/verify-save-growth.sh` commits this deterministic series:

```text
day 7:  10,344 bytes
day 30:  23,752 bytes
day 90:  56,085 bytes
day 180: 104,615 bytes
day 365: 202,644 bytes
day 730: 394,401 bytes
```

That is linear, monotonic, and roughly 540 bytes per simulated day, with no
plateau. Projected to the maximum supported simulation horizon the CLI itself
accepts (`--days 3650`), a single profile is ~2 MB, and the store keeps a
full second copy in the backup generation, so each commit writes roughly
4 MB for a state change that may be a few hundred bytes of actual progress.

The growth is structural, not incidental:

- `ProcessedRecordLedgerState.Entries` keeps one `ProcessedRecordEntry` per
  logical record **forever**, including entries that credited zero Vitality
  ("Kept even when conversion yields zero Vitality so replays stay no-ops").
- `RewardLedgerState.Records` keeps one `LedgerRecord` per applied transaction
  forever.
- `RewardLedgerState.EnsureIndex()` rebuilds a `HashSet` over every record on
  first use after a load, so every boot pays O(records) before doing anything.
- `ProcessedRecordLedgerState.TotalVitalityCredited` is recomputed by
  iterating every entry, and it is called on **every** boot by
  `GameStateValidator` and by `GameSession.GetDiagnostics()`.
- `docs/PERFORMANCE_BUDGETS.md` §15 asked for exactly this series to be
  reported, and it now is — but nothing in the design ever asked what the series
  should converge to.

The trust requirement ("replay must never double-credit") is real, but it does
not require retaining a full row for every record a player has ever walked in
for the lifetime of a profile.

## What Changes

- The durable record history becomes **bounded by retention policy** rather than
  unbounded, with a documented, conservative rule for what may be retired and a
  durable, human-meaningful record of what was retired.
- Retirement preserves the exactly-once guarantee: a retired record is never
  re-creditable, and re-delivery of retired data is detected and refused rather
  than silently re-processed.
- O(1) maintained aggregates replace the O(n) recomputations in the hot path
  (`TotalVitalityCredited`), with the maintained value cross-checked against a
  full recomputation by the existing validator invariant so corruption is still
  caught.
- The reward ledger's in-memory index is retained across commits instead of
  being rebuilt after every load, and the growth series is extended with a
  documented convergence target that becomes a gate.
- `docs/PERFORMANCE_BUDGETS.md` gains the convergence bound; the existing
  deterministic series remains the regression baseline.

**Non-goals:** changing the reconciliation horizon, the conversion rule, the
identity scheme, or the exactly-once semantics. Retirement must be provably
invisible to the trust properties the red-team suite already pins.

## Capabilities

### New Capabilities

- `state-durability`: the durability contract for canonical state that grows
  over a profile's lifetime — what the durable record history must retain to
  keep exactly-once crediting true, how that history is bounded, how retired
  records remain non-re-creditable, which aggregates are maintained
  incrementally versus recomputed, and the cost characteristics of a commit.

### Modified Capabilities

<!-- None. `openspec/specs/` is empty at HEAD; `state-durability` is introduced
     here. It deliberately does not modify `activity-trust-pipeline`: this
     change alters how much history is *retained*, not how any record is judged.
     The two changes touch adjacent code and are sequenced, but their contracts
     are independent. -->

## Impact

**Evidence from the audit**

- `scripts/verify-save-growth.sh` — the deterministic growth series quoted above
  is committed as evidence and gated in `ci.yml`.
- `src/WalkGame.Domain/Activity/ActivityIngestion.cs` —
  `ProcessedRecordLedgerState.Entries` is an append-only
  `Dictionary<string, ProcessedRecordEntry>`; `TotalVitalityCredited` iterates
  `Entries.Values`; `UnappliedReversalVitality` is a separate scalar.
- `src/WalkGame.Domain/Activity/RewardLedger.cs` — `Records` is an append-only
  `List<LedgerRecord>`; `EnsureIndex()` rebuilds `_index` from `Records` on
  first use; `TotalVitalityCredited` iterates `Records`.
- `src/WalkGame.Domain/Validation/GameStateValidator.cs` — calls
  `state.ProcessedRecords.TotalVitalityCredited` and
  `state.Ledger.TotalVitalityCredited` on every boot, making both O(n) on the
  critical boot path.
- `src/WalkGame.Application/GameSession.cs` `GetDiagnostics()` — calls
  `ProcessedRecords.TotalVitalityCredited` on every read.
- `src/WalkGame.Infrastructure/Persistence/SaveCodec.cs` — re-serializes the
  entire `GameState` on every `Encode`.
- `src/WalkGame.Domain/Activity/ActivityIngestion.cs` — the dedup ledger
  deliberately retains zero-credit entries "so replays stay no-ops", which is
  the invariant retirement must preserve.
- Tests that already pin the properties retirement must not break:
  `ActivityRedTeamTests` (6), `SessionIngestionTests` (15),
  `SaveCodecRoundtripTests` (5), `RewardLedgerTests` (4),
  `ActivityIngestionTests` (13), `TemporalAnomalyTests` (7).

**Affected code**

- `src/WalkGame.Domain/Activity/ActivityIngestion.cs` — `ProcessedRecordLedgerState`.
- `src/WalkGame.Domain/Activity/RewardLedger.cs` — `RewardLedgerState`.
- `src/WalkGame.Domain/Validation/GameStateValidator.cs` — the cross-check
  invariant.
- `src/WalkGame.Application/ReadModels/DiagnosticsReadModel.cs` — lifetime
  counters must remain correct after retirement.
- `src/WalkGame.Infrastructure/Persistence/SaveCodec.cs` — no change expected.
- `docs/PERFORMANCE_BUDGETS.md` and `scripts/verify-save-growth.sh` — the gate.

**Schema impact**

`ProcessedRecordLedgerState` gains durable retirement bookkeeping. This is an
**additive** change following D-036: absent decodes to "nothing retired yet",
which is the correct semantics for every existing save. `SchemaVersions` is not
bumped, consistent with the M4 precedent.

**Affected tests**

- `ActivityIngestionTests`, `RewardLedgerTests`, `ActivityRedTeamTests`,
  `SessionIngestionTests`, `TemporalAnomalyTests`, `SaveCodecRoundtripTests`,
  `M4BackwardDecodingTests`, `MatureSaveMigrationTests`, plus both acceptance
  suites.

**Dependencies**

- Apply **after** `anchor-activity-acceptance-window-to-trusted-time`: that
  change alters which records are accepted at all, and retirement policy should
  be tuned against the post-change acceptance window.
- `bound-secondary-resource-economy` may alter growth slightly (more expedition
  grants) but neither depends on the other; either order is safe.
