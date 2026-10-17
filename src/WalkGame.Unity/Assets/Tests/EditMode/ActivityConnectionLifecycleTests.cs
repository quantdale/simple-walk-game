using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Application.Activity;
using WalkGame.Infrastructure.Platform.HealthConnect;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>
    /// D-188: permission revocation and provider-unavailable states end to end through the shell,
    /// using the real <see cref="HealthConnectActivityAdapter"/> over a fake JNI bridge. Pins, for
    /// each mid-session change: no provider read while it cannot succeed, the Activity screen's
    /// player text and next step, navigation never trapped, earned progress never lost, and
    /// exactly-once credit after recovery. Unity-editor-tested; real Health Connect UNVERIFIED (KI-002).
    /// </summary>
    public sealed class ActivityConnectionLifecycleTests
    {
        private sealed class Bridge : IHealthConnectBridge
        {
            public string Sdk = "available", Permission = "granted", Response = "ok";
            public Exception? Throw;
            public int Reads;
            public string GetSdkStatus() => Sdk;
            public string GetPermissionStatus() => Permission;
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
            _dir = Path.Combine(Path.GetTempPath(), "wg-connlife-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir);
            _bridge = new Bridge(); _connectRequests = 0;
        }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private static string Walk(string id, int daysAgo, int steps)
        {
            var end = DateTimeOffset.UtcNow.AddDays(-daysAgo);
            return id + "\t" + end.AddHours(-1).ToUnixTimeMilliseconds() + "\t" + end.ToUnixTimeMilliseconds() + "\t" + steps + "\t" + end.ToUnixTimeMilliseconds();
        }

        private ShellController Launch()
        {
            var adapter = new HealthConnectActivityAdapter(_bridge, () => DateTimeOffset.UtcNow);
            _session = ShellBootstrap.CreateSession(_dir, adapter);
            // Frequency is D-185's concern (LiveSyncThrottleTests); here every resume may read.
            return new ShellController(_session, () => 42UL, devTools: false, adapter, () => _connectRequests++) { MinLiveSyncInterval = TimeSpan.Zero };
        }

        /// <summary>A started game that has been credited one walk through Health Connect.</summary>
        private ShellController Connected()
        {
            var c = Launch(); c.Boot(); c.Invoke("start");
            for (int i = 0; i < 10 && c.Current == ShellScreen.Guide; i++)
            {
                var m = c.Render();
                var next = m.Actions.FirstOrDefault(a => a.Id == "guide:next") ?? m.Actions.First(a => a.Id.StartsWith("queue:"));
                c.Invoke(next.Id);
            }
            _bridge.Response = "ok\n" + Walk("hc-1", 1, 6000);
            c.Resume();
            Assume.That(Credited(), Is.GreaterThan(0), "fixture: the first walk is credited");
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

        [Test] public void Revoking_mid_session_stops_reads_explains_settings_and_keeps_progress()
        {
            var c = Connected();
            long earned = Credited();
            int reads = _bridge.Reads;

            _bridge.Permission = "revoked";
            for (int i = 0; i < 3; i++) c.Resume();

            Assert.AreEqual(reads, _bridge.Reads, "no provider read while access is off");
            Assert.AreEqual(earned, Credited(), "revocation never takes earned progress back");
            Assert.AreEqual(ActivityPlayerStatus.PermissionDenied, _session.GetActivityStatus().Status);
            var m = ActivityScreen(c);
            Assert.Contains(PlayerStrings.T("activity.permissionDenied"), m.Lines);
            Assert.Contains(PlayerStrings.T("activity.action.openSettings"), m.Lines);
            Assert.IsFalse(m.Actions.Any(a => a.Id == "activity:connect"), "a revoked grant is restored in Settings, not by re-asking");
            AssertPlayable(c);
        }

        [Test] public void Re_granting_reads_again_without_re_crediting_old_walks()
        {
            var c = Connected();
            long earned = Credited();
            _bridge.Permission = "revoked"; c.Resume();

            _bridge.Permission = "granted";
            int reads = _bridge.Reads;
            c.Resume();
            Assert.AreEqual(reads + 1, _bridge.Reads, "a fresh grant reads at once");
            Assert.AreEqual(earned, Credited(), "the same walk re-delivered after re-grant credits nothing");

            _bridge.Response = "ok\n" + Walk("hc-1", 1, 6000) + "\n" + Walk("hc-2", 0, 3000);
            c.Resume(); c.Resume();
            long afterNew = Credited();
            Assert.Greater(afterNew, earned, "a new walk after re-grant counts");
            c.Resume();
            Assert.AreEqual(afterNew, Credited(), "and counts exactly once");
            Assert.AreEqual(ActivityPlayerStatus.ConnectedCurrent, _session.GetActivityStatus().Status);
            Assert.Contains(PlayerStrings.T("activity.connected"), ActivityScreen(c).Lines);
        }

        [Test] public void A_read_refused_mid_read_is_a_calm_refresh_failure_that_retries()
        {
            var c = Connected();
            long earned = Credited();
            _bridge.Response = "error:permission"; // permission check passed, the read itself was refused
            Assert.DoesNotThrow(() => c.Resume());
            Assert.AreEqual(earned, Credited());
            Assert.AreEqual(ActivityPlayerStatus.RefreshTemporarilyFailed, _session.GetActivityStatus().Status);
            var m = ActivityScreen(c);
            Assert.Contains(PlayerStrings.T("activity.refreshFailed"), m.Lines);
            Assert.Contains(PlayerStrings.T("activity.action.retryLater"), m.Lines);

            int reads = _bridge.Reads;
            _bridge.Response = "ok\n" + Walk("hc-1", 1, 6000);
            c.Resume();
            Assert.AreEqual(reads + 1, _bridge.Reads, "the next resume retries");
            Assert.AreEqual(earned, Credited());
            Assert.AreEqual(ActivityPlayerStatus.ConnectedCurrent, _session.GetActivityStatus().Status);
        }

        [Test] public void A_crashing_bridge_mid_session_is_contained_and_keeps_progress()
        {
            var c = Connected();
            long earned = Credited();
            _bridge.Throw = new Exception("simulated JNI crash");
            Assert.DoesNotThrow(() => c.Resume());
            Assert.AreEqual(earned, Credited());
            Assert.AreEqual(ActivityPlayerStatus.RefreshTemporarilyFailed, _session.GetActivityStatus().Status);
            Assert.Contains(PlayerStrings.T("activity.refreshFailed"), ActivityScreen(c).Lines);
            AssertPlayable(c);
        }

        [Test] public void Health_Connect_removed_mid_session_shows_unavailable_without_a_dead_end()
        {
            var c = Connected();
            long earned = Credited();
            int reads = _bridge.Reads;
            _bridge.Sdk = "unsupported";
            c.Resume(); c.Resume();
            Assert.AreEqual(reads, _bridge.Reads, "no read from a provider that is not installed");
            Assert.AreEqual(earned, Credited());
            Assert.AreEqual(ActivityPlayerStatus.SourceUnavailable, _session.GetActivityStatus().Status);
            var m = ActivityScreen(c);
            Assert.Contains(PlayerStrings.T("activity.sourceUnavailable"), m.Lines);
            Assert.IsFalse(m.Actions.Any(a => a.Id == "activity:connect"), "no Connect for a provider that cannot exist");
            AssertPlayable(c);
        }

        [Test] public void Provider_update_required_mid_session_asks_for_the_update_and_does_not_read()
        {
            var c = Connected();
            long earned = Credited();
            int reads = _bridge.Reads;
            _bridge.Sdk = "update_required";
            c.Resume();
            Assert.AreEqual(reads, _bridge.Reads);
            Assert.AreEqual(earned, Credited());
            Assert.AreEqual(ActivityPlayerStatus.ProviderUpdateRequired, _session.GetActivityStatus().Status);
            var m = ActivityScreen(c);
            Assert.Contains(PlayerStrings.T("activity.providerUpdate"), m.Lines);
            Assert.Contains(PlayerStrings.T("activity.action.updateProvider"), m.Lines);
            Assert.IsFalse(m.Actions.Any(a => a.Id == "activity:connect"));

            _bridge.Sdk = "available";
            c.Resume();
            Assert.AreEqual(reads + 1, _bridge.Reads, "reads resume once the provider is updated");
            Assert.AreEqual(earned, Credited(), "and nothing is credited twice");
        }

        [Test] public void An_unreadable_provider_status_mid_session_never_reads_or_loses_progress()
        {
            // The projected status for this case is an open question (KI-030); only the safety half is pinned.
            var c = Connected();
            long earned = Credited();
            int reads = _bridge.Reads;
            _bridge.Sdk = "status-garbled";
            Assert.DoesNotThrow(() => c.Resume());
            Assert.AreEqual(reads, _bridge.Reads);
            Assert.AreEqual(earned, Credited());
            ActivityScreen(c);
            AssertPlayable(c);
        }

        [Test] public void A_revocation_survives_a_restart_and_the_new_process_does_not_read()
        {
            Connected();
            long earned = Credited();
            _bridge.Permission = "revoked";
            int reads = _bridge.Reads;
            var c2 = Launch(); c2.Boot();
            Assert.AreEqual(StartStatus.Loaded, c2.LastStartStatus);
            Assert.AreEqual(reads, _bridge.Reads);
            Assert.AreEqual(earned, Credited(), "the reloaded save keeps every credited walk");
            Assert.Contains(PlayerStrings.T("activity.permissionDenied"), ActivityScreen(c2).Lines);
        }

        [Test] public void Revoked_while_away_and_restored_before_return_is_just_a_normal_read()
        {
            var c = Connected();
            long earned = Credited();
            _bridge.Permission = "revoked";
            _bridge.Permission = "granted"; // changed and changed back while the app was paused
            int reads = _bridge.Reads;
            c.Resume();
            Assert.AreEqual(reads + 1, _bridge.Reads);
            Assert.AreEqual(earned, Credited());
            Assert.AreEqual(0, _connectRequests, "nothing asked the player to reconnect");
        }
    }
}
