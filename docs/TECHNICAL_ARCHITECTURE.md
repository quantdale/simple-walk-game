# Technical Architecture

## 1. Architectural objective

The architecture must support a low-attention, offline-first mobile game whose canonical progression can be tested and simulated without a rendering engine.

The core architectural rule is:

> **Game state belongs to deterministic domain code. Unity presents that state; it does not own it.**

This protects the project from scene-script coupling, makes offline simulation trustworthy, allows headless tests, and makes activity/reward correctness auditable.

---

## 2. Proposed stack

Baseline implementation direction:

- **Unity 6 LTS** for mobile runtime and optional real-time 3D world presentation.
- **C#** across the domain/application layers.
- **Pure C# domain assemblies** with no `UnityEngine` reference.
- Platform-specific native bridges/adapters for Android/iOS activity APIs.
- Local durable persistence through an abstracted repository/store.
- Automated pure-C# tests plus Unity EditMode/PlayMode tests where appropriate.
- CI that can validate the domain and repository structure on a clean clone.

Exact package/plugin versions are implementation-time decisions and must be recorded in `DECISIONS.md`.

---

## 3. Layering

### 3.1 Domain

Owns:

- IDs and value objects;
- canonical game state;
- activity/reward ledger rules;
- project state machines;
- resource accounting;
- producer simulation;
- expedition resolution;
- discovery eligibility;
- region/restoration state;
- deterministic random seeds;
- validation and invariants;
- time-dependent simulation using injected time values.

May depend on:

- .NET base libraries approved for the target profile;
- other pure domain modules.

Must not depend on:

- UnityEngine;
- MonoBehaviour;
- scene objects;
- PlayerPrefs;
- platform health APIs;
- HTTP clients;
- file-system implementation details.

### 3.2 Application

Owns use-case orchestration:

- ingest activity batch;
- reconcile source;
- advance offline simulation;
- start/reorder/pause project;
- start/resolve expedition;
- construct return summary;
- load/save game;
- perform migration;
- expose read models to presentation.

Application code coordinates domain operations and ports, but does not contain platform UI behavior.

### 3.3 Infrastructure

Owns adapters for:

- persistence;
- file system;
- platform clock;
- logging/diagnostics;
- Health Connect/HealthKit integration;
- notifications;
- haptics/audio settings persistence;
- future cloud sync if introduced.

### 3.4 Presentation

Owns:

- Unity scenes;
- UI controllers/presenters/view models;
- 3D state bindings;
- animation;
- sound;
- particles;
- accessibility presentation;
- input;
- quality scaling;
- transition/loading UX.

Presentation translates canonical state into visuals. It may request application use cases but may not mutate domain state ad hoc.

---

## 4. Suggested repository structure

```text
/
  README.md
  docs/
  src/
    WalkGame.Domain/
      Activity/
      Economy/
      Projects/
      Regions/
      Expeditions/
      Discoveries/
      Simulation/
      Time/
      Common/
    WalkGame.Application/
      Activity/
      Progression/
      Persistence/
      Summaries/
      Diagnostics/
    WalkGame.Infrastructure/
      Persistence/
      Platform/
      Diagnostics/
    WalkGame.Unity/
      Assets/
        Scripts/
          Presentation/
          Composition/
          Platform/
        Scenes/
        Prefabs/
        Art/
        Audio/
  tests/
    WalkGame.Domain.Tests/
    WalkGame.Application.Tests/
    fixtures/
  tools/
    simulation/
    validation/
```

The actual Unity project layout may require adaptation, but dependency direction must remain explicit.

---

## 5. Dependency rule

Allowed dependency flow:

`Presentation → Application → Domain`

`Infrastructure → Application/Domain ports`

Composition root wires implementations.

Forbidden:

- Domain → Unity;
- Domain → platform adapter;
- Domain → concrete persistence;
- platform bridge directly mutating world state;
- scene object directly crediting Vitality;
- UI callback editing save structures.

---

## 6. Canonical aggregate/state strategy

Avoid one uncontrolled “GameManager” object.

Canonical state should be decomposed into explicit aggregates/modules, for example:

