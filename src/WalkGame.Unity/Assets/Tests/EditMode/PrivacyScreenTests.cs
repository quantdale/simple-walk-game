using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>D-111 Privacy screen and save export. Unity-editor-tested; Android share sheet on a device UNVERIFIED (KI-002).</summary>
    public sealed class PrivacyScreenTests
    {
        private string _dir = "";
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-priv-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private ShellController Started()
        {
            var c = new ShellController(ShellBootstrap.CreateSession(_dir), () => 42UL, devTools: true);
            c.Boot(); c.Invoke("start");
            return c;
        }

        private string[] Files() => Directory.GetFiles(_dir).OrderBy(f => f).Select(f => f + ":" + Convert.ToBase64String(File.ReadAllBytes(f))).ToArray();

        [Test] public void Settings_leads_to_privacy_and_back()
        {
            var c = Started(); c.Invoke("nav:Settings");
            Assert.IsTrue(c.Render().Actions.Any(a => a.Id == "nav:Privacy" && a.Label == "Privacy and your data"));
            c.Invoke("nav:Privacy");
            var m = c.Render();
            Assert.AreEqual(ShellScreen.Privacy, m.Screen);
            Assert.AreEqual(ScreenState.Normal, m.State);
            CollectionAssert.AreEqual(ShellController.PrivacyLines, m.Lines);
            CollectionAssert.AreEqual(new[] { "export", "nav:Settings" }, m.Actions.Select(a => a.Id));
            Assert.IsTrue(m.Lines.Any(l => l.Contains("nothing is sent anywhere")));
            c.Invoke("nav:Settings");
            Assert.AreEqual(ShellScreen.Settings, c.Current);
        }

        [Test] public void Export_shares_a_readable_json_copy_and_changes_nothing_on_disk()
        {
            var c = Started(); c.Invoke("nav:Privacy");
            var shared = new List<string>();
            c.ShareText = t => { shared.Add(t); return true; };
            var before = Files();
            c.Invoke("export");
            CollectionAssert.AreEqual(before, Files());
            Assert.AreEqual(1, shared.Count);
            // The document structure is pinned by the .NET SaveExportTests; here: the shell shares it as-is.
            StringAssert.StartsWith("{", shared[0].TrimStart());
            StringAssert.Contains("\"format\": \"simple-walk-game-export\"", shared[0]);
            StringAssert.Contains("\"state\": {", shared[0]);
            var m = c.Render();
            Assert.AreEqual(ScreenState.Normal, m.State);
            Assert.AreEqual("Your phone's share menu has the copy. You choose where it goes.", m.Notice);
            Assert.AreEqual(ShellScreen.Privacy, c.Current);
        }

        [Test] public void No_share_sheet_is_a_calm_error()
        {
            var c = Started(); c.Invoke("nav:Privacy");
            c.ShareText = null;
            c.Invoke("export");
            var m = c.Render();
            Assert.AreEqual(ScreenState.Error, m.State);
            Assert.AreEqual("Sharing isn't available on this device. Your save is safe.", m.Notice);
        }

        [Test] public void A_failing_or_throwing_share_sheet_is_a_calm_error()
        {
            var c = Started(); c.Invoke("nav:Privacy");
            c.ShareText = _ => false;
            c.Invoke("export");
            Assert.AreEqual("The share menu didn't open. Your save is safe.", c.Render().Notice);
            c.ShareText = _ => throw new InvalidOperationException("boom");
            Assert.DoesNotThrow(() => c.Invoke("export"));
            Assert.AreEqual("The share menu didn't open. Your save is safe.", c.Render().Notice);
        }

        [Test] public void Export_and_privacy_are_refused_before_a_game_is_loaded()
        {
            var c = new ShellController(ShellBootstrap.CreateSession(_dir), () => 42UL, devTools: true);
            c.Boot();
            bool called = false;
            c.ShareText = _ => { called = true; return true; };
            c.Invoke("nav:Privacy");
            Assert.AreEqual(ShellScreen.Onboarding, c.Current);
            c.Invoke("export");
            Assert.IsFalse(called);
            Assert.AreEqual(ScreenState.Error, c.Render().State);
        }

        [Test] public void Privacy_copy_has_no_raw_identifiers()
        {
            foreach (var line in ShellController.PrivacyLines)
            {
                StringAssert.DoesNotContain("_", line);
                StringAssert.DoesNotContain("json", line.ToLowerInvariant());
                Assert.LessOrEqual(line.Length, 200, line);
            }
        }

        [Test] public void Share_sheet_source_makes_no_network_call()
        {
            string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "Shell"));
            foreach (var f in new[] { "AndroidShareSheet.cs", "ShellController.cs" })
            {
                var src = File.ReadAllText(Path.Combine(root, f));
                foreach (var banned in new[] { "UnityWebRequest", "HttpClient", "WebClient", "System.Net", "Socket" })
                    StringAssert.DoesNotContain(banned, src, f + " uses " + banned);
            }
        }
    }
}
