# Active Execution Campaign — M4-H Region 1 Content Systems + Headless Qualification

**Status:** ACTIVE  
**Planned-From:** `f80127e035c6bd9f8fa8ce810687d02ee00bf8f0`  
**Target branch:** `main`  
**Campaign class:** IMPLEMENTATION + CONTENT QUALIFICATION (HEADLESS)  
**Primary roadmap target:** M4 — Region 1 content production  
**Target size:** one substantial integrated campaign, approximately 8–12 focused hours if the work remains coherent. Do not pad the session or split it into artificial micro-campaigns.

---

## 0. Operating mandate

Continue from the repository's **actual current state**, not from assumptions in this prompt.

Before any write, execute the mandatory `AGENTS.md` repository-identity / fetch / starting-SHA / writer-lease preflight exactly as written there. This campaign inherits that contract. If the preflight fails, stop and report rather than modifying anything.

Then:

1. Read `AGENTS.md`, `docs/AGENT_EXECUTION_GUIDE.md`, `.agent/PLANNER_HANDOFF.md`, this campaign, `README.md`, `docs/MASTER_PLAN.md`, `docs/ROADMAP.md`, `docs/PRODUCT_SPEC.md`, `docs/GAME_SYSTEMS.md`, `docs/WORLD_AND_CONTENT.md`, `docs/TECHNICAL_ARCHITECTURE.md`, `docs/UX_DESIGN.md`, `docs/TESTING_AND_RELEASE.md`, `docs/DECISIONS.md`, and `docs/RISK_REGISTER.md` before architectural changes.
2. Inspect the complete implementation/test/tooling tree, recent commits since `Planned-From`, open issues/PRs, hosted CI state, and any native runtime state that is actually relevant. Do not review only recently changed files.
3. Build a fresh campaign ledger. Classify findings as **LANDED/TRUSTED**, **M4 IMPLEMENTATION GAP**, **M4 VALIDATION GAP**, **M3-R EXTERNAL BLOCKER**, **NEW SAME-DOMAIN DEFECT**, or **STALE/SUPERSEDED**.
4. Preserve unrelated user work. Never reset, clean away, overwrite, or force-push other work to make integration convenient.
5. Keep the repository buildable at meaningful checkpoints. If isolated worktrees/branches are required by `AGENTS.md`, integrate accepted work back into `main` before completion.
6. Fix every Critical/High regression introduced or exposed by this campaign before completion. Record lower-severity unrelated findings precisely and defer them rather than expanding scope without limit.
7. During implementation, prefer focused tests around the work being changed. Run the full certification suite only at meaningful integration boundaries and at the end.

This is an **implementation-heavy headless M4 campaign**. It exists because the previous M3-R Unity runtime campaign is blocked externally at Gate A1: no Unity 6 LTS editor exists in that execution environment. That blocker remains truthful and unresolved. This campaign must not falsify or erase it.

The scheduling rule for this campaign is:

> Advance only M4 work that is valid, testable, and evidence-backed without Unity. Do not claim M3-R runtime verification, do not manufacture Unity assets, and do not let the external editor blocker freeze independent content/domain/simulation work.

---

## 1. Repository truth at planning time

The planner audited current `main` at `f80127e035c6bd9f8fa8ce810687d02ee00bf8f0`, recent history, the full tree, roadmap/master plan, the current execution handoff, and open PR state before activating this campaign.

Current evidence:

