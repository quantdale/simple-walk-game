using System;
using System.Linq;
using WalkGame.Application.Content;
using WalkGame.Domain.Projects;
using Xunit;
using ProjectId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProjectIdKind>;

namespace WalkGame.Application.Tests;

/// <summary>D-071: visible build stages are derived presentation data on unchanged seed definitions.</summary>
public sealed class VisibleBuildStageTests
{
    [Theory]
    [InlineData(0L, 0)]
    [InlineData(799L, 0)]
    [InlineData(800L, 1)]
    [InlineData(2_400L, 3)]
    [InlineData(3_999L, 4)]
    [InlineData(4_000L, 4)] // completion is shown by the Completed status, not a stage
    [InlineData(9_000L, 4)]
    public void Stage_is_the_invested_share_clamped_below_completion(long invested, int expected)
    {
        var d = new ProjectDefinition(new ProjectId("proj.t"), "T", 4_000L, visibleBuildStages: 5);
        Assert.Equal(expected, d.BuildStageFor(invested));
    }

    [Fact]
    public void Default_definition_has_no_visible_stages()
    {
        var d = new ProjectDefinition(new ProjectId("proj.t"), "T", 4_000L);
        Assert.Equal(1, d.VisibleBuildStages);
        Assert.Equal(0, d.BuildStageFor(3_000L));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void Out_of_range_stage_count_is_an_authoring_error(int n) =>
        Assert.Throws<ArgumentException>(() => new ProjectDefinition(new ProjectId("proj.t"), "T", 10L, visibleBuildStages: n));

    [Theory]
    [InlineData("proj.build-workshop", 1_500L, 2)]
    [InlineData("proj.wetland-drainage", 2_200L, 3)]
    [InlineData("proj.canopy-walkway", 4_000L, 5)]
    public void Long_seed_projects_author_stages_without_changing_cost(string id, long cost, int stages)
    {
        var p = Region1Catalog.Create().Projects.Single(x => x.Id.Value == id);
        Assert.Equal(cost, p.VitalityCost);
        Assert.Equal(stages, p.VisibleBuildStages);
        Assert.True(p.VitalityCost / p.VisibleBuildStages <= 800L);
    }

    [Fact]
    public void Content_version_is_five_after_the_storyline() => Assert.Equal(5L, Region1Catalog.Create().ContentVersion);
}
