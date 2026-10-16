using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine.UIElements;
using WalkGame.Domain.Regions;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>M6 Region restoration visuals: engine-free scene model + view wiring (Unity-editor-tested).</summary>
    public sealed class RegionSceneTests
    {
        private string _dir = "";
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-scene-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private ShellController NewGame()
        {
            var c = new ShellController(ShellBootstrap.CreateSession(_dir), () => 42UL, devTools: true);
            c.Boot(); c.Invoke("start");
            return c;
        }

        [Test] public void Region_screen_carries_a_scene_with_one_visual_per_landmark()
        {
            var c = NewGame(); c.Invoke("nav:Region");
            var m = c.Render();
            Assert.IsNotNull(m.Scene);
            Assert.That(m.Scene!.Landmarks.Count, Is.GreaterThan(0).And.LessThanOrEqualTo(RegionScene.MaxLandmarks));
            Assert.IsTrue(m.Scene.Landmarks.All(l => l.X > 0 && l.X < 1 && l.Y > 0 && l.Y < 1));
            Assert.IsTrue(m.Scene.Landmarks.All(l => !l.Label.Contains(".")), "labels are titles, not content keys");
        }

        [Test] public void Other_screens_carry_no_scene()
        {
            var c = NewGame();
            foreach (var s in new[] { "Home", "Projects", "Settings", "ActivityStatus" })
            {
                c.Invoke("nav:" + s);
                Assert.IsNull(c.Render().Scene, s);
            }
        }

        [Test] public void Scene_is_deterministic_for_the_same_state()
        {
            var c = NewGame(); c.Invoke("nav:Region");
            var a = c.Render().Scene!; var b = c.Render().Scene!;
            Assert.AreEqual(a.AltText, b.AltText);
            Assert.AreEqual(a.Greenness, b.Greenness);
            CollectionAssert.AreEqual(a.Landmarks.Select(l => (l.X, l.Y, l.Stage)).ToList(), b.Landmarks.Select(l => (l.X, l.Y, l.Stage)).ToList());
        }

        [Test] public void Every_stage_has_a_distinct_colour_and_higher_stages_draw_more()
        {
            var stages = (RestorationStage[])Enum.GetValues(typeof(RestorationStage));
            Assert.AreEqual(stages.Length, stages.Select(RegionScene.StageColor).Distinct().Count());
            // State is never colour-only: level drives size and the number of growth shapes too.
            Assert.IsTrue(stages.Select(s => (int)s).SequenceEqual(Enumerable.Range(0, stages.Length)));
        }

        [Test] public void Hills_turn_from_dry_to_green_monotonically()
        {
            uint dry = RegionScene.HillColor(0f), green = RegionScene.HillColor(1f);
            Assert.AreEqual(0xC9B48Au, dry);
            Assert.AreEqual(0x7FA772u, green);
            int lastRed = 0xFF;
            for (float t = 0; t <= 1.0001f; t += 0.1f)
            {
                int red = (int)(RegionScene.HillColor(t) >> 16);
                Assert.That(red, Is.LessThanOrEqualTo(lastRed));
                lastRed = red;
            }
            Assert.AreEqual(dry, RegionScene.HillColor(-3f));
            Assert.AreEqual(green, RegionScene.HillColor(9f));
        }

        [Test] public void Shape_count_stays_within_the_performance_budget()
        {
            var c = NewGame(); c.Invoke("nav:Region");
            var scene = c.Render().Scene!;
            Assert.That(scene.ShapeCount(), Is.LessThanOrEqualTo(RegionScene.MaxShapes));
            Assert.That(RegionScene.MaxShapes, Is.LessThanOrEqualTo(100), "a phone-safe vector budget");
        }

        [Test] public void Picture_has_a_text_alternative_without_identifiers()
        {
            var c = NewGame(); c.Invoke("nav:Region");
            var scene = c.Render().Scene!;
            StringAssert.StartsWith("A picture of the valley", scene.AltText);
            foreach (var n in Enum.GetNames(typeof(RestorationStage))) StringAssert.DoesNotContain(n, scene.AltText);
        }

        [Test] public void View_adds_the_valley_element_with_alt_tooltip()
        {
            var c = NewGame(); c.Invoke("nav:Region");
            var model = c.Render();
            var root = new VisualElement();
            ShellView.Render(root, model, _ => { });
            var valley = root.Q<ValleyElement>("valley");
            Assert.IsNotNull(valley);
            Assert.AreEqual(model.Scene!.AltText, valley.tooltip);
            Assert.AreEqual(ValleyElement.Height, valley.style.height.value.value);

            c.Invoke("nav:Home");
            var home = new VisualElement();
            ShellView.Render(home, c.Render(), _ => { });
            Assert.IsNull(home.Q<ValleyElement>("valley"));
        }

        private static WalkGame.Application.ReadModels.RegionReadModel Model(string? active, int stage, int stages) =>
            new WalkGame.Application.ReadModels.RegionReadModel("Millbrook Valley",
                new[] { new WalkGame.Application.ReadModels.RegionReadModel.LandmarkRow("lm.a", "Old Mill", RestorationStage.Ruined) },
                Array.Empty<WalkGame.Application.ReadModels.RegionReadModel.ProducerRow>(), 3, 20, active,
                activeBuildStage: stage, activeBuildStages: stages, activeProjectTitleKey: "Raise the canopy walkway");

        [Test] public void Staged_active_project_shows_a_build_site_that_rises_per_stage()
        {
            var a = RegionScene.Build(Model("proj.canopy-walkway", 1, 5));
            var b = RegionScene.Build(Model("proj.canopy-walkway", 3, 5));
            Assert.IsNotNull(a.BuildSite);
            Assert.AreEqual(2, b.ShapeCount() - a.ShapeCount(), "one post per stage reached");
            StringAssert.Contains("stage 4 of 5", b.AltText);
            Assert.That(b.ShapeCount(), Is.LessThanOrEqualTo(RegionScene.MaxShapes));
        }

        [Test] public void Unstaged_or_idle_project_shows_no_build_site()
        {
            Assert.IsNull(RegionScene.Build(Model("proj.clear-trailhead", 0, 1)).BuildSite);
            Assert.IsNull(RegionScene.Build(Model(null, 0, 1)).BuildSite);
        }

        [Test] public void Region1_landmarks_each_get_a_distinct_readable_silhouette()
        {
            var c = NewGame(); c.Invoke("nav:Region");
            var kinds = c.Render().Scene!.Landmarks.Select(l => l.Kind).ToList();
            Assert.That(kinds, Has.None.EqualTo(LandmarkKind.Generic), "every authored landmark maps to a silhouette");
            Assert.AreEqual(kinds.Count, kinds.Distinct().Count(), "no two landmarks look alike");
        }

        [Test] public void Unknown_landmark_falls_back_to_a_generic_hut()
        {
            Assert.AreEqual(LandmarkKind.Generic, RegionScene.KindOf("lm.something-new"));
            Assert.AreEqual(LandmarkKind.Generic, RegionScene.KindOf(null!));
        }
    
        [Test] public void Moment_ring_always_stays_inside_the_picture_and_around_its_landmark()
        {
            // KI-020 (D-087): same layout math as ValleyElement for every position, level and width.
            float h = ValleyElement.Height, half = ValleyElement.RingStroke * 0.5f;
            foreach (float w in new[] { 240f, 320f, 360f, 412f, 600f, 1080f })
            for (int n = 1; n <= RegionScene.MaxLandmarks; n++)
            for (int i = 0; i < n; i++)
            for (int level = 0; level <= 4; level++)
            {
                float cx = (i + 0.5f) / n * w, cy = (0.62f + (i % 2 == 0 ? 0.08f : -0.04f)) * h;
                float s = Math.Min(24f + 3f * level, w * 0.06f + 3f * level);
                var r = ValleyElement.RingGeometry(cx, cy, s, w, h);
                string at = "w=" + w + " n=" + n + " i=" + i + " level=" + level;
                Assert.GreaterOrEqual(r.X - r.R - half, 0f, at); Assert.LessOrEqual(r.X + r.R + half, w, at);
                Assert.GreaterOrEqual(r.Y - r.R - half, 0f, at); Assert.LessOrEqual(r.Y + r.R + half, h, at);
                float dx = r.X - cx, dy = r.Y - (cy - s * 0.5f);
                Assert.LessOrEqual(Math.Sqrt(dx * dx + dy * dy), r.R, at + ": ring no longer surrounds the landmark");
                Assert.GreaterOrEqual(r.R, s * 1.2f - 0.001f, at + ": ring shrank inside the landmark");
            }
        }

        [Test] public void Moment_ring_is_unchanged_when_there_is_room()
        {
            var r = ValleyElement.RingGeometry(500f, 130f, 30f, 1000f, ValleyElement.Height);
            Assert.AreEqual(500f, r.X, 1e-4); Assert.AreEqual(115f, r.Y, 1e-4); Assert.AreEqual(57f, r.R, 1e-4);
        }
    }
}
