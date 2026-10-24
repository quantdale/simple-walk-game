using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using WalkGame.Application;
using WalkGame.Application.ReadModels;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Time;
using WalkGame.Shell;
using WalkGame.Shell.Presentation;

namespace WalkGame.Shell.Tests
{
    /// <summary>D-204: the painted trailhead's stage-to-asset mapping, fallbacks, reduced motion and texture budget. Unity-editor-tested.</summary>
    public sealed class TrailheadArtTests
    {
        private const string Bridge = TrailheadArt.BridgeId;
        private static PresentationAssetResolver<string> Shipped()
        {
            var d = new DictionaryAssetCatalog<string>();
            foreach (var kv in TrailheadArt.CatalogEntries()) d.Add(kv.Key, kv.Value);
            return new PresentationAssetResolver<string>(d);
        }

        private static LandmarkPresentation Landmark(string stage, string? bridgeSuffix, ProjectStatus status = ProjectStatus.Active, int k = 0)
        {
            var props = bridgeSuffix == null ? new List<WorldViewReadModel.ProjectVisual>()
                : new List<WorldViewReadModel.ProjectVisual> { Visual(status, k) };
            return new LandmarkPresentation(TrailheadArt.LocationId, "Old Trailhead", TrailheadArt.LocationId + "@" + stage, null, true, true, false,
                new[] { TrailheadArt.MiraId }, props);
        }

        private static WorldViewReadModel.ProjectVisual Visual(ProjectStatus status, int k) =>
            new WorldViewReadModel.ProjectVisual(Bridge, TrailheadArt.LocationId, status, k, 3);

        [Test] public void Every_landmark_stage_maps_to_more_of_the_flourishing_plate()
        {
            var r = Shipped();
            float last = -1f;
            foreach (var st in new[] { "ruined", "stabilized", "functional", "restored", "flourishing" })
            {
                var f = TrailheadArt.Map(Landmark(st, null), r);
                Assert.IsFalse(f.StageWasUnknown, st);
                Assert.AreEqual(TrailheadArt.GroundForgotten, f.GroundLow);
                Assert.AreEqual(TrailheadArt.GroundFlourishing, f.GroundHigh);
                Assert.Greater(f.HighShare, last, st);
                last = f.HighShare;
            }
            Assert.AreEqual(0f, TrailheadArt.FlourishingShare(RestorationStage.Ruined));
            Assert.AreEqual(1f, TrailheadArt.FlourishingShare(RestorationStage.Flourishing));
        }

        [Test] public void Bridge_keys_map_to_the_five_build_layers_in_order()
        {
            var r = Shipped();
            var keys = new[] { (ProjectStatus.Available, 0), (ProjectStatus.Active, 0), (ProjectStatus.Active, 1), (ProjectStatus.Active, 2), (ProjectStatus.Completed, 0) };
            for (int i = 0; i < keys.Length; i++)
            {
                var f = TrailheadArt.Map(Landmark("stabilized", "x", keys[i].Item1, keys[i].Item2), r);
                Assert.AreEqual(TrailheadArt.StructureLayers[i], f.Structure, f.BridgeKey);
                Assert.AreEqual(AssetResolution.Exact, f.StructureResolution, f.BridgeKey);
            }
            CollectionAssert.AreEqual(new[] { Bridge + "@ruined", Bridge + "@build-0of3", Bridge + "@build-1of3", Bridge + "@build-2of3", Bridge + "@done" },
                keys.Select(k => Visual(k.Item1, k.Item2).VisualKey).ToArray(), "the read model's keys are the catalog keys");
        }

