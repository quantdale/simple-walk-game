package com.quantdale.walkgame.health;

import java.util.Iterator;
import java.util.Map;
import java.util.Set;

/**
 * Pure (Android-free) recovery after an expired Health Connect change token (KI-016(a)).
 *
 * The change log can no longer say what was deleted during the gap, so the bridge re-reads the
 * full span its index covers and hands the ids it saw here. Every indexed record whose interval
 * lies inside that span but which is no longer present was deleted during the gap: it moves to
 * the deleted set exactly like a change-log deletion (D-069). Re-emission is idempotent because
 * the pipeline's stale-revision rule, not this class, enforces exactly-once.
 *
 * Kept free of android.* so it is unit-tested headlessly (tools/android/test-bridge-logic.sh).
 */
final class GapReconciler {
    private GapReconciler() {}

    /** The [minStart, maxEnd] span of the known index, or null when the index is empty. */
    static long[] span(Map<String, long[]> known) {
        if (known.isEmpty()) return null;
        long min = Long.MAX_VALUE, max = Long.MIN_VALUE;
        for (long[] v : known.values()) {
            if (v[0] < min) min = v[0];
            if (v[1] > max) max = v[1];
        }
        return new long[] { min, max };
    }

    /**
     * Moves every known record fully inside [rangeStart, rangeEnd] that is absent from
     * {@code present} into {@code deleted} with deletedAt = {@code nowMs}. Records outside the
     * re-read range are left alone (absence there proves nothing). Returns how many moved.
     */
    static int reconcile(Map<String, long[]> known, Map<String, long[]> deleted, Set<String> present,
                         long rangeStart, long rangeEnd, long nowMs) {
        if (rangeEnd < rangeStart) return 0;
        int moved = 0;
        for (Iterator<Map.Entry<String, long[]>> it = known.entrySet().iterator(); it.hasNext(); ) {
            Map.Entry<String, long[]> e = it.next();
            long[] v = e.getValue();
            if (v[0] < rangeStart || v[1] > rangeEnd || present.contains(e.getKey())) continue;
            if (!deleted.containsKey(e.getKey()))
                deleted.put(e.getKey(), new long[] { v[0], v[1], v[2], nowMs });
            it.remove();
            moved++;
        }
        return moved;
    }
}
