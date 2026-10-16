# M7-A results (2026-10-16): Android Health Connect adapter

Label: **Unity-editor-tested** (Unity 6000.3.26f1 batchmode, box). Device behaviour: **UNVERIFIED** (KI-002, no device or KVM).

| Check | Result |
|---|---|
| `dotnet test`, all suites | Domain 148, Application 325, Infrastructure 82 (incl. 32 `HealthConnectActivityAdapterTests`): all pass |
| `scripts/verify-architecture.sh` | 15/15 |
| EditMode (`EVIDENCE_DIR=docs/evidence/m7-a tools/unity/run-editmode-tests.sh`) | 27/27 (4 new Health Connect wiring tests) |
| PlayMode (`tools/unity/run-playmode-tests.sh`) | 1/1 |
| Android arm64 IL2CPP debug build (`tools/unity/build-android-debug.sh`) | Succeeded, APK 30,979,826 bytes |
| APK inspection (`apk-inspection.txt`) | `HealthConnectBridge`, `$Api34`, `HealthPermissionRationaleActivity` in dex; manifest has `READ_STEPS`, the `VIEW_PERMISSION_USAGE` + `HEALTH_PERMISSIONS` rationale activity, minSdk 25, targetSdk 36 |

The design is recorded in DECISIONS D-068; the open gaps are in KNOWN_ISSUES KI-016.
