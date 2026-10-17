using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Application.ReadModels;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Time;
using WalkGame.Shell;
using WalkGame.Shell.Presentation;

namespace WalkGame.Shell.Tests
{
    /// <summary>
    /// D-176 (KI-027 a, b): through the seam, the Old Trailhead bridge is drawable as ruined → partly
    /// rebuilt → rebuilt, inspectable with its current and restored keys, and its completion replays as
    /// one ProjectCompleted transition. No art. Unity-editor-tested.
    /// </summary>
    public sealed class BridgePresentationTests
    {
        private const string Bridge = "proj.rebuild-trail-bridges";
        private const string Clear = "proj.clear-trailhead";
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);
        private string _dir = string.Empty;
        private int _walk;

        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-bridge-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); _walk = 0; }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private GameSession Started(ManualClock clock)
        {
            var s = ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(true), clock);
            Assert.AreEqual(StartStatus.NewGameCreated, s.StartNewGame(42UL, overwriteExisting: false).Status);
            return s;
        }

        /// <summary>One 1,500-step walk two hours later (15 Vitality: finer than a 100-Vitality bridge stage).</summary>
        private void Walk(GameSession s, ManualClock clock)
        {
            clock.Advance(TimeSpan.FromHours(2));
            _walk++;
            s.IngestActivityBatch(new[] { new NormalizedActivityRecord("bridge.provider", "walk-" + _walk, ActivityCategory.Walking,
                ActivityUnits.Steps, 1500L, clock.UtcNow.AddMinutes(-30), clock.UtcNow) });
            if (s.GetPendingReturnSummary() != null) s.AcknowledgeReturnSummary();
        }

        private static ProjectStatus StatusOf(GameSession s, string id) => s.GetProjects().Projects.Single(p => p.ProjectId == id).Status;

        [Test] public void The_trailhead_view_and_inspect_card_follow_the_bridge_from_ruined_to_rebuilt()
        {
            var clock = new ManualClock(T0);
            var s = Started(clock);
            var r = new Recorder();
            var c = new WorldPresentationController(s, r, r, r, r, new InMemoryPresentationMemory());
            Assert.IsTrue(s.EnqueueProject(Clear).IsSuccess);

            var landmarkKeys = new List<string>();
            var inspectKeys = new List<string>();
            for (int i = 0; i < 120 && StatusOf(s, Bridge) != ProjectStatus.Completed; i++)
            {
                Walk(s, clock);
                if (StatusOf(s, Clear) == ProjectStatus.Completed && StatusOf(s, Bridge) == ProjectStatus.Available)
                    Assert.IsTrue(c.ChooseRestorationGoal(Bridge).IsSuccess);
                c.Refresh();
                landmarkKeys.Add(r.Trailhead!.Props.Single(p => p.ProjectId == Bridge).VisualKey);
                if (c.TapProp(Bridge) == TapOutcome.Opened) inspectKeys.Add(r.Inspect!.PropKey!);
                c.CloseLocation();
            }
            var expected = new[] { "@ruined", "@build-0of3", "@build-1of3", "@build-2of3", "@done" }.Select(k => Bridge + k).ToArray();
            CollectionAssert.IsSubsetOf(new[] { Bridge + "@build-1of3", Bridge + "@build-2of3", Bridge + "@done" }, landmarkKeys.Distinct().ToArray());
            CollectionAssert.AreEqual(expected.Where(landmarkKeys.Contains).ToArray(), landmarkKeys.Distinct().ToArray(), "keys only move forward");
            CollectionAssert.Contains(inspectKeys, Bridge + "@build-1of3");
            Assert.AreEqual(Bridge + "@done", inspectKeys.Last());
            c.TapProp(Bridge);
            Assert.AreEqual(Bridge + "@done", r.Inspect!.DonePropKey);
        }

        [Test] public void Coming_back_to_a_finished_bridge_replays_its_transition_once_first_and_Mira_reacts()
        {
            var clock = new ManualClock(T0);
            var s = Started(clock);
            var r = new Recorder();
            var c = new WorldPresentationController(s, r, r, r, r, new SessionPresentationMemory(s));
            Assert.IsTrue(s.EnqueueProject(Clear).IsSuccess);
            for (int i = 0; i < 60 && StatusOf(s, Clear) != ProjectStatus.Completed; i++) Walk(s, clock);
            Assert.IsTrue(s.EnqueueProject(Bridge).IsSuccess);
            c.Resume();                                 // last open: bridge not done yet
            r.Played.Clear();

            for (int i = 0; i < 60 && StatusOf(s, Bridge) != ProjectStatus.Completed; i++) Walk(s, clock); // app closed
            var cold = ShellBootstrap.CreateSession(_dir, new ShellConnectionPort(true), clock);
            Assert.AreEqual(StartStatus.Loaded, cold.Continue().Status);
            var r2 = new Recorder();
            var c2 = new WorldPresentationController(cold, r2, r2, r2, r2, new SessionPresentationMemory(cold));
            Assert.Greater(c2.Resume(), 0);

            var batch = r2.Played.Single();
            var done = batch.Where(e => e.Kind == VisualChangeKind.ProjectCompleted && e.SubjectId == Bridge).ToList();
            Assert.AreEqual(1, done.Count);
            Assert.AreEqual(Bridge + "@done", done[0].ToKey);
            Assert.AreEqual(VisualChangeKind.ProjectCompleted, batch.First(e => e.AtUtc == done[0].AtUtc).Kind, "the bridge transition comes first in its batch");
            var mira = cold.GetCharacters().First(ch => ch.LocationId == "lm.trailhead").CharacterId;
            CollectionAssert.Contains(r2.Reactions(mira), CharacterReaction.Acknowledge);

            var r3 = new Recorder();
            Assert.AreEqual(0, new WorldPresentationController(cold, r3, r3, r3, r3, new SessionPresentationMemory(cold)).Resume(), "never replayed as if it just happened");
        }

        [Test] public void With_no_art_the_bridge_keys_are_on_the_to_make_list_and_fall_back_to_the_bridge()
        {
            var empty = new PresentationAssetResolver<string>(new EmptyAssetCatalog<string>());
            foreach (var k in new[] { "@ruined", "@build-1of3", "@done" }) Assert.AreEqual(AssetResolution.Missing, empty.Resolve(Bridge + k).Resolution);
            CollectionAssert.AreEquivalent(new[] { Bridge + "@ruined", Bridge + "@build-1of3", Bridge + "@done" }, empty.MissingKeys.ToArray());
            var some = new PresentationAssetResolver<string>(new DictionaryAssetCatalog<string>().Add(Bridge, "BRIDGE").Add(Bridge + "@done", "DONE"));
            Assert.AreEqual("BRIDGE", some.Resolve(Bridge + "@build-2of3").Asset);
            Assert.AreEqual("DONE", some.Resolve(Bridge + "@done").Asset);
        }

        private sealed class Recorder : IWorldPresenter, IDialoguePresenter, IChoicePreviewPresenter, IVisualChangePlayer,
            ICharacterPresenter, IInspectPresenter
        {
            public LandmarkPresentation? Trailhead;
            public InspectCard? Inspect;
            public readonly List<IReadOnlyList<VisualChangeEvent>> Played = new List<IReadOnlyList<VisualChangeEvent>>();
            private readonly Dictionary<string, View> _chars = new Dictionary<string, View>();
            public List<CharacterReaction> Reactions(string id) => _chars.TryGetValue(id, out var v) ? v.Reactions : new List<CharacterReaction>();

            public void ShowWorld(WorldViewReadModel view) { }
            public ILandmarkView? FindLandmark(string locationId) => locationId == "lm.trailhead" ? new Landmark(this) : null;
            public ICharacterView? FindCharacter(string characterId)
            {
                if (!_chars.TryGetValue(characterId, out var v)) _chars[characterId] = v = new View(characterId);
                return v;
            }
            public void ShowDialogue(DialogueSequence sequence) { }
            public void HideDialogue() { }
            public void ShowChoices(string locationId, IReadOnlyList<RestorationChoicesReadModel.Choice> choices) { }
            public void HideChoices() { }
            public void ShowInspect(InspectCard card) => Inspect = card;
            public void HideInspect() => Inspect = null;
            public void Play(IReadOnlyList<VisualChangeEvent> events, bool reducedMotion) => Played.Add(events);

            private sealed class Landmark : ILandmarkView
            {
                private readonly Recorder _r;
                public Landmark(Recorder r) { _r = r; }
                public string LocationId => "lm.trailhead";
                public void Show(LandmarkPresentation landmark) => _r.Trailhead = landmark;
            }

            private sealed class View : ICharacterView
            {
                public View(string id) { CharacterId = id; }
                public string CharacterId { get; }
                public readonly List<CharacterReaction> Reactions = new List<CharacterReaction>();
                public void Show(CharacterPresentation character) { }
                public void React(CharacterReaction reaction) => Reactions.Add(reaction);
            }
        }
    }
}
