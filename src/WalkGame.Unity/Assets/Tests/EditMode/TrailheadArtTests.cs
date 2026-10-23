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
            foreach (var name in TrailheadArt.CatalogEntries().Values.Distinct())
            {
                var tex = Resources.Load<Texture2D>(TrailheadArt.ResourceFolder + name);
                Assert.IsNotNull(tex, name);
                Assert.LessOrEqual(Mathf.Max(tex.width, tex.height), 2048, name);
                Assert.AreEqual(1, tex.mipmapCount, name + ": world sprites have no mipmaps");
                bytes += Profiler.GetRuntimeMemorySizeLong(tex);
            }
            Debug.Log("D204_TEXTURE_BYTES " + bytes);
            Assert.LessOrEqual(bytes, 12L * 1024 * 1024, "PERFORMANCE_BUDGETS §9: ≤ 12 MB for the trailhead slice (editor estimate)");
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
    }
}
