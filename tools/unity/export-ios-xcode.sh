#!/usr/bin/env bash
# Export an unsigned iOS Xcode project headless (Linux editor + free iOS Build Support module).
# Log -> ${EVIDENCE_DIR:-docs/evidence/ios-xcode-export}/ios-export.log. Output -> src/WalkGame.Unity/Build/iOS/xcode.
# Restores ProjectSettings.asset afterwards (KI-015: never commit it).
set -uo pipefail
root="$(git rev-parse --show-toplevel)"
unity="${UNITY:-$HOME/Unity/Hub/Editor/6000.3.26f1/Editor/Unity}"
ev="${EVIDENCE_DIR:-docs/evidence/ios-xcode-export}"; case "$ev" in /*) ;; *) ev="$root/$ev" ;; esac; mkdir -p "$ev"  # absolute EVIDENCE_DIR is used as-is
[ -d "$(dirname "$unity")/Data/PlaybackEngines/iOSSupport" ] || {
  echo "iOS Build Support module missing: unity install-modules -e 6000.3.26f1 -m ios --accept-eula (needs apt xz-utils cpio p7zip-full)" >&2; exit 2; }
"$root/tools/unity/sync-plugins.sh" >/dev/null
export WG_XCODE_PATH="$root/src/WalkGame.Unity/Build/iOS/xcode"
rm -rf "$WG_XCODE_PATH"
"$unity" -batchmode -nographics -quit -projectPath "$root/src/WalkGame.Unity" -buildTarget iOS \
  -executeMethod WalkGame.Shell.Editor.ShellBuild.BuildIosXcode -logFile "$ev/ios-export.log"
rc=$?
sed -i -E 's/(Machine Id:[[:space:]]*).*/\1[REDACTED]/; s/(License group:).*/\1 [REDACTED]/; s/^([[:space:]]+(Id|Entitlement Group Id|Serial|Ulf)[^:]*:[[:space:]]*).*/\1[REDACTED]/' "$ev"/*.log
git -C "$root" checkout -- src/WalkGame.Unity/ProjectSettings/ProjectSettings.asset 2>/dev/null || true
grep 'WG_BUILD_RESULT' "$ev/ios-export.log" | tail -1
exit $rc
