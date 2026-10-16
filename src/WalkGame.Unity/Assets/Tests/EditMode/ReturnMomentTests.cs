using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Application.Development;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Summaries;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>Return-summary restoration moments (UX_DESIGN §4, §19). Unity-editor-tested.</summary>
    public sealed class ReturnMomentTests
    {
        private string _dir = "";
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-moment-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private (ShellController c, GameSession s) AfterBigWalk()
        {
            var s = ShellBootstrap.CreateSession(_dir);
            var c = new ShellController(s, () => 42UL, devTools: true);
            c.Boot(); c.Invoke("start");
            foreach (var p in s.GetProjects().Projects.Where(p => p.Status == ProjectStatus.Available)) s.EnqueueProject(p.ProjectId);
            if (s.GetPendingReturnSummary() != null) s.AcknowledgeReturnSummary();
            var now = DateTimeOffset.UtcNow;
            s.IngestFromSource(new SyntheticWalkingSource(200000), now.AddDays(-10), now);
            c.Invoke("nav:ReturnSummary");
            return (c, s);
        }

        [Test] public void A_landmark_transformation_shows_the_valley_with_the_changed_landmark_circled()
        {
            var (c, s) = AfterBigWalk();
            var summary = s.GetPendingReturnSummary();
            Assert.IsNotNull(summary);
            Assume.That(summary!.Items.Any(i => i.Kind == SummaryItemKind.Transformation), "walk produced a transformation");
            var m = c.Render();
            Assert.IsNotNull(m.Scene);
            Assert.That(m.Scene!.ChangedCount, Is.GreaterThan(0), string.Join("|", summary.Items.Select(i => i.Text)));
            Assert.AreEqual("The valley changed while you were away.", m.Lines[0]);
            StringAssert.Contains("circled", m.Scene.AltText);
            Assert.That(m.Scene.ShapeCount(), Is.LessThanOrEqualTo(RegionScene.MaxShapes));
        }

        [Test] public void Moments_never_block_one_primary_continue_and_home_remain()
        {
            var (c, _) = AfterBigWalk();
            var m = c.Render();
            Assert.AreEqual("ack", m.Actions[0].Id, "first (primary) action dismisses");
            Assert.IsTrue(m.Actions.Any(a => a.Id == "nav:Home"));
            c.Invoke("ack");
            Assert.AreEqual(ShellScreen.Home, c.Current);
        }

        [Test] public void Dismissed_summary_has_no_moments()
        {
            var (c, _) = AfterBigWalk();
            c.Invoke("ack"); c.Invoke("nav:ReturnSummary");
            Assert.IsNull(c.Render().Scene);
        }

        [Test] public void Only_titles_named_in_transformation_lines_are_marked()
        {
            var s = ShellBootstrap.CreateSession(_dir);
            var c = new ShellController(s, () => 42UL, devTools: true); c.Boot(); c.Invoke("start");
            var region = s.GetRegion();
            var title = PlayerCopy.Title(region.Landmarks[0].TitleKey);
            var scene = RegionScene.Build(region, new[] { title + " is no longer in ruins." });
            Assert.IsTrue(scene.Landmarks[0].Changed);
            Assert.AreEqual(1, scene.ChangedCount);
            Assert.AreEqual(0, RegionScene.Build(region, new[] { "Something unrelated happened." }).ChangedCount);
            Assert.AreEqual(0, RegionScene.Build(region).ChangedCount);
            Assert.AreEqual(scene.ShapeCount(), RegionScene.Build(region).ShapeCount() + 1, "one ring per moment");
        }
    }
}
