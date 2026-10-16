using System;
using WalkGame.Application;
using WalkGame.Application.Development;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Time;
using WalkGame.Shell;

namespace WalkGame.Shell.PlayModeTests
{
    /// <summary>
    /// Seeds a genuinely completed Region 1 through the real pipeline (no save editing): a manual
    /// clock walks day by day (20,000 synthetic steps/day) and a low-decision player queues the
    /// first open project whenever the queue is idle, mirroring M4Region1AcceptanceTests. The
    /// pending return summary is left unacknowledged so it carries the region-complete moment.
    /// </summary>
    public static class CompletedRegionFixture
    {
        public static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        public const int HorizonDays = 220;

        public static ShellController Build(string dir, out GameSession session, out int days) =>
            BuildUntil(dir, out session, out days, HorizonDays);

        /// <summary>Same seeded pipeline, stopped after <paramref name="maxDays"/> days (mid/late-game captures).</summary>
        public static ShellController BuildUntil(string dir, out GameSession session, out int days, int maxDays)
        {
            var clock = new ManualClock(T0);
            session = ShellBootstrap.CreateSession(dir, new ShellConnectionPort(true), clock);
            var c = new ShellController(session, () => 42UL, devTools: true);
            c.Boot(); c.Invoke("start");
            var source = new SyntheticWalkingSource(20000);
            days = 0;
            while (days < maxDays && !session.GetRegion().RegionCompleted)
            {
                days++;
                clock.Set(T0.AddDays(days));
                session.IngestFromSource(source, T0.AddDays(days - 1), T0.AddDays(days));
                var home = session.GetHome();
                if (home.ActiveProjectId == null && home.Queued.Count == 0)
                    foreach (var p in session.GetProjects().Projects)
                        if (p.Status == ProjectStatus.Available && session.EnqueueProject(p.ProjectId).IsSuccess) break;
            }
            return c;
        }
    }
}
