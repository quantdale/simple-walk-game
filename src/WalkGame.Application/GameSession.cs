using System;
using System.Collections.Generic;
using System.IO;
using WalkGame.Application.Activity;
using WalkGame.Application.Persistence;
using WalkGame.Application.ReadModels;
using WalkGame.Application.Ux;
using WalkGame.Application.Summaries;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Common;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Simulation;
using WalkGame.Domain.Summaries;
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;
using RewardTransactionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RewardTransactionIdKind>;

namespace WalkGame.Application
{
    public enum StartStatus
    {
        NewGameCreated = 0,
        Loaded = 1,
        RecoveredFromBackup = 2,
        NoSaveFound = 3,
        SaveUnreadable = 4,
        StateInvalid = 5,
    }

        /// <summary>Boot outcome classification for the support diagnostics projection.</summary>
    public enum DiagnosticsBootOutcome
    {
        NeverBooted = 0,
        NewGameCreated = 1,
        Loaded = 2,
        RecoveredFromBackup = 3,
        NoSaveFound = 4,
        SaveUnreadable = 5,
        StateInvalid = 6,
    }

    /// <summary>Structured failure category of the last failed boot decode attempt.</summary>
    public enum CodecFailureCategory
    {
        None = 0,
        MalformedEnvelope = 1,
        ChecksumMismatch = 2,
        VersionTooOld = 3,
        VersionTooNew = 4,
        MigrationFailed = 5,
        DeserializationFailed = 6,
        StateValidationFailed = 7,
    }

    /// <summary>Stable application-level error codes for UX contract operations.</summary>
    public static class UxErrorCodes
    {
        public const string PreferencesStoreMissing = "ux.preferences-store-missing";
        public const string InvalidReminderTime = "ux.invalid-reminder-time";
        public const string InvalidOnboardingTarget = "ux.invalid-onboarding-target";
        public const string OnboardingPrerequisite = "ux.onboarding-prerequisite-not-met";
        public const string ConnectionPortMissing = "ux.connection-port-missing";
    }

    public sealed class StartResult
    {
        public StartStatus Status { get; }
        public IReadOnlyList<string> SummaryLines { get; }

        /// <summary>Typed durable summary snapshot (same content as SummaryLines).</summary>
        public ReturnSummaryReadModel? Summary { get; }

        public string? Detail { get; }

        internal StartResult(StartStatus status, IReadOnlyList<string>? summaryLines = null,
            ReturnSummaryReadModel? summary = null, string? detail = null)
        {
            Status = status;
            SummaryLines = summaryLines ?? Array.Empty<string>();
            Summary = summary;
            Detail = detail;
        }
    }

    public sealed class CreditResult
    {
        public bool DuplicateIgnored { get; }
        public bool Saved { get; }
        public IReadOnlyList<string> SummaryLines { get; }
        public ReturnSummaryReadModel? Summary { get; }

        internal CreditResult(bool duplicateIgnored, bool saved, IReadOnlyList<string> summaryLines,
            ReturnSummaryReadModel? summary)
        {
            DuplicateIgnored = duplicateIgnored;
            Saved = saved;
            SummaryLines = summaryLines;
            Summary = summary;
        }
    }

    /// <summary>Per-batch trust diagnostics: where every received record went.</summary>
    public sealed class IngestResult
    {
        public int TotalReceived { get; }
        public int Accepted { get; }
        public int Rejected { get; }
        public int DuplicatesIgnored { get; }

        /// <summary>Higher-revision redeliveries applied as value adjustments.</summary>
        public int CorrectionsApplied { get; }

        /// <summary>Deletion markers that removed remaining credited value.</summary>
        public int DeletionsApplied { get; }

        /// <summary>Deletions for logical records this pipeline never credited.</summary>
        public int DeletionsIgnored { get; }

        /// <summary>Redeliveries carrying a revision already processed or older.</summary>
        public int StaleRevisionsIgnored { get; }

        public int QuantityClamped { get; }
        public int DuplicateTransactionsIgnored { get; }
        public long VitalityCredited { get; }

        /// <summary>Cumulative reversals policy refused because the balance was too low.</summary>
        public long UnappliedReversalVitality { get; }

        public bool Saved { get; }
        public IReadOnlyDictionary<string, int> RejectionCounts { get; }
        public IReadOnlyList<string> SummaryLines { get; }

        /// <summary>Typed durable summary snapshot reflecting this batch's committed changes.</summary>
        public ReturnSummaryReadModel? Summary { get; }

        internal IngestResult(
            int totalReceived, int accepted, int rejected, int duplicatesIgnored,
            int correctionsApplied, int deletionsApplied, int deletionsIgnored,
            int staleRevisionsIgnored, int quantityClamped, int duplicateTransactionsIgnored,
            long vitalityCredited, long unappliedReversalVitality,
            bool saved, IReadOnlyDictionary<string, int> rejectionCounts,
            IReadOnlyList<string> summaryLines, ReturnSummaryReadModel? summary)
        {
            TotalReceived = totalReceived;
            Accepted = accepted;
            Rejected = rejected;
            DuplicatesIgnored = duplicatesIgnored;
            CorrectionsApplied = correctionsApplied;
            DeletionsApplied = deletionsApplied;
            DeletionsIgnored = deletionsIgnored;
            StaleRevisionsIgnored = staleRevisionsIgnored;
            QuantityClamped = quantityClamped;
            DuplicateTransactionsIgnored = duplicateTransactionsIgnored;
            VitalityCredited = vitalityCredited;
            UnappliedReversalVitality = unappliedReversalVitality;
            Saved = saved;
            RejectionCounts = rejectionCounts;
            SummaryLines = summaryLines;
            Summary = summary;
        }
    }

    /// <summary>
    /// Application use-case orchestration: load/save, offline advancement, activity
    /// crediting, queue management, read models and return summaries.
    /// Owns no platform behavior. Presentation calls these operations; it never edits
    /// domain state directly.
    ///
    /// Persistence policy (M1): every committed mutation saves immediately — a save is a
    /// few kilobytes, and durability before presentation is the architectural contract.
    /// </summary>
    public sealed class GameSession
    {
        private readonly ISaveStore _store;
        private readonly ISaveCodec _codec;
        private readonly IClock _clock;
        private readonly RegionDefinition _content;
        private readonly IUxPreferencesStore? _preferencesStore;
        private readonly IActivityConnectionPort? _connectionPort;

        private GameState? _state;

        /// <summary>Cached local UX preferences; documented defaults until a store is wired.</summary>
        private UxPreferencesState _preferences = UxPreferencesState.CreateDefault();

        private UxPreferencesLoadOutcome _preferencesLoadOutcome = UxPreferencesLoadOutcome.NotFound;

        private string? _preferencesLoadDetail;

        // Boot evidence for the support diagnostics projection (bounded, privacy-safe).
        private DiagnosticsBootOutcome _lastBootOutcome = DiagnosticsBootOutcome.NeverBooted;
        private bool _lastBootRecoveredFromBackup;
        private CodecFailureCategory _lastBootCodecFailure = CodecFailureCategory.None;
        private IReadOnlyList<string> _lastBootAppliedMigrations = Array.Empty<string>();

        public GameSession(ISaveStore store, ISaveCodec codec, IClock clock, RegionDefinition content)
            : this(store, codec, clock, content, preferencesStore: null, connectionPort: null)
        {
        }

