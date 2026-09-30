## Context

See `proposal.md` (Why) for the arithmetic and the code references.

What constrains the design:

- The formula in `OfflineAdvancer.TickProducers` is a **hot path**. It runs for
  every producer on every tick, and the committed pacing evidence in
  `docs/evidence/m4/` is produced by it. Rewriting it as checked arithmetic, or
  restructuring it to avoid the large intermediate product, would change
  behaviour in a way that must be re-validated against all seven reports.
- The correct fix is therefore to make the **authoring gate** sound rather than
  to change the simulation. Region 1's rates (1,200 / 1,500 / 2,500) are three
  orders of magnitude below even the current bound, so tightening the bound is
  invisible to shipped content.
- `ContentValidator` lives in Domain and takes only a `RegionDefinition`. It
  must not reach into `OfflineAdvancer` as a service; but the two must not be
  able to drift apart either. The resolution is a shared, purely-arithmetic
  constant that both reference, with a self-check that fails the build if the
  relationship is ever violated.
- The codebase already has this exact pattern for a different bound:
  `ProducerDefinition.MilliUnitsPerUnit` is a named constant used by both the
  simulation and the validator, and `ContentValidator` checks capacity against
  it. The change extends the established pattern rather than inventing one.
- `ContentValidator` runs in the `GameSession` constructor via a
  `ConditionalWeakTable`, so a corrected bound is enforced at every composition
  root automatically, with no gate wiring.

## Goals / Non-Goals

**Goals**

- Make the accepted rate range provably non-overflowing in the production tick.
- Prevent the validator bound and the simulation from drifting apart.
- Give every enforced bound a negative-edge test.
- Change nothing observable for shipped content.

**Non-Goals**

- Rewriting the production formula, or switching to checked arithmetic.
- Adding a different unit representation (fixed-point structs, etc.).
- Bounding rates more tightly than representability requires, on aesthetic or
  balance grounds. That is a content decision, not a validator contract.
- Reviewing whether the other magic constants in the simulation are well chosen.

## Decisions

### 1. Derive the bound from representability, express it once

Compute the maximum accepted rate from the two facts that determine it:
the sub-day remainder bound (`TimeSpan.TicksPerDay - 1`) and `long.MaxValue`.
The natural expression is
`long.MaxValue / (TimeSpan.TicksPerDay - 1)`, which is approximately 1.067e7.

This is ~1e5 times smaller than the current 1e12 and is, by construction, the
largest rate for which the remainder product is representable.

The constant lives beside the simulation (or in a small shared arithmetic
helper both reference) so the relationship is visible in one place. The
validator comment is rewritten to state the derivation rather than assert an
intention.

*Alternative considered:* keep a round number and add a comment. Rejected: that
is exactly the current state, and the drift is what caused the defect.

*Alternative considered:* use `checked` arithmetic in the tick so an overflow
throws instead of wrapping. Rejected for this change: a throw during a tick is
a crash on the player's critical path, which is a worse failure than the
authoring gate catching it. This remains a reasonable *later* hardening and is
recorded in the master plan's deferred list.

### 2. Add a self-check that the two agree

`ContentValidator` gains an internal assertion that recomputes the bound from the
simulation's interval and integer range and compares it to the published bound.
A mismatch is itself a content-validation failure naming both values, which
turns a future silent drift into a build-visible failure.

This is a small, cheap, self-enforcing guard and it is the reason this defect
class will not recur.

### 3. Check the whole product, not only the remainder term

The bound must hold for the **full-day** term as well:
`(cappedTicks / TicksPerDay) * MilliUnitsPerDay` where the quotient is capped at
`MaxProducerInterval.TotalDays` (3650). At the derived bound this product is
~3.9e10, comfortably representable — so the remainder term is indeed the
binding constraint, and the self-check should assert that, not assume it. If a
future interval change made the full-day term bind instead, the self-check
catches it.

### 4. Every bound gets a negative-edge test

The spec requires it, and the gap is real: `ContentValidationTests` currently
proves duplicate IDs, missing references, cycles and no-entry-project, but no
numeric bound at all. Add a table-driven case covering each enforced bound at
its accepted maximum and its first rejected value, asserting the violation
message names the offending content.

### 5. Region 1 content is explicitly re-verified, not assumed

Because a corrected bound is exactly the kind of change that could plausibly
break shipped content, `M4ContentGraphTests.AuthoredRegion1Catalog_Validates_WithZeroViolations`
is a required verification, and all seven committed pacing reports must still
reproduce byte-for-byte. If any Region 1 value is found near the new bound, that
is a signal to investigate the arithmetic, not to raise the bound.

## Risks / Trade-offs

- **[The corrected bound may reject a legitimate future content value]**
  → A rate above ~1.07e7 milli-units/day is 10.7 million units per day, which is
  not a plausible balance value at this game's scale. The bound is far above any
  rate the design could want, so the practical risk is nil.

- **[A self-check that can fail the build is a new failure mode]**
  → It only fires if someone changes the simulation's interval or the bound
  inconsistently, which is precisely when a human should look. The failure
  message names both values and the relationship, so the fix is obvious.

- **[Three changes now edit `ContentValidator`]**
  → This change touches only the numeric bounds; the other two add cap and
  tombstone rules. They are additive rule groups in the same method, so a
  mis-ordered rebase is easy to detect: the content validation suite's
  zero-violations case for the shipped catalog catches an accidental deletion of
  any rule group.

- **[No test exercises the current dangerous range today]**
  → After this change the range no longer exists (it is rejected at authoring
  time), so the correct regression test is the boundary case, not a simulation
  of an overflowing producer. The design deliberately tests the guarantee that
  remains, not the bug.

## Migration Plan

No migration and no data change. No authored value changes, so no committed
evidence regenerates.

Rollback: a plain revert restores the previous (weaker) bound with no
consequence for any current content.

## Open Questions

None. The bound is fully determined by the arithmetic; the only judgement call
is whether to tighten it further for balance reasons, which is explicitly out of
scope and belongs in the pacing change if at all.
