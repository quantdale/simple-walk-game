using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using WalkGame.Infrastructure.Platform.HealthConnect;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>
    /// D-107 battery / background-sync budget (PERFORMANCE_BUDGETS §12–§14). Headless, counted with a
    /// fake Health Connect bridge: the shell reads walking data only on boot and on return to the
    /// foreground, never while the player navigates, never on a timer, and over a bounded window.
    /// Device battery drain stays UNVERIFIED (KI-002).
    /// </summary>
    public sealed class BackgroundSyncBudgetTests
    {
        private string _dir = "";
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-sync-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private sealed class CountingBridge : IHealthConnectBridge
        {
            public int Sdk, Permission, Reads;
            public readonly List<(long Start, long End)> Windows = new List<(long, long)>();
            public string GetSdkStatus() { Sdk++; return "available"; }
            public string GetPermissionStatus() { Permission++; return "granted"; }
            public string ReadSteps(long s, long e) { Reads++; Windows.Add((s, e)); return "ok"; }
        }

        private ShellController Controller(CountingBridge b)
        {
            var adapter = new HealthConnectActivityAdapter(b, () => DateTimeOffset.UtcNow);
            return new ShellController(ShellBootstrap.CreateSession(_dir, adapter), () => 42UL, devTools: false, adapter, () => { });
        }

        private static ShellController Booted(Func<ShellController> make)
        {
            var first = make(); first.Boot(); first.Invoke("start");
            for (int i = 0; i < 12 && first.Current == ShellScreen.Guide; i++)
            {
                var m = first.Render();
                var q = m.Actions.FirstOrDefault(a => a.Id.StartsWith("queue:"));
                first.Invoke(m.Actions.Any(a => a.Id == "guide:next") ? "guide:next" : q?.Id ?? m.Actions[0].Id);
            }
            var c = make(); c.Boot();
            return c;
        }

        private static readonly string[] Tour = { "Home", "Projects", "Region", "Story", "Discoveries", "Progress", "Settings", "ActivityStatus", "ReturnSummary" };

        [Test] public void Navigation_and_rendering_never_read_walking_data()
        {
            var b = new CountingBridge();
            var c = Booted(() => Controller(b));
            int readsAfterBoot = b.Reads;
            Assert.That(readsAfterBoot, Is.GreaterThan(0), "a booted, granted shell syncs once");
            for (int i = 0; i < 100; i++) { c.Invoke("nav:" + Tour[i % Tour.Length]); c.Render(); }
            Assert.AreEqual(readsAfterBoot, b.Reads, "100 navigations must not query the provider");
        }

        [Test] public void Each_return_to_the_foreground_reads_exactly_once()
        {
            var b = new CountingBridge();
            var c = Booted(() => Controller(b));
            int before = b.Reads;
            for (int i = 0; i < 5; i++) c.Resume();
            Assert.AreEqual(before + 5, b.Reads, "one bounded read per resume, no retries or loops");
        }

        [Test] public void Every_read_window_is_bounded_to_the_sync_window()
        {
            var b = new CountingBridge();
            var c = Booted(() => Controller(b));
            c.Resume();
            long maxMs = (long)TimeSpan.FromDays(ShellController.SyncWindowDays).TotalMilliseconds + 60_000;
            Assert.IsNotEmpty(b.Windows);
            foreach (var w in b.Windows)
            {
                Assert.That(w.End, Is.GreaterThan(w.Start));
                Assert.That(w.End - w.Start, Is.LessThanOrEqualTo(maxMs), "read window exceeds SyncWindowDays");
            }
        }

        [Test] public void Status_checks_per_screen_render_are_bounded()
        {
            var b = new CountingBridge();
            var c = Booted(() => Controller(b));
            int sdk0 = b.Sdk, perm0 = b.Permission;
            const int renders = 90;
            for (int i = 0; i < renders; i++) { c.Invoke("nav:" + Tour[i % Tour.Length]); c.Render(); }
            double perRender = (double)(b.Sdk - sdk0 + b.Permission - perm0) / renders;
            UnityEngine.Debug.Log("WG_SYNC_BUDGET status-calls-per-render=" + perRender.ToString("0.00"));
            // Status calls are cheap local binder queries, not data reads; the budget caps them so a
            // future change cannot turn every redraw into a burst of platform calls.
            Assert.That(perRender, Is.LessThanOrEqualTo(8.0));
        }

        [Test] public void Shell_source_has_no_polling_or_timer_wakeups()
        {
            var shell = Path.Combine(UnityEngine.Application.dataPath, "Shell");
            var banned = new Regex(@"\bvoid\s+(Update|FixedUpdate|LateUpdate)\s*\(|InvokeRepeating|new\s+System\.Threading\.Timer|System\.Timers\.Timer|while\s*\(\s*true\s*\)");
            foreach (var file in Directory.GetFiles(shell, "*.cs"))
            {
                var src = File.ReadAllText(file);
                Assert.IsFalse(banned.IsMatch(src), Path.GetFileName(file) + " adds a polling loop or timer wake-up");
            }
            // WaitForSeconds is allowed only in the development-only emulator autodrive.
            var boot = File.ReadAllText(Path.Combine(shell, "ShellBootstrap.cs"));
            Assert.AreEqual(1, Regex.Matches(boot, "WaitForSeconds").Count);
            StringAssert.Contains("Debug.isDebugBuild && System.IO.File.Exists", boot);
            foreach (var file in Directory.GetFiles(shell, "*.cs").Where(f => !f.EndsWith("ShellBootstrap.cs")))
                Assert.IsFalse(File.ReadAllText(file).Contains("WaitForSeconds"), Path.GetFileName(file));
        }
    }
}
