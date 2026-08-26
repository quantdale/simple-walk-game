# Active Execution Campaign — M3/M4-R Unity Shell + Runtime Qualification

**Status:** BLOCKED (Gate A1: no Unity 6 LTS editor installed)  
**Planned-From:** `c4ba6f686741435144bf9fdb753612c5ceeabcfc`  
**Target branch:** `main`  
**Campaign class:** IMPLEMENTATION + RUNTIME VERIFICATION  
**Primary roadmap target:** close D-035 and runtime-qualify the already-landed M3 ambient loop plus M4 Region 1 presentation contract before full M5 UX expansion  
**Target size:** one substantial integrated campaign, approximately 8–12 focused hours if a compatible Unity editor is actually available. Do not pad the session or split it into artificial micro-campaigns.

---

## 0. Operating mandate

Continue from the repository's **actual current state**, not from assumptions in this prompt.

Before any write, execute the mandatory `AGENTS.md` repository-identity / fetch / starting-SHA / writer-lease preflight exactly as written there. This campaign inherits that contract. If the preflight fails, stop and report rather than modifying anything.

Then:

1. Read `AGENTS.md`, `docs/AGENT_EXECUTION_GUIDE.md`, `.agent/PLANNER_HANDOFF.md`, this campaign, `README.md`, `docs/MASTER_PLAN.md`, `docs/ROADMAP.md`, `docs/PRODUCT_SPEC.md`, `docs/GAME_SYSTEMS.md`, `docs/WORLD_AND_CONTENT.md`, `docs/TECHNICAL_ARCHITECTURE.md`, `docs/UX_DESIGN.md`, `docs/ACTIVITY_PIPELINE.md`, `docs/TESTING_AND_RELEASE.md`, `docs/PERFORMANCE_BUDGETS.md`, `docs/DECISIONS.md`, and `docs/RISK_REGISTER.md` before architectural changes.
2. Inspect the **complete** implementation/test/tooling/presentation tree, all commits since `Planned-From`, open issues/PRs, hosted CI, and actual native runtime/toolchain state. Do not review only recently changed files.
3. Build a fresh campaign ledger. Classify findings as **LANDED/TRUSTED**, **RUNTIME-ONLY GAP**, **M4 PRESENTATION-BINDING GAP**, **NEW SAME-DOMAIN DEFECT**, **EXTERNAL BLOCKER**, or **STALE/SUPERSEDED**.
4. Preserve unrelated user work. Never reset, clean away, overwrite, or force-push other work to make integration convenient.
5. Keep the repository buildable at meaningful checkpoints. If isolated worktrees/branches are required by `AGENTS.md`, integrate accepted work back into `main` before completion.
6. Fix every Critical/High regression introduced or exposed by this campaign before completion. Record lower-severity unrelated findings precisely and defer them rather than expanding scope without limit.
7. During implementation, use focused tests around the affected layer. Run the full headless suite and Unity runtime suite at meaningful integration boundaries and at the end.

This campaign is the **runtime dependency bottleneck**. M1, M2, headless M3, and headless M4 are already proven. Do not consume the session rewriting them simply because presentation work is harder.

The governing rule is:

> Make the existing canonical game visible, lifecycle-safe, and runtime-verified in a real Unity 6 LTS editor. Presentation consumes state and application operations; it never becomes authoritative state.

---

## 1. Repository truth at planning time

The planner audited current `main` at `c4ba6f686741435144bf9fdb753612c5ceeabcfc`, recent commits, repository structure, M4 outcome evidence, roadmap/master plan, current prompt state, open PR/issue state, and hosted CI before activating this campaign.

Current evidence:

- M1 deterministic core and durable state are automated-verified.
- M2 activity trust pipeline is automated-verified, including durable deduplication, corrections/deletions, bounded reconciliation, atomic checkpoint/reward persistence, and replay safety.
- The headless M3 ambient loop is automated-verified through `M3AmbientProgressionAcceptanceTests`, durable return summaries, queue controls, producer simulation, application read models, and the `walk --replay` acceptance path.
- M4-H is COMPLETED headlessly: Millbrook Valley content v2 contains **19 projects / 6 chains / 6 landmarks / 3 producers / 13 discoveries / 3 expeditions**, ecology + settlement arcs, a closure milestone, stable post-completion state, expanded validation, deterministic pacing reports, and `M4Region1AcceptanceTests`.
- Save schema remains v2. M4 additions are additive under D-036; do not invent a migration merely because Unity is being added.
- The current automated baseline is **180/180 tests**: Domain 101 / Infrastructure 25 / Application 54.
- The latest pushed SHA `c4ba6f686741435144bf9fdb753612c5ceeabcfc` has hosted `ci` success (run `32922388778`).
- There are no open PRs or open issues carrying unfinished work at planning time.
- `README.md` and `docs/MASTER_PLAN.md` identify the next dependency bottleneck as an installed Unity 6 LTS editor for the presentation/runtime lane.
- D-035 remains truthful: the prior runtime campaign stopped at Gate A1 because no Unity 6 LTS editor existed in that execution environment. No Unity project was manufactured and no runtime claim was falsified.
- There is still no verified `src/WalkGame.Unity` runtime project on `main` at planning time.
- Application already exposes presentation-safe boundaries including Home/Projects/Region/ReturnSummary read models and M4 discovery/expedition/region progression data. UX docs require M4 runtime binding for discovery reviewed state, expedition locked/available/completed state, ecology/settlement stages, closure, and bounded return-summary events.

Treat proven headless semantics as trusted evidence. If Unity exposes a real cross-layer defect, fix the correct layer and add regression coverage. Do not fork canonical logic into Unity-owned copies.

---

## 2. Campaign objective

Create and runtime-qualify the minimal **Unity mobile shell** that makes the existing M3/M4 game loop visible and operable without starting the full M5 polish program or M6 3D world.

By the end of this campaign, a deterministic development profile must be able to execute this story through the real Unity runtime and the same application/domain boundaries already proven headlessly:

`launch/load durable state → see pending return summary and Home state → inspect current project/region → queue/reorder/start restoration work → inject deterministic development activity through IActivityRecordSource → close/recreate runtime/session → reload persisted state → exactly-once activity advances canonical projects/landmarks/producers/discoveries/expeditions/arcs → see bounded durable summary → inspect Projects/Region/Discoveries/Expeditions → acknowledge summary/review discoveries through application operations → replay the exact same activity window → observe zero duplicate reward/world progress → reach or load a closure-state profile → verify completed-region presentation stays stable after restart`

The shell is intentionally modest. Its purpose is to prove:

- actual Unity compatibility;
- assembly/composition boundaries;
- lifecycle-safe durable persistence;
- application-driven mutations;
- M3/M4 read-model binding;
- deterministic development activity ingestion;
- restart/replay correctness;
- basic mobile navigation and accessible operation;
- runtime evidence strong enough that the next campaign can be **M5 UX completion/polish**, not another architecture bootstrap.

---

## 3. Workstream A — Runtime/toolchain gate and Unity project bootstrap

### A1. Prove the editor exists before generating editor state

Detect the actual installed Unity 6 LTS editor and record:

- exact editor version/build;
- executable path;
- platform/runtime backend relevant to the development host;
- package manager resolution state;
- whether batchmode EditMode/PlayMode execution is available.

**If no compatible Unity 6 LTS editor is installed or usable, STOP at Gate A1.**

Do not:

- hand-author unverifiable Unity project YAML to appear productive;
- claim editor import/compile/runtime evidence that did not execute;
- silently install a different major Unity line;
- spend the campaign rewriting headless systems to avoid the editor dependency.

Instead, set this prompt `BLOCKED`, record exact detection evidence and the first resumable action, keep headless truth untouched, commit/push only the truthful blocker record, and stop.

### A2. Bootstrap through the real editor/toolchain

If A1 passes, create the minimal Unity project through the actual supported toolchain, preferably at `src/WalkGame.Unity` unless current repository structure justifies an equivalent path.

Requirements:

- target the installed Unity 6 LTS version deliberately and record the decision;
- commit only source/config/assets needed for deterministic clean import;
- never commit `Library/`, `Temp/`, `Logs/`, generated caches, IDE state, local machine paths, credentials, license material, build output, or user-specific files;
- keep the package surface minimal;
- use the Unity Test Framework or equivalent editor-supported test mechanism rather than inventing an external pseudo-runtime harness;
- establish explicit assembly definitions/package boundaries so presentation depends inward on Application/Domain/Infrastructure without making those layers depend on Unity;
- prove actual compatibility of the current `netstandard2.1`/C# 9 shared projects and `System.Text.Json` dependency from the real editor.

If shared code requires a compatibility adjustment, choose the smallest architecture-preserving solution and prove the headless suite still passes. Never duplicate serializers, state models, content catalogs, or game rules only for Unity.

---

## 4. Workstream B — Composition root, persistence, lifecycle, and failure recovery

Build one explicit composition/bootstrap root that owns runtime wiring but not canonical state.

Wire the existing production stack deliberately:

- `AtomicFileSaveStore` rooted under the correct Unity persistent-data location;
- current `SaveCodec` and registered migration chain;
- production `SystemClock` for ordinary runtime execution;
- the **full M4 Region1Catalog**, not the old five-project seed assumption;
- `GameSession` and its read models/use cases;
- development-only synthetic activity source behind explicit development/editor configuration.

Required lifecycle behavior:

- cold launch can create or load a save deterministically;
- session recreation reloads from disk rather than static/singleton shadow state;
- all committed mutations persist before presentation treats them as durable;
- boot-time advancement is not applied twice because scenes reload;
- pending return summaries survive runtime restart until acknowledged;
- summary acknowledgement is idempotent and progression-neutral;
- save recovery/corruption failures produce a clear runtime state instead of a frozen or silently reset UI;
- migration/load failure never continues with a half-valid canonical state;
- exiting/re-entering screens cannot mutate world progress;
- runtime teardown does not manufacture extra producer time or activity credit.

Do not spread a service locator across arbitrary `MonoBehaviour`s. Keep composition centralized and testable.

---

## 5. Workstream C — Minimal runtime surfaces over canonical read models

Implement a coherent lightweight mobile shell. This is the **qualification shell**, not final visual polish.

### C1. Home + return summary

Home must answer the existing UX contract quickly:

- what changed;
- what is progressing now;
- whether attention is required.

Bind canonical/application data for:

- pending return summary;
- primary next action / nothing-needs-attention state;
- current project progress;
- Vitality/resource summary where useful;
- region completion/restoration summary;
- navigation to Projects, Region, Discoveries, and Expeditions.

The return summary must render the durable typed state and acknowledge only through the application operation after it is actually presented. Do not clear it on scene load.

### C2. Projects

Provide runtime controls for:

- locked / available / queued / active / completed states;
- enqueue/remove where allowed;
- ordered queue;
- reorder with an accessible non-drag alternative even if drag is also implemented;
- persisted auto-advance toggle;
- manual start/activation when automation is off;
- prerequisite/unlock explanation sufficient for diagnostics;
- explicit invalid-action feedback rather than silent no-op.

All mutations must go through `GameSession`/application use cases. Refresh from a new read-model snapshot after success; never patch UI-local state as if the operation succeeded.

### C3. Lightweight Region

Bind `RegionReadModel` and M4 canonical state without loading a 3D world.

Show at minimum:

- all six major landmarks with canonical stage/status;
- damaged/restored distinction without color-only meaning;
- active/current project context;
- producer unlock/output/store/cap state as exposed by application data;
- ecology stage;
- settlement stage;
- region completion state/timestamp where appropriate;
- compact progress toward closure.

### C4. Discoveries

Bind the landed M4 discovery contract:

- authored discovery identity/title/body/provenance/location keys resolved into displayable content;
- locked/unlocked distinction;
- unread/reviewed distinction that survives restart;
- review action routed through the canonical application operation;
- reviewing a discovery may never gate or grant progression.

### C5. Expeditions

Bind the landed M4 expedition contract:

- Locked / Available / Completed states;
- route title/description and authored requirements;
- no mandatory manual claim interaction;
- completion/result presentation reflects canonical automatic progression;
- one bounded celebration/summary hook is allowed, but no new foreground expedition rules may be invented in presentation.

### C6. Closure/post-completion presentation

Provide a deterministic lightweight closure state:

- `IsCompleted` is reflected correctly;
- closure is celebrated once from canonical event/summary state, not every launch;
- post-completion world status remains stable after restart;
- nothing resets Region 1 or forces Region 2.

---

## 6. Workstream D — Navigation, accessibility baseline, and runtime state coverage

Implement only the baseline necessary to prove the shell is viable and to avoid building obvious M5 debt.

Required:

- Home is always one clear action away;
- back behavior is coherent and does not recreate canonical state accidentally;
- loading never looks like frozen input;
- implemented screens have deliberate normal/loading/empty/error states;
- no core state is communicated by color alone;
- practical text scaling does not destroy navigation or primary actions;
- interactive controls have meaningful semantic labels where supported;
- focus order is sensible for keyboard/controller/screen-reader tooling where Unity permits it;
- queue reorder has a non-drag path;
- reduced-motion-safe behavior is the default for this qualification shell; no mandatory sweeping camera/particle transitions;
- haptics/audio, if added at all, are optional and never required to understand state.

Do **not** attempt complete M5 accessibility certification, notification architecture, polished onboarding, or every platform-specific semantics edge case here. The goal is to ensure the runtime architecture does not make those requirements impossible.

---

## 7. Workstream E — Development activity injector and deterministic runtime scenarios

Expose a clearly development-only activity/absence surface that drives the **existing** platform-neutral seam.

Rules:

- all synthetic records enter through `GameSession.IngestFromSource` → `IngestActivityBatch`;
- never call low-level `CreditActivity` or mutate Vitality directly for runtime acceptance;
- use stable provider/source IDs and deterministic timestamps so replay is exactly-once;
- release/non-development configuration must exclude or disable the injector cleanly;
- controlled time must use the narrowest existing clock abstraction; do not create a Unity-only clock model;
- test profiles may load known deterministic fixture saves, but fixtures must still pass through the real codec/validator and must not bypass progression logic to manufacture success.

Provide development scenarios sufficient to exercise:

1. ordinary one-day return;
2. multi-day absence with multiple project boundaries;
3. queue-empty/decision-needed state;
4. discovery unlock/review;
5. expedition availability/completion;
6. ecology/settlement stage advancement;
7. region closure/post-completion;
8. replay of the exact same activity window;
9. save recovery/error presentation where practically automatable.

---

## 8. Workstream F — EditMode/PlayMode/runtime acceptance evidence

Use the strongest practical tests supported by the actual editor.

### F1. EditMode / integration coverage

At minimum prove:

- composition root can instantiate the production stack without Unity-owned canonical state;
- Unity persistent-path adaptation can be redirected to an isolated temporary path for tests;
- save/load/recovery/migration wiring works through the real shared Infrastructure layer;
- Home/Projects/Region/Discoveries/Expeditions presenters/controllers consume immutable snapshots/application results;
- development injector is absent/disabled in production configuration;
- any shared-code compatibility adapter introduced for Unity is covered;
- content/resource/presentation key resolution fails diagnostically rather than silently.

### F2. PlayMode qualification

Create a named runtime acceptance test or small family of tests that proves the actual player-visible M3/M4 story through the shell:

1. clean deterministic launch;
2. Home renders canonical state;
3. player queues/chooses work through UI → application boundary;
4. synthetic activity enters through `IngestFromSource`;
5. one or more projects/landmarks/producers advance;
6. app/session-equivalent recreation reloads durable state;
7. pending return summary survives and acknowledges correctly;
8. Discoveries reflects unlock/review persistence;
9. Expeditions reflects locked/available/completed canonical state without manual claim semantics;
10. Region reflects ecology/settlement stages and completion state;
11. Projects reorder/toggle/manual-start operations remain correct after reload;
12. replaying the identical activity window credits **zero** additional reward/world progress and creates no fabricated transformation summary;
13. closure/post-completion presentation remains stable across another restart;
14. final canonical state validates cleanly.

