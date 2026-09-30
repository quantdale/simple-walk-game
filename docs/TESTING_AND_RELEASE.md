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

**Status (M8-H1, automated verified):** covered by `ActivityRedTeamTests`
(permutations, overlaps, corrections/deletions, junk, floods, replays),
`TemporalAnomalyTests` (boundaries, timezone-offset expression, backward clocks,
long absences) and `SessionIngestionTests` (crash-boundary interruption with
exactly-once retry). Permission-revoked and source-exception mid-query scenarios
remain platform-adapter work (M7).

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

**Status (M8-H1, automated verified):** covered by
`PersistenceFaultInjectionTests` and `SessionPersistenceHardeningTests`
(interrupted writes/promotions, corrupt/empty/malformed generation matrix, stale
temporaries, access failures, future schema fail-closed, interrupted-commit
exactly-once retry), `MatureSaveMigrationTests` (genuine v1 fixture migration,
post-migration replay exactly-once) and the `longhaul` CLI verb for months-scale
save growth. Recovery additionally guarantees the last healthy generation survives
recovery re-commits (`WriteAtomicPreservingBackup`, D-040).

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
dotnet --version                                   # toolchain visible; see global.json for the pin
dotnet build SimpleWalkGame.sln --no-incremental   # clean, zero errors, zero warnings
dotnet test SimpleWalkGame.sln                     # all suites green (295 as of the F-12 hardening pass)
scripts/assert-repo-identity.sh                    # exit 0 = right repository
scripts/install-git-hooks.sh                       # core.hooksPath=.githooks, idempotent
scripts/verify-architecture.sh                     # layering / engine-freedom gate, 11 assertions
tests/guards/run-guard-tests.sh                    # guard proof suite, 25 assertions
scripts/verify-ci-steps.sh                         # executes every ci.yml run: step locally

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

### Pinned toolchain version (checkable, since 2026-09-30)

"Documented toolchain version" above is satisfied by a **machine-checkable** pin
rather than by prose. The authority is `global.json` at the repository root:

```json
{ "sdk": { "version": "9.0.100", "rollForward": "latestFeature", "allowPrerelease": false } }
```

| Property | Value | Meaning |
|---|---|---|
| pinned baseline | `9.0.100` | the .NET 9 SDK, feature band 1 |
| `rollForward` | `latestFeature` | a newer feature band or patch of **9.0** is accepted; a different major/minor is a hard, self-explanatory failure |
| `allowPrerelease` | `false` | preview SDKs are excluded, so analyzer diagnostics cannot change between runs |

`dotnet --version` inside the checkout reports the SDK `global.json` actually
resolves to, which is the value to record in release evidence. It is **not**
necessarily the pinned baseline: a machine with 9.0.300 legitimately reports
9.0.300, and that is the intended behaviour of `latestFeature`.

CI installs the same pin via `actions/setup-dotnet` with `global-json-file: global.json`
rather than a hardcoded `9.0.x`, so the runner and a clean local checkout resolve one
toolchain. `scripts/verify-ci-steps.sh` asserts that coupling in both directions and
fails loudly if `global.json` is missing, if the workflow stops declaring it, or if no
installed SDK satisfies the pin — a silently relaxed pin would let the local gate certify
a build on an SDK that CI never uses. All three failure modes were exercised against a
deliberately broken fixture before the assertion was trusted.

Shared build settings are declared once in `Directory.Build.props` (universal:
nullable + warnings-as-errors) with `src/Directory.Build.props` layering the production
C# 9.0 language pin on top, and package versions are declared once in
`Directory.Packages.props` (central package management). See D-051.

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

### Implemented gate status (reconciled 2026-09-29)

| # | Target gate | Status |
|---|---|---|
| 1 | formatting/static checks | **Not adopted** as a *style/format* gate. `dotnet format --verify-no-changes` was deliberately NOT added: it fails against the existing tree and adopting it would force mass reformatting churn across files unrelated to any risk, which this repository's campaign policy forbids. **However**, the compiler/analyzer *warnings* policy is now enforced for every project (`TreatWarningsAsErrors=true`, `Directory.Build.props`): `dotnet build --no-incremental` is 0 errors, 0 warnings. See the build warning policy note below. |
| 2–4 | domain / application / persistence tests | **Implemented** — `dotnet test SimpleWalkGame.sln` (295 tests). |
| 5 | content validation | **Implemented** — the `validate` simulation step runs `GameStateValidator` against `Region1Catalog`; the same validator is a release gate enforced at session construction. |
| 6 | architecture dependency checks | **Implemented** — `scripts/verify-architecture.sh`, added 2026-09-29. Enforces that Domain references nothing and stays on `netstandard2.1`, Application references only Domain, Infrastructure references only Application + Domain, no engine namespace appears anywhere in `src/`, and Domain/Application perform no filesystem access (comments stripped first so prose cannot false-positive). This is the countermeasure for R-017. |
| 7–8 | Unity EditMode / PlayMode | **Blocked** — no Unity 6 LTS editor (D-035). |
| 9 | build smoke test | **Implemented** — `dotnet publish tools/simulation -c Release` must yield a runnable artifact. |
| 10 | documentation/evidence checks | **Partially implemented** — `scripts/verify-pacing-evidence.sh` proves every committed pacing report reproduces byte-for-byte. Full release-branch documentation review remains manual. |

