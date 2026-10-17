using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Application.Development;
using WalkGame.Application.ReadModels;
using WalkGame.Domain.Common;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Time;
using WalkGame.Shell;
using WalkGame.Shell.Presentation;

namespace WalkGame.Shell.Tests
{
    /// <summary>
    /// D-169 (OpenSpec add-unity-presentation-seam): the style-neutral presenter seam driven from the
    /// D-166/D-167 read models with recording presenters (no art, no UI). Unity-editor-tested.
    /// </summary>
    public sealed class WorldPresentationSeamTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);
        private readonly List<string> _dirs = new List<string>();

        [TearDown] public void TearDown() { foreach (var d in _dirs) if (Directory.Exists(d)) Directory.Delete(d, true); _dirs.Clear(); }

        private string NewDir()
        {
            var d = Path.Combine(Path.GetTempPath(), "wg-seam-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(d); _dirs.Add(d); return d;
        }

        private static GameSession Started(string dir, ManualClock clock)
        {
            var s = ShellBootstrap.CreateSession(dir, new ShellConnectionPort(true), clock);
            Assert.IsTrue(s.StartNewGame(42UL, overwriteExisting: false).Status == StartStatus.NewGameCreated);
            return s;
        }

        private static WorldPresentationController Controller(GameSession s, Recorder r, IPresentationMemory? memory = null) =>
            new WorldPresentationController(s, r, r, r, r, memory ?? new InMemoryPresentationMemory());

        // ---- tap → dialogue + choices -------------------------------------------------------

        [Test] public void Tapping_a_landmark_opens_its_own_dialogue_and_restoration_choices()
        {
            var s = Started(NewDir(), new ManualClock(T0));
            var r = new Recorder();
            var c = Controller(s, r);
            c.Refresh();

            Assert.AreEqual(LandmarkTapOutcome.Opened, c.TapLandmark("lm.trailhead"));
            Assert.AreEqual("lm.trailhead", r.ChoicesLocation);
            CollectionAssert.Contains(r.Choices.Select(x => x.ProjectId).ToList(), "proj.clear-trailhead");
            Assert.IsTrue(r.Choices.All(x => x.LocationId == "lm.trailhead"), "only this place's goals are offered");
            // Cast-agnostic: whoever the content places here is who talks (the cast is authored content).
            var row = c.CurrentView!.Find("lm.trailhead")!;
            var here = s.GetCharacters().Where(ch => row.CharacterIds.Contains(ch.CharacterId) && ch.CurrentLine != null).ToList();
            Assume.That(here.Count, Is.GreaterThan(0), "content places someone at the starting place");
            Assert.IsNotNull(r.Dialogue);
            CollectionAssert.AreEqual(here.Select(ch => ch.CurrentLine!.LineId).ToList(), r.Dialogue!.Beats.Select(b => b.LineId).ToList());
            var beat = r.Dialogue.Current!;
            Assert.AreEqual(here[0].PortraitKey, beat.PortraitKey);
            Assert.AreEqual(here[0].Name, beat.Name);
            Assert.IsTrue(r.Dialogue.CanSkip);
        }

        [Test] public void Mid_game_each_place_offers_only_its_own_goals()
        {
            var clock = new ManualClock(T0);
            var s = Started(NewDir(), clock);
            for (int day = 0; day < 40 && s.GetRestorationChoices().Choices.Select(x => x.LocationId).Distinct().Count() < 3; day++)
            {
                foreach (var p in s.GetProjects().Projects.Where(p => p.Status == ProjectStatus.Available)) s.EnqueueProject(p.ProjectId);
                var from = clock.UtcNow; clock.Advance(TimeSpan.FromDays(1));
                s.IngestFromSource(new SyntheticWalkingSource(9000), from, clock.UtcNow);
            }
            var all = s.GetRestorationChoices().Choices;
            Assert.GreaterOrEqual(all.Select(x => x.LocationId).Distinct().Count(), 2, "goals are spread over several places");

            var r = new Recorder();
            var c = Controller(s, r);
            c.Refresh();
            int opened = 0;
            foreach (var row in c.CurrentView!.Locations.Where(l => l.IsUnlocked))
            {
                Assert.AreEqual(LandmarkTapOutcome.Opened, c.TapLandmark(row.LocationId));
                CollectionAssert.AreEqual(all.Where(x => x.LocationId == row.LocationId).Select(x => x.ProjectId).ToList(),
                    r.Choices.Select(x => x.ProjectId).ToList(), row.LocationId);
                if (r.Dialogue != null && c.OpenDialogue != null)
                    Assert.IsTrue(r.Dialogue.Beats.All(b => row.CharacterIds.Contains(b.CharacterId)), "only people who are here talk");
                c.CloseLocation();
                opened++;
            }
            Assert.GreaterOrEqual(opened, 2);
        }

        [Test] public void Tapping_a_locked_or_unknown_place_opens_nothing()
        {
            var s = Started(NewDir(), new ManualClock(T0));
            var r = new Recorder();
            var c = Controller(s, r);
            c.Refresh();
            var locked = c.CurrentView!.Locations.First(l => !l.IsUnlocked).LocationId;

            Assert.AreEqual(LandmarkTapOutcome.Ignored, c.TapLandmark(locked));
            Assert.AreEqual(LandmarkTapOutcome.Ignored, c.TapLandmark("lm.nowhere"));
            Assert.IsNull(r.ChoicesLocation);
            Assert.IsNull(r.Dialogue);
            Assert.IsNull(c.OpenLocationId);
        }

        [Test] public void Every_place_reaches_its_landmark_view_by_variant_key_and_missing_views_are_skipped()
        {
            var s = Started(NewDir(), new ManualClock(T0));
            var r = new Recorder("lm.trailhead", "lm.river-intake"); // a presenter that only draws two places
            var c = Controller(s, r);
            Assert.DoesNotThrow(() => c.Refresh());

            var view = s.GetWorldView();
            Assert.AreEqual(2, r.Landmarks.Count);
            foreach (var lv in r.Landmarks.Values)
            {
                var row = view.Find(lv.LocationId)!;
                Assert.AreEqual(row.VariantKey, lv.Last!.VariantKey);
                Assert.AreEqual(row.LocationId + "@ruined", lv.Last.VariantKey);
                Assert.AreEqual(row.ConstructionKey, lv.Last.ConstructionKey);
            }
            Assert.AreEqual(view.AtmosphereKey, r.World!.AtmosphereKey);
        }

        // ---- dialogue is skippable and never blocks -----------------------------------------

        [Test] public void Dialogue_can_be_skipped_at_once_and_stays_revisitable()
        {
            var s = Started(NewDir(), new ManualClock(T0));
            var r = new Recorder();
            var c = Controller(s, r);
            c.Refresh();
            Assume.That(c.CurrentView!.Find("lm.trailhead")!.CharacterIds.Count, Is.GreaterThan(0), "content places someone at the starting place");
            Assert.IsTrue(c.CurrentView!.Find("lm.trailhead")!.HasPendingDialogue);

            c.TapLandmark("lm.trailhead");
            c.SkipDialogue();

            Assert.IsNull(c.OpenDialogue);
            Assert.AreEqual(1, r.DialogueHidden);
            Assert.IsFalse(c.CurrentView!.Find("lm.trailhead")!.HasPendingDialogue, "skipped lines count as seen");
            var skipped = r.Dialogue!.Beats[0];
            Assert.IsTrue(c.LineHistory(skipped.CharacterId).Any(b => b.LineId == skipped.LineId), "a skipped line can be read again");
            Assert.AreEqual("lm.trailhead", r.ChoicesLocation, "skipping the talk leaves the goals open");
        }

        [Test] public void Advancing_through_dialogue_ends_it()
        {
            var s = Started(NewDir(), new ManualClock(T0));
            var r = new Recorder();
            var c = Controller(s, r);
            c.TapLandmark("lm.trailhead");
            Assume.That(c.OpenDialogue, Is.Not.Null, "content places someone at the starting place");
            int lines = c.OpenDialogue!.Beats.Count;
            for (int i = 1; i < lines; i++) Assert.IsTrue(c.AdvanceDialogue());
            Assert.IsFalse(c.AdvanceDialogue());
            Assert.IsNull(c.OpenDialogue);
            Assert.IsFalse(c.AdvanceDialogue(), "advancing a closed dialogue is a no-op");
        }

        [Test] public void An_open_dialogue_never_blocks_choosing_or_progress_and_reading_it_changes_nothing_canonical()
        {
            var clockA = new ManualClock(T0); var clockB = new ManualClock(T0);
            var a = Started(NewDir(), clockA); var b = Started(NewDir(), clockB);
            var r = new Recorder();
            var c = Controller(a, r);

            c.TapLandmark("lm.trailhead");                 // dialogue left open, never advanced
            Assume.That(c.OpenDialogue, Is.Not.Null, "content places someone at the starting place");
            Assert.IsTrue(c.ChooseRestorationGoal("proj.clear-trailhead").IsSuccess, "a goal can be picked while someone is talking");
            Assert.IsTrue(b.EnqueueProject("proj.clear-trailhead").IsSuccess);

            a.IngestFromSource(new SyntheticWalkingSource(9000), T0.AddDays(-5), T0);
            b.IngestFromSource(new SyntheticWalkingSource(9000), T0.AddDays(-5), T0);
            Assert.AreEqual(ProjectStatus.Completed, a.GetProjects().Projects.Single(p => p.ProjectId == "proj.clear-trailhead").Status,
                "walking progress does not wait for the dialogue");

            c.SkipDialogue(); c.TapLandmark("lm.trailhead"); c.AdvanceDialogue(); foreach (var ch in a.GetCharacters()) c.LineHistory(ch.CharacterId);
            CollectionAssert.AreEqual(b.ExportSaveEnvelope(), a.ExportSaveEnvelope(),
                "a player who opens, reads and skips dialogue has the same save as one who never did");
        }

        // ---- choosing a goal uses the existing command ---------------------------------------

        [Test] public void Choosing_a_goal_calls_EnqueueProject_exactly_like_the_text_shell()
        {
            var a = Started(NewDir(), new ManualClock(T0)); var b = Started(NewDir(), new ManualClock(T0));
            var r = new Recorder();
            var c = Controller(a, r);
            c.TapLandmark("lm.trailhead");

            Assert.IsTrue(c.ChooseRestorationGoal("proj.clear-trailhead").IsSuccess);
            Assert.IsTrue(b.EnqueueProject("proj.clear-trailhead").IsSuccess);
            CollectionAssert.AreEqual(b.ExportSaveEnvelope(), a.ExportSaveEnvelope(), "same command, same canonical result");
            var status = a.GetProjects().Projects.Single(p => p.ProjectId == "proj.clear-trailhead").Status;
            Assert.That(status, Is.EqualTo(ProjectStatus.Queued).Or.EqualTo(ProjectStatus.Active));

            var saved = a.ExportSaveEnvelope();
            Assert.IsTrue(c.ChooseRestorationGoal("proj.clear-trailhead").IsSuccess, "picking it again is a no-op (D-128)");
            CollectionAssert.AreEqual(saved, a.ExportSaveEnvelope());
            Assert.IsTrue(r.Choices.Any(x => x.ProjectId == "proj.clear-trailhead" && x.Status == status), "choices re-shown with the new status");

            var unknown = c.ChooseRestorationGoal("proj.nope");
            Assert.IsFalse(unknown.IsSuccess);
            Assert.AreEqual(ErrorCodes.UnknownProject, unknown.Error!.Code);
        }

        // ---- visual changes since last open, replayed once ----------------------------------

        [Test] public void Changes_made_while_away_are_replayed_once_on_resume_in_completion_order()
        {
            var clock = new ManualClock(T0);
            var s = Started(NewDir(), clock);
            var r = new Recorder();
            var memory = new InMemoryPresentationMemory();
            var c = Controller(s, r, memory);

            Assert.AreEqual(0, c.Resume(), "first open: the current world is the baseline");
            foreach (var p in s.GetProjects().Projects.Where(p => p.Status == ProjectStatus.Available))
                Assert.IsTrue(c.ChooseRestorationGoal(p.ProjectId).IsSuccess);
            var shownThrough = memory.ChangesShownThroughUtc!.Value;
            r.Played.Clear();

            clock.Advance(TimeSpan.FromDays(10));         // app closed; walks synced on return
            s.IngestFromSource(new SyntheticWalkingSource(12000), T0, clock.UtcNow);
            var expected = s.GetVisualChangesSince(shownThrough);
            Assume.That(expected.Count, Is.GreaterThan(0), "ten days of walking changed the valley");

            Assert.AreEqual(expected.Count, c.Resume());
            Assert.AreEqual(1, r.Played.Count, "one replay batch");
            CollectionAssert.AreEqual(expected.Select(e => e.ToString()).ToList(), r.Played[0].Select(e => e.ToString()).ToList());
            CollectionAssert.IsOrdered(r.Played[0].Select(e => e.Sequence).ToList());

            Assert.AreEqual(0, c.Resume(), "already shown: never replayed twice");
            Assert.AreEqual(1, r.Played.Count);

            var reopened = ShellBootstrap.CreateSession(_dirs[0], new ShellConnectionPort(true), clock);
            Assert.AreEqual(StartStatus.Loaded, reopened.Continue().Status);
            Assert.AreEqual(0, Controller(reopened, r, memory).Resume(), "same memory after a reload: still once");
        }

        [Test] public void Resume_passes_reduced_motion_to_the_player()
        {
            var clock = new ManualClock(T0);
            var s = Started(NewDir(), clock);
            var r = new Recorder();
            var c = Controller(s, r);
            c.Resume();
            Assert.IsTrue(s.EnqueueProject("proj.clear-trailhead").IsSuccess);
            Assert.IsTrue(s.SetReducedMotion(true).IsSuccess);
            clock.Advance(TimeSpan.FromDays(5));
            s.IngestFromSource(new SyntheticWalkingSource(12000), T0, clock.UtcNow);
            Assume.That(c.Resume(), Is.GreaterThan(0));
            Assert.IsTrue(r.LastReducedMotion);
        }

        [Test] public void Null_presentation_runs_the_whole_loop_headless()
        {
            var clock = new ManualClock(T0);
            var s = Started(NewDir(), clock);
            var n = NullPresentation.Instance;
            var c = new WorldPresentationController(s, n, n, n, n, new InMemoryPresentationMemory());
            Assert.DoesNotThrow(() =>
            {
                c.Resume(); c.TapLandmark("lm.trailhead"); c.AdvanceDialogue(); c.SkipDialogue();
                c.ChooseRestorationGoal("proj.clear-trailhead"); c.CloseLocation();
                clock.Advance(TimeSpan.FromDays(3));
                s.IngestFromSource(new SyntheticWalkingSource(9000), T0, clock.UtcNow);
                c.Resume();
            });
        }

        // ---- asset catalog with missing-asset fallback ---------------------------------------

        [Test] public void Asset_resolution_falls_back_from_exact_to_subject_to_global_to_missing()
        {
            var exact = new PresentationAssetResolver<string>(new DictionaryAssetCatalog<string>().Add("lm.wetland@ruined", "A"));
            var r1 = exact.Resolve("lm.wetland@ruined");
            Assert.AreEqual(AssetResolution.Exact, r1.Resolution); Assert.AreEqual("A", r1.Asset); Assert.IsFalse(r1.IsFallback);

            var catalog = new DictionaryAssetCatalog<string>().Add("lm.wetland", "S").Add(PresentationAssetResolver<string>.GlobalFallbackKey, "G");
            var res = new PresentationAssetResolver<string>(catalog);
            var r2 = res.Resolve("lm.wetland@flourishing");
            Assert.AreEqual(AssetResolution.SubjectFallback, r2.Resolution); Assert.AreEqual("S", r2.Asset); Assert.AreEqual("lm.wetland", r2.ResolvedKey);
            var r3 = res.Resolve("portrait.example");
            Assert.AreEqual(AssetResolution.GlobalFallback, r3.Resolution); Assert.AreEqual("G", r3.Asset);
            CollectionAssert.AreEqual(new[] { "lm.wetland@flourishing", "portrait.example" }, res.MissingKeys.ToArray());

            var none = new PresentationAssetResolver<string>(new EmptyAssetCatalog<string>());
            var r4 = none.Resolve("lm.trailhead@ruined");
            Assert.AreEqual(AssetResolution.Missing, r4.Resolution); Assert.IsNull(r4.Asset); Assert.IsTrue(r4.IsFallback);
            Assert.AreEqual(AssetResolution.Missing, none.Resolve("").Resolution);
        }

        [Test] public void With_no_art_every_key_the_world_and_dialogue_name_resolves_to_missing_without_throwing()
        {
            var s = Started(NewDir(), new ManualClock(T0));
            var resolver = new PresentationAssetResolver<UnityEngine.Object>(new EmptyAssetCatalog<UnityEngine.Object>());
            var view = s.GetWorldView();
            var keys = view.Locations.Select(l => l.VariantKey).Concat(new[] { view.AtmosphereKey })
                .Concat(s.GetCharacters().Select(ch => ch.PortraitKey))
                .Concat(s.GetRestorationChoices().Choices.SelectMany(ch => new[] { ch.BeforeVariantKey, ch.AfterVariantKey }).Where(k => k != null).Select(k => k!))
                .ToList();
            Assert.IsNotEmpty(keys);
            foreach (var k in keys) Assert.AreEqual(AssetResolution.Missing, resolver.Resolve(k).Resolution, k);
            Assert.AreEqual(keys.Distinct().Count(), resolver.MissingKeys.Count, "the to-make list names every key once");
        }

        // ---- recording presenter -------------------------------------------------------------

        private sealed class Recorder : IWorldPresenter, IDialoguePresenter, IChoicePreviewPresenter, IVisualChangePlayer
        {
            public readonly Dictionary<string, LandmarkRecorder> Landmarks = new Dictionary<string, LandmarkRecorder>();
            private readonly HashSet<string>? _drawn;
            public Recorder(params string[] drawnLocations) { _drawn = drawnLocations.Length == 0 ? null : new HashSet<string>(drawnLocations); }

            public WorldViewReadModel? World;
            public DialogueSequence? Dialogue;
            public int DialogueHidden;
            public string? ChoicesLocation;
            public IReadOnlyList<RestorationChoicesReadModel.Choice> Choices = Array.Empty<RestorationChoicesReadModel.Choice>();
            public readonly List<IReadOnlyList<VisualChangeEvent>> Played = new List<IReadOnlyList<VisualChangeEvent>>();
            public bool LastReducedMotion;

            public void ShowWorld(WorldViewReadModel view) => World = view;
            public ILandmarkView? FindLandmark(string locationId)
            {
                if (_drawn != null && !_drawn.Contains(locationId)) return null;
                if (!Landmarks.TryGetValue(locationId, out var v)) Landmarks[locationId] = v = new LandmarkRecorder(locationId);
                return v;
            }
            public void ShowDialogue(DialogueSequence sequence) => Dialogue = sequence;
            public void HideDialogue() { DialogueHidden++; }
            public void ShowChoices(string locationId, IReadOnlyList<RestorationChoicesReadModel.Choice> choices) { ChoicesLocation = locationId; Choices = choices; }
            public void HideChoices() { ChoicesLocation = null; Choices = Array.Empty<RestorationChoicesReadModel.Choice>(); }
            public void Play(IReadOnlyList<VisualChangeEvent> events, bool reducedMotion) { Played.Add(events); LastReducedMotion = reducedMotion; }
        }

        private sealed class LandmarkRecorder : ILandmarkView
        {
            public LandmarkRecorder(string id) { LocationId = id; }
            public string LocationId { get; }
            public LandmarkPresentation? Last;
            public void Show(LandmarkPresentation landmark) => Last = landmark;
        }
    }
}
