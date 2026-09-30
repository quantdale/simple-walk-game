## 1. Shared build properties

- [x] 1.1 Create `Directory.Build.props` carrying the universal settings once:
  `LangVersion` 9.0, `Nullable` enable, and the warnings policy. Do **not** set
  `TargetFramework` unconditionally there — the test projects and the CLI target
  `net9.0` and must not inherit `netstandard2.1`.
- [x] 1.2 Remove the duplicated `LangVersion` / `Nullable` / explicit
  `TreatWarningsAsErrors` entries from the three production csprojs and the
  three test csprojs.
- [x] 1.3 Decide how the production-layer `netstandard2.1` target stays
  greppable for the architecture gate (recommended: keep
  `<TargetFramework>netstandard2.1</TargetFramework>` local to each production
  csproj and keep only universal settings in the props file). Record the choice
  in `docs/DECISIONS.md`.
- [x] 1.4 `dotnet build SimpleWalkGame.sln` and `dotnet test` green after this
  step alone.

## 2. Central package management

- [x] 2.1 Create `Directory.Packages.props` with
  `ManagePackageVersionsCentrally` enabled and a `PackageVersion` entry for each
  of: `coverlet.collector` 6.0.2, `Microsoft.NET.Test.Sdk` 17.12.0, `xunit`
  2.9.2, `xunit.runner.visualstudio` 2.8.2, `System.Text.Json` 8.0.5.
  **Do not change any version.**
- [x] 2.2 Convert every `PackageReference` in the six projects to the
  version-less form, in one pass. Leave `System.Text.Json` at 8.0.5 — this
  change is about pinning, not upgrading.
- [x] 2.3 `dotnet restore` succeeds with no `NU1008` / `NETSDK1206`.
- [x] 2.4 `dotnet build` and `dotnet test` green.

## 3. Pin the SDK

- [x] 3.1 Create `global.json` pinning the SDK to a specific feature band with
  `rollForward: latestFeature` and `allowPrerelease: false`.
- [x] 3.2 Update `.github/workflows/ci.yml` so the SDK version is derived from
  `global.json` rather than a hardcoded `9.0.x`.
- [x] 3.3 Update `scripts/verify-ci-steps.sh`'s local SDK assertion to read the
  pinned version from `global.json` (or the workflow), keeping the check that the
  local SDK satisfies it. This coupling is easy to miss — do not skip it.
- [x] 3.4 `dotnet --version` reports the pinned band on this machine.

## 4. Fix the architecture gate for the effective property

- [x] 4.1 Update `scripts/verify-architecture.sh` so the netstandard2.1 rule
  reads the **effective** target framework of each production layer, rather than
  grepping a single csproj file. Prefer resolving the effective MSBuild property
  (`dotnet msbuild <proj> -getProperty:TargetFramework`); if a simpler grep of
  both files is used instead, document in the script that the rule is weaker.
- [x] 4.2 **Prove the updated rule still fails on a broken fixture**: move one
  production layer off `netstandard2.1` in a scratch copy, confirm the gate
  fails, then revert. Rule 6 of that script exists precisely because an
  unproven gate shipped broken once; do not repeat that.
- [x] 4.3 Confirm the other rules (reference direction, no engine namespace, no
  filesystem access, exec-bit/CRLF) still pass unchanged.
- [x] 4.4 `scripts/verify-architecture.sh` still reports 11 passed, 0 failed.

## 5. Fix the 8 known analyzer warnings

- [x] 5.1 Run `dotnet build SimpleWalkGame.sln --no-incremental` and confirm the
  documented baseline of 8 xUnit analyzer warnings (6× `xUnit2013`, 1×
  `xUnit2020`, 1× `xUnit2009`) in `SessionIngestionTests`,
  `M4Region1AcceptanceTests`, `DiagnosticsReadModelTests`.
- [x] 5.2 Fix each by changing assertion **shape** only (`Assert.Single`,
  `Assert.Empty`, single-value equality) — never what the test asserts.
- [x] 5.3 Record the before/after test counts and outcomes and confirm they are
  identical.
- [x] 5.4 `dotnet build --no-incremental` reports 0 warnings.

## 6. Turn on the warnings policy

- [x] 6.1 Set `TreatWarningsAsErrors` once in `Directory.Build.props` for all
  projects, replacing the explicit `false` in `WalkGame.Application.csproj`.
- [x] 6.2 Build clean. If enabling it surfaces further warnings, fix or
  explicitly record each one — do **not** suppress.
- [x] 6.3 `dotnet build` and `dotnet test` green.
- [x] 6.4 Prove the policy works: introduce a deliberate unused variable in a
  scratch file, confirm the build fails, then remove it.

## 7. Documentation

- [x] 7.1 `docs/TESTING_AND_RELEASE.md` §14 — record the pinned toolchain
  version as a checkable value, not "9.x today".
- [x] 7.2 `docs/TESTING_AND_RELEASE.md` §16 — replace the "build warning
  baseline" note with the now-enforced policy and the count of warnings that
  were fixed.
- [x] 7.3 `ONBOARDING.md` §2/§4 — state the pinned SDK band as a prerequisite
  and how to resolve it.
- [x] 7.4 `docs/DECISIONS.md` — record the pinning decision, the
  `rollForward` choice, and the central-management decision.
- [x] 7.5 `docs/KNOWN_ISSUES.md` — no entry needed; confirm KI-007 (hook
  `--check` env coupling) is unaffected.

## 8. Verification

- [x] 8.1 `dotnet build SimpleWalkGame.sln --no-incremental` — 0 errors,
  0 warnings.
- [x] 8.2 `dotnet test` — all suites green, same test count as before this
  change.
- [x] 8.3 `scripts/verify-architecture.sh` — 11 passed, 0 failed, with the
  netstandard2.1 rule proven to fail on a broken fixture.
- [x] 8.4 `scripts/verify-ci-steps.sh` — every step green, including the
  updated SDK assertion.
- [x] 8.5 `scripts/verify-pacing-evidence.sh` — seven reproduced, 0 drifted.
- [x] 8.6 `scripts/verify-save-growth.sh` — growth series reproduces.
- [x] 8.7 `tests/guards/run-guard-tests.sh` — green.
- [x] 8.8 `openspec validate pin-build-toolchain-and-centralize-packages --strict`
  passes.
- [x] 8.9 `git status` clean apart from intended files; writer lease released.