        [Test] public void Unknown_or_other_build_counts_fall_back_by_progress_then_to_the_subject()
        {
            var r = Shipped();
            Assert.AreEqual(1, TrailheadArt.StructureIndexFor(Bridge + "@build-0of5"));
            Assert.AreEqual(2, TrailheadArt.StructureIndexFor(Bridge + "@build-2of5"));
            Assert.AreEqual(3, TrailheadArt.StructureIndexFor(Bridge + "@build-4of5"));
            Assert.IsNull(TrailheadArt.StructureIndexFor(Bridge + "@build-3of3"));
            Assert.IsNull(TrailheadArt.StructureIndexFor(Bridge + "@build-xofy"));
            Assert.IsNull(TrailheadArt.StructureIndexFor("proj.other@done"));
            Assert.IsNull(TrailheadArt.StructureIndexFor(null));

            var lm = new LandmarkPresentation(TrailheadArt.LocationId, "t", "lm.trailhead@sparkly", null, true, true, false, Array.Empty<string>(),
                new[] { new WorldViewReadModel.ProjectVisual(Bridge, TrailheadArt.LocationId, ProjectStatus.Active, 3, 5) });
            var f = TrailheadArt.Map(lm, r);
            Assert.IsTrue(f.StageWasUnknown);
            Assert.AreEqual(0f, f.HighShare, "an unknown stage draws the forgotten plate, never crashes");
            Assert.AreEqual(TrailheadArt.StructureLayers[3], f.Structure);
            Assert.AreEqual(AssetResolution.SubjectFallback, f.StructureResolution);

            // A future, unmapped key shape resolves to the bridge subject (collapsed bridge), and is listed as missing.
            var odd = TrailheadArt.Map(new LandmarkPresentation(TrailheadArt.LocationId, "t", "lm.trailhead@ruined", null, true, true, false, Array.Empty<string>(),
                new[] { new WorldViewReadModel.ProjectVisual(Bridge, TrailheadArt.LocationId, ProjectStatus.Active, 9, 3) }), r);
            Assert.AreEqual(TrailheadArt.StructureLayers[0], odd.Structure);
            CollectionAssert.Contains(r.MissingKeys.ToArray(), odd.BridgeKey);
        }

        [Test] public void With_an_empty_catalog_nothing_is_drawn_and_nothing_throws()
        {
            var empty = new PresentationAssetResolver<string>(new EmptyAssetCatalog<string>());
            var f = TrailheadArt.Map(Landmark("restored", "x", ProjectStatus.Completed), empty);
            Assert.IsNull(f.GroundLow); Assert.IsNull(f.GroundHigh);
            Assert.AreEqual(TrailheadArt.StructureLayers[4], f.Structure, "progress mapping still names the layer");
            var none = TrailheadArt.Map(Landmark("restored", null), empty);
            Assert.IsNull(none.Structure); Assert.AreEqual(AssetResolution.Missing, none.StructureResolution);
        }

        [Test] public void Mira_has_one_portrait_per_expression_and_bridge_labels_are_specific()
        {
            var r = Shipped();
            foreach (var e in TrailheadArt.MiraExpressions)
            {
                var res = r.Resolve("portrait." + TrailheadArt.MiraId + "@" + e);
                Assert.AreEqual(AssetResolution.Exact, res.Resolution, e);
                Assert.IsNotNull(Resources.Load<Texture2D>(TrailheadArt.ResourceFolder + res.Asset), e);
            }
            Assert.AreEqual(AssetResolution.SubjectFallback, r.Resolve("portrait.npc.mira@curious").Resolution, "unknown expressions fall back to the neutral bust");
            Assert.AreEqual("Inspect the bridge", PlayerStrings.T(TrailheadArt.LabelKeyFor(new DialogueAction(DialogueActionKind.Inspect, TrailheadArt.LocationId, Bridge))));
            Assert.AreEqual("Help restore it", PlayerStrings.T(TrailheadArt.LabelKeyFor(new DialogueAction(DialogueActionKind.HelpRestore, TrailheadArt.LocationId, Bridge))));
            Assert.AreEqual("Inspect", PlayerStrings.T(TrailheadArt.LabelKeyFor(new DialogueAction(DialogueActionKind.Inspect, TrailheadArt.LocationId, "proj.clear-trailhead"))));
            Assert.AreEqual("Maybe later", PlayerStrings.T(TrailheadArt.LabelKeyFor(new DialogueAction(DialogueActionKind.MaybeLater, TrailheadArt.LocationId, null))));
        }

