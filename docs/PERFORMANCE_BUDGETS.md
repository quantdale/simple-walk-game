# Performance Budgets

> **Direction (D-171, 2026-10-17):** under the Creative Direction Bible v1 the illustrated 2.5D world *is* the Home and lightweight mode; there is no separate 3D mode. Lightweight-mode goals (minimal idle CPU/GPU, fast launch, low memory) therefore apply to an animated world view. That means a throttled frame rate, ambient motion paused when idle or backgrounded, and atlased sprites. "Visit World" budgets apply to optional deeper exploration. Numbers are unchanged until measured (editor now; device blocked by KI-002).

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

**Measured (editor only, D-074):** on a mature 365-day restored save, every lightweight screen builds and renders in 0.14–0.41 ms (model + view) and warm boot takes 5–7 ms. These are well inside the 100 ms input budget on the box. Device timings are UNVERIFIED (KI-002).

**Re-measured (editor only, D-139, 2026-10-17):** after the D-122/D-138 localization overlays, the screen median is 0.010–0.045 ms and 3,000 enter/exit renders take 157 ms. No regression (`docs/evidence/d139-perf-remeasure/`). Device UNVERIFIED (KI-002).

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

**Measured (editor only, D-074):** repeated screen enter/exit is gated at < 1 MB of live-panel managed growth from 750 to 3,000 renders. The measured residual is about 116 B/render (KI-018), after the ScrollView-reuse fix. Device memory remains UNVERIFIED (KI-002); see `docs/evidence/m8-h1/PERFORMANCE_EDITOR.md`.

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

**Concept A texture budget (D-202, Phase C style guide §7.5–7.6):** ≤ 12 MB GPU texture memory for the Old Trailhead slice scene, ≤ 6 MB per additional visible area (current + neighbour loaded only); atlases ≤ 2048², ASTC 6×6, world-sprite mipmaps off; PPU 200 at the 2× master (= 100 at 1×). Batches < 60, ≤ 60 live particles. UNVERIFIED on device (KI-002) until measured.


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

**Headless review (D-107, 2026-10-16):** the shell reads walking data only on boot and resume, once each, over at most 30 days; navigation makes no data reads; no polling, timers or per-frame `Update` in the shell. Pinned by EditMode `BackgroundSyncBudgetTests`. Device battery numbers remain UNVERIFIED (KI-002).

**Amended (D-185, 2026-10-17):** a foreground resume within 30 s of the last *successful* provider read skips the read (the reload and UI still run), so a burst of resumes costs one read. Cold start, a fresh grant, any connection change, a failed or partial last read and a backward clock always read. Freshness cost: at most 30 s. Pinned by `BackgroundSyncBudgetTests` (rewritten to this contract) and `LiveSyncThrottleTests`.

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

**Status (2026-09-29, D-048): discharged.** `scripts/verify-save-growth.sh` reports the series in CI and requires it to reproduce exactly. Measured growth is **linear and unbounded at ~525-530 bytes per ingestion day** (day 7: 10,344 B; day 365: 202,644 B; day 730: 394,401 B), corroborating the ~554 B/day figure in `docs/evidence/m8-h1/LONG_HORIZON.md`. Region completes at day 365 and is stable through day 730, so post-completion growth is pure ledger accumulation.

Compaction is still deferred and is still a product decision, not an engineering one: D-045 records that late deletions target ledger rows without horizon checks, so compaction changes deletion semantics. The measurement that decision needs now exists; the decision has not been made.

**Update (2026-10-16, D-066): bounded.** Ledger retirement (F-07) makes retained activity detail plateau at the 45-day trusted window (~46 rows on the flat profile). Growth model: day 7: 11,986 B; day 365: 58,540 B; day 730: 80,458 B. The residual ~60 B/day is the identity-only retired set (an identity string per retired record), which is expected to keep growing slowly. Structures expected to plateau: `ProcessedRecords.Entries` and `Ledger.Records`. `scripts/verify-save-growth.sh` now asserts convergence: second-year growth must be ≤ 60% of first-year growth (measured 47%). Late deletions are window-validated since D-053, so retiring rows beyond the window does not change deletion semantics.

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

**Status (2026-09-29, D-048): 5/10 measured headlessly, 1/10 by explicit surrogate, 4/10 not simulated.** `tools/simulation scenarios` implements 1, 2, 6 (canonical half), 9 and 10; scenario 8 is covered only by a surrogate (repeated boot-from-disk plus advance, which is not real backgrounding). Scenarios 3, 4, 5 and 7 are **not simulated** — no editor, no device — and no figure was invented for them. Timings are recorded as evidence and deliberately not gated; the deterministic size/count half is gated by `scripts/verify-save-growth.sh`. Evidence: `docs/evidence/performance/SCENARIOS.md`.

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
