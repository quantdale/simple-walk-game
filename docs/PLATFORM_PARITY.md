# Platform parity: Android Health Connect vs iOS HealthKit (D-108)

Status as of 2026-10-16, main `33a72cf`. Labels follow TESTING_AND_RELEASE: **automated-verified**
(headless or EditMode test), **compiled** (APK build), **UNVERIFIED** (no device, KI-002).

## Summary

The ingestion seam is platform-neutral (`IActivityRecordSource`, `IActivityConnectionPort`, D-034/D-043),
but **only Android has a composed provider adapter**. Since D-112 a HealthKit adapter exists in code:
`HealthKitActivityAdapter` (automated-verified headless with a fake bridge), the `IosHealthKitBridge`
P/Invoke class and a Swift bridge (`Assets/Plugins/iOS/WalkGameHealthKitBridge.swift`, **compiled on hosted
macOS** by CI, never run or device-tested). It is NOT composed into `ShellBootstrap`, and no iOS build exists. The entitlement and
`NSHealthShareUsageDescription` steps are documented in [`HEALTHKIT_SETUP.md`](HEALTHKIT_SETUP.md) but not
applied. This gap is still tracked as **KI-022**.
ROADMAP M7 ("HealthKit integration or current approved equivalent") stays unchecked.

What an iOS (or any non-Android) **release** player does today is honest and playable, and is now
pinned by `PlatformParityTests` (EditMode, D-108): the source is reported as unsupported, the player
sees "This device has no walking data source we can read. Your progress is safe.", no Connect action
is offered (no dead end), onboarding completes, and no development walking is exposed.

## Capability matrix

| Capability | Android (Health Connect) | iOS (HealthKit) |
|---|---|---|
| Composition | `ShellBootstrap` uses `HealthConnectActivityAdapter` + `AndroidHealthConnectBridge` (D-068) | `ShellConnectionPort(false)`: Unsupported, no live source. `HealthKitActivityAdapter` exists but is not composed (D-112) |
| Adapter mapping, failure policy, deletions, read-access honesty | automated-verified | automated-verified headless on a fake bridge (D-112); Swift bridge UNVERIFIED |
| Availability states (unsupported / update required / available) | automated-verified (adapter tests, D-093) | Unsupported only (automated-verified, D-108) |
| Permission request + rationale | wired (`activity:connect`); device dialog UNVERIFIED | not applicable; no Connect action (automated-verified) |
| Step reads on boot/resume, 30-day bounded window | automated-verified (D-107 budget tests on the fake bridge); device UNVERIFIED | none |
| Deletions (change log) | automated-verified at contract level (D-069) | none |
| Expired change-token recovery | automated-verified, Java headless (D-077) | none |
| Local date per record (days walked, Today) | `endZoneOffset`, compiled in APK (D-103); documented non-null on the framework API; if absent, the device zone's offset at the record's end (D-157, headless-tested); device UNVERIFIED | `HKMetadataKeyTimeZone` when the sample is tagged, else the same device-zone fallback (D-157, adapter seam headless-tested, not composed) |
| Sub-unit step carry | provider-keyed (D-079), would apply unchanged | would apply unchanged |
| Background sync | none (KI-016c), by design for battery | none |
| OS versions | Android 14+ (9–13 report unsupported, KI-016b) | n/a |
| Save encryption | OS FBE (D-067) | OS Data Protection (D-067), UNVERIFIED |

## What a HealthKit adapter must match (contract for a future slice)

1. Implement both seams in Infrastructure, mirroring `HealthConnectActivityAdapter`: report platform truth
   only, never compute Vitality; fail a fetch as a whole (no partial credit, D-063).
2. Provider namespace distinct from `android.health-connect` (for example `ios.healthkit`) so dedup,
   step carry and deletion bookkeeping stay per provider.
3. Steps from `HKQuantityTypeIdentifierStepCount`; record id = sample UUID within the existing id bounds;
   deletions via `HKAnchoredObjectQuery` deleted objects through the same idempotent deletion path;
   a lost anchor triggers a full re-read of the indexed span (same rule as D-077).
4. Local date from the sample's time zone metadata when present, else the device zone (D-103 rules,
   lower-bound honesty).
5. Reads only on boot and resume over `ShellController.SyncWindowDays` (D-107 budget tests must pass
   with the iOS bridge fake). HealthKit read authorisation is not observable; the adapter must treat
   "no samples" as possibly denied and never claim Connected without data.
6. Privacy: read-only scope, no write types, usage string reviewed against PRIVACY_REVIEW.

Building it needs a macOS host with Xcode (not on this box) and a device for verification, so it is
outside what agents can verify here.
