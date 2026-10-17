using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Time;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>
    /// D-162 copy-quality sweep, shell half (the Application half is SummaryCopyQualityTests).
    /// Every shell template that interpolates a project title is rendered for every project title,
    /// and every line and button of the main screens is checked mid-game and on the completed
    /// valley's celebration, against known-bad patterns: an imperative project title used as a
    /// sentence subject ("Complete the valley survey is complete."), a doubled word ("Route route"),
    /// doubled punctuation, raw ids and unfilled placeholders. Unity-editor-tested; device UNVERIFIED (KI-002).
    /// </summary>
    public sealed class CopyQualitySweepTests
    {
        private static readonly Regex DoubledWord = new Regex(@"\b(\w+) \1\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex DoubledPunctuation = new Regex(@"[.!?,;:] ?[.!?,;:]| [.,;:!?]|  ", RegexOptions.CultureInvariant);
        private static readonly Regex RawId = new Regex(@"\b(proj|exp|disc|lm|prd|region|care|chain)\.[a-z]|\{\d\}", RegexOptions.CultureInvariant);

        public static List<string> Problems(string line)
        {
            var found = new List<string>();
            if (string.IsNullOrEmpty(line)) return found;
            var d = DoubledWord.Match(line);
            if (d.Success) found.Add("doubled word '" + d.Value + "'");
            var p = DoubledPunctuation.Match(line);
            if (p.Success) found.Add("punctuation/spacing '" + p.Value + "'");
            if (RawId.IsMatch(line)) found.Add("raw id or placeholder");
            foreach (var project in Region1Catalog.Create().Projects)
                foreach (var verb in new[] { " is ", " are ", " opens ", " continues ", " was ", " up" })
                    if (line.IndexOf(project.TitleKey + verb, StringComparison.Ordinal) >= 0)
                        found.Add("imperative title as subject: '" + project.TitleKey + verb.TrimEnd() + "'");
            return found;
        }

        private static void AssertClean(IEnumerable<string> lines, string where)
        {
            var bad = new List<string>();
            foreach (var l in lines)
                foreach (var problem in Problems(l)) bad.Add(where + ": \"" + l + "\" (" + problem + ")");
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test] public void Checker_rejects_the_known_bad_lines()
        {
            Assert.IsNotEmpty(Problems("Complete the valley survey is complete."));
            Assert.IsNotEmpty(Problems("Getting there: Wire settlement power opens once Restore the market hall is restored."));
            Assert.IsNotEmpty(Problems("Move Clear the old trailhead up"));
            Assert.IsNotEmpty(Problems("Source-to-Sound Route route is ready."));
            Assert.IsEmpty(Problems("The valley survey is complete."));
        }

        [Test] public void Shell_templates_read_well_for_every_project_title()
        {
            var titles = Region1Catalog.Create().Projects.Select(p => p.TitleKey).ToList();
            var lines = new List<string>();
            foreach (var t in titles)
            {
                lines.Add(PlayerStrings.F("label.moveUp", t));
                lines.Add(ShellController.QueueLabel(t));
                lines.Add(PlayerStrings.F("shell.story.path.lockedLater", t));
                lines.Add(PlayerStrings.F("shell.projects.opens", t));
                lines.Add(PlayerStrings.F("shell.discoveries.ahead", t));
                lines.Add(PlayerStrings.F("label.vitalityInvested", t, 40));
                lines.Add(PlayerStrings.F("scene.alt.build", t, 1, 3));
                foreach (var other in titles)
                {
                    lines.Add(PlayerStrings.F("shell.story.path.locked.one", t, other));
                    lines.Add(PlayerStrings.F("shell.story.path.locked.many", t, other + ", " + other));
                }
            }
            Assert.AreEqual(titles.Count * (7 + 2 * titles.Count), lines.Count);
            AssertClean(lines, "template");
        }

        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        private static readonly string[] Screens = { "Home", "Projects", "Story", "Region", "Discoveries", "Progress", "ReturnSummary" };

        private static IEnumerable<string> Everything(ShellController c)
        {
            foreach (var s in Screens)
            {
                c.Invoke("nav:" + s);
                var m = c.Render();
                foreach (var l in m.Lines) yield return s + "|" + l;
                foreach (var a in m.Actions) yield return s + "|button|" + a.Label;
            }
        }

        [Test] public void Every_screen_reads_well_midgame_and_on_the_celebration()
        {
            var dir = Path.Combine(Path.GetTempPath(), "wg-copy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var clock = new ManualClock(T0);
                var session = ShellBootstrap.CreateSession(dir, new ShellConnectionPort(true), clock);
                var c = new ShellController(session, () => 42UL, devTools: true);
                c.Boot(); c.Invoke("start");
                var source = new SyntheticWalkingSource(20000);
                var seen = new List<string>();
                bool sawMidGame = false;
                for (int day = 1; day <= 220 && !session.GetRegion().RegionCompleted; day++)
                {
                    clock.Set(T0.AddDays(day));
                    session.IngestFromSource(source, T0.AddDays(day - 1), T0.AddDays(day));
                    if (session.GetRegion().RegionCompleted) break; // keep the celebration unacknowledged
                    // Keep two or three projects queued so "Move up" buttons and locked story paths show.
                    foreach (var p in session.GetProjects().Projects)
                        if (session.GetHome().Queued.Count < 2 && p.Status == ProjectStatus.Available) session.EnqueueProject(p.ProjectId);
                    if (day % 9 == 0) { seen.AddRange(Everything(c)); sawMidGame = true; }
                    else { c.Invoke("nav:ReturnSummary"); seen.AddRange(c.Render().Lines.Select(l => "ReturnSummary|" + l)); }
                    if (session.GetPendingReturnSummary() != null) c.Invoke("ack");
                }
                Assert.IsTrue(sawMidGame);
                Assert.IsTrue(session.GetRegion().RegionCompleted);
                c.Invoke("nav:ReturnSummary");
                var celebration = c.Render();
                Assert.AreEqual("The valley is restored", celebration.Title);
                CollectionAssert.DoesNotContain(celebration.Lines, "Complete the valley survey is complete.");
                seen.AddRange(celebration.Lines.Select(l => "Celebration|" + l));
                seen.AddRange(Everything(c));
                AssertClean(seen.Distinct(), "screen");
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
