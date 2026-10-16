using System;
using WalkGame.Application.Persistence;
using System.IO;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Time;

namespace WalkGame.Application.Tests;

/// <summary>D-085 (KI-021): an explicit overwrite parameter on StartNewGame.</summary>
public sealed class StartOverwriteGuardTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private GameSession NewSession() => TestSessions.Create(_temp, new ManualClock(TestSessions.T0));

    [Fact]
    public void NoOverwrite_OnEmptyStore_CreatesAGame()
    {
        var start = NewSession().StartNewGame(7UL, overwriteExisting: false);
        Assert.Equal(StartStatus.NewGameCreated, start.Status);
        Assert.True(File.Exists(_temp.FilePath("save.json")));
    }

    [Fact]
    public void NoOverwrite_WithExistingSave_RefusesAndLeavesBytesUnchanged()
    {
        Assert.Equal(StartStatus.NewGameCreated, NewSession().StartNewGame(7UL).Status);
        var before = File.ReadAllBytes(_temp.FilePath("save.json"));

        var session = NewSession();
        var refused = session.StartNewGame(99UL, overwriteExisting: false);

        Assert.Equal(StartStatus.ExistingSaveRefused, refused.Status);
        Assert.Equal(before, File.ReadAllBytes(_temp.FilePath("save.json")));
        Assert.Equal(StartStatus.Loaded, session.Continue().Status);
    }

    [Fact]
    public void NoOverwrite_WithUnreadablePrimary_RefusesAndKeepsTheBytes()
    {
        Assert.Equal(StartStatus.NewGameCreated, NewSession().StartNewGame(7UL).Status);
        File.WriteAllBytes(_temp.FilePath("save.json"), new byte[] { 1, 2, 3 });

        var refused = NewSession().StartNewGame(99UL, overwriteExisting: false);

        Assert.Equal(StartStatus.ExistingSaveRefused, refused.Status);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(_temp.FilePath("save.json")));
    }

    [Fact]
    public void NoOverwrite_WithOnlyABackupGeneration_Refuses()
    {
        var first = NewSession();
        Assert.Equal(StartStatus.NewGameCreated, first.StartNewGame(7UL).Status);
        Assert.Equal(StartStatus.NewGameCreated, first.StartNewGame(8UL).Status); // rotates a backup
        File.Delete(_temp.FilePath("save.json"));

        var refused = NewSession().StartNewGame(99UL, overwriteExisting: false);
        Assert.Equal(StartStatus.ExistingSaveRefused, refused.Status);
        Assert.False(File.Exists(_temp.FilePath("save.json")));
    }

    [Fact]
    public void LegacyOverload_AndExplicitTrue_StillReplace()
    {
        Assert.Equal(StartStatus.NewGameCreated, NewSession().StartNewGame(7UL).Status);
        Assert.Equal(StartStatus.NewGameCreated, NewSession().StartNewGame(8UL).Status);
        Assert.Equal(StartStatus.NewGameCreated, NewSession().StartNewGame(9UL, overwriteExisting: true).Status);
    }

    [Fact]
    public void NoOverwrite_AfterDeleteAllLocalData_CreatesAGame()
    {
        var session = NewSession();
        Assert.Equal(StartStatus.NewGameCreated, session.StartNewGame(7UL).Status);
        Assert.Equal(LocalDataEraseStatus.Erased, session.DeleteAllLocalData().Status);
        Assert.Equal(StartStatus.NewGameCreated, session.StartNewGame(8UL, overwriteExisting: false).Status);
    }
}
