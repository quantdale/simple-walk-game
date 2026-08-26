# Simple Walk Game

> **An ambient fitness restoration game where real-world activity is the primary gameplay.**

Simple Walk Game is a mobile game designed for people who may want the motivation and progression of a game **without being required to spend significant time actively playing one**.

The core premise is deliberately simple:

**move in real life → generate progress → restore a persistent world → make short meaningful decisions → optionally visit and interact with that world**

This repository implements the product contract defined by its documentation: a deterministic, offline-first game core with an exactly-once activity trust pipeline, now proving its ambient progression loop end-to-end (M3).

---

## Product thesis

Most fitness games eventually become games that happen to consume fitness data. This project takes the opposite position:

- Real-world movement is the primary action.
- The game should continue progressing while the app is closed.
- A player should be able to get value from sessions lasting seconds.
- Longer sessions are optional, not required to preserve streaks, resources, or progress.
- The world should make physical activity visibly meaningful.
- Inactivity should slow progress, not destroy prior accomplishments.
- Fitness data should be handled conservatively and privately.

The intended experience is closer to a **persistent ambient world attached to a person’s real activity** than a conventional mobile game that demands attention.

---

## Core player loop

1. **Move** — walking, running, and eventually other supported activity contributes eligible activity.
2. **Convert** — validated activity becomes bounded game resources such as Vitality.
3. **Progress automatically** — queued restoration projects, producers, expeditions, ecosystems, and research consume those resources according to player priorities.
4. **Return briefly** — the player sees what changed, claims discoveries where appropriate, resolves a small number of choices, and sets priorities.
5. **Optionally go deeper** — the player can visit the world, inspect restored areas, customize settlements, read discoveries, or perform light management.
6. **Leave again** — the game remains useful while closed.

The default experience must not require repeated tapping, grinding, ad watching, or artificial waiting loops.

---

## Non-negotiable product rules

- **No screen-time tax.** Core progression cannot require long foreground sessions.
- **No punishment spiral.** Missing a day may reduce momentum, but does not erase buildings, destroy the world, or invalidate prior effort.
- **No pay-to-progress design assumption.** Monetization is outside the MVP architecture and must not contaminate progression design.
- **No fake precision.** Fitness/activity data is imperfect; systems must tolerate duplicates, late arrivals, corrections, source changes, and clock anomalies.
- **No silent double-crediting.** The same activity must never create rewards twice.
- **Offline-first.** The core game remains functional without a backend or account.
- **Privacy by default.** Raw health/activity data is minimized; only the information required for game progression is retained.
- **Canonical state first.** Visuals reflect deterministic game state. Presentation must not become the source of truth.
- **Performance is a feature.** Optional 3D presentation cannot make the passive game expensive to run.
- **Evidence over claims.** Documentation must distinguish implemented, automated-verified, editor-verified, device-verified, and unverified behavior.

---

## Target session model

| Session type | Target duration | Required? | Purpose |
|---|---:|---|---|
| Glance | 5–15 sec | Yes, occasionally | See what changed; dismiss/accept a critical choice |
| Daily check-in | 20–60 sec | No strict requirement | Review progress, choose next priority |
| Management | 2–5 min | Optional | Reorder projects, inspect producers, configure automation |
| World visit | 5–20+ min | Optional | Explore the restored region, inspect spaces, customize, read lore |

A player who only uses the first two modes must still experience a coherent, satisfying game.

---

## Documentation map

