## ADDED Requirements

### Requirement: The Old Trailhead draws its canonical state with painted layers
The trailhead view SHALL choose its ground plates from the landmark variant key and its structure layer from the bridge visual key,
SHALL fall back without throwing when a key has no asset, and SHALL never write game state.

#### Scenario: Bridge build progress
- **WHEN** the bridge visual key moves `@ruined` → `@build-0of3` → `@build-1of3` → `@build-2of3` → `@done`
- **THEN** the structure layer shows collapsed → posts → half span → full span → decorated, in that order

#### Scenario: Reduced motion
- **WHEN** reduced motion is on
- **THEN** stage changes swap instantly and Mira does not bob

### Requirement: Mira's dialogue is skippable and drawn in-engine
Tapping Mira SHALL open her current line with her portrait and the place actions plus Skip; all text is rendered by UI Toolkit.

#### Scenario: Skip
- **WHEN** the player taps Skip
- **THEN** the dialogue closes and the line counts as seen
