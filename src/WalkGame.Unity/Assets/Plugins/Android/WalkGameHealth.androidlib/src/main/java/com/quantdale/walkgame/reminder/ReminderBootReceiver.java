package com.quantdale.walkgame.reminder;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

/**
 * D-121: Android clears alarms on restart. When the phone finishes booting, this re-arms the
 * gentle reminder exactly as the game last armed it (same time and copy), and only if it was on
 * and notifications are still allowed. It does nothing else: no network, no wake lock, no
 * service, no exact alarm. RECEIVE_BOOT_COMPLETED is a normal install-time permission (no prompt).
 * Device behaviour UNVERIFIED (KI-002).
 */
public final class ReminderBootReceiver extends BroadcastReceiver {
    @Override
    public void onReceive(Context context, Intent intent) {
        if (intent == null || !Intent.ACTION_BOOT_COMPLETED.equals(intent.getAction())) return;
        try {
            ReminderBridge.rearmAfterBoot(context);
        } catch (RuntimeException ignored) {
            // A failed re-arm is harmless: the game re-syncs the reminder the next time it opens.
        }
    }
}
