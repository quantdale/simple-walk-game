using System;
using System.Collections.Generic;
using WalkGame.Domain;

namespace WalkGame.Application.Persistence
{
    public enum SaveReadOutcome
    {
        Success = 0,
        NotFound = 1,
        IoFailure = 2,
    }

    /// <summary>Raw envelope bytes plus the storage-layer outcome.</summary>
    public sealed class SaveReadResult
    {
        public SaveReadOutcome Outcome { get; }
        public byte[]? EnvelopeBytes { get; }
        public string? Detail { get; }

        public SaveReadResult(SaveReadOutcome outcome, byte[]? envelopeBytes = null, string? detail = null)
        {
            Outcome = outcome;
            EnvelopeBytes = envelopeBytes;
            Detail = detail;
        }

        public static SaveReadResult Ok(byte[] bytes) => new SaveReadResult(SaveReadOutcome.Success, bytes);

        public static SaveReadResult Fail(SaveReadOutcome outcome, string detail) =>
            new SaveReadResult(outcome, null, detail);
    }

    /// <summary>
    /// Durable storage port. Implementations own file placement, atomicity and backups;
    /// they never interpret payload semantics. Primary and backup are addressed separately
    /// so the application layer owns recovery policy.
    /// </summary>
    public interface ISaveStore
    {
        SaveReadResult ReadPrimary();

        SaveReadResult ReadBackup();

        /// <summary>Atomically commits envelope bytes to primary and refreshes the backup.</summary>
        void WriteAtomic(byte[] envelopeBytes);

        /// <summary>
        /// Atomically commits envelope bytes to primary WITHOUT rotating the current
        /// primary into the backup slot; existing backup bytes stay untouched. The boot-
        /// recovery path uses this so a known-bad primary can never displace the last
        /// healthy generation while recovered state is being made durable.
        /// </summary>
        void WriteAtomicPreservingBackup(byte[] envelopeBytes);
    }

    /// <summary>
    /// Optional capability of a local store: permanently delete every generation it owns
    /// (privacy requirement 7, PRODUCT_SPEC §7a). Kept separate from <see cref="ISaveStore"/>
    /// and <see cref="WalkGame.Application.Ux.IUxPreferencesStore"/> so existing adapters
    /// opt in explicitly. Implementations must delete the authoritative file LAST so that
    /// an interrupted erase never leaves only a backup that boot recovery would resurrect.
    /// Throws on I/O failure; a repeated call after success is a no-op.
    /// </summary>
    public interface ILocalDataEraser
    {
        void EraseAll();
    }

    public enum CodecStatus
    {
        Ok = 0,
        MalformedEnvelope = 1,
        ChecksumMismatch = 2,
        VersionTooOld = 3,
        VersionTooNew = 4,
        MigrationFailed = 5,
        DeserializationFailed = 6,
    }

    public sealed class DecodeResult
    {
        public CodecStatus Status { get; }
        public GameState? State { get; }
        public int SourceSchemaVersion { get; }
        public IReadOnlyList<string> AppliedMigrations { get; }
        public string? Detail { get; }

        public DecodeResult(CodecStatus status, GameState? state, int sourceSchemaVersion,
            IReadOnlyList<string>? appliedMigrations = null, string? detail = null)
        {
            Status = status;
            State = state;
            SourceSchemaVersion = sourceSchemaVersion;
            AppliedMigrations = appliedMigrations ?? Array.Empty<string>();
            Detail = detail;
        }
    }

    /// <summary>
    /// Codec port: envelope framing, payload integrity, schema versioning and migration.
    /// Implementations live behind this boundary so the application layer stays
    /// serialization-agnostic.
    /// </summary>
    public interface ISaveCodec
    {
        DecodeResult Decode(byte[] envelopeBytes);

        byte[] Encode(GameState state, DateTimeOffset savedAtUtc);
    }

    public enum LocalDataEraseStatus
    {
        Erased = 0,
        NotSupported = 1,
        Failed = 2,
    }

    /// <summary>Outcome of <c>GameSession.DeleteAllLocalData</c>; detail is an exception type name only.</summary>
    public sealed class LocalDataEraseResult
    {
        public LocalDataEraseResult(LocalDataEraseStatus status, string? failureCategory)
        {
            Status = status;
            FailureCategory = failureCategory;
        }

        public LocalDataEraseStatus Status { get; }
        public string? FailureCategory { get; }
    }
}
