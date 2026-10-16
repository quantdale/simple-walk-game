# D-111 share-sheet compile check (grok2212, 2026-10-16)

Base: `origin/main` 0574e21 (after D-111 #85, D-112 #86, #87). Run in a dedicated worktree.

| Check | Result | Label |
|---|---|---|
| `tools/unity/build-android-debug.sh` (arm64 IL2CPP debug) | `WG_BUILD_RESULT Succeeded`, APK 31,343,846 B, sha256 `a7437a2f…4715c6` (not committed); 0 `error CS` in the log | automated verified (compile only) |
| `AndroidShareSheet` Android branch (`#if UNITY_ANDROID && !UNITY_EDITOR`) compiled into the player | `global-metadata.dat` contains the type `AndroidShareSheet`, `ShareText`, and the literals that exist only in the Android branch: `Share a copy of your save`, `Simple Walk Game save`, `android.content.Intent`, `ACTION_SEND`, `EXTRA_TEXT`, `createChooser` | automated verified (compile only) |

No compile errors, so no fix was needed. The JNI calls resolve at runtime only, so the share sheet
itself on a device is still UNVERIFIED (KI-002). Log: `android-build.log` (licensing ids redacted by the script).
