using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;
using WalkGame.Domain.Activity;

namespace WalkGame.Infrastructure.Persistence
{
    /// <summary>
    /// Schema v2 → v3 (D-103): seeds the days-walked aggregate. A v2 save has no local
    /// date per record and retired rows keep no date at all (D-102), so the exact count
    /// is unrecoverable. The migration stores a PROVEN LOWER BOUND instead: the minimum
    /// number of local dates that can explain the retained credited rows' end instants,
    /// found by a greedy cover with the widest UTC span one local date can have
    /// (<see cref="LocalDayCalendar.MaxLocalDateUtcSpan"/>). Any save with credited or
    /// retired history is flagged so the player sees "at least N".
    /// Pure node transform: deterministic, scene-free.
    /// </summary>
    public sealed class MigrationV2ToV3 : ISaveMigration
    {
        public const string Id = "m2-to-v3-days-walked-lower-bound";

        public int FromVersion => 2;

        public int ToVersion => 3;

        public string MigrationId => Id;

        public JsonNode Migrate(JsonNode payload)
        {
            if (payload == null)
                throw new JsonNodeNullPayloadException();

            payload["schemaVersion"] = 3;

            var ends = new List<DateTimeOffset>();
            bool anyHistory = false;
            var processed = payload["processedRecords"] as JsonObject;
            if (processed != null)
            {
                if (processed["entries"] is JsonObject entries)
                {
                    foreach (var pair in entries)
                    {
                        var row = pair.Value;
                        if (row == null) continue;
                        long steps = TryLong(row["eligibleSteps"]);
                        if (steps <= 0L) continue;
                        anyHistory = true;
                        string? end = TryString(row["recordEndUtc"]);
                        if (end != null && DateTimeOffset.TryParse(end, CultureInfo.InvariantCulture,
                                DateTimeStyles.RoundtripKind, out var parsed))
                            ends.Add(parsed.ToUniversalTime());
                    }
                }
                if (TryLong(processed["retiredCount"]) > 0L)
                    anyHistory = true;
            }

            ends.Sort();
            int lowerBound = 0;
            DateTimeOffset groupStart = default;
            foreach (var t in ends)
            {
                if (lowerBound == 0 || t - groupStart >= LocalDayCalendar.MaxLocalDateUtcSpan)
                {
                    lowerBound++;
                    groupStart = t;
                }
            }

            if (processed == null)
            {
                processed = new JsonObject();
                payload["processedRecords"] = processed;
            }
            processed["walkedLocalDays"] = new JsonArray();
            processed["legacyDaysLowerBound"] = lowerBound;
            processed["legacyLastUtcDay"] = ends.Count > 0 ? (JsonNode)LocalDayCalendar.UtcDayOf(ends[ends.Count - 1]) : null;
            processed["isLegacyLowerBound"] = anyHistory;
            processed["hasUndatedCredits"] = false;
            processed["retiredThroughLocalDay"] = null;
            return payload;
        }

        private static long TryLong(JsonNode? node)
        {
            if (node is JsonValue v && v.TryGetValue<long>(out var l)) return l;
            return 0L;
        }

        private static string? TryString(JsonNode? node)
        {
            if (node is JsonValue v && v.TryGetValue<string>(out var s)) return s;
            return null;
        }
    }
}
