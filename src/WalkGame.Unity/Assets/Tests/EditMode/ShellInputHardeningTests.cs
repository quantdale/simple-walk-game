using System;
using System.IO;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>Hostile or out-of-order shell input (C, D-084). Unity-editor-tested; device UNVERIFIED (KI-002).</summary>
    public sealed class ShellInputHardeningTests
    {
        private string _dir = "";
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-hard-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private ShellController Shell() => new ShellController(ShellBootstrap.CreateSession(_dir), () => 42UL, devTools: true);

        [TestCase("nav:")] [TestCase("nav:Nowhere")] [TestCase("nav:home")] [TestCase("nav:1")] [TestCase("nav:99")]
        [TestCase("nav:Home,Story")] [TestCase("nav: Home")]
        public void Malformed_navigation_is_a_calm_error_not_an_exception(string action)
        {
            var c = Shell(); c.Boot(); c.Invoke("start");
            var before = c.Current;
            Assert.DoesNotThrow(() => c.Invoke(action));
            Assert.AreEqual(before, c.Current);
            var m = c.Render();
            Assert.AreEqual(ScreenState.Error, m.State);
            Assert.AreEqual("That screen does not exist.", m.Notice);
        }

        [TestCase("nav:Home")] [TestCase("nav:Story")] [TestCase("nav:Discoveries")] [TestCase("queue:proj.x")] [TestCase("ack")]
        [TestCase("toggle:sound")] [TestCase("dev:walk")] [TestCase("guide:next")] [TestCase("delete:confirm")] [TestCase("")] [TestCase(null)]
        public void Game_actions_before_a_game_exists_never_throw(string action)
        {
            var c = Shell(); c.Boot();
            Assert.AreEqual(ShellScreen.Onboarding, c.Current);
            Assert.DoesNotThrow(() => c.Invoke(action));
            Assert.AreEqual(ShellScreen.Onboarding, c.Current);
            Assert.DoesNotThrow(() => c.Render());
            Assert.AreEqual(ScreenState.Error, c.Render().State);
        }

        [Test] public void Start_never_replaces_a_loaded_game()
        {
            var c = Shell(); c.Boot(); c.Invoke("start");
            var save = Path.Combine(_dir, "save.json");
            var before = File.ReadAllBytes(save);
            c.Invoke("nav:Onboarding");
            c.Invoke("start");
            CollectionAssert.AreEqual(before, File.ReadAllBytes(save));
            Assert.AreEqual("A game is already here; it was not replaced.", c.Render().Notice);
        }

        [Test] public void Start_never_overwrites_an_unreadable_save()
        {
            var c = Shell(); c.Boot(); c.Invoke("start");
            var save = Path.Combine(_dir, "save.json");
            File.WriteAllText(save, "{ not a save");
            foreach (var f in Directory.GetFiles(_dir, "*.bak*")) File.WriteAllText(f, "{ not a save");
            var broken = Shell(); broken.Boot();
            Assert.AreEqual(ShellScreen.BootError, broken.Current);
            broken.Invoke("start");
            Assert.AreEqual("{ not a save", File.ReadAllText(save));
            Assert.AreEqual(ShellScreen.BootError, broken.Current);
        }

        [Test] public void Start_refuses_a_save_that_appeared_after_boot()
        {
            // D-085: a save written after this shell booted to onboarding (e.g. another
            // session) is refused by the Application layer, not replaced.
            var c = Shell(); c.Boot();
            Assert.AreEqual(ShellScreen.Onboarding, c.Current);
            var other = Shell(); other.Boot(); other.Invoke("start");
            var save = Path.Combine(_dir, "save.json");
            var before = File.ReadAllBytes(save);
            c.Invoke("start");
            CollectionAssert.AreEqual(before, File.ReadAllBytes(save));
            Assert.AreEqual(StartStatus.ExistingSaveRefused, c.LastStartStatus);
            Assert.AreEqual(ShellScreen.Onboarding, c.Current);
            Assert.AreEqual("A game is already here; it was not replaced.", c.Render().Notice);
        }

        [Test] public void BootError_is_refused_while_a_game_is_loaded()
        {
            // D-110: a loaded game never shows "We couldn't open your world".
            var c = Shell(); c.Boot(); c.Invoke("start"); c.Invoke("nav:Home");
            Assert.DoesNotThrow(() => c.Invoke("nav:BootError"));
            Assert.AreEqual(ShellScreen.Home, c.Current);
            var m = c.Render();
            Assert.AreEqual(ScreenState.Error, m.State);
            Assert.AreEqual("Your world is open and fine. There is no problem to show.", m.Notice);
            c.Invoke("nav:Settings");
            Assert.AreEqual(ShellScreen.Settings, c.Current, "navigation still works after the refusal");
        }

        [Test] public void BootError_stays_reachable_before_a_game_is_loaded()
        {
            var c = Shell(); c.Boot();
            c.Invoke("nav:BootError");
            Assert.AreEqual(ShellScreen.BootError, c.Current);
        }

        [Test] public void After_delete_all_a_new_game_can_still_start()
        {
            var c = Shell(); c.Boot(); c.Invoke("start");
            c.Invoke("nav:Settings"); c.Invoke("delete:request"); c.Invoke("delete:confirm");
            Assert.AreEqual(ShellScreen.Onboarding, c.Current);
            c.Invoke("start");
            Assert.AreEqual(StartStatus.NewGameCreated, c.LastStartStatus);
            Assert.AreNotEqual(ShellScreen.Onboarding, c.Current);
        }
    }
}
