using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WalkGame.Application;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Application.Tests;

/// <summary>D-111: local save export (envelope from the session, readable document from Infrastructure).</summary>
public sealed class SaveExportTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 3, 10, 8, 0, 0, TimeSpan.Zero);
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private GameSession Started()
    {
        var s = TestSessions.Create(_temp, new ManualClock(T0));
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(seed: 7UL).Status);
        return s;
    }

    private static string[] Snapshot(string dir) =>
        Directory.GetFiles(dir, "*", SearchOption.AllDirectories).OrderBy(f => f)
            .Select(f => f + ":" + Convert.ToBase64String(File.ReadAllBytes(f))).ToArray();

    [Fact]
    public void NoGameLoaded_ExportsNothing()
    {
        var s = TestSessions.Create(_temp, new ManualClock(T0));
        Assert.Null(s.ExportSaveEnvelope());
    }

    [Fact]
    public void Export_IsReadOnly_AndDecodesToTheSameState()
    {
        var s = Started();
        var before = Snapshot(_temp.Path);
        var env = s.ExportSaveEnvelope();
        Assert.NotNull(env);
        Assert.Equal(before, Snapshot(_temp.Path));
        var codec = new SaveCodec(new MigrationRunner(DefaultMigrations.All));
        var decoded = codec.Decode(env!);
        Assert.Equal(WalkGame.Application.Persistence.CodecStatus.Ok, decoded.Status);
    }

    [Fact]
    public void ReadableDocument_HasFormatSchemaAndState()
    {
        var r = SaveExport.ToReadableJson(Started().ExportSaveEnvelope());
        Assert.Equal(SaveExportStatus.Exported, r.Status);
        var doc = JsonNode.Parse(r.Json!)!.AsObject();
        Assert.Equal("simple-walk-game-export", (string?)doc["format"]);
        Assert.Equal(1, (int)doc["formatVersion"]!);
        Assert.Equal(WalkGame.Domain.SchemaVersions.Current, (int)doc["schemaVersion"]!);
        Assert.IsType<JsonObject>(doc["state"]);
        Assert.True(doc["state"]!.AsObject().Count > 0);
    }

    [Fact]
    public void TamperedPayload_IsRefused()
    {
        var env = JsonNode.Parse(Started().ExportSaveEnvelope()!)!.AsObject();
        var key = env.Select(kv => kv.Key).Single(k => k.IndexOf("payload", StringComparison.OrdinalIgnoreCase) >= 0 && k.IndexOf("sha", StringComparison.OrdinalIgnoreCase) < 0);
        env[key] = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"x\":1}"));
        var r = SaveExport.ToReadableJson(Encoding.UTF8.GetBytes(env.ToJsonString()));
        Assert.Equal(SaveExportStatus.ChecksumMismatch, r.Status);
        Assert.Null(r.Json);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("[1,2]")]
    [InlineData("{\"payloadBase64\":\"%%%\"}")]
    [InlineData("null")]
    public void HostileBytes_AreMalformed_NotAnException(string text)
    {
        var r = SaveExport.ToReadableJson(Encoding.UTF8.GetBytes(text));
        Assert.Equal(SaveExportStatus.Malformed, r.Status);
        Assert.Null(r.Json);
    }

    [Fact]
    public void NullBytes_AreMalformed() => Assert.Equal(SaveExportStatus.Malformed, SaveExport.ToReadableJson(null).Status);
}
