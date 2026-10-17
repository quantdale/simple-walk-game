using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Application.Activity;
using WalkGame.Application.Content;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Platform.HealthConnect;
using WalkGame.Infrastructure.Platform.HealthKit;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>
    /// D-196 (KI-031) through the shell: on HealthKit, after 3 quiet local days the Activity screen keeps
    /// "Connected" and adds one gentle hint line; it clears on the next sample; no notification and no
    /// progress effect; Health Connect never shows it. Real adapters over fake bridges, one manual clock.
    /// Unity-editor-tested; the Swift bridge and iOS composition are UNVERIFIED (KI-022, KI-002).
    /// </summary>
    public sealed class HealthKitQuietHintTests
    {
        private const int Manila = 8 * 3600;
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.FromHours(8)).ToUniversalTime();

        private sealed class Hk : IHealthKitBridge
        {
            public string Response = "ok";
            public string GetAvailability() => "available";
            public string GetRequestStatus() => "unnecessary";
            public string ReadSteps(long s, long e) => Response;
        }

        private sealed class Hc : IHealthConnectBridge
        {
            public string Response = "ok";
            public string GetSdkStatus() => "available";
            public string GetPermissionStatus() => "granted";
            public string ReadSteps(long s, long e) => Response;
        }

        private sealed class CountingScheduler : IReminderScheduler
        {
            public int Scheduled, Requests;
            public ReminderPermission Permission => ReminderPermission.Granted;
            public void RequestPermission() { Requests++; }
            public bool ScheduleDaily(int m, string t, string b) { Scheduled++; return true; }
            public void Cancel() { }
        }

        private string _dir = "";
        private ManualClock _clock = new ManualClock(T0);
        private GameSession _session = null!;
        private CountingScheduler _reminders = new CountingScheduler();

        [SetUp] public void SetUp()
        {
            PlayerStrings.Locale = ContentText.English;
            _dir = Path.Combine(Path.GetTempPath(), "wg-quiet-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir);
            _clock = new ManualClock(T0); _reminders = new CountingScheduler();
        }
        [TearDown] public void TearDown() { PlayerStrings.Locale = ContentText.English; if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private static string Line(string id, DateTimeOffset end, int steps, bool hc) =>
            id + "\t" + end.AddMinutes(-40).ToUnixTimeMilliseconds() + "\t" + end.ToUnixTimeMilliseconds() + "\t" + steps + "\t"
            + (hc ? end.ToUnixTimeMilliseconds().ToString() : Manila.ToString());

        private ShellController Launch(IActivityConnectionPort port, IActivityRecordSource source)
        {
            _session = ShellBootstrap.CreateSession(_dir, port, _clock);
            var c = new ShellController(_session, () => 42UL, devTools: false, source, () => { })
            {
                MinLiveSyncInterval = TimeSpan.Zero,
                UtcNow = () => _clock.UtcNow,
                LocalOffsetSeconds = () => Manila,
                Reminders = _reminders,
            };
            c.Boot(); c.Invoke("start");
            for (int i = 0; i < 10 && c.Current == ShellScreen.Guide; i++)
            {
                var m = c.Render();
                var next = m.Actions.FirstOrDefault(a => a.Id == "guide:next") ?? m.Actions.First(a => a.Id.StartsWith("queue:"));
                c.Invoke(next.Id);
            }
            return c;
        }

        private (ShellController c, Hk bridge) HealthKitWithOneWalk()
        {
            var b = new Hk();
            var adapter = new HealthKitActivityAdapter(b, () => _clock.UtcNow);
            var c = Launch(adapter, adapter);
            b.Response = "ok\n" + Line("hk-1", T0.AddHours(-1), 6000, hc: false);
            c.Resume();
            Assume.That(_session.GetDiagnostics().LifetimeVitalityCredited, Is.GreaterThan(0));
            b.Response = "ok"; // then nothing arrives (as when read access was turned off in iOS Settings)
            return (c, b);
        }

        private List<string> ActivityLines(ShellController c) { c.Invoke("nav:ActivityStatus"); return c.Render().Lines; }
        private static string Hint => PlayerStrings.T("activity.quietHint");
        private static DateTimeOffset ManilaAt(int day, int hour, int minute = 0) =>
            new DateTimeOffset(2026, 10, day, hour, minute, 0, TimeSpan.FromHours(8)).ToUniversalTime();

        [Test] public void Two_quiet_days_show_nothing_and_the_third_local_day_shows_one_gentle_line()
        {
            var (c, _) = HealthKitWithOneWalk();
            _clock.Set(ManilaAt(16, 23, 59)); c.Resume();
            CollectionAssert.DoesNotContain(ActivityLines(c), Hint);
            _clock.Set(ManilaAt(17, 0, 1)); c.Resume();
            var lines = ActivityLines(c);
            Assert.AreEqual(PlayerStrings.T("activity.connected"), lines[0], "the status stays Connected");
            Assert.AreEqual(Hint, lines[1]);
            Assert.AreEqual(1, lines.Count(l => l == Hint));
            Assert.AreEqual(ScreenState.Normal, c.Render().State);
            StringAssert.DoesNotContain("you", Hint.Split('.')[0].ToLowerInvariant(), "the first sentence never blames the player");
        }

        [Test] public void The_hint_never_nags_never_touches_progress_and_clears_on_the_next_sample()
        {
            var (c, b) = HealthKitWithOneWalk();
            long before = _session.GetDiagnostics().LifetimeVitalityCredited;
            for (int d = 1; d <= 6; d++) { _clock.Set(T0.AddDays(d)); c.Resume(); c.Invoke("nav:Home"); c.Render(); }
            Assert.AreEqual(before, _session.GetDiagnostics().LifetimeVitalityCredited);
            Assert.AreEqual(0, _reminders.Scheduled, "no notification is scheduled for it");
            Assert.AreEqual(0, _reminders.Requests);
            c.Invoke("nav:Home");
            Assert.IsFalse(c.Render().Lines.Contains(Hint), "only on the Activity screen");
            CollectionAssert.Contains(ActivityLines(c), Hint);

            b.Response = "ok\n" + Line("hk-2", _clock.UtcNow.AddHours(-1), 4000, hc: false);
            c.Resume();
            Assert.Greater(_session.GetDiagnostics().LifetimeVitalityCredited, before);
            CollectionAssert.DoesNotContain(ActivityLines(c), Hint);
        }

        [Test] public void The_local_day_boundary_follows_the_phone_zone()
        {
            var (c, _) = HealthKitWithOneWalk(); // arrived 14th 02:00 UTC = 14th 10:00 Manila = 13th 21:00 New York
            _clock.Set(new DateTimeOffset(2026, 10, 16, 6, 0, 0, TimeSpan.Zero)); c.Resume(); // 16th 14:00 Manila; 16th 01:00 New York
            CollectionAssert.DoesNotContain(ActivityLines(c), Hint, "Manila: 16 - 14 = 2 local days");
            c.LocalOffsetSeconds = () => -5 * 3600;
            CollectionAssert.Contains(ActivityLines(c), Hint, "New York: 16 - 13 = 3 local days");
            c.LocalOffsetSeconds = () => Manila;
            CollectionAssert.DoesNotContain(ActivityLines(c), Hint);
        }

        [Test] public void The_hint_follows_the_locale()
        {
            var (c, _) = HealthKitWithOneWalk();
            _clock.Set(T0.AddDays(4)); c.Resume();
            var key = ContentText.Key(PlayerStrings.OverlayId("activity.quietHint"), PlayerStrings.Field);
            PlayerStrings.Locale = ContentText.ForLocale("xx", new Dictionary<string, string> { [key] = "[xx] quiet" });
            CollectionAssert.Contains(ActivityLines(c), "[xx] quiet");
        }

        [Test] public void Health_connect_never_shows_the_hint()
        {
            var b = new Hc();
            var adapter = new HealthConnectActivityAdapter(b, () => _clock.UtcNow);
            var c = Launch(adapter, adapter);
            b.Response = "ok\n" + Line("hc-1", T0.AddHours(-1), 6000, hc: true);
            c.Resume();
            Assume.That(_session.GetDiagnostics().LifetimeVitalityCredited, Is.GreaterThan(0));
            b.Response = "ok";
            _clock.Set(T0.AddDays(10)); c.Resume();
            var lines = ActivityLines(c);
            Assert.AreEqual(PlayerStrings.T("activity.connected"), lines[0]);
            CollectionAssert.DoesNotContain(lines, Hint);
        }
    }
}
