# Active Execution Campaign — M1 Deterministic Trust Kernel + Minimum M2 Ingestion Slice

**Status:** SUPERSEDED — DO NOT EXECUTE. M1 and the M2 trust pipeline landed on `main`
after this prompt was written (see README "Repository state" and ROADMAP exit criteria).
Retained as a historical record; a new campaign requires a fresh planner audit and a
new prompt. Any session resuming from this file MUST still pass the `AGENTS.md`
preflight (identity guard + writer lease) before touching anything.  
**Planned-From:** `1c9a7ee1aae8aa83426162f0f5c491f875508692`  
**Planned-From:** `1c9a7ee1aae8aa83426162f0f5c491f875508692`  
**Target branch:** `main`  
**Campaign class:** IMPLEMENTATION  
**Target size:** one substantial long/overnight development campaign, roughly 8–12 hours of autonomous work if the environment and remaining work justify it; this is a sizing target, not a reason to pad work or stop early.  
**Primary roadmap target:** M1 — Deterministic core and durable state  
**Secondary slice:** only the minimum platform-neutral portion of M2 needed to prove the trust model with synthetic activity fixtures.  

---

## 0. Operating mandate

Continue from the repository's actual current state. Do not assume this prompt is more authoritative than code or repository state that changed after `Planned-From`.

Before editing:

1. Read `README.md`, `.agent/PLANNER_HANDOFF.md`, `docs/AGENT_EXECUTION_GUIDE.md`, `docs/MASTER_PLAN.md`, `docs/ROADMAP.md`, `docs/TECHNICAL_ARCHITECTURE.md`, `docs/ACTIVITY_PIPELINE.md`, `docs/GAME_SYSTEMS.md`, `docs/TESTING_AND_RELEASE.md`, `docs/DECISIONS.md`, and `docs/RISK_REGISTER.md`.
2. Inspect current `main`, recent commits, all implementation/test/tooling files now present, and any new issues/PRs or native agent state.
3. Reconcile current HEAD against `Planned-From`. If work from this campaign already landed, resume at the first genuinely incomplete requirement instead of recreating it.
4. Keep all work on `main`. Do not create a long-lived feature branch for this campaign unless a repository-enforced mechanism makes direct `main` work impossible. If a temporary local branch is unavoidable, integrate it back into `main` before campaign completion.
5. Preserve unrelated user work. Never discard or overwrite unrelated changes to make the tree convenient.

This is an **implementation campaign**, not a broad final-hardening campaign. Build the foundation deeply, add the targeted tests required to trust what you implement, and fix Critical/High regressions you introduce or expose. Do **not** consume the session on exhaustive release qualification, physical-device testing, broad UX polish, or speculative performance optimization that belongs to later milestones.

Do not stop because scaffolding compiles or because one subsystem works. Continue through the coherent workstreams below while meaningful implementation-ready work remains and the repository can be left better without violating the project contract.

---

# 1. Current repository truth

At planning time the repository is documentation-first and contains no production `src/`, `tests/`, Unity project, persistence implementation, simulation tool, or CI-backed game core. M0 is substantially specified but implementation has not begun.

The documentation converges on the same immediate dependency bottleneck:

- deterministic pure-C# canonical state outside Unity;
- stable IDs and explicit invariants;
- injected time and deterministic randomness;
- idempotent activity/reward transactions;
- a durable versioned save envelope with migration/recovery;
- fixture-driven activity ingestion that can be replayed safely;
- a headless simulator/validator capable of proving state determinism;
- clean-clone automated tests.

The master plan explicitly calls for **M1 plus the minimum M2 slice needed to prove the trust model** before visual/UI work.

No Unity presentation, Health Connect/HealthKit adapter, Region 1 content production, Visit World scene, backend, account system, multiplayer, or social infrastructure is required in this campaign.

---

# 2. Campaign objective

Create the first real production-grade engineering foundation for Simple Walk Game: a headless deterministic kernel that can ingest normalized synthetic walking activity, convert it to a bounded/versioned reward exactly once, advance minimal canonical restoration/project state, persist atomically, recover from damaged/interrupted persistence, reload deterministically, and expose the result through tests and a CLI diagnostic/simulation surface.

By the end of this campaign, a clean clone should be able to run a deterministic scenario such as:

`new state → ingest fixture activity → deduplicate → create stable reward transaction → credit Vitality → advance queued project/state → atomically save → reload → replay duplicate/reordered input → verify identical canonical result → dump readable diagnostics`