- M1 deterministic core, M2 trust pipeline, and the headless portion of M3 are implemented and automated-verified.
- The latest M3-R attempt stopped correctly at runtime Gate A1 because no Unity 6 LTS editor was installed. It did **not** change shared implementation code.
- Headless baseline at the blocker commit: `dotnet build SimpleWalkGame.sln` PASS and `dotnet test SimpleWalkGame.sln` PASS, 156/156 tests (Domain 89 / Infrastructure 23 / Application 44).
- No open pull request currently carries unfinished work for this repository.
- `Region1Catalog` is still explicitly a development seed, not final M4 content: **5 projects, 3 landmarks, 1 producer**.
- `RegionDefinition` currently owns only Projects, Landmarks, and Producers.
- Strong ID marker kinds for `DiscoveryId` and `ExpeditionId` already exist, but there are no landed discovery/expedition domain models or Region 1 definitions using them.
- `ContentValidator` currently checks basic duplicate IDs, prerequisite references, landmark/producer unlock references, existence of an entry project, and project-cycle detection. It does not yet qualify the full M4 content contract.
- `GameState` save schema is currently **v2**. Any new canonical persisted state that cannot be safely additive must bump the schema and ship a registered migration plus roundtrip/upgrade evidence.
- `docs/WORLD_AND_CONTENT.md` requires Region 1 to become a complete headless-validatable content package: 5–7 project chains, 12–20 meaningful project nodes, 6+ landmarks, 2+ producers, 10+ discoveries, 3+ expedition objectives/routes, ecological and settlement progression, a closure milestone, post-completion state, reference validation, and deterministic progression reports.
- M4 roadmap exit criteria are headless-friendly: critical path reachable, no dependency cycles, pacing across representative profiles, low foreground-decision pressure, presentation requirements documented, and Region 1 completable headlessly.

Treat the proven M1–M3 contracts as trusted starting evidence. If M4 exposes a genuine cross-layer defect, fix it at the correct layer and add regression coverage. Do not rewrite the trust pipeline, reward ledger, persistence architecture, queue semantics, or producer semantics without evidence.

---

## 2. Campaign objective

Turn Region 1 from a five-node development seed into a coherent, production-oriented **headless content system** that can be validated, simulated, persisted, completed, and audited without Unity.

By the end of this campaign, a clean deterministic profile must be able to progress Region 1 from the degraded starting state through a substantial restoration arc using the existing activity/reward pipeline, while the repository can prove all of the following:

- the full project dependency graph is valid and reachable;
- major landmarks advance through canonical restoration stages;
- producers unlock and behave within existing deterministic rules;
- discoveries unlock from durable canonical triggers and preserve provenance/review state;
- expedition objectives/routes have stable definitions and deterministic availability/completion hooks appropriate to M4;
- ecology and settlement progression are represented canonically at a deliberately simple abstraction level;
- the region reaches an explicit closure/completion milestone;
- post-completion state is stable and does not reset the region;
- replaying already-processed activity cannot create duplicate progression;
- representative low/moderate/high/irregular profiles produce analyzable pacing reports;
- every M4 exit criterion that does not require Unity is supported by named automated evidence.

This campaign should leave later presentation work with a data contract to bind to, not a pile of presentation-owned assumptions.

---

## 3. Workstream A — Content contract and model expansion

Audit the current `RegionDefinition`, `ProjectDefinition`, `RegionState`, `GameState`, validators, simulation events, read models, and save codec before adding types.

Introduce only the smallest coherent model extensions required for M4.

### A1. Versioned authored content

Evolve authored content toward an explicit, testable schema consistent with `WORLD_AND_CONTENT.md`.

At minimum, every meaningful authored definition must have stable identity and enough metadata for deterministic validation. Where appropriate, add:

- region identity;
- prerequisite/trigger references;
- title/description/presentation/localization keys instead of hard-wiring final player copy into canonical logic;
- content version or equivalent versioned authoring contract;
- documented stage/unlock effects;
- explicit critical-path / optional-branch semantics where validation requires it.

Do not build a generic content engine merely because one could exist. Optimize for Region 1 plus safe future extension.

### A2. Discoveries

Add a minimal but durable discovery model with the semantics already required by the content spec:

- stable `DiscoveryId`;
- category;
- title/body keys;
- deterministic unlock trigger;
- provenance data/text key;
- reviewed state separate from unlocked state;
- optional location/presentation metadata without Unity dependencies.

Discovery unlocks must be derived from canonical events/state and must be idempotent. Re-loading or replaying history must not duplicate unlocks or review transitions.

### A3. Expeditions

Add the smallest M4-appropriate expedition/objective model needed to author at least three routes/objectives and prove reference integrity.

