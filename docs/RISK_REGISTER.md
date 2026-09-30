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

**M8-H1 update (automated verified):** `ActivityRedTeamTests` converges five hostile permutations plus overlapping-window, duplicate-flood and full-replay scenarios to identical canonical state; interruption/retry is exactly-once (`SessionPersistenceHardeningTests`, acceptance scenario); corrections/deletions pinned to exact D-029 values. Status remains Open only for real-provider/device evidence.

---

## R-003 — Valid activity is silently lost

**Impact:** High  
**Likelihood:** Medium  
**Status:** Open

### Failure mode

Checkpoint handling, provider delay, permission transitions, or errors cause legitimate activity never to be credited.

### Mitigation

- bounded reconciliation window (anchored to trusted time, D-053);
- source health diagnostics;
- late-record handling;
- checkpoint sequencing;
- retryable failures;
- player-facing stale/source states.

**D-053 update (automated verified):** the "bounded reconciliation window" mitigation above
existed but was anchored to the wrong reference, which made it **worse than no bound** for
the product's own central case. It compared record ends against raw device wall clock
("14 days before now", "10 minutes after now"), so:

- a player returning after a holiday had their **entire** provider backlog rejected as
  `OutsideHorizon` — the activity that earned the most goodwill silently disappeared, in
  direct contradiction of the offline-first thesis (D-003/D-004) the 14-day bound was
  introduced to protect; and
- a device clock merely **minutes** fast made every record look future-dated, so crediting
  stopped **entirely** with no player-visible explanation (F-03).

Neither scenario had any coverage: `M5H1ShellAcceptanceTests.Scenario05` returns after 30
days but deliberately ingests **nothing** during the absence, so the realistic "provider
hands back the whole backlog on return" case was untested anywhere in the suite.

Closed by D-053: the window is now computed per batch from a `TrustedTime` reference
(§5.1 of `ACTIVITY_PIPELINE.md`) carrying the injected clock, the latest durably trusted
record end, and the reconciliation watermark. The lower bound tracks trusted progress less
a 45-day floor, so an absence of any length is creditable; the upper bound is the later of
clock and trusted progress plus a 10-minute skew, so a fast clock degrades the anti-future
bound instead of disabling crediting. The bound is not removed: a record more than 45 days
before the last trusted end is still `OutsideHorizon`, and a genuinely future-dated record
is still `FutureTimestamp`. Deletion markers now obey the same eligibility rules (F-09);
previously they bypassed validation entirely and could push the watermark arbitrarily far
forward.

**Covered headlessly (verified this pass):** 30-day and 45-day absences with the whole
backlog delivered in one batch, credited exactly once and re-crediting nothing on replay;
a 2-hour-fast and a 3-day-jump device clock; a backward clock movement; a 1-month forward
clock jump crediting only what the source reported; a hostile far-old dump mixed with
in-window records; malformed, future-dated and out-of-window deletion markers; and the
per-batch accounting identity with every counter exercised.

**Still device-blocked and honestly unclaimed:** real Health Connect / HealthKit provider
behaviour — whether a given platform actually returns a full backlog on reconnect, what
its backfill limits are, and how it behaves across a permission revocation or a device
clock correction. That is the M7 adapter lane and cannot be verified from this repository
(`KI-002`). Per-reason rejection counts are still **not persisted**; the breakdown is
produced and returned on the result object but has no durable path until the follow-up
diagnostics change.

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

**M8-H1 update (automated verified):** recovery re-commits can no longer displace the last healthy generation with a corrupt primary (`WriteAtomicPreservingBackup`, D-040); access failures and decode reasons are diagnosed instead of crashing or masquerading as "no save"; hostile-path matrix in `docs/evidence/m8-h1/PERSISTENCE_RECOVERY.md`.

