# Clean-clone validation

| field | value |
|---|---|
| source | `https://github.com/quantdale/simple-walk-game.git` |
| commit | `181db31a19d1a96a0a0d8ea951d4a2a39da67068` |
| started (UTC) | 2026-10-16T11:33:14Z |
| host | Linux x86_64, .NET 9.0.319 |
| Unity lane | yes (Unity-editor-tested; device UNVERIFIED, KI-002) |

| step | result | seconds | log |
|---|---|---|---|
| clone | **PASS** | 5 | `clone.log` |
| identity | **PASS** | 0 | `identity.log` |
| ci-steps | **PASS** | 84 | `ci-steps.log` |
| dotnet-tests | **PASS** | 6 | `dotnet-tests.log` |
| unity-editmode | **PASS** | 19 | `unity-editmode.log` |
| unity-playmode | **PASS** | 14 | `unity-playmode.log` |
| android-apk | **PASS** | 148 | `android-apk.log` |

## Counts (parsed from the logs)

- CI steps: CI-STEP REPRODUCTION: 17 ok, 0 warned, 0 failed, 2 skipped (skips are the hosted checkout/setup-dotnet steps, asserted locally)
- .NET: Passed! - Failed: 0, Passed: 148, Skipped: 0, Total: 148
- .NET: Passed! - Failed: 0, Passed: 118, Skipped: 0, Total: 118
- .NET: Passed! - Failed: 0, Passed: 423, Skipped: 0, Total: 423
- Unity editmode: result="Passed" total="131" passed="131" failed="0" skipped="0" 
- Unity playmode: result="Skipped:Ignored" total="5" passed="4" failed="0" skipped="1" 
- Android: WG_BUILD_RESULT Succeeded

finished (UTC): 2026-10-16T11:37:50Z

**CLEAN CLONE: PASS**
