using System;
using System.Collections.Generic;
using RewardTransactionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RewardTransactionIdKind>;
using WalkGame.Domain.Economy;

namespace WalkGame.Domain.Activity
{
    /// <summary>
    /// A durable reward operation with stable identity. The transaction ID is what makes
    /// replay/crash/retry safe: applying the same transaction twice is a no-op.
    /// </summary>
    public sealed record RewardTransaction(RewardTransactionId TransactionId, DateTimeOffset OccurredAtUtc, long VitalityAmount, string Reason);

    public enum LedgerApplyOutcome
    {
        AppliedFirstTime = 0,
        DuplicateIgnored = 1,
    }

    public sealed record LedgerRecord(string TransactionId, DateTimeOffset OccurredAtUtc, long VitalityAmount, string Reason);

    /// <summary>
    /// Append-only reward ledger. Exactly-once crediting: an unknown transaction credits
    /// Vitality exactly once; any re-application (replay, crash recovery, retry) is
    /// detected by durable transaction identity and ignored.
    /// </summary>
    public sealed class RewardLedgerState
    {
        public List<LedgerRecord> Records { get; } = new List<LedgerRecord>();

        private HashSet<string>? _index;

        /// <summary>Maintained lifetime total (D-066). Null on pre-D-066 saves; hydrated on first mutation.</summary>
        public long? MaintainedVitalityCredited { get; set; }

        /// <summary>Net Vitality carried by retired activity-derived rows.</summary>
        public long RetiredVitalityCredited { get; set; }

        /// <summary>Number of activity-derived rows retired so far.</summary>
        public long RetiredCount { get; set; }

        /// <summary>Times the transaction index was rebuilt from durable rows (test/diagnostic accelerator counter; not persisted).</summary>
        public int IndexBuildCount { get; private set; }

        public long TotalVitalityCredited => MaintainedVitalityCredited ?? RecomputeLifetimeVitality();

        public long RecomputeLifetimeVitality()
        {
            long total = RetiredVitalityCredited;
            foreach (var record in Records)
                total += record.VitalityAmount;
            return total;
        }

        /// <summary>
        /// Retires up to <paramref name="maxBatch"/> activity-derived rows (reason prefix
        /// "ingest") from the front of the ledger whose commit instant is strictly before
        /// <paramref name="cutoffUtc"/>. Stops at the first row that does not qualify, so
        /// retirement is deterministic and never reorders history. Non-activity rows are
        /// never retired: their identity may be re-presented by a caller, and only activity
        /// identities are protected upstream by the retired-identity set (D-066).
        /// Lifetime totals are unchanged.
        /// </summary>
        public int RetireActivityRows(DateTimeOffset cutoffUtc, int maxBatch)
        {
            long baseline = MaintainedVitalityCredited ?? RecomputeLifetimeVitality();
            int n = 0;
            while (n < maxBatch && n < Records.Count)
            {
                var r = Records[n];
                if (r.OccurredAtUtc >= cutoffUtc || r.Reason == null ||
                    !r.Reason.StartsWith("ingest", StringComparison.Ordinal))
                    break;
                n++;
            }
            if (n == 0) return 0;
            for (int i = 0; i < n; i++)
            {
                RetiredVitalityCredited = checked(RetiredVitalityCredited + Records[i].VitalityAmount);
                _index?.Remove(Records[i].TransactionId);
            }
            Records.RemoveRange(0, n);
            RetiredCount += n;
            MaintainedVitalityCredited = baseline;
            return n;
        }

        public bool HasTransaction(RewardTransactionId transactionId)
        {
            EnsureIndex();
            return _index!.Contains(transactionId.Value);
        }

        public LedgerApplyOutcome Apply(RewardTransaction transaction, ResourceBalances balances)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (balances == null) throw new ArgumentNullException(nameof(balances));
            if (transaction.VitalityAmount < 0L)
                throw new ArgumentOutOfRangeException(nameof(transaction), "Reward transactions cannot be negative; corrections are an M2 pipeline policy.");

            EnsureIndex();
            long baseline = MaintainedVitalityCredited ?? RecomputeLifetimeVitality();
            if (!_index!.Add(transaction.TransactionId.Value))
                return LedgerApplyOutcome.DuplicateIgnored;

            balances.Add(ResourceType.Vitality, transaction.VitalityAmount);
            Records.Add(new LedgerRecord(
                transaction.TransactionId.Value,
                transaction.OccurredAtUtc,
                transaction.VitalityAmount,
                transaction.Reason ?? string.Empty));
            MaintainedVitalityCredited = checked(baseline + transaction.VitalityAmount);
            return LedgerApplyOutcome.AppliedFirstTime;
        }

        /// <summary>
        /// Applies a pipeline correction of either sign with exactly-once identity.
        /// Negative amounts must be pre-clamped to the current balance by the correction
        /// policy; the guard below is defense in depth so the balance can never go negative.
        /// </summary>
        public LedgerApplyOutcome ApplyCorrection(RewardTransaction transaction, ResourceBalances balances)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (balances == null) throw new ArgumentNullException(nameof(balances));
            if (transaction.VitalityAmount < 0L &&
                balances.Get(ResourceType.Vitality) + transaction.VitalityAmount < 0L)
                throw new InvalidOperationException(
                    "Correction would drive the balance negative; clamp reversals before applying.");

            EnsureIndex();
            long baseline = MaintainedVitalityCredited ?? RecomputeLifetimeVitality();
            if (!_index!.Add(transaction.TransactionId.Value))
                return LedgerApplyOutcome.DuplicateIgnored;

            if (transaction.VitalityAmount < 0L)
            {
                if (!balances.TryConsume(ResourceType.Vitality, -transaction.VitalityAmount))
                    throw new InvalidOperationException(
                        "Correction deduction exceeded the available balance.");
            }
            else
            {
                balances.Add(ResourceType.Vitality, transaction.VitalityAmount);
            }

            Records.Add(new LedgerRecord(
                transaction.TransactionId.Value,
                transaction.OccurredAtUtc,
                transaction.VitalityAmount,
                transaction.Reason ?? string.Empty));
            MaintainedVitalityCredited = checked(baseline + transaction.VitalityAmount);
            return LedgerApplyOutcome.AppliedFirstTime;
        }

        private void EnsureIndex()
        {
            if (_index != null)
                return;
            IndexBuildCount++;
            _index = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in Records)
                _index.Add(record.TransactionId);
        }
    }
}
