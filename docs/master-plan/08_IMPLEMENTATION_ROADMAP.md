# 08 — Implementation Roadmap

This roadmap is intentionally organized as **large coherent campaigns** suitable for long autonomous coding sessions. Each milestone must leave the repository buildable and demonstrably more complete.

## Milestone 0 — Production foundation

### Objective

Create a production-grade mobile app skeleton with local persistence, deterministic test infrastructure, navigation, CI, and native-module capability.

### Scope

- Bootstrap Expo SDK 57 + TypeScript strict.
- Expo Router with Today / World / Projects / Journey tabs.
- Development-build configuration for Android/iOS.
- ESLint/formatting/typecheck.
- Jest + React Native Testing Library.
- Maestro baseline.
- Expo SQLite setup, WAL, migration runner.
- Theme/design tokens and accessibility baseline.
- Error boundary and structured logger with redaction.
- Fake clock/RNG/activity provider infrastructure.
- Debug diagnostics route behind non-production flag.
- GitHub Actions light CI.
- Durable `docs/STATE.md` / milestone waypoint tracking.

### Exit criteria

- Android emulator installs/launches.
- iOS configuration is structurally valid/buildable through supported environment.
- four-tab shell works;
- SQLite migration executes;
- tests/typecheck/lint pass;
- Maestro can launch and navigate;
- fake activity provider is injectable.

---

## Milestone 1 — Activity ledger vertical slice

### Objective

Prove that activity can be represented, reconciled, deduplicated, persisted, and displayed independently of native providers.

### Scope

- ActivityProvider interface.
- Fake provider with fixture timelines.
- activity provider/cursor/ledger schema.
- canonicalization/dedupe logic.
- reconciliation transaction.
- Today activity summary read model.
- diagnostics for last reconciliation.
- tests for duplicate/late/delete/timezone scenarios.
- fixture import UI in debug mode.

### Exit criteria

- a 7-day fake timeline can be replayed twice with identical ledger/state;
- Today displays deterministic activity totals;
- process death/restart retains state;
- cursor advances only after successful transaction.

---

## Milestone 2 — Vitality, projects and first world change

### Objective

Complete the first end-to-end game loop: activity causes automatic, persistent restoration.

### Scope

- balance/formula versioning;
- VitalityGrant schema/engine;
- allocation policies;
- project definitions + schema validation;
- project engine and progress events;
- world events + region reducer/snapshot;
- first Ashfall Basin project chain;
- away-report generator;
- Today hero world-change card;
- Journey entries.

### Exit criteria

Fixture activity -> grants -> allocation -> project completion -> world event -> away report works in one deterministic transaction flow. Replaying inputs creates no duplicate rewards or completions.

---

## Milestone 3 — Native Android Health Connect

### Objective

Make Android real-world activity drive the complete loop.

### Scope

- Expo native module/Kotlin Health Connect client;
- availability and permission UX;
- minimal step/distance/workout permissions;
- foreground reconciliation;
- background-read support where platform feature/permission allows;
- WorkManager/background trigger integration as optimization;
- data origin handling;
- native errors mapped to typed JS results;
- real-device qualification checklist;
- fake provider retained for automated tests.

### Exit criteria

A physical Android device can walk/record activity, later open the app, and see exactly-once world progress. Revoking/restoring permission is safe.

---

## Milestone 4 — Native iOS HealthKit

### Objective

Achieve equivalent iOS activity integration.

### Scope

- Expo native module/Swift HealthKit store;
- authorization UX;
- anchored queries;
- aggregate statistics where appropriate;
- observer query registration;
- background delivery entitlement/configuration;
- startup reconciliation;
- typed DTO/errors;
- physical-device qualification plan/evidence.

### Exit criteria

A physical iPhone can record HealthKit activity, leave the app unused, return, and reconcile correct progression. Observer/background behavior is validated on device rather than assumed from simulator.

---

## Milestone 5 — 30–90 second product-quality loop

### Objective

Make the product delightful for non-gamers with minimal screen time.

### Scope

- polished Today screen;
- away report with digesting and before/after visuals;
- Projects queue and allocation presets;
- first meaningful decisions;
- graceful partial permission/offline states;
- dynamic type/screen reader/reduced motion;
- notification permission contextual prompt;
- local project-completion/weekly digest notifications;
- Journey grouping;
- product copy pass.

