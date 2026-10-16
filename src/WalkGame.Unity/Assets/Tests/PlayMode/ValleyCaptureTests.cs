using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using WalkGame.Application.Development;
using WalkGame.Domain.Projects;
using WalkGame.Shell;

namespace WalkGame.Shell.PlayModeTests
{
    /// <summary>
    /// Editor-rendered capture of the Region screen (tools/unity/capture-valley.sh). Runs only when
    /// WG_CAPTURE_PNG is set and a graphics device exists (software GL under Xvfb); otherwise it
    /// is ignored, so ordinary -nographics runs stay unaffected. Evidence label: editor-rendered, NOT device.
    /// </summary>
    public sealed class ValleyCaptureTests
    {
        [UnityTest] public IEnumerator Capture_region_screen_png()
        {
            string output = Environment.GetEnvironmentVariable("WG_CAPTURE_PNG");
            if (string.IsNullOrEmpty(output)) { Assert.Ignore("WG_CAPTURE_PNG not set"); yield break; }
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) { Assert.Ignore("no graphics device"); yield break; }

            var dir = Path.Combine(UnityEngine.Application.temporaryCachePath, "wg-capture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var go = new GameObject("capture");
            var rt = new RenderTexture(540, 1170, 24, RenderTextureFormat.ARGB32);
            try
            {
                string screen = Environment.GetEnvironmentVariable("WG_CAPTURE_SCREEN") ?? "Region";
                ShellController c;
                if (screen == "Celebration" || screen == "Discoveries" || screen == "EpilogueJournal")
                {
                    // Seeded completed-region state through the real pipeline (CompletedRegionFixture).
                    c = CompletedRegionFixture.Build(dir, out var done, out _);
                    Assert.IsTrue(done.GetRegion().RegionCompleted);
                    // WG_CAPTURE_LOCALE=pseudo: the D-122 overlay with "[xx] " on every routed content string (KI-023).
                    if (Environment.GetEnvironmentVariable("WG_CAPTURE_LOCALE") == "pseudo") done.ContentLocale = PseudoLocale();
                    c.Invoke(screen == "Celebration" ? "nav:ReturnSummary" : screen == "EpilogueJournal" ? "nav:Story" : "nav:Discoveries"); // EpilogueJournal: D-129 journal on a completed region
                }
                else if (screen == "Onboarding")
                {
                    // Fresh device: boot with no save, before any start (screen tour, D-107).
                    c = new ShellController(ShellBootstrap.CreateSession(dir), () => 42UL, devTools: true);
                    c.Boot();
                    Assert.AreEqual(ShellScreen.Onboarding, c.Current);
                }
                else if (screen == "BootError")
                {
                    // Both save slots unreadable, as in AccessibilityAuditTests: recovery cannot open the world.
                    var first = new ShellController(ShellBootstrap.CreateSession(dir), () => 42UL, devTools: true);
                    first.Boot(); first.Invoke("start");
                    foreach (var f in Directory.GetFiles(dir, "save*.json")) File.WriteAllText(f, "{ not a save");
                    c = new ShellController(ShellBootstrap.CreateSession(dir), () => 42UL, devTools: true);
                    c.Boot();
                    Assert.AreEqual(ShellScreen.BootError, c.Current);
                }
                else if (Environment.GetEnvironmentVariable("WG_CAPTURE_STATE") is string st && (st == "mid" || st == "late"))
                {
                    // Seeded mid-game (day 30) or late-game (day 70) save through the real pipeline, for the
                    // Region 1 mid/late polish review. WG_CAPTURE_DAY overrides the day.
                    int day = int.TryParse(Environment.GetEnvironmentVariable("WG_CAPTURE_DAY"), out var d) ? d : (st == "mid" ? 30 : 70);
                    c = CompletedRegionFixture.BuildUntil(dir, out var played, out _, day);
                    if (played.GetPendingReturnSummary() != null) played.AcknowledgeReturnSummary();
                    c.Invoke("nav:" + screen);
                    Assert.AreEqual(screen, c.Current.ToString(), "capture could not reach " + screen);
                }
                else
                {
                    var session = ShellBootstrap.CreateSession(dir);
                    c = new ShellController(session, () => 42UL, devTools: true);
                    c.Boot(); c.Invoke("start");
                    // WG_CAPTURE_STATE=fresh: the first minutes after "start" (no walks, nothing queued),
                    // for the empty/early-game review. Default: a mid-game valley.
                    if (Environment.GetEnvironmentVariable("WG_CAPTURE_STATE") != "fresh")
                    {
                        // A mid-game valley: queue what is open and walk a few days so stages differ.
                        foreach (var p in session.GetProjects().Projects)
                            if (p.Status == ProjectStatus.Available) session.EnqueueProject(p.ProjectId);
                        var now = DateTimeOffset.UtcNow;
                        session.IngestFromSource(new SyntheticWalkingSource(120000), now.AddDays(-6), now);
                    }
                    if (screen == "DeleteConfirm") c.Invoke("delete:request");
                    else if (screen != "Guide") c.Invoke("nav:" + screen);
                    Assert.AreEqual(screen, c.Current.ToString(), "capture could not reach " + screen);
                }

                float textScale = float.TryParse(Environment.GetEnvironmentVariable("WG_CAPTURE_TEXT_SCALE"),
                    System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ts) ? ts : 1f;
                c.TextScaleSource = () => textScale;
                var doc = go.AddComponent<UIDocument>();
                var panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("ShellTheme");
                panel.targetTexture = rt;
                panel.scaleMode = PanelScaleMode.ConstantPixelSize;
                panel.scale = 1.5f;
                panel.clearColor = true;
                doc.panelSettings = panel;
                yield return null;
                ShellView.Render(doc.rootVisualElement, c.Render(), _ => { }, textScale);
                for (int i = 0; i < 10; i++) yield return null;

                // WG_CAPTURE_SCROLL=end scrolls the shared ScrollView to the bottom (e.g. the Privacy
                // export button), and WG_CAPTURE_EXPECT_BUTTON asserts that a button with that exact
                // text is fully inside the visible viewport in the captured frame.
                string expectButton = Environment.GetEnvironmentVariable("WG_CAPTURE_EXPECT_BUTTON");
                if (Environment.GetEnvironmentVariable("WG_CAPTURE_SCROLL") == "end")
                {
                    var scroll = doc.rootVisualElement.Q<ScrollView>("content");
                    Assert.IsNotNull(scroll, "no content ScrollView to scroll");
                    float max = Mathf.Max(0f, scroll.contentContainer.layout.height - scroll.contentViewport.layout.height);
                    scroll.scrollOffset = new Vector2(0f, max);
                    for (int i = 0; i < 10; i++) yield return null;
                    Debug.Log("WG_CAPTURE_SCROLL offset=" + scroll.scrollOffset.y + " max=" + max);
                }
                if (!string.IsNullOrEmpty(expectButton))
                {
                    var viewport = doc.rootVisualElement.Q<ScrollView>("content")?.contentViewport.worldBound ?? doc.rootVisualElement.worldBound;
                    var button = doc.rootVisualElement.Query<Button>().Where(x => x.text == expectButton).First();
                    Assert.IsNotNull(button, "no button with text '" + expectButton + "'");
                    var wb = button.worldBound;
                    Assert.IsTrue(wb.yMin >= viewport.yMin - 0.5f && wb.yMax <= viewport.yMax + 0.5f,
                        "button '" + expectButton + "' not fully visible: " + wb + " in viewport " + viewport);
                    Debug.Log("WG_CAPTURE_BUTTON visible '" + expectButton + "' bounds=" + wb + " viewport=" + viewport);
                }

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                // Raw RGBA32, bottom-up rows; tools/unity/capture-valley.sh encodes the PNG
                // (the project does not include the ImageConversion module).
                File.WriteAllBytes(output + ".rgba", tex.GetRawTextureData());
                File.WriteAllText(output + ".size", rt.width + " " + rt.height);
                var valley = doc.rootVisualElement.Q<ValleyElement>("valley");
                Debug.Log("WG_CAPTURE device=" + SystemInfo.graphicsDeviceType + " renderer=" + SystemInfo.graphicsDeviceName
                    + " shapes=" + (valley?.LastShapeCount ?? -1) + " path=" + output);
                Assert.IsTrue(File.Exists(output + ".rgba"));
            }
            finally { UnityEngine.Object.Destroy(go); rt.Release(); Directory.Delete(dir, true); }
        }