This is the first trustworthy vertical engineering slice. It is intentionally headless.

---

# 3. Workstream A — Repository/toolchain bootstrap

Create a maintainable solution structure aligned with `docs/TECHNICAL_ARCHITECTURE.md`, adapting only where the actual toolchain requires it.

Expected shape, unless a better evidence-backed layout is required:

```text
src/
  WalkGame.Domain/
  WalkGame.Application/
  WalkGame.Infrastructure/
tests/
  WalkGame.Domain.Tests/
  WalkGame.Application.Tests/
  WalkGame.Infrastructure.Tests/
  fixtures/
tools/
  WalkGame.Sim/
```

Deliver:

- solution/project files;
- central build settings where useful (`Directory.Build.props`, analyzers/style settings, nullable settings, warnings policy, deterministic builds, etc.);
- a target framework strategy compatible with the future Unity boundary; the pure domain/application code must not acquire a Unity dependency;
- package/version choices kept minimal and documented;
- `.gitignore` and repository hygiene required for the chosen C# toolchain;
- a clean-clone command sequence documented in the README or developer docs;
- CI that restores/builds/tests the headless solution on a clean checkout and runs the CLI smoke/validation path where practical.

Do not introduce a heavy DI framework, backend, database server, generalized event-sourcing framework, or architecture framework merely to make the project look enterprise-grade.

If choosing exact .NET SDK/test/serializer packages or locking an implementation-time runtime decision, record it in `docs/DECISIONS.md` with rationale and consequences.

---

# 4. Workstream B — Deterministic domain foundation

Implement a pure C# domain with explicit state ownership and invariants.

At minimum provide:

### Identity and common primitives

- durable strongly-typed or otherwise type-safe stable IDs for region, landmark/project, activity record/fingerprint, reward transaction, and content identities needed by this slice;
- value objects for authoritative quantities using integer/fixed-point semantics where precision matters;
- a domain result/error model that avoids exception-driven expected control flow;
- invariant validation facilities that can report precise failures.

### Time

- injected clock abstraction;
- explicit UTC timestamps/checkpoints in canonical state;
- no uncontrolled `DateTime.Now`/`DateTime.UtcNow` calls in domain logic;
- deterministic handling for zero elapsed time and backward time where this slice uses time.

### Randomness

- deterministic RNG abstraction/state suitable for future canonical randomness;
- known-seed deterministic behavior and persistence-ready RNG state;
- no canonical use of UI/runtime randomness.

### Canonical state skeleton

Create a small but real aggregate/state model containing the minimum meaningful pieces required to prove the architecture, such as:

- save metadata/schema version;
- player/resource state with Vitality;
- activity ledger state;
- reward transaction state/history/compact identity set;
- minimal region state;
- project definitions/runtime state;
- deterministic project queue state;
- processing/reconciliation checkpoint state.

Do not build all future systems now. Expeditions, discoveries, producers, full ecology, full Region 1 content, and presentation bindings remain later work unless a tiny type is strictly required to keep the current design coherent.

---

# 5. Workstream C — Minimal project/restoration progression slice

Implement enough project progression to prove that credited activity changes world state rather than merely increments a number.

Required capabilities:

- explicit project state machine consistent with the documented contract;
- deterministic prerequisites/availability for a tiny synthetic content set;
- active project + queue ordering;
- application of Vitality/progress to the active project;
- deterministic completion;
- remaining progress rolling into the next queued project where configured;
- duplicate completion/application is harmless;
- completed project mutates a canonical region/landmark stage or equivalent minimal restoration state;
- no UI or scene object owns this state.

Use a **small synthetic content fixture**, not Region 1 production content. The objective is to prove state transitions and architecture, not author the game world.

Test edge cases including:

- exact completion boundary;
- overage crossing one completion;
- overage crossing multiple queued projects if supported in this slice;
- empty queue/fallback behavior chosen for the slice;
- invalid prerequisite/start;
- repeated completion/application;
- zero input.

---

# 6. Workstream D — Activity normalization, validation, deduplication, and reward identity

Implement the minimum platform-neutral M2 path required by the master plan. Do not implement Health Connect or HealthKit yet.

Create a normalized activity representation supporting the narrow initial category: walking/step-derived activity.

Include only the minimum fields needed for correctness and provenance, such as:

- provider/source namespace;
- source record ID when available;
- deterministic fallback fingerprint when it is not;
- activity category;
- start/end UTC timestamps;
- integer quantity/unit;
- source revision/version metadata where needed by the chosen correction model;
- normalized schema/fingerprint version.

