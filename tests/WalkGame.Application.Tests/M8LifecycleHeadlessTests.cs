using System;
using System.IO;
using System.Linq;
using WalkGame.Application.Activity;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Time;

namespace WalkGame.Application.Tests;

/// <summary>
/// M8 Lifecycle campaign, HEADLESS half (D-072). Repeated background/resume, process death,
/// permission changes made outside the app, and interrupted boots are modelled as process
/// boundaries: every "process" is a fresh <see cref="GameSession"/> over the same save
/// directory, and nothing is shared in memory. Automated verified. Device behaviour (real
/// OS kill, reboot, Health Connect revocation) stays UNVERIFIED (KI-002).
/// </summary>
public sealed class M8LifecycleHeadlessTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private const long Steps = 9_000L;
    private readonly TempDirectory _a = new();
    private readonly TempDirectory _b = new();
    public void Dispose() { _a.Dispose(); _b.Dispose(); }

    private static GameSession Proc(string dir, DateTimeOffset now, IActivityConnectionPort? port = null) =>
        port == null ? TestSessions.Create(dir, new ManualClock(now)) : TestSessions.Create(dir, new ManualClock(now), null, port);

    private static void NewGame(string dir, IActivityConnectionPort? port = null) =>
        Assert.Equal(StartStatus.NewGameCreated, Proc(dir, T0, port).StartNewGame(42UL).Status);

    private static GameSession Resume(string dir, DateTimeOffset now, IActivityConnectionPort? port = null)
    {
        var s = Proc(dir, now, port);
        Assert.Equal(StartStatus.Loaded, s.Continue().Status);
        return s;
    }

    private static void KeepQueueBusy(GameSession s)
    {
        var home = s.GetHome();
        if (home.ActiveProjectId != null || home.Queued.Count > 0) return;
        foreach (var p in Region1Catalog.Create().Projects)
            if (s.EnqueueProject(p.Id.Value).IsSuccess) return;
    }

    private static (long lifetime, int completed, long vitality) Snapshot(string dir, DateTimeOffset now)
    {
        var s = Resume(dir, now);
        return (s.GetDiagnostics().LifetimeVitalityCredited, s.GetRegion().CompletedProjects, s.GetHome().Vitality);
    }

    /// <summary>Reference: one process per day, ingesting exactly that day.</summary>
    private void Reference(string dir, int days)
    {
        NewGame(dir);
        for (int d = 1; d <= days; d++)
        {
            var s = Resume(dir, T0.AddDays(d));
            s.IngestFromSource(new SyntheticWalkingSource(Steps), T0.AddDays(d - 1), T0.AddDays(d));
            KeepQueueBusy(s);
        }
    }

    [Fact]
    public void Repeated_background_resume_with_overlapping_refetches_credits_each_day_exactly_once()
    {
        const int days = 40;
        Reference(_a.Path, days);

        NewGame(_b.Path);
        for (int d = 1; d <= days; d++)
            for (int resume = 0; resume < 4; resume++)   // four foreground returns a day, each a new process
            {
                var now = T0.AddDays(d).AddHours(-18 + resume * 6);
                var s = Resume(_b.Path, now);
                // An adapter re-reads generously after resume: the whole history up to "now".
                s.IngestFromSource(new SyntheticWalkingSource(Steps), T0, T0.AddDays(d));
                KeepQueueBusy(s);
            }

        var end = T0.AddDays(days + 1);
        Assert.Equal(Snapshot(_a.Path, end), Snapshot(_b.Path, end));
    }

    [Fact]
    public void Process_death_leaving_torn_temp_files_boots_from_the_last_commit()
    {
        Reference(_a.Path, 12);
        var before = Snapshot(_a.Path, T0.AddDays(13));

        // Death mid-write: half-written temp files next to every committed file.
        foreach (var f in Directory.GetFiles(_a.Path))
            File.WriteAllBytes(f + ".tmp", new byte[] { 0x7B, 0x22, 0x73, 0x63 }); // "{\"sc" — truncated JSON

        Assert.Equal(before, Snapshot(_a.Path, T0.AddDays(13)));

        // Life goes on after the restart, exactly once.
        var s = Resume(_a.Path, T0.AddDays(13));
        var r = s.IngestFromSource(new SyntheticWalkingSource(Steps), T0, T0.AddDays(13));
        Assert.Equal(1, r.Accepted);
        Assert.Equal(12, r.DuplicatesIgnored);
    }

    [Fact]
    public void Fifty_interrupted_boots_neither_lose_nor_invent_progress()
    {
        Reference(_a.Path, 5);
        var before = Snapshot(_a.Path, T0.AddDays(5));

        // Interrupted world loading: boots that load and read every surface, then die.
        // A boot may commit its own bookkeeping (offline advance, return summary); it may
        // never change what the player has earned or built.
        for (int i = 0; i < 50; i++)
        {
            var s = Resume(_a.Path, T0.AddDays(5).AddMinutes(i));
            _ = s.GetRegion(); _ = s.GetHome(); _ = s.GetDiscoveries(); _ = s.GetProjects();
            Assert.Equal(before.lifetime, s.GetDiagnostics().LifetimeVitalityCredited);
        }

        var after = Snapshot(_a.Path, T0.AddDays(5).AddMinutes(50));
        Assert.Equal(before.lifetime, after.lifetime);
        Assert.Equal(before.completed, after.completed);
        // The day-6 record still lands exactly once after all those boots.
        var r = Resume(_a.Path, T0.AddDays(6)).IngestFromSource(new SyntheticWalkingSource(Steps), T0, T0.AddDays(6));
        Assert.Equal(1, r.Accepted);
        Assert.Equal(5, r.DuplicatesIgnored);
    }

    [Fact]
    public void Permission_revoked_outside_the_app_preserves_progress_and_regrant_resumes_exactly_once()
    {
        var port = new FakeActivityConnectionPort { Permission = ActivityPermissionState.Granted };
        NewGame(_a.Path, port);
        for (int d = 1; d <= 10; d++)
        {
            var s = Resume(_a.Path, T0.AddDays(d), port);
            s.IngestFromSource(new SyntheticWalkingSource(Steps), T0.AddDays(d - 1), T0.AddDays(d));
            KeepQueueBusy(s);
        }
        var earned = Snapshot(_a.Path, T0.AddDays(10));

        // Revoked in system settings while the app was closed; the adapter cannot read.
        port.Permission = ActivityPermissionState.Revoked;
        for (int d = 11; d <= 20; d++)
        {
            var s = Resume(_a.Path, T0.AddDays(d), port);
            Assert.Equal(ActivityPlayerStatus.PermissionDenied, s.GetActivityStatus().Status);
            Assert.Equal(earned.lifetime, s.GetDiagnostics().LifetimeVitalityCredited); // nothing taken back
            Assert.Equal(earned.completed, s.GetRegion().CompletedProjects);
        }

        // Re-granted: the adapter back-fills the whole history; only the gap is new.
        port.Permission = ActivityPermissionState.Granted;
        var back = Resume(_a.Path, T0.AddDays(21), port);
        var r = back.IngestFromSource(new SyntheticWalkingSource(Steps), T0, T0.AddDays(21));
        Assert.Equal(11, r.Accepted);
        Assert.Equal(10, r.DuplicatesIgnored);
        Assert.Equal(ActivityPlayerStatus.ConnectedCurrent, back.GetActivityStatus().Status);
    }
}
