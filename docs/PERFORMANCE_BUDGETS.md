# Performance Budgets

## 1. Principle

Performance is part of the product contract. A game intended to sit alongside a person’s daily activity cannot feel like an always-running battery tax.

The project therefore separates the inexpensive ambient app experience from the optional higher-cost 3D world experience.

Budgets below are initial engineering targets. They should be replaced or tightened using measured device evidence rather than relaxed because a build happens to exceed them.

---

## 2. Runtime modes

### Lightweight mode

Includes:

- Home;
- Projects;
- Region overview;
- Discoveries;
- Settings;
- activity reconciliation;
- offline simulation;
- return summary.

Goal: minimal CPU/GPU activity when idle, fast launch, low memory.

### Visit World mode

Includes:

- 3D world rendering;
- avatar/camera;
- environment effects;
- animation;
- audio;
- world inspection.

Goal: stable interactive frame rate with scalable quality.

Visit World should not remain loaded invisibly when the player is using lightweight screens unless measurement proves the cost is negligible and the architecture benefits justify it.

---

## 3. Launch budgets

Initial targets on representative mid-range target devices:

- cold launch to usable lightweight UI: **≤ 3.0 s target**;
- warm resume to usable UI: **≤ 1.0 s target**;
- first frame should not imply readiness before canonical state is validated;
- activity reconciliation may continue asynchronously if the last valid state can be shown safely;
- blocking migrations must expose progress/failure rather than frozen UI.

Track median and slow-tail performance, not only best-case local runs.

---

## 4. UI responsiveness

Targets:

- common input-to-visible-response: **< 100 ms** where no durable transaction is required;
- show progress/loading indication for operations exceeding roughly 200–300 ms;
- no main-thread health-source query;
- no large synchronous save serialization on every minor UI event;
- no avoidable frame hitch during return-summary construction.

---

## 5. Frame-rate targets

### Lightweight UI

- target refresh: **60 fps** where platform/display conditions permit;
- avoid continuous redraw/animation when the screen is visually idle;
- reduced-motion mode should further lower unnecessary animation work.

### Visit World

Baseline target tiers:

- mid-range device: **stable 30 fps minimum**, preferably 60 fps under normal quality;
- higher tier: optional 60 fps mode;
- low tier: quality reduction before persistent sub-30 fps behavior.

Frame pacing matters more than occasional peak FPS.

---

## 6. Frame-time budget

At 60 fps:

- total frame budget ≈ 16.7 ms.

At 30 fps:

- total frame budget ≈ 33.3 ms.

Do not intentionally consume the full budget in steady state. Leave headroom for OS/device variation and transient workload.

Profile CPU main thread, render thread, GPU, scripting, physics, animation, UI, particles, and GC separately.

---

## 7. Memory budgets

Exact device-class budgets must be established during implementation, but initial requirements are:

- lightweight shell should not retain 3D world assets unnecessarily;
- no unbounded history collections;
- bounded diagnostic logs;
- bounded activity-ledger metadata or compaction strategy;
- asset loading/unloading must be explicit;
- memory after repeated Visit World enter/exit cycles should return near a stable baseline;
- no monotonic memory growth across repeated background/resume cycles.

Track:

- managed heap;
- native memory;
- texture memory;
- mesh memory;
- audio memory;
- loaded asset bundles/addressable groups if used.

---

## 8. Garbage collection

Requirements:

- avoid steady per-frame allocations in core world loops;
- avoid LINQ/temporary collections in hot paths where profiling shows cost;
- pool only where measurements justify complexity;
- return-summary and offline simulation may allocate, but should not create pathological spikes;
- record GC allocations during representative 5-minute Visit World sessions.

Optimization must remain evidence-based; do not obscure maintainable code for hypothetical micro-allocations.

---

## 9. Asset budgets

Every major art category requires a budget before final content production:

- texture resolution/compression;
- material count;
- shader variants;
- triangle counts by object class;
- LOD levels;
- particle counts;
- animation bone counts;
- audio compression/streaming policy;
- light/shadow usage.

The first region should establish reusable asset rules rather than solve performance through late emergency downgrades.

---

## 10. Rendering policy

Recommended mobile principles:

- use an appropriate mobile rendering pipeline/profile;
- minimize transparent overdraw;
- constrain real-time shadows;
- use LOD/culling for world geometry;
- batch/instance repeated vegetation/props where effective;
- keep shader complexity visible in tooling;
- cap particle systems;
- disable effects outside relevant restoration states;
- avoid expensive full-screen effects as a default requirement;
- provide quality tiers.

