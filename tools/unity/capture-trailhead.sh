#!/usr/bin/env bash
# D-204: Unity editor captures (software GL under Xvfb, NOT a device) of the painted Old Trailhead at each
# canonical stage plus Mira's dialogue. Usage: tools/unity/capture-trailhead.sh [out-dir]
# (default docs/evidence/d204-trailhead). World and UI layers are dumped separately and composited here.
set -uo pipefail
root="$(git rev-parse --show-toplevel)"
unity="${UNITY:-$HOME/Unity/Hub/Editor/6000.3.26f1/Editor/Unity}"
out="$(realpath -m "${1:-$root/docs/evidence/d204-trailhead}")"; mkdir -p "$out"
log="$out/capture-run.log"
"$root/tools/unity/sync-plugins.sh" >/dev/null
export WG_TRAILHEAD_CAPTURE_DIR="$out" LIBGL_ALWAYS_SOFTWARE=1 GALLIUM_DRIVER=llvmpipe
xvfb-run -a -s "-screen 0 1280x1024x24" "$unity" -batchmode -force-glcore -projectPath "$root/src/WalkGame.Unity" \
  -runTests -testPlatform PlayMode -testFilter TrailheadCaptureTests -testResults "$out/capture-results.xml" -logFile "$log"
rc=$?
sed -i -E 's/(Machine Id:[[:space:]]*).*/\1[REDACTED]/; s/(License group:).*/\1 [REDACTED]/; s/^([[:space:]]+(Id|Entitlement Group Id|Serial|Ulf)[^:]*:[[:space:]]*).*/\1[REDACTED]/' "$log"
python3 "$root/tools/unity/composite-capture.py" "$out" && rm -f "$out"/*.rgba "$out"/*.size
grep -o '<test-run [^>]*>' "$out/capture-results.xml" 2>/dev/null | head -1
exit $rc
