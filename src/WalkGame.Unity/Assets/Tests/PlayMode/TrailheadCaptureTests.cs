using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using WalkGame.Application;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Time;
using WalkGame.Shell.Presentation;

namespace WalkGame.Shell.PlayModeTests
{
    /// <summary>
    /// D-204: editor-rendered captures of the painted Old Trailhead (tools/unity/capture-trailhead.sh), driven
    /// by canonical state through WorldPresentationController: a real session walks, picks goals and the
    /// trailhead view draws what the read models say. Runs only when WG_TRAILHEAD_CAPTURE_DIR is set and a
    /// graphics device exists. Label: Unity editor capture (software GL), NOT a device.
    /// </summary>
    public sealed class TrailheadCaptureTests
    {
        private const int W = 720, H = 1280;

        [UnityTest] public IEnumerator Capture_trailhead_stages_and_dialogue()
        {
            string outDir = Environment.GetEnvironmentVariable("WG_TRAILHEAD_CAPTURE_DIR");
            if (string.IsNullOrEmpty(outDir)) { Assert.Ignore("WG_TRAILHEAD_CAPTURE_DIR not set"); yield break; }
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) { Assert.Ignore("no graphics device"); yield break; }
            Directory.CreateDirectory(outDir);
            var dir = Path.Combine(UnityEngine.Application.temporaryCachePath, "wg-th-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);

            var root = new GameObject("trailhead");
            var camGo = new GameObject("cam");
            var uiGo = new GameObject("ui");
            var worldRt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var uiRt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true; cam.orthographicSize = H / 200f; cam.transform.position = new Vector3(0, 0, -10);
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.96f, 0.92f, 0.87f); cam.targetTexture = worldRt;

                var view = root.AddComponent<TrailheadSceneView>();
                view.Build();
                view.ReducedMotion = true; // captures show settled states

                var doc = uiGo.AddComponent<UIDocument>();
                var panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("ShellTheme");
                panel.targetTexture = uiRt; panel.scaleMode = PanelScaleMode.ConstantPixelSize; panel.scale = 1f;
                panel.clearColor = true; panel.colorClearValue = new Color(0, 0, 0, 0);
                doc.panelSettings = panel;
                yield return null;
                var caption = new Label { name = "capture-caption" };
                caption.style.position = Position.Absolute; caption.style.top = 8; caption.style.left = 8; caption.style.right = 8;
                caption.style.backgroundColor = new Color(0.24f, 0.25f, 0.22f, 0.8f); caption.style.color = Color.white; caption.style.fontSize = 15;
                caption.style.whiteSpace = WhiteSpace.Normal; caption.style.paddingLeft = 6; caption.style.paddingRight = 6;
                var dialogueHost = new VisualElement(); dialogueHost.style.position = Position.Absolute;
                dialogueHost.style.left = 0; dialogueHost.style.right = 0; dialogueHost.style.top = 0; dialogueHost.style.bottom = 0;
                doc.rootVisualElement.Add(dialogueHost); doc.rootVisualElement.Add(caption);

                var clock = new ManualClock(new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero));
                var s = ShellBootstrap.CreateSession(dir, new ShellConnectionPort(true), clock);
                Assert.AreEqual(StartStatus.NewGameCreated, s.StartNewGame(42UL, overwriteExisting: false).Status);
                var c = new WorldPresentationController(s, view, view, NullPresentation.Instance, NullPresentation.Instance, new InMemoryPresentationMemory());
                view.BindDialogue(dialogueHost, a => c.ChooseDialogueAction(a), () => c.SkipDialogue());
                c.Refresh();

                var shots = new List<string>();
                int walk = 0;
                IEnumerator Shot(string name, string note)
                {
                    var f = view.Frame!;
                    caption.text = "Unity editor capture (software GL, not a device) · Phase D trailhead · " + f.Stage.ToString().ToLowerInvariant()
                        + " · " + (f.BridgeKey ?? "no bridge key") + (note.Length > 0 ? " · " + note : "");
                    view.Settle();
                    for (int i = 0; i < 6; i++) yield return null;
                    cam.Render();
                    Dump(worldRt, Path.Combine(outDir, name + ".world"));
                    Dump(uiRt, Path.Combine(outDir, name + ".ui"));
                    shots.Add(name);
                    Debug.Log("WG_TRAILHEAD_CAPTURE " + name + " " + caption.text);
                }
                void WalkSteps(long steps, double hours)
                {
                    clock.Advance(TimeSpan.FromHours(hours));
                    s.IngestActivityBatch(new[] { new NormalizedActivityRecord("capture", "w" + (++walk), ActivityCategory.Walking, ActivityUnits.Steps, steps,
                        clock.UtcNow.AddMinutes(-30), clock.UtcNow) });
                    if (s.GetPendingReturnSummary() != null) s.AcknowledgeReturnSummary();
                    c.Refresh();
                }
                ProjectStatus St(string id) => s.GetProjects().Projects.Single(p => p.ProjectId == id).Status;

