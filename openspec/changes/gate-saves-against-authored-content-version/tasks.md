## 1. Record the authored content version (additive, no schema bump)

- [x] 1.1 In `src/WalkGame.Domain/GameState.cs`, add an additive
  `AuthoredContentVersion` field (nullable integer) to `GameState`. It records
  the content version the profile was **created** against.
- [x] 1.2 In `GameFactory.NewGame`, set it to `content.ContentVersion`. Do NOT
  update it anywhere else, so later commits preserve the original value.
- [x] 1.3 Do **not** bump `SchemaVersions.Current` and do **not** add an
  `ISaveMigration`. Absence of the field decodes to "unknown" and is evaluated
  as the oldest supported content version.
- [x] 1.4 Extend `tests/WalkGame.Infrastructure.Tests/SaveCodecRoundtripTests.Encode_Decode_PreservesEveryField`
  to cover the new field, including a state where it is absent.
- [x] 1.5 Confirm unchanged and still passing: `M4BackwardDecodingTests` (2),
  `MatureSaveMigrationTests` (4), `MigrationV1ToV2Tests` (4),
  `MigrationRunnerTests` (6).

## 2. Authored tombstones

- [x] 2.1 In `src/WalkGame.Domain/Regions/RegionModel.cs`, add a read-only
  tombstone set to `RegionDefinition` with a constructor parameter defaulting to
  empty. A tombstone carries a stable identifier and a non-empty human-readable
  reason; reject an empty reason at construction.
- [x] 2.2 Resolve tombstoned references in
  `src/WalkGame.Domain/Validation/GameStateValidator.cs`: a runtime project,
  discovery or expedition whose ID is declared as a tombstone validates
  successfully and resolves to its retired form; an ID that is neither live nor
  tombstoned still fails closed with a violation naming it.
- [x] 2.3 Ensure a tombstoned reference never grants new progression. A
  tombstoned producer must not unlock, a tombstoned project must not complete,
  and a tombstoned discovery/expedition must not award. Cover at least one of
  each in tests.
- [x] 2.4 Leave the Region 1 tombstone set **empty** in this change. The
  mechanism and its guard are introduced; nothing is retired.

## 3. Content validation rules

- [x] 3.1 In `src/WalkGame.Domain/Validation/ContentValidator.cs`, add: an
  identifier cannot be both a live definition and a tombstone; a live project
  may not list a tombstoned identifier as a prerequisite; a tombstone must carry
  a reason. Name the identifier in every violation.
- [x] 3.2 Add `ContentValidationTests` cases for each rule, including a positive
  case proving a valid tombstone set yields zero violations.
- [x] 3.3 Confirm `Region1Catalog.Create()` still validates with zero violations
  through `M4ContentGraphTests.AuthoredRegion1Catalog_Validates_WithZeroViolations`.

## 4. Boot classification

- [x] 4.1 In `src/WalkGame.Application/GameSession.cs`, classify the
  save↔content relationship during boot as exact match, compatible older, or
  newer than this build, and capture it alongside the existing boot evidence.
- [x] 4.2 A save recorded against a newer content version SHALL fail closed with
  a distinct `StartStatus` value and a matching `DiagnosticsBootOutcome` value,
  and SHALL NOT be rewritten. Coordinate the new enum members with the
  taxonomy work already landed by the backup-recovery change.
- [x] 4.3 Expose the classification in `DiagnosticsReadModel` as bounded,
  privacy-safe fields (a small enum plus the two version integers). No strings
  from the payload.
- [x] 4.4 Add `DiagnosticsReadModelTests` cases: exact match, compatible older,
  and newer-than-build fail-closed. Confirm `FutureSchemaSave_ClassifiesVersionTooNew_AndFailsClosed`
  (payload-schema axis) still behaves as before and that the two axes are
  reported independently.

## 5. CLI surface

- [x] 5.1 In `tools/simulation/Commands.cs`, have `validate` print the
  content-compatibility verdict alongside the existing schema version and
  violation count.
- [x] 5.2 Have `dump` print the recorded authored content version.
- [x] 5.3 Update the `validate` / `dump` usage text in `Cli.Usage` if it
  enumerates their output.

## 6. Content-drift gate (must live in ci.yml)

- [x] 6.1 Add a check that runs the shipped content against the repository's
  retained mature-save fixtures and fails on any unresolved identifier or
  validation violation. Put the reusable logic in a script under `scripts/`.
- [x] 6.2 Add a `run:` step to `.github/workflows/ci.yml` invoking that script.
  It MUST be inside the workflow file, because `scripts/verify-ci-steps.sh`
  executes the workflow verbatim (D-049) — a script CI does not call is not a
  gate.
