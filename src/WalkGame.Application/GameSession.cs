using System;
using WalkGame.Domain.Story;
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

        /// <summary>
        /// The save was written against a NEWER authored content version than this build
        /// provides (D-059). Fails closed and is NOT rewritten: the save's meaning cannot
        /// be reconstructed by a build that lacks the content it references.
        ///
        /// Distinct from every other outcome because it is not a file failure, not an
        /// invariant failure, and not absence — it is a VERSION mismatch, and the correct
        /// action is to upgrade the build, not to repair the save.
        /// </summary>
        ContentIncompatible = 6,

        /// <summary>
        /// D-085 (KI-021): <c>StartNewGame(seed, overwriteExisting: false)</c> found a save
        /// generation (primary or backup) or could not tell, so nothing was written. The
        /// existing world, even an unreadable one, is left for Continue/recovery.
        /// </summary>
        ExistingSaveRefused = 7,
    }

    /// <summary>Why a boot had to fall back to the backup generation (never set when the
    /// primary loaded cleanly). Reported in diagnostics so a support engineer can tell a
    /// crash-window primary loss apart from a damaged primary — and from a player who
    /// deliberately deleted the file, which looks identical from the store's side.</summary>
    public enum SaveRecoveryCause
    {
        /// <summary>The primary generation loaded; no recovery happened.</summary>
        None = 0,

        /// <summary>The primary slot existed but failed integrity, version, migration or decode.</summary>
        PrimaryDamaged = 1,

        /// <summary>The primary slot was absent from storage entirely.</summary>
        PrimaryMissing = 2,
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

        /// <summary>The primary slot was ABSENT and the backup generation was used. Distinct
        /// from <see cref="RecoveredFromBackup"/> (primary present but damaged): the store
        /// documents a delete/move window in which the primary is transiently missing, and
        /// that is the class this value exists to name.</summary>
        RecoveredFromMissingPrimary = 7,

        /// <summary>Save recorded against a newer content version than this build (D-059).</summary>
        ContentIncompatible = 8,
    }

    /// <summary>
    /// How a save's authored content version relates to the content being loaded with it
    /// (D-059). Reported on every boot so drift is observable even when it is compatible.
    ///
    /// This is a DIFFERENT axis from the payload schema version: that is the wire format,
    /// migrated by the ISaveMigration chain; this is the authored content graph the payload
    /// refers to, which is never migrated. The two are reported independently, because a
    /// save can be on a current schema and still reference retired content.
    /// </summary>
    public enum ContentCompatibility
    {
        /// <summary>The relationship could not be determined (neither side records a version).</summary>
        Unknown = 0,

        /// <summary>The save and the build author exactly the same content version.</summary>
        ExactMatch = 1,

        /// <summary>
        /// The save is OLDER than the content version this build provides, and the
        /// difference is within the supported compatibility range. Loadable; tombstones
        /// exist precisely so this case does not destroy mature profiles.
        /// </summary>
        CompatibleOlder = 2,

        /// <summary>
        /// The save is newer than this build's content version. Fails closed: this build
        /// cannot know what the newer content meant, and guessing is how a profile is lost.
        /// </summary>
        NewerThanBuild = 3,
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

        /// <summary>
        /// Steps the pipeline treated as ELIGIBLE: the requested quantity after the
        /// bounded-quantity clamp, summed over the records this batch trusted.
        ///
        /// The requested total and this total are different numbers whenever clamping
        /// fires, and the difference is exactly what a reader needs to see: the clamp
        /// discards surplus VOLUME, it does not reduce the credit rate. Reporting only the
        /// requested total under an "activity ingested" label is how a whole documentation
        /// section came to state a conversion rate that was off by a factor of seven
        /// (D-062, F-13).
        /// </summary>
        public long EligibleSteps { get; }

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
            IReadOnlyList<string> summaryLines, ReturnSummaryReadModel? summary,
            long eligibleSteps = 0L)
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
            EligibleSteps = eligibleSteps;
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
    public sealed partial class GameSession
    {
        /// <summary>
        /// Content validation is deterministic over an immutable graph, so each distinct
        /// RegionDefinition instance is validated at most once per process. Sessions are
        /// constructed per boot and per simulated app-closed day; re-walking the full
        /// content graph every time was pure duplicate work.
        /// </summary>
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
            RegionDefinition, IReadOnlyList<string>> ValidatedContent = new();

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
        private SaveRecoveryCause _lastBootRecoveryCause = SaveRecoveryCause.None;
        private CodecFailureCategory _lastBootCodecFailure = CodecFailureCategory.None;
        private IReadOnlyList<string> _lastBootAppliedMigrations = Array.Empty<string>();
        private ContentCompatibility _lastContentCompatibility = ContentCompatibility.Unknown;
        private int? _lastSaveContentVersion;
        private int _lastBuildContentVersion;

        /// <summary>
        /// Permanently deletes all local data this session owns: every save generation
        /// (primary, backup, temporaries) and, when wired, the local preferences file
        /// (PRODUCT_SPEC §7a requirement 7). Fails closed: if any wired store cannot erase,
        /// nothing is touched and <see cref="LocalDataEraseStatus.NotSupported"/> is returned.
        /// On success the in-memory session is reset to the never-booted state, so the next
        /// operation must be StartNewGame. The failure detail is the exception TYPE only
        /// (privacy-safe, D-044).
        /// </summary>
        public LocalDataEraseResult DeleteAllLocalData()
        {
            var saveEraser = _store as ILocalDataEraser;
            var prefsEraser = _preferencesStore as ILocalDataEraser;
            var memoryEraser = _presentationMemoryStore as ILocalDataEraser;
            if (saveEraser == null || (_preferencesStore != null && prefsEraser == null)
                || (_presentationMemoryStore != null && memoryEraser == null))
                return new LocalDataEraseResult(LocalDataEraseStatus.NotSupported, null);

            try
            {
                // Saves first: they hold the health-adjacent data. Preferences are UX-only.
                saveEraser.EraseAll();
                prefsEraser?.EraseAll();
                memoryEraser?.EraseAll(); // D-175: presentation memory is local data too
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                return new LocalDataEraseResult(LocalDataEraseStatus.Failed, ex.GetType().Name);
            }

            _state = null;
            _hasUnsavedChanges = false;
            _preferences = UxPreferencesState.CreateDefault();
            _preferencesLoadOutcome = UxPreferencesLoadOutcome.NotFound;
            _preferencesLoadDetail = null;
            _lastBootOutcome = DiagnosticsBootOutcome.NeverBooted;
            _lastBootRecoveredFromBackup = false;
            _lastBootRecoveryCause = SaveRecoveryCause.None;
            _lastBootCodecFailure = CodecFailureCategory.None;
            _lastBootAppliedMigrations = Array.Empty<string>();
            ResetPresentationMemoryAfterErase();
            return new LocalDataEraseResult(LocalDataEraseStatus.Erased, null);
        }

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
            : this(store, codec, clock, content, preferencesStore, connectionPort, presentationMemoryStore: null)
        {
        }

        /// <param name="presentationMemoryStore">D-175: optional durable presentation memory (what the world has
        /// already shown). Must also be an <see cref="ILocalDataEraser"/> for "Delete all local data" to work.</param>
        public GameSession(ISaveStore store, ISaveCodec codec, IClock clock, RegionDefinition content,
            IUxPreferencesStore? preferencesStore, IActivityConnectionPort? connectionPort,
            WalkGame.Application.Presentation.IPresentationMemoryStore? presentationMemoryStore)
        {
            _presentationMemoryStore = presentationMemoryStore;
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _preferencesStore = preferencesStore;
            _connectionPort = connectionPort;

            LoadPreferencesFromStore();

            // GetValue is atomic per key: concurrent session construction (parallel test
            // collections, harness loops) never duplicates validation or races Add().
            ValidatedContent.GetValue(_content, static validatedContent =>
            {
                var violations = ContentValidator.Validate(validatedContent);
                if (violations.Count > 0)
                    throw new ArgumentException("Invalid region content: " + string.Join("; ", violations), "content");
                return (IReadOnlyList<string>)Array.Empty<string>();
            });
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

        /// <summary>
        /// D-111: the current save as the exact envelope bytes the codec writes, for a player
        /// export. Null before a game is loaded. Read-only: nothing is written or changed.
        /// </summary>
        public byte[]? ExportSaveEnvelope() => _state == null ? null : _codec.Encode(_state, _clock.UtcNow);

        public RegionDefinition Content => _content;

        /// <summary>
        /// Creates and persists a fresh game, REPLACING any existing save. This is the
        /// long-standing explicit-overwrite contract used by tools and tests; it is the same
        /// as <c>StartNewGame(seed, overwriteExisting: true)</c>. Player-facing callers should
        /// pass <c>overwriteExisting: false</c> (D-085, KI-021).
        /// </summary>
        public StartResult StartNewGame(ulong seed) => StartNewGame(seed, overwriteExisting: true);

        /// <summary>
        /// D-085 (KI-021): with <paramref name="overwriteExisting"/> false, refuses with
        /// <see cref="StartStatus.ExistingSaveRefused"/> when the primary or backup slot holds
        /// anything, or when the store cannot say (an I/O failure fails closed). Nothing is
        /// written and the in-memory session is unchanged.
        /// </summary>
        public StartResult StartNewGame(ulong seed, bool overwriteExisting)
        {
            if (!overwriteExisting)
            {
                var primary = _store.ReadPrimary().Outcome;
                var backup = _store.ReadBackup().Outcome;
                if (primary != SaveReadOutcome.NotFound || backup != SaveReadOutcome.NotFound)
                    return new StartResult(StartStatus.ExistingSaveRefused,
                        detail: "A save already exists on this device; it was not replaced.");
            }

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
            _hasUnsavedChanges = false;
            CaptureBootOutcome(DiagnosticsBootOutcome.NewGameCreated, recoveredFromBackup: false);
            if (!overwriteExisting) ResetStaleOnboarding();
            return new StartResult(StartStatus.NewGameCreated, new[]
            {
                "A new restoration begins in " + _content.TitleKey + ".",
            });
        }

        /// <summary>
        /// Boot flow: locate save → validate integrity → fall back to backup → migrate →
        /// validate invariants → advance offline systems → persist → return summary.
        ///
        /// Recovery policy (the application layer owns it, per the <see cref="ISaveStore"/>
        /// port contract): BOTH generations are addressed independently, and
        /// <see cref="StartStatus.NoSaveFound"/> is reported only when BOTH are absent.
        /// An earlier version returned NoSaveFound the moment the primary was absent, which
        /// orphaned the healthy backup and let any composition root that treats "no save" as
        /// a fresh profile destroy the player's world (F-01).
        ///
        /// This matters because <c>AtomicFileSaveStore.ReplaceFile</c> promotes the previous
        /// generation with a delete-then-move pair — the store's own comment documents that a
        /// crash between the two steps leaves the primary transiently absent. That state is
        /// real, reachable, and was classified as "no save exists" by the caller.
        ///
        /// Classification is exhaustive and non-overlapping. Every <see cref="StartStatus"/>
        /// value is produced by exactly one of these boot conditions, and the value is
        /// surfaced unchanged through the diagnostics projection and the CLI exit code:
        ///
        ///   both generations absent ........... NoSaveFound  (no write performed)
        ///   a generation decoded + validated .. Loaded
        ///   only the backup decoded ........... RecoveredFromBackup
        ///   decoded, but state violates an
        ///     invariant ...................... StateInvalid
        ///   present but unreadable/integrity/
        ///     version/migration failure ...... SaveUnreadable
        /// </summary>
        public StartResult Continue()
        {
            _hasUnsavedChanges = false; // D-131: a reload discards any un-committed in-memory state.
            var primary = SafeReadPrimary();
            bool primaryAbsent = primary.Outcome == SaveReadOutcome.NotFound;

            DecodeAndValidateOutcome? primaryDecode = null;
            if (primary.EnvelopeBytes != null)
            {
                primaryDecode = DecodeAndValidate(primary.EnvelopeBytes);
                if (primaryDecode.Value.State != null)
                {
                    var primaryIncompatible = RejectIfContentIsNewer(primaryDecode.Value.State);
                    if (primaryIncompatible != null)
                        return primaryIncompatible;
                    return FinishBoot(primaryDecode.Value.State, SaveRecoveryCause.None);
                }
            }

            // The primary is unavailable for ANY reason — absent, unreadable, or corrupt —
            // so the backup generation is probed. Absence is not a special case: it is the
            // state the store's own delete/move promotion window can leave behind.
            DecodeAndValidateOutcome? backupDecode = null;
            var backup = SafeReadBackup();
            if (backup.EnvelopeBytes != null)
            {
                backupDecode = DecodeAndValidate(backup.EnvelopeBytes);
                if (backupDecode.Value.State != null)
                {
                    var backupIncompatible = RejectIfContentIsNewer(backupDecode.Value.State);
                    if (backupIncompatible != null)
                        return backupIncompatible;
                    return FinishBoot(
                        backupDecode.Value.State,
                        primaryAbsent ? SaveRecoveryCause.PrimaryMissing : SaveRecoveryCause.PrimaryDamaged);
                }
            }

            // Both generations absent is the ONLY state that means "no save exists". No write
            // is performed on this path: a genuinely fresh profile must not be created by a
            // boot that found nothing.
            if (primaryAbsent && backup.Outcome == SaveReadOutcome.NotFound)
            {
                CaptureBootOutcome(DiagnosticsBootOutcome.NoSaveFound, recoveredFromBackup: false);
                return new StartResult(StartStatus.NoSaveFound);
            }

            // A save that decodes but violates a documented invariant is a data-integrity
            // failure, not a file failure. It is reported as its own class so composition
            // roots, diagnostics and the CLI stop collapsing it into "corrupt".
            string? validationDetail = StateValidationDetail(primaryDecode) ?? StateValidationDetail(backupDecode);
            if (validationDetail != null)
            {
                CaptureBootOutcome(DiagnosticsBootOutcome.StateInvalid, recoveredFromBackup: false);
                return new StartResult(StartStatus.StateInvalid, detail: validationDetail);
            }

            // Surface the specific failure reason (integrity, version, migration,
            // validation) instead of a generic unreadable-save message. Primary detail wins
            // over backup detail; an absent primary contributes no detail of its own, so an
            // absent-primary-plus-corrupt-backup still reports the backup's specific failure.
            string reason =
                primaryDecode?.Error ??
                backupDecode?.Error ??
                primary.Detail ??
                backup.Detail ??
                "Save data could not be read.";
            CaptureBootOutcome(DiagnosticsBootOutcome.SaveUnreadable, recoveredFromBackup: false);
            return new StartResult(StartStatus.SaveUnreadable, detail: reason);
        }

        /// <summary>
        /// True when the processed-record ledger already holds this logical record at a
        /// revision at least as high as the one delivered, so processing it could only be
        /// a no-op. Pure lookup, no state change.
        /// </summary>
        private static bool IsRetiredRecord(GameState game, NormalizedActivityRecord record) =>
            !string.IsNullOrWhiteSpace(record.ProviderNamespace) &&
            game.ProcessedRecords.RetiredCount > 0 &&
            game.ProcessedRecords.IsRetired(ActivityIdentity.Compute(record));

        private static bool IsAlreadyTrustedNoOp(GameState game, NormalizedActivityRecord record)
        {
            if (string.IsNullOrWhiteSpace(record.ProviderNamespace))
                return false; // no computable identity; nothing was ever trusted under it
            if (!game.ProcessedRecords.TryGet(ActivityIdentity.Compute(record), out var existing)
                || existing == null)
                return false;
            return record.Revision <= existing.LastRevision;
        }

        /// <summary>
        /// Classifies the save's authored content version against this build's, and fails
        /// closed when the save is NEWER (D-059).
        ///
        /// Returns null when the save may proceed, in which case the classification has
        /// already been captured for diagnostics. A save that is older, or exactly equal,
        /// always proceeds: that is the whole point of recording the version, and tombstones
        /// are what keep an older save's retired references resolvable.
        /// </summary>
        private StartResult? RejectIfContentIsNewer(GameState state)
        {
            int buildVersion = _content.ContentVersion;
            int? recorded = state.AuthoredContentVersion;

            if (recorded == null || recorded.Value == buildVersion)
                _lastContentCompatibility = ContentCompatibility.ExactMatch;
            else if (recorded.Value < buildVersion)
                _lastContentCompatibility = ContentCompatibility.CompatibleOlder;
            else
                _lastContentCompatibility = ContentCompatibility.NewerThanBuild;

            _lastSaveContentVersion = recorded;
            _lastBuildContentVersion = buildVersion;

            if (_lastContentCompatibility != ContentCompatibility.NewerThanBuild)
                return null;

            // `recorded` is provably non-null here -- an absent version classifies as
            // ExactMatch -- but stated explicitly rather than left to a null-conditional
            // that would print nothing in the one case a player must read.
            int saveVersion = recorded ?? buildVersion;

            // Fail closed, and do NOT rewrite: this build cannot know what the newer
            // authored content meant, and silently "fixing" the save is how a profile is lost.
            CaptureBootOutcome(DiagnosticsBootOutcome.ContentIncompatible, recoveredFromBackup: false);
            return new StartResult(StartStatus.ContentIncompatible,
                detail: "Save was written against content version " + saveVersion
                        + " but this build provides content version " + buildVersion
                        + ". Update the game to continue; the save has not been changed.");
        }

        /// <summary>The decode error of a generation that failed INVARIANT validation (not
        /// integrity/version/migration/decode), or null when it did not fail that way.</summary>
        private static string? StateValidationDetail(DecodeAndValidateOutcome? outcome)
        {
            if (!outcome.HasValue)
                return null;
            return outcome.Value.FailedStateValidation ? outcome.Value.Error : null;
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
        ///
        /// ELIGIBILITY IS EVALUATED FIRST FOR EVERY RECORD, deletion markers included
        /// (D-053). Previously the `IsDeletion` branch preceded validation entirely, so a
        /// malformed marker could reverse value and push the durable watermark arbitrarily
        /// far forward without ever being checked. The deletion handler's remaining checks
        /// (unknown logical record, stale revision) are POLICY decisions and stay there.
        ///
        /// The window is anchored to trusted time, not to the raw device clock: see
        /// <see cref="TrustedTime"/>. A returning player's whole backlog is credited, and a
        /// device clock that is merely fast no longer disables crediting.
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
            int retiredIgnored = 0;
            long vitalityTotal = 0L, eligibleStepsTotal = 0L;
            DateTimeOffset maxTrustedEndUtc = game.IngestionCheckpointUtc;

            // Computed ONCE per batch, never per record: a per-record recomputation would
            // make validation O(n) in retained history, which the save-growth evidence
            // shows reaching ~19,000 entries by day 730. ProcessedRecordEntry does not
            // persist the record's own end (schema v2), so the maximum end the processed
            // ledger implies IS the durable watermark — the pipeline already maintains it as
            // the latest end of any record it has ever trusted. The two fields are kept
            // separate so a future schema that stores per-record ends needs no API change.
            var trustedTime = new TrustedTime(
                nowUtc,
                latestTrustedEndUtc: game.IngestionCheckpointUtc,
                reconciliationWatermarkUtc: game.IngestionCheckpointUtc);

            foreach (var record in records)
            {
                // 1. Eligibility, for EVERY record. A record that fails here changes no
                //    durable state: it does not reverse value, does not touch the
                //    processed-record row, and does not advance the watermark.
                var status = ActivityValidationPolicy.Validate(record, trustedTime);
                if (status != ActivityValidationStatus.Valid)
                {
                    // One carve-out, and it is safe in both directions: a record this
                    // pipeline has ALREADY durably trusted is an exactly-once no-op
                    // whatever the window says, so it is counted as a duplicate and
                    // dropped rather than reported as a rejection. Without this, replaying
                    // a long horizon would report the oldest days as "OutsideHorizon" and
                    // the flagship exactly-once evidence would read as refused activity.
                    //
                    // The revision guard is what makes this incapable of minting value: an
                    // out-of-window record carrying a HIGHER revision would otherwise reach
                    // the correction path and credit a delta. It is rejected like any other
                    // new out-of-window record, so an old record can only ever be a no-op.
                    if (IsRetiredRecord(game, record))
                    {
                        retiredIgnored++;
                        duplicates++; // a retired re-delivery is still an exactly-once no-op; RetiredRecordsIgnored is its sub-classification (D-066)
                        continue;
                    }
                    if (IsAlreadyTrustedNoOp(game, record))
                    {
                        duplicates++;
                        continue;
                    }

                    rejected++;
                    string key = status.ToString();
                    rejectionCounts[key] = rejectionCounts.TryGetValue(key, out int n) ? n + 1 : 1;
                    continue;
                }

                // 2. A record whose detail row was retired (D-066) is permanently
                //    non-creditable: no credit, no correction, no destructive reversal.
                if (IsRetiredRecord(game, record))
                {
                    retiredIgnored++;
                        duplicates++; // a retired re-delivery is still an exactly-once no-op; RetiredRecordsIgnored is its sub-classification (D-066)
                    continue;
                }

                // 3. Dispatch. Deletion markers are only reachable once eligible.
                if (record.IsDeletion)
                {
                    ProcessDeletion(game, record, nowUtc, events,
                        ref deletionsApplied, ref deletionsIgnored, ref staleRevisions,
                        ref dupTransactions, ref vitalityTotal, ref maxTrustedEndUtc);
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
                        ref maxTrustedEndUtc, ref eligibleStepsTotal);
                    continue;
                }

                long eligibleSteps = ActivityValidationPolicy.ClampQuantity(record.Category, record.Quantity);
                if (eligibleSteps != record.Quantity)
                    clamped++;
                eligibleStepsTotal += eligibleSteps;

                // D-079: pool with the provider's carried remainder so many small records
                // credit the same as one large record (KI-019).
                long vitality = StepConversionRuleV1.ConvertWithCarry(
                    game.ProcessedRecords.GetCarry(record.ProviderNamespace), eligibleSteps, out long carryAfter);
                game.ProcessedRecords.SetCarry(record.ProviderNamespace, carryAfter);

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
                    Math.Max(1, record.Revision),
                    endUtc,
                    LocalDayCalendar.DayOf(endUtc, record.EndZoneOffsetSeconds)));

                if (endUtc > maxTrustedEndUtc)
                    maxTrustedEndUtc = endUtc;
                accepted++;
            }

            game.IngestionCheckpointUtc = maxTrustedEndUtc;

            // Bounded ledger retirement (D-066), inside this same atomic commit. The cutoff is
            // the acceptance window's lower bound under the post-batch trusted progress, so
            // nothing can be retired that the pipeline would still accept; retired identities
            // stay permanently non-creditable. Lifetime totals and balances are unchanged.
            var retirementCutoff = new TrustedTime(nowUtc, maxTrustedEndUtc, maxTrustedEndUtc).WindowStartUtc;
            if (maxTrustedEndUtc != default)
            {
                game.ProcessedRecords.Retire(retirementCutoff, nowUtc, ActivityValidationPolicy.RetirementBatchSize);
                game.Ledger.RetireActivityRows(retirementCutoff, ActivityValidationPolicy.RetirementBatchSize);
            }

            // Bounded diagnostic evidence of this batch, committed in the SAME atomic
            // write as the rewards it describes (never consulted by progression math).
            //
            // The per-reason breakdown is kept, not just the aggregate: "the player's whole
            // backlog was outside the acceptance window" and "the source returned a zero
            // record" must not look identical to the only person who can act on it (D-063).
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
                RejectionCountsByReason = ToReasonCounts(rejectionCounts),
                DeletionsIgnored = deletionsIgnored,
                StaleRevisionsIgnored = staleRevisions,
                QuantityClamped = clamped,
                RetiredRecordsIgnored = retiredIgnored,
                LastFetchAttemptedUtc = nowUtc,
                LastFetchSucceededUtc = nowUtc,
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
                FormatSummaryLines(game.PendingReturnSummary), SnapshotSummary(game),
                eligibleStepsTotal);
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
                // is untouched: nothing reached the trust pipeline, and the PREVIOUS batch's
                // counters and credited progress are deliberately preserved rather than
                // zeroed, because a failed fetch is not evidence that what was earned was
                // never earned (D-063).
                var previous = game.LastIngestionOutcome;
                game.LastIngestionOutcome = new IngestionOutcomeState
                {
                    Outcome = IngestionOutcomeKind.SourceFetchFailed,
                    CompletedAtUtc = _clock.UtcNow,
                    ErrorCategory = ex.GetType().Name,
                    TotalReceived = previous?.TotalReceived ?? 0,
                    Accepted = previous?.Accepted ?? 0,
                    Rejected = previous?.Rejected ?? 0,
                    DuplicatesIgnored = previous?.DuplicatesIgnored ?? 0,
                    CorrectionsApplied = previous?.CorrectionsApplied ?? 0,
                    DeletionsApplied = previous?.DeletionsApplied ?? 0,
                    VitalityCredited = previous?.VitalityCredited ?? 0L,
                    UnappliedReversalVitality = previous?.UnappliedReversalVitality ?? 0L,
                    RejectionCountsByReason = previous?.RejectionCountsByReason == null
                        ? null
                        : new Dictionary<int, int>(previous.RejectionCountsByReason),
                    DeletionsIgnored = previous?.DeletionsIgnored ?? 0,
                    StaleRevisionsIgnored = previous?.StaleRevisionsIgnored ?? 0,
                    QuantityClamped = previous?.QuantityClamped ?? 0,
                    RetiredRecordsIgnored = previous?.RetiredRecordsIgnored ?? 0,
                    LastFetchAttemptedUtc = _clock.UtcNow,
                    LastFetchSucceededUtc = previous?.LastFetchSucceededUtc,
                };
                PersistOrThrow();
                throw;
            }

            // D-130: a provider bridge that returns null (not an empty list) means "nothing new".
            return IngestActivityBatch(records ?? Array.Empty<NormalizedActivityRecord>());
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
            ref DateTimeOffset maxTrustedEndUtc,
            ref long eligibleStepsTotal)
        {
            int revision = Math.Max(1, record.Revision);
            long eligibleSteps = ActivityValidationPolicy.ClampQuantity(record.Category, record.Quantity);
            if (eligibleSteps != record.Quantity)
                clamped++;

            // A correction REPLACES a previously trusted quantity, so counting this record's
            // full eligible steps would double-count it against the batch's requested total.
            // The net change is the honest contribution to the batch's eligible figure.
            eligibleStepsTotal += eligibleSteps - existing.EligibleSteps;

            // D-079: undo the old contribution to the provider pool (its steps less what it
            // credited), then add the corrected steps. The row is re-attributed from that pool.
            //
            // D-178: the pool is clamped to 0..MaxCarry, like the deletion path. The row stores net
            // APPLIED Vitality, so after a clawback the balance could not cover (clamped, counted
            // as UnappliedReversalVitality) it holds more Vitality than its steps justify, and the
            // unclamped formula read that excess as up to hundreds of carried steps. Each later
            // up-correction then credited it again: a record oscillating 150 -> 50 -> 150 steps
            // while the player had no unspent Vitality minted +1 per cycle, without bound. With
            // the clamp a row can never hold more than floor((MaxCarry + steps) / 100), so the
            // excess is bounded (at most one Vitality per record) and repeated cycles mint
            // nothing. The cost is under one Vitality of carry in rare legitimate re-attributions.
            long poolBefore = Math.Max(0L, Math.Min(StepConversionRuleV1.MaxCarry,
                game.ProcessedRecords.GetCarry(record.ProviderNamespace)
                - (existing.EligibleSteps - existing.VitalityCredited * StepConversionRuleV1.StepsPerVitality)));
            long targetVitality = StepConversionRuleV1.ConvertWithCarry(poolBefore, eligibleSteps, out long carryAfter);
            game.ProcessedRecords.SetCarry(record.ProviderNamespace, carryAfter);
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
                revision,
                record.EndUtc.ToUniversalTime(),
                LocalDayCalendar.DayOf(record.EndUtc, record.EndZoneOffsetSeconds) ?? existing.LocalDay));

            DateTimeOffset endUtc = record.EndUtc.ToUniversalTime();
            if (endUtc > maxTrustedEndUtc)
                maxTrustedEndUtc = endUtc;
            corrections++;
        }

        /// <summary>
        /// Deletion policy: reverse remaining credited value conservatively (clamped to
        /// balance), mark the row deleted by zeroing it, or count an ignored marker.
        /// Deletion records are never eligible for crediting.
        ///
        /// Reached ONLY after the record passed <see cref="ActivityValidationPolicy.Validate"/>
        /// (D-053). The checks below are POLICY decisions, not eligibility decisions, and are
        /// counted as such in the batch diagnostics.
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
                // Defence in depth: eligibility rejects an empty provider namespace before
                // reaching here, so this is unreachable through IngestActivityBatch. It is
                // retained because ProcessDeletion is also reachable from a caller that
                // has not validated, and silently reversing nothing is the safe default.
                deletionsIgnored++;
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

            // D-079: take this record's uncredited steps back out of the provider pool. Steps
            // it borrowed from earlier carry are not re-issued (clamped to 0..99), so a
            // deletion never creates credit; the drift is under one Vitality per deletion.
            long pool = game.ProcessedRecords.GetCarry(record.ProviderNamespace)
                - (existing.EligibleSteps - existing.VitalityCredited * StepConversionRuleV1.StepsPerVitality);
            game.ProcessedRecords.SetCarry(record.ProviderNamespace,
                Math.Max(0L, Math.Min(StepConversionRuleV1.MaxCarry, pool)));

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
                LastRevision: revision,
                RecordEndUtc: record.EndUtc.ToUniversalTime(),
                LocalDay: existing.LocalDay));

            DateTimeOffset endUtc = record.EndUtc.ToUniversalTime();
            if (endUtc > maxTrustedEndUtc)
                maxTrustedEndUtc = endUtc;
            deletionsApplied++;
        }

        public DomainResult EnqueueProject(string projectId)
        {
            var game = RequireState();
            // D-130: a null/empty id is an unknown project, not an ArgumentNullException from a dictionary lookup.
            if (string.IsNullOrEmpty(projectId))
                return DomainResult.Fail(ErrorCodes.UnknownProject, "Unknown project ''.");
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

            if (string.IsNullOrEmpty(projectId)) // D-130
                return DomainResult.Fail(ErrorCodes.NotQueued, "Project '' is not queued.");
            var definition = _content.FindProject(projectId);
            var runtime = game.Region.FindProject(projectId);
            if (definition == null || runtime == null || runtime.Status != ProjectStatus.Queued)
                return DomainResult.Fail(ErrorCodes.NotQueued, $"Project '{projectId}' is not queued.");

            // D-054: a secondary-resource requirement gates activation, all-or-nothing.
            // Checked BEFORE the queue is mutated so a failure consumes nothing and leaves
            // the active slot and the queue exactly as they were. The shortfall is named so
            // the shell can tell the player exactly what to go and produce.
            foreach (var requirement in definition.ResourceCosts)
            {
                long held = game.Resources.Get(requirement.Key);
                if (held < requirement.Value)
                {
                    return DomainResult.Fail(
                        ErrorCodes.ResourceRequirementNotMet,
                        $"'{definition.TitleKey}' needs {requirement.Value} {requirement.Key} to begin; you have {held}.");
                }
            }

            if (!game.Queue.QueuedProjectIds.Remove(projectId))
                return DomainResult.Fail(ErrorCodes.NotQueued, $"Project '{projectId}' is not queued.");

            runtime.Status = ProjectStatus.Active;
            game.Queue.ActiveProjectId = projectId;
            OfflineAdvancer.ConsumeRequirements(game, definition);

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
            int buildStages = 1;
            if (activeId != null)
            {
                var def = _content.FindProject(activeId);
                var runtime = region.FindProject(activeId);
                if (def != null && runtime != null)
                {
                    activeTitle = ContentTitle(def.Id.Value, def.TitleKey);
                    invested = runtime.VitalityInvested;
                    cost = def.VitalityCost;
                    buildStages = def.VisibleBuildStages;
                }
            }

            var queuedRows = new List<HomeReadModel.QueuedRow>();
            foreach (var queuedId in game.Queue.QueuedProjectIds)
            {
                var def = _content.FindProject(queuedId);
                queuedRows.Add(new HomeReadModel.QueuedRow(queuedId, def == null ? string.Empty : ContentTitle(queuedId, def.TitleKey)));
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
                landmarkRows.Add(new HomeReadModel.LandmarkRow(landmark.Id.Value, ContentTitle(landmark.Id.Value, landmark.TitleKey), stage));
            }

            long vitality = game.Resources.Get(ResourceType.Vitality);

            bool regionComplete = game.Region.IsCompleted;
            bool queueIdle = activeId == null && queuedRows.Count == 0;
            long banked = queueIdle ? vitality : 0L;
            var reason = HomeAttentionReason.None;
            bool requiresAttention = false;

            // D-055: a pending summary only claims attention while it holds at least one
            // CURRENT item. A summary whose every item is historical is still readable
            // (HasPendingSummary stays true) but the app must stop permanently demanding
            // attention for a frozen screen — that is what made the least-engaged player
            // the one who got the least useful information.
            bool summaryHasCurrentItem = false;
            if (game.PendingReturnSummary != null)
            {
                var summary = SnapshotSummary(game);
                summaryHasCurrentItem = summary != null && summary.HasCurrentItems;
            }

            if (summaryHasCurrentItem)
            {
                requiresAttention = true;
                reason = HomeAttentionReason.PendingReturnSummary;
            }
            // D-061: the banked-Vitality alarm is suppressed once the region is complete. It
            // asked the player to queue a project that no longer exists — a permanent
            // unsatisfiable instruction for exactly the player who engaged most. Pre-completion
            // it is unchanged and still pinned by M5H1ShellAcceptanceTests.Scenario06.
            else if (queueIdle && banked > 0 && !regionComplete)
            {
                requiresAttention = true;
                reason = HomeAttentionReason.QueueEmptyWithBankedVitality;
            }
            // A completed region is a CONCLUSION, so it never claims attention; the reason
            // value still distinguishes "finished" from "nothing has happened yet".
            else if (regionComplete)
            {
                reason = HomeAttentionReason.RegionComplete;
            }
            else if (queueIdle && !anyStarted)
            {
                reason = HomeAttentionReason.NoProjectStartedYet;
            }

            var careProgram = FindAuthoredCareProgram(_content);

            return new HomeReadModel(
                ContentTitle(_content.Id.Value, _content.TitleKey),
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
                banked,
                regionComplete,
                game.Region.RegionCompletedAtUtc,
                game.Region.PostCompletionCyclesCompleted,
                game.Region.PostCompletionInvestedVitality,
                careProgram?.VitalityCostPerCycle ?? 0L,
                buildStages);
        }

        /// <summary>
        /// The region's authored post-completion program, or null when it authors none. One
        /// program is authored for Region 1; the lookup is content-only and deterministic so
        /// every surface describes the same program.
        /// </summary>
        private static PostCompletionProgramDefinition? FindAuthoredCareProgram(RegionDefinition content)
        {
            foreach (var program in content.PostCompletionPrograms)
                return program;
            return null;
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
                    ContentTitle(definition.Id.Value, definition.TitleKey),
                    definition.VitalityCost,
                    runtime.VitalityInvested,
                    runtime.Status,
                    queuedPositions.ContainsKey(definition.Id.Value) ? position : (int?)null,
                    prerequisites,
                    definition.ResourceCosts,
                    OfflineAdvancer.CanAffordRequirements(game, definition)));
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
                landmarks.Add(new RegionReadModel.LandmarkRow(landmark.Id.Value, ContentTitle(landmark.Id.Value, landmark.TitleKey), stage));
            }

            var producers = new List<RegionReadModel.ProducerRow>();
            foreach (var definition in _content.Producers)
            {
                var runtime = game.Region.FindProducer(definition.Id.Value);
                long cap = _content.ResourceCap(definition.Output) ?? long.MaxValue;
                long held = game.Resources.Get(definition.Output);
                producers.Add(new RegionReadModel.ProducerRow(
                    definition.Id.Value,
                    ContentTitle(definition.Id.Value, definition.TitleKey),
                    definition.Output,
                    definition.MilliUnitsPerDay,
                    definition.CapacityUnits,
                    runtime?.Unlocked ?? false,
                    runtime?.StoredMilliUnits ?? 0L,
                    runtime?.TotalProducedMilliUnits ?? 0L,
                    resourceCapUnits: cap,
                    // "Capped" and "idle" are different things and the shell has to be able
                    // to tell them apart: a capped producer has produced plenty and is
                    // holding it in its pending store (D-054).
                    capacityLimited: runtime?.Unlocked == true && cap != long.MaxValue && held >= cap));
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

            RegionReadModel.PostCompletionCareRow? care = null;
            if (game.Region.IsCompleted)
            {
                var program = FindAuthoredCareProgram(_content);
                if (program != null)
                {
                    care = new RegionReadModel.PostCompletionCareRow(
                        program.Id,
                        ContentTitle(program.Id, program.TitleKey),
                        ContentLocale.Resolve(program.Id, WalkGame.Application.Content.ContentText.Fields.Description, program.DescriptionKey),
                        game.Region.PostCompletionCyclesCompleted,
                        game.Region.PostCompletionInvestedVitality,
                        program.VitalityCostPerCycle);
                }
            }

            int buildStage = 0, buildStages = 1;
            string? activeTitle = null;
            if (game.Queue.ActiveProjectId != null)
            {
                foreach (var def in _content.Projects)
                {
                    if (def.Id.Value != game.Queue.ActiveProjectId) continue;
                    var st = game.Region.FindProject(def.Id.Value);
                    activeTitle = ContentTitle(def.Id.Value, def.TitleKey);
                    buildStages = def.VisibleBuildStages;
                    if (st != null && st.Status != ProjectStatus.Completed)
                        buildStage = def.BuildStageFor(st.VitalityInvested);
                    break;
                }
            }

            return new RegionReadModel(
                ContentTitle(_content.Id.Value, _content.TitleKey),
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
                expeditionsCompleted,
                care,
                buildStage,
                buildStages,
                activeTitle);
        }

        /// <summary>Discovery journal snapshot: every authored discovery with its flags.</summary>
        /// <summary>Story journal snapshot derived from restoration state (D-076).</summary>
        /// <summary>Days walked (D-103): maintained count plus whether it is a lower bound.</summary>
        public DaysWalkedReadModel GetDaysWalked()
        {
            var processed = RequireState().ProcessedRecords;
            return new DaysWalkedReadModel(processed.DaysWalkedCount, processed.DaysWalkedIsLowerBound);
        }

        /// <summary>
        /// D-156: Vitality today's walks earned. "Today" is the local day of the trusted clock's
        /// instant under <paramref name="utcOffsetSeconds"/> (the device's current offset), the same
        /// day rule records use (D-103). Derived, read-only, no clock write.
        /// </summary>
        public DailyEarningsReadModel GetTodayEarnings(int utcOffsetSeconds)
        {
            var processed = RequireState().ProcessedRecords;
            int? today = LocalDayCalendar.DayOf(_clock.UtcNow, utcOffsetSeconds);
            if (today == null) return DailyEarningsReadModel.Unknown;
            long? v = processed.VitalityOnLocalDay(today.Value);
            return v == null ? new DailyEarningsReadModel(today, 0L, false) : new DailyEarningsReadModel(today, v.Value, true);
        }

        public StoryReadModel GetStory()
        {
            var game = RequireState();
            var progress = StoryProgress.Evaluate(_content, game.Region, game.CreatedAtUtc);

            var rows = new List<StoryReadModel.ChapterRow>(progress.Count);
            string? nextHint = null;
            foreach (var p in progress)
            {
                if (!p.Unlocked && nextHint == null) nextHint = ContentLocale.Resolve(p.Chapter.Id, WalkGame.Application.Content.ContentText.Fields.Hint, p.Chapter.HintKey);
                rows.Add(new StoryReadModel.ChapterRow(
                    p.Chapter.Id, p.Chapter.Number, ContentTitle(p.Chapter.Id, p.Chapter.TitleKey),
                    p.Unlocked ? ContentLocale.Resolve(p.Chapter.Id, WalkGame.Application.Content.ContentText.Fields.Body, p.Chapter.BodyKey) : null,
                    ContentLocale.Resolve(p.Chapter.Id, WalkGame.Application.Content.ContentText.Fields.Hint, p.Chapter.HintKey), p.Unlocked, p.UnlockedAtUtc,
                    p.Chapter.RequiredProjectIds, p.Chapter.MinCareCycles));
            }

            return new StoryReadModel(rows, StoryProgress.UnlockedCount(progress), nextHint);
        }

        /// <summary>
        /// D-118: Nell's letters. A letter is found once its project is completed; pure projection,
        /// no clock and no writes. A letter whose project is not in the loaded content stays unfound.
        /// </summary>
        /// <summary>
        /// D-122: presentation overlay for content copy. Defaults to the authored English; never
        /// persisted and never read by canonical state, validation or saves.
        /// </summary>
        public WalkGame.Application.Content.ContentText ContentLocale { get; set; } = WalkGame.Application.Content.ContentText.English;

        /// <summary>D-122: a content title through the presentation overlay (identity in English).</summary>
        private string ContentTitle(string stableId, string authoredEnglish)
            => ContentLocale.Resolve(stableId, WalkGame.Application.Content.ContentText.Fields.Title, authoredEnglish);

        /// <summary>
        /// D-122: authored discovery copy (body or origin, D-086) through the overlay. Returns null for
        /// an unknown key so a raw key never reaches a screen.
        /// </summary>
        public string? DiscoveryCopy(string discoveryId, string field, string copyKey)
        {
            var english = WalkGame.Application.Content.Region1DiscoveryCopy.Text(copyKey);
            return english == null ? null : ContentLocale.Resolve(discoveryId, field, english);
        }

        /// <summary>
        /// D-165 (OpenSpec add-interactive-world-presentation): the world view, one row per location,
        /// derived from canonical state. <paramref name="seenDialogueLineIds"/> is presentation-held
        /// "seen" state (never saved); null means nothing seen yet. Read-only, no clock write.
        /// </summary>
        public WorldViewReadModel GetWorldView(IReadOnlyCollection<string>? seenDialogueLineIds = null) =>
            WorldPresentationProjector.WorldView(_content, RequireState(), ContentLocale, seenDialogueLineIds);

        /// <summary>D-165: every character with the lines reached so far (revisitable). Gates nothing.</summary>
        public IReadOnlyList<CharacterDialogueReadModel> GetCharacters() =>
            WorldPresentationProjector.Characters(RequireState(), ContentLocale);

        /// <summary>
        /// D-167: what each selectable or in-progress restoration goal would change (typed consequences,
        /// before/after variant keys). Computed with the advancer's own completion rules; read-only.
        /// </summary>
        public RestorationChoicesReadModel GetRestorationChoices() =>
            WorldChangeProjector.Choices(_content, RequireState(), ContentLocale);

        /// <summary>
        /// D-167: visual changes after <paramref name="sinceUtc"/>, derived from canonical completion
        /// times (works across restarts and offline returns; nothing new is persisted). Empty when the
        /// history cannot be reproduced, never invented. Use <see cref="WorldViewDiff.Between"/> for
        /// in-session construction advances.
        /// </summary>
        public IReadOnlyList<VisualChangeEvent> GetVisualChangesSince(DateTimeOffset sinceUtc) =>
            WorldChangeProjector.ChangesSince(_content, RequireState(), sinceUtc);

        public LettersReadModel GetLetters()
        {
            var game = RequireState();
            var rows = new List<LettersReadModel.LetterRow>(WalkGame.Application.Content.Region1Letters.All.Count);
            foreach (var l in WalkGame.Application.Content.Region1Letters.All)
            {
                var p = game.Region.FindProject(l.ProjectId);
                bool found = p != null && p.Status == ProjectStatus.Completed;
                var text = ContentLocale;
                rows.Add(new LettersReadModel.LetterRow(l.Id, l.Order,
                    text.Resolve(l.Id, WalkGame.Application.Content.ContentText.Fields.Place, l.PlaceKey),
                    found ? text.Resolve(l.Id, WalkGame.Application.Content.ContentText.Fields.Body, l.Body) : null, found,
                    found ? p!.CompletedAtUtc : null));
            }
            return new LettersReadModel(rows);
        }

        /// <summary>
        /// D-129: the Region 1 epilogue, a pure read-only projection of the region's completion
        /// time and the trusted clock. Nothing before completion; afterwards the closing letters,
        /// the final journal entry and the valley notes reveal gently and never disappear.
        /// </summary>
        public EpilogueReadModel GetEpilogue()
        {
            var game = RequireState();
            if (!game.Region.IsCompleted || game.Region.RegionCompletedAtUtc == null) return EpilogueReadModel.None;
            int days = WalkGame.Application.Content.Region1Epilogue.DaysSince(game.Region.RegionCompletedAtUtc.Value, _clock.UtcNow);
            var text = ContentLocale;
            string R(WalkGame.Application.Content.Region1Epilogue.Entry e) => text.Resolve(e.Id, WalkGame.Application.Content.ContentText.Fields.Body, e.Body);
            var letters = new List<string>();
            foreach (var e in WalkGame.Application.Content.Region1Epilogue.ClosingLetters) if (days >= e.DayAfterCompletion) letters.Add(R(e));
            var notes = new List<string>();
            foreach (var e in WalkGame.Application.Content.Region1Epilogue.ValleyNotes) if (days >= e.DayAfterCompletion) notes.Add(R(e));
            var current = R(WalkGame.Application.Content.Region1Epilogue.ValleyNotes[WalkGame.Application.Content.Region1Epilogue.CurrentNoteIndex(days)]);
            return new EpilogueReadModel(true, R(WalkGame.Application.Content.Region1Epilogue.JournalEntry), letters, notes, current);
        }

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
                    ContentTitle(definition.Id.Value, definition.TitleKey),
                    definition.BodyKey,
                    definition.ProvenanceKey,
                    definition.LocationKey,
                    isUnlocked,
                    runtime?.DiscoveredAtUtc,
                    runtime?.Reviewed ?? false,
                    definition.UnlockedByProjectId));
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
                    ContentTitle(definition.Id.Value, definition.TitleKey),
                    ContentLocale.Resolve(definition.Id.Value, WalkGame.Application.Content.ContentText.Fields.Description, definition.DescriptionKey),
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

        /// <summary>
        /// D-092: a fresh-device start (no save in either slot) must not inherit onboarding
        /// progress from an erased world. Delete-all erases saves before preferences, so an
        /// app killed between the two leaves a stale stage (possibly Complete) that would
        /// skip onboarding for the new world. Only the stage is reset; other preferences
        /// (motion, sound, text) are player choices and stay. Best-effort: a preferences
        /// write failure never fails the start (the save is already durable).
        /// </summary>
        private void ResetStaleOnboarding()
        {
            if (_preferencesStore == null || _preferences.OnboardingStage == OnboardingStage.NotStarted) return;
            try { UpdatePreferences(p => p.OnboardingStage = OnboardingStage.NotStarted); }
            catch (IOException) { }
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

            return ProjectActivityStatus(RequireState());
        }

        /// <summary>
        /// Shared projection of the durable last outcome. Kept private and shared so the
        /// activity-status surface and the glance can never disagree about the same facts.
        /// </summary>
        private ActivityStatusReadModel ProjectActivityStatus(GameState game)
        {
            var outcome = game.LastIngestionOutcome;
            return ActivityStatusProjector.Project(
                _connectionPort == null ? null : _connectionPort.SnapshotConnection(),
                hasProcessedAnyRecord: game.ProcessedRecords.Count > 0,
                lastOutcome: outcome?.Outcome ?? IngestionOutcomeKind.NeverRun,
                lastBatchVitalityCredited: outcome?.Outcome == IngestionOutcomeKind.Succeeded ? outcome.VitalityCredited : 0L,
                lastProcessedAtUtc: outcome?.Outcome == IngestionOutcomeKind.Succeeded ? outcome.CompletedAtUtc : null);
        }

        // ---------------------------------------------------------------------
        // Player glance: the three answers in sentences, derived entirely from
        // reads. Consumes the existing attention classification rather than
        // re-deriving it, and never saves, acknowledges, or ingests — so a
        // second read returns the same sentences (D-065).
        // ---------------------------------------------------------------------

        /// <summary>
        /// One glance in bounded sentences: what changed, what is being restored now, and
        /// whether anything must be done.
        ///
        /// Parameterless and total: a session composed without an activity connection port
        /// (the normal headless simulation composition) still produces the source-unavailable
        /// activity sentence instead of throwing. Reading it has no side effects.
        /// </summary>
        public GlanceReadModel GetGlance()
        {
            var game = RequireState();
            var home = GetHome();
            PostCompletionProgramDefinition? careProgram = home.RegionComplete
                ? FindAuthoredCareProgram(_content)
                : null;

            return GlanceProjector.Project(
                home,
                GetPendingReturnSummary(),
                GetOnboarding(),
                ProjectActivityStatus(game),
                _content,
                careProgram,
                ContentLocale);
        }

        /// <summary>Support-oriented diagnostics snapshot. Privacy-safe by construction:
        /// classified enums, bounded counters, timestamps; adapter technical detail only via
        /// the adapter-owned string. Never raw records or payloads (D-044). Deliberately
        /// available EVEN WHEN boot failed — that is exactly when support needs it.</summary>
        public DiagnosticsReadModel GetDiagnostics()
        {
            var nowUtc = _clock.UtcNow;
            var game = _state;

            IngestionOutcomeState? outcome = game?.LastIngestionOutcome;
            long? watermarkAgeDays = null;
            if (game != null && game.IngestionCheckpointUtc != default)
            {
                var checkpointAge = nowUtc - game.IngestionCheckpointUtc;
                if (checkpointAge < TimeSpan.Zero)
                    checkpointAge = TimeSpan.Zero;
                watermarkAgeDays = (long)checkpointAge.TotalDays;
            }

            var (reconciliation, reconciliationPending) = ClassifyReconciliation(
                game, outcome, nowUtc);

            return new DiagnosticsReadModel(
                generatedAtUtc: nowUtc,
                bootOutcome: _lastBootOutcome,
                recoveredFromBackup: _lastBootRecoveredFromBackup,
                bootRecoveryCause: _lastBootRecoveryCause,
                lastBootCodecFailure: _lastBootCodecFailure,
                appliedMigrationsAtBoot: _lastBootAppliedMigrations,
                schemaVersion: game?.SchemaVersion ?? 0,
                contentCompatibility: _lastContentCompatibility,
                saveContentVersion: _lastSaveContentVersion,
                buildContentVersion: _lastBuildContentVersion,
                regionId: game?.Region.RegionId ?? string.Empty,
                ingestionCheckpointUtc: game?.IngestionCheckpointUtc ?? DateTimeOffset.MinValue,
                checkpointWatermarkAgeDays: watermarkAgeDays,
                processedRecordCount: game?.ProcessedRecords.Count ?? 0,
                lifetimeVitalityCredited: game?.ProcessedRecords.TotalVitalityCredited ?? 0L,
                unappliedReversalVitality: game?.ProcessedRecords.UnappliedReversalVitality ?? 0L,
                lastIngestion: outcome == null ? null : DiagnosticsIngestionRow.FromState(outcome),
                preferencesLoadOutcome: _preferencesLoadOutcome,
                preferencesLoadDetail: BoundDetail(_preferencesLoadDetail),
                connectionTechnicalDetail: BoundDetail(_connectionPort?.SnapshotConnection().TechnicalDetail),
                reconciliationState: reconciliation,
                reconciliationPending: reconciliationPending);
        }

        /// <summary>
        /// Classifies source health from DURABLE evidence only (D-063).
        ///
        /// Skew policy, stated once: when the injected clock reads EARLIER than the newest
        /// durable evidence, the pair cannot agree about how long ago that evidence was
        /// produced, and the classification fails to Stale. A reassuring "current" computed
        /// from a clock that has moved backwards is how a player whose activity stopped
        /// counting gets told that everything is fine.
        /// </summary>
        private static (ReconciliationState State, bool Pending) ClassifyReconciliation(
            GameState? game, IngestionOutcomeState? outcome, DateTimeOffset nowUtc)
        {
            if (outcome == null || outcome.LastFetchAttemptedUtc == null)
                return (ReconciliationState.NeverAttempted, false);

            DateTimeOffset lastAttempt = outcome.LastFetchAttemptedUtc!.Value;
            DateTimeOffset? lastSuccess = outcome.LastFetchSucceededUtc;
            DateTimeOffset watermark = game?.IngestionCheckpointUtc ?? default;

            // A fetch that was attempted after the last success and did not succeed.
            if (lastSuccess == null || lastAttempt > lastSuccess.Value)
                return (ReconciliationState.FailedSinceLastSuccess, game != null && watermark != default);

            // The newest durable activity evidence: the later of the last successful fetch and
            // the trusted watermark, both of which are durable rather than wall-clock.
            DateTimeOffset newestEvidence = lastSuccess.Value;
            if (watermark > newestEvidence)
                newestEvidence = watermark;

            // Backward clock: the evidence says one thing and the clock says another. Stale.
            if (nowUtc < newestEvidence)
                return (ReconciliationState.Stale, true);

            bool stale = nowUtc - newestEvidence > TimeSpan.FromDays(DiagnosticsReadModel.StalenessThresholdDays);

            // Reconciliation is pending when the profile HAS activity history and the game has
            // kept advancing since the newest trusted record arrived.
            bool pending = watermark != default
                && game != null
                && game.LastAdvancedUtc > watermark.AddDays(1);

            return (stale ? ReconciliationState.Stale : ReconciliationState.Current, pending);
        }

        /// <summary>
        /// Maps the batch's per-reason counts onto the durable ordinal keying. The keys already
        /// ARE the status names (that is how the batch accumulates them), so the mapping is a
        /// parse — and an unknown key is surfaced as an unmapped ordinal rather than dropped,
        /// because evidence about a rejection must not vanish into a rename.
        /// </summary>
        private static Dictionary<int, int> ToReasonCounts(SortedDictionary<string, int> rejectionCounts)
        {
            var durable = new Dictionary<int, int>();
            foreach (var pair in rejectionCounts)
            {
                if (Enum.TryParse(pair.Key, out ActivityValidationStatus status))
                {
                    durable[(int)status] = pair.Value;
                }
                else
                {
                    // Unknown reason name: keep the count under an explicit unmapped ordinal so
                    // the breakdown still sums to the recorded rejection total.
                    const int Unmapped = -1;
                    durable[Unmapped] = durable.TryGetValue(Unmapped, out int held) ? held + pair.Value : pair.Value;
                }
            }
            return durable;
        }

        /// <summary>Maps durable ordinals back to their classified status for the read model.</summary>
        private static IReadOnlyDictionary<ActivityValidationStatus, int> ToBreakdown(
            Dictionary<int, int>? counts)
        {
            var map = new SortedDictionary<ActivityValidationStatus, int>();
            if (counts != null)
            {
                foreach (var pair in counts)
                    map[(ActivityValidationStatus)pair.Key] = pair.Value;
            }
            return map;
        }
        private static string? BoundDetail(string? detail)
        {
            if (detail == null)
                return null;
            return detail.Length <= 300 ? detail : detail.Substring(0, 300);
        }

        private void CaptureBootOutcome(DiagnosticsBootOutcome outcome, bool recoveredFromBackup,
            SaveRecoveryCause cause = SaveRecoveryCause.None)
        {
            _lastBootOutcome = outcome;
            _lastBootRecoveredFromBackup = recoveredFromBackup;
            _lastBootRecoveryCause = recoveredFromBackup ? cause : SaveRecoveryCause.None;
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
                // D-094: an outdated provider outranks every permission state (as in D-093's projector step 0).
                if (snapshot.Availability == ActivitySourceAvailability.ProviderUpdateRequired)
                    activityStep = OnboardingActivityStepState.ProviderUpdateRequired;
                else switch (snapshot.Permission)
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

        private StartResult FinishBoot(GameState state, SaveRecoveryCause cause)
        {
            bool recoveredFromBackup = cause != SaveRecoveryCause.None;
            _state = state;
            CaptureBootOutcome(
                cause switch
                {
                    SaveRecoveryCause.PrimaryMissing => DiagnosticsBootOutcome.RecoveredFromMissingPrimary,
                    SaveRecoveryCause.PrimaryDamaged => DiagnosticsBootOutcome.RecoveredFromBackup,
                    _ => DiagnosticsBootOutcome.Loaded,
                },
                recoveredFromBackup,
                cause);

            var events = new List<SimulationEvent>();
            OfflineAdvancer.Advance(state, _content, _clock.UtcNow, events);

            // Compose BEFORE persisting: a crash after commit but before presentation
            // still finds the summary on disk at next boot.
            ComposePendingSummary(state, events);
            if (recoveredFromBackup)
                state.PendingReturnSummary = ReturnSummaryComposer.WithNotice(
                    state.PendingReturnSummary, _clock.UtcNow, RecoveryNotice(cause));

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

        /// <summary>
        /// Calm, non-technical, cause-accurate recovery notice. "Damaged" would be untrue
        /// when the primary file was simply missing, and an inaccurate notice on a recovery
        /// is how a player concludes their world was corrupted and starts over.
        /// </summary>
        private static string RecoveryNotice(SaveRecoveryCause cause) => cause switch
        {
            SaveRecoveryCause.PrimaryMissing =>
                "Your most recent save could not be found; the last healthy backup was restored.",
            _ =>
                "Your latest save was damaged; the most recent healthy backup was restored.",
        };

        /// <summary>Merges committed events into the durable pending summary. Skipped when
        /// nothing happened, so an idle operation never touches the existing summary.</summary>
        private void ComposePendingSummary(GameState game, List<SimulationEvent> events)
        {
            if (events == null || events.Count == 0)
                return;
            game.PendingReturnSummary = ReturnSummaryComposer.Compose(
                events, _content, game.PendingReturnSummary, _clock.UtcNow, game.Region.IsCompleted);
        }

        /// <summary>
        /// Projects the durable summary for presentation. Item currency is measured against
        /// the injected clock NOW, not against the summary's own generation time, so an
        /// un-recomposed summary still ages out of the attention signal (D-055).
        /// </summary>
        private ReturnSummaryReadModel? SnapshotSummary(GameState game) =>
            game.PendingReturnSummary == null
                ? null
                : ReturnSummaryReadModel.FromState(game.PendingReturnSummary, _clock.UtcNow, SummaryDisplay());

        private WalkGame.Application.Summaries.SummaryLineText? _summaryText;

        /// <summary>
        /// D-162: read-time display form of stored summary lines in every locale, English included
        /// (imperative project titles and "... Route" expedition titles get a form that reads well).
        /// Stored text is untouched; <see cref="ReturnSummaryReadModel.ItemRow.Text"/> stays canonical.
        /// </summary>
        private Func<string?, string> SummaryDisplay()
        {
            var t = _summaryText;
            if (t == null || !ReferenceEquals(t.Locale, ContentLocale))
                _summaryText = t = new WalkGame.Application.Summaries.SummaryLineText(ContentLocale, _content);
            return t.Display;
        }

        /// <summary>D-138: read-time display localizer for stored summary lines (null in English).</summary>
        private Func<string?, string>? SummaryLocalizer()
        {
            if (ReferenceEquals(ContentLocale, WalkGame.Application.Content.ContentText.English)) return null;
            var t = new WalkGame.Application.Summaries.SummaryLineText(ContentLocale, _content);
            return t.Localize;
        }

        private List<string> FormatSummaryLines(PendingReturnSummaryState? summary)
        {
            var lines = new List<string>();
            if (summary == null)
                return lines;
            var loc = SummaryLocalizer();
            foreach (var item in summary.Items)
                lines.Add(loc == null ? item.Text : loc(item.Text));
            if (!string.IsNullOrEmpty(summary.PrimaryNextAction))
                lines.Add("→ " + (loc == null ? summary.PrimaryNextAction : loc(summary.PrimaryNextAction)));
            return lines;
        }

        private DecodeAndValidateOutcome DecodeAndValidate(byte[] envelopeBytes)
        {
            var decoded = _codec.Decode(envelopeBytes);
            if (decoded.Status != CodecStatus.Ok || decoded.State == null)
            {
                // Recorded monotonically: a later successful BACKUP decode must not erase
                // the evidence that the primary failed (that failure caused the recovery).
                _lastBootCodecFailure = MapCodecFailure(decoded.Status);
                return new DecodeAndValidateOutcome(null, DescribeDecodeFailure(decoded),
                    failedStateValidation: false);
            }

            IReadOnlyList<string> violations;
            try
            {
                violations = GameStateValidator.Validate(decoded.State, _content);
            }
            catch (Exception ex) when (ex is NullReferenceException || ex is InvalidOperationException
                                       || ex is ArgumentException || ex is OverflowException || ex is KeyNotFoundException)
            {
                // D-098: a structurally broken payload (e.g. a null dedup entry) used to throw out
                // of the validator. Any such shape is invalid state: fail closed, file untouched.
                violations = new[] { "Save state is structurally incomplete (" + ex.GetType().Name + ")." };
            }
            if (violations.Count > 0)
            {
                _lastBootCodecFailure = CodecFailureCategory.StateValidationFailed;
                return new DecodeAndValidateOutcome(null, "Save state failed validation: " + string.Join("; ", violations),
                    failedStateValidation: true);
            }

            _lastBootAppliedMigrations = decoded.AppliedMigrations;
            return new DecodeAndValidateOutcome(decoded.State, null, failedStateValidation: false);
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
                _hasUnsavedChanges = false;
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                _hasUnsavedChanges = true;
                throw new IOException("Persisting game state failed: " + ex.Message, ex);
            }
            catch (IOException)
            {
                _hasUnsavedChanges = true;
                throw;
            }
        }

        private bool _hasUnsavedChanges;

        /// <summary>
        /// D-131: true after a commit failed, while the in-memory state is ahead of the last
        /// durable generation. Cleared by any later successful commit, by
        /// <see cref="RetryPendingSave"/>, or by a reload (<see cref="Continue"/> replaces the
        /// in-memory state with the durable one, so nothing is "ahead" any more).
        /// </summary>
        public bool HasUnsavedChanges => _state != null && _hasUnsavedChanges;

        /// <summary>
        /// D-131: re-attempts the commit that failed. Writes the whole in-memory state, so a
        /// credited transaction lands exactly once (its ledger entry and processed-record id
        /// travel with it). Returns true when nothing is pending or the retry committed;
        /// false (never throws for I/O) when storage is still failing.
        /// </summary>
        public bool RetryPendingSave()
        {
            if (!HasUnsavedChanges) return true;
            try { return PersistOrThrow(); }
            catch (IOException) { return false; }
        }

        /// <summary>
        /// D-134 (closes the D-131 resume gap): while saves keep failing, a resume must not
        /// reload (that would drop the unsaved progress), but offline time still passed. This
        /// runs the same offline catch-up as a boot, in memory: producers tick from their own
        /// monotonic checkpoints and <c>LastAdvancedUtc</c> moves forward, so calling it twice
        /// at the same clock credits nothing more (exactly once). Any return-summary events are
        /// merged into the pending summary. It then tries one commit of the whole state and
        /// never throws for I/O. Returns true when the state is now durable.
        /// </summary>
        public bool CatchUpWithoutReload()
        {
            var state = RequireState();
            var events = new List<SimulationEvent>();
            OfflineAdvancer.Advance(state, _content, _clock.UtcNow, events);
            ComposePendingSummary(state, events);
            try { return PersistOrThrow(); }
            catch (IOException) { return false; }
        }

        private GameState RequireState() =>
            _state ?? throw new InvalidOperationException("No game state loaded. Call StartNewGame or Continue first.");
    }

    internal readonly struct DecodeAndValidateOutcome
    {
        public GameState? State { get; }
        public string? Error { get; }

        /// <summary>True when the envelope decoded cleanly and canonical state failed
        /// <see cref="GameStateValidator"/> instead. Distinguishes "the file is fine, the
        /// contents are not" from every file-level failure, which is what lets boot report
        /// <see cref="StartStatus.StateInvalid"/> rather than folding it into unreadable.</summary>
        public bool FailedStateValidation { get; }

        public DecodeAndValidateOutcome(GameState? state, string? error, bool failedStateValidation)
        {
            State = state;
            Error = error;
            FailedStateValidation = failedStateValidation;
        }
    }
}
