using System;
using System.Linq;
using WalkGame.Application.Development;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Time;

namespace WalkGame.Application.Tests;

/// <summary>
/// The platform-neutral reconcile path: synthetic records from an IActivityRecordSource
/// enter the SAME IngestActivityBatch trust pipeline production adapters will use.
/// </summary>
public sealed class IngestFromSourceTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private static readonly DateTimeOffset WindowEnd = TestSessions.T0.AddDays(4);
    private static readonly DateTimeOffset WindowStart = WindowEnd.AddDays(-3);

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void SourceWindow_CreditsThroughTrustPipeline_WithPerDayRecords()
    {
        var clock = new ManualClock(WindowEnd);
        var session = TestSessions.Create(_temp, clock);
        Assert.Equal(StartStatus.NewGameCreated, session.StartNewGame(7UL).Status);

        var result = session.IngestFromSource(new SyntheticWalkingSource(20000L), WindowStart, WindowEnd);

        // Three full UTC days inside the window -> three normalized records.
        Assert.Equal(3, result.TotalReceived);
        Assert.Equal(3, result.Accepted);
        Assert.Equal(0, result.Rejected);
        Assert.Equal(600L, result.VitalityCredited); // 3 x 20000/100
        Assert.True(result.Saved);
        Assert.NotNull(result.Summary);

        var home = session.GetHome();
        Assert.Equal(600L, home.Vitality);
    }

    [Fact]
    public void SameSource_ReplayedAfterRestart_IsAnExactlyOnceNoOp()
    {
        var clock = new ManualClock(WindowEnd);
        var first = TestSessions.Create(_temp, clock);
        Assert.Equal(StartStatus.NewGameCreated, first.StartNewGame(7UL).Status);
        var original = first.IngestFromSource(new SyntheticWalkingSource(20000L), WindowStart, WindowEnd);
        int summaryItemsBefore = first.GetPendingReturnSummary()!.Items.Count;

        // Recreate the session from disk, replay the identical source window.
        var reloaded = TestSessions.Create(_temp, clock);
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);
        var replay = reloaded.IngestFromSource(new SyntheticWalkingSource(20000L), WindowStart, WindowEnd);

        Assert.Equal(original.Accepted, replay.DuplicatesIgnored);
        Assert.Equal(0, replay.Accepted);
        Assert.Equal(0L, replay.VitalityCredited);
        Assert.Equal(summaryItemsBefore, reloaded.GetPendingReturnSummary()!.Items.Count);
        Assert.Equal(600L, reloaded.GetHome().Vitality);
    }

    [Fact]
    public void DevInjector_NamespaceIsExplicitlyDevelopmentOnly()
    {
        var source = new SyntheticWalkingSource(1000L);

        Assert.Equal("dev.synthetic-walking", source.ProviderNamespace);

        var records = source.FetchRecords(WindowStart, WindowStart.AddDays(2));
        Assert.All(records, r => Assert.Equal("dev.synthetic-walking", r.ProviderNamespace));
        Assert.All(records, r => Assert.StartsWith("walk.", r.SourceRecordId!, StringComparison.Ordinal));
    }

    [Fact]
    public void PartialEdgeDays_AreNotFabricated()
    {
        var source = new SyntheticWalkingSource(1000L);

        // A window of less than one full day produces zero records.
        Assert.Empty(source.FetchRecords(WindowStart, WindowStart.AddHours(23)));

        // Exactly one day produces exactly one record covering [start, start+1d).
        var one = Assert.Single(source.FetchRecords(WindowStart, WindowStart.AddDays(1)));
        Assert.Equal(WindowStart, one.StartUtc);
        Assert.Equal(WindowStart.AddDays(1), one.EndUtc);
        Assert.Equal(1000L, one.Quantity);
        Assert.Equal(ActivityCategory.Walking, one.Category);
    }
}
