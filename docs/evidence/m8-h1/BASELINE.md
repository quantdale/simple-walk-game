# M8-H1 Baseline — Headless Trust & Persistence Hardening

**Campaign:** M8-H1 (HARDENING + DEFECT REPAIR + ADVERSARIAL VERIFICATION)
**Planned-From:** `51e7eab6adf30fde332e5391cfca795b10940cb8` (= `origin/main` at start; no drift to reconcile)
**Start SHA:** `51e7eab6adf30fde332e5391cfca795b10940cb8`
**Preflight:** repository identity guard OK (`quantdale/simple-walk-game`); writer lease acquired normally; clean tree.

## Baseline gates (measured at start)

| Gate | Result |
|---|---|
| `dotnet build SimpleWalkGame.sln` | PASS, 0 errors (8 pre-existing warnings) |
| `dotnet test SimpleWalkGame.sln` | **180/180** — Domain 101 / Infrastructure 25 / Application 54 |
| Guard proof suite (`tests/guards/run-guard-tests.sh`) | 25/25 (re-verified during campaign) |
| Hosted CI at Planned-From | success on `c4ba6f6` per README/planner record |

## Baseline audit summary (Workstream A)

Audited: `AtomicFileSaveStore`, save codec/envelope validation, migration runner,
v1→v2 migration, session boot/recovery, reward ledger, processed-record ledger,
source checkpoints, correction/deletion handling, reconciliation windows,
`OfflineAdvancer`, producer checkpoint/storage behavior, `GameStateValidator`,
content/state reference validation, return-summary durability, Region 1
completion/post-completion state, simulation CLI and profile harnesses, and all
180 existing tests.

Findings ledger:

* TRUSTED / ALREADY PROVEN — exactly-once ingestion core (identity, dedup ledger,
  conversion rule versioning, checkpoint/reward atomicity), backward-clock defense
  at every callable boundary, bounded producer stores, durable typed summaries,
  additive M4 decoding, content validator depth, queue contract.
* HARDENING GAP — no fault-injection evidence at persistence boundaries; recovery
  re-commit could destroy the last healthy generation (see DEFECT H-1); access
  failures unclassified (DEFECT H-2); boot discarded decode diagnostics (DEFECT
  H-3); stale temporaries never cleaned (GAP H-4).
* DEFECT — V-1: `GameStateValidator` did not require a runtime row for every
  content producer (silent permanent producer loss possible).
* TESTABILITY GAP — no mature-save fixtures; no genuine-v1 rich migration
  fixture; no adversarial permutation matrix; no long-horizon growth measurements;
  no end-to-end hardening acceptance scenario.
* PERFORMANCE / GROWTH RISK — persisted ledgers grow unbounded by design
  (documented D-028 consequence). Quantified in LONG_HORIZON.md; pruning remains
  intentionally NOT implemented (no safe retention proof exists).
* DOCUMENTATION DRIFT — none found pre-campaign beyond what this campaign itself
  lands; reconciled at completion.
* RUNTIME-ONLY / BLOCKED — Unity 6 LTS editor still absent; unchanged by this
  headless campaign.
