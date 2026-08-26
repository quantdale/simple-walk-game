using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using WalkGame.Application.Persistence;

namespace WalkGame.Application.Tests.TestSupport
{
    /// <summary>
    /// Test-support surgery on save envelopes: opens the opaque payload frame for
    /// mutation and re-wraps mutated payloads with a CORRECT integrity checksum so a
    /// test's intended semantic damage (not an accidental checksum failure) is what
    /// reaches the decode pipeline.
    /// </summary>
    internal static class PayloadSurgery
    {
        public static JsonNode OpenPayload(byte[] envelopeBytes)
        {
            var envelope = JsonSerializer.Deserialize<JsonNode>(envelopeBytes, SaveJsonProbeOptions)!;
            byte[] payload = Convert.FromBase64String(envelope["payloadBase64"]!.GetValue<string>());
            var node = JsonNode.Parse(payload);
            AssertThat(node != null, "payload parsed to null");
            return node!;
        }

        public static byte[] WrapPayload(JsonNode payload, int schemaVersion, DateTimeOffset savedAtUtc)
        {
            byte[] payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, SaveJsonProbeOptions);

            var envelope = new JsonObject
            {
                ["schemaVersion"] = schemaVersion,
                ["savedAtUtc"] = savedAtUtc,
                ["payloadSha256Base64"] = Convert.ToBase64String(Sha256(payloadBytes)),
                ["payloadBase64"] = Convert.ToBase64String(payloadBytes),
            };
            return JsonSerializer.SerializeToUtf8Bytes(envelope, SaveJsonProbeOptions);
        }

        private static byte[] Sha256(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(bytes);
        }

        private static void AssertThat(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static readonly JsonSerializerOptions SaveJsonProbeOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }
}
