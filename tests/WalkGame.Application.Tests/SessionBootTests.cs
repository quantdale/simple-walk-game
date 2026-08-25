using System;
using System.IO;
using WalkGame.Application.Persistence;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Application.Tests;

public sealed class SessionBootTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Continue_WithoutAnySave_ReturnsNoSaveFound()
    {
        var session = TestSessions.Create(_temp, new ManualClock(TestSessions.T0));

        var result = session.Continue();

        Assert.Equal(StartStatus.NoSaveFound, result.Status);
    }

    [Fact]
    public void StartNewGame_WritesSaveFile_ContinueLoads_HomeIsPristine()
    {
        var clock = new ManualClock(TestSessions.T0);
        var session = TestSessions.Create(_temp, clock);

        var start = session.StartNewGame(seed: 7UL);
        Assert.Equal(StartStatus.NewGameCreated, start.Status);
        Assert.True(File.Exists(_temp.FilePath("save.json")));

        var loaded = session.Continue();
        Assert.Equal(StartStatus.Loaded, loaded.Status);

        var home = session.GetHome();
        Assert.Equal(0L, home.Vitality);
        Assert.Equal(0L, home.Materials);
        Assert.Equal(0L, home.Knowledge);
        Assert.Equal(0, home.CompletedProjects);
        Assert.Equal(session.Content.Projects.Count, home.TotalProjects);
        Assert.Null(home.ActiveProjectId);

        var entry = session.Content.FindProject(TestSessions.EntryProjectId);
        Assert.NotNull(entry);
        Assert.Empty(entry!.Prerequisites);

        var persisted = TestSessions.NewCodec()
            .Decode(File.ReadAllBytes(_temp.FilePath("save.json")));
        Assert.Equal(CodecStatus.Ok, persisted.Status);
        var entryRuntime = persisted.State!.Region.FindProject(TestSessions.EntryProjectId);
        Assert.NotNull(entryRuntime);
        Assert.Equal(ProjectStatus.Available, entryRuntime!.Status);
    }

    [Fact]
    public void Continue_WithCorruptPrimaryAndHealthyBackup_RecoversFromBackup()
    {
        var clock = new ManualClock(TestSessions.T0);
        var writer = TestSessions.Create(_temp, clock);
        writer.StartNewGame(seed: 7UL);
        writer.CreditActivity(TestSessions.Tx1, TestSessions.T0, 250L, "walk");
        writer.CreditActivity(TestSessions.Tx2, TestSessions.T0.AddMinutes(1), 10L, "run");

        File.WriteAllText(_temp.FilePath("save.json"), "{corrupted");

        var session = TestSessions.Create(_temp, clock);
        var result = session.Continue();

        Assert.Equal(StartStatus.RecoveredFromBackup, result.Status);
        Assert.True(result.SummaryLines.Count > 0);
        Assert.Contains("backup", result.SummaryLines[0]);
        Assert.Equal(250L, session.GetHome().Vitality);
    }

    [Fact]
    public void Continue_WhenBothCopiesAreCorrupted_ReturnsSaveUnreadable()
    {
        var clock = new ManualClock(TestSessions.T0);
        var writer = TestSessions.Create(_temp, clock);
        writer.StartNewGame(seed: 7UL);
        writer.CreditActivity(TestSessions.Tx1, TestSessions.T0, 250L, "walk");

        File.WriteAllText(_temp.FilePath("save.json"), "junk-one");
        File.WriteAllText(_temp.FilePath("save.backup.json"), "junk-two");

        var session = TestSessions.Create(_temp, clock);
        var result = session.Continue();

        Assert.Equal(StartStatus.SaveUnreadable, result.Status);
        Assert.False(string.IsNullOrEmpty(result.Detail));
    }

    [Fact]
    public void StartNewGame_SecondGeneration_IsPreservedAsHealthyBackup()
    {
        var clock = new ManualClock(TestSessions.T0);
        var session = TestSessions.Create(_temp, clock);
        session.StartNewGame(seed: 7UL);

        session.CreditActivity(TestSessions.Tx1, TestSessions.T0, 10L, "walk");

        var store = new AtomicFileSaveStore(_temp.Path);
        Assert.Equal(SaveReadOutcome.Success, store.ReadBackup().Outcome);
        Assert.Equal(SaveReadOutcome.Success, store.ReadPrimary().Outcome);
    }
}
