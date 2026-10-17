using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Application.Development;
using WalkGame.Application.ReadModels;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Time;
using WalkGame.Shell;
using WalkGame.Shell.Presentation;

namespace WalkGame.Shell.Tests
{
    /// <summary>
    /// KI-027(d) (OpenSpec extend-unity-presentation-seam): character views, character and prop taps,
    /// dialogue actions and physics-free tap routing, with recording presenters (no art). Unity-editor-tested.
    /// </summary>
    public sealed class WorldPresentationInteractionTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);
        private const string Bridge = "proj.rebuild-trail-bridges";
        private const string Clear = "proj.clear-trailhead";
        private readonly List<string> _dirs = new List<string>();

        [TearDown] public void TearDown() { foreach (var d in _dirs) if (Directory.Exists(d)) Directory.Delete(d, true); _dirs.Clear(); }

        private string NewDir()
        {
            var d = Path.Combine(Path.GetTempPath(), "wg-seam2-" + Guid.NewGuid().ToString("N"));
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

        private static ProjectStatus StatusOf(GameSession s, string id) => s.GetProjects().Projects.Single(p => p.ProjectId == id).Status;

        /// <summary>Walks day by day until <paramref name="projectId"/> completes (or the cap).</summary>
        private static void WalkUntilDone(GameSession s, ManualClock clock, string projectId, int steps = 9000)
        {
            for (int d = 0; d < 60 && StatusOf(s, projectId) != ProjectStatus.Completed; d++)
            {
                var from = clock.UtcNow; clock.Advance(TimeSpan.FromDays(1));
                s.IngestFromSource(new SyntheticWalkingSource(steps), from, clock.UtcNow);
            }
            Assert.AreEqual(ProjectStatus.Completed, StatusOf(s, projectId));
        }

        private static string TrailheadSpeaker(WorldPresentationController c) =>
            c.Characters().First(ch => ch.LocationId == "lm.trailhead" && ch.CurrentLineId != null).CharacterId;

        // ---- character view: who is where, mood from progress -------------------------------

        [Test] public void Character_views_show_who_is_where_with_mood_and_portrait_keys_from_progress()
        {
            var clock = new ManualClock(T0);
            var s = Started(NewDir(), clock);
            var r = new Recorder();
            var c = Controller(s, r);
            c.Refresh();

            var rows = s.GetCharacters();
            Assert.AreEqual(rows.Count, r.Characters.Count, "every character reaches its view, present or not");
            foreach (var ch in rows)
            {
                var shown = r.Characters[ch.CharacterId].Last!;
                Assert.AreEqual(ch.LocationId, shown.LocationId);
                Assert.AreEqual(ch.IsPresent, shown.IsPresent);
                string expr = ch.CurrentLine?.Expression ?? CharacterPresentation.DefaultExpression;
                Assert.AreEqual(expr, shown.Expression);
                Assert.AreEqual(ch.CharacterId, shown.SpriteKey);
                Assert.AreEqual(ch.CharacterId + "@" + expr, shown.MoodKey);
                Assert.AreEqual(ch.PortraitKey + "@" + expr, shown.PortraitExpressionKey);
                Assert.AreEqual(ch.CurrentLine != null, shown.HasPendingLine, "nothing seen yet");
            }
            var who = TrailheadSpeaker(c);
            var moodBefore = r.Characters[who].Last!.MoodKey;
            Assert.IsTrue(rows.Any(ch => !ch.IsPresent), "some of the cast arrive later");

            Assert.IsTrue(s.EnqueueProject(Clear).IsSuccess);
            WalkUntilDone(s, clock, Clear);
            c.Refresh();
            var after = r.Characters[who].Last!;
            Assert.AreEqual(s.GetCharacters().Single(ch => ch.CharacterId == who).CurrentLine!.Expression, after.Expression);
            Assert.AreNotEqual(moodBefore, after.MoodKey, "restoring the trailhead moves the trailhead character's mood on");
        }

        [Test] public void Asset_resolver_falls_back_from_mood_and_expression_keys_to_the_character()
        {
            var catalog = new DictionaryAssetCatalog<string>().Add("npc.example", "SPRITE").Add("portrait.npc.example", "PORTRAIT");
            var res = new PresentationAssetResolver<string>(catalog);
            var p = new CharacterPresentation("npc.example", "Ex", "lm.trailhead", "portrait.npc.example", "line.x", "glad", true);
            Assert.AreEqual("SPRITE", res.Resolve(p.MoodKey).Asset);
            Assert.AreEqual("PORTRAIT", res.Resolve(p.PortraitExpressionKey).Asset);
            Assert.AreEqual(CharacterPresentation.DefaultExpression, new CharacterPresentation("a", "A", null, "portrait.a", null, "", false).Expression);
        }

        // ---- tap a character -----------------------------------------------------------------

        [Test] public void Tapping_a_character_reacts_and_opens_only_their_line_with_place_actions()
        {
            var s = Started(NewDir(), new ManualClock(T0));
            var r = new Recorder();
            var c = Controller(s, r);
            c.Refresh();
            var who = TrailheadSpeaker(c);

            Assert.AreEqual(TapOutcome.Opened, c.TapCharacter(who));
            CollectionAssert.AreEqual(new[] { CharacterReaction.Tapped }, r.Characters[who].Reactions);
            Assert.IsNotNull(r.Dialogue);
            Assert.IsTrue(r.Dialogue!.Beats.All(b => b.CharacterId == who));
            Assert.AreEqual(1, r.Dialogue.Beats.Count);
            Assert.IsNull(r.ChoicesLocation, "the world stays visible: no goal list is forced open");
            Assert.AreEqual("lm.trailhead", c.OpenLocationId);

            var focus = c.CurrentView!.Find("lm.trailhead")!.FocusProjectId;
            Assert.AreEqual(Clear, focus);
            CollectionAssert.AreEqual(new[]
            {
                new DialogueAction(DialogueActionKind.Inspect, "lm.trailhead", focus).ToString(),
                new DialogueAction(DialogueActionKind.HelpRestore, "lm.trailhead", focus).ToString(),
                new DialogueAction(DialogueActionKind.MaybeLater, "lm.trailhead", null).ToString(),
            }, r.Dialogue.Actions.Select(a => a.ToString()).ToArray());
        }

        [Test] public void Tapping_an_absent_or_unknown_character_opens_nothing()
        {
            var s = Started(NewDir(), new ManualClock(T0));
            var r = new Recorder();
            var c = Controller(s, r);
            c.Refresh();
            var absent = c.Characters().First(ch => !ch.IsPresent).CharacterId;
            Assert.AreEqual(TapOutcome.Ignored, c.TapCharacter(absent));
            Assert.AreEqual(TapOutcome.Ignored, c.TapCharacter("npc.nobody"));
            Assert.IsNull(r.Dialogue);
            Assert.IsEmpty(r.Characters[absent].Reactions);
        }

        // ---- dialogue actions ------------------------------------------------------------------

        [Test] public void Help_restore_opens_the_goal_card_and_its_start_is_EnqueueProject()
        {
            var a = Started(NewDir(), new ManualClock(T0)); var b = Started(NewDir(), new ManualClock(T0));
            var r = new Recorder();
            var c = Controller(a, r);
            c.TapCharacter(TrailheadSpeaker(Controller(a, new Recorder())));
            var help = r.Dialogue!.Actions.Single(x => x.Kind == DialogueActionKind.HelpRestore);
            var saved = a.ExportSaveEnvelope();

            Assert.IsTrue(c.ChooseDialogueAction(help));
            CollectionAssert.AreEqual(saved, a.ExportSaveEnvelope(), "opening the card changes nothing canonical");
            Assert.IsNull(c.OpenDialogue, "the talk ends");
            Assert.AreEqual(1, r.DialogueHidden);
            Assert.AreEqual("lm.trailhead", r.ChoicesLocation);
            CollectionAssert.AreEqual(new[] { Clear }, r.Choices.Select(x => x.ProjectId).ToArray(), "the card is that one goal");
            Assert.IsFalse(c.CurrentView!.Find("lm.trailhead")!.HasPendingDialogue, "lines count as seen");

            Assert.IsTrue(c.ChooseRestorationGoal(help.ProjectId!).IsSuccess);
            Assert.IsTrue(b.EnqueueProject(Clear).IsSuccess);
            CollectionAssert.AreEqual(b.ExportSaveEnvelope(), a.ExportSaveEnvelope());
        }

        [Test] public void Inspect_opens_a_read_only_card_and_maybe_later_returns_to_the_world()
        {
            var s = Started(NewDir(), new ManualClock(T0));
            var r = new Recorder();
            var c = Controller(s, r);
            c.TapLandmark("lm.trailhead");
            var inspect = r.Dialogue!.Actions.Single(x => x.Kind == DialogueActionKind.Inspect);
            var saved = s.ExportSaveEnvelope();

            Assert.IsTrue(c.ChooseDialogueAction(inspect));
            Assert.IsNotNull(r.Inspect);
            Assert.AreEqual("lm.trailhead", r.Inspect!.LocationId);
            Assert.AreEqual("lm.trailhead@ruined", r.Inspect.LandmarkVariantKey);
            Assert.AreEqual(Clear, r.Inspect.ProjectId);
            Assert.AreEqual(ProjectStatus.Available, r.Inspect.ProjectStatus);
            Assert.IsNotNull(r.Inspect.Choice, "a pickable goal carries its preview");
            Assert.IsFalse(string.IsNullOrEmpty(r.Inspect.ProjectTitle));
            Assert.IsNull(r.ChoicesLocation, "inspecting replaces the goal list");
            CollectionAssert.AreEqual(saved, s.ExportSaveEnvelope());

            Assert.IsFalse(c.ChooseDialogueAction(inspect), "the talk is over: its actions are gone");
            c.TapCharacter(TrailheadSpeaker(c));
            var later = r.Dialogue!.Actions.Single(x => x.Kind == DialogueActionKind.MaybeLater);
            Assert.IsFalse(c.ChooseDialogueAction(new DialogueAction(DialogueActionKind.Inspect, "lm.wetland", null)), "not offered by the open talk");
            Assert.IsNotNull(c.OpenDialogue, "a refused action leaves the talk open");
            Assert.IsTrue(c.ChooseDialogueAction(later));
            Assert.IsNull(c.OpenDialogue); Assert.IsNull(c.OpenInspect); Assert.IsNull(c.OpenLocationId);
            Assert.IsNull(r.ChoicesLocation);
            CollectionAssert.AreEqual(saved, s.ExportSaveEnvelope());
        }

        [Test] public void Every_action_has_a_player_string()
        {
            foreach (DialogueActionKind k in Enum.GetValues(typeof(DialogueActionKind)))
            {
                var key = new DialogueAction(k, "lm.trailhead", null).LabelKey;
                Assert.IsTrue(PlayerStrings.Has(key), key);
                Assert.IsNotEmpty(PlayerStrings.T(key));
            }
        }

        // ---- inspectable props -----------------------------------------------------------------

        [Test] public void The_bridge_prop_opens_once_its_project_is_open_and_shows_progress()
        {
            var clock = new ManualClock(T0);
            var s = Started(NewDir(), clock);
            var r = new Recorder();
            var c = Controller(s, r);
            c.Refresh();
            Assert.AreEqual(TapOutcome.Ignored, c.TapProp(Bridge), "the bridge project is still locked");
            Assert.AreEqual(TapOutcome.Ignored, c.TapProp("proj.nope"));
            Assert.IsNull(r.Inspect);

            Assert.IsTrue(s.EnqueueProject(Clear).IsSuccess);
            WalkUntilDone(s, clock, Clear);
            c.Refresh();
            Assert.AreEqual(Bridge, c.CurrentView!.Find("lm.trailhead")!.FocusProjectId, "bible §16: the trailhead is now about the bridge");
            Assert.AreEqual(TapOutcome.Opened, c.TapProp(Bridge));
            Assert.AreEqual(Bridge, r.Inspect!.ProjectId);
            Assert.AreEqual(StatusOf(s, Bridge), r.Inspect.ProjectStatus);
            Assert.AreNotEqual(ProjectStatus.Locked, r.Inspect.ProjectStatus);

            Assert.IsTrue(c.ChooseRestorationGoal(Bridge).IsSuccess);
            var from = clock.UtcNow; clock.Advance(TimeSpan.FromHours(6));
            s.IngestFromSource(new SyntheticWalkingSource(3000), from, clock.UtcNow);
            c.Refresh();
            Assume.That(StatusOf(s, Bridge), Is.EqualTo(ProjectStatus.Active));
            c.TapProp(Bridge);
            Assert.AreEqual(ProjectStatus.Active, r.Inspect!.ProjectStatus);
            Assert.Greater(r.Inspect.VitalityInvested, 0L);
            Assert.AreEqual(s.GetProjects().Projects.Single(p => p.ProjectId == Bridge).VitalityCost, r.Inspect.VitalityCost);
            Assert.AreEqual(c.CurrentView!.Find("lm.trailhead")!.ConstructionStages, r.Inspect.ConstructionStages);
        }

        // ---- dialogue never blocks progression -------------------------------------------------

        [Test] public void Talking_inspecting_and_declining_never_block_progress_or_change_the_save()
        {
            var clockA = new ManualClock(T0); var clockB = new ManualClock(T0);
            var a = Started(NewDir(), clockA); var b = Started(NewDir(), clockB);
            var r = new Recorder();
            var c = Controller(a, r);
            Assert.IsTrue(a.EnqueueProject(Clear).IsSuccess); Assert.IsTrue(b.EnqueueProject(Clear).IsSuccess);

            c.TapCharacter(TrailheadSpeaker(c));            // left open while walking
            Assume.That(c.OpenDialogue, Is.Not.Null);
            WalkUntilDone(a, clockA, Clear);
            WalkUntilDone(b, clockB, Clear);
            Assert.IsNotNull(c.OpenDialogue, "the talk did not hold anything back and is still open");

            c.ChooseDialogueAction(c.OpenDialogue!.Actions.First(x => x.Kind == DialogueActionKind.Inspect));
            c.TapProp(Bridge); c.TapLandmark("lm.trailhead");
            c.ChooseDialogueAction(c.OpenDialogue!.Actions.First(x => x.Kind == DialogueActionKind.MaybeLater));
            c.TapCharacter(TrailheadSpeaker(c)); c.SkipDialogue(); c.Resume();
            CollectionAssert.AreEqual(b.ExportSaveEnvelope(), a.ExportSaveEnvelope());
        }

        [Test] public void A_character_acknowledges_a_new_line_when_changes_are_replayed()
        {
            var clock = new ManualClock(T0);
            var s = Started(NewDir(), clock);
            var r = new Recorder();
            var c = Controller(s, r);
            c.Resume();
            var who = TrailheadSpeaker(c);
            Assert.IsTrue(s.EnqueueProject(Clear).IsSuccess);
            WalkUntilDone(s, clock, Clear);
            Assume.That(c.Resume(), Is.GreaterThan(0));
            CollectionAssert.Contains(r.Characters[who].Reactions, CharacterReaction.Acknowledge);
            r.Characters[who].Reactions.Clear();
            c.Resume();
            Assert.IsEmpty(r.Characters[who].Reactions, "acknowledged once, not on every open");
        }

        // ---- physics-free tap routing ----------------------------------------------------------

        [Test] public void Tap_router_resolves_front_to_back_with_a_minimum_touch_target()
        {
            var router = new WorldTapRouter(56f);
            router.Add(new WorldHitArea(WorldTapTargetKind.Landmark, "lm.trailhead", 0, 0, 400, 400, 0));
            router.Add(new WorldHitArea(WorldTapTargetKind.Prop, Bridge, 100, 100, 120, 60, 5));
            router.Add(new WorldHitArea(WorldTapTargetKind.Character, "npc.x", 150, 110, 20, 20, 10)); // tiny, in front
            router.Add(new WorldHitArea(WorldTapTargetKind.Character, "", 0, 0, 999, 999, 99));       // no id: never registered

            Assert.AreEqual("npc.x", router.HitTest(160, 120)!.Value.Id);
            Assert.AreEqual("npc.x", router.HitTest(160 + 26, 120)!.Value.Id, "grown to 56 around its centre");
            Assert.AreEqual(Bridge, router.HitTest(160 + 30, 120)!.Value.Id);
            Assert.AreEqual("lm.trailhead", router.HitTest(10, 10)!.Value.Id);
            Assert.IsNull(router.HitTest(500, 500));
            Assert.AreEqual(3, router.Areas.Count);
        }

        [Test] public void Tap_router_forwards_to_the_controller()
        {
            var s = Started(NewDir(), new ManualClock(T0));
            var r = new Recorder();
            var c = Controller(s, r);
            c.Refresh();
            var router = new WorldTapRouter(0f);
            router.Add(new WorldHitArea(WorldTapTargetKind.Landmark, "lm.trailhead", 0, 0, 100, 100, 0));
            router.Add(new WorldHitArea(WorldTapTargetKind.Character, TrailheadSpeaker(c), 10, 10, 10, 10, 1));
            router.Add(new WorldHitArea(WorldTapTargetKind.Prop, Bridge, 50, 50, 10, 10, 1));
            router.Add(new WorldHitArea(WorldTapTargetKind.Landmark, "lm.wetland", 200, 0, 50, 50, 0));

            Assert.AreEqual(TapOutcome.Opened, router.Route(c, 15, 15));
            Assert.IsNull(r.ChoicesLocation, "a character tap");
            Assert.AreEqual(TapOutcome.Opened, router.Route(c, 80, 80));
            Assert.AreEqual("lm.trailhead", r.ChoicesLocation, "a landmark tap");
            Assert.AreEqual(TapOutcome.Ignored, router.Route(c, 55, 55), "the bridge is still locked");
            Assert.AreEqual(TapOutcome.Ignored, router.Route(c, 210, 10), "a locked place");
            Assert.AreEqual(TapOutcome.Ignored, router.Route(c, 999, 999), "nothing there");
        }

        // ---- recording presenter -------------------------------------------------------------

        private sealed class Recorder : IWorldPresenter, IDialoguePresenter, IChoicePreviewPresenter, IVisualChangePlayer,
            ICharacterPresenter, IInspectPresenter
        {
            public readonly Dictionary<string, CharacterRecorder> Characters = new Dictionary<string, CharacterRecorder>();
            public DialogueSequence? Dialogue;
            public int DialogueHidden;
            public string? ChoicesLocation;
            public IReadOnlyList<RestorationChoicesReadModel.Choice> Choices = Array.Empty<RestorationChoicesReadModel.Choice>();
            public InspectCard? Inspect;
            public readonly List<IReadOnlyList<VisualChangeEvent>> Played = new List<IReadOnlyList<VisualChangeEvent>>();

            public void ShowWorld(WorldViewReadModel view) { }
            public ILandmarkView? FindLandmark(string locationId) => null;
            public ICharacterView? FindCharacter(string characterId)
            {
                if (!Characters.TryGetValue(characterId, out var v)) Characters[characterId] = v = new CharacterRecorder(characterId);
                return v;
            }
            public void ShowDialogue(DialogueSequence sequence) => Dialogue = sequence;
            public void HideDialogue() { DialogueHidden++; Dialogue = null; }
            public void ShowChoices(string locationId, IReadOnlyList<RestorationChoicesReadModel.Choice> choices) { ChoicesLocation = locationId; Choices = choices; }
            public void HideChoices() { ChoicesLocation = null; Choices = Array.Empty<RestorationChoicesReadModel.Choice>(); }
            public void ShowInspect(InspectCard card) => Inspect = card;
            public void HideInspect() => Inspect = null;
            public void Play(IReadOnlyList<VisualChangeEvent> events, bool reducedMotion) => Played.Add(events);
        }

        private sealed class CharacterRecorder : ICharacterView
        {
            public CharacterRecorder(string id) { CharacterId = id; }
            public string CharacterId { get; }
            public CharacterPresentation? Last;
            public readonly List<CharacterReaction> Reactions = new List<CharacterReaction>();
            public void Show(CharacterPresentation character) => Last = character;
            public void React(CharacterReaction reaction) => Reactions.Add(reaction);
        }
    }
}
