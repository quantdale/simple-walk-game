# Clean-clone validation

| field | value |
|---|---|
| source | `https://github.com/quantdale/simple-walk-game.git` |
| commit | `181db31a19d1a96a0a0d8ea951d4a2a39da67068` |
| started (UTC) | 2026-10-16T11:30:08Z |
| host | Linux x86_64, .NET none |
| Unity lane | yes (Unity-editor-tested; device UNVERIFIED, KI-002) |

| step | result | seconds | log |
|---|---|---|---|
| clone | **PASS** | 4 | `clone.log` |
| identity | **PASS** | 0 | `identity.log` |
| ci-steps | **FAIL** | 0 | `ci-steps.log` |
| dotnet-tests | **FAIL** | 0 | `dotnet-tests.log` |
| unity-editmode | **PASS** | 23 | `unity-editmode.log` |
| unity-playmode | **PASS** | 14 | `unity-playmode.log` |
| android-apk | **PASS** | 129 | `android-apk.log` |

## Counts (parsed from the logs)

- CI steps:  (skips are the hosted checkout/setup-dotnet steps, asserted locally)
- Unity editmode: result="Passed" total="131" passed="131" failed="0" skipped="0" 
- Unity playmode: result="Skipped:Ignored" total="5" passed="4" failed="0" skipped="1" 
- Android: WG_BUILD_RESULT Succeeded

finished (UTC): 2026-10-16T11:32:58Z

**CLEAN CLONE: FAIL**