Implement:

- validation for malformed timestamps, unsupported categories/units, zero/negative quantities, future/suspicious values as appropriate;
- bounded conversion input so pathological source values cannot create unbounded rewards;
- stable record identity/fingerprinting with an explicit version;
- durable dedup ledger semantics;
- deterministic activity-to-Vitality conversion rule **version 1** with explicit rounding/units and rule version stored on credited transactions;
- deterministic reward transaction IDs derived from stable underlying identity plus conversion rule version;
- idempotent reward application: the same transaction ID applied twice must produce no second reward;
- diagnostics sufficient to count accepted/rejected/duplicate/credited items without retaining wholesale raw health payloads.

Keep correction/deletion handling deliberately bounded for this campaign. If a fully correct correction policy would materially expand M2, define the data model/hooks and implement only the minimum deterministic policy required by existing docs, then document remaining M2 work honestly. Do not pretend the full platform trust pipeline is complete.

---

# 7. Workstream E — Application orchestration and transaction sequencing

Implement explicit use cases/services rather than allowing infrastructure or future UI to mutate domain state directly.

At minimum provide an application flow equivalent to:

1. load canonical state;
2. accept a normalized synthetic activity batch;
3. validate/normalize identity;
4. detect duplicates/revisions according to the implemented policy;
5. calculate eligible net activity;
6. produce stable reward transaction(s);
7. apply reward to canonical resource/project progression;
8. update ledger and processing checkpoint in the same durable state transition;
9. persist resulting canonical state;
10. only after successful persistence, emit/return a summary/diagnostic result.

The key invariant is non-negotiable:

> **The source/reconciliation checkpoint must never be durably advanced beyond the reward/ledger/game state that it represents.**

Design the API so a caller can retry safely after interruption.

Do not make synthetic fixture ingestion a separate toy code path. After the platform boundary, fixtures must traverse the same application/domain processing path intended for future real adapters.

---

# 8. Workstream F — Durable persistence, integrity, migration, and recovery

Resolve the initial persistence implementation during this campaign and record the choice in `docs/DECISIONS.md`.

Prefer the smallest design that can actually satisfy the contracts. A versioned snapshot with atomic replace/backup and explicit migration is acceptable if implemented and tested rigorously; use a journal or another local strategy only if it provides a concrete correctness benefit for this slice.

Required behaviors:

- versioned save envelope;
- explicit current schema version;
- serializer that round-trips canonical state deterministically;
- integrity metadata/check/checksum sufficient to distinguish obviously invalid/truncated state;
- atomic/recoverable write strategy using temp/replace/backup semantics appropriate to the host filesystem;
- last-known-good recovery path;
- clear load result categories: no save, valid primary, recovered backup, corrupt/unrecoverable, unsupported future version, migration failure, etc.;
- sequential migration registry/pipeline even if only schema v1 exists initially;
- at least one synthetic prior-version fixture/migration test if practical so the migration mechanism is proved rather than merely abstracted;
- state invariant validation after deserialize/migration and before committing migrated state;
- never destroy the final recoverable copy before a migrated/repaired replacement is validated;
- test seams/failure injection around critical save boundaries.

Keep persistence behind ports/interfaces. Domain code must remain unaware of file paths, JSON, SQLite, Unity `PlayerPrefs`, or OS APIs.

---

# 9. Workstream G — Fixture corpus and adversarial deterministic tests

Create a representative fixture corpus and fast automated test suite concentrated on the trust invariants introduced here.

At minimum cover:

### Determinism

- same initial state + same inputs + same seed => same final canonical state;
- save/load does not alter deterministic outcome;
- input ordering of independent records does not change credited total/final state.

### Exactly-once / replay

- same record repeated in one batch;
- same record repeated in later batch;
- overlapping batch windows;
- same reward transaction applied twice;
- save/restart then replay;
- repeated application after project completion.

### Validation / bounds

- zero quantity;
- negative quantity;
- malformed timestamps;
- unsupported unit/category;
- future/suspicious timestamp policy;
- huge/pathological quantity;
- deterministic rounding boundaries.

### Persistence / recovery

- save/load round trip;
- truncated/corrupted primary with valid backup;
- corrupt primary and corrupt/no backup;
- unsupported future schema;
- migration success;
- migration failure preserves recoverable source;
- injected write interruption/failure leaves either old valid state or new valid state, never a half-trusted canonical state;
- checkpoint cannot outrun durable reward/ledger state.

