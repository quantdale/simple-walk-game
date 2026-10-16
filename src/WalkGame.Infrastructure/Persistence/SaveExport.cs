using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WalkGame.Infrastructure.Persistence
{
    public enum SaveExportStatus { Exported = 0, Malformed = 1, ChecksumMismatch = 2 }

    public sealed class SaveExportResult
    {
        public SaveExportResult(SaveExportStatus status, string? json) { Status = status; Json = json; }
        public SaveExportStatus Status { get; }
        /// <summary>The readable export document; null unless <see cref="Status"/> is Exported.</summary>
        public string? Json { get; }
    }

    /// <summary>
    /// D-111: turns a save envelope (the exact bytes <see cref="SaveCodec.Encode"/> writes) into a
    /// readable export document for the player. The payload checksum is verified first; hostile or
    /// damaged bytes give a status, never an exception. Local only: no I/O, no network.
    /// </summary>
    public static class SaveExport
    {
        public const string Format = "simple-walk-game-export";
        public const int FormatVersion = 1;

        public static SaveExportResult ToReadableJson(byte[]? envelopeBytes)
        {
            if (envelopeBytes == null || envelopeBytes.Length == 0) return Fail(SaveExportStatus.Malformed);
            SaveEnvelopeDto? env;
            try { env = JsonSerializer.Deserialize<SaveEnvelopeDto>(envelopeBytes, SaveJson.Options); }
            catch (Exception ex) when (ex is JsonException || ex is NotSupportedException || ex is InvalidOperationException) { return Fail(SaveExportStatus.Malformed); }
            if (env == null || string.IsNullOrEmpty(env.PayloadBase64)) return Fail(SaveExportStatus.Malformed);

            byte[] payload, expected;
            try { payload = Convert.FromBase64String(env.PayloadBase64); expected = Convert.FromBase64String(env.PayloadSha256Base64 ?? ""); }
            catch (FormatException) { return Fail(SaveExportStatus.Malformed); }
            byte[] actual;
            using (var sha = SHA256.Create()) actual = sha.ComputeHash(payload);
            if (!FixedTimeEquals(actual, expected)) return Fail(SaveExportStatus.ChecksumMismatch);

            JsonNode? state;
            try { state = JsonNode.Parse(payload); }
            catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException) { return Fail(SaveExportStatus.Malformed); }
            if (!(state is JsonObject)) return Fail(SaveExportStatus.Malformed);

            var doc = new JsonObject
            {
                ["format"] = Format,
                ["formatVersion"] = FormatVersion,
                ["schemaVersion"] = env.SchemaVersion,
                ["savedAtUtc"] = env.SavedAtUtc.ToUniversalTime().ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                ["payloadSha256"] = env.PayloadSha256Base64,
                ["state"] = state,
            };
            return new SaveExportResult(SaveExportStatus.Exported, doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        private static SaveExportResult Fail(SaveExportStatus s) => new SaveExportResult(s, null);

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
