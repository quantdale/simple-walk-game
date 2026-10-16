# D-103 Java bridge compile check + full test rerun on main 62f33d4 (grok2058, 2026-10-16)

Base: `origin/main` 62f33d4 (after D-103 #74, D-105 #77, D-104 #76). Run in a dedicated worktree.

| Check | Result | Label |
|---|---|---|
| `tools/unity/build-android-debug.sh` (arm64 IL2CPP debug) | `WG_BUILD_RESULT Succeeded`, APK 31,332,558 B, sha256 `65a520c1…ef43df` (not committed) | automated verified (compile only) |
| D-103 `HealthConnectBridge` (endZoneOffset) in dex | `classes3.dex` contains `HealthConnectBridge` and `getEndZoneOffset` | automated verified (compile only) |
| .NET `dotnet test SimpleWalkGame.sln` | 728/728 (Domain 157, Infrastructure 121, Application 450) | automated verified |
| Unity EditMode | 151/151 passed | Unity-editor-tested |
| Unity PlayMode | 4 passed + 1 ignored (capture test: no graphics device under -nographics) | Unity-editor-tested |

No compile errors, so no fix PR was needed. The device runtime of the offset read is still UNVERIFIED (KI-002).
Logs: `android-build.log` (here), `../main-62f33d4-tests/` (NUnit XML + logs). Licensing ids are redacted by the scripts.
