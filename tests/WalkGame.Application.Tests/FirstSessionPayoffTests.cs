using System;
using WalkGame.Application.Pacing;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Time;

namespace WalkGame.Application.Tests;

/// <summary>D-145: very short sessions pay off. Display-time progress derived from canonical state.</summary>
public sealed class FirstSessionPayoffTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Theory]
    [InlineData(1L, 300L, 1, 1, 299L, false)]     // 100 steps: never reads as 0%
    [InlineData(3L, 300L, 1, 1, 297L, false)]     // 300-step first walk
    [InlineData(20L, 300L, 1, 6, 280L, false)]    // 2,000-step first walk
    [InlineData(299L, 300L, 1, 99, 1L, false)]    // never reads as 100% before completion
    [InlineData(1L, 1_500L, 2, 1, 749L, true)]    // workshop: next visible stage at 750
    [InlineData(750L, 1_500L, 2, 50, 750L, false)]
    [InlineData(1L, 2_200L, 3, 1, 733L, true)]    // ceil(2200/3) = 734
    [InlineData(734L, 2_200L, 3, 33, 733L, true)] // next at ceil(4400/3) = 1467
    public void Payoff_reports_percent_and_distance_to_next_visible_change(
        long invested, long cost, int stages, int percent, long toNext, bool buildStage)
    {
        var p = ProjectPayoff.For(invested, cost, stages);
        Assert.NotNull(p);
        Assert.Equal(percent, p!.Value.Percent);
        Assert.Equal(toNext, p.Value.VitalityToNext);
        Assert.Equal(toNext * 100L, p.Value.StepsToNext);
        Assert.Equal(buildStage, p.Value.NextIsBuildStage);
    }

    [Theory]
    [InlineData(0L, 300L)]
    [InlineData(300L, 300L)]
    [InlineData(5L, 0L)]
    public void Nothing_to_report_when_unfunded_complete_or_costless(long invested, long cost) =>
        Assert.Null(ProjectPayoff.For(invested, cost, 1));

    [Fact]
    public void Next_stage_threshold_matches_domain_build_stage_rule()
    {
        var def = new WalkGame.Domain.Projects.ProjectDefinition(
            new WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProjectIdKind>("proj.t"), "T", 2_200L, visibleBuildStages: 3);
        for (long inv = 1; inv < 2_200L; inv++)
        {
            var p = ProjectPayoff.For(inv, 2_200L, 3)!.Value;
            int now = def.BuildStageFor(inv);
            long target = inv + p.VitalityToNext;
            if (p.NextIsBuildStage)
            {
                Assert.Equal(now + 1, def.BuildStageFor(target));
                Assert.Equal(now, def.BuildStageFor(target - 1));
            }
            else Assert.Equal(2_200L, target);
        }
    }

    [Fact]
    public void A_300_step_first_walk_is_shown_as_visible_progress_on_home_and_summary()
    {
        var clock = new ManualClock(TestSessions.T0);
        var session = TestSessions.Create(_temp, clock);
        session.StartNewGame(seed: 7UL);
        Assert.True(session.EnqueueProject(TestSessions.EntryProjectId).IsSuccess);
        Assert.Null(session.GetGlance().PayoffSentence);

        session.CreditActivity(TestSessions.Tx1, TestSessions.T0, 3L, "walk");

        var home = session.GetHome();
        Assert.Equal(1, home.ActiveProjectPayoff!.Value.Percent);
        Assert.Equal(
            "Clear the old trailhead: 1% restored. About 29,700 more steps to finish it.",
            session.GetGlance().PayoffSentence);
        Assert.True(session.GetPendingReturnSummary()!.HasCurrentItems);
    }

    [Fact]
    public void Payoff_is_read_only_and_deterministic()
    {
        var clock = new ManualClock(TestSessions.T0);
        var session = TestSessions.Create(_temp, clock);
        session.StartNewGame(seed: 7UL);
        session.EnqueueProject(TestSessions.EntryProjectId);
        session.CreditActivity(TestSessions.Tx1, TestSessions.T0, 20L, "walk");
        var a = session.GetGlance().PayoffSentence;
        var b = session.GetGlance().PayoffSentence;
        Assert.Equal(a, b);
        Assert.Contains("6% restored", a);
        Assert.Equal(20L, session.GetHome().ActiveProjectInvested);
    }
}
