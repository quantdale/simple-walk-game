## 1. Fix the tamper self-test

- [ ] 1.1 In `tools/simulation/Commands.cs` `IntegritySelfTest`, determine the
  actual payload property name present in the parsed envelope and reassign
  through **that same name**. Never add a second key with a different casing.
- [ ] 1.2 Report the observed decode status and the expected status separately,
  so a failure message distinguishes "tamper was not detected" from "tamper was
  detected but classified as something other than a checksum mismatch".
- [ ] 1.3 Keep the tamper itself minimal and deterministic: flip one byte of the
  decoded payload and re-encode. Do not change what the codec sees.
- [ ] 1.4 Prove the fix: run `validate --selftest` against a save written with
  a **PascalCase** envelope variant in a scratch fixture and confirm the
  self-test still reports a checksum mismatch rather than a malformed envelope.
  Then revert the scratch fixture.
- [ ] 1.5 Confirm the normal camelCase path is unchanged: `validate --selftest`
  on a real save still prints `integrity-selftest PASS` and exits 0.

## 2. Exhaustive boot classification and exit codes

- [ ] 2.1 In `Cli.Boot`, give every `StartStatus` value an explicit arm with its
  own exit code and stderr message. Remove the reliance on the `default` arm for
  a value that has a documented meaning.
- [ ] 2.2 `StartStatus.StateInvalid` must map to exit **2** (domain/validation
  failure) with a message naming the invariant violations. This branch is only
  reachable **after** `recover-save-from-backup-when-primary-is-missing` has
  landed; confirm that has happened before starting.
- [ ] 2.3 Add a test asserting the status→exit-code mapping is exhaustive, so a
  future `StartStatus` value without a mapping fails the suite.

## 3. Uniform verb failure reporting

- [ ] 3.1 In `tools/simulation/Scenarios.cs`, replace the
  `InvalidOperationException` throws used for expected scenario failures with a
  documented exit code plus a stderr message naming the failing scenario.
- [ ] 3.2 Keep genuine unexpected errors (a real invariant violation in the tool
  itself) throwing, so `Program.cs`'s catch-all still means "unexpected error"
  as documented.
- [ ] 3.3 Review every verb for the same class of issue: an expected condition
  that surfaces as exit 4 or as an unhandled exception.

## 4. Argument validation

- [ ] 4.1 Check that the user-named `--save` directory's **parent** exists
  before the first read, so a non-existent path is a usage error (exit 1) rather
  than "no save found" (exit 3).
- [ ] 4.2 **Do not** apply that check to the `new` verb, which legitimately
  creates the save directory, and confirm `new` still works against a
  not-yet-existing directory.
- [ ] 4.3 Confirm `scenarios` and `bench`, which create their own work
  subdirectories, are unaffected.

## 5. Documentation

- [ ] 5.1 Confirm the usage text's exit-code line
  (`0 ok | 1 usage | 2 domain/validation failure | 3 save
  missing/unreadable | 4 unexpected error`) is accurate after the change, and
  add a per-verb note where a verb's failure set is narrower.
- [ ] 5.2 Update `docs/TESTING_AND_RELEASE.md` §14 and §16 if they restate the
  CLI's exit-code contract, so the tool and the release gate document agree.

## 6. Verification

- [ ] 6.1 `dotnet build SimpleWalkGame.sln` — zero errors, no new warnings.
- [ ] 6.2 `dotnet test` — all suites green.
- [ ] 6.3 `scripts/verify-ci-steps.sh` — every step green, in particular the
  simulation smoke step running `validate --selftest`.
- [ ] 6.4 Manually confirm the exit codes end to end: a missing save → 3; a
  corrupt save with no backup → 3; a save failing state validation → 2; a
  tampered payload → 2; an unknown verb → 1; a missing required flag → 1.
- [ ] 6.5 `scripts/verify-architecture.sh` — 11 passed, 0 failed.
- [ ] 6.6 `tests/guards/run-guard-tests.sh` — green.
- [ ] 6.7 `openspec validate harden-simulation-cli-integrity-selftest --strict`
  passes.
- [ ] 6.8 `git status` clean apart from intended files; writer lease released.