### Project progression

- queue progression and carry-over;
- invalid project transition consumes nothing;
- duplicate completion is a no-op;
- canonical region/landmark state survives reload.

Property-style or generative tests are encouraged for idempotency/order invariants if they remain maintainable and deterministic, but do not add a large framework solely for novelty.

Target tests should be numerous enough to establish confidence in this foundation, not merely one test per class.

---

# 10. Workstream H — Headless simulator, validator, and diagnostics

Create a CLI/tooling surface under `tools/` that makes the domain inspectable without Unity.

Useful commands/subcommands should include equivalents of:

- initialize/create a clean synthetic save;
- run one or more named fixture scenarios;
- ingest/replay a fixture batch;
- simulate a simple multi-day synthetic activity profile;
- dump canonical state in stable human-readable form;
- validate save/state invariants;
- print compact ledger/activity diagnostics;
- compare/replay determinism where practical.

The tool should be scriptable and return meaningful exit codes for CI.

Provide at least one deterministic end-to-end demo fixture that proves:

1. a clean state is created;
2. several activity records are processed;
3. Vitality is credited exactly once;
4. a queued project advances/completes;
5. canonical restoration state changes;
6. state is saved/reloaded;
7. replay/duplicate input causes no extra reward;
8. a stable diagnostic/state dump can be inspected.

Avoid elaborate terminal UI. This is engineering tooling, not the game UI.

---

# 11. Workstream I — CI and architectural fitness checks

Add a pragmatic CI workflow for the headless foundation.

At minimum CI should perform:

- restore;
- build with warnings policy appropriate to the repository;
- run domain/application/infrastructure tests;
- run the key CLI validation/smoke scenario;
- detect obvious forbidden dependency drift where feasible, especially a Unity dependency entering `WalkGame.Domain`;
- run from a clean checkout with no generated local state required.

Do not block this campaign on Unity licensing/editor CI because no Unity implementation is required yet.

If CI cannot be executed remotely from the current environment, validate the commands locally, commit the workflow, and report the unverified remote status precisely.

---

# 12. Workstream J — Documentation and decision reconciliation

Update documentation in the same campaign so it reflects implementation evidence rather than aspiration.

At minimum update:

- `README.md` current repository state, build/test/simulation commands, and what is actually implemented;
- `docs/ROADMAP.md` M0/M1 checklist/status based on evidence, without claiming future milestones complete;
- `docs/DECISIONS.md` with the exact toolchain/persistence/serialization/fingerprint/conversion choices that became concrete;
- `docs/TECHNICAL_ARCHITECTURE.md` if actual structure or persistence details differ from the proposal;
- `docs/ACTIVITY_PIPELINE.md` to distinguish the implemented fixture-based slice from still-unimplemented real platform reconciliation/correction behavior;
- `docs/TESTING_AND_RELEASE.md` only where new commands/evidence need recording;
- `docs/RISK_REGISTER.md` to reduce/annotate only risks for which this campaign produced concrete evidence; do not close device/platform risks based on host tests.

Use the repository evidence vocabulary precisely: SPECIFIED, IMPLEMENTED, AUTOMATED VERIFIED, RUNTIME VERIFIED, DEVICE VERIFIED, RELEASE QUALIFIED.

M1 should only be marked complete if its documented exit criteria genuinely pass. M2 must remain partial unless all of its actual exit criteria are met.

---

# 13. Required acceptance scenario

Before declaring the campaign complete, prove a deterministic headless scenario from a clean state that exercises the integrated stack:

1. create/load a clean canonical state;
2. use a deterministic clock and seed;
3. enqueue at least two tiny synthetic restoration projects;
4. ingest a synthetic walking activity batch;
5. reject/diagnose at least one invalid record;
6. deduplicate at least one duplicate record;
7. create stable reward transaction identity;
8. credit bounded Vitality exactly once;
9. advance project progress and cross at least one completion boundary;
10. mutate minimal region/landmark restoration state;
11. atomically persist the state;
12. reload from disk;
13. replay the same/overlapping activity;
14. verify no second reward or completion occurs;
15. process one genuinely new valid record and prove only its expected delta is added;
16. dump/validate the final state;
17. demonstrate corruption/recovery behavior through an automated test or deterministic tooling scenario.

The acceptance scenario must use the production application/domain/persistence path, not a test-only fake implementation that bypasses the real logic.

---

# 14. Validation strategy for this implementation campaign

Run the validation necessary to trust the new code, but keep the campaign implementation-first.

