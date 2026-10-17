# Final campaign report: 2026-10-16 agent campaign (M5 → M9 headless)

Written 2026-10-16 ~13:30 PT+8 by Grok Bot sessions `grok-*`. Repo **quantdale/simple-walk-game**; never walk-game.
Evidence labels: **automated verified** (headless .NET), **Unity-editor-tested** (Unity 6000.3.26f1 batchmode on the box), **editor-rendered** (Xvfb + llvmpipe). **Device runtime is UNVERIFIED** throughout (KI-002: no device, no `/dev/kvm`).

## 1. Repository state

| Item | Value |
|---|---|
| `main` before this report's PR | `bfdcac7b9a6b0c3a103b253be6fa262fb742578a` (merge of #35) |
| Last fully qualified commit | `0f9f3d79af7e269038e27a280f0259269937ce5a`. Package `docs/evidence/m9/release-evidence-0f9f3d7/`: 11/11 mechanical claims PASS, including clean clone and hosted CI. #35 after it added only evidence/docs plus two script-output fixes. |
| Hosted CI | Green on every PR #19–#35 and on main pushes (KI-001 closed: repo public) |
| Writer lease | FREE (every session released its lease) |
| Release tag | none: M9 cannot exit without device items H–Q |
| Content version | **4** (`Region1Catalog.AuthoredContentVersion`) |
| Toolchain | .NET SDK 9.0.319 (pinned 9.0.100 latestFeature); Unity 6000.3.26f1 |
| Leftover worktrees/branches | Per-session `agent/simple-walk-game/*` branches and `/workspace/simple-walk-game-wt-*` worktrees remain, all merged. They are safe to prune with `git worktree remove` and branch delete; no force or `clean -fdx` is needed. They were left in place because nothing required removing them. |

## 2. PRs merged this campaign (all CI green, merged by agent per repo policy)

| PR | Merge SHA | What |
|---|---|---|
| #19 | `e01cce9` | M5-U2: player-facing copy, Region screen, empty states |
| #20 | `5af061d` | M5-U3: onboarding polish, first PlayMode suite, KI-015 reversion findings, checklist G closed |
| #21 | `1919c9d` | Worktree helper honours `SWG_SESSION_ID` (KI-008) + guard scenario 12 |
| #22 | `84a32e3` | M7-A: Android Health Connect adapter (D-068), compiled into the APK |
| #23 | `a529ecf` | M7-B: Health Connect deletions via change log, exactly-once (D-069) |
| #24 | `72a4e83` | M6: procedural Region restoration visuals (created assets, `ASSET_LICENSES.md`) |
| #25 | `85e23cb` | M6: return-summary restoration moments (UX_DESIGN §4/§19) |
| #26 | `1e93735` | M6: editor-rendered valley capture + `tools/unity/capture-valley.sh` |
| #27 | `9f0fb1f` | Balance: visible-change cadence metric (D-070), KI-017 opened |
| #28 | `bce198e` | D-071 visible build stages (content v4), D-070 targets adopted, KI-017 resolved |
| #29 | `03d4267` | M6b: landmark silhouettes + recaptured PNG |
| #30 | `860a141` | M8 lifecycle headless (D-072) |
| #31 | `058aa91` | M8 UX/accessibility headless (D-073): OS font scale, screen-reader bridge, empty states, 7/30-day returns |
| #32 | `b1a719c` | M8 editor performance (D-074) + ScrollView retention fix, KI-018 |
| #33 | `a57b1bf` | M8 docs audit (README, WORLD_AND_CONTENT, UX_DESIGN §14, PERFORMANCE_BUDGETS) |
| #34 | `0f9f3d7` | M9 tooling (D-075): `validate-clean-clone.sh`, verifying evidence assembler, device checklist H–Q |
| #35 | `bfdcac7` | M9 release evidence package for `0f9f3d7` |

## 3. Features and decisions delivered

