using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WalkGame.Application.Persistence;
using WalkGame.Domain;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Infrastructure.Tests;

/// <summary>
/// D-142: adverse save-migration evidence. Hostile or damaged old saves must fail CLOSED through
/// the codec (a non-Ok status, never an exception, never a half-migrated payload), and the runner
/// must hand back the caller's original payload on any failure, including a mid-chain one.
/// </summary>
public sealed class SaveMigrationAdverseTests
{
    private static readonly DateTimeOffset T0 = new(2026, 2, 3, 4, 0, 0, TimeSpan.Zero);
    private static SaveCodec Codec() => new(new MigrationRunner(DefaultMigrations.All));

    private sealed class Step : ISaveMigration
    {
        private readonly Func<JsonNode, JsonNode?> _f;
        public Step(int from, int to, string id, Func<JsonNode, JsonNode?> f) { FromVersion = from; ToVersion = to; MigrationId = id; _f = f; }
        public int FromVersion { get; }
        public int ToVersion { get; }
        public string MigrationId { get; }
        public JsonNode Migrate(JsonNode payload) => _f(payload)!;
    }

    private static byte[] Envelope(int version, string payloadJson)
    {
        byte[] p = Encoding.UTF8.GetBytes(payloadJson);
        return JsonSerializer.SerializeToUtf8Bytes(new JsonObject
        {
            ["schemaVersion"] = version,
            ["savedAtUtc"] = T0.ToString("O"),
            ["payloadSha256Base64"] = Convert.ToBase64String(SHA256.HashData(p)),
            ["payloadBase64"] = Convert.ToBase64String(p),
        });
    }

    public static TheoryData<string, int, string> Hostile => new()
    {
        { "root is an array", 1, "[1,2,3]" },
        { "root is a string", 1, "\"save\"" },
        { "carry is text", 1, """{"schemaVersion":1,"region":{"producers":[{"producerId":"p","carryMilliUnits":"lots"}]}}""" },
        { "carry is an object", 1, """{"schemaVersion":1,"region":{"producers":[{"producerId":"p","carryMilliUnits":{"x":1}}]}}""" },
        { "producers is an object of junk", 1, """{"schemaVersion":1,"region":{"producers":[7, "x", true]}}""" },
        { "schema older than supported", 0, """{"schemaVersion":0}""" },
        { "negative schema", -4, """{"schemaVersion":-4}""" },
        { "schema newer than build", SchemaVersions.Current + 1, """{"schemaVersion":99}""" },
        { "empty object migrates but cannot validate", 1, "{}" },
    };

    [Theory]
    [MemberData(nameof(Hostile))]
    public void HostileOldSave_FailsClosed_NeverThrows(string why, int version, string payload)
    {
        DecodeResult? r = null;
        var ex = Record.Exception(() => r = Codec().Decode(Envelope(version, payload)));
        Assert.True(ex == null, why + ": " + ex);
        if (r!.Status == CodecStatus.Ok)
        {
            // Only a payload that is genuinely decodable may pass; it then must be a full, current state.
            Assert.NotNull(r.State);
            Assert.Equal(SchemaVersions.Current, r.State!.SchemaVersion);
        }
        else
        {
            Assert.Null(r.State);
        }
    }

    [Fact]
    public void NewerSchema_IsReportedAsMigrationFailure_NotDowngraded()
    {
        var r = Codec().Decode(Envelope(SchemaVersions.Current + 1, "{}"));
        Assert.NotEqual(CodecStatus.Ok, r.Status);
        Assert.Null(r.State);
    }

    [Fact]
    public void MidChainFailure_LeavesTheCallerPayloadUntouched()
    {
        var runner = new MigrationRunner(new ISaveMigration[]
        {
            new Step(1, 2, "ok", n => { n["a"] = 2; return n; }),
            new Step(2, 3, "bad", _ => throw new InvalidOperationException("boom")),
        });
        JsonNode payload = JsonNode.Parse("""{"a":1}""")!;
        var original = payload;
        Assert.False(runner.TryMigrate(1, 3, ref payload, out var applied, out var error));
        Assert.Same(original, payload);
        Assert.Equal("""{"a":1}""", payload.ToJsonString());
        Assert.Empty(applied);
        Assert.Contains("bad", error);
    }

    [Fact]
    public void StepReturningNull_FailsInsteadOfYieldingANullPayload()
    {
        var runner = new MigrationRunner(new ISaveMigration[] { new Step(1, 2, "nil", _ => null) });
        JsonNode payload = JsonNode.Parse("""{"a":1}""")!;
        Assert.False(runner.TryMigrate(1, 2, ref payload, out _, out var error));
        Assert.Contains("nil", error);
        Assert.Equal("""{"a":1}""", payload.ToJsonString());
    }

    [Fact]
    public void ChecksumMismatch_OnAnOldSave_IsRejectedBeforeAnyMigration()
    {
        var bytes = Envelope(1, """{"schemaVersion":1}""");
        var env = JsonNode.Parse(bytes)!;
        env["payloadSha256Base64"] = Convert.ToBase64String(new byte[32]);
        var r = Codec().Decode(JsonSerializer.SerializeToUtf8Bytes(env));
        Assert.NotEqual(CodecStatus.Ok, r.Status);
        Assert.Empty(r.AppliedMigrations);
    }
}
