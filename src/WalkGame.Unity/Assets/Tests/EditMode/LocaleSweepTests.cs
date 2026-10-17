using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using WalkGame.Application.Content;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>
    /// D-195 localization readiness at runtime: with a pseudo-locale over EVERY shell string key set
    /// after start-up, no screen may still show an English shell sentence. This catches copy that was
    /// captured once at type initialisation (Privacy lines were) or that bypasses the table. Content
    /// copy (project titles, letters) has its own overlay (D-122) and is not part of this sweep.
    /// Unity-editor-tested.
    /// </summary>
    public sealed class LocaleSweepTests
    {
        private string _dir = "";
        [SetUp] public void SetUp() { PlayerStrings.Locale = ContentText.English; _dir = Path.Combine(Path.GetTempPath(), "wg-l10n-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { PlayerStrings.Locale = ContentText.English; if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private static readonly Regex Placeholder = new Regex(@"\{\d+\}");

        /// <summary>Pseudo text: the English upper-cased inside markers, placeholders kept, so word
        /// order and arguments survive and any untranslated English stays detectable.</summary>
        private static string Pseudo(string english) =>
            "[[" + Placeholder.Replace(english.ToUpperInvariant(), m => m.Value) + "]]";

        private static ContentText PseudoLocale()
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var k in PlayerStrings.Keys)
                d[ContentText.Key(PlayerStrings.OverlayId(k), PlayerStrings.Field)] = Pseudo(PlayerStrings.T(k));
            return ContentText.ForLocale("xx", d);
        }

        /// <summary>English shell sentences worth detecting: at least three words, split at placeholders.</summary>
        private static List<(string key, string fragment)> EnglishFragments()
        {
            var list = new List<(string, string)>();
            foreach (var k in PlayerStrings.Keys)
                foreach (var part in Placeholder.Split(PlayerStrings.T(k)))
                {
                    var f = part.Trim();
                    if (f.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length >= 3 && f.Any(char.IsLower)) list.Add((k, f));
                }
            return list;
        }

        private static IEnumerable<string> Texts(ScreenModel m)
        {
            yield return m.Title;
            if (m.Notice != null) yield return m.Notice;
            foreach (var l in m.Lines) yield return l;
            foreach (var a in m.Actions) yield return a.Label;
            if (m.Scene != null) yield return m.Scene.AltText;
        }

        private static void FinishOnboarding(ShellController c)
        {
            for (int i = 0; i < 12 && c.Current == ShellScreen.Guide; i++)
            {
                var m = c.Render();
                var next = m.Actions.FirstOrDefault(a => a.Id == "guide:next") ?? m.Actions.FirstOrDefault(a => a.Id.StartsWith("queue:", StringComparison.Ordinal));
                if (next == null) break;
                c.Invoke(next.Id);
            }
        }

        [Test] public void The_pseudo_locale_keeps_placeholders_and_marks_every_key()
        {
            PlayerStrings.Locale = PseudoLocale();
            Assert.AreEqual("[[VITALITY 1 · MATERIALS 2 · KNOWLEDGE 3]]", PlayerStrings.F("label.resources", 1L, 2L, 3L));
            foreach (var k in PlayerStrings.Keys) StringAssert.StartsWith("[[", PlayerStrings.T(k), k);
        }

        [Test] public void Privacy_lines_follow_a_locale_chosen_after_they_were_first_read()
        {
            var english = ShellController.PrivacyLines.ToArray();
            CollectionAssert.IsNotEmpty(english);
            PlayerStrings.Locale = PseudoLocale();
            var c = new ShellController(ShellBootstrap.CreateSession(_dir), () => 42UL, devTools: true);
            c.Boot(); c.Invoke("start"); FinishOnboarding(c);
            c.Invoke("nav:Settings"); c.Invoke("nav:Privacy");
            var lines = c.Render().Lines;
            foreach (var e in english) CollectionAssert.DoesNotContain(lines, e);
            CollectionAssert.AreEqual(english.Select(Pseudo), ShellController.PrivacyLines);
        }

        [Test] public void No_screen_shows_an_english_shell_sentence_under_another_locale()
        {
            var c = new ShellController(ShellBootstrap.CreateSession(_dir), () => 42UL, devTools: true);
            c.Boot(); c.Invoke("start"); FinishOnboarding(c);
            c.Invoke("dev:walk");
            // The locale arrives after every shell type has been initialised, as a language switch would.
            foreach (var s in Enum.GetNames(typeof(ShellScreen))) { c.Invoke("nav:" + s); c.Render(); }
            var fragments = EnglishFragments(); // read while English is still the locale
            Assert.Greater(fragments.Count, 50);
            PlayerStrings.Locale = PseudoLocale();
            var leaks = new List<string>();
            int screens = 0;
            void Check(string where)
            {
                var m = c.Render(); screens++;
                foreach (var t in Texts(m))
                    foreach (var (key, f) in fragments)
                        if (t != null && t.Contains(f)) leaks.Add(where + " [" + key + "]: " + t);
            }
            foreach (var s in new[] { "Home", "Projects", "Settings", "ActivityStatus", "Region", "Story", "Discoveries", "Progress", "Privacy" })
            {
                c.Invoke("nav:" + s);
                Assert.AreEqual(s, c.Current.ToString(), "reached " + s);
                Assert.IsTrue(Texts(c.Render()).Any(t => t != null && t.Contains("[[")), s + " shows translated shell copy");
                Check(s);
            }
            c.Invoke("nav:Settings"); c.Invoke("delete:request"); Check("DeleteConfirm"); c.Invoke("delete:cancel");
            Assert.GreaterOrEqual(screens, 10);
            Assert.IsEmpty(leaks, string.Join("\n", leaks.Distinct()));
        }
    }
}
