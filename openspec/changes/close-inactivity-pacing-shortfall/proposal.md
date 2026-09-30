# Close the intermittent-engagement pacing shortfall with a design remedy

## Why

One of the seven activity profiles the product is balanced against **cannot
finish the game**, and the committed evidence says so plainly.
`docs/evidence/m4/pacing-inactivity.txt`:

```text
--- profile report: inactivity ---
pattern steps/day: weekdays 4000/9000 alternating, weekends silent,
                   days 100-144 entirely silent
region completed: NO (horizon reached)
days simulated: 400  horizon: 400
activity ingested: 1,642,000 steps / 16,420 vitality (exactly-once pipeline)
silent days (no record returned): 147
per-chain completion day: trail=30, water=80, settlement=206, wetland=296,
                          forest=incomplete, research=incomplete
final arcs: ecology 4/4, settlement 4/4
```

The arithmetic is exact. The six chains cost `1400 + 2350 + 3700 + 4150 + 5000
+ 2800 = 19,400` Vitality. This profile earns `16,420`. It is short by
**2,980** — which is precisely why the two most expensive chains, `forest`
(5,000) and `research` (2,800), never complete. The shortfall is not noise; it
is structural.

This is RISK_REGISTER **R-007** ("Inactivity mechanics create a quitting
spiral", High / Open) and **R-013** ("Content balance assumes a single ideal
activity pattern", High / Open) both realised. The profile models a player who
walks on weekdays and takes a six-week break — a completely ordinary person,
and precisely the "busy, inconsistent player" the product thesis in
`docs/MASTER_PLAN.md` §1 is written for.

The previous campaign made the right call by **recording** it rather than
silently re-tuning (re-costing content would have invalidated four already
committed pacing reports and the D-039 provisional targets). Recording was
correct. What is now missing is a *remedy*, and this change is that remedy.

There is a second, smaller documentation defect in the same area. ROADMAP §M4
and RISK_REGISTER R-013 both state the `pathological` profile shows
"≈686 steps/Vitality against the nominal 100". That number is wrong, and the
repository's own committed evidence contradicts it. Recomputing from
`pacing-pathological.txt`: the raw requested total is 3,184,633 steps, but the
per-record clamp caps each at 250,000, giving an **eligible** total of
2,184,633 steps for 21,844 Vitality — exactly **100.0** steps/Vitality. The
clamp discards surplus volume; it does not reduce the credit rate. This is
R-020 ("Documentation becomes aspirational fiction") in a concrete instance, in
the two documents a release reviewer is most likely to read.

## What Changes

- The remediation is **specified and decided**, not silently re-tuned. A design
  remedy is chosen, its rationale is recorded, and the consequence for every
  profile is measured rather than assumed.
- The remedy targets the actual mechanism: a player who walks less cannot
  afford 19,400 Vitality of restoration work at the current rate, and the two
  most expensive chains are the ones that fall off the end. The remedy makes
  *low-frequency* effort able to reach closure **without** making
  high-frequency effort trivially fast, so the `high` profile's day-97
  completion does not regress.
- A closure criterion is defined so that "finished" is not synonymous with
  "spent 19,400 Vitality on exactly this authored roster", which is what makes
  the current numbers brittle in the first place.
- The pacing reports are regenerated for all seven profiles in the same change,
  with each diff reviewed and explained, and the committed evidence updated.
- The incorrect "≈686 steps/Vitality" figure is corrected in ROADMAP and
  R-013, and the `profile` report is made to print the **eligible** (post-clamp)
  step total alongside the raw requested total, so the number cannot be
  misread again.
- KI-003 and KI-004 are dispositioned with the actual outcome.

**Non-goals:** changing the steps-to-Vitality conversion rate, weakening the
per-record quantity clamp, or adding a second region. This is a balance and
comms change with full evidence, not a mechanics change.

## Capabilities

### New Capabilities

- `region-pacing`: the contract for how authored restoration content paces
  against real activity — the activity profiles the region is balanced against,
  the closure criterion that defines completion, the documented targets each
  profile is measured against, and the rule that every balance change is
  re-measured and its evidence regenerated rather than assumed.

### Modified Capabilities

<!-- None. `openspec/specs/` is empty at HEAD; `region-pacing` is introduced
     here. It depends on the activity-trust-pipeline and economy-resources
     capabilities but modifies neither. -->

## Impact

**Evidence from the audit**

- `docs/evidence/m4/pacing-inactivity.txt` — quoted above; reproduces
  byte-for-byte under `scripts/verify-pacing-evidence.sh`.
- Chain costs, from the same report: `trail=1400, water=2350, settlement=3700,
  wetland=4150, forest=5000, research=2800`; total 19,400.
- `tools/simulation/Commands.cs` `Profile` — `ChainVitality` sums from content;
  `totalSteps += steps` accumulates the **pre-clamp requested** value, which is
  what produced the wrong `pathological` figure and is itself a reporting
  defect.
- `docs/ROADMAP.md` §M4 "Intermittent-engagement pacing finding" and
  `docs/RISK_REGISTER.md` R-013 — both state "≈686 steps/Vitality".
- `docs/DECISIONS.md` D-039 — the provisional targets this change must
  supersede or ratify.
- `docs/KNOWN_ISSUES.md` KI-003 (Medium, "reserved decision") and KI-004 (Low,
  "provisional").

**Affected code**

- `src/WalkGame.Application/Content/Region1Catalog.cs` — the balance numbers,
  if the chosen remedy re-costs content.
- `tools/simulation/Commands.cs` — the `profile` report must print the eligible
  post-clamp step total and the effective steps-per-Vitality ratio, and should
  report a per-chain "shortfall" when the horizon is reached with chains
  incomplete.
- `scripts/verify-pacing-evidence.sh` — unchanged as a gate; the committed
  reports it compares against are updated.

**Committed evidence that changes**

All seven `docs/evidence/m4/pacing-*.txt` reports, if any authored number
changes. This is expected and is the point: the change is worthless if the
evidence is not regenerated.

**Affected tests**

- `M4ContentGraphTests.AuthoredRegion1Catalog_MeetsTheDocumentedContentMinimum`
  (2–7 chains, 6+ landmarks, 10+ discoveries, 3+ expeditions) — the remedy must
  not reduce content below these minimums.
- `M4Region1AcceptanceTests` — the closure acceptance test must still reach the
  closure milestone under its profile.
- `M3AmbientProgressionAcceptanceTests` — byte-identical determinism.

**Dependencies**

- Apply **after** `bound-secondary-resource-economy`: that change may author
  secondary-resource requirements, which alters completion days, and doing both
  at once would make the evidence diff unreviewable.
- Apply **after** `gate-saves-against-authored-content-version`: a balance change
  alters what existing saves mean, and the content-version mechanism must be in
  place to record it.
- Apply **after** `anchor-activity-acceptance-window-to-trusted-time`: the
  acceptance window determines which records a returning player gets, and the
  remedy must be balanced against the post-change window.
