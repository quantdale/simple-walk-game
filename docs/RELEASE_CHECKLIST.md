# Release checklist

A committed, checkable form of `docs/TESTING_AND_RELEASE.md` §18. A release candidate
is not "probably ready"; it is a commit for which every row below is either satisfied with
evidence or explicitly UNVERIFIED with a reason. Rows are re-verified, not remembered.

`scripts/assemble-release-evidence.sh` checks the **[gate]** rows mechanically and copies
the remaining documents into the evidence package. Run it; do not assemble a package by hand.

## Mechanical gates — all must pass for the tagged commit

| # | Claim | Evidence |
|---|---|---|
| 1 | Identity guard passes | `scripts/assert-repo-identity.sh` |
| 2 | Clean build, zero warnings | `dotnet build SimpleWalkGame.sln` |
| 3 | Full test suite green | `dotnet test SimpleWalkGame.sln` |
| 4 | Architecture fitness (layering, engine-free, script hygiene) | `scripts/verify-architecture.sh` |
| 5 | Pacing evidence reproduces byte-for-byte (seven profiles) | `scripts/verify-pacing-evidence.sh` |
| 6 | Save-growth / scenario determinism | `scripts/verify-save-growth.sh` |
| 7 | Content-drift gate + self-test | `scripts/verify-content-compatibility.sh [--self-test]` |
| 8 | Guard proof suite (identity / lease / pre-push) | `tests/guards/run-guard-tests.sh` |
| 9 | No known dependency advisory; runtime surface is one first-party package | `scripts/verify-dependency-health.sh` |
| 10 | Coverage floor held (loss detector, not a target) | `scripts/verify-coverage-floor.sh` |
| 11 | Release evidence package assembled | `scripts/assemble-release-evidence.sh` |
| 12 | The CI body for this commit succeeded | hosted run for the tagged SHA; the assembler reads it with `gh run list --commit` |
| 13 | A fresh clone of the exact commit builds and passes every CI step (D-075) | `scripts/validate-clean-clone.sh --ref <sha> --with-unity`, passed to the assembler via `--clean-clone` |
| 14 | Unity lanes from a cold Library: EditMode, PlayMode, arm64 debug APK (Unity-editor-tested) | same run, `--with-unity` (`tools/unity/*.sh`) |

**Latest verified package:** `docs/evidence/m9/release-evidence-0f9f3d7/`. Gates 1–14 PASS for `0f9f3d7` (2026-10-16), with hosted `ci` green for that SHA. No release tag exists: device items H–U are open.

## Claims that cannot be established locally — must be stated, not omitted

| # | Claim | Status (2026-10-16) |
|---|---|---|
| A | Hosted GitHub Actions is green | **VERIFIED per commit** (KI-001 closed 2026-10-16: the repo is public and hosted `ci` runs are green). Checked by gate 12, not remembered. |
| B | Runtime/device behaviour: screens, notifications, permission dialogs, lifecycle | **Unity-editor-tested** (EditMode/PlayMode in batchmode; headless lifecycle D-072). **Device UNVERIFIED** (KI-002: no device, no KVM). See H–J. |
| C | Accessibility verification | **Unity-editor-tested** (D-073: font-scale layout, screen-reader outline and `AssistiveSupport` hierarchy). **TalkBack and hardware font scaling UNVERIFIED** (KI-002). See K. |
| D | On-device performance and battery budgets | **Editor-measured** (D-074; residual KI-018). **Device UNVERIFIED** (KI-002). See L. |
| E | Health Connect / HealthKit adapter behaviour | Android adapter and change-tracking deletions implemented and compiled into the APK (D-068, D-069); headless contract tests pass. **Device UNVERIFIED** (KI-002). **HealthKit not started.** See M–N. |
| F | At-rest confidentiality of the health-adjacent save | **Decided (D-067):** OS device encryption is a system requirement. Verify on device that the save and backup paths are app-private (see O). |
| G | PRODUCT_SPEC privacy requirements section | **CLOSED** (KI-005 closed on main via `589150b`: PRODUCT_SPEC §7a, eight requirements traced in PRIVACY_REVIEW §7). |

## Final Device and Release Qualification Checklist

Each item must be ticked on real hardware before release qualification, with the device model, OS version, build SHA and date recorded beside the tick. Option (a) of H alone may use a KVM-accelerated emulator; label it emulator-tested, never device-tested. Nothing here may be ticked from editor or headless evidence.

**How to unblock:** either the box user joins the `kvm` group (emulator for H only), or Michael installs the arm64 debug APK (`tools/unity/build-android-debug.sh`) on an Android 14+ phone with Health Connect and reports back against this list.

