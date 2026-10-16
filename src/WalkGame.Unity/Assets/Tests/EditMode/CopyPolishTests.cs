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
        [TestCase("BootRouter.cs")]
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

        private static void Check(string text, string context)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(text), context + " has no copy");
            foreach (var g in GuiltPhrases) StringAssert.DoesNotContain(g, text.ToLowerInvariant(), context + ": " + text);
            Assert.IsFalse(RawCode.IsMatch(text), context + " shows a code: " + text);
        }
    }
}
