# M5-U1 emulator run — 2026-10-16 (emulator-tested attempt, NOT device-tested)

Status: **BLOCKED — app never reached managed code on the emulator.**

Environment: AVD `wg34` (API 34, x86_64, google_apis), `-accel off` (TCG), `-gpu swiftshader_indirect`, 4 GB guest.
`/dev/kvm` exists but is `root:kvm 0660` and the box user is not in group `kvm` (no sudo), so the emulator
cannot be restarted with KVM. APK: `simple-walk-game-debug-x86_64.apk` (IL2CPP, x86_64, `WG_ANDROID_ARCH=x86_64`), installed.

Observed (see `emulator-logcat-app.txt`):
- Unity native player starts: `PlayerInitEngineGraphics OK`, window 1080x2400, "Fully drawn +1m8s".
- No `WG_BOOT`/`WG_RENDER` line within ~25 min of launch: the first scene's managed code never ran.
- Guest system is saturated (system_server >100% CPU, recurring `com.android.systemui` ANR dialog).
- Earlier run (`emulator-logcat-run1.txt`) ended with the activity window dying (`WIN DEATH`, "app died, no saved state").

Screenshots: `emulator-startup.png`, `emulator-startup-25min.png` — both show only the system ANR dialog;
Home and the Settings delete confirmation were not reachable.

No app runtime bug could be identified from this evidence; the blocker is emulator performance without KVM.
Unblock: add the box user to group `kvm` (or run on an arm64 device / KVM-capable host) and rerun.
