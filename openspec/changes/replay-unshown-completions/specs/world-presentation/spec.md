## MODIFIED Requirements

### Requirement: Changes since the last open are replayed once

On resume the presentation SHALL hand every visual change whose completion has not yet been
shown to the change player once, in stream order, and SHALL NOT replay it again, including
after a restart. Which completions were shown SHALL be remembered by stable completion id in
the presentation-memory file (never the save), so a completion stamped earlier than the last
shown instant is still replayed. Memory written before completion ids were recorded SHALL
load, treating every completion at or before its shown instant as shown.

#### Scenario: Second resume
- **WHEN** the app resumes twice with no new progress in between
- **THEN** the second resume replays nothing

#### Scenario: Device clock corrected backward
- **GIVEN** the trailhead was cleared and shown, and the device clock is then set back a day
- **WHEN** the bridge is rebuilt and the app resumes, also after a cold restart
- **THEN** the bridge's changes are replayed once, the trailhead's are not repeated, and every event of the stream has been shown exactly once

#### Scenario: Old memory file
- **GIVEN** a presentation-memory file without shown completion ids
- **WHEN** it is loaded
- **THEN** it loads, completions at or before its shown instant count as shown, and the next write records ids

## ADDED Requirements

### Requirement: The change stream respects prerequisites
The change stream SHALL never place a completion before a completed prerequisite, even when
the stored completion times are out of order.

#### Scenario: Regressed stamps
- **GIVEN** the bridge's completion time is earlier than the trailhead's, which it requires
- **WHEN** the stream is read
- **THEN** the trailhead's changes come first
