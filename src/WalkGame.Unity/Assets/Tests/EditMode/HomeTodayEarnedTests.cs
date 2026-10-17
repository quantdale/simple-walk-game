using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Time;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>
    /// D-156: Home says what today's walks earned, derived from credited records (schema unchanged).
    /// Shown only when above zero. Unity-editor-tested; device UNVERIFIED (KI-002).
    /// </summary>
    public sealed class HomeTodayEarnedTests
    {
        private const int Manila = 8 * 3600;
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 3, 10, 4, 0, 0, TimeSpan.Zero); // 12:00 Manila
        private string _dir = "";
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-today-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private (ShellController c, GameSession s) Fresh()
        {
            var s = ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(true), new ManualClock(Now));
            var c = new ShellController(s, () => 42UL, devTools: true) { LocalOffsetSeconds = () => Manila };
            c.Boot(); c.Invoke("start");
            return (c, s);
        }

        private static NormalizedActivityRecord Rec(string id, long qty, DateTimeOffset end, int? offset, int revision = 1, bool deletion = false) =>
            new NormalizedActivityRecord("p", id, ActivityCategory.Walking, ActivityUnits.Steps, qty, end.AddMinutes(-20), end, revision, deletion, offset);

        private static System.Collections.Generic.List<string> HomeLines(ShellController c) { c.Invoke("nav:Home"); return c.Render().Lines; }

        [Test] public void Todays_dated_walk_shows_the_line_after_the_unspent_balance()
        {
            var (c, s) = Fresh();
            s.IngestActivityBatch(new[] { Rec("a", 2000, Now.AddHours(-1), Manila) });
            if (s.GetPendingReturnSummary() != null) s.AcknowledgeReturnSummary();
            var lines = HomeLines(c);
            int i = lines.FindIndex(l => l.StartsWith("Unspent: ", StringComparison.Ordinal));
            Assert.GreaterOrEqual(i, 0, string.Join("\n", lines));
            Assert.AreEqual("Today: 20 Vitality earned from your walks.", lines[i + 1]);
        }

        [Test] public void Nothing_earned_today_shows_no_line()
        {
            var (c, s) = Fresh();
            s.IngestActivityBatch(new[] { Rec("y", 2000, Now.AddHours(-13), Manila) }); // 23:00 yesterday in Manila
            Assert.IsFalse(HomeLines(c).Any(l => l.StartsWith("Today: ", StringComparison.Ordinal)));
        }

        [Test] public void Undated_walks_show_no_line()
        {
            var (c, s) = Fresh();
            s.IngestActivityBatch(new[] { Rec("a", 2000, Now.AddHours(-1), null) });
            Assert.IsFalse(HomeLines(c).Any(l => l.StartsWith("Today: ", StringComparison.Ordinal)));
        }

        [Test] public void A_deleted_walk_takes_its_vitality_off_today()
        {
            var (c, s) = Fresh();
            s.IngestActivityBatch(new[] { Rec("a", 2000, Now.AddHours(-2), Manila), Rec("b", 1000, Now.AddHours(-1), Manila) });
            Assert.Contains("Today: 30 Vitality earned from your walks.", HomeLines(c));
            s.IngestActivityBatch(new[] { Rec("a", 0, Now.AddHours(-2), Manila, revision: 2, deletion: true) });
            var lines = HomeLines(c);
            Assert.IsTrue(lines.Contains("Today: 10 Vitality earned from your walks."), string.Join("\n", lines));
        }

        [Test] public void Same_instant_reads_as_a_different_day_under_another_device_offset()
        {
            var (c, s) = Fresh();
            s.IngestActivityBatch(new[] { Rec("a", 2000, Now.AddHours(-1), Manila) });
            c.LocalOffsetSeconds = () => -5 * 3600; // device now in UTC-5: still Mar 9 there
            Assert.IsFalse(HomeLines(c).Any(l => l.StartsWith("Today: ", StringComparison.Ordinal)));
        }
    }
}
