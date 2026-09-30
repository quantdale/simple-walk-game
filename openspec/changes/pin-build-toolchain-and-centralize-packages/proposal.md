# Pin the build toolchain and centralise package versions

## Why

The repository's own release gate requires "documented toolchain version"
(`docs/TESTING_AND_RELEASE.md` §14, clean-clone gate). It is not satisfied.
There is **no** `global.json`, no `Directory.Build.props`, and no central package
management. The consequences are concrete:

- **The SDK is unpinned.** CI pins `9.0.x` via `actions/setup-dotnet`, but a
  local build uses whatever is installed. `scripts/verify-ci-steps.sh` asserts
  only that the local major version is 9. A future .NET 10 SDK, or a machine
  with a preview feature band, builds the same tree with different defaults and
  potentially different analyzers. A release candidate cannot claim to be
  "reproducible from a clean checkout" when the toolchain is whatever the
  machine happens to have.
- **Every project restates the same settings.** `LangVersion`, `Nullable` and
  `TargetFramework` are duplicated across the three production csprojs, and the
  five test/CLI projects duplicate the three xUnit/Microsoft test package
  versions independently. A version bump today is a five-file edit with no
  mechanical check that they were all bumped.
- **`TreatWarningsAsErrors` is set inconsistently.** It is explicitly `false` in
  `WalkGame.Application.csproj` and absent (default `false`) elsewhere. The
  explicit `false` in one file and the implicit default in two others is a
  distinction without a difference that reads as intentional.
- **Analyzer warnings are already known and unenforced.**
  `docs/TESTING_AND_RELEASE.md` §16 records 8 pre-existing xUnit analyzer
  warnings in the test projects, deliberately left in place. They are unenforced
  only because nothing treats warnings as errors, which means a *new* warning
  would also pass silently. The same section already warns against citing
  "0 warnings" without `--no-incremental` — which is itself a symptom of the
  toolchain not being pinned.

None of this is a production defect. It is reproducibility and maintenance debt
in a repository whose entire release story rests on a locally reproducible gate
(hosted CI has been blocked since 2026-08-30, KI-001), so local reproducibility
is currently the *only* reproducibility story there is.

## What Changes

- A `global.json` pins the SDK major/minor/feature band and rolls forward within
  the pinned band, so every machine and CI runner resolves a known toolchain.
- A `Directory.Build.props` carries the shared project settings once:
  target framework, language version, nullable, and an explicit, uniform
  warnings policy.
- Central package management pins the shared package versions once, so the three
  test projects cannot drift apart.
- The existing 8 xUnit analyzer warnings are **fixed**, not suppressed, so the
  warnings policy can be meaningful rather than decorative.
- A new warning introduced anywhere fails the build.
- The documented toolchain version in `TESTING_AND_RELEASE.md` and
  `ONBOARDING.md` is updated to the pinned value and is checkable.

**Non-goals:** upgrading `System.Text.Json` 8.0.5, changing any product
behaviour, reformatting the source tree, or adopting `dotnet format` as a gate.
`TESTING_AND_RELEASE.md` §16 already records a considered decision not to adopt
`dotnet format`, and this change does not reopen it.

## Impact

**Evidence from the audit**

- `ls global.json Directory.Build.props Directory.Packages.props nuget.config
  NuGet.config` → all absent.
- `src/WalkGame.Domain/WalkGame.Domain.csproj`,
  `src/WalkGame.Application/WalkGame.Application.csproj`,
  `src/WalkGame.Infrastructure/WalkGame.Infrastructure.csproj` — each restates
  `<TargetFramework>netstandard2.1</TargetFramework>`, `<LangVersion>9.0</LangVersion>`,
  `<Nullable>enable</Nullable>`; only Application sets
  `<TreatWarningsAsErrors>false</TreatWarningsAsErrors>` explicitly.
- `tests/WalkGame.Domain.Tests/`, `tests/WalkGame.Application.Tests/`,
  `tests/WalkGame.Infrastructure.Tests/` — each independently declares
  `coverlet.collector` 6.0.2, `Microsoft.NET.Test.Sdk` 17.12.0, `xunit` 2.9.2,
  `xunit.runner.visualstudio` 2.8.2.
- `docs/TESTING_AND_RELEASE.md` §16 "Build warning baseline" — 8 pre-existing
  xUnit analyzer warnings (6× `xUnit2013`, 1× `xUnit2020`, 1× `xUnit2009`) in
  `SessionIngestionTests`, `M4Region1AcceptanceTests`, `DiagnosticsReadModelTests`.
- `docs/TESTING_AND_RELEASE.md` §14 — requires a "documented toolchain
  version"; currently satisfied only by prose ("9.x today").
- `docs/KNOWN_ISSUES.md` KI-001 — hosted CI blocked, making local reproducibility
  the operative gate.

**Affected files**

- New: `global.json`, `Directory.Build.props`, `Directory.Packages.props`.
- Modified: the six csproj files (settings removed, versions replaced by
  central declarations).
- Modified: three test files to fix the 8 analyzer warnings (assertion shape,
  not assertion meaning).
- Modified: `.github/workflows/ci.yml` to install the pinned SDK explicitly
  rather than `9.0.x`.
- Modified: `docs/TESTING_AND_RELEASE.md`, `ONBOARDING.md`.

**Important constraint**

`scripts/verify-architecture.sh` greps the three production csproj files for
`<TargetFramework>netstandard2.1</TargetFramework>` and **fails** if it is not
present in each. Moving that property into `Directory.Build.props` breaks the
architecture gate. The script must be updated in the same change to resolve the
effective property, and the update must keep the rule's actual intent (all three
production layers stay on `netstandard2.1`).

**Dependencies**

- Independent of every behavioural change. Land early, because it makes every
  later change's build reproducible.
