using System;
using System.Collections.Generic;
using WalkGame.Application;
using WalkGame.Application.ReadModels;
using WalkGame.Domain.Common;
using WalkGame.Domain.Projects;

namespace WalkGame.Shell.Presentation
{
    /// <summary>
    /// D-169 (OpenSpec add-unity-presentation-seam): drives the style-neutral presenter interfaces
    /// from the D-166/D-167 read models. It reads through <see cref="GameSession"/> read entry points
    /// and changes the game only through the existing Application command for picking a goal
    /// (<see cref="GameSession.EnqueueProject"/>, the same call the text shell makes). It never
    /// waits on a presenter, so dialogue and animations can never hold back progression.
    /// Not wired into the boot scene yet: the text shell (<see cref="ShellController"/>) stays the
    /// shipped UI until a visual direction is approved (D-164).
    /// </summary>
    public sealed class WorldPresentationController
    {
        private readonly GameSession _session;
        private readonly IWorldPresenter _world;
        private readonly IDialoguePresenter _dialogue;
        private readonly IChoicePreviewPresenter _choices;
        private readonly IVisualChangePlayer _changes;
        private readonly IPresentationMemory _memory;
        private readonly ICharacterPresenter? _characters;
        private readonly IInspectPresenter _inspect;

        /// <param name="characters">Character views (KI-027d); defaults to <paramref name="world"/> when it also implements <see cref="ICharacterPresenter"/>.</param>
        /// <param name="inspect">Inspect cards (KI-027d); defaults to <paramref name="choices"/> when it also implements <see cref="IInspectPresenter"/>, else draws nothing.</param>
        public WorldPresentationController(GameSession session, IWorldPresenter world, IDialoguePresenter dialogue,
            IChoicePreviewPresenter choices, IVisualChangePlayer changes, IPresentationMemory memory,
            ICharacterPresenter? characters = null, IInspectPresenter? inspect = null)
        {
            _characters = characters ?? world as ICharacterPresenter;
            _inspect = inspect ?? choices as IInspectPresenter ?? NullPresentation.Instance;
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _dialogue = dialogue ?? throw new ArgumentNullException(nameof(dialogue));
            _choices = choices ?? throw new ArgumentNullException(nameof(choices));
            _changes = changes ?? throw new ArgumentNullException(nameof(changes));
            _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        }

        /// <summary>The view last pushed to the presenters.</summary>
        public WorldViewReadModel? CurrentView { get; private set; }
        /// <summary>The place whose dialogue/choices are open, or null.</summary>
        public string? OpenLocationId { get; private set; }
        /// <summary>The open dialogue, or null when none is showing.</summary>
        public DialogueSequence? OpenDialogue { get; private set; }
        /// <summary>The inspect card showing, or null.</summary>
        public InspectCard? OpenInspect { get; private set; }

        /// <summary>Re-reads the world and pushes every place to its view (places without a view are skipped).</summary>
        public void Refresh()
        {
            var view = _session.GetWorldView(_memory.SeenLineIds);
            CurrentView = view;
            _world.ShowWorld(view);
            foreach (var row in view.Locations)
                _world.FindLandmark(row.LocationId)?.Show(LandmarkPresentation.From(row));
            if (_characters == null) return;
            var seen = new HashSet<string>(_memory.SeenLineIds, StringComparer.Ordinal);
            foreach (var c in _session.GetCharacters())
                _characters.FindCharacter(c.CharacterId)?.Show(CharacterPresentation.From(c, seen));
        }

        /// <summary>The characters as views see them (who is where, mood, pending line).</summary>
        public IReadOnlyList<CharacterPresentation> Characters()
        {
            var seen = new HashSet<string>(_memory.SeenLineIds, StringComparer.Ordinal);
            var list = new List<CharacterPresentation>();
            foreach (var c in _session.GetCharacters()) list.Add(CharacterPresentation.From(c, seen));
            return list;
        }

        /// <summary>
        /// A tap on a place. Unknown or locked places are ignored. Otherwise its restoration goals are
        /// shown at once, and the current line of everyone there opens as a skippable dialogue. The
        /// goals never wait for the dialogue.
        /// </summary>
        public LandmarkTapOutcome TapLandmark(string locationId)
        {
            var view = CurrentView ?? RefreshAndGet();
            var row = view.Find(locationId);
            if (row == null || !row.IsUnlocked) return LandmarkTapOutcome.Ignored;

            HideInspectOnly();
            OpenLocationId = row.LocationId;
            _choices.ShowChoices(row.LocationId, ChoicesAt(row.LocationId));

            var beats = BeatsAt(row, null);
            if (beats.Count > 0)
            {
                OpenDialogue = new DialogueSequence(row.LocationId, beats, ActionsAt(row));
                _dialogue.ShowDialogue(OpenDialogue);
            }
            else CloseDialogueOnly();
            return LandmarkTapOutcome.Opened;
        }