| Document | Purpose |
|---|---|
| [`docs/MASTER_PLAN.md`](docs/MASTER_PLAN.md) | Product and engineering master plan; global success criteria |
| [`docs/PRODUCT_SPEC.md`](docs/PRODUCT_SPEC.md) | Audience, experience pillars, loops, requirements, non-goals |
| [`docs/GAME_SYSTEMS.md`](docs/GAME_SYSTEMS.md) | Progression, restoration, projects, production, expeditions, discoveries |
| [`docs/ACTIVITY_PIPELINE.md`](docs/ACTIVITY_PIPELINE.md) | Health/activity ingestion, validation, deduplication, reconciliation, abuse resistance |
| [`docs/TECHNICAL_ARCHITECTURE.md`](docs/TECHNICAL_ARCHITECTURE.md) | Proposed code architecture, state boundaries, persistence, platform adapters |
| [`docs/UX_DESIGN.md`](docs/UX_DESIGN.md) | Attention budget, information architecture, accessibility, notification principles |
| [`docs/WORLD_AND_CONTENT.md`](docs/WORLD_AND_CONTENT.md) | Region structure, restoration language, content schema, authoring requirements |
| [`docs/PERFORMANCE_BUDGETS.md`](docs/PERFORMANCE_BUDGETS.md) | Frame, memory, load, battery, storage, background-work budgets |
| [`docs/TESTING_AND_RELEASE.md`](docs/TESTING_AND_RELEASE.md) | Test strategy, quality gates, device evidence, release qualification |
| [`docs/ROADMAP.md`](docs/ROADMAP.md) | Milestone sequence from foundations to production-ready MVP |
| [`docs/AGENT_EXECUTION_GUIDE.md`](docs/AGENT_EXECUTION_GUIDE.md) | Rules for autonomous coding agents and long development sessions |
| [`docs/RISK_REGISTER.md`](docs/RISK_REGISTER.md) | High-risk product/technical assumptions and mitigation plans |
| [`docs/DECISIONS.md`](docs/DECISIONS.md) | Architectural/product decisions and unresolved decision records |

---

## Proposed implementation direction

The baseline architecture is intended to preserve a portable, deterministic game core:

- **Unity 6 LTS + C#** for mobile presentation and optional real-time world visits.
- A **pure C# domain layer** with no Unity engine dependency.
- Explicit **application/use-case layer** for orchestration.
- Platform-specific adapters for **Android Health Connect** and **Apple HealthKit** or equivalent approved activity sources.
- Local, durable persistence through an adapter with atomic writes, migration support, integrity checks, and recovery.
- No mandatory backend for the first production slice.
- Tests for domain behavior must run outside the Unity runtime wherever practical.

The architecture is intentionally designed so the game can remain lightweight in ordinary use while retaining the option for a richer 3D visit mode.

---

## MVP definition

The first production-quality vertical slice is **one complete region**, not multiple shallow regions.

A qualifying MVP must demonstrate:

- passive/foreground activity ingestion on supported platforms;
- exactly-once conversion of activity into progression;
- a persistent region that visibly changes through several restoration stages;
- queued projects that progress while the app is closed;
- a small producer/economy layer with sensible automation;
- discoveries or expeditions that give movement emotional payoff beyond currency;
- onboarding that explains the premise in under two minutes;
- a daily loop that works in under one minute;
- an optional interactive world visit;
- durable save migration/recovery;
- accessibility and reduced-motion support;
- device-tested performance and battery behavior;
- automated regression tests around the highest-risk state transitions.

Anything that does not materially strengthen that loop is secondary until the slice is proven.

---

## Definition of done

“Implemented” is not enough.

A feature is considered production-ready only when:

1. its domain rules are documented;
2. state ownership and failure behavior are defined;
3. deterministic automated tests cover the important paths;
4. persistence/migration consequences are handled;
5. error, empty, loading, offline, and restart states are covered;
6. accessibility semantics exist where relevant;
7. target-device behavior has been verified when platform/runtime behavior is involved;
8. performance remains inside the documented budget;
9. documentation matches the actual implementation;
10. there are no known Critical or High severity regressions.

See [`docs/TESTING_AND_RELEASE.md`](docs/TESTING_AND_RELEASE.md) for the full qualification model.

---

## Current repository state

**Status: M1 (deterministic core), M2 (activity trust pipeline), headless M3 (ambient progression), M4-H (Region 1 content systems) implemented and automated-verified; M8-H1 (headless trust & persistence hardening) completed; M5-H1 (platform-neutral UX state contracts) completed headless — durable local preferences/onboarding store (D-042), activity connection/permission status projection behind `IActivityConnectionPort` (D-043), shell-facing read models incl. support diagnostics and explicit Home attention semantics (D-044), eleven named low-attention acceptance scenarios plus adversarial hardening suites. **295 automated tests**. Unity presentation and all runtime/device behavior remain UNVERIFIED and externally blocked (no editor in this environment; D-035).**

