## Why

Every M5 read model and use case the ordinary mobile shell needs already exists and is automated-verified headless: Home, Glance, Projects, Region, Discoveries, Expeditions, Return summary, Settings, Onboarding, Activity status, Diagnostics, and `DeleteAllLocalData`. What does not exist is any presentation that binds them (KI-002). D-035 is now resolved: Unity 6000.3.26f1 (6.3 LTS, Personal) is installed on the agent box, and activation is pending. This change fixes the M5-U1 scope **before** the licence lands, so the first editor session starts building rather than deciding, and cannot drift into putting game rules in `MonoBehaviour`s.

## What Changes

- Add a Unity project at `src/WalkGame.Unity` pinned to 6000.3.26f1 (`ProjectSettings/ProjectVersion.txt`). It consumes the existing `netstandard2.1` Domain, Application and Infrastructure assemblies as prebuilt plugins copied by a script. Unity never compiles those sources itself.
- Add one composition root that wires `GameSession` exactly as the headless tests do: `AtomicFileSaveStore` and `LocalPreferencesStore` on `Application.persistentDataPath` (app-private, D-067), `SystemClock`, Region 1 content, and a development activity source.
- Add five thin screens in UI Toolkit, each with normal, empty and error states sourced from read models: Home (with the glance), Return summary (acknowledge), Projects (queue and reorder), Settings (preferences, diagnostics toggle, and the **delete-all-local-data two-step confirmation**), and Activity status.
- Make lifecycle safe: boot through `Continue()`/`StartNewGame()` with every `StartStatus` mapped to a screen state, and advance and commit on resume.
- Add EditMode tests run in batchmode, a batchmode Android debug APK build script, and a guard that the Unity assemblies reference no Domain mutation types.

No change to Domain or Application semantics, save schema, crediting, or pacing.

## Capabilities

### New Capabilities

- `unity-shell`: the M5-U1 presentation contract. Read models are the only source of truth for what is shown, every mutation goes through an Application use case, the save directory is app-private, and every boot outcome and screen has a defined state.

### Modified Capabilities

(none)

## Impact

- New: `src/WalkGame.Unity/**`, `tools/unity/sync-plugins.sh`, `tools/unity/run-editmode-tests.sh`, `tools/unity/build-android-debug.sh`, and one architecture guard rule.
- Unity runs cannot execute on hosted CI (no licence there). Their evidence is local batchmode logs under `docs/evidence/m5-u1/`. Hosted CI keeps gating the headless suite and the new guard.
- Device behavior (screen reader, reduced motion at runtime, real lifecycle, performance) stays UNVERIFIED until a physical device is available.
