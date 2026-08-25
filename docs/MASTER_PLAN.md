# Master Plan

## 1. Mission

Build a production-quality ambient fitness game in which **real-world activity is the primary gameplay** and the mobile app is a lightweight progression, restoration, and discovery layer.

The product must work for players who are busy, inconsistent, or unwilling to spend large amounts of time actively playing. A player should be able to walk during normal life, open the app briefly, understand what changed, make one or two useful decisions, and leave.

The game succeeds when physical activity feels like it has transformed a persistent world without creating another attention-hungry mobile habit.

---

## 2. Strategic product position

The project should not compete by maximizing foreground engagement. It should compete on:

- **ambient progression**;
- **visible world transformation**;
- **low cognitive overhead**;
- **respect for the player's time**;
- **fitness motivation without shame mechanics**;
- **meaningful offline progress**;
- **high technical trustworthiness around activity data and saves**.

The core fantasy is: **your real movement brings a damaged world back to life**.

This is stronger than a generic step counter because the reward is not merely points. The player's repeated movement should restore water, vegetation, settlements, wildlife, transit, research, infrastructure, and eventually whole regions.

---

## 3. Experience pillars

### 3.1 Movement is the input
Walking and other eligible real-world activity create game progress. The player should not need to manually start a session for ordinary walking.

### 3.2 Restoration is the reward
Progress must create visible, persistent state changes. The world is the long-term progress bar.

### 3.3 Automation protects the attention budget
Projects, producers, expeditions, and background systems should continue under player-defined priorities while the app is closed.

### 3.4 Decisions matter more than taps
The game should ask for occasional meaningful choices rather than frequent low-value interactions.

### 3.5 Optional depth, mandatory simplicity
Players who want to inspect, customize, explore, optimize, or read lore may do so. None of those activities can become required maintenance.

### 3.6 Progress is durable
The game does not punish inactivity by deleting buildings, killing restored ecosystems, or resetting major accomplishments.

### 3.7 Technical trust is part of game design
A fitness game loses credibility immediately if activity is lost, duplicated, unexpectedly re-counted, or if saves corrupt. Exactly-once crediting and recovery are top-tier product requirements.

---

## 4. Product constraints

The following are hard constraints until deliberately superseded by a documented decision:

1. Mobile-first.
2. Offline-first.
3. No mandatory account for the MVP.
4. No required GPS trail recording for the core loop.
5. No destructive inactivity punishment.
6. No repetitive manual claiming requirement for ordinary progress.
7. No ad-dependent progression assumptions.
8. No backend dependency for basic progression.
9. One complete region before Region 2.
10. Canonical domain state must be testable independently of rendering.
11. Activity must be deduplicated and reconciled.
12. Platform integrations must fail safely.
13. Documentation must distinguish aspiration from verified behavior.

---

## 5. Core loop specification

### Continuous loop

`activity source → normalized activity → validated eligible activity → reward ledger → world simulation → persistent canonical state`

### Player-facing loop

`move → return → see change → choose priority → leave → world progresses`

### Optional deep loop

`visit world → inspect restored spaces → customize → discover lore → plan next restoration target`

The continuous loop is the backbone. The app must still function as a compelling product if the optional deep loop is used rarely.

---

## 6. Core systems hierarchy

### Tier 0 — Trust infrastructure
These systems must exist before substantial content work:

- time model;
- stable IDs;
- deterministic state transitions;
- activity ingestion;
- deduplication;
- reward ledger;
- save format and migrations;
- atomic persistence;
- recovery strategy;
- diagnostic logging;
- test harness.

### Tier 1 — Ambient progression

- Vitality/activity conversion;
- project queues;
- offline project progress;
- producer automation;
- bounded resource storage;
- world restoration stages;
- return summary.

### Tier 2 — Motivation and meaning

- discoveries;
- expeditions;
- region goals;
- ecosystem recovery;
- collection/provenance journal;
- light narrative;
- achievement milestones.

### Tier 3 — Optional depth

- 3D Visit World mode;
- cosmetic customization;
- deeper building placement;
- route exploration;
- optional optimization.

Tier 3 may not destabilize Tier 0 or Tier 1.

---

## 7. MVP region strategy

The MVP should ship one region that demonstrates the complete product thesis. A placeholder region name may be used during development, but its content architecture must support later regions without hard-coding region-specific rules into the domain.

A complete region should include:

- a clear degraded starting state;
- at least 5–7 major restoration landmarks or project chains;
- several visible environmental stages;
- at least one settlement or hub;
- at least one water/ecology restoration chain;
- producer/infrastructure progression;
- several discoveries;
- at least one expedition chain;
- a final region-level transformation milestone;
- post-completion evergreen progress that does not immediately force Region 2.

