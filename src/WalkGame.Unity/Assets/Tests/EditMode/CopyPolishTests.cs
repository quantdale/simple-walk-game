using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Application.Activity;
using WalkGame.Domain.Time;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>
    /// D-096 copy polish: every player-facing string in the shell passes the no-guilt check and
    /// never shows raw status codes, ids or exception text. Unity-editor-tested.
    /// </summary>
    public sealed class CopyPolishTests
    {
        /// <summary>Pressure, blame or loss framing. "Nothing is lost" reassurance is allowed; "lost" alone is not scanned here.</summary>
        public static readonly string[] GuiltPhrases =
        {
            "missed", "streak", "behind", "lazy", "penalty", "failed", "keep walking", "don't forget",
            "do not forget", "hurry", "you should", "only walked", "you haven't", "you have not walked", "!",
        };

        private static readonly Regex RawCode = new Regex(@"\([A-Z][A-Za-z]+\)|[a-z]+\.[a-z]+\.[a-z]|Exception|[A-Z][a-z]+[A-Z][a-z]+[A-Z]");

        private static string[] Literals(string file)
        {
            var src = File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Shell", file));
            // Player copy only: skip comments, exception messages and dev-only lines.
            var lines = src.Split('\n').Where(l => !l.TrimStart().StartsWith("//") && !l.Contains("throw new") && !l.Contains("dev:") && !l.Contains("Development"));
            return lines.SelectMany(l => Regex.Matches(l, "\"((?:[^\"\\\\]|\\\\.)*)\"").Cast<Match>().Select(m => m.Groups[1].Value))
                        .Where(s => s.Contains(" ")).ToArray();
        }

        [TestCase("ShellController.cs")]
        [TestCase("PlayerCopy.cs")]
        [TestCase("PlayerStrings.cs")] // D-120: boot copy moved into the table; the scan now covers every table string
        public void Shell_copy_passes_the_no_guilt_check(string file)
        {
            var literals = Literals(file);
            Assert.That(literals.Length, Is.GreaterThanOrEqualTo(4), "scanner found no copy in " + file);
            foreach (var s in literals)
                foreach (var g in GuiltPhrases)
                    StringAssert.DoesNotContain(g, s.ToLowerInvariant(), file + ": \"" + s + "\"");
        }

        [Test] public void Every_activity_status_and_action_reads_calmly()
        {
            foreach (ActivityPlayerStatus st in Enum.GetValues(typeof(ActivityPlayerStatus)))
                Check(PlayerCopy.ActivityStatus(st), st.ToString());
            foreach (ActivityRecommendedAction a in Enum.GetValues(typeof(ActivityRecommendedAction)))
            {
                var text = PlayerCopy.ActivityAction(a);
                if (text != null) Check(text, a.ToString());
            }
        }

        [Test] public void Every_boot_outcome_reassures_and_shows_no_codes()
        {
            foreach (StartStatus st in Enum.GetValues(typeof(StartStatus)))
            {
                var notice = BootRouter.Route(st).Notice;
                if (notice == null) continue;
                Check(notice, st.ToString());
            }
        }

        [Test] public void Error_notices_never_echo_ids_or_codes()
        {
            var dir = Path.Combine(Path.GetTempPath(), "wg-copy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var c = new ShellController(ShellBootstrap.CreateSession(dir, new ShellConnectionPort(true), new ManualClock(new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero))), () => 42UL, devTools: false);
                c.Boot(); c.Invoke("start");
                foreach (var bad in new[] { "bogus:thing", "nav:Nowhere", "dev:walk", "queue:proj.nope" })
                {
                    c.Invoke(bad);
                    var n = c.Render().Notice ?? "";
                    StringAssert.DoesNotContain("bogus", n, bad);
                    StringAssert.DoesNotContain("proj.", n, bad);
                    Check(n, bad);
                }
            }
            finally { Directory.Delete(dir, true); }
        }

        /// <summary>D-097: every Home line (Glance sentences, story line, balances, empty state) across fresh, walked and long-away states.</summary>
        [Test] public void Home_summary_lines_read_calmly_across_states()
        {
            var dir = Path.Combine(Path.GetTempPath(), "wg-home-copy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                // dev:walk ingests the day before the real UtcNow, so the session clock starts there too.
                var clock = new ManualClock(DateTimeOffset.UtcNow);
                var c = new ShellController(ShellBootstrap.CreateSession(dir, new ShellConnectionPort(true), clock), () => 42UL, devTools: true);
                c.Boot(); c.Invoke("start");
                for (int i = 0; i < 12 && c.Current == ShellScreen.Guide; i++)
                {
                    var g = c.Render();
                    var q = g.Actions.FirstOrDefault(a => a.Id.StartsWith("queue:"));
                    c.Invoke(g.Actions.Any(a => a.Id == "guide:next") ? "guide:next" : q?.Id ?? g.Actions[0].Id);
                }
                int checkedLines = 0;
                void Scan(string ctx)
                {
                    c.Invoke("nav:Home");
                    var r = c.Render();
                    Assert.AreEqual(ShellScreen.Home, c.Current, ctx);
                    foreach (var line in r.Lines) { Check(line, ctx); checkedLines++; }
                }
                Scan("fresh");
                var freshLines = string.Join("\n", c.Render().Lines);
                for (int d = 0; d < 5; d++) c.Invoke("dev:walk");
                Scan("after walking");
                Assert.AreNotEqual(freshLines, string.Join("\n", c.Render().Lines), "walking changed nothing on Home; the 'after walking' state is not real");
                clock.Advance(TimeSpan.FromDays(30));
                c.Boot();
                Scan("after 30 days away");
                Assert.That(checkedLines, Is.GreaterThanOrEqualTo(12), "too few Home lines scanned");
            }
            finally { Directory.Delete(dir, true); }
        }

        /// <summary>D-097: settings status lines are full calm sentences, never a bare "X: on".</summary>
        [Test] public void Settings_status_lines_are_plain_sentences()
        {
            foreach (var on in new[] { true, false })
                foreach (var line in new[] { ShellController.ReducedMotionLine(on), ShellController.SoundLine(on), ShellController.DiagnosticsLine(on) })
                {
                    Check(line, "settings " + on);
                    StringAssert.DoesNotContain(": ", line);
                    StringAssert.EndsWith(".", line);
                }
        }

        private static void Check(string text, string context)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(text), context + " has no copy");
            foreach (var g in GuiltPhrases) StringAssert.DoesNotContain(g, text.ToLowerInvariant(), context + ": " + text);
            Assert.IsFalse(RawCode.IsMatch(text), context + " shows a code: " + text);
        }
    }
}