Visual restoration contrast matters more than maximum shader complexity.

---

## 11. Physics policy

Visit World is not a physics sandbox.

Requirements:

- use simple colliders where possible;
- avoid unnecessary rigid bodies;
- use fixed update rates appropriate to actual mechanics;
- no high-frequency simulation for decorative world state;
- no physics work in lightweight screens;
- profile character controller and interaction queries separately.

---

## 12. Background-work budget

The app should not depend on continuous background execution.

Background strategy:

- rely on platform activity stores as authoritative external sources;
- reconcile on foreground/resume and platform-supported opportunities;
- perform bounded queries;
- avoid polling loops;
- avoid wake-heavy timers;
- batch persistence where safe;
- schedule notifications through platform facilities rather than keeping runtime active.

---

## 13. Battery policy

Battery regressions are release blockers if they make ordinary use materially expensive.

Measure at least:

- lightweight idle foreground;
- Visit World active session;
- repeated resume/reconciliation;
- realistic day of background usage where platform tooling permits;
- notification behavior;
- health-source querying frequency.

A device becoming hot during lightweight Home/Projects usage is a defect.

---

## 14. Activity-query budgets

Activity reconciliation should:

- prefer incremental/change APIs when reliable;
- bound historical overlap windows;
- batch records;
- avoid reprocessing huge history on every launch;
- use durable checkpoints;
- compact dedup state without compromising correctness;
- expose timing/count diagnostics.

Set alarms/diagnostics for unusually large batch counts or reconciliation duration.

---

## 15. Save budgets

Targets:

- ordinary save commit should be short enough not to create user-visible stalls;
- save size must remain bounded and measured over simulated months of play;
- diagnostics and ledgers require compaction/retention rules before unbounded growth appears;
- atomic backup/journal strategy must not multiply save size without bound.

CI simulation should report save-size growth across long-running synthetic profiles.

---

## 16. Storage budgets

Track:

- installed application size;
- downloaded optional assets if introduced;
- save data;
- backups;
- diagnostics;
- cached world assets.

Optional high-fidelity assets should not be silently downloaded over cellular without appropriate platform/user considerations.

---

## 17. Network policy

The MVP core must not require network access.

If network features are later introduced:

- no synchronous network dependency on launch for local progression;
- use timeouts/retries with bounded behavior;
- queue non-critical sync;
- expose offline state clearly;
- never block activity reconciliation that can be performed locally because a server is unavailable.

---

## 18. Quality tiers

Visit World should expose automatic or user-selectable quality tiers.

Potential knobs:

- render scale;
- shadow resolution/distance;
- foliage density;
- particle density;
- post-processing;
- LOD bias;
- target frame rate.

Quality changes may alter presentation only, never canonical progression.

---

## 19. Performance regression suite

Maintain repeatable scenarios:

1. cold boot with mature save;
2. return after 7 days with large activity batch;
3. Home idle for 5 minutes;
4. enter Visit World;
5. traverse dense/restored area;
6. trigger landmark state transition;
7. exit/re-enter Visit World repeatedly;
8. background/resume loop;
9. mature region with maximum unlocked ambient content;
10. long-running simulated save/ledger.

Record build, device, OS, quality tier, profiler configuration, and measurements.

---

## 20. Performance severity

### Critical

- crash/out-of-memory;
- save corruption caused by performance/resource pressure;
- runaway background behavior.

### High

- sustained sub-target frame rate on supported baseline device;
- severe input stalls;
- excessive battery/thermal behavior in lightweight mode;
- repeated multi-second blocking operations in normal check-in flow;
- memory growth across lifecycle cycles.

### Medium

- isolated hitches;
- slow optional transition;
- inefficient but non-user-blocking asset behavior.

Critical/High performance defects block release qualification.

---

## 21. Evidence requirements

Every performance claim should record:

- device model/class;
- OS version;
- build configuration;
- scene/state;
- measurement tool;
- duration/sample size;
- quality tier;
- observed metric;
- pass/fail against budget.

Desktop editor FPS is not evidence of mobile performance.

---

## 22. Definition of performance-ready

The MVP is performance-ready only when:

- lightweight workflows meet responsiveness goals on target devices;
- Visit World meets minimum frame/pacing targets;
- no lifecycle memory leak is observed in the qualification scenario;
- activity reconciliation is bounded and measured;
- save growth is bounded across long simulation;
- ordinary use does not exhibit unacceptable thermal/battery behavior;
- quality tiers work;
- reduced-motion mode works;
- no Critical/High performance defect remains unresolved;
- results are recorded as device evidence rather than inferred from editor runs.