The first region is not a tutorial. It is the proof that the whole product loop works.

---

## 8. Attention-budget requirements

The game must be designed against explicit interaction budgets.

### Glance session
Target: 5–15 seconds.

Player should be able to:

- understand the biggest change since last visit;
- see current project/progress;
- identify whether attention is required;
- leave without losing value.

### Daily check-in
Target: 20–60 seconds.

Player should be able to:

- review a concise offline summary;
- choose or confirm the next priority;
- resolve at most a small number of blocked decisions;
- close the app.

### Management session
Target: 2–5 minutes.

Player may:

- reorder projects;
- inspect production;
- choose expedition objectives;
- alter automation rules;
- review discoveries.

### World visit
Target: optional 5–20+ minutes.

This can be visually rich and interactive, but must never become required to preserve baseline progress.

---

## 9. Progression philosophy

Progression must have multiple timescales:

- **minutes/hours:** project segments, producer ticks, discoveries;
- **days:** landmark restorations, expedition outcomes, settlement improvements;
- **weeks:** region transformation, ecosystem recovery, substantial visual change;
- **months:** collection completion, advanced upgrades, mastery, future regions.

The system must avoid both extremes:

- instant gratification so frequent that progress becomes meaningless;
- long opaque timers that make activity feel disconnected from reward.

Every substantial real-world effort should produce understandable movement somewhere in the system.

---

## 10. Inactivity and return design

Inactivity is a normal user state, not a failure condition.

Required behavior:

- restored world state remains restored;
- completed projects remain complete;
- inventory remains intact;
- no catastrophic decay;
- no shame copy;
- no irreversible missed-day rewards that create quitting pressure;
- return summaries should welcome the player back and surface the next useful action.

Allowed gentle consequences:

- streak-like momentum may pause;
- time-limited optional bonuses may expire;
- automation may hit storage caps;
- new progress may wait for activity.

The design objective is **re-entry**, not punishment.

---

## 11. Engineering architecture objective

Adopt a layered architecture with a deterministic core:

### Domain
Pure C#. Owns rules, entities, value objects, simulation, progression, ledgers, state transitions, and validation. No Unity dependencies.

### Application
Use cases and orchestration: ingest activity, advance simulation, start project, reorder queue, collect discovery, load/save, reconcile platform state.

### Infrastructure
Persistence, clock, platform activity adapters, native health integrations, diagnostics, file system, optional future backend.

### Presentation
Unity scenes, UI, 3D world, animation, audio, haptics, accessibility bindings.

### Platform bridges
Android/iOS integrations isolated behind narrow interfaces and tested with fixtures.

The central rule is: **presentation is not authoritative state**.

---

## 12. Data integrity objective

The project must treat activity and reward processing similarly to a financial ledger.

Every externally sourced activity interval or aggregate must have:

- source identity where available;
- normalized time range;
- stable deduplication identity or deterministic fingerprint;
- processing status;
- credited amount;
- reconciliation metadata.

Reward application must be idempotent. Reprocessing the same source data cannot create additional reward.

Save mutations that span activity crediting and world progress must either be committed atomically or be reconstructable after interruption.

---

## 13. Performance objective

The ordinary experience should be cheap enough that users do not perceive the game as a battery-heavy tracking app.

The project therefore separates:

- low-cost background reconciliation;
- low-cost menu/summary UI;
- optional higher-cost 3D world presentation.

The 3D mode must have quality scaling and may not run unnecessarily while the user is in lightweight screens.

Specific budgets live in `PERFORMANCE_BUDGETS.md` and become release gates rather than advisory targets.

---

## 14. Privacy objective

The application should retain only the minimum health/activity information required to prove and reconcile game progress.

Principles:

- request the minimum platform permissions;
- explain why each permission exists;
- do not require precise location for the core loop;
- avoid storing raw health records when normalized aggregates/fingerprints are sufficient;
- support data deletion;
- never silently upload health data;
- future cloud sync must keep raw health data out of the backend unless a separately reviewed requirement justifies it.

---

## 15. Quality model

Every major feature moves through explicit evidence states:

1. **SPECIFIED** — documented contract exists.
2. **IMPLEMENTED** — production code exists.
3. **AUTOMATED VERIFIED** — relevant automated tests pass.
4. **RUNTIME VERIFIED** — verified in the Unity runtime where required.
5. **DEVICE VERIFIED** — verified on representative physical device(s) where platform behavior matters.
6. **RELEASE QUALIFIED** — all applicable gates pass with no Critical/High blockers.

Documentation must never label a feature “done” merely because code exists.

---

## 16. Milestone program

### M0 — Documentation and contracts
Define product, architecture, state model, quality gates, and roadmap.

**Exit:** repository contains coherent implementation-ready specs with contradictions resolved.

