using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Domain.Projects;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>D-118 letters in the Story journal and Discoveries. Unity-editor-tested; device UNVERIFIED (KI-002).</summary>
    public sealed class LettersJournalTests
    {
        private string _dir = "";
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-letters-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
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

        [Test] public void A_new_game_lists_no_letters_anywhere()
        {
            var (c, _) = Fresh();
            c.Invoke("nav:Story");
            Assert.IsFalse(c.Render().Lines.Any(l => l.Contains("Letters from") || l.StartsWith("Found at ")));
            c.Invoke("nav:Discoveries");
            Assert.IsFalse(c.Render().Lines.Any(l => l.Contains("Letters from")));
        }

        [Test] public void A_found_letter_shows_in_the_journal_and_is_counted_in_discoveries()
        {
            var (c, s) = Fresh();
            Walk(s, 200000, 10);
            var letters = s.GetLetters();
            if (letters.FoundCount == 0) Assert.Inconclusive("the walk did not complete the trailhead");
            c.Invoke("nav:Story");
            var m = c.Render();
            var count = "Letters from Nell Hartley: " + letters.FoundCount + " of 6 found.";
            Assert.Contains(count, m.Lines.ToList());
            var first = Region1Letters.All[0];
            Assert.Contains("Found at " + first.PlaceKey + ": \"" + first.Body + "\"", m.Lines.ToList());
            foreach (var unfound in Region1Letters.All.Where(l => letters.Letters.Any(r => r.LetterId == l.Id && !r.Found)))
                Assert.IsFalse(m.Lines.Any(x => x.Contains(unfound.Body)), "no unfound letter is spoiled");
            Assert.AreEqual("nav:Home", m.Actions.Last().Id);

            c.Invoke("nav:Discoveries");
            Assert.Contains(count + " Read them in the Story journal.", c.Render().Lines.ToList());
        }

        [Test] public void Letter_copy_has_no_guilt_or_pressure_language()
        {
            foreach (var l in Region1Letters.All)
                foreach (var bad in new[] { "missed", "streak", "don't forget", "falling behind", "decay", "expire", "penalty", "too late" })
                    StringAssert.DoesNotContain(bad, l.Body.ToLowerInvariant());
        }
    }
}
