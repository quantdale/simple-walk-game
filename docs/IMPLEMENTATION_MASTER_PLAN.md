# Implementation, Completion and Hardening Master Plan

**Repository:** `quantdale/simple-walk-game` (Simple Walk Game)
**Status:** analysis and planning only — **no product code was modified by this campaign**
**Baseline audited:** commit `69ca51a3cf83be57fc0e917fad6a76657407fd4b`, working tree clean
**Canonical execution specification:** the 15 OpenSpec changes under
[`openspec/changes/`](../openspec/changes). This document is the **index and
narrative**; each change's `proposal.md` / `design.md` / `tasks.md` is the
authoritative execution detail. Do not implement from this document alone.

---

> **2026-10-17 (D-164): governing direction changed.** Michael's redesign brief [`docs/direction/REDESIGN_2026-10-17.md`](direction/REDESIGN_2026-10-17.md) now sets presentation priorities. It replaces text-shell polish with an illustrated, interactive world-restoration presentation, gated by a one-time art-direction approval (KNOWN_ISSUES morning queue). The engineering hardening recorded in this plan stays valid and must be preserved. See `docs/ROADMAP.md` "Governing direction".

> **2026-10-17 (D-171): art and experience direction.** Michael's Creative Direction Bible v1 [`docs/direction/CREATIVE_BIBLE_v1_2026-10-17.md`](direction/CREATIVE_BIBLE_v1_2026-10-17.md) governs art and player experience. It is Cozy Grove–inspired but original. It supersedes D-164's concept round: round 1 (D-168) was not chosen. The single human art gate is now Phase B round 2 (A Painted Storybook Valley, preferred; B Layered Paper-Craft Wonderland; C Painterly Miniature Diorama). Phases C–F and gates §45–§48 are adopted. Every invariant and hardening item in this plan is preserved (bible §50): presentation reads canonical state and never owns it.

## 0. Application status (updated 2026-10-09)

> **2026-10-16 phase-1 re-assessment** (fresh clone at `97db0c6`): build clean, 505/505 tests, guard suite 25/25, simulation smoke green. `compact-activity-and-reward-ledgers` (F-07) is still the only unapplied change and is proposed as the next campaign — see `.agent/ROADMAP_PROPOSAL_2026-10-16.md` (PROPOSED, not active).

An apply session implemented **10 of the 15 changes** on `main`
(`df68574` … `d21e83b`). Independent review verified the applied work by
re-running the evidence rather than reading its comments: the two P0
reproductions were re-executed and both now behave correctly, and every gate is
green. Test count grew 295 → 418.

A second session applied **`define-post-completion-evergreen-state`** (F-20, the
P1 product-ending defect) on 2026-10-08: a completed region no longer demands an
impossible queue action, and continued activity has an authored sink
(`PostCompletionEvergreenStateTests`, 7 scenarios; D-061). Test count 418 → 437.

A third session applied **`close-inactivity-pacing-shortfall`** (F-19/F-13) on
2026-10-09: closure became an authored criterion, the M4-authored roster was rebalanced
19,400 → 15,350 Vitality with the five seed definitions untouched, and every balanced
profile now closes inside its documented target — the weekday-only profile with a
six-week absence went from never-closing to day 376 (D-062). The pacing harness now
FAILS on a shortfall instead of reporting one. Test count 437 → 453.
A fourth session applied `surface-ingestion-rejection-reasons` (F-14/F-17) on 2026-10-09: the per-reason rejection breakdown is durable (keyed by enum ordinal), source health is classified from durable fetch timestamps instead of a watermark age that floored to zero, and the no-leak sweep enumerates surfaces by reflection (D-063). Test count 453 → 466.

A fifth session applied `extend-ci-and-release-automation` (F-18) on 2026-10-09: a coverage floor (measured, deliberately low, as a loss detector), a dependency gate that enforces the documented one-runtime-package claim, Dependabot, a thin tag-triggered release lane, a scripted release-evidence assembler and a committed release checklist (D-064). The two new workflows are validated by executing their step bodies locally; hosted CI remains blocked (KI-001).

Remaining unapplied: 1 change (F-07 — see below).

| Applied | Findings | Verdict |
|---|---|---|
| `pin-build-toolchain-and-centralize-packages` | F-12 | Verified: SDK pinned, settings declared once, warnings-as-errors live at 0 warnings. |
| `recover-save-from-backup-when-primary-is-missing` | F-01, F-10 | **P0 fixed** — re-ran the original destruction; the 8-day world survived (`created:` unchanged, `recovered from backup`). |
| `anchor-activity-acceptance-window-to-trusted-time` | F-02, F-03, F-09 | **P0 fixed** — 17 tests incl. a real 30-day backlog credited in one batch, exactly-once on replay. |
| `bound-secondary-resource-economy` | F-06 | Caps bind (Materials 400, Knowledge 300 confirmed); two requirements on different chains; affordability gate correctly **stops** rather than skipping. Vitality deliberately uncapped (D-054). |
| `rotate-durable-return-summary` | F-05 | Unstable `List<T>.Sort` correctly replaced with a total comparator. |
| `align-content-validator-with-simulation-overflow-bounds` | F-08 | Bound now *derived* from the simulation's own arithmetic, with a drift self-check. |
| `gate-saves-against-authored-content-version` | F-04 | Compatibility classification + tombstones + a CI content-drift gate. |
| `close-application-use-case-test-gaps` | F-11, F-17 | `ReorderQueue` / `DequeueProject` covered; enumeration and error-code guards added. |
| `resolve-unused-domain-surface` | F-15, F-21 | Decisions recorded; seed-honesty and dormant-surface guards added. |
| `harden-simulation-cli-integrity-selftest` | F-16 | Self-test reads/writes the same key; exit-code mapping exhaustive. |
| `surface-ingestion-rejection-reasons` | F-14, F-17 | **P2 fixed** (D-063, 2026-10-09) — the per-reason rejection breakdown is durable (ordinal-keyed, bounded by the reason set), the source's health is classified from durable fetch timestamps rather than a watermark age that floored to zero, a failed fetch preserves the previous batch's facts, and the no-leak sweep enumerates every read-model surface by reflection. |
| `define-post-completion-evergreen-state` | F-20 | **P1 fixed** (D-061, 2026-10-08) — the audited permanent alarm (`RequiresAttention` + "queue the next restoration project" with 19/19 complete and 60,650 Vitality banked) is replaced by an explicit `RegionComplete` conclusion with `RequiresAttention = false`; continued activity is consumed by the authored Evergreen Tending program, so the unspent balance is bounded by one cycle. All seven pacing reports still reproduce byte-for-byte; architecture, save-growth and guard gates green. |
| `close-inactivity-pacing-shortfall` | F-19, F-13 | **P1 fixed** (D-062, 2026-10-09) — closure is an authored criterion (`ClosureCriterionDefinition`, validated for majority and reachability, enforced by `GameStateValidator`, reported by `profile` which now exits 2 naming unfinished chains); the M4-authored roster was rebalanced 19,400 → 15,350 Vitality with the five seed definitions verbatim; every balanced profile closes inside its documented target (inactivity: never → day 376, 6/6 chains); reports state requested AND eligible activity, which also corrects the wrong "≈686 steps/Vitality" figure. |

