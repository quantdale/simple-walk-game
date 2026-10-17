using System;
using System.Collections.Generic;
using System.Linq;
using WalkGame.Application.Content;
using WalkGame.Application.ReadModels;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Time;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>D-167: the visual-change stream is derived, ordered, deterministic and never invents a change.</summary>
public sealed class VisualChangeStreamTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private static List<string> Shapes(IEnumerable<VisualChangeEvent> e) =>
        e.Where(x => x.Kind != VisualChangeKind.ConstructionAdvanced).Select(x => x.Shape).OrderBy(s => s, StringComparer.Ordinal).ToList();

    [Fact]
    public void DailyStreamFromTimestamps_EqualsTheInSessionDiff_AndConstructionShowsOnlyInTheDiff()
    {
        var h = new WorldChangeHarness(_temp.Path, 8_000L);
        var view = h.Session.GetWorldView();
        int stageEvents = 0, construction = 0;
        for (int day = 1; day <= 300 && !h.Session.GetRegion().RegionCompleted; day++)
        {
            var since = h.Clock.UtcNow;
            h.PlayDay(day);
            var next = h.Session.GetWorldView();
            var diff = WorldViewDiff.Between(view, next);
            var stream = h.Session.GetVisualChangesSince(since);
            Assert.True(Shapes(stream).SequenceEqual(Shapes(diff)),
                $"day {day}: stream [{string.Join(", ", Shapes(stream))}] vs diff [{string.Join(", ", Shapes(diff))}]");
            Assert.All(stream, e => { Assert.NotNull(e.AtUtc); Assert.True(e.AtUtc!.Value > since); });
            Assert.DoesNotContain(stream, e => e.Kind == VisualChangeKind.ConstructionAdvanced);
            stageEvents += stream.Count(e => e.Kind == VisualChangeKind.LandmarkStageChanged);
            construction += diff.Count(e => e.Kind == VisualChangeKind.ConstructionAdvanced);
            Assert.Empty(WorldViewDiff.Between(next, next));
            view = next;
        }
        Assert.True(h.Session.GetRegion().RegionCompleted);
        var content = Region1Catalog.Create();
        // Each stage definition fires at most once; one that a higher stage already passed is skipped.
        Assert.InRange(stageEvents, content.Landmarks.Count, content.Landmarks.Sum(l => l.Stages.Count));
        Assert.True(construction > 0, "multi-stage projects should show construction advancing in-session");

        // The whole history, ordered and complete, ends in the region completing.
        var all = h.Session.GetVisualChangesSince(DateTimeOffset.MinValue);
        Assert.Equal(Enumerable.Range(0, all.Count), all.Select(e => e.Sequence));
        Assert.True(all.Zip(all.Skip(1), (a, b) => a.AtUtc <= b.AtUtc).All(ok => ok));
        Assert.Equal(VisualChangeKind.RegionCompleted, all.Last().Kind);
        Assert.Equal(1, all.Count(e => e.Kind == VisualChangeKind.RegionCompleted));
        Assert.Equal(stageEvents, all.Count(e => e.Kind == VisualChangeKind.LandmarkStageChanged));
    }

    [Fact]
    public void Stream_IsStableAcrossReload_AndSinceOnlyFilters()
    {
        var h = new WorldChangeHarness(_temp.Path, 20_000L);
        for (int day = 1; day <= 40; day++) h.PlayDay(day);
        var all = h.Session.GetVisualChangesSince(DateTimeOffset.MinValue);
        Assert.NotEmpty(all);
        var cut = WorldChangeHarness.T0.AddDays(20);
        var tail = h.Session.GetVisualChangesSince(cut);
        Assert.Equal(all.Where(e => e.AtUtc > cut).Select(e => e.ToString()), tail.Select(e => e.ToString()));

        var reloaded = TestSessions.Create(_temp.Path, new ManualClock(h.Clock.UtcNow));
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);
        Assert.Equal(all.Select(e => e.ToString()), reloaded.GetVisualChangesSince(DateTimeOffset.MinValue).Select(e => e.ToString()));
        Assert.Empty(reloaded.GetVisualChangesSince(h.Clock.UtcNow));
    }

    [Fact]
    public void AStateTheReplayCannotReproduce_ReportsNothing_AndLegacyUntimedCompletionsAreBaseline()
    {
        var h = new WorldChangeHarness(_temp.Path, 20_000L);
        for (int day = 1; day <= 20; day++) h.PlayDay(day);
        var content = Region1Catalog.Create();
        var decoded = TestSessions.NewCodec().Decode(h.Session.ExportSaveEnvelope()!);
        var state = decoded.State!;
        int full = WorldChangeProjector.ChangesSince(content, state, DateTimeOffset.MinValue).Count;
        Assert.True(full > 0);

        // Legacy: the entry project has no completion time. Its effects become baseline (not emitted);
        // later changes are still reported and the replay still matches canonical state.
        var entry = state.Region.FindProject(TestSessions.EntryProjectId)!;
        Assert.Equal(ProjectStatus.Completed, entry.Status);
        entry.CompletedAtUtc = null;
        var legacy = WorldChangeProjector.ChangesSince(content, state, DateTimeOffset.MinValue);
        Assert.NotEmpty(legacy);
        Assert.True(legacy.Count < full);
        Assert.DoesNotContain(legacy, e => e.SubjectId == TestSessions.EntryProjectId);

        // A stage the completions cannot explain: report nothing rather than invent history.
        state.Region.LandmarkStages["lm.observatory"] = RestorationStage.Flourishing;
        Assert.Empty(WorldChangeProjector.ChangesSince(content, state, DateTimeOffset.MinValue));
    }
}
