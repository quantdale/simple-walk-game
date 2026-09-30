## Purpose

Defines how a profile is booted from durable save storage: how the two save
generations are addressed, how integrity failures are classified, when the
backup generation is used, when the profile is allowed to start fresh, and what
durable evidence is recorded so every recovery class is distinguishable in
support diagnostics. The governing rule is that no readable generation is ever
abandoned, and no unreadable state is ever silently replaced.

## ADDED Requirements

### Requirement: Both save generations are consulted before concluding no save exists

The boot flow SHALL address the primary generation and the backup generation
independently. It SHALL report that no save exists only when **both** generations
are absent, and SHALL report that no save exists immediately, without a disk
write, when the two slots are probed and both are absent.

A readable or decodable backup generation SHALL be preferred over reporting an
absent save, in every case where the primary generation is unavailable for any
reason — including the primary file being absent entirely.

#### Scenario: Both generations absent reports a fresh profile

- **WHEN** the boot flow is invoked and neither the primary nor the backup save
  slot exists on storage
- **THEN** the result classifies the outcome as no save found
- **THEN** no write is performed against either slot

#### Scenario: Primary slot absent with a healthy backup recovers instead of resetting

- **WHEN** the primary save slot does not exist on storage and the backup
  generation exists and passes integrity, schema and state validation
- **THEN** the result classifies the outcome as recovered from backup
- **THEN** the decoded backup state becomes the loaded canonical state
- **THEN** the summary of that state is presented to the caller

#### Scenario: Primary slot absent with a stale temporary file present

- **WHEN** the primary save slot does not exist, a backup generation exists and
  is valid, and a leftover temporary write file from a previous crashed commit
  is also present
- **THEN** the boot flow recovers the backup generation
- **THEN** the leftover temporary file is never promoted to either save slot as
  data

#### Scenario: Primary slot absent with an undecodable backup fails closed

- **WHEN** the primary save slot does not exist and the backup generation exists
  but fails integrity, schema or state validation
- **THEN** the result classifies the outcome as an unreadable save
- **THEN** the reported reason names the specific failure that was observed
- **THEN** neither slot is overwritten and no new world is created

#### Scenario: A recovered backup is not rotated over itself

- **WHEN** the boot flow recovers from the backup generation and then commits the
  recovered state
- **THEN** the commit writes the primary slot without promoting any existing
  primary generation into the backup slot
- **THEN** the backup generation that was recovered from remains readable until a
  later successful normal commit rotates it

### Requirement: Save availability is classified into a complete, non-overlapping outcome taxonomy

Every boot attempt SHALL be classified into exactly one outcome, and every
outcome value that the system declares SHALL be reachable from the boot flow.
A boot failure that is attributable to canonical state violating a documented
invariant SHALL be classified distinctly from a failure to read, decode or
integrity-check a save file, and distinctly again from the absence of any save.

The classification SHALL be stable and machine-readable so that composition
roots, the support diagnostics projection and command-line tooling can each
react appropriately instead of collapsing distinct failures into one generic
"corrupt" state.

#### Scenario: An invariant-violating save is classified as invalid state

- **WHEN** a save decodes successfully but its canonical state fails documented
  invariant validation
- **THEN** the outcome is classified as invalid canonical state
- **THEN** the reported reason includes the specific invariant violations
- **THEN** the outcome is not reported as an unreadable or missing file

#### Scenario: A file that cannot be read is classified as a read failure

- **WHEN** a save slot exists but cannot be opened or read because of an access
  or input/output failure
- **THEN** the outcome is classified as an I/O failure and not as absence
- **THEN** the boot flow continues to evaluate the other generation

#### Scenario: Every declared outcome value is produced by some reachable path

- **WHEN** the outcome taxonomy is enumerated
- **THEN** each declared value corresponds to at least one boot condition the
  system can actually produce
- **THEN** no declared value is unreachable dead classification

### Requirement: Boot classification is surfaced as bounded, privacy-safe diagnostics evidence

The support diagnostics projection SHALL report the boot outcome class, whether
recovery came from the backup generation, the structured category of the last
failed decode attempt, and the migrations applied during the decode. The
reported codec failure category SHALL be monotonic across a boot: a later
successful decode of the backup generation SHALL NOT erase the record of the
primary generation's failure, because that failure is what caused the recovery.

Diagnostics SHALL remain available even when the boot failed, because that is
precisely when support needs them. Diagnostics SHALL carry no raw save content,
no raw exception text generated by this codebase, and no unbounded strings.

#### Scenario: Recovery preserves the primary failure category

- **WHEN** the primary generation fails to decode and the backup generation
  decodes successfully
- **THEN** the diagnostics report the successful recovery
- **THEN** the diagnostics still report the primary generation's failure
    category

#### Scenario: Diagnostics are available when no state could be loaded

- **WHEN** the boot flow did not produce a loaded canonical state for any reason
- **THEN** the diagnostics projection is still constructible and reports the
  boot outcome, the schema version, and the region identity as unavailable

### Requirement: Recovery never destroys the last healthy generation

A commit issued during or after a recovery boot SHALL NOT promote a known-bad or
absent primary generation into the backup slot. The last generation that passed
integrity and invariant validation SHALL remain recoverable until a subsequent
normal commit, which has itself been validated, replaces it.

#### Scenario: A failed recovery commit leaves the healthy generation recoverable

- **WHEN** a recovery boot is followed by a commit that fails to persist
- **THEN** the healthy generation that was recovered remains readable
- **THEN** no partially written bytes are presented as a valid save

#### Scenario: Repeated recovery boots are stable

- **WHEN** a profile whose primary generation is absent and whose backup is valid
  is booted repeatedly
- **THEN** every boot recovers the same generation
- **THEN** the recovery notice is not duplicated or accumulated across boots
