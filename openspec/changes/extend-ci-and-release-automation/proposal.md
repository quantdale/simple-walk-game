# Extend CI and release automation for the gaps the local gate cannot catch

## Why

The repository's release gate is unusually good and unusually *incomplete*, and
the incompleteness is now the binding constraint. Since hosted CI has executed
zero steps since 2026-08-30 (KI-001), the local gate is the only CI evidence
there is, and the following classes are simply not covered by anything:

**1. No coverage measurement or gate.** `coverlet.collector` 6.0.2 is
referenced by all three test projects, but no test run collects coverage and
`ci.yml` has no `--collect:"XPlat Code Coverage"`. The package is dead weight
in the dependency graph, and the project has no visibility into which behaviour
is actually exercised. This is the tool that would have made the
`ReorderQueue`/`DequeueProject` gap (zero test references) visible at a glance
instead of requiring a targeted grep.

**2. No dependency vulnerability or licence check.** There is no
`dotnet list package --vulnerable`, no SBOM generation, and no Dependabot
configuration. The runtime surface is deliberately tiny — one first-party
package, `System.Text.Json` 8.0.5 — which is a real strength, but it is
currently a fact observed once by hand rather than an enforced property. The
test-side packages are four third-party entries with no automated update path.

**3. No release workflow at all.** There is exactly one workflow,
`.github/workflows/ci.yml`, and it runs on push and pull request. There is no
tag-triggered release, no version stamping, no artifact publication, and no
`CHANGELOG`. `TESTING_AND_RELEASE.md` §18 requires a release evidence package
containing "commit SHA; build/version; test suite results; ... known issues by
severity; privacy review; accessibility verification; clean-clone/build result;
remaining unverified claims" — and today assembling that package is a manual
act with no tooling support, which is precisely the kind of manual step that
gets skipped under release pressure.

**4. No static analysis beyond the compiler.** `ci.yml` runs
`dotnet build` "with warnings visible", but warnings are not errors (fixed
separately by the toolchain change) and there is no analyzer package, no
`dotnet format` gate (a deliberate, recorded decision in `TESTING_AND_RELEASE.md`
§16), and no security analyzer. For a repository whose only external input is a
JSON save file, a deserialization-focused analyzer would be proportionate.

**5. `verify-ci-steps.sh` must be run by hand.** The script that reproduces the
entire CI gate locally exists precisely because hosted CI is blocked, but
nothing runs it automatically and its own header documents that it is manual
usage. A developer can therefore commit without ever executing the release gate,
and the D-049 discipline ("the workflow file is the single source of truth")
is opt-in rather than automatic.

## What Changes

- Coverage is collected on every test run, published as a CI artifact, and a
  **deliberately low** regression floor is added to catch gross loss of
  exercised behaviour. The floor is not a quality target.
- Dependency vulnerability checking runs in CI and fails the build on a known
  advisory, with a documented, time-boxed exception path.
- A minimal release workflow exists: tag-triggered, producing a version-stamped
  artifact and an assembled release evidence package from the documents the
  repository already maintains.
- A release evidence checklist is a committed, checkable document rather than a
  prose section, so "is this a release candidate" is answerable mechanically.
- `verify-ci-steps.sh` is wired into the local preflight path and into
  `ONBOARDING.md`'s baseline verification, so the gate is executed by default
  rather than by recall.
- A proportionate deserialization/security analyzer is evaluated and adopted or
  explicitly declined with a recorded reason.

**Non-goals:** no `dotnet format` gate (the existing recorded decision stands),
no introduction of new third-party runtime dependencies, no multi-targeting, no
containerisation, and no attempt to make the blocked hosted CI work.

## Impact

**Evidence from the audit**

- `tests/*/*.csproj` — `coverlet.collector` 6.0.2 referenced in all three test
  projects; never invoked.
- `.github/workflows/ci.yml` — the only workflow. Steps: identity guard, build,
  test, architecture checks, publish smoke, simulation smoke, exactly-once
  acceptance, long-horizon simulation, hook installer, guard proof suite, pacing
  evidence, save-growth determinism. No coverage, no dependency check, no
  artifact upload, no release job.
- `ls .github/workflows/` → `ci.yml` only. No `dependabot.yml`.
- `scripts/verify-ci-steps.sh` — header documents manual usage
  (`scripts/verify-ci-steps.sh [name-substring]`); nothing invokes it.
- `docs/TESTING_AND_RELEASE.md` §18 — the release evidence package contents,
  with no tooling.
- `docs/KNOWN_ISSUES.md` KI-001 — hosted CI blocked, making the local gate
  authoritative.
- `docs/KNOWN_ISSUES.md` KI-008 — a tooling-hygiene item about the guard/lease
  scripts breaking under short-lived shells; related to wiring `verify-ci-steps.sh`
  into an automated path.
- `docs/PRIVACY_REVIEW.md` §2 — the single runtime dependency, currently
  verified by hand.

**Affected files**

- `.github/workflows/ci.yml` — coverage collection, dependency check.
- New: `.github/workflows/release.yml`, `.github/dependabot.yml`.
- `ONBOARDING.md` — baseline verification includes the CI-step reproduction.
- `docs/TESTING_AND_RELEASE.md` — release evidence becomes a checklist.
- `docs/DECISIONS.md` — the analyzer adopt/decline decision and the coverage
  floor's rationale.

**Important constraint**

Every gate added must live inside `.github/workflows/ci.yml` as a `run:` step
for `scripts/verify-ci-steps.sh` to execute it locally. This is the D-049
property and the reason the two prior campaigns' gates work. A gate in a script
that CI does not call is not a gate — stated explicitly in `verify-ci-steps.sh`'s
own header. Any new gate committed without a corresponding workflow step is
incomplete.

**Dependencies**

- `pin-build-toolchain-and-centralize-packages` should land first: coverage
  thresholds, vulnerability checks and analyzer adoption all interact with the
  warnings policy, and doing both at once confounds the result.
- Otherwise independent.
