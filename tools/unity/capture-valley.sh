#!/usr/bin/env bash
# Editor-rendered (NOT device) PNG of a shell screen: software OpenGL (Mesa llvmpipe) under Xvfb.
# Usage: tools/unity/capture-valley.sh [out.png]   (WG_CAPTURE_SCREEN=Region|ReturnSummary|Home|Settings|Celebration|Discoveries|EpilogueJournal; WG_CAPTURE_TEXT_SCALE=1.5; WG_CAPTURE_SCROLL=end; WG_CAPTURE_EXPECT_BUTTON='exact text')
set -uo pipefail
root="$(git rev-parse --show-toplevel)"
unity="${UNITY:-$HOME/Unity/Hub/Editor/6000.3.26f1/Editor/Unity}"
out="$(realpath -m "${1:-$root/docs/evidence/m6-region/valley-editor-rendered.png}")"
log="$(dirname "$out")/capture-run.log"; mkdir -p "$(dirname "$out")"
"$root/tools/unity/sync-plugins.sh" >/dev/null
export WG_CAPTURE_PNG="$out" LIBGL_ALWAYS_SOFTWARE=1 GALLIUM_DRIVER=llvmpipe
xvfb-run -a -s "-screen 0 1280x1024x24" "$unity" -batchmode -force-glcore -projectPath "$root/src/WalkGame.Unity" \
  -runTests -testPlatform PlayMode -testFilter ValleyCaptureTests -testResults "$(dirname "$out")/capture-results.xml" -logFile "$log"
rc=$?
sed -i -E 's/(Machine Id:[[:space:]]*).*/\1[REDACTED]/; s/(License group:).*/\1 [REDACTED]/; s/^([[:space:]]+(Id|Entitlement Group Id|Serial|Ulf)[^:]*:[[:space:]]*).*/\1[REDACTED]/' "$log"
grep -h 'WG_CAPTURE' "$log" | tail -1
if [ -f "$out.rgba" ]; then python3 "$root/tools/unity/rgba-to-png.py" "$out.rgba" "$out.size" "$out" && rm -f "$out.rgba" "$out.size"; fi
ls -l "$out" 2>/dev/null
exit $rc
