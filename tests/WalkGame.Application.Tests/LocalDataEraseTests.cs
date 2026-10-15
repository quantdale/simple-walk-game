using System;
using System.IO;
using WalkGame.Application.Persistence;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Application.Tests;

/// <summary>
/// PRODUCT_SPEC §7a requirement 7: the player can delete ALL local data — every save
/// generation (primary, backup, temporaries) and local preferences — and the erase cannot
/// be undone by boot recovery from the backup generation (D-052).
/// </summary>
public sealed class LocalDataEraseTests : IDisposable
{
    private static readonly DateTimeOffset T0 = TestSessions.T0;
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private GameSession Session(DateTimeOffset now) =>
        TestSessions.Create(_temp.Path, new ManualClock(now), new LocalPreferencesStore(_temp.Path));

    private static NormalizedActivityRecord Walk(int day) =>
        new("erase.provider", "day-" + day, ActivityCategory.Walking, ActivityUnits.Steps, 8_000L,
            T0.AddDays(day), T0.AddDays(day).AddHours(1));

    private void SeedPopulatedProfile()
    {
        var s = Session(T0);
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(7UL).Status);
        Assert.True(s.SetReducedMotion(true).IsSuccess);
        var later = Session(T0.AddDays(2));
        Assert.Equal(StartStatus.Loaded, later.Continue().Status);
        later.IngestActivityBatch(new[] { Walk(1) }); // second commit => backup generation exists
        Assert.True(File.Exists(_temp.FilePath("save.backup.json")));
        Assert.True(File.Exists(_temp.FilePath("ux-preferences.json")));
    }

    [Fact]
    public void DeleteAllLocalData_RemovesEveryGenerationAndPreferences_AndBootFindsNothing()
    {
        SeedPopulatedProfile();
        File.WriteAllText(_temp.FilePath("save.tmp"), "stale"); // interrupted-commit leftover

        var s = Session(T0.AddDays(3));
        Assert.Equal(StartStatus.Loaded, s.Continue().Status);
        var result = s.DeleteAllLocalData();

        Assert.Equal(LocalDataEraseStatus.Erased, result.Status);
        Assert.Empty(Directory.GetFiles(_temp.Path));

        // No resurrection: a fresh boot must NOT recover from a backup generation.
        var reboot = Session(T0.AddDays(3).AddMinutes(1));
        Assert.Equal(StartStatus.NoSaveFound, reboot.Continue().Status);
    }

    [Fact]
    public void AfterErase_SessionIsNeverBooted_AndANewGameCanStart()
    {
        SeedPopulatedProfile();
        var s = Session(T0.AddDays(3));
        Assert.Equal(StartStatus.Loaded, s.Continue().Status);
        Assert.Equal(LocalDataEraseStatus.Erased, s.DeleteAllLocalData().Status);

        Assert.Throws<InvalidOperationException>(() => s.IngestActivityBatch(new[] { Walk(2) }));
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(9UL).Status);
        Assert.True(File.Exists(_temp.FilePath("save.json")));
    }

    [Fact]
    public void DeleteAllLocalData_IsIdempotent()
    {
        SeedPopulatedProfile();
        var s = Session(T0.AddDays(3));
        Assert.Equal(LocalDataEraseStatus.Erased, s.DeleteAllLocalData().Status);
        Assert.Equal(LocalDataEraseStatus.Erased, s.DeleteAllLocalData().Status);
        Assert.Empty(Directory.GetFiles(_temp.Path));
    }

    [Fact]
    public void StoreWithoutEraseCapability_FailsClosed_AndTouchesNothing()
    {
        SeedPopulatedProfile();
        var inner = new AtomicFileSaveStore(_temp.Path);
        var s = new GameSession(new NonErasingStore(inner), TestSessions.NewCodec(),
            new ManualClock(T0.AddDays(3)), WalkGame.Application.Content.Region1Catalog.Create(),
            new LocalPreferencesStore(_temp.Path));
        Assert.Equal(StartStatus.Loaded, s.Continue().Status);

        var result = s.DeleteAllLocalData();

        Assert.Equal(LocalDataEraseStatus.NotSupported, result.Status);
        Assert.True(File.Exists(_temp.FilePath("save.json")));
        Assert.True(File.Exists(_temp.FilePath("save.backup.json")));
        Assert.True(File.Exists(_temp.FilePath("ux-preferences.json")));
    }

    private sealed class NonErasingStore : ISaveStore
    {
        private readonly ISaveStore _inner;
        public NonErasingStore(ISaveStore inner) => _inner = inner;
        public SaveReadResult ReadPrimary() => _inner.ReadPrimary();
        public SaveReadResult ReadBackup() => _inner.ReadBackup();
        public void WriteAtomic(byte[] envelopeBytes) => _inner.WriteAtomic(envelopeBytes);
        public void WriteAtomicPreservingBackup(byte[] envelopeBytes) => _inner.WriteAtomicPreservingBackup(envelopeBytes);
    }
}
