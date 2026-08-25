using System;
using System.Text.Json.Nodes;
using WalkGame.Infrastructure.Persistence;
using Xunit;

namespace WalkGame.Infrastructure.Tests;

public sealed class MigrationRunnerTests
{
    private sealed class FakeMigration : ISaveMigration
    {
        private readonly Action<JsonObject> _transform;

        public FakeMigration(int fromVersion, int toVersion, string migrationId, Action<JsonObject> transform)
        {
            FromVersion = fromVersion;
            ToVersion = toVersion;
            MigrationId = migrationId;
            _transform = transform;
        }

        public int FromVersion { get; }
        public int ToVersion { get; }
        public string MigrationId { get; }

        public JsonNode Migrate(JsonNode payload)
        {
            _transform(payload!.AsObject());
            return payload;
        }
    }

    private static ISaveMigration RenameAToB(int fromVersion, int toVersion, string id) =>
        new FakeMigration(fromVersion, toVersion, id, obj =>
        {
            obj["b"] = obj["a"]?.DeepClone();
            obj.Remove("a");
        });

    private static ISaveMigration AddCFromB(int fromVersion, int toVersion, string id) =>
        new FakeMigration(fromVersion, toVersion, id, obj =>
            obj["c"] = (obj["b"]?.GetValue<int>() ?? 0) + 100);

    [Fact]
    public void Constructor_NonContiguousChain_ThrowsInvalidOperation()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new MigrationRunner(new ISaveMigration[]
        {
            RenameAToB(1, 2, "m1"),
            RenameAToB(3, 4, "m3"),
        }));

        Assert.Contains("not contiguous", ex.Message);
    }

    [Fact]
    public void TryMigrate_RunsChainSequentially_InRegistrationOrderOfVersions()
    {
        var runner = new MigrationRunner(new ISaveMigration[]
        {
            AddCFromB(2, 3, "m2"),
            RenameAToB(1, 2, "m1"),
        });

        JsonNode payload = JsonNode.Parse("""{"a":7}""")!;
        var success = runner.TryMigrate(1, 3, ref payload, out var appliedIds, out var error);

        Assert.True(success, error);
        Assert.Equal(new[] { "m1", "m2" }, appliedIds);
        var obj = payload!.AsObject();
        Assert.Null(obj["a"]);
        Assert.Equal(7, obj["b"]!.GetValue<int>());
        Assert.Equal(107, obj["c"]!.GetValue<int>());
    }

    [Fact]
    public void TryMigrate_ChainGap_FailsWithMissingMigrationError_InputUnchanged()
    {
        var runner = new MigrationRunner(new ISaveMigration[] { RenameAToB(2, 3, "m2") });

        JsonNode payload = JsonNode.Parse("""{"a":5}""")!;
        var original = payload.ToJsonString();
        var success = runner.TryMigrate(1, 3, ref payload, out _, out var error);

        Assert.False(success);
        Assert.Contains("No registered migration", error);
        Assert.Equal(original, payload.ToJsonString());
        Assert.Equal(5, payload.AsObject()["a"]!.GetValue<int>());
    }

    [Fact]
    public void TryMigrate_ThrowingStep_FailsWithError_InputUnchanged()
    {
        var runner = new MigrationRunner(new ISaveMigration[]
        {
            new FakeMigration(1, 2, "m1", _ => throw new InvalidOperationException("boom")),
        });

        JsonNode payload = JsonNode.Parse("""{"a":5}""")!;
        var original = payload.ToJsonString();
        var success = runner.TryMigrate(1, 2, ref payload, out _, out var error);

        Assert.False(success);
        Assert.Contains("m1", error);
        Assert.Contains("boom", error);
        Assert.Equal(original, payload.ToJsonString());
    }

    [Fact]
    public void TryMigrate_SourceEqualsTarget_SucceedsImmediately()
    {
        var runner = new MigrationRunner(Array.Empty<ISaveMigration>());

        JsonNode payload = JsonNode.Parse("""{"a":1}""")!;
        var original = payload.ToJsonString();
        var success = runner.TryMigrate(1, 1, ref payload, out var appliedIds, out var error);

        Assert.True(success);
        Assert.Empty(appliedIds);
        Assert.Equal(string.Empty, error);
        Assert.Equal(original, payload.ToJsonString());
    }

    [Fact]
    public void TryMigrate_SourceNewerThanTarget_ReportsUnsupportedSchema()
    {
        var runner = new MigrationRunner(Array.Empty<ISaveMigration>());

        JsonNode payload = JsonNode.Parse("""{"a":1}""")!;
        var success = runner.TryMigrate(3, 1, ref payload, out _, out var error);

        Assert.False(success);
        Assert.Contains("newer than this build", error);
    }
}
