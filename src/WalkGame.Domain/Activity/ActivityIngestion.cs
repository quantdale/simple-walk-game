using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using WalkGame.Domain.Common;

namespace WalkGame.Domain.Activity
{
    public enum ActivityCategory
    {
        Walking = 0,
    }

    /// <summary>Documented activity quantity units (ACTIVITY_PIPELINE.md §5).</summary>
    public static class ActivityUnits
    {
        public const string Steps = "steps";
    }

    /// <summary>
    /// Platform-neutral normalized activity record: the narrow contract every source
    /// adapter (fixture provider today, Health Connect/HealthKit later) must produce
    /// before the trust pipeline sees data. Carries provenance only — never raw payloads.
    /// </summary>
    public sealed record NormalizedActivityRecord(
        string ProviderNamespace,
        string? SourceRecordId,
        ActivityCategory Category,
        string Unit,
        long Quantity,
        DateTimeOffset StartUtc,
        DateTimeOffset EndUtc);

    public enum ActivityValidationStatus
    {
        Valid = 0,
        EmptyProvider = 1,
        UnsupportedCategory = 2,
        UnsupportedUnit = 3,
        ZeroQuantity = 4,
        NegativeQuantity = 5,
        MalformedTimestamps = 6,
        FutureTimestamp = 7,
    }

    /// <summary>
    /// Validation and bounding rules for normalized records. Deterministic given the
    /// injected clock; pathological quantities are clamped (not dropped) so a glitched
    /// source value cannot create unbounded rewards while honest large values still count.
    /// </summary>
    public static class ActivityValidationPolicy
    {
        /// <summary>Records ending more than this far in the future are rejected as suspicious.</summary>
        public static readonly TimeSpan MaxFutureSkew = TimeSpan.FromMinutes(10);

        /// <summary>Upper clamp on steps credited from a single record (~4x an extreme ultramarathon day).</summary>
        public const long MaxStepsPerRecord = 250_000L;

        public static long ClampQuantity(ActivityCategory category, long quantity)
        {
            if (category != ActivityCategory.Walking || quantity < MaxStepsPerRecord)
                return Math.Max(0L, quantity);
            return MaxStepsPerRecord;
        }

