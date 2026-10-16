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
    /// <summary>
    /// M8 Performance (D-074): repeated screen enter/exit in a LIVE UIDocument panel, the way the
    /// app runs. Retained managed growth is logged (WG_PERF) and bounded loosely; Unity-editor
    /// measurement, not device (KI-002).
    /// </summary>
    public sealed class ShellPanelMemoryTests
    {
        private static readonly string[] Screens = { "Home", "Projects", "Region", "Settings", "ActivityStatus", "ReturnSummary" };

        [UnityTest] public IEnumerator Repeated_enter_exit_in_a_live_panel_does_not_retain_memory()
        {
            var dir = Path.Combine(UnityEngine.Application.temporaryCachePath, "wg-mem-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var go = new GameObject("mem-test");
            try
            {
                var c = new ShellController(ShellBootstrap.CreateSession(dir), () => 9UL, devTools: true);
                c.Boot(); c.Invoke("start");
                var doc = go.AddComponent<UIDocument>();
                var panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("ShellTheme");
                panel.referenceResolution = new Vector2Int(400, 800);
                doc.panelSettings = panel;
                yield return null;
                var root = doc.rootVisualElement;
                void Cycle() { foreach (var s in Screens) { c.Invoke("nav:" + s); ShellView.Render(root, c.Render(), _ => { }); } }

                for (int i = 0; i < 50; i++) { Cycle(); if (i % 10 == 0) yield return null; }
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                long start = GC.GetTotalMemory(true);
                long[] at = new long[4];
                for (int block = 0; block < 4; block++)
                {
                    for (int i = 0; i < 125; i++) { Cycle(); if (i % 25 == 0) yield return null; }   // 750 renders per block
                    yield return null;
                    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                    at[block] = GC.GetTotalMemory(true) - start;
                }
                Debug.Log("WG_PERF live_panel_retained_bytes at_750=" + at[0] + " at_1500=" + at[1] + " at_2250=" + at[2] + " at_3000=" + at[3]);
                // Trend, not absolute: the warm-up baseline includes one-off GC compaction (negative deltas seen).
                Assert.That(at[3] - at[0], Is.LessThan(1L * 1024 * 1024), "retained growth between 750 and 3,000 live-panel renders");
            }
            finally { UnityEngine.Object.Destroy(go); Directory.Delete(dir, true); }
        }
    }
}
