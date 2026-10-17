using System;
using System.Collections.Generic;
using WalkGame.Application.ReadModels;

namespace WalkGame.Shell.Presentation
{
    // D-169 (OpenSpec add-unity-presentation-seam): style-neutral Unity adapter seam for the
    // D-166/D-167 read models. These interfaces name no art style, no sprite, prefab, layout or
    // animation. Whatever visual direction is approved (D-164) implements them;
    // until then only the null presenters exist and the text shell stays the shipped UI.

    /// <summary>Region-level presentation: draws the whole world view and hands out one view per place.</summary>
    public interface IWorldPresenter
    {
        /// <summary>Called on every refresh with the latest view (atmosphere key, arcs, completion).</summary>
        void ShowWorld(WorldViewReadModel view);

        /// <summary>The view for a location, or null when this presenter has none (the place is simply not drawn).</summary>
        ILandmarkView? FindLandmark(string locationId);
    }

    /// <summary>One place in the world, keyed by its D-166 location id and variant/construction keys.</summary>
    public interface ILandmarkView
    {
        string LocationId { get; }
        void Show(LandmarkPresentation landmark);
    }

    /// <summary>Character dialogue. It must never block anything: the controller keeps working while it is open.</summary>
    public interface IDialoguePresenter
    {
        void ShowDialogue(DialogueSequence sequence);
        void HideDialogue();
    }

    /// <summary>Restoration goals for one place with their D-167 previews.</summary>
    public interface IChoicePreviewPresenter
    {
        void ShowChoices(string locationId, IReadOnlyList<RestorationChoicesReadModel.Choice> choices);
        void HideChoices();
    }

    /// <summary>
    /// Plays visible transformations (D-167 events). Fire-and-forget: the controller never waits
    /// for playback, so an animation can never hold back progression. With reduced motion the
    /// player should show the end state without movement.
    /// </summary>
    public interface IVisualChangePlayer
    {
        void Play(IReadOnlyList<VisualChangeEvent> events, bool reducedMotion);
    }

    /// <summary>
    /// Presentation-held display memory (never the save, D-165): which dialogue lines were seen and
    /// up to which canonical instant visual changes have been shown.
    /// </summary>
    public interface IPresentationMemory
    {
        IReadOnlyCollection<string> SeenLineIds { get; }
        void MarkSeen(string lineId);
        /// <summary>Latest canonical change time already shown; null before the first open.</summary>
        DateTimeOffset? ChangesShownThroughUtc { get; set; }
    }

    /// <summary>In-memory <see cref="IPresentationMemory"/>. Lost on restart (see D-169 follow-ups).</summary>
    public sealed class InMemoryPresentationMemory : IPresentationMemory
    {
        private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);
        public IReadOnlyCollection<string> SeenLineIds => _seen;
        public void MarkSeen(string lineId) { if (!string.IsNullOrEmpty(lineId)) _seen.Add(lineId); }
        public DateTimeOffset? ChangesShownThroughUtc { get; set; }
    }

    /// <summary>What a landmark view needs to draw itself. Keys come straight from the D-166 read model.</summary>
    public sealed class LandmarkPresentation
    {
        public LandmarkPresentation(string locationId, string title, string variantKey, string? constructionKey,
            bool isUnlocked, bool isInteractable, bool hasPendingDialogue, IReadOnlyList<string> characterIds)
        {
            LocationId = locationId; Title = title; VariantKey = variantKey; ConstructionKey = constructionKey;
            IsUnlocked = isUnlocked; IsInteractable = isInteractable; HasPendingDialogue = hasPendingDialogue;
            CharacterIds = characterIds;
        }

        public string LocationId { get; }
        public string Title { get; }
        /// <summary>e.g. <c>lm.wetland@flourishing</c>; resolve through <see cref="PresentationAssetResolver{TAsset}"/>.</summary>
        public string VariantKey { get; }
        /// <summary>e.g. <c>proj.canopy-walkway@build-2of4</c>, or null.</summary>
        public string? ConstructionKey { get; }
        public bool IsUnlocked { get; }
        public bool IsInteractable { get; }
        public bool HasPendingDialogue { get; }
        public IReadOnlyList<string> CharacterIds { get; }

        internal static LandmarkPresentation From(WorldViewReadModel.LocationRow row) =>
            new LandmarkPresentation(row.LocationId, row.Title, row.VariantKey, row.ConstructionKey, row.IsUnlocked,
                row.IsInteractable, row.HasPendingDialogue, row.CharacterIds);
    }

    /// <summary>One line said by one character.</summary>
    public sealed class DialogueBeat
    {
        public DialogueBeat(string characterId, string name, string portraitKey, string lineId, string expression, string text)
        {
            CharacterId = characterId; Name = name; PortraitKey = portraitKey; LineId = lineId; Expression = expression; Text = text;
        }

        public string CharacterId { get; }
        public string Name { get; }
        /// <summary>e.g. <c>portrait.&lt;characterId&gt;</c>; combine with <see cref="Expression"/> for an expression variant.</summary>
        public string PortraitKey { get; }
        public string LineId { get; }
        public string Expression { get; }
        public string Text { get; }
    }

    /// <summary>The lines of the characters at a place, in order. Always skippable.</summary>
    public sealed class DialogueSequence
    {
        public DialogueSequence(string locationId, IReadOnlyList<DialogueBeat> beats)
        {
            LocationId = locationId;
            Beats = beats ?? throw new ArgumentNullException(nameof(beats));
        }

        public string LocationId { get; }
        public IReadOnlyList<DialogueBeat> Beats { get; }
        public int Index { get; internal set; }
        public bool IsFinished => Index >= Beats.Count;
        public DialogueBeat? Current => IsFinished ? null : Beats[Index];
        public bool CanSkip => true;
    }

    public enum LandmarkTapOutcome
    {
        /// <summary>Unknown or still-locked place: nothing opens.</summary>
        Ignored = 0,
        /// <summary>The place opened: its dialogue (if anyone is there) and its restoration goals.</summary>
        Opened = 1,
    }
}