- `PlayerProgressState`;
- `ActivityLedgerState`;
- `ResourceState`;
- `RegionState`;
- `ProjectQueueState`;
- `ProducerState`;
- `ExpeditionState`;
- `DiscoveryState`;
- `SettingsState`;
- `SaveMetadata`.

The application layer may compose these into a save snapshot, but mutation should occur through bounded domain operations.

---

## 7. Stable IDs

All content/state references must use stable IDs rather than scene names or array indices.

Examples:

- region IDs;
- landmark IDs;
- project IDs;
- producer IDs;
- discovery IDs;
- expedition IDs;
- reward transaction IDs.

Requirements:

- IDs are immutable after content ships unless migrated;
- renaming display text does not alter identity;
- save data stores IDs, not object references;
- validation detects duplicate or missing content IDs;
- deleted/retired IDs receive migration/tombstone handling.

---

## 8. Content definitions vs runtime state

Separate immutable content definitions from player runtime state.

### Definition

Contains authoring data:

- costs;
- prerequisites;
- labels;
- presentation references;
- reward tables;
- content version.

### Runtime state

Contains player-specific values:

- progress;
- completion;
- timestamps/checkpoints;
- balances;
- discovered flags;
- queue order.

Never duplicate mutable runtime truth into content assets.

---

## 9. Persistence contract

Persistence must provide:

- versioned schema;
- atomic commit behavior or recoverable journal;
- backup/recovery strategy;
- integrity metadata/checksum where practical;
- explicit migrations;
- durable transaction identity for idempotent operations;
- clear load failure categories;
- testable storage abstraction.

### Recommended initial pattern

For an early vertical slice, a versioned snapshot plus small journal/ledger can be simpler than a large database. If SQLite is later chosen, the domain should remain unaware of it.

The architecture must allow the persistence implementation to evolve without changing domain semantics.

---

## 10. Save lifecycle

Conceptual flow:

```text
Boot
  → locate save
  → validate envelope/integrity
  → recover latest valid snapshot if needed
  → migrate sequentially
  → validate domain invariants
  → reconcile activity
  → advance offline systems
  → atomically persist resulting state
  → present return summary
```

Presentation should not appear to complete a reward before the corresponding state is durable.

---

## 11. Migration strategy

Every persisted schema change must answer:

- source version;
- target version;
- deterministic migration function;
- invariant validation after migration;
- rollback/recovery behavior;
- tests with representative old fixtures.

Rules:

- migrations are sequential;
- migration does not depend on scene objects;
- migrations are idempotent or protected from repeated partial execution;
- previously shipped save fixtures become permanent regression assets where practical;
- migration failure preserves the original recoverable data.

---

## 12. Time architecture

Use an injected clock abstraction.

Do not scatter `DateTime.Now` or Unity time calls through domain code.

Separate:

- canonical UTC wall time;
- monotonic runtime duration where available;
- simulation checkpoints;
- player-local presentation time.

All offline systems should advance from explicit timestamps/checkpoints rather than hidden coroutine state.

---

## 13. Randomness architecture

Canonical randomness must be deterministic.

Use an injected deterministic RNG or domain random service with persisted seeds/state.

Rules:

- save/load cannot reroll an expedition result;
- rendering randomness cannot decide canonical rewards;
- test fixtures can supply known seeds;
- algorithm changes that affect persisted outcomes require version awareness.

---

## 14. Event model

Domain events may be useful to communicate significant transitions such as:

- activity credited;
- project advanced;
- project completed;
- landmark stage changed;
- producer capped;
- expedition completed;
- discovery unlocked;
- region milestone reached.

Events are outputs of committed domain operations, not the sole persistence model unless event sourcing is deliberately adopted later.

Use events to build summaries/telemetry/presentation effects without coupling systems.

---

## 15. Read models

UI should receive purpose-built read models instead of entire mutable domain graphs.

Examples:

- Home summary;
- project queue rows;
- region landmark status;
- discovery journal entries;
- diagnostics snapshot.

Benefits:

- presentation cannot accidentally mutate state;
- UI remains stable when internals evolve;
- expensive derived values can be computed centrally;
- tests can validate user-visible state separately.

