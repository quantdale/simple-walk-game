## 1. Shell
- [x] 1.1 `NextReminderTime` (half-hour grid, 06:00–21:00, never moves the wrong way), `reminder:earlier` / `reminder:later` actions and copy in `PlayerStrings` (EditMode `GentleReminderTests`).
## 2. Android
- [x] 2.1 `ReminderBootReceiver` + `RECEIVE_BOOT_COMPLETED`; `ReminderBridge` stores the armed state, `cancel` clears it; compiled through an arm64 APK build (`docs/evidence/apk-d121/RESULTS.md`).
## 3. Verify
- [x] 3.1 EditMode on the branch; device boot behaviour UNVERIFIED (KI-002).
