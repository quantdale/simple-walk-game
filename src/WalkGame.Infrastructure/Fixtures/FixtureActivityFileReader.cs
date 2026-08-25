using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WalkGame.Domain.Activity;

namespace WalkGame.Infrastructure.Fixtures
{
    /// <summary>
    /// Fixture activity provider adapter: reads a JSON array of normalized walking
    /// records from disk. Development/diagnostics provenance only — its output feeds
    /// exactly the same ingestion pipeline future Health Connect/HealthKit adapters
    /// will use, so fixtures prove the trust path rather than bypassing it.
    /// Structural problems in the file throw; semantic problems (zero quantity,
    /// malformed timestamps, ...) are deliberately left to pipeline validation.
    /// </summary>
    public static class FixtureActivityFileReader
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static List<NormalizedActivityRecord> LoadBatch(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Fixture batch path must be provided.", nameof(path));

            return ParseBatch(File.ReadAllText(path));
        }

        public static List<NormalizedActivityRecord> ParseBatch(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));

            List<FixtureRecordDto>? dtos;
            try
            {
                dtos = JsonSerializer.Deserialize<List<FixtureRecordDto>>(json, Options);
            }
            catch (JsonException ex)
            {
                throw new FormatException("Fixture batch is not a valid JSON record array: " + ex.Message, ex);
            }

            if (dtos == null)
                throw new FormatException("Fixture batch must be a JSON array of activity records.");

            var records = new List<NormalizedActivityRecord>(dtos.Count);
            foreach (var dto in dtos)
            {
                if (dto == null)
                    throw new FormatException("Fixture batch contains a null record.");
                if (dto.Quantity is null)
                    throw new FormatException("Fixture record is missing an integer 'quantity'.");

                var category = ParseCategory(dto.Category);
                records.Add(new NormalizedActivityRecord(
                    dto.ProviderNamespace ?? "fixture",
                    string.IsNullOrWhiteSpace(dto.SourceRecordId) ? null : dto.SourceRecordId!.Trim(),
                    category,
                    string.IsNullOrWhiteSpace(dto.Unit) ? ActivityUnits.Steps : dto.Unit!.Trim(),
                    dto.Quantity.Value,
                    ParseTimestamp(dto.StartUtc, "startUtc"),
                    ParseTimestamp(dto.EndUtc, "endUtc"),
                    dto.Revision ?? 1,
                    dto.IsDeletion));
            }

            return records;
        }

        private static ActivityCategory ParseCategory(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return ActivityCategory.Walking;
            if (Enum.TryParse<ActivityCategory>(value.Trim(), ignoreCase: true, out var parsed))
                return parsed;
            throw new FormatException($"Unknown fixture activity category '{value}'.");
        }

        private static DateTimeOffset ParseTimestamp(string? value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new FormatException($"Fixture record is missing '{field}'.");
            if (!DateTimeOffset.TryParse(
                    value, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsed))
                throw new FormatException($"Fixture field '{field}' is not an ISO-8601 timestamp: '{value}'.");
            return parsed;
        }

        private sealed class FixtureRecordDto
        {
            [JsonPropertyName("providerNamespace")]
            public string? ProviderNamespace { get; set; }

            [JsonPropertyName("sourceRecordId")]
            public string? SourceRecordId { get; set; }

            [JsonPropertyName("category")]
            public string? Category { get; set; }

            [JsonPropertyName("unit")]
            public string? Unit { get; set; }

            [JsonPropertyName("quantity")]
            public long? Quantity { get; set; }

            [JsonPropertyName("startUtc")]
            public string? StartUtc { get; set; }

            [JsonPropertyName("endUtc")]
            public string? EndUtc { get; set; }

            [JsonPropertyName("revision")]
            public int? Revision { get; set; }

            [JsonPropertyName("isDeletion")]
            public bool IsDeletion { get; set; }
        }
    }
}