### Exit criteria

A returning user can understand a week of progress and make one useful choice in <=90 seconds without needing to collect rewards or clear modal debt.

---

## Milestone 6 — Persistent world, expeditions and automation depth

### Objective

Provide months of passive progression depth.

### Scope

- multi-dimensional Ashfall Basin region state;
- larger project graph;
- expedition engine;
- discoveries/lore;
- Momentum rolling consistency;
- advanced allocation presets/custom priorities;
- blocked/pending decision management;
- headless persona simulator;
- 12/52-week balance simulations;
- content graph validation.

### Exit criteria

Multiple personas can simulate 12 weeks without dead ends, runaway economy, excessive decision spam, or rapid content exhaustion.

---

## Milestone 7 — World visualization 2.5D

### Objective

Make restoration visually compelling without requiring a game-engine session.

### Scope

- renderer technology spike and ADR;
- region map/diorama;
- staged ecology/water/infrastructure/settlement layers;
- project hotspots;
- fog/exploration;
- milestone transitions;
- before/after compare;
- lazy asset loading;
- memory/frame profiling;
- reduced-motion equivalent.

### Exit criteria

World screen visibly and performantly reflects canonical state across target devices and historical milestone replay works for selected events.

---

## Milestone 8 — Privacy, export/delete and optional account sync

### Objective

Prepare for durable personal use and eventual public release.

### Scope

- user export;
- reset/delete flows;
- privacy settings and permission management;
- optional Supabase auth;
- local outbox sync architecture;
- settings/world event synchronization first;
- conflict handling;
- multi-device activity-award protection design before enabling overlapping reward processing;
- privacy policy/data disclosure inventory.

### Exit criteria

Core app remains fully functional signed out/offline. Account can be added/removed without corrupting local progress. Export/delete are verified.

---

## Milestone 9 — Optional Visit World 3D

### Objective

Add a longer optional emotional-payoff experience without changing the passive core contract.

### Scope

- current ecosystem technology spike;
- ADR choosing 3D stack;
- lazy-loaded region scene;
- third-person or free-explore controller;
- landmarks/world state integration;
- wildlife/NPC ambient behaviors;
- photo mode;
- optional decoration;
- graphics tiers, culling/LOD, lifecycle disposal;
- performance qualification.

### Exit criteria

3D mode is stable, optional, lazy, performant, and removing/ignoring it does not block any progression.

---

## Milestone 10 — Release hardening

### Objective

Qualify production behavior across long-lived state and real devices.

### Scope

- migration matrix;
- 30/90/365-day backlog stress;
- process-kill/reboot/background tests;
- permission mutation tests;
- device performance matrix;
- accessibility certification pass;
- notification quiet-hours/digest verification;
- store metadata/privacy declarations;
- crash/error monitoring decision;
- dependency/security/license audit;
- backup/recovery exercises;
- Android/iOS release builds;
- final regression suite.

### Exit criteria

All Critical/High blockers closed; release evidence recorded; no known reward-duplication, data-loss, permission-crash, or privacy defects.

## Critical path

`M0 -> M1 -> M2 -> M3/M4 -> M5 -> M6 -> M7 -> M8 -> M9 -> M10`

Android and iOS provider work can partially overlap after M2, but the domain contract must be stable first.

## What not to parallelize too early

- two different progression engines;
- multiple competing DB abstractions;
- 3D world before M2/M5 proof;
- cloud sync before local event identity is solid;
- social/leaderboards before multi-device trust/dedupe;
- elaborate procedural content before the authored Ashfall Basin loop works.

## Recommended first overnight campaign

Implement **Milestone 0 + the majority of Milestone 1** in one long session:

- bootstrap app;
- local DB/migrations;
- four-tab shell;
- fake ActivityProvider;
- canonical ledger;
- reconciliation transaction;
- deterministic clock/fixtures;
- Today activity summary;
- debug fixture injection;
- tests/CI/Maestro baseline.

This produces substantial infrastructure while directly serving the most important product risk: reliable passive activity ingestion.
