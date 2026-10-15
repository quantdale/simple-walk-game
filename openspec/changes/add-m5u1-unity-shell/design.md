## Context

Headless contracts: `GameSession` and its read models (D-034, D-042..D-044, D-061, D-065), `DeleteAllLocalData` (PRODUCT_SPEC §7a req. 7), and app-private storage (D-067). Editor: 6000.3.26f1, installed on the box at `~/Unity/Hub/Editor/6000.3.26f1`, licence pending. No GPU, so `-batchmode -nographics`.

## Goals / Non-Goals

**Goals:** bind the existing contracts into a minimal, honest mobile shell; prove it compiles, passes EditMode tests, and builds a debug APK in batchmode.

**Non-goals:** Visit World / 3D (M6); Health Connect (M7); visual polish and art; notifications delivery; any new game rule.

## Decisions

1. **Prebuilt plugins, not source import.** `tools/unity/sync-plugins.sh` runs `dotnet build -c Release` for the three `netstandard2.1` projects and copies the DLLs plus `System.Text.Json` and its dependencies into `Assets/Plugins/WalkGame/`. This keeps one compiler and one source of truth, and makes the Unity side unable to edit domain code. Alternative rejected: UPM local package of sources, because Unity would compile with a different C# version and analyzer set.
2. **UI Toolkit**, with each screen a `VisualElement` controller that takes a read model and returns intents. Controllers are plain C# classes that are unit-testable in EditMode without a scene.
3. **Single composition root** (`ShellBootstrap` MonoBehaviour), the only place that constructs infrastructure. Screens receive the `GameSession` facade, never stores.
4. **Delete-all confirmation is presentation state.** Two steps: Settings → "Delete all local data" → a confirmation that states what is erased and that it cannot be undone. Only the confirm intent calls `DeleteAllLocalData()`. `NotSupported`/`Failed` show an error state; `Erased` routes to first-run onboarding.
5. **Lifecycle.** `OnApplicationPause(false)`/focus regain calls `Continue()` (which advances offline time and commits). Pausing does not write: every mutation already committed atomically.
6. **Guard.** `scripts/verify-architecture.sh` gains a rule: no file under `src/WalkGame.Unity/Assets` may reference `WalkGame.Domain.*State` setters, `ResourceBalances`, or `RewardLedgerState`. Presentation reads models and calls use cases only.

## Risks / Trade-offs

- **[RAM ~5–10 GB free]** → add swap before the first Gradle build; build only arm64 debug.
- **[No hosted Unity CI]** → local logs are evidence; mark UNVERIFIED anything not run.
- **[netstandard2.1 + System.Text.Json on IL2CPP]** → add `link.xml` preserving `WalkGame.*` and STJ. A codec round-trip EditMode test catches stripping.