Prefer several diagnosable scenario tests over one opaque giant test.

### F3. Headless regression preservation

After any Unity/shared-layer integration change, keep the existing repository gates green:

- repository identity/guard proof;
- `dotnet build SimpleWalkGame.sln`;
- `dotnet test SimpleWalkGame.sln`;
- documented simulation smoke;
- M3 `walk --replay` proof;
- M4 `profile`/validation evidence as applicable when content/economy code changes.

Do not regenerate M4 pacing evidence unless a code/content change actually affects it.

### F4. Hosted Unity CI

If reproducible hosted Unity tests can be added without unsafe licensing/secrets handling, do so narrowly. If licensing/environment prevents hosted Unity execution, keep hosted headless CI green and record Unity evidence as **local runtime verified** only. Never call it hosted-CI verified when it was not.

No physical-device claim is allowed unless a real device run actually occurred.

---

## 9. Workstream G — Runtime smoke performance and diagnostics

Do not turn this into M8 performance hardening, but collect enough runtime evidence to prevent obvious architectural mistakes.

At minimum inspect and record:

- cold import/compile stability;
- shell launch responsiveness on the development host;
- no unnecessary 3D scene or heavyweight world asset loading in lightweight screens;
- obvious managed allocation/update-loop mistakes introduced by presentation;
- save path and file size behavior under a mature M4 profile;
- repeated lightweight screen navigation for runaway object/listener accumulation;
- runtime logs free of repeating exceptions/warnings caused by the new shell.

If the shell obviously violates the documented lightweight architecture, fix it before declaring completion. Detailed device thermal/battery/frame-budget qualification remains M8/M9.

Add a small diagnostics surface or development logging only where it materially improves supportability: current save path/profile, activity-source status, last ingestion diagnostics/checkpoint, schema version, and runtime/editor version are useful; raw health payloads are not.

---

## 10. Cross-layer audit before completion

After the runtime slice works, perform a deliberate whole-repository review.

Inspect at least:

- Unity assembly/package dependency direction;
- whether any canonical state leaked into scene-owned mutable objects;
- save/recovery/migration behavior under Unity lifecycle and file paths;
- duplicate boot advancement and producer ticking;
- return-summary commit/presentation/ack ordering;
- queue mutation + snapshot refresh correctness;
- discovery review persistence;
- expedition automatic-completion semantics;
- region closure one-shot presentation;
- activity replay identity across runtime recreation;
- clock/UTC/time-zone formatting at presentation boundaries;
- listener/event subscription lifecycle across navigation;
- dev injector exclusion from production configuration;
- generated/project hygiene;
- headless CLI/tests after shared-layer compatibility changes;
- docs/evidence drift.

A runtime-discovered defect may live in shared code. Fix root cause with a regression test. Conversely, do not rewrite shared code when the defect is presentation misuse.

---

## 11. Documentation and evidence reconciliation

Before completion, update repository truth to exactly match what landed.

At minimum reconcile:

- `README.md` — actual Unity shell state, exact editor version, runtime evidence tier, current test totals, remaining unverified device/platform work;
- `docs/ROADMAP.md` — mark only runtime criteria supported by named evidence; do not mark full M5 complete from this shell;
- `docs/MASTER_PLAN.md` — if D-035 is genuinely closed, move the immediate-next pointer to the first remaining M5 completion campaign;
- `docs/DECISIONS.md` — exact Unity 6 LTS version, package/assembly strategy, composition/persistence choices, and any compatibility decisions;
- `docs/TECHNICAL_ARCHITECTURE.md` — real Unity composition root and dependency boundaries;
- `docs/UX_DESIGN.md` — implemented runtime behavior for Home/Projects/Region/Discoveries/Expeditions/closure and explicit M5 gaps;
- `docs/WORLD_AND_CONTENT.md` — only if runtime binding reveals a real presentation-contract correction;
- `docs/TESTING_AND_RELEASE.md` — exact editor/batchmode test commands, named EditMode/PlayMode/runtime evidence, and honest evidence tiers;
- `docs/PERFORMANCE_BUDGETS.md` — only for measurements actually established;
- `docs/RISK_REGISTER.md` — runtime lifecycle/toolchain/presentation risks exposed or mitigated;
- `.agent/EXECUTION_PROMPT.md` — append a concise execution outcome and set `ACTIVE` → `COMPLETED` only when every applicable campaign gate is satisfied. If Unity/runtime remains unavailable or an external gate prevents qualification, set `BLOCKED` with exact evidence and the first resumable action.

