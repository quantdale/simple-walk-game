## Context

See `proposal.md` (Why) for the evidence.

The one non-obvious constraint, and the reason this change needs a design
rather than a five-minute edit:

`scripts/verify-architecture.sh` contains this rule:

```sh
if ! grep -q '<TargetFramework>netstandard2.1</TargetFramework>' "$file"; then
    bad "$layer is not targeting netstandard2.1"
fi
```

It greps the **csproj file directly**. The whole point of
`Directory.Build.props` is to remove that line from the csproj files, which
would make the architecture gate fail on all three layers — a false positive
from a rule whose *intent* is correct and whose *implementation* is coupled to
file layout.

This is the second time this repository has been bitten by exactly this class of
problem, and the script documents the first instance: rule 6 exists because
"a gate script committed without the executable bit passes every local run on
Windows (which ignores the bit) and then fails in CI with 'Permission denied'.
That exact latent breakage was introduced and shipped once already". The lesson
recorded there — prove each rule fails against a deliberately broken fixture —
applies directly here.

Secondary constraints:

- Central package management (`Directory.Packages.props` with
  `ManagePackageVersionsCentrally`) changes how every `PackageReference` is
  written: a `Version` attribute on a `PackageReference` becomes an error, not a
  warning. All four test packages across three projects must be converted
  together.
- `verify-architecture.sh` also greps the production csprojs for
  `ProjectReference Include="..."` entries and compares the exact reference set.
  Centralising properties does not affect that, but the script must be
  re-verified end to end rather than assumed.
- `NETSDK1206` (a duplicate, non-central package version) and
  `NU1008` (a project using central management without the property) are the
  failure modes to expect during the conversion.

## Goals / Non-Goals

**Goals**

- Pin the SDK so a build is reproducible on any machine and on CI.
- State each shared build setting once, in one file.
- Make a new compiler or analyzer warning fail the build, after the existing
  known warnings are genuinely fixed.
- Keep every existing gate green, including the architecture gate.

**Non-Goals**

- Dependency upgrades. `System.Text.Json` stays at 8.0.5; xUnit and the test
  SDK stay at their current versions. Version churn is not the problem and
  bumping during a pinning change would confound the result.
- `dotnet format` as a gate. `TESTING_AND_RELEASE.md` §16 records a deliberate
  decision not to adopt it; this change does not reopen that.
- Source reformatting.
- Changing the `netstandard2.1` target for any layer.

## Decisions

### 1. Pin with `rollForward`, not an exact version

`global.json` pins `sdk.version` to a specific feature band with
`rollForward: latestFeature`, so a machine with a newer patch of the same band
builds successfully while a machine on a different major/minor fails fast with
a clear message. An exact pin would make onboarding brittle — a contributor
without that exact SDK would be blocked with no recourse.

`allowPrerelease: false` keeps preview SDKs out, which matters because preview
analyzers can change diagnostics between runs.

*Alternative considered:* pin an exact SDK version. Rejected: it turns a
patch-level SDK difference into a hard build failure for contributors, and
`ONBOARDING.md` §1 requires a *reproducible machine*, not an identical one.

### 2. Shared settings in `Directory.Build.props`, test settings scoped separately

`Directory.Build.props` carries the three production layers' shared settings
(`TargetFramework`, `LangVersion`, `Nullable`, warnings policy). It does **not**
set `TargetFramework` unconditionally, because the test projects and the CLI
target `net9.0` and must not inherit `netstandard2.1`.

The mechanism is condition-on-project: the props file applies the production
settings only to `src/**`, and the per-project `TargetFramework` for
`net9.0` projects stays where it is. Alternatively, set `TargetFramework` per
project and keep only the truly universal settings (`LangVersion`,
`Nullable`, warnings policy) in the props file.

The design records both options and requires the implementer to pick the one
that keeps `verify-architecture.sh` able to verify the netstandard2.1 rule for
the three production layers. The second is simpler and is recommended, because
the architecture rule is about a specific property on specific projects and is
best served by that property remaining local and greppable.

### 3. Update the architecture script to resolve the effective property

Whichever mechanism is chosen, `verify-architecture.sh` must verify the
**effective** target framework of each production layer rather than grepping a
single file. The robust approach is to read the effective value MSBuild itself
computes — `dotnet msbuild <proj> -getProperty:TargetFramework` — so the rule
survives any future restructuring of where the property is declared.

If that is judged too heavy for a shell gate, the fallback is to grep both the
csproj and `Directory.Build.props`. The weaker fallback must be called out as
such in the script's comments, following the script's own habit of documenting
what each rule does and does not guarantee.

