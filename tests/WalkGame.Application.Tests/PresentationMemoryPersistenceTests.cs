using System;
using System.IO;
using System.Linq;
using WalkGame.Application.Persistence;
using WalkGame.Application.Presentation;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-175 (KI-027 c): presentation memory survives a cold restart in its own file, never touches the
/// canonical save, degrades to "nothing shown" on damage, follows the loaded game, is erased by
/// "Delete all local data" and is not part of the privacy export.
/// </summary>
public sealed class PresentationMemoryPersistenceTests : IDisposable
{
    private static readonly DateTimeOffset T0 = TestSessions.T0;
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private GameSession Session(DateTimeOffset now, IPresentationMemoryStore? memory = null) =>
        new GameSession(new AtomicFileSaveStore(_temp.Path), TestSessions.NewCodec(), new ManualClock(now),
            WalkGame.Application.Content.Region1Catalog.Create(), new LocalPreferencesStore(_temp.Path), null,
            memory ?? new LocalPresentationMemoryStore(_temp.Path));

    private string MemoryFile => _temp.FilePath(LocalPresentationMemoryStore.DefaultFileName);

    private GameSession Started()
    {
        var s = Session(T0);
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(7UL).Status);
        return s;
    }

    private GameSession Reopened(DateTimeOffset at)
    {
        var s = Session(at);
        Assert.Equal(StartStatus.Loaded, s.Continue().Status);
        return s;
    }

    [Fact]
    public void Memory_survives_a_cold_restart()
    {
        var s = Started();
        Assert.Null(s.GetPresentationMemory().ChangesShownThroughUtc);
        Assert.Equal(PresentationMemorySaveStatus.Saved, s.SavePresentationMemory(T0.AddHours(1), new[] { "line.mira.1" }));

        var cold = Reopened(T0.AddDays(2));
        var m = cold.GetPresentationMemory();
        Assert.Equal(PresentationMemoryLoadOutcome.Success, cold.PresentationMemoryLoadOutcome);
        Assert.Equal(T0.AddHours(1), m.ChangesShownThroughUtc);
        Assert.Equal(new[] { "line.mira.1" }, m.SeenLineIds);
    }

    [Fact]
    public void Remembering_never_writes_or_changes_the_canonical_save()
    {
        var s = Started();
        var saveBytes = File.ReadAllBytes(_temp.FilePath("save.json"));
        var envelope = s.ExportSaveEnvelope();
        var files = Directory.GetFiles(_temp.Path).Select(Path.GetFileName).OrderBy(x => x).ToArray();

        for (int i = 0; i < 5; i++) s.SavePresentationMemory(T0.AddMinutes(i), new[] { "line." + i });
        s.GetPresentationMemory();

        Assert.Equal(saveBytes, File.ReadAllBytes(_temp.FilePath("save.json")));
        Assert.Equal(envelope, s.ExportSaveEnvelope());
        var after = Directory.GetFiles(_temp.Path).Select(Path.GetFileName).Where(f => f != LocalPresentationMemoryStore.DefaultFileName).OrderBy(x => x).ToArray();
        Assert.Equal(files, after); // no new save generation or temporary
        Assert.True(File.Exists(MemoryFile));
        Assert.Equal(StartStatus.Loaded, Session(T0.AddDays(1)).Continue().Status);
    }

    [Theory]
    [InlineData("{garbage")]
    [InlineData("")]
    [InlineData("{\"schemaVersion\":99}")]
    public void A_damaged_memory_file_means_nothing_shown_and_the_game_loads_normally(string content)
    {
        var s = Started();
        s.SavePresentationMemory(T0.AddHours(1), new[] { "line.mira.1" });
        File.WriteAllText(MemoryFile, content);

        var cold = Reopened(T0.AddDays(1));
        var saveBytes = File.ReadAllBytes(_temp.FilePath("save.json")); // after boot (offline catch-up may commit)
        var m = cold.GetPresentationMemory();
        Assert.NotEqual(PresentationMemoryLoadOutcome.Success, cold.PresentationMemoryLoadOutcome);
        Assert.Null(m.ChangesShownThroughUtc);   // the next open is a baseline: no replay
        Assert.Empty(m.SeenLineIds);
        Assert.Equal(PresentationMemorySaveStatus.Saved, cold.SavePresentationMemory(T0.AddHours(2), null));
        Assert.Equal(saveBytes, File.ReadAllBytes(_temp.FilePath("save.json"))); // reading or rewriting memory never touches the save
        Assert.Equal(T0.AddHours(2), Reopened(T0.AddDays(1)).GetPresentationMemory().ChangesShownThroughUtc);
    }

    [Fact]
    public void Memory_of_another_game_is_ignored()
    {
        var s = Started();
        s.SavePresentationMemory(T0.AddHours(1), new[] { "line.mira.1" });
        var replaced = Session(T0.AddDays(5));
        Assert.Equal(StartStatus.NewGameCreated, replaced.StartNewGame(8UL, overwriteExisting: true).Status);
        var m = replaced.GetPresentationMemory();
        Assert.Null(m.ChangesShownThroughUtc);
        Assert.Empty(m.SeenLineIds);
    }

    [Fact]
    public void No_game_loaded_means_nothing_to_remember()
    {
        var s = Session(T0);
        Assert.Equal(PresentationMemorySaveStatus.NoGame, s.SavePresentationMemory(T0, new[] { "x" }));
        Assert.Null(s.GetPresentationMemory().ChangesShownThroughUtc);
        Assert.False(File.Exists(MemoryFile));
    }

    [Fact]
    public void Delete_all_local_data_erases_the_memory_file()
    {
        var s = Started();
        s.SavePresentationMemory(T0.AddHours(1), new[] { "line.mira.1" });
        File.WriteAllText(MemoryFile + ".tmp", "leftover");
        Assert.True(File.Exists(MemoryFile));

        Assert.Equal(LocalDataEraseStatus.Erased, s.DeleteAllLocalData().Status);
        Assert.Empty(Directory.GetFiles(_temp.Path));
        Assert.Null(s.GetPresentationMemory().ChangesShownThroughUtc);
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(9UL).Status);
        Assert.Empty(s.GetPresentationMemory().SeenLineIds);
    }

    [Fact]
    public void Erase_fails_closed_when_the_memory_store_cannot_erase()
    {
        var s = Session(T0, new NonErasingStore());
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(7UL).Status);
        Assert.Equal(LocalDataEraseStatus.NotSupported, s.DeleteAllLocalData().Status);
        Assert.True(File.Exists(_temp.FilePath("save.json")), "nothing is touched");
    }

    [Fact]
    public void A_failed_write_is_reported_kept_for_the_session_and_never_thrown()
    {
        var s = Session(T0, new ThrowingStore());
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(7UL).Status);
        Assert.Equal(PresentationMemoryLoadOutcome.IoFailure, s.PresentationMemoryLoadOutcome);
        Assert.Equal(PresentationMemorySaveStatus.Failed, s.SavePresentationMemory(T0.AddHours(1), new[] { "line.a" }));
        Assert.Equal(T0.AddHours(1), s.GetPresentationMemory().ChangesShownThroughUtc);
    }

    [Fact]
    public void The_privacy_export_does_not_include_presentation_memory()
    {
        var s = Started();
        s.SavePresentationMemory(T0.AddHours(1), new[] { "line.marker-ki027c" });
        var export = SaveExport.ToReadableJson(s.ExportSaveEnvelope());
        Assert.Equal(SaveExportStatus.Exported, export.Status);
        Assert.DoesNotContain("line.marker-ki027c", export.Json!);
        Assert.DoesNotContain("changesShownThrough", export.Json!, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class NonErasingStore : IPresentationMemoryStore
    {
        public PresentationMemoryLoadResult Load() => new(PresentationMemoryLoadOutcome.NotFound);
        public void Save(PresentationMemoryState state) { }
    }

    private sealed class ThrowingStore : IPresentationMemoryStore, ILocalDataEraser
    {
        public PresentationMemoryLoadResult Load() => throw new IOException("disk");
        public void Save(PresentationMemoryState state) => throw new IOException("disk full");
        public void EraseAll() { }
    }
}
