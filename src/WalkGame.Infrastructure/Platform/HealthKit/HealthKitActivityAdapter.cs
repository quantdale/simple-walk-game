using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using WalkGame.Application.Activity;
using WalkGame.Domain.Activity;
using WalkGame.Infrastructure.Platform.HealthConnect;

namespace WalkGame.Infrastructure.Platform.HealthKit
{
    /// <summary>
    /// iOS HealthKit adapter (KI-022, D-112). Mirrors <see cref="HealthConnectActivityAdapter"/>:
    /// implements <see cref="IActivityRecordSource"/> and <see cref="IActivityConnectionPort"/>,
    /// reports platform truth only, never computes Vitality, and fails a fetch as a whole (D-063).
    ///
    /// HealthKit specifics (docs/PLATFORM_PARITY.md contract):
    /// - provider namespace <c>ios.healthkit</c>, so dedup, step carry and deletions stay per provider;
    /// - samples are immutable (an edit is delete + add), so a credit is revision 1 and a deletion revision 2;
    /// - read authorisation is not observable: after the dialog was answered the adapter reports
    ///   Granted so reads may run, but <see cref="ReadAccessConfirmed"/> stays false until a read
    ///   returns at least one sample. Callers must not tell the player "connected" from the grant alone.
    /// Headless-tested with a fake bridge; the Swift bridge and device behaviour are UNVERIFIED.
    /// </summary>
    public sealed class HealthKitActivityAdapter : IActivityRecordSource, IActivityConnectionPort
    {
        public const string Namespace = "ios.healthkit";
        public const int CreditRevision = 1;
        public const int DeletionRevision = 2;

        private readonly IHealthKitBridge _bridge;
        private readonly Func<DateTimeOffset> _clock;
        private readonly Func<DateTimeOffset, int?>? _deviceOffsetAt;
        private DateTimeOffset? _lastAttemptUtc;
        private DateTimeOffset? _lastSuccessUtc;
        private string? _lastFailure;
        private ActivityPermissionState? _lastKnownPermission; // D-192: last request status READ, this process only

        /// <param name="deviceOffsetAt">
        /// D-157 fallback, same rule as Health Connect: a sample without <c>HKMetadataKeyTimeZone</c> (the
        /// common case for iPhone step samples) gets the device zone's offset at its end instant. Null keeps
        /// it undated. Unwired until the iOS composition exists (KI-022).
        /// </param>
        public HealthKitActivityAdapter(IHealthKitBridge bridge, Func<DateTimeOffset> utcClock, Func<DateTimeOffset, int?>? deviceOffsetAt = null)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _clock = utcClock ?? throw new ArgumentNullException(nameof(utcClock));
            _deviceOffsetAt = deviceOffsetAt;
        }

        public string ProviderNamespace => Namespace;

        /// <summary>True once any read returned a sample: the only observable evidence of read access.</summary>
        public bool ReadAccessConfirmed { get; private set; }

        public ActivityConnectionSnapshot SnapshotConnection()
        {
            string availability = Safe(_bridge.GetAvailability);
            if (availability == "unsupported")
                return Snap(ActivityPermissionState.NotRequested, ActivitySourceAvailability.Unsupported, "healthkit unavailable on this device");
            if (availability != "available")
                return Snap(ActivityPermissionState.NotRequested, ActivitySourceAvailability.TemporarilyUnavailable, "healthkit status unreadable: " + Clip(availability), _lastKnownPermission);

            var permission = ReadRequestStatus();
            var avail = _lastFailure != null && _lastFailure != "permission"
                ? ActivitySourceAvailability.TemporarilyUnavailable
                : ActivitySourceAvailability.Available;
            string detail = _lastFailure != null ? "last refresh failed: " + _lastFailure
                : permission == ActivityPermissionState.Granted && !ReadAccessConfirmed ? "healthkit asked; read access not yet confirmed by data"
                : "healthkit available";
            return Snap(permission, avail, detail);
        }