**F-01 update (automated verified, D-052):** the M8-H1 mitigation above was
*incomplete*, and the gap was a total-loss path. The store layer had always kept a
healthy backup, and `WriteAtomicPreservingBackup` protected it — but the **application**
boot flow returned "no save found" the instant the primary slot was absent, without ever
reading that backup. Because `AtomicFileSaveStore.ReplaceFile` promotes a generation with a
delete-then-move pair, an absent primary is a *reachable* state (a crash between the two
steps), not a hypothetical. Any composition root treating "no save" as a fresh profile
then started a brand-new empty world and overwrote the only surviving copy.

This risk was open at **Critical** impact with a working mitigation that was never
exercised on the path that mattered, and no register entry recorded it (see the note under
`docs/KNOWN_ISSUES.md` "Rules"). Closed by D-052: `GameSession.Continue` now addresses both
generations independently and reports absence only when BOTH are absent. Reproduced
end-to-end through the committed CLI before and after; the post-fix run preserves 20 days
of progress (6 completed items, 40 Materials) where the pre-fix run reset the world.

**Residual:** recovery is the safe default, so a *deliberately* deleted primary now looks
like a crash to the player. The distinction is available to support via
`DiagnosticsReadModel.BootRecoveryCause` (`PrimaryMissing` vs `PrimaryDamaged`) and the
distinct `DiagnosticsBootOutcome.RecoveredFromMissingPrimary` value; it is not surfaced
to the player as an error, which is the intended trade. Multi-generation history and a
write-ahead journal remain deferred (disproportionate at this scale) and are recorded in
the master plan's deferred list, not as open mitigations of this risk.

## R-006 — Migration duplicates or invalidates activity rewards

**Impact:** Critical  
**Likelihood:** Medium  
**Status:** Open *(mitigation machinery exercised by first real migration)*

### Failure mode

A schema migration alters ledger identity, conversion semantics, or checkpoints so old activity is reprocessed or prior rewards disappear.

### Mitigation

- conversion-rule versioning;
- persistent transaction identity;
- sequential migrations;
- old-save fixtures;
- update-in-place qualification;
- post-migration duplicate reconciliation test.

**Evidence so far (automated verified):** schema v2 shipped behind registered `MigrationV1ToV2` with representative hand-built v1 envelope fixtures decoding through the real chain (`MigrationV1ToV2Tests`), value-promotion semantics documented in D-032, and post-migration replay still exactly-once (acceptance suite). M8-H1 adds genuine mature-v1 fixture migration with exactly-once replay after migration and canonical byte stability (`MatureSaveMigrationTests`, `docs/evidence/m8-h1/MATURE_SAVE_MIGRATION.md`). Device/update-in-place qualification remains open for release.

---

## R-007 — Inactivity mechanics create a quitting spiral

**Impact:** High  
**Likelihood:** Medium  
**Status:** Open *(core anti-punishment mechanics now implemented and tested)*

### Failure mode

Streak loss, overflowing producers, expiring rewards, or world decay makes returning after absence feel punishing.

### Mitigation

- no destructive world decay;
- durable completion;
- generous caps;
- no irreversible missed-day core rewards;
- welcoming return summary;
- seven-/thirty-day return acceptance scenarios.

**Evidence so far (automated verified):** unallocated Vitality banks indefinitely while queues are empty or automation is off; producer overflow is no-waste bounded parking (D-032); the return summary is durable, welcoming, priority-ordered and survives restarts without nagging (D-033). Long-absence production bounds are explicitly tested; M8-H1 adds a 4,000-day absence test and measured 400-day-absence simulation completing Region 1 cleanly (`TemporalAnomalyTests`, `docs/evidence/m8-h1/LONG_HORIZON.md`). Seven-/thirty-day UX scenarios remain open until presentation exists.

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

**Status update (2026-09-29, D-046):** all seven profiles named by GAME_SYSTEMS §13 are now executable with committed, SHA-256-reproducible evidence under `docs/evidence/m4/`. The `pathological` profile shows the D-030 clamp reducing effective credit to ≈686 steps/Vitality against the nominal 100. The `inactivity` profile exposes a genuine balance finding: a player who walks only on weekdays and takes a six-week break restores both region arcs to 4/4 but **cannot finish the forest or research chains within 400 days** — the exact quitting-spiral shape this risk describes. Status remains **Open** until that shortfall is a deliberate balance decision, not a silent re-tune.

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

