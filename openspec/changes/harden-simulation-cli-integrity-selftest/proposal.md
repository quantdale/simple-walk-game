# Harden the simulation CLI's integrity self-test and exit-code contract

## Why

The simulation CLI is the repository's release-evidence tool, and the CI gate
depends on its exit codes. Two defects make its self-test weaker than it
appears.

**1. The tamper self-test only works for one key casing, and fails wrong if that
ever changes.** `Cli.IntegritySelfTest` reads the payload with
`root?["payloadBase64"] ?? root?["PayloadBase64"]` — a defensive fallback for
both casings — but then writes the tampered value back with an
**unconditional** camelCase key:

```csharp
payload[0] ^= 0xFF;
root!["payloadBase64"] = JsonValue.Create(Convert.ToBase64String(payload));
```

Today the codec always writes camelCase (`SaveJson` sets
`PropertyNamingPolicy = JsonNamingPolicy.CamelCase`), so the fallback branch is
dead and the write is correct. But if the envelope were ever written
PascalCase, the fallback would *read* the PascalCase key and then *add* a
camelCase duplicate, producing a JSON document with two `payloadBase64` keys.
`SaveCodec.Decode` deserializes that with STJ, which rejects duplicate property
names, so it would return `MalformedEnvelope` instead of the
`ChecksumMismatch` the self-test requires. The self-test would report
`integrity-selftest FAIL` and `validate --selftest` would exit non-zero — a
false release-blocker caused by the very tool meant to prove integrity.

**2. A genuine integrity failure is reported as a missing file.** `Cli.Boot`
ends with:

```csharp
return new BootOutcome(start.Status == StartStatus.StateInvalid ? 2 : 3, start);
```

`StartStatus.StateInvalid` is never produced — `Continue()` folds a state
validation failure into `SaveUnreadable`. The branch is unreachable, so an
invariant-violating save exits **3** ("save missing/unreadable") rather than
**2** ("domain/validation failure"). `TESTING_AND_RELEASE.md` §16 lists the
`validate` step as a release gate; an exit code that does not distinguish
"corrupt data" from "no data" makes that gate's output misleading during triage.

**3. One verb reports success where it should fail.** `Cli.Validate` returns

```csharp
return violations.Count > 0 || !selftestOk ? 2 : 0;
```

which is correct, but `Scenarios.Run` throws
`InvalidOperationException` on a scenario failure while other verbs return
status codes, and `Program.cs` maps any unhandled exception to exit 4. So a
scenario regression surfaces as "unexpected error" rather than as a validation
failure. This is minor but it is the same class of problem: the tool's exit
codes are the gate's contract and they should be uniform.

**4. The `validate` verb can be pointed at a save directory that does not
exist** and reports "no save found" (exit 3) rather than an argument error,
because `--save` is required but the directory is never checked. Low impact,
but it is the same "one error class, one exit code" problem.

## What Changes

- The tamper self-test reads and writes the **same key** it found, so it is
  correct for any casing and can never produce a duplicate-key document.
- The self-test asserts the specific expected outcome (`ChecksumMismatch`) and
  distinguishes "tamper was detected" from "tamper produced a different
  classification", because those mean very different things.
- The CLI's boot outcome classification is made exhaustive, and
  `StartStatus.StateInvalid` is actually produced, so a state-invariant failure
  exits 2 rather than 3.
- Verb failures are uniform: every verb returns a documented exit code rather
  than throwing for an expected condition.
- The CLI's documented exit-code contract is stated once and is accurate.

**Non-goals:** no change to the save format, the codec, or the domain. This is
a change to the diagnostic tool's own reliability, which matters because the
tool is release evidence.

## Impact

**Evidence from the audit**

- `tools/simulation/Commands.cs` — `IntegritySelfTest` (the read/write casing
  mismatch) and `Boot` (the unreachable `StateInvalid` branch).
- `src/WalkGame.Infrastructure/Persistence/SaveJson.cs` —
  `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` (why the fallback is
  currently dead).
- `tools/simulation/Program.cs` — top-level catch maps any exception to
  exit 4.
- `tools/simulation/Scenarios.cs` — throws `InvalidOperationException` for
  expected scenario failures.
- `docs/TESTING_AND_RELEASE.md` §16 — the `validate` step is a release gate.
- `.github/workflows/ci.yml` — the simulation smoke step runs
  `validate --selftest`; a false failure here blocks CI.
- The unreachable-`StateInvalid` half of this is fixed in the same place by
  `recover-save-from-backup-when-primary-is-missing`; this change must not
  conflict with it. **Apply the backup-recovery change first**, then this one
  verifies the exit code end to end.

**Affected files**

- `tools/simulation/Commands.cs` — self-test, boot classification, exit codes.
- `tools/simulation/Scenarios.cs` — uniform failure reporting.
- `tools/simulation/Program.cs` — top-level exception handling, if needed.
- `docs/TESTING_AND_RELEASE.md` — the exit-code contract.

**Affected gates**

`.github/workflows/ci.yml`'s simulation smoke step, and therefore
`scripts/verify-ci-steps.sh`.

**Dependencies**

- Apply **after** `recover-save-from-backup-when-primary-is-missing`, which
  produces `StartStatus.StateInvalid` and makes the `Boot` branch reachable.
