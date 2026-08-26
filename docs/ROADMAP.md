# Roadmap

## Roadmap philosophy

This roadmap is intentionally **aggressive but dependency-aware**. It is not a chronological feature wishlist. Each milestone is a development campaign expected to deliver a coherent vertical concern across domain logic, persistence, tooling, tests, presentation, and documentation.

The sequencing rule is simple:

> **Trust infrastructure before content scale. Integrated vertical slices before expansion. Release evidence before claiming readiness.**

Do not skip foundational milestones to make the repository look visually advanced.

---

# M0 — Product and engineering contract

## Goal

Establish the specification that future implementation must satisfy.

## Scope

- product thesis;
- low-attention session model;
- core loop;
- game systems;
- activity trust pipeline;
- architecture boundaries;
- world/content strategy;
- UX rules;
- performance budgets;
- testing/release model;
- risk register;
- agent execution policy;
- decision log.

## Exit criteria

- [x] docs are internally consistent;
- [x] MVP scope and exclusions are explicit;
- [x] implementation architecture is defined;
- [x] activity exactly-once requirements are defined;
- [x] release qualification is evidence-based;
- [x] first engineering campaign is obvious from the docs.

**Repository state after this documentation overhaul: M0 substantially established.**

---

# M1 — Deterministic core and durable state

## Goal

Create a headless, deterministic game core that can simulate progression safely without Unity presentation.

## Major deliverables

### Domain foundation

- stable ID types;
- value objects;
- domain result/error model;
- injected clock abstraction;
- deterministic RNG abstraction/state;
- canonical resource accounting;
- `RegionState` skeleton;
- project state machine;
- project queue;
- activity/reward transaction model;
- domain validation/invariants.

### Persistence

- versioned save envelope;
- serializer;
- atomic snapshot store or snapshot+journal implementation;
- backup/recovery path;
- schema migration pipeline;
- save integrity validation;
- representative fixtures.

### Testing

- standalone domain test project;
- idempotency tests;
- save/load roundtrip tests;
- corruption/recovery tests;
- migration test harness;
- clock/RNG determinism tests.

### Tooling

- headless simulation/CLI utility;
- deterministic state dump;
- basic validation command.

## Exit criteria

