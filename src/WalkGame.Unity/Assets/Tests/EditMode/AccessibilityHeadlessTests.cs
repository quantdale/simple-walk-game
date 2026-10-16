using System;
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
    /// M8 UX/accessibility, headless half (D-073): large text, screen-reader outline and bridge,
    /// empty states, and 7-/30-day returns. Unity-editor-tested; real TalkBack and OS font
    /// scaling on hardware are UNVERIFIED (KI-002).
    /// </summary>
    public sealed class AccessibilityHeadlessTests
    {
        private static readonly string[] ContentKeyPrefixes = { "proj.", "lm.", "care.", "chain.", "disc.", "prod.", "exp.", "region." };
        private static readonly string[] GuiltWords = { "missed", "lost", "streak", "behind", "failed", "lazy", "only walked", "penalty" };

        private string _dir = "";
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-a11y-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);

        private (ShellController c, GameSession s) Fresh(DateTimeOffset now)
        {
            var s = ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(true), new ManualClock(now));
            var c = new ShellController(s, () => 42UL, devTools: false);
            c.Boot(); c.Invoke("start");
            return (c, s);
        }

        /// <summary>Finish first-run guidance the way a player would (queue the first offered project when asked).</summary>
        private static void FinishGuide(ShellController c)
        {
            for (int i = 0; i < 12 && c.Current == ShellScreen.Guide; i++)
            {
                var m = c.Render();
                var q = m.Actions.FirstOrDefault(a => a.Id.StartsWith("queue:"));
                c.Invoke(m.Actions.Any(a => a.Id == "guide:next") ? "guide:next" : q?.Id ?? m.Actions[0].Id);
            }
            Assert.AreNotEqual(ShellScreen.Guide, c.Current, "guide never finished");
        }

        private ShellController Reopen(DateTimeOffset now)
        {
            var s = ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(true), new ManualClock(now));
            var c = new ShellController(s, () => 42UL, devTools: false);
            c.Boot();
            return c;
        }

        private static readonly string[] Screens = { "Home", "Projects", "Region", "Settings", "ActivityStatus", "ReturnSummary" };

        // ---- large text -------------------------------------------------------------------

        [Test] public void Large_text_scales_title_body_and_touch_targets_and_everything_wraps()
        {
            var (c, _) = Fresh(T0);
            foreach (var screen in Screens)
            {
                c.Invoke("nav:" + screen);
                var root = new VisualElement();
                ShellView.Render(root, c.Render(), _ => { }, 2f);
                Assert.AreEqual(ShellView.TitleSize * 2f, root.Q<Label>("title").style.fontSize.value.value, 0.01f, screen);
                foreach (var l in root.Query<Label>().ToList())
                {
                    Assert.That(l.style.fontSize.value.value, Is.GreaterThanOrEqualTo(ShellView.BodySize * 2f - 0.01f), screen + ": " + l.text);
                    Assert.AreEqual(WhiteSpace.Normal, l.style.whiteSpace.value, "text must wrap, never clip: " + l.text);
                }
                foreach (var b in root.Query<Button>().ToList())
                {
                    Assert.That(b.style.minHeight.value.value, Is.GreaterThanOrEqualTo(ShellView.ButtonHeight * 2f - 0.01f), b.text);
                    Assert.AreEqual(WhiteSpace.Normal, b.style.whiteSpace.value, b.text);
                    Assert.IsFalse(b.style.width.keyword == StyleKeyword.Undefined && b.style.width.value.value > 0, "no fixed button widths");
                }
            }
        }

        [Test] public void Font_scale_is_clamped_so_text_never_shrinks_or_explodes()
        {
            Assert.AreEqual(1f, ShellView.ClampTextScale(0.5f));
            Assert.AreEqual(1f, ShellView.ClampTextScale(float.NaN));
            Assert.AreEqual(1.3f, ShellView.ClampTextScale(1.3f), 1e-6f);
            Assert.AreEqual(2f, ShellView.ClampTextScale(3.5f));
        }

        // ---- screen reader ----------------------------------------------------------------

        [Test] public void Every_screen_has_a_complete_ordered_screen_reader_outline()
        {
            var (c, _) = Fresh(T0);
            foreach (var screen in Screens)
            {
                c.Invoke("nav:" + screen);
                var m = c.Render();
                var o = AccessibleOutline.Build(m);
                Assert.AreEqual(AccessibleRole.Header, o[0].Role, screen);
                Assert.AreEqual(m.Title, o[0].Label);
                Assert.IsTrue(o.All(i => !string.IsNullOrWhiteSpace(i.Label)), screen + ": unlabelled stop");
                foreach (var i in o)
                    Assert.IsFalse(ContentKeyPrefixes.Any(p => i.Label.Contains(p)), screen + " announces a content key: " + i.Label);
                // Buttons come after all reading text, so a swipe-through reads the screen before its choices.
                int firstButton = o.FindIndex(i => i.Role == AccessibleRole.Button);
                Assert.That(firstButton, Is.GreaterThan(0), screen + " has no way out");
                Assert.IsTrue(o.Skip(firstButton).All(i => i.Role == AccessibleRole.Button));
                Assert.AreEqual(m.Actions.Count, o.Count(i => i.Role == AccessibleRole.Button));
                if (m.Scene != null)
                    Assert.IsTrue(o.Any(i => i.Role == AccessibleRole.Image && i.Label == m.Scene.AltText), "picture is described, not skipped");
            }
        }

        [Test] public void Errors_are_announced_as_problems_before_anything_else()
        {
            var m = new ScreenModel(ShellScreen.BootError, ScreenState.Error, "Your world could not be opened") { Notice = "The save could not be read." };
            m.Actions.Add(new ShellAction("retry", "Try again"));
            var o = AccessibleOutline.Build(m);
            Assert.AreEqual("Problem: The save could not be read.", o[1].Label);
        }

        [Test] public void Bridge_builds_one_node_per_stop_with_mapped_roles_and_primary_hint()
        {
            var (c, _) = Fresh(T0);
            c.Invoke("nav:Region");
            var o = AccessibleOutline.Build(c.Render());
            var h = ScreenReaderBridge.BuildHierarchy(o, _ => { });
            Assert.AreEqual(o.Count, h.rootNodes.Count);
            for (int i = 0; i < o.Count; i++)
            {
                Assert.AreEqual(o[i].Label, h.rootNodes[i].label);
                Assert.AreEqual(ScreenReaderBridge.RoleOf(o[i].Role), h.rootNodes[i].role);
            }
            c.Invoke("nav:Home");
            var home = AccessibleOutline.Build(c.Render());
            Assert.AreEqual(AccessibleOutline.PrimaryHint, home.First(i => i.Role == AccessibleRole.Button).Hint);
        }

        // ---- empty states -----------------------------------------------------------------

        [Test] public void Empty_states_explain_the_next_step_at_body_size_and_never_dead_end()
        {
            var (c, _) = Fresh(T0);
            if (c.Current != ShellScreen.Home) c.Invoke("nav:Home");
            foreach (var screen in new[] { "Home", "ActivityStatus", "ReturnSummary" })
            {
                c.Invoke("nav:" + screen);
                var m = c.Render();
                Assert.AreEqual(ScreenState.Empty, m.State, screen);
                var root = new VisualElement();
                ShellView.Render(root, m, _ => { });
                var empty = root.Q<Label>("empty-state");
                Assert.IsNotNull(empty, screen);
                Assert.That(empty.text.Length, Is.GreaterThan(25), screen + " empty copy must explain, not just say 'nothing here'");
                Assert.AreNotEqual("Nothing here yet", empty.text);
                Assert.That(empty.style.fontSize.value.value, Is.GreaterThanOrEqualTo(ShellView.BodySize - 0.01f));
                Assert.That(m.Actions.Count, Is.GreaterThan(0), screen + " dead-ends");
            }
        }

        // ---- 7-/30-day returns ------------------------------------------------------------

        private void QueueEverything(GameSession s)
        {
            foreach (var p in s.GetProjects().Projects.Where(p => p.Status == ProjectStatus.Available)) s.EnqueueProject(p.ProjectId);
            if (s.GetPendingReturnSummary() != null) s.AcknowledgeReturnSummary();
        }

        [TestCase(7)]
        [TestCase(30)]
        public void Returning_after_walking_away_lands_on_a_calm_summary_with_one_clear_continue(int days)
        {
            var (c0, s) = Fresh(T0);
            FinishGuide(c0);
            QueueEverything(s);
            var back = T0.AddDays(days);
            var later = ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(true), new ManualClock(back));
            Assert.AreEqual(StartStatus.Loaded, later.Continue().Status);
            later.IngestFromSource(new SyntheticWalkingSource(7000), T0, back);   // walks happened while the app was closed

            var c = Reopen(back.AddMinutes(1));
            Assert.AreEqual(ShellScreen.ReturnSummary, c.Current, days + "-day return should open on what changed");
            var m = c.Render();
            Assert.AreEqual("ack", m.Actions[0].Id);
            Assert.That(m.Lines.Count, Is.GreaterThan(0));
            string all = string.Join(" ", m.Lines).ToLowerInvariant();
            foreach (var w in GuiltWords) StringAssert.DoesNotContain(w, all, days + "-day summary uses guilt language");
            c.Invoke("ack");
            Assert.AreEqual(ShellScreen.Home, c.Current);
        }

        [TestCase(7)]
        [TestCase(30)]
        public void Returning_without_any_walking_keeps_everything_and_never_scolds(int days)
        {
            var (c0, s) = Fresh(T0);
            FinishGuide(c0);
            QueueEverything(s);
            s.IngestFromSource(new SyntheticWalkingSource(7000), T0.AddDays(-3), T0);
            if (s.GetPendingReturnSummary() != null) s.AcknowledgeReturnSummary();
            long vitality = s.GetHome().Vitality;
            int done = s.GetRegion().CompletedProjects;

            var c = Reopen(T0.AddDays(days));
            if (c.Current == ShellScreen.ReturnSummary) c.Invoke("ack");
            Assert.AreEqual(ShellScreen.Home, c.Current);
            var m = c.Render();
            string all = string.Join(" ", m.Lines).ToLowerInvariant();
            foreach (var w in GuiltWords) StringAssert.DoesNotContain(w, all, "idle " + days + "-day return scolds");
            var again = ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(true), new ManualClock(T0.AddDays(days).AddMinutes(2)));
            Assert.AreEqual(StartStatus.Loaded, again.Continue().Status);
            Assert.That(again.GetHome().Vitality, Is.GreaterThanOrEqualTo(vitality), "banked vitality never decays");
            Assert.That(again.GetRegion().CompletedProjects, Is.GreaterThanOrEqualTo(done), "nothing decays while away");
        }
    }
}
