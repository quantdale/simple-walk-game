using System;
using System.Collections.Generic;
using System.Linq;
using WalkGame.Application.Content;
using WalkGame.Application.ReadModels;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Projects;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>D-167: every restoration-choice preview tells the truth about what completing the project does.</summary>
public sealed class RestorationChoicePreviewTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private sealed record Snapshot(
        Dictionary<string, string> Variants, Dictionary<string, ProjectStatus> Status, HashSet<string> Producers,
        int Eco, int Set, int Discoveries, HashSet<string> Chapters, Dictionary<string, string?> CharacterAt,
        Dictionary<string, string?> CurrentLine);

    private static Snapshot Take(GameSession s)
    {
        var view = s.GetWorldView();
        var region = s.GetRegion();
        var chars = s.GetCharacters();
        return new Snapshot(
            view.Locations.ToDictionary(l => l.LocationId, l => l.VariantKey),
            s.GetProjects().Projects.ToDictionary(p => p.ProjectId, p => p.Status),
            region.Producers.Where(p => p.Unlocked).Select(p => p.ProducerId).ToHashSet(),
            region.EcologyStage, region.SettlementStage, region.DiscoveriesUnlocked,
            s.GetStory().Chapters.Where(c => c.Unlocked && c.MinCareCycles == 0).Select(c => c.ChapterId).ToHashSet(),
            chars.ToDictionary(c => c.CharacterId, c => c.LocationId),
            chars.ToDictionary(c => c.CharacterId, c => c.CurrentLine?.LineId));
    }

    [Fact]
    public void FreshValley_OffersTheTrailhead_WithItsRealConsequences()
    {
        var h = new WorldChangeHarness(_temp.Path, 8_000L);
        var choices = h.Session.GetRestorationChoices();
        var c = Assert.Single(choices.Choices);
        Assert.Equal(TestSessions.EntryProjectId, c.ProjectId);
        Assert.Equal("lm.trailhead", c.LocationId);
        Assert.Equal("lm.trailhead@ruined", c.BeforeVariantKey);
        Assert.Equal("lm.trailhead@stabilized", c.AfterVariantKey);
        Assert.False(c.IsClosureMilestone);
        Assert.Contains(c.Consequences, k => k.Kind == ConsequenceKind.LandmarkStage && k.SubjectId == "lm.trailhead");
        Assert.Contains(c.Consequences, k => k.Kind == ConsequenceKind.NewDialogue && k.SubjectId == "npc.mira" && k.ToKey == "line.mira.2");
        // D-172: clearing the trailhead also brings Tavi to the river intake.
        Assert.Contains(c.Consequences, k => k.Kind == ConsequenceKind.CharacterArrives && k.SubjectId == "npc.tavi" && k.LocationId == "lm.river-intake");
        // D-155: a discovery consequence never names the find.
        Assert.All(c.Consequences.Where(k => k.Kind == ConsequenceKind.DiscoveryAtLocation), k => { Assert.Null(k.SubjectId); Assert.Equal("lm.trailhead", k.LocationId); });
    }

    [Fact]
    public void AlongAWholePlaythrough_EveryPreviewEqualsWhatCompletionActuallyChanged()
    {
        var h = new WorldChangeHarness(_temp.Path, 8_000L);
        var verified = new HashSet<string>();
        int completedTotal = 0;
        for (int day = 1; day <= 300 && !h.Session.GetRegion().RegionCompleted; day++)
        {
            var choices = h.Session.GetRestorationChoices();
            var before = Take(h.Session);
            var saveBefore = h.Session.ExportSaveEnvelope()!;
            h.Session.GetRestorationChoices();
            Assert.Equal(saveBefore, h.Session.ExportSaveEnvelope()!); // previews never touch state
            h.PlayDay(day);
            var after = Take(h.Session);

            var newlyDone = after.Status.Where(p => p.Value == ProjectStatus.Completed && before.Status[p.Key] != ProjectStatus.Completed).Select(p => p.Key).ToList();
            completedTotal += newlyDone.Count;
            if (newlyDone.Count != 1) { Assert.Empty(newlyDone); continue; }
            string x = newlyDone[0];
            var preview = choices.Find(x);
            Assert.True(preview != null, $"day {day}: {x} completed without having been a choice");
            var k = preview!.Consequences;

            Set(k, ConsequenceKind.LandmarkStage, c => c.SubjectId + ":" + c.FromKey + ">" + c.ToKey, day, x,
                after.Variants.Where(v => before.Variants[v.Key] != v.Value).Select(v => v.Key + ":" + before.Variants[v.Key] + ">" + v.Value));
            Assert.Equal(after.Variants[preview.LocationId!], preview.AfterVariantKey);
            Assert.Equal(before.Variants[preview.LocationId!], preview.BeforeVariantKey);
            Set(k, ConsequenceKind.ProjectOpens, c => c.SubjectId!, day, x,
                after.Status.Where(p => before.Status[p.Key] == ProjectStatus.Locked && p.Value != ProjectStatus.Locked).Select(p => p.Key));
            Set(k, ConsequenceKind.ProducerUnlocks, c => c.SubjectId!, day, x, after.Producers.Except(before.Producers));
            Set(k, ConsequenceKind.EcologyAdvances, c => c.ToKey!, day, x, after.Eco > before.Eco ? new[] { after.Eco.ToString() } : Array.Empty<string>());
            Set(k, ConsequenceKind.SettlementAdvances, c => c.ToKey!, day, x, after.Set > before.Set ? new[] { after.Set.ToString() } : Array.Empty<string>());
            Assert.Equal(after.Discoveries - before.Discoveries, k.Count(c => c.Kind == ConsequenceKind.DiscoveryAtLocation));
            Set(k, ConsequenceKind.ChapterUnlocks, c => c.SubjectId!, day, x, after.Chapters.Except(before.Chapters));
            Set(k, ConsequenceKind.CharacterArrives, c => c.SubjectId + "@" + c.ToKey, day, x,
                after.CharacterAt.Where(c => before.CharacterAt[c.Key] == null && c.Value != null).Select(c => c.Key + "@" + c.Value));
            Set(k, ConsequenceKind.CharacterMoves, c => c.SubjectId + "@" + c.FromKey + ">" + c.ToKey, day, x,
                after.CharacterAt.Where(c => before.CharacterAt[c.Key] != null && c.Value != before.CharacterAt[c.Key]).Select(c => c.Key + "@" + before.CharacterAt[c.Key] + ">" + c.Value));
            Set(k, ConsequenceKind.NewDialogue, c => c.SubjectId + ":" + c.ToKey, day, x,
                after.CurrentLine.Where(c => c.Value != null && c.Value != before.CurrentLine[c.Key]).Select(c => c.Key + ":" + c.Value));
            Assert.All(k.Where(c => c.Kind == ConsequenceKind.DiscoveryAtLocation), c => Assert.Null(c.SubjectId));
            verified.Add(x);
        }
        var content = Region1Catalog.Create();
        Assert.True(h.Session.GetRegion().RegionCompleted, "the 8k-step walker should close the region within 300 days");
        Assert.Equal(content.Projects.Count, completedTotal);
        Assert.Equal(content.Projects.Count, verified.Count); // every project's preview was checked against its real completion
    }

    [Fact]
    public void OnlyTheMilestone_IsFlaggedAsClosure()
    {
        var h = new WorldChangeHarness(_temp.Path, 20_000L);
        bool sawMilestone = false;
        for (int day = 1; day <= 120 && !h.Session.GetRegion().RegionCompleted; day++)
        {
            foreach (var c in h.Session.GetRestorationChoices().Choices)
            {
                Assert.Equal(c.ProjectId == "proj.complete-valley-survey", c.IsClosureMilestone);
                sawMilestone |= c.IsClosureMilestone;
                Assert.Contains(c.Status, new[] { ProjectStatus.Available, ProjectStatus.Queued, ProjectStatus.Active });
            }
            h.PlayDay(day);
        }
        Assert.True(sawMilestone);
    }

    private static void Set(IReadOnlyList<Consequence> all, ConsequenceKind kind, Func<Consequence, string> key, int day, string x, IEnumerable<string> actual)
    {
        var predicted = all.Where(c => c.Kind == kind).Select(key).OrderBy(s => s, StringComparer.Ordinal).ToList();
        var real = actual.OrderBy(s => s, StringComparer.Ordinal).ToList();
        Assert.True(predicted.SequenceEqual(real),
            $"day {day}, {x}, {kind}: predicted [{string.Join(", ", predicted)}] but completion changed [{string.Join(", ", real)}]");
    }
}
