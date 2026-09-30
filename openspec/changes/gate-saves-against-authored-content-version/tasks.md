## 1. Record the authored content version (additive, no schema bump)

- [ ] 1.1 In `src/WalkGame.Domain/GameState.cs`, add an additive
  `AuthoredContentVersion` field (nullable integer) to `GameState`. It records
  the content version the profile was **created** against.
- [ ] 1.2 In `GameFactory.NewGame`, set it to `content.ContentVersion`. Do NOT
  update it anywhere else, so later commits preserve the original value.
- [ ] 1.3 Do **not** bump `SchemaVersions.Current` and do **not** add an
  `ISaveMigration`. Absence of the field decodes to "unknown" and is evaluated
  as the oldest supported content version.
- [ ] 1.4 Extend `tests/WalkGame.Infrastructure.Tests/SaveCodecRoundtripTests.Encode_Decode_PreservesEveryField`
  to cover the new field, including a state where it is absent.
- [ ] 1.5 Confirm unchanged and still passing: `M4BackwardDecodingTests` (2),
  `MatureSaveMigrationTests` (4), `MigrationV1ToV2Tests` (4),
  `MigrationRunnerTests` (6).

## 2. Authored tombstones

- [ ] 2.1 In `src/WalkGame.Domain/Regions/RegionModel.cs`, add a read-only
  tombstone set to `RegionDefinition` with a constructor parameter defaulting to
  empty. A tombstone carries a stable identifier and a non-empty human-readable
  reason; reject an empty reason at construction.
- [ ] 2.2 Resolve tombstoned references in
  `src/WalkGame.Domain/Validation/GameStateValidator.cs`: a runtime project,
  discovery or expedition whose ID is declared as a tombstone validates
  successfully and resolves to its retired form; an ID that is neither live nor
  tombstoned still fails closed with a violation naming it.
- [ ] 2.3 Ensure a tombstoned reference never grants new progression. A
  tombstoned producer must not unlock, a tombstoned project must not complete,
  and a tombstoned discovery/expedition must not award. Cover at least one of
  each in tests.
- [ ] 2.4 Leave the Region 1 tombstone set **empty** in this change. The
  mechanism and its guard are introduced; nothing is retired.

## 3. Content validation rules

- [ ] 3.1 In `src/WalkGame.Domain/Validation/ContentValidator.cs`, add: an
  identifier cannot be both a live definition and a tombstone; a live project
  may not list a tombstoned identifier as a prerequisite; a tombstone must carry
  a reason. Name the identifier in every violation.
- [ ] 3.2 Add `ContentValidationTests` cases for each rule, including a positive
  case proving a valid tombstone set yields zero violations.
- [ ] 3.3 Confirm `Region1Catalog.Create()` still validates with zero violations
  through `M4ContentGraphTests.AuthoredRegion1Catalog_Validates_WithZeroViolations`.

## 4. Boot classification

- [ ] 4.1 In `src/WalkGame.Application/GameSession.cs`, classify the
  save↔content relationship during boot as exact match, compatible older, or
  newer than this build, and capture it alongside the existing boot evidence.
- [ ] 4.2 A save recorded against a newer content version SHALL fail closed with
  a distinct `StartStatus` value and a matching `DiagnosticsBootOutcome` value,
  and SHALL NOT be rewritten. Coordinate the new enum members with the
  taxonomy work already landed by the backup-recovery change.
- [ ] 4.3 Expose the classification in `DiagnosticsReadModel` as bounded,
  privacy-safe fields (a small enum plus the two version integers). No strings
  from the payload.
- [ ] 4.4 Add `DiagnosticsReadModelTests` cases: exact match, compatible older,
  and newer-than-build fail-closed. Confirm `FutureSchemaSave_ClassifiesVersionTooNew_AndFailsClosed`
  (payload-schema axis) still behaves as before and that the two axes are
  reported independently.

## 5. CLI surface

- [ ] 5.1 In `tools/simulation/Commands.cs`, have `validate` print the
  content-compatibility verdict alongside the existing schema version and
  violation count.
- [ ] 5.2 Have `dump` print the recorded authored content version.
- [ ] 5.3 Update the `validate` / `dump` usage text in `Cli.Usage` if it
  enumerates their output.

## 6. Content-drift gate (must live in ci.yml)

- [ ] 6.1 Add a check that runs the shipped content against the repository's
  retained mature-save fixtures and fails on any unresolved identifier or
  validation violation. Put the reusable logic in a script under `scripts/`.
- [ ] 6.2 Add a `run:` step to `.github/workflows/ci.yml` invoking that script.
  It MUST be inside the workflow file, because `scripts/verify-ci-steps.sh`
  executes the workflow verbatim (D-049) — a script CI does not call is not a
  gate.
- [ ] 6.3 Commit the script with mode 100755 and LF line endings.
  `scripts/verify-architecture.sh` rule 6 fails otherwise, and that failure is
  the exact latent breakage the rule exists to catch.
- [ ] 6.4 Prove the gate fails on a deliberately broken fixture (a fixture with
  an unknown project ID) before trusting it, following the same
  prove-it-fails-first discipline `verify-architecture.sh` documents.
- [ ] 6.5 Add a doc section to `docs/AGENT_EXECUTION_GUIDE.md` — "changing
  authored content" — stating that a save-meaning change requires a content
  version bump or a tombstone, and that the new gate will fail otherwise.

## 7. Documentation

- [ ] 7.1 `docs/DECISIONS.md` — record the content-version axis, why it is
  separate from payload schema, and the tombstone policy.
- [ ] 7.2 `docs/TECHNICAL_ARCHITECTURE.md` — document the two version axes and
  where each is enforced.
- [ ] 7.3 `docs/RISK_REGISTER.md` R-018 — replace the "mitigation not
  implemented" status with the evidence that the tombstone policy and drift gate
  now exist, stating what remains unmitigated (a drift shipped through an
  intermediate build).
- [ ] 7.4 `docs/KNOWN_ISSUES.md` — add an entry only if a mitigation gap
  remains; the tombstone mechanism is the fix for the destructive half.

## 8. Verification

- [ ] 8.1 `dotnet build SimpleWalkGame.sln` — zero errors, no new warnings.
- [ ] 8.2 `dotnet test` — all suites green; confirm both acceptance suites still
  assert and achieve byte-identical deterministic state.
- [ ] 8.3 `scripts/verify-architecture.sh` — 11 passed, 0 failed (now including
  the new script's exec-bit/CRLF rule).
- [ ] 8.4 `scripts/verify-ci-steps.sh` — every step, including the new gate,
  passes locally.
- [ ] 8.5 `scripts/verify-pacing-evidence.sh` and
  `scripts/verify-save-growth.sh` — unchanged and still green (this change must
  not perturb content or state).
- [ ] 8.6 `tests/guards/run-guard-tests.sh` — green.
- [ ] 8.7 `openspec validate gate-saves-against-authored-content-version --strict`
  passes.
- [ ] 8.8 `git status` clean apart from intended files; writer lease released.