- **Shell (M5):** onboarding/guide, Home, Projects, Region, Settings and delete confirmation, Activity, Return summary, all with player-facing copy and empty states that explain the next step.
- **Health Connect (M7, D-068/D-069):** Android adapter behind `IActivityConnectionPort`; change-log deletions are honoured exactly once. Gaps are tracked in KI-016.
- **Region visuals (M6/M6b):** procedural vector valley within an 86-shape budget, per-landmark silhouettes, ruined vs restored forms, restoration-moment rings, a build-site for staged projects, alt text. PNG (editor-rendered): `docs/evidence/m6-region/valley-editor-rendered-m6b.png`.
- **Balance (D-070/D-071):** visible build stages on the workshop (2), wetland drainage (3) and canopy walkway (5). Seed costs are unchanged and no save migration was needed. Longest gap without a visible change: moderate 50→10 days, weekend-heavy 42→10, low 78→27. Closure days are unchanged (high 77, irregular 109, moderate 192, weekend-heavy 158, inactivity 376). Gate: `VisibleChangeCadenceTests`.
- **M8 lifecycle (D-072):** process-boundary suite. Repeated resume with overlapping re-fetch is exactly-once; torn temp files after process death are survived; 50 interrupted boots neither lose nor invent progress; permission revoked and re-granted outside the app never takes progress back and back-fills only new days.
- **M8 accessibility (D-073):** OS font scale (1–2×) with wrapping layout; `AccessibleOutline` plus `ScreenReaderBridge` publishing to `AssistiveSupport`; body-size empty states; 7/30-day return tests with no guilt language and no decay.
- **M8 performance (D-074):** mature 365-day restored save, every screen 0.14–0.41 ms and ≤ 25 elements, warm boot 5–7 ms, save 117 KB. Found and fixed linear UI retention (ScrollView reuse; ~470 → ~116 B/render; 3,000 renders 1,264 → 627 ms).
- **M9 (D-075):** `scripts/validate-clean-clone.sh` (fresh `--no-local` clone of one SHA → identity → all CI steps → counted .NET tests → optional Unity EditMode/PlayMode/APK from a cold Library). The assembler now verifies the clean-clone report (PASS and same SHA, proven to reject a mismatch) and reads hosted CI with `gh`. RELEASE_CHECKLIST rows A–G are refreshed, with the Final Device and Release Qualification Checklist H–Q.

## 4. Tests (latest, clean clone of `0f9f3d7`)

| Suite | Result |
|---|---|
| .NET Domain / Infrastructure / Application | 148 / 91 / 352 = **591 passed, 0 failed** |
| CI steps reproduced locally (`verify-ci-steps.sh`) | 16 ok, 0 failed, 2 hosted-only steps asserted |
| Unity EditMode | **56/56** (Unity-editor-tested) |
| Unity PlayMode | **3 passed**, 1 skipped by design (capture-only) |
| Android arm64 debug APK | builds (`WG_BUILD_RESULT Succeeded`) |
| Release gates | build, test, architecture (15/15), pacing (7 reproduced, 0 drifted), save-growth, content compatibility, guard suite, dependency health, coverage floor, clean clone, hosted CI: **all PASS** |

## 5. Open issues and blockers

