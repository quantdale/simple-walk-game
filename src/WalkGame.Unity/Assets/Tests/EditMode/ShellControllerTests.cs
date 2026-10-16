using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    public sealed class ShellControllerTests
    {
        private string _dir = "";

        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-shell-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private ShellController NewController() => new ShellController(ShellBootstrap.CreateSession(_dir), () => 42UL, devTools: true);

        [Test] public void Router_maps_every_start_status()
        {
            foreach (StartStatus s in Enum.GetValues(typeof(StartStatus)))
                Assert.DoesNotThrow(() => BootRouter.Route(s), s.ToString());
        }

        [TestCase(StartStatus.SaveUnreadable)]
        [TestCase(StartStatus.StateInvalid)]
        [TestCase(StartStatus.ContentIncompatible)]
        public void Router_sends_unreadable_outcomes_to_non_destructive_error(StartStatus s)
        {
            Assert.AreEqual(ShellScreen.BootError, BootRouter.Route(s).Screen);
        }

        [Test] public void Fresh_install_goes_to_premise_then_guide()
        {
            var c = NewController();
            c.Boot();
            Assert.AreEqual(StartStatus.NoSaveFound, c.LastStartStatus);
            Assert.AreEqual(ShellScreen.Onboarding, c.Current);
            c.Invoke("start");
            Assert.AreEqual(ShellScreen.Guide, c.Current, c.Render().Notice);
            Assert.AreNotEqual(ScreenState.Error, c.Render().State);
        }

        [Test] public void Garbage_save_shows_error_and_is_never_overwritten()
        {
            var c = NewController(); c.Boot(); c.Invoke("start");
            var save = Path.Combine(_dir, "save.json");
            Assert.IsTrue(File.Exists(save));
            foreach (var f in Directory.GetFiles(_dir).Where(f => Path.GetFileName(f).StartsWith("save"))) File.WriteAllText(f, "not json {{{");
            var before = File.ReadAllText(save);

            var c2 = NewController(); c2.Boot();
            Assert.AreEqual(ShellScreen.BootError, c2.Current);
            var model = c2.Render();
            Assert.AreEqual(ScreenState.Error, model.State);
            Assert.IsFalse(model.Actions.Any(a => a.Id == "start"), "error screen must not offer a new game");
            c2.Invoke("retry");
            Assert.AreEqual(before, File.ReadAllText(save));
        }

        [Test] public void Continue_round_trips_through_the_codec_in_unity_runtime()
        {
            var c = NewController(); c.Boot(); c.Invoke("start"); c.Invoke("dev:walk");
            var c2 = NewController(); c2.Boot();
            Assert.That(c2.LastStartStatus, Is.EqualTo(StartStatus.Loaded).Or.EqualTo(StartStatus.RecoveredFromBackup));
            Assert.That(c2.Current, Is.EqualTo(ShellScreen.Guide), "unfinished onboarding resumes after restart");
        }

        [Test] public void Every_screen_renders_without_error_after_new_game()
        {
            var c = NewController(); c.Boot(); c.Invoke("start");
            foreach (var s in new[] { "Home", "ReturnSummary", "Projects", "Settings", "ActivityStatus" })
            {
                c.Invoke("nav:" + s);
                var m = c.Render();
                Assert.AreNotEqual(ScreenState.Error, m.State, s);
                Assert.IsNotEmpty(m.Lines, s);
            }
        }

        [Test] public void Projects_queue_action_queues_a_project()
        {
            var c = NewController(); c.Boot(); c.Invoke("start"); c.Invoke("nav:Projects");
            var queue = c.Render().Actions.FirstOrDefault(a => a.Id.StartsWith("queue:"));
            Assume.That(queue, Is.Not.Null, "Region 1 offers an available project at start");
            c.Invoke(queue!.Id);
            var m = c.Render();
            Assert.AreNotEqual(ScreenState.Error, m.State, m.Notice);
            Assert.IsFalse(m.Actions.Any(a => a.Id == queue.Id));
        }

        [Test] public void Settings_toggle_persists_across_sessions()
        {
            var c = NewController(); c.Boot(); c.Invoke("start"); c.Invoke("nav:Settings");
            c.Invoke("toggle:reducedMotion");
            var c2 = NewController(); c2.Boot(); c2.Invoke("nav:Settings");
            Assert.Contains("Reduced motion: on", c2.Render().Lines);
        }

        [Test] public void Delete_cancel_keeps_data()
        {
            var c = NewController(); c.Boot(); c.Invoke("start"); c.Invoke("nav:Settings");
            c.Invoke("delete:request");
            Assert.AreEqual(ShellScreen.DeleteConfirm, c.Current);
            c.Invoke("delete:cancel");
            Assert.AreEqual(ShellScreen.Settings, c.Current);
            var c2 = NewController(); c2.Boot();
            Assert.AreNotEqual(StartStatus.NoSaveFound, c2.LastStartStatus);
        }

        [Test] public void Delete_confirm_erases_and_returns_to_onboarding()
        {
            var c = NewController(); c.Boot(); c.Invoke("start"); c.Invoke("nav:Settings");
            c.Invoke("delete:request"); c.Invoke("delete:confirm");
            Assert.AreEqual(ShellScreen.Onboarding, c.Current);
            Assert.IsEmpty(Directory.GetFiles(_dir));
            var c2 = NewController(); c2.Boot();
            Assert.AreEqual(StartStatus.NoSaveFound, c2.LastStartStatus);
        }

        private static void FinishOnboarding(ShellController c)
        {
            for (int i = 0; i < 12 && c.Current == ShellScreen.Guide; i++)
            {
                var actions = c.Render().Actions;
                var next = actions.FirstOrDefault(a => a.Id == "guide:next") ?? actions.First(a => a.Id.StartsWith("queue:"));
                c.Invoke(next.Id);
            }
        }

        [Test] public void Onboarding_walks_every_stage_and_lands_on_home()
        {
            var c = NewController(); c.Boot(); c.Invoke("start");
            var titles = new System.Collections.Generic.List<string>();
            for (int i = 0; i < 12 && c.Current == ShellScreen.Guide; i++)
            {
                var m = c.Render();
                Assert.AreNotEqual(ScreenState.Error, m.State, m.Notice);
                titles.Add(m.Title);
                var next = m.Actions.FirstOrDefault(a => a.Id == "guide:next") ?? m.Actions.First(a => a.Id.StartsWith("queue:"));
                c.Invoke(next.Id);
            }
            Assert.AreEqual(ShellScreen.Home, c.Current);
            CollectionAssert.IsSubsetOf(new[] { "The valley today", "Connect your walking", "Choose a first restoration", "Progress while you're away", "That's all" }, titles);
            var c2 = NewController(); c2.Boot();
            Assert.That(c2.Current, Is.EqualTo(ShellScreen.Home).Or.EqualTo(ShellScreen.ReturnSummary));
        }

        [Test] public void First_project_step_offers_no_skip()
        {
            var c = NewController(); c.Boot(); c.Invoke("start");
            for (int i = 0; i < 6; i++)
            {
                if (c.Render().Title == "Choose a first restoration") break;
                c.Invoke("guide:next");
            }
            var m = c.Render();
            Assert.AreEqual("Choose a first restoration", m.Title);
            Assert.IsFalse(m.Actions.Any(a => a.Id == "guide:next"), "first project completes only through a real project operation");
            Assert.IsTrue(m.Actions.Any(a => a.Id.StartsWith("queue:")));
        }

        [Test] public void Save_directory_is_under_persistent_data_path()
        {
            StringAssert.StartsWith(UnityEngine.Application.persistentDataPath, ShellBootstrap.SaveDirectory);
        }
    }
}
