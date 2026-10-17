using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Application.Summaries;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Time;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>D-138 (KI-023 remainder): stored summary lines and single-word labels follow the locale,
    /// display-only, English fallback, saves untouched. Unity-editor-tested.</summary>
    public sealed class SummaryAndLabelLocaleTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);
        private string _dir = "";
        [SetUp] public void SetUp() { PlayerStrings.Locale = ContentText.English; _dir = Path.Combine(Path.GetTempPath(), "wg-sumloc-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { PlayerStrings.Locale = ContentText.English; if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private static ContentText Pseudo()
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var k in PlayerStrings.Keys.Where(k => k.StartsWith("label.", StringComparison.Ordinal)))
                d[ContentText.Key(PlayerStrings.OverlayId(k), PlayerStrings.Field)] = "[xx] " + PlayerStrings.T(k);
            foreach (var n in new[] { "project-complete", "project-ready", "vitality-credited", "new-chapter", "more-ready" })
                d[ContentText.Key(SummaryLineText.PatternId(n), SummaryLineText.Field)] = "[xx-sum] {0}";
            return ContentText.ForLocale("xx", d);
        }

        [Test] public void English_labels_are_unchanged()
        {
            Assert.AreEqual("Home", PlayerStrings.T("label.home"));
            Assert.AreEqual("Earlier: x", PlayerStrings.F("label.earlier", "x"));
            Assert.AreEqual("Vitality 1 · Materials 2 · Knowledge 3", PlayerStrings.F("label.resources", 1L, 2L, 3L));
            Assert.AreEqual("Start: Clear it", ShellController.QueueLabel("Clear it"));
        }

        [Test] public void Return_summary_and_nav_labels_follow_the_locale_without_touching_the_save()
        {
            var s = ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(true), new ManualClock(T0));
            var c0 = new ShellController(s, () => 42UL, devTools: false);
            c0.Boot(); c0.Invoke("start");
            for (int i = 0; i < 12 && c0.Current == ShellScreen.Guide; i++)
            {
                var gm = c0.Render();
                var q = gm.Actions.FirstOrDefault(a => a.Id.StartsWith("queue:"));
                c0.Invoke(gm.Actions.Any(a => a.Id == "guide:next") ? "guide:next" : q?.Id ?? gm.Actions[0].Id);
            }
            foreach (var p in s.GetProjects().Projects.Where(p => p.Status == ProjectStatus.Available)) s.EnqueueProject(p.ProjectId);
            if (s.GetPendingReturnSummary() != null) s.AcknowledgeReturnSummary();
            var back = T0.AddDays(7);
            var later = ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(true), new ManualClock(back));
            Assert.AreEqual(StartStatus.Loaded, later.Continue().Status);
            later.IngestFromSource(new SyntheticWalkingSource(7000), T0, back);

            var s2 = ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(true), new ManualClock(back.AddMinutes(1)));
            var c = new ShellController(s2, () => 42UL, devTools: false);
            c.Boot();
            Assert.AreEqual(ShellScreen.ReturnSummary, c.Current);
            var english = c.Render();
            var saves = Directory.GetFiles(_dir, "*", SearchOption.AllDirectories).ToDictionary(f => f, File.ReadAllBytes);

            c.SetLocale(Pseudo());
            var local = c.Render();
            Assert.AreEqual(ShellScreen.ReturnSummary, c.Current);
            StringAssert.StartsWith("[xx] ", local.Actions.First(a => a.Id == "ack").Label);
            Assert.IsTrue(local.Lines.Any(l => l.Contains("[xx-sum]")), "a stored summary line is shown translated:\n" + string.Join("\n", local.Lines));
            Assert.IsFalse(local.Lines.Any(l => l.Contains("summary.")), "never a raw key");
            Assert.AreEqual(english.Lines.Count, local.Lines.Count, "localizing never drops or adds a line (moments still parse the stored English)");
            foreach (var kv in saves) CollectionAssert.AreEqual(kv.Value, File.ReadAllBytes(kv.Key));

            c.SetLocale(null);
            CollectionAssert.AreEqual(english.Lines, c.Render().Lines);
        }
    }
}
