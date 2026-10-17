## Purpose

Defines the engine-agnostic presentation contract for an interactive world view: per-location state, characters and dialogue, restoration-choice previews and visual-change events, all derived from canonical state.

## ADDED Requirements

### Requirement: Per-location world state derived from canonical state

The application SHALL expose one world-view row per authored landmark with its id, title, restoration stage, a style-neutral variant key, locked/unlocked, interactable, its projects and selectable restoration goals, an optional construction variant key, the characters present and pending dialogue. Every field SHALL be derived from canonical state and authored content at read time, and reading it SHALL NOT change canonical state or the save.

#### Scenario: Fresh valley
- **WHEN** a new game is started
- **THEN** the trailhead is unlocked and interactable with "Clear the old trailhead" selectable
- **AND** every landmark reports stage Ruined with variant key `<id>@ruined`

#### Scenario: Reading is side-effect free
- **WHEN** the world view is read any number of times
- **THEN** the encoded save is byte-identical before and after

### Requirement: Dialogue is short, skippable, revisitable and never gating

Characters SHALL have authored lines of at most 140 characters, each keyed by a restoration milestone or by none. All reached lines SHALL remain readable. Dialogue SHALL be localizable through `ContentText`. No progression rule SHALL read dialogue state, and "seen" state SHALL be held by presentation, not by the save.

#### Scenario: A milestone adds a line
- **WHEN** the project a line is keyed to completes
- **THEN** that line becomes the character's current line and earlier reached lines stay listed

#### Scenario: Never reading dialogue
- **WHEN** a player completes the region without ever reading a line
- **THEN** canonical progression is identical to a player who read every line

### Requirement: Restoration choices preview their consequences truthfully

Each selectable or in-progress project SHALL offer before/after variant keys for its location and typed consequences. The predicted consequences SHALL equal the effects that actually occur when that project completes. A preview SHALL NOT name an unfound discovery.

#### Scenario: Preview matches outcome
- **WHEN** a previewed project completes
- **THEN** the landmark stages, opened projects, producers, arc stages, discoveries and chapters that changed are exactly those the preview listed

### Requirement: Visual changes are an ordered, derived event stream

The application SHALL derive visual-change events from canonical completion timestamps, deterministically and without new persisted state, and SHALL derive in-session events by diffing two world views. The stream SHALL never report a change that canonical state does not contain.

#### Scenario: Return after a long absence
- **WHEN** the player returns after offline progress completed projects
- **THEN** the changes since the last viewed instant are listed in completion order with from/to variant keys

#### Scenario: Unverifiable history
- **WHEN** replaying completions cannot reproduce the canonical landmark stages
- **THEN** no events are reported rather than invented ones
