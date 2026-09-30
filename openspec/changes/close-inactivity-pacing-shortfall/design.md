## Context

See `proposal.md` (Why) for the committed evidence and the arithmetic.

Constraints that shape the approach:

- This is a **product-balance decision that was deliberately deferred**, not an
  oversight. `docs/ROADMAP.md` records the reasoning: re-costing content would
  have invalidated four already-committed pacing reports and the D-039
  provisional targets, and the previous campaign chose to record the finding
  honestly rather than re-tune silently. The deferral is what made the finding
  legible; this change is the discharge of that deferral.
- The shortfall is exact and structural: 16,420 earned against 19,400 authored
  cost. Any remedy must close a 2,980-Vitality gap for a weekday-only player
  while **not** compressing the `high` profile, which currently closes on day
  97. Those two constraints pull in opposite directions, which is why a
  mechanical re-cost is not sufficient.
- The two incomplete chains are the two most expensive (`forest=5,000`,
  `research=2,800`) and both are terminal chains — `research` contains the
  closure milestone project `proj.complete-valley-survey`. A shortfall therefore
  blocks *closure itself*, not just a side chain. That is what makes it a P-class
  product defect and not a "some content is hard" outcome.
- `docs/MASTER_PLAN.md` §1 defines the target player as "busy, inconsistent, or
  unwilling to spend large amounts of time actively playing", and §10 states
  the design objective is "re-entry, not punishment". An ordinary player who
  cannot finish is the failure mode R-007 names.
- `ContentValidator` already has a reachability gate
  (`M4ContentGraphTests.UnreachableProject_IsFlaggedAsHiddenDeadlock`) and
  `GameStateValidator` already has an "evergreen after closure" rule
  (`RegionState.IsCompleted` never resets, D-038). So the *structural* machinery
  for a second completion route does not exist and would have to be built.
- The `profile` harness already computes and prints per-chain completion days and
  already prints `region completed: NO (horizon reached)`. The report shape
  exists; what it lacks is a **fail** condition and the eligible/requested
  distinction.

## Goals / Non-Goals

**Goals**

- Make every balanced profile reach region closure.
- Define closure by an authored criterion rather than an implicit effort total.
- Regenerate and explain all pacing evidence in the same change.
- Correct the wrong conversion-rate figure and make the report incapable of
  reproducing that error.

**Non-Goals**

- Changing the steps-to-Vitality conversion rate or the per-record clamp.
- Adding a second region, a second activity category, or a difficulty selector.
- Reopening D-039's other decision: the `low` profile's long tail is a separate
  accepted outcome and is explicitly not re-opened here.
- Any change to mechanics. This is a content, criterion, tooling and
  documentation change.

## Decisions

### 1. Remedy shape: an authored closure criterion that is reachable by every
   balanced profile

Rather than re-costing the roster until 16,420 becomes 19,400, introduce an
authored **closure criterion** expressed in terms of progress rather than a
single resource total — for example, "the valley survey milestone is reached
**or** a defined set of the six chains is complete".

This is the structurally correct fix, and it is the one the deferred decision
was waiting for, because it addresses the root cause: closure is currently
defined implicitly as "spend 19,400 Vitality on this exact roster", which makes
the ending fragile against *any* future change to costs, conversion, or
content. Every other remedy (re-costing, adding a catch-up mechanic) treats the
symptom.

*Alternative considered:* re-cost the two expensive chains down by ~3,000 total.
Rejected as a sole remedy: it fixes today's roster and leaves the structural
brittleness. It is also immediately undone by the secondary-resource
requirements authored by the sibling change. It may be adopted *in addition* if
the criterion alone is judged insufficient, but not instead.

*Alternative considered:* a retroactive "catch-up" grant for long absences.
Rejected: it introduces a mechanic that rewards absence, which directly
contradicts D-004 and the anti-quitting-spiral thesis, and it would need its own
balance evidence.

### 2. The criterion must be validated, not just authored

`ContentValidator` gains: the closure criterion must be expressible in terms of
content that the validator has already proven reachable, and it must not be
satisfied only by content the region cannot reach. This is the same
reachability discipline the existing closure-milestone check applies, extended to
the alternative route.

`GameStateValidator` gains: post-closure state remains valid and evergreen
regardless of which route was taken, and the recorded route is consistent with
the canonical completion facts.

### 3. The pacing harness fails, rather than reporting, on a shortfall

