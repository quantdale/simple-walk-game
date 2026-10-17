using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application.Activity;
using WalkGame.Domain.Activity;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>
    /// D-164: issues found in the onboarding step captures (docs/evidence/d164-onboarding-steps).
    /// The walking step never says "keep going without connecting" under "Connected."; the first-project
    /// step does not ask the player to "pick one" when the valley offers a single place.
    /// Unity-editor-tested; device UNVERIFIED (KI-002).
    /// </summary>
    public sealed class OnboardingGuideCopyTests
    {
        private sealed class Port : IActivityConnectionPort
        {
            public ActivityPermissionState Permission = ActivityPermissionState.NotRequested;
            public ActivitySourceAvailability Availability = ActivitySourceAvailability.Available;
            public ActivityConnectionSnapshot SnapshotConnection() => new ActivityConnectionSnapshot(Permission, Availability);
        }

        private sealed class EmptySource : IActivityRecordSource
        {
            public string ProviderNamespace => "test.guidecopy";
            public IReadOnlyList<NormalizedActivityRecord> FetchRecords(DateTimeOffset s, DateTimeOffset e) => Array.Empty<NormalizedActivityRecord>();
        }

        private string _dir = "";
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-guidecopy-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private ShellController WalkingStep(Port port)
        {
            var c = new ShellController(ShellBootstrap.CreateSession(_dir, port), () => 42UL, devTools: false, new EmptySource(), () => { });
            c.Boot(); c.Invoke("start"); c.Invoke("guide:next");
            Assert.AreEqual(ShellScreen.Guide, c.Current);
            return c;
        }

        private static bool SaysKeepGoingWithoutConnecting(ScreenModel m) => m.Lines.Any(l => l.Contains("without connecting"));

        [Test] public void Connected_walking_step_does_not_offer_to_go_on_without_connecting()
        {
            var c = WalkingStep(new Port { Permission = ActivityPermissionState.Granted });
            var m = c.Render();
            Assert.IsTrue(m.Lines.Any(l => l.StartsWith("Connected", StringComparison.Ordinal)), string.Join("|", m.Lines));
            Assert.IsFalse(SaysKeepGoingWithoutConnecting(m), string.Join("|", m.Lines));
            Assert.IsTrue(m.Actions.Any(a => a.Id == "guide:next"));
        }

        [TestCase(ActivityPermissionState.NotRequested, ActivitySourceAvailability.Available)]
        [TestCase(ActivityPermissionState.Denied, ActivitySourceAvailability.Available)]
        [TestCase(ActivityPermissionState.NotRequested, ActivitySourceAvailability.Unsupported)]
        [TestCase(ActivityPermissionState.NotRequested, ActivitySourceAvailability.ProviderUpdateRequired)]
        public void Unconnected_walking_step_still_says_nothing_is_locked(ActivityPermissionState permission, ActivitySourceAvailability availability)
        {
            var c = WalkingStep(new Port { Permission = permission, Availability = availability });
            var m = c.Render();
            Assert.IsTrue(m.Lines.Any(l => l.Contains("nothing is locked")), string.Join("|", m.Lines));
            Assert.IsTrue(m.Actions.Any(a => a.Id == "guide:next"), "never trapped");
        }

        [Test] public void Connected_status_classification_covers_every_status()
        {
            foreach (ActivityPlayerStatus s in Enum.GetValues(typeof(ActivityPlayerStatus)))
            {
                bool expected = s == ActivityPlayerStatus.ConnectedCurrent || s == ActivityPlayerStatus.WaitingForFirstData
                                || s == ActivityPlayerStatus.RefreshTemporarilyFailed;
                Assert.AreEqual(expected, ShellController.IsConnectedStatus(s), s.ToString());
            }
        }

        [Test] public void First_project_step_matches_how_many_places_are_offered()
        {
            var c = WalkingStep(new Port());
            c.Invoke("guide:next");
            var m = c.Render();
            int offered = m.Actions.Count(a => a.Id.StartsWith("queue:", StringComparison.Ordinal));
            Assert.That(offered, Is.GreaterThan(0));
            bool pickOne = m.Lines.Any(l => l.StartsWith("Pick one place", StringComparison.Ordinal));
            bool startHere = m.Lines.Any(l => l.StartsWith("Start here.", StringComparison.Ordinal));
            Assert.AreEqual(offered > 1, pickOne, string.Join("|", m.Lines));
            Assert.AreEqual(offered == 1, startHere, string.Join("|", m.Lines));
        }
    }
}
