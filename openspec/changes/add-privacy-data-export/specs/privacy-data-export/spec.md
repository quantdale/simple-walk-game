## Purpose

Defines the local-only privacy screen and save export.

## ADDED Requirements

### Requirement: Players can see what is stored, in plain words

The game SHALL offer a Privacy screen from Settings that lists what is stored on the phone, where, and what is never collected, without raw identifiers.

#### Scenario: Open the screen
- **WHEN** a player with a loaded game opens Settings and chooses "Privacy and your data"
- **THEN** the Privacy screen lists stored data, says nothing is sent anywhere, and offers export and a way back

### Requirement: Export goes only through the OS share sheet

Export SHALL produce a readable JSON copy of the current save, verified against its checksum, and SHALL hand it to the OS share sheet as text. The game SHALL make no network call and SHALL NOT write the export to shared storage.

#### Scenario: Export succeeds
- **WHEN** the player chooses export and the share sheet is available
- **THEN** the share sheet receives a JSON document with `format` "simple-walk-game-export" and the decoded save state

#### Scenario: No share sheet
- **WHEN** the device has no share sheet
- **THEN** the player sees one calm sentence and nothing else changes

#### Scenario: No game loaded
- **WHEN** no game is loaded
- **THEN** export is refused calmly
