# Interactive world presentation contract (style-neutral groundwork)

**Status:** Approved for implementation by the executing agent as style-neutral groundwork under D-164, which allows this before the art-direction checkpoint. It locks in no art style, builds no Unity UI, art or prefabs, and touches no save schema. Region 1 (Millbrook Valley) only. Region 2 stays gated (D-011).

## Why

Michael's redesign brief (`docs/direction/REDESIGN_2026-10-17.md`, governing per D-164) asks for a world-first game. Players tap locations, talk to characters, pick restoration goals with previews, and watch the valley change. Today the Application layer only exposes list-shaped surfaces built for a text shell:
- `RegionReadModel.LandmarkRow` carries (id, title, stage) and nothing else.
- `ProjectsReadModel` is a flat roster with no place in the world.
- `ReturnSummaryReadModel` holds stored English sentences, not machine-readable changes.
- No characters or dialogue exist anywhere. Nell's letters (`Region1Letters`, D-118) are prose vignettes, not inhabitants.

Whichever art direction Michael approves, any world presentation will need the same engine-agnostic facts:
1. what each place looks like now;
2. what can be done there;
3. who is there and what they would say;
4. what a choice would change;
5. what changed since the player last looked.

Defining these now, headless and derived from canonical state, lets the post-checkpoint UI bind to a tested contract instead of parsing text.

## What Changes

- **World view read model** (`WorldViewReadModel`, `GameSession.GetWorldView`). It returns one row per location (the six Region 1 landmarks). Each row has the location id, title, `RestorationStage`, a style-neutral **variant key** (`lm.trailhead@stabilized`), locked/unlocked, interactable, the location's projects (and which are selectable restoration goals), an optional construction variant key for a visibly in-progress project, found-discovery count, characters present, and pending dialogue. Region-level fields are ecology/settlement stage, an atmosphere key, and completion.
- **Location layout binding** (`Region1WorldLayout`). This is presentation data that binds each D-062 restoration chain to the landmark where it happens. Every project gets exactly one location. A test checks the binding against the content.
- **Character and dialogue content model** (`Region1Characters`, `CharacterDialogueReadModel`, `GameSession.GetCharacters`). Characters have a stable id, a name, a portrait key and appearances (location by milestone). Lines are short (≤ 140 chars), each keyed by a restoration milestone (a project completion, or none for a baseline line) and given an expression key. Lines are localizable through `ContentText` (`<lineId>#line`, `<characterId>#name`). All reached lines can be revisited; the latest reached line is "current". Dialogue never gates progression: nothing in Domain or in any Application mutation reads it.
- **Pending dialogue without a save field.** "Pending" means the current line is not in a caller-supplied set of seen line ids. Presentation owns that set (a later Unity slice may keep it in local UX preferences, like other display state). Canonical state and the save never record it.
- **Restoration-choice previews** (`RestorationChoicesReadModel`, `GameSession.GetRestorationChoices`). Every Available, Queued or Active project has a preview with its location, cost, progress, affordability and before/after variant keys for its own location. Typed consequences list every landmark stage change, opened projects, unlocked producers, ecology/settlement arc steps, a discovery at a location (the location only, never the find, per D-155), characters arriving, new dialogue lines, chapters unlocked, and whether it is the closure milestone. The previews are computed from authored content with the same rules as `OfflineAdvancer.ApplyCompletionEffects` / `UpdateAvailability`.
- **Visual-change event stream** (`VisualChangeEvent`).
  - `GameSession.GetVisualChangesSince(DateTimeOffset)` derives a durable, deterministic event stream from canonical completion timestamps (`ProjectState.CompletedAtUtc`). It works across restarts and offline returns with no new persisted state.
  - `WorldViewDiff.Between(before, after)` derives in-session events (including construction-stage advances, which have no timestamp) from two world views.
  - Event kinds: location unlocked, landmark stage changed (from/to variant keys), construction advanced, ecology/settlement advanced, character arrived/moved, dialogue available, region completed.

## Non-goals

Unity UI, scenes, prefabs, sprites, portraits, animation, audio or layout; choosing 2D, 2.5D or 3D; Region 2; new gameplay rules, costs, pacing, or content-version changes; persisting "seen" dialogue in the save; letting dialogue unlock or block anything. Customization and exploration verbs (brief §5) are later slices once the art direction is chosen.

## Compatibility

- No save, schema (v3), content-version, fingerprint or pacing change. `Region1WorldLayout` and `Region1Characters` are presentation-only Application content, like `Region1Letters` / `Region1DiscoveryCopy`, and the content-compatibility gate does not see them.
- Existing read models are unchanged. All new surfaces are additive, read-only and side-effect free (no clock write, no save).
- Depends on D-062 chains, D-071 build stages, D-076 chapters, D-122 `ContentText`, D-155 (never name an unfound discovery).
