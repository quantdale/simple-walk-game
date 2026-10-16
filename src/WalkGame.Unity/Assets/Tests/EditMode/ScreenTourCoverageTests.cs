using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>
    /// Region 1 hardening (grok2230): the editor-rendered screen tour
    /// (tools/unity/capture-screen-tour.sh, D-107) must list every <see cref="ShellScreen"/>, so a new
    /// screen cannot ship without a reviewable PNG, and it must not list names that no longer exist.
    /// "Celebration" is the one non-enum entry (a ReturnSummary state reached from a completed region).
    /// </summary>
    public sealed class ScreenTourCoverageTests
    {
        private static string[] TourScreens()
        {
            string script = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "..", "tools", "unity", "capture-screen-tour.sh"));
            Assert.IsTrue(File.Exists(script), "screen tour script missing: " + script);
            var m = Regex.Match(File.ReadAllText(script), "^screens=\"([^\"]*)\"", RegexOptions.Multiline);
            Assert.IsTrue(m.Success, "screens=\"...\" list not found in capture-screen-tour.sh");
            return m.Groups[1].Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        [Test] public void Tour_lists_every_shell_screen()
        {
            var tour = TourScreens();
            var missing = Enum.GetNames(typeof(ShellScreen)).Where(n => !tour.Contains(n)).ToArray();
            Assert.IsEmpty(missing, "screen tour is missing: " + string.Join(", ", missing));
        }

        [Test] public void Tour_lists_only_real_screens_once()
        {
            var tour = TourScreens();
            var unknown = tour.Where(n => n != "Celebration" && !Enum.GetNames(typeof(ShellScreen)).Contains(n)).ToArray();
            Assert.IsEmpty(unknown, "screen tour names unknown screens: " + string.Join(", ", unknown));
            Assert.AreEqual(tour.Length, tour.Distinct().Count(), "screen tour lists a screen twice");
        }
    }
}
