using System.Linq;
using NUnit.Framework;
using WalkGame.Shell;
using WalkGame.Shell.Presentation;

namespace WalkGame.Shell.Tests
{
    /// <summary>D-219: lighter Home + painted Region map. Unity-editor-tested.</summary>
    public sealed class HomeMenuTests
    {
        [Test] public void Home_lists_primary_destinations_and_tucks_the_rest_under_More()
        {
            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wg-home-" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(dir);
            try
            {
                var c = new ShellController(ShellBootstrap.CreateSession(dir, new ShellConnectionPort(true), new WalkGame.Domain.Time.ManualClock(new System.DateTimeOffset(2026, 3, 2, 9, 0, 0, System.TimeSpan.Zero))), () => 1UL, devTools: false);
                c.Boot(); c.Invoke("start");
                if (c.Current != ShellScreen.Home) c.Invoke("nav:Home");
                var ids = c.Render().Actions.Select(a => a.Id).ToList();
                CollectionAssert.Contains(ids, "nav:Projects");
                CollectionAssert.Contains(ids, "nav:Region");
                CollectionAssert.Contains(ids, ShellController.WorldTrailheadAction);
                CollectionAssert.Contains(ids, "nav:Settings");
                CollectionAssert.Contains(ids, "nav:More");
                CollectionAssert.DoesNotContain(ids, "nav:Story");
                CollectionAssert.DoesNotContain(ids, "nav:Progress");
                c.Invoke("nav:More");
                Assert.AreEqual(ShellScreen.More, c.Current);
                var more = c.Render().Actions.Select(a => a.Id).ToList();
                CollectionAssert.Contains(more, "nav:Story");
                CollectionAssert.Contains(more, "nav:Progress");
                CollectionAssert.Contains(more, "nav:ActivityStatus");
                CollectionAssert.Contains(more, "nav:Discoveries");
            }
            finally { try { System.IO.Directory.Delete(dir, true); } catch (System.IO.IOException) { } }
        }

        [Test] public void Region_map_resource_exists()
        {
            var tex = UnityEngine.Resources.Load<UnityEngine.Texture2D>(RegionArt.ResourceFolder + RegionArt.Map);
            Assert.IsNotNull(tex);
            Assert.AreEqual(720, tex.width); Assert.AreEqual(1280, tex.height);
        }
    }
}
