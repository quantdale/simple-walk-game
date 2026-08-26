# M5-R1 RUNTIME BOOTSTRAP — STAGED INTEGRATION LAYOUT (UNVERIFIED)

Evidence tier for everything in this document: **implemented, NOT compiled —
UNVERIFIED**. No Unity Editor ever ran in this environment (see UNITY_ENVIRONMENT.md).
Nothing below is runtime or even compile evidence; it is the authored starting point
for the session that resumes this campaign after the two human environment actions.

## What was staged

All paths under `unity/` are plain hand-authored source/config files only. No
`.meta`, scene, ProjectSettings or any other Editor-owned artifact was fabricated
(D-018 discipline). The resuming Editor will generate all metadata on first import.

```
unity/
  Packages/manifest.json                     test-framework + module pins
  Assets/WalkGame/link.xml                   IL2CPP stripping guard for core DLLs
  Assets/WalkGame/Plugins/Core/              gitignored; produced by scripts/build-unity-plugins.ps1
  Assets/WalkGame/Scripts/
    AppHost.cs                               production composition root + lifecycle
    Composition/AppComposer.cs               engine-agnostic graph wiring (session/store/clock/content)
    Composition/AppSettings.cs               non-canonical app prefs (PlayerPrefs-backed)
    Shell/AppShell.cs                        single UI coordinator; pull-model refresh
    Shell/ScreenCoordinator.cs               nav stack + back behavior
    Shell/Ui.cs                              styled-element factories (44px targets, no color-only states)
    Screens/Home|Projects|Region|Journal|Expeditions|Settings|Diagnostics|
            Onboarding|Unrecoverable + ReturnSummaryOverlay
    Development/DevActivityGate.cs           WALKGAME_DEV_TOOLS-gated synthetic source access
    Development/DevToolsSection.cs           dev-only fixture injection UI (real pipeline)
  Assets/WalkGame/Tests/EditMode/*.cs        composition/persistence/pipeline/copy tests
  Assets/WalkGame/Tests/PlayMode/            asmdef ready; journey test awaits a runnable editor
  Assets/WalkGame/EditorTools/ProjectSetup.cs  -executeMethod bootstrap: defines,
                                             PanelSettings asset, Bootstrap scene (Editor-generated)
scripts/build-unity-plugins.ps1             dotnet publish → managed-plugin staging
```

## Design decisions embedded in the staged code

* Core consumed as **precompiled netstandard2.1 plugins** built by
  `scripts/build-unity-plugins.ps1` from the existing solution (no domain logic
  duplicated into Unity, D-024/D-008 respected). Verified working headlessly:
  staging produced 10 assemblies (WalkGame.* + System.Text.Json closure).
* **UI Toolkit** chosen over uGUI for the shell (runtime UITk is production-ready on
  6000.x, C#-constructed UI keeps every screen reviewable and compile-checked, and the
  6.3 line adds native screen-reader support to build accessibility on).
* Presentation never touches domain state: screens render read models returned by
  `GameSession` and act only through its use-case methods.
* Lifecycle model: boot = `Continue()` (handles recovery + offline advance + summary);
  fresh profile = explicit onboarding → `StartNewGame(seed)`; resume-after-background =
  re-`Continue()` + optional activity-source reconcile window. Every committed mutation
  already persists atomically inside the application layer, so suspend needs no extra
  flush path.
* Reduced motion / haptics live in app-level prefs (`AppSettings`), never in canonical
  state; auto-advance remains canonical via `SetAutoAdvance`.

## Verification status of staged code

* Compiled by Unity: **NO** — blocked per Gate A1 (UNITY_ENVIRONMENT.md).
* Headless-verified pieces: plugin staging script output (10 assemblies), and the fact
  that every application/domain call used by the shell exists with matching signatures
  at starting SHA (verified by inspection against `src/**`).
* First actions for the resuming session: import, fix any compile errors, run
  `ProjectSetup.SetupProject`, then EditMode suite, then PlayMode journey.