### M1 — Deterministic core
Build IDs, clock model, domain state, activity/reward ledger, persistence, migrations, and pure tests.

**Exit:** domain can simulate days of activity and progress deterministically without Unity scenes.

### M2 — Activity trust pipeline
Implement fixtures, platform abstractions, deduplication, reconciliation, correction handling, and error recovery.

**Exit:** replay/duplicate/late/corrected data cannot double-credit.

### M3 — Ambient gameplay vertical loop
Implement Vitality, project queue, restoration stages, producers, offline advancement, and return summary.

**Exit:** a player can progress an end-to-end world state by feeding activity fixtures.

### M4 — First region content
Author one complete region, content schema, landmark chains, discoveries, expedition hooks, and world state mapping.

**Exit:** region can progress from damaged to substantially restored without placeholder-only systems.

### M5 — Mobile UX
Implement onboarding, Home, Projects, Region, Discoveries, Settings, accessibility, notifications, and failure states.

**Exit:** ordinary daily use requires under one minute and no hidden mandatory maintenance.

### M6 — Optional Visit World mode
Create the interactive region presentation, quality tiers, visual transitions, navigation, inspect interactions, and safe state binding.

**Exit:** visually compelling but completely subordinate to canonical domain state.

### M7 — Platform integration
Integrate supported health/activity sources, permission UX, lifecycle behavior, background/foreground reconciliation, and platform-specific persistence tests.

**Exit:** real activity reaches the same validated domain pipeline used by fixtures.

### M8 — Hardening
Red-team activity, saves, clock changes, restarts, low-memory, interrupted writes, corrupt snapshots, accessibility, and performance.

**Exit:** no unresolved Critical/High issues and recovery behavior is proven.

### M9 — Release qualification
Run clean-clone build, automated suite, device matrix, battery/performance checks, migration checks, privacy review, and final documentation reconciliation.

**Exit:** release candidate is supported by evidence, not assumptions.

---

## 17. Aggressive execution principles

The project should develop in **integrated campaigns**, not isolated microtasks.

A good development campaign should typically deliver a complete vertical concern: domain rules, persistence, presentation, tests, diagnostics, documentation, and migration effects together.

Rules:

- Prefer one substantial end-to-end campaign over ten disconnected TODOs.
- Do not build Region 2 while Region 1 is not release-qualified.
- Do not add speculative social/multiplayer/backend complexity before the single-player loop is proven.
- Do not accept mock-only success for platform integrations.
- Fix data integrity issues before visual polish.
- Fix player-blocking UX before adding content volume.
- Keep the repository buildable at campaign boundaries.
- Every campaign must leave durable evidence and an updated roadmap.

---

## 18. MVP exclusion list

Explicitly out of scope unless the master plan is revised:

- multiplayer;
- guilds;
- PvP;
- live-service event infrastructure;
- mandatory account system;
- social feed;
- competitive leaderboards;
- server-authoritative progression;
- combat-centric gameplay;
- gacha;
- ads as a core economy mechanic;
- multiple regions before the first region is proven;
- complex real-time base defense;
- required continuous GPS routes;
- wearable-specific gameplay that blocks phone-only users.

These may be revisited later, but they are currently distractions from validating the ambient-restoration thesis.

---

## 19. North-star success criteria

The MVP is strategically successful when all of the following are true:

1. A busy player can benefit without long sessions.
2. Real movement reliably and visibly changes the world.
3. The player understands what happened while away.
4. The world looks materially different after sustained activity.
5. Activity cannot be silently lost or double-counted under tested scenarios.
6. Inactivity does not create a quit-inducing punishment spiral.
7. The app remains useful offline.
8. Ordinary use has acceptable battery/performance behavior.
9. Optional world exploration adds delight without becoming mandatory.
10. The first region feels complete enough that adding Region 2 would be expansion rather than compensation for an unfinished core.

---

## 20. Immediate next campaign

**Status update (2026-08-25): M1 and the M2 trust pipeline are implemented and automated-verified on `main`; see ROADMAP exit criteria and README for evidence. The objective below is history — it is kept because it explains why the core looks the way it does. The immediate next milestone is M3 (ambient progression vertical slice with minimal UI).**

The foundation campaign proved that the game can:

1. receive normalized activity fixtures;
2. deduplicate them;
3. convert them to bounded progression exactly once;
4. advance deterministic project/world state;
5. save atomically;
6. reload and reproduce the same state;
7. survive duplicate/reordered/replayed/corrected/deleted inputs;
8. expose a simple diagnostic representation of the resulting world.

Once M3 makes progression visible, every later visual and gameplay feature can build on it safely.

Do not begin new campaigns without the `AGENTS.md` preflight (repository identity guard, single-writer lease); concurrent uncoordinated sessions previously damaged this repository.
