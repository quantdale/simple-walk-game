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
            // ---- D-160: "what you restored" recap, from a genuinely completed region. ----

        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

        /// <summary>Completes Region 1 through the real pipeline (CompletedRegionFixture's loop, 20,000 steps/day).</summary>
        private static ShellController CompletedRegion(string dir, out GameSession session)
        {
            var clock = new ManualClock(T0);
            session = ShellBootstrap.CreateSession(dir, new ShellConnectionPort(true), clock);
            var c = new ShellController(session, () => 42UL, devTools: true);
            c.Boot(); c.Invoke("start");
            var source = new SyntheticWalkingSource(20000);
            for (int day = 1; day <= 220 && !session.GetRegion().RegionCompleted; day++)
            {
                clock.Set(T0.AddDays(day));
                session.IngestFromSource(source, T0.AddDays(day - 1), T0.AddDays(day));
                var home = session.GetHome();
                if (home.ActiveProjectId == null && home.Queued.Count == 0)
                    foreach (var p in session.GetProjects().Projects)
                        if (p.Status == ProjectStatus.Available && session.EnqueueProject(p.ProjectId).IsSuccess) break;
            }
            Assert.IsTrue(session.GetRegion().RegionCompleted);
            return c;
        }

        [Test] public void Celebration_keeps_the_final_completion_line_and_skips_repeats()
        {
            // D-163 (KI-026): the closing project's own line survives the crowded closure day; the
            // landmark line its detail repeats and "ready" lines for restored projects are not rendered.
            var dir = Path.Combine(Path.GetTempPath(), "wg-prio-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var c = CompletedRegion(dir, out var session);
                c.Invoke("nav:ReturnSummary");
                var m = c.Render();
                string all = string.Join("\n", m.Lines);
                Assert.Contains("The valley survey is complete.", m.Lines, all);
                int at = m.Lines.IndexOf("The valley survey is complete.");
                StringAssert.StartsWith("Ridge Observatory is now restored.", m.Lines[at + 1], all);
                CollectionAssert.DoesNotContain(m.Lines, "Ridge Observatory is restored.", all);
                foreach (var p in session.GetProjects().Projects)
                    Assert.IsFalse(m.Lines.Any(l => l.EndsWith("Ready to queue: " + p.TitleKey + ".")), all);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test] public void Celebration_recaps_what_was_restored_from_canonical_state()
        {
            var dir = Path.Combine(Path.GetTempPath(), "wg-recap-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var c = CompletedRegion(dir, out var session);
                c.Invoke("nav:ReturnSummary");
                var m = c.Render();
                Assert.AreEqual("The valley is restored", m.Title);
                Assert.AreEqual(ShellController.CelebrationHeadline, m.Lines[0]);
                Assert.AreEqual("What you restored:", m.Lines[2]);

                var region = session.GetRegion();
                Assert.AreEqual(region.TotalProjects + " restoration projects finished.", m.Lines[3]);
                StringAssert.StartsWith("Places brought back: ", m.Lines[4]);
                foreach (var l in region.Landmarks) StringAssert.Contains(l.TitleKey, m.Lines[4]);
                Assert.AreEqual(session.GetDiscoveries().TotalDiscoveries + " discoveries recorded.", m.Lines[5]);
                Assert.AreEqual(session.GetExpeditions().TotalExpeditions + " expeditions walked.", m.Lines[6]);
                Assert.AreEqual(session.GetLetters().TotalLetters + " letters from Nell found.", m.Lines[7]);
                // Synthetic walks carry no local day, so days walked is unknown here and its line is
                // omitted (never "0 days"); with dated walks it follows the letters line.
                string daysLine = ShellController.DaysWalkedLine(session.GetDaysWalked().Days, session.GetDaysWalked().IsLowerBound);
                if (daysLine != null) Assert.AreEqual(daysLine, m.Lines[8]);
                else StringAssert.DoesNotContain("walked on", m.Lines[8]);
                Assert.Contains("Millbrook Valley's restoration is complete.", m.Lines);

                var recap = string.Join(" ", m.Lines.Skip(2).Take(7)).ToLowerInvariant();
                foreach (var bad in new[] { " of ", "missed", "streak", "collect", "left", "remaining", "!" })
                    StringAssert.DoesNotContain(bad, recap);
                Assert.AreEqual("ack", m.Actions[0].Id, "Continue stays the primary action");

                // Shown once: after Continue, the recap is gone with the summary.
                c.Invoke("ack");
                c.Invoke("nav:ReturnSummary");
                CollectionAssert.DoesNotContain(c.Render().Lines, "What you restored:");
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test] public void Recap_omits_empty_counts_and_never_shows_without_a_celebration()
        {
            Assert.IsEmpty(ShellController.RestoredRecap(null, 3, 1, 2, null));
            var m = Summary("New chapter: The Way In.");
            Assert.IsFalse(ShellController.Celebrate(m, m.Lines.ToList()));
            CollectionAssert.DoesNotContain(m.Lines, "What you restored:");
        }
    }
}
