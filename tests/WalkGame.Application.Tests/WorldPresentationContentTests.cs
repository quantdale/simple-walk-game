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
