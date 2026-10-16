# Planner → Executor Handoff

Additive; stricter local rules win. A planner writes `.agent/EXECUTION_PROMPT.md` only after auditing actual code/tests/docs, recent commits/diffs, useful issues/PRs, and native state. It defines Status, Planned-From, target branch, one high-impact campaign, scope, workstreams, constraints, validation, acceptance/completion gates, and Git/reporting requirements; then commits/pushes and stops without implementing.

Planner obligations: never leave a superseded prompt marked ACTIVE; every execution prompt must inherit the `AGENTS.md` preflight (identity guard, writer lease, no force-push) rather than redefining it; prompts must target THIS repository's canonical slug (`quantdale/simple-walk-game`) and never reference sibling repositories.

For `/goal continue`: read repository instructions, this file, the execution prompt if present, and native state; run the `AGENTS.md` preflight BEFORE any write; reconcile current Git/implementation; resume an ACTIVE prompt from the first genuinely incomplete requirement without redoing landed work; validate, fix introduced Critical/High regressions, update state, and commit/push per local policy. Otherwise use native continuation or report that planning is required.

## Session note 2026-10-16 (grok-exec-20261016-m5u3)
- Merged PR #19 (M5-U2). Fixed RELEASE_CHECKLIST G (KI-005 closed via 589150b).
- M5-U3 onboarding polish + first PlayMode suite (EditMode 23/23, PlayMode 1/1, Unity-editor-tested). KI-015 records the reversion findings.
- Rule learned: run Unity only in a dedicated worktree and pass `EVIDENCE_DIR` per campaign. Next: runtime/device lane is still blocked on KI-002 (KVM or Michael's phone).

## Session note 2026-10-16 (M7-A, grok1135)
- Merged PR #21: `new-agent-worktree.sh` now honours `SWG_SESSION_ID` (guard scenario 12).
- M7-A Health Connect adapter (D-068) on a dedicated worktree: headless contract tests, Java bridge compiled into the arm64 APK. Device UNVERIFIED (KI-002); gaps in KI-016.
- Next slice: KI-016(a), deletions through the Health Connect changes API (headless, same bridge pattern). Alternatively, Region restoration visuals if runtime is still blocked.

## Session note 2026-10-16 (M7-B + M6 Region visuals, grok1149)
- Merged PR #23: Health Connect change-log deletions (D-069), closing KI-016(a) at contract level.
- M6 Region restoration visuals: a procedural valley picture with created assets (docs/ASSET_LICENSES.md). EditMode 35/35, PlayMode 2/2; device rendering UNVERIFIED.
- Next slice: return-summary restoration moments (UX_DESIGN §8: show what changed on the valley picture), or a device run if KI-002 unblocks.

## Session note 2026-10-16 (grok1207)
- Merged #25 (return-summary restoration moments). #26: editor-rendered Region PNG (llvmpipe/Xvfb) plus capture tooling.
- Balance cadence: KI-017 is resolved by D-071 (content v4 visible build stages). Longest gaps are now low 27, moderate 10 and weekend-heavy 10 days; the cadence gate is in VisibleChangeCadenceTests.
- Valley readability (M6b) is done: per-kind landmark silhouettes; recapture at docs/evidence/m6-region/valley-editor-rendered-m6b.png (editor-rendered).
- M8 lifecycle headless is done (D-072, 4 tests).
- M8 UX/accessibility headless is done (D-073): OS font scale, screen-reader outline and AssistiveSupport bridge, empty states, 7/30-day returns; Unity-editor-tested, and the Android debug APK compiles.
- M8 performance in the editor is done (D-074): mature-save/end-state/enter-exit budgets, ScrollView reuse fix, KI-018 residual.
- M8 docs audit is done: README, WORLD_AND_CONTENT, UX_DESIGN §14 and PERFORMANCE_BUDGETS reconciled; ROADMAP exit item checked.
- M9 headless: scripts/validate-clean-clone.sh and the assembler's --clean-clone / hosted-CI check are in (D-075). The Final Device and Release Qualification Checklist is expanded to H–Q.
- Verified release evidence package for 0f9f3d7 is committed at docs/evidence/m9/ (all 11 mechanical claims PASS, including clean clone and hosted CI).
- The final campaign report is at .agent/FINAL_CAMPAIGN_REPORT.md. Everything feasible without a device is done; next steps are in its §6 (device lane H–Q, Needs Michael).

## Session note 2026-10-16 (storyline A1/A2, grok-story-1520)
- Merged #37 (`7dc59dd`): storyline OpenSpec plus D-076.
- A2: ten derived chapters, content v5 with no save change, `StoryChapterUnlocked` emitted in `AllocateVitality`, `GetStory()`, and "New chapter" return-summary items. 608 .NET tests; compatibility and pacing gates green; cadence bounds in D-076.
- Next: A4 Unity Story screen. C: KI-016(a) re-read on expired token, KI-007 fixture identity, save/bridge tamper tests.
- A4: Story journal screen and chapter-moment link; EditMode 61/61; journal PNG at docs/evidence/story/ (editor-rendered).

## Session note 2026-10-16 (A4 + C, grok1537)
- #39 A4 Story journal; #40 KI-007 `--allow-no-origin`; #41 KI-016(a) expired-token recovery (D-077), bridge tamper tests, id bounds, KI-019 logged; this PR: save tamper (D-078).
- Next: KI-019 sub-unit carry (economy; needs a D-entry), then B (MVP features).

## Session note 2026-10-16 (hardening + B, grok1628)
- #43 renamed the A4 journal PNG to `docs/evidence/story/journal-editor-rendered.png`.
- KI-019 closed by D-079 (per-provider sub-unit step carry); `StepCarryTests` 7 cases; pacing-pathological +1 Vitality regenerated.
- B1: Region 1 completion celebration in the return summary (D-080), EditMode 65/65.
- Next B candidates: discoveries/expeditions detail screen; settings polish (text size preview).

## Session note 2026-10-16 (B1b–B3 + C, grok1651)
- #46 (D-081): celebration reached from a real completed region (`CompletedRegionFixture`, day 77) in PlayMode, with an editor-rendered PNG. Found KI-020 (ring clipping).
- #47 (D-082): Discoveries and expeditions detail screen. #48 (D-083): Settings text-size preview (follows the OS, no override).
- This PR (D-084): shell input hardening. Exact-name nav, no game actions before load, `start` cannot replace a game or an unreadable save. KI-021 is the Application-level residual.
- Capture now takes `WG_CAPTURE_SCREEN=Celebration|Discoveries|Settings` and `WG_CAPTURE_TEXT_SCALE`.
- Next: KI-020 ring inset; KI-021 decision; authored discovery body copy (keys only today); device lane (KI-002).

## Session note 2026-10-16 (grok1724)
- #50 (D-085): KI-021 resolved with `StartNewGame(seed, overwriteExisting)`; `false` refuses with `ExistingSaveRefused`; the shell uses it; the legacy overload is the explicit overwrite.
- #51 (D-086): authored discovery body and origin copy (`Region1DiscoveryCopy`, 26 keys), shown on the Discoveries screen; content fingerprint unchanged.
- #52 (D-087): KI-020 resolved (`ValleyElement.RingGeometry`); re-capture at `docs/evidence/b-ki020-ring/celebration-editor-rendered.png`.
- This PR (D-088): perf pass. Allocation per screen switch measured, pooled line labels, `WG_RENDER` debug-only; `docs/evidence/c-perf-pass/PERFORMANCE_PASS.md`.
- Unity Mono returns 0 from `GC.GetAllocatedBytesForCurrentThread`; use `GC.GetTotalMemory` deltas, which have 4 KB resolution.
- Next: pool or diff buttons too (largest remaining view allocation, KI-018); a Discoveries PNG with the new copy; Region 2 storyline design (OpenSpec first); device lane (KI-002).

## Session note 2026-10-16 (grok1745)
- D-089 (KI-018): pooled action buttons; per-switch view allocation ≤ ~4 KB on all screens, 3,000 enter/exits 384 → 151 ms (editor). `docs/evidence/d-buttons/`.
- Discoveries PNG re-captured with D-086 copy: `docs/evidence/b-discovery-copy/discoveries-editor-rendered.png` (editor-rendered). The older `b-discoveries/` PNG shows the pre-copy keys and is renamed `-SUPERSEDED-pre-D086` (see its SUPERSEDED.md).
- D-090: Home story line (current chapter + next hint; epilogue once complete). Region 2 storyline still needs OpenSpec first.
- D-091: `SaveRecoveryAdverseTests` (11) for byte-level primary/both damage; .NET 670 total.
- Next: onboarding/permission edge cases at the shell (denied-then-granted mid-onboarding); Region 2 storyline OpenSpec; device lane (KI-002).

## Session note 2026-10-16 (grok1814)
- D-092: onboarding/permission edge cases. A fresh-device start resets a stale onboarding stage left by a kill mid delete-all. `StaleOnboardingResetTests` (5), `OnboardingPermissionEdgeTests` (5); .NET 675, EditMode 116/116 (Unity-editor-tested). Device permission dialog UNVERIFIED (KI-002).

## Session note 2026-10-16 (grok1814, Region 2)
- `openspec/changes/add-region2-storyline/` is PROPOSED only. D-011 and the ROADMAP post-MVP gate forbid agent approval; logged in the KNOWN_ISSUES morning queue. No implementation.
- Worktree cleanup: 37 merged, clean worktrees removed with `git worktree remove` (no --force). Kept: `wt-checkin-grok1537` and `wt-story-openspec-grok1520` (merged but with uncommitted changes; a human should inspect). Branches were not deleted.

## Session note 2026-10-16 (grok1833)
- #61 (`bbb0d51`): `new-agent-worktree.sh` branches from a freshly fetched `origin/main` and refuses if the fetch fails; guard scenario 14, suite 49/49. #62 (`217d445`): blank lines in this file.
- D-093: outdated Health Connect provider gets an Update action (it used to show Connect or Retry, both dead ends) and 9 failure-state tests (uninstalled, revoked mid-read, backward clock, timezone, 40-day gap). .NET 684, EditMode 116/116 (Unity-editor-tested); device UNVERIFIED (KI-002).
- Not done: a deep link to the Play Store for the update; an accessibility audit beyond D-073. Region 2 is still PROPOSED only.

## Session note 2026-10-16 (grok1844)
- D-094: onboarding shows the D-093 update-required state (no Connect dead end). .NET 689, EditMode 117/117 (Unity-editor-tested); device UNVERIFIED (KI-002).
- D-095: accessibility audit across all 12 screens (focus order, labels, 48 dp touch, theme contrast, reduced motion); fixed primary-button contrast 6.29→8.03:1. EditMode 125/125 (Unity-editor-tested).
- D-096: copy polish + source-level no-guilt check (CopyPolishTests); domain error text no longer shown to players. EditMode 131/131 (Unity-editor-tested).

## Session note 2026-10-16 (grok1929)
- #68: release-candidate verification of main `181db31`. A clean clone with Unity gave .NET 689/689, EditMode 131/131, PlayMode 4 + 1 ignored, and the APK built; the assembler passed 11/11 (`docs/evidence/rc-181db31/`). The first attempt failed only because `dotnet` was not on PATH in the detached run. Export `PATH=$HOME/.dotnet:$PATH` before `validate-clean-clone.sh`.
- #67 (D-098): 400-day save-growth gate (60 B/day) and an 800-case save-load fuzz. It found and fixed two paths that threw instead of failing closed (a null collection, a null dedup entry). .NET 692.
- #69 (D-097): Home summary lines are now in the copy scan; settings status lines are plain sentences. EditMode 133.
- This PR (D-099): the "Your progress" screen. EditMode 139/139 merged; editor-rendered PNG.
- `dev:walk` ingests relative to the real `UtcNow`. A shell test that walks must use `new ManualClock(DateTimeOffset.UtcNow)`, or nothing is credited.
- Next: lifetime "days walked" (needs a persisted aggregate and a D-entry); richer project-completion moments; device lane (KI-002). Region 2 is still PROPOSED only.

## Session note 2026-10-16 (grok1954)
- #71 (D-100): a finished project in the return summary gets one calm line: the landmark stage it brings and its story chapter (`ProjectCompletionMoments`, derived at render time; stored text unchanged).
- #72 (D-101): valley ambience: footpaths between recovered neighbours, meadow flowers, birds; MaxShapes 86→100 (the existing cap); editor-rendered before/after PNGs in `docs/evidence/d101-valley/`.
- D-102: lifetime "days walked" is deferred. Retired rows keep no date, and records have no local day, so a v3 migration would be dishonest. The future OpenSpec design is in D-102. Hardening: `TitleMatchingInvariantTests`.
- Removed the 4 merged, clean -grok1929 worktrees (plain `git worktree remove`). Region 2 is still PROPOSED only.
- Merged-tree check on this branch: .NET 703/703, EditMode 144/144 (Unity-editor-tested).
- Next: the D-102 OpenSpec (local date per record); device lane (KI-002).

## Session note 2026-10-16 (grok2019)
- #74 (D-103): days walked is shipped. The OpenSpec `add-days-walked-count` was agent-approved (Region 1; D-011 gates only Region 2). It stores a local date per record (Health Connect endZoneOffset, an optional sixth bridge field), keeps a maintained distinct-day count, and migrates schema v2→v3 to a proven lower bound shown as "at least N". Health Connect offset on a device: UNVERIFIED (KI-002).
- #75: balance check. Low, irregular and high were rerun and all are on target (high d77, irregular d109, low exempt; gaps 27/7/4). No tuning was needed. Evidence: `docs/evidence/balance/balance-check-2026-10-16-grok2019.txt`.
- #77 (D-105): validator plausible-range rule for days walked, plus tamper tests and a 600-case fuzz.
- This PR (D-104): calm optional sound cues on the return summary. They are synthesised, play only with sound on and only on arrival; adds the built-in audio module.
- Counts: .NET 728/728 on main after #77; EditMode 151/151 on this branch merged with main before D-105 (Unity-editor-tested).
- Removed the 3 merged, clean -grok1954 worktrees with plain `git worktree remove`. Region 2 is still PROPOSED only.
- Next: Home layout polish with a PNG; device lane (KI-002); an APK build to compile the Java offset change.

## Session note 2026-10-16 (grok2058)
- The APK built from main `62f33d4` compiles the D-103 Java offset change: `getEndZoneOffset` is in `classes3.dex`, and no fix was needed. The full rerun on main after D-105 gave .NET 728/728, EditMode 151/151 and PlayMode 4 + 1 ignored. Evidence: `docs/evidence/apk-d103/RESULTS.md`. Device: UNVERIFIED (KI-002).
- #78 (`7ae6de3`) records that evidence. D-106: Home layout polish (no repeated line, no doubled empty-state message); before/after PNGs in `docs/evidence/d106-home/` (editor-rendered); EditMode 154/154 (Unity-editor-tested).

## Session note 2026-10-16 (grok2121)
- #80 (`c260c6f`): screen tour. `tools/unity/capture-screen-tour.sh` captures all 13 `ShellScreen`s plus the celebration into `docs/evidence/screen-tour/` (editor-rendered, 14/14).
- #81 (`b7ca427`, D-107): battery and background-sync budget review. Reads happen only on boot and resume, over at most 30 days, with no polling; `BackgroundSyncBudgetTests` (5).
- #82 (`04d4c71`, D-108): iOS parity check. No HealthKit adapter exists; KI-022, `docs/PLATFORM_PARITY.md`, `PlatformParityTests` (4).
- This PR (D-109): fixes the copy defects the tour found. EditMode 161/161 on the merged tree (Unity-editor-tested).
- Next: data export / privacy screen (needs OpenSpec, since it is a new player feature); the HealthKit adapter (needs macOS); device lane (KI-002). Region 2 is still PROPOSED only.

## Session note 2026-10-16 (grok2144)
- #84 (`709a921`, D-110): with a game loaded, `nav:BootError` is refused calmly and the screen stays put. EditMode 167/167.
- #85 (`56c24cb`, D-111): OpenSpec `add-privacy-data-export`, agent-approved (Region 1/shell scope, as D-103). Privacy screen from Settings; a checksum-verified readable JSON export goes to the Android share sheet as text. No network and no shared-storage file. .NET 739, EditMode 174 merged. Device share sheet UNVERIFIED (KI-002).
- #86 (`3744530`, D-112): KI-022 started. The `HealthKitActivityAdapter` is headless-tested (35 cases, fake bridge). The P/Invoke and Swift bridges are UNVERIFIED (never compiled) and NOT composed. `docs/HEALTHKIT_SETUP.md` covers the entitlement and usage string. .NET 774, EditMode 174.
- This PR: `PlatformParityTests` pins that the bootstrap does not compose the unverified HealthKit bridge. EditMode 175/175 (Unity-editor-tested), evidence in `docs/evidence/records-grok2144/`.
- Next: a Privacy PNG in the screen tour; an APK build to compile the `AndroidShareSheet` JNI path; HealthKit composition only after a macOS build. Region 2 is still PROPOSED only.


## Session note 2026-10-16 (grok2212)
- #88 (`0e71b27`): an arm64 debug APK built from main `0574e21` compiles the D-111 `AndroidShareSheet` path. Its Android-only literals are in `global-metadata.dat`. No fix was needed. Evidence: `docs/evidence/apk-d111/RESULTS.md` (compile only; device share sheet UNVERIFIED, KI-002).
- #89 (`0cf22d2`): the screen tour includes Privacy. `docs/evidence/screen-tour/privacy-editor-rendered.png` (editor-rendered); the tour is now 15 PNGs.
- #90 (`27d866a`, D-113): HealthKit prep, headless. `HealthKitSampleLedger` prunes the seen-samples maps (30 + 7 days, cap 50,000) and the Swift bridge mirrors it (UNVERIFIED). `HealthKitSourceDedup` is the Watch > iPhone > other design, NOT wired: it needs a bridge source field and a D-entry. The reinstall table is in `docs/HEALTHKIT_SETUP.md`. .NET 789/789, EditMode 175/175. The adapter is still not composed.
- This PR: `PlatformParityTests` pins the prune window to `SyncWindowDays` and keeps the D-113 types out of the shell; EditMode 176/176 (Unity-editor-tested, `docs/evidence/records-grok2212/`). Merged worktrees removed with plain `git worktree remove`.
- Next: a bridge protocol source field so the dedup can be wired (macOS needed to verify); device lane (KI-002). Region 2 is still PROPOSED only.

## Session note 2026-10-16 (grok2230)
- #92 (`d5d1ef5`): `.github/workflows/ios-bridge-compile.yml`. The repo is public and `macos-latest` is a standard runner, so it is free per the GitHub Actions billing docs (no artifacts, no cache). It type-checks and compiles the Swift HealthKit bridge with `xcrun --sdk iphoneos swiftc` (arm64, iOS 15 target) and checks the four `@_cdecl` symbols. It passed first time (Xcode 26.6, Swift 6.3.3); no fix was needed. Label: compiled on hosted macOS, not device-tested. Evidence: `docs/evidence/macos-swift-compile/RESULTS.md`.
- #93 (`cd498f3`, D-114): optional sixth bridge field `watch|phone|other`; the adapter applies `HealthKitSourceDedup` per response, omitting zero shares. Swift classification compiled on hosted macOS. The adapter is still NOT composed. .NET 803/803, EditMode 176/176. Residuals are in D-114 and KI-022.
- #94 (`7274a2b`): `docs/evidence/privacy-export-scrolled/` (editor-rendered). The capture takes `WG_CAPTURE_SCROLL=end` and `WG_CAPTURE_EXPECT_BUTTON`, which asserts that the button is fully visible.
- #95 (`7d63fb1`): `ScreenTourCoverageTests` (EditMode 178/178): the screen tour must list every `ShellScreen`.
- Next: Unity iOS export plus an Xcode build on hosted macOS (still free; no signing) to verify the P/Invoke branch and linking; device lane (KI-002). Region 2 is still PROPOSED only.

## Session note 2026-10-16 (grok2251)
- D-115: the free iOS Build Support module installs on the box (`unity install-modules -e 6000.3.26f1 -m ios`; needs apt `xz-utils cpio p7zip-full`). `tools/unity/export-ios-xcode.sh` exports the Xcode project in batchmode; `tools/unity/push-ios-export.sh <tag>` pushes it as chunks to a throwaway `ios-export/<tag>` branch; `ios-xcode-export-verify.yml` (macos-latest, free) built it unsigned: BUILD SUCCEEDED first time, the four `wg_hk_*` symbols are exported, and HealthKit is linked. Label: Xcode-built unsigned on hosted macOS, not device-tested. The adapter is still NOT composed. `ios-export/grok2251-a` is left for the owner to delete.

- Tooling: an absolute `EVIDENCE_DIR` is now used as-is by `run-editmode-tests.sh`, `build-android-debug.sh` and `export-ios-xcode.sh` (it used to create a stray `tmp/` inside the worktree); `export-ios-xcode.sh` fails fast without the iOS module. EditMode 178/178 with `EVIDENCE_DIR=/tmp/...` (Unity-editor-tested).

## Session note 2026-10-16 (grok2334)
- D-116: Unity engine binaries must never be pushed to this public repo. The owner deleted `ios-export/grok2251-a` (it carried `libiPhone-lib.a`). `push-ios-export.sh` now refuses (exit 3); `ios-xcode-export-verify` is manual-only and fails closed until a private transport exists. D-115 evidence (run 38920379246) stays valid.
- New guard `scripts/verify-no-engine-binaries.sh` (tracked names + content magic, `--history`, `--self-test`) runs in CI and in pre-commit (`--staged`).
- Audit finding: `3ca344e` had tracked the root `Library/` and `Logs/`; now untracked and ignored. History purge is an owner decision (morning queue). `origin/main` history never contained `libiPhone-lib.a` or export chunks.
- Never use the retired public export transport again.
- D-117 (KI-015): `scripts/verify-project-settings.sh` makes "never commit ProjectSettings.asset by accident" mechanical. Pre-commit `--staged` needs `SWG_PROJECT_SETTINGS_DECISION=D-NNN`; CI `--range` needs a `Project-Settings-Decision: D-NNN` trailer. Self-test 6/6.
- Remaining open KIs (002, 016, 018, 022) are device- or macOS-gated. Region 2 is still PROPOSED only.


## Session note 2026-10-17 (grok2345)
- #101 (`5dab899`, D-118): Nell Hartley's letters. Six letters tied to project completion, a pure projection (no save or content change). OpenSpec `add-landmark-letters` (agent-approved, Region 1).
- #102 (`7e61893`, D-119): an optional gentle daily reminder. It needs both opt-ins plus OS permission, and is never re-asked. It uses an inexact RTC alarm and a low-importance channel. OpenSpec `add-gentle-reminder`. Compiled into the arm64 APK (`docs/evidence/apk-d119/`); device delivery UNVERIFIED (KI-002). Residuals: no boot receiver, no time picker, no iOS delivery.
- #103 (`f7b1753`, D-120): `PlayerStrings` table plus `LocalizationReadinessTests` ratchet; the remaining migration is KI-023.
- This PR: `GetLetters` read-only hardening test and records. .NET 807/807, EditMode 191/191 on main `f7b1753` + this branch.
- Rule: no Codex PR reviews (no @codex, no review requests); use self-review and CI only.
- Next: continue KI-023 (ShellController 127 → 0, then the Application copy); a reminder time picker; device lane (KI-002). Region 2 is still PROPOSED only.

## Session note 2026-10-17 (grok0020)
- KI-023: #105 (`0f810ca`) PlayerCopy and RegionScene, #106 (`da67877`) and #107 (`75a6ca1`) ShellController 127 → 0, all routed through `PlayerStrings` with identical text; the ratchet now lists them as migrated.
- This PR (D-121): reminder time picker (earlier/later buttons) plus `ReminderBootReceiver`. EditMode 205/205; arm64 APK compiles the receiver (`docs/evidence/apk-d121/`). Device UNVERIFIED (KI-002).
- Next: Application-layer copy into a table (KI-023); device lane (KI-002). Region 2 is still PROPOSED only.

## Session note 2026-10-17 (grok0120)
- #109 (`f1c3bc3`): KI-023 part 2. AccessibleOutline and AndroidShareSheet copy moved to `PlayerStrings`; ShellBootstrap logs and ShellConnectionPort diagnostics are marked `l10n-ignore`. Every shell file is now at zero and no ceilings remain. EditMode 205/205.
- #110 (`7e8aa13`, D-122): content-copy localization design. A presentation-only `ContentText` overlay is keyed by stable id + field and falls back to the authored English; content, versions and saves are untouched. Letters are the first slice. .NET 811.
- #111 (`521920e`, D-123): `ReminderTime.java` (pure) with a headless 200k-case property test across all time zones and DST, plus the restart decision table. Fix: a damaged stored reminder time no longer makes "turn on" a dead end. .NET 813.
- RC on `521920e`: `docs/evidence/rc-521920e/` (11/11 mechanical PASS, clean clone with Unity and APK). Device UNVERIFIED (KI-002).
- Next: D-122 slices (titles by id in read models, chapters, discovery copy, glance phrases), then a language preference. Device lane (KI-002). Region 2 is still PROPOSED only.

## Session note 2026-10-17 (grok0157)
- Dependabot triage: #1/#2/#3 recreated on main as #113 (`f93d751`, checkout v7, setup-dotnet v6, upload-artifact v6; Node 24) and closed as superseded. #6 recreated as #114 (`65c27ba`, Test.Sdk 18.10.1, xunit 2.9.3, runner 4.0.1, coverlet 10.1.0) and closed. #5 (System.Text.Json 10) closed: runtime/Unity pin, see KI-024; dependabot.yml now ignores its majors.
- #115 (`70b467e`): KI-023 slice 2 (D-122). Titles, chapters and discovery copy route through `ContentText`; `ContentTitleOverlayTests` (2). .NET 815, EditMode 205/205.
- Next: glance phrases plus expedition/producer titles (D-122), then a language preference. Device lane (KI-002). Region 2 is still PROPOSED only.

## Session note 2026-10-17 (grok0215)
- #120 (`0db15d9`, D-124): System.Text.Json 8.0.5 → 10.0.13 (LTS); the plugin sync adds System.IO.Pipelines. EditMode 205/205; PlayMode 4 passed + 1 ignored (capture); arm64 IL2CPP APK built; iOS Xcode export built locally only (not pushed, D-116); stripped assemblies inspected. KI-024 closed. Evidence: `docs/evidence/stj10-grok0215/`. Device IL2CPP runtime UNVERIFIED (KI-002).
- #121 (`a8b5339`): KI-023 slice 3 (D-122). Glance phrases (`glance.<name>#text`, patterns with an English fallback) and producer, expedition and care-program titles go through `ContentText`. `DiscoveriesLocaleRenderTests` (EditMode) renders Discoveries with a pseudo locale; the PNG is in `docs/evidence/ki023-discoveries-locale/`. .NET 820, EditMode 206.
- #122 (`3aa5944`): Dependabot #117 (upload-artifact v7) and #118 (Test.Sdk 18.11.0) recreated and merged. #119 (STJ 8.0.6) was closed as superseded by #120.
- This PR: Region 1 first-run polish on Home (no "since you were last here" line on a new world; the choose-a-project prompt appears once). Fresh-state PNG review in `docs/evidence/region1-early-polish/`. EditMode 207/207.
- Next: stored summary lines and expedition/care descriptions through the overlay, a `PlayerStrings` locale and a language preference; the device lane (KI-002). Region 2 is still PROPOSED only.

## Session note 2026-10-17 (grok0242)
- #124 (`382cf1a`, D-125): titles are typed as text. Read-model titles are display text (blank when unknown, never an id); the dot-as-id heuristic in `PlayerCopy.Title` and the glance is gone, so a translated "St. Ives" shows. `TitleTypingTests` (.NET 2, fails without the fix), `TitleTypingShellTests` (EditMode 2). .NET 822/822.
- #125 (`ab13487`, D-126): `PlayerStrings.Locale` with the D-122 fallbacks (`ui.<key>#text`); `ShellController.SetLocale` drives shell + content. No language setting until a real translation exists. EditMode 214.
- This PR (D-127): Region 1 mid/late PNG review (`docs/evidence/region1-midlate-polish/`); Projects now lists active/waiting/ready before restored rows. Capture `WG_CAPTURE_STATE=mid|late`. EditMode 215/215 (Unity-editor-tested).
- No Dependabot PRs were open. Next: stored summary lines and expedition/care descriptions through the overlay; the first real translation plus a language preference; device lane (KI-002). Region 2 is still PROPOSED only.

