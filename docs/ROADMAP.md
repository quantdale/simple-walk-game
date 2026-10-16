# Roadmap

## Roadmap philosophy

This roadmap is intentionally **aggressive but dependency-aware**. It is not a chronological feature wishlist. Each milestone is a development campaign expected to deliver a coherent vertical concern across domain logic, persistence, tooling, tests, presentation, and documentation.

The sequencing rule is simple:

> **Trust infrastructure before content scale. Integrated vertical slices before expansion. Release evidence before claiming readiness.**

Do not skip foundational milestones to make the repository look visually advanced.

---

## Where the next implementation work is specified

Milestones above describe **what** the product becomes. The engineering work
needed to make the current tree correct, complete and release-shaped is
specified separately and takes precedence for any session that is not building
new product surface:

- [`docs/IMPLEMENTATION_MASTER_PLAN.md`](IMPLEMENTATION_MASTER_PLAN.md) — the
  audit findings register, the current-state assessment, the phased roadmap, the
  dependency order, the parallel workstreams, the repository-level Definition of
  Done, and the instructions for the implementation agent.
- [`openspec/changes/`](../openspec/changes) — the canonical execution
  specification: one OpenSpec change per coherent engineering change, each with
  its own proposal, behavioural spec deltas, design and ordered task list.
  Validate with `openspec validate --changes --strict`.

**Current automated test count (2026-10-16): 514** (Domain 148 / Infrastructure 49 / Application 317). Counts quoted in the per-milestone notes below are historical, as of each campaign.