`tools/simulation profile` already returns a non-zero exit when the state
validator reports violations. It SHALL also return non-zero when the horizon is
reached with the region not closed, and SHALL print the incomplete chains by
name. `scripts/verify-pacing-evidence.sh` then fails automatically, so the
shortfall cannot be committed as acceptable evidence again.

This is the same "make the gate mechanical" discipline D-049 applied to CI and
`verify-architecture.sh` applied to layering. The previous campaign's
shortfall survived for a full milestone precisely because the harness reported
it without failing on it.

*Alternative considered:* leave the harness reporting and rely on reviewer
discipline. Rejected: it already demonstrably did not work — the finding is
still open.

### 4. Report eligible and requested activity separately

`Cli.Profile` accumulates `totalSteps += steps`, the **pre-clamp requested**
value, and prints it as "activity ingested". Under the `pathological` profile
that is 3,184,633, while the eligible (post-clamp) total is 2,184,633. Dividing
the requested total by the credited vitality yields ~145.8, and dividing by
something else yields the 686 figure that appears in ROADMAP and R-013 — which
matches neither. The honest ratio, eligible ÷ credited, is exactly 100.0.

The report therefore gains three lines: requested steps, eligible steps, and the
achieved effective ratio. Bounded handling becomes *visible* rather than a
silent difference between two numbers a reader has to guess at.

This is the fix for the R-020 instance. A wrong number in the two documents a
release reviewer reads most is a quality defect in its own right, and the
reporting change is what prevents a recurrence.

### 5. Regenerate all seven reports, with each diff explained

Every committed report changes (at minimum the `pathological` one, from the
reporting change alone). The task list requires each diff to be reviewed and its
significance recorded in the decision log, with the constraint that the
`moderate` and `high` profiles' completion days do not regress.

D-039's provisional targets are explicitly **ratified or superseded**, not left
silently violated, as the spec requires.

## Risks / Trade-offs

- **[A second closure route could make the "dramatic transformation" ending feel
  cheap]**
  → This is the real product risk and it is why the criterion must be authored
  with care, not merely made reachable. The task list requires the criterion to
  still require the majority of the chains, so a player must restore
  substantially the valley; only the two most expensive chains become
  attainable at low effort. R-009 ("the world does not feel different enough")
  is the risk to watch, and it stays Open.

- **[The criterion adds a canonical state field, hence a schema question]**
  → Recording *which* route was taken is a small additive field and follows the
  D-036 pattern. It can be omitted entirely if the implementer judges it
  unnecessary, in which case closure is derived from canonical facts as today.
  The task list makes that an explicit choice rather than an omission.

- **[Regenerating seven reports is a large diff and easy to mis-review]**
  → The task list requires per-profile diffs, not a bulk overwrite, and requires
  the decision log to state which days moved and why. `verify-pacing-evidence.sh`
  then proves the committed set reproduces.

- **[Correcting the 686 figure may look like the previous campaign was wrong]**
  → It was, on that number, and correcting it is the point. `docs/DECISIONS.md`
  should record the correction plainly with the recomputation, consistent with
  this repository's practice of recording what was wrong rather than quietly
  overwriting it.

- **[Two of the three "grandfathered" seed projects must stay verbatim]**
  → `Region1Catalog` documents that the original M1/M3 seed definitions are
  preserved verbatim because durable saves and the M3 acceptance proof depend on
  them. The task list must state that the remedy may not re-cost or rename those
  definitions, and must find its lever in the M4-authored content instead.

## Migration Plan

No `ISaveMigration`. A closure-criterion field, if recorded at all, is additive.
Existing saves that already completed the region keep their `IsCompleted` flag
and remain valid; saves that have not completed simply gain the alternative
route's availability, which is strictly more permissive and cannot invalidate
anything.

Existing saves in the "stuck" state — a player with 16,000 banked Vitality and
two incomplete chains — become *completable* by the new criterion. That is the
repair, and it is why this change is worth its cost.

Rollback: a plain revert restores the prior roster and criterion; a save that
recorded the alternative route would then be flagged by the validator as an
unknown field only if it were persisted in a way the old build rejects, which
the additive pattern avoids.

## Open Questions

None. The specific closure criterion and the specific cost adjustments, if any,
are the implementer's product decision, constrained explicitly by the task list
(closure must remain reachable by the weekday-only profile; the majority of
chains must still be required; the seed definitions must not change; the
`moderate` and `high` completion days must not regress).
