## Purpose

Defines how a short walk is shown as visible progress.

## ADDED Requirements

### Requirement: Any funded project shows percent and distance to the next visible change

When the active project has received Vitality and is not complete, Home and the return summary SHALL show its percent restored (never 0% and never 100% before completion) and the approximate steps to its next visible change (an authored build stage, or completion). The value SHALL be derived at display time from canonical state and SHALL NOT be persisted.

#### Scenario: 300-step first walk
- **WHEN** a new player queues "Clear the old trailhead" (300 Vitality) and walks 300 steps
- **THEN** Home reads "Clear the old trailhead: 1% restored. About 29,700 more steps to finish it."

#### Scenario: Project with build stages
- **WHEN** "Rebuild the settlement workshop" (1,500 Vitality, 2 stages) has 1 Vitality invested
- **THEN** the line names about 74,900 steps to its next visible stage

#### Scenario: Nothing funded
- **WHEN** the active project has no Vitality invested
- **THEN** no payoff line is shown

### Requirement: Home does not repeat a funded project (D-149)

When the payoff line is shown on Home, Home SHALL NOT also show the "{title} is underway ({invested} of {cost})." restoring-now line for the same project. Before anything is invested, the restoring-now line SHALL still be shown.

#### Scenario: Funded first project
- **WHEN** the first project has 20 of 300 Vitality invested
- **THEN** Home shows "Clear the old trailhead: 6% restored. About 28,000 more steps to finish it." and not "Clear the old trailhead is underway (20 of 300)."
