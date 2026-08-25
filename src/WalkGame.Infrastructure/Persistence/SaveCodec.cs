using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using WalkGame.Application.Persistence;
using WalkGame.Domain;

namespace WalkGame.Infrastructure.Persistence
{
    /// <summary>
    /// Versioned save envelope codec:
    /// envelope { schemaVersion, savedAtUtc, payloadSha256, payload(base64) }.
    /// Decode path: frame → checksum → version gates → sequential migration → deserialize.
    /// </summary>
    public sealed class SaveCodec : ISaveCodec
    {
        public const int CurrentSchemaVersion = SchemaVersions.Current;
        private const int MinimumSupportedVersion = SchemaVersions.MinimumSupported;

        private readonly MigrationRunner _migrations;

        public SaveCodec(MigrationRunner migrations)
        {
            _migrations = migrations ?? throw new ArgumentNullException(nameof(migrations));
        }

        public byte[] Encode(GameState state, DateTimeOffset savedAtUtc)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            byte[] payloadBytes = JsonSerializer.SerializeToUtf8Bytes(state, SaveJson.Options);

            var envelope = new SaveEnvelopeDto
            {
                SchemaVersion = state.SchemaVersion,
                SavedAtUtc = savedAtUtc,
                PayloadSha256Base64 = Convert.ToBase64String(Sha256(payloadBytes)),
                PayloadBase64 = Convert.ToBase64String(payloadBytes),
            };

            return JsonSerializer.SerializeToUtf8Bytes(envelope, SaveJson.Options);
        }

        public DecodeResult Decode(byte[] envelopeBytes)
        {
            if (envelopeBytes == null || envelopeBytes.Length == 0)
                return new DecodeResult(CodecStatus.MalformedEnvelope, null, 0, detail: "Empty save payload.");

            SaveEnvelopeDto? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<SaveEnvelopeDto>(envelopeBytes, SaveJson.Options);
            }
            catch (JsonException ex)
            {
                return new DecodeResult(CodecStatus.MalformedEnvelope, null, 0, detail: ex.Message);
            }

            if (envelope == null || string.IsNullOrEmpty(envelope.PayloadBase64))
                return new DecodeResult(CodecStatus.MalformedEnvelope, null, 0, detail: "Envelope is missing a payload.");

            byte[] payloadBytes;
            try
            {
                payloadBytes = Convert.FromBase64String(envelope.PayloadBase64);
            }
            catch (FormatException ex)
            {
                return new DecodeResult(CodecStatus.MalformedEnvelope, null, envelope.SchemaVersion, detail: ex.Message);
            }

            var expectedChecksum = Convert.FromBase64String(envelope.PayloadSha256Base64 ?? string.Empty);
            var actualChecksum = Sha256(payloadBytes);
            if (!FixedTimeEquals(expectedChecksum, actualChecksum))
                return new DecodeResult(CodecStatus.ChecksumMismatch, null, envelope.SchemaVersion,
                    detail: "Payload does not match its recorded integrity hash.");

            int sourceVersion = envelope.SchemaVersion;
            if (sourceVersion > CurrentSchemaVersion)
                return new DecodeResult(CodecStatus.VersionTooNew, null, sourceVersion);
            if (sourceVersion < MinimumSupportedVersion)
                return new DecodeResult(CodecStatus.VersionTooOld, null, sourceVersion);

            JsonNode? payloadNode;
            try
            {
                payloadNode = JsonNode.Parse(payloadBytes);
            }
            catch (JsonException ex)
            {
                return new DecodeResult(CodecStatus.DeserializationFailed, null, sourceVersion, detail: ex.Message);
            }
            if (payloadNode == null)
                return new DecodeResult(CodecStatus.DeserializationFailed, null, sourceVersion, detail: "Payload parsed to empty node.");

            var appliedMigrations = (IReadOnlyList<string>)Array.Empty<string>();
            if (!_migrations.TryMigrate(sourceVersion, CurrentSchemaVersion, ref payloadNode, out var applied, out var migrationError))
                return new DecodeResult(CodecStatus.MigrationFailed, null, sourceVersion, appliedMigrations, migrationError);
            appliedMigrations = applied;

            GameState? state;
            try
            {
                state = payloadNode.Deserialize<GameState>(SaveJson.Options);
            }
            catch (JsonException ex)
            {
                return new DecodeResult(CodecStatus.DeserializationFailed, null, sourceVersion, appliedMigrations, ex.Message);
            }

            if (state == null)
                return new DecodeResult(CodecStatus.DeserializationFailed, null, sourceVersion, appliedMigrations, "Payload deserialized to null.");

            // Trust the migrated/current schema marker over any stale in-payload value.
            state.SchemaVersion = CurrentSchemaVersion;
            return new DecodeResult(CodecStatus.Ok, state, sourceVersion, appliedMigrations);
        }

        private static byte[] Sha256(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(bytes);
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
                return false;
            int diff = 0;
            for (int i = 0; i < left.Length; i++)
                diff |= left[i] ^ right[i];
            return diff == 0;
        }
    }
}
