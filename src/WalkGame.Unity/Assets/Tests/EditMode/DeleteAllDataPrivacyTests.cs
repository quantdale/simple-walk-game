using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>
    /// D-194: export → delete-all → new world, end to end through the shell. Deleting all local data
    /// also withdraws the OS daily reminder (it was left scheduled before this fix), and the readable
    /// export carries no device paths. Unity-editor-tested; Android notification cancel on a device UNVERIFIED (KI-002).
    /// </summary>
    public sealed class DeleteAllDataPrivacyTests
    {
        private sealed class FakeScheduler : IReminderScheduler
        {
            public ReminderPermission Permission { get; set; } = ReminderPermission.Granted;
            public bool Active;
            public int Cancels;
            public bool ThrowOnCancel;
            public void RequestPermission() { }
            public bool ScheduleDaily(int minutesOfDay, string title, string body) { Active = true; return true; }
            public void Cancel() { Cancels++; if (ThrowOnCancel) throw new InvalidOperationException("os"); Active = false; }
        }

        private string _dir = "";
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-erase-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private (ShellController c, GameSession s) Launch(FakeScheduler f)
        {
            var s = ShellBootstrap.CreateSession(_dir);
            return (new ShellController(s, () => 42UL, devTools: true) { Reminders = f }, s);
        }

        private static void FinishOnboarding(ShellController c)
        {
            for (int i = 0; i < 12 && (c.Current == ShellScreen.Guide); i++)
            {
                var m = c.Render();
                var next = m.Actions.FirstOrDefault(a => a.Id == "guide:next") ?? m.Actions.FirstOrDefault(a => a.Id.StartsWith("queue:", StringComparison.Ordinal));
                if (next == null) break;
                c.Invoke(next.Id);
            }
        }

        private (ShellController c, GameSession s) StartedWithReminder(FakeScheduler f)
        {
            var (c, s) = Launch(f);
            c.Boot(); c.Invoke("start"); FinishOnboarding(c);
            c.Invoke("nav:Settings"); c.Invoke("toggle:reminder");
            Assert.IsTrue(f.Active, "precondition: the daily reminder is scheduled");
            return (c, s);
        }

        [Test] public void Delete_all_withdraws_the_scheduled_daily_reminder()
        {
            var f = new FakeScheduler();
            var (c, _) = StartedWithReminder(f);
            c.Invoke("nav:Settings"); c.Invoke("delete:request"); c.Invoke("delete:confirm");
            Assert.AreEqual(ShellScreen.Onboarding, c.Current);
            Assert.IsEmpty(Directory.GetFiles(_dir));
            Assert.IsFalse(f.Active, "no notification from a game whose data was deleted");
        }

        [Test] public void A_reminder_left_from_an_interrupted_delete_is_withdrawn_on_the_next_launch()
        {
            var f = new FakeScheduler();
            StartedWithReminder(f);
            // Killed after the files went but before the OS heard about it.
            foreach (var file in Directory.GetFiles(_dir)) File.Delete(file);
            Assert.IsTrue(f.Active);
            var (again, _) = Launch(f); again.Boot();
            Assert.AreEqual(ShellScreen.Onboarding, again.Current);
            Assert.IsFalse(f.Active, "a launch with no save keeps no reminder scheduled");
        }

        [Test] public void A_failing_cancel_never_blocks_the_delete()
        {
            var f = new FakeScheduler();
            var (c, _) = StartedWithReminder(f);
            f.ThrowOnCancel = true;
            c.Invoke("nav:Settings"); c.Invoke("delete:request");
            Assert.DoesNotThrow(() => c.Invoke("delete:confirm"));
            Assert.AreEqual(ShellScreen.Onboarding, c.Current);
            Assert.IsEmpty(Directory.GetFiles(_dir));
            Assert.AreEqual(ScreenState.Normal, c.Render().State);
        }

        [Test] public void A_failed_delete_keeps_the_reminder_the_player_still_has()
        {
            var f = new FakeScheduler();
            var s = new GameSession(new UnerasableStore(new WalkGame.Infrastructure.Persistence.AtomicFileSaveStore(_dir)),
                new WalkGame.Infrastructure.Persistence.SaveCodec(new WalkGame.Infrastructure.Persistence.MigrationRunner(WalkGame.Infrastructure.Persistence.DefaultMigrations.All)),
                new WalkGame.Infrastructure.Platform.SystemClock(), WalkGame.Application.Content.Region1Catalog.Create(),
                new WalkGame.Infrastructure.Persistence.LocalPreferencesStore(_dir), new ShellConnectionPort(false));
            var c = new ShellController(s, () => 42UL, devTools: true) { Reminders = f };
            c.Boot(); c.Invoke("start"); FinishOnboarding(c);
            c.Invoke("nav:Settings"); c.Invoke("toggle:reminder");
            Assert.IsTrue(f.Active);
            c.Invoke("delete:request"); c.Invoke("delete:confirm");
            Assert.AreEqual(ShellScreen.Settings, c.Current);
            Assert.AreEqual(ScreenState.Error, c.Render().State);
            Assert.IsTrue(f.Active, "nothing was deleted, so the player's reminder stays");
        }

        [Test] public void Export_then_delete_then_new_world_round_trip_leaves_no_trace_of_the_old_world()
        {
            var f = new FakeScheduler();
            var (c, s) = StartedWithReminder(f);
            c.Invoke("dev:walk");
            Assert.Greater(s.GetDiagnostics().LifetimeVitalityCredited, 0);
            var shared = new List<string>();
            c.ShareText = t => { shared.Add(t); return true; };
            c.Invoke("nav:Privacy"); c.Invoke("export");
            Assert.AreEqual(1, shared.Count);
            string oldExport = shared[0];
            StringAssert.DoesNotContain(_dir, oldExport, "the export carries no device paths");
            StringAssert.DoesNotContain(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), oldExport);
            StringAssert.DoesNotContain(Environment.MachineName, oldExport);

            c.Invoke("nav:Settings"); c.Invoke("delete:request"); c.Invoke("delete:confirm");
            Assert.IsEmpty(Directory.GetFiles(_dir));

            var (again, s2) = Launch(f); again.Boot();
            Assert.AreEqual(StartStatus.NoSaveFound, again.LastStartStatus);
            again.Invoke("start");
            Assert.AreEqual(0, s2.GetDiagnostics().LifetimeVitalityCredited, "the new world starts from nothing");
            Assert.IsFalse(s2.GetSettings().DailyReminderEnabled, "the old reminder choice did not survive");
            Assert.IsFalse(f.Active);
            again.ShareText = t => { shared.Add(t); return true; };
            FinishOnboarding(again);
            again.Invoke("nav:Privacy"); again.Invoke("export");
            Assert.AreEqual(2, shared.Count);
            Assert.AreNotEqual(oldExport, shared[1]);
        }

        /// <summary>A store that saves but cannot erase: delete-all fails closed (NotSupported).</summary>
        private sealed class UnerasableStore : WalkGame.Application.Persistence.ISaveStore
        {
            private readonly WalkGame.Application.Persistence.ISaveStore _inner;
            public UnerasableStore(WalkGame.Application.Persistence.ISaveStore inner) => _inner = inner;
            public WalkGame.Application.Persistence.SaveReadResult ReadPrimary() => _inner.ReadPrimary();
            public WalkGame.Application.Persistence.SaveReadResult ReadBackup() => _inner.ReadBackup();
            public void WriteAtomic(byte[] envelopeBytes) => _inner.WriteAtomic(envelopeBytes);
            public void WriteAtomicPreservingBackup(byte[] envelopeBytes) => _inner.WriteAtomicPreservingBackup(envelopeBytes);
        }
    }
}
