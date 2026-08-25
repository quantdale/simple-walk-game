# Risk Register

## Purpose

This register captures product and engineering risks that could invalidate the project thesis or create unacceptable player-trust failures.

Ratings are intentionally conservative during the pre-implementation phase.

- **Impact:** Low / Medium / High / Critical
- **Likelihood:** Low / Medium / High
- **Status:** Open / Mitigating / Accepted / Closed

---

## R-001 — The game still demands too much screen time

**Impact:** Critical  
**Likelihood:** Medium  
**Status:** Open

### Failure mode

Projects, claiming, inventory, expeditions, notifications, or world interactions gradually create enough maintenance that a busy player must actively play every day.

### Mitigation

- explicit 5–15 second glance budget;
- ordinary check-in ≤ 60 seconds target;
- auto-advance project queue;
- no frequent claim requirement;
- generous storage caps;
- optional Visit World;
- simulation metric for required foreground decisions;
- UX review for every new system.

### Evidence required to close/reduce

Observed/tested daily flow remains coherent under low-engagement usage and multi-day absence.

---

## R-002 — Activity is double-credited

**Impact:** Critical  
**Likelihood:** Medium  
**Status:** Open

### Failure mode

Overlapping provider queries, app restart, provider replay, correction, or save interruption causes the same real-world activity to generate multiple rewards.

### Mitigation

- durable record identity/fingerprints;
- reward transaction IDs;
- idempotent processing;
- source checkpoint cannot outrun durable state;
- replay tests;
- failure injection at commit boundaries.

### Closure evidence

Red-team suite proves repeated/reordered/overlapping activity leads to the same final state as a single valid processing pass.

---

## R-003 — Valid activity is silently lost

**Impact:** High  
**Likelihood:** Medium  
**Status:** Open

### Failure mode

Checkpoint handling, provider delay, permission transitions, or errors cause legitimate activity never to be credited.

### Mitigation

- bounded reconciliation window;
- source health diagnostics;
- late-record handling;
- checkpoint sequencing;
- retryable failures;
- player-facing stale/source states.

---

## R-004 — Platform health APIs differ from assumptions

**Impact:** High  
**Likelihood:** Medium  
**Status:** Open

### Failure mode

Health Connect/HealthKit behavior, permissions, change tracking, corrections, or background availability do not fit the planned abstraction.

### Mitigation

- keep platform bridges narrow;
- validate current APIs during implementation;
- fixture-first domain contract;
- do not claim parity until physical-device evidence exists;
- allow platform-specific capability reporting.

---

## R-005 — Save corruption destroys long-term progress

**Impact:** Critical  
**Likelihood:** Low–Medium  
**Status:** Open

### Failure mode

Interrupted writes, schema changes, bugs, or storage failures make a mature world unrecoverable.

### Mitigation

- atomic commit or journal;
- prior-valid backup;
- integrity validation;
- migrations on copies/with recovery;
- mature-save fixtures;
- corruption tests;
- never overwrite last recoverable state before validation.

---

## R-006 — Migration duplicates or invalidates activity rewards

**Impact:** Critical  
**Likelihood:** Medium  
**Status:** Open

### Failure mode

A schema migration alters ledger identity, conversion semantics, or checkpoints so old activity is reprocessed or prior rewards disappear.

### Mitigation

- conversion-rule versioning;
- persistent transaction identity;
- sequential migrations;
- old-save fixtures;
- update-in-place qualification;
- post-migration duplicate reconciliation test.

---

## R-007 — Inactivity mechanics create a quitting spiral

**Impact:** High  
**Likelihood:** Medium  
**Status:** Open

### Failure mode

Streak loss, overflowing producers, expiring rewards, or world decay makes returning after absence feel punishing.

### Mitigation

- no destructive world decay;
- durable completion;
- generous caps;
- no irreversible missed-day core rewards;
- welcoming return summary;
- seven-/thirty-day return acceptance scenarios.

---

## R-008 — Activity rewards encourage unsafe overexertion

**Impact:** High  
**Likelihood:** Low–Medium  
**Status:** Open

### Failure mode

Unlimited linear rewards or competitive incentives encourage excessive activity.

### Mitigation

