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
        DateTimeOffset EndUtc,
        int Revision = 1,
        bool IsDeletion = false);

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
        OutsideHorizon = 8,
    }

    /// <summary>
    /// The trusted reference the acceptance window is evaluated against (D-053).
    ///
    /// The window used to be anchored to the RAW device clock ("now minus 14 days",
    /// "now plus 10 minutes"). Both anchors are wrong for the two situations this
    /// product is built for: a returning player has a provider backlog that is entirely
    /// older than 14 days, and a device whose clock runs slightly fast makes every
    /// record look future-dated so crediting stops entirely.
    ///
    /// This value carries the durable evidence the pipeline already owns, so validation
    /// stays a pure, O(1)-per-record function with no dependency on the state aggregate
    /// (scripts/verify-architecture.sh rule 5 keeps Domain free of ambient state). It is
    /// computed ONCE per ingestion batch and threaded through every record.
    /// </summary>
    public readonly struct TrustedTime
    {
        /// <summary>Clock time injected by the host for this batch.</summary>
        public DateTimeOffset NowUtc { get; }

        /// <summary>Latest activity end the pipeline has already durably trusted.</summary>
        public DateTimeOffset LatestTrustedEndUtc { get; }

        /// <summary>Durable reconciliation watermark (monotonic; never regresses).</summary>
        public DateTimeOffset ReconciliationWatermarkUtc { get; }

        public TrustedTime(DateTimeOffset nowUtc, DateTimeOffset latestTrustedEndUtc,
            DateTimeOffset reconciliationWatermarkUtc)
        {
            NowUtc = nowUtc;
            LatestTrustedEndUtc = latestTrustedEndUtc;
            ReconciliationWatermarkUtc = reconciliationWatermarkUtc;

            // LOWER bound: the EARLIER of the two durable trusted markers, less the floor.
            // Anchored to progress rather than to wall clock, so it slides forward as the
            // player progresses and a whole absence-long backlog stays inside it. With no
            // trusted progress at all (a profile that has never ingested a record) the
            // anchor falls back to "now", which is what keeps a brand-new profile from
            // inheriting the device's entire history.
            bool haveTrustedProgress =
                latestTrustedEndUtc != default || reconciliationWatermarkUtc != default;
            DateTimeOffset lowerAnchor = haveTrustedProgress
                ? (latestTrustedEndUtc <= reconciliationWatermarkUtc
                    ? latestTrustedEndUtc
                    : reconciliationWatermarkUtc)
                : nowUtc;

            // UPPER bound: the LATER of now and both durable markers, plus the skew
            // allowance. Taking the max is what stops a merely-fast device clock from
            // disabling crediting, and what stops a backward clock jump from moving the
            // bound below the already-trusted end (the watermark still never regresses).
            DateTimeOffset upperAnchor = nowUtc;
            if (latestTrustedEndUtc > upperAnchor) upperAnchor = latestTrustedEndUtc;
            if (reconciliationWatermarkUtc > upperAnchor) upperAnchor = reconciliationWatermarkUtc;

            // DateTimeOffset arithmetic is NOT saturating: `DateTimeOffset.MinValue - 45d`
            // throws. Guard the subtraction itself, then clamp the sum as well, so a
            // profile whose trusted progress sits at the representable minimum degrades to
            // "reject everything" instead of throwing during validation.
            WindowStartUtc = SafeSubtract(lowerAnchor, ActivityValidationPolicy.TrustedWindowFloor);
            WindowEndUtc = SafeAdd(upperAnchor, ActivityValidationPolicy.MaxFutureSkew);
        }

        /// <summary>Inclusive lower bound: records ending before this are OutsideHorizon.</summary>
        public DateTimeOffset WindowStartUtc { get; }

        /// <summary>Inclusive upper bound: records ending after this are FutureTimestamp.</summary>
        public DateTimeOffset WindowEndUtc { get; }

        // DateTimeOffset arithmetic is NOT saturating: `MinValue - 45d` throws. Guard the
        // operation itself against the distance available to the representable limit, so a
        // profile whose trusted progress sits at the minimum degrades to "reject
        // everything" instead of throwing during validation.
        private static DateTimeOffset SafeSubtract(DateTimeOffset value, TimeSpan amount) =>
            amount >= value - DateTimeOffset.MinValue
                ? DateTimeOffset.MinValue
                : value - amount;

        private static DateTimeOffset SafeAdd(DateTimeOffset value, TimeSpan amount) =>
            amount >= DateTimeOffset.MaxValue - value
                ? DateTimeOffset.MaxValue
                : value + amount;
    }

    /// <summary>
    /// Validation and bounding rules for normalized records. Deterministic given the
    /// trusted-time reference; pathological quantities are clamped (not dropped) so a
    /// glitched source value cannot create unbounded rewards while honest large values
    /// still count.
    /// </summary>
    public static class ActivityValidationPolicy
    {
        /// <summary>
        /// Compared against the trusted reference's UPPER bound, which is the later of the
        /// current time and the latest already-trusted record end. Comparing against a
        /// durable trusted mark rather than the raw device clock is what stops a clock that
        /// is merely minutes fast from making every record future-dated (F-03), while a
        /// source that pre-mints activity genuinely ahead of all trusted progress is still
        /// rejected.
        /// </summary>
        public static readonly TimeSpan MaxFutureSkew = TimeSpan.FromMinutes(10);

        /// <summary>
        /// How far BEFORE the last already-trusted record end the pipeline will still
        /// believe a record, compared against the trusted reference's LOWER bound (the
        /// earlier of the watermark and the latest trusted end, less this floor).
        ///
        /// Supersedes the old wall-clock `ReconciliationHorizon` of 14 days, which was the
        /// reason an entire returning player's backlog was rejected (F-02). Anchoring to
        /// durable trusted progress is what makes an absence of any length creditable; the
        /// floor is what keeps it bounded. 45 days comfortably exceeds the longest absence
        /// the product treats as normal (30-day return is the acceptance case, MASTER_PLAN
        /// §10) and is far below unbounded device history. See D-053.
        /// </summary>
        public static readonly TimeSpan TrustedWindowFloor = TimeSpan.FromDays(45);

        /// <summary>
        /// Maximum processed-record rows (and, separately, reward-ledger rows) retired in a
        /// single commit (D-066). Retirement is amortized inside ordinary ingestion commits so
        /// no single commit's work is unbounded; a mature legacy save drains over a few
        /// commits. Retention bound = <see cref="TrustedWindowFloor"/> behind trusted progress.
        /// </summary>
        public const int RetirementBatchSize = 256;

        /// <summary>
        /// Deprecated wall-clock name for the old 14-day constant, retained only so the
        /// historical decision (D-030) can be located in code review. It is NOT used by
        /// validation any more: the window is anchored to trusted time, not to wall clock.
        /// </summary>
        public static readonly TimeSpan ReconciliationHorizon = TimeSpan.FromDays(14);

        /// <summary>Upper clamp on steps credited from a single record (~4x an extreme ultramarathon day).</summary>
        public const long MaxStepsPerRecord = 250_000L;

        public static long ClampQuantity(ActivityCategory category, long quantity)
        {
            if (category != ActivityCategory.Walking || quantity < MaxStepsPerRecord)
                return Math.Max(0L, quantity);
            return MaxStepsPerRecord;
        }

        public static ActivityValidationStatus Validate(NormalizedActivityRecord record, TrustedTime trustedTime)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));

            if (string.IsNullOrWhiteSpace(record.ProviderNamespace))
                return ActivityValidationStatus.EmptyProvider;
            if (record.Category != ActivityCategory.Walking)
                return ActivityValidationStatus.UnsupportedCategory;
            if (!string.Equals(record.Unit, ActivityUnits.Steps, StringComparison.Ordinal))
                return ActivityValidationStatus.UnsupportedUnit;

            // A DELETION MARKER is judged by the same eligibility rules, with exactly one
            // documented exception: the quantity rules do not apply, because a marker
            // carries no activity by definition and every well-formed marker in this
            // repository has Quantity == 0. Applying ZeroQuantity to a marker would reject
            // every deletion ever issued and silently disable the correction policy, which
            // is the opposite of "the same eligibility rules". The checks that DO apply are
            // provenance, unit, the temporal extent, and both window bounds.
            if (!record.IsDeletion)
            {
                if (record.Quantity == 0L)
                    return ActivityValidationStatus.ZeroQuantity;
                if (record.Quantity < 0L)
                    return ActivityValidationStatus.NegativeQuantity;
            }

            if (record.StartUtc == default
                || record.EndUtc == default
                || record.EndUtc <= record.StartUtc)
                return ActivityValidationStatus.MalformedTimestamps;
            if (record.EndUtc > trustedTime.WindowEndUtc)
                return ActivityValidationStatus.FutureTimestamp;
            if (record.EndUtc < trustedTime.WindowStartUtc)
                return ActivityValidationStatus.OutsideHorizon;

            return ActivityValidationStatus.Valid;
        }

        /// <summary>
        /// Convenience overload for callers with no durable trusted progress to anchor to
        /// (a profile that has never ingested a record). The window then falls back to
        /// "now" as its anchor, so a brand-new profile is bounded exactly as before.
        /// </summary>
        public static ActivityValidationStatus Validate(NormalizedActivityRecord record, DateTimeOffset nowUtc) =>
            Validate(record, new TrustedTime(nowUtc, default, default));
    }

    /// <summary>
    /// Thread-scoped SHA-256 reuse: hashing is deterministic regardless of instance,
    /// so large ingestion batches and reward-ID derivation avoid one transform
    /// construction per call. Instances are never shared across threads.
    /// </summary>
    internal static class Sha256Pool
    {
        [ThreadStatic] private static System.Security.Cryptography.SHA256? _instance;

        public static System.Security.Cryptography.SHA256 Rent() =>
            _instance ??= System.Security.Cryptography.SHA256.Create();
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

            byte[] hash = Sha256Pool.Rent().ComputeHash(System.Text.Encoding.UTF8.GetBytes(canonical));
            return ToHex(hash);
        }

        private static string CategoryToken(ActivityCategory category) =>
            ((int)category).ToString(CultureInfo.InvariantCulture);

        private static string Normalize(string value) => (value ?? string.Empty).Trim().ToLowerInvariant();

        private static string ToHex(byte[] bytes)
        {
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            // Lowercase hex via table lookup: identical output to byte.ToString("x2",
            // InvariantCulture) without per-byte boxing and culture-aware formatting on
            // the ingest fingerprint hot path.
            const string HexDigits = "0123456789abcdef";
            var chars = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                chars[i * 2] = HexDigits[bytes[i] >> 4];
                chars[i * 2 + 1] = HexDigits[bytes[i] & 0xF];
            }
            return new string(chars);