        public GameSession(ISaveStore store, ISaveCodec codec, IClock clock, RegionDefinition content,
            IUxPreferencesStore? preferencesStore)
            : this(store, codec, clock, content, preferencesStore, connectionPort: null)
        {
        }

        public GameSession(ISaveStore store, ISaveCodec codec, IClock clock, RegionDefinition content,
            IUxPreferencesStore? preferencesStore, IActivityConnectionPort? connectionPort)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _preferencesStore = preferencesStore;
            _connectionPort = connectionPort;

            LoadPreferencesFromStore();

            var violations = ContentValidator.Validate(_content);
            if (violations.Count > 0)
                throw new ArgumentException("Invalid region content: " + string.Join("; ", violations), nameof(content));
        }

        /// <summary>
        /// Loads the durable local preferences once at construction. A damaged or future-
        /// version record degrades to documented defaults instead of blocking boot (D-042):
        /// preferences are never allowed to prevent gameplay.
        /// </summary>
        private void LoadPreferencesFromStore()
        {
            if (_preferencesStore == null)
                return;

            var result = _preferencesStore.Load();
            _preferencesLoadOutcome = result.Outcome;
            _preferencesLoadDetail = result.Detail;
            _preferences = result.State != null
                ? result.State
                : UxPreferencesState.CreateDefault();
        }

        public bool HasLoadedState => _state != null;

        public RegionDefinition Content => _content;

        public StartResult StartNewGame(ulong seed)
        {
            var nowUtc = _clock.UtcNow;
            var fresh = GameFactory.NewGame(_content, nowUtc, seed);

            byte[] envelopeBytes;
            try
            {
                envelopeBytes = _codec.Encode(fresh, nowUtc);
                _store.WriteAtomic(envelopeBytes);
            }
            catch (IOException ex)
            {
                return new StartResult(StartStatus.SaveUnreadable, detail: "Could not create a new save: " + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return new StartResult(StartStatus.SaveUnreadable, detail: "Could not create a new save: " + ex.Message);
            }

            _state = fresh;
            CaptureBootOutcome(DiagnosticsBootOutcome.NewGameCreated, recoveredFromBackup: false);
            return new StartResult(StartStatus.NewGameCreated, new[]
            {
                "A new restoration begins in " + _content.TitleKey + ".",
            });
        }

        /// <summary>
        /// Boot flow: locate save → validate integrity → fall back to backup → migrate →
        /// validate invariants → advance offline systems → persist → return summary.
        /// </summary>
        public StartResult Continue()
        {
            var primary = SafeReadPrimary();
            if (primary.Outcome == SaveReadOutcome.NotFound)
            {
                CaptureBootOutcome(DiagnosticsBootOutcome.NoSaveFound, recoveredFromBackup: false);
                return new StartResult(StartStatus.NoSaveFound);
            }

            string? primaryDecodeDetail = null;
            if (primary.EnvelopeBytes != null)
            {
                var decodedPrimary = DecodeAndValidate(primary.EnvelopeBytes);
                if (decodedPrimary.State != null)
                    return FinishBoot(decodedPrimary.State, recoveredFromBackup: false);
                primaryDecodeDetail = decodedPrimary.Error;
            }

            string? backupDecodeDetail = null;
            var backup = SafeReadBackup();
            if (backup.EnvelopeBytes != null)
            {
                var decodedBackup = DecodeAndValidate(backup.EnvelopeBytes);
                if (decodedBackup.State != null)
                    return FinishBoot(decodedBackup.State, recoveredFromBackup: true);
                backupDecodeDetail = decodedBackup.Error;
            }

            // Surface the specific failure reason (integrity, version, migration,
            // validation) instead of a generic unreadable-save message.
            string reason =
                primaryDecodeDetail ??
                backupDecodeDetail ??
                primary.Detail ??
                backup.Detail ??
                "Save data could not be read.";
            CaptureBootOutcome(DiagnosticsBootOutcome.SaveUnreadable, recoveredFromBackup: false);
            return new StartResult(StartStatus.SaveUnreadable, detail: reason);
        }

        /// <summary>
        /// Applies an activity-derived reward transaction with exactly-once semantics:
        /// durable transaction identity makes replays, retries and crash recovery no-ops.
        /// Low-level diagnostic primitive — the M3 acceptance path uses IngestActivityBatch.
        /// </summary>
        public CreditResult CreditActivity(Guid transactionId, DateTimeOffset occurredAtUtc, long vitalityAmount, string reason)
        {
            var game = RequireState();
            var events = new List<SimulationEvent>();
            var transaction = new RewardTransaction(
                RewardTransactionId.FromGuid(transactionId), occurredAtUtc, vitalityAmount, reason);

            var outcome = game.Ledger.Apply(transaction, game.Resources);
            bool duplicate = outcome == LedgerApplyOutcome.DuplicateIgnored;
            events.Add(duplicate
                ? (SimulationEvent)new ActivityDuplicate(_clock.UtcNow, transaction.TransactionId.Value)
                : new ActivityCredited(_clock.UtcNow, transaction.TransactionId.Value, vitalityAmount));

            OfflineAdvancer.AllocateVitality(game, _content, _clock.UtcNow, events);
            ComposePendingSummary(game, events);

            bool saved = PersistOrThrow();
            return new CreditResult(duplicate, saved,
                FormatSummaryLines(game.PendingReturnSummary), SnapshotSummary(game));
        }

        /// <summary>
        /// Trust-pipeline ingestion (M2): normalized synthetic activity in, validated/
        /// bounded/deduplicated exactly-once rewards out.
        ///
        /// Per record: validate → dedup against the durable processed-record ledger →
        /// higher-revision correction policy (up: credit delta; down: conservative clawback
        /// bounded by the unspent balance) → convert with rule v1 → derive stable reward-
        /// transaction ID → apply idempotently. The processed-record ledger and ingestion
        /// checkpoint advance in the same state transition as the rewards they describe,
        /// persisted atomically once at the end, so a crash can never leave a checkpoint
        /// that outruns credited rewards.
        /// </summary>
        public IngestResult IngestActivityBatch(IReadOnlyList<NormalizedActivityRecord> records)
        {
            if (records == null) throw new ArgumentNullException(nameof(records));

            var game = RequireState();
            var nowUtc = _clock.UtcNow;
            var events = new List<SimulationEvent>();
            var rejectionCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);

            int total = records.Count;
            int accepted = 0, rejected = 0, duplicates = 0, clamped = 0, dupTransactions = 0;
            int corrections = 0, deletionsApplied = 0, deletionsIgnored = 0, staleRevisions = 0;
            long vitalityTotal = 0L;
            DateTimeOffset maxTrustedEndUtc = game.IngestionCheckpointUtc;

            foreach (var record in records)
            {
                if (record.IsDeletion)
                {
                    ProcessDeletion(game, record, nowUtc, events,
                        ref deletionsApplied, ref deletionsIgnored, ref staleRevisions,
                        ref dupTransactions, ref vitalityTotal, ref maxTrustedEndUtc);
                    continue;
                }

                var status = ActivityValidationPolicy.Validate(record, nowUtc);
                if (status != ActivityValidationStatus.Valid)
                {
                    rejected++;
                    string key = status.ToString();
                    rejectionCounts[key] = rejectionCounts.TryGetValue(key, out int n) ? n + 1 : 1;
                    continue;
                }

                string identityKey = ActivityIdentity.Compute(record);
                DateTimeOffset endUtc = record.EndUtc.ToUniversalTime();

                if (game.ProcessedRecords.TryGet(identityKey, out var existing))
                {
                    if (record.Revision <= existing!.LastRevision)
                    {
                        // Replay of an already-trusted revision — exactly-once means ignore.
                        duplicates++;
                        continue;
                    }

                    ApplyCorrectionDelta(game, identityKey, existing, record, nowUtc, events,
                        ref corrections, ref clamped, ref dupTransactions, ref vitalityTotal,
                        ref maxTrustedEndUtc);
                    continue;
                }

                long eligibleSteps = ActivityValidationPolicy.ClampQuantity(record.Category, record.Quantity);
                if (eligibleSteps != record.Quantity)
                    clamped++;

                long vitality = StepConversionRuleV1.ConvertSteps(eligibleSteps);

                if (vitality > 0L)
                {
                    var txGuid = ActivityRewardIds.DeriveTransactionGuid(identityKey, StepConversionRuleV1.RuleVersion);
                    var transaction = new RewardTransaction(
                        RewardTransactionId.FromGuid(txGuid), nowUtc, vitality,
                        "ingest:" + record.ProviderNamespace);

                    var outcome = game.Ledger.Apply(transaction, game.Resources);
                    if (outcome == LedgerApplyOutcome.AppliedFirstTime)
                    {
                        events.Add(new ActivityCredited(nowUtc, transaction.TransactionId.Value, vitality));
                        vitalityTotal += vitality;
                    }
                    else
                    {
                        events.Add(new ActivityDuplicate(nowUtc, transaction.TransactionId.Value));
                        dupTransactions++;
                    }
                }

                game.ProcessedRecords.Record(new ProcessedRecordEntry(
                    identityKey,
                    StepConversionRuleV1.RuleVersion,
                    eligibleSteps,
                    vitality,
                    nowUtc,
                    Math.Max(1, record.Revision)));

                if (endUtc > maxTrustedEndUtc)
                    maxTrustedEndUtc = endUtc;
                accepted++;
            }

            game.IngestionCheckpointUtc = maxTrustedEndUtc;

            // Bounded diagnostic evidence of this batch, committed in the SAME atomic
            // write as the rewards it describes (never consulted by progression math).
            game.LastIngestionOutcome = new IngestionOutcomeState
            {
                Outcome = IngestionOutcomeKind.Succeeded,
                CompletedAtUtc = nowUtc,
                TotalReceived = total,
                Accepted = accepted,
                Rejected = rejected,
                DuplicatesIgnored = duplicates,
                CorrectionsApplied = corrections,
                DeletionsApplied = deletionsApplied,
                VitalityCredited = vitalityTotal,
                UnappliedReversalVitality = game.ProcessedRecords.UnappliedReversalVitality,
            };

            OfflineAdvancer.AllocateVitality(game, _content, nowUtc, events);
            ComposePendingSummary(game, events);

            bool saved = PersistOrThrow();
            return new IngestResult(
                total, accepted, rejected, duplicates,
                corrections, deletionsApplied, deletionsIgnored, staleRevisions,
                clamped, dupTransactions, vitalityTotal,
                game.ProcessedRecords.UnappliedReversalVitality,
                saved, rejectionCounts,
                FormatSummaryLines(game.PendingReturnSummary), SnapshotSummary(game));
        }

