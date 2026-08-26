# Active Execution Campaign — M8-H1 Headless Trust & Persistence Hardening

**Status:** COMPLETED
**Planned-From:** `51e7eab6adf30fde332e5391cfca795b10940cb8` (= `origin/main` at session start; no drift)
**Target branch:** `main`
**Campaign class:** HARDENING + DEFECT REPAIR + ADVERSARIAL VERIFICATION
**Primary roadmap target:** M8 — Hardening
**Historical note:** the earlier M3/M4-R Unity shell campaign remains BLOCKED at Gate A1 (no Unity 6 LTS editor in any execution environment so far); D-035 stays open. This headless campaign did not touch that blocker.

---

## Execution outcome (recorded by the executing session)

- **Start SHA:** `51e7eab6adf30fde332e5391cfca795b10940cb8`. **Implementation SHA:** `5505d6858df0fc0f2377e6716a518e4207cff1dd` — hosted `ci` **success** (run `32934471429`, inspected for this exact SHA).
- **Preflight:** identity guard OK (`quantdale/simple-walk-game`); writer lease acquired normally; worktree clean; no remote drift.
- **Commits (logical workstreams):**
  - `64327f2` Workstream B — persistence hardening;
  - `1148536` Workstream C — mature-save & migration qualification (+ validator rule D-041);
  - `3e4f30a` Workstreams D/E/G — adversarial red-team, temporal anomalies, seeded properties;
  - `d669c7c` Workstreams F/H/I — `longhaul` harness, long-horizon/performance evidence, end-to-end acceptance;
  - `5505d68` evidence package + documentation reconciliation.
- **Defects found & fixed (all regression-tested):**
  - H-1 (High): recovery re-commit rotated the corrupt primary into the backup slot, destroying the last healthy generation as a safety net → `ISaveStore.WriteAtomicPreservingBackup`; recovery path uses it exclusively (D-040).
  - H-2 (Medium): access failures escaped as crashes / misreported an intact-but-inaccessible save as "NoSaveFound" → reads probe-and-classify IoFailure; persist paths translate to the documented IOException type.
  - H-3 (Medium): boot discarded specific decode/validation failure reasons → surfaced in `StartResult.Detail`.
  - V-1 (Medium): missing producer runtime row silently undetected → validator requires a row per content producer (D-041).
  - H-4/Low + diagnostics/low: stale crash temporaries cleaned at construction; unidentifiable deletions counted in `DeletionsIgnored`; CS8604 in summary composer fixed.
- **Verification (final local state = pushed state):** build clean; `dotnet test` **221/221** (Domain 105 / Infrastructure 37 / Application 79; baseline 180); guard proof suite 25/25; simulation smoke violations=0 with integrity self-test PASS; M3 `walk --replay` credits zero on replay; new hostile-path suites all green.
- **Evidence package:** [`docs/evidence/m8-h1/`](../docs/evidence/m8-h1/CAMPAIGN_OUTCOME.md) — BASELINE, PERSISTENCE_RECOVERY, ACTIVITY_RED_TEAM, MATURE_SAVE_MIGRATION, LONG_HORIZON, PERFORMANCE, CAMPAIGN_OUTCOME.
- **Remaining risks / deferrals:** ledger growth is unbounded by design (documented, measured ≈554 B/day, intentionally not pruned without a safe-retention proof); device/update-in-place qualification and all runtime/device gates remain UNVERIFIED pending a Unity 6 LTS editor; permission-revoked and source-exception-mid-query red-team scenarios await platform adapters (M7).
- **Lease:** released normally after the final push.

---

## Original campaign brief (executed as specified)

The sections below are retained verbatim as the planner's contract for this campaign.

### §3 Primary objective

Prove: given the same valid real-world activity history, canonical final game state remains equivalent regardless of replay, duplicate delivery, ordering, corrections, deletion events, restarts, recoverable save failures, supported-schema migration, long inactivity, and batching differences — and at every persistence failure boundary either the new valid generation or a previously valid generation stays recoverable, never silently replaced by a fresh save.

### §5 Workstream B required invariants

Failed commit ≠ success; last recoverable generation not destroyed unnecessarily; boot selects newest valid generation by explicit contract; corrupt-primary→valid-backup recovery deterministic; recovery does not destroy the only valid copy before validation; stale temporaries never masquerade as saves; failures surfaced diagnostically; unrecoverable state fails explicitly rather than silently creating a fresh profile.

### §6 Workstream C required evidence

C1 old-bytes→migrate→validate→encode→reload determinism; C2 exactly-once after migration incl. restart replays; C3 unknown future schema fails closed without downgrade or reset; C4 content-identity durability with documented policy (no invented silent fallbacks).

### §7–§11 Workstreams D/E/F/G/H summaries

D: five hostile permutations + overlapping windows + duplicate floods converge to identical canonical state; corrections/deletions pinned to exact D-029 values; completed-history replay is a no-op. E: horizon/skew edges exact; offset/locale independence; zero-elapsed boots; backward clocks; 4,000-day absence bounded. F/H: `longhaul` verb; linear ~554 B/day growth documented, not pruned; no pathological complexity; wall-time records in evidence, no flaky timing gates. G: seeded property suites with reproducible seeds (producer partition drift <1 milli-unit/split; monotone completion; idempotent replay).

### §12 Workstream I acceptance

`M8H1HardeningAcceptanceTests`: clean profile → app-closed days with real reloads → durable pending summary → persistence interruption → boot through real recovery → retry credited exactly once → correction/deletion history across restarts → full processed-history replay ×2 crediting zero → ~6.5-month absence with clean validation → Region 1 closure → restart stability → final serialize/reload byte equivalence. Migration from historical fixtures lives in the separate `MatureSaveMigrationTests` scenario, as specified.

### §13 Defect policy outcome

All discovered Critical/High defects fixed before completion (H-1 was the only High; fixed first commit). Medium/Low items fixed when tightly related (H-2..H-4, V-1, diagnostic counting); none deferred beyond documentation.

### §17 Verification gates

All applicable headless gates executed and green (see outcome above). Runtime-only gates remain honestly UNVERIFIED.

### §20 Completion gate

Satisfied: no campaign-caused Critical/High regressions; persistence hostile-path coverage materially improved; mature-save/migration evidence exists; adversarial/replay evidence materially improved; long-horizon behavior exercised; every scenario validates cleanly; full headless suite green (221/221); docs reconciled; work pushed; implementation-SHA hosted CI inspected success; no Unity/runtime claims fabricated.
