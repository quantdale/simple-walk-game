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
| Seen-samples pruning (D-113) | `src/WalkGame.Infrastructure/Platform/HealthKit/HealthKitSampleLedger.cs`, mirrored in the Swift bridge | C# rule automated-verified (`HealthKitPrepTests`); Swift mirror UNVERIFIED |
| iPhone/Watch dedup design (D-113) | `src/WalkGame.Infrastructure/Platform/HealthKit/HealthKitSourceDedup.cs` | automated-verified (headless); NOT wired (the bridge protocol has no source field yet) |
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
- Steps from iPhone and Apple Watch: HealthKit returns both as separate samples, and today's pipeline would double count them. D-113 designs the fix (`HealthKitSourceDedup`: Watch > iPhone > other, the lower-priority sample keeps only its steps outside time already covered, prorated and floored) but it is not wired. Wiring needs a sixth bridge field (source kind from `HKDevice.model` + `HKSource.bundleIdentifier`) and its own D-entry. Check on a device with both before release.
- Delete a sample in the Health app: Vitality reverses once on the next resume.
- Reinstall: see "UserDefaults loss on reinstall" below.
- Fractional step quantities are floored by the bridge.

## UserDefaults loss on reinstall (D-113)

**Label: documented reasoning, UNVERIFIED on a device.** The bridge keeps three keys in
`UserDefaults.standard`: `wg.healthkit.anchor`, `wg.healthkit.intervals`, `wg.healthkit.deleted`.
The game save lives under `Application.persistentDataPath` (Documents on iOS).

| Event | Save | UserDefaults | Effect |
|---|---|---|---|
| Delete the app, reinstall | gone | gone | Fresh game. Consistent; nothing to reconcile. |
| Restore the device from a backup | restored | restored | Both come from the same snapshot. Samples added or deleted after the backup are read on the next resume (full re-emit of the 30-day window). |
| Save kept, UserDefaults lost (e.g. a future cloud save, or a partial restore) | kept | gone | Credits: the full re-read re-emits every sample in the window; the save's dedup ledger makes them exactly-once no-ops (pinned by `HealthKitActivityAdapterTests`, credit once across a restart). Deletions: a sample credited before the loss and deleted after it has no remembered interval, so no `del` line is emitted and its credit stays (pinned by `HealthKitPrepTests`, deletion of an unknown UUID is a no-op). Never a double credit; at worst a kept credit. |

Pruning (D-113) has the same failure direction: an entry is dropped only after it ended more than
30 + 7 days ago, so it can never be inside a read window again, unless the device clock was moved
back by more than 7 days, where a deletion may be missed (credit kept). Each map is capped at
50,000 entries, oldest end time evicted first.
