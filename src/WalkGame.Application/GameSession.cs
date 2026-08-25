using System;
using System.Collections.Generic;
using System.IO;
using WalkGame.Application.Persistence;
using WalkGame.Application.ReadModels;
using WalkGame.Application.Summaries;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Common;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Simulation;
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

    public sealed class StartResult
    {
        public StartStatus Status { get; }
        public IReadOnlyList<string> SummaryLines { get; }
        public string? Detail { get; }

        internal StartResult(StartStatus status, IReadOnlyList<string>? summaryLines = null, string? detail = null)
        {
            Status = status;
            SummaryLines = summaryLines ?? Array.Empty<string>();
            Detail = detail;
        }
    }

    public sealed class CreditResult
    {
        public bool DuplicateIgnored { get; }
        public bool Saved { get; }
        public IReadOnlyList<string> SummaryLines { get; }

        internal CreditResult(bool duplicateIgnored, bool saved, IReadOnlyList<string> summaryLines)
        {
            DuplicateIgnored = duplicateIgnored;
            Saved = saved;
            SummaryLines = summaryLines;
        }
    }

    /// <summary>Per-batch trust diagnostics: where every received record went.</summary>
    public sealed class IngestResult
    {
        public int TotalReceived { get; }
        public int Accepted { get; }
        public int Rejected { get; }
        public int DuplicatesIgnored { get; }
        public int QuantityClamped { get; }
        public int DuplicateTransactionsIgnored { get; }
        public long VitalityCredited { get; }
        public bool Saved { get; }
        public IReadOnlyDictionary<string, int> RejectionCounts { get; }
        public IReadOnlyList<string> SummaryLines { get; }

        internal IngestResult(
            int totalReceived, int accepted, int rejected, int duplicatesIgnored,
            int quantityClamped, int duplicateTransactionsIgnored, long vitalityCredited,
            bool saved, IReadOnlyDictionary<string, int> rejectionCounts,
            IReadOnlyList<string> summaryLines)
        {
            TotalReceived = totalReceived;
            Accepted = accepted;
            Rejected = rejected;
            DuplicatesIgnored = duplicatesIgnored;
            QuantityClamped = quantityClamped;
            DuplicateTransactionsIgnored = duplicateTransactionsIgnored;
            VitalityCredited = vitalityCredited;
            Saved = saved;
            RejectionCounts = rejectionCounts;
            SummaryLines = summaryLines;
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

        private GameState? _state;

        public GameSession(ISaveStore store, ISaveCodec codec, IClock clock, RegionDefinition content)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _content = content ?? throw new ArgumentNullException(nameof(content));

            var violations = ContentValidator.Validate(_content);
            if (violations.Count > 0)
                throw new ArgumentException("Invalid region content: " + string.Join("; ", violations), nameof(content));
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

            _state = fresh;
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
                return new StartResult(StartStatus.NoSaveFound);

            if (primary.EnvelopeBytes != null)
            {
                var decodedPrimary = DecodeAndValidate(primary.EnvelopeBytes);
                if (decodedPrimary.State != null)
                    return FinishBoot(decodedPrimary.State, recoveredFromBackup: false);
            }

            var backup = SafeReadBackup();
            if (backup.EnvelopeBytes != null)
            {
                var decodedBackup = DecodeAndValidate(backup.EnvelopeBytes);
                if (decodedBackup.State != null)
                    return FinishBoot(decodedBackup.State, recoveredFromBackup: true);
            }

            string reason =
                primary.Detail ??
                backup.Detail ??
                "Save data could not be read.";
            return new StartResult(StartStatus.SaveUnreadable, detail: reason);
        }

        /// <summary>
        /// Applies an activity-derived reward transaction with exactly-once semantics:
        /// durable transaction identity makes replays, retries and crash recovery no-ops.
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

            bool saved = PersistOrThrow();
            return new CreditResult(duplicate, saved, ReturnSummaryBuilder.Build(events, _content));
        }

        /// <summary>
        /// Trust-pipeline ingestion (minimum M2 slice): normalized synthetic activity in,
        /// validated/bounded/deduplicated exactly-once rewards out.
        ///
        /// Per record: validate → dedup against the durable processed-record ledger →
        /// clamp → convert with rule v1 → derive stable reward-transaction ID → apply
        /// idempotently. The processed-record ledger and ingestion checkpoint advance in
        /// the same state transition as the rewards they describe, persisted atomically
        /// once at the end, so a crash can never leave a checkpoint that outruns credited
        /// rewards.
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
            long vitalityTotal = 0L;
            DateTimeOffset maxTrustedEndUtc = game.IngestionCheckpointUtc;

            foreach (var record in records)
            {
                var status = ActivityValidationPolicy.Validate(record, nowUtc);
                if (status != ActivityValidationStatus.Valid)
                {
                    rejected++;
                    string key = status.ToString();
                    rejectionCounts[key] = rejectionCounts.TryGetValue(key, out int n) ? n + 1 : 1;
                    continue;
                }

                string identityKey = ActivityIdentity.Compute(record);
                if (game.ProcessedRecords.HasProcessed(identityKey))
                {
                    duplicates++;
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
                    nowUtc));

                DateTimeOffset endUtc = record.EndUtc.ToUniversalTime();
                if (endUtc > maxTrustedEndUtc)
                    maxTrustedEndUtc = endUtc;
                accepted++;
            }

            game.IngestionCheckpointUtc = maxTrustedEndUtc;

            OfflineAdvancer.AllocateVitality(game, _content, nowUtc, events);

            bool saved = PersistOrThrow();
            return new IngestResult(
                total, accepted, rejected, duplicates, clamped, dupTransactions,
                vitalityTotal, saved, rejectionCounts,
                ReturnSummaryBuilder.Build(events, _content));
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
            foreach (var pair in region.Projects)
                if (pair.Value.Status == ProjectStatus.Completed)
                    completed++;

            var landmarkRows = new List<HomeReadModel.LandmarkRow>();
            foreach (var landmark in _content.Landmarks)
            {
                var stage = region.LandmarkStages.TryGetValue(landmark.Id.Value, out var s)
                    ? s
                    : RestorationStage.Ruined;
                landmarkRows.Add(new HomeReadModel.LandmarkRow(landmark.Id.Value, landmark.TitleKey, stage));
            }

            return new HomeReadModel(
                _content.TitleKey,
                game.Resources.Get(ResourceType.Vitality),
                game.Resources.Get(ResourceType.Materials),
                game.Resources.Get(ResourceType.Knowledge),
                activeId, activeTitle, invested, cost,
                queuedRows,
                completed, _content.Projects.Count,
                landmarkRows);
        }

        private StartResult FinishBoot(GameState state, bool recoveredFromBackup)
        {
            _state = state;

            var events = new List<SimulationEvent>();
            OfflineAdvancer.Advance(state, _content, _clock.UtcNow, events);

            string? saveWarning = null;
            try
            {
                _store.WriteAtomic(_codec.Encode(state, _clock.UtcNow));
            }
            catch (IOException ex)
            {
                saveWarning = "Progress advanced but could not be persisted: " + ex.Message;
            }

            var lines = ReturnSummaryBuilder.Build(events, _content);
            if (recoveredFromBackup)
                lines.Insert(0, "Your latest save was damaged; the most recent healthy backup was restored.");
            if (saveWarning != null)
                lines.Add(saveWarning);

            return new StartResult(
                recoveredFromBackup ? StartStatus.RecoveredFromBackup : StartStatus.Loaded,
                lines);
        }

        private DecodeAndValidateOutcome DecodeAndValidate(byte[] envelopeBytes)
        {
            var decoded = _codec.Decode(envelopeBytes);
            if (decoded.Status != CodecStatus.Ok || decoded.State == null)
                return new DecodeAndValidateOutcome(null, DescribeDecodeFailure(decoded));

            var violations = GameStateValidator.Validate(decoded.State, _content);
            if (violations.Count > 0)
                return new DecodeAndValidateOutcome(null, "Save state failed validation: " + string.Join("; ", violations));

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

        private bool PersistOrThrow()
        {
            var state = RequireState();
            try
            {
                _store.WriteAtomic(_codec.Encode(state, _clock.UtcNow));
                return true;
            }
            catch (IOException)
            {
                throw;
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
