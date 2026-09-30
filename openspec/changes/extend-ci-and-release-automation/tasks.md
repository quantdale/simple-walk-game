## 1. Coverage collection and a low floor

- [ ] 1.1 Add `--collect:"XPlat Code Coverage"` (and the matching `--results-directory`)
  to the test step in `.github/workflows/ci.yml`.
- [ ] 1.2 Merge the three per-project coverage files into one report and upload
  it as an artifact. The artifact step is hosted-only; confirm
  `scripts/verify-ci-steps.sh` reports it as an explicit SKIP locally rather
  than silently dropping it.
- [ ] 1.3 Add a **low** regression floor and record the measured current value
  and the chosen floor in `docs/DECISIONS.md`, with the explicit rationale that
  the floor detects gross loss of exercised behaviour and is **not** a quality
  target.
- [ ] 1.4 Confirm coverage collection requires **no** dependency change:
  `coverlet.collector` 6.0.2 is already referenced in all three test projects.
- [ ] 1.5 Report per-project coverage in the CI log so a drop is attributable.

## 2. Dependency vulnerability checking

- [ ] 2.1 Add a `run:` step in `.github/workflows/ci.yml` executing
  `dotnet list package --vulnerable --include-transitive` and failing on a
  known advisory.
- [ ] 2.2 Add a committed, dated exception file so a triaged advisory — most
  plausibly a build-time-only transitive — is a visible reviewable decision
  rather than a suppression.
- [ ] 2.3 Confirm the gate runs today and is clean: the only runtime dependency
  is `System.Text.Json` 8.0.5.
- [ ] 2.4 Prove the gate fires: introduce a package with a known advisory in a
  scratch edit, confirm the step fails, then revert.
- [ ] 2.5 Add `.github/dependabot.yml` for the NuGet ecosystem, so version
  updates arrive as reviewable pull requests. Note in `docs/KNOWN_ISSUES.md`
  that Dependabot cannot open pull requests while hosted CI is blocked
  (KI-001), and that the vulnerability gate is the compensating control.

## 3. Release workflow and assembled evidence package

- [ ] 3.1 Add `.github/workflows/release.yml`, triggered on version tags.
- [ ] 3.2 The workflow publishes a version-stamped CLI artifact.
- [ ] 3.3 The workflow **assembles** the release evidence package from committed
  sources — never from re-typed values. The package must include: commit SHA,
  build/version, test suite results, migration fixture status, the
  known-issues register, the privacy review, and the outstanding unverified
  claims.
- [ ] 3.4 The package states explicitly that assembling evidence does **not**
  assert release readiness, and must carry the High open items (KI-001 hosted CI
  blocked, KI-002 no Unity editor) which block release qualification by the
  repository's own rule.
- [ ] 3.5 Convert the `TESTING_AND_RELEASE.md` §20 release qualification
  checklist into a committed, per-item checkable document that the workflow
  verifies, keeping it as the source of truth for *what must be true* rather
  than a person remembering it.
- [ ] 3.6 Confirm the new workflow is tag-triggered and does not run on ordinary
  pushes, and that no existing CI behaviour changes.

## 4. Make the local gate the default path

- [ ] 4.1 Add `scripts/verify-ci-steps.sh` to `ONBOARDING.md`'s baseline
  verification section.
- [ ] 4.2 Add it to `docs/AGENT_EXECUTION_GUIDE.md`'s preflight, so an agent
  session executes the release gate by default rather than by recall.
- [ ] 4.3 Address KI-008 in the same guidance: run the long gate detached, and
  export a stable `SWG_SESSION_ID` for the session so acquire/release match
  across processes. Both are recorded in KI-008 as working remedies.
- [ ] 4.4 Add a CHANGELOG or an equivalent release-note source the workflow can
  consume, so release notes are not re-typed per release.

## 5. Static analyzer: evaluate, then adopt or decline

- [ ] 5.1 Run a candidate deserialization/security analyzer against `src/` and
  `tools/` and record what it actually reports.
- [ ] 5.2 Adopt it only if it surfaces something actionable for this repository's
  surface (a save file deserialized with `System.Text.Json`, one first-party
  dependency, no network, no auth). Record the findings either way.
- [ ] 5.3 If declined, record the reason in `docs/DECISIONS.md`. Declining with
  a recorded rationale is an acceptable outcome — the value was in the
  evaluation, and adopting a tool that only generates noise is a net negative.
- [ ] 5.4 Do **not** adopt a `dotnet format` gate. `TESTING_AND_RELEASE.md` §16
  records that as a considered decision; this change does not reopen it.

## 6. Constraint: every gate must live in the workflow file

- [ ] 6.1 Confirm every gate added by this change is a `run:` step inside
  `.github/workflows/ci.yml`. A gate in a script CI does not call is not a gate
  — this is the D-049 property and is stated in `verify-ci-steps.sh`'s own
  header.
- [ ] 6.2 Confirm each new hosted-only step (artifact upload) reports an
  explicit SKIP under `verify-ci-steps.sh` rather than being silently skipped.
- [ ] 6.3 Confirm the parser-drift guard in `verify-ci-steps.sh` still passes
  after the workflow grows, and that the hollow-pass check still guards against
  silently executing empty step bodies.

## 7. Privacy re-review

- [ ] 7.1 Re-run `docs/PRIVACY_REVIEW.md`'s mechanical commands, confirming
  still zero network API usage in `src/` and `tools/`.
- [ ] 7.2 Confirm any newly added tooling is **build-time only** and does not
  appear in a published runtime assembly. The publish smoke step in `ci.yml`
  must still produce a runnable artifact with no new runtime dependency.

## 8. Verification

- [ ] 8.1 `dotnet build SimpleWalkGame.sln` — zero errors, no new warnings.
- [ ] 8.2 `dotnet test` — all suites green.
- [ ] 8.3 `scripts/verify-ci-steps.sh` — every locally-executable step passes,
  every hosted-only step reports an explicit SKIP, and the summary line is
  reported in the change description.
- [ ] 8.4 `scripts/verify-architecture.sh` — 11 passed, 0 failed.
- [ ] 8.5 `scripts/verify-pacing-evidence.sh` — seven reproduced, 0 drifted.
- [ ] 8.6 `scripts/verify-save-growth.sh` — growth series reproduces.
- [ ] 8.7 `tests/guards/run-guard-tests.sh` — green.
- [ ] 8.8 `openspec validate extend-ci-and-release-automation --strict` passes.
- [ ] 8.9 `git status` clean apart from intended files; writer lease released.
