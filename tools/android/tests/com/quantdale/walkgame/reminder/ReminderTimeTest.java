package com.quantdale.walkgame.reminder;

import java.util.Calendar;
import java.util.Random;
import java.util.TimeZone;

/**
 * D-123: seeded property/fuzz tests for the reminder time maths and the restart decision.
 * Dependency-free; exit code 1 on any failure. Fixed seed so a failure reproduces.
 */
public final class ReminderTimeTest {
    private static int failures = 0;
    private static int checks = 0;

    private static void check(boolean ok, String what) {
        checks++;
        if (!ok) { failures++; if (failures <= 20) System.out.println("FAIL: " + what); }
    }

    private static final long HOUR = 3_600_000L;
    private static final long DAY = 24 * HOUR;

    public static void main(String[] args) {
        String[] zones = TimeZone.getAvailableIDs();
        Random rnd = new Random(20261017L);
        long from = 946_684_800_000L;   // 2000-01-01Z
        long to = 2_208_988_800_000L;   // 2040-01-01Z
        int cases = 200_000;
        for (int i = 0; i < cases; i++) {
            TimeZone tz = TimeZone.getTimeZone(zones[rnd.nextInt(zones.length)]);
            long now = from + (long) (rnd.nextDouble() * (to - from));
            int m = rnd.nextInt(ReminderTime.MINUTES_PER_DAY);
            String c = tz.getID() + " now=" + now + " m=" + m;
            long t = ReminderTime.nextTriggerMillis(now, m, tz);
            check(t > now, "strictly in the future: " + c);
            check(t - now <= DAY + 3 * HOUR, "within a day (plus any DST shift): " + c);
            Calendar cal = Calendar.getInstance(tz);
            cal.setTimeInMillis(t);
            int got = cal.get(Calendar.HOUR_OF_DAY) * 60 + cal.get(Calendar.MINUTE);
            check(cal.get(Calendar.SECOND) == 0 && cal.get(Calendar.MILLISECOND) == 0, "on the minute: " + c);
            if (got != m) {
                // Only allowed when the wall-clock time does not exist that day (DST gap): the
                // offset must change within the few hours before the trigger.
                check(tz.getOffset(t) != tz.getOffset(t - 4 * HOUR), "wall time differs only in a DST gap: " + c + " got=" + got);
            }
            check(ReminderTime.nextTriggerMillis(t - 1, m, tz) == t, "idempotent just before the trigger: " + c);
            long after = ReminderTime.nextTriggerMillis(t, m, tz);
            check(after > t && after - t >= DAY - 3 * HOUR && after - t <= DAY + 3 * HOUR, "the next one is about a day later: " + c);
        }

        // Fixed DST cases (Europe/London spring forward 2026-03-29 01:00 UTC; fall back 2026-10-25).
        TimeZone london = TimeZone.getTimeZone("Europe/London");
        long springEve = 1_774_738_800_000L; // 2026-03-28T23:00Z = 23:00 GMT
        long t = ReminderTime.nextTriggerMillis(springEve, 60 + 30, london); // 01:30 does not exist on 03-29
        check(t > springEve && t - springEve < 4 * HOUR, "DST gap: reminder still arrives the same night/morning");
        long fallEve = 1_792_879_200_000L; // 2026-10-24T22:00Z = 23:00 BST
        long f = ReminderTime.nextTriggerMillis(fallEve, 8 * 60, london);
        check(f - fallEve == 10 * HOUR, "fall back: 08:00 GMT is 10 hours after 23:00 BST");

        // Bounds.
        for (int bad : new int[] { -1, ReminderTime.MINUTES_PER_DAY, Integer.MIN_VALUE, Integer.MAX_VALUE, 99_999 }) {
            boolean threw = false;
            try { ReminderTime.nextTriggerMillis(0L, bad, london); } catch (IllegalArgumentException e) { threw = true; }
            check(threw, "rejects minutes " + bad);
            check(!ReminderTime.validMinutes(bad), "invalid minutes " + bad);
        }
        boolean threw = false;
        try { ReminderTime.nextTriggerMillis(0L, 0, null); } catch (IllegalArgumentException e) { threw = true; }
        check(threw, "rejects a null zone");
        check(ReminderTime.validMinutes(0) && ReminderTime.validMinutes(ReminderTime.MINUTES_PER_DAY - 1), "edges valid");

        // Restart path: exhaustive over the boolean/nullness inputs, fuzzed over minutes.
        String[] texts = { null, "", "  ", "Millbrook" };
        for (int i = 0; i < 20_000; i++) {
            boolean armed = rnd.nextBoolean(), can = rnd.nextBoolean();
            int m = rnd.nextInt(4) == 0 ? rnd.nextInt() : rnd.nextInt(ReminderTime.MINUTES_PER_DAY);
            String title = texts[rnd.nextInt(texts.length)], body = texts[rnd.nextInt(texts.length)];
            boolean expect = armed && can && m >= 0 && m < 1440 && "Millbrook".equals(title) && "Millbrook".equals(body);
            check(ReminderTime.shouldRearm(armed, can, m, title, body) == expect,
                    "rearm armed=" + armed + " can=" + can + " m=" + m + " t=" + title + " b=" + body);
        }
        check(!ReminderTime.shouldRearm(false, true, 480, "a", "b"), "never re-arms what the game did not arm");
        check(!ReminderTime.shouldRearm(true, false, 480, "a", "b"), "never re-arms when notifications were turned off");
        check(ReminderTime.shouldRearm(true, true, 480, "a", "b"), "re-arms an armed reminder");

        System.out.println("ReminderTimeTest: " + (checks - failures) + "/" + checks + " checks passed (" + cases + " fuzz cases)");
        if (failures > 0) System.exit(1);
    }
}
