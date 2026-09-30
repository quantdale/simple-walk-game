# Align content validation with the simulation's real arithmetic bounds

## Why

`ContentValidator` accepts authored producer rates up to
`MaxMilliUnitsPerDay = 1_000_000_000_000`. The production simulation cannot
represent a rate anywhere near that.

`OfflineAdvancer.TickProducers` computes the earned amount as:

```csharp
long cappedTicks = Math.Min(elapsedTicks, MaxProducerInterval.Ticks);
long earnedMilliUnits = (cappedTicks / TimeSpan.TicksPerDay) * definition.MilliUnitsPerDay
                       + (cappedTicks % TimeSpan.TicksPerDay) * definition.MilliUnitsPerDay
                                              / TimeSpan.TicksPerDay;
```

`cappedTicks % TimeSpan.TicksPerDay` is strictly less than 86,400,000,000,000
(~8.64e11). For the product to stay representable,
`8.64e11 × MilliUnitsPerDay` must stay below `long.MaxValue` (9.22e18), which
requires `MilliUnitsPerDay < ~1.07e7`.

So content that **passes validation** — any rate between ~1.07e7 and 1e12 — can
overflow a `long` in the production tick. C# arithmetic here is unchecked, so
this does not throw: the product wraps. A wrapped `earnedMilliUnits` can be
negative, and `Math.Min(negative, roomInStore)` then makes
`runtime.StoredMilliUnits += negative`, producing a **negative producer store**.

That is not a cosmetic defect. `GameStateValidator` already flags a producer
store outside `[0, CapacityUnits × 1000]` as a violation, so the next boot
fails closed with "Save state failed validation" — the player's world becomes
unloadable, from an authored number that the validator explicitly approved.

The validation comment states the intent precisely and is simply wrong about
the number: "Producer rates multiply by up to MaxProducerInterval days; keep
products representable." The bound it chose does not achieve that.

Region 1's actual rates are 1,200–2,500 milli-units/day, so this is a latent
authoring hazard rather than a live defect. It is a real defect in the validator
contract, which is the release gate for authored content.

## What Changes

- The content validator's producer-rate bound is derived from the actual
  arithmetic it is meant to protect, not from a round number.
- The validator's bound is expressed so that it cannot silently drift away from
  the simulation again: the constant is expressed in terms of the simulation's
  own overflow threshold, with a check that fails if the two ever disagree.
- Producer capacity is checked against the same representability reasoning for
  the store-capacity product.
- A regression test proves the bound actually protects the simulation: a
  producer authored at exactly the maximum accepted rate produces a
  non-negative, representable, correctly-bounded store over the maximum
  supported interval.

**Non-goals:** changing any Region 1 rate, changing the production formula, or
adding checked arithmetic. Checked arithmetic is a separate, larger decision
(the formula is a hot path and the change would ripple through the committed
pacing evidence); this change makes the *authoring* gate correct instead.

## Capabilities

### New Capabilities

- `content-authoring`: the release-gate contract for authored region content —
  that identifiers are unique and resolvable, that the dependency graph is
  acyclic and fully reachable, that every authored number is inside the bounds
  the production simulation can actually represent, that costs and rates are
  positive and meaningful, and that the authored content meets the documented
  content minimums.

### Modified Capabilities

<!-- None. `openspec/specs/` is empty at HEAD; `content-authoring` is
     introduced here. `ContentValidator` is also edited by
     `bound-secondary-resource-economy` (caps and requirement satisfiability)
     and `gate-saves-against-authored-content-version` (tombstones); those
     changes are sequenced relative to this one so the rule sets do not
     conflict. -->

## Impact

**Evidence from the audit**

- `src/WalkGame.Domain/Validation/ContentValidator.cs` —
  `MaxMilliUnitsPerDay = 1_000_000_000_000L` and
  `MaxTotalVitalityCost = 1_000_000_000L`, with the comment
  "Producer rates multiply by up to MaxProducerInterval days; keep products
  representable."
- `src/WalkGame.Domain/Simulation/OfflineAdvancer.cs` — `MaxProducerInterval =
  TimeSpan.FromDays(3650L)`; the arithmetic quoted in the Why section; unchecked
  by default.
- `src/WalkGame.Domain/Regions/RegionModel.cs` —
  `ProducerDefinition.CapacityUnits` is validated only for
  `> long.MaxValue / 1000` (the store-capacity product), which is the right
  check, and is inconsistent with the rate check that is not.
- `src/WalkGame.Domain/Validation/GameStateValidator.cs` — the store-bounds
  violation that a wrapped rate would trip on the next boot.
- `src/WalkGame.Application/Content/Region1Catalog.cs` — actual rates
  1,200 / 1,500 / 2,500, so no live content is affected.
- `tests/WalkGame.Domain.Tests/ContentValidationTests.cs` (8 tests) — no case
  covers the rate bound at its edge.

**Affected code**

- `src/WalkGame.Domain/Validation/ContentValidator.cs` — the bound and its
  derivation, plus a self-check that the bound matches the simulation.
- `src/WalkGame.Domain/Simulation/OfflineAdvancer.cs` — no behaviour change;
  its constants become the single source of truth the validator derives from.
- `tests/WalkGame.Domain.Tests/ContentValidationTests.cs` — new boundary cases.

**Compatibility**

No behaviour change for existing content: every current Region 1 rate is three
orders of magnitude below the corrected bound, so `Region1Catalog` still
validates with zero violations and no committed evidence changes.

**Affected tests**

- `ContentValidationTests` (8), `M4ContentGraphTests` (9),
  `ProducerSimulationTests` (9), `GameStateValidationTests` (10),
  `M4Region1AcceptanceTests`.

**Dependencies**

- Apply **with or before** `bound-secondary-resource-economy` and
  `gate-saves-against-authored-content-version`; all three edit
  `ContentValidator`, and applying them in sequence avoids losing a rule set.
  If two are in flight together, rebase so the final file contains all three
  rule groups.
