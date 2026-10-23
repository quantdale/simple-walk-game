using UnityEngine.UIElements;
using WalkGame.Application.ReadModels;

namespace WalkGame.Shell.Presentation
{
    /// <summary>D-216: what <see cref="TrailheadWorldHost"/> needs from a painted place (the Old Trailhead, the River Intake).</summary>
    public interface IPaintedPlaceView : IWorldPresenter, ILandmarkView, ICharacterPresenter, ICharacterView, IDialoguePresenter
    {
        void Build(IPresentationAssetCatalog<string>? catalog = null);
        bool ReducedMotion { get; set; }
        float TextScale { get; set; }
        DialogueSequence? Dialogue { get; }
        void BindDialogue(VisualElement root, System.Action<DialogueAction> onAction, System.Action onSkip);
        /// <summary>The "Help restore it" reaction (no-op with reduced motion).</summary>
        void React();
        /// <summary>Jumps running crossfades to their end (captures, tests).</summary>
        void Settle();
    }
}