| ID | Sev | State | Blocker / next action |
|---|---|---|---|
| KI-002 | High | Open (device half) | No device, no `/dev/kvm`. Blocks every item H–Q. **Needs Michael.** |
| KI-016 | Medium | Open | Health Connect: missed deletions when the change token expires; Android 9–13 report unsupported; no background sync; device behaviour unverified |
| KI-018 | Low | Open | ~116 B/render residual UI Toolkit retention (editor); measure on device |
| KI-007 | Low | Open | Hook `--check` in origin-less fixtures needs `GITHUB_REPOSITORY` |
| KI-008 | Low | Open | Long suites vs short-lived shells can strand a lease (helper fixed in #21; harness limitation remains) |
| KI-015 | Low | Open (mitigated) | Earlier work-tree "reversion" during Unity batchmode; controlled rerun showed none |
| iOS / HealthKit | — | Not started | Checklist N |

## 6. Exact next steps

1. **Unblock the device lane (Michael, one of):**
   - (a) `sudo usermod -aG kvm box`, then restart the box session. Agents can then run the AVD with KVM and tick **H** as emulator-tested.
   - (b) Build the APK with `tools/unity/build-android-debug.sh` (output `src/WalkGame.Unity/Build/Android/simple-walk-game-debug.apk`), install it on an Android 14+ phone with Health Connect (`adb install -r …`), and report against checklist H–Q: first screen, onboarding, Home, Settings → delete confirmation, TalkBack, max font size, permission grant/deny/revoke, step reads.
2. **With a device:** work through RELEASE_CHECKLIST H→Q in order. Record model, OS, SHA and date beside each tick. File new defects in KNOWN_ISSUES. Priority after H: M (Health Connect), K (TalkBack/font scale), I (lifecycle), L (launch/memory/battery, KI-018), P (upgrade-in-place v3→v4).
3. **Re-qualify:** for the candidate SHA, run `scripts/validate-clean-clone.sh --ref <sha> --with-unity --output <dir>` (about 5 min, detached), then `scripts/assemble-release-evidence.sh --commit <sha> --clean-clone <dir> --output docs/evidence/m9/release-evidence-<sha7>` with HEAD at that SHA. Commit the package via PR and tag only when H–Q are ticked.
4. **Headless work still possible without a device (optional):**
   - KI-016(a): resync on an expired change token by doing a full-window re-read with exactly-once dedupe.
   - KI-007: give fixture repos an identity source.
   - Prune merged agent worktrees and branches.
5. **iOS:** start the HealthKit adapter behind the same `IActivityConnectionPort` (checklist N). It needs a Mac and Xcode, which are not available on the box.

## 7. Addendum: 2026-10-17 style-neutral hardening (D-180 → D-195)

Written 2026-10-17 ~9:20 PM Asia/Manila. The art gate is closed throughout: no assets, packages, images or UI rebuild. Every PR is small, merged on green CI and self-reviewed, with no Codex review. Leases were released without `--force`, and `ProjectSettings.asset` was restored after every Unity run.

| PR | D | What | Merge SHA |
|---|---|---|---|
| #182 | D-180 | KI-028 presentation memory v2 (`shownCompletionIds`) | `fb9ef15` |
| #183 | D-181 | Batch revision order | `031b07d` |
| #184 | D-182 | Newer-version save boot message | `b910921` |
| #185 | D-183 | KI-029 tombstone (no save-format change) | `7e86fcf` |
| #186 | D-184 | Unity `.meta` CI guard | `27fa3b6` |
| #187 | D-185 | Resume throttle (amends D-107) | `48bf511` |
| #188 | D-186 | Reduced motion: in-game OR OS | `c1c5a14` |
| #189 | D-187 | CI wiring gate + gate self-tests | `30e4fa8` |
| #190 | D-188 | Health Connect lifecycle suite (shell) | `7e0e610` |
| #191 | D-189 | Device checklist rows R–U | `e1e01a5` |
| #192 | D-190 | Pacing under noise | `81c4e47` |
| #193 | D-191 | Architecture rule 6 covers CI scripts | `1129dca` |
| #194 | D-192 | KI-030 resolved; amends D-144 (last-known grant) | `3b5292d` |
| #195 | D-193 | HealthKit lifecycle parity suite; KI-031 logged | `04175b0` |
| #196 | D-194 | Delete-all withdraws the OS reminder (bug) | `4e15fb4` |
| #197 | D-195 | Runtime pseudo-locale sweep; Privacy lines locale (bug) | `e81fe46` |

**Tests at `e81fe46`:**
- .NET: **1835/1835** (automated verified).
- Unity EditMode: **355/355** (Unity-editor-tested).
- Device runtime: still UNVERIFIED (KI-002). The device-only checks are in RELEASE_CHECKLIST H–U.

**Open issues added:** KI-031 (Low): iOS read denial is invisible, so the screen keeps the "connected" copy. Decide a copy rule before the iOS composition ships.

**Resolved this addendum:** KI-028, KI-029 and KI-030.
