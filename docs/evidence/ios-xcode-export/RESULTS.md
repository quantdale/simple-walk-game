# Unity iOS Xcode export + unsigned xcodebuild (KI-022)

**Label: Xcode-built unsigned on hosted macOS, not device-tested.** No signing, no install, no launch,
no HealthKit call was ever made. The HealthKit adapter stays NOT composed (D-112; no wiring decision).

## Steps
1. Box: `unity install-modules -e 6000.3.26f1 -m ios --accept-eula` (free iOS Build Support module,
   Linux editor). The first attempt failed with `COULD_NOT_EXTRACT` until `xz-utils`/`cpio`/`p7zip-full`
   were installed with apt.
2. Box: `tools/unity/export-ios-xcode.sh` → `ShellBuild.BuildIosXcode` (batchmode, IL2CPP, device SDK,
   automatic signing off, no team id). `WG_BUILD_RESULT Succeeded`; export 762 MB (log: `ios-export.log`,
   licence identifiers redacted). `ProjectSettings.asset` restored by the script, not committed.
3. Export checks: `Libraries/Plugins/iOS/WalkGameHealthKitBridge.swift` is in the UnityFramework
   sources, `HealthKit.framework` is linked (from the plugin `.meta`), and IL2CPP emitted the four
   `wg_hk_*` P/Invoke externs from the `UNITY_IOS` branch of `IosHealthKitBridge`.
4. `tools/unity/push-ios-export.sh grok2251-a` pushed the export as 3 xz chunks (122 MB total, SHA-256
   checked) on the throwaway branch `ios-export/grok2251-a` (source commit `cd71160`), never main.
5. `.github/workflows/ios-xcode-export-verify.yml` (macos-latest, standard runner, public repo → free;
   no Unity, no Unity licence, no secrets, no artifacts) ran
   `xcodebuild -scheme Unity-iPhone -configuration Release -sdk iphoneos -destination 'generic/platform=iOS'
   CODE_SIGNING_ALLOWED=NO`: **BUILD SUCCEEDED** first time (Xcode 26.6 / 17F113, iOS SDK 26.5) in
   run 38920379246 (7 m 50 s). No compile or link fix was needed.
6. `nm -gU UnityFramework` exports all four `wg_hk_*` symbols and `otool -L` shows HealthKit.framework.
   Because the link succeeded with IL2CPP's references to those externs, the P/Invoke iOS branch is
   now compiled and linked (still never run).

Excerpt: `ci-run-38920379246-excerpt.txt`.

## Not verified / gaps
- No device, no simulator run, no signing; HealthKit capability/entitlement and `NSHealthShareUsageDescription`
  are still not applied (docs/HEALTHKIT_SETUP.md steps 3–4).
- Adapter not composed; iOS players still get the D-108 unsupported state.
- The export branch carried Unity's iOS runtime static library (`libiPhone-lib.a`) inside the chunks in
  a public repository. **2026-10-16 (D-116):** the owner deleted `ios-export/grok2251-a`; the public
  transport is retired and the workflow is manual-only, needing a private transport. The result above
  (run 38920379246) stays valid.
