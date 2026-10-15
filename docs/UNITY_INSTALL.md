# Unity install readiness: 6000.3.26f1 (D-035)

**Status (2026-10-16):** prepared, **not installed**. KI-002 stays open until the editor is installed **and** activated.

## Chosen version
- Unity **6000.3.26f1** (Unity 6.3 LTS), released 2026-10-08, changeset `de8167e7440c`, supported to Dec 2027. Licence: **Personal**.

## What was checked on the agent box (Debian 13 trixie, x86_64, 8 vCPU, 16 GB RAM, ~115 GB free)
| Check | Result |
|---|---|
| Editor archive `LinuxEditorInstaller/Unity-6000.3.26f1.tar.xz` | reachable (HTTP 200), 4.54 GB download, 8.74 GB installed, md5 `80b975d11304a7ca03a7ea6a4011246d` |
| Android Build Support | listed in `unity-6000.3.26f1-linux.ini`; 2.11 GB download / 4.22 GB installed. The Linux ini points at the `.pkg` payload, which Unity Hub unpacks. **Use Hub, not a manual tarball** (the `LinuxEditorTargetInstaller` Android URL 404s). |
| Unity Hub apt repo `hub.unity3d.com/linux/repos/deb` | reachable; signing key 200; `unityhub` 3.x published for amd64 |
| Hub dependencies on Debian 13 | the `t64` renames (`libgtk-3-0t64`, `libatspi2.0-0t64`) provide the names Hub depends on; `xvfb`, `libnss3`, `libxss1`, `libasound2t64` available |
| sudo | available |
| Disk | ~20 GB needed (editor + Android + SDK/NDK/OpenJDK + cache); 115 GB free |
| RAM | ~5–8 GB available; tight for asset import and Gradle/IL2CPP. Add an 8 GB swapfile before the first Android build. |
| GPU | none. Use `-batchmode -nographics` for EditMode tests and Android builds. PlayMode rendering and performance evidence are not meaningful here. |

## Installable before a licence exists
Everything **except activation**: Hub, the editor, the Android module, SDK/NDK/OpenJDK child modules, Xvfb. Run:

```bash
tools/unity/install-unity-6000.3.26f1.sh           # dry run: prints every step
tools/unity/install-unity-6000.3.26f1.sh --apply   # performs the install (~7 GB download)
```

Without activation the editor will not open a project or run `-batchmode` builds/tests (it exits with a licence error).

## Activation (needs Michael)
1. Michael creates a Unity ID (free) and accepts the Personal terms.
2. On the box desktop (the agent box has a browser desktop the user can open), run `unityhub`, sign in with that Unity ID, and add a **Personal** licence (Preferences → Licenses → Add). This writes `~/.local/share/unity3d/Unity/Unity_lic.ulf`.
3. Verify headless: `~/Unity/Hub/Editor/6000.3.26f1/Editor/Unity -batchmode -nographics -quit -logFile -` should exit 0 without licence errors.

Agents must never handle the Unity password or copy the licence file anywhere else. Personal activation is per machine, and the licence file is a credential.

## After activation (first campaign, M5-U1)
Create `src/WalkGame.Unity` consuming the Application read models, EditMode tests in batchmode, and an Android debug APK build. That closes KI-002's install/activation half; device evidence stays UNVERIFIED until a physical device is available.
