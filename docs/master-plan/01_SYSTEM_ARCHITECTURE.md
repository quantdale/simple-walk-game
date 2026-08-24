# 01 — System Architecture

## 1. Recommended baseline stack

Use a production Expo development-build workflow rather than Expo Go as the primary environment because HealthKit and Health Connect require native integration.

- **Expo SDK 57**
- **React Native 0.86**
- **React 19.2.x**
- **TypeScript strict mode**
- **Expo Router** stable navigation APIs
- **Hermes**
- **Expo SQLite** with WAL as local persistence
- **Native Expo Modules** for HealthKit (Swift) and Health Connect (Kotlin)
- **expo-notifications** for local/push notification plumbing
- **expo-background-task / platform-native callbacks** only as opportunistic triggers
- **Maestro** for device E2E
- **Jest + React Native Testing Library** for unit/component/integration tests
- Optional later: Supabase Auth/Postgres/Edge Functions for account and cloud sync

Do not adopt experimental router stacks or unrelated infrastructure in Milestone 0.

## 2. Architectural principle

The most important boundary is:

`native health APIs -> activity provider -> normalization/reconciliation -> local activity ledger -> pure progression engine -> persisted world event log -> derived read models -> UI`

The progression engine must not import React Native, Expo, Supabase, HealthKit, Health Connect, notification APIs, or rendering code.

## 3. Proposed repository structure

```text
app/
  (tabs)/
    today.tsx
    world.tsx
    projects.tsx
    journey.tsx
  onboarding/
  permissions/
  settings/
  project/[id].tsx
  decision/[id].tsx
  expedition/[id].tsx
src/
  domain/
    activity/
    economy/
    projects/
    world/
    expeditions/
    decisions/
    progression/
  application/
    reconcile/
    commands/
    queries/
    summaries/
  data/
    db/
      migrations/
      schema/
      repositories/
    sync/
  native/
    health/
      ActivityProvider.ts
      fake/
      ios/
      android/
  features/
    today/
    world/
    projects/
    journey/
    onboarding/
    settings/
  presentation/
    components/
    theme/
    accessibility/
  notifications/
  telemetry/
  testing/
    fixtures/
    clocks/
    builders/
modules/
  walk-health/
    ios/
    android/
    src/
assets/
docs/
```

Exact paths may evolve, but boundaries should remain recognizable.

## 4. Domain modules

### Activity

Owns normalized activity records and provenance. It does not know how HealthKit/Health Connect query APIs work.

Core types:

```ts
type ActivityKind =
  | 'steps'
  | 'distance_walk_run'
  | 'workout_duration'
  | 'elevation_gain'
  | 'wheelchair_pushes';

type ActivityProvenance =
  | 'health_store'
  | 'device_sensor'
  | 'manual'
  | 'fixture';

interface ActivityLedgerEntry {
  id: string;
  provider: 'healthkit' | 'health_connect' | 'pedometer' | 'fixture';
  providerRecordId?: string;
  kind: ActivityKind;
  startAt: string;
  endAt: string;
  quantity: number;
  unit: string;
  provenance: ActivityProvenance;
  sourceAppId?: string;
  sourceDeviceId?: string;
  fingerprint: string;
  observedAt: string;
}
```

### Economy

Pure conversion from eligible normalized activity to `VitalityGrant` and optional affinity grants.

The conversion function must accept a formula version and be replayable.

### Projects

Project graph, prerequisites, progress, completion, unlocks and effects.

### World

Append-only `WorldEvent`s plus materialized region state. World state should be reconstructible from genesis + events/snapshots.

### Decisions

Pending/resolved player decisions and outcome effects.

### Expeditions

Activity-driven long-running discovery state.

## 5. Application layer

Application services coordinate domain logic and persistence inside explicit transactions.

Key use cases:

- `ReconcileActivityUseCase`
- `ProcessUnrewardedActivityUseCase`
- `AllocateVitalityUseCase`
- `AdvanceProjectsUseCase`
- `ResolveDecisionUseCase`
- `AdvanceExpeditionsUseCase`
- `BuildAwayReportUseCase`
- `RebuildReadModelsUseCase`

