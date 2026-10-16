using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Application.Development;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Time;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>Discoveries and expeditions detail screen (B2, D-082). Unity-editor-tested; device UNVERIFIED (KI-002).</summary>
    public sealed class DiscoveriesScreenTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        private string _dir = "";
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-disc-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private (ShellController c, GameSession s) Play(int days)
        {
            var clock = new ManualClock(T0);
            var s = ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(true), clock);
            var c = new ShellController(s, () => 42UL, devTools: true);
            c.Boot(); c.Invoke("start");
            var src = new SyntheticWalkingSource(20000);
            for (int d = 1; d <= days; d++)
            {
                clock.Set(T0.AddDays(d));
                s.IngestFromSource(src, T0.AddDays(d - 1), T0.AddDays(d));
                var h = s.GetHome();
                if (h.ActiveProjectId == null && h.Queued.Count == 0)
                    foreach (var p in s.GetProjects().Projects)
                        if (p.Status == ProjectStatus.Available && s.EnqueueProject(p.ProjectId).IsSuccess) break;
            }
            return (c, s);
        }

        [Test] public void Region_links_to_the_detail_screen_and_a_new_game_is_an_empty_state()
        {
            var (c, s) = Play(0);
            c.Invoke("nav:Region");
            Assert.IsTrue(c.Render().Actions.Any(a => a.Id == "nav:Discoveries"));
            c.Invoke("nav:Discoveries");
            var m = c.Render();
            Assert.AreEqual(ShellScreen.Discoveries, m.Screen);
            Assert.AreEqual(ScreenState.Empty, m.State);
            Assert.AreEqual("Discoveries: 0 of 13 found.", m.Lines[0]);
            StringAssert.StartsWith("Nothing found yet.", m.Lines[1]);
            Assert.IsTrue(m.Lines.Any(l => l.Contains("13 more finds are still waiting")));
            Assert.IsTrue(m.Actions.Any(a => a.Id == "nav:Home"));
        }

        [Test] public void Unfound_discoveries_are_never_named()
        {
            var (c, s) = Play(10);
            c.Invoke("nav:Discoveries");
            var text = string.Join("\n", c.Render().Lines);
            foreach (var row in s.GetDiscoveries().Discoveries)
            {
                if (row.Unlocked) StringAssert.Contains(row.TitleKey, text);
                else StringAssert.DoesNotContain(row.TitleKey, text);
            }
        }

        [Test] public void Completed_region_lists_every_find_and_route_without_raw_keys()
        {
            var (c, s) = Play(90);
            Assert.IsTrue(s.GetRegion().RegionCompleted);
            c.Invoke("nav:Discoveries");
            var m = c.Render();
            Assert.AreEqual(ScreenState.Normal, m.State);
            var d = s.GetDiscoveries(); var e = s.GetExpeditions();
            Assert.AreEqual("Discoveries: " + d.UnlockedCount + " of " + d.TotalDiscoveries + " found.", m.Lines[0]);
            Assert.AreEqual(e.TotalExpeditions, m.Lines.Count(l => l.Contains(" · completed.") || l.Contains(" · open;") || l.Contains(" · opens after:")));
            foreach (var l in m.Lines)
            {
                StringAssert.DoesNotContain("disc.", l); StringAssert.DoesNotContain("exp.", l);
                StringAssert.DoesNotContain("lm.", l); StringAssert.DoesNotContain("@", l);
            }
        }

        [Test] public void Found_discoveries_show_their_story_and_origin()
        {
            var (c, s) = Play(90);
            c.Invoke("nav:Discoveries");
            var lines = c.Render().Lines;
            int shown = 0;
            foreach (var row in s.GetDiscoveries().Discoveries.Where(r => r.Unlocked))
            {
                int i = lines.FindIndex(l => l.StartsWith(row.TitleKey + " · "));
                Assert.GreaterOrEqual(i, 0, row.TitleKey);
                Assert.AreEqual(WalkGame.Application.Content.Region1DiscoveryCopy.Text(row.BodyKey), lines[i + 1]);
                Assert.AreEqual("Where it came from: " + WalkGame.Application.Content.Region1DiscoveryCopy.Text(row.ProvenanceKey), lines[i + 2]);
                shown++;
            }
            Assert.Greater(shown, 0);
        }

        [Test] public void Unfound_discovery_copy_is_never_shown()
        {
            var (c, s) = Play(10);
            c.Invoke("nav:Discoveries");
            var text = string.Join("\n", c.Render().Lines);
            foreach (var row in s.GetDiscoveries().Discoveries.Where(r => !r.Unlocked))
            {
                StringAssert.DoesNotContain(WalkGame.Application.Content.Region1DiscoveryCopy.Text(row.BodyKey), text);
                StringAssert.DoesNotContain(WalkGame.Application.Content.Region1DiscoveryCopy.Text(row.ProvenanceKey), text);
            }
        }

        [Test] public void Locked_routes_explain_what_opens_them_in_player_words()
        {
            var (c, _) = Play(0);
            c.Invoke("nav:Discoveries");
            var routes = c.Render().Lines.Where(l => l.Contains(" · opens after: ")).ToList();
            Assert.AreEqual(3, routes.Count);
            StringAssert.Contains("Reward: +50 Vitality.", routes.Single(l => l.StartsWith("Valley Transect")));
            StringAssert.Contains("flourishing", routes.Single(l => l.StartsWith("Valley Transect")));
        }

        [Test] public void Stage_need_never_leaks_raw_ids()
        {
            var lm = new Dictionary<string, string> { { "lm.canopy", "The Canopy" } };
            Assert.AreEqual("The Canopy flourishing", ShellController.StageNeed("lm.canopy@Flourishing", lm));
            Assert.AreEqual("a landmark restored", ShellController.StageNeed("lm.unknown@Restored", lm));
            Assert.AreEqual("more restoration", ShellController.StageNeed("garbage", lm));
            Assert.AreEqual("more restoration", ShellController.StageNeed(null, lm));
            Assert.AreEqual("find", PlayerCopy.DiscoveryCategory("unknown-kind"));
        }

        [Test] public void Viewing_changes_no_progression_and_copy_is_calm()
        {
            var (c, s) = Play(10);
            var before = File.ReadAllBytes(Path.Combine(_dir, "save.json"));
            c.Invoke("nav:Discoveries");
            var text = string.Join(" ", c.Render().Lines).ToLowerInvariant();
            CollectionAssert.AreEqual(before, File.ReadAllBytes(Path.Combine(_dir, "save.json")));
            foreach (var bad in new[] { "missed", "lost", "streak", "hurry", "don't forget", "decay", "expire", "!" })
                StringAssert.DoesNotContain(bad, text);
        }
    }
}
