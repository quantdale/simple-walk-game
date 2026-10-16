package com.quantdale.walkgame.reminder;

import java.util.Calendar;
import java.util.TimeZone;

/**
 * D-123: Android-free reminder time maths, so it can be property-tested headless
 * (tools/android/test-bridge-logic.sh). Used by ReminderBridge.scheduleDaily and the restart path.
 */
public final class ReminderTime {
    public static final int MINUTES_PER_DAY = 24 * 60;

    private ReminderTime() {}

    public static boolean validMinutes(int minutesOfDay) {
        return minutesOfDay >= 0 && minutesOfDay < MINUTES_PER_DAY;
    }

    /**
     * The first instant strictly after nowMillis whose wall-clock time in zone is minutesOfDay.
     * If that wall-clock time does not exist on a day (a daylight-saving gap), the lenient calendar
     * moves it forward by the gap, so the reminder still arrives that day, slightly later.
     */
    public static long nextTriggerMillis(long nowMillis, int minutesOfDay, TimeZone zone) {
        if (!validMinutes(minutesOfDay)) throw new IllegalArgumentException("minutesOfDay out of range: " + minutesOfDay);
        if (zone == null) throw new IllegalArgumentException("zone required");
        Calendar day = Calendar.getInstance(zone);
        day.setTimeInMillis(nowMillis);
        for (int i = 0; i < 3; i++) {
            Calendar next = (Calendar) day.clone();
            next.set(Calendar.HOUR_OF_DAY, minutesOfDay / 60);
            next.set(Calendar.MINUTE, minutesOfDay % 60);
            next.set(Calendar.SECOND, 0);
            next.set(Calendar.MILLISECOND, 0);
            long t = next.getTimeInMillis();
            if (t > nowMillis) return t;
            day.add(Calendar.DAY_OF_YEAR, 1);
        }
        throw new IllegalStateException("no trigger within three calendar days");
    }

    /** D-121 restart path: re-arm only what the game armed, only while notifications are allowed. */
    public static boolean shouldRearm(boolean armed, boolean canNotify, int minutesOfDay, String title, String body) {
        return armed && canNotify && validMinutes(minutesOfDay)
                && title != null && !title.trim().isEmpty() && body != null && !body.trim().isEmpty();
    }
}
