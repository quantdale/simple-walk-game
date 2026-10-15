# M5-U1 Unity shell: results (2026-10-16, Asia/Manila)

Editor 6000.3.26f1 (changeset de8167e7440c), Personal licence via the Hub licensing client.
Box: Debian 13, no GPU, 15 GB RAM + 8 GB swap. All runs were `-batchmode -nographics`; the editor GUI was never opened.

## Verified on this box

| Check | Result | Evidence |
|---|---|---|
| EditMode suite (`tools/unity/run-editmode-tests.sh`) | **13/13 passed** | `editmode-results.xml`, `editmode-run.log` |
| Android debug APK (`tools/unity/build-android-debug.sh`) | **Succeeded** in 3m20s; IL2CPP, arm64-v8a only, Development build | `android-build.log` (`WG_BUILD_RESULT Succeeded`) |
| APK | `src/WalkGame.Unity/Build/Android/simple-walk-game-debug.apk` (gitignored), 30,730,606 bytes, sha256 `fe1fff7bff4aebcef1db142ee4810feeb5b994dc063ae6252c4c85a37b739e3f` | `unzip -l`: `lib/arm64-v8a/libil2cpp.so`, `libunity.so`, `global-metadata.dat` |
| Architecture rule 7 + self-test | pass; self-test rejects a canonical-state and an adapter-outside-root fixture | `scripts/verify-architecture.sh --selftest-rule7`, guard scenario 11 |

EditMode coverage: the router maps every `StartStatus` value; the three unreadable outcomes route to a non-destructive error with no "start" action; a garbage save stays byte-identical after boot and retry; new game → Home; codec round-trip via `Continue()` under Unity's Mono runtime; all five screens render non-error after a new game; queueing a project; settings persist across sessions; delete cancel keeps data; delete confirm empties the directory and the next boot is `NoSaveFound`; the save directory sits under `Application.persistentDataPath`.

Finding fixed during the run: STJ 8 binds `System.Runtime.CompilerServices.Unsafe` 6.0, which Unity's profile does not provide. Without it, `StartNewGame` returned `SaveUnreadable` ("Could not load file or assembly ..."). `sync-plugins.sh` now ships it. The EditMode suite caught this.

## UNVERIFIED (not run; must not be claimed)

- The APK has never been installed or launched (no device or emulator). IL2CPP stripping safety at runtime, the save path on a device, and `OnApplicationPause` resume are UNVERIFIED. `link.xml` preserves the core and STJ, but nothing has executed them on-device.
- Visual rendering. The screens are built in code from engine-free `ScreenModel`s (no UXML/USS). `PanelSettings` is created at runtime with no theme stylesheet, so the look is unstyled and unseen.
- Screen-reader navigation, reduced-motion runtime behaviour, haptics and sound.
- PlayMode tests (none exist yet).
- The Health Connect adapter (M5-A). `ShellConnectionPort` reports the synthetic source as connected only in development builds, and as `Unsupported` in release builds.
- Hosted CI does not run Unity. The Unity evidence above is local-box only.
