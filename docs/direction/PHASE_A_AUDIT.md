# Phase A audit: presentation against the Creative Direction Bible v1 (D-173)

**Date:** 2026-10-17, Asia/Manila. **Base:** `origin/main` at `0c46f4c` (after D-169, D-171 and D-172).
**Scope:** bible §39. This is a review only: no UI rebuild, no art, no scene change. The bible is `docs/direction/CREATIVE_BIBLE_v1_2026-10-17.md`, and section numbers (§) below refer to it.
**Verification labels:** every statement about the current build is from reading code, tests and committed **editor-rendered** captures (`docs/evidence/screen-tour/*-editor-rendered.png`). Nothing here is device-tested (KI-002).

## 1. Verdict in one paragraph

The engineering foundation is a strong base for the bible, and none of it needs replacing. That covers deterministic state, exactly-once crediting, saves v3, offline catch-up, the D-165–D-167 world read models and the D-169 presenter seam. The **entire player-facing presentation is placeholder and must be replaced**:
- a vertical UI Toolkit text stack (`ShellView`);
- a ~86-shape procedural Painter2D valley (`RegionScene`/`ValleyElement`);
- synthesised sine-tone cues (`SoundCues`);
- the default runtime theme (`ShellTheme.tss`).

The current editor captures look exactly like the patterns the bible rejects in §37: a text-heavy dashboard, symbolic rectangles standing in for buildings, and a lifeless background under text. The Unity project is also missing the packages a 2.5D sprite world needs (§5). Several docs still assume an "optional 3D Visit World" beside a "lightweight text UI", which contradicts the bible's world-first Home (§3).

## 2. Presentation components: replace / reuse / extend

| Component | Path | Today | Verdict | Notes |
|---|---|---|---|---|
| Composition root | `src/WalkGame.Unity/Assets/Shell/ShellBootstrap.cs` | Builds `GameSession`, adapters, `ShellController`, `UIDocument` | **Reuse / extend** | Add construction of the world presenter (D-169 implementations) and a persisted presentation memory. Keep it as the only place that builds Infrastructure. |
| Screen state machine and commands | `Shell/ShellController.cs` (1,163 lines; `Home()` L471, `Summary()` L520, `Projects()` L586, `Region()` L644) | Boot, sync, lifecycle, every command (`EnqueueProject`, acknowledge, settings, delete, `dev:walk`) and builders for 14 screens | **Reuse** commands, boot and sync; **replace** the Home/Region/Projects/ReturnSummary builders as the *primary* presentation | Keep the text builders as the accessible text equivalent and the fallback when the world fails to load (§4). Don't delete their tests. |
| Screen model | `Shell/ScreenModel.cs` | Engine-free title, lines and actions | **Reuse** | The text/screen-reader layer and the secondary-info panels (§18). |
| Text renderer | `Shell/ShellView.cs` | One ScrollView of labels and 56 dp buttons; "no motion at all" | **Replace** for Home, Region, Projects, dialogue and return; **reuse** (restyled in Phase C) for Settings, Privacy, DeleteConfirm, ActivityStatus, BootError and Guide | Keep the contrast (≥7:1), text-scale and touch-target rules. |
| Valley picture | `Shell/RegionScene.cs`, `Shell/ValleyElement.cs` | Procedural Painter2D: sky, hills, river, 6 block landmarks; static | **Replace** (placeholder, D-164) | The bible §30 says stop polishing it. Retire `RegionSceneTests`, `ValleyPlayModeTests` and `ValleyCaptureTests` only when the replacement lands, re-pointing their invariants (alt text, shape/perf budget, ring inside frame) at the new world view. |
| Theme | `Assets/Resources/ShellTheme.tss` | Unity default runtime theme | **Replace** in Phase C | Fonts (display and body, §23), palette (§8) and organic borders. Each font needs a licence entry. |
| Strings / copy | `Shell/PlayerStrings.cs`, `Shell/PlayerCopy.cs`, Application `ContentText` | One localisable table; dialogue already via `ContentText` | **Reuse / extend** | Add dialogue-UI labels (Inspect / Help restore / Maybe later, §16) and player copy for typed D-167 consequences. |
| Accessibility | `Shell/AccessibleOutline.cs`, `Shell/ScreenReaderBridge.cs` | Outline from the screen model; picture alt text | **Reuse / extend** | Per-landmark and per-NPC labels from the world view. Real reduced motion (UX_DESIGN §15) becomes a live requirement once anything animates. |
| Sound | `Shell/SoundCues.cs` | 3 synthesised cues, off by default, return summary only | **Extend**, then replace samples | Keep the rules (once per arrival, never on redraw, muted-sense, §26). Add ambience per world condition. Audio files need licence entries. |
| Boot routing | `Shell/BootRouter.cs` | Boot outcome → screen | **Reuse** | |
| Platform bridges | `Shell/Android*`, `Shell/IosHealthKitBridge.cs`, `ShellConnectionPort.cs`, `ReminderScheduler.cs` | Activity, share, reminders | **Untouched** | Not presentation. |
| Presenter seam (D-169) | `Shell/Presentation/*` | `IWorldPresenter`, `ILandmarkView`, `IDialoguePresenter`, `IChoicePreviewPresenter`, `IVisualChangePlayer`, `IPresentationMemory`, `WorldPresentationController`, `PresentationAssetResolver`; null presenters only; not wired | **Extend** | Gaps for the Phase D slice are listed in §4 below. |
| World read models (D-165–D-167) | `src/WalkGame.Application/ReadModels/World*`, `RestorationChoicesReadModel.cs`, `VisualChangeEvent.cs`, `WorldChangeProjector.cs` | Per-place variant keys `<id>@<stage>`, characters, lines, previews, change stream | **Reuse / extend** | Two content-to-visual gaps for the bridge (§4.4). |
| Content | `Application/Content/Region1Catalog.cs`, `Region1WorldLayout.cs`, `Region1Characters.cs` (D-172 cast), `Region1Letters.cs` | 6 landmarks, 19 projects, 4 NPCs, 6 letters | **Reuse** | The cast is a candidate until art approval. |
| Scene | `Assets/Scenes/Boot.unity` | Main Camera plus the `Shell` GameObject (UIDocument) | **Extend** | World root, orthographic camera and layers (§4.1). |
| Packages | `src/WalkGame.Unity/Packages/manifest.json` | Only accessibility, androidjni, audio, imgui, jsonserialize, ui, uielements, test-framework | **Extend** (finding) | No sprite tooling, animation, particle system, physics2d or image conversion. See §5. |
| Asset register | `docs/ASSET_LICENSES.md` | Lists only the procedural valley and the theme; no image, audio, font or model files | **Extend** | Every slice asset needs a row. AI-derived assets are blocked until D-170 (Proposed) is decided. |
| Capture and test tooling | `tools/unity/capture-*.sh`, `run-editmode-tests.sh`, `run-playmode-tests.sh`, `build-android-debug.sh`, `Assets/Tests/PlayMode/ValleyCaptureTests.cs` | llvmpipe/Xvfb editor-rendered PNGs; EditMode 269; PlayMode; arm64 APK | **Reuse / extend** | §47 evidence (editor captures, and a recording where feasible) builds on the capture tests. Device runs stay blocked (KI-002). |

