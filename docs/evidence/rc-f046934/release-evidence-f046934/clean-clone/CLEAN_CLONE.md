# Clean-clone validation

| field | value |
|---|---|
| source | `https://github.com/quantdale/simple-walk-game.git` |
| commit | `f046934ecb76bc01b62780ba5d2ac716576628a4` |
| started (UTC) | 2026-10-17T05:01:52Z |
| host | Linux x86_64, .NET 9.0.319 |
| Unity lane | yes (Unity-editor-tested; device UNVERIFIED, KI-002) |

| step | result | seconds | log |
|---|---|---|---|
| clone | **PASS** | 6 | `clone.log` |
| identity | **PASS** | 0 | `identity.log` |
| ci-steps | **PASS** | 132 | `ci-steps.log` |
| dotnet-tests | **PASS** | 20 | `dotnet-tests.log` |
| unity-editmode | **PASS** | 25 | `unity-editmode.log` |
| unity-playmode | **PASS** | 14 | `unity-playmode.log` |
| android-apk | **PASS** | 154 | `android-apk.log` |

## Counts (parsed from the logs)

- CI steps: CI-STEP REPRODUCTION: 19 ok, 0 warned, 0 failed, 2 skipped (skips are the hosted checkout/setup-dotnet steps, asserted locally)
- .NET: Passed! - Failed: 0, Passed: 162, Skipped: 0, Total: 162
- .NET: Passed! - Failed: 0, Passed: 208, Skipped: 0, Total: 208
- .NET: Passed! - Failed: 0, Passed: 729, Skipped: 0, Total: 729
- Unity editmode: result="Passed" total="252" passed="252" failed="0" skipped="0" 
- Unity playmode: result="Skipped:Ignored" total="5" passed="4" failed="0" skipped="1" 
- Android: WG_BUILD_RESULT Succeeded

finished (UTC): 2026-10-17T05:07:43Z

**CLEAN CLONE: PASS**
