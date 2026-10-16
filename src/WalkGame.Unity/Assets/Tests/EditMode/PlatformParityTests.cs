using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>
    /// D-108 iOS / non-Android parity check. Without a HealthKit adapter (KI-022), a release player on
    /// any non-Android platform must say plainly that no walking source exists, offer no dead-end
    /// Connect action, and still let the player finish onboarding and play. Headless; iOS device UNVERIFIED.
    /// </summary>
    public sealed class PlatformParityTests
    {
        private string _dir = "";
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-parity-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        // Same composition ShellBootstrap uses off Android in a release build: no live source, no permission request.
        private ShellController Release() =>
            new ShellController(ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(false)), () => 42UL, devTools: false);

        [Test] public void Release_port_reports_unsupported_with_a_traceable_detail()
        {
            var snap = new ShellConnectionPort(false).SnapshotConnection();
            Assert.AreEqual(WalkGame.Application.Activity.ActivitySourceAvailability.Unsupported, snap.Availability);
            Assert.AreEqual(WalkGame.Application.Activity.ActivityPermissionState.NotRequested, snap.Permission);
            StringAssert.Contains("KI-022", ShellConnectionPort.UnsupportedDetail);
        }

        [Test] public void Onboarding_without_a_provider_has_no_connect_dead_end_and_completes()
        {
            var c = Release(); c.Boot(); c.Invoke("start");
            bool sawConnectStage = false;
            for (int i = 0; i < 12 && c.Current == ShellScreen.Guide; i++)
            {
                var m = c.Render();
                if (m.Title == "Connect your walking")
                {
                    sawConnectStage = true;
                    Assert.IsFalse(m.Actions.Any(a => a.Id == "activity:connect"), "no Connect action without a provider");
                    Assert.IsTrue(m.Lines.Contains(PlayerCopy.ActivityStatus(WalkGame.Application.Activity.ActivityPlayerStatus.SourceUnavailable)), string.Join("|", m.Lines));
                }
                var q = m.Actions.FirstOrDefault(a => a.Id.StartsWith("queue:"));
                c.Invoke(m.Actions.Any(a => a.Id == "guide:next") ? "guide:next" : q?.Id ?? m.Actions[0].Id);
            }
            Assert.IsTrue(sawConnectStage, "the activity step was shown");
            Assert.AreEqual(ShellScreen.Home, c.Current, "onboarding completes without a provider");
        }

        [Test] public void Activity_screen_without_a_provider_is_honest_and_never_leaks_diagnostics()
        {
            var c = Release(); c.Boot(); c.Invoke("start");
            c.Invoke("nav:ActivityStatus");
            var m = c.Render();
            Assert.AreEqual(ShellScreen.ActivityStatus, c.Current);
            Assert.IsFalse(m.Actions.Any(a => a.Id == "activity:connect"));
            Assert.IsTrue(m.Lines.Contains(PlayerCopy.ActivityStatus(WalkGame.Application.Activity.ActivityPlayerStatus.SourceUnavailable)), string.Join("|", m.Lines));
            Assert.IsFalse(m.Lines.Any(l => l.Contains("KI-022") || l.Contains("HealthKit")), "diagnostic detail is not player copy");
        }

        [Test] public void Release_build_offers_no_development_walking()
        {
            var c = Release(); c.Boot(); c.Invoke("start");
            c.Invoke("nav:Home");
            Assert.IsFalse(c.Render().Actions.Any(a => a.Id.StartsWith("dev:")));
        }
    }
}
