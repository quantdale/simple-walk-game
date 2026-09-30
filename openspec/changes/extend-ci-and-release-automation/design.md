## Context

See `proposal.md` (Why) for the evidence.

What constrains the design:

- **The D-049 property is the load-bearing constraint.** `scripts/verify-ci-steps.sh`
  parses `.github/workflows/ci.yml`, extracts every `run:` step body verbatim,
  and executes it under GitHub Actions' own default shell. Its header states the
  consequence plainly: "The workflow file is the single source of truth: a step
  added to ci.yml is executed here without this script changing at all", and it
  hard-fails on parser drift. So any new gate must be a `run:` step in the
  workflow, or it does not exist as far as the local gate — which, with hosted
  CI blocked since 2026-08-30, is the gate that actually runs.
- The script already asserts local prerequisites for hosted-only steps
  (`actions/checkout` → git worktree at HEAD; `actions/setup-dotnet` → dotnet
  present) and explicitly SKIPs them. Any new *hosted action* step
  (`upload-artifact`, `actions/dependabot`) is therefore reported as a SKIP
  locally, not silently dropped — that behaviour is already implemented and
  tested by construction.
- `coverlet.collector` is already referenced in all three test projects, so
  coverage collection needs **no dependency change**. It is a test-invocation
  change only. That is a favourable finding: the tool was paid for and unused.
- The repository's deliberate `dotnet format` decision (`TESTING_AND_RELEASE.md`
  §16) is a considered choice with a recorded rationale, and must not be
  quietly reversed by adding "static checks" in its place.
- `docs/KNOWN_ISSUES.md` KI-008 records that long guard/proof suites break on
  this harness's short-lived shell model (stranded writer leases, session
  identity). `verify-ci-steps.sh` runs the full gate including the guard proof
  suite, so automating its invocation inherits that fragility. This is a real
  constraint, not a hypothetical.
- The repository has **no third-party runtime dependency** beyond
  `System.Text.Json` 8.0.5, and `PRIVACY_REVIEW.md` §2 asserts zero
  transitive third-party runtime packages. A vulnerability gate protects a
  surface that is already tiny — which is an argument that the gate is cheap
  insurance, not that it is urgent.

## Goals / Non-Goals

**Goals**

- Cover the automation classes that nothing currently covers: coverage,
  dependency risk, release packaging, and default execution of the local gate.
- Make "is this a release candidate" answerable from committed documents rather
  than from a release manager's memory.
- Preserve the D-049 property for every gate added.

**Non-Goals**

- Reversing the `dotnet format` decision.
- New third-party **runtime** dependencies. Analyzers and coverage tooling are
  build-time only and do not affect the shipped assembly, but that distinction
  must be preserved and re-verified by `PRIVACY_REVIEW.md`.
- Making hosted CI work. KI-001 is an account-billing matter outside this
  repository.
- Adding a coverage target that encodes a quality opinion. The floor is a
  loss detector, not a goal.

## Decisions

### 1. Coverage is collected, published, and floored low

Add `--collect:"XPlat Code Coverage"` to the test step, upload the result as an
artifact, and add a **floor** well below current coverage.

The floor is deliberately low. Its purpose is to catch a gross regression — a
suite accidentally reduced to a handful of tests, a project whose tests stop
running — not to incentivise test-writing against a number. A high target on a
295-test suite with a deliberately layered architecture would create pressure to
write assertion-light tests, which is the exact failure mode the current
well-named, behaviour-asserting suite avoids.

*Alternative considered:* publish coverage with no floor. Rejected: an
unmonitored metric decays silently, and a floor is what makes someone look.

*Alternative considered:* a high target. Rejected as actively harmful here; the
reasoning is recorded in `docs/DECISIONS.md` so a future contributor can see it
was considered and why it was declined.

### 2. Dependency checking with an explicit, time-boxed exception path

Add a `dotnet list package --vulnerable --include-transitive` step that fails
the build on a known advisory. Provide a documented exception file so a
transitive advisory in a build-time-only package can be triaged without either
silently ignoring the gate or stalling the build indefinitely.

The exception file is committed, so every accepted advisory is a visible,
reviewable, dated decision rather than a suppressed warning.

*Alternative considered:* Dependabot alone. Rejected as insufficient: Dependabot
opens pull requests but does not fail a build, and with hosted CI blocked it
cannot even open one reliably today. Both are adopted together.

