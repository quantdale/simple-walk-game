# Ambient Fitness Game — Master Implementation Plan

**Repository:** `quantdale/simple-walk-game`  
**Planning baseline:** `main` @ `563155b71250e2091b26fecc3db6a9f46850f67e`  
**Baseline tree:** `b44d1acdb7104de32c850579c3f216199c6d4fd9`  
**Baseline state:** one initial commit; only a minimal README existed. There is no legacy implementation to preserve.

## What this plan changes

The product is not a conventional game that rewards walking with currency and then expects the user to spend substantial time playing. The target user may exercise regularly while having little interest in active gaming sessions.

The redesigned loop is:

`real-world activity -> normalized activity ledger -> Vitality -> automated project allocation -> world changes -> optional decisions -> concise away report`

The app should feel closer to a **living fitness companion / ambient world simulator** than a mobile game demanding attention.

## Document map

- `00_PRODUCT_THESIS.md` — target user, product rules, loops, metrics, anti-patterns.
- `01_SYSTEM_ARCHITECTURE.md` — stack, modules, boundaries, data flow, local-first architecture.
- `02_ACTIVITY_INGESTION.md` — HealthKit, Health Connect, reconciliation, deduplication, permissions, background behavior.
- `03_PROGRESSION_WORLD.md` — Vitality economy, projects, allocation automation, expeditions, world event model.
- `04_UX_SESSION_DESIGN.md` — Today/World/Projects/Journey UX, 30-second check-in, away report, notifications.
- `05_DATA_PRIVACY_SYNC_TRUST.md` — SQLite schema, privacy, sync, provenance, trust/anti-cheat, export/delete.
- `06_WORLD_PRESENTATION.md` — 2.5D world first, optional Visit World, later 3D architecture and performance constraints.
- `07_TESTING_QUALITY.md` — deterministic fixtures, tests, E2E, physical-device qualification, performance/accessibility gates.
- `08_IMPLEMENTATION_ROADMAP.md` — milestones, dependencies, acceptance criteria, campaign sizing.
- `09_AGENT_EXECUTION.md` — autonomous-agent workflow, waypoints, commit discipline, definition of done.
- `10_SOURCE_REGISTER.md` — official primary references used to anchor the technical choices.
- `11_DECISIONS.md` — initial ADR-style decisions and deferred decisions.

## Priority order

1. Bootstrap a production-quality mobile foundation.
2. Build deterministic activity ingestion with fake providers before native providers.
3. Build an idempotent activity-to-progression ledger.
4. Deliver a complete 30–90 second Today check-in loop.
5. Add automated project allocation and persistent world simulation.
6. Add world visualization and long-term progression depth.
7. Add optional account/sync, export/delete, notification intelligence.
8. Add optional interactive Visit World only after the passive loop is already satisfying.
9. Release-harden on real devices.

## Definition of product success

A user can install the app, grant a minimal set of health permissions, live normally for several days, reopen the app, and see a **correct, emotionally legible, meaningful transformation of their world** without having needed to keep the app open or manually grind.