- bounded/diminishing conversion where appropriate;
- no medical/weight-loss claims;
- no MVP competitive leaderboard;
- celebrate consistency and world progress rather than extreme totals;
- review copy and balance for pressure patterns.

---

## R-009 — The world does not feel different enough

**Impact:** Critical  
**Likelihood:** Medium  
**Status:** Open

### Failure mode

The player earns lots of numbers, but restoration states look too similar, undermining the main fantasy.

### Mitigation

- before/after art direction;
- multi-dimensional stage effects;
- one complete Region 1;
- landmark-specific visual contracts;
- world is the primary progress visualization;
- test transformation readability with UI hidden.

---

## R-010 — 3D scope consumes the entire project

**Impact:** High  
**Likelihood:** High  
**Status:** Open

### Failure mode

Terrain, art, shaders, traversal, animation, and optimization dominate development before the ambient loop is proven.

### Mitigation

- Visit World begins only after ambient vertical slice/mobile shell;
- one region only;
- no combat/interior sprawl;
- fast travel;
- stateful landmarks over map size;
- performance budgets established before final art production.

---

## R-011 — Optional 3D becomes mandatory through design creep

**Impact:** High  
**Likelihood:** Medium  
**Status:** Open

### Failure mode

Important rewards, project activation, or discovery progression become locked behind manual world exploration.

### Mitigation

- core progression acceptance tests never open Visit World;
- optional discoveries clearly labeled;
- lightweight Region/Projects support all core decisions;
- architecture treats 3D as presentation, not canonical state owner.

---

## R-012 — Poor mobile performance/battery undermines the concept

**Impact:** High  
**Likelihood:** Medium  
**Status:** Open

### Failure mode

The app drains battery, runs hot, launches slowly, or retains expensive world resources despite being intended for casual ambient use.

### Mitigation

- separate lightweight and 3D modes;
- no continuous background-runtime requirement;
- incremental activity queries;
- on-demand world scene;
- quality tiers;
- physical-device performance/battery qualification.

---

## R-013 — Content balance assumes a single ideal activity pattern

**Impact:** High  
**Likelihood:** High  
**Status:** Open

### Failure mode

Players who walk irregularly, less frequently, or mostly on certain days experience stalled or broken pacing.

### Mitigation

Simulate:

- low;
- moderate;
- high;
- irregular;
- weekend-heavy;
- long-absence profiles.

Balance against distributions, not one daily step target.

---

## R-014 — Too many currencies obscure the activity-to-world relationship

**Impact:** Medium  
**Likelihood:** Medium  
**Status:** Open

### Failure mode

The economy becomes a conventional resource-management game where players no longer understand how movement matters.

### Mitigation

- Vitality remains dominant activity-derived resource;
- minimal secondary resource set;
- each currency requires unique purpose;
- no premium-currency architecture in MVP core.

---

## R-015 — Automation removes all meaningful choice

**Impact:** Medium  
**Likelihood:** Medium  
**Status:** Open

### Failure mode

In reducing busywork, the game becomes a passive progress bar with no meaningful player agency.

### Mitigation

- automate maintenance, not strategic priority;
- meaningful project choices;
- optional expedition/collection objectives;
- visible consequences for chosen restoration order;
- preserve seconds-long decisions.

---

## R-016 — Architecture becomes overengineered before product validation

**Impact:** High  
**Likelihood:** Medium  
**Status:** Open

### Failure mode

Microservices, generalized frameworks, complex DI, event sourcing, or backend infrastructure consume effort without improving the MVP.

### Mitigation

- simple layered architecture;
- local-first persistence;
- manual DI acceptable;
- no mandatory backend;
- add infrastructure only against demonstrated need;
- architecture fitness checks focus on boundaries, not framework count.

---

## R-017 — Architecture collapses into Unity scene scripts

**Impact:** High  
**Likelihood:** Medium  
**Status:** Open

### Failure mode

As presentation work accelerates, state and rules move into MonoBehaviours, making deterministic tests and offline simulation unreliable.

### Mitigation

- pure domain assembly;
- dependency checks;
- presentation read models;
- scene state is derived;
- domain tests must remain runnable outside Unity.

---

