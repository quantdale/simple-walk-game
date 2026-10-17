using System.Collections.Generic;
using WalkGame.Application.ReadModels;

namespace WalkGame.Shell.Presentation
{
    /// <summary>
    /// D-169: a presenter that draws nothing. It lets the controller run headless (tests, or a build
    /// where only the text shell exists) and is the default until a visual direction is approved (D-164).
    /// </summary>
    public sealed class NullPresentation : IWorldPresenter, IDialoguePresenter, IChoicePreviewPresenter, IVisualChangePlayer,
        ICharacterPresenter, IInspectPresenter
    {
        public static readonly NullPresentation Instance = new NullPresentation();
        public void ShowWorld(WorldViewReadModel view) { }
        public ILandmarkView? FindLandmark(string locationId) => null;
        public void ShowDialogue(DialogueSequence sequence) { }
        public void HideDialogue() { }
        public void ShowChoices(string locationId, IReadOnlyList<RestorationChoicesReadModel.Choice> choices) { }
        public void HideChoices() { }
        public void Play(IReadOnlyList<VisualChangeEvent> events, bool reducedMotion) { }
        public ICharacterView? FindCharacter(string characterId) => null;
        public void ShowInspect(InspectCard card) { }
        public void HideInspect() { }
    }
}