Do **not** turn M4 into a complete foreground expedition gameplay system if that belongs later. M4 needs stable definitions, deterministic availability/unlock/completion hooks, rewards/effects only where existing architecture can support them safely, and clear presentation requirements for M5/M6.

If the correct M4 boundary is “definition + availability/completion state + deterministic simulation hook” rather than a full interactive mechanic, choose that boundary and document it.

### A4. Region progression axes and completion

Implement a simple canonical representation for the Region 1 restoration arc. At minimum cover:

- ecological progression;
- settlement/hub progression;
- explicit region completion/closure milestone;
- post-completion/evergreen state that does not reset completed world state.

Prefer discrete, explainable stages over speculative continuous simulation.

The new state must remain reconstructable, persistable, validator-clean, and presentation-independent.

---

## 4. Workstream B — Full Region 1 authored graph

Replace the current five-project development seed with a coherent Region 1 graph. Preserve stable IDs already used by tests/save fixtures unless an evidence-backed migration strategy makes a change necessary.

Target the documented content minimum without padding weak nodes:

- **5–7 major restoration chains**;
- **12–20 meaningful projects** total;
- **6+ major landmarks**;
- **2+ producer/infrastructure systems**;
- **10+ discoveries** with deterministic provenance-bearing unlocks;
- **3+ expedition objectives/routes**;
- one region-level ecological progression arc;
- one settlement/hub progression arc;
- one strong region closure milestone;
- a stable post-completion state.

Use the existing Millbrook Valley seed as the starting fiction unless repository docs provide a stronger current direction.

### B1. Chain quality

Every major chain must represent a transformation story rather than repeated numeric gates. Use dependency relationships that produce visible/systemic consequences, for example access → stabilization → restored function → dependent ecosystem/community payoff.

### B2. Dependency design

Require:

- at least one reachable entry path from a clean profile;
- no hidden deadlock;
- no cycles;
- optional branches cannot block the critical path accidentally;
- cross-chain dependencies are understandable and intentional;
- the queue can always make progress when a legitimate available project exists.

### B3. Presentation contract without Unity

For every major landmark/stage and project outcome, author enough presentation metadata/requirements that a later Unity campaign can bind canonical state to visuals without reverse-engineering game logic.

This is documentation/data-contract work only. Do not create scenes, prefabs, meshes, materials, animation controllers, or hand-written Unity YAML.

---

## 5. Workstream C — Deterministic progression integration

Wire the expanded authored content into canonical progression without creating a parallel rules engine.

Project completion and world advancement must produce deterministic consequences at the existing domain/application boundaries.

Cover at least:

- project prerequisite unlocking;
- landmark stage advancement;
- producer unlocks;
- ecology/settlement stage updates;
- discovery unlocks;
- expedition availability/completion hooks where applicable;
- region completion detection;
- post-completion behavior;
- return-summary/event visibility for major changes where the existing summary architecture can represent them coherently.

Do not bypass `GameSession`, the trust pipeline, reward ledger, or offline advancement with content-specific shortcuts.

If new event types are required, keep them typed, deterministic, bounded, and useful for summaries/tests/diagnostics. Avoid emitting high-volume noise for every tiny state change.

---

## 6. Workstream D — Persistence and migration safety

Audit whether new canonical Region 1 state can be added safely under schema v2.

If persisted semantics require a schema bump:

- increment `SchemaVersions.Current` deliberately;
- add a registered migration from the previous schema;
- define deterministic defaults for old saves;
- preserve already-earned project/landmark/producer progress;
- prove decode → migrate → validate → encode stability;
- add representative v2 fixtures or explicit programmatic migration cases;
- verify backup/recovery behavior remains correct.

Never silently reinterpret old canonical data in a way that grants or destroys progression.

If additive fields legitimately require no migration, document why and add backward-decoding tests proving the default semantics are correct.

---

## 7. Workstream E — Content validation as a release gate

Expand `ContentValidator` or introduce a narrowly scoped validator layer so invalid Region 1 content fails fast before runtime.

