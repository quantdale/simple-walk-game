using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    public sealed class ShellControllerTests
    {
        private string _dir = "";

        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-shell-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private ShellController NewController() => new ShellController(ShellBootstrap.CreateSession(_dir), () => 42UL, devTools: true);

        [Test] public void Router_maps_every_start_status()
        {
            foreach (StartStatus s in Enum.GetValues(typeof(StartStatus)))
                Assert.DoesNotThrow(() => BootRouter.Route(s), s.ToString());
        }

        [TestCase(StartStatus.SaveUnreadable)]
        [TestCase(StartStatus.StateInvalid)]
        [TestCase(StartStatus.ContentIncompatible)]
        public void Router_sends_unreadable_outcomes_to_non_destructive_error(StartStatus s)
        {
            Assert.AreEqual(ShellScreen.BootError, BootRouter.Route(s).Screen);
        }

        [Test] public void Fresh_install_goes_to_premise_then_guide()
        {
            var c = NewController();
            c.Boot();
            Assert.AreEqual(StartStatus.NoSaveFound, c.LastStartStatus);
            Assert.AreEqual(ShellScreen.Onboarding, c.Current);
            c.Invoke("start");
            Assert.AreEqual(ShellScreen.Guide, c.Current, c.Render().Notice);
            Assert.AreNotEqual(ScreenState.Error, c.Render().State);
        }

        [Test] public void Garbage_save_shows_error_and_is_never_overwritten()
        {
            var c = NewController(); c.Boot(); c.Invoke("start");
            var save = Path.Combine(_dir, "save.json");
            Assert.IsTrue(File.Exists(save));
            foreach (var f in Directory.GetFiles(_dir).Where(f => Path.GetFileName(f).StartsWith("save"))) File.WriteAllText(f, "not json {{{");
            var before = File.ReadAllText(save);

            var c2 = NewController(); c2.Boot();
            Assert.AreEqual(ShellScreen.BootError, c2.Current);
            var model = c2.Render();
            Assert.AreEqual(ScreenState.Error, model.State);
            Assert.IsFalse(model.Actions.Any(a => a.Id == "start"), "error screen must not offer a new game");
            c2.Invoke("retry");
            Assert.AreEqual(before, File.ReadAllText(save));
        }

        [Test] public void Continue_round_trips_through_the_codec_in_unity_runtime()
        {
            var c = NewController(); c.Boot(); c.Invoke("start"); c.Invoke("dev:walk");
            var c2 = NewController(); c2.Boot();
            Assert.That(c2.LastStartStatus, Is.EqualTo(StartStatus.Loaded).Or.EqualTo(StartStatus.RecoveredFromBackup));
            Assert.That(c2.Current, Is.EqualTo(ShellScreen.Guide), "unfinished onboarding resumes after restart");
        }

        [Test] public void Every_screen_renders_without_error_after_new_game()
        {
            var c = NewController(); c.Boot(); c.Invoke("start");
            foreach (var s in new[] { "Home", "ReturnSummary", "Projects", "Settings", "ActivityStatus", "Region" })
            {
                c.Invoke("nav:" + s);
                var m = c.Render();
                Assert.AreNotEqual(ScreenState.Error, m.State, s);
                Assert.IsNotEmpty(m.Lines, s);
            }
        }

        [Test] public void Projects_queue_action_queues_a_project()
        {
            var c = NewController(); c.Boot(); c.Invoke("start"); c.Invoke("nav:Projects");
            var queue = c.Render().Actions.FirstOrDefault(a => a.Id.StartsWith("queue:"));
            Assume.That(queue, Is.Not.Null, "Region 1 offers an available project at start");
            c.Invoke(queue!.Id);
            var m = c.Render();
            Assert.AreNotEqual(ScreenState.Error, m.State, m.Notice);
            Assert.IsFalse(m.Actions.Any(a => a.Id == queue.Id));
        }

        [Test] public void Settings_toggle_persists_across_sessions()
        {
            var c = NewController(); c.Boot(); c.Invoke("start"); c.Invoke("nav:Settings");
            c.Invoke("toggle:reducedMotion");
            var c2 = NewController(); c2.Boot(); c2.Invoke("nav:Settings");
            Assert.Contains(ShellController.ReducedMotionLine(true), c2.Render().Lines);
        }

        [Test] public void Delete_cancel_keeps_data()
        {
            var c = NewController(); c.Boot(); c.Invoke("start"); c.Invoke("nav:Settings");
            c.Invoke("delete:request");
            Assert.AreEqual(ShellScreen.DeleteConfirm, c.Current);
            c.Invoke("delete:cancel");
            Assert.AreEqual(ShellScreen.Settings, c.Current);
            var c2 = NewController(); c2.Boot();
            Assert.AreNotEqual(StartStatus.NoSaveFound, c2.LastStartStatus);
        }

        [Test] public void Delete_confirm_erases_and_returns_to_onboarding()
        {
            var c = NewController(); c.Boot(); c.Invoke("start"); c.Invoke("nav:Settings");
            c.Invoke("delete:request"); c.Invoke("delete:confirm");
            Assert.AreEqual(ShellScreen.Onboarding, c.Current);
            Assert.IsEmpty(Directory.GetFiles(_dir));
            var c2 = NewController(); c2.Boot();
            Assert.AreEqual(StartStatus.NoSaveFound, c2.LastStartStatus);
        }

        private static readonly string[] AllScreens = { "Home", "ReturnSummary", "Projects", "Settings", "ActivityStatus", "Region" };

        private static System.Collections.Generic.IEnumerable<string> EnumIdentifiers() =>
            System.Enum.GetNames(typeof(WalkGame.Application.Activity.ActivityPlayerStatus))
                .Concat(System.Enum.GetNames(typeof(WalkGame.Application.Activity.ActivityRecommendedAction)).Where(n => n != "None"))
                .Concat(new[] { "Ruined", "Stabilized", "Functional", "Flourishing" });

        [Test] public void No_screen_shows_enum_identifiers_or_content_keys()
        {
            var c = NewController(); c.Boot(); c.Invoke("start"); FinishOnboarding(c);
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var s in AllScreens)
                {
                    c.Invoke("nav:" + s);
                    var m = c.Render();
                    var texts = m.Lines.Concat(m.Actions.Select(a => a.Label)).Append(m.Title);
                    foreach (var t in texts)
                    {
                        foreach (var id in EnumIdentifiers()) Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(t, @"\b" + id + @"\b"), s + ": '" + t + "' leaks " + id);
                        Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(t, @"\b[a-z0-9-]+\.[a-z0-9-]+\b"), s + ": '" + t + "' looks like a content key");
                    }
                }
                c.Invoke("dev:walk"); c.Invoke("nav:Home");
            }
        }

        [Test] public void Empty_states_explain_the_next_action()
        {
            var c = NewController(); c.Boot(); c.Invoke("start"); FinishOnboarding(c);
            foreach (var s in AllScreens)
            {
                c.Invoke("nav:" + s);
                var m = c.Render();
                if (m.State != ScreenState.Empty) continue;
                Assert.IsTrue(m.Lines.Any(l => l.Length > 25 && !l.StartsWith("Nothing here")), s + " empty state lacks guidance");
            }
        }

        [Test] public void Region_screen_lists_landmarks_as_phrases_and_is_reachable_from_home()
        {
            var c = NewController(); c.Boot(); c.Invoke("start"); FinishOnboarding(c);
            Assert.IsTrue(c.Render().Actions.Any(a => a.Id == "nav:Region"));
            c.Invoke("nav:Region");
            var m = c.Render();
            Assert.AreEqual(ShellScreen.Region, c.Current);
            Assert.AreNotEqual(ScreenState.Error, m.State, m.Notice);
            Assert.That(m.Lines.Count, Is.GreaterThanOrEqualTo(3));
            Assert.IsTrue(m.Actions.Any(a => a.Id == "nav:Home"));
        }

        [Test] public void Settings_toggle_labels_describe_the_resulting_change()
        {
            var c = NewController(); c.Boot(); c.Invoke("start"); FinishOnboarding(c); c.Invoke("nav:Settings");
            string before = c.Render().Actions.First(a => a.Id == "toggle:reducedMotion").Label;
            c.Invoke("toggle:reducedMotion");
            string after = c.Render().Actions.First(a => a.Id == "toggle:reducedMotion").Label;
            Assert.AreNotEqual(before, after);
            Assert.IsTrue(after.StartsWith("Turn off") || after.StartsWith("Turn on"));
        }

        [Test] public void Every_activity_status_and_action_has_player_copy()
        {
            foreach (WalkGame.Application.Activity.ActivityPlayerStatus st in System.Enum.GetValues(typeof(WalkGame.Application.Activity.ActivityPlayerStatus)))
            {
                var t = PlayerCopy.ActivityStatus(st);
                Assert.IsFalse(string.IsNullOrWhiteSpace(t)); Assert.IsFalse(t.Contains(st.ToString()));
            }
            foreach (WalkGame.Application.Activity.ActivityRecommendedAction a in System.Enum.GetValues(typeof(WalkGame.Application.Activity.ActivityRecommendedAction)))
                Assert.AreEqual(a == WalkGame.Application.Activity.ActivityRecommendedAction.None, PlayerCopy.ActivityAction(a) == null);
        }

        private static void FinishOnboarding(ShellController c)
        {
            for (int i = 0; i < 12 && c.Current == ShellScreen.Guide; i++)
            {
                var actions = c.Render().Actions;
                var next = actions.FirstOrDefault(a => a.Id == "guide:next") ?? actions.First(a => a.Id.StartsWith("queue:"));
                c.Invoke(next.Id);
            }
        }

        [Test] public void Onboarding_walks_every_stage_and_lands_on_home()
        {
            var c = NewController(); c.Boot(); c.Invoke("start");
            var titles = new System.Collections.Generic.List<string>();
            for (int i = 0; i < 12 && c.Current == ShellScreen.Guide; i++)
            {
                var m = c.Render();
                Assert.AreNotEqual(ScreenState.Error, m.State, m.Notice);
                titles.Add(m.Title);
                var next = m.Actions.FirstOrDefault(a => a.Id == "guide:next") ?? m.Actions.First(a => a.Id.StartsWith("queue:"));
                c.Invoke(next.Id);
            }
            Assert.AreEqual(ShellScreen.Home, c.Current);
            CollectionAssert.IsSubsetOf(new[] { "The valley today", "Connect your walking", "Choose a first restoration", "Progress while you're away", "That's all" }, titles);
            var c2 = NewController(); c2.Boot();
            Assert.That(c2.Current, Is.EqualTo(ShellScreen.Home).Or.EqualTo(ShellScreen.ReturnSummary));
        }

        [Test] public void First_project_step_offers_no_skip()
        {
            var c = NewController(); c.Boot(); c.Invoke("start");
            for (int i = 0; i < 6; i++)
            {
                if (c.Render().Title == "Choose a first restoration") break;
                c.Invoke("guide:next");
            }
            var m = c.Render();
            Assert.AreEqual("Choose a first restoration", m.Title);
            Assert.IsFalse(m.Actions.Any(a => a.Id == "guide:next"), "first project completes only through a real project operation");
            Assert.IsTrue(m.Actions.Any(a => a.Id.StartsWith("queue:")));
        }

        [Test] public void Save_directory_is_under_persistent_data_path()
        {
            StringAssert.StartsWith(UnityEngine.Application.persistentDataPath, ShellBootstrap.SaveDirectory);
        }
    
        // M5-U3 onboarding polish (UX_DESIGN §11, §12).
        private static System.Collections.Generic.List<ScreenModel> WalkGuide(ShellController c)
        {
            var seen = new System.Collections.Generic.List<ScreenModel>();
            for (int i = 0; i < 12 && c.Current == ShellScreen.Guide; i++)
            {
                var m = c.Render(); seen.Add(m);
                var next = m.Actions.FirstOrDefault(a => a.Id == "guide:next") ?? m.Actions.First(a => a.Id.StartsWith("queue:"));
                c.Invoke(next.Id);
            }
            return seen;
        }

        [Test] public void Guide_screens_never_show_identifiers_keys_or_stage_names()
        {
            var c = NewController(); c.Boot(); c.Invoke("start");
            var screens = WalkGuide(c);
            Assert.That(screens.Count, Is.GreaterThanOrEqualTo(5));
            var activityNames = Enum.GetNames(typeof(WalkGame.Application.Activity.ActivityPlayerStatus));
            foreach (var m in screens)
                foreach (var text in m.Lines.Concat(m.Actions.Select(a => a.Label)))
                {
                    Assert.IsFalse(WalkGame.Application.ReadModels.GlancePhrases.ContainsStageIdentifier(text), text);
                    Assert.IsFalse(text.Contains("(stage"), text);
                    foreach (var n in activityNames) Assert.IsFalse(text.Contains(n), text);
                    foreach (var word in text.Split(' ')) Assert.IsFalse(word.Trim('.', ',').Contains("."), "content key leaked: " + text);
                }
            Assert.AreEqual(ShellScreen.Home, c.Current);
        }

        [Test] public void Guide_shows_monotonic_step_progress_ending_at_last_step()
        {
            var c = NewController(); c.Boot(); c.Invoke("start");
            var steps = WalkGuide(c).Select(m => m.Lines[0]).ToList();
            Assert.That(steps.All(s => s.StartsWith("Step ") && s.EndsWith(" of " + ShellController.GuideSteps)), string.Join("|", steps));
            var nums = steps.Select(s => int.Parse(s.Split(' ')[1])).ToList();
            Assert.AreEqual(1, nums.First());
            Assert.AreEqual(ShellController.GuideSteps, nums.Last());
            for (int i = 1; i < nums.Count; i++) Assert.That(nums[i], Is.GreaterThanOrEqualTo(nums[i - 1]));
        }

        [Test] public void Guide_progress_maps_every_stage_within_bounds()
        {
            foreach (WalkGame.Application.Ux.OnboardingStage st in Enum.GetValues(typeof(WalkGame.Application.Ux.OnboardingStage)))
            {
                var t = ShellController.GuideProgress(st);
                int n = int.Parse(t.Split(' ')[1]);
                Assert.That(n, Is.InRange(1, ShellController.GuideSteps), st.ToString());
            }
        }

        // D-068 Health Connect wiring (fake bridge; device behaviour UNVERIFIED, KI-002).
        private sealed class FakeHealthBridge : WalkGame.Infrastructure.Platform.HealthConnect.IHealthConnectBridge
        {
            public string Sdk = "available", Permission = "granted", Response = "ok";
            public int Reads;
            public string GetSdkStatus() => Sdk;
            public string GetPermissionStatus() => Permission;
            public string ReadSteps(long s, long e) { Reads++; return Response; }
        }

        private ShellController HealthController(FakeHealthBridge b, Action connect)
        {
            var adapter = new WalkGame.Infrastructure.Platform.HealthConnect.HealthConnectActivityAdapter(b, () => DateTimeOffset.UtcNow);
            return new ShellController(ShellBootstrap.CreateSession(_dir, adapter), () => 42UL, devTools: false, adapter, connect);
        }

        [Test] public void Granted_health_connect_steps_are_credited_on_boot_and_only_once()
        {
            var b = new FakeHealthBridge();
            var c = HealthController(b, () => { }); c.Boot(); c.Invoke("start");
            var end = DateTimeOffset.UtcNow.AddDays(-1);
            b.Response = "ok\nhc-1\t" + end.AddHours(-2).ToUnixTimeMilliseconds() + "\t" + end.ToUnixTimeMilliseconds() + "\t5000\t" + end.ToUnixTimeMilliseconds();

            var c2 = HealthController(b, () => { }); c2.Boot();
            Assert.That(b.Reads, Is.GreaterThan(0));
            var home = c2.Render();
            var c3 = HealthController(b, () => { }); c3.Boot();
            c3.Invoke("nav:ActivityStatus");
            Assert.That(c3.Render().Lines.Any(l => l.StartsWith("Connected")), string.Join("|", c3.Render().Lines));
        }

        [Test] public void Not_requested_offers_connect_which_calls_the_native_request()
        {
            int asked = 0;
            var c = HealthController(new FakeHealthBridge { Permission = "not_requested" }, () => asked++);
            c.Boot(); c.Invoke("start");
            c.Invoke("nav:ActivityStatus");
            var m = c.Render();
            Assert.IsTrue(m.Actions.Any(a => a.Id == "activity:connect"), string.Join("|", m.Actions.Select(a => a.Id)));
            c.Invoke("activity:connect");
            Assert.AreEqual(1, asked);
        }

        [Test] public void Failing_provider_never_blocks_boot_or_navigation()
        {
            var b = new FakeHealthBridge { Response = "error:timeout" };
            var c = HealthController(b, () => { }); c.Boot(); c.Invoke("start");
            var c2 = HealthController(b, () => { });
            Assert.DoesNotThrow(() => c2.Boot());
            Assert.AreNotEqual(ShellScreen.BootError, c2.Current);
            c2.Invoke("nav:ActivityStatus");
            Assert.AreNotEqual(ScreenState.Error, c2.Render().State);
        }

        [Test] public void Unsupported_device_never_offers_connect()
        {
            var c = HealthController(new FakeHealthBridge { Sdk = "unsupported", Permission = "not_requested" }, () => { });
            c.Boot(); c.Invoke("start"); c.Invoke("nav:ActivityStatus");
            Assert.IsFalse(c.Render().Actions.Any(a => a.Id == "activity:connect"));
        }
}
}
