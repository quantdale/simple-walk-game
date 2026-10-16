using System.Collections.Generic;
using System.Linq;
using WalkGame.Application.Content;
using WalkGame.Application.Summaries;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>D-100: project-completion moments name the landmark stage and the storyline chapter.</summary>
public sealed class ProjectCompletionMomentsTests
{
    private static readonly string[] Guilt = { "streak", "miss", "behind", "hurry", "should", "lose", "lost", "late", "!" };

    [Fact]
    public void Trailhead_NamesLandmarkStage_AndChapterAhead()
    {
        var line = ProjectCompletionMoments.Describe(Region1Catalog.Create(), "Clear the old trailhead is complete.");
        Assert.Equal("Old Trailhead is now stabilized. The story moves toward \"The Way In\".", line);
    }

    [Fact]
    public void UnlockedChapter_IsNamedAsOpen()
    {
        var line = ProjectCompletionMoments.Describe(Region1Catalog.Create(), "Clear the old trailhead is complete.",
            new HashSet<string> { "story.way-in" });
        Assert.EndsWith("It is part of the chapter \"The Way In\".", line);
    }

    [Theory]
    [InlineData("New chapter: The Way In.")]
    [InlineData("3 restoration projects are complete.")]
    [InlineData("Millbrook Valley's restoration is complete.")]
    [InlineData("Unknown thing is complete.")]
    [InlineData("")]
    public void NonProjectItems_GetNoLine(string text) =>
        Assert.Null(ProjectCompletionMoments.Describe(Region1Catalog.Create(), text));

    [Fact]
    public void EveryProject_LinkedToALandmarkOrChapter_GetsCalmBoundedCopy()
    {
        var content = Region1Catalog.Create();
        int described = 0;
        foreach (var p in content.Projects)
        {
            var line = ProjectCompletionMoments.Describe(content, p.TitleKey + " is complete.");
            if (line == null) continue;
            described++;
            Assert.True(line.Length <= 200, p.Id.Value + ": " + line);
            Assert.DoesNotContain("proj.", line);
            Assert.DoesNotContain("lm.", line);
            foreach (var word in Guilt)
                Assert.DoesNotContain(word, line.ToLowerInvariant());
        }
        Assert.True(described >= content.Landmarks.Sum(l => l.Stages.Count) / 2, "too few projects described: " + described);
    }
}
