# 07 — Testing, Quality and Release Gates

## 1. Quality philosophy

The hardest bugs are not visual—they are duplicated rewards, lost activity, non-deterministic catch-up, permission edge cases, and corrupted long-lived state. Test those contracts first.

## 2. Test layers

### Pure domain tests

Cover:

- activity eligibility;
- Vitality formulas by version;
- allocation routing;
- project prerequisites/progress/completion;
- decision consequences;
- expedition progress;
- world event reduction;
- Momentum;
- away-report summarization;
- seeded randomness.

### Property/fuzz tests

Useful properties:

- processing same batch twice does not change final world after first application;
- total allocated Vitality never exceeds grants + prior banked balance;
- project completion occurs at most once;
- event sequence strictly increases;
- replay from snapshot produces same state hash;
- no negative balances unless explicitly modeled;
- content graph references remain valid.

### Database integration tests

Run real SQLite migrations and repositories:

- fresh install;
- migrate from every supported schema fixture;
- transaction rollback injection;
- duplicate unique-key paths;
- outbox behavior;
- state rebuild.

### Provider contract tests

All providers run the same contract suite using fixtures:

- availability;
- permission states;
- pagination;
- duplicate/change delivery;
- cursor persistence;
- error normalization.

Native provider platform tests validate DTO conversion separately.

### UI/component tests

Test key states:

- onboarding;
- permission partial/denied;
- Today no activity;
- away report;
- project completion;
- decision pending;
- offline;
- provider delayed;
- accessibility labels.

### E2E

Maestro critical journeys using fake activity injection:

1. fresh install -> onboarding -> fake permission -> first activity -> first world change;
2. 7-day away fixture -> launch -> catch-up -> report;
3. change allocation -> activity -> correct project progresses;
4. project completes -> world map changes -> Journey entry;
5. permission revoked -> safe degraded state -> restored;
6. decision resolved -> downstream project unlock;
7. DB migration upgrade fixture;
8. export/delete/reset flows.

## 3. Deterministic test mode

Add a non-production test harness with:

- fixture provider;
- controllable clock;
- seeded RNG;
- persona presets;
- reset/import DB fixture;
- direct navigation test hooks;
- semantic test IDs;
- ability to simulate foreground/background-trigger events.

Test hooks must be excluded or securely disabled in production builds.

## 4. State hashes

Create a canonical stable JSON/state hash for core progression state. Simulation tests can assert final hashes for fixture timelines.

Hash excludes volatile fields like diagnostics timestamps.

## 5. Native health qualification

Automated fake-provider E2E is necessary but not sufficient.

Android physical-device matrix:

- Health Connect available/integrated OS variants;
- permission grant/partial/deny/revoke;
- foreground reads;
- background-read permission where supported;
- delayed updates;
- reboot/app process death;
- two contributing source apps if practical.

Apple physical-device matrix:

- authorization combinations;
- anchored reconciliation;
- observer background delivery;
- locked-device behavior;
- process termination/relaunch;
- Health app edits/deletions;
- Apple Watch-contributed workout if available.

Record device/OS/build identifiers in qualification evidence.

## 6. Performance tests

Scenarios:

- cold start no backlog;
- 7-day backlog;
- 30-day backlog;
- 365-day imported history bounded by product policy;
- 50k ledger rows;
- 10k world events;
- region map maximum content;
- Journey long list;
- low-memory resume;
- 30-minute optional Visit World session when that mode exists.

Track p50/p95 where feasible.

## 7. Accessibility gate

Before milestone completion:

- screen-reader traversal on primary screens;
- dynamic text at large sizes;
- reduced-motion behavior;
- touch target size;
- contrast audit;
- no meaning encoded by color alone;
- key flows operable without complex gestures.

## 8. CI

Every PR/campaign should run:

- clean dependency install with lockfile;
- TypeScript typecheck;
- lint;
- unit/integration tests;
- content-schema validation;
- Expo doctor/config validation;
- production JS bundle/build smoke as practical;
- Android build at defined checkpoints;
- Maestro E2E in dedicated CI/nightly path once emulator infrastructure is stable.

Later:

- iOS build through EAS/macOS runner;
- performance smoke;
- visual snapshots;
- dependency/security/license checks.

## 9. Release blockers

Critical/High examples:

- duplicate Vitality grants;
- lost eligible activity across normal reconciling;
- migration data loss;
- world state non-determinism;
- permission flow crash;
- app unusable offline;
- health data leaked in logs/analytics;
- broken export/delete;
- severe accessibility blocker on onboarding;
- background design requiring impossible exact scheduling;
- production test/debug activity injection enabled.

## 10. Definition of done for a feature

A feature is not done when the happy-path UI exists. It requires:

- domain behavior;
- persistence/migration if needed;
- error states;
- fake-provider/test support;
- unit/integration tests;
- E2E for user-critical path;
- accessibility semantics;
- diagnostics where operationally important;
- updated docs/ADR if architecture changed.
