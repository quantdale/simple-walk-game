# Clean-clone validation

| field | value |
|---|---|
| source | `https://github.com/quantdale/simple-walk-game.git` |
| commit | `521920ed5fb94b18ebed9b201bad83b79a3cf656` |
| started (UTC) | 2026-10-16T17:41:11Z |
| host | Linux x86_64, .NET 9.0.319 |
| Unity lane | yes (Unity-editor-tested; device UNVERIFIED, KI-002) |

| step | result | seconds | log |
|---|---|---|---|
| clone | **PASS** | 6 | `clone.log` |
| identity | **PASS** | 0 | `identity.log` |
| ci-steps | **PASS** | 99 | `ci-steps.log` |
| dotnet-tests | **PASS** | 8 | `dotnet-tests.log` |
| unity-editmode | **PASS** | 20 | `unity-editmode.log` |
| unity-playmode | **PASS** | 14 | `unity-playmode.log` |
| android-apk | **PASS** | 149 | `android-apk.log` |

## Counts (parsed from the logs)

- CI steps: CI-STEP REPRODUCTION: 19 ok, 0 warned, 0 failed, 2 skipped (skips are the hosted checkout/setup-dotnet steps, asserted locally)
- .NET: Passed! - Failed: 0, Passed: 157, Skipped: 0, Total: 157
- .NET: Passed! - Failed: 0, Passed: 185, Skipped: 0, Total: 185
- .NET: Passed! - Failed: 0, Passed: 471, Skipped: 0, Total: 471
- Unity editmode: result="Passed" total="205" passed="205" failed="0" skipped="0" 
- Unity playmode: result="Skipped:Ignored" total="5" passed="4" failed="0" skipped="1" 
- Android: WG_BUILD_RESULT Succeeded

finished (UTC): 2026-10-16T17:46:07Z

**CLEAN CLONE: PASS**
