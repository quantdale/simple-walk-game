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
    /// <summary>D-154: the Projects screen says what finishing each open project opens. Unity-editor-tested.</summary>
    public sealed class ProjectChoiceFramingTests
    {
        private string _dir = "";
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-opens-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private (ShellController c, GameSession s) Fresh()
        {
            var clock = new ManualClock(new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero));
            var s = ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(true), clock);
            var c = new ShellController(s, () => 42UL, devTools: true);
            c.Boot(); c.Invoke("start");
            return (c, s);
        }

        [Test] public void Opens_next_matches_what_completing_the_project_would_unlock()
        {
            var (_, s) = Fresh();
            var p = s.GetProjects();
            var open = p.Projects.Where(r => r.Status != ProjectStatus.Locked && r.Status != ProjectStatus.Completed).ToList();
            Assume.That(open.Count, Is.GreaterThan(0));
            bool anyOpens = false;
            foreach (var r in open)
            {
                var opens = ShellController.OpensNext(p, r.ProjectId);
                var expected = p.Projects.Where(x => x.Status == ProjectStatus.Locked && x.PrerequisiteIds.Count > 0
                        && x.PrerequisiteIds.All(id => id == r.ProjectId || p.Projects.Any(q => q.ProjectId == id && q.Status == ProjectStatus.Completed))
                        && x.PrerequisiteIds.Contains(r.ProjectId))
                    .Select(x => PlayerCopy.Title(x.TitleKey)).ToList();
                CollectionAssert.AreEqual(expected, opens, r.ProjectId);
                anyOpens |= opens.Count > 0;
            }
            Assert.IsTrue(anyOpens, "the entry project should open at least one follow-up");
        }

        [Test] public void Projects_screen_shows_opens_line_right_after_its_project_row()
        {
            var (c, s) = Fresh();
            var p = s.GetProjects();
            var r = p.Projects.First(x => x.Status != ProjectStatus.Locked && x.Status != ProjectStatus.Completed && ShellController.OpensNext(p, x.ProjectId).Count > 0);
            c.Invoke("nav:Projects");
            var lines = c.Render().Lines;
            int i = lines.FindIndex(l => l.StartsWith(PlayerCopy.Title(r.TitleKey) + " — ", StringComparison.Ordinal));
            Assert.GreaterOrEqual(i, 0, string.Join("\n", lines));
            Assert.AreEqual(PlayerStrings.F("shell.projects.opens", string.Join(", ", ShellController.OpensNext(p, r.ProjectId))), lines[i + 1]);
        }

        [Test] public void Completed_and_locked_projects_have_no_opens_line()
        {
            var (c, _) = Fresh();
            c.Invoke("nav:Projects");
            var lines = c.Render().Lines;
            int opensLines = lines.Count(l => l.StartsWith("Finishing it opens:", StringComparison.Ordinal));
            int openRows = lines.Count(l => l.Contains(" — ") && l.Contains("vitality)"));
            Assert.LessOrEqual(opensLines, openRows);
        }
    }
}