        public static ActivityValidationStatus Validate(NormalizedActivityRecord record, DateTimeOffset nowUtc)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));

            if (string.IsNullOrWhiteSpace(record.ProviderNamespace))
                return ActivityValidationStatus.EmptyProvider;
            if (record.Category != ActivityCategory.Walking)
                return ActivityValidationStatus.UnsupportedCategory;
            if (!string.Equals(record.Unit, ActivityUnits.Steps, StringComparison.Ordinal))
                return ActivityValidationStatus.UnsupportedUnit;
            if (record.Quantity == 0L)
                return ActivityValidationStatus.ZeroQuantity;
            if (record.Quantity < 0L)
                return ActivityValidationStatus.NegativeQuantity;
            if (record.StartUtc == default
                || record.EndUtc == default
                || record.EndUtc <= record.StartUtc)
                return ActivityValidationStatus.MalformedTimestamps;
            if (record.EndUtc > nowUtc + MaxFutureSkew)
                return ActivityValidationStatus.FutureTimestamp;

            return ActivityValidationStatus.Valid;
        }
    }

    /// <summary>
    /// Versioned stable identity for logical activity records (ACTIVITY_PIPELINE.md §6).
    /// Preferred: platform source record ID inside a provider namespace.
    /// Fallback: deterministic fingerprint over normalized stable fields.
    /// The version prefix is part of the key: changing the algorithm changes identity,
    /// which forces an explicit reconciliation decision instead of silent reprocessing.
    /// </summary>
    public static class ActivityIdentity
    {
        public const string SourceIdSchemeVersion = "rec1";
        public const string FingerprintSchemeVersion = "fpt1";

        public static string Compute(NormalizedActivityRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (!string.IsNullOrWhiteSpace(record.SourceRecordId))
                return SourceIdSchemeVersion
                    + "|" + record.ProviderNamespace.Trim()
                    + "|" + CategoryToken(record.Category)
                    + "|" + record.SourceRecordId.Trim();

            return FingerprintSchemeVersion + "|" + Fingerprint(record);
        }

        /// <summary>Deterministic fallback fingerprint: SHA-256 over canonical stable fields.</summary>
        public static string Fingerprint(NormalizedActivityRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));

            string canonical = string.Join("|",
                "fpv1",
                record.ProviderNamespace.Trim(),
                CategoryToken(record.Category),
                Normalize(record.Unit),
                record.StartUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                record.EndUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                record.Quantity.ToString(CultureInfo.InvariantCulture));

            byte[] hash;
            using (var sha = SHA256.Create())
                hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(canonical));
            return ToHex(hash);
        }

        private static string CategoryToken(ActivityCategory category) =>
            ((int)category).ToString(CultureInfo.InvariantCulture);

        private static string Normalize(string value) => (value ?? string.Empty).Trim().ToLowerInvariant();

        private static string ToHex(byte[] bytes)
        {
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            var builder = new System.Text.StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
#else
            return Convert.ToHexString(bytes).ToLowerInvariant();
#endif
        }
    }

    /// <summary>
    /// Conversion rule version 1: integer math, floor rounding, documented unit.
    /// 100 walking steps convert to 1 Vitality; sub-unit remainder is discarded.
    /// Rule version is stored on every processed-record entry so historical credits
    /// remain auditable when rules evolve (DECISIONS D-014).
    /// </summary>
    public static class StepConversionRuleV1
    {
        public const int RuleVersion = 1;
        public const long StepsPerVitality = 100L;

        public static long ConvertSteps(long clampedSteps) =>
            clampedSteps / StepsPerVitality;
    }

    /// <summary>Durable proof that one logical activity record was already trusted.</summary>
    public sealed record ProcessedRecordEntry(
        string IdentityKey,
        int ConversionRuleVersion,
        long EligibleSteps,
        long VitalityCredited,
        DateTimeOffset ProcessedAtUtc);

    /// <summary>
    /// Append-only processed-record ledger: the durable dedup structure answering
    /// "have we seen this logical record, under which rule, for how much value?".
    /// Kept even when conversion yields zero Vitality so replays stay no-ops.
    /// </summary>
    public sealed class ProcessedRecordLedgerState
    {
        public Dictionary<string, ProcessedRecordEntry> Entries { get; } =
            new Dictionary<string, ProcessedRecordEntry>(StringComparer.Ordinal);

        public bool HasProcessed(string identityKey) =>
            identityKey != null && Entries.ContainsKey(identityKey);

        public void Record(ProcessedRecordEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (!Entries.TryAdd(entry.IdentityKey, entry))
                throw new InvalidOperationException("Processed-record ledger already contains '" + entry.IdentityKey + "'.");
        }

        public int Count => Entries.Count;

        public long TotalVitalityCredited
        {
            get
            {
                long total = 0L;
                foreach (var entry in Entries.Values)
                    total += entry.VitalityCredited;
                return total;
            }
        }
    }

    /// <summary>
    /// Stable reward-transaction identity derived from record identity plus conversion
    /// rule version (ACTIVITY_PIPELINE.md §8): replaying the same logical record through
    /// the same rule always produces the same transaction ID, making reward application
    /// idempotent even across restarts, retries, and re-queries.
    /// </summary>
    public static class ActivityRewardIds
    {
        public static Guid DeriveTransactionGuid(string identityKey, int conversionRuleVersion)
        {
            if (string.IsNullOrWhiteSpace(identityKey))
                throw new ArgumentException("Identity key must be non-empty.", nameof(identityKey));

            string canonical = "rtx1|" + identityKey + "|rule=" +
                conversionRuleVersion.ToString(CultureInfo.InvariantCulture);
            byte[] hash;
            using (var sha = SHA256.Create())
                hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(canonical));

            var guidBytes = new byte[16];
            Array.Copy(hash, guidBytes, 16);
            return new Guid(guidBytes);
        }
    }
}
