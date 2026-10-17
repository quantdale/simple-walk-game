using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Application.Activity;
using WalkGame.Infrastructure.Platform.HealthKit;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>
    /// D-193: HealthKit parity with the Health Connect lifecycle suite (D-188), using the real
    /// <see cref="HealthKitActivityAdapter"/> over a fake Swift bridge behind a <see cref="ShellController"/>.
    /// HealthKit cannot report read denial (the request status only says the dialog was answered), so a
    /// revoked read in iOS Settings is invisible: reads keep succeeding with no samples. That platform
    /// limit is pinned as "never loses progress, never double-credits", not as a Settings prompt.
    /// Unity-editor-tested; the Swift bridge and the iOS composition are UNVERIFIED (KI-022, KI-002).
    /// </summary>
    public sealed class HealthKitConnectionLifecycleTests
    {
        private sealed class Bridge : IHealthKitBridge
        {
            public string Availability = "available", Request = "unnecessary", Response = "ok";
            public Exception? Throw;
            public int Reads;
            public string GetAvailability() => Availability;
            public string GetRequestStatus() => Request;
            public string ReadSteps(long s, long e)
            {
                Reads++;
                if (Throw != null) throw Throw;
                return Response;
            }
        }

        private string _dir = "";
        private Bridge _bridge = new Bridge();
        private GameSession _session = null!;
        private int _connectRequests;

        [SetUp] public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "wg-hklife-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir);
            _bridge = new Bridge(); _connectRequests = 0;
        }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private static string Sample(string id, int daysAgo, int steps)
        {
            var end = DateTimeOffset.UtcNow.AddDays(-daysAgo);
            return id + "\t" + end.AddHours(-1).ToUnixTimeMilliseconds() + "\t" + end.ToUnixTimeMilliseconds() + "\t" + steps + "\t";
        }

        private ShellController Launch()
        {
            var adapter = new HealthKitActivityAdapter(_bridge, () => DateTimeOffset.UtcNow);
            _session = ShellBootstrap.CreateSession(_dir, adapter);
            return new ShellController(_session, () => 42UL, devTools: false, adapter, () => _connectRequests++) { MinLiveSyncInterval = TimeSpan.Zero };
        }

        private ShellController Started()
        {
            var c = Launch(); c.Boot(); c.Invoke("start");
            for (int i = 0; i < 10 && c.Current == ShellScreen.Guide; i++)
            {
                var m = c.Render();
                var next = m.Actions.FirstOrDefault(a => a.Id == "guide:next") ?? m.Actions.First(a => a.Id.StartsWith("queue:"));
                c.Invoke(next.Id);
            }
            return c;
        }

        private ShellController Connected()
        {
            var c = Started();
            _bridge.Response = "ok\n" + Sample("hk-1", 1, 6000);
            c.Resume();
            Assume.That(Credited(), Is.GreaterThan(0), "fixture: the first sample is credited");
            Assume.That(_session.GetActivityStatus().Status, Is.EqualTo(ActivityPlayerStatus.ConnectedCurrent));
            return c;
        }

        private long Credited() => _session.GetDiagnostics().LifetimeVitalityCredited;

        private static ScreenModel ActivityScreen(ShellController c)
        {
            c.Invoke("nav:ActivityStatus");
            var m = c.Render();
            Assert.AreEqual(ShellScreen.ActivityStatus, c.Current);
            Assert.AreNotEqual(ScreenState.Error, m.State, m.Notice);
            Assert.IsTrue(m.Actions.Any(a => a.Id == "nav:Home"), "the Activity screen never traps navigation");
            return m;
        }

        private static void AssertPlayable(ShellController c)
        {
            Assert.AreEqual(StartStatus.Loaded, c.LastStartStatus);
            c.Invoke("nav:Home");
            Assert.AreNotEqual(ScreenState.Error, c.Render().State);
            c.Invoke("nav:Settings");
            Assert.AreNotEqual(ScreenState.Error, c.Render().State);
        }

        [Test] public void Not_yet_asked_offers_connect_and_never_reads()
        {
            _bridge.Request = "should_request";
            var c = Started();
            c.Resume();
            Assert.AreEqual(0, _bridge.Reads);
            Assert.AreEqual(ActivityPlayerStatus.PermissionNeeded, _session.GetActivityStatus().Status);
            var m = ActivityScreen(c);
            Assert.Contains(PlayerStrings.T("activity.permissionNeeded"), m.Lines);
            Assert.IsTrue(m.Actions.Any(a => a.Id == "activity:connect"));
            c.Invoke("activity:connect");
            Assert.AreEqual(1, _connectRequests);
        }

        [Test] public void Connected_samples_are_credited_exactly_once_across_resumes_and_restarts()
        {
            var c = Connected();
            long earned = Credited();
            c.Resume(); c.Resume();
            Assert.AreEqual(earned, Credited());
            var c2 = Launch(); c2.Boot();
            Assert.AreEqual(earned, Credited(), "a restart re-reads the same samples and credits nothing");
            Assert.Contains(PlayerStrings.T("activity.connected"), ActivityScreen(c2).Lines);
        }

        [Test] public void A_read_revoked_in_ios_settings_is_invisible_but_never_loses_or_doubles_progress()
        {
            // Platform limit: HealthKit returns no samples rather than an error for a denied read.
            var c = Connected();
            long earned = Credited();
            _bridge.Response = "ok";
            c.Resume(); c.Resume();
            Assert.AreEqual(earned, Credited(), "an empty answer takes nothing back");
            Assert.AreNotEqual(ActivityPlayerStatus.PermissionDenied, _session.GetActivityStatus().Status, "HealthKit cannot tell us");
            _bridge.Response = "ok\n" + Sample("hk-1", 1, 6000) + "\n" + Sample("hk-2", 0, 2500);
            c.Resume();
            Assert.Greater(Credited(), earned, "re-allowed: the new sample counts");
            long after = Credited();
            c.Resume();
            Assert.AreEqual(after, Credited(), "and only once");
        }

        [Test] public void A_read_refused_with_a_permission_error_is_a_calm_refresh_failure_that_retries()
        {
            var c = Connected();
            long earned = Credited();
            _bridge.Response = "error:permission";
            Assert.DoesNotThrow(() => c.Resume());
            Assert.AreEqual(earned, Credited());
            Assert.AreEqual(ActivityPlayerStatus.RefreshTemporarilyFailed, _session.GetActivityStatus().Status);
            var m = ActivityScreen(c);
            Assert.Contains(PlayerStrings.T("activity.refreshFailed"), m.Lines);
            Assert.Contains(PlayerStrings.T("activity.action.retryLater"), m.Lines);
            int reads = _bridge.Reads;
            _bridge.Response = "ok\n" + Sample("hk-1", 1, 6000);
            c.Resume();
            Assert.AreEqual(reads + 1, _bridge.Reads);
            Assert.AreEqual(earned, Credited());
            Assert.AreEqual(ActivityPlayerStatus.ConnectedCurrent, _session.GetActivityStatus().Status);
        }

        [TestCase("error:timeout")]
        [TestCase("error:io")]
        [TestCase("error:unavailable")]
        [TestCase("garbage-response")]
        public void Bridge_errors_are_contained_and_keep_progress(string response)
        {
            var c = Connected();
            long earned = Credited();
            _bridge.Response = response;
            Assert.DoesNotThrow(() => c.Resume());
            Assert.AreEqual(earned, Credited());
            Assert.AreEqual(ActivityPlayerStatus.RefreshTemporarilyFailed, _session.GetActivityStatus().Status);
            AssertPlayable(c);
        }

        [Test] public void A_crashing_bridge_is_contained_and_keeps_progress()
        {
            var c = Connected();
            long earned = Credited();
            _bridge.Throw = new Exception("simulated Swift bridge crash");
            Assert.DoesNotThrow(() => c.Resume());
            Assert.AreEqual(earned, Credited());
            Assert.AreEqual(ActivityPlayerStatus.RefreshTemporarilyFailed, _session.GetActivityStatus().Status);
            AssertPlayable(c);
        }

        [Test] public void HealthKit_unavailable_mid_session_shows_unavailable_without_a_dead_end()
        {
            var c = Connected();
            long earned = Credited();
            int reads = _bridge.Reads;
            _bridge.Availability = "unsupported";
            c.Resume();
            Assert.AreEqual(reads, _bridge.Reads);
            Assert.AreEqual(earned, Credited());
            Assert.AreEqual(ActivityPlayerStatus.SourceUnavailable, _session.GetActivityStatus().Status);
            var m = ActivityScreen(c);
            Assert.Contains(PlayerStrings.T("activity.sourceUnavailable"), m.Lines);
            Assert.IsFalse(m.Actions.Any(a => a.Id == "activity:connect"));
            AssertPlayable(c);
        }

        [Test] public void An_unreadable_status_after_a_grant_is_a_calm_retry_and_a_new_process_says_not_connected()
        {
            var c = Connected();
            long earned = Credited();
            int reads = _bridge.Reads;
            _bridge.Availability = "weird";
            c.Resume();
            Assert.AreEqual(reads, _bridge.Reads);
            Assert.AreEqual(earned, Credited());
            Assert.AreEqual(ActivityPlayerStatus.RefreshTemporarilyFailed, _session.GetActivityStatus().Status, "D-192");
            Assert.IsFalse(ActivityScreen(c).Actions.Any(a => a.Id == "activity:connect"));

            var c2 = Launch(); c2.Boot();
            Assert.AreEqual(ActivityPlayerStatus.PermissionNeeded, _session.GetActivityStatus().Status, "a new process knows no grant");
            Assert.AreEqual(earned, Credited());
        }

        [Test] public void Watch_and_phone_overlap_never_double_credits_through_the_shell()
        {
            var c = Started();
            var end = DateTimeOffset.UtcNow.AddDays(-1);
            string s = end.AddHours(-1).ToUnixTimeMilliseconds().ToString(), e = end.ToUnixTimeMilliseconds().ToString();
            _bridge.Response = "ok\nhk-phone\t" + s + "\t" + e + "\t4000\t\tphone\nhk-watch\t" + s + "\t" + e + "\t4000\t\twatch";
            c.Resume();
            long once = Credited();
            Assert.Greater(once, 0);
            _bridge.Response = "ok\nhk-watch\t" + s + "\t" + e + "\t4000\t\twatch";
            c.Resume();
            Assert.AreEqual(once, Credited(), "the overlapping window counted once (D-114)");
        }
    }
}
