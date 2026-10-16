using System;
using System.Linq;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Time;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>D-129: the Region 1 epilogue is a calm, read-only, never-expiring projection of region completion.</summary>
public sealed class EpilogueTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private static readonly string[] Banned =
        { "missed", "streak", "hurry", "don't forget", "falling behind", "decay", "expire", "penalty", "you lost", "too late", "disappointed", "fault", "region 2", "next region", "coming soon", "beyond the ridge", "new valley" };

    private static Region1Epilogue.Entry[] AllEntries() =>
        new[] { Region1Epilogue.JournalEntry }.Concat(Region1Epilogue.ClosingLetters).Concat(Region1Epilogue.ValleyNotes).ToArray();

    [Fact]
    public void Copy_IsCalm_Bounded_Unique_AndHasNoRegion2Hook()
    {
        var all = AllEntries();
        Assert.Equal(all.Length, all.Select(e => e.Id).Distinct().Count());
        Assert.Equal(2, Region1Epilogue.ClosingLetters.Count);
        Assert.Equal(8, Region1Epilogue.ValleyNotes.Count);
        foreach (var e in all)
        {
            Assert.InRange(e.Body.Length, 40, Region1Epilogue.MaxBodyLength);
            Assert.StartsWith("epilogue.", e.Id);
            Assert.DoesNotContain("\"", e.Body);
            foreach (var bad in Banned) Assert.DoesNotContain(bad, e.Body.ToLowerInvariant());
        }
        Assert.Equal(Region1Epilogue.ValleyNotes.Select(n => n.DayAfterCompletion).OrderBy(d => d), Region1Epilogue.ValleyNotes.Select(n => n.DayAfterCompletion));
    }

    [Theory]
    [InlineData(-5, 0)] [InlineData(0, 0)] [InlineData(29, 0)] [InlineData(30, 1)] [InlineData(239, 7)] [InlineData(240, 0)] [InlineData(3650, 1)]
    public void CurrentNote_CyclesForever_AndABackwardClockShowsDayZero(int days, int expected)
    {
        int since = Region1Epilogue.DaysSince(T0, T0.AddDays(days));
        Assert.Equal(Math.Max(0, days), since);
        Assert.Equal(expected, Region1Epilogue.CurrentNoteIndex(since));
    }

    [Fact]
    public void NewGame_HasNoEpilogue_AndExposesNoText()
    {
        var s = TestSessions.Create(_temp.Path, new ManualClock(T0));
        s.StartNewGame(42UL);
        var e = s.GetEpilogue();
        Assert.False(e.Available);
        Assert.Null(e.JournalEntry); Assert.Null(e.CurrentNote);
        Assert.Empty(e.ClosingLetters); Assert.Empty(e.ValleyNotes);
    }

    [Fact]
    public void AfterCompletion_TheEpilogueRevealsGently_NeverShrinks_AndIsReadOnly()
    {
        var content = Region1Catalog.Create();
        var s = TestSessions.Create(_temp.Path, new ManualClock(T0));
        s.StartNewGame(42UL);
        DateTimeOffset? completedAt = null;
        int day = 0;
        for (day = 1; day <= 250 && completedAt == null; day++)
        {
            var end = T0.AddDays(day);
            var d = TestSessions.Create(_temp.Path, new ManualClock(end));
            Assert.Equal(StartStatus.Loaded, d.Continue().Status);
            d.IngestFromSource(new SyntheticWalkingSource(20_000L), end.AddDays(-1), end);
            var home = d.GetHome();
            if (home.ActiveProjectId == null && home.Queued.Count == 0)
                foreach (var id in content.Projects.Select(p => p.Id.Value))
                    if (d.EnqueueProject(id).IsSuccess) break;
            if (d.GetPendingReturnSummary() != null) d.AcknowledgeReturnSummary();
            if (!d.GetEpilogue().Available) continue;
            completedAt = d.GetRegion().RegionCompletedAtUtc;
        }
        Assert.NotNull(completedAt);

        int prevLetters = 0, prevNotes = 0;
        foreach (var after in new[] { 0, 3, 7, 29, 30, 95, 240, 400, 1000 })
        {
            var d = TestSessions.Create(_temp.Path, new ManualClock(completedAt!.Value.AddDays(after).AddHours(1)));
            Assert.Equal(StartStatus.Loaded, d.Continue().Status);
            var files = System.IO.Directory.GetFiles(_temp.Path).OrderBy(f => f).ToArray();
            var before = files.Select(System.IO.File.ReadAllBytes).ToArray();
            var e = d.GetEpilogue();
            for (int i = 0; i < files.Length; i++) Assert.Equal(before[i], System.IO.File.ReadAllBytes(files[i]));

            Assert.True(e.Available);
            Assert.Equal(Region1Epilogue.JournalEntry.Body, e.JournalEntry);
            Assert.Equal(after >= 7 ? 2 : 1, e.ClosingLetters.Count);
            Assert.Equal(Math.Min(8, 1 + after / 30), e.ValleyNotes.Count);
            Assert.Equal(Region1Epilogue.ValleyNotes[(after / 30) % 8].Body, e.CurrentNote);
            Assert.True(e.ClosingLetters.Count >= prevLetters && e.ValleyNotes.Count >= prevNotes, "nothing shown ever disappears");
            prevLetters = e.ClosingLetters.Count; prevNotes = e.ValleyNotes.Count;
        }
    }
}