---

## 16. Composition root

Create one explicit composition/bootstrap boundary that wires:

- persistence implementation;
- clock;
- activity adapter;
- application services;
- notification adapter;
- diagnostics;
- presentation entry points.

Avoid service location from arbitrary MonoBehaviours.

Dependency injection can be manual. A framework is not required merely to achieve separation.

---

## 17. Unity scene architecture

Scenes should be presentation containers, not save-state owners.

Recommended separation:

- bootstrap/composition scene or persistent app root;
- lightweight shell/menu UI;
- optional world scene loaded only when needed;
- test scenes for PlayMode verification.

World objects bind to stable landmark/content IDs and render the corresponding canonical state.

If a scene unloads and reloads, canonical progress must remain unchanged.

---

## 18. Background/foreground lifecycle

On pause/background:

- finish or safely cancel in-flight application operations;
- persist dirty canonical state;
- store simulation checkpoint;
- avoid expensive continuous execution.

On resume:

- restore/validate state if required;
- reconcile elapsed time;
- query activity source as appropriate;
- commit new state;
- produce concise change summary.

The architecture must not depend on a Unity process remaining alive in the background.

---

## 19. Platform bridge isolation

Native platform code is high-risk and must be narrow.

A bridge may:

- request/query permissions;
- fetch platform records;
- register notifications;
- report platform lifecycle/source status.

A bridge may not:

- calculate Vitality;
- mark projects complete;
- write arbitrary save fields;
- contain game balance rules.

---

## 20. Diagnostics architecture

Introduce structured diagnostic events early.

Categories:

- save/load/migration;
- activity ingestion;
- reward transactions;
- offline simulation;
- content validation;
- platform permission state;
- performance timing;
- scene/world binding failures.

Diagnostics must redact sensitive/raw health data by default and use bounded local retention.

---

## 21. Content validation

Add automated validation for definitions before runtime:

- duplicate stable IDs;
- missing prerequisites;
- dependency cycles;
- impossible unlock conditions;
- missing presentation references;
- invalid negative costs;
- unknown resources;
- orphaned content;
- project chains with no completion route;
- restoration stage mappings without canonical target state.

Invalid content should fail CI/build validation rather than appear as silent runtime corruption.

---

## 22. Testing topology

### Pure domain tests
Fastest and largest suite. Covers invariants, state transitions, simulation, ledgers, time, RNG.

### Application tests
Use fake ports to test orchestration, recovery, activity ingestion, persistence sequencing, summaries.

### Infrastructure tests
Validate serializers, file store, migrations, native bridge contracts where possible.

### Unity EditMode
Validate presentation-independent Unity asset/configuration integration.

### Unity PlayMode
Validate scene binding, lifecycle, UI transitions, input, rendering-side state reflection.

### Device tests
Validate actual health-source behavior, lifecycle, permissions, battery/performance, storage, notifications.

---

## 23. Architectural fitness checks

CI should eventually enforce or detect:

- Domain assembly has no Unity reference;
- forbidden dependency direction;
- all content IDs unique;
- migrations are registered sequentially;
- no unresolved content validation errors;
- tests pass from clean checkout;
- docs/checklists reflect evidence state.

---

## 24. Architectural non-goals

Do not prematurely add:

- microservices;
- server-authoritative event sourcing;
- distributed caches;
- generalized ECS purely for fashion;
- a custom scripting language;
- remote-config dependency for core balance;
- backend orchestration for offline systems;
- complicated DI frameworks unless the project actually needs them.

The architecture should be rigorous but small enough for an MVP team/agent workflow to understand completely.

---

## 25. Architecture exit criteria for M1

M1 is complete only when:

- the pure domain compiles independently of Unity;
- activity/reward transactions are modeled;
- project/region state transitions are deterministic;
- persistence port and at least one durable implementation exist;
- migrations are versioned;
- clock/RNG are injected;
- state can be loaded, advanced, saved, and reloaded deterministically;
- duplicate transaction application is proven safe;
- clean-clone automated tests pass;
- dependency rules are documented and reflected in project/assembly structure.
