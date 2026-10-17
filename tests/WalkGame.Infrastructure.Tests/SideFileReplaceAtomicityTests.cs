using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WalkGame.Application.Presentation;
using WalkGame.Application.Ux;
using WalkGame.Infrastructure.Persistence;
using WalkGame.Infrastructure.Tests.TestSupport;

namespace WalkGame.Infrastructure.Tests;

/// <summary>
/// D-179: rewriting a side file (UX preferences, presentation memory) never leaves a moment with
/// no committed file. Before D-179 the stores deleted the file and then moved the temporary into
/// place; a crash in between lost the player's settings, because a temporary on its own is never
/// trusted (M5-H1 contract, kept). A concurrent reader makes that window observable: with
/// delete+move it saw the file missing in about a third of its reads.
/// </summary>
public sealed class SideFileReplaceAtomicityTests : IDisposable
{
    private static readonly DateTimeOffset Created = new(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task Preferences_ReaderNeverSeesTheFileMissing_WhileItIsRewritten()
    {
        var store = new LocalPreferencesStore(_temp.Path);
        var s = UxPreferencesState.CreateDefault();
        s.ReducedMotion = true;
        store.Save(s);
        int missing = 0, lostSetting = 0, reads = 0;
        using var stop = new CancellationTokenSource();
        var reader = Task.Run(() =>
        {
            var r = new LocalPreferencesStore(_temp.Path);
            while (!stop.IsCancellationRequested)
            {
                var result = r.Load();
                Interlocked.Increment(ref reads);
                if (result.Outcome == UxPreferencesLoadOutcome.NotFound) Interlocked.Increment(ref missing);
                else if (result.Outcome == UxPreferencesLoadOutcome.Success && !result.State!.ReducedMotion) Interlocked.Increment(ref lostSetting);
            }
        });
        for (int i = 0; i < 2000; i++) { s.SoundEnabled = (i % 2) == 0; store.Save(s); }
        stop.Cancel();
        await reader;
        Assert.True(missing == 0, $"preferences file missing in {missing} of {reads} reads");
        Assert.Equal(0, lostSetting);
    }

    [Fact]
    public async Task Memory_ReaderNeverSeesTheFileMissing_WhileItIsRewritten()
    {
        var store = new LocalPresentationMemoryStore(_temp.Path);
        store.Save(PresentationMemoryState.Empty(Created));
        int missing = 0, reads = 0;
        using var stop = new CancellationTokenSource();
        var reader = Task.Run(() =>
        {
            var r = new LocalPresentationMemoryStore(_temp.Path);
            while (!stop.IsCancellationRequested)
            {
                if (r.Load().Outcome == PresentationMemoryLoadOutcome.NotFound) Interlocked.Increment(ref missing);
                Interlocked.Increment(ref reads);
            }
        });
        for (int i = 0; i < 2000; i++)
            store.Save(new PresentationMemoryState { GameCreatedAtUtc = Created, ChangesShownThroughUtc = Created.AddMinutes(i) });
        stop.Cancel();
        await reader;
        Assert.True(missing == 0, $"presentation memory file missing in {missing} of {reads} reads");
    }

    [Fact]
    public void Rewrites_LeaveNoTemporaryOrBackupBehind_AndTheLastWriteWins()
    {
        var prefs = new LocalPreferencesStore(_temp.Path);
        var memory = new LocalPresentationMemoryStore(_temp.Path);
        var s = UxPreferencesState.CreateDefault();
        for (int i = 0; i < 5; i++)
        {
            s.DailyReminderMinutesOfDay = 600 + i;
            prefs.Save(s);
            memory.Save(new PresentationMemoryState { GameCreatedAtUtc = Created, ChangesShownThroughUtc = Created.AddDays(i) });
        }
        Assert.Equal(new[] { LocalPresentationMemoryStore.DefaultFileName, "ux-preferences.json" },
            Array.ConvertAll(Directory.GetFiles(_temp.Path), f => Path.GetFileName(f)!).OrderBy());
        Assert.Equal(604, new LocalPreferencesStore(_temp.Path).Load().State!.DailyReminderMinutesOfDay);
        Assert.Equal(Created.AddDays(4), new LocalPresentationMemoryStore(_temp.Path).Load().State!.ChangesShownThroughUtc);
    }

    [Fact]
    public void FirstWrite_WithAStaleTemporary_StillCommits()
    {
        File.WriteAllText(_temp.FilePath("ux-preferences.json.tmp"), "torn");
        File.WriteAllText(_temp.FilePath(LocalPresentationMemoryStore.DefaultFileName + ".tmp"), "torn");
        var s = UxPreferencesState.CreateDefault();
        s.ReducedMotion = true;
        new LocalPreferencesStore(_temp.Path).Save(s);
        new LocalPresentationMemoryStore(_temp.Path).Save(PresentationMemoryState.Empty(Created));
        Assert.True(new LocalPreferencesStore(_temp.Path).Load().State!.ReducedMotion);
        Assert.Equal(PresentationMemoryLoadOutcome.Success, new LocalPresentationMemoryStore(_temp.Path).Load().Outcome);
    }
}

internal static class SortExtensions
{
    public static string[] OrderBy(this string[] a) { var c = (string[])a.Clone(); Array.Sort(c, StringComparer.Ordinal); return c; }
}
