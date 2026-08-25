# Simple Walk Game

> **An ambient fitness restoration game where real-world activity is the primary gameplay.**

Simple Walk Game is a mobile game designed for people who may want the motivation and progression of a game **without being required to spend significant time actively playing one**.

The core premise is deliberately simple:

**move in real life → generate progress → restore a persistent world → make short meaningful decisions → optionally visit and interact with that world**

This repository is currently in a **documentation-first / pre-implementation phase**. The documentation defines the product contract, architecture, gameplay systems, quality gates, and implementation roadmap before production code is introduced.

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

**Status: M1 (deterministic core and durable state) and M2 (activity trust pipeline) implemented and automated-verified; Unity presentation not started.**

The repository now contains a headless .NET implementation of the deterministic game core:

- `src/WalkGame.Domain` — pure domain (`netstandard2.1`, C# 9, zero engine/platform dependencies): stable IDs, resource accounting, reward ledger with exactly-once semantics, project state machine and queue with auto-rollover, region/landmark/producer model, deterministic offline advancement with backward-clock defense, injected clock, persisted-seed RNG, content/state validators.
- `src/WalkGame.Application` — use-case orchestration (`GameSession`): boot/load/recover/migrate/advance/save flow, activity crediting, queue management, return summaries, read models. Dev content seed in `Content/Region1Catalog`.
- `src/WalkGame.Infrastructure` — versioned JSON save envelope with SHA-256 payload integrity, sequential migration pipeline, atomic snapshot store with one-generation backup recovery.
- `tests/` — 131 automated tests across domain, infrastructure and application suites (idempotency, determinism, roundtrip, corruption/recovery, migration harness).
- `tools/simulation` — headless CLI: `new / credit / advance / simulate / dump / validate` for deterministic multi-day simulation and save validation.

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

- `dotnet build SimpleWalkGame.sln` — clean, zero warnings/errors;
- `dotnet test` — Domain.Tests 85 passed, Infrastructure.Tests 19 passed, Application.Tests 27 passed;
- CLI smoke run — fresh save → credit → 10-day simulation → project completion + queue rollover + landmark stage change → validate (0 violations, integrity self-test PASS);
- guard proof suite — `tests/guards/run-guard-tests.sh`, 25/25 assertions green;
- **unverified:** device/runtime behavior, battery/performance budgets, Health Connect/HealthKit integration, Unity scene binding (M5–M7 scope).

Key implementation decisions are recorded in [`docs/DECISIONS.md`](docs/DECISIONS.md) D-024…D-031.

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

The immediate next campaign is M3 (ambient progression vertical slice with minimal UI), per [`docs/ROADMAP.md`](docs/ROADMAP.md).

The project should resist premature feature expansion. A small, deeply integrated, polished ambient-fitness loop is more valuable than a large collection of disconnected game systems.
