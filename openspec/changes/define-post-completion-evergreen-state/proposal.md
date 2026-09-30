# Define a real post-completion evergreen state instead of a permanent dead end

## Why

Region 1's ending is a dead end that nags the player forever. Found during a
second-pass audit, and reproduced with the committed CLI.

After driving a profile to region completion (19/19 projects, region closed) and
then continuing to walk for a further 60 simulated days:

```text
$ tools/simulation longhaul --days 400 --shape flat --steps-per-day 20000
completed projects: 19/19  region completed: True
validator-clean: yes

$ (60 further days of walking through the real trust pipeline)
$ tools/simulation dump
balances: vitality=60650 materials=1495 knowledge=399
projects:
  proj.build-nesting-islets Completed  ...
  ... all 19 Completed
```

60,650 Vitality banked, with **every project and every expedition complete**. Now
trace what the Home surface says. `GameSession.GetHome()` computes:

```csharp
bool queueIdle = activeId == null && queuedRows.Count == 0;   // true: nothing left
long banked = queueIdle ? vitality : 0L;                       // 60650
...
else if (queueIdle && banked > 0)
{
    requiresAttention = true;
    reason = HomeAttentionReason.QueueEmptyWithBankedVitality;
}
```

That branch is **not conditioned on region completion**. So from the moment the
game is finished until the player stops walking, Home reports
`RequiresAttention = true` with the reason "no active/queued project while
unallocated Vitality sits banked" — an instruction to do something that is now
impossible. The attention signal is a permanent false alarm, and it is precisely
wrong for the player who engaged most.

Three separate problems compose here:

1. **A permanent unsatisfiable instruction.** R-001's failure mode is attention
   that cannot be acted on. R-015's mitigation is "meaningful project choices";
   after closure there are none, and the shell is still told to look.
2. **The economy dead-ends.** Vitality has no sink once all projects are
   complete. Combined with the fact that Materials and Knowledge have no sink at
   all (separate change, `bound-secondary-resource-economy`), the entire
   resource model becomes a scoreboard with nothing to spend it on.
3. **It contradicts the decision that created it.** D-038 records post-completion
   as "evergreen" — a stable, non-resetting state. What is actually delivered is
   a state that keeps changing (a growing bank) and keeps asking for attention.
   `docs/MASTER_PLAN.md` §10 requires return summaries to "surface the next
   **useful** action"; there is none.

No test covers this. `M4Region1AcceptanceTests` proves "post-completion
stability", but grepping the test suite for any assertion of the Home read model
after region completion returns nothing. The acceptance test proves the *state*
is stable; nothing proves the *surface* is sensible.

This is the most product-visible finding in the audit after the two P0s, and it
is the difference between a game that ends well and a game that nags.

## What Changes

- Region completion produces an explicit **post-completion state** in which the
  game stops asking for a project choice it cannot honour.
- The "banked Vitality with an empty queue" attention reason is **suppressed
  once the region is complete** and every authored work item is done, and is
  replaced by an explicit "your valley is complete" classification that the
  shell can present as a conclusion rather than as a to-do.
- Vitality has a **defined post-completion sink**, so a player who keeps walking
  has something real to do with it. The sink is authored content in the same
  shape as every other content node, validated with the rest of the graph.
- Secondary resources gain the same treatment, or an explicit documented
  statement that they are a lifetime score with no sink by design.
- Every region-surface signal is checked against the completed state: summary
  attention, project availability, expeditions, and the primary next action.
- Regression tests for the completed state across all of these surfaces.

**Non-goals:** no Region 2, no new activity category, no change to the closure
milestone itself, and no change to what "region complete" means. The milestone
and its detection are correct; what is missing is what happens next.

## Capabilities

### New Capabilities

- `post-completion`: the contract for the state a region is in after its closure
  milestone is reached — that completion is permanent and detected exactly once,
  that the game asks for nothing it cannot honour, that earned activity has a
  defined use, that every surface reports the completed state truthfully, and
  that the state remains stable and never regresses.

### Modified Capabilities

<!-- None. `openspec/specs/` is empty at HEAD; `post-completion` is introduced
     here. It is a distinct capability from `region-pacing` (which governs how
     content paces *toward* closure) and from `economy-resources` (which governs
     the resource model generally); this one governs the state *after* closure
     across all surfaces. -->

## Impact

**Evidence from the audit**

- `src/WalkGame.Application/GameSession.cs` `GetHome()` — the
  `queueIdle && banked > 0` branch, with no region-completion condition.
- `src/WalkGame.Application/ReadModels/HomeReadModel.cs` —
  `HomeAttentionReason` has four values; none of them represents "the region is
  complete".
- `src/WalkGame.Application/Summaries/ReturnSummaryComposer.cs` —
  `DerivePrimaryNextAction` returns "Queue the next restoration project."
  whenever any `ActionableDecision` item is present, with no completion check.
- Reproduced: `longhaul` to completion then 60 further days leaves
  `vitality=60650` with 19/19 projects complete (output quoted in Why).
- `src/WalkGame.Domain/Regions/RegionModel.cs` — `RegionState.IsCompleted` and
  `RegionCompletedAtUtc` exist and are validated, so the fact needed for this
  change is already durable.
- `src/WalkGame.Domain/Simulation/OfflineAdvancer.cs` —
  `EvaluateRegionCompletion` fires once and never resets; post-completion
  producers and discoveries keep operating, which is the "evergreen" part that
  already works.
- `tests/` — no assertion of `GetHome()` after region completion exists
  anywhere in the suite.
- `docs/DECISIONS.md` D-038 — records post-completion as "evergreen", which the
  delivered behaviour only partly satisfies.
- `docs/RISK_REGISTER.md` R-001, R-009, R-015 — all bear on the ending's quality.

**Affected code**

- `src/WalkGame.Application/GameSession.cs` — `GetHome()` attention derivation;
  possibly a new completion-aware summary composition path.
- `src/WalkGame.Application/ReadModels/HomeReadModel.cs` — a new
  `HomeAttentionReason` value.
- `src/WalkGame.Application/Summaries/ReturnSummaryComposer.cs` — next-action
  derivation.
- `src/WalkGame.Domain/Regions/RegionModel.cs`,
  `src/WalkGame.Application/Content/Region1Catalog.cs` — authored
  post-completion sink content.
- `src/WalkGame.Domain/Validation/ContentValidator.cs` — the sink is authored
  content and is validated with everything else.

**Affected tests**

- `M4Region1AcceptanceTests` — extended with post-completion surface
  assertions.
- `M3AmbientProgressionAcceptanceTests`, `M4ProgressionMechanicsTests` (5),
  `M5H1ShellAcceptanceTests` (11), `M4ContentGraphTests` (9),
  `ContentValidationTests` (8), `ReturnSummaryDurabilityTests` (5).

**Dependencies**

- Apply **after** `bound-secondary-resource-economy`, so the post-completion sink
  is authored in terms of the bounded resource model that change establishes
  (caps, and secondary requirements) rather than fighting it.
- Apply **after** `close-inactivity-pacing-shortfall`, which changes what
  "complete" requires and therefore what "after completion" contains.
- Independent of the save-recovery, activity-window, ledger and content-version
  changes.
