using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WalkGame.Application.Persistence;
using WalkGame.Domain;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Infrastructure.Tests;

/// <summary>
/// Schema v1 → v2 migration evidence: a representative v1 producer payload decodes
/// through the registered chain with its sub-unit carry promoted into the bounded
/// pending-output store, and re-encoding lands cleanly on the current schema.
/// </summary>
public sealed class MigrationV1ToV2Tests
{
    private static readonly DateTimeOffset T0 =
        new DateTimeOffset(2026, 2, 3, 4, 0, 0, TimeSpan.Zero);

    private static SaveCodec CodecWithRealChain() =>
        new(new MigrationRunner(DefaultMigrations.All));

    [Fact]
    public void RegisteredChain_IsContiguous_FromMinimumSupportedToCurrent()
    {
        var runner = new MigrationRunner(DefaultMigrations.All);

        JsonNode payload = new JsonObject();
        string error;
        IReadOnlyList<string> applied;
        bool ok = runner.TryMigrate(SchemaVersions.MinimumSupported, SchemaVersions.Current, ref payload, out applied, out error);

        Assert.True(ok, error);
        Assert.Equal(new[] { MigrationV1ToV2.Id }, applied);
    }

    [Fact]
    public void Decode_V1ProducerPayload_PromotesCarryIntoStoredMilliUnits()
    {
        var codec = CodecWithRealChain();

        var result = codec.Decode(V1Envelope(carryMilliUnits: 750L));

        Assert.Equal(CodecStatus.Ok, result.Status);
        Assert.Equal(1, result.SourceSchemaVersion);
        Assert.Equal(new[] { MigrationV1ToV2.Id }, result.AppliedMigrations);

        var state = result.State!;
        Assert.Equal(SchemaVersions.Current, state.SchemaVersion);
        var producer = Assert.Single(state.Region.Producers);
        Assert.Equal("prd.workshop-salvage", producer.ProducerId);
        Assert.Equal(750L, producer.StoredMilliUnits);
        Assert.Equal(123L, producer.TotalProducedMilliUnits);
    }

    [Fact]
    public void Decode_V1WithoutProducers_YieldsEmptyStoreDefaults()
    {
        var codec = CodecWithRealChain();

        var result = codec.Decode(V1Envelope(null));

        Assert.Equal(CodecStatus.Ok, result.Status);
        Assert.Equal(new[] { MigrationV1ToV2.Id }, result.AppliedMigrations);
    }

    [Fact]
    public void V1Decode_Reencode_Redecode_IsStableAtCurrentSchema()
    {
        var codec = CodecWithRealChain();
        var migratedOnce = codec.Decode(V1Envelope(750L));
        Assert.Equal(CodecStatus.Ok, migratedOnce.Status);

        var reencoded = codec.Encode(migratedOnce.State!, T0.AddHours(1));
        var migratedTwice = codec.Decode(reencoded);

        Assert.Equal(CodecStatus.Ok, migratedTwice.Status);
        Assert.Empty(migratedTwice.AppliedMigrations);
        var producer = Assert.Single(migratedTwice.State!.Region.Producers);
        Assert.Equal(750L, producer.StoredMilliUnits);
        Assert.Equal(
            migratedOnce.State!.Region.Producers.Single().LastTickUtc,
            producer.LastTickUtc);
    }

    /// <summary>Builds a hand-crafted but faithful v1 envelope with a valid checksum.</summary>
    private static byte[] V1Envelope(long? carryMilliUnits)
    {
        var producers = new JsonArray();
        if (carryMilliUnits != null)
        {
            producers.Add(new JsonObject
            {
                ["producerId"] = "prd.workshop-salvage",
                ["unlocked"] = false,
                ["carryMilliUnits"] = carryMilliUnits.Value,
                ["totalProducedMilliUnits"] = 123L,
                ["lastTickUtc"] = T0.ToString("O"),
            });
        }

        var payload = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["createdAtUtc"] = T0.ToString("O"),
            ["lastAdvancedUtc"] = T0.ToString("O"),
            ["resources"] = new JsonObject
            {
                ["amounts"] = new JsonObject { ["vitality"] = 42L },
                ["caps"] = new JsonObject(),
            },
            ["ledger"] = new JsonObject { ["records"] = new JsonArray() },
            ["processedRecords"] = new JsonObject
            {
                ["entries"] = new JsonObject(),
                ["unappliedReversalVitality"] = 0L,
            },
            ["ingestionCheckpointUtc"] = T0.ToString("O"),
            ["region"] = new JsonObject
            {
                ["regionId"] = "region.millbrook-valley",
                ["projects"] = new JsonObject(),
                ["landmarkStages"] = new JsonObject(),
                ["producers"] = producers,
            },
            ["queue"] = new JsonObject
            {
                ["queuedProjectIds"] = new JsonArray(),
                ["activeProjectId"] = null,
                ["autoAdvance"] = true,
            },
            ["rng"] = new JsonObject
            {
                ["S0"] = 1L, ["S1"] = 2L, ["S2"] = 3L, ["S3"] = 4L,
            },
        };

        byte[] payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        var envelope = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["savedAtUtc"] = T0.ToString("O"),
            ["payloadSha256Base64"] = Convert.ToBase64String(SHA256.HashData(payloadBytes)),
            ["payloadBase64"] = Convert.ToBase64String(payloadBytes),
        };
        return JsonSerializer.SerializeToUtf8Bytes(envelope);
    }
}
