## 1. Stop asking for impossible work

- [x] 1.1 In `src/WalkGame.Application/ReadModels/HomeReadModel.cs`, add a
  `HomeAttentionReason.RegionComplete` member.
- [x] 1.2 In `src/WalkGame.Application/GameSession.cs` `GetHome()`, suppress the
  `queueIdle && banked > 0` → `QueueEmptyWithBankedVitality` branch when
  `game.Region.IsCompleted` is true, and report `RegionComplete` instead.
- [x] 1.3 `RequiresAttention` SHALL be **false** for `RegionComplete` — the
  state is concluded, not outstanding — while the reason value still lets a
  shell distinguish "finished" from "nothing has happened yet".
- [x] 1.4 Expose the region's completion instant on `HomeReadModel` so the shell
  can render the ending ("Millbrook Valley was restored on <date>") rather than
  showing an empty screen.
- [x] 1.5 In `src/WalkGame.Application/Summaries/ReturnSummaryComposer.cs`, make
  `DerivePrimaryNextAction` completion-aware so it can never propose queueing a
  restoration project once the region is complete.
- [x] 1.6 **Do not** change the pre-completion behaviour.
  `M5H1ShellAcceptanceTests.Scenario06_QueueEmptyWhileAway_ExplicitReasonAndNextAction`
  must still pass unchanged.

## 2. Author a post-completion sink

- [x] 2.1 Author a post-completion stewardship node in
  `src/WalkGame.Application/Content/Region1Catalog.cs`: a repeatable
  content item, enabled by the completed region, that consumes earned progress
  and yields a visible result.
- [x] 2.2 The node MUST be optional depth: it must not gate anything, must not
  be required to reach or keep region completion, and must not reframe
  completion as "not yet finished". Assert all three in tests.
- [x] 2.3 Decide and record the sink for each resource. Any resource that is
  deliberately a lifetime score with no sink MUST be declared in writing in the
  region-surface contract, and MUST NOT be presented as progress toward an
  unattainable goal.

## 3. Content validation for the post-completion node

- [x] 3.1 In `src/WalkGame.Domain/Validation/ContentValidator.cs`, introduce a
  declared post-completion set.
- [x] 3.2 Its members are **exempt from the entry-reachability rule** (they are
  reachable only after closure) but remain subject to every other rule:
  reference resolution, positive costs, arithmetic representability, and being
  enabled by the completed region.
- [x] 3.3 **This is the most likely failure point of the change.** A post-completion
  node added without (3.1)–(3.2) is reported as an unreachable hidden deadlock,
  and the tempting "fix" is to delete the node, which silently removes the
  ending's sink. Do not do that. The validator change and the content change
  land together.
- [x] 3.4 Add `ContentValidationTests` cases: a valid post-completion node yields
  zero violations; a post-completion node with a dangling reference is flagged; a
  post-completion node with an unrepresentable cost is flagged; a post-completion
  node missing from the declared set is treated as ordinary content.
- [x] 3.5 Confirm `M4ContentGraphTests.AuthoredRegion1Catalog_Validates_WithZeroViolations`
  and `AuthoredRegion1Catalog_MeetsTheDocumentedContentMinimum` still pass.

## 4. Surfaces report the completed state truthfully

- [x] 4.1 Confirm `GetRegion()` reports completion, the completion instant, and
  the still-operating producers, with no chain implied to be pending.
- [x] 4.2 Confirm `GetProjects()` presents every project as complete in the
  completed state, with nothing ready, queued or actionable.
- [x] 4.3 Confirm repeated summaries in the completed state converge rather than
  accumulating contradictory guidance.
- [x] 4.4 Confirm the Discoveries and Expeditions surfaces are correct in the
  completed state.

## 5. Tests

- [x] 5.1 New: after driving a profile to region completion through the real
  trust pipeline, `GetHome()` reports `RequiresAttention == false` with reason
  `RegionComplete` — **reproducing the audited defect as a failing test first**,
  then making it pass.
- [x] 5.2 New: continuing to earn progress after completion does not set
  `RequiresAttention` and does not report an actionable deficit.
- [x] 5.3 New: before completion, an empty queue with banked progress still
  reports `QueueEmptyWithBankedVitality` and `RequiresAttention == true`.
- [x] 5.4 New: post-completion progress is consumed by the authored sink and the
  resulting state reflects the use rather than an ever-growing unused bank.
- [x] 5.5 New: the completed region's bank does not grow without bound across a
  long post-completion horizon.
- [x] 5.6 New: reaching region completion does not require the post-completion
  node, and the node gates nothing.
- [x] 5.7 New: completion survives reload, restart and a long absence, and is
  not re-detected.
- [x] 5.8 New: the summary in the completed state offers no impossible next
  action.
- [x] 5.9 Extend `M4Region1AcceptanceTests` with post-completion **surface**
  assertions (it currently proves state stability only — that gap is the root
  cause of this defect going unnoticed).
- [x] 5.10 Confirm unchanged and still green: `M3AmbientProgressionAcceptanceTests`,
  `M4ProgressionMechanicsTests` (5), `M5H1ShellAcceptanceTests` (11),
  `M4ContentGraphTests` (9), `ContentValidationTests` (8),
  `ReturnSummaryDurabilityTests` (5), `GameStateValidationTests` (10),
  `M4StateValidationTests` (7).

