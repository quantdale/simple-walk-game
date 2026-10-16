# Release checklist

A committed, checkable form of `docs/TESTING_AND_RELEASE.md` §18. A release candidate
is not "probably ready"; it is a commit for which every row below is either satisfied with
evidence or explicitly UNVERIFIED with a reason. Rows are re-verified, not remembered.

`scripts/assemble-release-evidence.sh` checks the **[gate]** rows mechanically and copies
the remaining documents into the evidence package. Run it; do not assemble a package by hand.

## Mechanical gates — all must pass for the tagged commit

| # | Claim | Evidence |
|---|---|---|
| 1 | Identity guard passes | `scripts/assert-repo-identity.sh` |
| 2 | Clean build, zero warnings | `dotnet build SimpleWalkGame.sln` |
| 3 | Full test suite green | `dotnet test SimpleWalkGame.sln` |
| 4 | Architecture fitness (layering, engine-free, script hygiene) | `scripts/verify-architecture.sh` |
| 5 | Pacing evidence reproduces byte-for-byte (seven profiles) | `scripts/verify-pacing-evidence.sh` |
| 6 | Save-growth / scenario determinism | `scripts/verify-save-growth.sh` |
| 7 | Content-drift gate + self-test | `scripts/verify-content-compatibility.sh [--self-test]` |
| 8 | Guard proof suite (identity / lease / pre-push) | `tests/guards/run-guard-tests.sh` |
| 9 | No known dependency advisory; runtime surface is one first-party package | `scripts/verify-dependency-health.sh` |
| 10 | Coverage floor held (loss detector, not a target) | `scripts/verify-coverage-floor.sh` |
| 11 | Release evidence package assembled | `scripts/assemble-release-evidence.sh` |
| 12 | The CI body for this commit succeeded | hosted run for the tagged SHA (see below) |

## Claims that cannot be established locally — must be stated, not omitted

| # | Claim | Status |
|---|---|---|
| A | Hosted GitHub Actions is green | **UNVERIFIED** (KI-001: the account's runners execute zero steps). The local gate reproduction is the only executable evidence that exists. |
| B | Runtime/device behaviour — screens, notifications, permission dialogs, lifecycle | **UNVERIFIED** (KI-002: no Unity editor exists in the environment). |
| C | Accessibility verification | **UNVERIFIED** (KI-002: the runtime half of M5-H1's semantics). |
| D | On-device performance and battery budgets | **UNVERIFIED** (KI-002). |
| E | Health Connect / HealthKit adapter behaviour | **UNVERIFIED** (KI-002). Android adapter implemented and compiled into the APK (D-068, KI-016); headless contract tests pass; HealthKit not started. |
| F | At-rest confidentiality of the health-adjacent save | **Decided (D-067):** OS device encryption is a system requirement. Before release, verify on device that the save and backup paths are app-private and that the iOS protection class is not lowered. |
| G | PRODUCT_SPEC privacy requirements section | **CLOSED** (KI-005 closed on main via `589150b`: PRODUCT_SPEC §7a, eight requirements traced in PRIVACY_REVIEW §7). |

## Final Device and Release Qualification Checklist

Must be ticked on real hardware (or, for H's option (a) only, a KVM-accelerated emulator, labelled emulator-tested, never device-tested) before release qualification.

| # | Item | Status |
|---|---|---|
| H | Android debug APK launches and reaches the first screen; onboarding → Home → Settings → delete confirmation reachable; logcat free of crashes. Blocked on the box: no `/dev/kvm` access, emulator unusable (`docs/evidence/m5-u1/EMULATOR_RESULTS.md`). Unblock: box user joins `kvm`, or Michael installs the arm64 debug APK on his phone and reports. | **UNVERIFIED** (KNOWN_ISSUES "Needs Michael", KI-002) |

## Repository hygiene

- [ ] `git status` clean apart from intended files; the writer lease is released.
- [ ] No committed generated artifacts; `bin/`/`obj/` ignored.
- [ ] `docs/KNOWN_ISSUES.md` dispositions every item this release closes.
- [ ] Every new decision recorded in `docs/DECISIONS.md` with its rationale.
- [ ] No documentation claim contradicted by committed evidence.
