# Add first-session payoff (display-time progress after any credit)

**Status:** Approved for implementation by the executing agent under delegated design authority (D-145). Region 1 scope; no save, schema, content-version or content-hash change.

## Why

Product invariant: very short sessions pay off. The first project (`proj.clear-trailhead`) costs 300 Vitality (30,000 steps). A typical 300–2,000-step first walk funds 1–7% of it, and the return summary only says "Vitality credited: N." Home says "Clear the old trailhead is underway (3 of 300)." Nothing tells the player that the walk moved anything visible, or how far the next visible change is.

## What Changes

- New pure helper `WalkGame.Application.Pacing.ProjectPayoff.For(invested, cost, visibleBuildStages)`: integer percent (at least 1 once anything is invested, at most 99 before completion), the Vitality and approximate steps (`StepConversionRuleV1.StepsPerVitality`) to the next visible change, and whether that change is an authored build stage (D-071) or completion. The next-stage threshold uses the same floor rule as `ProjectDefinition.BuildStageFor`.
- `HomeReadModel` gains `ActiveProjectVisibleBuildStages` (optional ctor arg, default 1) and the derived `ActiveProjectPayoff`.
- `GlanceReadModel` gains `PayoffSentence` (optional ctor arg): "{title}: {n}% restored. About {steps} more steps to finish it." or "... to its next visible stage." Localizable via the glance overlay (`glance.payoff-finish`, `glance.payoff-next-stage`).
- Unity shell: Home shows the payoff line under "restoring now"; the return summary shows it after the stored items whenever the summary has a current item (micro-feedback after any credit).

## Considered and rejected

- **Onboarding-only gentle first-project tuning** (cheaper first project). Changes authored content, so the content version/hash and the deterministic pacing evidence (`verify-pacing-evidence.sh`, cadence gate) would all move, and a save gate migration would be needed for a purely perceptual problem. Display-time progress fixes the perception without touching canonical state. Revisit only if device playtests show the payoff copy is not enough.
- **A new stored summary line.** Would add persisted text and a `SummaryLineText` pattern; display-time derivation is always current and needs no storage.

## Non-goals

Streaks, time pressure, notifications, any change to costs, conversion or reward identity.

## Compatibility

None. Read models only; saves, schema, content versions, fingerprint and pacing evidence are unchanged.
