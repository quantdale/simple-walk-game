using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine.UIElements;
using WalkGame.Application;
using WalkGame.Application.Development;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Time;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>
    /// M8 Performance, editor half (D-074): the shell over a mature (365-day) save whose valley is
    /// fully restored, plus repeated screen enter/exit. Structural budgets are asserted; wall-times
    /// are LOGGED ("WG_PERF") as Unity-editor measurements, never gated (M8-H1 no-flaky-timing rule).
    /// Device launch/memory/thermal/battery stay UNVERIFIED (KI-002).
    /// </summary>
    public sealed class ShellPerformanceTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero);
        private const int MaxElementsPerScreen = 250;
        private static string _dir = "";
        private static ShellController? _mature;
        private static readonly string[] Screens = { "Home", "Projects", "Region", "Settings", "ActivityStatus", "ReturnSummary" };

        [OneTimeSetUp] public void BuildMatureSave()
        {
            _dir = Path.Combine(Path.GetTempPath(), "wg-perf-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            var sw = Stopwatch.StartNew();
            var first = ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(true), new ManualClock(T0));
            Assert.AreEqual(StartStatus.NewGameCreated, first.StartNewGame(42UL).Status);
            for (int d = 1; d <= 365; d++)
            {
                var s = ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(true), new ManualClock(T0.AddDays(d)));
                Assert.AreEqual(StartStatus.Loaded, s.Continue().Status);
                s.IngestFromSource(new SyntheticWalkingSource(d % 7 == 0 ? 2_000 : 14_000), T0.AddDays(d - 1), T0.AddDays(d));
                var home = s.GetHome();
                if (home.ActiveProjectId == null && home.Queued.Count == 0)
                    foreach (var p in s.GetProjects().Projects.Where(p => p.Status == ProjectStatus.Available))
                        if (s.EnqueueProject(p.ProjectId).IsSuccess) break;
            }
            UnityEngine.Debug.Log("WG_PERF mature_save_build_365_days_ms=" + sw.ElapsedMilliseconds
                + " save_bytes=" + Directory.GetFiles(_dir).Sum(f => new FileInfo(f).Length));
            var boot = Stopwatch.StartNew();
            var session = ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(true), new ManualClock(T0.AddDays(366)));
            _mature = new ShellController(session, () => 42UL, devTools: false);
            _mature.Boot();
            UnityEngine.Debug.Log("WG_PERF warm_boot_mature_ms=" + boot.ElapsedMilliseconds);
        }

        [OneTimeTearDown] public void Cleanup() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private static int CountElements(VisualElement e) { int n = 1; foreach (var c in e.Children()) n += CountElements(c); return n; }

        [Test] public void Mature_save_reaches_the_restored_end_state()
        {
            _mature!.Invoke("nav:Region");
            var m = _mature.Render();
            Assert.IsTrue(m.Scene!.Completed, "365 days of moderate walking restores the valley");
            StringAssert.Contains("fully restored", string.Join(" ", m.Lines));
        }

        [Test] public void Every_screen_on_a_mature_restored_save_stays_within_structural_budgets()
        {
            foreach (var screen in Screens)
            {
                _mature!.Invoke("nav:" + screen);
                var root = new VisualElement();
                var times = new List<double>();
                for (int i = 0; i < 20; i++)
                {
                    var sw = Stopwatch.StartNew();
                    ShellView.Render(root, _mature.Render(), _ => { });
                    times.Add(sw.Elapsed.TotalMilliseconds);
                }
                times.Sort();
                int elements = CountElements(root);
                var model = _mature.Render();
                UnityEngine.Debug.Log("WG_PERF screen=" + screen + " median_model_plus_render_ms=" + times[10].ToString("0.000")
                    + " elements=" + elements + " lines=" + model.Lines.Count);
                Assert.That(elements, Is.LessThanOrEqualTo(MaxElementsPerScreen), screen + " element budget");
                if (model.Scene != null) Assert.That(model.Scene.ShapeCount(), Is.LessThanOrEqualTo(RegionScene.MaxShapes));
            }
        }

        [Test] public void Repeated_screen_enter_exit_never_stacks_elements()
        {
            var root = new VisualElement();
            void Cycle() { foreach (var s in Screens) { _mature!.Invoke("nav:" + s); ShellView.Render(root, _mature.Render(), _ => { }); } }
            for (int i = 0; i < 20; i++) Cycle();                       // warm-up
            int elementsAfterWarmup = CountElements(root);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 500; i++) Cycle();                      // 3,000 screen enters/exits
            UnityEngine.Debug.Log("WG_PERF enter_exit_3000_ms=" + sw.Elapsed.TotalMilliseconds.ToString("0"));
            // Memory is measured in a LIVE panel (PlayMode ShellPanelMemoryTests): a panel-less EditMode
            // root retains ~450 B per render inside UI Toolkit, which a live panel does not (D-074).
            Assert.AreEqual(1, root.childCount, "each render replaces, never stacks");
            Assert.AreEqual(elementsAfterWarmup, CountElements(root));
        }
    
        private static readonly string[] AllGameScreens = { "Home", "Projects", "Region", "Settings", "ActivityStatus", "ReturnSummary", "Story", "Discoveries" };

        // Unity's Mono returns 0 from GC.GetAllocatedBytesForCurrentThread, so allocation is read as
        // the growth of GC.GetTotalMemory(false) per call; samples where a collection ran (negative
        // or collection count changed) are dropped and the median kept. Approximate, in bytes.
        private static long MedianBytes(System.Action act, int n)
        {
            var samples = new List<long>();
            for (int i = 0; i < n; i++)
            {
                int gc = GC.CollectionCount(0);
                long a = GC.GetTotalMemory(false);
                act();
                long b = GC.GetTotalMemory(false);
                if (GC.CollectionCount(0) == gc && b >= a) samples.Add(b - a);
            }
            if (samples.Count == 0) return -1;
            samples.Sort();
            return samples[samples.Count / 2];
        }

        /// <summary>
        /// Perf pass (D-088): approximate managed bytes allocated per screen switch on the mature
        /// save, split into the screen model (controller + read models) and the view build. Logged
        /// as WG_PERF; a loose ceiling catches order-of-magnitude regressions only.
        /// </summary>
        [Test] public void Screen_switch_allocations_are_measured_and_bounded()
        {
            var root = new VisualElement();
            foreach (var screen in AllGameScreens)
            {
                _mature!.Invoke("nav:" + screen);
                for (int i = 0; i < 5; i++) ShellView.Render(root, _mature.Render(), _ => { }); // warm-up / JIT
                long modelBytes = MedianBytes(() => _mature.Render(), 60);
                var model = _mature.Render();
                long viewBytes = MedianBytes(() => ShellView.Render(root, model, _ => { }), 60);
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < 200; i++) _mature.Invoke("nav:" + screen);
                double switchUs = sw.Elapsed.TotalMilliseconds * 1000.0 / 200;
                UnityEngine.Debug.Log("WG_PERF alloc screen=" + screen + " model_bytes~" + modelBytes + " view_bytes~" + viewBytes
                    + " nav_invoke_us=" + switchUs.ToString("0.0"));
                Assert.That(modelBytes, Is.LessThan(MaxModelBytesPerRender), screen + " model allocation ceiling");
                Assert.That(viewBytes, Is.LessThan(MaxViewBytesPerRender), screen + " view allocation ceiling");
            }
        }

        [Test] public void Pooled_line_labels_never_carry_stale_text_or_names_between_screens()
        {
            var root = new VisualElement();
            foreach (var screen in new[] { "Story", "ActivityStatus", "Discoveries", "Home", "Settings", "Story" })
            {
                _mature!.Invoke("nav:" + screen);
                var model = _mature.Render();
                ShellView.Render(root, model, _ => { });
                var shown = root.Query<Label>().ToList().Where(l => !(l is Button) && l.parent?.name != "actions"
                    && l.name != "title" && l.name != "notice" && l.name != "error" && l.name != "text-preview").Select(l => l.text).ToList();
                CollectionAssert.AreEqual(model.Lines, shown, screen);
                Assert.AreEqual(model.State == ScreenState.Empty ? 1 : 0, root.Query<Label>("empty-state").ToList().Count, screen);
            }
        }

        [Test] public void Pooled_buttons_match_each_screen_and_fire_the_current_action()
        {
            var root = new VisualElement();
            ShellView.ActionButton? firstSeen = null; bool reused = false;
            foreach (var screen in new[] { "Settings", "Home", "Story", "ActivityStatus", "Discoveries", "Settings" })
            {
                _mature!.Invoke("nav:" + screen);
                var model = _mature.Render();
                var fired = new List<string>();
                ShellView.Render(root, model, fired.Add);
                var buttons = root.Q("actions").Children().Cast<ShellView.ActionButton>().ToList();
                CollectionAssert.AreEqual(model.Actions.Select(a => a.Id), buttons.Select(b => b.name), screen);
                CollectionAssert.AreEqual(model.Actions.Select(a => a.Label), buttons.Select(b => b.text), screen);
                for (int i = 0; i < buttons.Count; i++)
                    Assert.AreEqual(i == 0 ? UnityEngine.Color.white : ShellView.Ink, buttons[i].style.color.value, screen + " primary styling");
                foreach (var b in buttons) b.Activate();
                CollectionAssert.AreEqual(model.Actions.Select(a => a.Id), fired, screen + " taps fire this render's ids");
                if (buttons.Count > 0)
                {
                    if (firstSeen != null && buttons.Contains(firstSeen)) reused = true;
                    firstSeen ??= buttons[0];
                }
            }
            Assert.IsTrue(reused, "a button instance is reused across screen switches");
        }

        private const long MaxModelBytesPerRender = 1_000_000;
        private const long MaxViewBytesPerRender = 1_000_000;
    }
}
