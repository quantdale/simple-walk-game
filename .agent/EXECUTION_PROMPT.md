# Active Execution Campaign — M3 Ambient Progression Vertical Slice

**Status:** BLOCKED — RUNTIME VERIFICATION PENDING (headless M3 implementation complete and automated verified; Unity editor unavailable in execution environment, see §14)
**Planned-From:** `4419b17760881a4ac9833105b67641d975f39cb7`  
**Target branch:** `main`  
**Campaign class:** IMPLEMENTATION  
**Primary roadmap target:** M3 — Ambient progression vertical slice  
**Target size:** one substantial integrated autonomous campaign. Continue while coherent M3 work remains; do not pad the session, split the milestone into artificial micro-campaigns, or advance into M4 merely to keep working.

---

## 0. Operating mandate

Continue from the repository's **actual current state**, not from assumptions in this prompt.

Before any write, execute the mandatory `AGENTS.md` repository-identity / fetch / starting-SHA / writer-lease preflight exactly as written there. This campaign inherits that contract; it does not replace or weaken it. If the preflight fails, stop and report rather than modifying anything.

Then:

1. Read `AGENTS.md`, `docs/AGENT_EXECUTION_GUIDE.md`, `.agent/PLANNER_HANDOFF.md`, this campaign, `README.md`, `docs/MASTER_PLAN.md`, `docs/ROADMAP.md`, `docs/PRODUCT_SPEC.md`, `docs/GAME_SYSTEMS.md`, `docs/TECHNICAL_ARCHITECTURE.md`, `docs/UX_DESIGN.md`, `docs/ACTIVITY_PIPELINE.md`, `docs/TESTING_AND_RELEASE.md`, `docs/PERFORMANCE_BUDGETS.md`, `docs/DECISIONS.md`, and `docs/RISK_REGISTER.md` before making architectural changes.
2. Inspect the complete implementation/test/tooling tree, recent commits since `Planned-From`, open issues/PRs, CI state, and native agent state. Do not review only recently changed files: reason about the effect of every M3 change across domain, application, persistence, tooling, presentation, tests, and docs.
3. If `main` advanced after `Planned-From`, reconcile those commits first. If parts of this campaign already landed, resume at the first genuinely incomplete requirement instead of recreating them.
4. Preserve unrelated user work. Never reset, clean away, overwrite, or force-push other work to make integration convenient.
5. Keep the repository buildable at meaningful checkpoints. If isolated campaign branches/worktrees are required by `AGENTS.md`, integrate all completed work back into `main` before campaign completion.
6. Fix any Critical/High regression introduced or exposed by this work before completion. For lower-severity findings outside M3, record them precisely and defer rather than expanding scope without limit.

This is an **M3 implementation campaign**, not M4 content production, M5 full mobile UX polish, M6 Visit World, M7 platform-health integration, M8 broad hardening, or M9 release qualification.

---

# 1. Repository truth at planning time

The planner audited the complete repository tree at `4419b17760881a4ac9833105b67641d975f39cb7`, the recent history, current milestone docs, source/test/tooling files, and open GitHub work.

## Landed foundation

- M1 deterministic core and durable state is recorded as implemented and automated-verified.
- M2 activity trust pipeline is recorded as implemented and automated-verified.
- The repository records 131 headless tests at the current milestone boundary (Domain 85 / Infrastructure 19 / Application 27), plus simulation and guard proof tooling. Treat these as historical evidence only: rerun the applicable gates yourself before relying on them.
- The M2 implementation includes normalized activity records, validation/bounding, stable source/fingerprint identities, durable dedup rows, versioned step conversion, exactly-once reward transactions, correction/deletion handling, checkpoint sequencing, fixtures, persistence recovery, and diagnostics.
- No open issue or pull request was visible at planning time. Re-check this at execution start.
- The six commits after the M2 completion commit are repository/agent-safety and documentation work; no gameplay source files changed after M2. The recent wrong-repository/concurrent-writer incident has already been addressed by the tracked identity guard, writer lease, hooks, CI checks, and `AGENTS.md` contract. Do not redesign those mechanisms during this campaign unless a new bypass is actually discovered.

## M3 primitives already present — extend, do not rebuild blindly

