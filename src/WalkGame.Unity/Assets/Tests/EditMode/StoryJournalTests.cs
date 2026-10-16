using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Application.Development;
using WalkGame.Domain.Projects;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>Story journal and chapter moments (D-076). Unity-editor-tested; device UNVERIFIED (KI-002).</summary>
    public sealed class StoryJournalTests
    {
        private string _dir = "";
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-story-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private (ShellController c, GameSession s) Fresh()
        {
            var s = ShellBootstrap.CreateSession(_dir);
            var c = new ShellController(s, () => 42UL, devTools: true);
            c.Boot(); c.Invoke("start");
            return (c, s);
        }

        private static void Walk(GameSession s, long steps, int days)
        {
            foreach (var p in s.GetProjects().Projects.Where(p => p.Status == ProjectStatus.Available)) s.EnqueueProject(p.ProjectId);
            if (s.GetPendingReturnSummary() != null) s.AcknowledgeReturnSummary();
            var now = DateTimeOffset.UtcNow;
            s.IngestFromSource(new SyntheticWalkingSource(steps), now.AddDays(-days), now);
        }

        [Test] public void Home_offers_the_story_and_a_new_game_shows_only_the_prologue_text()
        {
            var (c, s) = Fresh();
            c.Invoke("nav:Story");
            var m = c.Render();
            Assert.AreEqual(ShellScreen.Story, m.Screen);
            Assert.AreEqual("Story", m.Title);
            Assert.AreEqual("1 of 10 chapters unlocked. Restoring the valley reveals the next one.", m.Lines[0]);
            Assert.AreEqual("Prologue · The Quiet Valley", m.Lines[1]);
            StringAssert.StartsWith("Millbrook was a working valley", m.Lines[2]);
            StringAssert.Contains("(next). Clear the old trailhead", m.Lines[3]);
            // Later chapters: title only; no chapter text can leak before it is earned.
            var story = s.GetStory();
            foreach (var locked in story.Chapters.Skip(2))
                Assert.IsTrue(m.Lines.Any(l => l.EndsWith(locked.TitleKey + " (ahead)")), locked.TitleKey);
            Assert.AreEqual(story.TotalChapters + 2, m.Lines.Count, "header + prologue body + one line per other chapter");
            Assert.IsTrue(m.Actions.Any(a => a.Id == "nav:Home"));
        }

        [Test] public void Home_shows_the_current_chapter_and_the_next_hint_without_spoilers()
        {
            var (c, s) = Fresh();
            c.Invoke("nav:Home");
            var story = s.GetStory();
            var line = c.Render().Lines.Single(l => l.StartsWith("Story: ", StringComparison.Ordinal));
            Assert.AreEqual("Story: Prologue · " + story.Chapters[0].TitleKey + ". Next: " + story.NextHintKey, line);
            foreach (var locked in story.Chapters.Where(ch => !ch.Unlocked))
                StringAssert.DoesNotContain(locked.TitleKey, line, "a locked chapter title is not spoiled on Home");

            Walk(s, 200000, 10);
            c.Invoke("nav:Home");
            story = s.GetStory();
            Assert.That(story.UnlockedCount, Is.GreaterThanOrEqualTo(2));
            var latest = story.Chapters.Last(ch => ch.Unlocked);
            line = c.Render().Lines.Single(l => l.StartsWith("Story: ", StringComparison.Ordinal));
            StringAssert.StartsWith("Story: Chapter " + latest.Number + " · " + latest.TitleKey + ".", line);
            foreach (var bad in new[] { "missed", "lost", "streak", "hurry", "don't forget", "falling behind", "decay", "expire", "behind", "penalty" })
                StringAssert.DoesNotContain(bad, line.ToLowerInvariant());
        }

        [Test] public void Home_story_line_becomes_the_epilogue_when_every_chapter_is_unlocked()
        {
            var rows = Enumerable.Range(0, 3).Select(n => new WalkGame.Application.ReadModels.StoryReadModel.ChapterRow(
                "c" + n, n, "T" + n, "B" + n, "H" + n, true, DateTimeOffset.UnixEpoch)).ToList();
            var done = new WalkGame.Application.ReadModels.StoryReadModel(rows, 3, null);
            Assert.AreEqual(ShellController.StoryEpilogueLine, ShellController.HomeStoryLine(done));
            foreach (var bad in new[] { "missed", "lost", "streak", "hurry", "decay", "expire", "behind", "penalty" })
                StringAssert.DoesNotContain(bad, ShellController.StoryEpilogueLine.ToLowerInvariant());
        }

        [Test] public void Copy_has_no_guilt_or_pressure_language()
        {
            var (c, _) = Fresh();
            c.Invoke("nav:Story");
            var text = string.Join(" ", c.Render().Lines).ToLowerInvariant();
            foreach (var bad in new[] { "missed", "lost", "streak", "hurry", "don't forget", "falling behind", "decay", "expire" })
                StringAssert.DoesNotContain(bad, text);
        }

        [Test] public void A_walk_that_completes_a_milestone_yields_a_chapter_moment_with_a_journal_link()
        {
            var (c, s) = Fresh();
            Walk(s, 200000, 10);
            c.Invoke("nav:ReturnSummary");
            var m = c.Render();
            Assert.That(m.Lines.Any(l => l.StartsWith(ShellController.ChapterMomentPrefix)), string.Join("|", m.Lines));
            Assert.AreEqual("ack", m.Actions[0].Id, "Continue stays the primary action");
            Assert.IsTrue(m.Actions.Any(a => a.Id == "nav:Story" && a.Label == "Read the new chapter"));

            c.Invoke("nav:Story");
            var j = c.Render();
            Assert.That(s.GetStory().UnlockedCount, Is.GreaterThanOrEqualTo(2));
            Assert.IsTrue(j.Lines.Contains("Chapter 1 · The Way In"));
        }

        [Test] public void A_summary_without_a_chapter_moment_has_no_journal_link()
        {
            var (c, s) = Fresh();
            Walk(s, 200000, 10);
            s.AcknowledgeReturnSummary();
            var now = DateTimeOffset.UtcNow;
            s.IngestFromSource(new SyntheticWalkingSource(50), now.AddHours(-1), now.AddMinutes(-1));
            c.Invoke("nav:ReturnSummary");
            var m = c.Render();
            if (m.Lines.Any(l => l.StartsWith(ShellController.ChapterMomentPrefix))) Assert.Inconclusive("tiny walk still finished a milestone");
            Assert.IsFalse(m.Actions.Any(a => a.Id == "nav:Story"));
        }

        [Test] public void Journal_outline_reads_header_status_chapters_then_home_button()
        {
            var (c, _) = Fresh();
            c.Invoke("nav:Story");
            var m = c.Render();
            var outline = AccessibleOutline.Build(m);
            Assert.AreEqual(AccessibleRole.Header, outline[0].Role);
            Assert.AreEqual("Story", outline[0].Label);
            Assert.AreEqual(m.Lines[0], outline[1].Label);
            Assert.AreEqual(AccessibleRole.Button, outline.Last().Role);
            Assert.AreEqual("nav:Home", outline.Last().ActionId);
            Assert.AreEqual(m.Lines.Count + 2, outline.Count);
        }
        [Test] public void D133_progress_line_after_restoration_points_to_tending_not_restoring()
        {
            Assert.AreEqual("9 of 10 chapters unlocked. Tending the restored valley reveals the last one.", ShellController.StoryProgressLine(9, 10, true));
            Assert.AreEqual("1 of 10 chapters unlocked. Restoring the valley reveals the next one.", ShellController.StoryProgressLine(1, 10, false));
            Assert.AreEqual(PlayerStrings.T("shell.youHaveReadThe"), ShellController.StoryProgressLine(10, 10, true));
        }
    }
}
