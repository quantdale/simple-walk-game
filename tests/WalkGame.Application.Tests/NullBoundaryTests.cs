using System;
using System.Collections.Generic;
using WalkGame.Application.Activity;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-130: null-input scan of the GameSession string/collection entry points. A null id or a
/// provider that returns a null batch must produce a calm DomainResult or an empty ingest,
/// never a NullReferenceException/ArgumentNullException from inside the domain.
/// </summary>
public sealed class NullBoundaryTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ManualClock _clock = new(TestSessions.T0);
    public void Dispose() => _temp.Dispose();

    private GameSession Started()
    {
        var s = TestSessions.Create(_temp.Path, _clock, new LocalPreferencesStore(_temp.Path), new FakeActivityConnectionPort());
        s.StartNewGame(7UL);
        return s;
    }

    [Fact] public void EnqueueNull_FailsCalmly() => Assert.False(Started().EnqueueProject(null!).IsSuccess);
    [Fact] public void DequeueNull_FailsCalmly() => Assert.False(Started().DequeueProject(null!).IsSuccess);
    [Fact] public void ActivateNull_FailsCalmly() => Assert.False(Started().ActivateQueuedProject(null!).IsSuccess);
    [Fact] public void MarkReviewedNull_FailsCalmly() => Assert.False(Started().MarkDiscoveryReviewed(null!).IsSuccess);
    [Fact] public void DiscoveryCopyNull_ReturnsNull() => Assert.Null(Started().DiscoveryCopy(null!, null!, null!));

    private sealed class NullBatchSource : IActivityRecordSource
    {
        public string ProviderNamespace => "test.nullbatch";
        public IReadOnlyList<NormalizedActivityRecord> FetchRecords(DateTimeOffset a, DateTimeOffset b) => null!;
    }

    [Fact]
    public void NullBatchFromProvider_IngestsNothing()
    {
        var s = Started();
        var before = s.GetHome().Vitality;
        s.IngestFromSource(new NullBatchSource(), TestSessions.T0.AddDays(-1), TestSessions.T0);
        Assert.Equal(before, s.GetHome().Vitality);
    }
}
