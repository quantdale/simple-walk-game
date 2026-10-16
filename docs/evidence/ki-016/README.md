# KI-016(a) expired change token: evidence (D-077)

- `tools/android/test-bridge-logic.sh`: 10/10 headless checks of `GapReconciler`, now a CI step:
  - span;
  - absent → deleted, with interval and lastModified kept;
  - idempotent re-run;
  - out-of-range records never inferred deleted;
  - an existing change-log deletion time is preserved;
  - inverted range;
  - full presence.
- `android-build.log`: arm64 IL2CPP debug APK built in the Unity editor with the updated bridge (`WG_BUILD_RESULT Succeeded`). `classes*.dex` contains `GapReconciler` and `recoverFromExpiredToken`. Licensing identifiers are redacted.
- Device behaviour (a real token expiry on Android 14+) is UNVERIFIED (KI-002).
