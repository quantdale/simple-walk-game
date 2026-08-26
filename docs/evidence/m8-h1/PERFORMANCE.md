# M8-H1 Performance & Pathological-Complexity Audit (Workstream H)

Scope note: not a micro-optimization campaign; this documents algorithmic review
plus measurements from the long-horizon harness (see LONG_HORIZON.md).

## Algorithmic review of hot paths

* Ingestion: per-record identity/dedup via dictionary lookups (O(1) amortized);
  batch work O(n). No list scans inside loops found.
* Reward ledger duplicate detection: lazily rebuilt hash index → O(1) per apply
  after load; load rebuild is O(records) once per boot.
* Validation: single pass over collections plus two O(ledger)/O(processed)
  aggregate sums — called once per boot/save-validation, not in loops.
* Persistence: envelope encode = JSON + base64 + SHA-256, all O(state size);
  every committed mutation rewrites the snapshot (documented durability contract),
  so daily cost grows linearly with state size — measured acceptable (≈29 ms/day
  including boot at day 365 scale).
* Producer ticking / allocation: bounded by content sizes (3 producers, ≤19
  projects); allocation loop terminates because completion is monotonic.

No pathological complexity was found that justified optimization; determinism and
correctness were never traded for speed. No flaky timing gates were added —
wall-time figures live in evidence docs, not CI assertions.

## Measurements summary

See LONG_HORIZON.md table. Highlights: 365-day flat run = 10.9 s wall for 365
boots+ingests+saves (≈30 ms/day); save 202 KB; validator clean throughout.
Duplicate-flood handling (5,010-record batch of already-known identities) completes
immediately with all duplicates ignored (`ActivityRedTeamTests.HugeDuplicateFlood_...`).

## Systemic optimization pass (D-045)

Phase-level harness (`tools/simulation bench`, Release, Windows/SSD, medians of 3):

| Phase | Before | After | Δ |
|---|---:|---:|---:|
| decode (21 KB save, v2 fast path) | 1.17 ms/op | 0.57 ms/op | −51% |
| session construction | 0.19 ms/op | 0.08 ms/op | −58% |
| app-closed day (boot+ingest+commits) | 14.5 ms/day | 10.9 ms/day | −25% |
| longhaul 365-day flat wall time | ≈6.95 s | ≈5.1 s | −27% |

Correctness cross-checks after the pass: all unit suites green; guard proof suite
25/25; longhaul final state byte-identical (same ledger/processed sizes, save bytes,
vitality); `profile --profile moderate` reproduces `pacing-moderate.txt` exactly
(day-242 completion). Remaining per-day cost is dominated by durable-commit I/O
(2 commits × temp-write + backup-copy flushes), which the durability contract
(D-040, offline-first invariant) requires and which was deliberately not reduced.
