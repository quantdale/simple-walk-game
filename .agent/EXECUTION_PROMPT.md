# Active Execution Campaign — M3-R Unity Presentation + Runtime Qualification

**Status:** BLOCKED  
**Planned-From:** `7aeae185134df4578373ba2114510df1a6fe6877`  
**Target branch:** `main`  
**Campaign class:** IMPLEMENTATION + RUNTIME VERIFICATION  
**Primary roadmap target:** close D-035 and complete the remaining M3 runtime-only gates  
**Target size:** one substantial integrated runtime-enabled campaign. Continue while coherent M3-R work remains; do not pad the session, split it into artificial micro-campaigns, or advance into M4 merely to keep working.

---

## 0. Operating mandate

Continue from the repository's **actual current state**, not from assumptions in this prompt.

Before any write, execute the mandatory `AGENTS.md` repository-identity / fetch / starting-SHA / writer-lease preflight exactly as written there. This campaign inherits that contract. If the preflight fails, stop and report rather than modifying anything.

Then:

1. Read `AGENTS.md`, `docs/AGENT_EXECUTION_GUIDE.md`, `.agent/PLANNER_HANDOFF.md`, this campaign, `README.md`, `docs/MASTER_PLAN.md`, `docs/ROADMAP.md`, `docs/PRODUCT_SPEC.md`, `docs/GAME_SYSTEMS.md`, `docs/TECHNICAL_ARCHITECTURE.md`, `docs/UX_DESIGN.md`, `docs/ACTIVITY_PIPELINE.md`, `docs/TESTING_AND_RELEASE.md`, `docs/PERFORMANCE_BUDGETS.md`, `docs/DECISIONS.md`, and `docs/RISK_REGISTER.md` before architectural changes.
2. Inspect the **complete** implementation/test/tooling/presentation tree, recent commits since `Planned-From`, open issues/PRs, CI state, and native agent/runtime state. Do not review only recently changed files: reason about the effects of the Unity boundary across Domain, Application, Infrastructure, persistence, tooling, tests, docs, and runtime lifecycle.
3. Build a fresh campaign ledger from current evidence. Classify findings as: **LANDED/TRUSTED**, **RUNTIME-ONLY REMAINING**, **NEW SAME-DOMAIN DEFECT**, **STALE/SUPERSEDED**, or **EXTERNAL BLOCKER**. Do not copy old checklists blindly.
4. Preserve unrelated user work. Never reset, clean away, overwrite, or force-push other work to make integration convenient.
5. Keep the repository buildable at meaningful checkpoints. If isolated worktrees/branches are required by `AGENTS.md`, integrate completed work back into `main` before completion.
6. Fix any Critical/High regression introduced or exposed by this campaign before completion. Record lower-severity unrelated findings precisely and defer rather than expanding scope without limit.

This campaign exists to finish the runtime/presentation portion of **M3**. It is not M4 content production, M5 full mobile UX, M6 Visit World, M7 Health Connect/HealthKit integration, M8 broad hardening, or M9 release qualification.

---

## 1. Repository truth at planning time

The planner audited current `main`, recent history, the source/test trees, current docs, and hosted CI before activating this campaign.

Evidence at `7aeae185134df4578373ba2114510df1a6fe6877`:

- Hosted `ci` for the exact SHA completed successfully on the push workflow.
- No open pull request is currently carrying unfinished work for this repository.
- M1 deterministic core, M2 trust pipeline, and the **headless portion of M3** are implemented and automated-verified.
- The repository records **156 passing headless tests** (Domain 89 / Infrastructure 23 / Application 44) plus the deterministic M3 walk/replay acceptance harness.
- `M3AmbientProgressionAcceptanceTests` already proves the 12-step ambient loop across durable session recreation, trust-pipeline ingestion, project completion boundaries, landmark changes, producer unlock/offline production, return-summary durability, queue decisions, replay no-op behavior, final validation, and byte-identical deterministic rerun.
- Domain/Application/Infrastructure target `netstandard2.1` with C# 9. Infrastructure currently depends on `System.Text.Json` 8.0.5. Verify Unity compatibility from the actual editor/runtime; do not duplicate or fork canonical logic merely to make Unity compile.
- Application already exposes the intended presentation boundary: `HomeReadModel`, `ProjectsReadModel`, `RegionReadModel`, `ReturnSummaryReadModel`, queue/auto-advance/manual-start operations, durable summary acknowledgement, and `GameSession` orchestration.
- `IActivityRecordSource` is the single platform-neutral activity seam; `SyntheticWalkingSource` is development-only and enters the same `IngestActivityBatch` trust path through `GameSession.IngestFromSource`.
- Infrastructure already provides `AtomicFileSaveStore`, codec/migration chain, and `SystemClock`.
- There is still **no `src/WalkGame.Unity` project**. D-035 explicitly deferred Unity project creation because the previous executor had no Unity 6 LTS editor and therefore could not honestly import/compile/PlayMode-test editor-generated assets.
- `docs/MASTER_PLAN.md` identifies the Unity presentation shell + runtime verification of the already-implemented M3 boundaries as the immediate next campaign.