| # | Item | Headless/editor evidence today | Device status |
|---|---|---|---|
| H | APK installs, launches, reaches first screen; onboarding → Home → Settings → delete confirmation reachable; logcat free of crashes | ShellController/BootRouter EditMode tests; APK builds (gate 14) | **UNVERIFIED** (KI-002, "Needs Michael") |
| I | Lifecycle: background/resume 20×, OS process kill (`adb shell am kill`), device reboot, then resume with no lost or double credit | D-072 process-boundary suite | **UNVERIFIED** |
| J | Valley picture renders on a GPU (not llvmpipe), with no visual corruption at 1× and 2× font scale | `valley-editor-rendered-m6b.png` (editor-rendered) | **UNVERIFIED** |
| K | TalkBack reads Home → Projects → Settings in outline order, and double-tap invokes actions; Android font size at maximum keeps every screen usable | D-073 outline and hierarchy tests | **UNVERIFIED** |
| L | Cold launch ≤ 3.0 s, warm resume ≤ 1.0 s, no monotonic memory growth over 100 navigations (KI-018), and battery drain from background reads within PERFORMANCE_BUDGETS §13–14 | D-074 editor measurements | **UNVERIFIED** |
| M | Health Connect: permission grant, denial and revocation in system settings; steps read; deletions honoured exactly once (D-069). Also mid-session (D-188): revoke access in the Health Connect app while the game is in the background, return. The Activity screen says access is off and points to Settings, nothing crashes, and progress is kept. Re-grant: walks resume and none is counted twice. Repeat with Health Connect uninstalled, then with it needing an update | D-068/D-069 contract tests; `ActivityConnectionLifecycleTests` (D-188, real adapter over a fake bridge) | **UNVERIFIED** |
| N | iOS build and HealthKit adapter | C# adapter with fake-bridge tests (D-112); Swift bridge compiled on hosted macOS (`ios-bridge-compile`); unsigned Xcode export builds (D-115). Never run, and not composed (KI-022) | **NOT STARTED** |
| O | Save and backup live in app-private storage; uninstall removes them; OS encryption is on (D-067) | path code review | **UNVERIFIED** |
| P | Upgrade-in-place: install the previous release's APK, play, install this APK over it, and progress survives (content v3 → v4 CompatibleOlder) | content-compatibility gate; mature-save migration tests | **UNVERIFIED** |
| Q | Mature-save smoothness on a mid-range device (365-day save from `tools/simulation`) | D-074 mature-save budgets | **UNVERIFIED** |
| R | Side-file atomic replace under IL2CPP (D-179): on an Android and an iOS device, toggle reduced motion and sound about 20 times, force-stopping the app (`adb shell am force-stop`) between toggles. Every relaunch shows the last saved choice, and no `*.tmp` side file is left in app storage (`run-as`) | `SideFileReplaceAtomicityTests` (.NET); EditMode under Mono only. `File.Replace` has never run under IL2CPP | **UNVERIFIED** |
| S | Android "Remove animations" (D-186): turn on Settings → Accessibility → Remove animations (sets `animator_duration_scale` to 0) with the in-game setting off. Settings shows "Reduced motion is on because your phone is set to show less motion", and valley changes play in their reduced form. Turn it off while the game is in the background, return: full motion again, and the in-game choice is unchanged. The JNI read must not crash or log an exception | `OsReducedMotionTests`, `WorldPresentationSeamTests` (editor, null adapter); the JNI path is compiled out in the editor | **UNVERIFIED** |
| T | iOS Reduce Motion bridge (D-186): needs N. With Settings → Accessibility → Motion → Reduce Motion on, the same Settings line and reduced valley changes appear. Toggled while backgrounded, it applies on return. `wg_reduce_motion_enabled` links in the Xcode build | `ios-bridge-compile` checks the symbol (hosted macOS, compile-only); editor tests as S | **NOT STARTED** |
| U | Resume throttle battery (D-185): with Health Connect connected, background/foreground 20 times within 30 s. Count the step reads for the burst using Health Connect's per-app access history where the installed version shows it, or a debug build's read counter otherwise: expect one read, and a resume 30 s or more later reads again. Over a 30-minute session with frequent app switching, battery and thermal stay within PERFORMANCE_BUDGETS §13–14. Freshness cost is at most 30 s | `LiveSyncThrottleTests`, `BackgroundSyncBudgetTests` (editor) | **UNVERIFIED** |

## Repository hygiene

- [ ] `git status` clean apart from intended files; the writer lease is released.
- [ ] No committed generated artifacts; `bin/`/`obj/` ignored.
- [ ] `docs/KNOWN_ISSUES.md` dispositions every item this release closes.
- [ ] Every new decision recorded in `docs/DECISIONS.md` with its rationale.
- [ ] No documentation claim contradicted by committed evidence.
