# 09 — Autonomous Agent Execution Plan

## 1. Purpose

This document converts the roadmap into a durable execution protocol for long/overnight coding-agent sessions.

## 2. Branch discipline

- `main` should remain releasable/buildable.
- One large campaign may use one feature branch.
- Avoid multiple agents writing the same files simultaneously unless explicitly coordinated.
- Commit logical checkpoints during long sessions.
- Push completed checkpoints so external review can inspect progress.

## 3. Durable state file

As soon as implementation starts, create `docs/STATE.md` containing:

```md
# Project state

Current milestone: M0
Current campaign: <name>
Starting SHA: <sha>
Latest certified SHA: <sha>

## Completed
- ...

## In progress
- ...

## Blocked/deferred
- ...

## Required next verification
- ...

## Architecture decisions
- ADR-...
```

Update only when meaningful state changes.

## 4. Campaign template

Every autonomous campaign should internally answer:

### Baseline

- What is current SHA/branch?
- Is working tree clean?
- What changed recently?
- Which milestone exit criteria are already satisfied?
- Which existing tests represent current contracts?

### Scope

- What coherent capability will be fully advanced?
- Which files/modules will likely change?
- Which migrations are required?
- Which tests/E2E fixtures must accompany it?

### Validation

- typecheck;
- lint;
- unit/integration;
- build;
- platform-specific tests;
- E2E if relevant;
- state/content validation.

### Completion report

- starting/final SHA;
- branch;
- commits;
- implementation summary;
- migrations;
- tests and exact outcomes;
- screenshots/artifacts if useful;
- remaining blockers;
- next highest-impact campaign.

## 5. Definition of a good overnight campaign

Good:

- complete fake-provider-to-ledger vertical slice with DB, UI, tests, diagnostics, CI.
- complete Vitality/project/world-event engine and first content pack.
- complete Android native provider including permissions, reconciliation, tests, and device qualification hooks.

Bad:

- add three screens with placeholder state;
- add a library without integrating it;
- partially scaffold many milestones;
- build 3D assets while activity reconciliation is incomplete.

## 6. Agent boundaries

Suggested workstreams after foundation:

- **Domain agent:** progression/economy/projects/world simulation.
- **Native Android agent:** Health Connect module.
- **Native iOS agent:** HealthKit module.
- **UX agent:** Today/Projects/Journey/accessibility.
- **World presentation agent:** map/diorama and later 3D.
- **QA agent:** fixtures, Maestro, migration/performance/device matrices.

Parallelization begins only after interfaces are committed and write boundaries are explicit.

## 7. Mandatory re-inspection

Before each campaign, agents must inspect actual current code and recent commits. This master plan is directional; do not implement a task that has already been completed differently and correctly.

## 8. ADR policy

Create `docs/adr/NNNN-title.md` when changing a high-impact decision, including:

- context;
- decision;
- alternatives;
- consequences;
- migration/rollback implications.

ADR candidates:

- database abstraction/ORM;
- native health module implementation strategy;
- health-record vs aggregate ingestion strategy;
- activity correction semantics;
- 2.5D renderer;
- cloud sync conflict model;
- 3D runtime.

## 9. Stop conditions

Stop expanding scope and stabilize if any of these appear:

- reward duplication;
- migration corruption;
- nondeterministic replay;
- health permission crash;
- native module lifecycle instability;
- app cannot start offline;
- tests are being disabled to ship changes;
- implementation violates zero-required-screen-time contract.

## 10. First campaign prompt skeleton

An autonomous agent can be given:

> Inspect the repository and implement the next coherent portion of Milestone 0 and Milestone 1 from `docs/master-plan/08_IMPLEMENTATION_ROADMAP.md`. Build a production Expo SDK 57 foundation, four-tab shell, SQLite migrations, deterministic test harness, fake ActivityProvider, canonical activity ledger, idempotent reconciliation, Today activity summary, diagnostics, CI and Maestro smoke coverage. Preserve the architectural boundaries in the master plan. Do not implement native HealthKit/Health Connect yet unless M0/M1 exit criteria are already satisfied. Validate typecheck, lint, tests, build and relevant E2E. Commit/push cohesive checkpoints and update `docs/STATE.md` with exact status. If repository state has advanced beyond the plan, adapt to actual code rather than duplicating work.
