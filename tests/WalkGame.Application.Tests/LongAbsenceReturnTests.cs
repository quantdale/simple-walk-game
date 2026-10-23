using System;
using WalkGame.Application.ReadModels;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Time;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-198: long-absence returns. Offline production stays inside its existing caps, nothing the
/// player accomplished is lost, and the welcome tier is derived from the gap only (backward clock
/// = no welcome). Session-only: nothing new is saved.
/// </summary>
public sealed class LongAbsenceReturnTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private (int completed, long vitality, long materials) Seed()
    {
        var a = TestSessions.Create(_temp, new ManualClock(TestSessions.T0));
        a.StartNewGame(7UL);
        a.EnqueueProject(TestSessions.EntryProjectId);
        a.ActivateQueuedProject(TestSessions.EntryProjectId);
        a.CreditActivity(TestSessions.Tx1, TestSessions.T0.AddHours(1), 50_000L, "big walk");
        var h = a.GetHome();
        return (h.CompletedProjects, h.Vitality, h.Materials);
    }

    public static TheoryData<double, AbsenceTier> Gaps => new()
    {
        { 0.5, AbsenceTier.None },
        { 1, AbsenceTier.None },
        { 2, AbsenceTier.FewDays },
        { 13.9, AbsenceTier.FewDays },
        { 14, AbsenceTier.Long },
        { 183, AbsenceTier.Long },
        { 365.0 * 100, AbsenceTier.Long },
    };

    [Theory]
    [MemberData(nameof(Gaps))]
    public void Return_after_gap_keeps_every_accomplishment_and_classifies_the_welcome(double days, AbsenceTier tier)
    {
        var before = Seed();
        var now = TestSessions.T0.AddHours(1).AddDays(days);
        var b = TestSessions.Create(_temp, new ManualClock(now));
        var status = b.Continue().Status;
        Assert.True(status == StartStatus.Loaded, $"got {status}");
        Assert.Equal(tier, b.GetAbsence());
        var h = b.GetHome();
        Assert.True(h.CompletedProjects >= before.completed, "a completed project was lost");
        Assert.True(h.Vitality >= before.vitality, "unspent Vitality shrank while away");
        Assert.True(h.Materials >= before.materials, "materials shrank while away");
    }

    [Fact]
    public void Six_months_and_a_hundred_years_produce_the_same_capped_world()
    {
        Seed();
        var six = TestSessions.Create(_temp, new ManualClock(TestSessions.T0.AddDays(183)));
        six.Continue();
        var sixHome = six.GetHome();

        using var other = new TempDirectory();
        var a = TestSessions.Create(other, new ManualClock(TestSessions.T0));
        a.StartNewGame(7UL);
        a.EnqueueProject(TestSessions.EntryProjectId);
        a.ActivateQueuedProject(TestSessions.EntryProjectId);
        a.CreditActivity(TestSessions.Tx1, TestSessions.T0.AddHours(1), 50_000L, "big walk");
        var century = TestSessions.Create(other, new ManualClock(TestSessions.T0.AddDays(36500)));
        century.Continue();
        var cHome = century.GetHome();

        // Producer stores are bounded: past saturation, more time adds nothing.
        Assert.Equal(sixHome.Materials, cHome.Materials);
        Assert.Equal(sixHome.Knowledge, cHome.Knowledge);
        Assert.Equal(sixHome.Vitality, cHome.Vitality);
        Assert.Equal(sixHome.CompletedProjects, cHome.CompletedProjects);
    }

    [Fact]
    public void Backward_clock_jump_never_welcomes_and_never_loses_progress()
    {
        var before = Seed();
        var b = TestSessions.Create(_temp, new ManualClock(TestSessions.T0.AddDays(-30)));
        Assert.Equal(StartStatus.Loaded, b.Continue().Status);
        Assert.Equal(AbsenceTier.None, b.GetAbsence());
        var h = b.GetHome();
        Assert.Equal(before.completed, h.CompletedProjects);
        Assert.Equal(before.vitality, h.Vitality);
    }

    [Fact]
    public void Clock_jumps_back_then_forward_welcomes_only_for_real_elapsed_time_since_last_advance()
    {
        Seed();
        var clock = new ManualClock(TestSessions.T0.AddDays(-30));
        var b = TestSessions.Create(_temp, clock);
        b.Continue();
        clock.Set(TestSessions.T0.AddHours(1).AddDays(1));
        b.CatchUpWithoutReload();
        Assert.Equal(AbsenceTier.None, b.GetAbsence());
    }

    [Fact]
    public void Resume_after_two_weeks_welcomes_and_acknowledge_clears_it_for_the_session()
    {
        Seed();
        var clock = new ManualClock(TestSessions.T0.AddHours(2));
        var b = TestSessions.Create(_temp, clock);
        b.Continue();
        Assert.Equal(AbsenceTier.None, b.GetAbsence());
        clock.Set(TestSessions.T0.AddDays(15));
        b.CatchUpWithoutReload();
        Assert.Equal(AbsenceTier.Long, b.GetAbsence());
        b.AcknowledgeAbsence();
        Assert.Equal(AbsenceTier.None, b.GetAbsence());
        b.CatchUpWithoutReload(); // same clock: nothing elapsed, no repeat
        Assert.Equal(AbsenceTier.None, b.GetAbsence());
    }

    [Fact]
    public void Absence_is_not_persisted()
    {
        Seed();
        var b = TestSessions.Create(_temp, new ManualClock(TestSessions.T0.AddDays(20)));
        b.Continue();
        Assert.Equal(AbsenceTier.Long, b.GetAbsence());
        var c = TestSessions.Create(_temp, new ManualClock(TestSessions.T0.AddDays(20).AddMinutes(1)));
        c.Continue();
        Assert.Equal(AbsenceTier.None, c.GetAbsence());
    }
}