The tree already contains meaningful portions of M3:

- `GameState` owns canonical resources, region state, project queue, producer runtimes, activity/dedup state, timestamps, and RNG.
- `ProjectModel` already defines `Locked → Available → Queued → Active → Completed`, ordered queued IDs, active project, and `AutoAdvance`.
- `OfflineAdvancer` already allocates banked Vitality, rolls completion surplus into queued work, updates project availability, advances landmark stages, unlocks producers, and performs deterministic producer ticking.
- `Region1Catalog` is a **development M3 seed**, not final M4 Region 1 content: five restoration projects, three landmarks, and one producer are enough to prove the vertical loop.
- `GameSession` already exposes boot/recovery, activity ingestion, enqueue/dequeue/reorder, persistence sequencing, and a Home read model.
- `HomeReadModel` and `ReturnSummaryBuilder` already establish the intended presentation boundary.
- Tests already cover queue rollover, producer determinism, persistence/recovery, ingestion replay/corrections/deletions, and some return-summary behavior.
- `tools/simulation` can create, credit, advance, simulate, dump, and validate saves.

The campaign is therefore an **integration/completion campaign**, not a greenfield M3 rewrite.

## Concrete integration gaps found during planning

Treat these as starting hypotheses to verify against current HEAD, then fix at the appropriate layer:

1. **No presentation/runtime project exists.** The repository has no `WalkGame.Unity` (or equivalent) project, no Home/Projects/Region runtime UI, no composition root, and no UI-level acceptance path. D-009 already chooses Unity 6 LTS as the baseline presentation direction, while the exact Unity version remains an implementation-time decision.
2. **The simulator's multi-day acceptance path bypasses M2.** `tools/simulation simulate` currently calculates daily Vitality and calls `CreditActivity` directly. That is useful as a low-level developer operation, but it cannot be the M3 proof because it skips normalized records, validation, stable identity, dedup/replay, and `IngestActivityBatch`.
3. **Producer capacity semantics are incomplete.** `ProducerDefinition.CapacityUnits` exists and `GAME_SYSTEMS.md` specifies `produced = min(capacityRemaining, rate × eligibleElapsedTime)`, but current production applies directly to `ResourceBalances` and does not use the producer definition's capacity. Existing cap tests manually set a resource cap instead. Reconcile the model, implementation, copy, diagnostics, and tests so “capacity” means one explicit thing and is actually enforced.
4. **Direct producer ticking can regress its checkpoint under a backward clock.** Top-level `OfflineAdvancer.Advance` correctly defends `LastAdvancedUtc`, but the public `TickProducers` path currently writes `LastTickUtc = nowUtc` before returning on negative elapsed time, and a test codifies that regression. Make checkpoint behavior defensively monotonic at every callable boundary or narrow the API so misuse cannot silently create future overproduction.
5. **Return summaries are ephemeral strings, not a robust re-entry state.** `ReturnSummaryBuilder` aggregates events into a mutable `List<string>` with no hard output bound, no durable “committed but not yet presented/acknowledged” state, and no first-class primary next action. A crash after committing progress but before presentation can therefore lose the intended re-entry explanation. M3 should make return summary behavior concise, deterministic, replay-safe, and presentation-friendly without turning the UI into canonical state.
6. **Queue management is only partially surfaced.** Application operations exist for enqueue/dequeue/reorder, but there is no explicit application operation for toggling auto-advance, no complete Projects read model, no lightweight Region read model, and no user-facing accessible queue controls/empty-state behavior.
7. **There is no M3 development activity-source/injector flow.** Fixture parsing exists, but no lightweight presentation-facing test injector/coordinator proves “app closed → synthetic activity accumulates → reopen → reconcile through the production trust pipeline → advance world → show one summary”.
8. **Documentation has minor evidence drift.** The README's early prose still calls the repository “documentation-first / pre-implementation” even though its later repository-state section correctly describes landed M1/M2 code. Reconcile stale status copy while updating M3 evidence.

Do not assume this list is exhaustive. The executor must re-audit adjacent code and tests before changing behavior.

---

# 2. Campaign objective

Deliver the first **player-visible, end-to-end ambient progression loop** on top of the already-trusted M1/M2 foundation.

