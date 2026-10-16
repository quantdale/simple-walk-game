using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Time;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>
/// Security review 2026-10-16: checksum-VALID but semantically tampered saves. A single tampered
/// generation must recover from the backup; both tampered must fail closed with a typed status;
/// nothing may throw. Self-cheats that stay internally consistent (a huge balance) load by
/// design: the game is offline and single-player, and arithmetic is overflow-guarded.
/// </summary>
public sealed class SaveTamperTests : IDisposable
{
    private static readonly DateTimeOffset T0 = TestSessions.T0;
    private const int Days = 30;
    private static readonly object Gate = new();
    private static string? _fixture;

    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    /// <summary>One mid-game save shared by every case (driven once; copied per case).</summary>
    private static string Fixture()
    {
        lock (Gate)
        {
            if (_fixture != null) return _fixture;
            var dir = Path.Combine(Path.GetTempPath(), "wg-tamper-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var content = Region1Catalog.Create();
            TestSessions.Create(dir, new ManualClock(T0)).StartNewGame(42UL);
            for (int d = 1; d <= Days; d++)
            {
                var x = TestSessions.Create(dir, new ManualClock(T0.AddDays(d)));
                x.Continue();
                x.IngestFromSource(new SyntheticWalkingSource(20_000), T0.AddDays(d - 1), T0.AddDays(d));
                var h = x.GetHome();
                if (h.ActiveProjectId == null && h.Queued.Count == 0)
                    foreach (var p in content.Projects) if (x.EnqueueProject(p.Id.Value).IsSuccess) break;
            }
            return _fixture = dir;
        }
    }

    private static readonly Dictionary<string, Action<JsonNode>> Mutations = new()
    {
        ["neg-vitality"] = p => p["resources"]!["amounts"]!["vitality"] = -1,
        ["materials-over-cap"] = p => p["resources"]!["amounts"]!["materials"] = 1_000_000,
        ["completed-without-times"] = p => { foreach (var kv in p["region"]!["projects"]!.AsObject()) kv.Value!["status"] = "completed"; },
        ["care-cycles-before-completion"] = p => p["region"]!["postCompletionCyclesCompleted"] = long.MaxValue,
        ["region-completed-flag-only"] = p => { p["region"]!["isCompleted"] = true; p["region"]!["regionCompletedAtUtc"] = T0.AddDays(1).ToString("o"); },
        ["unknown-project"] = p => p["region"]!["projects"]!.AsObject()["proj.injected"] = JsonNode.Parse("{\"projectId\":\"proj.injected\",\"status\":\"completed\"}"),
        ["summary-over-item-bound"] = p => { var a = new JsonArray(); for (int i = 0; i < 200; i++) a.Add(JsonNode.Parse("{\"kind\":\"transformation\",\"text\":\"x" + i + "\",\"firstCommittedAtUtc\":\"" + T0.ToString("o") + "\"}")); p["pendingReturnSummary"]!["items"] = a; },
        ["summary-huge-text"] = p => p["pendingReturnSummary"]!["items"]![0]!["text"] = new string('z', 1_000_000),
        ["queue-active-unknown"] = p => p["queue"]!["activeProjectId"] = "proj.nope",
        ["rng-all-zero"] = p => { foreach (var k in new[] { "s0", "s1", "s2", "s3" }) p["rng"]![k] = 0; },
        ["schema-version-99"] = p => p["schemaVersion"] = 99,
        ["content-version-99"] = p => p["authoredContentVersion"] = 99,
        ["max-vitality"] = p => p["resources"]!["amounts"]!["vitality"] = long.MaxValue,
        ["unknown-field"] = p => p["evil"] = "x",
    };

    private StartStatus Load(string mutation, bool both, out GameSession session)
    {
        foreach (var f in Directory.GetFiles(Fixture())) File.Copy(f, Path.Combine(_temp.Path, Path.GetFileName(f)), true);
        foreach (var file in both ? new[] { "save.json", "save.backup.json" } : new[] { "save.json" })
        {
            var path = Path.Combine(_temp.Path, file);
            Assert.True(File.Exists(path), file + " missing from fixture");
            var payload = PayloadSurgery.OpenPayload(File.ReadAllBytes(path));
            Mutations[mutation](payload);
            File.WriteAllBytes(path, PayloadSurgery.WrapPayload(payload, (int)payload["schemaVersion"]!, T0.AddDays(Days)));
        }
        session = TestSessions.Create(_temp.Path, new ManualClock(T0.AddDays(Days)));
        return session.Continue().Status;
    }

    [Theory]
    [InlineData("neg-vitality", StartStatus.StateInvalid)]
    [InlineData("materials-over-cap", StartStatus.StateInvalid)]
    [InlineData("completed-without-times", StartStatus.StateInvalid)]
    [InlineData("care-cycles-before-completion", StartStatus.StateInvalid)]
    [InlineData("region-completed-flag-only", StartStatus.StateInvalid)]
    [InlineData("unknown-project", StartStatus.StateInvalid)]
    [InlineData("summary-over-item-bound", StartStatus.StateInvalid)]
    [InlineData("summary-huge-text", StartStatus.StateInvalid)]
    [InlineData("queue-active-unknown", StartStatus.StateInvalid)]
    [InlineData("rng-all-zero", StartStatus.StateInvalid)]
    [InlineData("schema-version-99", StartStatus.SaveUnreadable)]
    public void Tampered_primary_recovers_from_backup_and_both_tampered_fail_closed(string mutation, StartStatus bothStatus)
    {
        Assert.Equal(StartStatus.RecoveredFromBackup, Load(mutation, both: false, out var recovered));
        Assert.Equal(5, recovered.GetStory().UnlockedCount);  // the untouched backup's honest progress

        Assert.Equal(bothStatus, Load(mutation, both: true, out var failed));
        Assert.False(failed.HasLoadedState);
    }

    [Fact]
    public void A_newer_content_version_fails_closed_without_falling_back()
    {
        Assert.Equal(StartStatus.ContentIncompatible, Load("content-version-99", both: false, out var s));
        Assert.False(s.HasLoadedState);
    }

    [Theory]
    [InlineData("max-vitality")]
    [InlineData("unknown-field")]
    public void Consistent_self_edits_load_and_stay_overflow_safe(string mutation)
    {
        Assert.Equal(StartStatus.Loaded, Load(mutation, both: true, out var s));
        long before = s.GetHome().Vitality;
        var later = TestSessions.Create(_temp.Path, new ManualClock(T0.AddDays(Days + 1)));
        Assert.Equal(StartStatus.Loaded, later.Continue().Status);
        later.IngestFromSource(new SyntheticWalkingSource(20_000), T0.AddDays(Days), T0.AddDays(Days + 1));
        Assert.True(later.GetHome().Vitality >= 0, "balance wrapped negative");
        Assert.True(later.GetStory().UnlockedCount <= later.GetStory().TotalChapters);
        _ = before;
    }
}
