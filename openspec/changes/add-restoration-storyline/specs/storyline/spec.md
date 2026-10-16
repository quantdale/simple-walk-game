## Purpose

Defines the restoration storyline: authored chapters that unlock from restoration milestones, read in order, never regress, and surface in a journal and in the return summary.

## ADDED Requirements

### Requirement: Chapters unlock only from restoration milestones, in order

A chapter SHALL unlock only when all of its required projects are completed, its required post-completion care cycles are reached, and the previous chapter is unlocked. Step counts, streaks and elapsed time SHALL NOT be criteria.

#### Scenario: Restoring out of order still reads in order
- **WHEN** a later chapter's projects complete before an earlier chapter's
- **THEN** the later chapter stays locked until the earlier chapter unlocks
- **THEN** both report unlock times in chapter order

#### Scenario: Walking alone does not unlock a chapter
- **WHEN** activity is credited but no required project completes
- **THEN** no new chapter unlocks

### Requirement: Unlocked chapters never lock again
Once a chapter is unlocked it SHALL remain unlocked across saves, restarts, idle periods and content-compatible upgrades. No surface SHALL describe a chapter as lost, missed or expiring.

#### Scenario: A month away
- **WHEN** a player returns after 30 idle days
- **THEN** every previously unlocked chapter is still unlocked

### Requirement: Storyline needs no save migration
Chapter state SHALL be derived from canonical state that saves already carry. A save written before chapters existed SHALL load and show every chapter its restorations have earned.

#### Scenario: An older save gains its earned chapters
- **WHEN** a content-version-4 save with completed projects loads under content version 5
- **THEN** it classifies as CompatibleOlder and the chapters matching its completed projects are unlocked

### Requirement: A newly unlocked chapter is a return-summary moment
When a committed batch unlocks a chapter, the pending return summary SHALL contain a "New chapter" item for it, within the existing summary bound.

#### Scenario: Chapter during an absence
- **WHEN** walking while the app was closed completes a chapter's last required project
- **THEN** the next return summary names the new chapter

### Requirement: Chapters land at sensible intervals
For every committed pacing profile that reaches closure, chapter unlock days SHALL be measured, and the longest gap between consecutive chapters SHALL stay within the bound recorded in D-076.
