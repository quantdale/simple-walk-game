using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>
    /// D-120 localization readiness. Player strings go through <see cref="PlayerStrings"/>. A
    /// source scan of Assets/Shell counts multi-word string literals (the shape of player copy).
    /// Migrated files must have none; every other file is pinned at its current count and may only
    /// go DOWN (lower the pin when you migrate). A literal that is not player copy carries
    /// "// l10n-ignore: reason" on its line. Unity-editor-tested.
    /// </summary>
    public sealed class LocalizationReadinessTests
    {
        private static readonly Regex Literal = new Regex("\"(?:[^\"\\\\]|\\\\.)*\"");
        private static readonly Regex Words = new Regex("[A-Za-z]{2,}[^\"]*\\s[^\"]*[A-Za-z]{2,}");

        /// <summary>Files fully routed through the table: zero hard-coded player literals.</summary>
        private static readonly string[] Migrated = { "BootRouter.cs", "ReminderScheduler.cs", "PlayerCopy.cs", "RegionScene.cs" };

        /// <summary>Ratchet ceilings for files not yet migrated (2026-10-16). Lower them; never raise.</summary>
        private static readonly Dictionary<string, int> Ceiling = new Dictionary<string, int>
        {
            ["AccessibleOutline.cs"] = 1, ["AndroidShareSheet.cs"] = 2, ["ShellBootstrap.cs"] = 3, ["ShellConnectionPort.cs"] = 2,
            ["ShellController.cs"] = 127,
        };

        private static string ShellDir => Path.Combine(UnityEngine.Application.dataPath, "Shell");

        public static int CountPlayerLiterals(IEnumerable<string> lines)
        {
            int n = 0;
            foreach (var line in lines)
            {
                var t = line.Trim();
                if (t.StartsWith("//") || line.Contains("l10n-ignore")) continue;
                foreach (Match m in Literal.Matches(line)) if (Words.IsMatch(m.Value)) n++;
            }
            return n;
        }

        [Test] public void The_scanner_counts_player_copy_and_ignores_comments_and_marked_lines()
        {
            Assert.AreEqual(1, CountPlayerLiterals(new[] { "x = \"Hello there, walker.\";" }));
            Assert.AreEqual(0, CountPlayerLiterals(new[] { "/// \"Doc text here\"", "x = \"nav:Home\";", "throw new X(\"Bad thing\"); // l10n-ignore: dev" }));
        }

        [Test] public void Migrated_files_have_no_hard_coded_player_strings()
        {
            foreach (var f in Migrated)
                Assert.AreEqual(0, CountPlayerLiterals(File.ReadAllLines(Path.Combine(ShellDir, f))), f);
        }

        [Test] public void No_shell_file_gains_hard_coded_player_strings()
        {
            foreach (var path in Directory.GetFiles(ShellDir, "*.cs"))
            {
                var name = Path.GetFileName(path);
                if (name == "PlayerStrings.cs" || Migrated.Contains(name)) continue;
                int n = CountPlayerLiterals(File.ReadAllLines(path));
                int max = Ceiling.TryGetValue(name, out var c) ? c : 0;
                Assert.LessOrEqual(n, max, name + ": route new player copy through PlayerStrings (D-120)");
            }
        }

        [Test] public void Every_table_entry_is_non_empty_and_its_template_formats()
        {
            Assert.IsNotEmpty(PlayerStrings.Keys.ToList());
            foreach (var k in PlayerStrings.Keys)
            {
                var v = PlayerStrings.T(k);
                Assert.IsFalse(string.IsNullOrWhiteSpace(v), k);
                Assert.DoesNotThrow(() => PlayerStrings.F(k, "a", "b", "c"), k);
            }
            Assert.AreEqual(string.Empty, PlayerStrings.T("no.such.key"), "a missing key never shows raw");
        }

        [Test] public void Migrated_player_copy_reads_exactly_as_before()
        {
            Assert.AreEqual("waiting, 2nd in line", PlayerCopy.ProjectStatus(WalkGame.Domain.Projects.ProjectStatus.Queued, 1));
            Assert.AreEqual("ready to start", PlayerCopy.ProjectStatus(WalkGame.Domain.Projects.ProjectStatus.Available, null));
            Assert.AreEqual("restored", PlayerCopy.ProjectStatus(WalkGame.Domain.Projects.ProjectStatus.Completed, null));
            Assert.AreEqual("Next: turn access back on in your phone's settings.",
                PlayerCopy.ActivityAction(WalkGame.Application.Activity.ActivityRecommendedAction.OpenSettings));
            Assert.AreEqual("plant life", PlayerCopy.DiscoveryCategory("flora"));
            Assert.AreEqual(" Footpaths link 3 recovered places.", PlayerStrings.F("scene.alt.trail.many", 3));
            Assert.AreEqual("A picture of the valley: 2 of 6 landmarks restored, the hills turning green.",
                PlayerStrings.F("scene.alt.progress", 2, 6, PlayerStrings.T("scene.alt.hills.greening")));
        }

        [Test] public void Every_key_used_in_shell_code_exists_in_the_table()
        {
            var use = new Regex("PlayerStrings\\.[TF]\\(\"([^\"]+)\"");
            foreach (var path in Directory.GetFiles(ShellDir, "*.cs"))
                foreach (Match m in use.Matches(File.ReadAllText(path)))
                    Assert.IsTrue(PlayerStrings.Has(m.Groups[1].Value), Path.GetFileName(path) + ": " + m.Groups[1].Value);
        }
    }
}
