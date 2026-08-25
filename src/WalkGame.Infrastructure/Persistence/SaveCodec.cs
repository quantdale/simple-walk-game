using System;
using System.Buffers;
using System.Buffers.Text;
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
    /// envelope { schemaVersion, savedAtUtc, payloadSha256Base64, payload(base64) }.
    /// Decode path: frame → checksum → version gates → sequential migration → deserialize.
    /// The frame is streamed with UTF-8 JSON primitives so large payloads never round-trip
    /// through managed base64 strings; current-version payloads deserialize directly,
    /// skipping the JsonNode detour that exists solely for migrations.
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

            // Stream the envelope straight into UTF-8 bytes: the payload goes through
            // WriteBase64String once instead of materializing a base64 .NET string.
            var buffer = new ArrayBufferWriter<byte>(payloadBytes.Length + (payloadBytes.Length / 4) + 256);
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WriteNumber("schemaVersion", state.SchemaVersion);
                writer.WriteString("savedAtUtc", savedAtUtc);
                writer.WriteString("payloadSha256Base64", Convert.ToBase64String(Sha256(payloadBytes)));
                writer.WriteBase64String("payloadBase64", payloadBytes);
                writer.WriteEndObject();
            }

            return buffer.WrittenSpan.ToArray();
        }

        public DecodeResult Decode(byte[] envelopeBytes)
        {
            if (envelopeBytes == null || envelopeBytes.Length == 0)
                return new DecodeResult(CodecStatus.MalformedEnvelope, null, 0, detail: "Empty save payload.");

            int schemaVersion = 0;
            byte[] expectedChecksum = Array.Empty<byte>();
            byte[] payloadBytes = Array.Empty<byte>();
            try
            {
                (schemaVersion, expectedChecksum, payloadBytes) = ReadEnvelope(envelopeBytes);
            }
            catch (JsonException ex)
            {
                return new DecodeResult(CodecStatus.MalformedEnvelope, null, 0, detail: ex.Message);
            }
            catch (FormatException ex)
            {
                return new DecodeResult(CodecStatus.MalformedEnvelope, null, schemaVersion, detail: ex.Message);
            }

            if (payloadBytes.Length == 0)
                return new DecodeResult(CodecStatus.MalformedEnvelope, null, 0, detail: "Envelope is missing a payload.");

            var actualChecksum = Sha256(payloadBytes);
            if (!FixedTimeEquals(expectedChecksum, actualChecksum))
                return new DecodeResult(CodecStatus.ChecksumMismatch, null, schemaVersion,
                    detail: "Payload does not match its recorded integrity hash.");

            int sourceVersion = schemaVersion;
            if (sourceVersion > CurrentSchemaVersion)
                return new DecodeResult(CodecStatus.VersionTooNew, null, sourceVersion);
            if (sourceVersion < MinimumSupportedVersion)
                return new DecodeResult(CodecStatus.VersionTooOld, null, sourceVersion);

            var appliedMigrations = (IReadOnlyList<string>)Array.Empty<string>();
            GameState? state;
            try
            {
                if (sourceVersion == CurrentSchemaVersion)
                {
                    // Common case: the payload already carries the current schema, so the
                    // JsonNode detour (parse DOM → migrate → re-parse) is pure overhead.
                    state = JsonSerializer.Deserialize<GameState>(payloadBytes, SaveJson.Options);
                }
                else
                {
                    JsonNode? payloadNode = JsonNode.Parse(payloadBytes);
                    if (payloadNode == null)
                        return new DecodeResult(CodecStatus.DeserializationFailed, null, sourceVersion, detail: "Payload parsed to empty node.");

                    if (!_migrations.TryMigrate(sourceVersion, CurrentSchemaVersion, ref payloadNode, out var applied, out var migrationError))
                        return new DecodeResult(CodecStatus.MigrationFailed, null, sourceVersion, applied, migrationError);
                    appliedMigrations = applied;

                    state = payloadNode.Deserialize<GameState>(SaveJson.Options);
                }
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

        /// <summary>
        /// Streams the envelope frame without materializing the payload base64 as a
        /// managed string. Field names must stay exactly camelCase — the previous STJ
        /// binding was case-sensitive, and malformed-frame outcomes are preserved.
        /// </summary>
        private static (int SchemaVersion, byte[] Checksum, byte[] Payload) ReadEnvelope(byte[] envelopeBytes)
        {
            var reader = new Utf8JsonReader(envelopeBytes, new JsonReaderOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Envelope is not a JSON object.");

            int schemaVersion = 0;
            byte[] checksum = Array.Empty<byte>();
            byte[] payload = Array.Empty<byte>();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;
                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException("Unexpected token in save envelope.");

                string property = reader.GetString()!;
                if (!reader.Read())
                    throw new JsonException("Envelope property is missing a value.");

                switch (property)
                {
                    case "schemaVersion":
                        schemaVersion = reader.GetInt32();
                        break;

                    case "savedAtUtc":
                        _ = reader.GetDateTimeOffset();
                        break;

                    case "payloadSha256Base64":
                        checksum = ReadBase64Value(ref reader);
                        break;

                    case "payloadBase64":
                        payload = ReadBase64Value(ref reader);
                        break;

                    default:
                        reader.Skip();
                        break;
                }
            }

            return (schemaVersion, checksum, payload);
        }

        /// <summary>Decodes base64 directly from the reader's UTF-8 span; falls back to the
        /// whitespace-tolerant string conversion for anything exotic, preserving the
        /// FormatException contract of the previous implementation.</summary>
        private static byte[] ReadBase64Value(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException("Expected a base64 string value.");

            if (reader.HasValueSequence)
                return Convert.FromBase64String(reader.GetString()!);

            ReadOnlySpan<byte> raw = reader.ValueSpan;
            if (raw.IsEmpty)
                return Array.Empty<byte>();

            var decoded = new byte[Base64.GetMaxDecodedFromUtf8Length(raw.Length)];
            OperationStatus status = Base64.DecodeFromUtf8(raw, decoded, out _, out int written);
            if (status != OperationStatus.Done)
                return Convert.FromBase64String(reader.GetString()!);

            if (written != decoded.Length)
                Array.Resize(ref decoded, written);
            return decoded;
        }

        private static byte[] Sha256(byte[] bytes)
        {
#if NET5_0_OR_GREATER
            return SHA256.HashData(bytes);
#else
            using var sha = SHA256.Create();
            return sha.ComputeHash(bytes);
#endif
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
