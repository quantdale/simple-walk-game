# Active Execution Campaign — M5-H1 Headless UX State Contracts + Mobile-Shell Readiness

**Status:** ACTIVE  
**Planned-From:** `d73183497a6d2ca3f7845cfee1697d1faeff7c5d` (= `origin/main` at planner audit)  
**Target branch:** `main`  
**Campaign class:** IMPLEMENTATION + HEADLESS QUALIFICATION + UX CONTRACT HARDENING  
**Primary roadmap target:** M5 — Mobile shell and low-attention UX  
**Target size:** one substantial integrated campaign, approximately 8–12 focused hours. Do not pad the session or split it into artificial micro-campaigns.  
**Dependency note:** D-035 remains open: no Unity 6 LTS editor has been available in prior execution environments. **Do not re-run the already-proven Unity Gate A1 loop in this campaign.** This campaign exists specifically because it is useful, implementation-ready M5 work that can be completed and proven headlessly without fabricating runtime evidence.

---

## 0. Operating mandate

Continue from the repository's **actual current state**, not from assumptions in this prompt.

Before any write, execute the mandatory `AGENTS.md` repository-identity / fetch / starting-SHA / writer-lease preflight exactly as written there. This campaign inherits that contract. If the preflight fails, stop and report rather than modifying anything.

Then:

1. Read `AGENTS.md`, `docs/AGENT_EXECUTION_GUIDE.md`, `.agent/PLANNER_HANDOFF.md`, this campaign, `README.md`, `docs/MASTER_PLAN.md`, `docs/ROADMAP.md`, `docs/PRODUCT_SPEC.md`, `docs/GAME_SYSTEMS.md`, `docs/TECHNICAL_ARCHITECTURE.md`, `docs/UX_DESIGN.md`, `docs/ACTIVITY_PIPELINE.md`, `docs/TESTING_AND_RELEASE.md`, `docs/PERFORMANCE_BUDGETS.md`, `docs/DECISIONS.md`, and `docs/RISK_REGISTER.md` before architectural changes.
2. Inspect the **complete** Domain/Application/Infrastructure/test/tooling tree, not only recently changed files. Inspect all commits since `Planned-From`, open issues/PRs, hosted CI, and any environment facts that materially affect this campaign.
3. Build a fresh campaign ledger. Classify findings as **LANDED/TRUSTED**, **M5 CONTRACT GAP**, **PERSISTENCE/OWNERSHIP GAP**, **NEW SAME-DOMAIN DEFECT**, **RUNTIME-ONLY GAP**, **PLATFORM-ONLY GAP**, **EXTERNAL BLOCKER**, or **STALE/SUPERSEDED**.
4. Preserve unrelated user work. Never reset, clean away, overwrite, or force-push other work to make integration convenient.
5. Keep the repository buildable at meaningful checkpoints. If isolated worktrees/branches are required by `AGENTS.md`, integrate accepted work back into `main` before completion.
6. Fix every Critical/High regression introduced or exposed by this campaign before completion. Record lower-severity unrelated findings precisely and defer them rather than expanding scope without limit.
7. During implementation, use focused tests around affected layers. Run the full headless suite, guard proof suite, simulation smoke, and named campaign acceptance scenarios at integration boundaries and at the end.

The governing rule is:

> Make M5's ordinary mobile experience explicit, deterministic, persistable, diagnosable, and presentation-ready **without putting game rules in presentation and without pretending Unity/native behavior has been verified**.

---

## 1. Repository truth at planning time

The planner audited current `main` at `d73183497a6d2ca3f7845cfee1697d1faeff7c5d`, recent commits, current campaign state, roadmap/master plan, current source/read-model structure, open PR/issue state, and the last completed evidence package before activating this campaign.

Current evidence:

