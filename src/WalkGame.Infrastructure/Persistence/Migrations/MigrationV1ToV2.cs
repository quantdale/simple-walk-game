using System.Text.Json.Nodes;

namespace WalkGame.Infrastructure.Persistence
{
    /// <summary>
    /// Schema v1 → v2 (M3): producer runtime rows gain a bounded pending-output store.
    /// v1 carried only a sub-unit remainder in <c>carryMilliUnits</c> (always 0–999);
    /// v2 replaces it with <c>storedMilliUnits</c>. The old carry value is promoted
    /// unchanged — identical value semantics for the fractional remainder a v1 save
    /// could hold — and the obsolete node is removed so dumps stay clean.
    /// Pure node transform: deterministic, scene-free, idempotent on its inputs.
    /// </summary>
    public sealed class MigrationV1ToV2 : ISaveMigration
    {
        public const string Id = "m1-to-v2-producer-stored-milli-units";

        public int FromVersion => 1;

        public int ToVersion => 2;

        public string MigrationId => Id;

        public JsonNode Migrate(JsonNode payload)
        {
            if (payload == null)
                throw new JsonNodeNullPayloadException();

            payload["schemaVersion"] = 2;

            if (payload["region"]?["producers"] is JsonArray producers)
            {
                foreach (var producerNode in producers)
                {
                    if (producerNode == null)
                        continue;

                    long carryMilliUnits = producerNode["carryMilliUnits"]?.GetValue<long>() ?? 0L;
                    if (carryMilliUnits < 0L)
                        carryMilliUnits = 0L;
                    producerNode["storedMilliUnits"] = carryMilliUnits;
                    if (producerNode is JsonObject producerObject)
                        producerObject.Remove("carryMilliUnits");
                }
            }

            return payload;
        }
    }

    /// <summary>Registered migrations for this build, in chain order.</summary>
    public static class DefaultMigrations
    {
        public static ISaveMigration[] All { get; } =
        {
            new MigrationV1ToV2(),
        };
    }

    internal sealed class JsonNodeNullPayloadException : System.Exception
    {
        public JsonNodeNullPayloadException()
            : base("Migration payload was null.")
        {
        }
    }
}
