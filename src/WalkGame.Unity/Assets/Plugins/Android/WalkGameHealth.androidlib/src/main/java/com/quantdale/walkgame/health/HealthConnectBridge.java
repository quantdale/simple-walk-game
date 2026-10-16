package com.quantdale.walkgame.health;

import android.app.Activity;
import android.content.Context;
import android.content.SharedPreferences;
import android.content.pm.PackageManager;
import android.os.Build;
import android.os.OutcomeReceiver;

import java.io.BufferedReader;
import java.io.File;
import java.io.FileOutputStream;
import java.io.FileReader;
import java.io.OutputStreamWriter;
import java.nio.charset.StandardCharsets;
import java.time.Instant;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
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

    /**
     * App-private record index (D-069): Health Connect deletion logs carry only an id and a time,
     * so we remember the interval/lastModified of every record we returned, and keep deleted
     * entries so their markers are re-emitted on every read (exactly-once is enforced by the
     * pipeline's stale-revision rule, not by this file). Entries older than RETAIN_MS are pruned.
     * Stored in noBackupFilesDir, beside the save's protection class (D-067).
     */
    static final class Index {
        static final long RETAIN_MS = 45L * 24 * 3600 * 1000;
        final Map<String, long[]> known = new LinkedHashMap<>();   // id -> start, end, mod
        final Map<String, long[]> deleted = new LinkedHashMap<>(); // id -> start, end, mod, deletedAt
        private final File file;

        Index(Context ctx) { file = new File(ctx.getNoBackupFilesDir(), "wg_hc_index.tsv"); }

        void load() {
            known.clear(); deleted.clear();
            if (!file.exists()) return;
            try (BufferedReader r = new BufferedReader(new FileReader(file))) {
                String line;
                while ((line = r.readLine()) != null) {
                    String[] f = line.split("\t");
                    try {
                        if (f.length == 5 && f[0].equals("k"))
                            known.put(f[1], new long[] { Long.parseLong(f[2]), Long.parseLong(f[3]), Long.parseLong(f[4]) });
                        else if (f.length == 6 && f[0].equals("d"))
                            deleted.put(f[1], new long[] { Long.parseLong(f[2]), Long.parseLong(f[3]), Long.parseLong(f[4]), Long.parseLong(f[5]) });
                    } catch (NumberFormatException ignored) { }
                }
            } catch (Exception ignored) { }
        }

        void save(long nowMs) throws java.io.IOException {
            long cutoff = nowMs - RETAIN_MS;
            File tmp = new File(file.getPath() + ".tmp");
            try (OutputStreamWriter w = new OutputStreamWriter(new FileOutputStream(tmp), StandardCharsets.UTF_8)) {
                for (Map.Entry<String, long[]> e : known.entrySet())
                    if (e.getValue()[1] >= cutoff)
                        w.write("k\t" + e.getKey() + "\t" + e.getValue()[0] + "\t" + e.getValue()[1] + "\t" + e.getValue()[2] + "\n");
                for (Map.Entry<String, long[]> e : deleted.entrySet())
                    if (e.getValue()[1] >= cutoff)
                        w.write("d\t" + e.getKey() + "\t" + e.getValue()[0] + "\t" + e.getValue()[1] + "\t" + e.getValue()[2] + "\t" + e.getValue()[3] + "\n");
            }
            if (!tmp.renameTo(file)) throw new java.io.IOException("index rename failed");
        }
    }

    /** Thrown inside Api34 to return a protocol error code. */
    static final class BridgeError extends Exception {
        final String code;
        BridgeError(String code) { super(code); this.code = code; }
    }

    /** Isolated so the class verifier never resolves API 34 types on older devices. */
    private static final class Api34 {
        interface Call<T> { void start(OutcomeReceiver<T, android.health.connect.HealthConnectException> receiver); }

        static <T> T await(Call<T> call) throws BridgeError, InterruptedException {
            final CountDownLatch done = new CountDownLatch(1);
            final AtomicReference<T> ok = new AtomicReference<>();
            final AtomicReference<android.health.connect.HealthConnectException> err = new AtomicReference<>();
            call.start(new OutcomeReceiver<T, android.health.connect.HealthConnectException>() {
                @Override public void onResult(T r) { ok.set(r); done.countDown(); }
                @Override public void onError(android.health.connect.HealthConnectException e) { err.set(e); done.countDown(); }
            });
            if (!done.await(READ_TIMEOUT_SECONDS, TimeUnit.SECONDS)) throw new BridgeError("error:timeout");
            if (err.get() != null) {
                int code = err.get().getErrorCode();
                if (code == android.health.connect.HealthConnectException.ERROR_SECURITY) throw new BridgeError("error:permission");
                if (code == android.health.connect.HealthConnectException.ERROR_RATE_LIMIT_EXCEEDED) throw new BridgeError("error:timeout");
                if (code == android.health.connect.HealthConnectException.ERROR_INVALID_ARGUMENT) throw new BridgeError("token");
                throw new BridgeError("error:io");
            }
            return ok.get();
        }

        static String read(Context ctx, long startMs, long endMs) throws InterruptedException {
            final android.health.connect.HealthConnectManager manager =
                ctx.getSystemService(android.health.connect.HealthConnectManager.class);
            if (manager == null) return "error:unavailable";
            Index index = new Index(ctx);
            index.load();
            try {
                syncDeletions(ctx, manager, index);
                StringBuilder out = new StringBuilder("ok\n");
                readRecords(manager, index, startMs, endMs, out);
                index.save(System.currentTimeMillis());
                for (Map.Entry<String, long[]> e : index.deleted.entrySet()) {
                    long[] v = e.getValue();
                    out.append("del\t").append(e.getKey()).append('\t').append(v[0]).append('\t').append(v[1])
                       .append('\t').append(v[2]).append('\t').append(v[3]).append('\n');
                }
                return out.toString();
            } catch (BridgeError e) {
                return e.code.equals("token") ? "error:io" : e.code;
            } catch (java.io.IOException e) {
                return "error:io";
            }
        }

        /**
         * Consumes the steps change log. The index is written BEFORE the next token is stored,
         * so a crash can only replay a deletion (harmless), never lose one. An expired token
         * (ERROR_INVALID_ARGUMENT) is recovered by a full re-read of the indexed span (KI-016(a)).
         */
        static void syncDeletions(Context ctx, final android.health.connect.HealthConnectManager manager, Index index)
                throws BridgeError, InterruptedException, java.io.IOException {
            SharedPreferences prefs = ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
            String token = prefs.getString("changesToken", null);
            if (token == null) {
                prefs.edit().putString("changesToken", freshToken(manager)).apply();
                return;
            }
            for (int page = 0; page < MAX_PAGES; page++) {
                final android.health.connect.changelog.ChangeLogsRequest req =
                    new android.health.connect.changelog.ChangeLogsRequest.Builder(token).build();
                android.health.connect.changelog.ChangeLogsResponse resp;
                try {
                    resp = await(r -> manager.getChangeLogs(req, EXECUTOR, r));
                } catch (BridgeError e) {
                    if (!e.code.equals("token")) throw e;
                    recoverFromExpiredToken(prefs, manager, index);
                    return;
                }
                List<android.health.connect.changelog.ChangeLogsResponse.DeletedLog> logs = resp.getDeletedLogs();
                for (android.health.connect.changelog.ChangeLogsResponse.DeletedLog d : logs) {
                    long[] k = index.known.remove(d.getDeletedRecordId());
                    if (k != null)
                        index.deleted.put(d.getDeletedRecordId(), new long[] { k[0], k[1], k[2], d.getDeletedTime().toEpochMilli() });
                }
                index.save(System.currentTimeMillis());
                token = resp.getNextChangesToken();
                prefs.edit().putString("changesToken", token).apply();
                if (!resp.hasMorePages()) return;
            }
        }

        /**
         * KI-016(a): the change log cannot report deletions made while the token was expired.
         * Order matters: take the new token FIRST (later deletions land in its log), re-read the
         * whole indexed span, move absent records to the deleted set, save the index, and only
         * then store the token. Any failure throws before the token is stored, so the next read
         * simply retries the recovery; nothing is lost and replays are stale-revision no-ops.
         */
        static void recoverFromExpiredToken(SharedPreferences prefs, final android.health.connect.HealthConnectManager manager,
                                            Index index) throws BridgeError, InterruptedException, java.io.IOException {
            String next = freshToken(manager);
            long[] span = GapReconciler.span(index.known);
            if (span != null) {
                java.util.Set<String> present = new java.util.HashSet<>();
                readIds(manager, span[0], span[1], present);
                GapReconciler.reconcile(index.known, index.deleted, present, span[0], span[1], System.currentTimeMillis());
                index.save(System.currentTimeMillis());
            }
            prefs.edit().putString("changesToken", next).putBoolean("changesGap", false)
                 .putLong("changesGapRecoveredAt", System.currentTimeMillis()).apply();
        }

        static void readIds(final android.health.connect.HealthConnectManager manager, long startMs, long endMs,
                            java.util.Set<String> ids) throws BridgeError, InterruptedException {
            android.health.connect.TimeInstantRangeFilter range = new android.health.connect.TimeInstantRangeFilter.Builder()
                .setStartTime(Instant.ofEpochMilli(startMs)).setEndTime(Instant.ofEpochMilli(endMs)).build();
            long pageToken = -1;
            for (int page = 0; page < MAX_PAGES; page++) {
                android.health.connect.ReadRecordsRequestUsingFilters.Builder<android.health.connect.datatypes.StepsRecord> b =
                    new android.health.connect.ReadRecordsRequestUsingFilters.Builder<>(android.health.connect.datatypes.StepsRecord.class)
                        .setTimeRangeFilter(range).setPageSize(PAGE_SIZE);
                if (pageToken != -1) b.setPageToken(pageToken);
                final android.health.connect.ReadRecordsRequestUsingFilters<android.health.connect.datatypes.StepsRecord> req = b.build();
                android.health.connect.ReadRecordsResponse<android.health.connect.datatypes.StepsRecord> resp =
                    await(r -> manager.readRecords(req, EXECUTOR, r));
                for (android.health.connect.datatypes.StepsRecord r : resp.getRecords())
                    ids.add(r.getMetadata().getId().replace('\t', ' ').replace('\n', ' '));
                pageToken = resp.getNextPageToken();
                if (pageToken == -1) return;
            }
            // A partial span must never be read as "absent = deleted".
            throw new BridgeError("error:io");
        }

        static String freshToken(final android.health.connect.HealthConnectManager manager) throws BridgeError, InterruptedException {
            final android.health.connect.changelog.ChangeLogTokenRequest req =
                new android.health.connect.changelog.ChangeLogTokenRequest.Builder()
                    .addRecordType(android.health.connect.datatypes.StepsRecord.class).build();
            android.health.connect.changelog.ChangeLogTokenResponse resp = await(r -> manager.getChangeLogToken(req, EXECUTOR, r));
            return resp.getToken();
        }

        static void readRecords(final android.health.connect.HealthConnectManager manager, Index index,
                                long startMs, long endMs, StringBuilder out) throws BridgeError, InterruptedException {
            android.health.connect.TimeInstantRangeFilter range = new android.health.connect.TimeInstantRangeFilter.Builder()
                .setStartTime(Instant.ofEpochMilli(startMs)).setEndTime(Instant.ofEpochMilli(endMs)).build();
            long pageToken = -1;
            for (int page = 0; page < MAX_PAGES; page++) {
                android.health.connect.ReadRecordsRequestUsingFilters.Builder<android.health.connect.datatypes.StepsRecord> b =
                    new android.health.connect.ReadRecordsRequestUsingFilters.Builder<>(android.health.connect.datatypes.StepsRecord.class)
                        .setTimeRangeFilter(range).setPageSize(PAGE_SIZE);
                if (pageToken != -1) b.setPageToken(pageToken);
                final android.health.connect.ReadRecordsRequestUsingFilters<android.health.connect.datatypes.StepsRecord> req = b.build();
                android.health.connect.ReadRecordsResponse<android.health.connect.datatypes.StepsRecord> resp =
                    await(r -> manager.readRecords(req, EXECUTOR, r));
                for (android.health.connect.datatypes.StepsRecord r : resp.getRecords()) {
                    String id = r.getMetadata().getId().replace('\t', ' ').replace('\n', ' ');
                    long s = r.getStartTime().toEpochMilli(), e = r.getEndTime().toEpochMilli();
                    long mod = r.getMetadata().getLastModifiedTime().toEpochMilli();
                    index.known.put(id, new long[] { s, e, mod });
                    index.deleted.remove(id);
                    out.append(id).append('\t').append(s).append('\t').append(e).append('\t')
                       .append(r.getCount()).append('\t').append(mod).append('\t');
                    // D-103: sixth field = end zone offset in seconds (empty when unknown).
                    java.time.ZoneOffset zo = r.getEndZoneOffset();
                    if (zo != null) out.append(zo.getTotalSeconds());
                    out.append('\n');
                }
                pageToken = resp.getNextPageToken();
                if (pageToken == -1) return;
            }
            throw new BridgeError("error:io"); // more than MAX_PAGES pages: refuse a partial window
        }
    }
}
