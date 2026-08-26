# Testing and Release Qualification

## 1. Quality philosophy

The project must optimize for **trustworthy behavior under interruption, replay, absence, and platform variability**, not merely happy-path feature demos.

The highest-risk areas are:

- activity exactly-once crediting;
- save integrity;
- migration;
- offline simulation;
- time/clock behavior;
- lifecycle transitions;
- permission/source failures;
- canonical world-state binding;
- mobile performance.

Testing effort should be weighted accordingly.

---

## 2. Evidence states

Every feature should use one of these states:

- **SPECIFIED** — behavior documented; code may not exist.
- **IMPLEMENTED** — production code exists but verification may be incomplete.
- **AUTOMATED VERIFIED** — applicable automated tests pass.
- **RUNTIME VERIFIED** — behavior exercised successfully in the Unity runtime/editor/player where relevant.
- **DEVICE VERIFIED** — platform/device-specific behavior verified on a representative physical device.
- **RELEASE QUALIFIED** — all required evidence and quality gates pass.

Do not collapse these states into “done.”

---

## 3. Test pyramid

### Pure domain tests — largest layer

Cover:

- activity/reward ledger;
- project state machines;
- resource invariants;
- offline producer simulation;
- expedition resolution;
- discovery eligibility;
- region progression;
- deterministic RNG;
- clock boundaries;
- content rules;
- save-independent state validation.

These tests should be fast enough to run constantly.

### Application tests

Cover orchestration with fake ports:

- activity batch processing;
- transaction sequencing;
- persistence failures;
- checkpoint recovery;
- return-summary generation;
- lifecycle reconciliation;
- migration orchestration;
- diagnostics.

### Infrastructure tests

Cover:

- serializer round trips;
- atomic write/recovery;
- backup handling;
- migration fixtures;
- platform bridge mapping where host tests are possible;
- notification scheduling adapters.

### Unity EditMode tests

Cover:

- content asset validation;
- assembly wiring;
- presentation mapping logic that needs Unity types;
- prefab/content references.

### Unity PlayMode tests

Cover:

- bootstrap;
- scene transitions;
- world-state binding;
- UI flows;
- lifecycle callbacks;
- optional Visit World interactions;
- restart behavior with test persistence.

### Device qualification

Cover behavior impossible to certify in host/editor tests:

- real activity providers;
- permission flows;
- app background/resume;
- notifications;
- real storage behavior;
- thermal/battery/performance;
- mobile accessibility behavior;
- upgrade/migration from installed prior build.

---

## 4. Mandatory domain invariants

At minimum, automate these invariants:

1. Replaying identical activity does not change final state.
2. Reordering independent activity records does not change final credited total.
3. Reward transaction IDs are idempotent.
4. Resource balances never become negative through valid operations.
5. Failed project-start operation consumes nothing.
6. Completing a project twice does not duplicate unlocks/rewards.
7. Expedition result cannot be claimed/applied twice.
8. Save/load round trip preserves canonical state.
9. Same input + same seed + same starting state yields same result.
10. Time moving backward does not produce negative production or duplicated progress.
11. Project dependency graph contains no cycles.
12. Region completion is reachable from valid initial state/content.

---

## 5. Activity red-team suite

Required scenarios:

- same record repeated in same batch;
- same record repeated in later batch;
- overlapping historical windows;
- records in reverse chronological order;
- late arrival;
- source revision/correction;
- deletion;
- zero quantity;
- negative quantity;
- extremely large quantity;
- unsupported unit;
- malformed timestamp;
- future timestamp;
- record spanning time-zone transition;
- permission revoked mid-lifecycle;
- source exception halfway through query;
- app crash after reward calculation;
- app crash after domain mutation but before durable save;
- app crash after save but before checkpoint update;
- replay after recovery.

Each scenario must define expected ledger and game-state outcome.

---

## 6. Persistence red-team suite

Required scenarios:

- clean first launch;
- valid existing save;
- interrupted write;
- corrupted primary with valid backup;
- corrupted primary and backup;
- truncated file;
- unknown future schema version;
- migration from each retained fixture version;
- migration interruption;
- repeated migration attempt;
- low-storage failure where testable;
- invalid invariant after deserialize;
- activity transaction pending at crash boundary;
- many months of synthetic save growth.

Never overwrite the last recoverable save before a migration is proven successful.

---

## 7. Offline simulation suite

Simulate:

- 1 hour;
- 1 day;
- 7 days;
- 30 days;
- very long absence;
- producer reaches cap early;
- project completes during absence;
- multiple queued projects complete;
- queue becomes empty;
- expedition completes;
- multiple systems complete at same timestamp;
- device clock moved backward/forward;
- repeated resume without elapsed time.

Verify deterministic ordering when multiple transitions share a boundary.

