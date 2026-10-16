using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using WalkGame.Application.Activity;
using WalkGame.Domain.Activity;

namespace WalkGame.Infrastructure.Platform.HealthConnect
{
    /// <summary>
    /// Android Health Connect adapter (M7 / M5-A, D-068). Implements BOTH platform seams:
    /// <see cref="IActivityRecordSource"/> (normalized step records into the unchanged trust
    /// pipeline, D-034) and <see cref="IActivityConnectionPort"/> (ephemeral permission and
    /// availability truth, D-043). It only REPORTS platform truth: it never computes Vitality,
    /// credits, or writes state.
    ///
    /// Failure policy: a fetch that cannot be trusted fails as a whole (no partial credit).
    /// Missing permission throws <see cref="UnauthorizedAccessException"/>; provider missing or
    /// malformed bridge output throws <see cref="InvalidOperationException"/>; timeouts and I/O
    /// throw <see cref="IOException"/>. <c>GameSession.IngestFromSource</c> records all three as
    /// a durable SourceFetchFailed outcome and leaves prior progress untouched (D-063).
    /// </summary>
    public sealed class HealthConnectActivityAdapter : IActivityRecordSource, IActivityConnectionPort
    {
        public const string Namespace = "android.health-connect";

        /// <summary>
        /// Upper bound for a Health Connect record id (they are UUID-like). Ids are persisted in the
        /// save's ledger and shown in support diagnostics, so an oversized or control-character id
        /// is treated as a malformed line (fail closed) rather than stored (security review 2026-10-16).
        /// </summary>
        public const int MaxRecordIdLength = 128;