**Not yet applied (1 change, correctly left as specified future work):**

| Change | Findings | State |
|---|---|---|
| `compact-activity-and-reward-ledgers` | F-07 | **Applied (D-066, 2026-10-16)** on branch `agent/simple-walk-game/f07-ledger-compaction-20261016`: day 730 save 395,716 → 80,458 B; convergence gate added; 505 → 514 tests. |
| `extend-ci-and-release-automation` | F-18 | **P3 fixed** (D-064, 2026-10-09) — coverage floor at 55% (deliberately low; measured aggregate 59.51%), a dependency gate that fails on a known advisory AND on a runtime surface other than the documented single first-party package, Dependabot for the test-side packages, a thin tag-triggered release lane, a scripted release-evidence assembler, and a committed release checklist. The new workflows are validated by executing their step bodies locally; hosted CI remains blocked (KI-001). |

**Release qualification remains blocked** by `KI-001` (hosted CI billing) and
`KI-002` (no Unity editor) regardless of how much of this plan ships. Both are
external dependencies, not engineering work in this repository.

---

> This document is deliberately **not** named `docs/MASTER_PLAN.md`. That file is
> the product vision and milestone program, is referenced by `README.md` and
> `docs/ROADMAP.md`, and is correct as written. This is the engineering plan.

---

## 1. Executive Summary

### What the project actually is

An offline-first ambient fitness game in which real-world walking is the primary
gameplay input and a persistent restoration world is the progress display. There
is no backend, no network code, no analytics, no accounts, and no telemetry. The
repository is a **headless, deterministic game core** in .NET 9 / C# 9: three
`netstandard2.1` production libraries, a headless simulation CLI, and three xUnit
projects. There is **no Unity project in this checkout** — the presentation
runtime is externally blocked (`KI-002`).

### Overall maturity

**High for its stage, with two genuine data-loss defects and one product-ending
defect.** This is not an unfinished prototype. The deterministic core, the
activity trust pipeline, the content graph, the persistence layer, the guard
tooling and the CI reproduction machinery are all real, well-tested and
documented to an unusual standard: 466 automated tests, all green; architecture
layering, the exactly-once invariant, seven pacing profiles and the save-growth
series are all **mechanically CI-gated** rather than asserted in prose.

The dominant characteristic of the codebase is *disciplined honesty*: the
repository consistently records what is unverified (`KI-001` hosted CI blocked,
`KI-002` no Unity editor), reserves runtime/device claims, and refuses to let
documentation overstate implementation. That discipline is itself an asset and
must be preserved by this plan.

### Major strengths

- **The trust pipeline is genuinely well built.** Stable record identity, stable
  reward-transaction identity, both persisted in the same atomic commit as the
  rewards they describe, with a fail-closed validator cross-check. The red-team
  suite converges five hostile permutations to identical canonical state.
- **Persistence is careful.** Atomic commit, one-generation backup, an explicit
  "never promote a known-bad primary into the backup slot" path (`D-040`), and a
  mature-v1 fixture migration proving exactly-once replay after migration.
- **The gates are real.** `scripts/verify-architecture.sh` enforces layering and
  proves each rule fails on a broken fixture. `scripts/verify-ci-steps.sh`
  reproduces the entire CI gate locally because hosted CI is blocked — an
  unusually mature response to an infrastructure failure.
- **Determinism is real, not aspirational.** Both acceptance suites assert
  byte-identical canonical state across reruns.
- **Content validation is a genuine release gate** with reachability, cycle
  detection and overflow-awareness.

### Major weaknesses

Three defects are serious enough that the repository's own severity model rates
them release-blocking, and two of them cause **total loss of a player's world**:

1. **A missing primary save is reported as "no save found" and the healthy
   backup is never read** — reproduced end-to-end, resulting in a fresh game
   replacing an 8-day world.
2. **A >14-day absence discards the player's entire activity backlog**, which
   contradicts the offline-first thesis the product is built on.
3. **After region completion the game permanently demands an action that is
   impossible**, with earned progress growing without a sink.

Beyond those: two secondary currencies with no consumer and no cap, an unbounded
durable history that grows forever, a content-version guard that does not exist
despite being listed as a mitigation, a saturating return summary, and several
tooling/reproducibility gaps.

### Primary remaining objective

**Not completion — hardening, correctness and product integrity.** Every headline
system exists and works. What is missing is (a) two data-loss paths, (b) one
product-ending dead end, (c) an economy with no terminal state, and (d)
mechanical guarantees that do not yet exist. The recommended end state is
described in §5.

---

## 2. Repository / System Overview

### Major subsystems

| Subsystem | Location | Responsibility |
|---|---|---|
| Domain | `src/WalkGame.Domain/` | Pure rules, state, simulation. No project refs, no packages, no filesystem, no engine namespace. |
| Application | `src/WalkGame.Application/` | Use-case orchestration and read models. References Domain only. No filesystem access. |
| Infrastructure | `src/WalkGame.Infrastructure/` | Save codec, atomic file store, local preferences store, migrations, system clock, fixture reader. Only runtime dep: `System.Text.Json` 10.0.13 (D-124). |
| Simulation CLI | `tools/simulation/` | Headless dev/release-evidence tool. Verbs: `new credit advance simulate walk profile longhaul bench scenarios ack dump validate`. |
| Tests | `tests/` | 3 xUnit projects — Domain 148, Infrastructure 49, Application 269 = **466, all green**. |
| Guard/tooling | `scripts/`, `.githooks/`, `tests/guards/` | Repo identity guard, single-writer lease, worktree helper, CI-step reproduction, architecture fitness, evidence gates. |
| CI | `.github/workflows/ci.yml` | The only workflow. 12 `run:` steps. |

### Architectural boundaries (mechanically enforced)

`scripts/verify-architecture.sh` enforces, and proves each rule can fail:

1. `WalkGame.Domain` references **nothing**, stays on `netstandard2.1`, declares
   no `PackageReference`.
2. `WalkGame.Application` references **only** Domain.
3. `WalkGame.Infrastructure` references **only** Application + Domain.
4. No `UnityEngine` / `UnityEditor` anywhere in `src/`.
5. Domain and Application perform **no filesystem access** (comments stripped so
   prose cannot false-positive).
