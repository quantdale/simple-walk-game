## ADDED Requirements

### Requirement: Presentation reads models and calls use cases only

The Unity shell SHALL obtain everything it displays from Application read models
and SHALL perform every state change through a `GameSession` use case. Unity
assemblies SHALL NOT mutate Domain state objects directly and SHALL NOT construct
persistence adapters outside the single composition root.

#### Scenario: Architecture guard rejects direct state mutation

- **WHEN** a file under `src/WalkGame.Unity/Assets` references a Domain state mutator or ledger type
- **THEN** `scripts/verify-architecture.sh` fails and names the file

#### Scenario: Screens are driven by read models

- **WHEN** an EditMode test hands a screen controller a read model fixture
- **THEN** the rendered state matches the model without any `GameSession` mutation

### Requirement: Save data lives in app-private storage

The composition root SHALL construct the save and preferences stores on
`Application.persistentDataPath` only (D-067), and no save generation SHALL be
written elsewhere.

#### Scenario: Composition root uses the persistent data path

- **WHEN** the shell boots
- **THEN** the save store directory equals `Application.persistentDataPath`

### Requirement: Every boot outcome has a defined screen state

The shell SHALL map every `StartStatus` value to a defined screen state: a new
game routes to onboarding, a loaded or recovered save routes to Home (recovery
is shown as a notice), and unreadable, invalid, or newer-content saves route to
a non-destructive error state that never starts a new game silently.

#### Scenario: Unreadable save never becomes a fresh profile

- **WHEN** boot returns `SaveUnreadable`
- **THEN** the error state is shown and no `StartNewGame` call is made

### Requirement: Delete all local data requires explicit confirmation

Settings SHALL offer "Delete all local data" behind a confirmation step that
states it is permanent. Only the confirm intent SHALL call `DeleteAllLocalData`.
An `Erased` result routes to first-run onboarding; `NotSupported` or `Failed`
shows an error and leaves the shell usable.

#### Scenario: Cancel deletes nothing

- **WHEN** the player opens the confirmation and cancels
- **THEN** `DeleteAllLocalData` is not called and the save files are unchanged

#### Scenario: Confirm erases and returns to onboarding

- **WHEN** the player confirms
- **THEN** `DeleteAllLocalData` is called once and the onboarding screen is shown

### Requirement: Batchmode evidence

The change SHALL provide scripts that run EditMode tests and build an Android
debug APK in `-batchmode -nographics` with editor 6000.3.26f1, and SHALL commit
their logs as evidence. A step that could not run SHALL be recorded as
UNVERIFIED.

#### Scenario: EditMode suite runs headless

- **WHEN** `tools/unity/run-editmode-tests.sh` is executed on an activated box
- **THEN** it exits 0 and writes an NUnit XML result under `docs/evidence/m5-u1/`
