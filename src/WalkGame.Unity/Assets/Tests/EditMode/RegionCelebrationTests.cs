using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>Region 1 completion celebration (D-080). Unity-editor-tested; device UNVERIFIED (KI-002).</summary>
    public sealed class RegionCelebrationTests
    {
        private static ScreenModel Summary(params string[] lines)
        {
            var m = new ScreenModel(ShellScreen.ReturnSummary, ScreenState.Normal, "While you were away");
            m.Lines.AddRange(lines);
            return m;
        }

        [Test] public void Region_complete_moment_retitles_and_leads_with_the_headline()
        {
            var m = Summary("Millbrook Valley's restoration is complete.");
            Assert.IsTrue(ShellController.Celebrate(m, m.Lines.ToList()));
            Assert.AreEqual("The valley is restored", m.Title);
            Assert.AreEqual(ShellController.CelebrationHeadline, m.Lines[0]);
            Assert.AreEqual("Millbrook Valley's restoration is complete.", m.Lines[2]);
        }

        [Test] public void Ordinary_summaries_are_not_celebrations()
        {
            var m = Summary("New chapter: The Way In.", "The settlement has reached stage 2.");
            Assert.IsFalse(ShellController.Celebrate(m, m.Lines.ToList()));
            Assert.AreEqual("While you were away", m.Title);
            Assert.AreEqual(2, m.Lines.Count);
        }

        [Test] public void Celebration_copy_is_calm_and_reads_in_order()
        {
            var m = Summary("Millbrook Valley's restoration is complete.");
            ShellController.Celebrate(m, m.Lines.ToList());
            m.Actions.Add(new ShellAction("ack", "Continue"));
            m.Actions.Add(new ShellAction("nav:Region", "See the restored valley"));
            var text = string.Join(" ", m.Lines).ToLowerInvariant();
            foreach (var bad in new[] { "missed", "lost", "streak", "hurry", "don't forget", "decay", "expire", "!" })
                StringAssert.DoesNotContain(bad, text);
            var outline = AccessibleOutline.Build(m);
            Assert.AreEqual("The valley is restored", outline[0].Label);
            Assert.AreEqual(ShellController.CelebrationHeadline, outline[1].Label);
            Assert.AreEqual("ack", outline[outline.Count - 2].ActionId, "Continue stays the primary action");
        }

        [Test] public void A_new_game_summary_has_no_celebration_link()
        {
            var dir = Path.Combine(Path.GetTempPath(), "wg-cel-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var c = new ShellController(ShellBootstrap.CreateSession(dir), () => 42UL, devTools: true);
                c.Boot(); c.Invoke("start"); c.Invoke("nav:ReturnSummary");
                Assert.IsFalse(c.Render().Actions.Any(a => a.Id == "nav:Region"));
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
