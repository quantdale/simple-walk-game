using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine.UIElements;
using WalkGame.Application;
using WalkGame.Application.Development;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Time;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>
    /// D-199 audit gap: the D-095/D-147 audits render each screen once, in a brand-new world
    /// (no valley picture, no completions, no celebration, no welcome-back line). This repeats the
    /// label / focus-order / 48 dp / 2x-text / no-animation checks on a PLAYED world returning after
    /// two weeks, where every screen carries its richest content. Unity-editor-tested; TalkBack on
    /// hardware UNVERIFIED (KI-002).
    /// </summary>
    public sealed class AccessibilityPlayedWorldTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);
        private static readonly float[] Scales = { 1f, 1.3f, 2f };
        private string _dir = "";
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-a11y-played-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private Dictionary<string, ScreenModel> PlayedScreens()
        {
            var s = ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(true), new ManualClock(T0));
            var c0 = new ShellController(s, () => 42UL, devTools: false);
            c0.Boot(); c0.Invoke("start");
            for (int i = 0; i < 12 && c0.Current == ShellScreen.Guide; i++)
            {
                var m = c0.Render();
                var q = m.Actions.FirstOrDefault(a => a.Id.StartsWith("queue:"));
                c0.Invoke(m.Actions.Any(a => a.Id == "guide:next") ? "guide:next" : q?.Id ?? m.Actions[0].Id);
            }
            foreach (var p in s.GetProjects().Projects.Where(p => p.Status == ProjectStatus.Available)) s.EnqueueProject(p.ProjectId);
            if (s.GetPendingReturnSummary() != null) s.AcknowledgeReturnSummary();

            var back = T0.AddDays(15);
            var later = ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(true), new ManualClock(back));
            Assert.AreEqual(StartStatus.Loaded, later.Continue().Status);
            later.IngestFromSource(new SyntheticWalkingSource(9000), T0, back);

            var c = new ShellController(ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(true), new ManualClock(back.AddMinutes(1))), () => 42UL, devTools: false);
            c.Boot();
            var all = new Dictionary<string, ScreenModel> { ["ReturnSummary"] = c.Render() };
            Assume.That(c.Current, Is.EqualTo(ShellScreen.ReturnSummary));
            foreach (var screen in new[] { "Home", "Projects", "Region", "Story", "Discoveries", "Progress", "Settings", "Privacy", "ActivityStatus", "More", "More" })
            {
                c.Invoke("nav:" + screen);
                Assert.AreEqual(screen, c.Current.ToString(), "could not reach " + screen);
                all[screen] = c.Render();
            }
            return all;
        }

        [Test] public void Played_world_has_rich_content_to_audit()
        {
            var all = PlayedScreens();
            Assert.IsTrue(all.Values.Any(m => m.Scene != null), "no screen carried the valley picture");
            Assert.That(all["ReturnSummary"].Lines.Count, Is.GreaterThan(1));
        }

        [Test] public void Every_played_screen_has_unique_human_labels_and_reading_order_focus()
        {
            foreach (var kv in PlayedScreens())
            {
                var labels = kv.Value.Actions.Select(a => a.Label).ToList();
                foreach (var l in labels)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(l), kv.Key + ": unlabelled action");
                    // An id leaks as a space-free token with a separator ("nav:Home", "proj.bridge"); "Start: Rebuild the trail bridges" is prose.
                    Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(l, @"\b[A-Za-z0-9]+[:._][A-Za-z0-9]"), kv.Key + " shows an id-like label: " + l);
                }
                Assert.AreEqual(labels.Count, labels.Distinct(StringComparer.OrdinalIgnoreCase).Count(), kv.Key + ": two controls read the same: " + string.Join(" | ", labels));

                var root = new VisualElement();
                ShellView.Render(root, kv.Value, _ => { });
                var buttons = root.Query<Button>().ToList();
                CollectionAssert.AreEqual(kv.Value.Actions.Select(a => a.Id), buttons.Select(b => b.name), kv.Key + ": focus order");
                foreach (var b in buttons)
                {
                    Assert.IsTrue(b.focusable && b.tabIndex >= 0, kv.Key + ": " + b.text + " not focusable");
                    Assert.AreEqual(b.text, b.tooltip, kv.Key + ": screen-reader label != visible label");
                }
                var outline = AccessibleOutline.Build(kv.Value);
                Assert.IsTrue(outline.All(i => !string.IsNullOrWhiteSpace(i.Label)), kv.Key + ": unlabelled screen-reader stop");
                CollectionAssert.AreEqual(kv.Value.Actions.Select(a => a.Id), outline.Where(i => i.Role == AccessibleRole.Button).Select(i => i.ActionId), kv.Key);
                if (kv.Value.Scene != null)
                    Assert.IsTrue(outline.Any(i => i.Role == AccessibleRole.Image && !string.IsNullOrWhiteSpace(i.Label)), kv.Key + ": valley picture has no description");
            }
        }

        [Test] public void Every_played_screen_keeps_48dp_targets_wraps_at_2x_and_never_animates()
        {
            foreach (var kv in PlayedScreens())
                foreach (var k in Scales)
                {
                    var root = new VisualElement();
                    ShellView.Render(root, kv.Value, _ => { }, k);
                    foreach (var b in root.Query<Button>().ToList())
                    {
                        Assert.That(b.style.minHeight.value.value, Is.GreaterThanOrEqualTo(48f), kv.Key + " @" + k + ": " + b.text);
                        Assert.That(b.style.minWidth.value.value, Is.GreaterThanOrEqualTo(48f), kv.Key + " @" + k + ": " + b.text);
                        Assert.AreEqual(WhiteSpace.Normal, b.style.whiteSpace.value, kv.Key + ": button text must wrap: " + b.text);
                    }
                    foreach (var l in root.Query<Label>().ToList())
                    {
                        Assert.AreEqual(WhiteSpace.Normal, l.style.whiteSpace.value, kv.Key + " @" + k + ": text must wrap: " + l.text);
                        // No ancestor may both fix its height and hide overflow: that clips grown text.
                        for (var p = l.parent; p != null && p != root; p = p.parent)
                        {
                            bool fixedHeight = p.style.height.keyword == StyleKeyword.Undefined && p.style.height.value.value > 0;
                            Assert.IsFalse(fixedHeight && p.style.overflow.value == Overflow.Hidden, kv.Key + " @" + k + ": '" + l.text + "' sits in a fixed-height clipping box " + p.name);
                        }
                    }
                    foreach (var e in root.Query<VisualElement>().ToList())
                    {
                        var d = e.style.transitionDuration;
                        Assert.IsTrue(d.keyword != StyleKeyword.Undefined || d.value == null || d.value.Count == 0, kv.Key + ": " + e.name + " animates");
                    }
                }
        }
    }
}
