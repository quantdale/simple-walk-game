package com.quantdale.walkgame.health;

import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.Set;

/** Dependency-free tests for GapReconciler (KI-016(a)). Exit code 1 on any failure. */
public final class GapReconcilerTest {
    private static int failures = 0;

    private static void check(boolean ok, String what) {
        if (!ok) { failures++; System.out.println("FAIL: " + what); } else System.out.println("PASS: " + what);
    }

    private static Map<String, long[]> known() {
        Map<String, long[]> k = new LinkedHashMap<>();
        k.put("a", new long[] { 100, 200, 210 });
        k.put("b", new long[] { 300, 400, 410 });
        k.put("c", new long[] { 500, 600, 610 });
        return k;
    }

    private static Set<String> set(String... ids) { Set<String> s = new HashSet<>(); for (String i : ids) s.add(i); return s; }

    public static void main(String[] args) {
        // span
        check(GapReconciler.span(new LinkedHashMap<>()) == null, "empty index has no span");
        long[] sp = GapReconciler.span(known());
        check(sp[0] == 100 && sp[1] == 600, "span covers min start to max end");

        // a record deleted during the gap moves to deleted with original interval + mod, deletedAt = now
        Map<String, long[]> k = known(); Map<String, long[]> d = new LinkedHashMap<>();
        int moved = GapReconciler.reconcile(k, d, set("a", "c"), 100, 600, 9999);
        check(moved == 1 && !k.containsKey("b") && d.containsKey("b"), "absent record b is reported deleted");
        long[] b = d.get("b");
        check(b[0] == 300 && b[1] == 400 && b[2] == 410 && b[3] == 9999, "deleted entry keeps interval and lastModified");
        check(k.containsKey("a") && k.containsKey("c"), "present records stay known");

        // idempotent: a second reconcile with the same presence changes nothing
        int again = GapReconciler.reconcile(k, d, set("a", "c"), 100, 600, 12345);
        check(again == 0 && d.get("b")[3] == 9999, "second reconcile is a no-op (no duplicate, deletedAt unchanged)");

        // records outside the re-read range are never inferred deleted
        k = known(); d = new LinkedHashMap<>();
        moved = GapReconciler.reconcile(k, d, set(), 250, 450, 1);
        check(moved == 1 && d.containsKey("b") && k.containsKey("a") && k.containsKey("c"), "only records inside the range can be inferred deleted");

        // an already-deleted entry is not overwritten
        k = known(); d = new LinkedHashMap<>();
        d.put("b", new long[] { 300, 400, 410, 777 });
        GapReconciler.reconcile(k, d, set("a", "c"), 100, 600, 9999);
        check(d.get("b")[3] == 777, "existing change-log deletion time is preserved");

        // inverted range is rejected
        k = known(); d = new LinkedHashMap<>();
        check(GapReconciler.reconcile(k, d, set(), 600, 100, 1) == 0 && k.size() == 3, "inverted range never deletes");

        // everything present: nothing moves
        k = known(); d = new LinkedHashMap<>();
        check(GapReconciler.reconcile(k, d, set("a", "b", "c"), 100, 600, 1) == 0 && d.isEmpty(), "full presence deletes nothing");

        System.out.println(failures == 0 ? "GAP RECONCILER: all checks passed" : "GAP RECONCILER: " + failures + " failed");
        System.exit(failures == 0 ? 0 : 1);
    }
}
