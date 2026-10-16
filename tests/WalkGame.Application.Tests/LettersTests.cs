using System;
using System.Linq;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Time;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>D-118: Nell's letters are a pure, monotonic, spoiler-free projection of project completion.</summary>
public sealed class LettersTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private static readonly string[] Banned =
        { "missed", "streak", "hurry up", "don't forget", "falling behind", "decay", "expire", "penalty", "you lost", "too late", "disappointed", "fault" };

    [Fact]
    public void EveryLetter_IsTiedToARealRegion1Project_InOrder_WithCalmCopy()
    {
        var projects = Region1Catalog.Create().Projects.Select(p => p.Id.Value).ToHashSet();
        Assert.Equal(6, Region1Letters.All.Count);
        Assert.Equal(Region1Letters.All.Count, Region1Letters.All.Select(l => l.Id).Distinct().Count());
        Assert.Equal(Region1Letters.All.Count, Region1Letters.All.Select(l => l.ProjectId).Distinct().Count());
        Assert.Equal(Enumerable.Range(1, Region1Letters.All.Count), Region1Letters.All.Select(l => l.Order));
        foreach (var l in Region1Letters.All)
        {
            Assert.Contains(l.ProjectId, projects);
            Assert.InRange(l.Body.Length, 60, Region1Letters.MaxBodyLength);
            Assert.False(string.IsNullOrWhiteSpace(l.PlaceKey));
            Assert.DoesNotContain("proj.", l.Body);
            Assert.DoesNotContain("\"", l.Body);
            foreach (var bad in Banned) Assert.DoesNotContain(bad, l.Body.ToLowerInvariant());
        }
    }

    [Fact]
    public void NewGame_HasNoFoundLetters_AndExposesNoText()
    {
        var s = TestSessions.Create(_temp.Path, new ManualClock(T0));
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(42UL).Status);
        var letters = s.GetLetters();
        Assert.Equal(6, letters.TotalLetters);
        Assert.Equal(0, letters.FoundCount);
        Assert.All(letters.Letters, l => { Assert.Null(l.Body); Assert.Null(l.FoundAtUtc); Assert.False(l.Found); });
    }

    [Fact]
    public void Letters_AreFoundExactlyWhenTheirProjectCompletes_NeverUnfound_AndSurviveReload()
    {
        var content = Region1Catalog.Create();
        var s = TestSessions.Create(_temp.Path, new ManualClock(T0));
        s.StartNewGame(42UL);
        int previous = 0;
        for (int day = 1; day <= 120; day++)
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

            var letters = d.GetLetters();
            var completed = d.GetProjects().Projects
                .Where(p => p.Status == WalkGame.Domain.Projects.ProjectStatus.Completed).Select(p => p.ProjectId).ToHashSet();
            foreach (var row in letters.Letters)
            {
                var def = Region1Letters.All.Single(l => l.Id == row.LetterId);
                Assert.Equal(completed.Contains(def.ProjectId), row.Found);
                Assert.Equal(row.Found ? def.Body : null, row.Body);
                if (row.Found) Assert.NotNull(row.FoundAtUtc);
            }
            Assert.True(letters.FoundCount >= previous, "a found letter never becomes unfound");
            previous = letters.FoundCount;
        }
        Assert.True(previous >= 1, "a steady high walker finds at least the first letter within 120 days");
    }
}