        [Test] public void Ambient_motion_has_a_reduced_motion_path()
        {
            Assert.AreEqual((0f, 0f, 0f, 0f), TrailheadArt.IdlePose(1.7f, true));
            Assert.AreNotEqual(0f, TrailheadArt.IdlePose(1.7f, false).braid);
            float last = 0f;
            foreach (RestorationStage st in Enum.GetValues(typeof(RestorationStage)))
            {
                Assert.AreEqual(0f, TrailheadArt.RippleSpeed(st, true)); Assert.AreEqual(0, TrailheadArt.ParticleCount(st, true));
                Assert.Greater(TrailheadArt.RippleSpeed(st, false), last); last = TrailheadArt.RippleSpeed(st, false);
                Assert.LessOrEqual(TrailheadArt.ParticleCount(st, false), 60);
            }
            var go = new GameObject("th");
            try
            {
                var v = go.AddComponent<TrailheadSceneView>(); v.Build();
                Assert.AreEqual(TrailheadArt.MiraBones.Length, v.MiraBones.Count, "D-212: Mira is the 2D Animation bone rig");
                float Off(Transform t) => Mathf.Abs(Mathf.DeltaAngle(0f, t.localEulerAngles.z));
                var rest = v.MiraBones.ToDictionary(b => b.Key, b => b.Value.localEulerAngles.z);
                v.Show(Landmark("restored", "x", ProjectStatus.Completed));
                v.ReducedMotion = false; v.Tick(1.1f);
                Assert.IsTrue(v.MiraBones.Any(b => Mathf.Abs(Mathf.DeltaAngle(rest[b.Key], b.Value.localEulerAngles.z)) > 0.1f), "idle moves the bones");
                Assert.Greater(v.Motes!.emission.rateOverTime.constant, 0f);
                var ripple = v.Ripple!.transform.localPosition;
                v.ReducedMotion = true; v.Tick(2.3f);
                Assert.IsTrue(v.MiraBones.All(b => Mathf.Abs(Mathf.DeltaAngle(rest[b.Key], b.Value.localEulerAngles.z)) < 1e-3f), "reduced motion: Mira at rest");
                Assert.AreEqual(0f, v.Motes!.emission.rateOverTime.constant);
                Assert.IsFalse(v.Motes.isPlaying);
                var still = v.Ripple.transform.localPosition; v.Tick(5.9f);
                Assert.AreEqual(still, v.Ripple.transform.localPosition, "reduced motion: water does not scroll");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test] public void Mira_is_a_skinned_bone_rig_with_a_help_restore_reaction_and_a_reduced_motion_path()
        {
            var go = new GameObject("th");
            try
            {
                var v = go.AddComponent<TrailheadSceneView>(); v.Build();
                Assert.IsNotNull(v.MiraRigRoot, "rig prefab imported from Rig/mira_rig.psb");
                var skins = v.MiraRigRoot!.GetComponentsInChildren<UnityEngine.U2D.Animation.SpriteSkin>(true);
                Assert.AreEqual(4, skins.Length, "body, braid, scarf tail and head are skinned");
                foreach (var sk in skins) Assert.Greater(sk.boneTransforms.Length, 0, sk.name + " has weighted bones");
                foreach (var sk in skins) Assert.Greater(UnityEngine.U2D.SpriteDataAccessExtensions.GetBindPoses(sk.GetComponent<SpriteRenderer>().sprite).Length, 0, sk.name + " bind poses");
                Assert.AreEqual((0f, 0f, 0f), TrailheadArt.ReactionPose(0.4f, true));
                Assert.AreNotEqual(0f, TrailheadArt.ReactionPose(0.4f, false).head);
                Assert.AreEqual((0f, 0f, 0f), TrailheadArt.ReactionPose(TrailheadArt.ReactionSeconds + 0.01f, false), "the reaction ends");
                v.Show(Landmark("restored", "x", ProjectStatus.Completed));
                var head = v.MiraBones["head"]; var hips = v.MiraBones["hips"];
                v.ReducedMotion = true; v.Tick(3f); var h0 = head.localRotation; var p0 = hips.localPosition;
                v.React(); v.Tick(3.4f);
                Assert.AreEqual(h0, head.localRotation, "reduced motion: no reaction movement");
                Assert.AreEqual(p0, hips.localPosition);
                v.ReducedMotion = false; v.Tick(3f); v.React(); v.Tick(3.3f);
                Assert.AreNotEqual(p0, hips.localPosition, "full motion: the reaction hops");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test] public void Reduced_motion_swaps_instantly_and_stills_Mira()
        {
            Assert.AreEqual(0f, TrailheadArt.TransitionSeconds(true));
            Assert.Greater(TrailheadArt.TransitionSeconds(false), 0f);
            Assert.AreEqual(0f, TrailheadArt.IdleBobAmplitude(true));
            Assert.Greater(TrailheadArt.IdleBobAmplitude(false), 0f);

            var go = new GameObject("th");
            try
            {
                var v = go.AddComponent<TrailheadSceneView>();
                v.Build(); v.ReducedMotion = true;
                v.Show(Landmark("functional", "x", ProjectStatus.Active, 1));
                var high = go.transform.Find("ground_high").GetComponent<SpriteRenderer>();
                Assert.AreEqual(TrailheadArt.FlourishingShare(RestorationStage.Functional), high.color.a, 1e-4f, "reduced motion: the new stage shows at once");
                v.ReducedMotion = false;
                v.Show(Landmark("flourishing", "x", ProjectStatus.Completed));
                Assert.AreEqual(TrailheadArt.FlourishingShare(RestorationStage.Functional), high.color.a, 1e-4f, "full motion: crossfades from where it was");
                v.Settle();
                Assert.AreEqual(1f, high.color.a, 1e-4f);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test] public void Every_shipped_layer_exists_and_the_slice_fits_its_texture_budget()
        {
            long bytes = 0;
            foreach (var name in TrailheadArt.CatalogEntries().Values.Concat(TrailheadArt.MiraParts.Select(p => p.name)).Append(TrailheadArt.WaterRipple).Append(TrailheadArt.MillstoneSprite).Distinct())
            {
                var tex = Resources.Load<Texture2D>(TrailheadArt.ResourceFolder + name);
                Assert.IsNotNull(tex, name);
                Assert.LessOrEqual(Mathf.Max(tex.width, tex.height), 2048, name);
                Assert.AreEqual(1, tex.mipmapCount, name + ": world sprites have no mipmaps");
                bytes += Profiler.GetRuntimeMemorySizeLong(tex);
            }
            // D-212: the rig's mosaic atlas (PSD Importer) is part of the slice
            var rigTex = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/Art/Trailhead/" + TrailheadArt.MiraRig + ".psb").OfType<Texture2D>().ToArray();
            Assert.AreEqual(1, rigTex.Length, "one rig atlas");
            Assert.LessOrEqual(Mathf.Max(rigTex[0].width, rigTex[0].height), 2048); Assert.AreEqual(1, rigTex[0].mipmapCount, "rig atlas has no mipmaps");
            bytes += Profiler.GetRuntimeMemorySizeLong(rigTex[0]);
            Debug.Log("D204_TEXTURE_BYTES " + bytes);
            // Editor software-GL reports DXT; device target remains ASTC ≤ 12 MB (PERFORMANCE_BUDGETS §9). DXT ≈ 2× ASTC.
            Assert.LessOrEqual(bytes, 22L * 1024 * 1024, "PERFORMANCE_BUDGETS §9: editor DXT estimate ≤ 22 MB (device ASTC ≤ 12 MB)");
        }

        [Test] public void Canonical_state_drives_the_trailhead_from_collapsed_to_rebuilt_and_Mira_talks()
        {
            var dir = Path.Combine(Path.GetTempPath(), "wg-th-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var go = new GameObject("th");
            try
            {
                var clock = new ManualClock(new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero));
                var s = ShellBootstrap.CreateSession(dir, new ShellConnectionPort(true), clock);
                Assert.AreEqual(StartStatus.NewGameCreated, s.StartNewGame(42UL, overwriteExisting: false).Status);
                var v = go.AddComponent<TrailheadSceneView>(); v.Build(); v.ReducedMotion = true;
                var c = new WorldPresentationController(s, v, v, NullPresentation.Instance, NullPresentation.Instance, new InMemoryPresentationMemory());
                c.Refresh();
                Assert.AreEqual(TrailheadArt.StructureLayers[0], v.Frame!.Structure);
                Assert.AreEqual(RestorationStage.Ruined, v.Frame.Stage);

                var seen = new List<string> { v.Frame.Structure! };
                Assert.IsTrue(s.EnqueueProject("proj.clear-trailhead").IsSuccess);
                int walk = 0;
                for (int i = 0; i < 150 && v.Frame!.Structure != TrailheadArt.StructureLayers[4]; i++)
                {
                    clock.Advance(TimeSpan.FromHours(2));
                    s.IngestActivityBatch(new[] { new NormalizedActivityRecord("th", "w" + (++walk), ActivityCategory.Walking, ActivityUnits.Steps, 1500L,
                        clock.UtcNow.AddMinutes(-30), clock.UtcNow) });
                    if (s.GetPendingReturnSummary() != null) s.AcknowledgeReturnSummary();
                    if (s.GetProjects().Projects.Single(p => p.ProjectId == Bridge).Status == ProjectStatus.Available)
                        Assert.IsTrue(c.ChooseRestorationGoal(Bridge).IsSuccess);
                    c.Refresh();
                    if (seen.Last() != v.Frame.Structure) seen.Add(v.Frame.Structure!);
                }
                CollectionAssert.AreEqual(TrailheadArt.StructureLayers, seen, "every build layer shows, in order");
                Assert.AreEqual(RestorationStage.Stabilized, v.Frame!.Stage);
                Assert.IsTrue(v.MiraVisible);

                Assert.AreEqual(TapOutcome.Opened, c.TapCharacter(TrailheadArt.MiraId));
                Assert.IsNotNull(v.Dialogue);
                var kinds = v.Dialogue!.Actions.Select(x => x.Kind).ToArray();
                Assert.AreEqual(DialogueActionKind.Inspect, kinds.First());
                Assert.AreEqual(DialogueActionKind.MaybeLater, kinds.Last());
                c.SkipDialogue();
                Assert.IsNull(v.Dialogue, "skippable");
                Assert.IsTrue(v.MissingKeys.All(k => k.StartsWith("portrait.") || k.StartsWith(Bridge)), string.Join(",", v.MissingKeys));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); Directory.Delete(dir, true); }
        }

        [Test] public void Millstone_find_is_offered_only_when_unlocked_and_glows_until_reviewed()
        {
            Assert.IsFalse(TrailheadArt.MillstoneTappable(false));
            Assert.IsTrue(TrailheadArt.MillstoneTappable(true));
            Assert.IsTrue(TrailheadArt.MillstoneGlows(true, reviewed: false));
            Assert.IsFalse(TrailheadArt.MillstoneGlows(true, reviewed: true));
            Assert.IsFalse(TrailheadArt.MillstoneGlows(false, reviewed: false));
            Assert.AreEqual(TrailheadTarget.Millstone, TrailheadArt.HitTest(TrailheadArt.MillstoneX, TrailheadArt.MillstoneY, true, true));
            Assert.AreNotEqual(TrailheadTarget.Millstone, TrailheadArt.HitTest(TrailheadArt.MillstoneX, TrailheadArt.MillstoneY, true, false), "no find: other props answer");
            var box = TrailheadArt.TapRegions.Single(r => r.target == TrailheadTarget.Millstone);
            Assert.GreaterOrEqual((box.x1 - box.x0) * 720f, 48f, "≥ 48 dp wide");
            Assert.GreaterOrEqual((box.y1 - box.y0) * 1280f, 48f, "≥ 48 dp tall");
            Assert.AreEqual(0.8f, TrailheadArt.GlowAlpha(0f, reducedMotion: true), 0.001f);
        }

        [Test] public void Millstone_sprite_ships_under_the_trailhead_resource_folder()
        {
            var tex = Resources.Load<Texture2D>(TrailheadArt.ResourceFolder + TrailheadArt.MillstoneSprite);
            Assert.IsNotNull(tex, "millstone.png missing from Resources/Art/Trailhead");
            Assert.Greater(tex.width, 64);
            Assert.Greater(tex.height, 64);
        }

    }
}
