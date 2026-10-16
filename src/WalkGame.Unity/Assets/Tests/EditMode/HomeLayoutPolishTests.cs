using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application.Development;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>D-106 Home layout polish: no repeated lines, no doubled empty-state message. Unity-editor-tested.</summary>
    public sealed class HomeLayoutPolishTests
    {
        private string _dir = "";
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-home-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private ShellController Walked(bool queue)
        {
            var s = ShellBootstrap.CreateSession(_dir);
            var c = new ShellController(s, () => 42UL, devTools: true);
            c.Boot(); c.Invoke("start");
            if (queue)
                foreach (var p in s.GetProjects().Projects.Where(p => p.Status == WalkGame.Domain.Projects.ProjectStatus.Available)) s.EnqueueProject(p.ProjectId);
            var now = DateTimeOffset.UtcNow;
            s.IngestFromSource(new SyntheticWalkingSource(120000), now.AddDays(-6), now);
            c.Invoke("nav:Home");
            return c;
        }

        [TestCase(true)] [TestCase(false)]
        public void Home_shows_each_line_once(bool queue)
        {
            var m = Walked(queue).Render();
            Assert.AreEqual(m.Lines.Count, m.Lines.Distinct(StringComparer.Ordinal).Count(), string.Join("|", m.Lines));
        }

        [Test] public void Empty_home_says_nothing_is_restored_only_once()
        {
            var s = ShellBootstrap.CreateSession(_dir);
            var c = new ShellController(s, () => 42UL, devTools: false);
            c.Boot(); c.Invoke("start"); c.Invoke("nav:Home");
            var m = c.Render();
            Assume.That(m.State, Is.EqualTo(ScreenState.Empty));
            Assert.AreEqual(1, m.Lines.Count(l => l.StartsWith("Nothing is being restored", StringComparison.Ordinal)), string.Join("|", m.Lines));
            Assert.IsTrue(m.Lines.Contains("Nothing is being restored yet. Choose a project to begin."));
        }
    }
}