        /// <summary>
        /// KI-027(d), bible §15: a tap on a character. The character reacts, and their current line opens
        /// as a skippable dialogue with the place's actions (Inspect / Help restore / Maybe later). The
        /// world stays as it is: no goal list is forced open. An unknown or absent character, or one at a
        /// place that is not open yet, is ignored. Nothing here changes canonical state.
        /// </summary>
        public TapOutcome TapCharacter(string characterId)
        {
            var view = CurrentView ?? RefreshAndGet();
            CharacterDialogueReadModel? found = null;
            foreach (var c in _session.GetCharacters())
                if (string.Equals(c.CharacterId, characterId, StringComparison.Ordinal)) { found = c; break; }
            if (found?.LocationId == null || found.CurrentLine == null) return TapOutcome.Ignored;
            var row = view.Find(found.LocationId);
            if (row == null || !row.IsUnlocked) return TapOutcome.Ignored;

            _characters?.FindCharacter(found.CharacterId)?.React(CharacterReaction.Tapped);
            HideInspectOnly();
            _choices.HideChoices();
            OpenLocationId = row.LocationId;
            OpenDialogue = new DialogueSequence(row.LocationId, BeatsAt(row, found.CharacterId), ActionsAt(row));
            _dialogue.ShowDialogue(OpenDialogue);
            return TapOutcome.Opened;
        }

        /// <summary>
        /// KI-027(d): a tap on an inspectable prop, keyed by the project it belongs to (e.g. the damaged
        /// bridge, <c>proj.rebuild-trail-bridges</c>). Opens its inspect card when its place is open and the
        /// project is no longer locked; otherwise ignored. Read-only.
        /// </summary>
        public TapOutcome TapProp(string projectId)
        {
            var view = CurrentView ?? RefreshAndGet();
            var row = PlaceOf(view, projectId);
            if (row == null || !row.IsUnlocked || StatusOf(projectId) is null or ProjectStatus.Locked) return TapOutcome.Ignored;
            CloseDialogueOnly();
            _choices.HideChoices();
            OpenLocationId = row.LocationId;
            ShowInspectFor(row, projectId);
            return TapOutcome.Opened;
        }

        /// <summary>
        /// KI-027(d), bible §15–§16: takes one of the open dialogue's actions. Any action ends the talk
        /// (its lines count as seen and stay revisitable). Inspect opens the inspect card; Help restore opens
        /// the project card for that goal, whose start action is <see cref="ChooseRestorationGoal"/> (the
        /// existing <c>EnqueueProject</c> command); Maybe later returns to the world. Taking an action never
        /// changes canonical state by itself. Returns false when no dialogue offers this action.
        /// </summary>
        public bool ChooseDialogueAction(DialogueAction action)
        {
            var d = OpenDialogue;
            if (action == null || d == null || !Offers(d, action)) return false;
            foreach (var b in d.Beats) _memory.MarkSeen(b.LineId);
            CloseDialogueOnly();
            Refresh();
            var row = CurrentView!.Find(action.LocationId);
            switch (action.Kind)
            {
                case DialogueActionKind.Inspect:
                    if (row != null) { _choices.HideChoices(); OpenLocationId = row.LocationId; ShowInspectFor(row, action.ProjectId); }
                    break;
                case DialogueActionKind.HelpRestore:
                    var choice = action.ProjectId == null ? null : _session.GetRestorationChoices().Find(action.ProjectId);
                    HideInspectOnly();
                    OpenLocationId = action.LocationId;
                    _choices.ShowChoices(action.LocationId, choice == null
                        ? (IReadOnlyList<RestorationChoicesReadModel.Choice>)ChoicesAt(action.LocationId)
                        : new[] { choice });
                    break;
                default:
                    CloseLocation();
                    break;
            }
            return true;
        }

        /// <summary>Moves past the current line (marking it seen). Returns false when the dialogue is over (and closed).</summary>
        public bool AdvanceDialogue()
        {
            var d = OpenDialogue;
            if (d == null) return false;
            var current = d.Current;
            if (current != null) _memory.MarkSeen(current.LineId);
            d.Index++;
            if (d.IsFinished) { FinishDialogue(); return false; }
            _dialogue.ShowDialogue(d);
            return true;
        }

