# M9 release evidence (headless)

| Package | Commit | Verdict |
|---|---|---|
| [`release-evidence-0f9f3d7/`](release-evidence-0f9f3d7/README.md) | `0f9f3d79af7e269038e27a280f0259269937ce5a` (main after #34) | All 11 mechanical claims **PASS**: build, tests, architecture, pacing, save-growth, content-compatibility, guard suite, dependency health, coverage floor, **clean clone** and **hosted CI**. Device claims are **UNVERIFIED** (KI-002). |

The package was produced by `scripts/assemble-release-evidence.sh --commit <sha> --clean-clone <dir>` with HEAD at that exact commit. It is not edited by hand.

**Clean clone** (`scripts/validate-clean-clone.sh --ref 0f9f3d7 --with-unity`, 2026-10-16 13:16–13:21 PT+8), in about 5 minutes:
- **Clone and identity:** passed.
- **CI steps:** 16 ok, 0 failed; the 2 skips are the hosted checkout/setup-dotnet steps, asserted locally.
- **.NET tests:** 148 + 91 + 352 = 591 passed.
- **Unity EditMode:** 56/56.
- **Unity PlayMode:** 3 passed, plus 1 capture-only skip.
- **Android:** the arm64 debug APK built.

**Known cosmetic defect in this package:** the `repository` field prints the raw identity JSON, because the assembler looked for a `slug` key. It is fixed in the script for future packages; the package is left as generated.

**Not a release:** M9 cannot exit until the Final Device and Release Qualification Checklist (H–Q in `docs/RELEASE_CHECKLIST.md`) is ticked on hardware.