## 6. Evidence and documentation

- [x] 6.1 `scripts/verify-pacing-evidence.sh` — all seven reports reproduce
  byte-for-byte. A post-completion node must not change any profile's closure
  day.
- [x] 6.2 `scripts/verify-save-growth.sh` — growth series reproduces. If the
  committed series legitimately changes, regenerate it and explain the diff; do
  not overwrite silently.
- [x] 6.3 `docs/DECISIONS.md` — record the post-completion sink, the
  `RegionComplete` classification, and the explicit lifetime-score declaration.
- [x] 6.4 `docs/GAME_SYSTEMS.md` §9 — document what post-completion state
  contains and that completion is permanent.
- [x] 6.5 `docs/MASTER_PLAN.md` §10 — record the ending's defined behaviour.
- [x] 6.6 `docs/RISK_REGISTER.md` R-001, R-009 and R-015 — add the evidence
  that the completed state no longer demands impossible action, stating what
  remains UX judgement and therefore unproven headlessly.

## 7. Verification

- [x] 7.1 `dotnet build SimpleWalkGame.sln` — zero errors, no new warnings.
- [x] 7.2 `dotnet test` — all suites green.
- [x] 7.3 `scripts/verify-pacing-evidence.sh` — seven reproduced, 0 drifted.
- [x] 7.4 `scripts/verify-save-growth.sh` — growth series reproduces.
- [x] 7.5 `scripts/verify-architecture.sh` — 11 passed, 0 failed.
- [x] 7.6 `tests/guards/run-guard-tests.sh` — green.
- [x] 7.7 `openspec validate define-post-completion-evergreen-state --strict`
  passes.
- [x] 7.8 `git status` clean apart from intended files; writer lease released.

---

## Implementation record (added during apply, 2026-10-08)

1. **The sink is a NEW content kind, not a project.** `PostCompletionProgramDefinition`
   (Domain) holds id/title/description/cycle cost/enabling milestone, and
   `RegionDefinition` gained a separate `PostCompletionPrograms` collection. Deliberately
   NOT a `ProjectDefinition`: adding it to the project roster would have made a completed
   region read "19 of 20 complete" in `HomeReadModel.TotalProjects` and
   `RegionReadModel.TotalProjects`, and `GetProjects()` would have shown a row the player
   could never act on — the exact "surface implies unfinished work" failure this change
   exists to remove. Reported through the region surface (`PostCompletionCareRow`) and Home
   instead, so the shell needs no new screen.

2. **The reachability exemption is structural, and stated where it matters.** Because the
   program is a separate collection, `ContentValidator`'s entry-reachability pass never sees
   it, so the trap in task 3.3 cannot fire at all. What replaces it is an explicit rule
   group (`ValidatePostCompletionPrograms`) that (a) rejects a program id that is also
   ordinary content — task 3.4's "missing from the declared set is treated as ordinary
   content" case, since an id in the project collection *is* ordinary content — and
   (b) requires `EnabledByMilestoneProjectId` to be the region's own closure milestone,
   which is the rule that enforces "enabled by the completed region". The comment in the
   validator names the exemption explicitly so a future author does not "fix" the node away.

3. **`AllocateVitality` applies the sink itself, and one test fixture had to change.**
   `ApplyPostCompletionPrograms` runs after project allocation inside the single public
   entry point, so every caller (ingest, enqueue, auto-advance toggle, manual activation,
   boot advance) gets it without a new call to remember. `SessionQueueControlTests.SeedQueue`
   completed every project except the three under test — including the closure milestone —
   so its region was already completing as a side effect; the fixture now leaves the closure
   milestone alone, which is the region state those queue tests are actually about.

4. **Two decisions the tasks left open, decided and recorded.**
   - Cycle cost 1,000 Vitality: ~5% of the region's whole authored cost, so a moderate
     walker banks a cycle about every ten days. It is NOT tuned to feel productive quickly —
     the ending is a conclusion, not a second game.
   - Materials and Knowledge are explicitly declared lifetime scores after completion
     (task 2.3's alternative). Both are already capped with real pre-completion consumers
     (D-054); a post-completion consumer for them would have been balance change without a
     balance question, and it would have changed the pacing reports' capped-store figures.
     Stated in `GAME_SYSTEMS.md` §9 and `MASTER_PLAN.md` §10.

5. **Attention semantics, stated exactly.** With a *current* durable summary the Home surface
   still reports `PendingReturnSummary` with `RequiresAttention = true` — that is news
   ("the valley is restored"), not an instruction, and `PrimaryNextAction` is null. The
   audited alarm was the `QueueEmptyWithBankedVitality` branch instructing an impossible
   action; that branch is now conditioned on `!IsCompleted`. The tests therefore assert both
   states explicitly rather than asserting one and hoping for the other.

6. **Measured, not asserted:** `longhaul --days 400` ends with `vitality=0` (was 60,650) and
   `evergreen-care: cycles=60 invested=650` — the audited bank, exactly accounted for, since
   80,000 credited + 50 expedition grant − 19,400 region cost = 60,650. All seven committed
   pacing reports still reproduce byte-for-byte; the save grows by two additive `long`
   fields (~65 bytes), which the save-growth gate reports as unchanged behaviour.
