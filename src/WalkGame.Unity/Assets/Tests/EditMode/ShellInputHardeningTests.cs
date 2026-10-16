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
