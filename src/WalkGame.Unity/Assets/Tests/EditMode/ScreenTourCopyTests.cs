using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>D-109: copy defects found by the editor-rendered screen tour (docs/evidence/screen-tour/).</summary>
    public sealed class ScreenTourCopyTests
    {
        private string _dir = "";
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-tourcopy-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
        private ShellController New() => new ShellController(ShellBootstrap.CreateSession(_dir), () => 42UL, devTools: true);

        [Test] public void Boot_error_shows_no_raw_status_identifier()
        {
            var c = New(); c.Boot(); c.Invoke("start");
            foreach (var f in Directory.GetFiles(_dir, "save*.json")) File.WriteAllText(f, "{ not a save");
            var broken = New(); broken.Boot();
            Assert.AreEqual(ShellScreen.BootError, broken.Current);
            var m = broken.Render();
            var names = Enum.GetNames(typeof(StartStatus));
            var text = string.Join("|", m.Lines.Append(m.Notice ?? "").Append(m.Title));
            foreach (var n in names) StringAssert.DoesNotContain(n, text);
            Assert.IsFalse(string.IsNullOrWhiteSpace(m.Notice), "the plain-language problem is still shown");
            Assert.IsTrue(m.Actions.Any(a => a.Id == "retry"));
        }

        [Test] public void Queue_buttons_never_double_the_verb()
        {
            var c = New(); c.Boot(); c.Invoke("start");
            int seen = 0;
            for (int i = 0; i < 12 && c.Current == ShellScreen.Guide; i++)
            {
                var g = c.Render();
                var queue = g.Actions.Where(a => a.Id.StartsWith("queue:")).ToList();
                foreach (var a in queue) { AssertLabel(a.Label); seen++; }
                c.Invoke(g.Actions.Any(a => a.Id == "guide:next") ? "guide:next" : queue.FirstOrDefault()?.Id ?? g.Actions[0].Id);
            }
            c.Invoke("nav:Projects");
            var m = c.Render();
            var labels = m.Actions.Where(a => a.Id.StartsWith("queue:")).Select(a => a.Label).ToList();
            foreach (var l in labels) { AssertLabel(l); seen++; }
            Assert.That(seen, Is.GreaterThan(0), "no queue button was checked");
        }

        private static void AssertLabel(string label)
        {
            StringAssert.StartsWith("Start: ", label);
            var words = label.Substring("Start: ".Length).Split(' ');
            Assert.IsFalse(words.Length > 1 && string.Equals(words[0], words[1], StringComparison.OrdinalIgnoreCase), label);
            StringAssert.DoesNotContain("Restore Restore", label);
        }
    }
}
