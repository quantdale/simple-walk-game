using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using WalkGame.Application.Content;
using WalkGame.Application.Persistence;
using WalkGame.Domain;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Validation;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Infrastructure.Tests;

/// <summary>
/// M4 persistence evidence (D-036): the discovery/expedition/progression/completion
/// fields are additive under save schema v2. A payload written before M4 — i.e. without
/// any of the new JSON properties — must decode with exactly "nothing unlocked yet"
/// semantics, pass canonical validation, and re-encode stably. No migration required.
/// </summary>
public class M4BackwardDecodingTests : IDisposable
{
    private readonly Infrastructure.Tests.TestSupport.TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void PreM4V2Payload_DecodesWithDefaultSemantics_AndValidates()
    {
        var content = Region1Catalog.Create();
        var codec = new SaveCodec(new MigrationRunner(DefaultMigrations.All));

        // Fresh v2 game, then strip every M4-added property to simulate an older writer.
        byte[] envelope = codec.Encode(GameFactory.NewGame(content, T0, seed: 5UL), T0);
        byte[] strippedEnvelope = StripM4RegionProperties(envelope);

        var decoded = codec.Decode(strippedEnvelope);

        Assert.Equal(CodecStatus.Ok, decoded.Status);
        var state = decoded.State!;
        Assert.Equal(2, decoded.SourceSchemaVersion);
        Assert.Empty(state.Region.Discoveries);
        Assert.Empty(state.Region.Expeditions);
        Assert.Equal(0, state.Region.EcologyStage);
        Assert.Equal(0, state.Region.SettlementStage);
        Assert.False(state.Region.IsCompleted);
        Assert.Null(state.Region.RegionCompletedAtUtc);
        Assert.Empty(GameStateValidator.Validate(state, content));

        // Re-encode stability: decode → encode → decode yields identical semantics.
        byte[] reencoded = codec.Encode(state, T0.AddDays(1));
        var second = codec.Decode(reencoded);
        Assert.Equal(CodecStatus.Ok, second.Status);
        Assert.False(second.State!.Region.IsCompleted);
        Assert.Empty(second.State.Region.Discoveries);
    }

    [Fact]
    public void PreM4Save_ContinuesIntoTheFullGraph_WithoutGrantingOrDestroyingProgress()
    {
        var content = Region1Catalog.Create();
        var codec = new SaveCodec(new MigrationRunner(DefaultMigrations.All));

        // An older save that had completed the seed's entry project already.
        var older = GameFactory.NewGame(content, T0, seed: 5UL);
        var entry = older.Region.FindProject("proj.clear-trailhead")!;
        entry.Status = ProjectStatus.Completed;
        entry.CompletedAtUtc = T0.AddDays(1);
        entry.VitalityInvested = 300L;
        byte[] stripped = StripM4RegionProperties(codec.Encode(older, T0));

        var decoded = codec.Decode(stripped);
        Assert.Equal(CodecStatus.Ok, decoded.Status);
        var state = decoded.State!;

        // Earned progression is preserved verbatim; nothing M4-related is granted.
        Assert.Equal(ProjectStatus.Completed, state.Region.FindProject("proj.clear-trailhead")!.Status);
        Assert.True(state.Region.Discoveries.ContainsKey("disc.old-millstone") == false,
            "a pre-M4 save must not gain discoveries it did not earn");
        Assert.False(state.Region.IsCompleted);

        // The state remains validator-clean against the full authored graph.
        Assert.Empty(GameStateValidator.Validate(state, content));
    }

    /// <summary>Removes all M4-added properties from the region object of an envelope.</summary>
    private static byte[] StripM4RegionProperties(byte[] envelope)
    {
        JsonObject? root = JsonNode.Parse(envelope) as JsonObject;
        Assert.NotNull(root);
        byte[] payload = Convert.FromBase64String(root!["payloadBase64"]!.GetValue<string>());
        var payloadNode = Assert.IsType<JsonObject>(JsonNode.Parse(payload));
        var region = Assert.IsType<JsonObject>(payloadNode["region"]);

        region.Remove("discoveries");
        region.Remove("expeditions");
        region.Remove("ecologyStage");
        region.Remove("settlementStage");
        region.Remove("isCompleted");
        region.Remove("regionCompletedAtUtc");

        string strippedPayload = payloadNode.ToJsonString(SaveJsonOptionsForRoundtrip());
        root["payloadBase64"] = JsonValue.Create(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(strippedPayload)));
        using var sha = System.Security.Cryptography.SHA256.Create();
        root["payloadSha256Base64"] = JsonValue.Create(Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(strippedPayload))));

        return System.Text.Encoding.UTF8.GetBytes(root.ToJsonString());
    }

    private static JsonSerializerOptions SaveJsonOptionsForRoundtrip()
    {
        // Match the production writer shape closely enough for this test: camel case.
        return new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };
    }

    private static readonly DateTimeOffset T0 =
        new DateTimeOffset(2026, 3, 10, 8, 0, 0, TimeSpan.Zero);
}
