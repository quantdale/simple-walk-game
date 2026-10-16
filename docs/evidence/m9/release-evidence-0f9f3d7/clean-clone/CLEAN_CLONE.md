# Clean-clone validation

| field | value |
|---|---|
| source | `https://github.com/quantdale/simple-walk-game.git` |
| commit | `0f9f3d79af7e269038e27a280f0259269937ce5a` |
| started (UTC) | 2026-10-16T05:17:07Z |
| host | Linux x86_64, .NET 9.0.319 |
| Unity lane | yes (Unity-editor-tested; device UNVERIFIED, KI-002) |

| step | result | seconds | log |
|---|---|---|---|
| clone | **PASS** | 5 | `clone.log` |
| identity | **PASS** | 0 | `identity.log` |
| ci-steps | **PASS** | 90 | `ci-steps.log` |
| dotnet-tests | **PASS** | 7 | `dotnet-tests.log` |
| unity-editmode | **PASS** | 20 | `unity-editmode.log` |
| unity-playmode | **PASS** | 14 | `unity-playmode.log` |
| android-apk | **PASS** | 139 | `android-apk.log` |

## Counts (parsed from the logs)

- CI steps: 16 ok of 18
- .NET: Passed! - Failed: 0, Passed: 148, Skipped: 0, Total: 148
- .NET: Passed! - Failed: 0, Passed: 91, Skipped: 0, Total: 91
- .NET: Passed! - Failed: 0, Passed: 352, Skipped: 0, Total: 352
- Unity editmode: result="Passed" total="56" passed="56" failed="0" skipped="0" 
- Unity playmode: result="Skipped:Ignored" total="4" passed="3" failed="0" skipped="1" 
- Android: WG_BUILD_RESULT Succeeded

finished (UTC): 2026-10-16T05:21:42Z

**CLEAN CLONE: PASS**
