using System;
using System.Collections.Generic;
using System.IO;
using WalkGame.Application.Content;
using WalkGame.Application.Persistence;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;
using LandmarkId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.LandmarkIdKind>;
using ProducerId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProducerIdKind>;
using ProjectId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProjectIdKind>;
using RegionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RegionIdKind>;
using RewardTransactionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RewardTransactionIdKind>;

namespace WalkGame.Application.Tests;

public sealed class SessionRecoveryAndSummaryTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Continue_AfterThirtySixHourGap_SummarizesCompletion()
    {
        var clock = new ManualClock(TestSessions.T0);
        WriteMidProgressSave();

        clock.Advance(TimeSpan.FromHours(36));

        var session = TestSessions.Create(_temp, clock);
        var boot = session.Continue();

        Assert.Equal(StartStatus.Loaded, boot.Status);
        Assert.Contains(boot.SummaryLines, line => line.Contains("complete", StringComparison.Ordinal));

        var home = session.GetHome();
        Assert.Equal(1, home.CompletedProjects);
        Assert.Null(home.ActiveProjectId);
        Assert.Equal(200L, home.Vitality);
    }

    [Fact]
    public void Continue_WithBackwardClock_IgnoresSkew_WithoutRegression()
    {
        var clock = new ManualClock(TestSessions.T0);
        WriteMidProgressSave();
        clock.Advance(TimeSpan.FromHours(36));
        var forwardSession = TestSessions.Create(_temp, clock);
        Assert.Equal(StartStatus.Loaded, forwardSession.Continue().Status);

        clock.Set(TestSessions.T0);

        var session = TestSessions.Create(_temp, clock);
        var boot = session.Continue();

        Assert.Equal(StartStatus.Loaded, boot.Status);
        Assert.Contains(boot.SummaryLines, line => line.Contains("ignored", StringComparison.Ordinal));

        var persisted = TestSessions.NewCodec()
            .Decode(File.ReadAllBytes(_temp.FilePath("save.json")));
        Assert.Equal(CodecStatus.Ok, persisted.Status);
        Assert.Equal(
            TestSessions.T0.AddHours(36),
            persisted.State!.LastAdvancedUtc);
        Assert.Equal(ProjectStatus.Completed,
            persisted.State.Region.FindProject(TestSessions.EntryProjectId)!.Status);
        Assert.Equal(1, session.GetHome().CompletedProjects);
    }

    [Fact]
    public void Constructor_DuplicateProjectIds_ThrowsArgumentException()
    {
        var projects = new List<ProjectDefinition>
        {
            new ProjectDefinition(new ProjectId("proj.dup-a"), "Dup A", 100L),
            new ProjectDefinition(new ProjectId("proj.dup-a"), "Dup B", 120L),
        };
        var content = new RegionDefinition(new RegionId("region.dup"), "Duplicate Region",
            projects,
            Array.Empty<LandmarkDefinition>(),
            Array.Empty<ProducerDefinition>());

        Assert.Throws<ArgumentException>(
            () => TestSessions.Create(_temp.Path, new ManualClock(TestSessions.T0), content));
    }

    private void WriteMidProgressSave()
    {
        var content = Region1Catalog.Create();
        var state = GameFactory.NewGame(content, TestSessions.T0, seed: 7UL);
        state.Ledger.Apply(
            new RewardTransaction(RewardTransactionId.FromGuid(TestSessions.Tx1), TestSessions.T0, 250L, "walk"),
            state.Resources);
        var trailhead = state.Region.FindProject(TestSessions.EntryProjectId)!;
        trailhead.Status = ProjectStatus.Active;
        trailhead.VitalityInvested = 250L;
        state.Queue.ActiveProjectId = TestSessions.EntryProjectId;

        byte[] envelope = TestSessions.NewCodec().Encode(state, TestSessions.T0);
        new AtomicFileSaveStore(_temp.Path).WriteAtomic(envelope);
    }
}