Either way, **rule 6's proof discipline applies**: the updated rule must be
demonstrated to fail against a deliberately broken fixture (a production layer
moved off `netstandard2.1`) before it is trusted. `verify-architecture.sh`'s
header explicitly records that every rule was proven this way; that property
must not be lost in this edit.

### 4. Fix the 8 analyzer warnings rather than suppress them

`TESTING_AND_RELEASE.md` §16 records them as "deliberately left untouched" from
the M5-H1 campaign. That was a defensible scoping decision at the time — the
campaign was about UX contracts, and churning unrelated test files was
explicitly against its policy. The situation is different now: leaving them
means the warnings policy can never be turned on, and turning it on is the only
way a *new* warning is caught.

The fix is assertion **shape**, not assertion **meaning**: `xUnit2013` wants
`Assert.Equal(expectedCount, collection.Count)` replaced with
`Assert.Equal(expectedCount, collection.Count)` in single-value form (or
`Assert.Single`/`Assert.Empty` where applicable), `xUnit2020` and `xUnit2009`
want `Assert.True(collection.Property)` replaced with a specific collection
assertion. Every one of these makes the assertion *stronger*; none should change
what any test asserts. The task list requires confirming the test count and
outcomes are identical before and after.

### 5. Uniform, explicit warnings policy

`TreatWarningsAsErrors` is currently explicit `false` in one production csproj
and implicit `false` in the others. Set it once in `Directory.Build.props` for
all projects. Test projects get the same policy — the 8 warnings are fixed
first, so the policy is satisfiable.

*Alternative considered:* warnings-as-errors for production only. Rejected as
a needless asymmetry: a warning in a test is exactly how a test rots, and the
repository's own discipline is that unenforced warnings accumulate.

### 6. Update CI to install the pinned SDK

`.github/workflows/ci.yml` currently uses `dotnet-version: 9.0.x`. It must use
`global.json` (`actions/setup-dotnet` reads it when no explicit version is
given, or the version is stated explicitly). Whichever is chosen,
`scripts/verify-ci-steps.sh` asserts the local version satisfies the workflow's
declared version — that assertion must keep working, and if the workflow stops
declaring a version, the script's assertion needs updating to read
`global.json` instead.

That coupling between the CI workflow and the CI-reproduction script is easy to
miss and is called out here explicitly.

## Risks / Trade-offs

- **[The architecture gate could silently weaken]**
  → The primary risk. Mitigation is the existing rule-6 discipline: prove the
  updated rule fails on a broken fixture, and keep the rule's documentation
  explicit about what it guarantees. A gate that silently stops checking is
  worse than no gate.

- **[Central package management can break restore if any project is
  inconsistent]**
  → Convert all PackageReference declarations in one pass, then restore
  cleanly. The failure modes (`NU1008`, `NETSDK1206`) are loud, not silent.

- **[Turning on warnings-as-errors may surface a wave of new warnings]**
  → The current clean-build baseline is 8 warnings, all xUnit analyzer warnings
  in tests, all recorded. If enabling the policy surfaces more, that is
  information worth having; the task list requires fixing or explicitly
  recording each one rather than suppressing.

- **[Pinning the SDK may block a contributor]**
  → Mitigated by `rollForward` within the feature band. The error message for a
  mismatched major version is self-explanatory.

- **[A newer SDK could still change behaviour within the band]**
  → Accepted; this is the standard trade and is strictly better than today's
  unpinned state. A fully hermetic build would need a containerised toolchain,
  which is disproportionate here and is recorded in the master plan's deferred
  list.

## Migration Plan

No product migration.

Order of operations matters, because each step can leave the build broken:

1. Add `Directory.Build.props` with universal settings only; remove the
   duplicated `LangVersion`/`Nullable` from the csprojs. Build and test.
2. Add `Directory.Packages.props`; convert every `PackageReference` to
   version-less. Restore, build, test.
3. Add `global.json`. Update `ci.yml` and `verify-ci-steps.sh` together. Build
   and test.
4. Update `verify-architecture.sh` for the effective-property read; prove the
   rule fails on a broken fixture.
5. Fix the 8 analyzer warnings. Build clean with `--no-incremental`.
6. Turn on `TreatWarningsAsErrors`. Build and test.

Each step is independently revertible, which matters because hosted CI cannot
validate intermediate states (KI-001) and the local gate is the only feedback.

## Open Questions

None. The two mechanisms for the target-framework rule are both recorded above
with a recommendation; the choice does not change the shape of this change.