        /// <summary>
        /// Platform-neutral reconcile path (M3): pulls normalized records from any
        /// IActivityRecordSource for the requested window and pushes them through the SAME
        /// IngestActivityBatch trust pipeline production adapters will use. Retry/replay
        /// after restart is safe by exactly-once construction — no separate code path.
        /// </summary>
        public IngestResult IngestFromSource(IActivityRecordSource source, DateTimeOffset windowStartUtc, DateTimeOffset windowEndUtc)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var game = RequireState();

            IReadOnlyList<NormalizedActivityRecord> records;
            try
            {
                records = source.FetchRecords(windowStartUtc, windowEndUtc);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                // Durably record the transient refresh failure as bounded evidence so the
                // shell can classify "temporarily unable to refresh" across restarts, then
                // rethrow — callers keep their existing failure semantics. Prior progress
                // is untouched: nothing reached the trust pipeline.
                game.LastIngestionOutcome = new IngestionOutcomeState
                {
                    Outcome = IngestionOutcomeKind.SourceFetchFailed,
                    CompletedAtUtc = _clock.UtcNow,
                    ErrorCategory = ex.GetType().Name,
                };
                PersistOrThrow();
                throw;
            }

            return IngestActivityBatch(records);
        }

        /// <summary>
        /// Correction policy (ACTIVITY_PIPELINE §11): positive late corrections add net
        /// eligible credit; negative corrections claw back only what the unspent balance
        /// allows — completed world content is never destroyed by a source correction.
        /// The unclamped remainder is durably counted for diagnostics.
        /// </summary>
        private void ApplyCorrectionDelta(
            GameState game,
            string identityKey,
            ProcessedRecordEntry existing,
            NormalizedActivityRecord record,
            DateTimeOffset nowUtc,
            List<SimulationEvent> events,
            ref int corrections, ref int clamped, ref int dupTransactions, ref long vitalityTotal,
            ref DateTimeOffset maxTrustedEndUtc)
        {
            int revision = Math.Max(1, record.Revision);
            long eligibleSteps = ActivityValidationPolicy.ClampQuantity(record.Category, record.Quantity);
            if (eligibleSteps != record.Quantity)
                clamped++;

            long targetVitality = StepConversionRuleV1.ConvertSteps(eligibleSteps);
            long delta = targetVitality - existing.VitalityCredited;

            long appliedAmount = 0L;
            if (delta != 0L)
            {
                appliedAmount = delta;
                if (delta < 0L)
                {
                    long balance = game.Resources.Get(ResourceType.Vitality);
                    appliedAmount = Math.Max(delta, -balance);
                    if (appliedAmount > delta)
                        game.ProcessedRecords.UnappliedReversalVitality += appliedAmount - delta;
                }

                if (appliedAmount != 0L)
                {
                    var txGuid = ActivityRewardIds.DeriveCorrectionGuid(
                        identityKey, StepConversionRuleV1.RuleVersion, revision, appliedAmount);
                    var transaction = new RewardTransaction(
                        RewardTransactionId.FromGuid(txGuid), nowUtc, appliedAmount,
                        "ingest-corr:" + record.ProviderNamespace);

                    var outcome = delta > 0L
                        ? game.Ledger.Apply(transaction, game.Resources)
                        : game.Ledger.ApplyCorrection(transaction, game.Resources);

                    if (outcome == LedgerApplyOutcome.AppliedFirstTime)
                    {
                        events.Add(delta > 0L
                            ? (SimulationEvent)new ActivityCredited(nowUtc, transaction.TransactionId.Value, appliedAmount)
                            : new ActivityCorrected(nowUtc, transaction.TransactionId.Value, appliedAmount));
                        vitalityTotal += appliedAmount;
                    }
                    else
                    {
                        events.Add(new ActivityDuplicate(nowUtc, transaction.TransactionId.Value));
                        dupTransactions++;
                    }
                }
            }

            // The row tracks net APPLIED vitality (what the durable ledger actually saw),
            // so a clamped clawback can never make dedup state outrun reward state.
            long newAppliedVitality = existing.VitalityCredited + appliedAmount;

            game.ProcessedRecords.Update(new ProcessedRecordEntry(
                identityKey,
                StepConversionRuleV1.RuleVersion,
                eligibleSteps,
                newAppliedVitality,
                nowUtc,
                revision));

            DateTimeOffset endUtc = record.EndUtc.ToUniversalTime();
            if (endUtc > maxTrustedEndUtc)
                maxTrustedEndUtc = endUtc;
            corrections++;
        }

        /// <summary>
        /// Deletion policy: reverse remaining credited value conservatively (clamped to
        /// balance), mark the row deleted by zeroing it, or count an ignored marker.
        /// Deletion records are never eligible for crediting.
        /// </summary>
        private void ProcessDeletion(
            GameState game,
            NormalizedActivityRecord record,
            DateTimeOffset nowUtc,
            List<SimulationEvent> events,
            ref int deletionsApplied, ref int deletionsIgnored, ref int staleRevisions,
            ref int dupTransactions, ref long vitalityTotal, ref DateTimeOffset maxTrustedEndUtc)
        {
            if (string.IsNullOrWhiteSpace(record.ProviderNamespace))
            {
                deletionsIgnored++; // nothing identifiable to reverse; counted diagnostically.
                return;
            }

            string identityKey = ActivityIdentity.Compute(record);
            if (!game.ProcessedRecords.TryGet(identityKey, out var existing))
            {
                deletionsIgnored++;
                return;
            }

            int revision = Math.Max(1, record.Revision);
            if (revision <= existing!.LastRevision)
            {
                staleRevisions++;
                return;
            }

            long reversalTarget = -existing.VitalityCredited;
            long balance = game.Resources.Get(ResourceType.Vitality);
            long appliedAmount = Math.Max(reversalTarget, -balance);
            if (appliedAmount > reversalTarget)
                game.ProcessedRecords.UnappliedReversalVitality += appliedAmount - reversalTarget;

            if (appliedAmount != 0L)
            {
                var txGuid = ActivityRewardIds.DeriveCorrectionGuid(
                    identityKey, StepConversionRuleV1.RuleVersion, revision, appliedAmount);
                var transaction = new RewardTransaction(
                    RewardTransactionId.FromGuid(txGuid), nowUtc, appliedAmount,
                    "ingest-del:" + record.ProviderNamespace);

                var outcome = game.Ledger.ApplyCorrection(transaction, game.Resources);
                if (outcome == LedgerApplyOutcome.AppliedFirstTime)
                {
                    events.Add(new ActivityCorrected(nowUtc, transaction.TransactionId.Value, appliedAmount));
                    vitalityTotal += appliedAmount;
                }
                else
                {
                    events.Add(new ActivityDuplicate(nowUtc, transaction.TransactionId.Value));
                    dupTransactions++;
                }
            }

            long remainingAppliedVitality = existing.VitalityCredited + appliedAmount;

            game.ProcessedRecords.Update(new ProcessedRecordEntry(
                identityKey,
                StepConversionRuleV1.RuleVersion,
                EligibleSteps: 0L,
                VitalityCredited: remainingAppliedVitality,
                ProcessedAtUtc: nowUtc,
                LastRevision: revision));

            DateTimeOffset endUtc = record.EndUtc.ToUniversalTime();
            if (endUtc > maxTrustedEndUtc)
                maxTrustedEndUtc = endUtc;
            deletionsApplied++;
        }

        public DomainResult EnqueueProject(string projectId)
        {
            var game = RequireState();
            var definition = _content.FindProject(projectId);
            var runtime = game.Region.FindProject(projectId);
            if (definition == null || runtime == null)
                return DomainResult.Fail(ErrorCodes.UnknownProject, $"Unknown project '{projectId}'.");

            switch (runtime.Status)
            {
                case ProjectStatus.Completed:
                    return DomainResult.Fail(ErrorCodes.AlreadyCompleted, $"'{definition.TitleKey}' is already completed.");
                case ProjectStatus.Queued:
                    return DomainResult.Fail(ErrorCodes.AlreadyQueued, $"'{definition.TitleKey}' is already queued.");
                case ProjectStatus.Active:
                    return DomainResult.Fail(ErrorCodes.AlreadyQueued, $"'{definition.TitleKey}' is already active.");
                case ProjectStatus.Locked:
                    return DomainResult.Fail(ErrorCodes.PrerequisiteNotMet, $"'{definition.TitleKey}' is still locked.");
            }

            runtime.Status = ProjectStatus.Queued;
            game.Queue.QueuedProjectIds.Add(projectId);

            var events = new List<SimulationEvent>();
            OfflineAdvancer.AllocateVitality(game, _content, _clock.UtcNow, events);
            ComposePendingSummary(game, events);
            PersistOrThrow();
            return DomainResult.Ok();
        }

        public DomainResult DequeueProject(string projectId)
        {
            var game = RequireState();
            if (!game.Queue.QueuedProjectIds.Remove(projectId))
                return DomainResult.Fail(ErrorCodes.NotQueued, $"Project '{projectId}' is not queued.");

            var runtime = game.Region.FindProject(projectId);
            if (runtime != null && runtime.Status == ProjectStatus.Queued)
                runtime.Status = ProjectStatus.Available;

            PersistOrThrow();
            return DomainResult.Ok();
        }

        /// <summary>Persists the automation switch. Enabling it immediately activates the
        /// head of the queue when the active slot is free, without waiting for new activity.</summary>
        public DomainResult SetAutoAdvance(bool enabled)
        {
            var game = RequireState();
            game.Queue.AutoAdvance = enabled;

            var events = new List<SimulationEvent>();
            OfflineAdvancer.AllocateVitality(game, _content, _clock.UtcNow, events);
            ComposePendingSummary(game, events);
            PersistOrThrow();
            return DomainResult.Ok();
        }

        /// <summary>
        /// Manual start: promotes one queued project into the single active slot. This is
        /// how work continues when auto-advance is disabled; it also lets a player promote
        /// a specific queued project while automation is on. Banked Vitality flows into
        /// the activated project immediately.
        /// </summary>
        public DomainResult ActivateQueuedProject(string projectId)
        {
            var game = RequireState();
            if (game.Queue.ActiveProjectId != null)
                return DomainResult.Fail(ErrorCodes.ProjectAlreadyActive,
                    $"'{game.Queue.ActiveProjectId}' is already active; only one project can be active.");

            var runtime = game.Region.FindProject(projectId);
            if (runtime == null || runtime.Status != ProjectStatus.Queued)
                return DomainResult.Fail(ErrorCodes.NotQueued, $"Project '{projectId}' is not queued.");

            if (!game.Queue.QueuedProjectIds.Remove(projectId))
                return DomainResult.Fail(ErrorCodes.NotQueued, $"Project '{projectId}' is not queued.");

            runtime.Status = ProjectStatus.Active;
            game.Queue.ActiveProjectId = projectId;

            var events = new List<SimulationEvent>
            {
                new ProjectBecameActive(_clock.UtcNow, projectId),
            };
            OfflineAdvancer.AllocateVitality(game, _content, _clock.UtcNow, events);
            ComposePendingSummary(game, events);
            PersistOrThrow();
            return DomainResult.Ok();
        }

        /// <summary>Reorders the queue. Must be a permutation of the current queued set.</summary>
        public DomainResult ReorderQueue(IReadOnlyList<string> orderedProjectIds)
        {
            var game = RequireState();
            var current = new List<string>(game.Queue.QueuedProjectIds);
            if (orderedProjectIds.Count != current.Count)
                return DomainResult.Fail(ErrorCodes.InvalidQueueOrder, "Reorder must contain every queued project exactly once.");

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in orderedProjectIds)
            {
                if (!current.Contains(id) || !seen.Add(id))
                    return DomainResult.Fail(ErrorCodes.InvalidQueueOrder, "Reorder must contain every queued project exactly once.");
            }

            game.Queue.QueuedProjectIds.Clear();
            game.Queue.QueuedProjectIds.AddRange(orderedProjectIds);
            PersistOrThrow();
            return DomainResult.Ok();
        }

        public HomeReadModel GetHome()
        {
            var game = RequireState();
            var region = game.Region;

            string? activeId = game.Queue.ActiveProjectId;
            string? activeTitle = null;
            long invested = 0L, cost = 0L;
            if (activeId != null)
            {
                var def = _content.FindProject(activeId);
                var runtime = region.FindProject(activeId);
                if (def != null && runtime != null)
                {
                    activeTitle = def.TitleKey;
                    invested = runtime.VitalityInvested;
                    cost = def.VitalityCost;
                }
            }

            var queuedRows = new List<HomeReadModel.QueuedRow>();
            foreach (var queuedId in game.Queue.QueuedProjectIds)
            {
                var def = _content.FindProject(queuedId);
                queuedRows.Add(new HomeReadModel.QueuedRow(queuedId, def?.TitleKey ?? queuedId));
            }

            int completed = 0;
            bool anyStarted = false;
            foreach (var pair in region.Projects)
            {
                if (pair.Value.Status == ProjectStatus.Completed)
                {
                    completed++;
                    anyStarted = true;
                }
                else if (pair.Value.Status == ProjectStatus.Active)
                {
                    anyStarted = true;
                }
            }

            var landmarkRows = new List<HomeReadModel.LandmarkRow>();
            foreach (var landmark in _content.Landmarks)
            {
                var stage = region.LandmarkStages.TryGetValue(landmark.Id.Value, out var s)
                    ? s
                    : RestorationStage.Ruined;
                landmarkRows.Add(new HomeReadModel.LandmarkRow(landmark.Id.Value, landmark.TitleKey, stage));
            }

            long vitality = game.Resources.Get(ResourceType.Vitality);

            bool queueIdle = activeId == null && queuedRows.Count == 0;
            long banked = queueIdle ? vitality : 0L;
            var reason = HomeAttentionReason.None;
            bool requiresAttention = false;
            if (game.PendingReturnSummary != null)
            {
                requiresAttention = true;
                reason = HomeAttentionReason.PendingReturnSummary;
            }
            else if (queueIdle && banked > 0)
            {
                requiresAttention = true;
                reason = HomeAttentionReason.QueueEmptyWithBankedVitality;
            }
            else if (queueIdle && !anyStarted)
            {
                reason = HomeAttentionReason.NoProjectStartedYet;
            }

            return new HomeReadModel(
                _content.TitleKey,
                vitality,
                game.Resources.Get(ResourceType.Materials),
                game.Resources.Get(ResourceType.Knowledge),
                activeId, activeTitle, invested, cost,
                queuedRows,
                completed, _content.Projects.Count,
                landmarkRows,
                game.Queue.AutoAdvance,
                game.PendingReturnSummary != null,
                game.PendingReturnSummary?.PrimaryNextAction,
                requiresAttention,
                reason,
                banked);
        }

        /// <summary>Complete Projects management snapshot for presentation.</summary>
        public ProjectsReadModel GetProjects()
        {
            var game = RequireState();

            var queuedPositions = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < game.Queue.QueuedProjectIds.Count; i++)
                queuedPositions[game.Queue.QueuedProjectIds[i]] = i;

            var rows = new List<ProjectsReadModel.ProjectRow>();
            foreach (var definition in _content.Projects)
            {
                var runtime = game.Region.FindProject(definition.Id.Value);
                if (runtime == null)
                    continue;

                var prerequisites = new List<string>();
                foreach (var prerequisite in definition.Prerequisites)
                    prerequisites.Add(prerequisite.Value);

                queuedPositions.TryGetValue(definition.Id.Value, out int position);
                rows.Add(new ProjectsReadModel.ProjectRow(
                    definition.Id.Value,
                    definition.TitleKey,
                    definition.VitalityCost,
                    runtime.VitalityInvested,
                    runtime.Status,
                    queuedPositions.ContainsKey(definition.Id.Value) ? position : (int?)null,
                    prerequisites));
            }

            return new ProjectsReadModel(game.Queue.AutoAdvance, game.Queue.ActiveProjectId, rows);
        }

        /// <summary>Lightweight region status snapshot for presentation.</summary>
        public RegionReadModel GetRegion()
        {
            var game = RequireState();

            var landmarks = new List<RegionReadModel.LandmarkRow>();
            foreach (var landmark in _content.Landmarks)
            {
                var stage = game.Region.LandmarkStages.TryGetValue(landmark.Id.Value, out var s)
                    ? s
                    : RestorationStage.Ruined;
                landmarks.Add(new RegionReadModel.LandmarkRow(landmark.Id.Value, landmark.TitleKey, stage));
            }

            var producers = new List<RegionReadModel.ProducerRow>();
            foreach (var definition in _content.Producers)
            {
                var runtime = game.Region.FindProducer(definition.Id.Value);
                producers.Add(new RegionReadModel.ProducerRow(
                    definition.Id.Value,
                    definition.TitleKey,
                    definition.Output,
                    definition.MilliUnitsPerDay,
                    definition.CapacityUnits,
                    runtime?.Unlocked ?? false,
                    runtime?.StoredMilliUnits ?? 0L,
                    runtime?.TotalProducedMilliUnits ?? 0L));
            }

            int completed = 0;
            foreach (var pair in game.Region.Projects)
                if (pair.Value.Status == ProjectStatus.Completed)
                    completed++;

            int expeditionsAvailable = 0, expeditionsCompleted = 0;
            foreach (var pair in game.Region.Expeditions)
            {
                if (pair.Value.CompletedAtUtc != null) expeditionsCompleted++;
                else expeditionsAvailable++;
            }

            return new RegionReadModel(
                _content.TitleKey,
                landmarks,
                producers,
                completed,
                _content.Projects.Count,
                game.Queue.ActiveProjectId,
                game.Region.EcologyStage,
                game.Region.SettlementStage,
                game.Region.IsCompleted,
                game.Region.RegionCompletedAtUtc,
                game.Region.Discoveries.Count,
                expeditionsAvailable,
                expeditionsCompleted);
        }

        /// <summary>Discovery journal snapshot: every authored discovery with its flags.</summary>
        public DiscoveriesReadModel GetDiscoveries()
        {
            var game = RequireState();

            var rows = new List<DiscoveriesReadModel.DiscoveryRow>();
            int unlocked = 0, unreviewed = 0;
            foreach (var definition in _content.Discoveries)
            {
                game.Region.Discoveries.TryGetValue(definition.Id.Value, out var runtime);
                bool isUnlocked = runtime != null;
                if (isUnlocked) unlocked++;
                if (isUnlocked && runtime!.Reviewed == false) unreviewed++;

                rows.Add(new DiscoveriesReadModel.DiscoveryRow(
                    definition.Id.Value,
                    definition.Category,
                    definition.TitleKey,
                    definition.BodyKey,
                    definition.ProvenanceKey,
                    definition.LocationKey,
                    isUnlocked,
                    runtime?.DiscoveredAtUtc,
                    runtime?.Reviewed ?? false));
            }

            return new DiscoveriesReadModel(rows, _content.Discoveries.Count, unlocked, unreviewed);
        }

        /// <summary>Expedition route snapshot with deterministic availability/completion status.</summary>
        public ExpeditionsReadModel GetExpeditions()
        {
            var game = RequireState();

            var rows = new List<ExpeditionsReadModel.ExpeditionRow>();
            int available = 0, completed = 0;
            foreach (var definition in _content.Expeditions)
            {
                game.Region.Expeditions.TryGetValue(definition.Id.Value, out var runtime);
                var status = ExpeditionsReadModel.ExpeditionStatus.Locked;
                if (runtime != null)
                {
                    if (runtime.CompletedAtUtc != null)
                    {
                        status = ExpeditionsReadModel.ExpeditionStatus.Completed;
                        completed++;
                    }
                    else
                    {
                        status = ExpeditionsReadModel.ExpeditionStatus.Available;
                        available++;
                    }
                }

                var requiredProjects = new List<string>();
                foreach (var projectId in definition.RequiredProjectIds)
                    requiredProjects.Add(projectId);

                var requiredStages = new List<string>();
                foreach (var requirement in definition.RequiredStages)
                    requiredStages.Add(requirement.LandmarkId + "@" + requirement.Stage);

                rows.Add(new ExpeditionsReadModel.ExpeditionRow(
                    definition.Id.Value,
                    definition.TitleKey,
                    definition.DescriptionKey,
                    status,
                    requiredProjects,
                    requiredStages,
                    definition.Reward?.Type,
                    definition.Reward?.Units ?? 0L,
                    runtime?.CompletedAtUtc));
            }

            return new ExpeditionsReadModel(rows, _content.Expeditions.Count, available, completed);
        }

        /// <summary>
        /// Marks an unlocked discovery reviewed. Presentation convenience only (GAME_SYSTEMS
        /// §7): idempotent, never gates progression, never alters earned state.
        /// </summary>
        public DomainResult MarkDiscoveryReviewed(string discoveryId)
        {
            var game = RequireState();
            if (_content.FindDiscovery(discoveryId) == null)
                return DomainResult.Fail(ErrorCodes.DiscoveryUnknown, $"Unknown discovery '{discoveryId}'.");
            if (!game.Region.Discoveries.TryGetValue(discoveryId, out var runtime) || runtime == null)
                return DomainResult.Fail(ErrorCodes.DiscoveryNotUnlocked, $"Discovery '{discoveryId}' has not been unlocked yet.");

            if (!runtime.Reviewed)
            {
                runtime.Reviewed = true;
                runtime.ReviewedAtUtc = _clock.UtcNow;
                PersistOrThrow();
            }
            return DomainResult.Ok();
        }

        /// <summary>
        /// The pending return summary of already-committed progress, or null when nothing
        /// is awaiting presentation. Survives restart; acknowledgement is a separate op.
        /// </summary>
        public ReturnSummaryReadModel? GetPendingReturnSummary()
        {
            var game = RequireState();
            return SnapshotSummary(game);
        }

        /// <summary>
        /// Idempotently acknowledges the pending summary. Dismissing it never alters the
        /// underlying earned progression; when nothing is pending this is a no-op.
        /// </summary>
        public DomainResult AcknowledgeReturnSummary()
        {
            var game = RequireState();
            if (game.PendingReturnSummary == null)
                return DomainResult.Ok();

            game.PendingReturnSummary = null;
            PersistOrThrow();
            return DomainResult.Ok();
        }

        // ---------------------------------------------------------------------
        // M5-H1: local UX preferences + onboarding (D-042). These operations
        // touch ONLY the local preferences store — never GameState, never the
        // canonical save envelope. Preference writes cannot alter progression.
        // ---------------------------------------------------------------------

        /// <summary>Settings snapshot built from local preferences plus the canonical auto-advance flag.</summary>
        public SettingsReadModel GetSettings()
        {
            var game = RequireState();
            var categories = new List<NotificationCategoryRow>
            {
                Row(NotificationCategory.ProjectCompletions, _preferences.NotifyProjectCompletions),
                Row(NotificationCategory.ExpeditionResults, _preferences.NotifyExpeditionResults),
                Row(NotificationCategory.Discoveries, _preferences.NotifyDiscoveries),
            };

            return new SettingsReadModel(
                _preferences.ReducedMotion,
                _preferences.HapticsEnabled,
                _preferences.SoundEnabled,
                _preferences.NotificationsOptIn,
                categories,
                _preferences.DailyReminderEnabled,
                _preferences.DailyReminderMinutesOfDay,
                UxPreferencesState.ReminderMinutesMin,
                UxPreferencesState.ReminderMinutesMax,
                _preferences.DiagnosticsVisible,
                game.Queue.AutoAdvance);
        }

        public DomainResult SetReducedMotion(bool enabled) => UpdatePreferences(p => p.ReducedMotion = enabled);

        public DomainResult SetHapticsEnabled(bool enabled) => UpdatePreferences(p => p.HapticsEnabled = enabled);

        public DomainResult SetSoundEnabled(bool enabled) => UpdatePreferences(p => p.SoundEnabled = enabled);

        public DomainResult SetNotificationsOptIn(bool optIn) => UpdatePreferences(p => p.NotificationsOptIn = optIn);

        public DomainResult SetNotificationCategory(NotificationCategory category, bool enabled)
        {
            return UpdatePreferences(p =>
            {
                switch (category)
                {
                    case NotificationCategory.ProjectCompletions: p.NotifyProjectCompletions = enabled; break;
                    case NotificationCategory.ExpeditionResults: p.NotifyExpeditionResults = enabled; break;
                    case NotificationCategory.Discoveries: p.NotifyDiscoveries = enabled; break;
                    case NotificationCategory.DailyReminder:
                        p.DailyReminderEnabled = enabled && p.DailyReminderEnabled;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(category));
                }
            });
        }

        /// <summary>Configures the optional daily reminder. Quiet hours are delegated to the OS; only a time-of-day is stored.</summary>
        public DomainResult SetDailyReminder(bool enabled, int minutesOfDay)
        {
            if (minutesOfDay < UxPreferencesState.ReminderMinutesMin || minutesOfDay > UxPreferencesState.ReminderMinutesMax)
                return DomainResult.Fail(
                    UxErrorCodes.InvalidReminderTime,
                    "Reminder minutes-of-day must be between " + UxPreferencesState.ReminderMinutesMin + " and " + UxPreferencesState.ReminderMinutesMax + ".");

            return UpdatePreferences(p =>
            {
                p.DailyReminderEnabled = enabled;
                p.DailyReminderMinutesOfDay = minutesOfDay;
            });
        }

        public DomainResult SetDiagnosticsVisible(bool visible) => UpdatePreferences(p => p.DiagnosticsVisible = visible);

        /// <summary>Outcome of the last preferences load, for support diagnostics only.</summary>
        public UxPreferencesLoadOutcome PreferencesLoadOutcome => _preferencesLoadOutcome;

        public string? PreferencesLoadDetail => _preferencesLoadDetail;

        private DomainResult UpdatePreferences(Action<UxPreferencesState> mutate)
        {
            if (_preferencesStore == null)
                return DomainResult.Fail(UxErrorCodes.PreferencesStoreMissing, "No UX preferences store is configured for this session.");

            mutate(_preferences);
            try
            {
                _preferencesStore.Save(_preferences.Clone());
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new IOException("Persisting UX preferences failed: " + ex.Message, ex);
            }
            return DomainResult.Ok();
        }

        // ---------------------------------------------------------------------
        // Onboarding flow state machine (durable in the local store, forward-only,
        // canonical-gated at the first-project step).
        // ---------------------------------------------------------------------

        /// <summary>
        /// Presentation-ready onboarding projection. Side-effect free; safe to call in
        /// every state including permission-denied profiles.
        /// </summary>
        public OnboardingReadModel GetOnboarding() => BuildOnboardingReadModel(_preferences.OnboardingStage);

        /// <summary>
        /// Forward-only onboarding progress. Moving backwards or to the current stage is an
        /// idempotent no-op. Reaching Complete requires that a first project actually exists
        /// in canonical queue/active/completed state — onboarding alone can never satisfy
        /// it; the choice must have been made through the real project operations.
        /// </summary>
        public DomainResult AdvanceOnboarding(OnboardingStage target)
        {
            if (target < OnboardingStage.NotStarted || target > OnboardingStage.Complete)
                return DomainResult.Fail(UxErrorCodes.InvalidOnboardingTarget, "Unknown onboarding stage value.");

            if (target <= _preferences.OnboardingStage)
                return DomainResult.Ok();

            if (target == OnboardingStage.Complete && !HasFirstProjectBeenChosen())
                return DomainResult.Fail(
                    UxErrorCodes.OnboardingPrerequisite,
                    "Complete requires a chosen first project through the normal project operations.");

            return UpdatePreferences(p => p.OnboardingStage = target);
        }

        // ---------------------------------------------------------------------
        // M5-H1: activity connection status projection (D-043) — pure, side-effect
        // free, and never a mutator of progression. Requires a connection port;
        // headless compositions without one get an explicit error.
        // ---------------------------------------------------------------------

        /// <summary>Player-safe activity connection status. Reading never touches state.</summary>
        public ActivityStatusReadModel GetActivityStatus()
        {
            if (_connectionPort == null)
                throw new InvalidOperationException(
                    "No activity connection port is configured for this session (" + UxErrorCodes.ConnectionPortMissing + ").");

            var game = RequireState();
            var outcome = game.LastIngestionOutcome;
            return ActivityStatusProjector.Project(
                _connectionPort.SnapshotConnection(),
                hasProcessedAnyRecord: game.ProcessedRecords.Count > 0,
                lastOutcome: outcome?.Outcome ?? IngestionOutcomeKind.NeverRun,
                lastBatchVitalityCredited: outcome?.Outcome == IngestionOutcomeKind.Succeeded ? outcome.VitalityCredited : 0L,
                lastProcessedAtUtc: outcome?.Outcome == IngestionOutcomeKind.Succeeded ? outcome.CompletedAtUtc : null);
        }

        /// <summary>Support-oriented diagnostics snapshot. Privacy-safe by construction:
        /// classified enums, bounded counters, timestamps; adapter technical detail only via
        /// the adapter-owned string. Never raw records or payloads (D-044).</summary>
        public DiagnosticsReadModel GetDiagnostics()
        {
            var game = RequireState();
            var nowUtc = _clock.UtcNow;

            var outcome = game.LastIngestionOutcome;
            var checkpointAge = nowUtc - game.IngestionCheckpointUtc;
            if (checkpointAge < TimeSpan.Zero)
                checkpointAge = TimeSpan.Zero;

            return new DiagnosticsReadModel(
                generatedAtUtc: nowUtc,
                bootOutcome: _lastBootOutcome,
                recoveredFromBackup: _lastBootRecoveredFromBackup,
                lastBootCodecFailure: _lastBootCodecFailure,
                appliedMigrationsAtBoot: _lastBootAppliedMigrations,
                schemaVersion: game.SchemaVersion,
                regionId: game.Region.RegionId,
                ingestionCheckpointUtc: game.IngestionCheckpointUtc,
                checkpointWatermarkAgeDays: (long)checkpointAge.TotalDays,
                processedRecordCount: game.ProcessedRecords.Count,
                lifetimeVitalityCredited: game.ProcessedRecords.TotalVitalityCredited,
                unappliedReversalVitality: game.ProcessedRecords.UnappliedReversalVitality,
                lastIngestion: outcome == null ? null : DiagnosticsIngestionRow.FromState(outcome),
                preferencesLoadOutcome: _preferencesLoadOutcome,
                preferencesLoadDetail: BoundDetail(_preferencesLoadDetail),
                connectionTechnicalDetail: BoundDetail(_connectionPort?.SnapshotConnection().TechnicalDetail));
        }

        /// <summary>Bounds adapter/store-provided technical text so diagnostics stay bounded.</summary>
        private static string? BoundDetail(string? detail)
        {
            if (detail == null)
                return null;
            return detail.Length <= 300 ? detail : detail.Substring(0, 300);
        }

        private void CaptureBootOutcome(DiagnosticsBootOutcome outcome, bool recoveredFromBackup)
        {
            _lastBootOutcome = outcome;
            _lastBootRecoveredFromBackup = recoveredFromBackup;
        }

        private static CodecFailureCategory MapCodecFailure(CodecStatus status)
        {
            switch (status)
            {
                case CodecStatus.MalformedEnvelope: return CodecFailureCategory.MalformedEnvelope;
                case CodecStatus.ChecksumMismatch: return CodecFailureCategory.ChecksumMismatch;
                case CodecStatus.VersionTooOld: return CodecFailureCategory.VersionTooOld;
                case CodecStatus.VersionTooNew: return CodecFailureCategory.VersionTooNew;
                case CodecStatus.MigrationFailed: return CodecFailureCategory.MigrationFailed;
                case CodecStatus.DeserializationFailed: return CodecFailureCategory.DeserializationFailed;
                default: return CodecFailureCategory.MalformedEnvelope;
            }
        }

        private NotificationCategoryRow Row(NotificationCategory category, bool enabled) =>
            new NotificationCategoryRow(category, enabled, _preferences.NotificationsOptIn && enabled);

        private OnboardingReadModel BuildOnboardingReadModel(OnboardingStage stage)
        {
            bool firstProjectChosen = HasFirstProjectBeenChosen();

            var activityStep = OnboardingActivityStepState.NotAvailable;
            if (_connectionPort != null)
            {
                var snapshot = _connectionPort.SnapshotConnection();
                switch (snapshot.Permission)
                {
                    case ActivityPermissionState.Granted:
                    case ActivityPermissionState.PartiallyGranted:
                        activityStep = OnboardingActivityStepState.Granted;
                        break;
                    case ActivityPermissionState.NotRequested:
                        activityStep = OnboardingActivityStepState.NotYetRequested;
                        break;
                    case ActivityPermissionState.Denied:
                    case ActivityPermissionState.Revoked:
                        activityStep = OnboardingActivityStepState.Denied;
                        break;
                    default:
                        activityStep = snapshot.Availability == ActivitySourceAvailability.Unsupported
                            ? OnboardingActivityStepState.SourceUnavailable
                            : OnboardingActivityStepState.NotYetRequested;
                        break;
                }
            }

            var nextAction = stage switch
            {
                OnboardingStage.NotStarted => OnboardingNextAction.ExplainPremise,
                OnboardingStage.Premise => OnboardingNextAction.ShowWorldBaseline,
                OnboardingStage.WorldBaseline => OnboardingNextAction.OfferActivityConnection,
                OnboardingStage.ActivityConnection => OnboardingNextAction.ChooseFirstProject,
                OnboardingStage.FirstProject => firstProjectChosen
                    ? OnboardingNextAction.DemonstrateProgression
                    : OnboardingNextAction.ChooseFirstProject,
                OnboardingStage.Simulation => OnboardingNextAction.ShowExitMessage,
                OnboardingStage.Exit => OnboardingNextAction.None,
                _ => OnboardingNextAction.None,
            };

            bool deniedButNavigable = activityStep == OnboardingActivityStepState.Denied;
            return new OnboardingReadModel(stage, nextAction, firstProjectChosen, activityStep, deniedButNavigable);
        }

        /// <summary>Canonical fact: any queued, active, or completed project exists.</summary>
        private bool HasFirstProjectBeenChosen()
        {
            var game = RequireState();
            if (game.Queue.ActiveProjectId != null || game.Queue.QueuedProjectIds.Count > 0)
                return true;

            foreach (var pair in game.Region.Projects)
                if (pair.Value.Status == ProjectStatus.Completed)
                    return true;
            return false;
        }

        private StartResult FinishBoot(GameState state, bool recoveredFromBackup)
        {
            _state = state;
            CaptureBootOutcome(
                recoveredFromBackup ? DiagnosticsBootOutcome.RecoveredFromBackup : DiagnosticsBootOutcome.Loaded,
                recoveredFromBackup);

            var events = new List<SimulationEvent>();
            OfflineAdvancer.Advance(state, _content, _clock.UtcNow, events);

            // Compose BEFORE persisting: a crash after commit but before presentation
            // still finds the summary on disk at next boot.
            ComposePendingSummary(state, events);
            if (recoveredFromBackup)
                state.PendingReturnSummary = ReturnSummaryComposer.WithNotice(
                    state.PendingReturnSummary, _clock.UtcNow,
                    "Your latest save was damaged; the most recent healthy backup was restored.");

            string? saveWarning = null;
            try
            {
                // Recovery commits must not rotate the known-bad primary into the backup
                // slot: that would replace the last healthy generation with garbage, so a
                // failure during this very commit could destroy the only valid copy.
                if (recoveredFromBackup)
                    _store.WriteAtomicPreservingBackup(_codec.Encode(state, _clock.UtcNow));
                else
                    _store.WriteAtomic(_codec.Encode(state, _clock.UtcNow));
            }
            catch (IOException ex)
            {
                // Presentation-only warning: the durable pending summary already reflects
                // everything committed before this failed write.
                saveWarning = "Progress advanced but could not be persisted: " + ex.Message;
            }
            catch (UnauthorizedAccessException ex)
            {
                saveWarning = "Progress advanced but could not be persisted: " + ex.Message;
            }

            var lines = new List<string>(FormatSummaryLines(state.PendingReturnSummary));
            if (saveWarning != null)
                lines.Add(saveWarning);

            return new StartResult(
                recoveredFromBackup ? StartStatus.RecoveredFromBackup : StartStatus.Loaded,
                lines,
                SnapshotSummary(state));
        }

        /// <summary>Merges committed events into the durable pending summary. Skipped when
        /// nothing happened, so an idle operation never touches the existing summary.</summary>
        private void ComposePendingSummary(GameState game, List<SimulationEvent> events)
        {
            if (events == null || events.Count == 0)
                return;
            game.PendingReturnSummary = ReturnSummaryComposer.Compose(
                events, _content, game.PendingReturnSummary, _clock.UtcNow);
        }

        private static ReturnSummaryReadModel? SnapshotSummary(GameState game) =>
            game.PendingReturnSummary == null
                ? null
                : ReturnSummaryReadModel.FromState(game.PendingReturnSummary);

        private static List<string> FormatSummaryLines(PendingReturnSummaryState? summary)
        {
            var lines = new List<string>();
            if (summary == null)
                return lines;
            foreach (var item in summary.Items)
                lines.Add(item.Text);
            if (!string.IsNullOrEmpty(summary.PrimaryNextAction))
                lines.Add("→ " + summary.PrimaryNextAction);
            return lines;
        }

        private DecodeAndValidateOutcome DecodeAndValidate(byte[] envelopeBytes)
        {
            var decoded = _codec.Decode(envelopeBytes);
            if (decoded.Status != CodecStatus.Ok || decoded.State == null)
            {
                _lastBootCodecFailure = MapCodecFailure(decoded.Status);
                return new DecodeAndValidateOutcome(null, DescribeDecodeFailure(decoded));
            }

            var violations = GameStateValidator.Validate(decoded.State, _content);
            if (violations.Count > 0)
            {
                _lastBootCodecFailure = CodecFailureCategory.StateValidationFailed;
                return new DecodeAndValidateOutcome(null, "Save state failed validation: " + string.Join("; ", violations));
            }

            _lastBootCodecFailure = CodecFailureCategory.None;
            _lastBootAppliedMigrations = decoded.AppliedMigrations;
            return new DecodeAndValidateOutcome(decoded.State, null);
        }

        private static string DescribeDecodeFailure(DecodeResult decoded) =>
            decoded.Status switch
            {
                CodecStatus.ChecksumMismatch => "Save integrity check failed.",
                CodecStatus.VersionTooNew => "Save was written by a newer game version.",
                CodecStatus.VersionTooOld => "Save version is too old to migrate.",
                CodecStatus.MigrationFailed => "Save migration failed.",
                CodecStatus.DeserializationFailed => "Save payload could not be interpreted.",
                _ => "Save file is malformed." + (decoded.Detail == null ? string.Empty : " " + decoded.Detail),
            };

        private SaveReadResult SafeReadPrimary()
        {
            try
            {
                return _store.ReadPrimary();
            }
            catch (IOException ex)
            {
                return SaveReadResult.Fail(SaveReadOutcome.IoFailure, "Reading primary save failed: " + ex.Message);
            }
        }

        private SaveReadResult SafeReadBackup()
        {
            try
            {
                return _store.ReadBackup();
            }
            catch (IOException ex)
            {
                return SaveReadResult.Fail(SaveReadOutcome.IoFailure, "Reading backup save failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Persists current state or throws. Reaching the caller means the bytes are
        /// durably committed — a failed commit is never reported as success. Access-
        /// denied failures are translated to IOException so callers handle one documented
        /// persistence-failure type.
        /// </summary>
        private bool PersistOrThrow()
        {
            var state = RequireState();
            try
            {
                _store.WriteAtomic(_codec.Encode(state, _clock.UtcNow));
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new IOException("Persisting game state failed: " + ex.Message, ex);
            }
        }

        private GameState RequireState() =>
            _state ?? throw new InvalidOperationException("No game state loaded. Call StartNewGame or Continue first.");
    }

    internal readonly struct DecodeAndValidateOutcome
    {
        public GameState? State { get; }
        public string? Error { get; }

        public DecodeAndValidateOutcome(GameState? state, string? error)
        {
            State = state;
            Error = error;
        }
    }
}
