using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Domain.Time;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>D-099 Progress screen: reachable from Home, read-only, calm, no streak or goal framing. Unity-editor-tested.</summary>
    public sealed class ProgressScreenTests
    {
        private string _dir;
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-progress-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private ShellController Played(int walkDays)
        {
            var c = new ShellController(ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(true), new ManualClock(DateTimeOffset.UtcNow)), () => 42UL, devTools: true);
            c.Boot(); c.Invoke("start");
            for (int i = 0; i < 12 && c.Current == ShellScreen.Guide; i++)
            {
                var g = c.Render();
                var q = g.Actions.FirstOrDefault(a => a.Id.StartsWith("queue:"));
                c.Invoke(g.Actions.Any(a => a.Id == "guide:next") ? "guide:next" : q?.Id ?? g.Actions[0].Id);
            }
            for (int d = 0; d < walkDays; d++) c.Invoke("dev:walk");
            c.Invoke("nav:Home");
            return c;
        }

        private static void Calm(string line)
        {
            foreach (var g in CopyPolishTests.GuiltPhrases) StringAssert.DoesNotContain(g, line.ToLowerInvariant(), line);
            StringAssert.DoesNotContain("goal", line.ToLowerInvariant());
            StringAssert.DoesNotContain("record", line.ToLowerInvariant());
        }

        [Test] public void Home_links_to_progress_and_back()
        {
            var c = Played(0);
            Assert.IsTrue(c.Render().Actions.Any(a => a.Id == "nav:Progress" && a.Label == "Your progress"));
            c.Invoke("nav:Progress");
            var m = c.Render();
            Assert.AreEqual(ShellScreen.Progress, m.Screen);
            Assert.AreEqual("Your progress", m.Title);
            Assert.IsTrue(m.Actions.Any(a => a.Id == "nav:Home"));
            Assert.IsFalse(m.Actions.Any(a => a.Id.StartsWith("queue:") || a.Id.StartsWith("toggle:")), "progress is read-only");
        }

        [Test] public void After_walking_shows_lifetime_vitality_and_steps()
        {
            var c = Played(3);
            c.Invoke("nav:Progress");
            var m = c.Render();
            Assert.AreEqual(ScreenState.Normal, m.State);
            Assert.IsTrue(m.Lines.Any(l => l.StartsWith("Vitality gathered from your walks: ")), string.Join("\n", m.Lines));
            Assert.IsTrue(m.Lines.Any(l => l.StartsWith("That is about ") && l.Contains(" steps")));
            Assert.IsTrue(m.Lines.Any(l => l.StartsWith("Restorations finished: ") || l.StartsWith("Every restoration")));
            foreach (var l in m.Lines) Calm(l);
        }

        [Test] public void Rendering_progress_does_not_change_the_save()
        {
            var c = Played(2);
            var before = File.ReadAllBytes(Path.Combine(_dir, "save.json"));
            c.Invoke("nav:Progress"); c.Render(); c.Render();
            CollectionAssert.AreEqual(before, File.ReadAllBytes(Path.Combine(_dir, "save.json")));
        }

        [TestCase(0L, 0, false)]
        [TestCase(1234L, 3, false)]
        [TestCase(98765L, 23, true)]
        public void Progress_copy_is_calm_in_every_state(long vitality, int done, bool complete)
        {
            var lines = ShellController.ProgressLines(vitality, done, 23, 4, 13, 2, 10, complete);
            Assert.That(lines.Count, Is.GreaterThanOrEqualTo(5));
            foreach (var l in lines) Calm(l);
            if (vitality == 0) StringAssert.Contains("no rush", lines[0]);
            else StringAssert.Contains((vitality * 100).ToString("N0", System.Globalization.CultureInfo.InvariantCulture), lines[1]);
        }
    }
}
