using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application.Content;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>D-126 (KI-023): shell strings take a locale with the D-122 fallback rules. Unity-editor-tested.</summary>
    public sealed class PlayerStringsLocaleTests
    {
        private string _dir = "";
        [SetUp] public void SetUp() { PlayerStrings.Locale = ContentText.English; _dir = Path.Combine(Path.GetTempPath(), "wg-psloc-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { PlayerStrings.Locale = ContentText.English; if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private static ContentText Locale(params (string key, string text)[] entries)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (k, t) in entries) d[ContentText.Key(PlayerStrings.OverlayId(k), PlayerStrings.Field)] = t;
            return ContentText.ForLocale("xx", d);
        }

        [Test] public void English_is_the_default_and_byte_identical()
        {
            var before = PlayerStrings.Keys.ToDictionary(k => k, PlayerStrings.T);
            PlayerStrings.Locale = ContentText.English;
            foreach (var kv in before) Assert.AreSame(kv.Value, PlayerStrings.T(kv.Key));
            PlayerStrings.Locale = null;
            foreach (var kv in before) Assert.AreEqual(kv.Value, PlayerStrings.T(kv.Key));
        }

        [Test] public void A_locale_entry_replaces_text_and_missing_or_blank_entries_fall_back()
        {
            string en = PlayerStrings.T("settings.sound.on");
            string other = PlayerStrings.T("settings.sound.off");
            PlayerStrings.Locale = Locale(("settings.sound.on", "[xx] sound"), ("settings.sound.off", "   "));
            Assert.AreEqual("[xx] sound", PlayerStrings.T("settings.sound.on"));
            Assert.AreEqual(other, PlayerStrings.T("settings.sound.off"));
            Assert.AreNotEqual(en, PlayerStrings.T("settings.sound.on"));
        }

        [Test] public void Templates_reorder_and_a_broken_template_falls_back_to_English()
        {
            string expected = PlayerStrings.F("reminder.on", "08:00");
            PlayerStrings.Locale = Locale(("reminder.on", "[xx] {0} ok"));
            Assert.AreEqual("[xx] 08:00 ok", PlayerStrings.F("reminder.on", "08:00"));
            PlayerStrings.Locale = Locale(("reminder.on", "[xx] {7}"));
            Assert.AreEqual(expected, PlayerStrings.F("reminder.on", "08:00"));
        }

        [Test] public void An_overlay_cannot_add_keys()
        {
            PlayerStrings.Locale = Locale(("no.such.key", "[xx] leak"));
            Assert.AreEqual(string.Empty, PlayerStrings.T("no.such.key"));
        }

        [Test] public void SetLocale_drives_shell_and_content_together_and_never_touches_saves()
        {
            var s = ShellBootstrap.CreateSession(_dir);
            var c = new ShellController(s, () => 42UL, devTools: true);
            c.Boot(); c.Invoke("start");
            var saves = Directory.GetFiles(_dir, "save*.json").ToDictionary(f => f, File.ReadAllText);
            var loc = Locale(("settings.sound.on", "[xx] sound"), ("settings.sound.off", "[xx] quiet"));
            c.SetLocale(loc);
            Assert.AreSame(loc, s.ContentLocale);
            Assert.AreSame(loc, PlayerStrings.Locale);
            c.Invoke("nav:Settings");
            var all = string.Join("\n", c.Render().Lines);
            StringAssert.Contains("[xx] ", all);
            foreach (var kv in saves) Assert.AreEqual(kv.Value, File.ReadAllText(kv.Key));
            c.SetLocale(null);
            Assert.AreSame(ContentText.English, PlayerStrings.Locale);
            Assert.IsFalse(string.Join("\n", c.Render().Lines).Contains("[xx]"));
        }
    }
}
