## Purpose

Defines optional character letters revealed by restoring Region 1 landmarks.

## ADDED Requirements

### Requirement: Letters are derived from project completion

A letter SHALL be found if and only if its tied project is completed, and SHALL never become unfound. Derivation SHALL NOT write state or read a clock.

#### Scenario: Trailhead cleared
- **WHEN** the old trailhead project completes
- **THEN** the first letter is found and appears in the Story journal with where it was found

### Requirement: Letters never spoil or pressure

Unfound letters SHALL expose no text. Letter copy and its surfaces SHALL contain no streak, guilt or time-pressure language, and nothing SHALL be listed before the first letter is found.

#### Scenario: New game
- **WHEN** a new game opens the Story journal or Discoveries
- **THEN** no letter line is shown
