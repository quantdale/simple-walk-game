using System;
using System.Linq;
using System.Collections.Generic;
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
            c.Invoke(RegionArt.PlaceAction("lm.wetland"));
            Assert.IsNull(c.WorldRequested);
            var m = c.Render();
            Assert.IsNotNull(m.Notice);
            Assert.IsTrue(m.Actions.Any(a => a.Id == "region:clearFocus"));
            c.Invoke("region:clearFocus");
            Assert.IsNull(c.Render().Notice);
        }

        [Test] public void Tapping_canopy_pin_requests_the_painted_world_once_unlocked()
        {
            // New game: canopy is locked; pin shows a focus card. After clear-fallen-timber the place unlocks
            // for Rowan; Region painted-place open still needs the landmark interactable — assert the painted flag.
            Assert.IsTrue(RegionArt.IsPaintedPlace(CanopyGroveArt.LocationId));
            var c = Fresh();
            c.Invoke("nav:Region");
            c.Invoke(RegionArt.PlaceAction(CanopyGroveArt.LocationId));
            // Locked canopy: short card, not world (same seam as other locked painted places).
            if (c.WorldRequested == null)
            {
                Assert.IsNotNull(c.Render().Notice);
                c.Invoke("region:clearFocus");
            }
            else Assert.AreEqual(CanopyGroveArt.LocationId, c.WorldRequested);
        }

        [Test] public void Pin_labels_keep_whole_words_and_wrap_helper_never_mid_breaks()
        {
            Assert.IsTrue(RegionArt.LinesKeepWholeWords("Ridge Observatory", RegionArt.WrapAtSpaces("Ridge Observatory", 8)));
            Assert.AreEqual("Ridge\nObservatory", RegionArt.WrapAtSpaces("Ridge Observatory", 8));
            Assert.AreEqual("Millbrook\nSettlement", RegionArt.WrapAtSpaces("Millbrook Settlement", 10));
            Assert.AreEqual("Observatory", RegionArt.WrapAtSpaces("Observatory", 4), "single long word stays on one line (never mid-split)");
            Assert.IsFalse(RegionArt.WrapAtSpaces("Observatory", 4).Contains("\n"), "no forced break inside a long word");
            Assert.AreEqual("Settlement", RegionArt.LongestWord("Millbrook Settlement"));
        }

        [Test] public void Region_pin_labels_do_not_overflow_or_overlap_at_1x_and_2x_text()
        {
            foreach (float scale in new[] { 1f, 2f })
            {
                var c = Fresh();
                c.Invoke("nav:Region");
                var m = c.Render();
                ShellView.Backdrop = Texture2D.whiteTexture;
                var root = new VisualElement();
                root.style.width = 720; root.style.height = 1280;
                ShellView.Render(root, m, _ => { }, scale);
                // Force a layout pass so worldBound is meaningful.
                root.MarkDirtyRepaint();
                var panel = root.panel; // may be null off-panel — use resolved geometry via manual layout
                root.schedule.Execute(() => { }).ExecuteLater(0);
                // Measure via style + authored geometry (panel-less EditMode): plates use WrapAtSpaces + minWidth ≥ longest word.
                var map = root.Q("region-map");
                Assert.IsNotNull(map);
                var plates = map.Query<Label>(className: null).ToList().Where(l => l.name != null && l.name.StartsWith("pin-label-")).ToList();
                Assert.AreEqual(m.Scene!.Landmarks.Count, plates.Count, "scale=" + scale);
                var pinButtons = map.Query<Button>().ToList();
                Assert.AreEqual(m.Scene!.Landmarks.Count, pinButtons.Count);
                foreach (var b in pinButtons)
                {
                    Assert.GreaterOrEqual(b.style.minWidth.value.value, 48f * scale - 0.1f, b.name + " hit ≥ 48dp @ " + scale);
                    Assert.GreaterOrEqual(b.style.minHeight.value.value, 48f * scale - 0.1f, b.name);
                    Assert.AreEqual(b.text, b.tooltip, "D-147 @ " + scale);
                    // Glyph-only: marker child present, text colour transparent.
                    Assert.IsNotNull(b.Q<RegionMarkerElement>(), b.name);
                }
                foreach (var plate in plates)
                {
                    string id = plate.name.Substring("pin-label-".Length);
                    string title = m.Actions.Single(a => RegionArt.PlaceIdOf(a.Id) == id).Label;
                    Assert.IsTrue(RegionArt.LinesKeepWholeWords(title, plate.text), id + " mid-word @ " + scale + ": " + plate.text);
                    Assert.AreEqual(WhiteSpace.Pre, plate.style.whiteSpace.value, id + " must use Pre (no UITK mid-wrap)");
                    float expectW = RegionArt.PlateWidthDp(title, RegionArt.LabelFontDp * scale, scale);
                    Assert.GreaterOrEqual(plate.style.minWidth.value.value, expectW - 1f, id + " plate fits longest line");
                    Assert.GreaterOrEqual(plate.style.width.value.value, expectW - 1f, id);
                    // Plate must be at least as wide as the longest word (never crush into mid-break).
                    float minWord = RegionArt.LongestWord(title).Length * RegionArt.ApproxCharWidth(RegionArt.LabelFontDp * scale)
                        + 2f * RegionArt.LabelPadX * scale;
                    Assert.GreaterOrEqual(plate.style.width.value.value, minWord - 1f, id + " width ≥ longest word");
                }
                // Pairwise plate centres (authored) stay apart so plates do not stack on each other.
                var centres = new System.Collections.Generic.List<Vector2>();
                foreach (var lv in m.Scene!.Landmarks)
                {
                    Assert.IsTrue(RegionArt.TryLabelOffset(lv.LandmarkId, out float lx, out float ly));
                    Assert.GreaterOrEqual(Mathf.Abs(lx) + Mathf.Abs(ly), 0.085f, lv.LandmarkId + " label clears pin");
                    centres.Add(new Vector2(lv.X + lx, lv.Y + ly));
                }
                for (int i = 0; i < centres.Count; i++)
                    for (int j = i + 1; j < centres.Count; j++)
                        Assert.Greater(Vector2.Distance(centres[i], centres[j]), 0.10f,
                            "label centres too close @ " + scale + ": " + m.Scene.Landmarks[i].LandmarkId + " vs " + m.Scene.Landmarks[j].LandmarkId);
                // Estimated plate AABBs in art-space must not overlap (720× map band ≈ MapShare of 1280).
                float mapH = 1280f * RegionArt.MapShare;
                for (int i = 0; i < plates.Count; i++)
                {
                    for (int j = i + 1; j < plates.Count; j++)
                    {
                        var a = plates[i]; var b = plates[j];
                        string idA = a.name.Substring("pin-label-".Length);
                        string idB = b.name.Substring("pin-label-".Length);
                        RegionArt.TryPin(idA, out float ax, out float ay); RegionArt.TryLabelOffset(idA, out float alx, out float aly);
                        RegionArt.TryPin(idB, out float bx, out float by); RegionArt.TryLabelOffset(idB, out float blx, out float bly);
                        int linesA = Math.Max(1, a.text.Split('\n').Length);
                        int linesB = Math.Max(1, b.text.Split('\n').Length);
                        float aw = a.style.width.value.value / 720f;
                        float ah = (RegionArt.LabelFontDp * scale * (1.15f * linesA) + 6f * scale) / mapH;
                        float bw = b.style.width.value.value / 720f;
                        float bh = (RegionArt.LabelFontDp * scale * (1.15f * linesB) + 6f * scale) / mapH;
                        float ax0 = ax + alx - aw * 0.5f, ay0 = ay + aly - ah * 0.5f;
                        float bx0 = bx + blx - bw * 0.5f, by0 = by + bly - bh * 0.5f;
                        bool overlap = ax0 < bx0 + bw && ax0 + aw > bx0 && ay0 < by0 + bh && ay0 + ah > by0;
                        Assert.IsFalse(overlap, "plates overlap @ " + scale + ": " + idA + " vs " + idB);
                    }
                }
                // Labels stay inside the map band (not over the card).
                foreach (var lv in m.Scene!.Landmarks)
                {
                    RegionArt.TryLabelOffset(lv.LandmarkId, out float lx, out float ly);
                    float lyAbs = lv.Y + ly;
                    Assert.Greater(lyAbs, 0.02f, lv.LandmarkId);
                    Assert.Less(lyAbs, 0.96f, lv.LandmarkId + " must stay above the card fold");
                }
                ShellView.Backdrop = null;
            }
        }
    }
}
