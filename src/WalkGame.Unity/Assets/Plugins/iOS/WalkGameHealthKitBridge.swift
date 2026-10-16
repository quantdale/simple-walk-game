// WalkGameHealthKitBridge.swift — native half of the HealthKit seam (KI-022, D-112).
//
// STATUS: UNVERIFIED. This file has never been compiled (no macOS/Xcode host) or run on a device.
// It speaks the string protocol documented in IHealthKitBridge.cs; every mapping rule on the C#
// side is tested headless with a fake bridge. Read-only: no HealthKit write types are requested.
//
// Exposed to C# through @_cdecl C symbols (DllImport("__Internal") in IosHealthKitBridge.cs).
// Returned strings are strdup'ed; the Mono/IL2CPP marshaller frees them.

import Foundation
import HealthKit

private let store = HKHealthStore()
private let stepType = HKQuantityType.quantityType(forIdentifier: .stepCount)!
private let readTimeoutSeconds: Double = 10
private let anchorKey = "wg.healthkit.anchor"
private let intervalsKey = "wg.healthkit.intervals" // uuid -> "startMs\tendMs" for samples already returned
private let deletedKey = "wg.healthkit.deleted"     // uuid -> "startMs\tendMs" for returned samples later deleted

private func cstr(_ s: String) -> UnsafeMutablePointer<CChar>? { return strdup(s) }

@_cdecl("wg_hk_availability")
public func wg_hk_availability() -> UnsafeMutablePointer<CChar>? {
    return cstr(HKHealthStore.isHealthDataAvailable() ? "available" : "unsupported")
}

@_cdecl("wg_hk_request_status")
public func wg_hk_request_status() -> UnsafeMutablePointer<CChar>? {
    guard HKHealthStore.isHealthDataAvailable() else { return cstr("unknown") }
    var result = "unknown"
    let done = DispatchSemaphore(value: 0)
    store.getRequestStatusForAuthorization(toShare: [], read: [stepType]) { status, error in
        if error == nil {
            switch status {
            case .shouldRequest: result = "should_request"
            case .unnecessary: result = "unnecessary"
            default: result = "unknown"
            }
        }
        done.signal()
    }
    if done.wait(timeout: .now() + readTimeoutSeconds) == .timedOut { return cstr("unknown") }
    return cstr(result)
}

/// Shows the HealthKit sheet (read: stepCount only). Fire-and-forget; the game re-reads status on resume.
@_cdecl("wg_hk_request_authorization")
public func wg_hk_request_authorization() {
    guard HKHealthStore.isHealthDataAvailable() else { return }
    store.requestAuthorization(toShare: nil, read: [stepType]) { _, _ in }
}

private func loadAnchor() -> HKQueryAnchor? {
    guard let data = UserDefaults.standard.data(forKey: anchorKey) else { return nil }
    return try? NSKeyedUnarchiver.unarchivedObject(ofClass: HKQueryAnchor.self, from: data)
}

private func saveAnchor(_ anchor: HKQueryAnchor?) {
    guard let anchor = anchor,
          let data = try? NSKeyedArchiver.archivedData(withRootObject: anchor, requiringSecureCoding: true) else { return }
    UserDefaults.standard.set(data, forKey: anchorKey)
}

// D-114 source field, mirroring HealthKitSourceDedup.Classify (C#, headless-tested): only Apple's
// own collectors (bundle "com.apple.*") count as watch/phone, by HKDevice.model prefix.
private func sourceKind(_ s: HKSample) -> String {
    let bundle = s.sourceRevision.source.bundleIdentifier
    let model = s.device?.model ?? ""
    guard bundle.hasPrefix("com.apple.") else { return "other" }
    if model.hasPrefix("Watch") { return "watch" }
    if model.hasPrefix("iPhone") { return "phone" }
    return "other"
}

private func ms(_ d: Date) -> Int64 { return Int64((d.timeIntervalSince1970 * 1000).rounded(.down)) }

// D-113 pruning, mirroring HealthKitSampleLedger (C#, headless-tested): drop entries that ended
// more than 30 + 7 days ago (never inside a read window again), then cap each map at 50,000 by
// evicting the oldest end times (ties by UUID). UNVERIFIED like the rest of this file.
private let retentionMs: Int64 = Int64(37) * 86_400_000
private let maxEntriesPerMap = 50_000
private func prune(_ map: [String: String], nowMs: Int64) -> [String: String] {
    let cutoff = nowMs - retentionMs
    var kept: [(String, String, Int64)] = []
    for (id, iv) in map {
        let parts = iv.split(separator: "\t")
        guard parts.count == 2, let e = Int64(parts[1]) else { continue } // unreadable entry: drop
        if e >= cutoff { kept.append((id, iv, e)) }
    }
    if kept.count > maxEntriesPerMap {
        kept.sort { $0.2 != $1.2 ? $0.2 < $1.2 : $0.0 < $1.0 }
        kept.removeFirst(kept.count - maxEntriesPerMap)
    }
    var out: [String: String] = [:]
    for (id, iv, _) in kept { out[id] = iv }
    return out
}

