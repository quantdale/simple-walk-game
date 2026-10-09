## Why

The headless core can already answer UX_DESIGN §3's three glance questions as facts, and nothing turns those facts into the sentences a player would read. `tools/simulation/Out.HomeLine` prints `attention=QueueEmptyWithBankedVitality(True)` and project ids. `ReturnSummaryComposer` is the only player prose, and a landmark advance is stored as `"{TitleKey} has reached {RestorationStage}."` — the round-trip fixture pins `Old Trailhead has reached Stabilized.` Unity screens remain externally blocked (KI-002, D-035), so a pixel shell cannot close this gap. A read-time glance projection can, and can be proven without an editor.

## What Changes

- Add a deterministic, side-effect-free player glance projection that answers, in calm English sentences: what changed, what is being restored now, and whether the player must do anything. One primary next action, or an explicit nothing-to-do.
- Map the existing attention, activity, and onboarding classifications onto those sentences. Do not invent a second attention policy.
- Stop newly composed landmark-stage summary lines from using the `has reached {RestorationStage}` form. Historical stored text is not rewritten.
- Add a simulation `glance` verb that prints only the projection. Existing evidence lines (`HomeLine`, `dump`, `profile`, `validate`, pacing reports) stay byte-compatible.

No breaking save-schema change. No change to crediting, pacing numbers, or attention classification.

## Capabilities

### New Capabilities

- `player-glance`: the read-time contract that turns Home, return-summary, activity-status, and onboarding facts into bounded player sentences, including the rule for newly composed landmark-stage lines and the guarantee that raw exceptions, health payloads, and stage-enum identifiers do not appear in those sentences.

### Modified Capabilities

- None. `openspec/specs/` is empty. `return-summary` (introduced by `rotate-durable-return-summary`) keeps its bounds, rotation, acknowledgement, and attention-currency requirements. This change adds a sentence rule beside that capability rather than revising it. `post-completion` attention behavior (D-061) is reused, not redefined.

## Impact

**Evidence**

- `docs/UX_DESIGN.md` §3 requires the three questions in a few seconds; §4 requires transformation before raw numbers; §12 forbids streak-shame and body-metric copy; §17 requires empty states to name a next action.
- `src/WalkGame.Application/ReadModels/HomeReadModel.cs` — `HomeAttentionReason` already distinguishes `PendingReturnSummary`, `QueueEmptyWithBankedVitality`, `NoProjectStartedYet`, and `RegionComplete` (`RequiresAttention` is false for the last). `GetHome()` in `GameSession.cs` derives that classification. The projection must consume it, not re-decide it.
- `src/WalkGame.Application/Summaries/ReturnSummaryComposer.cs` — `LandmarkStageReached` appends `Title(...) + " has reached {stage}."`. `Title()` returns `TitleKey` or the id. `DerivePrimaryNextAction` already returns `"Queue the next restoration project."` or null, and returns null once the region is complete.
- `src/WalkGame.Application/Activity/ActivityStatusProjector.cs` — `ActivityPlayerStatus` and `ActivityRecommendedAction` are the §5 states. The no-leak sweep is `PlayerFacingSurfaces_NeverLeakRawExceptionTextOrHealthPayloads` in `tests/WalkGame.Application.Tests/M5H1ContractHardeningTests.cs`.
- `src/WalkGame.Application/ReadModels/OnboardingReadModel.cs` — `OnboardingNextAction` is the ordered §11 sequence, including denial that stays navigable.
- `src/WalkGame.Application/Content/Region1Catalog.cs` — display titles are already English (`"Old Trailhead"`, `"Millbrook Valley"`) stored in `TitleKey`. Projects already carry `descriptionKey`. Landmark stages do not.
- `tools/simulation/Out.cs` `HomeLine` / `HomeBlock` omit `PrimaryNextAction`, banked vitality, summary text, and activity status.
- `tests/WalkGame.Infrastructure.Tests/SaveCodecRoundtripTests.cs` stores `Text = "Old Trailhead has reached Stabilized."` and must still round-trip that historical sentence unchanged.
- Refero MCP (metadata only, not a visual audit) returned streak, weekly-chart, and equal-weight stat homes (Clearful, Atoms, Train Fitness, Roots) and permission flows that explain the benefit, offer deferral, then the system sheet (Haptic flow 2128, Miles flow 6704). Those confirm UX_DESIGN's rejection of a fitness dashboard. They are not a layout to copy.

**Affected code**

- New application projection and read model beside the existing read models. Domain stays free of copy and engines.
- `ReturnSummaryComposer` landmark-stage line for newly composed items only.
- `tools/simulation` — new `glance` verb only.
- Application tests for the three questions, the five attention reasons, the six activity statuses, onboarding denial, and the no-leak sweep extended to the new model.
- `docs/UX_DESIGN.md` — record that the glance sentences are the headless binding, and that Unity rendering remains UNVERIFIED.

**Compatibility**

- No schema bump and no `ISaveMigration`. Glance sentences are not canonical state.
- Already-stored summary items, including the pinned `Old Trailhead has reached Stabilized.` fixture, are not rewritten. A shell that renders raw historical summary text may still show the old form until that item rotates or is acknowledged. The glance projection must not echo that form.
- Newly composed landmark-stage lines change. Tests that compose a fresh `LandmarkStageReached` and assert the old sentence must be updated to the new sentence, not deleted.
- Pacing evidence, save-growth evidence, and content version 3 stay untouched. This change does not depend on `compact-activity-and-reward-ledgers` and must not be mixed with it.

**Assumptions**

- English sentences are the MVP copy. No localization catalog and no split of `TitleKey` into a stable id plus a display string. That split would rewrite saved summary text and is deferred.
- Stage phrases are a fixed application table, not new authored content fields, so the content graph and its evidence hashes do not change. The table is an assumption pinned by tests: Ruined → "is still damaged"; Stabilized → "is safe to approach"; Functional → "is working again"; Restored → "is restored"; Flourishing → "is thriving". Example: `Old Trailhead is safe to approach.`
- A headless projection is the whole of this change. It is not M5 completion.

**Out of scope**

- Unity scenes, prefabs, screen-reader runtime, reduced-motion rendering, notification delivery, OS permission dialogs, and device verification (KI-002).
- Restyling `HomeLine`, `dump`, `profile`, `validate`, or pacing output.
- Changing attention precedence, summary bounds, rotation, acknowledgement, conversion, or ledger retention.
- A streak, step-goal, weekly-chart, or equal-weight card dashboard.
- App-layer save encryption (KI-006). That remains a product decision.
