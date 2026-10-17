## 1. Read model
- [x] 1.1 `LocationRow.FocusProjectId` in `WorldPresentationProjector.WorldView`; .NET `WorldViewFocusTests` (fresh valley, clearing → bridge, whole-playthrough invariant, read-only).
## 2. Seam
- [x] 2.1 `PresentationInteractions.cs`: character presenter/view, presentation keys, reactions, inspect presenter/card, dialogue actions, tap router.
- [x] 2.2 `WorldPresentationController`: `TapCharacter`, `TapProp`, `ChooseDialogueAction`, `Characters()`, character refresh, acknowledgements; `NullPresentation` implements the new interfaces; `PlayerStrings` action labels.
## 3. Tests (`Assets/Tests/EditMode/WorldPresentationInteractionTests.cs`)
- [x] 3.1 Who is where, mood and portrait keys follow progress; key fallback to the character.
- [x] 3.2 Character tap: reaction, only their line, place actions, no forced goal list; absent/unknown ignored.
- [x] 3.3 Help restore opens that goal's card (save unchanged) and its start equals `EnqueueProject`; Inspect is read-only; Maybe later closes everything; actions not offered are refused; every action has a label.
- [x] 3.4 Bridge prop: ignored while locked, opens once open, shows progress while active.
- [x] 3.5 Talking, inspecting and declining never block walking progress and leave the save byte-identical.
- [x] 3.6 Acknowledgement once per replayed new line; tap router front-to-back, minimum target, routing.
## 4. Verify
- [x] 4.1 Unity 6000.3.26f1 batch EditMode green; `dotnet test` green; `scripts/verify-architecture.sh` green. `ProjectSettings.asset` reverted before commit.
