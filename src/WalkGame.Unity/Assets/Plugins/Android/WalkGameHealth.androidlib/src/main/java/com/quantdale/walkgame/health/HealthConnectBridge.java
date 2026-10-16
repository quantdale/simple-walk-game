package com.quantdale.walkgame.health;

import android.app.Activity;
import android.content.Context;
import android.content.SharedPreferences;
import android.content.pm.PackageManager;
import android.os.Build;
import android.os.OutcomeReceiver;

import java.time.Instant;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.Executor;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;

/**
 * Native half of the Health Connect adapter (D-068). Speaks the string protocol documented on
 * WalkGame.Infrastructure.Platform.HealthConnect.IHealthConnectBridge. Uses the Android 14+
 * framework API (android.health.connect), so no Maven dependencies are needed; older devices
 * report "unsupported". Reads steps only. Never computes game values.
 *
 * Device behaviour is UNVERIFIED (KI-002): compiled into the Unity Android build only.
 */
public final class HealthConnectBridge {
    public static final String READ_STEPS = "android.permission.health.READ_STEPS";
    private static final String PREFS = "wg_health_connect";
    private static final int REQUEST_CODE = 7101;
    private static final long READ_TIMEOUT_SECONDS = 20;
    private static final int PAGE_SIZE = 1000;
    private static final int MAX_PAGES = 50;
    private static final Executor EXECUTOR = Executors.newSingleThreadExecutor();

    private HealthConnectBridge() {}

    public static String getSdkStatus(Context ctx) {
        if (Build.VERSION.SDK_INT < 34) return "unsupported";
        try {
            return ctx.getSystemService(android.health.connect.HealthConnectManager.class) != null ? "available" : "unsupported";
        } catch (Throwable t) {
            return "unsupported";
        }
    }

    public static String getPermissionStatus(Context ctx) {
        SharedPreferences p = ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
        if (ctx.checkSelfPermission(READ_STEPS) == PackageManager.PERMISSION_GRANTED) {
            p.edit().putBoolean("everGranted", true).apply();
            return "granted";
        }
        if (p.getBoolean("everGranted", false)) return "revoked";
        if (p.getBoolean("requested", false)) return "denied";
        return "not_requested";
    }

    /** Shows the system permission dialog. The result is observed by the next getPermissionStatus. */
    public static void requestPermission(Activity activity) {
        if (Build.VERSION.SDK_INT < 34) return;
        activity.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit().putBoolean("requested", true).apply();
        activity.requestPermissions(new String[] { READ_STEPS }, REQUEST_CODE);
    }

    /** Blocking read (call off the Android UI thread). */
    public static String readSteps(Context ctx, long startMs, long endMs) {
        if (Build.VERSION.SDK_INT < 34) return "error:unavailable";
        if (ctx.checkSelfPermission(READ_STEPS) != PackageManager.PERMISSION_GRANTED) return "error:permission";
        try {
            return Api34.read(ctx, startMs, endMs);
        } catch (SecurityException e) {
            return "error:permission";
        } catch (Throwable t) {
            return "error:io";
        }
    }

    /** Isolated so the class verifier never resolves API 34 types on older devices. */
    private static final class Api34 {
        static String read(Context ctx, long startMs, long endMs) throws InterruptedException {
            android.health.connect.HealthConnectManager manager =
                ctx.getSystemService(android.health.connect.HealthConnectManager.class);
            if (manager == null) return "error:unavailable";
            android.health.connect.TimeInstantRangeFilter range = new android.health.connect.TimeInstantRangeFilter.Builder()
                .setStartTime(Instant.ofEpochMilli(startMs)).setEndTime(Instant.ofEpochMilli(endMs)).build();

            StringBuilder out = new StringBuilder("ok\n");
            long pageToken = -1;
            for (int page = 0; page < MAX_PAGES; page++) {
                android.health.connect.ReadRecordsRequestUsingFilters.Builder<android.health.connect.datatypes.StepsRecord> b =
                    new android.health.connect.ReadRecordsRequestUsingFilters.Builder<>(android.health.connect.datatypes.StepsRecord.class)
                        .setTimeRangeFilter(range).setPageSize(PAGE_SIZE);
                if (pageToken != -1) b.setPageToken(pageToken);

                final CountDownLatch done = new CountDownLatch(1);
                final AtomicReference<android.health.connect.ReadRecordsResponse<android.health.connect.datatypes.StepsRecord>> ok = new AtomicReference<>();
                final AtomicReference<android.health.connect.HealthConnectException> err = new AtomicReference<>();
                manager.readRecords(b.build(), EXECUTOR,
                    new OutcomeReceiver<android.health.connect.ReadRecordsResponse<android.health.connect.datatypes.StepsRecord>, android.health.connect.HealthConnectException>() {
                        @Override public void onResult(android.health.connect.ReadRecordsResponse<android.health.connect.datatypes.StepsRecord> r) { ok.set(r); done.countDown(); }
                        @Override public void onError(android.health.connect.HealthConnectException e) { err.set(e); done.countDown(); }
                    });
                if (!done.await(READ_TIMEOUT_SECONDS, TimeUnit.SECONDS)) return "error:timeout";
                if (err.get() != null) {
                    int code = err.get().getErrorCode();
                    if (code == android.health.connect.HealthConnectException.ERROR_SECURITY) return "error:permission";
                    if (code == android.health.connect.HealthConnectException.ERROR_RATE_LIMIT_EXCEEDED) return "error:timeout";
                    return "error:io";
                }
                for (android.health.connect.datatypes.StepsRecord r : ok.get().getRecords()) {
                    String id = r.getMetadata().getId().replace('\t', ' ').replace('\n', ' ');
                    out.append(id).append('\t')
                       .append(r.getStartTime().toEpochMilli()).append('\t')
                       .append(r.getEndTime().toEpochMilli()).append('\t')
                       .append(r.getCount()).append('\t')
                       .append(r.getMetadata().getLastModifiedTime().toEpochMilli()).append('\n');
                }
                pageToken = ok.get().getNextPageToken();
                if (pageToken == -1) return out.toString();
            }
            return "error:io"; // more than MAX_PAGES pages: refuse a partial window
        }
    }
}
