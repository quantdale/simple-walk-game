## ADDED Requirements

### Requirement: Style-neutral Unity presentation seam

The Unity layer SHALL bind the world-presentation read models through presenter interfaces (world, landmark, dialogue, choice preview, visual-change player) that name no art style. A presenter that draws nothing SHALL be able to run the whole loop.

#### Scenario: Tap a place
- **WHEN** the player taps an unlocked place
- **THEN** that place's restoration goals and the current lines of the characters there are presented
- **AND** a tap on a locked or unknown place presents nothing

### Requirement: Dialogue never blocks and choosing uses the existing command

Dialogue SHALL be skippable at any time and SHALL NOT be required before choosing a goal or before walking progress applies. Choosing a goal SHALL call the existing `EnqueueProject` command with the text shell's semantics.

#### Scenario: Choose while someone is talking
- **WHEN** a goal is chosen while a dialogue is open
- **THEN** the goal is queued exactly as by `EnqueueProject`, and the save equals that of a player who never opened the dialogue

### Requirement: Changes since the last open are replayed once

On resume the presentation SHALL hand the visual changes since the last shown instant to the change player once, in stream order, and SHALL NOT replay them again.

#### Scenario: Second resume
- **WHEN** the app resumes twice with no new progress in between
- **THEN** the second resume replays nothing

### Requirement: Asset keys resolve through a pluggable catalog with a fallback

Keys SHALL resolve through a replaceable catalog with a deterministic fallback (exact, subject, global, missing). A missing asset SHALL never throw, and missing keys SHALL be listed.

#### Scenario: No art yet
- **WHEN** the catalog is empty
- **THEN** every key resolves to Missing and is listed once
