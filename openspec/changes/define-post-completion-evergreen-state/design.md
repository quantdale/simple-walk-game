## Context

See `proposal.md` (Why) for the reproduction.

What already works, and must be preserved:

- `EvaluateRegionCompletion` in `OfflineAdvancer` sets `RegionState.IsCompleted`
  and `RegionCompletedAtUtc` exactly once and returns early thereafter, so
  completion is already permanent and never re-detected.
- `GameStateValidator` already enforces the completion invariants: a completed
  region must have a completion timestamp and a completed milestone, and a
  region with a timestamp must be marked complete.
- Producers, discoveries and arc stages keep operating after closure. That is
  the "evergreen" property D-038 asked for, and it works.
- `M4Region1AcceptanceTests` proves the completed state's canonical bytes are
  stable and replay-safe.

So the *state machine* is correct. The defect is entirely in the **presentation
layer's interpretation of that state**, plus the absence of any content that
consumes post-completion progress. That narrows the design considerably.

What constrains the design:

- The `HomeAttentionReason` enum is a public, persisted-adjacent contract: it is
  returned in `HomeReadModel`, consumed by the (future) shell, and asserted in
  `M5H1ShellAcceptanceTests`. Adding a member is additive and safe; changing the
  meaning of an existing member is not.
- `queueIdle && banked > 0` is currently the *correct* behaviour before
  completion, and `M5H1ShellAcceptanceTests.Scenario06_QueueEmptyWhileAway_ExplicitReasonAndNextAction`
  pins it. The change must suppress it only in the completed case and must
  prove the pre-completion case still works.
- `ReturnSummaryComposer.DerivePrimaryNextAction` returns a single hardcoded
  string whenever any `ActionableDecision` item is present. Post-completion,
  `ProjectBecameAvailable` events are not emitted (every project is complete), so
  in practice the string is not produced after closure — but the derivation has
  no completion awareness, and that is a latent version of the same bug.
- The content graph has no concept of a post-completion node. Introducing one is
  authored content and must pass the existing reachability and arithmetic gates
  without being treated as an "unreachable project" — which is a real trap, since
  `ContentValidator` flags any project unreachable from an entry project as a
  hidden deadlock.

## Goals / Non-Goals

**Goals**

- Stop the game asking for work it cannot offer.
- Give post-completion progress a real, visible use.
- Make the completed state a first-class classification rather than an absence
  of content.
- Prove the pre-completion attention behaviour is unchanged.

**Non-Goals**

- No change to what region completion *means* or how it is detected.
- No Region 2, no new activity category, no new project type that pre-completion
  work could use.
- No change to the closure milestone, its cost, or its pacing.
- No cosmetic re-theming of the ending.

## Decisions

### 1. Condition the attention reason on region completion, not on content counts

`GetHome()` should suppress the banked-Vitality attention reason when
`game.Region.IsCompleted` is true, and report a new
`HomeAttentionReason.RegionComplete` instead.

*Why not "suppress when no work remains"?* Because that requires computing
"is there any work left", which is a second source of truth about the roster
that could disagree with the completion flag. The completion flag is already
durable, validated, and set exactly once. Deriving the ending from it keeps one
source of truth.

*Alternative considered:* add a new `ProjectStatus` (e.g. `PostCompletion`).
Rejected: it would require a schema-visible change to every project row and a
migration, for a fact already carried by the region.

### 2. Give the completed state a positive classification, not just silence

