# HealthKit setup for an iOS build (KI-022, D-112)

**Status: UNVERIFIED.** Nothing here has been built or run. There is no macOS/Xcode host on the
agent box and no iOS device. This page lists what a human with a Mac must do and check. No claim is
made that an iOS build works.

## What exists

| Part | Path | Verification |
|---|---|---|
| C# adapter (both seams) | `src/WalkGame.Infrastructure/Platform/HealthKit/HealthKitActivityAdapter.cs` | automated-verified (headless, `HealthKitActivityAdapterTests`, fake bridge) |
| String protocol | `src/WalkGame.Infrastructure/Platform/HealthKit/IHealthKitBridge.cs` | documented; pinned by the same tests |
| P/Invoke bridge | `src/WalkGame.Unity/Assets/Shell/IosHealthKitBridge.cs` | compiles in the editor (stub branch only); the iOS branch is UNVERIFIED |
| Swift bridge | `src/WalkGame.Unity/Assets/Plugins/iOS/WalkGameHealthKitBridge.swift` | UNVERIFIED (never compiled) |
| Composition | none: `ShellBootstrap` does not use the adapter yet | iOS players still get the D-108 unsupported state |

## Steps for the first macOS build

1. Unity: install the iOS Build Support module for 6000.3.26f1. Do not commit `ProjectSettings/ProjectSettings.asset` changes made by switching platform; record any needed setting in a D-entry instead.
2. Build the Xcode project. Confirm `WalkGameHealthKitBridge.swift` is in `UnityFramework` and that the plugin importer linked `HealthKit.framework`. Unity Swift plugins need a bridging setup; if the `@_cdecl` symbols are not exported, fix it there and record the change.
3. **Capability and entitlement** (UnityFramework is not the app target; set these on `Unity-iPhone`):
   - Signing & Capabilities → add **HealthKit**. This writes `com.apple.developer.healthkit = true` to the entitlements file.
   - Leave "Clinical Health Records" and "Background Delivery" off. The game reads on boot and resume only (D-107), so no `com.apple.developer.healthkit.background-delivery`.
4. **Info.plist usage strings**:
   - `NSHealthShareUsageDescription` (required for reads). Proposed text: "Simple Walk Game reads your step count so your walks can restore the valley. It never writes to Health and nothing leaves your phone."
   - `NSHealthUpdateUsageDescription`: not needed. The bridge requests no write types. Do not add it.
   - Check the text against `docs/PRIVACY_REVIEW.md` and the no-guilt copy rules (UX_DESIGN §5).
5. Wire the composition (a separate PR with its own D-entry): in `ShellBootstrap.Start`, when `IosHealthKitBridge.IsIosPlayer`, build `HealthKitActivityAdapter(new IosHealthKitBridge(), ...)` and pass it as source and port with `IosHealthKitBridge.RequestAuthorization` as the permission request. Update `PlatformParityTests` (D-108) at the same time: they pin today's unsupported state.
6. Shell copy: HealthKit never says whether read access was granted. The adapter reports Granted after the dialog was answered, but `ReadAccessConfirmed` stays false until a read returns a sample. The shell must not say "connected" on the grant alone (contract item 5 in `docs/PLATFORM_PARITY.md`). This copy does not exist yet.

## Device checks (all UNVERIFIED today)

- The dialog shows only "Steps" under read; no write rows.
- Deny all, then grant in Settings → Health → Data Access: steps appear after the next resume.
- Steps from iPhone and Apple Watch: HealthKit returns both as separate samples. The pipeline does not merge overlapping sources. Check for double counting before release (open question).
- Delete a sample in the Health app: Vitality reverses once on the next resume.
- Reinstall: the anchor and the interval maps live in `UserDefaults` and are lost; the full re-read replays as no-ops, and deletions of samples credited before the reinstall are not seen (known limitation).
- Fractional step quantities are floored by the bridge.