**D-054 update (automated verified):** the "each currency requires unique purpose"
mitigation was **aspirational** until this change — it was a sentence in this register with
nothing behind it. The unmitigated form was literally in the build: Materials and Knowledge
had **no consumer and no cap**. `ResourceBalances.SetCap` existed with zero call sites
outside tests, so `GetCap` always returned `long.MaxValue`; nothing ever called
`TryConsume` for either currency; and the entire "park the refused surplus and flush it
later" mechanism — which `ProducerSimulationTests` proved worked — was unreachable dead
code. `GAME_SYSTEMS.md` §3 listed "optional secondary-resource requirements" as a project
schema field and `ProjectDefinition` had no such field, so project costs were Vitality-only
and **no resource-allocation decision existed anywhere in the game**.

Now: Region 1 authors **Materials 400 / Knowledge 300** caps, both of which *bind* over a
730-day run (measured, not asserted), and two projects carry real requirements — Restore
Market Hall 40 Materials (settlement chain) and Calibrate Survey Rig 20 Knowledge
(research chain). The currencies now carry a decision, the cap machinery is reachable in
production, and refused output is retained and released automatically rather than lost.
Vitality remains dominant and is deliberately **uncapped** (capping it would silently
discard real walking credit — see D-054).

**Covered headlessly:** cap application and the over-cap-on-load invariant, all-or-nothing
requirement consumption, the activation failure path and its stable error code, auto-advance
stalling at an unaffordable head of queue while Vitality banks, parking and no-elapsed-time
flush, no-waste on a full store, requirement satisfiability validation, and the read-model
and summary projections that make the pressure explainable.

**Still unmitigated / not claimed:** whether a cap is the right *number* for a real player
is a balance judgement no headless test can settle — the seven pacing profiles are
byte-identical, but a cap that binds around day 300 says nothing about how it feels at day
30. Whether the new choice *improves* the activity-to-world relationship rather than adding
a side-resource chore is a UX judgement, and is the M5 shell's call, not this repository's.

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

**Status update (2026-09-29):** the "dependency checks" bullet above was previously an aspiration with nothing enforcing it. `scripts/verify-architecture.sh` now runs in CI and mechanically enforces the layering: Domain references nothing and stays on `netstandard2.1`; Application references only Domain; Infrastructure references only Application + Domain; no engine namespace appears anywhere in `src/`; and Domain/Application perform no filesystem access (comments stripped first so prose cannot false-positive). Every rule was proven to fail against a deliberately-broken fixture, not merely to pass against the current tree. Status remains **Open** rather than Mitigating — the gate proves the headless layering today, but R-017 is only re-testable end to end once M5/M6 land the Unity presentation project.

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

**D-059 update (automated verified):** the "tombstone/migration policy" mitigation above
did not exist, and nothing detected drift before it destroyed a profile.
`RegionDefinition.ContentVersion` was set to 2 and read by **nothing** — a grep found only
the declaration and the constructor call.

The failure was specific and unrecoverable. Canonical state fails CLOSED on unknown
content, which is correct and is exactly what makes a rename dangerous: rename one project
or drop one discovery an existing save unlocked, and every existing save fails validation
on boot, both generations are unreadable, and the one-generation backup holds the same
invalid content so it fails too.

Now implemented and verified:

- **the version is recorded** — `GameState.AuthoredContentVersion`, set at creation and
  never rewritten, additive so no schema bump or migration was needed;
- **boot gates on it** — the save↔content relationship is classified on every boot
  (`ExactMatch` / `CompatibleOlder` / `NewerThanBuild`), and a save NEWER than the build
  fails closed with a distinct outcome and is never rewritten;
