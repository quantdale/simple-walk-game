using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using WalkGame.Shell;

namespace WalkGame.Shell.PlayModeTests
{
    /// <summary>The Region picture lives in a real UIDocument panel across frames without errors.
    /// Batchmode -nographics: layout runs, pixels are never rasterized (no visual claim).</summary>
    public sealed class ValleyPlayModeTests
    {
        [UnityTest] public IEnumerator Region_picture_lays_out_in_a_live_panel_without_errors()
        {
            var dir = Path.Combine(UnityEngine.Application.temporaryCachePath, "wg-valley-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var go = new GameObject("valley-test");
            try
            {
                var c = new ShellController(ShellBootstrap.CreateSession(dir), () => 9UL, devTools: true);
                c.Boot(); c.Invoke("start"); c.Invoke("nav:Region");
                var doc = go.AddComponent<UIDocument>();
                var panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("ShellTheme");
                Assert.IsNotNull(panel.themeStyleSheet, "shipped theme");
                panel.referenceResolution = new Vector2Int(400, 800);
                doc.panelSettings = panel;
                yield return null;
                ShellView.Render(doc.rootVisualElement, c.Render(), _ => { });
                for (int i = 0; i < 5; i++) yield return null;
                var valley = doc.rootVisualElement.Q<ValleyElement>("valley");
                Assert.IsNotNull(valley);
                Assert.That(valley.LastShapeCount, Is.LessThanOrEqualTo(RegionScene.MaxShapes));
                LogAssert.NoUnexpectedReceived();
            }
            finally { UnityEngine.Object.Destroy(go); Directory.Delete(dir, true); }
        }
    }
}