## 3. Docs that contradict the bible

Docs revised in this change (each clear contradiction is now marked or rewritten, and prior art is marked as placeholder):

| Doc | Contradiction | Revision |
|---|---|---|
| `docs/UX_DESIGN.md` §3 | Home is a card/status hierarchy ("major return card, current project, … compact region status, secondary cards") | Replaced with bible §19: the illustrated world is the focus, with a compact top status and one main action. The text hierarchy is kept as the secondary/accessible layer. |
| `docs/UX_DESIGN.md` §9–§10, §16 | Region "should not require loading the full 3D world"; Visit World = 3D; "3D load failure" | Region and exploration are the illustrated 2.5D world (§4, §20). "3D load failure" is now "world view load failure", with the text shell as fallback. |
| `docs/UX_DESIGN.md` §14 status | "Reduced motion: met by construction (no motion)" | Marked as true of the placeholder text shell only. The illustrated world must implement §15 for real (§22, §24, §25). |
| `docs/UX_DESIGN.md` §19 | Celebration policy did not cover the return sequence | Points to bible §22: valley, highlight, short transition, NPC reaction, concise summary, one next action; never replay a finished restoration. |
| `docs/WORLD_AND_CONTENT.md` §12, §15–§17 | Mesh-based binding example; "3D world scope discipline"; avatar traversal | The binding example now uses layered sprite variants. §15 points to the bible palette and stages. §16 is now "World scope discipline (2.5D)". §17: optional exploration, no avatar required. |
| `docs/PRODUCT_SPEC.md` §6 | Home = "fastest view" of text; Visit World = "optional real-time world exploration" | Home is the illustrated world (bible §19). Visit World becomes optional deeper exploration of the same 2.5D world. |
| `docs/MASTER_PLAN.md` §6, §11, §13, M6 | "3D Visit World mode", "optional higher-cost 3D world" | Banner: the bible chooses illustrated 2.5D. M6 is read as Phase D/E, and "3D" as "the illustrated world". |
| `docs/PERFORMANCE_BUDGETS.md` §1–§2 | The lightweight mode is text-only; the 3D world is the high-cost mode | Banner: the illustrated Home now *is* the lightweight mode. Idle cost must stay minimal (throttled frame rate, ambient motion paused when idle or backgrounded). Budgets are unchanged until measured. |
| `docs/ASSET_LICENSES.md` | Valley picture listed without status | Marked **placeholder (D-164/D-171), to be replaced**. |