- **tombstones exist** — a retired identifier resolves to an authored `ContentTombstone`
  instead of becoming an unresolvable reference, and grants NOTHING: a retired project may
  not sit Active, a retired producer may not unlock, a retired expedition may not be
  recorded as completed. Region 1's tombstone set is deliberately empty;
- **drift is gated in CI** — `scripts/verify-content-compatibility.sh` proves the shipped
  content validates and that mature saves at five ages all resolve against it, and is
  wired into `ci.yml` (a script CI does not call is not a gate, per D-049). The gate was
  proven to fail on a deliberately drifted fixture before being trusted.

**Covered headlessly:** exact-match, compatible-older and newer-than-build classification;
fail-closed without rewriting; the additive absent-field contract; tombstone resolution,
the no-grant rules for project/producer/expedition, and that an unknown identifier still
fails closed; every tombstone content rule; the two version axes reported independently;
and the CI gate end to end over 15 workflow steps.

**Still unmitigated and honestly recorded:**

- A drift that shipped through an **intermediate** build — a profile created against a
  version then retired without a tombstone — is **detected and refused, not recovered**. The
  mechanism prevents the next drift; it cannot reconstruct meaning this build does not
  have.
- **Detecting drift is not preventing it.** Tombstone discipline is a human review
  obligation. The gate catches the consequence; `AGENT_EXECUTION_GUIDE.md` now states the
  authoring rule so the cause is addressed where it is made.
- Real-device migration behaviour is untouched by this and remains device-blocked
  (`KI-002`).

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

## Campaign updates — M4-H (Region 1 content systems, headless)

- **R-013 (balance assumes one activity pattern):** mitigated by measured evidence — four deterministic profiles simulated to completion/horizon (`evidence/m4/`); moderate d242 / high d97 / irregular d139 complete; low is a documented long tail with no punishment mechanics. Provisional targets recorded in D-039; future tuning must beat the committed baseline.
- **R-018 (content ID/schema drift breaks mature saves):** seed IDs/costs/prerequisites preserved verbatim in content v2; M4 state is additive schema-v2 with absent-means-default decoding proven by `M4BackwardDecodingTests`; validator rejects unknown runtime IDs on load.
- **R-006 (migration duplicates/invalidates rewards):** unchanged trust pipeline consumed by all new systems; full-history replay after region completion credits zero (`M4Region1AcceptanceTests`), and expedition rewards fire exactly once by construction.
- **R-021 (scope expansion prevents completing Region 1):** Region 1 reached headless content-completeness within a single campaign without Unity assets, generic content frameworks or Region 2 work; scope boundaries from EXECUTION_PROMPT §11 held.

---

## Campaign updates — M5-H1 (platform-neutral UX state contracts, headless)

- **R-003 (valid activity is silently lost):** further mitigated headlessly — player-safe source status projection now distinguishes waiting-for-first-data from connected-current and durably classifies transient refresh failures across restarts (D-043); support diagnostics expose checkpoint-watermark age and last-batch counters so staleness is observable before data is lost. Real-provider behavior remains open until M7 adapters.
- **R-022 (privacy scope expands unnoticed):** strengthened by construction — D-043/D-044 pin status and diagnostics to enum/count/timestamp projections with adapter technical detail bounded to one gated field; automated reflection sweeps assert raw exception text and health payloads cannot reach any player-facing surface.
- **R-019 (notification design becomes manipulative):** preference contract landed as opt-in master plus per-category toggles and an optional reminder time only; quiet hours delegated to the OS; no delivery, scheduling, or copy exists in this campaign to become manipulative.
- **R-001 (screen-time creep):** the eleven acceptance scenarios enforce glance-budget bounds (≤12 summary items), explicit nothing-needs-attention states, and denial paths that never trap the profile.

---

Review this file:

- at the beginning of every large campaign;
- whenever architecture/product scope changes;
- before platform integration;
- before content lock;
- before release qualification.

New Critical/High risks should immediately influence roadmap priority rather than remain documentation-only warnings.
