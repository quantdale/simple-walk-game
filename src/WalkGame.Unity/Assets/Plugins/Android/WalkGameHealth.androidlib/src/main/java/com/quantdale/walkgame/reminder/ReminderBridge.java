package com.quantdale.walkgame.reminder;

import android.app.Activity;
import android.app.AlarmManager;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.os.Build;
import java.util.TimeZone;

/**
 * D-119: optional gentle daily reminder. Inexact repeating RTC alarm (never wakes the device,
 * batched by the OS), local only, no network. Called from AndroidReminderScheduler.cs.
 * Device behaviour UNVERIFIED (KI-002).
 */
public final class ReminderBridge {
    static final String PREFS = "wg_reminder";
    private static final int REQUEST_CODE = 4119;
    private static final String POST_NOTIFICATIONS = "android.permission.POST_NOTIFICATIONS";

    private ReminderBridge() {}

    public static boolean canNotify(Context context) {
        if (Build.VERSION.SDK_INT >= 33
                && context.checkSelfPermission(POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED) {
            return false;
        }
        NotificationManager nm = (NotificationManager) context.getSystemService(Context.NOTIFICATION_SERVICE);
        return nm != null && nm.areNotificationsEnabled();
    }

    public static void requestPermission(Activity activity) {
        if (Build.VERSION.SDK_INT >= 33) {
            activity.requestPermissions(new String[] { POST_NOTIFICATIONS }, REQUEST_CODE);
        }
    }

    private static PendingIntent pending(Context context) {
        Intent intent = new Intent(context, ReminderReceiver.class);
        return PendingIntent.getBroadcast(context, REQUEST_CODE, intent,
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
    }

    public static boolean scheduleDaily(Context context, int minutesOfDay, String title, String body) {
        if (!ReminderTime.validMinutes(minutesOfDay)) return false;
        AlarmManager am = (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);
        if (am == null) return false;
        // D-121: remember what is armed so ReminderBootReceiver can re-arm it after a restart.
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit()
                .putString("title", title).putString("body", body)
                .putInt("minutes", minutesOfDay).putBoolean("armed", true).apply();
        // D-123: the trigger maths is pure and property-tested headless (ReminderTimeTest).
        long next = ReminderTime.nextTriggerMillis(System.currentTimeMillis(), minutesOfDay, TimeZone.getDefault());
        PendingIntent pi = pending(context);
        am.cancel(pi);
        am.setInexactRepeating(AlarmManager.RTC, next, AlarmManager.INTERVAL_DAY, pi);
        return true;
    }

    /**
     * D-121: after a phone restart, re-arm the same inexact alarm only if the game had armed one
     * and notifications are still allowed. One AlarmManager call, no network, no wake lock.
     */
    static boolean rearmAfterBoot(Context context) {
        android.content.SharedPreferences p = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
        boolean armed = p.getBoolean("armed", false);
        if (!armed) return false;
        int minutes = p.getInt("minutes", -1);
        String title = p.getString("title", null);
        String body = p.getString("body", null);
        if (!ReminderTime.shouldRearm(armed, canNotify(context), minutes, title, body)) return false;
        return scheduleDaily(context, minutes, title, body);
    }

    public static void cancel(Context context) {
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit().putBoolean("armed", false).apply();
        AlarmManager am = (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);
        if (am != null) am.cancel(pending(context));
        NotificationManager nm = (NotificationManager) context.getSystemService(Context.NOTIFICATION_SERVICE);
        if (nm != null) nm.cancel(ReminderReceiver.NOTIFICATION_ID);
    }
}
