# M8-H1 Activity Red-Team Evidence (Workstream D)

Suite: `ActivityRedTeamTests` (Application). Every record — hostile or honest —
flows through the production `IngestActivityBatch` / `IngestFromSource` trust
pipeline; nothing mutates Vitality directly.

## Convergence matrix

Canonical reference: 60 valid records (30 durable source-ID rows + 30
fingerprint-only rows) across distinct windows, ingested in ordered batches with
session recreation.

Compared surfaces (all must match): Vitality balance, reward-ledger total and
record count, processed-ledger total and row count, unapplied-reversal counter,
completed-project count, landmark stage sum, producer lifetime totals, discovery
count, completed-expedition count, ecology/settlement stages, region completion,
ingestion checkpoint. State validator must be clean in every scenario.

| Hostile variant | Result |
|---|---|
| all 60 records in ONE batch | converges to reference |
| batches AND records reversed | converges |
| every record duplicated within its batch (dupes ignored) | converges |
| fresh session + restart between EVERY record | converges |
| 6 junk rows per batch (negative, zero, wrong unit, future >10 min, stale >14 d, end≤start) | junk fully rejected deterministically; honest subset converges |
| overlapping query windows through `IActivityRecordSource` (1-day overlap per window) | exactly-once; converges |
| 5,010-record duplicate flood after completion | all ignored, zero credit, state unchanged |
| replay of each completed hostile history | exact no-op |

## Correction / deletion semantics pinned by exact-value tests (D-029)

* Correction UP across restart: delta credited against net-applied value (+50 case).
* Correction DOWN: deltas against NET-APPLIED row value; clawback clamped to the
  unspent balance; unclawed remainder durably counted
  (`UnappliedReversalVitality`); processed row never outruns ledger totals.
* Deletion: reverses only what the balance funds; remainder counted durably;
  later positive correction converges back toward earned value.
* Duplicate deletion at same revision → `StaleRevisionsIgnored`.
* Deletion for never-credited record → `DeletionsIgnored`.
* Unidentifiable deletion (no namespace) → counted in diagnostics.

## Interpretation

No duplicate-credit or lost-credit defect exists on any tested path. The only
order-dependent economic surface is the documented conservative-clawback clamp,
which is deliberately balance-dependent (player-trust policy, ACTIVITY_PIPELINE
§11) and is pinned by exact-value assertions rather than cross-permutation equality.