6. Every CI-invoked shell script is committed mode `100755` and LF-only.

### Primary execution flows

**Activity → world (the core loop)**

```
adapter/port → NormalizedActivityRecord
  → ActivityValidationPolicy.Validate (eligibility + temporal window)
  → ActivityIdentity.Compute (rec1 source-id | fpt1 SHA-256 fingerprint)
  → dedup vs ProcessedRecordLedgerState
  → correction/deletion policy (conservative, balance-clamped)
  → StepConversionRuleV1 (100 steps → 1 Vitality, floor)
  → ActivityRewardIds.Derive*TransactionGuid (stable, rule-versioned)
  → RewardLedgerState.Apply / ApplyCorrection (idempotent)
  → OfflineAdvancer.AllocateVitality (project queue, auto-advance rollover)
  → OfflineAdvancer.TickProducers (integer milli-unit production, bounded store)
  → ReturnSummaryComposer.Compose (durable bounded summary)
  → one atomic commit (ledger + dedup + checkpoint + summary together)
```

**Boot**

```
Continue() → read primary → checksum → version gate → migration → deserialize
  → GameStateValidator.Validate (fail closed) → OfflineAdvancer.Advance
  → ComposePendingSummary → atomic commit → StartResult
```

**Tooling gates**

```
scripts/verify-ci-steps.sh   parses ci.yml, executes every run: step locally
scripts/verify-architecture.sh   layering, engine-freedom, script hygiene
scripts/verify-pacing-evidence.sh  7 profiles reproduce byte-for-byte
scripts/verify-save-growth.sh      size/count series reproduces; growth gate
tests/guards/run-guard-tests.sh    identity/lease/pre-push proof suite
```

### Persistence model

Versioned JSON envelope `{schemaVersion, savedAtUtc, payloadSha256, payload}`.
Decode path: frame → checksum (fixed-time compare) → version gates → sequential
`ISaveMigration` chain → deserialize → validate. Commit path: durable temp write
→ rotate primary into backup → replace primary. Recovery path uses
`WriteAtomicPreservingBackup` so a known-bad primary is never promoted.

Save schema `v2`; `MinimumSupported = 1`; one registered migration
(`m1-to-v2-producer-stored-milli-units`). Additive fields follow the D-036
pattern (absent decodes to "nothing yet", no migration).

### External integrations

**None.** Zero network API usage, mechanically verified by grep in
`docs/PRIVACY_REVIEW.md` §1. No accounts, no analytics, no ads, no cloud. The
only external surface is the local filesystem, host-injected.

### Platform assumptions

The Domain/Application/Infrastructure core is `netstandard2.1` with no engine
dependency by design. The save directory is **host-injected** (`--save`); no
`Environment.GetFolderPath` or OS path appears in `src/`. Unity 6 LTS is the
intended presentation runtime (D-009) and **does not exist in this checkout**.

### Build / test / release structure

```bash
dotnet build SimpleWalkGame.sln          # 0 errors, 0 warnings (--no-incremental)
dotnet test                               # 466 passing
scripts/verify-architecture.sh            # 11 passed, 0 failed
scripts/verify-ci-steps.sh                # reproduces all 12 CI steps locally
scripts/verify-pacing-evidence.sh         # 7 reproduced, 0 drifted
scripts/verify-save-growth.sh             # series reproduces; convergence gate
tests/guards/run-guard-tests.sh           # guard proof suite
```

---

## 3. Current-State Assessment

### Complete and healthy

- **Deterministic core** — xoshiro256\*\*, integer-only economy math, injected
  clock, byte-identical determinism proven by two acceptance suites.
- **Exactly-once crediting** — record identity, transaction identity, durable
  dedup ledger, checkpoint that cannot outrun reward state, validator cross-check.
- **Corrective policy** (D-029) — conservative balance-clamped clawback, net-applied
  accounting, durably counted `UnappliedReversalVitality`, world content never
  destroyed.
- **Region 1 content graph** — 19 projects, 6 landmarks, 3 producers, 13
  discoveries, 3 expeditions, 2 arcs, closure milestone; validated with zero
  violations and full reachability.
- **Migration** — genuine mature-v1 fixture migration with exactly-once replay.
- **Guard and lease tooling** — fail-closed identity, single-writer lease, exit
  codes 86/87/88; proven by a fixture-based proof suite.
- **Documentation discipline** — evidence states, known-issues register, privacy
  review, decision log, and an explicit refusal to claim unverified behaviour.
- **Instrumentation harnesses** — `profile`, `longhaul`, `bench`, `scenarios`
  produce machine-readable, deterministic output.

### Implemented but requiring hardening

- **Atomic save store** — correct at the store layer for every hostile path
  tested, but the **application boot flow** does not honour its recovery
  guarantee (F-01, P0).
- **Temporal validation window** — the anti-abuse bound is sound in principle but
  anchored to the wrong reference, losing real activity (F-02, F-02/F-03, P0/P1).
- **Resource model** — the cap, parking and flush machinery is fully implemented
  and tested, but unreachable in production because no cap is ever set, and
  Materials/Knowledge have no consumer at all (F-06, P1).
- **Return summary** — durable, bounded, priority-ordered, crash-safe; but it only
  grows, so it saturates and then stops reporting new progress (F-05, P1).
- **Durable history** — correct and invariant-checked, but append-only forever
  (F-07, P2).
- **Content validator** — strong on references, cycles and reachability; its
  numeric bound does not match the simulation's real arithmetic (F-08, P2).
- **Diagnostics** — privacy-safe and test-enforced, but discards the per-reason
  rejection breakdown it already computes, and cannot distinguish a stale source
  from a quiet one (F-14, P2).
- **Simulation CLI** — the release-evidence tool; its tamper self-test has a
  latent key-casing defect and its exit codes misclassify two conditions
  (F-16, P3).
- **Build reproducibility** — no `global.json`, no shared props, no central
  package management, 8 unenforced analyzer warnings (F-12, P2).

### Partial

- **Boot outcome taxonomy** — `StartStatus.StateInvalid` and
  `DiagnosticsBootOutcome.StateInvalid` are declared but never produced; the CLI's
  exit-2 branch is unreachable (F-10, P2). Partially closed by
  `recover-save-from-backup-when-primary-is-missing`.
- **Post-completion state** — was partial: completion is permanent, validated and detected
  once, and producers keep operating (the "evergreen" part worked), while everything downstream
  of it was missing. Closed by `define-post-completion-evergreen-state` (D-061): Home reports a
  conclusion instead of an impossible instruction, and continued activity has an authored sink.
- **Deletion handling** — reverse-clamping and diagnostics work, but deletion
  markers bypass eligibility validation entirely (F-09, P2).