At minimum prove:

- all stable IDs are unique within their entity kind;
- all project prerequisite references resolve;
- no project dependency cycles;
- an entry path exists;
- the designated critical path is reachable to the closure milestone;
- optional branches cannot be mandatory by accidental reference;
- landmark stage trigger references resolve and stages are monotonic;
- producer unlock references resolve and capacities/rates remain representable;
- discovery trigger references resolve;
- expedition references/rewards/effects resolve;
- content/localization/presentation keys required by the new schema are non-empty and structurally valid;
- every major canonical landmark state has documented presentation binding requirements;
- region completion conditions are satisfiable;
- no impossible or overflow-prone resource/progress requirement exists;
- the final authored Region 1 definition itself validates with zero violations.

Add red-team tests for malformed content, not only a happy-path catalog test.

Do not make validation order-dependent. The current prerequisite validation checks references while accumulating project IDs; fix any behavior that incorrectly rejects a valid forward reference.

---

## 8. Workstream F — Deterministic Region 1 simulation and pacing reports

Extend the existing headless tooling instead of creating a separate simulator.

Add a reproducible command/report that can run Region 1 from clean state through completion using representative activity profiles:

- low;
- moderate;
- high;
- irregular/bursty.

The simulator must use canonical application/domain paths. It must not directly set completion flags merely to reach the end.

Produce machine-readable or stable text evidence for at least:

- activity/Vitality required per major project or chain;
- region completion range per profile;
- bottleneck resources/producers;
- queue-empty frequency;
- capped/idle producer time where measurable;
- discovery unlock pacing;
- expedition availability/completion pacing where implemented;
- number of required foreground decisions;
- final ecological/settlement stages;
- closure milestone reached;
- validator-clean final state.

Add deterministic replay evidence: identical profile + seed + content must produce the same meaningful final canonical state/report, and replaying already-trusted activity must not duplicate rewards/world progress.

Do not invent arbitrary “good pacing” thresholds just to make tests pass. If tuning targets are not yet specified, report the measured distributions, document the chosen provisional targets, and make obviously pathological outcomes fail.

---

## 9. Workstream G — Automated acceptance evidence

Add named tests that make M4 completion auditable.

Expected coverage includes:

### Domain/content tests

- full Region 1 graph validates;
- forward prerequisite references validate correctly;
- cycles/missing refs/duplicate IDs fail;
- critical path is reachable;
- discoveries unlock once and review state is independent;
- expedition availability/completion hooks are deterministic;
- ecology/settlement stages advance monotonically;
- region completion is idempotent;
- post-completion state remains stable;
- producer bounds remain unchanged under the larger graph.

### Persistence tests

- new state roundtrips;
- old-save compatibility/migration works;
- migrated state remains validator-clean;
- deterministic re-encode remains stable where existing policy requires it.

### Application/integration acceptance

Create a named M4 Region 1 acceptance test that drives a clean profile through the actual trust/progression stack and proves:

1. initial graph is valid and exposes reachable work;
2. representative activity enters through the normal ingestion/application path;
3. queued work crosses multiple completion boundaries correctly;
4. at least several landmark stages change;
5. both producer/infrastructure systems unlock and remain bounded;
6. discoveries unlock at intended milestones without duplicates;
7. expedition hooks become available/complete deterministically;
8. ecological and settlement stages progress;
9. the region closure milestone is reached;
10. the game is persisted/reloaded at meaningful boundaries;
11. replaying already-processed activity is a no-op for reward and world progression;
12. post-completion state remains stable after another advance/reload;
13. final state and content validators are clean.

Keep the acceptance test deterministic and diagnosable; avoid one giant opaque assertion.

---

## 10. Workstream H — Documentation and evidence reconciliation

Before completion, update repository truth to exactly match what landed.

At minimum reconcile:

- `README.md` — actual M4 headless implementation/evidence, and M3-R still externally blocked if Unity remains unavailable;
- `docs/ROADMAP.md` — check only M4 exit criteria supported by named evidence;
- `docs/MASTER_PLAN.md` — record the dependency-safe scheduling pivot: M4 headless work advanced while M3-R runtime qualification remained externally blocked; do not rewrite history as if M3-R completed;
- `docs/WORLD_AND_CONTENT.md` — actual authored schema, Region 1 composition, presentation contract, simulation/pacing evidence;
- `docs/GAME_SYSTEMS.md` — any new canonical discovery/expedition/region-progression rules;
- `docs/TECHNICAL_ARCHITECTURE.md` — actual content/state/validation boundaries;
- `docs/UX_DESIGN.md` — presentation requirements implied by discoveries/expeditions/region completion, without claiming implemented Unity screens;
- `docs/TESTING_AND_RELEASE.md` — exact validation/simulation commands and named M4 automated evidence;
- `docs/DECISIONS.md` — durable choices about content schema, discovery/expedition boundary, completion model, migrations, and pacing methodology;
- `docs/RISK_REGISTER.md` — content deadlock, pacing, migration, and scope risks exposed/mitigated;
- `.agent/EXECUTION_PROMPT.md` — append a concise execution outcome and change `ACTIVE` to `COMPLETED` only if every applicable headless M4 gate is satisfied. If implementation work is blocked, set `BLOCKED` with exact evidence and first resumable gate.

Do not mark Unity runtime/device evidence complete unless it actually ran.

---

## 11. Scope boundaries

Do **not** spend this campaign on:

- installing or configuring Unity merely to unblock M3-R;
- Unity scenes, prefabs, hand-authored YAML, final art, shaders, 3D world, character controller, camera, traversal, or Visit World (M6);
- full mobile onboarding/settings/notification/accessibility implementation (M5);
- Health Connect/HealthKit, permissions, background platform APIs, or physical-device ingestion (M7);
- cloud sync, accounts, backend, multiplayer, social systems, monetization, ads, or live-service infrastructure;
- Region 2;
- speculative ECS/content frameworks or generic authoring platforms;
- broad performance benchmarking unrelated to deterministic content simulation;
- rewriting proven M1–M3 semantics without evidence.

M4 may add **presentation requirements/keys** needed for later runtime work, but presentation itself remains out of scope.

---

## 12. Completion gates

This campaign is complete only when all applicable gates below are satisfied:

1. Repository identity/lease/reconciliation policy was followed.
2. The final authored Region 1 meets the documented M4 content scale without padding.
3. The final content graph validates with zero violations.
4. Critical path reaches the explicit region closure milestone from a clean state.
5. No dependency cycle/deadlock remains.
6. Discoveries and expedition hooks have durable deterministic semantics and validation.
7. Ecology/settlement progression and post-completion state are canonical and tested.
8. Save compatibility/migration evidence covers every new persisted semantic.
9. Representative low/moderate/high/irregular simulations complete or produce an explicitly justified result, with deterministic reports.
10. Replay/exactly-once behavior remains intact.
11. Focused new tests pass.
12. `dotnet build SimpleWalkGame.sln` passes.
13. `dotnet test SimpleWalkGame.sln` passes in full.
14. Existing guard/identity proof and repository-documented simulation/validation gates remain green.
15. Relevant docs match the final implementation and distinguish HEADLESS/AUTOMATED VERIFIED from RUNTIME VERIFIED.
16. Every introduced Critical/High defect is fixed.
17. Intended work is committed and pushed to `origin/main` without force-push/history rewrite.
18. Final local `main` equals `origin/main` and the working tree is clean.
19. Hosted CI for the final pushed SHA is inspected. If it fails for an implementation-addressable reason, fix and push until green; if externally blocked, record exact evidence without pretending success.
20. `.agent/EXECUTION_PROMPT.md` records the final outcome and no stale superseded campaign is left ACTIVE.

A campaign is not complete because “most content exists.” It is complete when the repository can **prove** Region 1 is structurally valid, deterministic, persistable, completable headlessly, and ready for later presentation binding.

---

## 13. Git and reporting contract

Use the repository's `AGENTS.md` policy as authoritative.

In addition:

- Start from current `main`; fetch/reconcile before implementation.
- Never force-push or rewrite shared history.
- Use focused implementation commits with meaningful messages, but do not fragment trivial edits into noise.
- Preserve unrelated user work.
- Before final push, inspect the full diff for generated files, machine-local state, secrets, and accidental scope creep.
- Final commit/report must state: start SHA, final SHA, major systems changed, schema/migration effect, new test counts/evidence, exact simulation commands/results, remaining blockers/deferrals, CI result, and whether M3-R Unity qualification remains externally blocked.
- Finish on `main`, push to `origin/main`, and verify exact SHA equality.

---

## 14. Stop conditions

Stop and record a precise `BLOCKED` state instead of fabricating evidence if:

- repository identity/lease safeguards fail;
- required source truth is contradictory enough that a safe model cannot be chosen without a product decision;
- a migration cannot preserve existing canonical progress safely;
- content completion would require redefining a proven trust/reward invariant without evidence;
- an external service/tool is required for a gate that cannot be reproduced headlessly.

The absence of Unity is **not** itself a stop condition for this M4-H campaign, because Unity work is explicitly outside scope. It remains a separate M3-R blocker.

When the headless M4 campaign is complete, stop. Do not automatically begin M5, M6, M7, hardening, or the blocked M3-R runtime campaign in the same session.

---

## 15. Execution outcome (recorded by the executing session)

**Outcome:** COMPLETED. Every applicable headless M4-H gate was satisfied; the only external blocker (no Unity editor -> M3-R runtime qualification) remains truthful, unchanged, and outside this campaign scope.

- **Start SHA:** `27a6f5801f64189428c24075bd38d9a3aa8bc005` (plan activation) - **Final SHA:** `25bf731ed68885dbad61025fc45a6cab76a94e4e` (= `origin/main`, pushed without force-push; local == remote; tree clean).
- **Landed (d673aa5 -> 25bf731):** WS-A/B domain models (discoveries, expeditions, progression arcs, closure milestone) + full Millbrook Valley authored graph (content v2: 19 projects / 6 chains / 6 landmarks / 3 producers / 13 discoveries / 3 expeditions; five seed definitions preserved verbatim); WS-C application integration (read models, review op, summary visibility); WS-E validator release gate incl. forward-reference defect fix + red-team suite; WS-G named acceptance proof `M4Region1AcceptanceTests`; WS-D additive schema-v2 backward-decoding evidence (D-036); WS-F deterministic `profile` pacing harness with committed reports (`docs/evidence/m4/`); WS-H documentation reconciliation across README, ROADMAP, MASTER_PLAN, WORLD_AND_CONTENT, GAME_SYSTEMS, TECHNICAL_ARCHITECTURE, UX_DESIGN, TESTING_AND_RELEASE, DECISIONS (D-036..D-039), RISK_REGISTER.
- **Verification:** `dotnet build` clean; `dotnet test` 180/180 (Domain 101 / Infrastructure 25 / Application 54); AGENTS.md simulation smoke green; completed-region `validate --selftest` PASS (violations=0); walk replay of 16 windows credited 0 (16 duplicates ignored); guard proof suite 25/25; hosted CI **success** on final SHA `25bf731` (run 32922198325).
- **Pacing evidence (D-039):** high d97 / irregular d139 / moderate d242 / low = documented long tail (62% vitality at d400); foreground pressure: exactly 19 queue decisions, <=1 queue-empty day, 0 capped-store days per profile.
- **Schema effect:** none - additive v2 fields with absent-means-default decoding (D-036), proven by strip-and-redecode tests.
- **Remaining blockers / deferrals:** M3-R Unity runtime qualification stays externally blocked (Gate A1: no Unity 6 LTS editor); low-profile completion beyond one year recorded as accepted pacing characteristic (D-039); discovery trigger models beyond project-completion deferred per D-037.
- **Lease note:** preflight found a stale lease from dead session `pid-45188`; released via operator-authorized `writer-lease.ps1 -Release -Force` after PID-liveness verification, then acquired normally. No history rewrites occurred.