/// Reads stepCount samples with an anchored query restricted to [start, end). New samples become
/// credit lines; deleted objects become `del` lines using the interval remembered when the sample
/// was first returned (HKDeletedObject has only a UUID). If the anchor cannot be used, the bridge
/// drops it and re-reads the whole window; the C# pipeline makes replays exactly-once no-ops.
/// Every sample still in the window is re-emitted on each read (like the Android bridge), so a
/// fresh install or a lost anchor never loses credit.
@_cdecl("wg_hk_read_steps")
public func wg_hk_read_steps(_ startMs: Int64, _ endMs: Int64) -> UnsafeMutablePointer<CChar>? {
    guard HKHealthStore.isHealthDataAvailable() else { return cstr("error:unavailable") }
    let start = Date(timeIntervalSince1970: Double(startMs) / 1000)
    let end = Date(timeIntervalSince1970: Double(endMs) / 1000)
    let predicate = HKQuery.predicateForSamples(withStart: start, end: end, options: [.strictStartDate, .strictEndDate])

    var intervals = (UserDefaults.standard.dictionary(forKey: intervalsKey) as? [String: String]) ?? [:]
    var lines: [String] = []
    var failure: String? = nil
    let done = DispatchSemaphore(value: 0)

    // 1. Current samples in the window (full re-emit; idempotent downstream).
    let sampleQuery = HKSampleQuery(sampleType: stepType, predicate: predicate, limit: HKObjectQueryNoLimit, sortDescriptors: nil) { _, samples, error in
        if let error = error as? HKError {
            failure = error.code == .errorAuthorizationDenied || error.code == .errorAuthorizationNotDetermined ? "error:permission" : "error:io"
        } else if error != nil {
            failure = "error:io"
        } else {
            for case let s as HKQuantitySample in samples ?? [] {
                let count = Int64(s.quantity.doubleValue(for: HKUnit.count()).rounded(.down))
                var offset = ""
                if let tzName = s.metadata?[HKMetadataKeyTimeZone] as? String, let tz = TimeZone(identifier: tzName) {
                    offset = String(tz.secondsFromGMT(for: s.endDate))
                }
                let id = s.uuid.uuidString
                lines.append("\(id)\t\(ms(s.startDate))\t\(ms(s.endDate))\t\(count)\t\(offset)\t\(sourceKind(s))")
                intervals[id] = "\(ms(s.startDate))\t\(ms(s.endDate))"
            }
        }
        done.signal()
    }
    store.execute(sampleQuery)
    if done.wait(timeout: .now() + readTimeoutSeconds) == .timedOut { store.stop(sampleQuery); return cstr("error:timeout") }
    if let f = failure { return cstr(f) }

    // 2. Deletions since the stored anchor. A nil/unusable anchor means "from the beginning".
    let deletedDone = DispatchSemaphore(value: 0)
    var deletedIds: [String] = []
    var newAnchor: HKQueryAnchor? = nil
    let anchored = HKAnchoredObjectQuery(type: stepType, predicate: nil, anchor: loadAnchor(), limit: HKObjectQueryNoLimit) { _, _, deleted, anchor, error in
        if error != nil { UserDefaults.standard.removeObject(forKey: anchorKey) } // lost anchor: next read starts over
        else { deletedIds = (deleted ?? []).map { $0.uuid.uuidString }; newAnchor = anchor }
        deletedDone.signal()
    }
    store.execute(anchored)
    if deletedDone.wait(timeout: .now() + readTimeoutSeconds) == .timedOut { store.stop(anchored); return cstr("error:timeout") }

    // Like the Android bridge (D-069), every known deletion is re-emitted on each read while its
    // interval is inside the window, so a failed ingest never loses one; the pipeline's
    // stale-revision rule makes it exactly-once.
    var deleted = (UserDefaults.standard.dictionary(forKey: deletedKey) as? [String: String]) ?? [:]
    for id in deletedIds { if let iv = intervals[id] { deleted[id] = iv; intervals[id] = nil } }
    for (id, iv) in deleted.sorted(by: { $0.key < $1.key }) {
        let parts = iv.split(separator: "\t")
        guard parts.count == 2, let s0 = Int64(parts[0]), let e0 = Int64(parts[1]), s0 >= startMs, e0 <= endMs else { continue }
        lines.append("del\t\(id)\t\(iv)")
    }
    // D-113: prune both maps (mirrors HealthKitSampleLedger.Prune, which is the tested rule).
    let nowMs = ms(Date())
    deleted = prune(deleted, nowMs: nowMs)
    intervals = prune(intervals, nowMs: nowMs)
    UserDefaults.standard.set(deleted, forKey: deletedKey)
    UserDefaults.standard.set(intervals, forKey: intervalsKey)
    saveAnchor(newAnchor)
    return cstr((["ok"] + lines).joined(separator: "\n"))
}