### 3. A release workflow, and an evidence package that is assembled not remembered

A tag-triggered workflow that publishes a version-stamped CLI artifact and
assembles a release evidence package from documents the repository already
maintains: the known-issues register, the privacy review, the test counts, the
migration status, and the outstanding unverified claims.

The critical design point: the evidence package must be **derived from committed
sources**, not re-typed. The repository's own `TESTING_AND_RELEASE.md` §19
states "Documentation drift is a quality defect", and the C4 campaign
(`docs/evidence/ci-local/CAMPAIGN-2026-09-29.md`) exists because re-typing
steps by hand is how drift happens. An assembled package cannot drift from its
sources.

*Alternative considered:* a hand-written release checklist in Markdown. Kept, but
as the *input* to the workflow rather than the output — it is the source of
truth for what must be true, and the workflow verifies each item rather than a
person remembering it.

### 4. Wire the local gate into the default path

Add `scripts/verify-ci-steps.sh` to `ONBOARDING.md`'s baseline verification and
to `docs/AGENT_EXECUTION_GUIDE.md`'s preflight, so executing the release gate
is the default rather than a recall step.

KI-008's lease-fragility finding must be addressed in the same change: the
guidance must say to run the long gate detached and to export a stable
`SWG_SESSION_ID` for the session, both of which are recorded in KI-008 as
working remedies. Automating a gate that strands writer leases would make the
tooling worse, not better.

*Alternative considered:* a git hook that runs the full gate on commit.
Rejected: the gate takes minutes, hosted CI is already the intended place for
it, and a minutes-long pre-commit hook is a well-known way to make developers
bypass hooks entirely.

### 5. Adopting a static analyzer is evaluated, not assumed

For a repository whose only untrusted input is a save file deserialized with
`System.Text.Json`, the proportionate question is whether a deserialization or
security analyzer surfaces anything real. The task list requires running a
candidate analyzer and recording the findings.

If it surfaces nothing actionable, the recorded decision is to decline it —
which is a legitimate outcome and better than adopting a tool that only
generates noise. This change therefore permits "evaluate and decline with a
recorded reason" as a success, because the value was in the evaluation.

### 6. Gate steps must be `run:` steps, and the script's SKIP path is used honestly

Every new gate is added as a `run:` step. Hosted-only actions (`upload-artifact`,
Dependabot's own scheduling) are reported as SKIPs by the existing
`verify-ci-steps.sh` logic. The task list requires confirming that each new
hosted-only step reports SKIP locally rather than being silently skipped, since
a silent skip would mean the gate does not run in the environment that matters
most right now.

## Risks / Trade-offs

- **[More CI steps, more time, and hosted CI cannot currently verify any of
  them]**
  → Each step must be runnable under `verify-ci-steps.sh`, which is a hard
  requirement, not an aspiration. If a step cannot run locally, it belongs in
  the hosted-only list with an explicit local prerequisite assertion, following
  the pattern already used for `setup-dotnet`.

- **[Coverage floors can be gamed or can incentivise bad tests]**
  → Mitigated by a deliberately low floor and by recording the reasoning. The
  enumeration guard and behaviour-asserting suites from the sibling test change
  are the real protection; coverage is a loss detector.

- **[Automating the local gate increases the chance of stranding writer
  leases]**
  → KI-008 is explicitly addressed, with the two recorded remedies (detached
  execution, stable `SWG_SESSION_ID`) written into the guidance. The alternative
  — leaving the gate manual — is what allows a commit to skip the release gate
  entirely, which is worse.

- **[A release workflow could imply release-readiness that does not exist]**
  → The evidence package must include the outstanding unverified claims, and KI-001
  (hosted CI blocked) and KI-002 (no Unity editor) are severity-High open items
  that block release qualification by the repository's own rule. The workflow
  assembles evidence; it does not assert readiness. That distinction must be
  explicit in the package itself.

- **[A vulnerability gate on a build-time package could block releases on an
  unfixable transitive advisory]**
  → The committed, dated exception file is the escape hatch, and it is visible
  rather than silent.

## Migration Plan

No product migration. Workflow-only plus documentation changes.

Rollout: add each gate individually and run `verify-ci-steps.sh` after each, so
a broken step is caught in the change that introduced it rather than at the end.

## Open Questions

None. The coverage floor value, the analyzer decision, and the exception-file
policy are all explicit task items with recorded-rationale requirements; none
changes the shape of this change.
