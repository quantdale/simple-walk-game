## MODIFIED Requirements

### Requirement: Home does not repeat a funded project (D-149)

When the payoff line is shown on Home, Home SHALL NOT also show the "Now restoring: {title} ({invested} of {cost})." restoring-now line for the same project. Before anything is invested, the restoring-now line SHALL still be shown.

#### Scenario: Funded first project
- **WHEN** the first project has 20 of 300 Vitality invested
- **THEN** Home shows "Clear the old trailhead: 6% restored. About 28,000 more steps to finish it." and not "Now restoring: Clear the old trailhead (20 of 300)."
