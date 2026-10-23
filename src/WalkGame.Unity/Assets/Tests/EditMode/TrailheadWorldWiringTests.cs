using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Time;
using WalkGame.Shell;
using WalkGame.Shell.Presentation;

namespace WalkGame.Shell.Tests
{
    /// <summary>D-207: Home opens the painted trailhead; taps on Mira, the signpost and the bridge open the right thing. Unity-editor-tested.</summary>
    public sealed class TrailheadWorldWiringTests
    {
        private string _dir = string.Empty;
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-thw-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        [Test] public void Home_offers_the_trailhead_and_the_text_shell_stays_current()
        {
            var c = new ShellController(ShellBootstrap.CreateSession(_dir), () => 42UL, devTools: true);
            c.Boot();
            c.Invoke(ShellController.WorldTrailheadAction);
            Assert.IsNull(c.WorldRequested, "no world before a game exists");
            c.Invoke("start");
            if (c.Current != ShellScreen.Home) c.Invoke("nav:Home");
            Assert.IsTrue(c.Render().Actions.Any(a => a.Id == ShellController.WorldTrailheadAction && a.Label == "Visit the Old Trailhead"));
            c.Invoke(ShellController.WorldTrailheadAction);
            Assert.AreEqual(TrailheadArt.LocationId, c.WorldRequested);
            Assert.AreEqual(ShellScreen.Home, c.Current, "the text shell is still there underneath");
            c.CloseWorld();
            Assert.IsNull(c.WorldRequested);
        }

        [Test] public void Tap_regions_map_screen_points_to_props()
        {
            Assert.AreEqual(TrailheadTarget.Mira, TrailheadArt.HitTest(0.75f, 0.8f, true));
            Assert.AreEqual(TrailheadTarget.None, TrailheadArt.HitTest(0.75f, 0.8f, false));
            Assert.AreEqual(TrailheadTarget.Signpost, TrailheadArt.HitTest(0.08f, 0.45f, true));
            Assert.AreEqual(TrailheadTarget.Bridge, TrailheadArt.HitTest(0.5f, 0.5f, true));
            Assert.AreEqual(TrailheadTarget.None, TrailheadArt.HitTest(0.5f, 0.05f, true));
            var mid = TrailheadArt.ScreenToArt(0.5f, 0.5f, 9f / 19.5f);
            Assert.AreEqual(0.5f, mid.x, 1e-5f);
            var edge = TrailheadArt.ScreenToArt(0f, 0.5f, 720f / 1280f);
            Assert.AreEqual(0f, edge.x, 1e-5f);
        }

        [Test] public void Taps_open_Mira_the_place_and_the_bridge_through_the_controller()
        {
            var clock = new ManualClock(new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero));
            var s = ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(true), clock);
            Assert.AreEqual(StartStatus.NewGameCreated, s.StartNewGame(42UL, overwriteExisting: false).Status);
            var go = new UnityEngine.GameObject("v");
            try
            {
                var v = go.AddComponent<TrailheadSceneView>(); v.Build(); v.ReducedMotion = true;
                var c = new WorldPresentationController(s, v, v, NullPresentation.Instance, NullPresentation.Instance, new InMemoryPresentationMemory());
                c.Refresh();
                Assert.IsTrue(TrailheadArt.Route(TrailheadTarget.Signpost, c));
                Assert.AreEqual(TrailheadArt.LocationId, c.OpenLocationId);
                c.CloseLocation();
                Assert.IsTrue(TrailheadArt.Route(TrailheadTarget.Bridge, c), "a locked bridge opens the place instead");
                Assert.IsNull(c.OpenInspect);
                c.CloseLocation();

                Assert.IsTrue(s.EnqueueProject("proj.clear-trailhead").IsSuccess);
                int w = 0;
                for (int i = 0; i < 80 && s.GetProjects().Projects.Single(p => p.ProjectId == "proj.clear-trailhead").Status != ProjectStatus.Completed; i++)
                {
                    clock.Advance(TimeSpan.FromHours(2));
                    s.IngestActivityBatch(new[] { new NormalizedActivityRecord("t", "w" + (++w), ActivityCategory.Walking, ActivityUnits.Steps, 1500L, clock.UtcNow.AddMinutes(-30), clock.UtcNow) });
                    if (s.GetPendingReturnSummary() != null) s.AcknowledgeReturnSummary();
                }
                c.Refresh();
                Assert.IsTrue(TrailheadArt.Route(TrailheadTarget.Bridge, c));
                Assert.AreEqual(TrailheadArt.BridgeId, c.OpenInspect?.ProjectId);
                c.CloseLocation();
                Assert.IsTrue(TrailheadArt.Route(TrailheadTarget.Mira, c));
                Assert.IsNotNull(v.Dialogue);
                var help = v.Dialogue!.Actions.Single(a => a.Kind == DialogueActionKind.HelpRestore);
                Assert.AreEqual(TrailheadArt.BridgeId, help.ProjectId);
                Assert.IsTrue(c.ChooseDialogueAction(help));
                Assert.IsNull(v.Dialogue);
                Assert.IsTrue(c.ChooseRestorationGoal(TrailheadArt.BridgeId).IsSuccess, "the card's start button is the existing command");
                Assert.AreEqual(ProjectStatus.Active, s.GetProjects().Projects.Single(p => p.ProjectId == TrailheadArt.BridgeId).Status is ProjectStatus.Active or ProjectStatus.Queued ? ProjectStatus.Active : ProjectStatus.Locked);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