---

## 8. Content validation suite

CI should fail on:

- duplicate IDs;
- missing references;
- dependency cycles;
- negative costs/rates;
- impossible prerequisites;
- missing required localization keys;
- missing canonical world-state mapping;
- invalid reward references;
- orphaned project nodes;
- unreachable region completion;
- invalid stage ordering;
- content definitions incompatible with save schema/version rules.

---

## 9. UX acceptance suite

Test from the player’s perspective:

- onboarding with permission grant;
- onboarding with permission denial;
- first project selection;
- one-day return;
- seven-day return;
- no meaningful return changes;
- project completion summary;
- queue empty state;
- discovery result;
- expedition result;
- source unavailable;
- permission revoked;
- save recovered;
- reduced motion;
- screen-reader traversal;
- large text;
- Visit World enter/exit;
- Visit World load failure;
- app killed and relaunched during major flow.

---

## 10. Accessibility qualification

At minimum verify:

- focus order;
- labels/roles/state announcements;
- alternative to drag-only controls;
- contrast/readability;
- text scaling in core screens;
- touch target sizing;
- no color-only critical meaning;
- reduced motion;
- sound/haptics are not required to understand state;
- core progression does not require 3D traversal.

Accessibility failures that block core progression are High severity.

---

## 11. Performance qualification

Use scenarios from `PERFORMANCE_BUDGETS.md`.

At minimum record:

- cold launch;
- warm resume;
- mature-save reconciliation;
- lightweight idle;
- 3D world dense scene;
- 3D world restored end-state;
- repeated world enter/exit;
- background/resume loop;
- memory behavior;
- frame pacing;
- battery/thermal observations.

Editor performance is not equivalent to device performance.

---

## 12. Device matrix

Before MVP release, establish at least:

- one representative lower/mid supported Android device;
- one representative mainstream Android device;
- one supported iPhone tier if iOS ships in the same release;
- current supported OS versions plus at least one older supported version where feasible.

The actual matrix must be recorded with device/OS/build details.

---

## 13. Upgrade qualification

Every release candidate that changes persisted state must test:

1. install previous release/build;
2. create representative mature save;
3. process activity;
4. update app in place;
5. migrate;
6. verify state/invariants;
7. process duplicate/overlapping activity again;
8. verify no duplicated rewards;
9. continue gameplay;
10. restart device/app and reverify.

Clean installs alone are not adequate release evidence.

---

## 14. Clean-clone gate

A release candidate must be reproducible from a clean checkout.

Required:

- documented toolchain version;
- dependencies resolve;
- domain tests run;
- Unity project opens/imports without undocumented manual repair;
- build configuration exists;
- content validation runs;
- no required secret is committed;
- platform-specific setup is documented;
- generated local state is not accidentally required.

Executable headless verification (run from the repository root; CI executes the same
commands in `.github/workflows/ci.yml`):

```bash
dotnet --version                                   # toolchain visible (9.x today)
dotnet build SimpleWalkGame.sln                    # clean, zero errors
dotnet test SimpleWalkGame.sln                     # all suites green (156 as of M3)
scripts/assert-repo-identity.sh                    # exit 0 = right repository
scripts/install-git-hooks.sh                       # core.hooksPath=.githooks, idempotent
tests/guards/run-guard-tests.sh                    # guard proof suite, 25 assertions

# deterministic simulation smoke + tamper selftest:
SAVES=$(mktemp -d)
dotnet run --project tools/simulation -- new      --save "$SAVES" --seed 7 --at 2026-08-20T08:00:00Z
dotnet run --project tools/simulation -- simulate --save "$SAVES" --days 5 --start 2026-08-20T08:00:00Z
dotnet run --project tools/simulation -- validate --save "$SAVES" --selftest
rm -rf "$SAVES"

# M3 acceptance harness (normalized records through the trust pipeline,
# session recreated from disk every window) + replay exactly-once proof:
SAVES=$(mktemp -d)
dotnet run --project tools/simulation -- walk    --save "$SAVES" --days 16 --at 2026-08-20T08:00:00Z --steps-per-day 20000
dotnet run --project tools/simulation -- walk    --save "$SAVES" --days 16 --at 2026-08-20T08:00:00Z --steps-per-day 20000 --replay   # must credit zero
dotnet run --project tools/simulation -- validate --save "$SAVES" --selftest                                                        # schema v2, 0 violations
rm -rf "$SAVES"
```

Windows users without Git Bash may run the PowerShell twins
(`scripts\assert-repo-identity.ps1`, `scripts\writer-lease.ps1`,
`scripts\install-git-hooks.ps1`); the proof suite itself requires a POSIX shell
(Git Bash on Windows).

---

## 15. Severity model

### Critical

