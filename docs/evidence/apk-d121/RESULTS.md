# D-121 evidence: reminder time picker + ReminderBootReceiver (2026-10-17, grok0020)

Branch `agent/simple-walk-game/remindertime-grok0020` on main `75a6ca1`.

- EditMode: 205/205 passed, 0 failed (`editmode-results.xml`, batchmode `-nographics`, Unity 6000.3.26f1). Unity-editor-tested.
- Android: `tools/unity/build-android-debug.sh` (arm64 IL2CPP debug) → `WG_BUILD_RESULT Succeeded`, APK 31,448,180 bytes, sha256 `7ece1e1cb678cf6b251711a3d9f3ec2c80be3d927d9930c8b1973a146d3155c2` (not committed).
  - `classes4.dex` contains `com/quantdale/walkgame/reminder/ReminderBootReceiver` and `rearmAfterBoot`.
  - The merged binary `AndroidManifest.xml` contains `ReminderBootReceiver`, `RECEIVE_BOOT_COMPLETED` and `BOOT_COMPLETED`.
  - No fix was needed. The build rewrote `ProjectSettings.asset`; it was restored before commit (KI-015/D-117).
- Device: boot re-arm and notification delivery UNVERIFIED (KI-002, no device).