#else
            return Convert.ToHexString(bytes).ToLowerInvariant();
#endif
        }
    }

    /// <summary>
    /// Conversion rule version 1: integer math, floor rounding, documented unit.
    /// 100 walking steps convert to 1 Vitality; the sub-unit remainder carries per provider (D-079).
    /// Rule version is stored on every processed-record entry so historical credits
    /// remain auditable when rules evolve (DECISIONS D-014).
    /// </summary>
    public static class StepConversionRuleV1
    {
        public const int RuleVersion = 1;
        public const long StepsPerVitality = 100L;

        public static long ConvertSteps(long clampedSteps) =>
            clampedSteps / StepsPerVitality;

        /// <summary>Largest legal carried remainder (D-079).</summary>
        public const long MaxCarry = StepsPerVitality - 1L;

        /// <summary>
        /// Converts steps with the provider's carried sub-unit remainder (D-079, KI-019):
        /// returns the Vitality for <c>carry + steps</c> and the new remainder (0..99).
        /// A negative pool (a reversal that consumed more carry than exists) clamps to 0;
        /// the player is never charged for it.
        /// </summary>
        public static long ConvertWithCarry(long carry, long steps, out long newCarry)
        {
            long pool = carry + steps;
            if (pool < 0L) pool = 0L;
            newCarry = pool % StepsPerVitality;
            return pool / StepsPerVitality;
        }
    }

    /// <summary>Durable proof that one logical activity record was already trusted.</summary>
    public sealed record ProcessedRecordEntry(
        string IdentityKey,
        int ConversionRuleVersion,
        long EligibleSteps,
        long VitalityCredited,
        DateTimeOffset ProcessedAtUtc,
        int LastRevision = 1,
        DateTimeOffset? RecordEndUtc = null);

    /// <summary>
    /// Append-only processed-record ledger: the durable dedup structure answering
    /// "have we seen this logical record, under which rule, for how much value?".
    /// Kept even when conversion yields zero Vitality so replays stay no-ops.
    /// </summary>
    public sealed class ProcessedRecordLedgerState
    {
        public Dictionary<string, ProcessedRecordEntry> Entries { get; } =
            new Dictionary<string, ProcessedRecordEntry>(StringComparer.Ordinal);

        /// <summary>
        /// Identities of records whose detail row was retired (D-066). The identity is the
        /// trust anchor and is kept forever; only the audit detail is dropped. A retired
        /// identity is permanently non-creditable. Absent decodes to "nothing retired".
        /// Appended in deterministic retirement order.
        /// </summary>
        public List<string> RetiredIdentities { get; } = new List<string>();

        /// <summary>Number of rows retired so far (durable bookkeeping, D-066).</summary>
        public long RetiredCount { get; set; }

        /// <summary>Commit instant of the most recent retirement; null when nothing was retired.</summary>
        public DateTimeOffset? LastRetiredAtUtc { get; set; }

        /// <summary>Net applied Vitality carried by retired rows (so the lifetime total survives retirement).</summary>
        public long RetiredVitalityCredited { get; set; }

        /// <summary>
        /// Maintained lifetime credited total, updated in <see cref="Record"/>/<see cref="Update"/>.
        /// Null on saves written before D-066; hydrated from a recomputation on first mutation.
        /// The validator cross-checks it against <see cref="RecomputeLifetimeVitality"/>.
        /// </summary>
        public long? MaintainedVitalityCredited { get; set; }

        private HashSet<string>? _retiredIndex;

        public bool HasProcessed(string identityKey) =>
            identityKey != null && Entries.ContainsKey(identityKey);

        public bool IsRetired(string identityKey)
        {
            if (identityKey == null) return false;
            EnsureRetiredIndex();
            return _retiredIndex!.Contains(identityKey);
        }

        public bool TryGet(string identityKey, out ProcessedRecordEntry? entry)
        {
            if (identityKey != null && Entries.TryGetValue(identityKey, out var found))
            {
                entry = found;
                return true;
            }
            entry = null;
            return false;
        }

        public void Record(ProcessedRecordEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (IsRetired(entry.IdentityKey))
                throw new InvalidOperationException("Processed-record '" + entry.IdentityKey + "' was retired and is permanently non-creditable.");
            long baseline = HydratedTotal();
            if (!Entries.TryAdd(entry.IdentityKey, entry))
                throw new InvalidOperationException("Processed-record ledger already contains '" + entry.IdentityKey + "'.");
            MaintainedVitalityCredited = checked(baseline + entry.VitalityCredited);
        }

        /// <summary>Replaces an existing row during correction processing; must already exist.</summary>
        public void Update(ProcessedRecordEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (!Entries.TryGetValue(entry.IdentityKey, out var previous))
                throw new InvalidOperationException("Processed-record ledger has no row '" + entry.IdentityKey + "' to update.");
            long baseline = HydratedTotal();
            Entries[entry.IdentityKey] = entry;
            MaintainedVitalityCredited = checked(baseline - previous.VitalityCredited + entry.VitalityCredited);
        }

        /// <summary>
        /// Retires up to <paramref name="maxBatch"/> rows whose record end (or, for rows
        /// written before record ends were stored, their processing instant) lies strictly
        /// before <paramref name="cutoffUtc"/>. Selection is deterministic: ordered by
        /// processing instant, then ordinal identity key. Changes no lifetime total and no
        /// balance; only retained detail moves to the identity-only retired set (D-066).
        /// </summary>
        public int Retire(DateTimeOffset cutoffUtc, DateTimeOffset nowUtc, int maxBatch)
        {
            if (maxBatch <= 0 || Entries.Count == 0) return 0;
            var candidates = new List<ProcessedRecordEntry>();
            foreach (var entry in Entries.Values)
            {
                DateTimeOffset end = entry.RecordEndUtc ?? entry.ProcessedAtUtc;
                if (end < cutoffUtc)
                    candidates.Add(entry);
            }
            if (candidates.Count == 0) return 0;
            candidates.Sort((a, b) =>
            {
                int c = a.ProcessedAtUtc.UtcTicks.CompareTo(b.ProcessedAtUtc.UtcTicks);
                return c != 0 ? c : string.CompareOrdinal(a.IdentityKey, b.IdentityKey);
            });
            long baseline = HydratedTotal();
            EnsureRetiredIndex();
            int n = Math.Min(maxBatch, candidates.Count);
            for (int i = 0; i < n; i++)
            {
                var entry = candidates[i];
                Entries.Remove(entry.IdentityKey);
                RetiredIdentities.Add(entry.IdentityKey);
                _retiredIndex!.Add(entry.IdentityKey);
                RetiredVitalityCredited = checked(RetiredVitalityCredited + entry.VitalityCredited);
            }
            RetiredCount += n;
            LastRetiredAtUtc = nowUtc;
            MaintainedVitalityCredited = baseline; // unchanged by construction
            return n;
        }

        public int Count => Entries.Count;

        /// <summary>
        /// Per-provider sub-unit step remainder (D-079, KI-019): steps that did not yet make a
        /// whole Vitality. Absent on older saves, which decode to "no carry". Values are 0..99.
        /// </summary>
        public Dictionary<string, long> StepCarryByProvider { get; } =
            new Dictionary<string, long>(StringComparer.Ordinal);

        public long GetCarry(string provider) =>
            provider != null && StepCarryByProvider.TryGetValue(provider, out var c) ? c : 0L;

        public void SetCarry(string provider, long carry)
        {
            if (provider == null) return;
            if (carry == 0L) StepCarryByProvider.Remove(provider);
            else StepCarryByProvider[provider] = carry;
        }

        /// <summary>
        /// Cumulative reversal remainder the correction policy refused to claw back because
        /// the unspent balance was too low. Bounded trust-impact accounting: completed world
        /// content is never destroyed by a source correction (ACTIVITY_PIPELINE §11).
        /// </summary>
        public long UnappliedReversalVitality { get; set; }

        /// <summary>Lifetime credited total in O(1) (maintained; recomputed only for legacy saves).</summary>
        public long TotalVitalityCredited => MaintainedVitalityCredited ?? RecomputeLifetimeVitality();

        /// <summary>Full recomputation: retained rows plus the durably recorded retired total.</summary>
        public long RecomputeLifetimeVitality()
        {
            long total = RetiredVitalityCredited;
            foreach (var entry in Entries.Values)
                total += entry.VitalityCredited;
            return total;
        }

        private long HydratedTotal() => MaintainedVitalityCredited ?? RecomputeLifetimeVitality();

        private void EnsureRetiredIndex()
        {
            if (_retiredIndex != null && _retiredIndex.Count == RetiredIdentities.Count) return;
            _retiredIndex = new HashSet<string>(RetiredIdentities, StringComparer.Ordinal);
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
            return Derive(canonical);
        }

        /// <summary>
        /// Stable identity for a correction adjustment: distinct namespace from first-time
        /// credit IDs so a correction can never collide with (and be swallowed by) the
        /// original reward transaction.
        /// </summary>
        public static Guid DeriveCorrectionGuid(string identityKey, int conversionRuleVersion, int revision, long appliedAmount)
        {
            if (string.IsNullOrWhiteSpace(identityKey))
                throw new ArgumentException("Identity key must be non-empty.", nameof(identityKey));

            string canonical = "rtx-corr1|" + identityKey + "|rule=" +
                conversionRuleVersion.ToString(CultureInfo.InvariantCulture) +
                "|rev=" + revision.ToString(CultureInfo.InvariantCulture) +
                "|amt=" + appliedAmount.ToString(CultureInfo.InvariantCulture);
            return Derive(canonical);
        }

        private static Guid Derive(string canonical)
        {
            byte[] hash = Sha256Pool.Rent().ComputeHash(System.Text.Encoding.UTF8.GetBytes(canonical));

            var guidBytes = new byte[16];
            Array.Copy(hash, guidBytes, 16);
            return new Guid(guidBytes);
        }
    }
}