Treat the headless M3 contracts as trusted starting evidence, not immutable dogma. If the runtime exposes a real cross-layer defect, fix it at the correct layer and add regression coverage. Do not reopen proven domain/application semantics without evidence.

---

## 2. Campaign objective

Make the existing ambient progression loop **player-visible and runtime-verified in Unity** without creating a second source of truth.

By the end of M3-R, a deterministic development profile must be able to execute this story through the Unity runtime and the same application boundaries already proven headlessly:

`launch/load durable state → see pending return summary/Home state → choose or queue restoration work → simulate deterministic absence/activity through the development source → restart/reopen → reconcile via IngestFromSource/IngestActivityBatch → exactly-once Vitality advances projects/landmarks/producers → see one bounded durable summary → inspect Projects and Region → reorder/toggle automation/start work through application use cases → restart again → replay the same source window → observe zero duplicate reward/world progress and no fabricated new summary`

The Unity shell is intentionally lightweight. Its job is to prove architecture, lifecycle, persistence, usability, and runtime integration. It does not need final art, a complete Region 1, platform health APIs, or 3D Visit World.

---

## 3. Workstream A — Runtime/toolchain qualification and Unity project bootstrap

### A1. Prove the runtime exists before generating editor state

- Detect the installed Unity 6 LTS editor and record the **exact version/build** actually used.
- If no compatible Unity editor is available, do **not** hand-author unverifiable Unity YAML/project files merely to appear productive. Record the exact blocker/evidence in this campaign, leave headless truth untouched, commit/push the blocker state if documentation changed, and stop.
- If multiple compatible editors exist, choose the smallest supported baseline consistent with D-009/D-035 and record the decision in `docs/DECISIONS.md`.

### A2. Create the minimal runtime project through the real editor/toolchain

Create `src/WalkGame.Unity` (or an evidence-backed equivalent) with only the packages and settings required for this slice.

Requirements:

- commit project/config/source assets required for a clean import;
- never commit `Library/`, `Temp/`, `Logs/`, local caches, machine-specific editor state, secrets, or generated junk;
- keep package surface minimal; do not add a heavy DI/state-management/UI framework simply to produce three screens;
- establish a clean assembly boundary so Unity presentation consumes the existing Domain/Application/Infrastructure code without copying canonical business logic;
- verify actual Unity compatibility of `netstandard2.1`, C# 9 constructs, and `System.Text.Json` 8.0.5. If an integration incompatibility exists, solve it with the smallest architecture-preserving adapter/build strategy and add regression evidence. Do not create divergent Unity-only models or serialization rules.

Record any durable runtime/toolchain choice as a new decision rather than silently changing D-035 assumptions.

---

## 4. Workstream B — Composition root, persistence, and lifecycle

Build one explicit Unity composition/bootstrap boundary that owns wiring but not canonical state.

Wire the existing production components deliberately:

- `AtomicFileSaveStore` rooted under a Unity-appropriate persistent data location;
- current `SaveCodec` + registered migration chain;
- production `SystemClock` for ordinary runtime flow;
- `Region1Catalog` development M3 content seed;
- `GameSession`;
- development activity source/injector only in clearly non-production builds or editor/development configuration.

Requirements:

- presentation must never mutate `GameState`, resource dictionaries, save JSON, project states, or domain collections directly;
- no scene-owned shadow copy may become authoritative across reloads;
- no arbitrary service-locator pattern spread across `MonoBehaviour`s;
- start/new-game/continue/recovery failure states must be explicit and user-visible enough to diagnose the M3 slice;
- app/session recreation must reload durable state rather than relying on static/singleton memory;
- summary acknowledgement must call the application operation only after the runtime has actually presented the summary state; acknowledgement must remain idempotent and progression-neutral.