Additional gates implemented 2026-09-29 that were not in the original target list:

- **Exactly-once acceptance** — `walk` followed by `walk --replay`; the replayed window must report `vitality credited: 0` and `duplicate records ignored: 16`. The repository's flagship invariant previously had no CI gate at all.
- **Long-horizon offline simulation** — `longhaul --days 365` (TESTING_AND_RELEASE §7 offline simulation was documented but not run in CI).
- **Performance-scenario / save-growth determinism** — `scripts/verify-save-growth.sh`, added 2026-09-29. Runs the PERFORMANCE_BUDGETS §19 headless scenarios twice and requires the deterministic size/count lines to match, discharging §15's "CI simulation should report save-size growth" obligation. Wall-clock is deliberately **not** gated.

### Build warning policy (enforced, since 2026-09-30)

**There is no longer a warning baseline to carry.** `TreatWarningsAsErrors=true` is set
once, for every project, in `Directory.Build.props`, with no `NoWarn` and no
`WarningsAsErrors` exemptions:

```bash
dotnet build SimpleWalkGame.sln --no-incremental   # 0 errors, 0 warnings
```

This replaces the earlier arrangement in which the policy was explicit `false` in
`WalkGame.Application.csproj` and implicit `false` elsewhere, and in which a clean
`--no-incremental` build still emitted **8 xUnit analyzer warnings** (6x `xUnit2013`,
1x `xUnit2020`, 1x `xUnit2009`, in `SessionIngestionTests`, `M4Region1AcceptanceTests`
and `DiagnosticsReadModelTests`).

Those 8 were **fixed, not suppressed** — each was an assertion *shape* problem
(`Assert.Equal(0, collection.Count)` → `Assert.Empty`/`Assert.Single`,
`Assert.True(false, msg)` → `Assert.Fail(msg)`,
`Assert.False(s.Contains(x))` → `Assert.DoesNotContain(x, s)`). Every fix makes the
assertion strictly stronger; none changes what any test asserts. The test count and
every outcome are identical before and after (295 passed, 0 failed, before and after).
The policy was then proved live: a deliberately unused local in a scratch file makes
`dotnet build` fail with `CS0219` as an **error**, and the scratch file was removed.

Consequences worth stating plainly:

- `dotnet build` — with or without `--no-incremental` — now means the same thing, so the
  previous warning that "0 warnings" must never be cited without `--no-incremental`
  no longer applies to new work. It remains true of **older commits**: evidence recorded
  before this change must keep its `--no-incremental` qualifier.
- A new analyzer warning in a test is now a build failure, which is the point: a test
  warning is how a test rots, and unenforced warnings accumulate.
- `dotnet format` remains deliberately **not** adopted as a gate (row 1 of the table
  above). That decision is unchanged; this policy is a compiler/analyzer policy, not a
  style gate, and it introduced no reformatting churn.

### Hosted CI availability (external blocker, observed 2026-09-29)

Hosted GitHub Actions runs for this repository are currently **not executing**, for an
account-level reason outside the repository's control. Runs `36486102864` (C1) and
`36486996353` (C2) both report `conclusion: failure` with **`steps: 0`** and a two-second
duration, annotated:

> The job was not started because recent account payments have failed or your spending
> limit needs to be increased. Please check the 'Billing & plans' section in your settings

This is **not** a code, test or workflow failure — no runner was ever allocated, so no step
executed. The last green run was `33293682455` on 2026-08-30; every run since has been
refused before starting.

Consequences, recorded honestly rather than papered over:

- **No commit after 2026-08-30 has hosted-CI verification.** Any statement that a change is
  "CI-verified" is false for those commits and must not be written.
- Every gate in this section remains **locally reproducible** and was executed directly:
  `dotnet build`, `dotnet test`, `scripts/verify-architecture.sh`, `scripts/verify-pacing-evidence.sh`,
  `tests/guards/run-guard-tests.sh`, and the simulation verbs. Local execution is evidence of
  the gate passing; it is **not** a substitute for a hosted run and is not reported as one.
- Restoring hosted runs requires an account owner to resolve billing/spending limits. Until
  then, treat hosted CI as an external blocker (condition B) and rely on local gates.
- Mechanical local reproduction is now scripted: `scripts/verify-ci-steps.sh` parses
  `ci.yml`, executes every `run:` step body in checkout order under the GitHub Actions
  Linux default shell, honors `continue-on-error`, marks hosted-only `uses:` steps as
  explicit SKIPs against asserted local facts, and treats parser drift against the
  workflow file as a hard error rather than a silently empty step (D-049). Runs
  `36488324241` (C3) and `36488888405` (HEAD) are in the same no-job-allocated state as
  the two runs above. Status at HEAD `3b5d3e0`: 12 reproduced steps ok, 2 hosted-only
  SKIP, 0 failed (295/295 tests). Evidence: `docs/evidence/ci-local/CAMPAIGN-2026-09-29.md`.

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

