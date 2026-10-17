using System;
using System.IO;
using System.Linq;
using WalkGame.Application.Presentation;
using WalkGame.Infrastructure.Persistence;
using WalkGame.Infrastructure.Tests.TestSupport;

namespace WalkGame.Infrastructure.Tests;

/// <summary>D-175 store contract: own versioned file, atomic write, every damage reads as "nothing shown".</summary>
public sealed class LocalPresentationMemoryStoreTests : IDisposable
{
    private static readonly DateTimeOffset Created = new(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private LocalPresentationMemoryStore NewStore() => new(_temp.Path);
    private string FilePath => _temp.FilePath(LocalPresentationMemoryStore.DefaultFileName);

    [Fact]
    public void Missing_file_is_NotFound()
    {
        var r = NewStore().Load();
        Assert.Equal(PresentationMemoryLoadOutcome.NotFound, r.Outcome);
        Assert.Null(r.State);
    }

    [Fact]
    public void Save_then_load_round_trips_in_a_new_store_instance_and_normalizes_ids()
    {
        NewStore().Save(new PresentationMemoryState
        {
            GameCreatedAtUtc = Created,
            ChangesShownThroughUtc = Created.AddDays(3).AddMinutes(7),
            SeenLineIds = { "line.b", "line.a", "line.b", "", new string('x', 200) },
        });
        var r = NewStore().Load();
        Assert.Equal(PresentationMemoryLoadOutcome.Success, r.Outcome);
        Assert.Equal(Created, r.State!.GameCreatedAtUtc);
        Assert.Equal(Created.AddDays(3).AddMinutes(7), r.State.ChangesShownThroughUtc);
        Assert.Equal(new[] { "line.a", "line.b" }, r.State.SeenLineIds);
        Assert.False(File.Exists(FilePath + ".tmp"), "the temporary is moved into place");
        Assert.Contains("\"schemaVersion\":1", File.ReadAllText(FilePath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{not json")]
    [InlineData("{\"schemaVersion\":0}")]
    [InlineData("{\"seenLineIds\":[\"a\"]}")]
    [InlineData("{\"schemaVersion\":1,\"changesShownThroughUtc\":\"yesterday\"}")]
    [InlineData("{\"schemaVersion\":1,\"seenLineIds\":[1,2]}")]
    [InlineData("null")]
    [InlineData("[1,2,3]")]
    public void Damaged_files_are_Malformed_and_never_throw(string content)
    {
        File.WriteAllText(FilePath, content);
        var r = NewStore().Load();
        Assert.Equal(PresentationMemoryLoadOutcome.Malformed, r.Outcome);
        Assert.Null(r.State);
    }

    [Fact]
    public void Oversized_file_is_Malformed_without_being_parsed()
    {
        File.WriteAllText(FilePath, "{\"schemaVersion\":1,\"seenLineIds\":[\"" + new string('a', LocalPresentationMemoryStore.MaxFileBytes) + "\"]}");
        Assert.Equal(PresentationMemoryLoadOutcome.Malformed, NewStore().Load().Outcome);
    }

    [Fact]
    public void Newer_schema_is_FutureVersion_and_not_interpreted()
    {
        File.WriteAllText(FilePath, "{\"schemaVersion\":2,\"changesShownThroughUtc\":\"2026-03-04T00:00:00+00:00\"}");
        var r = NewStore().Load();
        Assert.Equal(PresentationMemoryLoadOutcome.FutureVersion, r.Outcome);
        Assert.Null(r.State);
    }

    [Fact]
    public void Leftover_temporary_is_ignored_and_replaced_by_the_next_write()
    {
        File.WriteAllText(FilePath + ".tmp", "half-written");
        Assert.Equal(PresentationMemoryLoadOutcome.NotFound, NewStore().Load().Outcome);
        NewStore().Save(PresentationMemoryState.Empty(Created));
        Assert.Equal(PresentationMemoryLoadOutcome.Success, NewStore().Load().Outcome);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void Line_ids_are_bounded()
    {
        var many = Enumerable.Range(0, PresentationMemoryState.MaxSeenLineIds + 50).Select(i => "line." + i.ToString("D4"));
        NewStore().Save(new PresentationMemoryState { SeenLineIds = many.ToList() });
        Assert.Equal(PresentationMemoryState.MaxSeenLineIds, NewStore().Load().State!.SeenLineIds.Count);
    }

    [Fact]
    public void EraseAll_removes_file_and_temporary_and_is_idempotent()
    {
        NewStore().Save(PresentationMemoryState.Empty(Created));
        File.WriteAllText(FilePath + ".tmp", "x");
        var s = NewStore();
        s.EraseAll(); s.EraseAll();
        Assert.Empty(Directory.GetFiles(_temp.Path));
    }
}
