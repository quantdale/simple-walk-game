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
    /// <summary>D-216: the painted River Intake's mapping, the optional find's rules, reduced motion and budget. Unity-editor-tested.</summary>
    public sealed class RiverIntakeArtTests
    {
        private static PresentationAssetResolver<string> Shipped()
        {
            var d = new DictionaryAssetCatalog<string>();
            foreach (var kv in RiverIntakeArt.CatalogEntries()) d.Add(kv.Key, kv.Value);
            return new PresentationAssetResolver<string>(d);
        }
        private static LandmarkPresentation Lm(string stage, ProjectStatus status, int k = 0, int n = 1) =>
            new LandmarkPresentation(RiverIntakeArt.LocationId, "River Intake", RiverIntakeArt.LocationId + "@" + stage, null, true, true, false,
                new[] { RiverIntakeArt.TaviId }, new[] { new WorldViewReadModel.ProjectVisual(RiverIntakeArt.GateId, RiverIntakeArt.LocationId, status, k, n) });

        [Test] public void Gate_layer_follows_the_intake_visual_key_through_three_structural_stages()
        {
            Assert.AreEqual(0, RiverIntakeArt.GateIndexFor(RiverIntakeArt.GateId + "@ruined"));
            Assert.AreEqual(1, RiverIntakeArt.GateIndexFor(RiverIntakeArt.GateId + "@build-0of2"));
            Assert.AreEqual(1, RiverIntakeArt.GateIndexFor(RiverIntakeArt.GateId + "@build-1of2"));
            Assert.AreEqual(2, RiverIntakeArt.GateIndexFor(RiverIntakeArt.GateId + "@done"));
            Assert.IsNull(RiverIntakeArt.GateIndexFor("proj.rebuild-trail-bridges@done"));
            Assert.IsNull(RiverIntakeArt.GateIndexFor(RiverIntakeArt.GateId + "@weird"));
            var f = RiverIntakeArt.Map(Lm("ruined", ProjectStatus.Available), Shipped());
            Assert.AreEqual(RiverIntakeArt.GateJammed, f.Gate); Assert.AreEqual(0f, f.HighShare); Assert.IsNull(f.Accents);
            var mid = RiverIntakeArt.Map(Lm("stabilized", ProjectStatus.Active, k: 0, n: 2), Shipped());
            Assert.AreEqual(RiverIntakeArt.GateRebuilding, mid.Gate);
            var done = RiverIntakeArt.Map(Lm("stabilized", ProjectStatus.Completed, n: 2), Shipped());
            Assert.AreEqual(RiverIntakeArt.GateRestored, done.Gate); Assert.AreEqual(RestorationStage.Stabilized, done.Stage);
            Assert.IsNull(done.Accents, "Stabilized: gate restored, accents wait for Restored");
            var restored = RiverIntakeArt.Map(Lm("restored", ProjectStatus.Completed, n: 2), Shipped());
            Assert.AreEqual(RiverIntakeArt.AccentsFlourish, restored.Accents);
            Assert.IsTrue(RiverIntakeArt.ShowAccents(RestorationStage.Restored));
            Assert.IsTrue(RiverIntakeArt.ShowAccents(RestorationStage.Flourishing));
            Assert.IsFalse(RiverIntakeArt.ShowAccents(RestorationStage.Functional));
            Assert.AreEqual(TrailheadArt.FlourishingShare(RestorationStage.Stabilized), done.HighShare, 1e-5f, "same stage ramp as the trailhead");
        }

        [Test] public void The_find_is_optional_canonical_and_only_on_the_restored_gate()
        {
            Assert.IsFalse(RiverIntakeArt.PlateTappable(false, 2), "not before the discovery is unlocked");
            Assert.IsFalse(RiverIntakeArt.PlateTappable(true, 0), "not on the jammed gate");
            Assert.IsFalse(RiverIntakeArt.PlateTappable(true, 1), "not while rebuilding");
            Assert.IsTrue(RiverIntakeArt.PlateTappable(true, 2));
            Assert.IsTrue(RiverIntakeArt.PlateGlows(true, 2, reviewed: false));
            Assert.IsFalse(RiverIntakeArt.PlateGlows(true, 2, reviewed: true), "read: no glow, still tappable");
            Assert.AreEqual(IntakeTarget.Plate, RiverIntakeArt.HitTest(RiverIntakeArt.PlateX, RiverIntakeArt.PlateY, true, true));
            Assert.AreEqual(IntakeTarget.Gate, RiverIntakeArt.HitTest(RiverIntakeArt.PlateX, RiverIntakeArt.PlateY, true, false), "no find: the gate answers");
            Assert.AreEqual(IntakeTarget.None, RiverIntakeArt.HitTest(0.18f, 0.80f, false, false), "absent Tavi is not tappable");
            Assert.AreEqual(IntakeTarget.Tavi, RiverIntakeArt.HitTest(0.18f, 0.80f, true, false));
            var plate = RiverIntakeArt.TapRegions.Single(r => r.target == IntakeTarget.Plate);
            Assert.GreaterOrEqual((plate.x1 - plate.x0) * 720f, 48f, "plate tap box ≥ 48 dp wide at the reference size");
            Assert.GreaterOrEqual((plate.y1 - plate.y0) * 1280f, 48f, "plate tap box ≥ 48 dp tall");
        }

        [Test] public void Reduced_motion_stills_Tavi_and_the_glow()
        {
            Assert.AreEqual((0f, 0f, 0f), RiverIntakeArt.IdlePose(1.3f, true));
            Assert.AreEqual(RiverIntakeArt.GlowAlpha(0f, true), RiverIntakeArt.GlowAlpha(1.1f, true), "static glow");
            Assert.Greater(RiverIntakeArt.GlowAlpha(0f, true), 0.5f, "still clearly visible");
            Assert.AreNotEqual(RiverIntakeArt.GlowAlpha(0f, false), RiverIntakeArt.GlowAlpha(0.6f, false), "pulses with motion on");
        }

        [Test] public void Every_intake_layer_exists_and_fits_its_texture_budget()
        {
            long bytes = 0;
            var names = RiverIntakeArt.CatalogEntries().Values.Concat(RiverIntakeArt.TaviParts.Select(p => p.name)).Distinct();
            foreach (var name in names)
            {
                var tex = Resources.Load<Texture2D>(RiverIntakeArt.ResourceFolder + name);
                Assert.IsNotNull(tex, name);
                Assert.AreEqual(1, tex.mipmapCount, name);
                Assert.AreEqual(0, tex.width % 4, name); Assert.AreEqual(0, tex.height % 4, name);
                bytes += Profiler.GetRuntimeMemorySizeLong(tex);
            }
            var rigTex = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/Art/Intake/" + RiverIntakeArt.TaviRig + ".psb").OfType<Texture2D>().ToArray();
            Assert.AreEqual(1, rigTex.Length, "one Tavi rig atlas");
            bytes += Profiler.GetRuntimeMemorySizeLong(rigTex[0]);
            Debug.Log("D217_TEXTURE_BYTES " + bytes);
            Assert.LessOrEqual(bytes, 12L * 1024 * 1024, "PERFORMANCE_BUDGETS §9: ≤ 12 MB per place (editor estimate)");
        }

        [Test] public void Tavi_bone_rig_authors_the_expected_bones_with_reduced_motion_path()
        {
            var go = new GameObject("tavi-rig-test");
            try
            {
                var v = go.AddComponent<RiverIntakeSceneView>(); v.Build();
                Assert.IsNotNull(v.TaviRigRoot, "bone rig preferred over cut-out");
                Assert.AreEqual(RiverIntakeArt.TaviBones.Length, v.TaviBones.Count);
                CollectionAssert.AreEquivalent(RiverIntakeArt.TaviBones, v.TaviBones.Keys);
                var rest = v.TaviBones.ToDictionary(b => b.Key, b => b.Value.localEulerAngles.z);
                v.ReducedMotion = false; v.Tick(1.1f);
                Assert.IsTrue(v.TaviBones.Any(b => Mathf.Abs(Mathf.DeltaAngle(rest[b.Key], b.Value.localEulerAngles.z)) > 0.1f), "idle moves the bones");
                v.ReducedMotion = true; v.Tick(2.3f);
                Assert.IsTrue(v.TaviBones.All(b => Mathf.Abs(Mathf.DeltaAngle(rest[b.Key], b.Value.localEulerAngles.z)) < 1e-3f), "reduced motion: Tavi at rest");
                var head = v.TaviBones["head"]; var h0 = head.localRotation;
                v.React(); v.Tick(v.GetType().GetField("_clock", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) == null ? 0f : 0f);
                // reaction under reduced motion: React sets the clock marker but pose stays at rest
                v.Tick(0.4f);
                Assert.AreEqual(h0, head.localRotation, "reduced motion: no reaction movement");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test] public void The_view_draws_what_the_presentation_says()
        {
            var go = new GameObject("intake");
            try
            {
                var v = go.AddComponent<RiverIntakeSceneView>(); v.Build(); v.ReducedMotion = true;
                v.Show(Lm("ruined", ProjectStatus.Available));
                Assert.AreEqual(0, v.Frame!.GateIndex); Assert.IsFalse(v.Glow.enabled);
                v.SetDiscovery(true, false); Assert.IsFalse(v.Glow.enabled, "jammed gate: no find yet");
                v.Show(Lm("stabilized", ProjectStatus.Active, k: 0, n: 2)); Assert.AreEqual(1, v.Frame!.GateIndex); Assert.IsFalse(v.Glow.enabled);
                v.Show(Lm("stabilized", ProjectStatus.Completed, n: 2)); Assert.AreEqual(2, v.Frame!.GateIndex); Assert.IsTrue(v.Glow.enabled); Assert.IsTrue(v.PlateTappable);
                v.SetDiscovery(true, true); Assert.IsFalse(v.Glow.enabled); Assert.IsTrue(v.PlateTappable);
                v.Show(Lm("restored", ProjectStatus.Completed, n: 2)); Assert.IsNotNull(v.Frame!.Accents);
                Assert.IsEmpty(v.MissingKeys);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
