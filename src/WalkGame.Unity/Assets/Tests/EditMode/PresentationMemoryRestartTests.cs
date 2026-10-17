using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Application.Development;
using WalkGame.Application.Persistence;
using WalkGame.Application.Presentation;
using WalkGame.Application.ReadModels;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;
using WalkGame.Shell;
using WalkGame.Shell.Presentation;

namespace WalkGame.Shell.Tests
{
    /// <summary>
    /// D-175 (KI-027 c, bible §22, audit §4.10): with the shipped composition (<see cref="ShellBootstrap.CreateSession(string, WalkGame.Application.Activity.IActivityConnectionPort, IClock)"/>)
    /// and <see cref="SessionPresentationMemory"/>, "changes since last open" and seen lines survive a cold
    /// restart, are shown exactly once, never touch the save, and a damaged memory file means no replay.
    /// Unity-editor-tested.
    /// </summary>
    public sealed class PresentationMemoryRestartTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);
        private string _dir = string.Empty;

        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-memory-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private string MemoryFile => Path.Combine(_dir, LocalPresentationMemoryStore.DefaultFileName);

        private GameSession Boot(ManualClock clock, bool fresh = false)
        {
            var s = ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(true), clock);
            if (fresh) Assert.AreEqual(StartStatus.NewGameCreated, s.StartNewGame(42UL, overwriteExisting: false).Status);
            else Assert.AreEqual(StartStatus.Loaded, s.Continue().Status);
            return s;
        }

        private static WorldPresentationController Controller(GameSession s, Player p) =>
            new WorldPresentationController(s, NullPresentation.Instance, NullPresentation.Instance, NullPresentation.Instance, p,
                new SessionPresentationMemory(s));

        private static string Snapshot(WorldViewReadModel v) =>
            v.AtmosphereKey + "|" + string.Join(";", v.Locations.Select(l => l.VariantKey + "," + l.ConstructionKey + "," + l.FocusProjectId + ",[" + string.Join(",", l.CharacterIds) + "]"));

        [Test] public void Changes_earned_while_the_app_was_closed_are_shown_once_after_a_cold_restart()
        {
            var clock = new ManualClock(T0);
            var s = Boot(clock, fresh: true);
            var p = new Player();
            Assert.AreEqual(0, Controller(s, p).Resume(), "first open ever: baseline");
            Assert.IsTrue(File.Exists(MemoryFile), "the baseline is remembered on disk");
            Assert.IsTrue(s.EnqueueProject("proj.clear-trailhead").IsSuccess);
            var shownThrough = s.GetPresentationMemory().ChangesShownThroughUtc!.Value;

            // App killed. Ten days of walking later it cold-starts and syncs.
            clock.Advance(TimeSpan.FromDays(10));
            var cold = Boot(clock);
            cold.IngestFromSource(new SyntheticWalkingSource(12000), T0, clock.UtcNow);
            var expected = cold.GetVisualChangesSince(shownThrough);
            Assume.That(expected.Count, Is.GreaterThan(0), "the walking changed the valley");

            var p2 = new Player();
            Assert.AreEqual(expected.Count, Controller(cold, p2).Resume(), "the return shows what changed while away");
            CollectionAssert.AreEqual(expected.Select(e => e.ToString()).ToList(), p2.Played.Single().Select(e => e.ToString()).ToList());

            var p3 = new Player();
            Assert.AreEqual(0, Controller(Boot(clock), p3).Resume(), "another cold start: already shown, never replayed");
            Assert.IsEmpty(p3.Played);
        }

        [Test] public void A_completion_stamped_before_the_last_open_by_a_corrected_back_clock_is_still_shown_once()
        {
            // D-180 (KI-028): the trailhead is shown at day 2, then the device clock is corrected back to day 1
            // and the bridge is rebuilt while the app is closed. Its stamp is older than the last shown instant.
            var clock = new ManualClock(T0.AddDays(2));
            var s = Boot(clock, fresh: true);
            Assert.AreEqual(0, Controller(s, new Player()).Resume(), "baseline");
            Assert.IsTrue(s.EnqueueProject("proj.clear-trailhead").IsSuccess);
            s.CreditActivity(Guid.NewGuid(), clock.UtcNow, 100000, "test");
            var p1 = new Player();
            Assert.Greater(Controller(s, p1).Resume(), 0, "the trailhead is shown");

            clock.Set(T0.AddDays(1));
            var closed = Boot(clock);
            Assert.IsTrue(closed.EnqueueProject("proj.rebuild-trail-bridges").IsSuccess);
            closed.CreditActivity(Guid.NewGuid(), clock.UtcNow, 100000, "test");

            var p2 = new Player();
            int played = Controller(Boot(clock), p2).Resume();
            Assert.Greater(played, 0, "the rebuilt bridge is replayed although its stamp is older");
            var batch = p2.Played.Single();
            Assert.AreEqual(VisualChangeKind.ProjectCompleted, batch[0].Kind);
            Assert.AreEqual("proj.rebuild-trail-bridges", batch[0].SubjectId);
            Assert.IsTrue(batch.All(e => e.CompletionId == "proj.rebuild-trail-bridges"), "only the bridge, the trailhead is not repeated");

            var p3 = new Player();
            Assert.AreEqual(0, Controller(Boot(clock), p3).Resume(), "and never again");
            Assert.IsEmpty(p3.Played);
            int total = p1.Played.Sum(b => b.Count) + played;
            Assert.AreEqual(Boot(clock).GetVisualChangesSince(DateTimeOffset.MinValue).Count, total, "every change of the stream was shown exactly once");
        }

        [Test] public void Seen_lines_stay_seen_after_a_cold_restart()
        {
            var clock = new ManualClock(T0);
            var s = Boot(clock, fresh: true);
            var c = Controller(s, new Player());
            c.Refresh();
            Assume.That(c.CurrentView!.Find("lm.trailhead")!.HasPendingDialogue, "someone has something to say at the start");
            c.TapLandmark("lm.trailhead");
            c.SkipDialogue();

            var cold = Controller(Boot(clock), new Player());
            cold.Refresh();
            Assert.IsFalse(cold.CurrentView!.Find("lm.trailhead")!.HasPendingDialogue, "a line read before the restart is not new again");
        }

        [Test] public void After_a_cold_restart_the_world_is_the_same_and_the_save_is_untouched_by_presentation()
        {
            var clock = new ManualClock(T0);
            var s = Boot(clock, fresh: true);
            Assert.IsTrue(s.EnqueueProject("proj.clear-trailhead").IsSuccess);
            var from = clock.UtcNow; clock.Advance(TimeSpan.FromHours(10));
            s.IngestFromSource(new SyntheticWalkingSource(4000), from, clock.UtcNow);
            var c = Controller(s, new Player());
            c.Resume(); c.TapLandmark("lm.trailhead"); c.SkipDialogue();
            var before = Snapshot(s.GetWorldView());

            var cold = Boot(clock); // same clock: nothing new to credit
            var saveBytes = File.ReadAllBytes(Path.Combine(_dir, "save.json")); // after boot's own commit
            var c2 = Controller(cold, new Player());
            c2.Resume(); c2.TapLandmark("lm.trailhead"); c2.AdvanceDialogue(); c2.CloseLocation();
            Assert.AreEqual(before, Snapshot(cold.GetWorldView()), "same variant, construction and focus keys and the same people in the same places");
            Assert.AreEqual(saveBytes, File.ReadAllBytes(Path.Combine(_dir, "save.json")), "presentation memory never writes the save");
        }

        [Test] public void A_damaged_memory_file_means_no_replay_and_changes_nothing_canonical()
        {
            var clock = new ManualClock(T0);
            var s = Boot(clock, fresh: true);
            Controller(s, new Player()).Resume();
            Assert.IsTrue(s.EnqueueProject("proj.clear-trailhead").IsSuccess);
            clock.Advance(TimeSpan.FromDays(10));
            var cold = Boot(clock);
            cold.IngestFromSource(new SyntheticWalkingSource(12000), T0, clock.UtcNow);
            File.WriteAllText(MemoryFile, "{ damaged");

            var again = Boot(clock);
            var saveBytes = File.ReadAllBytes(Path.Combine(_dir, "save.json")); // after boot's own commit
            var p = new Player();
            Assert.AreEqual(0, Controller(again, p).Resume(), "unknown memory: baseline, never an invented or repeated replay");
            Assert.AreEqual(PresentationMemoryLoadOutcome.Malformed, again.PresentationMemoryLoadOutcome);
            Assert.IsEmpty(p.Played);
            Assert.AreEqual(saveBytes, File.ReadAllBytes(Path.Combine(_dir, "save.json")));
            Assert.AreEqual(ProjectStatus.Completed, again.GetProjects().Projects.Single(x => x.ProjectId == "proj.clear-trailhead").Status);
            Assert.AreEqual(PresentationMemoryLoadOutcome.Success, new LocalPresentationMemoryStore(_dir).Load().Outcome, "the baseline replaced the damaged file");
        }

        [Test] public void Delete_all_local_data_in_the_shipped_composition_erases_presentation_memory()
        {
            var clock = new ManualClock(T0);
            var s = Boot(clock, fresh: true);
            Controller(s, new Player()).Resume();
            Assert.IsTrue(File.Exists(MemoryFile));
            Assert.AreEqual(LocalDataEraseStatus.Erased, s.DeleteAllLocalData().Status);
            Assert.IsEmpty(Directory.GetFiles(_dir));
        }

        private sealed class Player : IVisualChangePlayer
        {
            public readonly List<IReadOnlyList<VisualChangeEvent>> Played = new List<IReadOnlyList<VisualChangeEvent>>();
            public void Play(IReadOnlyList<VisualChangeEvent> events, bool reducedMotion) => Played.Add(events);
        }
    }
}
