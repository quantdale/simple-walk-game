package com.quantdale.walkgame.reminder;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.os.Build;

/** D-119: posts the calm reminder on a low-importance channel (no sound, no vibration). */
public final class ReminderReceiver extends BroadcastReceiver {
    static final int NOTIFICATION_ID = 4119;
    private static final String CHANNEL = "wg_gentle_reminder";

    @Override
    public void onReceive(Context context, Intent intent) {
        if (!ReminderBridge.canNotify(context)) return;
        NotificationManager nm = (NotificationManager) context.getSystemService(Context.NOTIFICATION_SERVICE);
        if (nm == null) return;
        SharedPreferences p = context.getSharedPreferences(ReminderBridge.PREFS, Context.MODE_PRIVATE);
        String title = p.getString("title", "Millbrook");
        String body = p.getString("body", "The valley is here whenever you feel like a walk.");
        Notification.Builder b;
        if (Build.VERSION.SDK_INT >= 26) {
            NotificationChannel ch = new NotificationChannel(CHANNEL, "Gentle reminder", NotificationManager.IMPORTANCE_LOW);
            ch.setDescription("An optional daily invitation to walk. Off unless you turn it on in the game.");
            nm.createNotificationChannel(ch);
            b = new Notification.Builder(context, CHANNEL);
        } else {
            b = new Notification.Builder(context);
        }
        Intent launch = context.getPackageManager().getLaunchIntentForPackage(context.getPackageName());
        if (launch != null) {
            b.setContentIntent(PendingIntent.getActivity(context, NOTIFICATION_ID, launch,
                    PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE));
        }
        b.setSmallIcon(context.getApplicationInfo().icon)
                .setContentTitle(title)
                .setContentText(body)
                .setAutoCancel(true);
        nm.notify(NOTIFICATION_ID, b.build());
    }
}
