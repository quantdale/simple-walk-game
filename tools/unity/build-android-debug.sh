#!/usr/bin/env bash
# Build an arm64 IL2CPP development APK headless. Log -> ${EVIDENCE_DIR:-docs/evidence/m5-u1}/android-build.log.
set -uo pipefail
root="$(git rev-parse --show-toplevel)"
unity="${UNITY:-$HOME/Unity/Hub/Editor/6000.3.26f1/Editor/Unity}"
ev="${EVIDENCE_DIR:-docs/evidence/m5-u1}"; case "$ev" in /*) ;; *) ev="$root/$ev" ;; esac; mkdir -p "$ev"  # absolute EVIDENCE_DIR is used as-is
"$root/tools/unity/sync-plugins.sh" >/dev/null
# WG_ANDROID_ARCH=x86_64 -> emulator APK (simple-walk-game-debug-x86_64.apk); default arm64.
arch="${WG_ANDROID_ARCH:-arm64}"; export WG_ANDROID_ARCH="$arch"
suffix=""; [ "$arch" = "x86_64" ] && suffix="-x86_64"
export WG_APK_PATH="$root/src/WalkGame.Unity/Build/Android/simple-walk-game-debug$suffix.apk"
"$unity" -batchmode -nographics -quit -projectPath "$root/src/WalkGame.Unity" -buildTarget Android \
  -executeMethod WalkGame.Shell.Editor.ShellBuild.BuildAndroidDebug -logFile "$ev/android-build$suffix.log"
rc=$?
# Public repo: redact licensing identifiers before the log is committed as evidence.
sed -i -E 's/(Machine Id:[[:space:]]*).*/\1[REDACTED]/; s/(License group:).*/\1 [REDACTED]/; s/^([[:space:]]+(Id|Entitlement Group Id|Serial|Ulf)[^:]*:[[:space:]]*).*/\1[REDACTED]/' "$ev"/*.log
grep 'WG_BUILD_RESULT' "$ev/android-build$suffix.log" | tail -1
ls -l "$WG_APK_PATH" 2>/dev/null
exit $rc