Silencing the alarm would leave Home reporting `None` with no explanation, which
is its own failure: the player has just finished the game and the most important
thing the surface can say is nothing. A dedicated
`HomeAttentionReason.RegionComplete` lets the shell render the ending properly —
a conclusion, with the completion instant — which is also what R-009 ("the world
does not feel different enough") most needs at the climax.

`RequiresAttention` is false for this reason: the state is complete, not
outstanding. The reason value is what lets the shell distinguish "complete" from
"nothing has happened yet", which `None` currently conflates with
`NoProjectStartedYet`.

### 3. Author a post-completion sink as content, with a content-kind marker

The cleanest fit with the existing architecture is a new **content kind** — for
example, a repeatable post-completion "stewardship" project — that consumes
progress and repeats indefinitely. It is authored in `Region1Catalog`, validated
by `ContentValidator`, and reported through the existing project and region
read models, so the shell needs no new surface.

*Alternative considered:* let the bank simply accumulate and treat it as a
lifetime score. Rejected for Vitality: 60,650 and growing is not a score, it is
an unbounded number that makes the ledger look broken, and it keeps
`GameStateValidator`'s cross-checks meaningful while giving the player nothing.

*Alternative considered:* a spend menu or upgrade tree. Rejected as large scope
for a region-1 ending; the stewardship node is the minimal honest answer and
leaves the richer option open for Region 2.

### 4. Avoid the reachability trap explicitly

`ContentValidator` flags any project not reachable from an entry project as a
hidden deadlock. A post-completion node is reachable only *after* closure, so a
naive addition is reported as a defect. The validator therefore needs a
declared post-completion set whose members are exempt from the entry-reachability
rule but **still** subject to every other rule (existence of references,
positive costs, arithmetic bounds, and being enabled by the completed region).

This is called out in the task list because it is the most likely way this
change goes wrong: a content change that appears to break validation, gets
"fixed" by removing the node, and the ending silently loses its sink.

### 5. Keep the summary's next-action derivation completion-aware

`DerivePrimaryNextAction` should not propose queueing a project when the region
is complete. It already cannot in practice, but the derivation should be
explicitly guarded so a future event source cannot reintroduce it. This is a
small, defensive change with a test.

### 6. Declare the lifetime-score resources explicitly

For any resource that is deliberately a score with no sink, the region-surface
contract says so in writing, and the read model does not present it as progress
toward anything. This closes the gap between what the code does and what a
reader assumes, which is the same defect class as the unused error codes handled
in a sibling change.

## Risks / Trade-offs

- **[A stewardship node could turn the ending into a grind]**
  → The primary risk, and the reason the node must be optional-depth rather than
  required. It must not introduce a new mandatory decision, must not gate
  anything, and must not re-frame completion as "not really done". The task list
  states this constraint explicitly and the acceptance test asserts that
  completing the region does not depend on it.

- **[Suppressing the attention reason could mask a genuine dead end before
  completion]**
  → The suppression is conditioned strictly on the completion flag, and the
  pre-completion behaviour is pinned by an existing test plus a new explicit
  one. If the flag is ever set spuriously, that is a completion-detection bug
  with its own tests.

- **[Reaching the reachability trap and "fixing" it by deleting the node]**
  → Called out in the design and made an explicit task with a specific
  instruction. The validator change and the content change must land together.

- **[The completed state becoming a second content roadmap]**
  → Scope control: exactly one post-completion node kind, authored once, with no
  branching, no prerequisites, and no new mechanics. The richer option is
  explicitly left for a later decision.

- **[Ending quality is inherently subjective and untestable end to end]**
  → The *mechanical* properties are testable (no unsatisfiable instruction, a
  defined sink, truthful surfaces, permanence). Whether the ending is
  emotionally satisfying is a human/UX judgement and is honestly outside what
  this change can prove. That limitation is recorded rather than papered over.

## Migration Plan

No schema bump. `HomeAttentionReason` gains a member (additive, and it is a
transient read-model enum, not persisted). The post-completion node is authored
content, which the content-version mechanism from a sibling change is designed
to cover.

Existing completed saves: they already have `IsCompleted = true`, so they
immediately get the corrected attention classification with no migration. Their
banked progress is absorbed by the new node on the next completion-effect
evaluation. That is the repair for the profiles already in this state.

Rollback: a plain revert. A save that recorded post-completion consumption
decodes fine under the old build, which simply sees a larger bank.

## Open Questions

None. The specific post-completion node, its cost and its yield are authored
content decisions assigned explicitly in the task list, constrained by "must not
be required, must not gate anything, must not reframe completion".