By the end of M3, a deterministic development profile must be able to follow this exact story through production boundaries:

`fresh durable state → choose/queue restoration work → remain closed while several days of synthetic normalized activity accumulate → reopen/reconcile those records through the same M2 trust pipeline used by future platform adapters → exactly-once Vitality is credited → queued projects advance across completion boundaries → landmark stages change → a producer unlocks and creates bounded secondary production over elapsed time → all resulting canonical state is committed → one concise return summary explains the important changes and next action → player chooses/reorders the next project in the lightweight UI → process is killed/reloaded → replaying the same source records produces no duplicate reward or world change`

The resulting slice must demonstrate the product thesis with **minimal foreground attention**. It does not need final art, a complete Region 1, health-platform APIs, or Visit World.

---

# 3. Workstream A — Baseline verification and M3 architecture reconciliation

Before feature implementation:

- run the current clean headless build/tests, simulation smoke, content validation, and guard proof suite applicable to the environment;
- inspect all M3-adjacent domain/application/infrastructure files and their callers/tests, not just files named in this prompt;
- verify the current save schema and migration assumptions before adding persisted M3 state;
- map the intended M3 dependency flow (`Presentation → Application → Domain`, infrastructure behind ports) and identify any existing shortcuts that would let presentation mutate canonical state;
- resolve the exact Unity 6 LTS editor/runtime version used for the new presentation project and record it in `docs/DECISIONS.md` if Unity is introduced in this campaign;
- prefer the smallest compatible Unity/package surface. Do not add a heavy DI/state-management framework merely to create three screens.

If a required runtime/editor is unavailable in the execution environment, continue with everything that can be implemented and automated honestly, but mark runtime-only gates **UNVERIFIED**. Never fabricate editor/device evidence.

---

# 4. Workstream B — Finish ambient progression semantics before binding UI

Strengthen the existing M3 domain/application primitives instead of replacing them.

## Project queue and continuity

Provide a complete, deterministic application-facing queue contract:

- enqueue an available project;
- remove a queued project without corrupting status;
- reorder queued work using a validated permutation;
- toggle auto-advance through an application use case and persist it atomically;
- define a valid way to start/continue work when auto-advance is disabled, if the current model otherwise leaves the player unable to activate work;
- preserve all unallocated Vitality when the queue is empty or automation is disabled;
- cross one or several completion boundaries without losing or double-consuming Vitality;
- make malformed/duplicate/stale queue entries fail validation or recover explicitly rather than being silently discarded in a way that hides corruption;
- ensure completion effects (availability, landmark stages, producer unlocks) are idempotent.

Do not add a complex scheduler or generalized workflow engine.

## Producer capacity and time correctness

Resolve the currently incomplete producer-capacity contract with an explicit decision:

- define what `ProducerDefinition.CapacityUnits` means in the canonical model;
- enforce that meaning in deterministic simulation;
- make resource/producer capacity interactions unambiguous when more than one producer outputs the same resource;
- ensure a producer that reaches capacity cannot mint value beyond the documented bound;
- preserve fractional milli-unit carry deterministically;
- make every simulation checkpoint monotonic under backward clock movement; direct callable paths may not backdate a producer checkpoint and later overproduce;
- ensure unlock timestamps prevent retroactive production before a producer existed;
- keep long-absence bounding explicit and tested;
- make summary/diagnostic wording match the actual capacity that was hit.

If this requires changing persisted producer/runtime shape, bump the save schema and add a sequential migration plus representative migration fixture/tests. Do not silently mutate schema-v1 meaning.

## Canonical state validation

Extend content/state validators for every new invariant, especially:

- queue consistency and active/queued status agreement;
- producer capacity/checkpoint invariants;
- restoration stage monotonicity as applicable;
- any persisted return-summary/checkpoint state introduced later in this campaign.

---

# 5. Workstream C — One platform-neutral resume/reconcile path

M3 must prove the ambient loop through the **real M2 downstream trust path**, not through direct Vitality injection.

Introduce the smallest application-level abstraction/coordinator necessary for a development activity source to provide normalized records to the existing ingestion pipeline. Keep future Health Connect/HealthKit behind the same narrow seam without implementing those providers now.

Required behavior:

1. load/recover canonical state;
2. obtain deterministic synthetic/fixture activity records for the requested interval;
3. call the same `IngestActivityBatch` trust pipeline used by production adapters;
4. advance applicable offline systems in a clearly defined order;
5. commit the resulting canonical state before presentation treats progress as complete;
6. return typed diagnostics/read models suitable for the lightweight UI;
7. retry/replay after restart without double-crediting.

Reconcile this flow with the architecture's conceptual boot order instead of adding a parallel “special M3” path.

## Upgrade the developer tooling

Keep `CreditActivity`/`credit` only if it remains useful as an explicitly low-level diagnostic primitive. The M3 acceptance tooling must not depend on it.

Enhance `tools/simulation` so a deterministic multi-day scenario can:

- generate or ingest normalized step records with stable provider/source IDs and timestamps;
- process them through `IngestActivityBatch`;
- replay the same source window and prove a no-op;
- optionally consume checked-in fixture files through the same path;
- close/recreate `GameSession` instances between days/windows so persistence and boot logic are actually exercised;
- emit useful summary/diagnostic evidence without exposing or storing raw health payloads.

The simulator should become a reproducible M3 acceptance harness, not merely a balance shortcut.

---

# 6. Workstream D — Durable, bounded return-summary system

Turn the return summary into a first-class M3 re-entry contract while keeping canonical progression independent of presentation.

Implement a typed summary/read model (names are implementation choices) that can represent at least:

- major project/landmark transformation;
- newly available or blocked actionable project decision;
- eligible activity/Vitality aggregate;
- meaningful producer output/cap state;
- save recovery/clock-protection notice where applicable;
- a single primary next action or explicit “nothing needs attention” state.

Requirements:

- deterministic priority: transformation → actionable decision → meaningful production/notice → concise aggregates;
- hard bounded output suitable for the 5–15 second glance target; do not dump every simulation event;
- no duplicate lines/cards for repeated/replayed activity;
- no hidden dependence on scene/UI state;
- progress must already be durable before the summary claims it happened;
- if the process dies after committing progress but before the summary is displayed/acknowledged, the important summary must still be available after restart;
- acknowledging/dismissing a summary is idempotent and may not alter the underlying earned progression;
- “no meaningful changes” is a valid empty state.

Choose the smallest persistence strategy that satisfies the crash/re-entry contract. If persisted shape changes, perform the schema/migration work rather than stuffing durable truth into presentation preferences.

Add focused tests for:

- one-day return;
- seven-day return;
- multiple project/landmark transitions in one absence;
- queue becomes empty;
- producer reaches capacity;
- no-change return;
- crash/restart before summary acknowledgement;
- repeated acknowledgement;
- replayed activity does not regenerate a false “new progress” summary;
- summary remains within the chosen item/line bound.

---

# 7. Workstream E — Read models and minimal Unity shell

Create the minimum Unity 6 LTS presentation needed to prove M3. Follow the repository's documented `src/WalkGame.Unity` direction unless the actual Unity project layout requires an evidence-backed adaptation.

## Composition boundary

Create one explicit bootstrap/composition root that wires:

- durable save implementation;
- clock;
- development activity source/injector;
- application session/coordinator;
- lightweight presentation.

Do not use arbitrary `MonoBehaviour` service location. Do not let UI callbacks edit `GameState`, resource dictionaries, project states, save JSON, or scene-owned shadow state directly.

## Home

A glanceable Home screen should expose, through immutable read models/application operations:

- the pending/most-relevant return summary;
- current project and progress;
- recent eligible activity impact where meaningful;
- one primary next action;
- compact region/restoration status;
- clear queue-empty / nothing-needs-attention states.

## Projects

Provide the smallest usable management surface for:

- locked/available/queued/active/completed status;
- effort/cost and prerequisite explanation;
- enqueue/remove;
- ordered queue;
- reorder using both a direct manipulation path if chosen **and an accessible non-drag equivalent**;
- auto-advance on/off;
- empty queue and invalid-action feedback.

## Region

Provide a lightweight, non-3D Region status view showing at least:

- landmarks and canonical restoration stages;
- active project context;
- damaged/restored distinction without relying on color alone;
- producer unlock/output/cap status where useful;
- overall progress derived from canonical/read-model data.

