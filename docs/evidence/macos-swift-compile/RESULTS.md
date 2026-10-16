# Swift HealthKit bridge — compiled on hosted macOS (grok2230)

**Label: compiled on hosted macOS. NOT device-tested, NOT linked into an app, never run.**

## Cost check (no paid costs)
- Repository `quantdale/simple-walk-game` is **public** (GitHub API `visibility: public`, checked 2026-10-16).
- GitHub docs, "GitHub Actions billing" (docs.github.com/en/billing/concepts/product-billing/github-actions):
  "GitHub Actions usage is free for self-hosted runners and for public repositories that use standard
  GitHub-hosted runners" and "Larger runners are always charged for". `macos-latest` is a standard runner.
- The job uploads no artifacts and uses no cache (no storage use). Logs do not count toward storage.

## Workflow
`.github/workflows/ios-bridge-compile.yml`, job `swift-healthkit-bridge` (path-filtered to
`Assets/Plugins/iOS/**` and the workflow; also `workflow_dispatch`).
1. `xcrun --sdk iphoneos swiftc -typecheck -parse-as-library -target arm64-apple-ios15.0 *.swift`
2. `xcrun --sdk iphoneos swiftc -c -parse-as-library -O ... -o bridge.o`, then `nm -gU` must list the
   four `@_cdecl` symbols that `IosHealthKitBridge.cs` imports.

## First run (PR #92, run 38916443484, 2026-10-16 22:31 PHT)
- Runner image `macos-26-arm64` 20261013.0424.1; Xcode 26.6; Apple Swift 6.3.3.
- Type-check: PASS, no diagnostics. Object compile: PASS, no diagnostics (the only warning in the log is
  from the `swiftc --version` step: "using sysroot for 'iPhoneOS' but targeting 'MacOSX'", harmless).
- Exported symbols: `_wg_hk_availability`, `_wg_hk_read_steps`, `_wg_hk_request_authorization`,
  `_wg_hk_request_status` (all `T`).
- No compile errors, so no source fix was needed.

## Not covered
Unity iOS export, Xcode project/UnityFramework link, entitlements, Swift/Unity bridging header, runtime
HealthKit behaviour, device. The P/Invoke iOS branch of `IosHealthKitBridge.cs` is still UNVERIFIED.