- **Closure definition** — was implicit: "the player spent the roster's total on this exact
  roster". Closed by `close-inactivity-pacing-shortfall` (D-062): closure is authored content
  (chains + criterion), validated, enforced and reported, and every balanced profile reaches
  it inside its documented target.
- **Reconciliation watermark** — durable and monotonic, but never *drives* a
  query window; it is evidence, not a scheduler (noted in F-14's change).
- **Mature-save safety** — fail-closed on unknown IDs, but no content-version
  guard and no tombstone policy (F-04, P1).
- **Test coverage of the use-case surface** — 2 of 6 queue operations have zero
  test references (F-11, P2).

### Missing

- A content-version / tombstone compatibility mechanism (F-04).
- Any consumer for Materials or Knowledge; any resource cap in production (F-06).
- Any post-completion content (F-21).
- Coverage collection or gate; dependency vulnerability check; any release
  workflow or release evidence assembly (F-18, P3).
- Pinned SDK and centralised package versions (F-12, P2).
- A Unity project, platform adapters, and every runtime/device lane (externally
  blocked, `KI-002` — **not actionable in this repository**).

### Problematic / defective

See the Findings Register (§4). The three P0/P1 items that matter most:
F-01 (total save loss), F-02 (total activity loss), F-21 (broken ending).

### Uncertain

| Area | Why uncertain | How to resolve |
|---|---|---|
| Unity/runtime lanes | No editor exists; `KI-002` | Requires an external environment. Not resolvable here. |
| Hosted CI | Account billing blocker; `KI-001` | Requires the account owner. A first hosted green must be diffed against `docs/evidence/ci-local/`. |
| On-device performance | No device lane | Requires M7/device. The headless `scenarios` subset is the only available evidence. |
| Ending quality (F-21) | "Is the ending satisfying?" is a human judgement | The mechanical properties are testable; emotional quality is honestly unprovable headlessly and is recorded as such. |
| At-rest confidentiality (`KI-006`) | Save is health-adjacent, stored plaintext at the app layer | Product/security decision required before M9; not a code fix. |
| Pacing targets (`KI-004`) | D-039 targets are provisional | Ratified or superseded by `close-inactivity-pacing-shortfall`. |

---

## 4. Findings Register

21 findings. Each maps to exactly one OpenSpec change, which carries the
authoritative evidence, requirements, design and task list.

| ID | Title | Pri | Category | Evidence (file / reproduction) | OpenSpec change |
|---|---|---|---|---|---|
| F-01 | Backup-only recovery is unreachable; a missing primary save is reported as "no save found" and the healthy backup is never read, so a composition root starts a fresh world and discards all progress | **P0** | Correctness / data loss | `GameSession.Continue()` early-returns on `SaveReadOutcome.NotFound` before `SafeReadBackup()`. `AtomicFileSaveStore.ReplaceFile`'s own comment documents the delete/move window that leaves the primary absent. **Reproduced**: `walk --days 8` → `rm save.json` → `walk` prints `[dev] started fresh game seed=42`; an 8-day world was destroyed while the healthy backup was orphaned | `recover-save-from-backup-when-primary-is-missing` |
| F-02 | A player returning after >14 days has their **entire** activity backlog rejected as `OutsideHorizon` | **P0** | Correctness / product | `ActivityValidationPolicy.ReconciliationHorizon = 14 days`, checked unconditionally against injected now. Pinned by `SessionIngestionTests.RecordBeyondReconciliationHorizon_IsRejectedWithoutSideEffects`. Contradicts D-003/D-004 offline-first. No test covers a real backlog after a 30-day return — `M5H1ShellAcceptanceTests.Scenario05` ingests nothing during the absence | `anchor-activity-acceptance-window-to-trusted-time` |
| F-03 | A device clock merely **fast** (>10 min) makes every record future-dated, silently disabling crediting | P1 | Correctness / reliability | `MaxFutureSkew = 10 min` vs `SystemClock.UtcNow`. `GAME_SYSTEMS.md` §15 requires not relying solely on device wall clock. No clock-offset normalisation exists anywhere | `anchor-activity-acceptance-window-to-trusted-time` |
| F-04 | No save↔content version gate: `ContentVersion` is write-only, so a content rename/remove permanently bricks mature saves (both generations fail closed) | P1 | Data integrity / maturity | `RegionDefinition.ContentVersion` is assigned in the constructor and **never read**; only 2 non-constructive references repo-wide. `MatureSaveMigrationTests` proves unknown IDs fail closed, which is exactly what makes the rename unrecoverable. R-018 lists a "tombstone/migration policy" that does not exist | `gate-saves-against-authored-content-version` |
| F-05 | The durable return summary saturates and then never reports new progress; Home demands attention forever | P1 | Product / UX | `ReturnSummaryComposer.Compose` merges into existing items with no expiry; `Finalize` caps at 12 sorted by kind, so newest Transformation items displace and lower-priority kinds are dropped forever. `List<T>.Sort` is unstable, contradicting the code's own "insertion order within a kind" comment. `GetHome()` sets `RequiresAttention` whenever a summary exists | `rotate-durable-return-summary` |
| F-06 | Materials and Knowledge are uncapped, unconsumed dead currencies; the cap/parking mechanism is unreachable in production | P1 | Design / economy | `ResourceBalances.SetCap` has **zero** call sites in `src/`+`tools/`; `TryConsume` is only ever called for Vitality. The `hitResourceCap` branch and the whole "park and flush" mechanism proven by `ProducerSimulationTests` are dead in production. `ProjectDefinition` lacks the "optional secondary-resource requirements" that `GAME_SYSTEMS.md` §3 promises. R-014 realised | `bound-secondary-resource-economy` |
| F-07 | Durable record + reward history grow without bound; whole state re-serialised on every commit | P2 | Performance / scalability | Committed growth series: 10 KB @ d7 → 203 KB @ d365 → 394 KB @ d730, linear, ~540 B/day, no plateau. `RewardLedgerState.EnsureIndex()` is O(n) after every load; `TotalVitalityCredited` is O(n) and is called on every boot and every diagnostics read | `compact-activity-and-reward-ledgers` |
| F-08 | `ContentValidator` accepts producer rates the production tick cannot represent; overflow can produce a negative store, which fails the save closed on the next boot | P2 | Correctness | `MaxMilliUnitsPerDay = 1e12` passes validation, but `TickProducers` computes `(cappedTicks % TicksPerDay) * MilliUnitsPerDay` where the remainder term is < 8.64e11 — so rates above ~1.07e7 overflow `long` (unchecked). `GameStateValidator` then rejects the save. Current Region 1 rates (1,200–2,500) are safe, so this is latent | `align-content-validator-with-simulation-overflow-bounds` |
| F-09 | Deletion markers bypass eligibility validation entirely and can push the durable watermark arbitrarily far forward | P2 | Correctness | In `IngestActivityBatch` the `if (record.IsDeletion)` branch precedes `ActivityValidationPolicy.Validate`, and `ProcessDeletion` advances `maxTrustedEndUtc` from the unvalidated `record.EndUtc` | `anchor-activity-acceptance-window-to-trusted-time` |
| F-10 | Boot outcome taxonomy is not exhaustive: `StateInvalid` is declared but never produced, and the CLI's exit-2 branch is unreachable | P2 | Design / diagnosability | `StartStatus.StateInvalid` and `DiagnosticsBootOutcome.StateInvalid` have zero producers; `Continue()` folds validator failure into `SaveUnreadable`; `Commands.Boot`'s `StateInvalid ? 2 : 3` is dead, so a data-integrity failure exits 3 ("missing/unreadable") | `recover-save-from-backup-when-primary-is-missing` (bundled) |
| F-11 | Two public mutating use cases have **zero** test references: `ReorderQueue` and `DequeueProject` | P2 | Testing | `grep -rl "ReorderQueue\|DequeueProject" --include=*.cs tests` → 0 files. Both mutate and persist durable queue state; both are exactly what a drag-to-reorder shell would call first | `close-application-use-case-test-gaps` |
| F-12 | Build is not reproducible: no `global.json`, no shared props, no central package management, 8 unenforced analyzer warnings | P2 | Tooling / reproducibility | `global.json`, `Directory.Build.props`, `Directory.Packages.props` all absent. Test package versions triplicated across 3 csprojs. `TreatWarningsAsErrors=false` in one csproj, implicit elsewhere. `TESTING_AND_RELEASE.md` §14 requires a "documented toolchain version"; §16 records 8 pre-existing xUnit warnings left unenforced | `pin-build-toolchain-and-centralize-packages` |
| F-13 | The `pathological` pacing finding is **numerically wrong** in the two documents a release reviewer reads most | P2 | Documentation integrity | ROADMAP §M4 and R-013 both state "≈686 steps/Vitality". Recomputed from the committed evidence: raw requested 3,184,633 steps; per-record clamp (250,000) reduces the eligible total to 2,184,633; credited vitality 21,844 → **exactly 100.0** steps/Vitality. The clamp discards surplus, it does not reduce the credit rate. Root cause: `Cli.Profile` prints the **pre-clamp** total under an "activity ingested" label | `close-inactivity-pacing-shortfall` (§6) |
| F-14 | Rejection reasons that lose player activity are computed and then discarded; a stale source is indistinguishable from a quiet one | P2 | Observability | `IngestionResult.RejectionCounts` is produced and returned but has no persistence path. `DiagnosticsIngestionRow` carries only aggregate `Rejected`. So `OutsideHorizon` (player lost everything) is indistinguishable from `ZeroQuantity` (normal). `CheckpointWatermarkAgeDays` floors to 0, so "5 min ago" and "20 hours ago" both read 0 | `surface-ingestion-rejection-reasons` |
| F-15 | `DeterministicRng` is fully implemented, persisted, round-tripped and unit-tested, but **no production code consumes it**; the `seed` argument cannot affect any outcome | P3 | Dead surface | `grep -rn "NextUInt64\|NextInt64\|NextDouble" --include=*.cs src tools` → only the definitions. `SeededProgressionPropertyTests` uses it as a *test-data* generator, which is the most misreadable reference in the repo. D-017 is satisfied vacuously | `resolve-unused-domain-surface` |
| F-16 | The CLI tamper self-test reads a defensive dual-casing key but writes camelCase unconditionally, so a casing change yields a duplicate-key document and a **false** integrity failure | P3 | Tooling correctness | `Commands.IntegritySelfTest` reads `root?["payloadBase64"] ?? root?["PayloadBase64"]` then writes `root!["payloadBase64"]`. Today harmless (`SaveJson` is always camelCase), but the fallback branch is dead and the failure mode is `MalformedEnvelope` instead of `ChecksumMismatch` — a false release-blocker from the tool meant to prove integrity | `harden-simulation-cli-integrity-selftest` |
| F-17 | The reflection-based no-leak sweep covers 6 of 9 player-facing read models | P3 | Testing | `M5H1ContractHardeningTests.PlayerFacingSurfaces_...` walks status/onboarding/settings/home/projects/region. `GetDiscoveries`, `GetExpeditions`, `GetPendingReturnSummary` are absent — the one test whose purpose is preventing a leak class has an unwatched surface | `close-application-use-case-test-gaps` + `surface-ingestion-rejection-reasons` |
| F-18 | CI has no coverage gate, no dependency/SBOM check, no release workflow, and the local-gate reproducer is manual | P3 | Release readiness | `coverlet.collector` referenced in all 3 test projects but never invoked. `.github/workflows/` contains only `ci.yml`. No `dependabot.yml`, no artifact upload, no tag trigger. `verify-ci-steps.sh` is invoked by nothing | `extend-ci-and-release-automation` |
| F-19 | A weekday-only player with a 6-week break **cannot finish the game**: 16,420 Vitality earned against 19,400 authored cost | P1 | Product balance | `docs/evidence/m4/pacing-inactivity.txt`: `region completed: NO (horizon reached)`, `forest=incomplete, research=incomplete`, `final arcs: ecology 4/4, settlement 4/4`. Exact shortfall 2,980. R-007 and R-013 both realised for the product's own stated target player (`MASTER_PLAN.md` §1) | `close-inactivity-pacing-shortfall` |
| F-20 | **After region completion the game permanently demands an impossible action, and earned progress has no sink** | P1 | Product / UX | Reproduced: `longhaul --days 400` → 19/19 complete; then 60 further days of walking leaves `balances: vitality=60650` with every project and expedition complete. `GetHome()` computes `queueIdle = true`, `banked = 60650 > 0` → `RequiresAttention = true`, reason `QueueEmptyWithBankedVitality` — with **no completion condition**. No test asserts Home after region completion anywhere. Contradicts D-038 ("evergreen") and `MASTER_PLAN.md` §10 ("the next *useful* action") | `define-post-completion-evergreen-state` |
| F-21 | `DomainResult<T>` and three `ErrorCodes` members are dead; each is a small lie about what the system distinguishes | P3 | Dead surface | `DomainResult<` has 0 references in `src/`/`tools/`; 2 in its own unit test. `ErrorCodes.NotAvailable`, `.QueueEmpty`, `.InvalidArgument` have 0 references outside their declarations. C# does not warn on any of it | `resolve-unused-domain-surface` |

### Considered and deliberately not proposed

Recorded so the next agent does not re-investigate them.

| Item | Why not proposed |
|---|---|
| `RegionDefinition.FindX` are O(n) linear scans called in per-completion and per-boot loops | No demonstrated impact: 19 projects, and `bench`/`scenarios` already record the cost. The content graph is authored to grow, so revisit if Region 2 lands. |
| `AtomicFileSaveStore(directory, slotName)` has no path-traversal validation on `slotName` | No untrusted input reaches it; every composition root passes a compile-time constant. Defense-in-depth only, with no threat model. |
| `system.text.json` 8.0.5 is behind 9.x | No advisory applies and the runtime surface is one first-party package. Version churn without benefit. |
| Adopting `dotnet format` as a CI gate | Already a considered, recorded decision in `TESTING_AND_RELEASE.md` §16. This plan does not reopen it. |
| Introducing a `dotnet format` gate as part of the toolchain change | Same as above. |
| Making the producer tick use checked arithmetic | A throw on the player's critical path is worse than the authoring gate catching the overflow; F-08's change fixes the gate instead. Recorded as deferred. |
| A write-ahead journal for saves | Disproportionate at this scale; F-01's change makes the existing guarantee hold at the application layer. Deferred to a pre-Region-2 decision. |
| Containerised/hermetic builds | Disproportionate; `global.json` + central versions is proportionate for a single-developer-quality repo. Deferred. |

---

## 5. Target Architecture / Desired End State

Evolutionary, not a rewrite. Every existing structural strength is preserved.

### Architectural boundaries (after this roadmap)

Unchanged and still enforced: Domain has no references, no packages, no
filesystem access, no engine namespace; Application references only Domain;
Infrastructure references Application + Domain. The plan adds **no** new layer
and **no** new cross-layer dependency.

The one genuinely new structural concept is a **trusted-time reference** for
activity acceptance, which is a Domain value type computed once per ingestion
batch from durable evidence the Domain already owns. It keeps validation pure
and O(1) per record — it does not make validation depend on the state aggregate.

### Reliability characteristics

- Every readable save generation is recovered, including the absent-primary case.
- Activity is credited across realistic absence and device-clock skew, and never
  re-credited.
- Deletion markers obey the same eligibility rules as ordinary records.
- Canonical state converges to a documented bound instead of growing forever,
  with retirement that provably cannot re-credit a record.
- Boot outcomes are exhaustive, so no failure class is silently misreported.

### Security expectations

Unchanged and already strong: no network, no telemetry, no accounts, minimal
health-adjacent retention, fail-closed on unknown content, privacy-safe
diagnostics with an exhaustive no-leak sweep. Added: a save↔content compatibility
gate so content drift is caught at a gate rather than by a player's first boot.

The one open security question is **not** a code change: at-rest confidentiality
(`KI-006`, plaintext health-adjacent save) is a product/security decision that
must be dispositioned before M9.

### Testing expectations

- Every public use case is covered, with an enumeration guard preventing a new
  one from shipping untested.
- Every numeric bound the content validator enforces has a negative-edge test.
- The trusted-time window has boundary tests at the floor and at the skew
  allowance, plus the realistic backlog and fast-clock scenarios.
- Retirement, post-completion and the completed-state surfaces all have
  behavioural regression tests — the three areas where this audit found defects
  precisely because the *state* was tested but the *surface* was not.
- Coverage is collected and floored low, as a loss detector rather than a target.

### Observability expectations

No logging framework is introduced (the repository's zero-network, privacy-first
posture is preserved). The observability surface is the bounded diagnostics
read model, extended so that the reason a player's activity was not credited, and
whether a source is stale, are both answerable from durable state after a
restart.

### Performance expectations

- Commit cost is bounded by a documented growth model, with a convergence gate.
- Boot no longer performs O(retained history) recomputation for the lifetime
  aggregates or the transaction index.
- The growth series stops being linear and is asserted to converge.

### Product-quality expectations

- The return summary keeps reporting **recent** progress indefinitely and stops
  demanding attention for a summary that is entirely historical.
- Every balanced activity profile — including weekday-only with a multi-week
  absence — can finish the game.
- After completion the game concludes rather than nags, and continued activity
  has a visible use.
- Secondary resources carry a real decision or are explicitly declared as
  lifetime scores.

### Release-readiness criteria

Unchanged in shape from `TESTING_AND_RELEASE.md` §20, with two additions: the
content-drift gate and the dependency-vulnerability gate. **Release
qualification remains blocked** by `KI-001` (hosted CI) and `KI-002` (no Unity
editor) regardless of how much of this roadmap is implemented — the plan makes
the headless lane sound, it does not unblock the runtime lane.

---

## 6. Implementation Roadmap

Phases, not a wishlist. Each phase has an exit condition and its OpenSpec
changes. Within a phase, changes may run in parallel **except** where §7 states
otherwise.

### Phase 0 — Baseline and safety

Land the reproducibility foundation first, so every later phase's build and test
result is trustworthy.

- `pin-build-toolchain-and-centralize-packages` (F-12)

**Exit:** `dotnet build --no-incremental` reports 0 errors and 0 warnings; the
architecture gate's netstandard2.1 rule still **fails on a broken fixture**;
`verify-ci-steps.sh` runs the whole gate.

### Phase 1 — Critical defects (data loss)

Both P0s, plus the taxonomy cleanup that F-01 must carry.

- `recover-save-from-backup-when-primary-is-missing` (F-01, F-10)
- `anchor-activity-acceptance-window-to-trusted-time` (F-02, F-03, F-09)

**Exit:** the F-01 reproduction no longer loses a world; a 30-day backlog is
credited in full; a fast clock does not disable crediting; all seven pacing
reports still reproduce byte-for-byte; the then-current test baseline was green plus
additions.

### Phase 2 — Product integrity

The ending, the attention surface, and the economy's terminal state.

- `rotate-durable-return-summary` (F-05)
- `define-post-completion-evergreen-state` (F-20)
- `bound-secondary-resource-economy` (F-06)

**Exit:** the summary keeps reporting recent progress; a completed region
requires no attention and has a defined sink; every produced resource has an
authored, validated cap; at least one project carries a secondary requirement.
Pacing evidence regenerated and every diff explained.

### Phase 3 — Mature-save safety and data integrity

- `gate-saves-against-authored-content-version` (F-04)
- `align-content-validator-with-simulation-overflow-bounds` (F-08)
- `compact-activity-and-reward-ledgers` (F-07)

**Exit:** content drift is caught by a CI gate and cannot brick a profile via a
tombstoned ID; accepted content rates are provably non-overflowing; the growth
series converges and the convergence is asserted.

### Phase 4 — Pacing remedy

- `close-inactivity-pacing-shortfall` (F-19, F-13)

**Exit:** every balanced profile reaches closure; the pacing harness **fails**
rather than reports a shortfall; all seven reports regenerated with per-profile
diffs explained; the incorrect conversion figure corrected everywhere it appears.

### Phase 5 — Verification and diagnosability

- `surface-ingestion-rejection-reasons` (F-14)
- `close-application-use-case-test-gaps` (F-11, F-17)

**Exit:** a lost-activity report is answerable from durable state; the
reconciliation state distinguishes stale from quiet; every public use case is
covered and an enumeration guard prevents regressions; the no-leak sweep is
exhaustive by construction.

### Phase 6 — Tooling and operability

- `harden-simulation-cli-integrity-selftest` (F-16)
- `resolve-unused-domain-surface` (F-15, F-21)
- `extend-ci-and-release-automation` (F-18)

**Exit:** the self-test is correct under any key casing; CLI exit codes match
their documented contract; every dormant surface item has a recorded decision;
coverage, vulnerability checking and a release-evidence workflow exist as
workflow steps.

### Dependency order (authoritative)

```
Phase 0  pin-build-toolchain
   |
   +--> Phase 1  recover-save-from-backup  ──┐
   |            anchor-activity-window       │
   |                                          v
   +--> Phase 2  bound-secondary-resource  <──┘ (needs the post-change window)
   |            rotate-durable-return-summary
   |            define-post-completion-evergreen  (needs caps + closure remedy)
   |
   +--> Phase 3  align-content-validator  <── shared file with the two below
   |            gate-saves-content-version
   |            compact-ledgers           (needs the post-change window)
   |
   +--> Phase 4  close-inactivity-pacing  (needs caps, content-version, window)
   |
   +--> Phase 5  surface-ingestion-rejection  (needs the window change)
   |            close-use-case-test-gaps      (independent; land any time)
   |
   +--> Phase 6  harden-cli-selftest      (needs recover-save-from-backup)
                resolve-unused-domain    (independent)
                extend-ci-automation     (needs pin-build-toolchain)
```

**Two hard couplings a reviewer must watch:**

1. `bound-secondary-resource-economy`, `gate-saves-against-authored-content-version`
   and `align-content-validator-with-simulation-overflow-bounds` **all edit
   `ContentValidator`**. Applied out of order, one rule set can be lost in a
   rebase. The zero-violations test for the shipped catalog catches a lost rule
   group, so re-verify the full `ContentValidationTests` after any rebase.
2. `recover-save-from-backup-when-primary-is-missing` must land **before**
   `harden-simulation-cli-integrity-selftest`, which verifies the exit code that
   the former makes reachable.

---

## 7. Parallel Workstreams

### Lanes

| Lane | Changes | Notes |
|---|---|---|
| **Persistence / save integrity** | recover-save-from-backup, gate-saves-content-version, compact-ledgers | Touches `GameSession`, `GameState`, codec, validator. Highest conflict risk. |
| **Activity trust pipeline** | anchor-activity-window, surface-ingestion-rejection | Both touch `IngestActivityBatch` and `IngestionOutcomeState`. **Sequential** (rejection change follows the window change). |
| **Product surface / UX** | rotate-durable-return-summary, define-post-completion-evergreen | Both touch `GetHome()` and the summary composer. **Sequential** (post-completion needs the caps). |
| **Economy / content** | bound-secondary-resource, align-content-validator, close-inactivity-pacing | All three touch `ContentValidator` or `Region1Catalog`. **Sequential**, in that order. |
| **Testing / verification** | close-use-case-test-gaps, (coverage part of) extend-ci-automation | Test-project only. Safe to run in parallel with everything. |
| **Tooling / DX** | pin-build-toolchain, harden-cli-selftest, extend-ci-automation | `pin-build-toolchain` first; the others interact with the warnings policy. |
| **Documentation / evidence** | regeneration embedded in bound-secondary-resource and close-inactivity-pacing; the F-13 correction is in close-inactivity-pacing | Not a separate lane — evidence must be regenerated **inside** the change that invalidates it, never after. |

### Safe to delegate in parallel (no shared architectural surface)

- `close-application-use-case-test-gaps` — test project only.
- `resolve-unused-domain-surface` — Domain declaration files plus docs.
- `pin-build-toolchain-and-centralize-packages` — project files and workflows,
  provided it lands before `extend-ci-and-release-automation`.

### Must remain sequential

- The three `ContentValidator` changes.
- The two activity-pipeline changes.
- The two `GetHome()`/summary changes.
- `recover-save-from-backup` → `harden-cli-selftest`.
- `pin-build-toolchain` → `extend-ci-automation`.
- `bound-secondary-resource` + `close-inactivity-pacing` → `define-post-completion-evergreen`.

### Multi-writer safety (from `AGENTS.md`)

Concurrent implementation sessions **must not** share a writable work tree. Use
`scripts/new-agent-worktree.sh <campaign> <session-id>`, which creates the branch
and linked worktree and acquires the lease. Never pass `--force` to the lease
tooling. The P0s in Phase 1 are urgent enough to justify two parallel sessions
only if they are given separate worktrees and Phase 2–6 are sequenced behind
them.

---

## 8. Definition of Done

Measurable, repository-level. "Complete and hardened" means **all** of the
following are true.

### Functionality and correctness

- [ ] Every activity profile the region is balanced against reaches closure
      within its documented horizon; the pacing harness **fails** on a shortfall.
- [ ] A returning player after any realistic absence has their full backlog
      credited exactly once, and a device-clock offset does not disable crediting.
- [ ] A save whose primary generation is absent is recovered from the backup; a
      recovered generation is never orphaned or displaced by a known-bad one.
- [ ] No boot condition is silently misreported; every declared outcome value is
      reachable; a state-invariant failure exits with the validation exit code.
- [ ] Deletion markers obey the same eligibility rules as ordinary records.
- [ ] Post-completion, the game requires no impossible action and continued
      activity has a visible use.
- [ ] The return summary reports recent progress indefinitely and stops claiming
      attention for a wholly historical summary.

### Data integrity

- [ ] Content drift is caught by a CI gate; a retired content identifier resolves
      to a tombstone rather than bricking a profile.
- [ ] Every authored producer rate and capacity is provably representable in the
      production simulation, with a negative-edge test per bound.
- [ ] Durable history converges to a documented bound; retirement provably cannot
      re-credit a record, and a correction to a retired record is handled
      conservatively.
- [ ] Maintained lifetime aggregates are cross-checked against a full
      recomputation on every load.
- [ ] Every produced resource has an authored, validated cap; parked output is
      released without any player action.

### Tests and verification

- [ ] Every public application use case has at least one test, and an enumeration
      guard fails if a new one ships untested.
- [ ] Every numeric bound the content validator enforces has a negative-edge test.
- [ ] The player-facing and support surfaces are covered by an exhaustive no-leak
      sweep that is proven to fail on a deliberately leaking read model.
- [ ] `dotnet test` green; both acceptance suites assert **and achieve**
      byte-identical deterministic canonical state.
- [ ] All seven pacing profiles and the save-growth series reproduce
      byte-for-byte from a clean checkout.
- [ ] Every new gate is **proven to fail** against a deliberately broken fixture
      before it is trusted.

### Build, tooling, reproducibility

- [ ] `dotnet build --no-incremental`: 0 errors, 0 warnings.
- [ ] SDK pinned; shared settings declared once; package versions centralised.
- [ ] `scripts/verify-architecture.sh`: 11 passed, 0 failed, with every rule
      proven to fail on a broken fixture.
- [ ] `scripts/verify-ci-steps.sh` executes every CI step and is part of the
      default local preflight.
- [ ] `tests/guards/run-guard-tests.sh` green; identity guard exits 0.

### Documentation and evidence

- [ ] No documentation claim is contradicted by committed evidence. Specifically:
      every pacing figure is derivable from its committed report, and the
      incorrect conversion-rate figure is corrected everywhere it appears.
- [ ] `docs/KNOWN_ISSUES.md` dispositions every item this plan closes, with the
      evidence that closes it; no item is closed without evidence.
- [ ] `docs/RISK_REGISTER.md` R-002, R-005, R-007, R-013, R-014, R-017, R-018,
      R-020 each carry the evidence this plan produced, and still state honestly
      what remains unmitigated or externally blocked.
- [ ] `docs/DECISIONS.md` records every product, balance and architectural
      decision this plan required, with its rationale.
- [ ] `docs/PRIVACY_REVIEW.md` is re-run and its mechanical commands re-verified.
- [ ] No feature is labelled complete above its evidence state.

### Release readiness — explicitly NOT achieved by this plan

- [ ] **No Critical or High item open** (`TESTING_AND_RELEASE.md` §15).
      `KI-001` (hosted CI billing) and `KI-002` (no Unity editor) are
      **externally blocked** and cannot be closed from this repository.
- [x] `KI-006` (at-rest confidentiality; D-067, OS encryption as system requirement) dispositioned as a product/security
      decision.
- [ ] Runtime, device, accessibility and performance lanes verified — blocked on
      `KI-002`.

### Unresolved defect thresholds

- Critical: **0**. High: **0** open from this repository's own code; externally
  blocked items are listed above and never counted as closed. Medium: each carries
  a recorded pre-release disposition. Low: may ship with documented rationale.

---

## 9. Final Instructions for the Implementation Agent

1. **Read this document, then read the change you are assigned in full** —
   `proposal.md`, `design.md`, `tasks.md`, and the `specs/**/spec.md` deltas.
   This document is an index; the change artifacts are the contract.

2. **Verify repository state before starting.**
   ```bash
   scripts/assert-repo-identity.sh          # must print OK; exit 86 = STOP
   git fetch origin && git status --short --branch
   git rev-parse HEAD                       # record your starting SHA
   scripts/writer-lease.sh acquire          # exit 87 = another writer, STOP
   ```
   Never pass `--force`. Do not write into another session's worktree.

3. **Do not blindly trust the plan if the code has changed.** The findings were
   established against `69ca51a`. If a finding's evidence no longer holds —
   because an earlier change in this roadmap already fixed it — verify that
   directly and record the reconciliation rather than implementing a fix for a
   defect that no longer exists.

4. **Execute higher-priority and prerequisite work first**, per the dependency
   order in §6. Two couplings are load-bearing: the three `ContentValidator`
   changes must be applied sequentially, and
   `recover-save-from-backup-when-primary-is-missing` must precede
   `harden-simulation-cli-integrity-selftest`.

5. **Preserve working functionality unless the roadmap explicitly calls for a
   behaviour change.** The exactly-once guarantees, the fail-closed validator
   posture, the byte-identical determinism, and the privacy-safe diagnostics
   envelope are not to be relaxed for convenience. Where a change does alter
   behaviour, the change's spec says so explicitly.

6. **Add regression coverage alongside fixes**, in the same commit, naming the
   test file. Several defects in this plan survived a hardening campaign that
   covered the *state* but not the *surface* — the tests must assert observable
   behaviour, not only internal state.

7. **Prove every new gate fails before trusting it.** This is the repository's
   established discipline (`verify-architecture.sh` rule 6 exists because an
   unproven gate shipped broken once). A gate never observed failing is not a
   gate.

8. **Regenerate committed evidence inside the change that invalidates it**, and
   produce a **per-profile diff** with the significance of each change explained
   in `docs/DECISIONS.md`. Never bulk-overwrite `docs/evidence/`, and never
   regenerate evidence to make a failing check pass.

9. **Validate every significant change** with the real gates:
   ```bash
   dotnet build SimpleWalkGame.sln --no-incremental
   dotnet test
   scripts/verify-architecture.sh
   scripts/verify-pacing-evidence.sh
   scripts/verify-save-growth.sh
   tests/guards/run-guard-tests.sh
   openspec validate <change> --strict
   ```
   A gate you cannot execute in your environment must be reported as
   **UNVERIFIED**, honestly — never claimed green.

10. **Keep the repository clean.** No build output, save slots, temp files or
    editor state committed. Commit in logical units with detailed messages. No
    half-broken state on the integration branch. No `git push --force`,
    `git reset --hard`, or `git clean -fdx` without explicit human-operator
    authorization; reconcile deliberately if `origin` has advanced.

11. **Update the OpenSpec change as you work.** Tick tasks in `tasks.md` as they
    complete. If you discover that a finding is wrong, a design is unsuitable,
    or the repository differs from this document, **say so in the change
    artifacts** rather than silently diverging.

12. **Surface newly discovered high-impact defects rather than absorbing them.**
    If you find a new P0 or P1, stop and report it. Three of the findings in this
    plan (F-01, F-02, F-20) were missed by the previous campaign and found only
    by probing behaviour the existing tests asserted only partially. That is the
    expected mode; act on it.

13. **Re-run this audit's own method at the end.** For each P0/P1 finding,
    re-execute the exact reproduction recorded in §4 and show it now behaves
    correctly. A finding is closed by evidence, not by a merged diff.

14. **Perform final repository-wide certification** once the roadmap is
    implemented: re-run every gate from a clean checkout, re-run the privacy
    review's mechanical commands, reconcile documentation against implementation,
    and state plainly which Definition-of-Done items remain blocked on the
    external Unity and hosted-CI dependencies. **Do not claim release
    qualification** — that is blocked by `KI-001` and `KI-002` regardless of how
    much of this roadmap ships.
