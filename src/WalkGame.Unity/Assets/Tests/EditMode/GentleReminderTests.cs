using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>D-119 optional gentle reminder, with a fake scheduler. Unity-editor-tested; Android delivery UNVERIFIED (KI-002).</summary>
    public sealed class GentleReminderTests
    {
        private sealed class FakeScheduler : IReminderScheduler
        {
            public ReminderPermission Permission { get; set; } = ReminderPermission.NotGranted;
            public bool GrantOnRequest;
            public int Requests, Cancels;
            public readonly List<(int minutes, string title, string body)> Scheduled = new List<(int, string, string)>();
            public bool Active;
            public void RequestPermission() { Requests++; if (GrantOnRequest) Permission = ReminderPermission.Granted; }
            public bool ScheduleDaily(int minutesOfDay, string title, string body) { Scheduled.Add((minutesOfDay, title, body)); Active = true; return true; }
            public void Cancel() { Cancels++; Active = false; }
        }

        private string _dir = "";
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-remind-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private (ShellController c, GameSession s) Fresh(FakeScheduler? f)
        {
            var s = ShellBootstrap.CreateSession(_dir);
            var c = new ShellController(s, () => 42UL, devTools: true) { Reminders = f };
            c.Boot(); c.Invoke("start");
            c.Invoke("nav:Settings");
            return (c, s);
        }

        [Test] public void Off_by_default_and_never_scheduled_or_asked_without_opt_in()
        {
            var f = new FakeScheduler { Permission = ReminderPermission.Granted };
            var (c, s) = Fresh(f);
            c.Resume(); c.SyncReminder();
            Assert.IsFalse(s.GetSettings().DailyReminderEnabled);
            Assert.IsEmpty(f.Scheduled);
            Assert.AreEqual(0, f.Requests);
            c.Invoke("nav:Settings");
            var m = c.Render();
            Assert.Contains("Daily reminder is off. If you like, the game can send one gentle note a day.", m.Lines.ToList());
            Assert.IsTrue(m.Actions.Any(a => a.Id == "toggle:reminder" && a.Label == "Turn on a gentle daily reminder"));
        }

        [Test] public void Turning_on_asks_the_OS_and_schedules_only_once_permission_is_granted()
        {
            var f = new FakeScheduler { Permission = ReminderPermission.NotGranted };
            var (c, s) = Fresh(f);
            c.Invoke("toggle:reminder");
            Assert.AreEqual(1, f.Requests);
            Assert.IsEmpty(f.Scheduled, "nothing is scheduled without OS permission");
            Assert.IsTrue(c.Render().Lines.Any(l => l.Contains("notifications are not allowed")));

            f.Permission = ReminderPermission.Granted; // granted in the OS dialog; read again on resume
            c.Resume();
            Assert.AreEqual(1, f.Scheduled.Count);
            Assert.AreEqual(s.GetSettings().DailyReminderMinutesOfDay, f.Scheduled[0].minutes);
            Assert.AreEqual(ReminderCopy.Title, f.Scheduled[0].title);
            Assert.AreEqual(ReminderCopy.Body, f.Scheduled[0].body);
            c.Invoke("nav:Settings");
            Assert.Contains("Daily reminder is on, around 08:00. It is one quiet note, and you can turn it off at any time.", c.Render().Lines.ToList());
        }

        [Test] public void Turning_off_cancels_and_a_revoked_permission_cancels_on_resume()
        {
            var f = new FakeScheduler { GrantOnRequest = true };
            var (c, s) = Fresh(f);
            c.Invoke("toggle:reminder");
            Assert.IsTrue(f.Active);
            f.Permission = ReminderPermission.NotGranted; c.Resume();
            Assert.IsFalse(f.Active, "revoked in the OS: cancelled, no retry nagging");
            Assert.AreEqual(1, f.Requests, "the game does not ask again on its own");
            f.Permission = ReminderPermission.Granted; c.Resume();
            Assert.IsTrue(f.Active);
            c.Invoke("nav:Settings"); c.Invoke("toggle:reminder");
            Assert.IsFalse(f.Active);
            Assert.IsFalse(s.GetSettings().DailyReminderEnabled);
        }

        [Test] public void Without_a_scheduler_the_setting_says_not_available_and_offers_no_toggle()
        {
            var (c, _) = Fresh(null);
            var m = c.Render();
            Assert.Contains("Daily reminder: not available on this device.", m.Lines.ToList());
            Assert.IsFalse(m.Actions.Any(a => a.Id == "toggle:reminder"));
        }

        [Test] public void Reminder_copy_has_no_streak_guilt_or_pressure_language()
        {
            var lines = new[] { ReminderCopy.Title, ReminderCopy.Body,
                ShellController.ReminderLine(null, ReminderPermission.NotGranted, 480),
                ShellController.ReminderLine(false, ReminderPermission.NotGranted, 480),
                ShellController.ReminderLine(true, ReminderPermission.NotGranted, 480),
                ShellController.ReminderLine(true, ReminderPermission.Granted, 480) };
            foreach (var l in lines)
                foreach (var bad in new[] { "streak", "missed", "don't forget", "falling behind", "decay", "expire", "lose", "lost", "hurry", "penalty", "days in a row", "come back" })
                    StringAssert.DoesNotContain(bad, l.ToLowerInvariant());
        }
    }
}
