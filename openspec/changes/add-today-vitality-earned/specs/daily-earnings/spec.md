## Purpose

Defines how the game reports the Vitality the player's walks earned on the current local day.

## ADDED Requirements

### Requirement: Today's earnings are derived from credited records, never stored separately

Today's earned Vitality SHALL be the sum of the net applied Vitality of retained processed records whose local day is today. It SHALL NOT be persisted as a separate ledger, and the save schema SHALL NOT change.

#### Scenario: A replayed record
- **WHEN** a record already credited today is delivered again with the same revision
- **THEN** today's earned Vitality is unchanged

### Requirement: Never overstated

A record without a known local day SHALL NOT count. A day that may include retired rows SHALL be reported as unknown. A correction SHALL reduce its day by the Vitality actually reversed, and a deleted walk SHALL count nothing.

#### Scenario: Provider deletes a walk
- **WHEN** a record credited today is deleted by the provider
- **THEN** that record no longer contributes to today's earned Vitality

#### Scenario: Legacy save
- **WHEN** a v1 or v2 save is loaded
- **THEN** its migrated rows have no local day and contribute nothing to today

### Requirement: Calm presentation

Home SHALL show "Today: N Vitality earned from your walks." only when N > 0, and SHALL show no line for a day with nothing earned.

#### Scenario: Nothing walked yet today
- **WHEN** no dated record has been credited today
- **THEN** Home shows no "Today" line