                yield return Shot("trailhead-s0-forgotten-bridge-ruined", "canonical: new game");
                Assert.IsTrue(s.EnqueueProject("proj.clear-trailhead").IsSuccess);
                for (int i = 0; i < 80 && St("proj.clear-trailhead") != ProjectStatus.Completed; i++) WalkSteps(1500, 2);
                c.Refresh();
                yield return Shot("trailhead-s1-awakening-bridge-ruined", "canonical: trailhead cleared");

                Assert.AreEqual(TapOutcome.Opened, c.TapCharacter(TrailheadArt.MiraId));
                yield return Shot("trailhead-dialogue-mira", "canonical: tap Mira (skippable)");
                view.TextScale = 2f; view.RedrawDialogue();
                yield return Shot("trailhead-dialogue-mira-text2x", "canonical: same dialogue at 2x text");
                var rootRect = dialogueHost.worldBound;
                foreach (var b in dialogueHost.Query<Button>().ToList())
                {
                    Debug.Log("WG_TRAILHEAD_BUTTON '" + b.text + "' " + b.worldBound + " in " + rootRect);
                    Assert.LessOrEqual(b.worldBound.xMax, rootRect.xMax + 0.5f, b.text + " fits at 2x text");
                    Assert.GreaterOrEqual(b.worldBound.xMin, rootRect.xMin - 0.5f, b.text);
                    Assert.LessOrEqual(b.worldBound.yMax, rootRect.yMax + 0.5f, b.text);
                }
                view.TextScale = 1f;
                c.SkipDialogue();

                Assert.IsTrue(c.ChooseRestorationGoal(TrailheadArt.BridgeId).IsSuccess);
                string last = view.Frame!.Structure!;
                yield return Shot("trailhead-s1-bridge-build-0of3", "canonical: bridge goal picked");
                for (int i = 0; i < 120 && St(TrailheadArt.BridgeId) != ProjectStatus.Completed; i++)
                {
                    WalkSteps(1500, 2);
                    if (view.Frame!.Structure != last)
                    {
                        last = view.Frame.Structure!;
                        yield return Shot("trailhead-s1-bridge-" + view.Frame.BridgeKey!.Substring(TrailheadArt.BridgeId.Length + 1), "canonical: walking");
                    }
                }

                // Later landmark stages: a low-decision player keeps walking (20,000 steps/day) and queues the first open goal.
                var stagesShot = new HashSet<string>();
                for (int day = 0; day < 400 && !stagesShot.Contains("restored"); day++)
                {
                    if (!s.GetProjects().Projects.Any(p => p.Status == ProjectStatus.Queued || p.Status == ProjectStatus.Active))
                    {
                        // D-208: keep the lookout for after the workshop so the trailhead passes through Functional on screen.
                        bool functionalSeen = view.Frame!.Stage >= WalkGame.Domain.Regions.RestorationStage.Functional;
                        var open = s.GetProjects().Projects.FirstOrDefault(p => p.Status == ProjectStatus.Available && (functionalSeen || p.ProjectId != "proj.open-lookout"))
                            ?? s.GetProjects().Projects.FirstOrDefault(p => p.Status == ProjectStatus.Available);
                        if (open != null) s.EnqueueProject(open.ProjectId);
                    }
                    WalkSteps(20000, 24);
                    string st = view.Frame!.Stage.ToString().ToLowerInvariant();
                    if ((st == "functional" || st == "restored") && stagesShot.Add(st))
                        yield return Shot("trailhead-s" + (int)view.Frame.Stage + "-" + st, "canonical: day " + (day + 1) + " of continued play");
                }

                // Flourishing: no Region 1 content raises the trailhead past Restored, so this frame is key-driven.
                view.Show(new LandmarkPresentation(TrailheadArt.LocationId, "Old Trailhead", TrailheadArt.LocationId + "@flourishing", null, true, true, false,
                    new[] { TrailheadArt.MiraId }, new[] { new WalkGame.Application.ReadModels.WorldViewReadModel.ProjectVisual(TrailheadArt.BridgeId, TrailheadArt.LocationId, ProjectStatus.Completed, 0, 3) }));
                yield return Shot("trailhead-s4-flourishing-keydriven", "KEY-DRIVEN (stage not reachable in current content)");