This is not Visit World. Do not build character control, camera exploration, terrain streaming, 3D landmark art production, or M6 systems.

## Development activity injector

Expose a clearly development-only way to feed deterministic synthetic activity/absence scenarios through the same platform-neutral source/reconcile path. It must be impossible to mistake this for a production health provider, and production builds must be able to exclude/disable it cleanly.

## M3 UX minimums

Full M5 polish is not required, but the M3 shell must already respect the architecture and basic usability contract:

- Home always reachable;
- loading/empty/error states for the implemented flows;
- semantic labels and logical focus where supported;
- readable text scaling/layout within the chosen minimal shell;
- no color-only critical state;
- reduced-motion-safe behavior (avoid introducing mandatory motion-heavy transitions);
- progression remains fully operable without precise drag gestures.

---

# 8. Workstream F — End-to-end M3 acceptance proof

Add one named, reproducible automated acceptance scenario that exercises the complete headless vertical loop. It should be runnable on a clean clone without platform health APIs.

At minimum prove:

1. fresh state starts valid and persists;
2. player queues the first project through an application use case;
3. several days of deterministic normalized synthetic step records are produced;
4. those records enter `IngestActivityBatch` rather than direct Vitality credit;
5. project progress crosses at least one completion boundary and remaining Vitality is preserved/rolled correctly;
6. at least one landmark stage changes;
7. a producer unlocks and later produces bounded secondary resources from elapsed time;
8. the process/session is recreated from disk between activity windows;
9. return summary is concise and survives a commit-before-presentation restart;
10. player queues/reorders/selects the next project through application use cases;
11. the same activity window is replayed after restart and produces no additional Vitality, project completion, producer unlock, or false return-summary change;
12. final state validates and is deterministic from the same starting seed/inputs.

Also add targeted tests for boundary cases the acceptance scenario does not isolate cleanly:

- exact project completion boundary;
- surplus spanning multiple queued projects;
- queue empty while activity continues;
- auto-advance disabled;
- invalid queue reorder/duplicate IDs;
- producer capacity boundary and long absence;
- backward time at all public simulation boundaries;
- save/load roundtrip of every new M3 field;
- migration from retained prior schema if schema changes;
- recovery from backup with pending M3 summary state;
- presentation/read-model tests proving callers receive snapshots rather than mutable canonical collections.

Where Unity is available, add appropriate EditMode/PlayMode coverage for bootstrap and the core Home → Projects → Region → Home flow, including restart/test persistence. Do not claim device verification; physical platform integration belongs later.

---

# 9. Workstream G — Documentation and evidence reconciliation

Before completion, update durable repository truth to match what actually landed.

At minimum reconcile:

- `README.md` — remove stale “pre-implementation” wording, describe the M3 state and exact evidence level;
- `docs/ROADMAP.md` — mark each M3 exit criterion complete **only** when supported by named evidence; leave genuinely unverified items unchecked;
- `docs/MASTER_PLAN.md` — update immediate-next-campaign status after M3 only if M3 truly exits;
- `docs/DECISIONS.md` — record the exact Unity version/tooling choice, producer-capacity semantics, durable return-summary semantics, and any other implementation-time decision that materially constrains later work;
- `docs/GAME_SYSTEMS.md`, `docs/TECHNICAL_ARCHITECTURE.md`, `docs/UX_DESIGN.md` — reconcile any contract that changed during implementation;
- `docs/TESTING_AND_RELEASE.md` — add executable M3 clean-clone/runtime verification commands where practical;
- `docs/RISK_REGISTER.md` — update risks exposed/mitigated by the vertical slice;
- `.agent/EXECUTION_PROMPT.md` — when and only when every campaign completion gate is satisfied, change this prompt's status from `ACTIVE` to `COMPLETED` (or an equally unambiguous terminal status) and append concise evidence/blocker notes so `/goal continue` can never mistake stale work for an active campaign.

Do not mark M4/M5/M6/M7 work complete merely because a stub exists.

---

# 10. Scope boundaries

Do **not** spend this campaign on:

- final Region 1 content volume/balance (M4);
- discoveries/expeditions/ecology as full systems unless a tiny non-authoritative placeholder is strictly necessary for a view contract;
- full onboarding/settings/notifications/accessibility qualification (M5);
- 3D Visit World, character controller, camera, exploration, art production, shaders, or quality tiers (M6);
- Health Connect, HealthKit, permissions, background platform queries, or device provider work (M7);
- cloud sync, backend, accounts, social features, multiplayer, leaderboards, monetization, ads, or live-service systems;
- broad release hardening/performance/device qualification unrelated to proving M3;
- speculative abstractions/frameworks not demanded by the vertical slice.

Preserve the M1/M2 trust invariants above all: deterministic canonical state, exactly-once activity crediting, integer economy math, atomic durability, dedup state never outrunning reward state, and conservative correction behavior.

---

# 11. Required validation

Run the strongest applicable gates in the execution environment and record exact commands/results.

Minimum headless bar:

- repository identity/guard preflight and guard proof suite;
- `dotnet build SimpleWalkGame.sln`;
- full `dotnet test SimpleWalkGame.sln` (or the repository-equivalent full solution command);
- content/state validation;
- existing deterministic simulation smoke;
- new M3 normalized-activity end-to-end acceptance scenario;
- replay of the same M3 source records after restart;
- migration/recovery tests if persisted shape changes.

Unity bar when the editor/runtime is available:

- project opens/imports cleanly using the exact recorded Unity 6 LTS version;
- compilation has zero unresolved errors;
- relevant EditMode tests pass;
- relevant PlayMode/bootstrap/UI tests pass;
- lightweight Home/Projects/Region flow is runtime-verified;
- presentation does not retain authoritative state across a scene/reload boundary.

If CI exists for the pushed SHA, inspect the exact workflow result. Fix locally actionable failures introduced by the campaign. Environment-only or unavailable gates must be reported as `UNVERIFIED`, not silently omitted or called green.

---

# 12. M3 completion gates

Do not declare this campaign complete until all of the following are true or a genuine external blocker is recorded explicitly:

- [ ] M3 acceptance uses normalized synthetic activity through the M2 trust pipeline, not direct-credit shortcuts.
- [ ] app/session can be absent between synthetic activity windows and resume from durable state.
- [ ] queued progress crosses completion boundaries without loss, double-spend, or manual timing requirements.
- [ ] queue management supports the minimal player decisions required by M3, including persisted auto-advance control.
- [ ] landmark restoration stages visibly/readably derive from canonical state.
- [ ] producer unlock/offline production works deterministically and the documented capacity is actually enforced.
- [ ] backward-clock handling cannot regress canonical simulation checkpoints through any supported public path.
- [ ] return summary is typed/presentation-friendly, bounded, deterministic, and survives commit-before-presentation restart.
- [ ] Home, Projects, and lightweight Region presentation exist and consume application/read-model boundaries rather than owning canonical state.
- [ ] development activity injection is clearly isolated from future production providers and exercises the real downstream pipeline.
- [ ] restart/recovery/replay cannot duplicate Vitality, project/world completion, producer effects, or return-summary “new” events.
- [ ] every new persisted field has roundtrip + migration implications handled explicitly.
- [ ] full applicable automated suite is green with no unresolved Critical/High regression.
- [ ] docs accurately distinguish automated-verified, runtime-verified, device-verified, and unverified behavior.
- [ ] no M4+ scope was used to hide an incomplete M3 integration.

If all substantive M3 implementation is complete but a runtime-only gate is impossible because the required editor/toolchain is genuinely unavailable, do **not** falsify completion. Record the exact blocker/evidence state in the campaign and roadmap so the next session can resume at that runtime gate rather than recreating implementation.

---

# 13. Git, handoff, and finish protocol

Follow `AGENTS.md` and `docs/AGENT_EXECUTION_GUIDE.md` exactly.

Before final integration/push:

1. inspect `git status`, staged/unstaged diff, recent log, and generated/untracked files;
2. exclude unrelated, generated, local-machine, secret, and sensitive files;
3. rerun the applicable completion gates on the final integrated tree;
4. fetch/reconcile `origin/main` deliberately; never force-push or discard a remote advance;
5. ensure all completed implementation, tests, migrations, Unity project/config, tooling, and docs are committed;
6. use detailed logical commit messages. The final handoff/integration commit message must summarize the campaign's delivered behavior, root causes/gaps fixed, schema/decision changes, exact validation evidence, remaining unverified runtime/device gates, and intentional deferrals;
7. push the completed work to `origin/main`;
8. verify local `HEAD` equals `origin/main` and the working tree is clean;
9. inspect CI/workflow status for that exact pushed SHA where available and fix actionable failures;
10. release the writer lease on normal completion.