If Unity lifecycle behavior exposes a save/reload race, duplicate boot advancement, or stale read-model problem, root-cause it across the whole call path and cover it with the strongest practical automated regression.

---

## 5. Workstream C — Minimal Home / Projects / Region presentation

Implement the smallest usable presentation that proves the M3 contracts.

### Home

Render immutable application/read-model data for:

- pending return summary and primary next action;
- current project and progress;
- Vitality and compact resource state where useful;
- queue-empty / nothing-needs-attention states;
- compact restoration progress;
- navigation to Projects and Region.

The Home screen must be reachable reliably after navigation and restart.

### Projects

Provide runtime controls for:

- locked / available / queued / active / completed status;
- enqueue and remove;
- ordered queue;
- reorder via an accessible non-drag equivalent (for example explicit move-up/move-down controls even if drag is also present);
- persisted auto-advance toggle;
- manual activation/start when auto-advance is off;
- explicit invalid-action feedback instead of silent no-ops.

All mutations go through `GameSession` application operations. Refresh read models after committed operations; do not patch presentation state as if the operation succeeded.

### Region

Provide a lightweight non-3D Region status surface showing:

- landmark identity/title and canonical restoration stage;
- damaged/restored distinction without color-only meaning;
- active project context;
- producer unlock/output/store/cap state where represented by the current read model;
- overall progress derived from canonical/application data.

This is not Visit World. Do not build character control, cameras, terrain, shaders, streaming, exploration, or 3D art production.

### M3 usability minimum

- loading/empty/error states exist for implemented flows;
- text remains readable under practical scaling;
- critical state is not color-only;
- focus/navigation order is logical where Unity UI supports it;
- progression is fully operable without a precision drag gesture;
- avoid mandatory motion-heavy transitions; reduced-motion-safe behavior is the default for this shell.

---

## 6. Workstream D — Development activity injector and runtime acceptance story

Expose a clearly development-only runtime surface that can drive deterministic synthetic walking/absence scenarios through the **existing** activity source seam.

Rules:

- the injector must call `GameSession.IngestFromSource` (therefore `IngestActivityBatch`) rather than `CreditActivity` or direct resource mutation;
- stable provider/source identities and timestamps must make replay deterministic and exactly-once;
- production/release configuration must be able to exclude or disable the injector cleanly;
- do not create a special Unity-only reward path, queue path, clock path, or persistence format;
- if controlled time is required to reproduce absence windows, introduce the narrowest test/development clock seam consistent with the existing `IClock` architecture and keep it out of production behavior.

The runtime acceptance flow must recreate the app/session from disk at meaningful boundaries and then replay an already-processed activity window, proving the UI reflects a no-op rather than merely trusting headless assertions.

---

## 7. Workstream E — Unity automated tests and runtime evidence

Add the strongest practical Unity-side coverage supported by the actual editor/toolchain.

### EditMode/integration coverage

At minimum cover:

- composition/bootstrap can instantiate the existing application stack without Unity-owned canonical state;
- save path/store/codec/migration wiring works from a temporary test location;
- Home/Projects/Region presenters/controllers consume snapshots and application results rather than mutable domain collections;
- development injector is isolated from production configuration;
- any compatibility adapter introduced for Unity is regression-tested.

### PlayMode/runtime coverage

Add a named M3-R runtime acceptance test/fixture that proves, as far as practical in PlayMode:

1. clean launch/new or known deterministic save;
2. Home renders canonical state;
3. player queues/chooses work through the UI/application boundary;
4. development activity enters via `IngestFromSource`;
5. session/process-equivalent recreation reloads persisted progress;
6. return summary remains available after restart until acknowledged;
7. Projects controls reorder/toggle/start correctly;
8. Region reflects landmark/producer state from read models;
9. replaying the same activity window credits nothing twice and creates no false new-progress summary;
10. final canonical state remains validator-clean.

Use temporary/test-specific persistence so automated runs never overwrite a developer's real save.

### Preserve headless regression evidence

The existing headless suite remains mandatory after Unity integration:

- repository guard/identity proof;
- `dotnet build SimpleWalkGame.sln`;
- full `dotnet test SimpleWalkGame.sln`;
- repository-documented M3 `walk --replay` / validation acceptance commands.

Run Unity import/compile and EditMode/PlayMode tests with the exact installed editor version. Record the exact commands/results in `docs/TESTING_AND_RELEASE.md` and in the final campaign outcome.

