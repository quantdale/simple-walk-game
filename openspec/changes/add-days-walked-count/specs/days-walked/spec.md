## Purpose

Defines how the game counts the days a player has walked, honestly, across save versions.

## ADDED Requirements

### Requirement: Days are local calendar days of credited records

A day SHALL count when at least one credited record with steps ends on that local date, using the provider's zone offset for that record. A record without an offset SHALL NOT be assigned a day, and the count SHALL then be presented as a lower bound.

#### Scenario: Morning walk in UTC+8
- **WHEN** a record ends at 23:30 UTC with an offset of +8 h
- **THEN** it counts for the next UTC calendar date's local day

### Requirement: The count is maintained and never overstated

The count SHALL be maintained in the save and updated on credit, deletion and correction, and SHALL survive record retirement. A legacy save SHALL be migrated to a proven lower bound and shown as "at least N".

#### Scenario: Legacy save
- **WHEN** a v2 save with retained and retired rows is loaded
- **THEN** it migrates to v3 with a lower bound no greater than the true day count, and the Progress screen says "at least"

#### Scenario: Deleted walk
- **WHEN** the only credited record on a day is deleted by the provider
- **THEN** that day no longer counts
