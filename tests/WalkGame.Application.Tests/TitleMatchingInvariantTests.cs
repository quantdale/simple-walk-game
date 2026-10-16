using System.Linq;
using WalkGame.Application.Content;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-102 hardening: summary items carry text only, so the shell links them back to content by
/// title (RegionScene moment rings by substring, D-100 completion lines by exact project title).
/// These invariants keep that matching unambiguous for the authored Region 1 content.
/// </summary>
public sealed class TitleMatchingInvariantTests
{
    [Fact]
    public void ProjectTitles_AreUnique()
    {
        var titles = Region1Catalog.Create().Projects.Select(p => p.TitleKey).ToList();
        Assert.Equal(titles.Count, titles.Distinct().Count());
    }

    [Fact]
    public void LandmarkTitles_AreUnique_AndNoneContainsAnother()
    {
        var titles = Region1Catalog.Create().Landmarks.Select(l => l.TitleKey).ToList();
        Assert.Equal(titles.Count, titles.Distinct().Count());
        foreach (var a in titles)
            foreach (var b in titles)
                if (a != b) Assert.False(a.Contains(b), $"landmark title '{a}' contains '{b}'; moment rings would mislink");
    }

    [Fact]
    public void NoProjectTitle_ContainsALandmarkTitle_OfADifferentLandmark()
    {
        // A project completion line naming a landmark would ring that landmark in the picture.
        // Allowed only when the project actually raises that landmark's stage.
        var content = Region1Catalog.Create();
        foreach (var p in content.Projects)
            foreach (var lm in content.Landmarks)
                if (p.TitleKey.Contains(lm.TitleKey))
                    Assert.Contains(lm.Stages, s => s.UnlockedByProjectId == p.Id.Value);
    }
}
