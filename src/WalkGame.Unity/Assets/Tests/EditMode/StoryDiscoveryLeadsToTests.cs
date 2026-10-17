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
    /// <summary>
    /// D-155: Story and Discoveries say what open restorations lead to (same framing as D-154).
    /// Story: under the next chapter, where each of its required restorations stands. Discoveries:
    /// which open projects turn up a find, naming the project and never the find. Display only.
    /// Unity-editor-tested; device UNVERIFIED (KI-002).
    /// </summary>
    public sealed class StoryDiscoveryLeadsToTests
    {
        private string _dir = "";
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-leads-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private (ShellController c, GameSession s) Fresh()
        {
            var clock = new ManualClock(new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero));
            var s = ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(true), clock);
            var c = new ShellController(s, () => 42UL, devTools: true);
            c.Boot(); c.Invoke("start");
            return (c, s);
        }

        [Test] public void Next_chapter_path_lines_follow_its_required_restorations()
        {
            var (c, s) = Fresh();
            var story = s.GetStory();
            var p = s.GetProjects();
            var next = story.Chapters.First(x => !x.Unlocked);
            var expected = next.RequiredProjectIds.Where(id => p.Projects.Any(r => r.ProjectId == id && r.Status != ProjectStatus.Completed)).Count();
            var lines = ShellController.ChapterPathLines(next, p);
            Assert.AreEqual(expected, lines.Count);
            Assert.Greater(lines.Count, 0, "a new game's next chapter waits on a restoration");
            c.Invoke("nav:Story");
            var screen = c.Render().Lines;
            int i = screen.FindIndex(l => l.Contains(" (next). "));
            Assert.GreaterOrEqual(i, 0, string.Join("\n", screen));
            CollectionAssert.AreEqual(lines, screen.Skip(i + 1).Take(lines.Count).ToList());
            StringAssert.StartsWith("Getting there: ", screen[i + 1]);
        }

        [Test] public void Open_required_project_shows_status_and_progress()
        {
            var (_, s) = Fresh();
            var p = s.GetProjects();
            var next = s.GetStory().Chapters.First(x => !x.Unlocked);
            var r = p.Projects.First(x => x.ProjectId == next.RequiredProjectIds[0]);
            Assume.That(r.Status, Is.Not.EqualTo(ProjectStatus.Locked));
            Assert.AreEqual(PlayerStrings.F("shell.story.path.open", PlayerCopy.Title(r.TitleKey), PlayerCopy.ProjectStatus(r.Status, r.QueuedPosition),
                PlayerStrings.F("shell.story.path.amount", r.VitalityInvested, r.VitalityCost)), ShellController.ChapterPathLines(next, p)[0]);
        }

        [Test] public void Unlocked_and_care_gated_chapters_add_no_path_lines()
        {
            var (_, s) = Fresh();
            var story = s.GetStory();
            var p = s.GetProjects();
            foreach (var ch in story.Chapters.Where(x => x.Unlocked || x.RequiredProjectIds.Count == 0))
                Assert.IsEmpty(ShellController.ChapterPathLines(ch, p), ch.ChapterId);
        }

        [Test] public void Locked_required_project_names_only_open_prerequisites()
        {
            var (_, s) = Fresh();
            var p = s.GetProjects();
            foreach (var ch in s.GetStory().Chapters.Where(x => !x.Unlocked))
                foreach (var line in ShellController.ChapterPathLines(ch, p))
                    foreach (var locked in p.Projects.Where(r => r.Status == ProjectStatus.Locked && !ch.RequiredProjectIds.Contains(r.ProjectId)))
                        StringAssert.DoesNotContain("once " + PlayerCopy.Title(locked.TitleKey), line, "a locked prerequisite is never named as the next step");
        }

        [Test] public void Finds_ahead_names_open_projects_that_unlock_an_unfound_discovery()
        {
            var (c, s) = Fresh();
            var d = s.GetDiscoveries();
            var p = s.GetProjects();
            var expected = p.Projects.Where(r => r.Status != ProjectStatus.Locked && r.Status != ProjectStatus.Completed
                    && d.Discoveries.Any(x => !x.Unlocked && x.UnlockedByProjectId == r.ProjectId))
                .Select(r => PlayerCopy.Title(r.TitleKey)).ToList();
            var ahead = ShellController.FindsAhead(d, p);
            CollectionAssert.AreEqual(expected, ahead);
            Assert.Greater(ahead.Count, 0);
            c.Invoke("nav:Discoveries");
            var lines = c.Render().Lines;
            Assert.Contains(PlayerStrings.F("shell.discoveries.ahead", string.Join(", ", ahead)), lines);
            var text = string.Join("\n", lines);
            foreach (var hidden in d.Discoveries.Where(x => !x.Unlocked))
                StringAssert.DoesNotContain(hidden.TitleKey, text, "an unfound discovery is never named");
        }
    }
}
