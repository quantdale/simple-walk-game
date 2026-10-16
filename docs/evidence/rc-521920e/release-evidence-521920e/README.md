# Release evidence package

| field | value |
|---|---|
| tag | (untagged) |
| commit | 521920ed5fb94b18ebed9b201bad83b79a3cf656 |
| assembled at | 2026-10-16T17:49:11Z |
| repository | quantdale/simple-walk-game |

## Mechanical claims (re-verified here, not copied)

- **PASS** `build` — see `build.log`
- **PASS** `test` — see `test.log`
- **PASS** `architecture` — see `architecture.log`
- **PASS** `pacing-evidence` — see `pacing-evidence.log`
- **PASS** `save-growth` — see `save-growth.log`
- **PASS** `content-compatibility` — see `content-compatibility.log`
- **PASS** `guard-suite` — see `guard-suite.log`
- **PASS** `dependency-health` — see `dependency-health.log`
- **PASS** `coverage-floor` — see `coverage-floor.log`
- **PASS** `clean-clone` — fresh clone of this commit; see `clean-clone/CLEAN_CLONE.md`
- **PASS** `hosted-ci` — 1 successful hosted `ci` run(s) for this commit (`gh run list --commit`)

## Documented evidence (copied verbatim from the maintained documents)

- known issues by severity: `docs/KNOWN_ISSUES.md`
- privacy review: `docs/PRIVACY_REVIEW.md`
- decisions affecting this release: `docs/DECISIONS.md`
- release qualification model: `docs/TESTING_AND_RELEASE.md`

## Claims that CANNOT be established here

- runtime/DEVICE behaviour: launch, lifecycle (OS kill/reboot), TalkBack,
  OS font scaling, Health Connect permission/read/deletions, on-device
  performance/memory/battery, GPU rendering (KI-002: no device and no KVM).
  Unity-editor-tested evidence exists for the shell (EditMode/PlayMode) and the
  arm64 debug APK builds; that is not device evidence.
- iOS / HealthKit: not started.

The full device list is the Final Device and Release Qualification Checklist in
`RELEASE_CHECKLIST.md` (copied into this package).

These are stated as unverified rather than omitted: a release package that
quietly drops its open blockers is worse than one that lists them.
