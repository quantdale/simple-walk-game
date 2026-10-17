using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalkGame.Application.Persistence;
using WalkGame.Application.Presentation;
using WalkGame.Application.ReadModels;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-180 (KI-028): presentation memory records which completions were shown, so a completion stamped
/// earlier than the last shown instant (device clock corrected backward) is still replayed exactly once,
/// across restarts. Old (v1) memory files load and fall back to the timestamp mark.
/// </summary>
public sealed class ClockRollbackReplayTests : IDisposable
{
    private const string Trailhead = "proj.clear-trailhead";
    private const string Bridge = "proj.rebuild-trail-bridges";
    private static readonly DateTimeOffset T0 = TestSessions.T0;
    private readonly TempDirectory _temp = new();
    private readonly ManualClock _clock = new(T0.AddDays(2));
    private int _tx;
    public void Dispose() => _temp.Dispose();

    private GameSession Session() =>
        new GameSession(new AtomicFileSaveStore(_temp.Path), TestSessions.NewCodec(), _clock,
            WalkGame.Application.Content.Region1Catalog.Create(), new LocalPreferencesStore(_temp.Path), null,
            new LocalPresentationMemoryStore(_temp.Path));

    private GameSession Reopened()
    {
        var s = Session();
        Assert.Equal(StartStatus.Loaded, s.Continue().Status);
        return s;
    }

