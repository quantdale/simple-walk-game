# M8-H1 Long-Horizon Evidence (Workstream F)

Harness: `tools/simulation longhaul` (deterministic, real trust pipeline, fresh
session from disk every app-closed day, auto-queue policy identical to `walk`).
Seed 42; base instant 2026-01-01T08:00:00Z.

## Measured runs

| days | shape | processed rows | ledger records | ledger vitality | save bytes | projects completed | region completed | violations | wall ms |
|---:|---|---:|---:|---:|---:|---:|---|---:|---:|
| 30 | flat 8k/day | 30 | 30 | 2,400 | 23,393 | 4/19 | no | 0 | 1,045 |
| 90 | flat 8k/day | 90 | 90 | 7,200 | 55,712 | 8/19 | no | 0 | 2,732 |
| 180 | flat 8k/day | 180 | 180 | 14,400 | 104,236 | 15/19 | no | 0 | 5,291 |
| 365 | flat 8k/day | 365 | 365 | 29,200 | 202,280 | 19/19 | yes | 0 | 10,896 |
| 365 | irregular weekly | 365 | 365 | 51,220 | 203,152 | 19/19 | yes | 0 | 10,717 |
| 400 | absence (60 active / 180 silent / resume) | 220 | 220 | 26,400 | 126,984 | 19/19 | yes | 0 | 9,330 |

Reproduce with e.g.:

```
dotnet run --project tools/simulation -- longhaul --save <dir> --days 365 --shape flat \
  --steps-per-day 8000 --at 2026-01-01T08:00:00Z
```

## Growth analysis

* Save size grows linearly at ≈554 bytes per ingestion day (~202 KB after one
  year of daily activity). Producers, arcs and completion markers are fixed-size.
* The processed-record ledger grows exactly one row per ingested logical record;
  the reward ledger one record per transaction. Both are UNBOUNDED by design:
  exactly-once crediting requires durable identity for every record a provider
  might replay (D-028 consequence). Per campaign policy NO pruning was implemented
  — no formal retention/reconciliation proof exists that would make deletion safe,
  and exactly-once outranks file size. At measured rates a decade of daily use is
  on the order of a few MB: acceptable for an offline-first mobile title, revisited
  only with a proven compaction design.
* Wall time scales linearly (~29 ms/day incl. full save rewrite + decode/validate):
  no accidental O(n²) appeared at mature sizes. Boot cost is dominated by one
  encode+decode+validate over the whole state, which is O(state size).

## Absence behavior

The 400-day absence run (provider silent days 61–240) completes Region 1 with only
220 processed rows: silence creates no retroactive production debt, no fabricated
activity, no checkpoint regression, and validator-clean state throughout.
