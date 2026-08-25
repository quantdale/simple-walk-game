using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using WalkGame.Application.Content;
using WalkGame.Application.Persistence;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using Xunit;

namespace WalkGame.Application.Tests;

public sealed class CodecMigrationIntegrationTests
{
    private static readonly DateTimeOffset T0 = TestSessions.T0;

    [Fact]
    public void PlainDecode_ReportsOkWithNoAppliedMigrations()
    {
        var codec = TestSessions.NewCodec();
        var state = GameFactory.NewGame(Region1Catalog.Create(), T0, seed: 42UL);

        var encoded = codec.Encode(state, T0);
        var result = codec.Decode(encoded);

        Assert.Equal(CodecStatus.Ok, result.Status);
        Assert.Equal(SchemaVersions.Current, result.SourceSchemaVersion);
        Assert.NotNull(result.State);
        Assert.Empty(result.AppliedMigrations);
        Assert.Equal(SchemaVersions.Current, result.State!.SchemaVersion);
    }

    [Fact]
    public void Decode_LegacyShapedPayload_FailsGracefullyWithoutMigrations()
    {
        var codec = TestSessions.NewCodec();
        var state = GameFactory.NewGame(Region1Catalog.Create(), T0, seed: 42UL);
        var envelope = JsonNode.Parse(codec.Encode(state, T0))!.AsObject();

        var payloadBytes = Convert.FromBase64String(envelope["payloadBase64"]!.GetValue<string>());
        var legacy = JsonNode.Parse(payloadBytes)!.AsObject();
        legacy["created_at_utc"] = legacy["createdAtUtc"]!.DeepClone();
        legacy.Remove("createdAtUtc");
        legacy["last_advanced_utc"] = legacy["lastAdvancedUtc"]!.DeepClone();
        legacy.Remove("lastAdvancedUtc");
        legacy["rng"] = JsonValue.Create("legacy-xoshiro-v0-blob");

        var legacyBytes = JsonSerializer.SerializeToUtf8Bytes(legacy);
        envelope["payloadBase64"] = Convert.ToBase64String(legacyBytes);
        envelope["payloadSha256Base64"] = Convert.ToBase64String(SHA256.HashData(legacyBytes));

        var result = codec.Decode(JsonSerializer.SerializeToUtf8Bytes(envelope));

        Assert.Equal(CodecStatus.DeserializationFailed, result.Status);
        Assert.Null(result.State);
        Assert.Empty(result.AppliedMigrations);
        Assert.False(string.IsNullOrEmpty(result.Detail));
    }
}