## R-018 — Content IDs/schema drift break mature saves

**Impact:** High  
**Likelihood:** Medium  
**Status:** Open

### Failure mode

Content assets are renamed/reorganized and save references no longer resolve.

### Mitigation

- immutable stable IDs;
- content validator;
- tombstone/migration policy;
- no array-index identity;
- mature-save fixtures across content updates.

---

## R-019 — Notification design becomes manipulative

**Impact:** Medium  
**Likelihood:** Medium  
**Status:** Open

### Failure mode

Retention pressure introduces generic “come back” prompts, inactivity shame, or artificial urgency.

### Mitigation

- event-value notification policy;
- opt-in/category controls;
- no shame copy;
- no fake urgency;
- notifications tied to genuine completed events or user-set reminders.

---

## R-020 — Documentation becomes aspirational fiction

**Impact:** High  
**Likelihood:** High  
**Status:** Open

### Failure mode

Docs describe planned capabilities as if shipped, making agent decisions and release claims unreliable.

### Mitigation

- evidence states;
- documentation reconciliation at milestone/release boundaries;
- release evidence packages;
- report unverified behavior explicitly.

---

## R-021 — Scope expansion prevents a complete Region 1

**Impact:** High  
**Likelihood:** High  
**Status:** Open

### Failure mode

Region 2, social systems, wearables, broader activities, multiplayer, cosmetics, or backend work begin before the core slice is qualified.

### Mitigation

- explicit MVP exclusion list;
- roadmap gate after M9;
- one-region definition of done;
- agent guide prioritizes current dependency bottleneck.

---

## R-022 — Privacy scope expands unnoticed

**Impact:** Critical  
**Likelihood:** Low–Medium  
**Status:** Open

### Failure mode

Raw health records, routes, or unrelated metrics are retained/uploaded because integration code exposes them conveniently.

### Mitigation

- normalized minimum data model;
- raw payloads excluded from save by default;
- redacted diagnostics;
- backend upload prohibited without new decision review;
- minimum permission set.

---

## R-023 — Autonomous agent operates on the wrong repository

**Impact:** High  
**Likelihood:** Low  
**Status:** Mitigating

### Failure mode

An agent session intended for the sibling repository `quantdale/walk-game` (or launched from a stale prompt) reads, modifies, commits, or pushes in this checkout by mistake. Both products involve walking/Vitality/restoration vocabulary, so prompt-level confusion is realistic.

### Mitigation

- `.repo-identity.json` manifest + `scripts/assert-repo-identity.{sh,ps1}` fail closed (exit 86) on any slug/sentinel/CI mismatch (D-031);
- root `AGENTS.md` contract binds every harness; all `/goal` adapters inherit the preflight;
- CI re-runs the guard under GitHub's own `GITHUB_REPOSITORY`;
- guard behaviors proven by `tests/guards/run-guard-tests.sh`.

### Evidence required to close/reduce

Guard suite green in CI over time; zero wrong-repo incidents after adoption.

---

## R-024 — Concurrent agent writers corrupt shared work-tree state

**Impact:** High  
**Likelihood:** Medium  
**Status:** Mitigating

### Failure mode

Two sessions write the same working tree simultaneously; interleaved edits and reconciliation deletes another lineage's implementations (occurred: commits `b12f52c`, `67368e3`), or a push discards another session's landed commits.

### Mitigation

- single-writer lease per worktree (`scripts/writer-lease.{sh,ps1}`, exit 87 when busy; explicit human override for stale locks);
- worktree isolation helper (`scripts/new-agent-worktree.sh`: one writer = one worktree = one branch);
- pre-push lost-update refusal (exit 88) plus fetch/reconcile procedure in `AGENTS.md`;
- force-push/hard-reset forbidden conflict shortcuts without operator authorization.

### Evidence required to close/reduce

Proof-suite lease/race scenarios stay green; no recurrence of interleaved-lineage incidents.

---

## Risk-review cadence

Review this file:

- at the beginning of every large campaign;
- whenever architecture/product scope changes;
- before platform integration;
- before content lock;
- before release qualification.

New Critical/High risks should immediately influence roadmap priority rather than remain documentation-only warnings.