Milestone status and evidence state are unchanged by that plan. In particular,
**release qualification remains blocked** by `KI-001` (hosted CI billing) and
`KI-002` (no Unity editor) regardless of how much of the implementation plan
ships; those are external dependencies, not engineering work.

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
- [x] pacing works across the GAME_SYSTEMS §13 activity profiles; *(deterministic `tools/simulation profile` reports committed under docs/evidence/m4/ — high d77, irregular d109, weekend-heavy d158, moderate d192, pathological d22, inactivity d376 (every balanced profile closes inside its documented target and the harness FAILS on a shortfall, D-062), low long-tail documented as an explicit exemption; the previously unfinishable weekday-only profile now closes with all six chains complete)*
- [x] required foreground decisions remain low; *(exactly one queue decision per project (19 total) with auto-advance; zero-to-one queue-empty days and zero capped-store days across the completed profiles)*
- [x] all major stages have presentation requirements documented; *(WORLD_AND_CONTENT §12 binding table covers all six landmarks' canonical stages, discovery/expedition/arc presentation keys; UX_DESIGN §8 lists runtime binding requirements)*
- [x] Region 1 can be completed headlessly. *(M4Region1AcceptanceTests drives a clean profile through the real trust pipeline to the closure milestone with replay exactly-once, post-completion stability and byte-identical determinism)*

### Intermittent-engagement pacing finding (recorded 2026-09-29, R-013 / R-007)

The seven GAME_SYSTEMS §13 profiles are now all executable and have committed evidence
(`docs/evidence/m4/pacing-*.txt`; each reproduces byte-for-byte by SHA-256). Three were
added in this campaign because only four existed, and two of the new ones changed the
balance picture materially:

- **`pathological`** (new) completed the region on **day 29** from 3,184,633 ingested
  steps yielding 21,844 Vitality. **Corrected 2026-10-09 (D-062 / F-13):** the stated figure
  "≈686 steps/Vitality against the nominal 100" was WRONG, and the repository's own
  committed evidence contradicted it. Recomputed: the profile requests 3,184,633 steps, the
  per-record clamp (250,000) reduces the eligible total to 2,184,633 steps, and 21,844
  Vitality is credited — an effective **100.0 eligible steps per Vitality**, exactly the
  documented rate. The clamp discards surplus VOLUME; it does not reduce the credit rate, so
  "100 steps → 1 Vitality" holds precisely rather than being a ceiling. The wrong figure came
  from dividing the pre-clamp total by the credited vitality under an "activity ingested"
  label; the report now prints requested and eligible totals separately.
- **`inactivity`** (new: silent weekends plus a fully silent day 100–144) **did not reach
  the closure milestone within 400 days** at the v2 costs: it earned 16,420 Vitality against
  a 19,400-Vitality roster (short by 2,980), so the forest and research chains stayed
  incomplete. **Remedied 2026-10-09 (D-062):** the M4-authored roster was rebalanced to
  15,350 Vitality with the seed definitions untouched, closure became an authored criterion,
  and the harness now fails on a shortfall instead of reporting one. The profile closes on
  **day 376** with all six chains complete.

That is a real product finding, not a test defect: a player who walks only on weekdays
and takes a six-week break could restore the region's arcs but could never finish the last
two chains on the v2 numbers. R-007 explicitly names this quitting-spiral shape.
It was recorded rather than silently re-balanced — changing chain costs would have
invalidated the committed M4 pacing reports and the D-039 provisional targets, so the
rebalance was discharged as its own change (D-062) once the constraint of preserving the
original seed definitions verbatim was understood to bound the available lever.

### Pacing contract after the remedy (D-062, 2026-10-09)

Every profile the region is balanced against is measured against an authored target, and a
profile that must close and does not fails the gate:

| profile | closure day | documented target |
|---|---:|---|
| high | 77 | ≤120 |
| moderate | 192 | ≤300 |
| irregular | 109 | ≤250 |
| weekend-heavy | 158 | ≤300 |
| pathological | 22 | ≤400 |
| inactivity | 376 | ≤400 |
| low | never (exempt) | documented long tail |

All seven reports are committed under `docs/evidence/m4/` and reproduce byte-for-byte. The
`low` exemption is a recorded decision, not a silent pass: the report prints
`closure criterion: NOT MET` and states the chains left unfinished.

---

> **Balance cadence (2026-10-16, automated verified, headless):** `profile --cadence` and `VisibleChangeCadenceTests` measure how long each profile waits to see the valley change (D-070). high and irregular are healthy (longest gaps 20 and 29 days). low (78), moderate (50) and weekend-heavy (42) exceed the proposed 30-day target, recorded as KI-017. **Resolved by D-071 (visible build stages, content v4):** the longest gaps are now low 27, moderate 10 and weekend-heavy 10 days, with closure unchanged. Unity-editor-tested build-site visual. **M6b readability:** per-kind landmark silhouettes, editor-rendered recapture `docs/evidence/m6-region/valley-editor-rendered-m6b.png`.

# M5 — Mobile shell and low-attention UX

> **M5-H1 headless contract campaign (2026-08-26): the platform-neutral UX state layer is implemented and automated verified** (295 automated tests): durable local preferences/onboarding store with documented corruption/future-version policy and byte-proven canonical isolation (D-042); platform-neutral activity connection/permission status projection with documented precedence, conformance tables, and durable refresh-failure evidence (D-043); shell-facing read models for onboarding/settings/activity-status/support-diagnostics plus explicit Home attention classification; explicit empty/error/recovery semantics incl. save-recovery and future-schema fail-closed states (D-044); eleven named low-attention acceptance scenarios (first-run grant/denial, 1/7/30-day returns, queue-empty, source-failure, external revocation, save recovery, preference isolation, replay-after-UX zero-recredit) plus adversarial hardening suites.
>
> **M5-U1 (implemented 2026-10-16, box-verified):** `src/WalkGame.Unity` on 6000.3.26f1 (D-035) with prebuilt plugins, one composition root on `persistentDataPath` (D-067), a StartStatus router and Home/Return/Projects/Settings (incl. delete-all confirmation)/Activity screens. The batchmode EditMode suite passes 13/13 and a batchmode IL2CPP arm64 debug APK builds (30.7 MB); see `docs/evidence/m5-u1/M5-U1-RESULTS.md`. The APK has never been run on a device, and visual rendering, accessibility and the Health Connect adapter remain UNVERIFIED.
>
> **M5-U2 (merged 2026-10-16, PR #19):** player copy for Projects/Activity/Settings, Region screen, §17 empty states; EditMode 20/20 (Unity-editor-tested).
>
> **M5-U3 (2026-10-16, Unity-editor-tested):** onboarding/first-run polish per UX_DESIGN §11/§12: the guide shows "Step n of 5", landmarks render as stage phrases, the activity step uses player copy, and first-project choices use titles, so no identifier, content key or enum name reaches the guide. EditMode 23/23 and the first **PlayMode** suite 1/1 both pass in batchmode `-nographics` (`tools/unity/run-playmode-tests.sh`; evidence `docs/evidence/m5-u3/`). Device runtime stays UNVERIFIED (KI-002, no KVM).
>
> **M7-A Health Connect adapter (2026-10-16, Unity-editor-tested build; device UNVERIFIED):** `HealthConnectActivityAdapter` plus a Java bridge on the Android 14+ framework API (D-068). It covers permission/revocation/failure states, a Connect action, and a 30-day sync on boot/resume. Headless: 35 adapter/projector tests. EditMode 27/27, PlayMode 1/1. The arm64 IL2CPP APK builds with the bridge classes, `READ_STEPS` and the rationale activity (`docs/evidence/m7-a/`). Gaps are tracked in KI-016.
>
> **M7-B Health Connect deletions (2026-10-16, Unity-editor-tested build; device UNVERIFIED):** change-log deletions reverse credit through the existing deletion path, exactly-once across re-delivery and restart (D-069); 9 new headless tests; the APK builds with the index and change-log code.
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

> **Return-summary restoration moments (2026-10-16, Unity-editor-tested):** "While you were away" shows the valley with each changed landmark circled, leads with transformation, keeps one Continue, and marks older items "Earlier:". No motion. EditMode 39/39 (`docs/evidence/m6-moments/`).
>
> **M6 Region restoration visuals (2026-10-16, Unity-editor-tested; device rendering UNVERIFIED):** the Region screen now shows a procedural valley picture (`RegionScene` model + `ValleyElement` Painter2D). Landmarks grow by restoration stage in size, structure and colour, so state is never shown by colour alone. Hills shift from dry to green with completed restorations. Every picture has a text alternative. No textures, no motion; at most 76 shapes per repaint. Assets are created in-repo and registered in `docs/ASSET_LICENSES.md`. EditMode 35/35, PlayMode 2/2 (`docs/evidence/m6-region/`). Pixels have never been rasterized on the box (no GPU), so visual quality is UNVERIFIED.


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
- [ ] performance budgets supported by measurements *(long-horizon growth/wall-time records landed in M8-H1: `docs/evidence/m8-h1/`; editor-measured shell budgets for the mature save, restored end-state and repeated enter/exit landed in D-074 ([`PERFORMANCE_EDITOR.md`](evidence/m8-h1/PERFORMANCE_EDITOR.md)), with a ScrollView retention fix; device budgets remain open)*;
- [x] migration/recovery evidence exists *(genuine mature-v1 migration + exactly-once post-migration replay, recovery-safe commits — automated verified)*;
- [x] docs reflect actual implementation state *(docs audit 2026-10-16: README status/test counts, WORLD_AND_CONTENT content v4, UX_DESIGN §14 implementation status, PERFORMANCE_BUDGETS editor measurements; device-only claims stay marked UNVERIFIED)*.

**M8-H1 (headless hardening campaign) status:** the Activity and Persistence campaigns plus seeded property testing and long-horizon measurement are complete headlessly — see [`docs/evidence/m8-h1/CAMPAIGN_OUTCOME.md`](evidence/m8-h1/CAMPAIGN_OUTCOME.md). Lifecycle headless half: automated verified (D-072, [`LIFECYCLE_HEADLESS.md`](evidence/m8-h1/LIFECYCLE_HEADLESS.md)), covering repeated resume, process death with torn temp files, interrupted boots, and permission revoked/re-granted. UX/accessibility headless half: Unity-editor-tested (D-073, [`UX_ACCESSIBILITY_HEADLESS.md`](evidence/m8-h1/UX_ACCESSIBILITY_HEADLESS.md)), covering large text, the screen-reader outline/bridge, empty states, and 7-/30-day returns. Real OS kill/reboot/revocation, TalkBack/hardware font scaling and on-device performance items require the Unity runtime lane (still externally blocked, KI-002).

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

**M9 headless status (2026-10-16, D-075):** clean-clone validation (`scripts/validate-clean-clone.sh`) and a verified release evidence package (`docs/evidence/m9/`) exist for the current main. The remaining deliverables are the device matrix, activity-provider evidence, device accessibility and performance/battery evidence (Final Device and Release Qualification Checklist H–Q). All are blocked on hardware (KI-002); M9 cannot exit without them.

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

# Campaign 2026-10-16 (continuation): storyline, features, hardening

## Storyline (D-076, OpenSpec `add-restoration-storyline`)
- [x] A1 Design: OpenSpec change and D-076 (PR #37)
- [x] A2 Domain/content/application: ten milestone-gated chapters, content v5 (additive), `GetStory()`, "New chapter" summary moments, validator rules, cadence gate (`StoryCadenceTests`)
- [x] A4 Unity Story journal screen (Home → Story), chapter moments in the return summary with a "Read the new chapter" link; EditMode 61/61; editor-rendered PNG `docs/evidence/story/journal-editor-rendered.png` (Unity-editor-tested; device UNVERIFIED, KI-002)

## Hardening (C)
- [x] KI-019 per-provider sub-unit step carry (D-079, PR #44)
- [x] Shell input hardening (D-084): exact-name navigation (a comma list such as `nav:Home,Story` used to open a different screen), no game action or game screen before a game is loaded, and `start` can no longer replace a loaded game or an unreadable save. `ShellInputHardeningTests` 21 cases; EditMode 101/101, PlayMode 4 passed + 1 capture-only ignored (`docs/evidence/c-nav-hardening/`, Unity-editor-tested); see KI-021 for the Application-level residual
- [x] KI-021 resolved (D-085): `StartNewGame(seed, overwriteExisting)`; `false` refuses with `ExistingSaveRefused` if any save generation exists (fail closed on I/O errors); the shell uses it; the legacy overload stays the explicit overwrite. `StartOverwriteGuardTests` 6 cases; .NET 653/653; EditMode 102/102 (`docs/evidence/c-ki021/`, Unity-editor-tested)
- [x] Performance pass (D-088): allocation per screen switch measured on all 8 game screens; pooled body-line labels (Story view ~86 KB → ~12 KB/render, 3,000 enter/exits 678 → 392 ms, live-panel growth ~116 → ~78 B/render); `WG_RENDER` log debug-only; EditMode 108/108, PlayMode 4 + 1 ignored. `docs/evidence/c-perf-pass/PERFORMANCE_PASS.md` (Unity-editor-tested; device UNVERIFIED, KI-002)
- [x] Adverse save damage (D-091): truncated / bit-flipped / oversized / re-encoded / NUL primary recovers and persists; both damaged fail closed and a safe start refuses; 11 cases (`docs/evidence/d-save-adverse/`, automated verified)
- [x] Pooled action buttons (D-089, KI-018): per-switch view allocation ≤ ~4 KB on all 8 screens (Settings ~41 KB → ~4 KB), 3,000 enter/exits 384 → 151 ms; EditMode 109/109, PlayMode 4 + 1 ignored. `docs/evidence/d-buttons/PERFORMANCE_BUTTONS.md` (Unity-editor-tested; device UNVERIFIED, KI-002)
- [x] Home story line (D-090): current chapter + next hint on Home, epilogue line once the story is complete; no content/save change; EditMode 111/111 on the merged branch (`docs/evidence/d-home-chapter/`, Unity-editor-tested; device UNVERIFIED, KI-002)
- [x] Release-candidate verification of main `181db31`: clean clone with Unity (.NET 689/689, EditMode 131/131, PlayMode 4 + 1 ignored, APK built) and all 11 assembler claims PASS (`docs/evidence/rc-181db31/`; device UNVERIFIED, KI-002)
- [x] Long-run save growth + save-load fuzz (D-098): 400-day profile gated at ≤ 100 B/day late growth (measured 60 B/day); 800 deterministic mutated-save cases (v1 migration path + current schema) load valid or fail closed untouched; fixed two throw-instead-of-fail-closed paths (null collection, null dedup entry). .NET 692/692 (automated verified)

## Features (B)
- [x] KI-020 ring clipping fixed (D-087): `ValleyElement.RingGeometry` keeps the moment ring inside the picture; sweep test; EditMode 103/103 pre-merge; re-captured `docs/evidence/b-ki020-ring/celebration-editor-rendered.png` (Unity-editor-tested, editor-rendered; device UNVERIFIED, KI-002)
- [x] B1 Region 1 completion celebration in the return summary (D-080): retitled, calm static headline, "See the restored valley" link after Continue; EditMode 65/65 (`docs/evidence/b-celebration/`; Unity-editor-tested; device UNVERIFIED, KI-002)
- [x] B1b Celebration gap closed (D-081): `CelebrationPlayModeTests` drives a real Region 1 completion (77 synthetic days, real pipeline) to the celebration, renders it in a live UIDocument and proves it shows once; editor-rendered PNG `docs/evidence/b-celebration-capture/celebration-editor-rendered.png` (`WG_CAPTURE_SCREEN=Celebration`). PlayMode 4 passed + 1 capture-only ignored (Unity-editor-tested; device UNVERIFIED, KI-002)
- [x] B2 Discoveries and expeditions detail screen (D-082): Region → "Discoveries and expeditions"; found discoveries with kind and date, unfound ones unnamed; routes with status, player-word requirements and reward; read-only. `DiscoveriesScreenTests` 6 cases, EditMode 71/71 (`docs/evidence/b-discoveries/`); editor-rendered PNG superseded by `docs/evidence/b-discovery-copy/discoveries-editor-rendered.png` after D-086 (the original, showing raw keys, is renamed `...-SUPERSEDED-pre-D086.png`; see `docs/evidence/b-discoveries/SUPERSEDED.md`) (Unity-editor-tested; device UNVERIFIED, KI-002)
- [x] B3 Settings text-size preview (D-083): "Text size: N%" line following the OS font scale (clamped 100–200%) plus a bordered sample at the effective scale; read by the screen reader before the actions. `TextSizePreviewTests` 9 cases, EditMode 80/80; editor-rendered PNG at 150% `docs/evidence/b-text-preview/settings-text-150-editor-rendered.png` (Unity-editor-tested; real OS font-scale wiring device UNVERIFIED, KI-002)
- [x] B4 Discovery story and origin text (D-086): authored copy for all 13 Region 1 discoveries (26 keys), storyline-consistent with the Millbrook chapters, shown under each found discovery; content fingerprint and saves unchanged. `DiscoveryCopyTests` 6 cases, `DiscoveriesScreenTests` 8 cases, EditMode 103/103 (`docs/evidence/b-discovery-copy/`, Unity-editor-tested; device UNVERIFIED, KI-002). Re-captured with the authored copy: `docs/evidence/b-discovery-copy/discoveries-editor-rendered.png` (editor-rendered, llvmpipe/Xvfb, NOT device)
- [x] B5 "Your progress" screen (D-099): lifetime Vitality, about-N steps, restorations/discoveries/chapters, no streaks or goals; read-only; EditMode 139/139 merged with main, PlayMode 4 + 1 ignored; editor-rendered PNG `docs/evidence/d099-progress/progress-editor-rendered.png` (Unity-editor-tested; device UNVERIFIED, KI-002). Gap: lifetime "days walked" needs a persisted aggregate (deferred)
- [x] Days walked (D-103, OpenSpec `add-days-walked-count`): local date per record, maintained distinct-day count, schema v2→v3 "at least N" migration; .NET 723/723 (automated verified); Progress line Unity-editor-tested; Health Connect offset on device UNVERIFIED (KI-002)
- [x] Balance check 2026-10-16 (grok2019): the deterministic 400-day pacing for low, irregular and high was rerun after D-100..D-102. High closes on day 77 (target <=120), irregular on day 109 (<=250), low is exempt; cadence gaps 27/7/4 and first change on day 10/2/2, all within D-062/D-071. No tuning needed; the reports are byte-identical to the committed m4 evidence (`docs/evidence/balance/balance-check-2026-10-16-grok2019.txt`, automated verified)
- [x] Days-walked hardening (D-105): validator plausible-range rule; tamper tests; a 600-case fuzz of processedRecords on the v2 and v3 paths that loads valid or fails closed untouched; .NET 728/728 (automated verified)
- [x] Calm optional sound cues (D-104): synthesised restoration, chapter and celebration cues on the return summary. They play only with sound on and once on arrival (Unity-editor-tested; audible output on a device UNVERIFIED, KI-002)
- [x] Copy scan on Home summary text + settings status sentences (D-097): Home lines checked across fresh/walked/30-days-away states; EditMode 133/133 (`docs/evidence/d097-copy-scan/`, Unity-editor-tested)

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