- data loss/corruption with no safe recovery;
- reward duplication exploit in ordinary/replay path;
- crash on core flow for supported device;
- privacy-sensitive data exposure;
- app cannot launch/update for supported users.

### High

- eligible activity routinely lost;
- permission flow prevents use/recovery;
- core project progression blocked;
- migration fails for supported prior save;
- severe performance/battery issue;
- core accessibility flow impossible;
- incorrect canonical world state after restart.

### Medium

- non-blocking visual/state presentation issue;
- isolated UX confusion;
- optional content defect;
- moderate performance regression outside core flow.

### Low

- cosmetic polish issue;
- minor copy/layout problem with workaround.

No known Critical or High defect may remain open at release qualification.

---

## 16. CI gates

Target CI pipeline:

1. formatting/static checks where adopted;
2. pure domain tests;
3. application tests;
4. persistence/migration tests;
5. content validation;
6. architecture dependency checks;
7. Unity EditMode tests;
8. Unity PlayMode tests where CI runtime supports them;
9. build smoke test;
10. documentation/evidence checks for release branches.

A failure must be treated as a real blocker or explicitly quarantined with owner, rationale, and remediation criteria.

---

## 17. Flaky test policy

Flaky tests are defects.

Do not normalize rerunning CI until green.

If a test is quarantined:

- identify it explicitly;
- document why;
- preserve failure evidence;
- create a repair task;
- avoid claiming the affected behavior as fully automated-verified.

---

## 18. Release evidence package

A release candidate should produce an evidence summary containing:

- commit SHA;
- build/version;
- test suite results;
- migration fixtures tested;
- device matrix;
- activity provider scenarios;
- performance measurements;
- known issues by severity;
- privacy/permission review;
- accessibility verification;
- clean-clone/build result;
- remaining unverified claims.

This can be a Markdown artifact committed under a future `verification/` or `docs/releases/` path.

---

## 19. Documentation reconciliation

Before release, compare documentation against implementation.

Update claims so that:

- future features are not described as shipped;
- editor-tested behavior is not called device-tested;
- unsupported platform behavior is not implied;
- performance numbers reflect measured builds;
- known limitations are explicit;
- architecture diagrams/contracts match actual dependency structure.

Documentation drift is a quality defect.

---

## 20. Release qualification checklist

A build can be marked RELEASE QUALIFIED only when:

- [ ] clean clone succeeds;
- [ ] all required automated suites pass;
- [ ] no known Critical/High issues remain;
- [ ] activity exactly-once scenarios pass;
- [ ] save recovery scenarios pass;
- [ ] supported migrations pass;
- [ ] offline simulation passes;
- [ ] content validation passes;
- [ ] core UX acceptance passes;
- [ ] accessibility qualification passes;
- [ ] device performance budgets pass;
- [ ] lifecycle/background-resume scenarios pass;
- [ ] permission/revocation scenarios pass;
- [ ] privacy behavior is reviewed;
- [ ] documentation is reconciled;
- [ ] evidence package records remaining limitations honestly.

---

## M4-H automated evidence (headless)

Named, reproducible gates added by the M4-H campaign (all automated verified; runtime/device evidence remains UNVERIFIED and out of scope):

1. dotnet build SimpleWalkGame.sln — zero errors (180 tests compile clean).
2. dotnet test SimpleWalkGame.sln — Domain 101 / Infrastructure 25 / Application 54:
   - M4ContentGraphTests — authored Region 1 validates with zero violations; content minimum met (19 projects / 6 landmarks / 3 producers / 13 discoveries / 3 expeditions); forward-reference regression fixed; red-team cases for duplicate IDs, broken triggers, unreachable stages/projects/closure.
   - M4ProgressionMechanicsTests — discovery unlock idempotency, expedition availability/completion one-shot semantics + cap-clamped rewards, monotonic arcs, closure once + post-completion evergreen.
   - M4StateValidationTests — canonical-state red team (unknown discovery/expedition IDs, review/timestamp inconsistencies, arc bounds, completion-flag consistency).
   - M4BackwardDecodingTests — pre-M4 v2 payloads strip all new properties, decode with default semantics, validate clean, re-encode stably (D-036).
   - M4Region1AcceptanceTests — THE named acceptance: clean profile through the real trust pipeline to the closure milestone with replay exactly-once, review independence, post-completion stability across reloads and byte-identical determinism.
3. dotnet run --project tools/simulation -- profile --save <dir> --profile low|moderate|high|irregular --days 400 — deterministic pacing reports (committed under vidence/m4/); rerunning any profile reproduces its report byte-for-byte.
4. dotnet run --project tools/simulation -- walk --save <dir> --days N --at <iso> --replay — unchanged M3 replay proof still credits zero against the expanded graph.
5. 	ests/guards/run-guard-tests.sh — guard proof suite green.
