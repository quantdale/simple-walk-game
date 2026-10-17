using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Domain.Time;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>D-100: a finished project in the return summary gets a calm landmark + story line. Unity-editor-tested.</summary>
    public sealed class ProjectCompletionMomentTests
    {
        private string _dir;
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-pcm-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        [Test] public void Finished_trailhead_shows_landmark_and_story_line_in_the_summary()
        {
            var session = ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(true), new ManualClock(DateTimeOffset.UtcNow));
            var c = new ShellController(session, () => 42UL, devTools: true);
            c.Boot(); c.Invoke("start");
            for (int i = 0; i < 12 && c.Current == ShellScreen.Guide; i++)
            {
                var g = c.Render();
                var q = g.Actions.FirstOrDefault(a => a.Id.StartsWith("queue:"));
                c.Invoke(g.Actions.Any(a => a.Id == "guide:next") ? "guide:next" : q?.Id ?? g.Actions[0].Id);
            }
            var now = DateTimeOffset.UtcNow; // dev:walk gives 60 Vitality; one bigger synthetic day finishes the 300-Vitality trailhead
            session.IngestFromSource(new WalkGame.Application.Development.SyntheticWalkingSource(60000), now.AddDays(-1), now);
            c.Invoke("nav:ReturnSummary");
            var m = c.Render();
            string all = string.Join("\n", m.Lines);
            int at = m.Lines.FindIndex(l => l == "The old trailhead is clear."); // D-162 display form of "Clear the old trailhead is complete."
            Assert.That(at, Is.GreaterThanOrEqualTo(0), all);
            Assert.That(m.Lines[at + 1], Does.StartWith("Old Trailhead is now stabilized."), all);
            StringAssert.Contains("The Way In", m.Lines[at + 1]);
            foreach (var g in CopyPolishTests.GuiltPhrases) StringAssert.DoesNotContain(g, m.Lines[at + 1].ToLowerInvariant());
        }
    }
}
