## 1. Fix the tamper self-test

- [x] 1.1 In `tools/simulation/Commands.cs` `IntegritySelfTest`, determine the
  actual payload property name present in the parsed envelope and reassign
  through **that same name**. Never add a second key with a different casing.
- [x] 1.2 Report the observed decode status and the expected status separately,
  so a failure message distinguishes "tamper was not detected" from "tamper was
  detected but classified as something other than a checksum mismatch".
- [x] 1.3 Keep the tamper itself minimal and deterministic: flip one byte of the
  decoded payload and re-encode. Do not change what the codec sees.
- [x] 1.4 Prove the fix: run `validate --selftest` against a save written with
  a **PascalCase** envelope variant in a scratch fixture and confirm the
  self-test still reports a checksum mismatch rather than a malformed envelope.
  Then revert the scratch fixture.
- [x] 1.5 Confirm the normal camelCase path is unchanged: `validate --selftest`
  on a real save still prints `integrity-selftest PASS` and exits 0.

## 2. Exhaustive boot classification and exit codes

- [x] 2.1 In `Cli.Boot`, give every `StartStatus` value an explicit arm with its
  own exit code and stderr message. Remove the reliance on the `default` arm for
  a value that has a documented meaning.
- [x] 2.2 `StartStatus.StateInvalid` must map to exit **2** (domain/validation
  failure) with a message naming the invariant violations. This branch is only
  reachable **after** `recover-save-from-backup-when-primary-is-missing` has
  landed; confirm that has happened before starting.
- [x] 2.3 Add a test asserting the status→exit-code mapping is exhaustive, so a
  future `StartStatus` value without a mapping fails the suite.

## 3. Uniform verb failure reporting

- [x] 3.1 In `tools/simulation/Scenarios.cs`, replace the
  `InvalidOperationException` throws used for expected scenario failures with a
  documented exit code plus a stderr message naming the failing scenario.
- [x] 3.2 Keep genuine unexpected errors (a real invariant violation in the tool
  itself) throwing, so `Program.cs`'s catch-all still means "unexpected error"
  as documented.
- [x] 3.3 Review every verb for the same class of issue: an expected condition
  that surfaces as exit 4 or as an unhandled exception.

## 4. Argument validation

- [x] 4.1 Check that the user-named `--save` directory's **parent** exists
  before the first read, so a non-existent path is a usage error (exit 1) rather
  than "no save found" (exit 3).
- [x] 4.2 **Do not** apply that check to the `new` verb, which legitimately
  creates the save directory, and confirm `new` still works against a
  not-yet-existing directory.
- [x] 4.3 Confirm `scenarios` and `bench`, which create their own work
  subdirectories, are unaffected.

## 5. Documentation

- [x] 5.1 Confirm the usage text's exit-code line
  (`0 ok | 1 usage | 2 domain/validation failure | 3 save
  missing/unreadable | 4 unexpected error`) is accurate after the change, and
  add a per-verb note where a verb's failure set is narrower.
- [x] 5.2 Update `docs/TESTING_AND_RELEASE.md` §14 and §16 if they restate the
  CLI's exit-code contract, so the tool and the release gate document agree.

## 6. Verification

- [x] 6.1 `dotnet build SimpleWalkGame.sln` — zero errors, no new warnings.
- [x] 6.2 `dotnet test` — all suites green.
- [x] 6.3 `scripts/verify-ci-steps.sh` — every step green, in particular the
  simulation smoke step running `validate --selftest`.
- [x] 6.4 Manually confirm the exit codes end to end: a missing save → 3; a
  corrupt save with no backup → 3; a save failing state validation → 2; a
  tampered payload → 2; an unknown verb → 1; a missing required flag → 1.
- [x] 6.5 `scripts/verify-architecture.sh` — 11 passed, 0 failed.
- [x] 6.6 `tests/guards/run-guard-tests.sh` — green.
- [x] 6.7 `openspec validate harden-simulation-cli-integrity-selftest --strict`
  passes.
- [ ] 6.8 `git status` clean apart from intended files; writer lease released.

---

## Implementation record (added during apply)

1. **The task list's "a tampered payload -> 2" is ambiguous, and BOTH readings are correct.**
   Verified end to end (task 6.4):

   | scenario | outcome | exit | contract |
   |---|---|---|---|
   | missing save | `NoSaveFound` | **3** | save missing/unreadable |
   | corrupt save, no backup | `SaveUnreadable` (malformed envelope) | **3** | save missing/unreadable |
   | payload tampered, checksum left stale | `SaveUnreadable` (integrity check failed) | **3** | save missing/unreadable |
   | content tampered, checksum RECOMPUTED (valid envelope, invariant violated) | `StateInvalid` | **2** | domain/validation failure |
   | unknown verb / missing flag / bad `--save` parent | usage | **1** | usage |
   | valid save, `dump` / `validate --selftest` | ok | **0** | ok |

   So a tampered payload yields **3** when it trips the checksum and **2** when it trips
   state validation. That is the documented contract, not a defect: the checksum is an
   integrity check against corruption, while `GameStateValidator` is the invariant gate.
   The two axes were deliberately left distinct.

2. **A save rewritten wholesale, with its checksum recomputed, passes validation. That is
   the DOCUMENTED threat model, not a new defect.** Observed while proving the exit codes:
   inflating the Vitality balance and recomputing `payloadSha256Base64` produces
   `violations=0`. `docs/PRIVACY_REVIEW.md` states the save is "plaintext JSON (base64
   payload, SHA-256 for *integrity only*)", and `KNOWN_ISSUES.md` KI-006 already records
   the save as plaintext at the application layer. Adding an authenticity mechanism
   (signature/MAC over the payload) is a security-design change the roadmap does not ask
   for and is not implied by this change, so it is recorded rather than silently absorbed.
   It belongs with the open KI-006 product/security decision.

3. **Task 1.4's PascalCase proof had to be done at the logic level, and doing so proved the
   defect exactly.** A PascalCase envelope cannot reach the self-test through the CLI
   today: `validate` boots first, boot cannot decode a PascalCase envelope, and the
   successful backup boot REWRITES `save.json` in camelCase before the self-test reads it.
   The hazard was therefore proven where it actually lives -- in the write-back keying:

   | implementation | tampered document's keys |
   |---|---|
   | OLD (always assigns `payloadBase64`) | `SchemaVersion, SavedAtUtc, PayloadSha256Base64, PayloadBase64, payloadBase64` — **five**, two payload properties differing only in case |
   | NEW (assigns through the observed key) | `SchemaVersion, SavedAtUtc, PayloadSha256Base64, PayloadBase64` — **four**, one payload property |

   The duplicate-key document is what produced `MalformedEnvelope` instead of
   `ChecksumMismatch`: a false release-blocker from the tool whose job is to prove
   integrity. Task 1.5 is separately confirmed -- `validate --selftest` on a real save
   prints `integrity-selftest PASS` and exits 0, unchanged.

4. **Task 2.1's exhaustive arms were added deliberately rather than left to `default`.**
   Every `StartStatus` now has an explicit arm, including the three that return early, so
   adding a member compiles and is *noticed* rather than silently falling into a generic
   message. `StartStatus.ContentIncompatible` (added by D-059) maps to exit 2 alongside
   `StateInvalid`: both are "this build cannot safely represent or validate this save".
