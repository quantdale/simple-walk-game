using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine.UIElements;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Time;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>
    /// KI-023 / D-122: the Discoveries screen rendered (ShellView into a VisualElement tree) with a
    /// test locale. Discovery titles, body and origin copy plus expedition and project titles come
    /// from the overlay; ids never leak; save bytes are untouched. Unity-editor-tested; device UNVERIFIED.
    /// </summary>
    public sealed class DiscoveriesLocaleRenderTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        private string _dir = "";
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-discl-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private const string X = "[xx] ";

        private static ContentText Pseudo()
        {
            var c = Region1Catalog.Create();
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            void Add(string id, string field, string en) { if (!string.IsNullOrEmpty(en)) d[ContentText.Key(id, field)] = X + en; }
            foreach (var p in c.Projects) Add(p.Id.Value, ContentText.Fields.Title, p.TitleKey);
            foreach (var l in c.Landmarks) Add(l.Id.Value, ContentText.Fields.Title, l.TitleKey);
            foreach (var e in c.Expeditions) Add(e.Id.Value, ContentText.Fields.Title, e.TitleKey);
            foreach (var disc in c.Discoveries)
            {
                Add(disc.Id.Value, ContentText.Fields.Title, disc.TitleKey);
                Add(disc.Id.Value, ContentText.Fields.Body, Region1DiscoveryCopy.Text(disc.BodyKey));
                Add(disc.Id.Value, ContentText.Fields.Provenance, Region1DiscoveryCopy.Text(disc.ProvenanceKey));
            }
            return ContentText.ForLocale("xx", d);
        }

        private (ShellController c, GameSession s) Play(int days)
        {
            var clock = new ManualClock(T0);
            var s = ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(true), clock);
            var c = new ShellController(s, () => 42UL, devTools: true);
            c.Boot(); c.Invoke("start");
            var src = new SyntheticWalkingSource(20000);
            for (int d = 1; d <= days; d++)
            {
                clock.Set(T0.AddDays(d));
                s.IngestFromSource(src, T0.AddDays(d - 1), T0.AddDays(d));
                var h = s.GetHome();
                if (h.ActiveProjectId == null && h.Queued.Count == 0)
                    foreach (var p in s.GetProjects().Projects)
                        if (p.Status == ProjectStatus.Available && s.EnqueueProject(p.ProjectId).IsSuccess) break;
            }
            return (c, s);
        }

        private static List<string> RenderedLabels(ScreenModel m)
        {
            var root = new VisualElement();
            ShellView.Render(root, m, _ => { });
            return root.Query<TextElement>().ToList().Select(t => t.text ?? "").Where(t => t.Length > 0).ToList();
        }

        [Test] public void Discoveries_render_through_a_test_locale_without_leaking_ids_or_touching_saves()
        {
            var (c, s) = Play(40);
            var english = s.GetDiscoveries().Discoveries.Where(r => r.Unlocked).ToList();
            Assume.That(english.Count, Is.GreaterThan(0), "fixture should have found something in 40 days");
            var saves = Directory.GetFiles(_dir, "save*.json").ToDictionary(f => f, File.ReadAllText);

            s.ContentLocale = Pseudo();
            c.Invoke("nav:Discoveries");
            var m = c.Render();
            Assert.AreEqual(ShellScreen.Discoveries, m.Screen);
            var labels = RenderedLabels(m);
            var all = string.Join("\n", labels);

            foreach (var row in english)
            {
                StringAssert.Contains(X + row.TitleKey, all, "discovery title not localized: " + row.DiscoveryId);
                var body = Region1DiscoveryCopy.Text(row.BodyKey);
                if (!string.IsNullOrEmpty(body)) StringAssert.Contains(X + body, all);
            }
            foreach (var x in s.GetExpeditions().Expeditions)
            {
                StringAssert.StartsWith(X, x.TitleKey, "expedition title not localized: " + x.ExpeditionId);
                StringAssert.Contains(x.TitleKey, all, "expedition line missing: " + x.ExpeditionId);
            }
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(all, @"\b(disc|exp|proj|lm|care)\.[a-z]"), "a stable id leaked:\n" + all);
            Assert.IsFalse(all.Contains(X + X), "double overlay");
            foreach (var kv in saves) Assert.AreEqual(kv.Value, File.ReadAllText(kv.Key), "a locale must never change save bytes");

            s.ContentLocale = ContentText.English;
            var en = string.Join("\n", RenderedLabels(c.Render()));
            Assert.IsFalse(en.Contains(X), "English must be restored byte-for-byte");
        }
    }
}
