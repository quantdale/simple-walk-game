using System;
using System.Collections.Generic;
using System.Linq;
using WalkGame.Application.Content;
using WalkGame.Application.ReadModels;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>D-165: the presentation bindings (locations, characters, lines) agree with the authored content.</summary>
public sealed class WorldPresentationContentTests
{
    private static readonly string[] Banned =
        { "missed", "streak", "hurry", "don't forget", "falling behind", "decay", "expire", "penalty", "you lost", "too late", "disappointed", "fault", "you must", "should have" };

    [Fact]
    public void EveryChain_IsBoundToARealLandmark_AndEveryProjectHasExactlyOneLocation()
    {
        var content = Region1Catalog.Create();
        var landmarks = content.Landmarks.Select(l => l.Id.Value).ToHashSet();
        Assert.Equal(content.Chains.Select(c => c.Id).OrderBy(x => x), Region1WorldLayout.LocationOfChain.Keys.OrderBy(x => x));
        Assert.All(Region1WorldLayout.LocationOfChain.Values, l => Assert.Contains(l, landmarks));
        Assert.Equal(landmarks.OrderBy(x => x), Region1WorldLayout.LocationOfChain.Values.Distinct().OrderBy(x => x));

        var memberships = content.Chains.SelectMany(c => c.ProjectIds).GroupBy(p => p).ToDictionary(g => g.Key, g => g.Count());
        var locationOf = WorldPresentationProjector.LocationOfProject(content);
        foreach (var p in content.Projects)
        {
            Assert.True(memberships.TryGetValue(p.Id.Value, out var n) && n == 1, p.Id.Value + " must be in exactly one chain");
            Assert.True(locationOf.ContainsKey(p.Id.Value), p.Id.Value + " has no location");
        }
    }

    [Fact]
    public void Characters_ReferenceRealProjectsAndPlaces_WithShortCalmLines()
    {
        var content = Region1Catalog.Create();
        var projects = content.Projects.Select(p => p.Id.Value).ToHashSet();
        var landmarks = content.Landmarks.Select(l => l.Id.Value).ToHashSet();
        var lineIds = new HashSet<string>(StringComparer.Ordinal);
        Assert.NotEmpty(Region1Characters.All);
        Assert.Equal(Region1Characters.All.Count, Region1Characters.All.Select(c => c.Id).Distinct().Count());
        foreach (var c in Region1Characters.All)
        {
            Assert.StartsWith("npc.", c.Id);
            Assert.False(string.IsNullOrWhiteSpace(c.Name));
            Assert.Equal("portrait." + c.Id, c.PortraitKey);
            Assert.NotEmpty(c.Appearances);
            foreach (var a in c.Appearances)
            {
                Assert.Contains(a.LocationId, landmarks);
                if (a.AfterProjectId != null) Assert.Contains(a.AfterProjectId, projects);
            }
            Assert.NotEmpty(c.Lines);
            Assert.Equal(c.Lines.Select(l => l.Order).OrderBy(o => o), c.Lines.Select(l => l.Order));
            Assert.Equal(c.Lines.Count, c.Lines.Select(l => l.Order).Distinct().Count());
            foreach (var l in c.Lines)
            {
                Assert.True(lineIds.Add(l.Id), "duplicate line id " + l.Id);
                Assert.StartsWith("line.", l.Id);
                if (l.MilestoneProjectId != null) Assert.Contains(l.MilestoneProjectId, projects);
                Assert.Contains(l.Expression, Region1Characters.Expressions);
                Assert.InRange(l.Text.Length, 20, Region1Characters.MaxLineLength);
                Assert.DoesNotContain("proj.", l.Text);
                Assert.DoesNotContain("{", l.Text);
                foreach (var bad in Banned) Assert.DoesNotContain(bad, l.Text.ToLowerInvariant());
            }
        }
    }

    [Fact]
    public void Cast_FollowsTheD172Reconciliation()
    {
        // D-172: on-screen cast from the bible's §12 candidate list; Nell stays the off-screen
        // letter writer and Arlo stays a figure from her letters (neither is an on-screen character).
        Assert.Equal(new[] { "npc.mira", "npc.tavi", "npc.rowan", "npc.eli" }, Region1Characters.All.Select(c => c.Id));
        Assert.Equal(new[] { "Mira", "Tavi", "Rowan", "Eli" }, Region1Characters.All.Select(c => c.Name));
        Assert.DoesNotContain(Region1Characters.All, c => c.Name is "Nell" or "Arlo" or "Wren" or "Ida");
        Assert.Equal("Nell Hartley", Region1Letters.Writer);
        Assert.Contains(Region1Letters.All, l => l.Body.Contains("Arlo"));

        string? At(string id, params string[] done) =>
            WorldPresentationProjector.CharacterLocation(Region1Characters.Find(id)!, done.Contains);
        Assert.Equal("lm.trailhead", At("npc.mira"));
        Assert.Null(At("npc.tavi"));
        Assert.Equal("lm.river-intake", At("npc.tavi", "proj.clear-trailhead"));
        Assert.Equal("lm.settlement", At("npc.tavi", "proj.clear-trailhead", "proj.build-workshop"));
        Assert.Equal("lm.canopy", At("npc.rowan", "proj.clear-fallen-timber"));
        Assert.Equal("lm.wetland", At("npc.rowan", "proj.clear-fallen-timber", "proj.wetland-drainage"));
        Assert.Equal("lm.canopy", At("npc.rowan", "proj.clear-fallen-timber", "proj.wetland-drainage", "proj.canopy-walkway"));
        Assert.Null(At("npc.eli", "proj.clear-trailhead"));
        Assert.Equal("lm.observatory", At("npc.eli", "proj.open-lookout"));
        // Tavi was Arlo's apprentice: the workshop line ties the letters to the on-screen cast.
        Assert.Contains("Arlo", Region1Characters.Find("npc.tavi")!.Lines.Single(l => l.MilestoneProjectId == "proj.build-workshop").Text);
    }

    [Fact]
    public void EveryLine_IsReachable_WhenItsCharacterIsPresent()
    {
        // A line keyed to a milestone that completes before its character arrives is still
        // reached once they arrive; a line can never be stranded forever.
        var all = Region1Catalog.Create().Projects.Select(p => p.Id.Value).ToHashSet();
        foreach (var c in Region1Characters.All)
        {
            Assert.NotNull(WorldPresentationProjector.CharacterLocation(c, all.Contains));
            Assert.Equal(c.Lines.Count, WorldPresentationProjector.ReachedLines(c, all.Contains).Count);
            Assert.DoesNotContain(WorldPresentationProjector.ReachedLines(c, _ => false), l => l.MilestoneProjectId != null);
        }
    }

    [Fact]
    public void Keys_AreStyleNeutralAndStable()
    {
        Assert.Equal("lm.wetland@flourishing", WorldViewReadModel.VariantKeyFor("lm.wetland", WalkGame.Domain.Regions.RestorationStage.Flourishing));
        Assert.Equal("lm.trailhead@ruined", WorldViewReadModel.VariantKeyFor("lm.trailhead", WalkGame.Domain.Regions.RestorationStage.Ruined));
        Assert.Equal("proj.canopy-walkway@build-2of4", WorldViewReadModel.ConstructionKeyFor("proj.canopy-walkway", 2, 4));
        Assert.Equal("region@eco2-set1", WorldViewReadModel.AtmosphereKeyFor(2, 1));
    }
}