- [x] domain has no Unity dependency; *(netstandard2.1, C# 9, no engine references — D-024)*
- [x] same initial state + inputs yields same final state; *(OfflineAdvancerDeterminismTests)*
- [x] duplicate reward transaction is a no-op; *(RewardLedgerTests + SessionCreditFlowTests across restarts)*
- [x] save/reload preserves canonical state; *(SaveCodecRoundtripTests — incl. get-only-collection Populate regression, D-027)*
- [x] interrupted/corrupt primary save has a tested recovery path; *(AtomicFileSaveStoreTests + SessionBootTests backup recovery)*
- [x] clean clone runs the headless tests. *(at M1 exit: 91 tests green; current suite has grown to 131 — Domain 85 / Infrastructure 19 / Application 27; headless CLI in `tools/simulation`)*

---

# M2 — Activity trust pipeline

## Goal

Build the full platform-neutral activity ingestion/reconciliation path and prove exactly-once crediting under hostile inputs.

## Major deliverables

- `ActivitySource` port;
- normalized record model;
- source checkpoint model;
- dedup ledger/fingerprint strategy;
- validation/eligibility rules;
- conversion-rule versioning;
- reward transaction generation;
- correction/deletion policy implementation;
- bounded historical reconciliation;
- source diagnostics;
- fixture provider.

## Red-team scenarios

- duplicate records;
- overlapping windows;
- late records;
- corrected records;
- deletions;
- out-of-order input;
- huge/invalid values;
- permission/source failures;
- persistence interruption at each transaction boundary;
- restart/replay.

## Exit criteria

- [x] replay cannot double-credit; *(SessionIngestionTests.ReplaySameBatchAfterRestart_IsAFullNoOp + FingerprintIdentity_DedupSurvivesPersistenceRoundtrip — durable processed-record ledger keyed by source ID or content fingerprint)*
- [x] late valid activity credits once; *(NewValidRecordAfterFullReplay_AddsOnlyItsDelta)*
- [x] correction behavior is deterministic; *(CorrectionUp_HigherRevision_CreditsExactlyTheDelta_AndReplaysClean, CorrectionDown_ClampsToUnspentBalance_TracksRemainder_KeepsWorldContent, Deletion_ReversesRemainingValue_DuplicateDeletionIsIgnored, CorrectionFixtureBatch_NetsToZero_WithExactDiagnostics — conservative clawback clamped to unspent balance, net-applied row accounting, D-029)*
- [x] checkpoint cannot outrun durable reward state; *(CheckpointNeverExceedsMaxTrustedEndUtc + SaveFailureMidIngest_LeavesDiskConsistent_AndRetryCreditsExactlyOnce — ledger, dedup rows and watermark persist in one atomic file commit)*
- [x] diagnostics explain accepted/rejected/duplicate totals; *(IngestResult: received/accepted/rejected-by-code/duplicates/corrections/deletions/stale/clamped/net vitality/unapplied-reversal totals)*
- [x] activity fixtures pass through the same post-adapter pipeline as production data will. *(tests/fixtures/activity JSON → FixtureActivityFileReader → GameSession.IngestActivityBatch; no fixture-specific code path exists)*

---

# M3 — Ambient progression vertical slice

## Goal

Prove the complete gameplay thesis using headless fixtures and a minimal UI before investing heavily in 3D world production.

## Major deliverables

- Vitality allocation;
- project queue and auto-advance;
- landmark restoration stages;
- producer/offline simulation;
- return-summary domain model;
- lightweight Home UI;
- Projects UI;
- basic Region status UI;
- local test activity injector for development;
- restart/recovery behavior.

## Acceptance scenario

A test profile should be able to:

1. start from clean state;
2. feed several days of synthetic activity;
3. automatically advance a project;
4. complete a landmark;
5. generate secondary production;
6. close/reload the game;
7. receive a concise return summary;
8. choose the next project;
9. continue without any duplicate rewards.

## Exit criteria

- [x] end-to-end loop works without platform health API; *(M3AmbientProgressionAcceptanceTests: normalized synthetic records through IngestActivityBatch, exactly-once crediting, completion boundaries, landmark stages, producer unlock + bounded production, byte-identical deterministic rerun; `tools/simulation walk` CLI harness)*
- [x] app may remain closed between synthetic activity periods; *(fresh GameSession recreated from disk between every activity window in acceptance test and walk CLI; boot/recovery exercised each time)*
- [x] no progress is lost because the queue crosses a completion boundary; *(QueueAllocationTests rollover, acceptance banked-surplus assertions, unallocated Vitality persists while queue empty/auto-advance off)*
- [x] return summary stays concise; *(ReturnSummaryComposer hard 12-item bound with priority ordering and dedupe; durable pending summary survives commit-before-presentation restarts — ReturnSummaryDurabilityTests)*
- [x] no UI owns canonical progression state. *(presentation consumes HomeReadModel/ProjectsReadModel/RegionReadModel/ReturnSummaryReadModel plus use cases only; Domain/Application carry zero Unity references — D-035)*

**M3 status: implemented and automated verified headless (180 tests across all suites at M4-H completion).** Runtime-only deliverables — the minimal Unity 6 LTS shell (Home/Projects/Region scenes), EditMode/PlayMode coverage and editor/device verification — are explicitly deferred to a runtime-enabled session (D-035): no Unity editor exists in the environment where this campaign ran, and unverifiable editor artifacts must not be committed as evidence.

---

# M4 — Region 1 content production

## Goal

Turn the systems vertical slice into one coherent, complete restoration region.

## Major deliverables

- final Region 1 content graph;
- 5–7 major restoration chains;
- 12–20 meaningful project nodes;
- 6+ major landmarks;
- ecological progression;
- settlement progression;
- 10+ discoveries;
- 3+ expedition routes/objectives;
- producer/infrastructure definitions;
- region completion milestone;
- post-completion state;
- content validators;
- simulation reports across activity profiles.

## Exit criteria

- [x] critical path is reachable; *(ContentValidator fixpoint reachability gate: every project, landmark stage, producer unlock, discovery trigger and expedition requirement reachable from an entry project; closure milestone `proj.complete-valley-survey` reachable — M4ContentGraphTests)*
- [x] no dependency cycles; *(cycle detection plus hidden-deadlock red-team tests: UnreachableProject/UnreachableClosureMilestone flagged — M4ContentGraphTests)*
- [x] pacing works across low/moderate/high/irregular profiles; *(deterministic `tools/simulation profile` reports committed under docs/evidence/m4/: high d97, irregular d139, moderate d242, low long-tail documented with provisional targets D-039)*
- [x] required foreground decisions remain low; *(exactly one queue decision per project (19 total) with auto-advance; zero-to-one queue-empty days and zero capped-store days across all four profiles)*
- [x] all major stages have presentation requirements documented; *(WORLD_AND_CONTENT §12 binding table covers all six landmarks' canonical stages, discovery/expedition/arc presentation keys; UX_DESIGN §8 lists runtime binding requirements)*
- [x] Region 1 can be completed headlessly. *(M4Region1AcceptanceTests drives a clean profile through the real trust pipeline to the closure milestone with replay exactly-once, post-completion stability and byte-identical determinism)*

---

# M5 — Mobile shell and low-attention UX

> **M5-H1 headless contract campaign (2026-08-26): the platform-neutral UX state layer is implemented and automated verified** (295 automated tests): durable local preferences/onboarding store with documented corruption/future-version policy and byte-proven canonical isolation (D-042); platform-neutral activity connection/permission status projection with documented precedence, conformance tables, and durable refresh-failure evidence (D-043); shell-facing read models for onboarding/settings/activity-status/support-diagnostics plus explicit Home attention classification; explicit empty/error/recovery semantics incl. save-recovery and future-schema fail-closed states (D-044); eleven named low-attention acceptance scenarios (first-run grant/denial, 1/7/30-day returns, queue-empty, source-failure, external revocation, save recovery, preference isolation, replay-after-UX zero-recredit) plus adversarial hardening suites.
>
> **NOT yet delivered by M5-H1 (runtime/platform, remains open until an editor exists, D-035): actual Unity screens/scenes, screen-reader navigation, reduced-motion runtime behavior, notification DELIVERY, OS permission dialogs, device lifecycle/performance evidence. These stay UNVERIFIED and M5 is NOT complete as a milestone.**

## Goal

Make the ordinary daily experience polished before the optional 3D mode becomes the center of development.

## Major deliverables

- onboarding;
- Home;
- return summary;
- Projects;
- lightweight Region;
- Discoveries;
- Expeditions;
- Settings;
- permission status UX;
- diagnostics screen/section for development/support;
- notification preferences;
- reduced motion;
- accessibility semantics;
- empty/loading/error states;
- lifecycle-safe navigation.

## UX gates

- glance use ≤ 15 seconds target;
- ordinary check-in ≤ 60 seconds target;
- player can leave without claiming dozens of items;
- return after seven days is understandable;
- permission denied does not trap the player.

## Exit criteria

- [ ] all core screens have normal/loading/empty/error behavior;
- [ ] screen-reader core navigation is viable;
- [ ] reduced motion works;
- [ ] no mandatory 3D interaction exists;
- [ ] common daily check-in is one coherent flow.

---

# M6 — Visit World visual vertical slice

## Goal

Create optional real-time presentation that makes restoration emotionally tangible without becoming authoritative state.

## Major deliverables

- on-demand world scene;
- character/camera/navigation;
- landmark state bindings;
- damaged/restored visual sets;
- environment-state controller;
- simple inspect interactions;
- fast travel;
- audio/ambient state;
- restoration celebration hooks;
- quality tiers;
- performance instrumentation;
- safe load failure/exit behavior.

## Exit criteria

- [ ] world accurately reflects canonical Region 1 state;
- [ ] leaving/reloading scene does not alter progress;
- [ ] lightweight UI does not retain unnecessary world cost;
- [ ] target frame-rate floor met on representative device tier;
- [ ] Visit World can be ignored without blocking progression.

---

# M7 — Real platform activity integration

## Goal

Replace development fixtures with real supported activity providers while preserving exactly the same downstream trust pipeline.

## Android work

- Health Connect integration or current approved equivalent;
- permission flow;
- incremental/reconciliation queries;
- lifecycle behavior;
- source diagnostics;
- device verification.

## iOS work

- HealthKit integration or current approved equivalent;
- permission flow;
- queries/reconciliation;
- lifecycle behavior;
- source diagnostics;
- device verification.

If platform schedules differ, one platform may qualify before the other; documentation must state that clearly rather than imply parity.

## Exit criteria

- [ ] real records enter normalized pipeline;
- [ ] permissions work on physical devices;
- [ ] app-closed activity reconciles after return;
- [ ] duplicate reconciliation is harmless;
- [ ] permission revocation has correct UX;
- [ ] platform-specific limitations are documented.

---

# M8 — Hardening and red-team campaign

## Goal

Attack the system as if trying to break player trust.

## Major campaigns

### Activity

- duplicates/replay;
- corrections;
- provider unavailability;
- clock/time-zone anomalies;
- huge batches;
- stale checkpoints.

### Persistence

- corruption;
- interruption;
- migration;
- update-in-place;
- low storage;
- long-lived saves.

### Lifecycle

- repeated background/resume;
- process death;
- device reboot;
- permission changes outside app;
- interrupted world loading.

### UX/accessibility

- large text;
- screen reader;
- reduced motion;
- no network;
- empty content states;
- seven-/thirty-day return.

### Performance

- cold/warm launch;
- mature save;
- restored world end-state;
- repeated scene enter/exit;
- memory/thermal/battery.

## Exit criteria

- [ ] no unresolved Critical/High defect;
- [x] all failure classes have explicit recovery behavior *(headless classes: persistence hostile-path matrix, activity red-team, temporal anomalies — automated verified, M8-H1; lifecycle/device classes await runtime)*;
- [ ] performance budgets supported by measurements *(long-horizon growth/wall-time records landed in M8-H1: `docs/evidence/m8-h1/`; device budgets remain open)*;
- [x] migration/recovery evidence exists *(genuine mature-v1 migration + exactly-once post-migration replay, recovery-safe commits — automated verified)*;
- [ ] docs reflect actual implementation state.

**M8-H1 (headless hardening campaign) status:** the Activity and Persistence campaigns plus seeded property testing and long-horizon measurement are complete headlessly — see [`docs/evidence/m8-h1/CAMPAIGN_OUTCOME.md`](evidence/m8-h1/CAMPAIGN_OUTCOME.md). Lifecycle (process death, device reboot), UX/accessibility and on-device performance items require the Unity runtime lane (still externally blocked).

---

# M9 — Release qualification

## Goal

Produce a release candidate supported by reproducible evidence.

## Deliverables

- clean-clone validation;
- full automated suite;
- content validation;
- device matrix;
- activity-provider evidence;
- upgrade/migration evidence;
- accessibility evidence;
- performance/battery evidence;
- known-issues register;
- privacy review;
- release evidence package;
- documentation reconciliation.

## Exit criteria

All checklist items in `TESTING_AND_RELEASE.md` pass.

---

# Post-MVP decision gate

After M9, do not automatically begin Region 2.

Evaluate:

- Is the low-attention loop actually satisfying?
- Is visible restoration strong enough?
- Are users opening because they want to see change rather than because the game nags them?
- Is the activity pipeline trustworthy?
- Is Visit World additive or underused/too expensive?
- Are projects too passive or too demanding?
- Does the player understand where activity went?

Then choose the next campaign.

Potential directions include:

- Region 2;
- deeper ecological systems;
- improved automation;
- richer discoveries;
- optional customization;
- broader activity support;
- cloud backup/sync;
- wearables;
- carefully reviewed social features.

None are pre-authorized by this roadmap.

---

# Cross-milestone rules

Every milestone must:

- end with a buildable repository;
- update docs to match implementation;
- leave no unexplained migration impact;
- add tests for new invariants;
- preserve exactly-once activity behavior;
- avoid expanding scope as a substitute for finishing integration;
- record verification gaps explicitly;
- keep Critical/High defects blocking.

The project should prefer **fewer, larger, integrated campaigns** over fragmented micro-commits that create the appearance of progress without completing vertical functionality.
