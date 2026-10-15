## 0. Preconditions

- [x] 0.1 Personal licence activated on the box; `Unity -batchmode -nographics -quit` exits 0 (closes KI-002 install/activation half)
- [x] 0.2 Add an 8 GB swapfile if MemAvailable < 8 GB

## 1. Project skeleton

- [x] 1.1 Create `src/WalkGame.Unity` with 6000.3.26f1 (`-createProject` in batchmode); commit `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, `.gitignore` (Library/Temp/Logs/Build excluded)
- [x] 1.2 `tools/unity/sync-plugins.sh`: Release build of the three netstandard2.1 projects, copy DLLs and STJ dependencies into `Assets/Plugins/WalkGame/`; add `link.xml`
- [x] 1.3 Assembly definitions: `WalkGame.Shell` (runtime), `WalkGame.Shell.Tests` (EditMode)

## 2. Composition root and lifecycle

- [x] 2.1 `ShellBootstrap`: wires `GameSession` (AtomicFileSaveStore and LocalPreferencesStore on `persistentDataPath`, SystemClock, Region1Catalog, development source)
- [x] 2.2 StartStatus → screen-state router (all enum values; test enumerates them)
- [x] 2.3 Resume handling via `Continue()`

## 3. Screens (UI Toolkit controllers + UXML)

- [x] 3.1 Home + glance  - [x] 3.2 Return summary (acknowledge)  - [x] 3.3 Projects (queue/reorder)
- [x] 3.4 Settings, including the delete-all two-step confirmation  - [x] 3.5 Activity status
- [x] 3.6 Normal/empty/error state for each screen

Note: screens are rendered in code from engine-free `ScreenModel`s (no UXML); visual rendering is UNVERIFIED.

## 4. Guards and tests

- [x] 4.1 `verify-architecture.sh` rule: no Domain state mutation in Unity assets (with a self-test fixture)
- [x] 4.2 EditMode tests: controllers vs read-model fixtures; router covers every StartStatus; delete-confirm cancel/confirm; codec round-trip under the Unity runtime (catches IL2CPP stripping)

## 5. Build and evidence

- [x] 5.1 `tools/unity/run-editmode-tests.sh` (batchmode, NUnit XML to `docs/evidence/m5-u1/`)
- [x] 5.2 `tools/unity/build-android-debug.sh` (arm64 debug APK; log to evidence)
- [x] 5.3 Record results (`docs/evidence/m5-u1/M5-U1-RESULTS.md`); anything not run is UNVERIFIED; update ROADMAP M5, KNOWN_ISSUES KI-002, README
