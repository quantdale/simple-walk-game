using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using WalkGame.Application;
using WalkGame.Domain.Time;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>
    /// D-095 accessibility audit across EVERY ShellScreen: focus order, labels, minimum touch
    /// size, theme-colour contrast (WCAG 2.x formula) and reduced motion. Unity-editor-tested;
    /// TalkBack, switch access and physical touch on hardware are UNVERIFIED (KI-002).
    /// </summary>
    public sealed class AccessibilityAuditTests
    {
        /// <summary>Android's accessibility minimum touch target (48 dp); the shell aims higher (56).</summary>
        public const float AndroidMinTouchDp = 48f;

        private string _dir = "";
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-audit-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);

        private ShellController Shell() =>
            new ShellController(ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(true), new ManualClock(T0)), () => 42UL, devTools: false);

        /// <summary>One rendered model per ShellScreen value, reached the way a player reaches it.</summary>
        private Dictionary<ShellScreen, ScreenModel> EveryScreen()
        {
            var all = new Dictionary<ShellScreen, ScreenModel>();
            var c = Shell(); c.Boot();
            all[c.Current] = c.Render();                       // Onboarding (no save yet)
            c.Invoke("start");
            all[c.Current] = c.Render();                       // Guide
            for (int i = 0; i < 12 && c.Current == ShellScreen.Guide; i++)
            {
                var m = c.Render();
                if (!all.ContainsKey(ShellScreen.Guide)) all[ShellScreen.Guide] = m;
                var q = m.Actions.FirstOrDefault(a => a.Id.StartsWith("queue:"));
                c.Invoke(m.Actions.Any(a => a.Id == "guide:next") ? "guide:next" : q?.Id ?? m.Actions[0].Id);
            }
            foreach (var s in new[] { "Home", "ReturnSummary", "Projects", "Region", "Story", "Discoveries", "Progress", "Settings", "Privacy", "ActivityStatus" })
            {
                c.Invoke("nav:" + s);
                Assert.AreEqual(s, c.Current.ToString(), "could not reach " + s);
                all[c.Current] = c.Render();
            }
            c.Invoke("delete:request");
            all[c.Current] = c.Render();                       // DeleteConfirm
            c.Invoke("delete:cancel");

            // Both slots unreadable (primary and backup), so recovery cannot open the world.
            foreach (var f in Directory.GetFiles(_dir, "save*.json")) File.WriteAllText(f, "{ not a save");
            var broken = Shell(); broken.Boot();
            all[broken.Current] = broken.Render();             // BootError
            return all;
        }

        [Test] public void The_audit_covers_every_screen()
        {
            var all = EveryScreen();
            var missing = Enum.GetValues(typeof(ShellScreen)).Cast<ShellScreen>().Except(all.Keys).ToList();
            Assert.IsEmpty(missing, "unaudited: " + string.Join(", ", missing) + " | files: " + string.Join(", ", Directory.GetFiles(_dir).Select(Path.GetFileName)));
        }

        [Test] public void Focus_order_follows_reading_order_and_every_action_is_focusable()
        {
            foreach (var kv in EveryScreen())
            {
                var root = new VisualElement();
                ShellView.Render(root, kv.Value, _ => { });
                var buttons = root.Query<Button>().ToList();
                CollectionAssert.AreEqual(kv.Value.Actions.Select(a => a.Id), buttons.Select(b => b.name), kv.Key + ": button tree order != action order");
                foreach (var b in buttons)
                {
                    Assert.IsTrue(b.focusable, kv.Key + ": " + b.text + " not focusable");
                    Assert.That(b.tabIndex, Is.GreaterThanOrEqualTo(0), kv.Key + ": " + b.text + " skipped by focus");
                }
                // The title precedes every button in the tree, so focus/swipe reads the screen first.
                var all = root.Query<VisualElement>().ToList();
                int title = all.IndexOf(root.Q<Label>("title"));
                Assert.IsTrue(buttons.All(b => all.IndexOf(b) > title), kv.Key.ToString());
                var outlineButtons = AccessibleOutline.Build(kv.Value).Where(i => i.Role == AccessibleRole.Button).Select(i => i.ActionId);
                CollectionAssert.AreEqual(kv.Value.Actions.Select(a => a.Id), outlineButtons, kv.Key + ": screen-reader order");
            }
        }

        [Test] public void Every_control_has_a_unique_human_label()
        {
            foreach (var kv in EveryScreen())
            {
                var m = kv.Value;
                Assert.IsFalse(string.IsNullOrWhiteSpace(m.Title), kv.Key + ": no title");
                var labels = m.Actions.Select(a => a.Label).ToList();
                foreach (var l in labels)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(l), kv.Key + ": unlabelled action");
                    Assert.IsFalse(l.Contains(":") || l.Contains("_") || l.Contains("."), kv.Key + " shows an id-like label: " + l);
                }
                Assert.AreEqual(labels.Count, labels.Distinct(StringComparer.OrdinalIgnoreCase).Count(), kv.Key + ": two controls read the same: " + string.Join(" | ", labels));
                var root = new VisualElement();
                ShellView.Render(root, m, _ => { });
                foreach (var b in root.Query<Button>().ToList()) Assert.AreEqual(b.text, b.tooltip, kv.Key.ToString());
            }
        }

        [Test] public void Every_touch_target_meets_the_android_minimum_at_every_text_scale()
        {
            Assert.That(ShellView.ButtonHeight, Is.GreaterThanOrEqualTo(AndroidMinTouchDp));
            foreach (var kv in EveryScreen())
                foreach (var k in new[] { 1f, 1.3f, 2f })
                {
                    var root = new VisualElement();
                    ShellView.Render(root, kv.Value, _ => { }, k);
                    foreach (var b in root.Query<Button>().ToList())
                    {
                        Assert.That(b.style.minHeight.value.value, Is.GreaterThanOrEqualTo(AndroidMinTouchDp), kv.Key + ": " + b.text);
                        Assert.That(b.style.marginBottom.value.value, Is.GreaterThanOrEqualTo(8f), kv.Key + ": targets too close");
                    }
                }
        }

        // ---- contrast ----------------------------------------------------------------------

        public static double Luminance(Color c)
        {
            double Ch(float v) => v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
            return 0.2126 * Ch(c.r) + 0.7152 * Ch(c.g) + 0.0722 * Ch(c.b);
        }

        public static double Contrast(Color a, Color b)
        {
            double x = Luminance(a), y = Luminance(b);
            return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
        }

        [Test] public void Theme_text_colours_meet_the_documented_7_to_1_body_contrast()
        {
            // UX_DESIGN §14 / ShellView: body text >= 7:1 (WCAG AAA) on whatever it sits on.
            Assert.That(Contrast(ShellView.Ink, ShellView.Background), Is.GreaterThanOrEqualTo(7.0), "Ink on paper");
            Assert.That(Contrast(ShellView.Muted, ShellView.Background), Is.GreaterThanOrEqualTo(7.0), "Muted on paper");
            Assert.That(Contrast(ShellView.ErrorInk, ShellView.Background), Is.GreaterThanOrEqualTo(7.0), "Problem text on paper");
            Assert.That(Contrast(ShellView.Ink, Color.white), Is.GreaterThanOrEqualTo(7.0), "secondary button text");
            Assert.That(Contrast(Color.white, ShellView.Accent), Is.GreaterThanOrEqualTo(7.0), "primary button text");
        }

        [Test] public void Non_text_indicators_meet_3_to_1()
        {
            // WCAG 1.4.11: button borders and notice bars must be visible against the page.
            Assert.That(Contrast(ShellView.Accent, ShellView.Background), Is.GreaterThanOrEqualTo(3.0));
            Assert.That(Contrast(ShellView.Accent, Color.white), Is.GreaterThanOrEqualTo(3.0));
            Assert.That(Contrast(ShellView.ErrorInk, ShellView.Background), Is.GreaterThanOrEqualTo(3.0));
        }

        [Test] public void Rendered_screens_only_use_audited_colour_pairs()
        {
            var audited = new[] { ShellView.Ink, ShellView.ErrorInk, Color.white };
            foreach (var kv in EveryScreen())
            {
                var root = new VisualElement();
                ShellView.Render(root, kv.Value, _ => { });
                foreach (var l in root.Query<Label>().ToList())
                {
                    if (l is TextElement && l.parent is Button) continue;
                    var col = l.style.color.value;
                    Assert.IsTrue(audited.Contains(col), kv.Key + ": unaudited text colour on " + l.text);
                }
                foreach (var b in root.Query<Button>().ToList())
                    Assert.That(Contrast(b.style.color.value, b.style.backgroundColor.value), Is.GreaterThanOrEqualTo(7.0), kv.Key + ": " + b.text);
            }
        }

        // ---- reduced motion -----------------------------------------------------------------

        [Test] public void No_screen_animates_so_reduced_motion_holds_with_the_setting_on_or_off()
        {
            foreach (var reduced in new[] { false, true })
            {
                var c = Shell(); c.Boot(); c.Invoke("start");
                for (int i = 0; i < 12 && c.Current == ShellScreen.Guide; i++)
                {
                    var m = c.Render();
                    var q = m.Actions.FirstOrDefault(a => a.Id.StartsWith("queue:"));
                    c.Invoke(m.Actions.Any(a => a.Id == "guide:next") ? "guide:next" : q?.Id ?? m.Actions[0].Id);
                }
                c.Invoke("nav:Settings");
                if (reduced) c.Invoke("toggle:reducedMotion");
                Assert.IsTrue(c.Render().Lines.Contains(ShellController.ReducedMotionLine(reduced)));
                foreach (var s in new[] { "Home", "ReturnSummary", "Projects", "Region", "Story", "Discoveries", "Progress", "Settings", "ActivityStatus" })
                {
                    c.Invoke("nav:" + s);
                    var root = new VisualElement();
                    ShellView.Render(root, c.Render(), _ => { });
                    foreach (var e in root.Query<VisualElement>().ToList())
                    {
                        var d = e.style.transitionDuration;
                        Assert.IsTrue(d.keyword != StyleKeyword.Undefined || d.value == null || d.value.Count == 0, s + ": element " + e.name + " has a transition");
                    }
                }
            }
        }

        // ---- D-147: Region 1 accessibility invariants across EVERY ShellScreen -------------------

        private static readonly float[] Scales = { 1f, 1.3f, 2f };

        [Test] public void D147_every_screen_survives_2x_text()
        {
            foreach (var kv in EveryScreen())
            {
                var root = new VisualElement();
                ShellView.Render(root, kv.Value, _ => { }, 2f);
                var title = root.Q<Label>("title");
                Assert.IsNotNull(title, kv.Key + ": no title label");
                Assert.That(title.style.fontSize.value.value, Is.GreaterThan(ShellView.BodySize * 2f), kv.Key + ": title must stay larger than 2x body");
                foreach (var l in root.Query<Label>().ToList())
                {
                    if (l.parent is Button) continue;
                    Assert.That(l.style.fontSize.value.value, Is.GreaterThanOrEqualTo(ShellView.BodySize * 2f - 0.01f), kv.Key + ": text did not scale: " + l.text);
                    Assert.AreEqual(WhiteSpace.Normal, l.style.whiteSpace.value, kv.Key + ": text must wrap, never clip: " + l.text);
                    Assert.IsFalse(l.style.height.keyword == StyleKeyword.Undefined && l.style.height.value.value > 0, kv.Key + ": fixed-height text clips at 2x: " + l.text);
                }
                foreach (var b in root.Query<Button>().ToList())
                {
                    Assert.That(b.style.fontSize.value.value, Is.GreaterThanOrEqualTo(ShellView.BodySize * 2f - 0.01f), kv.Key + ": " + b.text);
                    Assert.AreEqual(WhiteSpace.Normal, b.style.whiteSpace.value, kv.Key + ": button text must wrap: " + b.text);
                    Assert.IsFalse(b.style.height.keyword == StyleKeyword.Undefined && b.style.height.value.value > 0, kv.Key + ": fixed button height clips at 2x: " + b.text);
                    Assert.IsFalse(b.style.width.keyword == StyleKeyword.Undefined && b.style.width.value.value > 0, kv.Key + ": fixed button width: " + b.text);
                }
            }
        }

        [Test] public void D147_every_touch_target_is_at_least_48dp_in_both_dimensions_at_every_scale()
        {
            foreach (var kv in EveryScreen())
                foreach (var k in Scales)
                {
                    var root = new VisualElement();
                    ShellView.Render(root, kv.Value, _ => { }, k);
                    foreach (var b in root.Query<Button>().ToList())
                    {
                        Assert.That(b.style.minHeight.value.value, Is.GreaterThanOrEqualTo(AndroidMinTouchDp), kv.Key + " x" + k + ": height " + b.text);
                        Assert.That(b.style.minWidth.value.value, Is.GreaterThanOrEqualTo(AndroidMinTouchDp), kv.Key + " x" + k + ": width " + b.text);
                        Assert.That(b.style.minHeight.value.value, Is.GreaterThanOrEqualTo(ShellView.ButtonHeight * k - 0.01f), kv.Key + " x" + k + ": target does not grow with text " + b.text);
                    }
                }
        }

        [Test] public void D147_every_interactive_element_has_a_semantic_name_and_a_screen_reader_label()
        {
            foreach (var kv in EveryScreen())
            {
                var root = new VisualElement();
                ShellView.Render(root, kv.Value, _ => { });
                var outline = AccessibleOutline.Build(kv.Value);
                var readerButtons = outline.Where(i => i.Role == AccessibleRole.Button).ToList();
                var buttons = root.Query<Button>().ToList();
                Assert.AreEqual(buttons.Count, readerButtons.Count, kv.Key + ": a control the screen reader cannot reach (or vice versa)");
                for (int i = 0; i < buttons.Count; i++)
                {
                    var b = buttons[i];
                    Assert.IsFalse(string.IsNullOrWhiteSpace(b.name), kv.Key + ": unnamed control " + b.text);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(b.text), kv.Key + ": control " + b.name + " has no visible label");
                    Assert.AreEqual(b.text, b.tooltip, kv.Key + ": accessible name != visible label for " + b.name);
                    Assert.AreEqual(b.name, readerButtons[i].ActionId, kv.Key + ": reader stop does not map to the control");
                    Assert.AreEqual(b.text, readerButtons[i].Label, kv.Key + ": reader announces something other than the label");
                }
                Assert.AreEqual(kv.Value.Title, outline[0].Label, kv.Key + ": the reader must start at the title");
                Assert.AreEqual(AccessibleRole.Header, outline[0].Role, kv.Key.ToString());
                // Nothing focusable except the controls: decoration never steals a focus stop.
                foreach (var e in root.Query<VisualElement>().ToList())
                    if (e.focusable && !(e is Button) && !(e is ScrollView) && !(e is Scroller) && e.GetFirstAncestorOfType<Scroller>() == null)
                        Assert.Fail(kv.Key + ": non-control element is focusable: " + e.GetType().Name + " " + e.name);
                var valley = root.Q<ValleyElement>("valley");
                if (kv.Value.Scene != null)
                {
                    Assert.IsNotNull(valley, kv.Key.ToString());
                    Assert.IsFalse(string.IsNullOrWhiteSpace(valley.tooltip), kv.Key + ": picture without a text alternative");
                    Assert.IsTrue(outline.Any(i => i.Role == AccessibleRole.Image && i.Label == kv.Value.Scene.AltText), kv.Key + ": picture skipped by the reader");
                }
            }
        }

        [Test] public void D147_every_rendered_colour_pair_meets_contrast_on_every_screen()
        {
            foreach (var kv in EveryScreen())
            {
                var root = new VisualElement();
                ShellView.Render(root, kv.Value, _ => { });
                Assert.AreEqual(ShellView.Background, root.style.backgroundColor.value, kv.Key + ": page colour drifted from the audited theme");
                foreach (var l in root.Query<Label>().ToList())
                {
                    if (l.parent is Button) continue;
                    Assert.That(Contrast(l.style.color.value, ShellView.Background), Is.GreaterThanOrEqualTo(7.0), kv.Key + ": body text contrast " + l.text);
                    if (l.style.borderLeftWidth.value > 0)
                        Assert.That(Contrast(l.style.borderLeftColor.value, ShellView.Background), Is.GreaterThanOrEqualTo(3.0), kv.Key + ": indicator bar " + l.name);
                }
                foreach (var b in root.Query<Button>().ToList())
                {
                    Assert.That(Contrast(b.style.color.value, b.style.backgroundColor.value), Is.GreaterThanOrEqualTo(7.0), kv.Key + ": button text " + b.text);
                    var edge = b.style.borderLeftColor.value;
                    Assert.That(Math.Max(Contrast(edge, ShellView.Background), Contrast(b.style.backgroundColor.value, ShellView.Background)), Is.GreaterThanOrEqualTo(3.0), kv.Key + ": button boundary invisible " + b.text);
                }
            }
        }

        [Test] public void D147_no_element_on_any_screen_has_a_transition_or_animation()
        {
            foreach (var kv in EveryScreen())
                foreach (var k in Scales)
                {
                    var root = new VisualElement();
                    ShellView.Render(root, kv.Value, _ => { }, k);
                    foreach (var e in root.Query<VisualElement>().ToList())
                    {
                        var d = e.style.transitionDuration;
                        Assert.IsTrue(d.keyword != StyleKeyword.Undefined || d.value == null || d.value.Count == 0, kv.Key + ": " + e.name + " has a transition duration");
                        var p = e.style.transitionProperty;
                        Assert.IsTrue(p.keyword != StyleKeyword.Undefined || p.value == null || p.value.Count == 0, kv.Key + ": " + e.name + " has a transition property");
                    }
                }
        }
    }
}
