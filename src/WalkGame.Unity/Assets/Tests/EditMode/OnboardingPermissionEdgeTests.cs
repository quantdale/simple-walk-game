using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Application.Activity;
using WalkGame.Application.Ux;
using WalkGame.Domain.Activity;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>
    /// Onboarding and permission edge cases at the shell (D-092): deny then grant mid-onboarding,
    /// revoke after onboarding, app killed mid-onboarding or mid delete-all.
    /// Unity-editor-tested; device UNVERIFIED (KI-002): the real Health Connect dialog is not exercised.
    /// </summary>
    public sealed class OnboardingPermissionEdgeTests
    {
        private sealed class MutablePort : IActivityConnectionPort
        {
            public ActivityPermissionState Permission = ActivityPermissionState.NotRequested;
            public ActivitySourceAvailability Availability = ActivitySourceAvailability.Available;
            public ActivityConnectionSnapshot SnapshotConnection() =>
                new ActivityConnectionSnapshot(Permission, Availability);
        }

        private sealed class CountingSource : IActivityRecordSource
        {
            public int Fetches;
            public string ProviderNamespace => "test.edge";
            public IReadOnlyList<NormalizedActivityRecord> FetchRecords(DateTimeOffset s, DateTimeOffset e) { Fetches++; return Array.Empty<NormalizedActivityRecord>(); }
        }

        private string _dir = "";
        private MutablePort _port = new MutablePort();
        private CountingSource _source = new CountingSource();
        private int _requests;

        [SetUp] public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "wg-edge-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir);
            _port = new MutablePort(); _source = new CountingSource(); _requests = 0;
        }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        /// <summary>A fresh process over the same directory: what a killed-and-relaunched app sees.</summary>
        private ShellController Launch() => new ShellController(ShellBootstrap.CreateSession(_dir, _port), () => 42UL, devTools: false, _source, () => _requests++);


        private static bool Has(ShellController c, string id) => c.Render().Actions.Any(a => a.Id == id);

        private static void ToActivityStep(ShellController c)
        {
            c.Boot(); c.Invoke("start"); c.Invoke("guide:next");
            Assert.AreEqual("Connect your walking", c.Render().Title);
        }

        private static void FinishOnboarding(ShellController c)
        {
            for (int i = 0; i < 10 && c.Current == ShellScreen.Guide; i++)
            {
                var m = c.Render();
                var next = m.Actions.FirstOrDefault(a => a.Id == "guide:next") ?? m.Actions.First(a => a.Id.StartsWith("queue:"));
                c.Invoke(next.Id);
            }
            Assert.AreEqual(ShellScreen.Home, c.Current);
        }

        [Test] public void Deny_then_grant_mid_onboarding_keeps_the_step_and_starts_syncing()
        {
            var c = Launch(); ToActivityStep(c);
            Assert.IsTrue(Has(c, "activity:connect"));
            c.Invoke("activity:connect");
            Assert.AreEqual(1, _requests);

            _port.Permission = ActivityPermissionState.Denied; c.Resume();
            Assert.AreEqual(ShellScreen.Guide, c.Current);
            Assert.AreEqual("Connect your walking", c.Render().Title, "a denial never moves or loses the step");
            Assert.IsFalse(Has(c, "activity:connect"), "denied: point to settings, do not re-prompt");
            Assert.IsTrue(Has(c, "guide:next"), "denied: never trapped");
            Assert.AreEqual(0, _source.Fetches);

            _port.Permission = ActivityPermissionState.Granted; c.Resume();
            Assert.AreEqual("Connect your walking", c.Render().Title);
            Assert.AreEqual(ScreenState.Normal, c.Render().State);
            Assert.AreEqual(1, _source.Fetches, "a grant made in settings is picked up on resume");
            FinishOnboarding(c);
        }

        [Test] public void Outdated_health_connect_shows_update_required_not_connect()
        {
            var c = Launch(); c.Boot(); c.Invoke("start");
            _port.Availability = ActivitySourceAvailability.ProviderUpdateRequired; c.Invoke("guide:next");
            var m = c.Render();
            Assert.AreEqual("Update Health Connect", m.Title, "D-094: no Connect dead end");
            Assert.IsFalse(Has(c, "activity:connect"));
            Assert.IsTrue(m.Lines.Any(l => l.Contains("needs an update")));
            Assert.IsTrue(m.Lines.Any(l => l.StartsWith("Next: update Health Connect")));
            Assert.IsTrue(Has(c, "guide:next"), "never trapped");
            Assert.AreEqual(0, _source.Fetches, "no read on an outdated provider");

            _port.Availability = ActivitySourceAvailability.Available; c.Resume();
            Assert.AreEqual("Connect your walking", c.Render().Title, "after the update the normal step returns");
            Assert.IsTrue(Has(c, "activity:connect"));
            FinishOnboarding(c);
        }

        [Test] public void Revoke_after_onboarding_never_reopens_the_guide_or_reads_steps()
        {
            _port.Permission = ActivityPermissionState.Granted;
            var c = Launch(); c.Boot(); c.Invoke("start"); FinishOnboarding(c);
            int fetches = _source.Fetches;

            _port.Permission = ActivityPermissionState.Revoked; c.Resume();
            Assert.AreNotEqual(ShellScreen.Guide, c.Current);
            Assert.AreEqual(fetches, _source.Fetches, "no read without permission");
            c.Invoke("nav:ActivityStatus");
            var m = c.Render();
            Assert.AreNotEqual(ScreenState.Error, m.State);
            Assert.IsFalse(m.Actions.Any(a => a.Id == "activity:connect"));

            var relaunched = Launch(); relaunched.Boot();
            Assert.AreEqual(StartStatus.Loaded, relaunched.LastStartStatus);
            Assert.AreNotEqual(ShellScreen.Guide, relaunched.Current, "revocation is not a reason to repeat onboarding");
        }

        [Test] public void Killed_right_after_start_resumes_the_guide_not_a_new_game()
        {
            var c = Launch(); c.Boot(); c.Invoke("start");
            var again = Launch(); again.Boot();
            Assert.AreEqual(StartStatus.Loaded, again.LastStartStatus, "the started world is loaded, not replaced");
            Assert.AreEqual(ShellScreen.Guide, again.Current);
            Assert.AreEqual("The valley today", again.Render().Title);
        }

        [Test] public void Killed_at_the_first_project_step_resumes_there_after_a_denial()
        {
            var c = Launch(); ToActivityStep(c);
            _port.Permission = ActivityPermissionState.Denied;
            c.Invoke("guide:next");
            Assert.AreEqual("Choose a first restoration", c.Render().Title);

            var again = Launch(); again.Boot();
            Assert.AreEqual(ShellScreen.Guide, again.Current);
            Assert.AreEqual("Choose a first restoration", again.Render().Title);
            Assert.IsFalse(Has(again, "guide:next"), "the canonical first-project gate survives a restart");
            FinishOnboarding(again);
        }

        [Test] public void Killed_mid_delete_all_does_not_skip_onboarding_for_the_new_world()
        {
            var c = Launch(); c.Boot(); c.Invoke("start"); FinishOnboarding(c);
            c.Invoke("toggle:reducedMotion");
            // Delete-all erases saves first, then preferences; simulate a kill between the two.
            foreach (var f in Directory.GetFiles(_dir).Where(f => !Path.GetFileName(f).StartsWith("ux-preferences", StringComparison.Ordinal)))
                File.Delete(f);
            Assert.IsTrue(Directory.GetFiles(_dir).Length > 0, "stale preferences remain");

            var again = Launch(); again.Boot();
            Assert.AreEqual(ShellScreen.Onboarding, again.Current);
            again.Invoke("start");
            Assert.AreEqual(ShellScreen.Guide, again.Current, "a new world starts its onboarding (D-092)");
            Assert.AreEqual("The valley today", again.Render().Title);
            Assert.IsNull(again.Render().Notice);
        }
    }
}
