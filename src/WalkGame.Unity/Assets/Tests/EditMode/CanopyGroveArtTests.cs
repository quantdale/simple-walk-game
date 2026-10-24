using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using WalkGame.Application.ReadModels;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Shell.Presentation;

namespace WalkGame.Shell.Tests
{
    /// <summary>D-222: Canopy Grove mapping, orchids find, reduced motion, texture budget. Unity-editor-tested.</summary>
    public sealed class CanopyGroveArtTests
    {
        private static PresentationAssetResolver<string> Shipped()
        {
            var d = new DictionaryAssetCatalog<string>();
            foreach (var kv in CanopyGroveArt.CatalogEntries()) d.Add(kv.Key, kv.Value);
            return new PresentationAssetResolver<string>(d);
        }
        private static LandmarkPresentation Lm(string stage, ProjectStatus status, int k = 0, int n = 5, ProjectStatus? clearFallen = null)
        {
            var props = new System.Collections.Generic.List<WorldViewReadModel.ProjectVisual>
            {
                new WorldViewReadModel.ProjectVisual(CanopyGroveArt.WalkwayId, CanopyGroveArt.LocationId, status, k, n)
            };
            if (clearFallen != null)
                props.Add(new WorldViewReadModel.ProjectVisual(CanopyGroveArt.ClearTimberId, CanopyGroveArt.LocationId, clearFallen.Value, 0, 1));
            return new LandmarkPresentation(CanopyGroveArt.LocationId, "Canopy Grove", CanopyGroveArt.LocationId + "@" + stage, null, true, true, false,
                new[] { CanopyGroveArt.RowanId }, props.ToArray());
        }

        [Test] public void Timber_and_walkway_follow_stage_and_walkway_key()
        {
            Assert.AreEqual(0, CanopyGroveArt.WalkwayIndexFor(CanopyGroveArt.WalkwayId + "@ruined"));
            Assert.AreEqual(1, CanopyGroveArt.WalkwayIndexFor(CanopyGroveArt.WalkwayId + "@build-0of5"));
            Assert.AreEqual(2, CanopyGroveArt.WalkwayIndexFor(CanopyGroveArt.WalkwayId + "@done"));
            Assert.IsNull(CanopyGroveArt.WalkwayIndexFor("proj.river-intake@done"));
            var ruined = CanopyGroveArt.Map(Lm("ruined", ProjectStatus.Available), Shipped());
            Assert.AreEqual(CanopyGroveArt.TimberFallen, ruined.Timber);
            Assert.IsNull(ruined.Walkway);
            Assert.AreEqual(0f, ruined.HighShare);
            var cleared = CanopyGroveArt.Map(Lm("ruined", ProjectStatus.Available, clearFallen: ProjectStatus.Completed), Shipped());
            Assert.IsNull(cleared.Timber, "clear-fallen-timber removes the timber layer even while landmark stays Ruined");
            var mid = CanopyGroveArt.Map(Lm("functional", ProjectStatus.Active, k: 1, n: 5), Shipped());
            Assert.IsNull(mid.Timber);
            Assert.AreEqual(CanopyGroveArt.WalkwayRaised, mid.Walkway);
            Assert.AreEqual(TrailheadArt.FlourishingShare(RestorationStage.Functional), mid.HighShare, 1e-5f);
            var done = CanopyGroveArt.Map(Lm("flourishing", ProjectStatus.Completed, n: 5), Shipped());
            Assert.AreEqual(CanopyGroveArt.WalkwayRaised, done.Walkway);
            Assert.AreEqual(1f, done.HighShare);
        }

        [Test] public void Orchids_find_is_optional_and_only_from_Functional()
        {
            Assert.IsFalse(CanopyGroveArt.OrchidsTappable(false, RestorationStage.Flourishing));
            Assert.IsFalse(CanopyGroveArt.OrchidsTappable(true, RestorationStage.Ruined));
            Assert.IsFalse(CanopyGroveArt.OrchidsTappable(true, RestorationStage.Stabilized));
            Assert.IsTrue(CanopyGroveArt.OrchidsTappable(true, RestorationStage.Functional));
            Assert.IsTrue(CanopyGroveArt.OrchidsGlow(true, RestorationStage.Functional, reviewed: false));
            Assert.IsFalse(CanopyGroveArt.OrchidsGlow(true, RestorationStage.Functional, reviewed: true));
            Assert.AreEqual(CanopyTarget.Orchids, CanopyGroveArt.HitTest(CanopyGroveArt.OrchidsX, CanopyGroveArt.OrchidsY, true, true, false, true));
            Assert.AreEqual(CanopyTarget.None, CanopyGroveArt.HitTest(0.18f, 0.80f, false, false, false, false));
            Assert.AreEqual(CanopyTarget.Rowan, CanopyGroveArt.HitTest(0.18f, 0.80f, true, false, false, false));
            var orch = CanopyGroveArt.TapRegions.Single(r => r.target == CanopyTarget.Orchids);
            Assert.GreaterOrEqual((orch.x1 - orch.x0) * 720f, 48f);
            Assert.GreaterOrEqual((orch.y1 - orch.y0) * 1280f, 48f);
        }

