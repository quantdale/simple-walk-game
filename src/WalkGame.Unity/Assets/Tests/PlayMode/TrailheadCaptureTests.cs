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
                view.SetDiscovery(true, false);
                view.Tick(1.2f);
                yield return Shot("trailhead-millstone-glow", "canonical: Stabilized; optional millstone find glowing");
                Assert.AreEqual(TrailheadTarget.Millstone, TrailheadArt.HitTest(TrailheadArt.MillstoneX, TrailheadArt.MillstoneY, true, true));

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
                // D-211: ambient motion ON (idle, ripple, motes) — one still plus a short frame sequence
                // (tools/unity/capture-trailhead.sh turns anim-* frames into an animated GIF). Simulated time, editor only.
                view.ReducedMotion = false;
                for (int fr = 0; fr < 24; fr++)
                {
                    float t = fr / 6f;
                    if (fr == 12) view.React(); // D-212: the "Help restore it" reaction, mid-sequence
                    view.Tick(t); view.Motes!.Simulate(fr == 0 ? 6f : 1f / 6f, true, fr == 0, false);
                    caption.text = "Unity editor capture (software GL, not a device) · Phase D trailhead · motion ON (idle, ripple, motes) · t=" + t.ToString("0.00") + " s (simulated)";
                    for (int i = 0; i < 2; i++) yield return null;
                    cam.Render();
                    if (fr == 9) { Debug.Log("WG_MOTES live=" + view.Motes.particleCount); Assert.Greater(view.Motes.particleCount, 0, "motes are live with motion on"); }
                    string nm = fr == 9 ? "trailhead-s4-motion-on" : fr == 15 ? "trailhead-mira-reaction-help-restore" : "anim-" + fr.ToString("00");
                    Dump(worldRt, Path.Combine(outDir, nm + ".world")); Dump(uiRt, Path.Combine(outDir, nm + ".ui"));
                    if (fr == 9 || fr == 15) { Dump(worldRt, Path.Combine(outDir, "anim-" + fr.ToString("00") + ".world")); Dump(uiRt, Path.Combine(outDir, "anim-" + fr.ToString("00") + ".ui")); }
                }
                view.ReducedMotion = true;

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
                // D-210: the text-shell screens around the world (bible §47: first launch, return after walking,
                // Home with the trailhead entry scrolled into view, the Region view), rendered by the real ShellView.
                var shellRt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
                var shellPanel = ScriptableObject.CreateInstance<PanelSettings>();
                shellPanel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("ShellTheme");
                shellPanel.targetTexture = shellRt; shellPanel.scaleMode = PanelScaleMode.ConstantPixelSize; shellPanel.scale = 1f;
                shellPanel.clearColor = true; shellPanel.colorClearValue = ShellView.Background;
                var shellGo = new GameObject("shell-doc");
                var doc = shellGo.AddComponent<UIDocument>(); doc.panelSettings = shellPanel;
                IEnumerator ShellShot(string name, string note, string? scrollToAction = null, float textScale = 1f)
                {
                    var root = doc.rootVisualElement;
                    root.style.backgroundColor = ShellView.Background;
                    // D-215: the painted stage plate behind the card, as ShellBootstrap does
                    if (shell.Current == ShellScreen.Region)
                        ShellView.Backdrop = Resources.Load<Texture2D>(RegionArt.ResourceFolder + RegionArt.Backdrop());
                    else
                    {
                        WalkGame.Domain.Regions.RestorationStage? st = null;
                        try { st = shell.Session.GetRegion().Landmarks.FirstOrDefault(l => l.LandmarkId == TrailheadArt.LocationId)?.Stage; } catch (Exception) { }
                        ShellView.Backdrop = Resources.Load<Texture2D>(TrailheadArt.ResourceFolder + TrailheadArt.BackdropFor(st));
                    }
                    ShellView.Render(root, shell.Render(), _ => { }, textScale);
                    var cap = new Label("Unity editor capture (software GL, not a device) · text shell · " + note);
                    cap.style.position = Position.Absolute; cap.style.top = 4; cap.style.left = 8; cap.style.right = 8;
                    cap.style.backgroundColor = new Color(0.24f, 0.25f, 0.22f, 0.8f); cap.style.color = Color.white; cap.style.fontSize = 15; cap.style.whiteSpace = WhiteSpace.Normal;
                    root.Add(cap);
                    for (int i = 0; i < 4; i++) yield return null;
                    if (scrollToAction != null)
                    {
                        var target = root.Query<ShellView.ActionButton>().ToList().FirstOrDefault(b => b.ActionId == scrollToAction);
                        Assert.IsNotNull(target, "no button for " + scrollToAction);
                        var scroll = root.Q<ScrollView>("content");
                        float max = Mathf.Max(0f, scroll.contentContainer.layout.height - scroll.contentViewport.layout.height);
                        float y = target!.worldBound.y - scroll.contentContainer.worldBound.y - scroll.contentViewport.layout.height * 0.4f;
                        scroll.scrollOffset = new Vector2(0f, Mathf.Clamp(y, 0f, max));
                        for (int i = 0; i < 4; i++) yield return null;
                        var vp = scroll.contentViewport.worldBound;
                        Assert.IsTrue(vp.Overlaps(target.worldBound), "the Visit the Old Trailhead button is on screen after scrolling");
                        Debug.Log("WG_CAPTURE_SCROLL_TO offset=" + scroll.scrollOffset.y + " max=" + max);
                    }
                    Dump(shellRt, Path.Combine(outDir, name + ".world"));
                    File.WriteAllBytes(Path.Combine(outDir, name + ".ui.rgba"), new byte[W * H * 4]);
                    File.WriteAllText(Path.Combine(outDir, name + ".ui.size"), W + " " + H);
                    Debug.Log("WG_TRAILHEAD_CAPTURE " + name + " " + cap.text);
                }
                shell.Boot();
                yield return ShellShot("shell-first-launch", "first launch / onboarding");
                shell.Invoke("start");
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
                clock.Advance(TimeSpan.FromHours(20));
                s.IngestActivityBatch(new[] { new NormalizedActivityRecord("capture", "b" + (++walk), ActivityCategory.Walking, ActivityUnits.Steps, 4200L, clock.UtcNow.AddMinutes(-90), clock.UtcNow) });
                shell.Invoke("nav:Home");
                if (s.GetPendingReturnSummary() != null)
                {
                    shell.Invoke("nav:ReturnSummary");
                    yield return ShellShot("shell-return-after-walking", "return after a walk (While you were away)");
                    shell.Invoke("ack");
                }
                shell.Invoke("nav:Home");
                yield return ShellShot("shell-home-visit-trailhead", "Home (lighter menu) with Visit the Old Trailhead", ShellController.WorldTrailheadAction);
                yield return ShellShot("shell-home-visit-trailhead-text2x", "Home at 2x text, scrolled to Visit the Old Trailhead", ShellController.WorldTrailheadAction, 2f);
                shell.Invoke("nav:Region");
                yield return ShellShot("shell-region-view", "Region view (direction-A card over the painted Region 1 map)");
                shell.Invoke("nav:Home");
                ShellView.Backdrop = null;
                Assert.IsTrue(TrailheadArt.OpenWorldFirst(shell.Current == ShellScreen.Home, false, false), "D-215: boot lands in the world");
                UnityEngine.Object.DestroyImmediate(shellGo); shellRt.Release();
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
                    caption.text = "Unity editor capture (software GL, not a device) · Phase D trailhead (world-first boot / from Home) · " + note;
                    host.View.Settle();
                    for (int i = 0; i < 6; i++) yield return null;
                    cam.Render();
                    Dump(worldRt, Path.Combine(outDir, name + ".world")); Dump(uiRt, Path.Combine(outDir, name + ".ui"));
                    Debug.Log("WG_TRAILHEAD_CAPTURE " + name + " " + caption.text);
                }
                float aspect = (float)W / H;
                yield return Shot("home-trailhead-open", "Home → Visit the Old Trailhead (canonical: trailhead cleared)");
                Assert.IsTrue(host.View.MillstoneTappable, "clear-trailhead unlocks disc.old-millstone");
                Assert.AreEqual(TrailheadTarget.Millstone, host.TapAt(new Vector2(TrailheadArt.MillstoneX, TrailheadArt.MillstoneY), aspect));
                Assert.IsTrue(host.ShownCard != null && host.ShownCard.StartsWith("find:"));
                yield return Shot("trailhead-millstone-find-card", "tap the old millstone → the find (in-engine text; marks it reviewed)");
                host.CloseFind();
                host.View.TextScale = 2f;
                Assert.IsTrue(host.OpenFind());
                yield return Shot("trailhead-millstone-find-card-text2x", "the millstone find at 2x text");
                host.View.TextScale = 1f;
                host.CloseFind();
                Assert.AreEqual(TrailheadTarget.Mira, host.TapAt(new Vector2(0.75f, 0.8f), aspect));
                Assert.IsNotNull(host.View.Dialogue);
                yield return Shot("home-tap-mira-dialogue", "tap Mira");
                var help = host.View.Dialogue!.Actions.Single(a => a.Kind == DialogueActionKind.HelpRestore);
                host.View.React(); // as the dialogue button does (TrailheadWorldHost binding)
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

        /// <summary>D-216: the River Intake reached on foot from the trailhead in the running host: Tavi, the jammed gate,
        /// the restoration walked to completion, the restored gate and the optional brass-plate find.</summary>
        [UnityTest] public IEnumerator Capture_river_intake_with_tavi_and_find()
        {
            string outDir = Environment.GetEnvironmentVariable("WG_TRAILHEAD_CAPTURE_DIR");
            if (string.IsNullOrEmpty(outDir)) { Assert.Ignore("WG_TRAILHEAD_CAPTURE_DIR not set"); yield break; }
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) { Assert.Ignore("no graphics device"); yield break; }
            Directory.CreateDirectory(outDir);
            var dir = Path.Combine(UnityEngine.Application.temporaryCachePath, "wg-ri-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var worldRt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var uiRt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var go = new GameObject("intake-world");
            try
            {
                var clock = new ManualClock(new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero));
                var s = ShellBootstrap.CreateSession(dir, new ShellConnectionPort(true), clock);
                Assert.AreEqual(StartStatus.NewGameCreated, s.StartNewGame(42UL, overwriteExisting: false).Status);
                int walk = 0;
                void Walk(long steps, double hours)
                {
                    clock.Advance(TimeSpan.FromHours(hours));
                    s.IngestActivityBatch(new[] { new NormalizedActivityRecord("capture", "r" + (++walk), ActivityCategory.Walking, ActivityUnits.Steps, steps, clock.UtcNow.AddMinutes(-30), clock.UtcNow) });
                    if (s.GetPendingReturnSummary() != null) s.AcknowledgeReturnSummary();
                }
                ProjectStatus St(string id) => s.GetProjects().Projects.Single(p => p.ProjectId == id).Status;
                Assert.IsTrue(s.EnqueueProject("proj.clear-trailhead").IsSuccess);
                for (int i = 0; i < 80 && St("proj.clear-trailhead") != ProjectStatus.Completed; i++) Walk(1500, 2);

                var panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("ShellTheme");
                panel.targetTexture = uiRt; panel.scaleMode = PanelScaleMode.ConstantPixelSize; panel.scale = 1f;
                panel.clearColor = true; panel.colorClearValue = new Color(0, 0, 0, 0);
                var host = go.AddComponent<TrailheadWorldHost>();
                host.Open(s, new InMemoryPresentationMemory(), NullOsMotionPreference.Instance, 1f, () => { }, panel, worldRt);
                host.Place.ReducedMotion = true;
                var travel = host.Overlay!.Q<Button>("world-travel");
                Assert.IsNotNull(travel, "the trailhead offers the path to the intake once it is open");
                Assert.GreaterOrEqual(travel!.resolvedStyle.minHeight.value, 48f);
                host.GoTo(RiverIntakeArt.LocationId);
                Assert.IsNotNull(host.Intake); host.Place.ReducedMotion = true;
                var caption = new Label { name = "capture-caption" }; caption.style.position = Position.Absolute; caption.style.top = 8; caption.style.left = 8; caption.style.right = 110;
                caption.style.backgroundColor = new Color(0.24f, 0.25f, 0.22f, 0.8f); caption.style.color = Color.white; caption.style.fontSize = 15; caption.style.whiteSpace = WhiteSpace.Normal;
                caption.pickingMode = PickingMode.Ignore;
                var cam = go.GetComponentInChildren<Camera>();
                var shots = new List<string>();
                IEnumerator Shot(string name, string note)
                {
                    if (caption.parent != host.Overlay) host.Overlay!.Add(caption);
                    var f = host.Intake!.Frame!;
                    caption.text = "Unity editor capture (software GL, not a device) · Phase D River Intake · " + f.Stage.ToString().ToLowerInvariant() + " · " + (f.GateKey ?? "no gate key") + " · " + note;
                    host.Place.Settle();
                    for (int i = 0; i < 6; i++) yield return null;
                    cam.Render();
                    Dump(worldRt, Path.Combine(outDir, name + ".world")); Dump(uiRt, Path.Combine(outDir, name + ".ui"));
                    shots.Add(name);
                    Debug.Log("WG_TRAILHEAD_CAPTURE " + name + " " + caption.text);
                }
                float aspect = (float)W / H;
                Assert.IsTrue(host.Intake!.TaviVisible, "Tavi waits at the intake after the trailhead is cleared");
                Assert.AreEqual(0, host.Intake.Frame!.GateIndex);
                Assert.IsFalse(host.Intake.PlateTappable, "no find before the gates are freed");
                yield return Shot("intake-s0-gate-jammed-tavi", "walked from the trailhead (canonical: trailhead cleared)");
                Assert.AreEqual(IntakeTarget.Tavi, host.TapIntakeAt(new Vector2(0.18f, 0.80f), aspect));
                Assert.IsNotNull(host.Place.Dialogue);
                yield return Shot("intake-dialogue-tavi", "tap Tavi (skippable)");
                host.Place.TextScale = 2f; host.Intake.RedrawDialogue();
                yield return Shot("intake-dialogue-tavi-text2x", "same dialogue at 2x text");
                host.Place.TextScale = 1f; host.Controller.SkipDialogue();
                host.Controller.CloseLocation();
                // D-218: walk the intake build (G0 → G1 → G2), then landmark Functional and Restored from real play.
                Assert.IsTrue(s.EnqueueProject(RiverIntakeArt.GateId).IsSuccess);
                bool sawG1 = false;
                for (int day = 0; day < 80 && St(RiverIntakeArt.GateId) != ProjectStatus.Completed; day++)
                {
                    Walk(1500, 2);
                    host.Controller.Resume();
                    if (!sawG1 && host.Intake!.Frame!.GateIndex == 1)
                    {
                        sawG1 = true; host.Place.ReducedMotion = true;
                        yield return Shot("intake-gate-rebuilding-g1", "canonical: proj.river-intake mid-build (structural)");
                    }
                }
                Assert.IsTrue(sawG1, "G1 rebuilding layer was reached during the build");
                Assert.AreEqual(ProjectStatus.Completed, St(RiverIntakeArt.GateId));
                host.Controller.Resume(); host.GoTo(TrailheadArt.LocationId); host.GoTo(RiverIntakeArt.LocationId); host.Place.ReducedMotion = true;
                Assert.AreEqual(2, host.Intake!.Frame!.GateIndex);
                Assert.IsTrue(host.Intake.PlateTappable, "the find is offered once the intake discovery is unlocked");
                Assert.IsTrue(host.Intake.Glow.enabled, "glow ring on (static under reduced motion)");
                yield return Shot("intake-s1-gate-restored-plate-glow", "canonical: Stabilized + gate @done; optional find glowing");
                var findButton = host.Overlay!.Q<Button>("world-find");
                Assert.AreEqual(DisplayStyle.Flex, findButton.resolvedStyle.display, "screen-reader route to the same find");
                Assert.AreEqual(IntakeTarget.Plate, host.TapIntakeAt(new Vector2(RiverIntakeArt.PlateX, RiverIntakeArt.PlateY), aspect));
                Assert.AreEqual("find:" + RiverIntakeArt.DiscoveryId, host.ShownCard);
                yield return Shot("intake-find-card", "tap the brass plate → the find (in-engine text; marks it reviewed)");
                Assert.IsFalse(host.Intake.Glow.enabled, "reviewed: the glow stops, the plate stays tappable");
                host.Place.TextScale = 2f; host.OpenFind();
                yield return Shot("intake-find-card-text2x", "the find at 2x text");
                host.Place.TextScale = 1f;
                host.CloseFind(); Assert.IsNull(host.ShownCard);
                var stagesShot = new HashSet<string>();
                for (int day = 0; day < 500 && !stagesShot.Contains("restored"); day++)
                {
                    if (!s.GetProjects().Projects.Any(p => p.Status == ProjectStatus.Queued || p.Status == ProjectStatus.Active))
                    {
                        var open = s.GetProjects().Projects.FirstOrDefault(p => p.Status == ProjectStatus.Available);
                        if (open != null) s.EnqueueProject(open.ProjectId);
                    }
                    Walk(20000, 24);
                    host.Controller.Resume();
                    string st = host.Intake!.Frame!.Stage.ToString().ToLowerInvariant();
                    if ((st == "functional" || st == "restored") && stagesShot.Add(st))
                    {
                        host.Place.ReducedMotion = true;
                        yield return Shot("intake-s" + (int)host.Intake.Frame.Stage + "-" + st,
                            "canonical: day " + (day + 1) + (st == "restored" ? " (accents on)" : ""));
                        if (st == "restored") Assert.IsNotNull(host.Intake.Frame.Accents, "Restored draws the flourish accents");
                    }
                }
                Assert.IsTrue(stagesShot.Contains("functional"), "Functional reached by WetlandDrainage in real play");
                Assert.IsTrue(stagesShot.Contains("restored"), "Restored reached by CanopyWalkway in real play");
                host.Intake.Show(new LandmarkPresentation(RiverIntakeArt.LocationId, "River Intake", RiverIntakeArt.LocationId + "@flourishing", null, true, true, false,
                    new[] { RiverIntakeArt.TaviId }, new[] { new WalkGame.Application.ReadModels.WorldViewReadModel.ProjectVisual(RiverIntakeArt.GateId, RiverIntakeArt.LocationId, ProjectStatus.Completed, 0, 2) }));
                yield return Shot("intake-s4-flourishing-keydriven", "KEY-DRIVEN (no Flourishing stage in current content; accents on)");
                Assert.IsNotNull(host.Intake.Frame!.Accents);
                File.WriteAllText(Path.Combine(outDir, "captures-intake.txt"), string.Join("\n", shots) + "\nmissing keys: " + string.Join(", ", host.Intake.MissingKeys) + "\n");
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
