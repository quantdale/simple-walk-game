import UIKit

// D-186: reports the system "Reduce Motion" setting to OsMotionPreference.cs (IosOsMotionPreference).
// Read-only; called from Unity's main thread. Device behaviour UNVERIFIED (KI-002).
@_cdecl("wg_reduce_motion_enabled")
public func wg_reduce_motion_enabled() -> Int32 {
    return UIAccessibility.isReduceMotionEnabled ? 1 : 0
}