        internal static bool IsAcceptableId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > MaxRecordIdLength) return false;
            foreach (char c in id)
                if (char.IsControl(c) || char.IsSurrogate(c)) return false;
            return true;
        }

        /// <summary>Revision epoch: lastModified seconds since this instant (fits int until 2088).</summary>
        private static readonly DateTimeOffset RevisionEpoch = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private readonly IHealthConnectBridge _bridge;
        private readonly Func<DateTimeOffset> _clock;
        private DateTimeOffset? _lastAttemptUtc;
        private DateTimeOffset? _lastSuccessUtc;
        private string? _lastFailure;

        public HealthConnectActivityAdapter(IHealthConnectBridge bridge, Func<DateTimeOffset> utcClock)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _clock = utcClock ?? throw new ArgumentNullException(nameof(utcClock));
        }

        public string ProviderNamespace => Namespace;

        public ActivityConnectionSnapshot SnapshotConnection()
        {
            string sdk = Safe(_bridge.GetSdkStatus);
            if (sdk == "unsupported")
                return Snap(ActivityPermissionState.NotRequested, ActivitySourceAvailability.Unsupported, "health connect unavailable on this device");
            if (sdk == "update_required")
                return Snap(ActivityPermissionState.NotRequested, ActivitySourceAvailability.ProviderUpdateRequired, "health connect provider update required");
            if (sdk != "available")
                return Snap(ActivityPermissionState.NotRequested, ActivitySourceAvailability.TemporarilyUnavailable, "health connect status unreadable: " + Clip(sdk));

            var permission = MapPermission(Safe(_bridge.GetPermissionStatus));
            var availability = _lastFailure != null && _lastFailure != "permission"
                ? ActivitySourceAvailability.TemporarilyUnavailable
                : ActivitySourceAvailability.Available;
            return Snap(permission, availability, _lastFailure == null ? "health connect available" : "last refresh failed: " + _lastFailure);
        }

        public IReadOnlyList<NormalizedActivityRecord> FetchRecords(DateTimeOffset windowStartUtc, DateTimeOffset windowEndUtc)
        {
            _lastAttemptUtc = _clock();
            if (windowEndUtc <= windowStartUtc) { MarkSuccess(); return Array.Empty<NormalizedActivityRecord>(); }

            string sdk = Safe(_bridge.GetSdkStatus);
            if (sdk != "available") throw Fail("unavailable", new InvalidOperationException("Health Connect is not available (" + Clip(sdk) + ")."));
            if (MapPermission(Safe(_bridge.GetPermissionStatus)) != ActivityPermissionState.Granted)
                throw Fail("permission", new UnauthorizedAccessException("Step read permission is not granted."));

            long startMs = windowStartUtc.ToUnixTimeMilliseconds();
            long endMs = windowEndUtc.ToUnixTimeMilliseconds();
            string raw;
            try { raw = _bridge.ReadSteps(startMs, endMs) ?? ""; }
            catch (Exception ex) { throw Fail("io", new IOException("Health Connect bridge call failed.", ex)); }

            var lines = raw.Replace("\r", "").Split('\n');
            string head = lines[0].Trim();
            switch (head)
            {
                case "ok": break;
                case "error:permission": throw Fail("permission", new UnauthorizedAccessException("Health Connect refused the read (permission)."));
                case "error:unavailable": throw Fail("unavailable", new InvalidOperationException("Health Connect became unavailable."));
                case "error:timeout": throw Fail("timeout", new IOException("Health Connect read timed out."));
                case "error:io": throw Fail("io", new IOException("Health Connect read failed."));
                default: throw Fail("protocol", new InvalidOperationException("Unrecognized bridge response."));
            }

            var records = new List<NormalizedActivityRecord>();
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].Length == 0) continue;
                var record = ParseLine(lines[i]);
                if (record == null) throw Fail("protocol", new InvalidOperationException("Malformed step record on line " + i + "."));
                // Contract: only records lying entirely inside [start, end).
                if (record.StartUtc < windowStartUtc || record.EndUtc > windowEndUtc) continue;
                records.Add(record);
            }
            records.Sort((a, b) =>
            {
                if (a.IsDeletion != b.IsDeletion) return a.IsDeletion ? 1 : -1; // credits first, then deletions
                int c = a.StartUtc.CompareTo(b.StartUtc);
                return c != 0 ? c : string.CompareOrdinal(a.SourceRecordId, b.SourceRecordId);
            });
            MarkSuccess();
            return records;
        }

        private static NormalizedActivityRecord? ParseLine(string line)
        {
            if (line.StartsWith("del\t", StringComparison.Ordinal)) return ParseDeletion(line);
            var f = line.Split('\t');
            // Five fields, or six with the record's end zone offset in seconds (D-103). An
            // empty sixth field means Health Connect supplied no offset.
            if ((f.Length != 5 && f.Length != 6) || !IsAcceptableId(f[0].Trim())) return null;
            var inv = CultureInfo.InvariantCulture;
            if (!long.TryParse(f[1], NumberStyles.Integer, inv, out long s) ||
                !long.TryParse(f[2], NumberStyles.Integer, inv, out long e) ||
                !long.TryParse(f[3], NumberStyles.Integer, inv, out long count) ||
                !long.TryParse(f[4], NumberStyles.Integer, inv, out long modified))
                return null;
            int? offsetSeconds = null;
            if (f.Length == 6 && f[5].Length > 0)
            {
                if (!int.TryParse(f[5], NumberStyles.AllowLeadingSign, inv, out int off)
                    || off > LocalDayCalendar.MaxOffsetSeconds || off < -LocalDayCalendar.MaxOffsetSeconds)
                    return null;
                offsetSeconds = off;
            }
            DateTimeOffset start, end, mod;
            try
            {
                start = DateTimeOffset.FromUnixTimeMilliseconds(s);
                end = DateTimeOffset.FromUnixTimeMilliseconds(e);
                mod = DateTimeOffset.FromUnixTimeMilliseconds(modified);
            }
            catch (ArgumentOutOfRangeException) { return null; }
            // Quantity/timestamp validity is judged by the trust pipeline, not here.
            return new NormalizedActivityRecord(Namespace, f[0].Trim(), ActivityCategory.Walking, ActivityUnits.Steps,
                count, start, end, RevisionFor(mod), EndZoneOffsetSeconds: offsetSeconds);
        }

        /// <summary>
        /// Deletion marker (D-069) for the existing correction/deletion path: same identity as the
        /// credited record (namespace + Health Connect id), its ORIGINAL interval so validation and the
        /// trusted-end watermark are unchanged, quantity 0, and a revision strictly above the credited
        /// one. The revision is a pure function of the line, so re-delivery is a stale-revision no-op.
        /// </summary>
        private static NormalizedActivityRecord? ParseDeletion(string line)
        {
            var f = line.Split('\t');
            if (f.Length != 6 || !IsAcceptableId(f[1].Trim())) return null;
            var inv = CultureInfo.InvariantCulture;
            if (!long.TryParse(f[2], NumberStyles.Integer, inv, out long s) ||
                !long.TryParse(f[3], NumberStyles.Integer, inv, out long e) ||
                !long.TryParse(f[4], NumberStyles.Integer, inv, out long modified) ||
                !long.TryParse(f[5], NumberStyles.Integer, inv, out long deleted))
                return null;
            DateTimeOffset start, end, mod, del;
            try
            {
                start = DateTimeOffset.FromUnixTimeMilliseconds(s);
                end = DateTimeOffset.FromUnixTimeMilliseconds(e);
                mod = DateTimeOffset.FromUnixTimeMilliseconds(modified);
                del = DateTimeOffset.FromUnixTimeMilliseconds(deleted);
            }
            catch (ArgumentOutOfRangeException) { return null; }
            return new NormalizedActivityRecord(Namespace, f[1].Trim(), ActivityCategory.Walking, ActivityUnits.Steps,
                0L, start, end, DeletionRevisionFor(mod, del), IsDeletion: true);
        }

        /// <summary>Strictly above the credited revision, even if the device clock ran backwards.</summary>
        public static int DeletionRevisionFor(DateTimeOffset lastModifiedUtc, DateTimeOffset deletedUtc)
        {
            int credited = RevisionFor(lastModifiedUtc);
            int byTime = RevisionFor(deletedUtc);
            if (credited == int.MaxValue) return int.MaxValue;
            return Math.Max(byTime, credited + 1);
        }

        /// <summary>Monotonic revision from Health Connect lastModifiedTime, so an edited record supersedes its earlier version.</summary>
        public static int RevisionFor(DateTimeOffset lastModifiedUtc)
        {
            double seconds = (lastModifiedUtc - RevisionEpoch).TotalSeconds;
            if (seconds < 1) return 1;
            return seconds >= int.MaxValue ? int.MaxValue : (int)seconds;
        }

        public static ActivityPermissionState MapPermission(string status)
        {
            switch (status)
            {
                case "granted": return ActivityPermissionState.Granted;
                case "denied": return ActivityPermissionState.Denied;
                case "revoked": return ActivityPermissionState.Revoked;
                // not_requested and anything unknown: never claim a grant we cannot see.
                default: return ActivityPermissionState.NotRequested;
            }
        }

        private void MarkSuccess() { _lastSuccessUtc = _lastAttemptUtc; _lastFailure = null; }

        private Exception Fail(string category, Exception ex) { _lastFailure = category; return ex; }

        private ActivityConnectionSnapshot Snap(ActivityPermissionState p, ActivitySourceAvailability a, string detail)
            => new ActivityConnectionSnapshot(p, a, _lastSuccessUtc, _lastAttemptUtc, detail);

        private static string Safe(Func<string> call)
        {
            try { return (call() ?? "").Trim(); }
            catch (Exception) { return "error"; }
        }

        private static string Clip(string s) => s.Length <= 40 ? s : s.Substring(0, 40);
    }
}