    private void Complete(GameSession s, string projectId)
    {
        Assert.True(s.EnqueueProject(projectId).IsSuccess);
        s.CreditActivity(new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)(++_tx)), _clock.UtcNow, 100_000, "test");
    }

    /// <summary>Opens the world: what is unshown is "played" and recorded. Returns what was played.</summary>
    private static List<VisualChangeEvent> Open(GameSession s)
    {
        var events = s.GetUnshownVisualChanges().ToList();
        Assert.Equal(PresentationMemorySaveStatus.Saved, s.MarkVisualChangesShown(events));
        return events;
    }

    private GameSession StartedWithBaseline()
    {
        var s = Session();
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(7UL).Status);
        Assert.Empty(s.GetUnshownVisualChanges()); // never opened: baseline, nothing to replay
        Assert.Equal(PresentationMemorySaveStatus.Saved, s.MarkAllVisualChangesShown());
        return s;
    }

    [Fact]
    public void ClockCorrectedBackward_EveryChangeIsReplayedExactlyOnce()
    {
        var s = StartedWithBaseline();
        Complete(s, Trailhead);                               // stamped day 2
        var first = Open(s);
        Assert.All(first, e => Assert.Equal(Trailhead, e.CompletionId));

        _clock.Set(T0.AddDays(1));                            // device clock corrected backward
        Complete(s, Bridge);                                  // stamped day 1, before the shown mark
        var second = Open(s);
        Assert.NotEmpty(second);
        Assert.All(second, e => Assert.Equal(Bridge, e.CompletionId));
        Assert.Equal(VisualChangeKind.ProjectCompleted, second[0].Kind);
        Assert.Equal(Bridge, second[0].SubjectId);

        var all = s.GetVisualChangesSince(DateTimeOffset.MinValue);
        // The KI-028 probe saw 8 events, because the regressed stamp sorted the bridge before the
        // trailhead it depends on. In causal order the stream is 9: Mira's line advances 1 -> 2 for the
        // trailhead and 2 -> 3 for the bridge. With the old timestamp rule, 0 of the bridge's events replayed.
        Assert.Equal(9, all.Count);
        Assert.Empty(PresentationReplay.Unshown(all, null, first.Max(e => e.AtUtc)));
        Assert.Equal(all.Count, first.Count + second.Count);  // every event played ...
        Assert.Equal(all.Select(e => e.Sequence).OrderBy(n => n), first.Concat(second).Select(e => e.Sequence).OrderBy(n => n)); // ... exactly once
        Assert.Empty(s.GetUnshownVisualChanges());
        Assert.Empty(Reopened().GetUnshownVisualChanges());   // and never again after a restart
    }

    [Fact]
    public void Stream_NeverPlacesAProjectBeforeItsPrerequisite_EvenWithRegressedStamps()
    {
        var s = StartedWithBaseline();
        Complete(s, Trailhead);
        _clock.Set(T0.AddDays(1));
        Complete(s, Bridge);
        var ids = PresentationReplay.CompletionIdsOf(s.GetVisualChangesSince(DateTimeOffset.MinValue));
        Assert.Equal(new[] { Trailhead, Bridge }, ids);
    }

    [Fact]
    public void ChangesWhileClosed_AreReplayedOnceAfterColdRestart_ThenNever()
    {
        var s = StartedWithBaseline();
        Complete(s, Trailhead);
        Open(s);
        _clock.Set(T0.AddDays(1));
        Complete(s, Bridge);                                  // app "closed" before the world opened

        var cold = Reopened();
        var played = Open(cold);
        Assert.Equal(new[] { Bridge }, PresentationReplay.CompletionIdsOf(played));
        Assert.Empty(Open(Reopened()));
        Assert.Empty(Open(Reopened()));                       // idempotent across repeated restarts
    }

    [Fact]
    public void ShownCompletions_ArePrunedToTheGamesCompletions_AndBounded()
    {
        var s = StartedWithBaseline();
        Complete(s, Trailhead);
        var junk = Enumerable.Range(0, 2000).Select(i => "proj.unknown-" + i).Append(Trailhead).Append("").Append(null!);
        Assert.Equal(PresentationMemorySaveStatus.Saved, s.SavePresentationMemory(_clock.UtcNow, null, junk));
        Assert.Equal(new[] { Trailhead }, Reopened().GetPresentationMemory().ShownCompletionIds);
    }

    [Fact]
    public void OldV1File_LoadsAndDerivesShownCompletionsFromItsMark_ThenUpgradesOnWrite()
    {
        var s = StartedWithBaseline();
        Complete(s, Trailhead);
        var created = s.GetPresentationMemory().GameCreatedAtUtc!.Value;
        var file = _temp.FilePath(LocalPresentationMemoryStore.DefaultFileName);
        File.WriteAllText(file, "{\"schemaVersion\":1,\"gameCreatedAtUtc\":\"" + created.ToString("o")
            + "\",\"changesShownThroughUtc\":\"" + _clock.UtcNow.ToString("o") + "\",\"seenLineIds\":[\"line.a\"]}");

        var cold = Reopened();
        Assert.Equal(PresentationMemoryLoadOutcome.Success, cold.PresentationMemoryLoadOutcome);
        var m = cold.GetPresentationMemory();
        Assert.Equal(new[] { Trailhead }, m.ShownCompletionIds); // derived: the trailhead was at or before the mark
        Assert.Equal(new[] { "line.a" }, m.SeenLineIds);
        Assert.Empty(cold.GetUnshownVisualChanges());

        _clock.Set(T0.AddDays(1));
        Complete(cold, Bridge);                               // earlier stamp than the v1 mark: still unshown
        Assert.Equal(new[] { Bridge }, PresentationReplay.CompletionIdsOf(Open(cold)));
        Assert.Contains("\"schemaVersion\":2", File.ReadAllText(file));
        Assert.Contains("\"shownCompletionIds\"", File.ReadAllText(file));
        Assert.Empty(Reopened().GetUnshownVisualChanges());
    }

    [Fact]
    public void DeleteAllLocalData_ErasesShownCompletions_AndTheNextGameStartsFromABaseline()
    {
        var s = StartedWithBaseline();
        Complete(s, Trailhead);
        Open(s);
        Assert.Equal(LocalDataEraseStatus.Erased, s.DeleteAllLocalData().Status);
        Assert.False(File.Exists(_temp.FilePath(LocalPresentationMemoryStore.DefaultFileName)));
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(8UL).Status);
        var m = s.GetPresentationMemory();
        Assert.Null(m.ChangesShownThroughUtc);
        Assert.Empty(m.ShownCompletionIds!);
        Assert.Empty(s.GetUnshownVisualChanges());
    }

    [Fact]
    public void Replay_ReadsNeverWriteTheSave()
    {
        var s = StartedWithBaseline();
        Complete(s, Trailhead);
        var save = File.ReadAllBytes(_temp.FilePath("save.json"));
        Open(s);
        Assert.Equal(save, File.ReadAllBytes(_temp.FilePath("save.json")));
        var cold = Reopened();
        save = File.ReadAllBytes(_temp.FilePath("save.json"));
        Open(cold);
        cold.MarkAllVisualChangesShown();
        Assert.Equal(save, File.ReadAllBytes(_temp.FilePath("save.json")));
    }
}