        /// <summary>Closes the dialogue at once. Every line in it counts as seen; all of them stay revisitable.</summary>
        public void SkipDialogue()
        {
            var d = OpenDialogue;
            if (d == null) return;
            foreach (var b in d.Beats) _memory.MarkSeen(b.LineId);
            FinishDialogue();
        }

        /// <summary>Every line a character has reached so far, for revisiting (D-165).</summary>
        public IReadOnlyList<DialogueBeat> LineHistory(string characterId)
        {
            foreach (var c in _session.GetCharacters())
            {
                if (!string.Equals(c.CharacterId, characterId, StringComparison.Ordinal)) continue;
                var beats = new List<DialogueBeat>(c.ReachedLines.Count);
                foreach (var l in c.ReachedLines) beats.Add(new DialogueBeat(c.CharacterId, c.Name, c.PortraitKey, l.LineId, l.Expression, l.Text));
                return beats;
            }
            return Array.Empty<DialogueBeat>();
        }

        /// <summary>Closes the place's dialogue, goals and inspect card.</summary>
        public void CloseLocation()
        {
            CloseDialogueOnly();
            HideInspectOnly();
            _choices.HideChoices();
            OpenLocationId = null;
        }

        /// <summary>
        /// Picks a restoration goal through the existing Application command (<see cref="GameSession.EnqueueProject"/>),
        /// exactly as the text shell does: a goal already queued or active is a no-op (D-128). Works whether or
        /// not a dialogue is open. Changes this causes right away (for example banked Vitality finishing it)
        /// are played once and then counted as shown, so a later <see cref="Resume"/> does not repeat them.
        /// </summary>
        public DomainResult ChooseRestorationGoal(string projectId)
        {
            var before = CurrentView ?? RefreshAndGet();
            var choice = _session.GetRestorationChoices().Find(projectId);
            if (choice != null && (choice.Status == ProjectStatus.Queued || choice.Status == ProjectStatus.Active))
                return DomainResult.Ok();

            var result = _session.EnqueueProject(projectId);
            if (!result.IsSuccess) return result;

            Refresh();
            var inSession = WorldViewDiff.Between(before, CurrentView!);
            if (inSession.Count > 0) { _changes.Play(inSession, ReducedMotion()); Acknowledge(inSession); }
            MarkChangesShownThroughLatest();
            if (OpenLocationId != null) _choices.ShowChoices(OpenLocationId, ChoicesAt(OpenLocationId));
            return result;
        }

        /// <summary>
        /// App opened or resumed. Replays, once, the canonical visual changes since the last time they
        /// were shown (D-167 stream, completion order), then refreshes. On the very first open there is
        /// nothing to compare against, so the current world is the baseline and nothing is replayed.
        /// Returns the number of events handed to the player.
        /// </summary>
        public int Resume()
        {
            int played = 0;
            var since = _memory.ChangesShownThroughUtc;
            if (since == null) MarkChangesShownThroughLatest();
            else
            {
                var events = _session.GetVisualChangesSince(since.Value);
                if (events.Count > 0)
                {
                    _changes.Play(events, ReducedMotion());
                    Acknowledge(events);
                    played = events.Count;
                    _memory.ChangesShownThroughUtc = Latest(events, since.Value);
                }
            }
            Refresh();
            return played;
        }

        private WorldViewReadModel RefreshAndGet() { Refresh(); return CurrentView!; }

        private void FinishDialogue()
        {
            CloseDialogueOnly();
            Refresh(); // pending markers clear now that the lines are seen
        }

        private void CloseDialogueOnly()
        {
            if (OpenDialogue != null) _dialogue.HideDialogue();
            OpenDialogue = null;
        }

        private bool ReducedMotion() => _session.GetSettings().ReducedMotion;

        private void HideInspectOnly()
        {
            if (OpenInspect != null) _inspect.HideInspect();
            OpenInspect = null;
        }

        /// <summary>Characters who got a new line or arrived react once (bible §13 acknowledgement). Fire-and-forget.</summary>
        private void Acknowledge(IReadOnlyList<VisualChangeEvent> events)
        {
            if (_characters == null) return;
            var done = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in events)
                if ((e.Kind == VisualChangeKind.DialogueAvailable || e.Kind == VisualChangeKind.CharacterArrived)
                    && e.SubjectId != null && done.Add(e.SubjectId))
                    _characters.FindCharacter(e.SubjectId)?.React(CharacterReaction.Acknowledge);
        }