The repository now contains a headless .NET implementation of the deterministic game core:

- `src/WalkGame.Domain` — pure domain (`netstandard2.1`, C# 9, zero engine/platform dependencies): stable IDs, resource accounting, reward ledger with exactly-once semantics, project state machine and queue with auto-rollover, region/landmark/producer model, deterministic offline advancement with backward-clock defense, injected clock, persisted-seed RNG, content/state validators.
- `src/WalkGame.Application` — use-case orchestration (`GameSession`): boot/load/recover/migrate/advance/save flow, activity crediting, queue management, return summaries, read models. Dev content seed in `Content/Region1Catalog`.
- `src/WalkGame.Infrastructure` — versioned JSON save envelope with SHA-256 payload integrity, sequential migration pipeline, atomic snapshot store with one-generation backup recovery.
- `tests/` — 221 automated tests across domain, infrastructure and application suites (idempotency, determinism, roundtrip, corruption/recovery, migration harness, producer capacity/store bounds, durable return summaries, queue control, content red-team validation, end-to-end M3 and M4 acceptance, plus the M8-H1 hardening suites described below).
- `tools/simulation` — headless CLI: `new / credit / advance / simulate / walk / profile / longhaul / bench / ack / dump / validate` for deterministic multi-day simulation, pacing reports, long-horizon growth records, phase-level performance measurement and save validation.

**M2 activity trust pipeline (automated verified):**

- Normalized activity records (`WalkGame.Domain.Activity`): platform-neutral shape carrying provenance only — never raw health payloads.
- Versioned identity: durable source record ID when available, deterministic SHA-256 content fingerprint otherwise (`rec1`/`fpt1` scheme prefixes).
- Versioned validation policy: category/unit gates, positive quantities, timestamp ordering, future-skew rejection, 14-day reconciliation horizon (D-030), pathological quantity clamping (~4× extreme day).
- Conversion rule v1: integer floor division, 100 steps → 1 Vitality, version stored on every processed row.
- Durable dedup ledger (`ProcessedRecordLedgerState`): exactly-once trust keyed by logical record identity, surviving restarts and overlapping queries.
- Correction/deletion policy (D-029): higher-revision redeliveries adjust credit deterministically; reversals clamp conservatively to the unspent balance so completed world content can never be destroyed; unclawed remainders are durably counted for diagnostics.
- Ingestion checkpoint watermark advances only in the same atomic commit as the rewards it describes.
- Per-batch diagnostics: received/accepted/rejected-by-code/duplicate/corrected/deleted/stale/clamped totals plus net vitality movement.
- Fixture provider (`FixtureActivityFileReader`) feeds checked-in JSON fixtures through exactly the production ingestion path — no fixture-specific code exists.

Verification evidence (automated verified per `docs/AGENT_EXECUTION_GUIDE.md` §17):

- `dotnet build SimpleWalkGame.sln` — clean, zero errors;
- `dotnet test` — Domain.Tests 89 passed, Infrastructure.Tests 23 passed, Application.Tests 44 passed;
- M3 acceptance path (`tools/simulation walk`) — 16 app-closed days of normalized synthetic records through `IngestActivityBatch`: 3200 Vitality credited exactly once, three project completions, landmark stage changes, producer unlock + bounded offline production, durable return summaries surviving restarts, replay of the identical window crediting zero (16 duplicates ignored), save validates at schema v2 with integrity self-test PASS;
- guard proof suite — `tests/guards/run-guard-tests.sh`, all assertions green;
- **unverified:** device/runtime behavior, battery/performance budgets, Health Connect/HealthKit integration, Unity scene binding (M5–M7 scope).

**M3 ambient progression vertical slice (automated verified, headless):**

- Producer offline production enforces the documented capacity contract (`min(storeRoom, rate × elapsed)`, D-032): bounded pending-output store, no-waste overflow, auto-delivery, parked-flush when resource caps free space; monotonic checkpoints at every public path including backward-clock defense on direct `TickProducers` calls.
- Save schema v2 with registered sequential migration (`m1-to-v2-producer-stored-milli-units`) and representative v1 fixture tests.
- Durable typed return summaries (D-033): committed-before-presented crash safety, priority-ordered bounded items, single primary next action, idempotent acknowledgement that never alters progression.
- Complete queue contract: persisted auto-advance toggle, manual start when automation is off, validated reorder, Projects/Region/Home read models.
- Platform-neutral activity-source seam (D-034): `IngestFromSource` drives the unchanged M2 trust pipeline; dev-only synthetic injector isolated in `WalkGame.Application.Development`; `tools/simulation walk --replay` is the reproducible acceptance harness.

Key implementation decisions are recorded in [`docs/DECISIONS.md`](docs/DECISIONS.md) D-024…D-035.

**M4-H Region 1 content systems (automated verified, headless; D-036…D-039):**

- Full authored Region 1 graph — Millbrook Valley content version 2: **19 projects** in six interdependent restoration chains (trail access, water system, settlement community, wetland recovery, woodland, research), **6 landmarks** with canonical stage triggers, **3 bounded producers**, **13 provenance-bearing discoveries** with deterministic project-completion triggers and independent reviewed state, **3 expedition routes** with deterministic availability/completion hooks and cap-clamped rewards, region-level **ecology and settlement arcs** (4 discrete stages each), and the **Complete Valley Survey closure milestone** with a stable post-completion evergreen state. The original five seed definitions are preserved verbatim for save compatibility.
- Additive schema-v2 persistence (D-036): absent entries mean "nothing unlocked yet" — pre-M4 saves decode with exactly-once semantics intact, proven by strip-and-redecode backward tests.
- `ContentValidator` is now a release gate: forward-safe reference resolution, cycle detection, hidden-deadlock/unreachable-content rejection, critical-path reachability to the closure milestone, arc monotonicity, discovery/expedition/producer integrity and overflow bounds.
- Deterministic pacing evidence (`tools/simulation profile`, reports under [`docs/evidence/m4/`](docs/evidence/m4)): high completes day 97, irregular day 139, moderate day 242; low is a documented long tail (62% of region vitality within 400 days); foreground pressure stays at one queue decision per project everywhere.
- Named acceptance proof `M4Region1AcceptanceTests`: clean profile → real ingestion pipeline → all chains complete → closure milestone → replay of the entire window credits zero → post-completion stable after reload → validators clean → byte-identical determinism.

Verification evidence additions:

- `dotnet build SimpleWalkGame.sln` — clean, zero errors;
- `dotnet test SimpleWalkGame.sln` — Domain.Tests 101 passed, Infrastructure.Tests 25 passed, Application.Tests 54 passed;
- `dotnet run --project tools/simulation -- validate --save <tmpdir> --selftest` on a completed-region save — violations=0, integrity self-test PASS;
- guard proof suite — `tests/guards/run-guard-tests.sh`, all assertions green;
- **unverified:** device/runtime behavior, battery/performance budgets, Health Connect/HealthKit integration, Unity scene binding (M5–M7 scope).

**M5-H1 platform-neutral UX state contracts (automated verified, headless):**

- Durable local UX-preferences/onboarding store (D-042, `LocalPreferencesStore` + `IUxPreferencesStore`): versioned schema v1 envelope, atomic single-file writes proportionate to data value, explicit malformed/future-version → documented-defaults policy with merge-over-defaults for missing keys, forward-only onboarding flow whose Complete step is canonically gated on a real first project chosen through normal queue operations. Preference writes are provably byte-neutral to the canonical save across restarts and churn.
- Activity connection/permission status projection (D-043): `IActivityConnectionPort` snapshot + pure `ActivityStatusProjector` producing player-safe states (`connected-current / permission-needed / permission-denied / source-unavailable / waiting-for-first-data / refresh-temporarily-failed`) plus last-outcome facts, with a documented precedence table under conformance tests; failed source fetches durably record bounded evidence then rethrow; external revocation is representable without touching earned progress.
- Shell-facing read models: `OnboardingReadModel`, `SettingsReadModel` (local preferences separated from canonical auto-advance), `ActivityStatusReadModel`, `DiagnosticsReadModel` (privacy-safe operational facts only — boot/recovery/migration evidence, watermark age, trust-pipeline aggregates including forever-visible unapplied reversals; no raw records/exceptions/payloads, adapter detail bounded to 300 chars), and `HomeReadModel` attention classification (`RequiresAttention`/reason/banked vitality).
- Acceptance + hardening: eleven named scenarios in `M5H1ShellAcceptanceTests` (grant/denial first run, 1/7/30-day returns, queue-empty, transient source failure, external revocation, save recovery, preference isolation, replay-after-UX zero-recredit) and `M5H1ContractHardeningTests` (hostile payload table, interrupted writes, rapid transitions, per-step onboarding interruption, reflection-based leak sweep).
- Evidence package: [`docs/evidence/m5-h1/`](docs/evidence/m5-h1). Decisions D-042/D-043/D-044.

**M8-H1 headless trust & persistence hardening (automated verified):**

- Persistence fault injection (`PersistenceFaultInjectionTests`, `SessionPersistenceHardeningTests`): recovery commits can no longer displace the last healthy backup generation (D-040 `WriteAtomicPreservingBackup`); access failures are diagnosed instead of crashing or masquerading as "no save found"; boot surfaces specific decode/validation reasons; unrecoverable saves fail closed without fabricating a fresh world; future-schema saves are never overwritten.
- Mature-save & migration qualification (`MatureSaveMigrationTests`): genuine rich v1 payload migrates through the registered chain with exactly-once replay after migration and canonical byte stability; content-identity durability under checksum-correct payload surgery; validator now requires a runtime row for every content producer (D-041).
- Adversarial red-team (`ActivityRedTeamTests`) and temporal anomalies (`TemporalAnomalyTests`): hostile permutations converge to identical canonical state; corrections/deletions pinned to exact D-029 semantics; horizon/skew edges decided exactly at documented boundaries; timezone/offset independence through the pipeline.
- Long-horizon & performance: `longhaul` CLI verb measures months-scale runs (365-day saves ≈202 KB, linear ~554 B/day growth documented as accepted exactly-once cost); seeded property suite (`SeededProgressionPropertyTests`); named end-to-end acceptance `M8H1HardeningAcceptanceTests` (interruption → recovery → exactly-once retry → corrections/deletions → double replay → long absence → closure → byte equivalence).
- Evidence package: [`docs/evidence/m8-h1/`](docs/evidence/m8-h1).

### Repository safety rails (agent isolation)

After two concurrent autonomous executor sessions damaged overlapping work (commits
`b12f52c`/`67368e3`), the repo enforces fail-safe rails for ANY coding agent:

- **Identity:** `.repo-identity.json` + `scripts/assert-repo-identity.{sh,ps1}` prove this
  checkout is `quantdale/simple-walk-game` (exit 86 otherwise) — sibling-repository
  execution fails closed.
- **Single writer:** `scripts/writer-lease.{sh,ps1}` grants one atomic per-worktree lease
  (exit 87 when busy); stale leases require an explicit human override, never auto-theft.
- **Lost-update protection:** `.githooks/pre-push` refuses pushes that would discard
  remote commits (exit 88); reconcile via fetch + deliberate merge/rebase instead.
- **CI:** `.github/workflows/ci.yml` re-checks identity under GitHub's own
  `GITHUB_REPOSITORY`, builds/tests, runs the simulation smoke and the guard proof suite.
- **Contract:** root [`AGENTS.md`](AGENTS.md) binds every harness; all `/goal` adapters
  inherit the preflight. See also `scripts/new-agent-worktree.sh` for isolated
  concurrent sessions (`one writer = one worktree = one branch`).

The immediate next campaign is the **Unity presentation shell + runtime verification of the M3/M4/M5-H1 boundaries** (requires an installed Unity 6 LTS editor), per [`docs/ROADMAP.md`](docs/ROADMAP.md) and D-035. The M3-R blocker remains truthful: no Unity editor exists in this execution environment. M5-H1 narrowed that future job to: render typed read models, invoke application operations, bind native permission/source adapters to `IActivityConnectionPort`, bind preferences to real controls, implement normal/loading/empty/error visuals, add accessibility/reduced-motion runtime behavior, and prove lifecycle/device budgets.

The project should resist premature feature expansion. A small, deeply integrated, polished ambient-fitness loop is more valuable than a large collection of disconnected game systems.
