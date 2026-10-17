## ADDED Requirements

### Requirement: Every project has one visual key
The world view SHALL give each project at a location exactly one visual key:
`<project>@ruined` while not started, `<project>@build-kofN` while active with N visible build
stages, and `<project>@done` once completed. Keys SHALL only move forward.

#### Scenario: The trailhead bridge is rebuilt in stages
- **GIVEN** the trail is cleared and the bridge is the active goal
- **WHEN** Vitality is invested in it until it completes
- **THEN** its key moves ruined → build-0of3 → build-1of3 → build-2of3 → done

### Requirement: Project completion is one change event
The change stream SHALL emit one `ProjectCompleted` event per completed project with ToKey
`<project>@done`, first among the events of that completion, and the in-session diff SHALL
emit the same shape.

#### Scenario: The bridge finished while the app was closed
- **GIVEN** the bridge completed after the last open
- **WHEN** the app is opened after a cold restart
- **THEN** one `ProjectCompleted` event for the bridge is replayed first in its batch, and not again on the next open