        private static bool Offers(DialogueSequence d, DialogueAction action)
        {
            foreach (var a in d.Actions)
                if (ReferenceEquals(a, action) || (a.Kind == action.Kind && a.LocationId == action.LocationId && a.ProjectId == action.ProjectId))
                    return true;
            return false;
        }

        /// <summary>Inspect (focus project or the place itself), Help restore (when the focus goal can be picked or is underway), Maybe later.</summary>
        private IReadOnlyList<DialogueAction> ActionsAt(WorldViewReadModel.LocationRow row)
        {
            var actions = new List<DialogueAction>(3) { new DialogueAction(DialogueActionKind.Inspect, row.LocationId, row.FocusProjectId) };
            if (row.FocusProjectId != null && _session.GetRestorationChoices().Find(row.FocusProjectId) != null)
                actions.Add(new DialogueAction(DialogueActionKind.HelpRestore, row.LocationId, row.FocusProjectId));
            actions.Add(new DialogueAction(DialogueActionKind.MaybeLater, row.LocationId, null));
            return actions;
        }

        private void ShowInspectFor(WorldViewReadModel.LocationRow row, string? projectId)
        {
            string? title = null; ProjectStatus? status = null; long invested = 0, cost = 0;
            if (projectId != null)
                foreach (var p in _session.GetProjects().Projects)
                {
                    if (!string.Equals(p.ProjectId, projectId, StringComparison.Ordinal)) continue;
                    title = _session.ContentLocale.Resolve(p.ProjectId, WalkGame.Application.Content.ContentText.Fields.Title, p.TitleKey);
                    status = p.Status; invested = p.VitalityInvested; cost = p.VitalityCost;
                    break;
                }
            if (title == null) projectId = null;
            bool active = projectId != null && row.ActiveProjectId == projectId;
            OpenInspect = new InspectCard(row.LocationId, row.Title, row.VariantKey, projectId, title, status, invested, cost,
                active ? row.ConstructionStage : 0, active ? row.ConstructionStages : 1,
                projectId == null ? null : _session.GetRestorationChoices().Find(projectId));
            _inspect.ShowInspect(OpenInspect);
        }

        private static WorldViewReadModel.LocationRow? PlaceOf(WorldViewReadModel view, string projectId)
        {
            foreach (var l in view.Locations)
                foreach (var p in l.ProjectIds)
                    if (string.Equals(p, projectId, StringComparison.Ordinal)) return l;
            return null;
        }

        private ProjectStatus? StatusOf(string projectId)
        {
            foreach (var p in _session.GetProjects().Projects)
                if (string.Equals(p.ProjectId, projectId, StringComparison.Ordinal)) return p.Status;
            return null;
        }

        private void MarkChangesShownThroughLatest()
        {
            var floor = _memory.ChangesShownThroughUtc ?? DateTimeOffset.MinValue;
            _memory.ChangesShownThroughUtc = Latest(_session.GetVisualChangesSince(floor), floor);
        }

        private static DateTimeOffset Latest(IReadOnlyList<VisualChangeEvent> events, DateTimeOffset floor)
        {
            var latest = floor;
            foreach (var e in events) if (e.AtUtc.HasValue && e.AtUtc.Value > latest) latest = e.AtUtc.Value;
            return latest;
        }

        private IReadOnlyList<RestorationChoicesReadModel.Choice> ChoicesAt(string locationId)
        {
            var list = new List<RestorationChoicesReadModel.Choice>();
            foreach (var c in _session.GetRestorationChoices().Choices)
                if (string.Equals(c.LocationId, locationId, StringComparison.Ordinal)) list.Add(c);
            return list;
        }

        private IReadOnlyList<DialogueBeat> BeatsAt(WorldViewReadModel.LocationRow row, string? onlyCharacterId)
        {
            var beats = new List<DialogueBeat>();
            if (row.CharacterIds.Count == 0) return beats;
            var characters = _session.GetCharacters();
            foreach (var id in row.CharacterIds)
            {
                if (onlyCharacterId != null && !string.Equals(id, onlyCharacterId, StringComparison.Ordinal)) continue;
                foreach (var c in characters)
                {
                    if (!string.Equals(c.CharacterId, id, StringComparison.Ordinal)) continue;
                    var line = c.CurrentLine;
                    if (line != null) beats.Add(new DialogueBeat(c.CharacterId, c.Name, c.PortraitKey, line.LineId, line.Expression, line.Text));
                }
            }
            return beats;
        }
    }
}