        private static WalkGame.Application.Content.ContentText PseudoLocale()
        {
            var c = WalkGame.Application.Content.Region1Catalog.Create();
            var d = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
            void Add(string id, string field, string en) { if (!string.IsNullOrEmpty(en)) d[WalkGame.Application.Content.ContentText.Key(id, field)] = "[xx] " + en; }
            const string T = WalkGame.Application.Content.ContentText.Fields.Title;
            foreach (var p in c.Projects) Add(p.Id.Value, T, p.TitleKey);
            foreach (var l in c.Landmarks) Add(l.Id.Value, T, l.TitleKey);
            foreach (var e in c.Expeditions) Add(e.Id.Value, T, e.TitleKey);
            foreach (var x in c.Discoveries)
            {
                Add(x.Id.Value, T, x.TitleKey);
                Add(x.Id.Value, WalkGame.Application.Content.ContentText.Fields.Body, WalkGame.Application.Content.Region1DiscoveryCopy.Text(x.BodyKey));
                Add(x.Id.Value, WalkGame.Application.Content.ContentText.Fields.Provenance, WalkGame.Application.Content.Region1DiscoveryCopy.Text(x.ProvenanceKey));
            }
            return WalkGame.Application.Content.ContentText.ForLocale("xx", d);
        }
    }
}
