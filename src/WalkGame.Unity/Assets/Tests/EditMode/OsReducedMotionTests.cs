using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>
    /// D-186: effective reduced motion = in-game setting OR the phone's setting. Pins the OR logic,
    /// the null default, failure safety, and the Settings copy. Native adapters are compiled out in
    /// the editor; their device behaviour is UNVERIFIED (KI-002).
    /// </summary>
    public sealed class OsReducedMotionTests
    {
        private string _dir = "";
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-osmotion-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private sealed class FixedOs : IOsMotionPreference
        {
            public bool Value; public int Reads;
            public bool ReduceMotionRequested { get { Reads++; return Value; } }
        }

        private sealed class ThrowingOs : IOsMotionPreference
        {
            public bool ReduceMotionRequested => throw new InvalidOperationException("native bridge failed");
        }

        private ShellController Started(IOsMotionPreference os)
        {
            var c = new ShellController(ShellBootstrap.CreateSession(_dir), () => 42UL, devTools: false) { OsMotion = os };
            c.Boot(); c.Invoke("start");
            return c;
        }

        [TestCase(false, false, false)]
        [TestCase(true, false, true)]
        [TestCase(false, true, true)]
        [TestCase(true, true, true)]
        public void Effective_flag_is_setting_or_os(bool setting, bool os, bool expected) =>
            Assert.AreEqual(expected, MotionPreference.Effective(setting, new FixedOs { Value = os }));

        [Test] public void A_missing_adapter_means_setting_only()
        {
            Assert.IsFalse(MotionPreference.Effective(false, null));
            Assert.IsTrue(MotionPreference.Effective(true, null));
        }

        [Test] public void A_failing_adapter_falls_back_to_the_setting()
        {
            Assert.IsFalse(MotionPreference.Effective(false, new ThrowingOs()));
            Assert.IsTrue(MotionPreference.Effective(true, new ThrowingOs()));
        }

        [Test] public void The_os_is_not_asked_when_the_setting_is_already_on()
        {
            var os = new FixedOs { Value = false };
            Assert.IsTrue(MotionPreference.Effective(true, os));
            Assert.AreEqual(0, os.Reads);
        }

        [Test] public void The_editor_uses_the_null_adapter_and_native_adapters_report_false()
        {
            Assert.IsFalse(AndroidOsMotionPreference.IsAvailable);
            Assert.IsFalse(IosOsMotionPreference.IsAvailable);
            Assert.AreSame(NullOsMotionPreference.Instance, MotionPreference.ForCurrentPlatform());
            Assert.IsFalse(NullOsMotionPreference.Instance.ReduceMotionRequested);
            Assert.IsFalse(new AndroidOsMotionPreference().ReduceMotionRequested);
            Assert.IsFalse(new IosOsMotionPreference().ReduceMotionRequested);
        }

        [TestCase(0f, true)]
        [TestCase(-0f, true)]
        [TestCase(-1f, true)]
        [TestCase(0.5f, false)]
        [TestCase(1f, false)]
        [TestCase(10f, false)]
        [TestCase(float.NaN, false)]
        public void Android_remove_animations_is_a_zero_scale(float scale, bool expected) =>
            Assert.AreEqual(expected, AndroidOsMotionPreference.IsRemoveAnimations(scale));

        [Test] public void Controller_defaults_to_the_null_adapter()
        {
            var c = new ShellController(ShellBootstrap.CreateSession(_dir), () => 42UL, devTools: false);
            Assert.AreSame(NullOsMotionPreference.Instance, c.OsMotion);
            c.Boot(); c.Invoke("start");
            Assert.IsFalse(c.EffectiveReducedMotion);
        }

        [Test] public void Controller_effective_flag_follows_the_phone_without_changing_the_saved_setting()
        {
            var os = new FixedOs { Value = true };
            var c = Started(os);
            Assert.IsTrue(c.EffectiveReducedMotion);
            os.Value = false;
            Assert.IsFalse(c.EffectiveReducedMotion, "the phone setting is read live, not latched");
            c.Invoke("nav:Settings"); c.Invoke("toggle:reducedMotion");
            Assert.IsTrue(c.EffectiveReducedMotion);
            var reloaded = new ShellController(ShellBootstrap.CreateSession(_dir), () => 42UL, devTools: false);
            reloaded.Boot();
            Assert.IsTrue(reloaded.EffectiveReducedMotion, "the in-game choice persists on its own");
        }

        [Test] public void Settings_explains_when_the_phone_turns_reduced_motion_on()
        {
            var c = Started(new FixedOs { Value = true });
            c.Invoke("nav:Settings");
            var m = c.Render();
            Assert.Contains(PlayerStrings.T("settings.motion.os"), m.Lines);
            Assert.IsFalse(m.Lines.Contains(ShellController.ReducedMotionLine(false)));
            Assert.IsTrue(m.Actions.Any(a => a.Id == "toggle:reducedMotion"), "the in-game toggle stays available");
        }

        [Test] public void Settings_shows_the_in_game_line_when_the_setting_is_on_or_the_phone_is_off()
        {
            var c = Started(new FixedOs { Value = false });
            c.Invoke("nav:Settings");
            Assert.Contains(ShellController.ReducedMotionLine(false), c.Render().Lines);
            c.OsMotion = new FixedOs { Value = true };
            c.Invoke("toggle:reducedMotion");
            var lines = c.Render().Lines;
            Assert.Contains(ShellController.ReducedMotionLine(true), lines);
            Assert.IsFalse(lines.Contains(PlayerStrings.T("settings.motion.os")));
        }

        [Test] public void Settings_survive_a_failing_adapter()
        {
            var c = Started(new ThrowingOs());
            c.Invoke("nav:Settings");
            Assert.Contains(ShellController.ReducedMotionLine(false), c.Render().Lines);
            Assert.IsFalse(c.EffectiveReducedMotion);
        }
    }
}