The foreground catch-up pipeline should be callable as one idempotent orchestration:

```text
health reconcile
  -> canonicalize/dedupe
  -> create missing grants
  -> apply allocation policy
  -> advance projects/expeditions
  -> emit world events
  -> refresh derived summaries
  -> schedule/cancel relevant notifications
```

## 6. Determinism

Inputs to simulation:

- normalized ledger entries;
- balance/config version;
- prior persisted state;
- user commands/decisions;
- deterministic clock where current time is genuinely needed.

Randomness must use explicit seeded RNG and persist seeds/outcomes when events are committed. Do not call `Math.random()` inside domain progression logic.

## 7. Local-first source of truth

SQLite is authoritative for core gameplay.

Reasons:

- activity can arrive while offline;
- progression should work without an account;
- foreground catch-up can be transactional;
- deterministic fixtures and migrations are testable;
- cloud sync can operate through an outbox rather than being embedded into every domain write.

Recommended database modes:

- WAL journal mode;
- foreign keys ON;
- explicit migrations;
- transactions around award/allocation/completion pipelines;
- indices on time ranges, provider IDs, unprocessed flags, project status, world event sequence.

## 8. Cloud architecture — deferred but anticipated

Cloud sync is not part of the critical path for the first playable product.

When introduced:

- Supabase Auth for optional identity;
- Postgres tables mirror only sync-worthy records;
- local outbox tracks unsynced mutations;
- server assigns conflict metadata/versions;
- health raw data remains minimized;
- no requirement for network to open or progress the world;
- server-side validation becomes relevant only for social/competitive features.

## 9. Read models

The UI should not repeatedly replay the full event log. Maintain materialized views/read tables such as:

- current activity day/week summary;
- current Vitality balance/allocation;
- active project cards;
- region restoration stage;
- pending decisions;
- expedition status;
- latest away-report checkpoint;
- journey timeline.

Read models can be rebuilt from canonical state and events if corrupted.

## 10. Versioning

Persist versions for:

- DB schema;
- activity normalization rules;
- Vitality conversion formula;
- project/content definition pack;
- world-event schema;
- save/sync protocol.

Never silently reinterpret historical rewards under a new formula. New formulas apply prospectively unless an explicit migration is designed and tested.

## 11. Configuration/content

World/project definitions should be data-driven, schema-validated at build/test time, and stable-ID based.

A project definition should not contain arbitrary executable JS. Prefer typed declarative effects that the domain engine interprets.

## 12. Error strategy

Errors must be classified:

- recoverable permission state;
- health provider unavailable;
- query/reconciliation transient failure;
- DB migration failure;
- invariant violation;
- content-definition error;
- sync conflict;
- rendering failure.

Activity ingestion errors must not corrupt already-processed progression. Store provider checkpoints only after a successful transaction.

## 13. Performance budget

The passive loop is primarily data processing, not rendering. Set early budgets:

- cold app usable shell: target <2.5 s on reference mid-tier Android after production build;
- foreground reconciliation for a normal 7-day backlog: target <500 ms p95 after provider results are available;
- 90-day backlog: target <2 s with progress UI if needed;
- no unbounded O(days × all events) scans on every launch;
- Today interactions: 60 FPS, no JS long tasks >50 ms during normal navigation;
- SQLite writes grouped in transactions;
- world visualization loads independently from catch-up completion where possible.

## 14. Observability

Local diagnostics screen in non-production/debug builds:

- provider availability/permission status;
- last reconciliation cursor/time;
- raw normalized entries added on last sync;
- duplicate count;
- grants created;
- projects advanced;
- world events emitted;
- catch-up duration;
- DB schema version;
- background trigger history;
- notification schedule state.

Diagnostics should enable export of a sanitized JSON bundle that excludes sensitive health records unless explicitly selected.