Required before completion:

- clean restore/build;
- all new headless automated tests;
- CLI end-to-end acceptance/smoke scenario;
- persistence corruption/recovery tests;
- replay/idempotency tests;
- migration tests;
- no obvious forbidden domain dependency;
- documentation command examples actually work;
- repository remains buildable from a clean checkout or the exact environmental blocker is recorded.

Do **not** spend the session on:

- physical Android/iOS verification;
- real Health Connect/HealthKit permission flows;
- Unity PlayMode/EditMode/device validation;
- large-scale performance/battery profiling;
- exhaustive security/release certification;
- Region 1 balance/content QA;
- broad accessibility/UI QA.

Those belong to later implementation/hardening milestones.

---

# 15. Explicit non-goals

Do not implement as part of this campaign unless a tiny stub/port is strictly necessary for dependency direction:

- Unity scenes, prefabs, menu UI, or Visit World;
- final art/audio;
- Region 1 production content;
- Android Health Connect integration;
- iOS HealthKit integration;
- notifications;
- backend/cloud sync/accounts;
- analytics/telemetry backend;
- multiplayer/social/leaderboards;
- monetization, ads, premium currency, gacha;
- broad expedition/discovery/producer systems;
- speculative ECS/event sourcing/microservices;
- final activity correction/deletion parity across real platforms;
- full release qualification.

Do not let attractive unrelated work displace the trust kernel.

---

# 16. Severity and repair policy

During this campaign:

- immediately fix any Critical/High regression caused or exposed by the work when actionable;
- data corruption, duplicated rewards, silently lost durable state, migration destroying the recoverable source, or domain architecture collapsing into engine/infrastructure coupling are blockers;
- Medium/Low issues may be documented for the next campaign when they do not invalidate the current acceptance criteria;
- do not hide failing tests by deleting, skipping, weakening, or marking them flaky without a documented and defensible reason.

---

# 17. Autonomous execution rules

1. Work broadly across the coherent campaign. Do not stop after project scaffolding, the first passing test, the first save file, or the first successful fixture ingestion.
2. Prefer several coherent checkpoint commits over a giant opaque dump, but do not fragment work into meaningless micro-commits.
3. If one optional subtask becomes blocked, continue other independent in-scope work rather than ending the session immediately.
4. Do not invent device/runtime verification. Report host-only evidence as host/automated evidence.
5. Do not silently weaken the documented product invariants to get tests green.
6. Keep the dependency flow explicit: Presentation (future) → Application → Domain; Infrastructure implements ports and may depend inward, never the reverse.
7. Keep raw activity/health payload retention minimal. Fixtures may contain synthetic values, but production state structures should retain only what the game needs for identity, provenance, reconciliation, and auditability.
8. Do not switch to a broad hardening campaign while this large implementation campaign remains meaningfully incomplete. The `next-campaign` planner will select a hardening campaign later when implementation-ready work is exhausted or the roadmap reaches the appropriate gate.

---

# 18. Git and `main` requirements — hard rules

This repository must end the session with all campaign work visible to the planner on GitHub.

### At start

- operate from `main`;
- fetch/reconcile `origin/main`;
- record the starting SHA;
- do not discard unrelated local work.

### During work

- commit coherent checkpoints to `main`;
- push completed checkpoints when practical so progress is durable remotely;
- never leave the only copy of substantial completed work unpushed at the end of the session.

### At final campaign boundary

These are mandatory unless GitHub credentials or remote availability make them technically impossible:

1. all intended campaign changes are committed;
2. all intended campaign commits are on local `main`;
3. push `main` to `origin/main`;
4. fetch/reconcile once more;
5. verify local `HEAD` equals `origin/main`;
6. verify the working tree is clean;
7. report exact final SHA and push status.

Do not finish with completed campaign work only on another branch. Do not leave a PR as the final state when you have permission to integrate the campaign to `main`. Do not force-push shared history merely to satisfy synchronization.

The final campaign commit should have a **detailed commit message body** that serves as a durable session-level summary: major delivered capabilities, important correctness guarantees/fixes, tests/validation run, documentation/state changes, and material known gaps. The subject may remain concise; the body should be sufficiently detailed that a later planner can understand the campaign outcome from Git history without reconstructing the whole session.

If push is impossible, do not falsely claim success. Leave the repository safe and committed locally, report the exact blocker, and give the exact command/state needed to complete synchronization.

---

# 19. Completion gates

Do not mark this execution prompt COMPLETE until all applicable gates below are genuinely satisfied or an explicit environment blocker makes further progress impossible.

