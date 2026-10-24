using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using WalkGame.Domain.Time;
using WalkGame.Shell;
using WalkGame.Shell.Presentation;

namespace WalkGame.Shell.Tests
{
    /// <summary>D-221: interactive Region 1 map — structural pins, compact card, a11y labels. Unity-editor-tested.</summary>
    public sealed class RegionMapTests
    {
        private static readonly System.DateTimeOffset T0 = new System.DateTimeOffset(2026, 3, 2, 9, 0, 0, System.TimeSpan.Zero);

        private static ShellController Fresh()
        {
            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wg-rmap-" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(dir);
            var c = new ShellController(ShellBootstrap.CreateSession(dir, new ShellConnectionPort(true), new ManualClock(T0)), () => 1UL, devTools: false);
            c.Boot(); c.Invoke("start");
            if (c.Current != ShellScreen.Home) c.Invoke("nav:Home");
            return c;
        }

        [Test] public void Region_card_is_compact_and_place_actions_carry_full_a11y_labels()
        {
            var c = Fresh();
            c.Invoke("nav:Region");
            var m = c.Render();
            Assert.That(m.Lines.Count, Is.LessThanOrEqualTo(5), "compact summary, not a dashboard list");
            Assert.IsFalse(m.Lines.Any(l => l.Contains("Workshop Salvage") || l.Contains("Nursery Greenhouse")), "producer paragraphs left the Region card");
            var places = m.Actions.Where(a => RegionArt.IsPlaceAction(a.Id)).ToList();
            Assert.That(places.Count, Is.EqualTo(m.Scene!.Landmarks.Count));
            foreach (var a in places)
                Assert.AreEqual(a.Label, a.AccessibleLabel, "D-147: accessible name == visible label");
            var outline = AccessibleOutline.Build(m);
            foreach (var lv in m.Scene!.Landmarks)
                Assert.IsTrue(outline.Any(i => i.Role == AccessibleRole.StaticText && i.Label.StartsWith(lv.Label) && i.Label.Length > lv.Label.Length), lv.LandmarkId);
            foreach (var a in places)
                Assert.IsTrue(outline.Any(i => i.Role == AccessibleRole.Button && i.Label == a.Label && i.ActionId == a.Id && i.Hint != null && i.Hint.StartsWith(a.Label)), a.Id);
        }

        [Test] public void Region_map_pins_match_authored_positions_and_draw_as_buttons_in_action_order()
        {
            var c = Fresh();
            c.Invoke("nav:Region");
            var m = c.Render();
            ShellView.Backdrop = Texture2D.whiteTexture;
            var root = new VisualElement();
            ShellView.Render(root, m, _ => { });
            var map = root.Q("region-map");
            Assert.IsNotNull(map, "map overlay present over the painted Region backdrop");
            var buttons = root.Query<Button>().ToList();
            CollectionAssert.AreEqual(m.Actions.Select(a => a.Id), buttons.Select(b => b.name), "marker+card button order == Actions");
            foreach (var lv in m.Scene!.Landmarks)
            {
                Assert.IsTrue(RegionArt.TryPin(lv.LandmarkId, out float x, out float y));
                Assert.AreEqual(x, lv.X, 0.001f);
                Assert.AreEqual(y, lv.Y, 0.001f);
                Assert.GreaterOrEqual(RegionArt.MarkerShapeCount(lv.Level), 2);
            }
            ShellView.Backdrop = null;
        }

        [Test] public void Tapping_trailhead_pin_requests_the_painted_world()
        {
            var c = Fresh();
            // Clear trailhead so the place is unlocked (new game: trailhead is interactable from the start).
            c.Invoke("nav:Region");
            Assert.IsNull(c.WorldRequested);
            c.Invoke(RegionArt.PlaceAction(TrailheadArt.LocationId));
            Assert.AreEqual(TrailheadArt.LocationId, c.WorldRequested);
        }

        [Test] public void Unpainted_pin_opens_a_short_focus_card_not_the_world()
        {
            var c = Fresh();
            c.Invoke("nav:Region");
            c.Invoke(RegionArt.PlaceAction("lm.canopy"));
            Assert.IsNull(c.WorldRequested);
            var m = c.Render();
            Assert.IsNotNull(m.Notice);
            Assert.IsTrue(m.Actions.Any(a => a.Id == "region:clearFocus"));
            c.Invoke("region:clearFocus");
            Assert.IsNull(c.Render().Notice);
        }
    }
}
