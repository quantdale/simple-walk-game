# D-119 gentle reminder: APK compile and EditMode evidence (2026-10-16, grok2345)

- Tree: branch `agent/simple-walk-game/reminder-grok2345` at `0173114` (D-119 commit `3f6f4f4` merged with main `5dab899`).
- `tools/unity/build-android-debug.sh` (arm64 IL2CPP debug): `WG_BUILD_RESULT Succeeded`, APK 31,437,984 bytes (not committed).
- `classes4.dex` contains `Lcom/quantdale/walkgame/reminder/ReminderBridge;` and `ReminderReceiver;`, plus the `setInexactRepeating` and `areNotificationsEnabled` references.
- The merged `AndroidManifest.xml` declares `android.permission.POST_NOTIFICATIONS` and the receiver `com.quantdale.walkgame.reminder.ReminderReceiver`.
- `global-metadata.dat` contains the C# bridge literal `com.quantdale.walkgame.reminder.ReminderBridge` (the `AndroidReminderScheduler` Android branch compiled under IL2CPP).
- EditMode on the same tree: 186/186 passed (`editmode-results.xml`), including `GentleReminderTests` (5, fake scheduler).

Labels: compiled into the APK; behaviour Unity-editor-tested with a fake. Notification delivery, the OS permission dialog, and alarm timing on a device are UNVERIFIED (KI-002).