Do not finish by printing another giant prompt for the operator to paste. The durable repository state is the handoff. The next invocation should be able to resume from `.agent/EXECUTION_PROMPT.md` and the repository itself.

If M3 genuinely satisfies every applicable completion gate, stop after the final pushed verification. Do not automatically begin M4 in the same campaign.

---

# 14. Execution outcome (2026-08-26)

**Status: headless M3 implementation COMPLETE and automated verified; runtime-only gates BLOCKED by environment.** This is a terminal status for this campaign session — `/goal continue` must not treat the sections above as an active work list; resume at §14.3 only.

## 14.1 Delivered (automated verified unless noted)

- Producer capacity contract resolved and enforced (D-032): bounded pending-output store, `min(storeRoom, rate × elapsed)` minting, no-waste overflow, auto-delivery, parked-flush behind resource caps, monotonic checkpoints at every public path (backward-clock regression on direct `TickProducers` fixed along with its codifying test), unlock-time stamping, long-absence bounds.
- Save schema v2 + registered sequential migration `m1-to-v2-producer-stored-milli-units` with representative v1 envelope fixtures and re-encode stability tests.
- Durable typed return summaries (D-033): composed before persistence, priority/dedupe/12-item bound, primary next action, idempotent acknowledgement, crash-before-presentation survival.
- Complete queue contract: persisted auto-advance toggle (`SetAutoAdvance`), manual start when automation is off (`ActivateQueuedProject`), validated reorder/enqueue/dequeue, Projects/Region/Home read models with producer store state.
- Platform-neutral reconcile path (D-034): `IActivityRecordSource` port + `GameSession.IngestFromSource` over the unchanged M2 trust pipeline; dev-only `SyntheticWalkingSource` isolated in `WalkGame.Application.Development`.
- Simulation CLI acceptance harness: `walk` (+ `--replay` exactly-once proof) and `ack`; `credit`/`simulate` documented as low-level diagnostics.
- Named acceptance scenario `M3AmbientProgressionAcceptanceTests`: full 12-step story incl. session recreation between every window, landmark stage changes, producer unlock + bounded offline production (parked half-unit preserved), summary crash-safety, choose/reorder/start next project, whole-history replay no-op with zero fabricated claims, byte-identical deterministic rerun, validator-clean final state.

## 14.2 Verification evidence

- `dotnet build SimpleWalkGame.sln` — clean, zero errors.
- `dotnet test SimpleWalkGame.sln` — 156 passed / 0 failed (Domain 89, Infrastructure 23, Application 44).
- CLI walk smoke: 16 days × 20000 steps → 3200 Vitality exactly once; replay of identical window → 0 credited, 16 duplicates ignored; `validate --selftest` PASS at schema v2.
- Guard suite + repository identity preflight green (see final handoff commit message for exact rerun results).

## 14.3 Remaining gates (BLOCKED — exact blocker)

- **Blocker:** no Unity 6 LTS editor exists in this execution environment; committing editor-generated project files that cannot be opened/imported/compiled/tested here would violate the evidence rules (D-018) — decision D-035 records this honestly.
- Deferred to a runtime-enabled session: create `src/WalkGame.Unity` shell (Home/Projects/Region consuming the existing read models/use cases), EditMode/PlayMode coverage, bootstrap/composition root in Unity, runtime verification of the same acceptance story, then device work per M5–M7.
- Everything needed for that session already exists and is tested: read models, use cases, save store/clock/activity-source seams, migration chain, acceptance harness.

## 14.4 Intentional deferrals

- M4 Region 1 content volume/balance; real resource sinks/caps that make producer stores bind in ordinary play (content-level concern).
- Health Connect/HealthKit providers behind `IActivityRecordSource` (M7).
- Discoveries/expeditions/ecology systems (not required for the M3 view contracts).
