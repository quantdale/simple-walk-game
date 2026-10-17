# Design

## Placement and layering
- Files live in `src/WalkGame.Unity/Assets/Shell/Presentation/`, namespace `WalkGame.Shell.Presentation`, compiled into the existing `WalkGame.Shell` asmdef (no new assembly).
- The code uses only Application read models, `GameSession`, and Domain `DomainResult`/`ProjectStatus`. No `UnityEngine` type appears in the contracts, so the controller is testable without a scene.
- The asset resolver is generic over `TAsset : class`. A presenter instantiates it with `Sprite`, `GameObject` or whatever its direction uses.

## Controller flow
| Call | Reads | Writes | Presenter calls |
|---|---|---|---|
| `Refresh()` | `GetWorldView(memory.SeenLineIds)` | none | `ShowWorld(view)`, then `FindLandmark(id)?.Show(LandmarkPresentation)` per row (a null view is skipped) |
| `TapLandmark(id)` | current view, `GetRestorationChoices`, `GetCharacters` | none | Unknown or locked → `Ignored`, nothing shown. Otherwise `ShowChoices(id, choices whose LocationId == id)` and, if characters are present, `ShowDialogue` with each present character's current line |
| `AdvanceDialogue()` / `SkipDialogue()` | none | memory only (`MarkSeen`) | `ShowDialogue` / `HideDialogue`, then `Refresh` so pending markers clear |
| `ChooseRestorationGoal(pid)` | `GetRestorationChoices` | `GameSession.EnqueueProject(pid)` (unless already Queued/Active: Ok no-op) | `Play(WorldViewDiff.Between(before, after))` when non-empty; re-`ShowChoices` |
| `Resume()` | `GetVisualChangesSince(memory.ChangesShownThroughUtc)` | memory only | `Play(events, reducedMotion)` once, then `Refresh` |

- **Never blocking:** the controller never waits on a presenter, and no method checks dialogue state before acting. `IVisualChangePlayer.Play` is fire-and-forget.
- **Replay once:** the watermark `ChangesShownThroughUtc` is the latest canonical `AtUtc` already shown. `Resume` moves it to the latest replayed event. `ChooseRestorationGoal` moves it past any completion it caused, which it showed through the in-session diff. A null watermark (first open) is set to the latest existing event without playing anything.
- **Reduced motion** comes from `GetSettings().ReducedMotion` and is passed to the player.

## Asset resolution
`Resolve(key)` tries, in order:
1. the exact key (`lm.wetland@flourishing`);
2. the subject, meaning the text before `@` (`lm.wetland`);
3. `fallback.missing`;
4. `Missing` (asset null).

It never throws. Every non-exact request is added to `MissingKeys`, an ordinal sorted set that serves as the art pipeline's to-make list.

## Rejected
- **Driving presenters from `ShellController`:** that mixes the text shell with the new seam and risks its tests. The new controller stands alone and can be composed later.
- **Persisting seen/watermark in the save:** display-only state does not justify a schema v4 (D-165 rejected this as well).
- **Writing a separate presentation file next to the save:** `DeleteAllLocalData` would not erase it (privacy, PRODUCT_SPEC §7a). Deferred.
