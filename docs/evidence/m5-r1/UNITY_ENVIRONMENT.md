# M5-R1 UNITY ENVIRONMENT — RESOLUTION ATTEMPT RECORD

Campaign: M5-R1 runtime bootstrap + mobile shell qualification
Date: 2026-08-26
Outcome: **BLOCKED — HUMAN UNITY ENVIRONMENT ACTION REQUIRED** (Gate A1, now with a
complete technical resolution record; see CAMPAIGN_OUTCOME.md)

## Machine state at session start

| Item | State |
|---|---|
| Unity Hub | 3.12.1.0 installed at `C:\Program Files\Unity Hub\Unity Hub.exe`; never launched (no `%APPDATA%\UnityHub`, no `%LOCALAPPDATA%\UnityHub`) |
| Installed editors | none (no editors under `C:\Program Files\Unity*`, no `editors.json`) |
| Licenses | none (`C:\ProgramData\Unity` absent, no `%LOCALAPPDATA%\Unity\licenses`) |
| Unity account credentials | none stored on machine (no `UNITY_*` env vars, no credential-manager entries) |
| Session privileges | user is in local Administrators group but runs with a **filtered (non-elevated) token**; UAC consent cannot be granted non-interactively |
| Disk | C: 180.8 GB free; D: 1038 GB free |

## Target release selected

**Unity 6000.3.22f1** — changeset `1c726e1fb402` — released 2026-08-13.
Current LTS line (supported until December 2027). Chosen over 6000.0.x LTS because that
line ends support October 2026. This resolves the "exact Unity 6 LTS editor/runtime
version" open decision listed in DECISIONS.md (D-023 open items).

## Attempt log (chronological, exact outcomes)

1. **Direct silent install of official Windows installer**
   - Downloaded and size-verified:
     `https://download.unity3d.com/download_unity/1c726e1fb402/Windows64EditorInstaller/UnitySetup64-6000.3.22f1.exe` (3932 MB)
     `https://download.unity3d.com/download_unity/1c726e1fb402/TargetSupportInstaller/UnitySetup-Android-Support-for-Editor-6000.3.22f1.exe` (1472 MB)
     Both staged at `D:\UnityDownloads\`.
   - `Start-Process ... /S /D=D:\Unity\6000.3.22f1` → failed,
     "operation was canceled by the user".
   - Diagnostic with `UseShellExecute=false` → definitive error:
     **"The requested operation requires elevation."**
   - Conclusion: the installer manifest requires an administrator token; SmartScreen
     was ruled out by unblocking the file first.

2. **Installer extraction without execution**
   - 7-Zip 25.00 `x` produced only PE section dumps (`.text`, `.rdata`, `.rsrc`, …).
   - `7z l -tNsis` lists no archive entries → the modern Unity installer is no longer
     NSIS-extractable. Route closed.

3. **Unity Hub headless CLI install**
   - Pre-configured `%APPDATA%\UnityHub\secondaryInstallPath.json` = `"D:\\Unity"`
     (user-writable target, avoiding Program Files).
   - `"C:\Program Files\Unity Hub\Unity Hub.exe" -- --headless install --version 6000.3.22f1 --changeset 1c726e1fb402 --module android --module android-sdk --module android-ndk --module android-open-jdk`
   - First attempt died on EPIPE when the launching shell detached (progress renderer);
     relaunched fully detached → download proceeded normally (~4 GB staged in temp).
   - Hub's final step executes the downloaded installer via
     `powershell.exe Start-Process` from an unelevated context → same elevation wall.
     Log (`%APPDATA%\UnityHub\logs\info-log.json`):
     ```
     UnityInstallerWindows: Command failed: powershell.exe Start-Process -FilePath "'C:\Users\palac\AppData\Local\Temp\8fe63c0a…'"
     Installer: 6000.3.22f1-x86_64: Validation FAILED.
     Transition to state "install_failed" on event "ERROR"
     ```
   - Hub cleaned up its temp payloads and exited. No editor was left on disk.

4. **Portable/archive distribution probe**
   - `Windows64EditorInstaller/Unity-6000.3.22f1.zip` and `.tar.xz` under the same
     changeset path → both HTTP 404. Unity publishes no unzip-and-run Windows editor.

5. **Licensing pre-check**
   - No credentials exist for any non-interactive activation flow
     (`Unity.Licensing.Client` ships inside the editor payload; there is nothing to
     activate until an editor exists AND an account signs in interactively).

## Exact missing human actions

Two independent interactive steps are required, in order:

1. **Elevation approval** to run one of:
   - `D:\UnityDownloads\UnitySetup64-6000.3.22f1.exe /S /D=D:\Unity\6000.3.22f1`
     (then `D:\UnityDownloads\UnitySetup-Android-Support-for-Editor-6000.3.22f1.exe /S /D=D:\Unity\6000.3.22f1`),
     or
   - open Unity Hub once and install Unity 6000.3.22f1 with Android modules
     (Hub remembers `D:\Unity` as secondary install path already), accepting its UAC prompt.
2. **Unity Personal license sign-in** (first Hub/Editor launch, browser OAuth).

## Resume procedure for the next agent/session

```powershell
# after the two human actions above succeed:
Test-Path D:\Unity\6000.3.22f1\Editor\Unity.exe        # must be True
& D:\Unity\6000.3.22f1\Editor\Unity.exe -batchmode -quit -logFile -   # must exit 0 with a license

# then resume THIS campaign at Workstream A (unity/ sources are committed):
git fetch origin && git log --oneline origin/main -3    # reconcile drift first
# plugin staging (already scripted):
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build-unity-plugins.ps1
# project setup (creates PanelSettings, Bootstrap scene, defines — all Editor-generated):
& D:\Unity\6000.3.22f1\Editor\Unity.exe -batchmode -quit -projectPath <repo>\unity `
    -executeMethod WalkGame.UnityShell.EditorTools.ProjectSetup.SetupProject -logFile -
```

The `unity/Assets/WalkGame/**` sources committed by this campaign were authored but
**never compiled by any compiler other than review** — treat them as UNVERIFIED until
the Editor import/build gate has actually run.
