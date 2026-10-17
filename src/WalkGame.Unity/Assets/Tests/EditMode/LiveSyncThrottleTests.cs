using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Application.Activity;
using WalkGame.Domain.Activity;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>
    /// D-185 (amends D-107): within <see cref="ShellController.MinLiveSyncInterval"/> (30 s) of the
    /// last SUCCESSFUL provider read, a foreground resume skips the read; the reload and UI still
    /// run. Cold start, a fresh grant, any connection change and a failed or partial last read
    /// always read. Unity-editor-tested; device battery UNVERIFIED (KI-002).
    /// </summary>
    public sealed class LiveSyncThrottleTests
    {
        private sealed class MutablePort : IActivityConnectionPort
        {
            public ActivityPermissionState Permission = ActivityPermissionState.Granted;
            public ActivitySourceAvailability Availability = ActivitySourceAvailability.Available;
            public ActivityConnectionSnapshot SnapshotConnection() => new ActivityConnectionSnapshot(Permission, Availability);
        }

        private sealed class CountingSource : IActivityRecordSource
        {
            public int Fetches;
            public Exception? Throw;
            public string ProviderNamespace => "test.throttle";
            public IReadOnlyList<NormalizedActivityRecord> FetchRecords(DateTimeOffset s, DateTimeOffset e)
            {
                Fetches++;
                if (Throw != null) throw Throw;
                return Array.Empty<NormalizedActivityRecord>();
            }
        }

        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 10, 17, 2, 0, 0, TimeSpan.Zero);
        private string _dir = "";
        private MutablePort _port = new MutablePort();
        private CountingSource _source = new CountingSource();
        private DateTimeOffset _now = T0;

        [SetUp] public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "wg-throttle-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir);
            _port = new MutablePort(); _source = new CountingSource(); _now = T0;
        }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private ShellController Launch()
        {
            var c = new ShellController(ShellBootstrap.CreateSession(_dir, _port), () => 42UL, devTools: true, _source);
            c.UtcNow = () => _now;
            return c;
        }

        /// <summary>A started game whose last read succeeded at <see cref="_now"/>.</summary>
        private ShellController Started()
        {
            var c = Launch(); c.Boot(); c.Invoke("start");
            for (int i = 0; i < 10 && c.Current == ShellScreen.Guide; i++)
            {
                var m = c.Render();
                var next = m.Actions.FirstOrDefault(a => a.Id == "guide:next") ?? m.Actions.First(a => a.Id.StartsWith("queue:"));
                c.Invoke(next.Id);
            }
            _now = _now.AddMinutes(10);
            int before = _source.Fetches;
            c.Resume();
            Assert.AreEqual(before + 1, _source.Fetches, "fixture: a resume long after any read reads");
            return c;
        }

        private static void AssertOpen(ShellController c)
        {
            Assert.AreEqual(StartStatus.Loaded, c.LastStartStatus, "the reload still ran");
            Assert.That(c.Current, Is.EqualTo(ShellScreen.Home).Or.EqualTo(ShellScreen.ReturnSummary));
        }

        [Test] public void A_burst_of_resumes_inside_the_window_makes_one_read()
        {
            var c = Started();
            int before = _source.Fetches;
            _now = _now.AddMinutes(1);
            for (int i = 0; i < 20; i++) { c.Resume(); _now = _now.AddSeconds(1); }
            Assert.AreEqual(before + 1, _source.Fetches, "20 resumes in 20 s: the first reads, the rest skip");
            AssertOpen(c);
        }

        [Test] public void The_first_resume_after_the_window_reads_again()
        {
            var c = Started();
            int before = _source.Fetches;
            _now = _now.AddSeconds(29); c.Resume();
            Assert.AreEqual(before, _source.Fetches);
            _now = _now.AddSeconds(1); c.Resume(); // exactly 30 s after the last good read
            Assert.AreEqual(before + 1, _source.Fetches);
        }

        [Test] public void A_cold_start_always_reads()
        {
            Started();
            int before = _source.Fetches;
            Launch().Boot();
            Assert.AreEqual(before + 1, _source.Fetches);
        }

        [Test] public void A_fresh_grant_is_read_at_once()
        {
            var c = Started();
            _port.Permission = ActivityPermissionState.Denied;
            _now = _now.AddSeconds(2); c.Resume();
            int before = _source.Fetches;
            _port.Permission = ActivityPermissionState.Granted;
            _now = _now.AddSeconds(2); c.Resume();
            Assert.AreEqual(before + 1, _source.Fetches);
        }

        [Test] public void A_permission_change_while_granted_reads_at_once()
        {
            var c = Started();
            int before = _source.Fetches;
            _port.Permission = ActivityPermissionState.PartiallyGranted;
            _now = _now.AddSeconds(2); c.Resume();
            Assert.AreEqual(before + 1, _source.Fetches, "granted -> partially granted");
            _port.Permission = ActivityPermissionState.Granted;
            _now = _now.AddSeconds(2); c.Resume();
            Assert.AreEqual(before + 2, _source.Fetches, "partially granted -> granted");
        }

        [Test] public void A_partial_read_never_opens_the_window()
        {
            var c = Started();
            _port.Permission = ActivityPermissionState.PartiallyGranted;
            int before = _source.Fetches;
            for (int i = 0; i < 3; i++) { _now = _now.AddSeconds(2); c.Resume(); }
            Assert.AreEqual(before + 3, _source.Fetches);
        }

        [Test] public void A_failed_read_is_retried_on_the_next_resume()
        {
            var c = Started();
            _now = _now.AddMinutes(1);
            _source.Throw = new IOException("provider busy");
            int before = _source.Fetches;
            c.Resume();
            Assert.AreEqual(before + 1, _source.Fetches);
            _source.Throw = null;
            _now = _now.AddSeconds(2); c.Resume();
            Assert.AreEqual(before + 2, _source.Fetches, "a failed last read always reads");
            _now = _now.AddSeconds(2); c.Resume();
            Assert.AreEqual(before + 2, _source.Fetches, "after a good read the window applies again");
        }

        [Test] public void A_clock_set_backward_always_reads()
        {
            var c = Started();
            int before = _source.Fetches;
            _now = _now.AddHours(-3); c.Resume();
            Assert.AreEqual(before + 1, _source.Fetches);
        }

        [Test] public void A_zero_interval_reads_on_every_resume()
        {
            var c = Started();
            c.MinLiveSyncInterval = TimeSpan.Zero;
            int before = _source.Fetches;
            c.Resume(); c.Resume();
            Assert.AreEqual(before + 2, _source.Fetches);
        }
    }
}
