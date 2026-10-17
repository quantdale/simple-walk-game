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

        [Test] public void D148_text_capture_covers_home_summary_settings_with_the_payoff_state()
        {
            string tools = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "..", "tools", "unity"));
            string script = Path.Combine(tools, "capture-text-screens.sh");
            Assert.IsTrue(File.Exists(script), "text capture script missing: " + script);
            string text = File.ReadAllText(script);
            foreach (var spec in new[] { "|Home|payoff|1", "|ReturnSummary|payoff|1", "|Settings|", "|Home|payoff|2" })
                StringAssert.Contains(spec, text, "text capture does not include " + spec);
            foreach (var screen in new[] { "Home", "ReturnSummary", "Settings" })
                Assert.IsTrue(Enum.GetNames(typeof(ShellScreen)).Contains(screen), screen);
            string capture = File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Tests", "PlayMode", "ValleyCaptureTests.cs"));
            StringAssert.Contains("capState == \"payoff\"", capture, "ValleyCaptureTests no longer seeds the D-145 payoff state");
        }
    }
}