Do not erase the historical fact that the earlier M3-R attempt was blocked; record that this campaign resumed and either closed or re-confirmed the blocker.

---

## 12. Scope boundaries

Do **not** spend this campaign on:

- changing Region 1 content scale/balance merely for novelty;
- Region 2;
- full M5 onboarding polish, notification implementation, permission flow integration, exhaustive settings, or complete accessibility certification beyond the baseline required here;
- M6 Visit World: character controller, camera, terrain, 3D exploration, final art, shaders, streaming, environment production, quality tiers;
- M7 Health Connect/HealthKit/native activity providers or physical-device permission qualification;
- broad M8 red-team/performance/device hardening;
- M9 release candidate work;
- backend, accounts, cloud sync, multiplayer, social features, leaderboards, monetization, ads, live-service systems;
- speculative framework migrations, ECS, generic MVVM packages, heavy DI frameworks, or custom content engines not demanded by runtime qualification;
- rewriting proven trust/reward/persistence/content semantics without evidence.

The shell may contain placeholders for art/layout, but placeholders must bind real canonical data and must not masquerade as completed M5/M6 presentation.

---

## 13. Completion gates

This campaign is complete only when all applicable gates below are satisfied:

1. Repository identity/lease/reconciliation policy was followed.
2. A real compatible Unity 6 LTS editor was detected and exact version recorded.
3. Unity project imports/compiles through the real editor without unverifiable hand-authored bootstrap artifacts.
4. Shared Domain/Application/Infrastructure remain the canonical implementation; no Unity-owned fork exists.
5. Composition root wires the production save/codec/clock/catalog/session stack correctly.
6. Runtime lifecycle reloads durable state rather than relying on scene/static shadow state.
7. Home + return summary work through application/read-model boundaries.
8. Projects queue/reorder/auto-advance/manual-start operations work through application use cases.
9. Lightweight Region accurately reflects M4 landmark/producer/ecology/settlement/completion state.
10. Discoveries accurately reflect unlock/review state and review persistence.
11. Expeditions accurately reflect locked/available/completed state without manual-claim divergence.
12. Closure/post-completion presentation is one-shot/stable and never resets Region 1.
13. Development activity enters through `IActivityRecordSource` / `IngestFromSource`; no presentation shortcut credits progression.
14. Runtime restart/reload evidence proves summary and world state durability.
15. Replay of already-processed activity is a no-op for reward/world progression in runtime evidence.
16. EditMode/integration tests pass.
17. PlayMode/runtime acceptance tests pass.
18. `dotnet build SimpleWalkGame.sln` passes.
19. `dotnet test SimpleWalkGame.sln` passes in full with no regression.
20. Repository simulation/validation/guard gates remain green.
21. No repeating runtime exceptions/warnings or obvious lifecycle listener leaks remain.
22. No introduced Critical/High defect remains unresolved.
23. Docs accurately distinguish AUTOMATED VERIFIED, RUNTIME VERIFIED, DEVICE VERIFIED, and UNVERIFIED claims.
24. Intended work is committed and pushed to `origin/main` without force-push/history rewrite.
25. Final local `main` equals `origin/main` and the working tree is clean.
26. Hosted CI for the final pushed SHA is inspected; implementation-addressable failures are fixed before completion.
27. `.agent/EXECUTION_PROMPT.md` records the outcome and no stale superseded campaign remains ACTIVE.

If Gate A1 fails because Unity is unavailable, the correct result is **BLOCKED**, not a partial fake implementation.

---

## 14. Git and reporting contract

Use `AGENTS.md` as authoritative.

In addition:

- start from current `main`; fetch/reconcile before implementation;
- never force-push or rewrite shared history;
- preserve unrelated user work;
- use logical implementation commits with detailed messages;
- before final integration/push, inspect the complete diff for generated Unity junk, local editor state, secrets, machine-specific paths, accidental binary/cache output, and scope creep;
- if `origin/main` advanced after the starting SHA, reconcile deliberately under the repository lost-update policy and re-run affected verification;
- final commit/report must state: start SHA, final SHA, exact Unity version, project/package/assembly strategy, major runtime surfaces landed, shared-layer compatibility changes, schema/migration effect, headless + Unity test counts/results, runtime acceptance scenarios, remaining M5/M6/M7 gaps, device evidence status, CI result, and whether D-035 is closed;
- finish on `main`, push to `origin/main`, verify exact SHA equality, and release the writer lease normally.

---

## 15. Stop conditions

Stop and record a precise `BLOCKED` state instead of fabricating evidence if:

- repository identity or writer-lease safeguards fail;
- no compatible Unity 6 LTS editor exists or licensing prevents actual editor execution;
- the project cannot be imported/compiled without a material toolchain decision that requires operator input;
- a migration/save compatibility issue cannot preserve canonical progress safely;
- runtime qualification would require bypassing the trust pipeline or duplicating canonical state;
- a required external service/license/tool blocks a gate that cannot be reproduced honestly.

Do not begin M5 full UX completion, M6, M7, hardening, or release qualification in the same session after this campaign completes. Stop after reconciliation and push so the planner can choose the next campaign from evidence.

---

## 16. Execution outcome (recorded by the executing session)

**Outcome:** BLOCKED at Gate A1 — no compatible Unity 6 LTS editor is installed or usable in this execution environment. No editor state was generated and no runtime claim was made. Headless repository truth is untouched.

- **Start SHA:** `7022dab86c02e009e47132ee672a78702cd5b8a1`. Session reconciled Git first: local `main` was behind `origin/main` by exactly this one planning commit with a clean tree; deliberate `git merge --ff-only origin/main` applied it (start-of-session checkout was `c4ba6f686741435144bf9fdb753612c5ceeabcfc`, the `Planned-From`).
- **Preflight:** identity guard OK (`quantdale/simple-walk-game`); writer lease acquired normally (no stale lock encountered).
- **Gate A1 detection evidence (Windows development host):**
  - `C:\Program Files\Unity\Hub\Editor` — MISSING (no Hub-managed editors).
  - `C:\Program Files\Unity`, `C:\Program Files (x86)\*Unity*`, `D:\Program Files\Unity\Hub\Editor` — all MISSING.
  - Recursive `Unity.exe` search across `C:\Program Files` and `D:\` (depth 3) — zero results.
  - Registry uninstall entries: **Unity Hub 3.12.1** present (`C:\Program Files\Unity Hub\Unity Hub.exe`); NO Unity editor entry.
  - `%APPDATA%\UnityHub` absent → Hub has never been run/configured; no secondary install path, no `editors-v2.json`.
  - `HKLM\SOFTWARE\Unity Technologies` contains only the `Hub` key; `where.exe unity` → not on PATH.
- **Conclusion:** Unity Hub is installed but contains no editor. There is no Unity 6 LTS editor to detect, bootstrap, import, compile, or execute, so Workstream A1 fails closed exactly as specified. D-035 remains open and truthful; this outcome re-confirms (does not close) the external runtime-toolchain blocker.
- **First resumable action:** the human operator installs a licensed Unity 6 LTS editor (6000.x LTS line) via Unity Hub (`"C:\Program Files\Unity Hub\Unity Hub.exe"` → Installs), verifies the editor launches (including `batchmode` capability), then resumes/reactivates this campaign from Workstream A1. Agents must not silently install editors or substitute a different major Unity line.
- **Headless gates:** untouched — zero implementation/test/tooling files changed by this session; baseline remains `dotnet build` PASS / `dotnet test` 180/180 with hosted CI success on `c4ba6f6` (run 32922388778).
- **Lease:** released normally after committing/pushing this blocker record.
