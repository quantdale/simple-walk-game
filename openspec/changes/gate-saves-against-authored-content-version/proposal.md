# Gate saves against the authored content version they were written for

## Why

`RegionDefinition.ContentVersion` exists, is set to 2, and is never read by
anything. A grep over `src/`, `tools/` and `tests/` finds exactly two
non-constructive uses: the constant declaration and the constructor call. No
validator, session, codec or CLI path compares a save against the content
version it is being loaded with.

The consequence is a specific, unrecoverable failure mode. Canonical state
fails **closed** on unknown content — `MatureSaveMigrationTests` proves
`UnknownDiscoveryRuntime_InPayload_FailsClosedWithoutRewrite` and
`UnknownProjectRuntime_InPayload_FailsClosed`. That is correct behaviour, and
it is exactly what makes a content rename dangerous: if a future campaign
renames `proj.clear-trailhead` to `proj.cut-trailhead`, or drops a discovery
that an existing save already unlocked, every existing save fails validation on
boot, both generations are unreadable, and the player has no recovery path. The
one-generation backup holds the same invalid content, so it fails too.

RISK_REGISTER R-018 ("Content IDs/schema drift break mature saves", High /
Open) lists the intended mitigations as "immutable stable IDs; content
validator; **tombstone/migration policy**; no array-index identity". Two of the
four are enforced. The tombstone/migration policy does not exist, and nothing
detects the drift before it destroys a profile.

## What Changes

- A save records the authored content version it was created against, so the
  version relationship between a save and the content being loaded with is
  checkable rather than assumed.
- The boot path **compares** the two and produces a distinct, diagnosable
  outcome for: exact match, save older than content and still compatible, save
  newer than content, and save referencing content the build no longer
  defines.
- A content-removal policy is introduced so that retired content IDs resolve to
  an explicit tombstone instead of an unresolvable reference. Tombstones are
  authored, validated, and must be reachable only from content that can no longer
  be reached, so they cannot silently hide live content.
- A validator-level check reports content-drift risk during authoring: any ID
  present in the authored tombstone set that is also a live definition is a
  content error.
- The content-version guard is available to the CLI and to the release gate, so a
  release candidate can prove every retained save fixture still decodes against
  the shipped content.

**Non-goals:** renaming, removing or re-costing any Region 1 content; changing
the save schema version; implementing a general content-migration framework; or
retrofitting the existing `ISaveMigration` chain (that chain migrates *payload
schema*, which is a different axis from *authored content*).

## Capabilities

### New Capabilities

- `content-compatibility`: the contract binding a durable save to the authored
  content it was written against — how the content version is recorded, how
  compatibility is determined at boot, how retired content identifiers are
  tombstoned, and what the system does when a save references content the
  current build does not define.

### Modified Capabilities

<!-- None. `openspec/specs/` is empty at HEAD; `content-compatibility` is
     introduced here. It is deliberately a separate capability from
     `save-recovery` (introduced by the backup-recovery change) because payload
     schema migration and authored-content compatibility are independent axes
     with independent failure modes. -->

## Impact

**Evidence from the audit**

- `src/WalkGame.Domain/Regions/RegionModel.cs` — `RegionDefinition.ContentVersion`
  is a get-only property assigned once in the constructor and never read.
- `src/WalkGame.Application/Content/Region1Catalog.cs` —
  `AuthoredContentVersion = 2`, passed to the constructor; no other reference.
- `tests/WalkGame.Application.Tests/M4ContentGraphTests.cs` — the only test
  reference is line 106, which *copies* `baseContent.ContentVersion` when
  constructing a derived content fixture. Nothing asserts on its value.
- `tests/WalkGame.Application.Tests/MatureSaveMigrationTests.cs` —
  `UnknownDiscoveryRuntime_InPayload_FailsClosedWithoutRewrite` and
  `UnknownProjectRuntime_InPayload_FailsClosed` demonstrate the destructive
  half of the problem with no guard in front of it.
- `docs/RISK_REGISTER.md` R-018 — the tombstone/migration mitigation is listed
  but does not exist.
- `src/WalkGame.Domain/GameState.cs` — `GameState` has `SchemaVersion` but no
  content-version field.

**Affected code**

- `src/WalkGame.Domain/GameState.cs` — record the authored content version the
  save was created against.
- `src/WalkGame.Domain/Regions/RegionModel.cs` — authored tombstone set on
  `RegionDefinition`.
- `src/WalkGame.Domain/Validation/GameStateValidator.cs` — compatibility verdict
  and tombstone resolution.
- `src/WalkGame.Application/GameSession.cs` — boot classification and a new
  `StartStatus` / `DiagnosticsBootOutcome` value for an incompatible content
  version.
- `src/WalkGame.Domain/Validation/ContentValidator.cs` — tombstone/live
  collision check.
- `tools/simulation/Commands.cs` — expose the verdict in `validate` and
  `dump`.
- `scripts/verify-architecture.sh` — no change expected; layering is preserved.

**Schema impact**

Recording the content version in `GameState` is an **additive** field. Per the
schema's established convention (D-036) it decodes as absent-means-unknown,
which must be treated as "written by a build that did not record it" and
handled as compatible-with-legacy rather than as a failure. `SchemaVersions`
is deliberately **not** bumped; see design.md.

**Affected tests**

- `MatureSaveMigrationTests` (4), `M4BackwardDecodingTests` (2),
  `SaveCodecRoundtripTests` (5), `M4ContentGraphTests` (9),
  `ContentValidationTests` (8), `SessionBootTests` (5),
  `DiagnosticsReadModelTests` (8), `M4Region1AcceptanceTests`,
  `M3AmbientProgressionAcceptanceTests`.

**Dependencies**

- Apply **after** `recover-save-from-backup-when-primary-is-missing`: that
  change establishes the complete boot-outcome taxonomy this change adds a
  member to.
- Apply **after** `align-content-validator-with-simulation-overflow-bounds`,
  which also edits `ContentValidator`.
- The `inactivity` pacing change (`close-inactivity-pacing-shortfall`) re-uses
  the same mechanism if it re-costs any project, so it should land after this
  one.
