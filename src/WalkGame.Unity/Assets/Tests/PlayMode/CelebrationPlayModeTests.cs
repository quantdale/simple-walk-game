using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using WalkGame.Shell;

namespace WalkGame.Shell.PlayModeTests
{
    /// <summary>
    /// Integration: a real completed Region 1 reaches the celebration screen (D-080) and it renders
    /// into a live UIDocument over real frames. Unity-editor-tested (batchmode); device UNVERIFIED (KI-002).
    /// </summary>
    public sealed class CelebrationPlayModeTests
    {
        [UnityTest] public IEnumerator Completed_region_reaches_the_celebration_once()
        {
            var dir = Path.Combine(UnityEngine.Application.temporaryCachePath, "wg-cel-pm-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var go = new GameObject("celebration");
            try
            {
                var c = CompletedRegionFixture.Build(dir, out var session, out int days);
                Assert.IsTrue(session.GetRegion().RegionCompleted, "region did not complete within " + CompletedRegionFixture.HorizonDays + " days");
                c.Invoke("nav:Home");
                var home = c.Render();
                Assert.IsTrue(home.Actions.Any(a => a.Id == "nav:ReturnSummary"), "pending summary offered on Home");
                c.Invoke("nav:ReturnSummary");
                var m = c.Render();
                Assert.AreEqual("The valley is restored", m.Title);
                Assert.AreEqual(ShellController.CelebrationHeadline, m.Lines[0]);
                Assert.AreEqual("ack", m.Actions[0].Id, "Continue stays primary");
                Assert.IsTrue(m.Actions.Any(a => a.Id == "nav:Region"));

                var doc = go.AddComponent<UIDocument>();
                var panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("ShellTheme");
                doc.panelSettings = panel;
                yield return null;
                ShellView.Render(doc.rootVisualElement, m, _ => { });
                for (int i = 0; i < 3; i++) yield return null;
                var labels = doc.rootVisualElement.Query<Label>().ToList().Select(l => l.text).ToList();
                Assert.Contains(ShellController.CelebrationHeadline, labels);

                c.Invoke("nav:Region");
                Assert.AreEqual(ShellScreen.Region, c.Current);
                StringAssert.Contains("fully restored", c.Render().Lines[0]);
                c.Invoke("nav:ReturnSummary"); c.Invoke("ack");
                c.Invoke("nav:ReturnSummary");
                Assert.AreNotEqual("The valley is restored", c.Render().Title, "celebration shows once");
                Debug.Log("WG_CELEBRATION days=" + days);
            }
            finally { UnityEngine.Object.Destroy(go); Directory.Delete(dir, true); }
        }
    }
}
