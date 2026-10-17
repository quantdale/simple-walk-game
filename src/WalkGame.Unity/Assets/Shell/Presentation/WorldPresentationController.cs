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

        public WorldPresentationController(GameSession session, IWorldPresenter world, IDialoguePresenter dialogue,
            IChoicePreviewPresenter choices, IVisualChangePlayer changes, IPresentationMemory memory)
        {
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

        /// <summary>Re-reads the world and pushes every place to its view (places without a view are skipped).</summary>
        public void Refresh()
        {
            var view = _session.GetWorldView(_memory.SeenLineIds);
            CurrentView = view;
            _world.ShowWorld(view);
            foreach (var row in view.Locations)
                _world.FindLandmark(row.LocationId)?.Show(LandmarkPresentation.From(row));
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

            OpenLocationId = row.LocationId;
            _choices.ShowChoices(row.LocationId, ChoicesAt(row.LocationId));

            var beats = BeatsAt(row);
            if (beats.Count > 0)
            {
                OpenDialogue = new DialogueSequence(row.LocationId, beats);
                _dialogue.ShowDialogue(OpenDialogue);
            }
            else CloseDialogueOnly();
            return LandmarkTapOutcome.Opened;
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

        /// <summary>Closes the place's dialogue and goals.</summary>
        public void CloseLocation()
        {
            CloseDialogueOnly();
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
            if (inSession.Count > 0) _changes.Play(inSession, ReducedMotion());
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

        private IReadOnlyList<DialogueBeat> BeatsAt(WorldViewReadModel.LocationRow row)
        {
            var beats = new List<DialogueBeat>();
            if (row.CharacterIds.Count == 0) return beats;
            var characters = _session.GetCharacters();
            foreach (var id in row.CharacterIds)
                foreach (var c in characters)
                {
                    if (!string.Equals(c.CharacterId, id, StringComparison.Ordinal)) continue;
                    var line = c.CurrentLine;
                    if (line != null) beats.Add(new DialogueBeat(c.CharacterId, c.Name, c.PortraitKey, line.LineId, line.Expression, line.Text));
                }
            return beats;
        }
    }
}