## Repository/build

- [ ] production C# solution/project structure exists;
- [ ] clean restore/build succeeds;
- [ ] domain compiles with no Unity dependency;
- [ ] CI workflow for headless build/test/validation exists.

## Deterministic domain

- [ ] stable IDs/value objects/result model exist;
- [ ] injected clock exists and is used by canonical time-sensitive logic;
- [ ] deterministic RNG abstraction/state exists;
- [ ] minimal canonical resource/activity/project/region state exists;
- [ ] invariants are explicit and validated.

## Progression

- [ ] minimal project state machine/queue works;
- [ ] progress carry-over/completion boundaries are deterministic;
- [ ] completion changes canonical restoration state;
- [ ] repeated completion/reward paths are safe.

## Activity/reward trust slice

- [ ] normalized synthetic walking records exist;
- [ ] validation/bounds exist;
- [ ] stable identity/fingerprint strategy is versioned;
- [ ] conversion rule v1 is deterministic/versioned;
- [ ] reward transaction IDs are stable;
- [ ] replay/duplicate processing cannot double-credit;
- [ ] diagnostics report accepted/rejected/duplicate/credited totals.

## Persistence/recovery

- [ ] versioned save envelope exists;
- [ ] durable implementation exists behind a port;
- [ ] atomic/recoverable commit path exists;
- [ ] integrity validation exists;
- [ ] backup/recovery behavior exists and is tested;
- [ ] migration pipeline exists and is tested;
- [ ] checkpoint and reward/ledger state cannot become durably inconsistent under tested failure paths.

## Tooling/tests

- [ ] fixture corpus exists;
- [ ] deterministic/idempotency/order tests pass;
- [ ] save/load/corruption/migration tests pass;
- [ ] project progression tests pass;
- [ ] CLI simulator/validator works;
- [ ] integrated acceptance scenario passes.

## Documentation/evidence

- [ ] README reflects real implementation and commands;
- [ ] decisions are recorded;
- [ ] roadmap status is evidence-based;
- [ ] docs distinguish implemented fixture slice from future platform work;
- [ ] no host-only behavior is mislabeled as device verified.

## Git

- [ ] all intended work committed to `main`;
- [ ] all intended work pushed to `origin/main`;
- [ ] `HEAD == origin/main` after final fetch/reconcile;
- [ ] working tree clean;
- [ ] final SHA recorded.

---

# 20. End-of-campaign state update

Before the final push/report:

- change this file's `Status` from `ACTIVE` to `COMPLETE` only if the completion gates are actually satisfied;
- if the campaign is blocked before completion, keep `Status: ACTIVE` and add a concise blocker/progress section identifying the first incomplete requirement and what remains;
- update any native agent state/checkpoint files used by the environment so a later `/goal continue` can resume without redoing landed work.

Do not delete this prompt merely because the campaign completes; it is useful durable history unless repository policy explicitly archives/replaces it.

---

# 21. Final report format

End the session with a detailed factual report containing:

### Repository state

- starting SHA;
- final SHA;
- branch (`main` expected);
- push result;
- confirmation whether `HEAD == origin/main`;
- working-tree cleanliness.

### Delivered

- domain foundation;
- activity/reward trust slice;
- project/restoration slice;
- persistence/migrations/recovery;
- CLI/tooling;
- CI;
- documentation/decision updates.

### Verification

List the exact commands/suites run and their outcomes. Distinguish automated host verification from anything unverified.

### Correctness evidence

Explicitly state evidence for:

- duplicate/replay safety;
- deterministic state evolution;
- save/reload equivalence;
- corruption/recovery behavior;
- checkpoint/reward durability ordering;
- migration behavior.

### Known gaps

List real remaining Medium/Low defects, unverified runtime/device behavior, and intentionally deferred M2+ work. Do not pad this section with roadmap items unrelated to the current boundary.

### Recommended next campaign

Recommend exactly one substantial next campaign based on the **new** repository state. Expect M2 Activity Trust Pipeline completion to be a likely candidate if M1 is genuinely qualified, but re-audit rather than assuming it.

---

## Success condition

A strong outcome is not “the repo now has C# files.” A strong outcome is that the repository has crossed from specification-only into a **trustworthy deterministic executable core** whose most dangerous invariants—replay, duplicate reward, persistence corruption, migration, and deterministic progression—are demonstrably protected by code, fixtures, recovery logic, and tests, with everything synchronized to `main` for the next planner review.