- [x] 6.3 Commit the script with mode 100755 and LF line endings.
  `scripts/verify-architecture.sh` rule 6 fails otherwise, and that failure is
  the exact latent breakage the rule exists to catch.
- [x] 6.4 Prove the gate fails on a deliberately broken fixture (a fixture with
  an unknown project ID) before trusting it, following the same
  prove-it-fails-first discipline `verify-architecture.sh` documents.
- [x] 6.5 Add a doc section to `docs/AGENT_EXECUTION_GUIDE.md` — "changing
  authored content" — stating that a save-meaning change requires a content
  version bump or a tombstone, and that the new gate will fail otherwise.

## 7. Documentation

- [x] 7.1 `docs/DECISIONS.md` — record the content-version axis, why it is
  separate from payload schema, and the tombstone policy.
- [x] 7.2 `docs/TECHNICAL_ARCHITECTURE.md` — document the two version axes and
  where each is enforced.
- [x] 7.3 `docs/RISK_REGISTER.md` R-018 — replace the "mitigation not
  implemented" status with the evidence that the tombstone policy and drift gate
  now exist, stating what remains unmitigated (a drift shipped through an
  intermediate build).
- [x] 7.4 `docs/KNOWN_ISSUES.md` — add an entry only if a mitigation gap
  remains; the tombstone mechanism is the fix for the destructive half.

## 8. Verification

- [x] 8.1 `dotnet build SimpleWalkGame.sln` — zero errors, no new warnings.
- [x] 8.2 `dotnet test` — all suites green; confirm both acceptance suites still
  assert and achieve byte-identical deterministic state.
- [x] 8.3 `scripts/verify-architecture.sh` — 11 passed, 0 failed (now including
  the new script's exec-bit/CRLF rule).
- [x] 8.4 `scripts/verify-ci-steps.sh` — every step, including the new gate,
  passes locally.
- [x] 8.5 `scripts/verify-pacing-evidence.sh` and
  `scripts/verify-save-growth.sh` — unchanged and still green (this change must
  not perturb content or state).
- [x] 8.6 `tests/guards/run-guard-tests.sh` — green.
- [x] 8.7 `openspec validate gate-saves-against-authored-content-version --strict`
  passes.
- [ ] 8.8 `git status` clean apart from intended files; writer lease released.

---

## Implementation record (added during apply)

1. **`verify-architecture.sh` now reports 12 passed, not 11** (task 8.3 anticipated this
   with "now including the new script's exec-bit/CRLF rule"). Rule 6 discovered
   `scripts/verify-content-compatibility.sh` from the updated `ci.yml` and checked its
   committed mode and line endings automatically. That is the rule working as designed on a
   newly added CI-invoked script, with no edit to the script itself.

2. **The gate's first failure message was unhelpfully indirect, and proving it caught that.**
   The drifted-fixture proof initially reported only "validate produced no verdict" — which
   would have sent a reader hunting for a harness bug rather than a content break. The gate
   now prints the underlying validator output, so the proof failure names the actual cause:
   `Runtime project 'proj.renamed-away' is unknown to content definitions`. Task 6.4's
   discipline is what surfaced it.

3. **`Region1Catalog.Create(IEnumerable<ContentTombstone>?)` was added** (additive overload)
   so the tombstone mechanism can be proven against the REAL content graph. The
   parameterless overload production uses is untouched, and passing no tombstones is exactly
   the shipped content. Task 2.4 is respected: Region 1's tombstone set is empty.

4. **The two version axes are independent, and the test says so precisely.** An earlier
   draft asserted `diag.SchemaVersion == SchemaVersions.Current` on a save that FAILS to
   boot — which is impossible, because a failed boot loads no state and the field reads 0.
   The test now asserts what independence actually means: a current-schema save authored
   against a newer content version is rejected as `ContentIncompatible` while carrying NO
   codec failure and NO migrations. Conflating the axes would have produced the wrong
   corrective action ("migrate the payload") for a content problem.

5. **The infrastructure round-trip test needed a checksum helper** this file did not have
   (its only other case deliberately corrupts the payload so the checksum MISMATCHES). The
   helper re-serializes the payload with camelCase naming to match `SaveJson.Options` —
   serializing with a different naming policy would have broken the payload for a reason
   unrelated to the field under test.

6. **`KNOWN_ISSUES.md` needs no new entry** (task 7.4). The tombstone mechanism IS the fix
   for the destructive half of R-018, and what remains (a drift shipped through an
   intermediate build) is recorded as unmitigated in `RISK_REGISTER.md` rather than as an
   open defect — there is no code change that can reconstruct content meaning a build does
   not have.