        [Test] public void Reduced_motion_stills_Rowan_and_the_glow()
        {
            Assert.AreEqual((0f, 0f), CanopyGroveArt.IdlePose(1.3f, true));
            Assert.AreEqual(CanopyGroveArt.GlowAlpha(0f, true), CanopyGroveArt.GlowAlpha(1.1f, true));
            Assert.Greater(CanopyGroveArt.GlowAlpha(0f, true), 0.5f);
            Assert.AreNotEqual(CanopyGroveArt.GlowAlpha(0f, false), CanopyGroveArt.GlowAlpha(0.6f, false));
        }

        [Test] public void Every_canopy_layer_exists_and_fits_its_texture_budget()
        {
            long bytes = 0;
            var names = CanopyGroveArt.CatalogEntries().Values.Concat(CanopyGroveArt.RowanParts.Select(p => p.name)).Distinct();
            foreach (var name in names)
            {
                var tex = Resources.Load<Texture2D>(CanopyGroveArt.ResourceFolder + name);
                Assert.IsNotNull(tex, name);
                Assert.AreEqual(1, tex.mipmapCount, name);
                Assert.AreEqual(0, tex.width % 4, name); Assert.AreEqual(0, tex.height % 4, name);
                bytes += Profiler.GetRuntimeMemorySizeLong(tex);
            }
            var rigTex = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/Art/Canopy/" + CanopyGroveArt.RowanRig + ".psb").OfType<Texture2D>().ToArray();
            Assert.AreEqual(1, rigTex.Length, "one Rowan rig atlas");
            bytes += Profiler.GetRuntimeMemorySizeLong(rigTex[0]);
            Debug.Log("D222_TEXTURE_BYTES " + bytes);
            Assert.LessOrEqual(bytes, 22L * 1024 * 1024, "editor DXT estimate ≤ 22 MB (device ASTC ≤ 12 MB)");
        }

        [Test] public void Rowan_idle_uses_cut_out_parts_with_reduced_motion_path()
        {
            var go = new GameObject("rowan-rig-test");
            try
            {
                var v = go.AddComponent<CanopyGroveSceneView>(); v.Build();
                Assert.GreaterOrEqual(v.RowanPartsList.Count, 2, "cut-out parts (body+head)");
                Assert.IsTrue(UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/Art/Canopy/" + CanopyGroveArt.RowanRig + ".psb").OfType<Texture2D>().Any(), "PSB atlas shipped for later bone weights");
                var head = v.RowanPartsList.Single(p => p.name.EndsWith("head")).t;
                var r0 = head.localEulerAngles.z;
                v.ReducedMotion = false; v.Tick(1.1f);
                Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(r0, head.localEulerAngles.z)), 0.1f, "idle moves the head");
                v.ReducedMotion = true; v.Tick(2.3f);
                Assert.Less(Mathf.Abs(Mathf.DeltaAngle(0f, head.localEulerAngles.z)), 1e-3f, "reduced motion: Rowan at rest");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test] public void The_view_draws_what_the_presentation_says()
        {
            var go = new GameObject("canopy");
            try
            {
                var v = go.AddComponent<CanopyGroveSceneView>(); v.Build(); v.ReducedMotion = true;
                v.Show(Lm("ruined", ProjectStatus.Available));
                Assert.IsNotNull(v.Frame!.Timber); Assert.IsFalse(v.Glow.enabled);
                v.SetDiscovery(true, false); Assert.IsFalse(v.Glow.enabled, "ruined: no find yet");
                v.Show(Lm("functional", ProjectStatus.Active, k: 0, n: 5));
                Assert.IsNull(v.Frame!.Timber); Assert.IsNotNull(v.Frame!.Walkway);
                Assert.IsTrue(v.OrchidsTappable); Assert.IsTrue(v.Glow.enabled);
                v.SetDiscovery(true, true); Assert.IsFalse(v.Glow.enabled); Assert.IsTrue(v.OrchidsTappable);
                v.Show(Lm("flourishing", ProjectStatus.Completed, n: 5));
                Assert.IsNotNull(v.Frame!.Walkway);
                Assert.IsEmpty(v.MissingKeys);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test] public void Region_map_opens_canopy_as_a_painted_place()
        {
            Assert.IsTrue(RegionArt.IsPaintedPlace(CanopyGroveArt.LocationId));
            Assert.IsTrue(RegionArt.TryPin(CanopyGroveArt.LocationId, out var x, out var y));
            Assert.Greater(x, 0f); Assert.Greater(y, 0f);
        }
    }
}