Not contradicting (no change needed):
- `AGENTS.md`;
- `docs/GAME_SYSTEMS.md` (systems are presentation-agnostic; §7 "Visit World discovery must not require precise input" still holds);
- `docs/ROADMAP.md` (updated by D-171).

## 4. What Phase D (Old Trailhead + damaged bridge, §42) needs from code

All of this is post-gate work, done in the approved direction. The rule throughout: presentation reads canonical state and never owns progress (§31, §50).

### 4.1 Scene structure
- `Boot.unity` gets a `World` root beside `Shell`. It has an orthographic camera (three-quarter elevated look painted into the art, §4; no 3D camera), a `TrailheadSlice` location prefab and the UI Toolkit panel as a transparent overlay for the HUD, dialogue and cards. Only the slice location is loaded (region-aware loading, §31). `Resources` or a direct reference is enough; Addressables are not needed for one location.
- If the world prefab or an asset fails, the shell falls back to the existing text Home (UX_DESIGN §16). `PresentationAssetResolver` already resolves a missing key to `fallback.missing` and lists it.

### 4.2 Sorting and parallax
- Sorting layers `Background`, `Midground`, `World`, `Foreground` and `Ambient`/`UI-World`, plus custom-axis transparency sort (0,1,0) for stable Y-depth within `World` (§31). This touches `ProjectSettings/TagManager.asset` and `GraphicsSettings.asset`. Each needs a deliberate commit with a decision reference. `ProjectSettings.asset` itself should not need to change; if it does, the D-117 trailer rule applies.
- Parallax: 2–3 background layers with a fixed factor driven by a small camera pan. It is off under reduced motion. The world must fill a portrait phone with safe areas, and the 56 dp touch-target rule applies to hit areas.

### 4.3 Landmark stage variants driven by D-166 keys
- `ILandmarkView` implementation: swap sprite sets by `VariantKey` (`lm.trailhead@ruined|stabilized|functional|restored|flourishing`, stages 0–4), resolved through `PresentationAssetResolver`, and show `ConstructionKey` scaffolds while active.
- **Finding:** `lm.trailhead` authors only stages 0–3. Its Functional stage is triggered by `proj.build-workshop`, which is the settlement's project, not the trailhead's. Its Restored stage is triggered by `proj.open-lookout`. The trailhead's art for stages 2–3 must therefore read as "the valley beyond is reviving", not "the bridge is fixed". Flourishing is never reached here, so a missing `@flourishing` set is expected, not a gap.

### 4.4 The bridge itself (two read-model gaps)
- **Gap A: no per-project visual state in the world view.** `proj.rebuild-trail-bridges` changes no landmark stage. Its completion opens the lookout and the boardwalk and adds Mira's line, but `WorldViewReadModel.LocationRow` exposes no "this project is done" key, and `VisualChangeKind` has no `ProjectCompleted`. A presenter could read the status from `GetProjects()`, but that splits the world binding across read models, and the bridge's completion still emits no visual-change event of its own, so the §25 bridge transition has nothing to play from. **Needed:** an Application-only extension, specified through an OpenSpec delta to `add-interactive-world-presentation`. It would add per-project prop keys on the row (for example `proj.rebuild-trail-bridges@done`) and a `ProjectCompleted` event, derived from `ProjectState.Status`/`CompletedAtUtc`. No save change.
- **Gap B: no intermediate state.** The bridge project has `VisibleBuildStages = 1`, so there is no construction key and §42.8 ("see intermediate progress") has nothing to bind to. Options:
  1. author visible build stages for the bridge, a content change through the D-071 process (content version, cadence and pacing evidence re-verified); or
  2. a presentation-only bucket of `VitalityInvested / cost`.

  Option 1 keeps a single authored source of truth and is recommended. It needs its own decision.

