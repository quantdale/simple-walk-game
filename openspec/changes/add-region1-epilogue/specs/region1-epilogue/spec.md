## Purpose

Defines the calm Region 1 epilogue shown after Millbrook is restored.

## ADDED Requirements

### Requirement: The epilogue is derived from region completion

The epilogue SHALL be unavailable, exposing no text, until the region is completed. Derivation SHALL NOT write state. Entries SHALL appear by whole days since `RegionCompletedAtUtc` and SHALL NOT disappear as time moves forward; a clock before completion SHALL show the day-0 epilogue.

#### Scenario: Region completed
- **WHEN** the closure milestone completes
- **THEN** the Story journal shows the final journal entry, Nell's first closing letter and the first valley note

#### Scenario: Continuing player
- **WHEN** a player opens the journal 95 days after completion
- **THEN** both closing letters and four valley notes are readable, and "In the valley now" shows the fourth note

### Requirement: The epilogue never pressures

Epilogue copy and its surfaces SHALL contain no streak, guilt, count-to-collect or time-pressure language and SHALL contain no Region 2 hook.

#### Scenario: New game
- **WHEN** a new game opens the Story journal
- **THEN** no epilogue line is shown