        public IReadOnlyList<NormalizedActivityRecord> FetchRecords(DateTimeOffset windowStartUtc, DateTimeOffset windowEndUtc)
        {
            _lastAttemptUtc = _clock();
            if (windowEndUtc <= windowStartUtc) { MarkSuccess(); return Array.Empty<NormalizedActivityRecord>(); }

            string availability = Safe(_bridge.GetAvailability);
            if (availability != "available") throw Fail("unavailable", new InvalidOperationException("HealthKit is not available (" + Clip(availability) + ")."));
            if (ReadRequestStatus() != ActivityPermissionState.Granted)
                throw Fail("permission", new UnauthorizedAccessException("HealthKit authorisation has not been requested."));

            string raw;
            try { raw = _bridge.ReadSteps(windowStartUtc.ToUnixTimeMilliseconds(), windowEndUtc.ToUnixTimeMilliseconds()) ?? ""; }
            catch (Exception ex) { throw Fail("io", new IOException("HealthKit bridge call failed.", ex)); }

            var lines = raw.Replace("\r", "").Split('\n');
            switch (lines[0].Trim())
            {
                case "ok": break;
                case "error:permission": throw Fail("permission", new UnauthorizedAccessException("HealthKit refused the read (permission)."));
                case "error:unavailable": throw Fail("unavailable", new InvalidOperationException("HealthKit became unavailable."));
                case "error:timeout": throw Fail("timeout", new IOException("HealthKit read timed out."));
                case "error:io": throw Fail("io", new IOException("HealthKit read failed."));
                default: throw Fail("protocol", new InvalidOperationException("Unrecognized bridge response."));
            }

            var records = new List<NormalizedActivityRecord>();
            var kinds = new Dictionary<string, HealthKitSourceKind>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].Length == 0) continue;
                var record = ParseLine(lines[i], out var kind);
                if (record == null) throw Fail("protocol", new InvalidOperationException("Malformed step sample on line " + i + "."));
                // A sample UUID appears at most once per kind in one response; a repeat is a bridge bug.
                if (!seen.Add((record.IsDeletion ? "del:" : "add:") + record.SourceRecordId))
                    throw Fail("protocol", new InvalidOperationException("Duplicate sample on line " + i + "."));
                if (record.StartUtc < windowStartUtc || record.EndUtc > windowEndUtc) continue;
                if (!record.IsDeletion) kinds[record.SourceRecordId!] = kind;
                records.Add(Platform.DeviceZoneOffsets.WithFallbackOffset(record, _deviceOffsetAt));
            }
            records = ApplySourceDedup(records, kinds);
            records.Sort((a, b) =>
            {
                if (a.IsDeletion != b.IsDeletion) return a.IsDeletion ? 1 : -1;
                int c = a.StartUtc.CompareTo(b.StartUtc);
                return c != 0 ? c : string.CompareOrdinal(a.SourceRecordId, b.SourceRecordId);
            });
            if (records.Exists(r => !r.IsDeletion)) ReadAccessConfirmed = true;
            MarkSuccess();
            return records;
        }

        /// <summary>
        /// D-114: iPhone + Apple Watch overlap is resolved with <see cref="HealthKitSourceDedup"/> over the
        /// credit lines of ONE response (Watch &gt; Phone &gt; Other). A credit's quantity becomes its
        /// deduplicated share (never above the bridge count; a zero share is omitted); deletions are untouched. Lines without
        /// the sixth field are Other, so a five-field bridge behaves exactly as before D-114.
        /// </summary>
        private static List<NormalizedActivityRecord> ApplySourceDedup(List<NormalizedActivityRecord> records, Dictionary<string, HealthKitSourceKind> kinds)
        {
            var credits = records.FindAll(r => !r.IsDeletion);
            if (credits.Count < 2) return records;
            var resolved = HealthKitSourceDedup.Resolve(credits.ConvertAll(r => new HealthKitSourcedSample(
                r.SourceRecordId!, r.StartUtc.ToUnixTimeMilliseconds(), r.EndUtc.ToUnixTimeMilliseconds(), r.Quantity, kinds[r.SourceRecordId!])));
            var output = new List<NormalizedActivityRecord>(records.Count);
            foreach (var r in records)
            {
                if (r.IsDeletion || r.Quantity <= 0) { output.Add(r); continue; } // invalid counts: judged by the trust pipeline
                long share = Math.Min(r.Quantity, resolved[r.SourceRecordId!]);
                // A sample fully covered by a higher-priority device is omitted, not sent as 0 (the
                // pipeline would reject a zero quantity); if the covering sample is later deleted,
                // the next read re-emits this one with its full share.
                if (share > 0) output.Add(r with { Quantity = share });
            }
            return output;
        }

        /// <summary>D-114 sixth field: <c>watch</c> | <c>phone</c> | <c>other</c>; absent or empty → Other.</summary>
        public static bool TryParseSourceKind(string token, out HealthKitSourceKind kind)
        {
            switch (token)
            {
                case "": case "other": kind = HealthKitSourceKind.Other; return true;
                case "watch": kind = HealthKitSourceKind.Watch; return true;
                case "phone": kind = HealthKitSourceKind.Phone; return true;
                default: kind = HealthKitSourceKind.Other; return false;
            }
        }

        private static NormalizedActivityRecord? ParseLine(string line, out HealthKitSourceKind kind)
        {
            kind = HealthKitSourceKind.Other;
            var f = line.Split('\t');
            var inv = CultureInfo.InvariantCulture;
            if (f[0] == "del")
            {
                if (f.Length != 4 || !HealthConnectActivityAdapter.IsAcceptableId(f[1].Trim())) return null;
                if (!long.TryParse(f[2], NumberStyles.Integer, inv, out long ds) || !long.TryParse(f[3], NumberStyles.Integer, inv, out long de)) return null;
                if (!TryInstant(ds, out var dStart) || !TryInstant(de, out var dEnd)) return null;
                return new NormalizedActivityRecord(Namespace, f[1].Trim(), ActivityCategory.Walking, ActivityUnits.Steps,
                    0L, dStart, dEnd, DeletionRevision, IsDeletion: true);
            }
            if (f.Length < 5 || f.Length > 6 || !HealthConnectActivityAdapter.IsAcceptableId(f[0].Trim())) return null;
            if (!long.TryParse(f[1], NumberStyles.Integer, inv, out long s) ||
                !long.TryParse(f[2], NumberStyles.Integer, inv, out long e) ||
                !long.TryParse(f[3], NumberStyles.Integer, inv, out long count))
                return null;
            int? offset = null;
            if (f[4].Length > 0)
            {
                if (!int.TryParse(f[4], NumberStyles.AllowLeadingSign, inv, out int off)
                    || off > LocalDayCalendar.MaxOffsetSeconds || off < -LocalDayCalendar.MaxOffsetSeconds)
                    return null;
                offset = off;
            }
            if (f.Length == 6 && !TryParseSourceKind(f[5], out kind)) return null;
            if (!TryInstant(s, out var start) || !TryInstant(e, out var end)) return null;
            // Quantity/timestamp validity is judged by the trust pipeline, not here.
            return new NormalizedActivityRecord(Namespace, f[0].Trim(), ActivityCategory.Walking, ActivityUnits.Steps,
                count, start, end, CreditRevision, EndZoneOffsetSeconds: offset);
        }

        private static bool TryInstant(long ms, out DateTimeOffset value)
        {
            try { value = DateTimeOffset.FromUnixTimeMilliseconds(ms); return true; }
            catch (ArgumentOutOfRangeException) { value = default; return false; }
        }

        /// <summary>HealthKit cannot report a read grant; an answered dialog is the most it says.</summary>
        public static ActivityPermissionState MapRequestStatus(string status) =>
            status == "unnecessary" ? ActivityPermissionState.Granted : ActivityPermissionState.NotRequested;

        private void MarkSuccess() { _lastSuccessUtc = _lastAttemptUtc; _lastFailure = null; }
        private Exception Fail(string category, Exception ex) { _lastFailure = category; return ex; }
        private ActivityConnectionSnapshot Snap(ActivityPermissionState p, ActivitySourceAvailability a, string detail, ActivityPermissionState? lastKnown = null)
            => new ActivityConnectionSnapshot(p, a, _lastSuccessUtc, _lastAttemptUtc, detail, lastKnown);

        /// <summary>Maps the request status; a recognised answer becomes the last known permission (D-192).</summary>
        private ActivityPermissionState ReadRequestStatus()
        {
            string raw = Safe(_bridge.GetRequestStatus);
            var permission = MapRequestStatus(raw);
            if (raw == "unnecessary" || raw == "should_request")
                _lastKnownPermission = permission;
            return permission;
        }
        private static string Safe(Func<string> call)
        {
            try { return (call() ?? "").Trim(); }
            catch (Exception) { return "error"; }
        }
        private static string Clip(string s) => s.Length <= 40 ? s : s.Substring(0, 40);
    }
}
