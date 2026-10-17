# Unity presentation seam (style-neutral adapter interfaces)

**Status:** Approved for implementation by the executing agent as style-neutral groundwork under D-164. Michael asked for it on 2026-10-17, while the visual direction is still being settled (D-164). It locks in no art style, builds no UI, scene, prefab, sprite or animation, and does not replace or rewire the text shell. Region 1 only; Region 2 stays gated (D-011).

## Why

`add-interactive-world-presentation` (D-165–D-167) gives the Application layer engine-agnostic read models: `GameSession.GetWorldView`, `GetCharacters`, `GetRestorationChoices`, `GetVisualChangesSince` and `WorldViewDiff.Between`. Its tasks say the Unity binding "waits for the art-direction checkpoint". Whatever visual direction is approved, it needs the same Unity-side flow:
- tap a place to get its dialogue and goals;
- skip dialogue;
- pick a goal through the existing command;
- replay what changed since the last open;
- resolve keys to whatever assets exist.

That flow can be defined and tested now behind interfaces, with no art. Once a direction is approved, only the presenters need writing.

## What Changes

- New Unity folder `src/WalkGame.Unity/Assets/Shell/Presentation/` (in the existing `WalkGame.Shell` assembly):
  - Interfaces `IWorldPresenter`, `ILandmarkView` (keyed by D-166 location id, variant key and construction key), `IDialoguePresenter`, `IChoicePreviewPresenter`, `IVisualChangePlayer` and `IPresentationMemory`.
  - Style-neutral value types: `LandmarkPresentation`, `DialogueBeat`, `DialogueSequence`.
  - `WorldPresentationController`, which drives the interfaces from the read models.
  - `NullPresentation`, a presenter that draws nothing, and `InMemoryPresentationMemory`.
  - `PresentationAssetResolver<TAsset>` over a pluggable `IPresentationAssetCatalog<TAsset>`. The fallback chain is exact → subject → `fallback.missing` → Missing, with a missing-key list. `DictionaryAssetCatalog` and `EmptyAssetCatalog` are provided, and no assets ship.
- EditMode tests in `Assets/Tests/EditMode/WorldPresentationSeamTests.cs`.

## Non-goals

Any visible UI or scene change; wiring the controller into `ShellBootstrap` or `Boot.unity`; sprites, prefabs, portraits or animation; choosing a style; persisting presentation memory across restarts (see Compatibility); new gameplay rules; Region 2.

## Compatibility

- No save, schema (v3), content, fingerprint, pacing or preference-file change. The controller mutates only through `GameSession.EnqueueProject`, the same command `ShellController.Enqueue` uses, with the same D-128 no-op for an already queued or active goal.
- `scripts/verify-architecture.sh` rule 7 still holds: no canonical state types are named and no Infrastructure adapters are constructed.
- The text shell is untouched, so every existing EditMode/PlayMode test keeps passing.
- Presentation memory is in-memory only. A cold start therefore takes the current world as the baseline and replays nothing. Persisting it needs a home that `GameSession.DeleteAllLocalData` erases (for example the UX preferences file). That is a follow-up with its own decision, not this change.
- Depends on D-165 (contract), D-166 (world view, dialogue) and D-167 (choices, change stream).
