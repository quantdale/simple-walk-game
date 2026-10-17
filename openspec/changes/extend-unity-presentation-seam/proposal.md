# Extend the Unity presentation seam: characters, taps and dialogue actions (KI-027 d)

**Status:** Approved for implementation by the executing agent as style-neutral groundwork (D-171 Phase D prerequisite, KI-027 (d), Phase A audit §4.5–§4.7). Michael asked for the KI-027 groundwork on 2026-10-17. It holds for any of the three round 2 concepts (all hand-painted 2.5D) and builds no UI, scene, sprite or animation. Region 1 only; Region 2 stays gated (D-011).

## Why

D-169 routes only `TapLandmark(locationId)` and its dialogue carries no actions. The Old Trailhead slice (bible §42 steps 3–6) needs:
- the NPC physically present, with an expression that follows progress (§13);
- a tap on the NPC that makes them react and opens one short line (§15);
- actions under the line: Inspect the bridge / Help restore it / Maybe later (§16);
- a tap on the damaged bridge itself to inspect it (§42.5);
- hit-testing that does not need a physics package, with decoration never capturing taps (§10).

## What Changes

- Application (read model only): `WorldViewReadModel.LocationRow.FocusProjectId`, the project a place is about right now (active here, else first selectable here, else last restored here, else none). Derived; no save change.
- Unity seam (`Assets/Shell/Presentation/PresentationInteractions.cs`):
  - `ICharacterPresenter` / `ICharacterView` with `CharacterPresentation` (location or absent, current line, expression, pending flag; keys `SpriteKey` `npc.<id>`, `MoodKey` `npc.<id>@<expression>`, `PortraitExpressionKey` `portrait.npc.<id>@<expression>`, all resolvable with the D-169 subject fallback) and `CharacterReaction` (`Tapped`, `Acknowledge`).
  - `IInspectPresenter` with a read-only `InspectCard` (place, variant key, project, status, progress, build stage, choice preview).
  - `DialogueAction` (`Inspect`, `HelpRestore`, `MaybeLater`) carried on `DialogueSequence.Actions`, labelled through `PlayerStrings` (`dialogue.action.*`).
  - `WorldTapRouter`: front-to-back hit areas grown to a minimum touch target, routing to landmark, character or prop taps.
- `WorldPresentationController`: `TapCharacter`, `TapProp`, `ChooseDialogueAction`, `Characters()`, character views pushed on every refresh, acknowledgement reactions when replayed changes give a character a new line. Optional constructor parameters, so D-169 callers are unchanged.

## Non-goals

Any visible UI, scene or prefab; wiring into `ShellBootstrap`/`Boot.unity`; art; persistence of presentation memory (separate change); per-project prop keys and bridge build stages (separate change); Region 2.

## Compatibility

- No save, schema (v3), content version, fingerprint, pacing or preference change. Actions change nothing canonical; the only mutation stays `ChooseRestorationGoal` → `GameSession.EnqueueProject` (D-169).
- `scripts/verify-architecture.sh` rule 7 holds.
- The text shell is untouched.
