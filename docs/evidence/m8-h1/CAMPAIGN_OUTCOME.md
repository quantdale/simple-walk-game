# M8-H1 Campaign Outcome

**Status:** COMPLETED (headless scope)
**Start SHA:** `51e7eab6adf30fde332e5391cfca795b10940cb8` · **Branch:** `main`

## Delivered

* **Persistence hostile-path hardening (B):** recovery-safe re-commits
  (`WriteAtomicPreservingBackup`) so a known-corrupt primary can never displace the
  last healthy generation; access-failure classification (no more crashes or
  "no save found" on inaccessible saves); specific boot diagnostics instead of a
  generic unreadable message; stale-temp hygiene; deletion diagnostics counting.
* **Mature-save & migration qualification (C):** genuine rich v1 → v2 migration
  through the registered chain with exactly-once replay after migration;
  canonical byte stability; future-schema fail-closed; content-identity durability
  under checksum-correct payload surgery; validator rule V-1 (producer runtime rows).
* **Adversarial red-team (D):** five hostile permutations + overlapping windows +
  duplicate floods converge to identical canonical state; corrections/deletions
  pinned to exact D-029 values; full replays are no-ops.
* **Temporal anomalies (E):** horizon/skew edges decided exactly at documented
  boundaries; locale/offset independence through the pipeline; zero-elapsed boots,
  backward clocks, year/month/leap-day boundaries, 4,000-day absence all bounded.
* **Long-horizon & performance (F/H):** new `longhaul` CLI verb; 30/90/180/365-day
  plus irregular and long-absence runs measured (see LONG_HORIZON.md); linear
  growth (~554 B/day) documented as accepted exactly-once cost; no pathological
  complexity found; no unsafe pruning.
* **Seeded property testing (G):** producer partition-drift bound (<1 milli-unit
  per split), store capacity under seeded absences, completion/arc monotonicity,
  idempotent transaction replay across randomized scripts (failures print seed).
* **End-to-end acceptance (I):** `M8H1HardeningAcceptanceTests` — interruption →
  recovery → retry exactly-once → corrections/deletions → full replay ×2 →
  ~6.5-month absence → Region 1 closure → restart stability → byte equivalence.

## Defects fixed during campaign

| ID | Severity | Summary |
|---|---|---|
| H-1 | High | Recovery re-commit rotated corrupt primary over last good backup |
| H-2 | Medium | Access failures crashed / misreported as "no save found" |
| H-3 | Medium | Boot discarded specific decode/validation failure reasons |
| V-1 | Medium | Missing producer runtime row silently undetected |
| H-4 | Low | Stale crash temporaries never cleaned |
| D-low | Low | Unidentifiable deletions absent from diagnostic counters |

## Verification

* `dotnet build SimpleWalkGame.sln` — clean.
* `dotnet test SimpleWalkGame.sln` — **221/221** (Domain 105 / Infrastructure 37 /
  Application 79); baseline was 180.
* Guard proof suite — 25/25.
* Simulation smoke + M3 `walk --replay` + M4 acceptance — green (existing gates,
  re-run in CI).

## Remaining risks / deferrals

* Unity 6 LTS editor STILL ABSENT in this environment: Gate A1 runtime blocker
  unchanged, D-035 remains open, M5–M7 remain blocked. No runtime/device claims
  made.
* Ledger growth is unbounded by design (documented, measured, intentionally not
  pruned).
* Device/update-in-place qualification (R-006 release half) still open until a
  real device lane exists.