- M1 deterministic core and durable state are automated-verified.
- M2 activity trust pipeline is automated-verified with durable deduplication, corrections/deletions, bounded reconciliation, atomic checkpoint/reward persistence, and replay safety.
- Headless M3 ambient progression is automated-verified.
- M4-H Region 1 content is automated-verified: 19 projects / 6 restoration chains / 6 landmarks / 3 producers / 13 discoveries / 3 expeditions, ecology + settlement arcs, closure milestone, post-completion stability.
- M8-H1 is **COMPLETED**. It raised the baseline to **221 automated tests** and added persistence hostile-path, mature-save/migration, activity red-team, temporal-anomaly, seeded property, long-horizon/performance, and end-to-end recovery/replay evidence.
- M8-H1 fixed the only discovered High defect in that campaign: recovery re-commit could displace the last healthy generation. Recovery now preserves the valid backup generation (D-040).
- Current Application presentation-safe surfaces already include `HomeReadModel`, `ProjectsReadModel`, `RegionReadModel`, `DiscoveriesReadModel`, `ExpeditionsReadModel`, and `ReturnSummaryReadModel`.
- There is still no verified Unity presentation project on `main`. D-035 remains truthful and open.
- M5 remains broadly unimplemented: onboarding, activity/permission status UX contract, settings/preferences, development/support diagnostics, notification preferences, reduced-motion configuration, explicit normal/empty/error state projections, and low-attention shell acceptance are not yet represented as a coherent platform-neutral application contract.
- The roadmap still has M5, M6, M7, the runtime/device half of M8, and M9 open.
- There were **no open issues and no open PRs** carrying unfinished work at planning time.
- The M8-H1 implementation SHA `5505d6858df0fc0f2377e6716a518e4207cff1dd` had hosted `ci` success (run `32934471429`) as recorded by the completed campaign. The prompt-only outcome commit followed it.

Do not reopen proven M1–M4/M8-H1 semantics without evidence of an actual defect. Do not grow more headless trust machinery merely because Unity remains blocked. The next value is to finish the platform-neutral M5 contract that presentation will consume.

---

## 2. Campaign objective

Implement and qualify the missing **platform-neutral UX/application state contracts for the ordinary mobile shell** so a future Unity-enabled campaign can bind real screens instead of inventing state ownership, persistence, error semantics, or support diagnostics inside `MonoBehaviour`s.

By the end of this campaign, a deterministic headless profile must be able to execute and inspect the following product stories through real Application/Infrastructure boundaries:

`first run → onboarding state explains premise/activity benefit → permission/source state is represented safely → first project selection uses existing canonical use cases → preferences persist → player leaves → activity/progression occurs → return after 1/7/30 days → shell-facing read models explain what changed + what is active + whether action is required → queue-empty / no-data / source-unavailable / permission-denied-or-revoked / save-recovery states are explicit → support diagnostics explain the technical state separately → restarting preserves all durable user choices → reading/changing presentation preferences never mutates earned progression → replayed activity remains exactly-once`

This campaign should leave Unity with a narrow job later: render typed state, invoke application operations, bind native permission/source adapters, and provide accessibility semantics. Unity must not have to invent canonical state or duplicate business rules.

---

## 3. Workstream A — Ownership audit and M5 state model

Before adding types, perform a whole-codebase ownership audit for every proposed M5 datum.

Classify each datum as one of:

- **canonical progression state** — belongs to the durable game state only if it affects game rules/progress;
- **durable local UX preference** — persists locally but must not become canonical progression authority;
- **derived application/read-model state** — computed from canonical state + adapter status and never persisted redundantly;
- **ephemeral platform state** — owned by an adapter/native system and projected through a narrow interface;
- **diagnostic evidence** — bounded/support-oriented and privacy-safe.

Do not put presentation-only flags into `GameState` merely because that is convenient. Do not create a second shadow copy of canonical project/activity/world state in a settings store.

If persistence ownership requires a new architectural decision, record it in `docs/DECISIONS.md` with consequences and migration/recovery implications.

Required M5 state concerns to resolve deliberately:

- onboarding progress/completion and resumability;
- reduced-motion preference;
- haptics preference;
- audio preference if the docs/current architecture justify it;
- notification category preferences and quiet-hours preference **only as local configuration** — no native notification delivery in this campaign;
- activity connection/source status projection;
- permission-needed / denied / revoked states;
- last successful activity reconciliation status useful to players/support;
- development/support diagnostics visibility preference if needed;
- any additional settings already explicitly required by source-of-truth docs.

Avoid a generic settings framework. Implement only requirements justified by M5 docs and acceptance scenarios.

---

## 4. Workstream B — Durable local preferences + onboarding contract

Implement a small, versioned, deterministic local UX-preferences/onboarding persistence contract in the correct layer.

Requirements:

- explicit defaults;
- versioned serialization if a separate persisted record is introduced;
- atomic write/recovery semantics appropriate to the value of the data;
- malformed/future-version behavior defined and tested;
- no silent corruption-driven mutation of canonical progress;
- restart preserves user choices;
- preferences can be changed repeatedly/idempotently;
- preference writes cannot change Vitality, reward ledgers, processed activity, queue/project state, content completion, summaries, or canonical checkpoints;
- onboarding can resume after interruption without duplicating rewards or silently auto-completing gameplay choices;
- starting/selecting the first project must route through existing Application/domain operations rather than an onboarding-only shortcut;
- onboarding state must tolerate permission denial and still allow safe navigation/exploration as required by `UX_DESIGN.md`.

If the existing save envelope is objectively the right owner for some item, prove why. If a separate local preferences store is chosen, do not clone the main save system mechanically; keep the design proportionate and tested.

---

## 5. Workstream C — Activity connection + permission status projection

M5 needs understandable player-facing activity state before M7 provides real Health Connect/HealthKit adapters.

Introduce a narrow platform-neutral status contract that can later be implemented by native adapters without changing the downstream trust pipeline.

Required player-facing states from `UX_DESIGN.md`:

- connected and current;
- permission needed;
- permission denied/revoked;
- source unavailable;
- waiting for first data;
- temporarily unable to refresh;
- data processed successfully.

Rules:

- raw exceptions/messages must not become ordinary player copy;
- technical detail may be available in a separate diagnostics projection;
- source/permission status must not itself award, revoke, replay, or mutate game progression;
- permission state changes outside the app must be representable cleanly;
- no Health Connect/HealthKit SDK, Android/iOS native project, or fake device verification is allowed here;
- test doubles/development providers must sit behind the same status abstraction future native adapters will implement;
- existing `IActivityRecordSource` and M2 ingestion semantics remain authoritative for records and exactly-once processing.

Add conformance-style tests so future platform adapters have a clear behavioral contract.

---

## 6. Workstream D — Shell-facing read models and explicit state projections

Audit the existing read models first. Extend them only where M5 genuinely lacks presentation-ready information.

The future lightweight shell must be able to answer without reading canonical internals directly:

### Home / attention state

- what meaningful thing changed;
- what project is active and its progress;
- whether attention is required;
- why attention is required;
- region completion/restoration summary;
- concise activity/source status;
- one primary next action or explicit nothing-needs-attention state.

### Onboarding

- current onboarding stage;
- what the next user-visible action is;
- whether activity connection is optional/blocked/complete;
- whether the first project has been chosen through canonical operations;
- safe resume state after restart.

### Settings

- current durable preference values;
- which settings are locally meaningful even without platform adapters;
- validation/range rules where applicable.

### Activity status

- the player-safe status classification;
- whether retry/open-settings/connect action is relevant;
- bounded last-success/last-attempt information where justified;
- no raw health payload or private record dump.

### Diagnostics/support

Expose a separate support-oriented model containing only privacy-safe operational facts useful for debugging, such as:

- save load/recovery outcome;
- schema/migration status;
- source/checkpoint identity metadata already considered safe by existing architecture;
- accepted/rejected/duplicate/correction/deletion aggregate counts from the trust pipeline where available;
- latest processing outcome/failure classification;
- canonical validator health summary;
- application/build/content/save version identifiers if already available or cheap to expose correctly.

Do not build a generic MVVM/UI framework. Do not create a single god-object shell model. Prefer small explicit immutable models with tests.

---

## 7. Workstream E — Empty/error/recovery semantics

Encode the M5 shell's high-value non-happy-path states so presentation does not improvise behavior later.

At minimum, prove explicit application/read-model behavior for:

- no current project;
- no queued projects;
- no discoveries yet;
- no expeditions available;
- no activity data yet;
- no meaningful offline changes;
- producer not unlocked;
- region completed;
- permission denied;
- permission revoked externally;
- source unavailable;
- temporary refresh failure;
- save recovery used;
- save unreadable/unrecoverable;
- migration failure/future schema;
- long absence with valid but compact return summary.

Requirements:

- every state has an explicit next-action classification where a useful action exists;
- unrecoverable save state remains fail-closed and must never be converted into a fresh profile by UX helpers;
- save-recovery messaging must distinguish successful recovery from unrecoverable failure;
- reading these states must be side-effect free;
- retry operations must route through existing application boundaries and remain exactly-once safe.

Do not hard-code final marketing copy into domain objects. Stable semantic codes + concise default text in the appropriate presentation/application boundary are acceptable if consistent with current architecture.

---

## 8. Workstream F — Low-attention acceptance harness

Add named automated acceptance scenarios that prove the M5 platform-neutral contract over realistic time gaps and failure states.

