using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Application.Development;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Time;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>D-127: Region 1 mid/late-game polish found in the editor-rendered review. Unity-editor-tested.</summary>
    public sealed class MidGamePolishTests
    {
        private string _dir = "";
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-mid-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private (ShellController c, GameSession s) PlayDays(int maxDays)
        {
            var t0 = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
            var clock = new ManualClock(t0);
            var s = ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(true), clock);
            var c = new ShellController(s, () => 42UL, devTools: true);
            c.Boot(); c.Invoke("start");
            var src = new SyntheticWalkingSource(20000);
            for (int d = 1; d <= maxDays; d++)
            {
                clock.Set(t0.AddDays(d));
                s.IngestFromSource(src, t0.AddDays(d - 1), t0.AddDays(d));
                var home = s.GetHome();
                if (home.ActiveProjectId == null && home.Queued.Count == 0)
                    foreach (var p in s.GetProjects().Projects)
                        if (p.Status == ProjectStatus.Available && s.EnqueueProject(p.ProjectId).IsSuccess) break;
            }
            if (s.GetPendingReturnSummary() != null) s.AcknowledgeReturnSummary();
            return (c, s);
        }

        [Test] public void Mid_game_projects_list_actionable_rows_before_restored_ones()
        {
            var (c, s) = PlayDays(30);
            var rows = s.GetProjects().Projects.Where(r => r.Status != ProjectStatus.Locked).ToList();
            Assume.That(rows.Count(r => r.Status == ProjectStatus.Completed), Is.GreaterThan(2));
            Assume.That(rows.Count(r => r.Status == ProjectStatus.Available), Is.GreaterThan(0));
            c.Invoke("nav:Projects");
            var lines = c.Render().Lines;
            int lastOpen = -1, firstDone = int.MaxValue;
            foreach (var r in rows)
            {
                int i = lines.FindIndex(l => l.StartsWith(PlayerCopy.Title(r.TitleKey) + " — ", StringComparison.Ordinal));
                Assert.GreaterOrEqual(i, 0, r.ProjectId);
                if (r.Status == ProjectStatus.Completed) { firstDone = Math.Min(firstDone, i); StringAssert.DoesNotContain("vitality", lines[i]); }
                else lastOpen = Math.Max(lastOpen, i);
            }
            Assert.Less(lastOpen, firstDone, string.Join("\n", lines));
            var active = rows.FirstOrDefault(r => r.Status == ProjectStatus.Active);
            if (active != null)
                StringAssert.StartsWith(PlayerCopy.Title(active.TitleKey), lines.First(l => l.Contains(" — ")));
        }
    }
}
