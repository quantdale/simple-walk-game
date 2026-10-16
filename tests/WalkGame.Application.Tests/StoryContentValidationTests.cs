using System.Collections.Generic;
using System.Linq;
using WalkGame.Application.Content;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Story;
using WalkGame.Domain.Validation;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>D-076 authoring rules: the validator must reject every broken chapter shape.</summary>
public sealed class StoryContentValidationTests
{
    private static RegionDefinition With(IEnumerable<StoryChapterDefinition> chapters)
    {
        var a = Region1Catalog.Create();
        return new RegionDefinition(a.Id, a.TitleKey, a.Projects, a.Landmarks, a.Producers,
            contentVersion: a.ContentVersion, discoveries: a.Discoveries, expeditions: a.Expeditions,
            ecologyProgression: a.EcologyProgression, settlementProgression: a.SettlementProgression,
            completionMilestoneProjectId: a.CompletionMilestoneProjectId, resourceCaps: a.ResourceCaps,
            tombstones: a.Tombstones.Values, postCompletionPrograms: a.PostCompletionPrograms,
            chains: a.Chains, closureCriterion: a.ClosureCriterion, chapters: chapters);
    }

    private static List<StoryChapterDefinition> Authored() => Region1Catalog.Create().Chapters.ToList();

    private static StoryChapterDefinition Copy(StoryChapterDefinition c, string? id = null, int? number = null,
        string? body = null, string? hint = null, IEnumerable<string>? projects = null, long? care = null) =>
        new(id ?? c.Id, number ?? c.Number, c.TitleKey, body ?? c.BodyKey, hint ?? c.HintKey,
            projects ?? c.RequiredProjectIds, care ?? c.MinCareCycles);

    [Fact]
    public void Authored_region_one_story_is_valid() =>
        Assert.Empty(ContentValidator.Validate(Region1Catalog.Create()));

    [Fact]
    public void Duplicate_ids_are_rejected()
    {
        var ch = Authored();
        ch[2] = Copy(ch[2], id: ch[1].Id);
        Assert.Contains(ContentValidator.Validate(With(ch)), v => v.Contains("Duplicate chapter ID"));
    }

    [Fact]
    public void Gapped_numbering_is_rejected()
    {
        var ch = Authored();
        ch[3] = Copy(ch[3], number: 7);
        Assert.Contains(ContentValidator.Validate(With(ch)), v => v.Contains("numbered contiguously"));
    }

    [Fact]
    public void Overlong_bodies_are_rejected()
    {
        var ch = Authored();
        ch[1] = Copy(ch[1], body: new string('x', StoryChapterDefinition.MaxBodyLength + 1));
        Assert.Contains(ContentValidator.Validate(With(ch)), v => v.Contains("glance budget"));
    }

    [Fact]
    public void Free_chapters_after_the_prologue_are_rejected()
    {
        var ch = Authored();
        ch[2] = Copy(ch[2], projects: new string[0], care: 0);
        Assert.Contains(ContentValidator.Validate(With(ch)), v => v.Contains("only the prologue may be free"));
    }

    [Fact]
    public void Missing_hints_and_unknown_projects_are_rejected()
    {
        var ch = Authored();
        ch[1] = Copy(ch[1], hint: " ");
        ch[2] = Copy(ch[2], projects: new[] { "proj.does-not-exist" });
        var v = ContentValidator.Validate(With(ch));
        Assert.Contains(v, x => x.Contains("needs a hint"));
        Assert.Contains(v, x => x.Contains("missing project 'proj.does-not-exist'"));
    }

    [Fact]
    public void A_story_without_a_closure_chapter_is_rejected()
    {
        var ch = Authored().Where(c => !c.RequiredProjectIds.Contains(Region1Catalog.Create().CompletionMilestoneProjectId!)).ToList();
        for (int i = 0; i < ch.Count; i++) ch[i] = Copy(ch[i], number: i);
        Assert.NotEmpty(ContentValidator.Validate(With(ch)));
    }

    [Fact]
    public void No_chapters_at_all_stays_valid_for_older_fixtures() =>
        Assert.Empty(ContentValidator.Validate(With(new StoryChapterDefinition[0])));
}