### 4.5 NPC presence
- `LocationRow.CharacterIds` and `GetCharacters()` already give who is where. The D-172 cast puts Mira at the trailhead from the start.
- **Needed in the seam:** an `ICharacterView` (in-world sprite, idle loop, expression from `Line.Expression`, a reaction hook) placed at authored anchor points per location. The anchors are presentation data, not save data.
- §13: polish one NPC (Mira) with idle, talk/expression change, happy and concerned states, a celebrate reaction and a restoration acknowledgement.

### 4.6 Tap routing
- D-169 routes only `TapLandmark(locationId)`. **Needed:**
  - `TapCharacter(characterId)`, which opens that character's dialogue. Order per §15: react, panel, name and portrait, one line, actions, return.
  - `TapProp(projectId)` to inspect the bridge.
  - A hit-test independent of physics: per-view screen rects/polygons resolved front to back, so `physics2d` is not needed.
- Decoration never captures taps (§10).

### 4.7 Dialogue panel over the visible world
- `IDialoguePresenter` as a bottom sheet in the UI Toolkit overlay, at most about a third of the screen high. The world stays visible and is never replaced by a full-screen panel (§15).
- **Needed:** `DialogueSequence` carries beats but no **actions**. Add optional actions: Inspect (→ prop/landmark card), Help restore (→ project card for that place's selectable goal), Maybe later (→ close).
- Dialogue gates nothing. The D-169 tests already prove the save is byte-identical either way.

### 4.8 Project card with previews
- `IChoicePreviewPresenter` renders `RestorationChoicesReadModel.Choice`: the before image (`BeforeVariantKey`, or the bridge prop's ruined key), the after preview (`AfterVariantKey` or the prop's `@done` key), a short description, progress and cost, key unlocks from the typed `Consequences`, and one Start/Queue action that calls `EnqueueProject` (D-169).
- **Needed:** player-facing, localisable phrasing for each `ConsequenceKind`, through `PlayerStrings`/`ContentText`. A D-155 discovery is never named.

### 4.9 Restoration transition playback from D-167 events
- `IVisualChangePlayer` maps events to short sequences (§25 bridge: debris clears, supports rise, the path becomes usable). It is fire-and-forget and skippable, and reduced motion shows the end state.
- Already true: state is canonical before playback, so skipping can never skip progress.
- **Finding (blocker for §22 and restart consistency):** D-169's `IPresentationMemory` is **in-memory only**. After a cold start the first open resets the baseline, so changes earned while the app was closed are *not* shown on return. That defeats the walk-then-return payoff. Seen-dialogue state is also lost. **Needed:** persist the watermark and seen lines in an erase-covered, non-save store (for example the existing UX preferences file that `DeleteAllLocalData` already erases). That needs its own decision (D-169 follow-up). It must not be a save-schema change.

### 4.10 Restart consistency
- On every boot the world is drawn only from `GetWorldView`/`GetCharacters`/`GetRestorationChoices`, so it matches canonical state by construction.
- Needed tests:
  - PlayMode: restart mid-construction and after completion; check the same variant, prop and construction keys and NPC placement, and no replay of events already shown. Unshown events are replayed exactly once.
  - Reduced motion and skip leave state unchanged.
  - Kill during playback, then reboot: same world.
- The isolated dev fixture for §42.7 already exists (`dev:walk` in development builds, through the real ingest pipeline: `ShellController.cs` L368). Release builds never show it.

### 4.11 Home composition (§19)
- World fills the screen. Top: compact status, a connection dot and one progress indicator. Bottom: one main action and minimal navigation. The text Home stays reachable as "details" and as the screen-reader path.
- Performance: idle frame-rate throttling and paused ambient motion when idle or backgrounded, so the lightweight-mode budget (`PERFORMANCE_BUDGETS.md` §2) still holds. Measure it in the editor; device measurement is blocked (KI-002).

## 5. Engine and package prerequisites (finding)

`src/WalkGame.Unity/Packages/manifest.json` has none of the following, and the slice needs them:
- **2D Sprite** (`com.unity.2d.sprite`: sprite editor, SpriteAtlas v2 authoring);
- **Animation module** (`com.unity.modules.animation`) for Animator-driven NPC idles, or **2D Animation** (`com.unity.2d.animation`) if skeletal rigs are chosen in Phase C;
- **Particle System module** (`com.unity.modules.particlesystem`) for leaves, dust and water sparkle;
- **Image Conversion module** (`com.unity.modules.imageconversion`) if captures need it.

Adding packages changes `Packages/packages-lock.json` and must be checked against the IL2CPP arm64 APK build and stripping (`link.xml`). The render pipeline is Built-in (`GraphicsSettings` has no custom RP), which is adequate for sprites. URP 2D lights are optional and not needed for the slice.

**Disposition (D-177, 2026-10-17):** deferred until Michael picks a round 2 direction. The set is style-dependent; concept A (recommended) needs 2D Sprite, 2D Animation and PSD Importer plus the Animation and Particle System modules, and no URP.

## 6. Bible §33 minimum vertical-slice asset list (Old Trailhead + damaged bridge)

Keys follow the D-166 and D-169 conventions so `PresentationAssetResolver` can report what is missing. All assets are original; licensing per `docs/ASSET_LICENSES.md` and the D-170 outcome. Nothing is produced before the round 2 approval.

| Group | Asset | Key(s) / states |
|---|---|---|
| Environment | Background sky and far hills (2–3 parallax layers), per atmosphere | `region@eco<e>-set<s>` (at least eco0 and eco1) |
| Environment | Midground: river at the trailhead, banks, ground and path treatment | `lm.trailhead@ruined`, `@stabilized`, `@functional`, `@restored` |
| Environment | Foreground framing: overhanging branches, grass tufts | shared, plus a restored variant |
| Environment | Trees: at least 3 variants (bare, budding, full) | `env.tree.<n>@<stage-band>` |
| Environment | Plants: shrubs, ferns, wildflowers (at least 3 each across stages) | `env.plant.*` |
| Environment | Rocks, fence, fallen log, broken and repaired signpost, small props | `env.prop.*`; signpost `@ruined/@restored` |
| Landmark | Damaged bridge: ruined, partly rebuilt, rebuilt | `proj.rebuild-trail-bridges@ruined`, `@build-<k>of<n>` (needs §4.4 B), `@done` (needs §4.4 A) |
| Landmark | Trailhead gate/camp: overgrown → cleared | `lm.trailhead@ruined/@stabilized` |
| Character | Mira in-world sprite: idle loop, talk, react/celebrate | `npc.mira` with anim states `idle`, `talk`, `celebrate` |
| Character | Mira portrait, at least 3 expressions | `portrait.npc.mira` × `curious`, `glad`, `worried` (plus `proud`) |
| Interface | World-first Home composition, compact status, progress, navigation | style guide components |
| Interface | Dialogue sheet (name, portrait, line, 2–3 actions) | `ui.dialogue.*` |
| Interface | Project card (before/after, progress, unlocks, start) | `ui.project-card.*` |
| Interface | Display and body fonts (licensed), icon set | `ui.font.*`, `ui.icon.*` |
| Feedback | Bridge restoration transition (debris clears, supports rise, path usable) | `fx.restore.proj.rebuild-trail-bridges` |
| Feedback | Ambient animation: grass sway, water flow, leaves, a bird | `fx.ambient.*` |
| Feedback | Tap response: highlight and NPC reaction | `fx.tap.*` |
| Audio | Ambience: stream, birds, damaged vs restored | `audio.amb.trailhead@<band>` |
| Audio | Soft UI and completion cues; Mira's voice blip | `audio.ui.*`, `audio.npc.mira` |

## 7. Top findings, ranked

1. **The presentation is wholly placeholder.** It is a text stack plus primitive shapes, matching §37 and §38 rejection patterns. Replace it rather than polish it.
2. **The bridge is not bound in the world read models.** There is no per-project visual key, no `ProjectCompleted` event, and no intermediate build stages for the bridge (§4.4). It needs an OpenSpec delta (Application-only) and, for intermediate stages, a content decision.
3. **Presentation memory is in-memory only.** The return payoff (§22) and restart consistency (§42) both need an erase-covered persisted watermark and seen-lines store, which is its own decision.
4. **Seam gaps:** no character view, no NPC/prop tap, and no dialogue actions (§4.5–§4.7).
5. **Missing Unity packages** for sprites, animation and particles (§5).
6. **The docs assumed a 3D Visit World beside a text UI.** They are revised here to the bible's world-first illustrated 2.5D. Performance budgets must be re-measured for an animated Home (editor only, KI-002).
7. **Trailhead stage semantics:** Functional and Restored are triggered from other chains (§4.3). The art must tell that story honestly.

## 8. What stays untouched

Domain, Application progression, ingestion and trust pipeline, saves (schema v3), offline catch-up, content (except a future D-071-style bridge build-stage decision), letters, platform bridges and every test. No save-schema change is proposed here. Region 2 is not approved (D-011).
