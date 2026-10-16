using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Application.Development;
using WalkGame.Domain.Projects;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>D-104 calm optional sound cues. Unity-editor-tested (logic and samples); audible output on a device is UNVERIFIED.</summary>
    public sealed class SoundCueTests
    {
        private string _dir = "";
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-sound-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        [Test] public void Sound_off_means_no_cue_ever()
        {
            foreach (bool a in new[] { false, true })
            foreach (bool b in new[] { false, true })
            foreach (bool r in new[] { false, true })
                Assert.AreEqual(SoundCue.None, SoundCues.ForSummary(false, a, b, r));
        }

        [Test] public void Cue_priority_celebration_then_chapter_then_restoration()
        {
            Assert.AreEqual(SoundCue.Celebration, SoundCues.ForSummary(true, true, true, true));
            Assert.AreEqual(SoundCue.Chapter, SoundCues.ForSummary(true, false, true, true));
            Assert.AreEqual(SoundCue.Restoration, SoundCues.ForSummary(true, false, false, true));
            Assert.AreEqual(SoundCue.None, SoundCues.ForSummary(true, false, false, false));
        }

        [Test] public void Plays_only_on_arrival_never_on_redraw()
        {
            var m = new ScreenModel(ShellScreen.ReturnSummary, ScreenState.Normal, "t") { Cue = SoundCue.Restoration };
            Assert.IsTrue(SoundCues.ShouldPlay(null, m));
            Assert.IsTrue(SoundCues.ShouldPlay(ShellScreen.Home, m));
            Assert.IsFalse(SoundCues.ShouldPlay(ShellScreen.ReturnSummary, m));
            Assert.IsFalse(SoundCues.ShouldPlay(null, new ScreenModel(ShellScreen.Home, ScreenState.Normal, "h")));
        }

        [Test] public void Samples_are_quiet_short_click_free_and_deterministic()
        {
            foreach (SoundCue cue in new[] { SoundCue.Restoration, SoundCue.Chapter, SoundCue.Celebration })
            {
                var s = SoundCues.Samples(cue);
                Assert.That(s.Length, Is.GreaterThan(0));
                Assert.That(s.Length / (double)SoundCues.SampleRate, Is.LessThanOrEqualTo(1.0), cue + " stays under a second");
                Assert.That(s.Max(Math.Abs), Is.LessThanOrEqualTo(SoundCues.Peak));
                Assert.That(Math.Abs(s[0]), Is.LessThan(0.001f), "starts silent");
                Assert.AreEqual(0f, s[s.Length - 1], "ends silent");
                float maxJump = 0f;
                for (int i = 1; i < s.Length; i++) maxJump = Math.Max(maxJump, Math.Abs(s[i] - s[i - 1]));
                Assert.That(maxJump, Is.LessThan(0.08f), "no clicks");
                CollectionAssert.AreEqual(s, SoundCues.Samples(cue));
            }
            Assert.IsEmpty(SoundCues.Samples(SoundCue.None));
        }

        [Test] public void A_real_return_summary_carries_a_cue_only_with_sound_on()
        {
            var s = ShellBootstrap.CreateSession(_dir);
            var c = new ShellController(s, () => 42UL, devTools: true);
            c.Boot(); c.Invoke("start");
            foreach (var p in s.GetProjects().Projects.Where(p => p.Status == ProjectStatus.Available)) s.EnqueueProject(p.ProjectId);
            if (s.GetPendingReturnSummary() != null) s.AcknowledgeReturnSummary();
            var now = DateTimeOffset.UtcNow;
            s.IngestFromSource(new SyntheticWalkingSource(200000), now.AddDays(-10), now);
            c.Invoke("nav:ReturnSummary");
            Assume.That(s.GetPendingReturnSummary(), Is.Not.Null);

            bool soundOn = s.GetSettings().SoundEnabled;
            Assert.AreEqual(soundOn, c.Render().Cue != SoundCue.None, "cue follows the sound setting");
            s.SetSoundEnabled(!soundOn);
            Assert.AreEqual(!soundOn, c.Render().Cue != SoundCue.None, "cue follows the toggled setting");
        }
    }
}