If hosted Unity CI is practical **without weakening secrets/licensing policy**, add or extend CI so the runtime tests can be reproduced. If licensing/environment prevents hosted Unity execution, keep current headless CI green and record Unity evidence as local runtime verification; do not call it hosted-CI verified.

No physical-device claim is allowed unless a real device run actually happened.

---

## 8. Cross-layer audit after integration

After the Unity slice works, perform a deliberate whole-repository impact review before declaring success.

Inspect at least:

- Unity assembly/package boundaries versus Domain/Application/Infrastructure dependency rules;
- save/migration behavior under Unity file-system paths and restart lifecycle;
- summary durability/ack timing;
- queue operations and stale UI snapshots after mutations;
- activity replay identity across runtime restarts;
- clock/timezone/UTC handling at the presentation boundary;
- producer/read-model state formatting;
- error and recovery paths;
- build artifacts/repository hygiene;
- headless simulator and tests after any shared-layer compatibility changes;
- documentation/evidence drift caused by the new runtime state.

Do not limit review to files added under `WalkGame.Unity`. A runtime-discovered defect may originate in shared code; conversely, do not rewrite shared code when the defect is only presentation misuse.

---

## 9. Documentation and evidence reconciliation

Before completion, update repository truth to match what **actually** landed.

At minimum reconcile:

- `README.md` — runtime-visible M3 state and exact evidence level;
- `docs/ROADMAP.md` — mark M3 runtime criteria complete only with named evidence;
- `docs/MASTER_PLAN.md` — if M3 is truly runtime-verified, make the next-campaign pointer advance to the next evidence-backed milestone (normally M4) but do not start it here;
- `docs/DECISIONS.md` — exact Unity 6 LTS version, package/integration strategy, and any material runtime constraint;
- `docs/TECHNICAL_ARCHITECTURE.md` — actual composition/assembly boundary;
- `docs/UX_DESIGN.md` — any clarified Home/Projects/Region runtime behavior;
- `docs/TESTING_AND_RELEASE.md` — exact Unity import/build/test commands and evidence distinctions;
- `docs/PERFORMANCE_BUDGETS.md` only if new runtime measurements/constraints are actually established;
- `docs/RISK_REGISTER.md` — runtime/lifecycle/toolchain risks exposed or mitigated;
- `.agent/EXECUTION_PROMPT.md` — append a concise execution outcome and change `ACTIVE` to `COMPLETED` only if every applicable M3-R gate is satisfied. If an external runtime/toolchain blocker remains, change status to `BLOCKED` with exact evidence and the first resumable gate.

Do not mark M4/M5/M6/M7 work complete merely because a placeholder exists.

---

## 10. Scope boundaries

Do **not** spend this campaign on:

- expanding Region 1 content volume/balance (M4);
- complete onboarding/settings/notifications/accessibility qualification (M5);
- 3D Visit World, character controller, camera, terrain, exploration, final art, shaders, or quality tiers (M6);
- Health Connect/HealthKit providers, permissions, background platform APIs, or device ingestion (M7);
- cloud sync, accounts, backend, social features, multiplayer, leaderboards, monetization, ads, or live-service systems;
- broad performance/device/release hardening unrelated to proving M3 runtime correctness;
- speculative abstractions/frameworks not demanded by the slice.

Preserve the M1–M3 trust invariants above all: deterministic canonical state, exactly-once activity crediting, integer economy math, atomic durability, dedup state never outrunning reward state, monotonic simulation checkpoints, durable bounded summaries, and presentation never becoming authoritative.

---

## 11. Completion gates

Do not declare M3-R complete until every applicable item is supported by evidence:

- [ ] Exact Unity 6 LTS editor version is recorded and the project imports/compiles cleanly.
- [ ] `src/WalkGame.Unity` (or justified equivalent) exists without generated/cache pollution.
- [ ] Unity consumes existing Domain/Application/Infrastructure behavior without duplicated canonical logic.
- [ ] One explicit composition root wires save/codec/migrations/clock/content/session and development-only injector correctly.
- [ ] Home, Projects, and lightweight Region runtime surfaces exist and use application/read-model boundaries only.
- [ ] Queue operations, reorder, auto-advance toggle, and manual start work through application use cases with visible invalid-action handling.
- [ ] Pending return summary survives runtime restart and acknowledgement is idempotent/progression-neutral.
- [ ] Development activity enters through `IActivityRecordSource` → `IngestFromSource` → `IngestActivityBatch`, never direct credit.
- [ ] Runtime/session recreation loads durable progress rather than scene/static shadow state.
- [ ] Replaying the same activity window after restart produces no duplicate Vitality/world progress or fabricated new summary.
- [ ] EditMode/integration coverage for the composition boundary is green.
- [ ] PlayMode/runtime acceptance coverage for the core Home → Projects → Region → restart/replay story is green.
- [ ] Existing headless build/test/M3 replay acceptance remains green with no unresolved Critical/High regression.
- [ ] Documentation accurately distinguishes automated, runtime, hosted-CI, and device verification.
- [ ] No M4+ scope was used to hide an incomplete M3 runtime integration.

If Unity is genuinely unavailable, do not falsify completion and do not manufacture editor files. Record the blocker and stop at the first runtime gate.

---

## 12. Git, handoff, and finish protocol

Follow `AGENTS.md` and `docs/AGENT_EXECUTION_GUIDE.md` exactly.

Before final integration/push:

1. inspect `git status`, staged/unstaged diff, recent log, and generated/untracked files;
2. exclude Unity caches, generated output, local-machine files, secrets, credentials, and unrelated work;
3. rerun all applicable completion gates on the final integrated tree;
4. fetch/reconcile `origin/main` deliberately; never force-push or discard a remote advance;
5. ensure completed code, Unity project/config, tests, tooling, and docs are committed;
6. use detailed logical commit messages. The final handoff/integration commit must summarize delivered runtime behavior, root causes/gaps fixed, runtime/toolchain decisions, exact validation evidence, remaining unverified device/hosted gates, and intentional deferrals;
7. push completed work to `origin/main`;
8. verify local `HEAD` equals `origin/main` and the working tree is clean;
9. inspect hosted CI/workflow status for that exact pushed SHA and fix locally actionable failures;
10. release the writer lease on normal completion.

Do not finish by printing another giant prompt for the operator to paste. The durable repository state is the handoff.

If M3-R genuinely satisfies every applicable gate, stop after final pushed verification. Do **not** automatically begin M4 in the same campaign.

---

## 13. Execution outcome

**BLOCKED at Gate A1 (runtime/toolchain qualification) — 2026-08-26.**

### What ran

- Mandatory preflight completed: repository identity guard printed OK
  (`quantdale/simple-walk-game`), starting SHA recorded (`7aeae185`, then fast-forward
  pulled to `3517eda` = this campaign's planning commit), single-writer lease acquired.
- Gate A1 executed against the real machine before any project generation.

### Blocker evidence (Windows host, 2026-08-26)

No Unity editor of any version is installed; Unity Hub is installed but has never been
configured/run:

- `C:\Program Files\Unity\Hub\Editor\` — MISSING (standard Hub editor root)
- `C:\Program Files\Unity\` — MISSING (non-Hub editor location)
- `C:\Program Files\Unity Hub\Unity Hub.exe` — PRESENT, but `%APPDATA%\UnityHub\`
  does not exist (no `editors-v2.json`, no `secondaryInstallPath.json`) → Hub never run,
  zero editors ever installed through it
- Registry `HKLM:\SOFTWARE\Unity Technologies*` — no entries
- No `Unity*` directories on `D:\` or `E:\`; `where.exe Unity.exe` — not found on PATH

Per campaign §3-A1 and §11: no compatible editor ⇒ do **not** hand-author unverifiable
Unity YAML/project files merely to appear productive. Nothing was generated; headless
code, tests, tooling, and docs are untouched by this session.

### Exact resumable state

- **First resumable gate:** A1/A2 — requires an installed Unity 6 LTS editor.
- **Operator action needed:** install a Unity 6 LTS editor via Unity Hub (requires
  interactive licensing/sign-in) on this machine, then re-activate this campaign. All
  downstream gates (A2 bootstrap → B composition → C presentation → D injector → E
  runtime tests) remain pending behind it and were not started.
- This repeats the environmental fact already recorded in D-035 and README; the campaign
  cannot convert it into completion without falsifying evidence (D-018).

### Verification performed

- `dotnet build SimpleWalkGame.sln` — PASS on final tree (docs-only change).
- `dotnet test SimpleWalkGame.sln` — PASS, all suites green (docs-only change; headless
  evidence base unchanged).
