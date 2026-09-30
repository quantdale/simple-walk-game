## 1. Stop asking for impossible work

- [ ] 1.1 In `src/WalkGame.Application/ReadModels/HomeReadModel.cs`, add a
  `HomeAttentionReason.RegionComplete` member.
- [ ] 1.2 In `src/WalkGame.Application/GameSession.cs` `GetHome()`, suppress the
  `queueIdle && banked > 0` → `QueueEmptyWithBankedVitality` branch when
  `game.Region.IsCompleted` is true, and report `RegionComplete` instead.
- [ ] 1.3 `RequiresAttention` SHALL be **false** for `RegionComplete` — the
  state is concluded, not outstanding — while the reason value still lets a
  shell distinguish "finished" from "nothing has happened yet".
- [ ] 1.4 Expose the region's completion instant on `HomeReadModel` so the shell
  can render the ending ("Millbrook Valley was restored on <date>") rather than
  showing an empty screen.
- [ ] 1.5 In `src/WalkGame.Application/Summaries/ReturnSummaryComposer.cs`, make
  `DerivePrimaryNextAction` completion-aware so it can never propose queueing a
  restoration project once the region is complete.
- [ ] 1.6 **Do not** change the pre-completion behaviour.
  `M5H1ShellAcceptanceTests.Scenario06_QueueEmptyWhileAway_ExplicitReasonAndNextAction`
  must still pass unchanged.

## 2. Author a post-completion sink

- [ ] 2.1 Author a post-completion stewardship node in
  `src/WalkGame.Application/Content/Region1Catalog.cs`: a repeatable
  content item, enabled by the completed region, that consumes earned progress
  and yields a visible result.
- [ ] 2.2 The node MUST be optional depth: it must not gate anything, must not
  be required to reach or keep region completion, and must not reframe
  completion as "not yet finished". Assert all three in tests.
- [ ] 2.3 Decide and record the sink for each resource. Any resource that is
  deliberately a lifetime score with no sink MUST be declared in writing in the
  region-surface contract, and MUST NOT be presented as progress toward an
  unattainable goal.

## 3. Content validation for the post-completion node

- [ ] 3.1 In `src/WalkGame.Domain/Validation/ContentValidator.cs`, introduce a
  declared post-completion set.
- [ ] 3.2 Its members are **exempt from the entry-reachability rule** (they are
  reachable only after closure) but remain subject to every other rule:
  reference resolution, positive costs, arithmetic representability, and being
  enabled by the completed region.
- [ ] 3.3 **This is the most likely failure point of the change.** A post-completion
  node added without (3.1)–(3.2) is reported as an unreachable hidden deadlock,
  and the tempting "fix" is to delete the node, which silently removes the
  ending's sink. Do not do that. The validator change and the content change
  land together.
- [ ] 3.4 Add `ContentValidationTests` cases: a valid post-completion node yields
  zero violations; a post-completion node with a dangling reference is flagged; a
  post-completion node with an unrepresentable cost is flagged; a post-completion
  node missing from the declared set is treated as ordinary content.
- [ ] 3.5 Confirm `M4ContentGraphTests.AuthoredRegion1Catalog_Validates_WithZeroViolations`
  and `AuthoredRegion1Catalog_MeetsTheDocumentedContentMinimum` still pass.

## 4. Surfaces report the completed state truthfully

- [ ] 4.1 Confirm `GetRegion()` reports completion, the completion instant, and
  the still-operating producers, with no chain implied to be pending.
- [ ] 4.2 Confirm `GetProjects()` presents every project as complete in the
  completed state, with nothing ready, queued or actionable.
- [ ] 4.3 Confirm repeated summaries in the completed state converge rather than
  accumulating contradictory guidance.
- [ ] 4.4 Confirm the Discoveries and Expeditions surfaces are correct in the
  completed state.

## 5. Tests

- [ ] 5.1 New: after driving a profile to region completion through the real
  trust pipeline, `GetHome()` reports `RequiresAttention == false` with reason
  `RegionComplete` — **reproducing the audited defect as a failing test first**,
  then making it pass.
- [ ] 5.2 New: continuing to earn progress after completion does not set
  `RequiresAttention` and does not report an actionable deficit.
- [ ] 5.3 New: before completion, an empty queue with banked progress still
  reports `QueueEmptyWithBankedVitality` and `RequiresAttention == true`.
- [ ] 5.4 New: post-completion progress is consumed by the authored sink and the
  resulting state reflects the use rather than an ever-growing unused bank.
- [ ] 5.5 New: the completed region's bank does not grow without bound across a
  long post-completion horizon.
- [ ] 5.6 New: reaching region completion does not require the post-completion
  node, and the node gates nothing.
- [ ] 5.7 New: completion survives reload, restart and a long absence, and is
  not re-detected.
- [ ] 5.8 New: the summary in the completed state offers no impossible next
  action.
- [ ] 5.9 Extend `M4Region1AcceptanceTests` with post-completion **surface**
  assertions (it currently proves state stability only — that gap is the root
  cause of this defect going unnoticed).
- [ ] 5.10 Confirm unchanged and still green: `M3AmbientProgressionAcceptanceTests`,
  `M4ProgressionMechanicsTests` (5), `M5H1ShellAcceptanceTests` (11),
  `M4ContentGraphTests` (9), `ContentValidationTests` (8),
  `ReturnSummaryDurabilityTests` (5), `GameStateValidationTests` (10),
  `M4StateValidationTests` (7).

## 6. Evidence and documentation

- [ ] 6.1 `scripts/verify-pacing-evidence.sh` — all seven reports reproduce
  byte-for-byte. A post-completion node must not change any profile's closure
  day.
- [ ] 6.2 `scripts/verify-save-growth.sh` — growth series reproduces. If the
  committed series legitimately changes, regenerate it and explain the diff; do
  not overwrite silently.
- [ ] 6.3 `docs/DECISIONS.md` — record the post-completion sink, the
  `RegionComplete` classification, and the explicit lifetime-score declaration.
- [ ] 6.4 `docs/GAME_SYSTEMS.md` §9 — document what post-completion state
  contains and that completion is permanent.
- [ ] 6.5 `docs/MASTER_PLAN.md` §10 — record the ending's defined behaviour.
- [ ] 6.6 `docs/RISK_REGISTER.md` R-001, R-009 and R-015 — add the evidence
  that the completed state no longer demands impossible action, stating what
  remains UX judgement and therefore unproven headlessly.

## 7. Verification

- [ ] 7.1 `dotnet build SimpleWalkGame.sln` — zero errors, no new warnings.
- [ ] 7.2 `dotnet test` — all suites green.
- [ ] 7.3 `scripts/verify-pacing-evidence.sh` — seven reproduced, 0 drifted.
- [ ] 7.4 `scripts/verify-save-growth.sh` — growth series reproduces.
- [ ] 7.5 `scripts/verify-architecture.sh` — 11 passed, 0 failed.
- [ ] 7.6 `tests/guards/run-guard-tests.sh` — green.
- [ ] 7.7 `openspec validate define-post-completion-evergreen-state --strict`
  passes.
- [ ] 7.8 `git status` clean apart from intended files; writer lease released.
