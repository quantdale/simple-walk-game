## 0. Preconditions

- [ ] 0.1 Personal licence activated on the box; `Unity -batchmode -nographics -quit` exits 0 (closes KI-002 install/activation half)
- [ ] 0.2 Add an 8 GB swapfile if MemAvailable < 8 GB

## 1. Project skeleton

- [ ] 1.1 Create `src/WalkGame.Unity` with 6000.3.26f1 (`-createProject` in batchmode); commit `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, `.gitignore` (Library/Temp/Logs/Build excluded)
- [ ] 1.2 `tools/unity/sync-plugins.sh`: Release build of the three netstandard2.1 projects, copy DLLs and STJ dependencies into `Assets/Plugins/WalkGame/`; add `link.xml`
- [ ] 1.3 Assembly definitions: `WalkGame.Shell` (runtime), `WalkGame.Shell.Tests` (EditMode)

## 2. Composition root and lifecycle

- [ ] 2.1 `ShellBootstrap`: wires `GameSession` (AtomicFileSaveStore and LocalPreferencesStore on `persistentDataPath`, SystemClock, Region1Catalog, development source)
- [ ] 2.2 StartStatus → screen-state router (all enum values; test enumerates them)
- [ ] 2.3 Resume handling via `Continue()`

## 3. Screens (UI Toolkit controllers + UXML)

- [ ] 3.1 Home + glance  - [ ] 3.2 Return summary (acknowledge)  - [ ] 3.3 Projects (queue/reorder)
- [ ] 3.4 Settings, including the delete-all two-step confirmation  - [ ] 3.5 Activity status
- [ ] 3.6 Normal/empty/error state for each screen

## 4. Guards and tests

- [ ] 4.1 `verify-architecture.sh` rule: no Domain state mutation in Unity assets (with a self-test fixture)
- [ ] 4.2 EditMode tests: controllers vs read-model fixtures; router covers every StartStatus; delete-confirm cancel/confirm; codec round-trip under the Unity runtime (catches IL2CPP stripping)

## 5. Build and evidence

- [ ] 5.1 `tools/unity/run-editmode-tests.sh` (batchmode, NUnit XML to `docs/evidence/m5-u1/`)
- [ ] 5.2 `tools/unity/build-android-debug.sh` (arm64 debug APK; log to evidence)
- [ ] 5.3 Record results; anything not run is UNVERIFIED; update ROADMAP M5, KNOWN_ISSUES KI-002, README
