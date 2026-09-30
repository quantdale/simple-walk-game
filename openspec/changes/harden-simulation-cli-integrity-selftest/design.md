## Context

See `proposal.md` (Why) for the code references.

What constrains the design:

- `tools/simulation` is the release-evidence tool, not a product surface.
  `.github/workflows/ci.yml` runs four of its verbs as gates, and
  `scripts/verify-ci-steps.sh` executes those steps verbatim. A false failure
  here blocks CI; a false success here lets a broken save ship. Both directions
  matter, which is why the self-test's *classification* of its own result is
  part of the fix rather than a nicety.
- `Program.cs` is a top-level-statement program with a single catch-all mapping
  to exit 4. That is a reasonable shape; the issue is that expected conditions
  reach it. Uniform per-verb exit codes keep the catch-all for genuine
  unexpected errors, which is its actual purpose.
- `SaveJson.Options` sets `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`,
  and every producer of the save envelope uses it. So the self-test's PascalCase
  fallback is currently dead. The fix is about robustness of a *diagnostic*, and
  must not change what a correct self-test does today.
- `StartStatus.StateInvalid` becomes reachable via
  `recover-save-from-backup-when-primary-is-missing`. Applying that change first
  is what makes this change's exit-code fix verifiable rather than speculative.
- The CLI is documented in its own usage text and in
  `docs/TESTING_AND_RELEASE.md` §14/§16; the exit-code contract appears in
  both, so both need the same correction.

## Goals / Non-Goals

**Goals**

- Make the tamper self-test correct under any key casing and never produce a
  misleading classification.
- Make every verb's failure mode a documented exit code rather than a thrown
  exception.
- Make the exit-code contract accurate and stated once.

**Non-Goals**

- No change to `SaveCodec`, `SaveJson`, the envelope format, or the domain.
- No new verb, and no removal of a documented one.
- No change to the CI workflow's step bodies beyond what the corrected exit
  codes require (in practice: none).

## Decisions

### 1. Read the key, then write the same key

`IntegritySelfTest` should determine the actual property name present in the
parsed envelope and reassign through that same name, rather than
unconditionally writing the camelCase spelling. This makes the
`?? root?["PayloadBase64"]` fallback honest: it is a real fallback, and the
write follows whatever the read found.

*Alternative considered:* delete the fallback and keep only camelCase. Rejected:
it makes the tool silently wrong rather than loudly wrong if the casing ever
changes, and the fallback costs one line.

*Alternative considered:* normalise the key to camelCase on both paths
(rename the property). Rejected: mutating the key changes the document
structure, and re-serialising with a renamed property is a second way to get
this subtly wrong.

### 2. Distinguish "tamper detected" from "tamper misclassified"

Today the self-test returns a single bool: `codec.Decode(tampered).Status ==
CodecStatus.ChecksumMismatch`. If the codec ever returned
`MalformedEnvelope` — as the duplicate-key path would — the self-test returns
false and the CLI prints "integrity-selftest FAIL", which reads as "the
integrity check is broken" when in fact the tamper harness was broken.

The self-test should therefore report the observed status and the expected
status separately, so a failure message says which one was wrong. This is the
difference between a 5-minute diagnosis and a 5-hour one during release triage.

### 3. Exhaustive boot classification in `Cli.Boot`

`Boot` already has a `switch` over `StartStatus` with a `default` arm. The
change makes every `StartStatus` value an explicit arm with its own exit code
and message, so adding a status value later forces a decision here rather than
silently falling into the default. A `StartStatus` value with no arm is a
compile-time-visible omission only if the switch is exhaustive-by-type; where it
is not, the guard is a test.

*Alternative considered:* map state-invalid to exit 3 and document it.
Rejected: exit 2 is already documented as "domain/validation failure", which is
precisely what it is, and a release reviewer triaging a corrupt save should not
have to read the tool's source to learn that exit 3 means something else.

### 4. Expected conditions return codes; only unexpected errors throw

`Scenarios.Run` throws `InvalidOperationException` for "scenario 1 cold boot
failed" and similar. Those are expected conditions under a failing gate, not
crashes. They should return a documented exit code with the failure on stderr.

*Alternative considered:* leave the throws, since the catch-all already
produces a non-zero exit. Rejected: exit 4 is documented as "unexpected error",
so a scenario regression is currently reported as an internal fault, and the
gate output misattributes the cause.

### 5. State the exit-code contract once, in the usage text, and mirror it

`Cli.Usage` already prints
`exit codes: 0 ok | 1 usage | 2 domain/validation failure | 3 save missing/unreadable | 4 unexpected error`.
That line is the contract. It is correct; the implementation was not matching it
in two places. The change corrects the implementation and adds a per-verb note
where a verb has a narrower set.

### 6. Check `--save` up front

A save directory that does not exist is an argument error, not "no save found".
Checking it before the first read turns a confusing exit 3 into a clear exit 1.
Small, and it removes a triage confusion that would otherwise recur in CI logs.

## Risks / Trade-offs

- **[Correcting exit codes could change CI behaviour]**
  → Only in the previously-unreachable state-invalid case, and only from 3 to 2.
  Both are non-zero, so no gate that currently fails starts passing. This must
  be verified by running the affected gate locally before merge — the change's
  own verification task requires it.

- **[Making `Boot` exhaustive could break on a future status value]**
  → That is the point. A new value should force a decision about its exit code
  rather than inherit the default silently.

- **[Checking `--save` up front could break a legitimate workflow]** that
  expects the verb to create the directory. `AtomicFileSaveStore`'s constructor
  creates the directory today; the CLI's own `Scenarios` and `Bench` verbs
  create their own work directories. The check must apply only to the save
  directory the user names, and must not break `new`, which legitimately
  creates it.

*That last point is a real regression risk and is called out explicitly in the
task list.*

## Migration Plan

None. Tool-only change; no product code, no schema, no evidence.

Rollback: a plain revert.

## Open Questions

None.
