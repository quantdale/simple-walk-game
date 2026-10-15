#!/usr/bin/env bash
# Headless install of Unity Hub + Unity 6000.3.26f1 (6.3 LTS, D-035) + Android Build Support
# on a Debian/Ubuntu x86_64 box. DRY-RUN by default: prints what it would do. Pass --apply
# to execute. Nothing here activates a licence: activation needs Michael's Unity ID (see
# docs/UNITY_INSTALL.md). Installing Hub and the editor does NOT need a licence.
set -euo pipefail

VERSION="6000.3.26f1"
CHANGESET="de8167e7440c"
MODULES=(android android-sdk-ndk-tools android-open-jdk)
INSTALL_ROOT="${UNITY_INSTALL_ROOT:-$HOME/Unity/Hub/Editor}"
APPLY=0; [ "${1:-}" = "--apply" ] && APPLY=1

run() { if [ "$APPLY" -eq 1 ]; then echo "+ $*"; eval "$@"; else echo "[dry-run] $*"; fi; }

echo "== preflight =="
[ "$(uname -m)" = "x86_64" ] || { echo "ERROR: Unity Linux editor requires x86_64" >&2; exit 2; }
avail_gb=$(df -BG --output=avail "$HOME" | tail -1 | tr -dc 0-9)
echo "free disk under \$HOME: ${avail_gb} GB (need ~20 GB: editor 8.7 + Android 4.2 + SDK/NDK/JDK ~4 + cache)"
[ "$avail_gb" -ge 25 ] || { echo "ERROR: not enough disk" >&2; exit 2; }
mem_gb=$(awk '/MemAvailable/ {print int($2/1048576)}' /proc/meminfo)
echo "available RAM: ${mem_gb} GB (editor import + Gradle builds want >= 8 GB; consider swap)"

echo "== 1. Unity Hub apt repository + deps (needs sudo) =="
run "curl -fsSL https://hub.unity3d.com/linux/keys/public | sudo gpg --dearmor -o /usr/share/keyrings/Unity_Technologies_ApS.gpg"
run "echo 'deb [signed-by=/usr/share/keyrings/Unity_Technologies_ApS.gpg] https://hub.unity3d.com/linux/repos/deb stable main' | sudo tee /etc/apt/sources.list.d/unityhub.list"
run "sudo apt-get update"
run "sudo apt-get install -y unityhub xvfb libnss3 libasound2t64 xz-utils p7zip-full cpio"   # xz: Hub extracts the editor .tar.xz; 7z/cpio: Android .pkg payload   # Debian 13 t64 names; Hub deps resolve via Provides

echo "== 2. Editor + Android modules via Hub CLI (no licence required) =="
run "mkdir -p '$INSTALL_ROOT'"
run "xvfb-run -a unityhub --no-sandbox --headless install-path --set '$INSTALL_ROOT'"
run "xvfb-run -a unityhub --no-sandbox --headless install --version $VERSION --changeset $CHANGESET $(printf -- '--module %s ' "${MODULES[@]}") --childModules"

echo "== 3. Verify =="
run "test -x '$INSTALL_ROOT/$VERSION/Editor/Unity' && echo EDITOR_PRESENT"
run "ls '$INSTALL_ROOT/$VERSION/Editor/Data/PlaybackEngines/AndroidPlayer'"
echo ""
echo "Next: licence activation (Personal) — see docs/UNITY_INSTALL.md §Activation. KI-002 stays open until then."
