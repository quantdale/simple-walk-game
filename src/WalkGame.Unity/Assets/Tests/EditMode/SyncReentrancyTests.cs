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
    /// D-128: concurrency and re-entrancy at the shell sync pipeline, and the Unity-to-domain
    /// exception boundary. Unity-editor-tested; device UNVERIFIED (KI-002).
    /// </summary>
    public sealed class SyncReentrancyTests
    {
        private sealed class GrantedPort : IActivityConnectionPort
        {
            public ActivityConnectionSnapshot SnapshotConnection() =>
                new ActivityConnectionSnapshot(ActivityPermissionState.Granted, ActivitySourceAvailability.Available);
        }

        /// <summary>A provider whose fetch calls back into the shell (an OS resume or a tap delivered mid-read).</summary>
        private sealed class CallbackSource : IActivityRecordSource
        {
            public int Fetches;
            public Action? DuringFetch;
            public Exception? Throw;
            public string ProviderNamespace => "test.reentry";
            public IReadOnlyList<NormalizedActivityRecord> FetchRecords(DateTimeOffset s, DateTimeOffset e)
            {
                Fetches++;
                var cb = DuringFetch; DuringFetch = null;
                cb?.Invoke();
                if (Throw != null) throw Throw;
                return Array.Empty<NormalizedActivityRecord>();
            }
        }

        private string _dir = "";
        private CallbackSource _source = new CallbackSource();
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-reentry-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); _source = new CallbackSource(); }
        [TearDown] public void TearDown()
        {
            try { File.SetAttributes(_dir, FileAttributes.Normal); } catch (Exception) { }
            Chmod("755");
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private void Chmod(string mode)
        {
            try { System.Diagnostics.Process.Start("chmod", mode + " \"" + _dir + "\"")?.WaitForExit(); } catch (Exception) { }
        }

        // D-185: this suite pins re-entrancy (one coalesced re-run, never nested), not read
        // frequency, so the 30 s skip window is off here; LiveSyncThrottleTests covers it.
        private ShellController Launch() => new ShellController(ShellBootstrap.CreateSession(_dir, new GrantedPort()), () => 42UL, devTools: true, _source) { MinLiveSyncInterval = TimeSpan.Zero };

        /// <summary>A loaded world: Home, or the return summary a boot can lead to.</summary>
        private static void AssertOpen(ShellController c) =>
            Assert.That(c.Current, Is.EqualTo(ShellScreen.Home).Or.EqualTo(ShellScreen.ReturnSummary));

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

        [Test] public void Resume_during_a_boot_sync_is_coalesced_into_one_rerun()
        {
            var c = Started();
            var relaunched = Launch();
            int resumes = 0;
            _source.Fetches = 0;
            _source.DuringFetch = () => { resumes++; relaunched.Resume(); relaunched.Resume(); relaunched.Resume(); };
            relaunched.Boot();
            Assert.AreEqual(1, resumes, "callback ran once");
            Assert.AreEqual(2, _source.Fetches, "boot fetch + exactly one coalesced resume fetch, never nested");
            Assert.IsFalse(relaunched.IsBusy);
            AssertOpen(relaunched);
        }

        [Test] public void Double_tap_during_a_running_action_is_dropped()
        {
            var c = Started();
            int nested = 0;
            _source.DuringFetch = () => { nested++; c.Invoke("delete:confirm"); };
            c.Resume(); // the resume's fetch receives the stray tap
            Assert.AreEqual(1, nested);
            AssertOpen(c); // the stray delete tap never ran
            Assert.IsTrue(File.Exists(Path.Combine(_dir, "save.json")));
        }

        [Test] public void Double_tapped_start_on_a_project_is_a_no_op_not_an_error()
        {
            // D-137: the Guide queues the only open project, so after Started() Projects has no
            // "queue:" action and the old Assume made this test Inconclusive (Unity then exits 2,
            // failing the clean-clone EditMode lane). Double-tap the Guide's own Start instead.
            var c = Launch(); c.Boot(); c.Invoke("start");
            ShellAction? q = null;
            for (int i = 0; i < 10 && q == null; i++)
            {
                var m = c.Render();
                q = m.Actions.FirstOrDefault(a => a.Id.StartsWith("queue:"));
                if (q == null) c.Invoke(m.Actions.First(a => a.Id == "guide:next").Id);
            }
            Assert.IsNotNull(q, "the Guide offers a project to start");
            c.Invoke(q!.Id);
            Assert.AreNotEqual(ScreenState.Error, c.Render().State);
            c.Invoke(q.Id);
            Assert.AreNotEqual(ScreenState.Error, c.Render().State, "a second tap on the same Start is calm");
        }

        [Test] public void A_bridge_exception_of_any_type_never_aborts_the_boot()
        {
            Started();
            _source.Throw = new FormatException("jni-ish failure");
            var c = Launch();
            Assert.DoesNotThrow(() => c.Boot());
            AssertOpen(c);
            Assert.IsFalse(c.IsBusy);
        }

        [Test] public void A_save_that_cannot_be_written_is_a_calm_error_not_an_exception()
        {
            if (Environment.OSVersion.Platform != PlatformID.Unix) Assert.Ignore("needs POSIX permissions");
            var c = Started();
            var before = File.ReadAllBytes(Path.Combine(_dir, "save.json"));
            Chmod("555");
            try
            {
                Assert.DoesNotThrow(() => c.Invoke("dev:walk"));
                Assert.AreEqual(ScreenState.Error, c.Render().State);
                // D-131: the walk was credited in memory but not committed, so the notice says so calmly.
                Assert.AreEqual(PlayerStrings.T("shell.notSavedYet"), c.Render().Notice);
                Assert.IsFalse(c.IsBusy, "a failure never leaves the shell locked");
            }
            finally { Chmod("755"); }
            CollectionAssert.AreEqual(before, File.ReadAllBytes(Path.Combine(_dir, "save.json")), "the committed save is untouched");
            Assert.DoesNotThrow(() => c.Invoke("nav:Home"));
        }

        [Test] public void D131_unsaved_progress_is_retried_on_resume_and_lands_exactly_once()
        {
            if (Environment.OSVersion.Platform != PlatformID.Unix) Assert.Ignore("needs POSIX permissions");
            var c = Started();
            var before = File.ReadAllBytes(Path.Combine(_dir, "save.json"));
            Chmod("555");
            try
            {
                c.Invoke("dev:walk");
                Assert.AreEqual(PlayerStrings.T("shell.notSavedYet"), c.Render().Notice);
                c.Resume(); // storage still failing: keep the in-memory world, same calm notice, no reload
                Assert.AreEqual(PlayerStrings.T("shell.notSavedYet"), c.Render().Notice);
                Assert.IsFalse(c.IsBusy);
            }
            finally { Chmod("755"); }
            CollectionAssert.AreEqual(before, File.ReadAllBytes(Path.Combine(_dir, "save.json")));
            c.Resume(); // storage back: the retry commits the whole state once
            Assert.AreNotEqual(PlayerStrings.T("shell.notSavedYet"), c.Render().Notice);
            CollectionAssert.AreNotEqual(before, File.ReadAllBytes(Path.Combine(_dir, "save.json")), "the pending progress was committed");
            var a = ShellBootstrap.CreateSession(_dir, new GrantedPort()); a.Continue();
            var b = ShellBootstrap.CreateSession(_dir, new GrantedPort()); b.Continue();
            long credited = a.GetDiagnostics().LifetimeVitalityCredited;
            Assert.AreEqual(credited, b.GetDiagnostics().LifetimeVitalityCredited);
            Assert.Greater(credited, 0L, "the walk credited during the gap was not lost");
            c.Resume(); c.Resume(); // further resumes never re-credit the gap
            var d = ShellBootstrap.CreateSession(_dir, new GrantedPort()); d.Continue();
            Assert.AreEqual(credited, d.GetDiagnostics().LifetimeVitalityCredited, "landed exactly once");
        }

        [Test] public void D134_resumes_during_failing_saves_catch_up_in_memory_and_land_once()
        {
            if (Environment.OSVersion.Platform != PlatformID.Unix) Assert.Ignore("needs POSIX permissions");
            var c = Started();
            Chmod("555");
            try
            {
                c.Invoke("dev:walk");
                for (int i = 0; i < 3; i++)
                {
                    Assert.DoesNotThrow(() => c.Resume()); // D-134: offline catch-up runs in memory, no reload
                    Assert.AreEqual(PlayerStrings.T("shell.notSavedYet"), c.Render().Notice);
                    Assert.IsFalse(c.IsBusy);
                    AssertOpen(c);
                }
            }
            finally { Chmod("755"); }
            c.Resume();
            Assert.AreNotEqual(PlayerStrings.T("shell.notSavedYet"), c.Render().Notice);
            var a = ShellBootstrap.CreateSession(_dir, new GrantedPort()); a.Continue();
            long credited = a.GetDiagnostics().LifetimeVitalityCredited;
            Assert.Greater(credited, 0L, "the walk credited during the gap was not lost");
            c.Resume();
            var b = ShellBootstrap.CreateSession(_dir, new GrantedPort()); b.Continue();
            Assert.AreEqual(credited, b.GetDiagnostics().LifetimeVitalityCredited, "landed exactly once");
        }

        [Test] public void Pause_resume_storm_leaves_one_consistent_state()
        {
            var c = Started();
            for (int i = 0; i < 50; i++) { c.Resume(); c.Invoke("nav:Story"); c.Resume(); c.Invoke("nav:Home"); }
            Assert.IsFalse(c.IsBusy);
            Assert.AreEqual(ShellScreen.Home, c.Current);
            var again = Launch(); again.Boot();
            AssertOpen(again); // the save reloads cleanly after the storm
        }
    }
}
