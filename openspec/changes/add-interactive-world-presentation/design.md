# Design

## Layering
Everything lives in `src/WalkGame.Application` (references Domain only; `scripts/verify-architecture.sh`). The pieces:
- Pure projectors: `ReadModels/WorldPresentationProjector.cs`, a static class taking `(RegionDefinition, GameState, ContentText, …)`.
- Read-model types: `ReadModels/WorldViewReadModel.cs`, `ReadModels/CharacterDialogueReadModel.cs`, `ReadModels/RestorationChoicesReadModel.cs`, `ReadModels/VisualChangeEvent.cs`.
- Presentation content: `Content/Region1WorldLayout.cs`, `Content/Region1Characters.cs`.
- `GameSession` gets thin read-only entry points that call `RequireState()` and the projector.

No Domain change. No new persisted field.

## Canonical sources (reuse, no parallel path)
| Fact | Source |
|---|---|
| Landmark stage | `RegionState.LandmarkStages` (absent = `Ruined`), set only by `OfflineAdvancer.ApplyCompletionEffects` (monotonic max) |
| Project status / progress / completion time | `RegionState.Projects` (`ProjectState.Status`, `VitalityInvested`, `CompletedAtUtc`) |
| Construction stage | `ProjectDefinition.BuildStageFor(invested)` / `VisibleBuildStages` (D-071) |
| Arcs | `RegionState.EcologyStage` / `SettlementStage`; `RegionDefinition.EcologyProgression` / `SettlementProgression` |
| Discoveries | `RegionState.Discoveries`; `DiscoveryDefinition.UnlockedByProjectId` |
| Chapters | `StoryProgress.Evaluate` (D-076) |
| Opened projects | prerequisite rule of `OfflineAdvancer.UpdateAvailability` |
| Producers | `ProducerDefinition.UnlockedByProjectId` |
| Affordability | `OfflineAdvancer.CanAffordRequirements` |

## Keys (style-neutral)
- Variant: `<locationId>@<stage>`, where stage is the lowercase `RestorationStage` name, e.g. `lm.wetland@flourishing`. An art pipeline maps keys to whatever assets the approved direction produces. No key encodes a style.
- Construction: `<projectId>@build-<k>of<n>` for an incomplete Active project with `VisibleBuildStages > 1`; otherwise null.
- Atmosphere: `region@eco<e>-set<s>`.
- Portrait: `portrait.<characterId>`. Expression: one of `neutral`, `glad`, `wistful`, `curious`, `proud`, `worried`.

## Location rules
- A location's projects are the projects of the chain(s) bound to it by `Region1WorldLayout`.
- **Unlocked:** stage > Ruined, or any of its projects is not `Locked`.
- **Selectable goals:** its projects with status Available or Queued.
- **Interactable:** unlocked and (a selectable goal, an Active project here, a character present, or a found discovery here).
- **Discoveries here:** found discoveries whose unlocking project is bound to this location.

## Characters and dialogue
- `CharacterDefinition(id, name, portraitKey, appearances[], lines[])`.
  - An appearance is (afterProjectId or null for baseline, locationId). The current location is the last appearance whose milestone is completed. A character with no reached appearance is absent.
  - A line is (id, order, milestoneProjectId or null, expression, English text ≤ 140).
- **Reached lines:** milestone null or completed. **Current line:** the reached line with the highest order. A line is never reached while its character is absent.
- **Localization:** `ContentText.Resolve(lineId, "line", english)` and `Resolve(characterId, "name", english)`, adding `ContentText.Fields.Line` and `Fields.Name`.
- **Pending:** `seenLineIds` is not null and does not contain the current line id, or `seenLineIds` is null and a current line exists. A null set means "nothing seen yet", so every present character has a pending line.
- **Never gating:** the dialogue model is reachable only from read entry points. Validation tests check every character's milestones, locations and line lengths. Reading dialogue leaves `GameState` byte-identical (encoded save unchanged).

## Choice preview
For project X (Available/Queued/Active), the preview simulates only completion effects, on copies of small values, never on `GameState`:
- **Landmark stages:** for each landmark, `max(current, stages where UnlockedByProjectId == X)`. Emit a consequence when it rises. `Before`/`After` variant keys are taken from X's own location.
- **Opened projects:** currently Locked projects whose prerequisites are all completed once X counts as completed.
- **Producers:** not yet unlocked, `UnlockedByProjectId == X`.
- **Arcs:** the highest stage > current with `UnlockedByProjectId == X`.
- **Discovery:** not found, `UnlockedByProjectId == X`. Exposes the location only.
- **Characters:** a first appearance whose milestone is X (arrival) or a later appearance (move); lines whose milestone is X.
- **Chapters:** locked, `MinCareCycles == 0`, X among the required projects, and every other required project completed.
- **Closure milestone:** `X == CompletionMilestoneProjectId`. The flag says the milestone only; the closure criterion's chain minimum (D-159) still decides.

Effects of opened projects (second-order) are not chained; the preview shows only X's direct consequences. Output order is content order throughout, so previews are deterministic.

## Visual-change stream
- **`GetVisualChangesSince(since)`:** order completed projects by (`CompletedAtUtc`, content index). Replay their effects from baseline (Ruined, arcs 0, characters at baseline appearance). At each completion with `CompletedAtUtc > since`, emit events with `AtUtc` and a monotonically increasing `Sequence`. Completed projects with a null `CompletedAtUtc` (legacy) are applied to the baseline and never emitted.
  - **Replay check:** after replay, every landmark stage equals `RegionState.LandmarkStages` and both arcs equal state.
  - If the replay disagrees, for example on an imported legacy save, the stream falls back to no events. It is never wrong in the other direction (never invent a change).
  - `RegionCompleted` is emitted at `RegionCompletedAtUtc` when `> since`.
- **`WorldViewDiff.Between(before, after)`:** per location, emits unlocked, stage up (from/to keys), construction advanced (from/to construction keys, only within the same project and with the same or a higher build stage), character arrived/moved, and dialogue available (current line changed). Also emits arc steps and region completion. Stage regressions are never emitted (stages are monotonic). Diffing a view against itself yields nothing.

## Rejected
- **Persisting "seen dialogue" in the save:** this needs a schema v4 + migration for display-only state. The brief requires revisitable, non-gating dialogue, which a presentation-held seen set satisfies.
- **Parsing return-summary sentences for events:** they are English display text (D-138). Timestamps are canonical.
- **Putting locations on `ProjectDefinition`:** that changes Domain content and the content fingerprint. The binding is presentation data.
