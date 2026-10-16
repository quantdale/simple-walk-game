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