                File.WriteAllText(Path.Combine(outDir, "captures.txt"), string.Join("\n", shots) + "\nmissing keys: " + string.Join(", ", view.MissingKeys) + "\n");
                Assert.GreaterOrEqual(shots.Count, 6);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(camGo); UnityEngine.Object.DestroyImmediate(uiGo);
                worldRt.Release(); uiRt.Release();
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        /// <summary>D-207: the trailhead opened from Home in the running shell, with taps on Mira, the bridge and the signpost.</summary>
        [UnityTest] public IEnumerator Capture_trailhead_opened_from_home_with_taps()
        {
            string outDir = Environment.GetEnvironmentVariable("WG_TRAILHEAD_CAPTURE_DIR");
            if (string.IsNullOrEmpty(outDir)) { Assert.Ignore("WG_TRAILHEAD_CAPTURE_DIR not set"); yield break; }
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) { Assert.Ignore("no graphics device"); yield break; }
            Directory.CreateDirectory(outDir);
            var dir = Path.Combine(UnityEngine.Application.temporaryCachePath, "wg-thb-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var worldRt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var uiRt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var go = new GameObject("trailhead-world");
            try
            {
                var clock = new ManualClock(new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero));
                var shell = new ShellController(ShellBootstrap.CreateSession(dir, new ShellConnectionPort(true), clock), () => 42UL, devTools: true);
                shell.Boot(); shell.Invoke("start");
                if (shell.Current != ShellScreen.Home) shell.Invoke("nav:Home");
                var s = shell.Session;
                Assert.IsTrue(s.EnqueueProject("proj.clear-trailhead").IsSuccess);
                int walk = 0;
                for (int i = 0; i < 80 && s.GetProjects().Projects.Single(p => p.ProjectId == "proj.clear-trailhead").Status != ProjectStatus.Completed; i++)
                {
                    clock.Advance(TimeSpan.FromHours(2));
                    s.IngestActivityBatch(new[] { new NormalizedActivityRecord("capture", "b" + (++walk), ActivityCategory.Walking, ActivityUnits.Steps, 1500L, clock.UtcNow.AddMinutes(-30), clock.UtcNow) });
                    if (s.GetPendingReturnSummary() != null) s.AcknowledgeReturnSummary();
                }
                shell.Invoke("nav:Home");
                shell.Invoke(ShellController.WorldTrailheadAction);
                Assert.AreEqual(TrailheadArt.LocationId, shell.WorldRequested, "Home opened the trailhead");

                var panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("ShellTheme");
                panel.targetTexture = uiRt; panel.scaleMode = PanelScaleMode.ConstantPixelSize; panel.scale = 1f;
                panel.clearColor = true; panel.colorClearValue = new Color(0, 0, 0, 0);
                var host = go.AddComponent<TrailheadWorldHost>();
                bool closed = false;
                host.Open(s, new InMemoryPresentationMemory(), NullOsMotionPreference.Instance, 1f, () => { closed = true; shell.CloseWorld(); }, panel, worldRt);
                host.View.ReducedMotion = true;
                var caption = new Label(); caption.style.position = Position.Absolute; caption.style.top = 8; caption.style.left = 8; caption.style.right = 110;
                caption.style.backgroundColor = new Color(0.24f, 0.25f, 0.22f, 0.8f); caption.style.color = Color.white; caption.style.fontSize = 15; caption.style.whiteSpace = WhiteSpace.Normal;
                caption.pickingMode = PickingMode.Ignore;
                host.Overlay!.Add(caption);
                var cam = go.GetComponentInChildren<Camera>();
                IEnumerator Shot(string name, string note)
                {
                    caption.text = "Unity editor capture (software GL, not a device) · Phase D trailhead from Home · " + note;
                    host.View.Settle();
                    for (int i = 0; i < 6; i++) yield return null;
                    cam.Render();
                    Dump(worldRt, Path.Combine(outDir, name + ".world")); Dump(uiRt, Path.Combine(outDir, name + ".ui"));
                    Debug.Log("WG_TRAILHEAD_CAPTURE " + name + " " + caption.text);
                }
                float aspect = (float)W / H;
                yield return Shot("home-trailhead-open", "Home → Visit the Old Trailhead (canonical: trailhead cleared)");
                Assert.AreEqual(TrailheadTarget.Mira, host.TapAt(new Vector2(0.75f, 0.8f), aspect));
                Assert.IsNotNull(host.View.Dialogue);
                yield return Shot("home-tap-mira-dialogue", "tap Mira");
                var help = host.View.Dialogue!.Actions.Single(a => a.Kind == DialogueActionKind.HelpRestore);
                host.Controller.ChooseDialogueAction(help);
                for (int i = 0; i < 3; i++) yield return null;
                Assert.AreEqual("choices", host.ShownCard);
                yield return Shot("home-help-restore-goal-card", "Help restore it → goal card (Start = existing EnqueueProject)");
                host.Controller.CloseLocation();
                Assert.AreEqual(TrailheadTarget.Bridge, host.TapAt(new Vector2(0.5f, 0.5f), aspect));
                Assert.AreEqual("inspect:" + TrailheadArt.BridgeId, host.ShownCard);
                yield return Shot("home-tap-bridge-inspect", "tap the bridge → inspect card");
                host.Controller.CloseLocation();
                Assert.AreEqual(TrailheadTarget.Signpost, host.TapAt(new Vector2(0.08f, 0.45f), aspect));
                Assert.AreEqual(TrailheadArt.LocationId, host.Controller.OpenLocationId);
                host.Close();
                yield return null;
                Assert.IsTrue(closed); Assert.IsNull(shell.WorldRequested); Assert.AreEqual(ShellScreen.Home, shell.Current, "Home button returns to the text shell");
            }
            finally
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
                worldRt.Release(); uiRt.Release();
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        private static void Dump(RenderTexture rt, string path)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path + ".rgba", tex.GetRawTextureData());
            File.WriteAllText(path + ".size", rt.width + " " + rt.height);
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
