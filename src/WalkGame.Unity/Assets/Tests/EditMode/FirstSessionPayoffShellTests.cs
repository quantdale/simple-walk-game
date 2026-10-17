using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application.Development;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>D-145: a short first walk shows visible progress on Home and the return summary. Unity-editor-tested.</summary>
    public sealed class FirstSessionPayoffShellTests
    {
        private string _dir = "";
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-payoff-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private static bool IsPayoff(string l) => l.Contains("% restored. About ");

        [Test] public void Short_first_walk_shows_percent_on_home_and_summary()
        {
            var s = ShellBootstrap.CreateSession(_dir);
            var c = new ShellController(s, () => 42UL, devTools: false);
            c.Boot(); c.Invoke("start");
            var first = s.GetProjects().Projects.First(p => p.Status == WalkGame.Domain.Projects.ProjectStatus.Available);
            s.EnqueueProject(first.ProjectId);
            c.Invoke("nav:Home");
            Assert.IsFalse(c.Render().Lines.Any(IsPayoff), "nothing funded yet");

            var now = DateTimeOffset.UtcNow;
            s.IngestFromSource(new SyntheticWalkingSource(2000), now.AddDays(-1), now);
            var after = s.GetHome();
            Assert.That(after.ActiveProjectInvested, Is.GreaterThan(0L).And.LessThan(after.ActiveProjectCost), "a short walk funds the first project partially");

            c.Invoke("nav:Home");
            var home = c.Render();
            Assert.AreEqual(1, home.Lines.Count(IsPayoff), string.Join("|", home.Lines));

            c.Invoke("nav:ReturnSummary");
            var summary = c.Render();
            Assert.AreEqual(1, summary.Lines.Count(IsPayoff), string.Join("|", summary.Lines));
        }
    }
}
