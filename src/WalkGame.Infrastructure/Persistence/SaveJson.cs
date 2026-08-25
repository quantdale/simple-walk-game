using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using WalkGame.Domain.Common;

namespace WalkGame.Infrastructure.Persistence
{
    internal sealed class SaveEnvelopeDto
    {
        public int SchemaVersion { get; set; }

        public DateTimeOffset SavedAtUtc { get; set; }

        public string PayloadSha256Base64 { get; set; } = string.Empty;

        /// <summary>Payload is opaque base64 so envelope framing stays stable across payload schema changes.</summary>
        public string PayloadBase64 { get; set; } = string.Empty;
    }

    internal static class SaveJson
    {
        public static JsonSerializerOptions Options { get; } = CreateOptions();

        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                IncludeFields = true,
            };
            options.Converters.Add(new IdConverterFactory());
            options.Converters.Add(new JsonStringEnumConverter(namingPolicy: JsonNamingPolicy.CamelCase));
            options.TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { PopulateReadOnlyCollections },
            };
            return options;
        }

        /// <summary>
        /// Canonical state exposes read-only collection properties (invariant enforcement).
        /// Without this modifier STJ would replace-and-drop their contents on load —
        /// silent total progress loss. Populate keeps deserialization additive.
        /// </summary>
        private static void PopulateReadOnlyCollections(JsonTypeInfo type)
        {
            foreach (var property in type.Properties)
            {
                var propertyType = property.PropertyType;
                bool isCollection = propertyType.IsGenericType &&
                    (propertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>) ||
                     propertyType.GetGenericTypeDefinition() == typeof(List<>));
                if (isCollection)
                    property.ObjectCreationHandling = JsonObjectCreationHandling.Populate;
            }
        }
    }

    /// <summary>
    /// Serializes strongly-typed stable IDs (<c>Id&lt;TKind&gt;</c>) as plain strings so
    /// save data stores IDs, never type machinery.
    /// </summary>
    internal sealed class IdConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) =>
            typeToConvert.IsGenericType &&
            typeToConvert.GetGenericTypeDefinition() == typeof(Id<>);

        public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var kindType = typeToConvert.GetGenericArguments()[0];
            var converterType = typeof(IdConverter<>).MakeGenericType(kindType);
            return (JsonConverter?)Activator.CreateInstance(converterType);
        }
    }

    internal sealed class IdConverter<TKind> : JsonConverter<Id<TKind>>
        where TKind : class
    {
        public override Id<TKind> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException($"Expected string for {typeof(Id<TKind>).Name}.");
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
                throw new JsonException("Stable ID values cannot be empty.");
            return new Id<TKind>(value);
        }

        public override void Write(Utf8JsonWriter writer, Id<TKind> value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value ?? string.Empty);
    }
}