Two §18 artifacts now exist as living documents rather than one-off release
attachments: [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md) is the durable severity
register (Critical/High open items block release qualification by rule), and
[`PRIVACY_REVIEW.md`](PRIVACY_REVIEW.md) is the command-verified privacy and
permission review. Both cover the headless lanes only; runtime/device halves
stay explicitly UNVERIFIED until those lanes exist.

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

1. dotnet build SimpleWalkGame.sln — zero errors (180 tests compile clean at M4-H exit).
2. dotnet test SimpleWalkGame.sln — Domain 101 / Infrastructure 25 / Application 54 *(M4-H-era counts; the current suite is 295 — Domain 105 / Infrastructure 48 / Application 142)*:
   - M4ContentGraphTests — authored Region 1 validates with zero violations; content minimum met (19 projects / 6 landmarks / 3 producers / 13 discoveries / 3 expeditions); forward-reference regression fixed; red-team cases for duplicate IDs, broken triggers, unreachable stages/projects/closure.
   - M4ProgressionMechanicsTests — discovery unlock idempotency, expedition availability/completion one-shot semantics + cap-clamped rewards, monotonic arcs, closure once + post-completion evergreen.
   - M4StateValidationTests — canonical-state red team (unknown discovery/expedition IDs, review/timestamp inconsistencies, arc bounds, completion-flag consistency).
   - M4BackwardDecodingTests — pre-M4 v2 payloads strip all new properties, decode with default semantics, validate clean, re-encode stably (D-036).
   - M4Region1AcceptanceTests — THE named acceptance: clean profile through the real trust pipeline to the closure milestone with replay exactly-once, review independence, post-completion stability across reloads and byte-identical determinism.
3. dotnet run --project tools/simulation -- profile --save <dir> --profile low|moderate|high|irregular|weekend-heavy|inactivity|pathological --days 400 — deterministic pacing reports (committed under `evidence/m4/`); rerunning any profile reproduces its report byte-for-byte.
4. dotnet run --project tools/simulation -- walk --save <dir> --days N --at <iso> --replay — unchanged M3 replay proof still credits zero against the expanded graph.
5. `tests/guards/run-guard-tests.sh` — guard proof suite green.


---

## M5-H1 automated evidence (headless)

Named, reproducible gates added by the M5-H1 campaign (all automated verified; runtime/device evidence remains UNVERIFIED and out of scope):

1. dotnet build SimpleWalkGame.sln — zero errors (295 tests compile clean).
2. dotnet test SimpleWalkGame.sln — Domain 105 / Infrastructure 48 / Application 142. M5-H1 suites:
   - LocalPreferencesStoreTests — D-042 store contract: atomic single-file writes, NotFound/Malformed/FutureVersion degradation, absent-keys-mean-default merge, byte-stable rewrites, interrupted-write temporaries ignored, no canonical-state keys in payload.
   - OnboardingAndPreferencesTests — durable forward-only stage flow, canonical first-project gate for Complete (stable error codes), restart resume, preference churn provably byte-neutral to save.json, reminder range validation, missing-store explicit failure, damaged prefs never blocking boot.
   - ActivityStatusProjectionTests — documented six-state precedence table, recommended-action mapping, rapid-transition determinism, zero-record vs mature-history classification, denial does not trap navigation, external revocation without progress mutation, durable source-failure classification with exactly-once retry, side-effect-free reads.
   - DiagnosticsReadModelTests — boot/recovery/migration evidence (recovered-from-backup keeps primary-failure category), mixed-batch counters, watermark age null until a real batch, VersionTooNew via properly framed future envelope, diagnostics available even when boot failed, raw exception text can never leak.
   - M5H1ShellAcceptanceTests — THE named low-attention acceptance: scenarios 1-11 (first-run grant; first-run denial not trapping the profile; calm 1-day return; 7-day app-closed advancement surviving reloads; bounded 30-day long-absence summary inside the glance budget; queue-empty attention reason with banked vitality and unchanged fallback policy; transient source failure preserving progress with exactly-once retry; external permission revocation with intact progress and representable reconnect; calm save-recovery surfacing with diagnostics proof and no silent reset; byte-level preference isolation across restarts; zero-additional replay after onboarding/settings/status/diagnostics operations).
   - M5H1ContractHardeningTests — hostile preferences payload table (never throw, never partially interpret), interrupted writes never promote temporaries, path-independent byte-stable rewrites under churn, documented transition sequences end in exact classifications, read models side-effect-free at zero records AND mature histories, stale diagnostic snapshots frozen and never canonical inputs, onboarding interruption resumed exactly at every durable step with the gate enforced after each restart, explicit per-version-class schema policy, reflection-based sweep proving no raw exception/health text reaches any player-facing surface.
3. Guard proof suite (`tests/guards/run-guard-tests.sh`) — unchanged, green.
4. Simulation smoke + tamper selftest and `tools/simulation walk --replay` exactly-once proof — unchanged contracts consumed unchanged by all new code.