At minimum cover:

1. **First-run / grant path** — onboarding starts, source status becomes connected/current via test adapter, first project chosen through real operation, restart resumes/completes correctly.
2. **First-run / denial path** — permission denied does not trap the profile; safe lightweight navigation/read models remain available; no fabricated activity credit.
3. **One-day return** — normal concise summary + active project + no false attention request.
4. **Seven-day return** — bounded summary remains comprehensible; durable state survives app-closed advancement/restart.
5. **Thirty-day return** — long absence does not explode shell state or require per-event claiming; summary remains bounded by existing D-033 rules.
6. **Queue empty while away** — explicit attention reason + useful next action; banked/fallback canonical policy remains unchanged.
7. **Source temporarily fails** — prior progress preserved; status/diagnostics explain failure; retry later can process valid records exactly once.
8. **Permission revoked externally** — status changes without mutating earned progress; reconnect path is representable.
9. **Save recovery used** — recovered canonical state is surfaced calmly; no silent reset; diagnostics expose recovery evidence.
10. **Preference isolation** — repeatedly toggle reduced motion/haptics/notification preferences across restarts while asserting byte-/state-equivalent canonical progression apart from intentionally separate local preference bytes.
11. **Replay after UX operations** — onboarding/settings/status reads/writes followed by replay of the same activity history still credits zero additional progress.

Where useful, build a deterministic CLI/test helper that dumps these shell-facing projections for evidence. Do not add tooling merely for screenshots or cosmetic output.

---

## 9. Workstream G — Property/adversarial coverage for M5 contracts

Add targeted automated hardening around the new state/persistence boundaries.

Examples to cover where applicable:

- malformed preferences payload;
- future preferences schema;
- interrupted preference write;
- repeated identical preference writes;
- rapid status transitions (needed → denied → connected → unavailable → connected);
- status/read-model calls with zero records and mature histories;
- stale diagnostic snapshots cannot become canonical decisions;
- onboarding interruption at every durable step;
- preference/onboarding data from old versions migrates or fails according to explicit policy;
- no player-facing state leaks raw exception text or raw health/activity payloads.

Prefer deterministic table/property tests to large mock hierarchies.

---

## 10. Workstream H — Documentation + evidence reconciliation

Update source-of-truth docs to describe **actual landed behavior**, not aspiration.

At minimum reconcile:

- `README.md` — current M5-H1 state and automated evidence;
- `docs/ROADMAP.md` — mark only M5 sub-items genuinely automated-verified; do **not** mark M5 complete because Unity/device UX remains open;
- `docs/MASTER_PLAN.md` — immediate-next-campaign status after this work;
- `docs/UX_DESIGN.md` — concrete ownership/status/read-model contracts and which runtime/accessibility items remain unverified;
- `docs/TECHNICAL_ARCHITECTURE.md` — persistence/ownership boundaries for preferences/onboarding/status adapters;
- `docs/TESTING_AND_RELEASE.md` — named M5-H1 acceptance/contract suites;
- `docs/DECISIONS.md` — any new ownership/persistence/status decisions;
- `docs/RISK_REGISTER.md` — only evidence-backed mitigation updates.

Create `docs/evidence/m5-h1/` only for durable machine-readable or human-readable evidence that adds real value (acceptance outputs, compatibility/migration matrix, campaign outcome). Do not dump routine test logs into the repository.

Documentation evidence labels must remain honest: **IMPLEMENTED / AUTOMATED VERIFIED** only. Unity runtime, screen-reader behavior, Android/iOS permissions, notifications, native deep links, device lifecycle, battery, and physical-device performance remain UNVERIFIED.

---

## 11. Defect policy

During this campaign:

- **Critical/High defect in touched or directly dependent code:** fix immediately, regression-test it, document if behavior/architecture changes.
- **Medium defect tightly coupled to campaign correctness:** fix if reasonably bounded.
- **Unrelated Medium/Low defect:** record precisely; do not derail M5-H1 into another repository-wide hardening campaign.
- Never weaken a test, validator, exactly-once rule, save/recovery contract, or identity/lease guard to make new work fit.

If the campaign uncovers evidence that the proposed M5 ownership model would corrupt or duplicate canonical state, redesign the boundary before continuing.

---

## 12. Explicit non-goals

Do **not** implement in this campaign:

- Unity project/scenes/prefabs/assets;
- a repeat of the known Unity 6 LTS Gate A1 detection loop;
- Health Connect native integration;
- HealthKit native integration;
- actual OS permission requests;
- native notifications, scheduling, deep-link delivery, or push infrastructure;
- screen-reader/runtime accessibility certification;
- M6 Visit World / 3D navigation / world assets;
- Region 2;
- new economy/content progression as a substitute for M5 work;
- multiplayer/social/cloud backend/account systems;
- remote analytics;
- release qualification claims.

If a requirement cannot be verified without Unity/native/device execution, define the narrow contract and tests that are meaningful headlessly, mark the runtime portion UNVERIFIED, and move on.

---

## 13. Verification gates

Run focused tests throughout. Before completion, all applicable gates below must be executed on the final integrated tree:

```bash
dotnet build SimpleWalkGame.sln
dotnet test SimpleWalkGame.sln

tests/guards/run-guard-tests.sh

# Existing simulation smoke
dotnet run --project tools/simulation -- new --save <tmpdir> --seed 7 --at 2026-08-20T08:00:00Z
dotnet run --project tools/simulation -- simulate --save <tmpdir> --days 5 --start 2026-08-20T08:00:00Z
dotnet run --project tools/simulation -- validate --save <tmpdir> --selftest

# Existing exactly-once regression path
dotnet run --project tools/simulation -- walk --save <tmpdir-or-fresh> <campaign-supported-args>
# replay the identical history using the tool's supported replay mode and prove zero additional credit
```

Also run every new named M5-H1 acceptance/conformance suite and any new preference/onboarding fault-injection suite.

The baseline at planning time is **221 passing automated tests**. The final count may grow; do not hard-code success to a particular new number. The requirement is zero regressions and all new relevant tests green.

If hosted CI exists for the pushed implementation SHA, inspect the exact pushed SHA before declaring completion. If CI is unavailable, record that honestly.

---

## 14. Completion gate

This campaign is **COMPLETED** only when all of the following are true:

1. repository identity/preflight/lease contract was followed;
2. M5 UX state ownership is explicit and architecture-preserving;
3. durable local preferences/onboarding state exists with tested defaults, restart behavior, corruption/future-version policy, and no canonical-progress leakage;
4. platform-neutral activity connection/permission status projection exists with player-safe states and a separate privacy-safe diagnostics projection;
5. missing shell-facing M5 read models/state projections are implemented without a god model or duplicated canonical state;
6. the required empty/error/recovery states are explicit and side-effect free;
7. named first-run / denial / 1-day / 7-day / 30-day / queue-empty / source-failure / permission-revoked / save-recovery / preference-isolation acceptance scenarios pass;
8. replay after M5 UX operations still credits zero additional activity/progression;
9. no Critical/High defect remains from this campaign;
10. full headless build/tests/guards/simulation gates are green;
11. docs reflect actual implementation and keep runtime/device claims UNVERIFIED;
12. commits are logical and detailed;
13. final integration follows `AGENTS.md` lost-update rules;
14. work is pushed to the target branch;
15. the **final commit message is a detailed session report**, including start SHA, major implementation changes, defects fixed, validation executed/results, explicit runtime/platform deferrals, and evidence locations;
16. `.agent/EXECUTION_PROMPT.md` is changed from **ACTIVE → COMPLETED** with the execution outcome, final implementation SHA, evidence, remaining risks, and next dependency;
17. the writer lease is released normally.

Do not mark M5 complete as a roadmap milestone merely because this headless contract campaign passes. The mobile UI, accessibility behavior, Unity lifecycle, native activity permissions/providers, notification delivery, and device evidence are still later runtime/platform work.

---

## 15. Expected state after completion

A future Unity-enabled M5 runtime campaign should be able to start from a much narrower problem:

- render already-defined immutable read models;
- invoke already-defined application operations;
- bind native source/permission status to the platform-neutral contract;
- bind durable local preferences to actual controls;
- implement normal/loading/empty/error visuals;
- add runtime accessibility semantics/reduced-motion behavior;
- prove lifecycle and interaction budgets in the real editor/device environment.

It should **not** need to invent onboarding persistence, settings ownership, player-safe activity states, support diagnostics, long-return semantics, or canonical error/recovery behavior inside Unity.

If the Unity 6 LTS blocker is still unresolved after this campaign, do not automatically generate another fake-runtime campaign. The next planner must audit whether another independent, high-value implementation lane exists; if not, stop and report the external dependency rather than manufacturing work.