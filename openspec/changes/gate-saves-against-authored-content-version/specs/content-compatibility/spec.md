## Purpose

Binds a durable save to the authored region content it was written against, so
that changes to authored content are detected and classified before they can
make an existing profile unbootable, and so that retired content identifiers
resolve to an explicit tombstone rather than to an unresolvable reference that
destroys a save.

## ADDED Requirements

### Requirement: A save records the authored content version it was created against

Every canonical save SHALL record the authored content version in effect when
the profile was created. The recorded value SHALL be preserved across subsequent
commits of that profile and SHALL NOT be silently rewritten when the content is
later updated.

A save that predates content-version recording SHALL decode as "version unknown"
and SHALL be treated as compatible with current content by the same rules that
apply to the oldest supported content version.

#### Scenario: A fresh profile records the current content version

- **WHEN** a new profile is created against region content
- **THEN** the persisted state records that content's authored version
- **THEN** later commits of the profile preserve the originally recorded value

#### Scenario: A save with no recorded content version is not treated as corrupt

- **WHEN** a save written before content-version recording is loaded
- **THEN** it decodes successfully with the recorded version treated as unknown
- **THEN** it is evaluated against the oldest supported content version rather
  than being rejected

#### Scenario: The recorded version round-trips exactly

- **WHEN** a state carrying a recorded content version is encoded and decoded
- **THEN** the decoded state carries the same recorded value

### Requirement: Boot classifies the relationship between save and content

The boot flow SHALL compare a save's recorded content version against the
content it is being loaded with and SHALL produce exactly one of: an exact
match; a compatible older save; a save written by a newer content version than
this build supports. Each SHALL be individually classifiable and SHALL be
surfaced in the durable boot diagnostics.

A save recorded against a **newer** content version SHALL fail closed: it SHALL
NOT be loaded, SHALL NOT be rewritten, and SHALL NOT be downgraded.

#### Scenario: Exact match is reported as such

- **WHEN** a save's recorded content version equals the loaded content's version
- **THEN** the boot succeeds
- **THEN** the diagnostics report an exact content match

#### Scenario: An older compatible save loads and is reported as older

- **WHEN** a save's recorded content version is lower than the loaded content's
  version, and every identifier it references still resolves
- **THEN** the boot succeeds
- **THEN** the diagnostics report the save as an older content version

#### Scenario: A newer save fails closed

- **WHEN** a save's recorded content version is higher than the loaded content's
  version
- **THEN** the boot fails closed with a distinct, specific outcome
- **THEN** neither save generation is rewritten
- **THEN** the reported reason states that the save was written by a newer
  version of the game content

### Requirement: Retired content identifiers are tombstoned, not erased

Content that has been removed from a region SHALL be declared as a tombstone
carrying a stable identifier and a human-readable reason. A canonical state
that references a tombstoned identifier SHALL continue to load successfully: the
reference SHALL resolve to a documented retired form rather than failing
validation.

Tombstones SHALL only apply to identifiers that are no longer live definitions.
Declaring an identifier as both live and tombstoned SHALL be a content
validation failure.

#### Scenario: A save referencing retired content still loads

- **WHEN** a canonical state references a project, discovery or expedition
  identifier that the current content has tombstoned
- **THEN** the state validates successfully
- **THEN** the reference resolves to its documented retired form
- **THEN** the player's earned progress for that content is preserved

#### Scenario: A reference to genuinely unknown content still fails closed

- **WHEN** a canonical state references an identifier that is neither a live
  definition nor a declared tombstone
- **THEN** validation reports a violation naming the identifier
- **THEN** the state is not rewritten

#### Scenario: Live and tombstoned identifiers cannot collide

- **WHEN** region content declares an identifier as both a live definition and
  a tombstone
- **THEN** content validation reports a violation naming the identifier
- **THEN** the content is not accepted

#### Scenario: A tombstone must reference content that once existed

- **WHEN** a tombstone is declared with no explanation of what it replaces
- **THEN** content validation reports a violation

### Requirement: Content drift is checked during authoring, not only at boot

Authored region content SHALL be validated so that an identifier intended to be
retired cannot silently remain reachable through live content, and so that a
project reachable from an entry point cannot depend on a tombstoned identifier.
A build's content SHALL also be checked against its retained save fixtures, so
that content drift is caught by a gate rather than by a player's first boot.

#### Scenario: A live project depending on a tombstoned prerequisite is flagged

- **WHEN** a live project's prerequisite refers to a tombstoned identifier
- **THEN** content validation reports a violation naming both identifiers

#### Scenario: A retained save fixture that no longer decodes fails the gate

- **WHEN** the shipped content is checked against the repository's retained
  mature-save fixtures
- **THEN** a fixture that fails to decode or validate against the shipped
  content is reported as a failure
- **THEN** the failure names the fixture and the unresolved identifier

#### Scenario: Adding content without a content-version bump is flagged

- **WHEN** authored content changes in a way that alters the meaning of existing
  saves and the authored content version is not increased
- **THEN** the change is reported as content drift
