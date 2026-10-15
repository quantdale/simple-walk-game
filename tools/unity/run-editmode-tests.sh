#!/usr/bin/env bash
# Run the Unity EditMode suite headless; NUnit XML + log land in docs/evidence/m5-u1/.
set -uo pipefail
root="$(git rev-parse --show-toplevel)"
unity="${UNITY:-$HOME/Unity/Hub/Editor/6000.3.26f1/Editor/Unity}"
ev="$root/docs/evidence/m5-u1"; mkdir -p "$ev"
"$root/tools/unity/sync-plugins.sh" >/dev/null
"$unity" -batchmode -nographics -projectPath "$root/src/WalkGame.Unity" \
  -runTests -testPlatform EditMode -testResults "$ev/editmode-results.xml" \
  -logFile "$ev/editmode-run.log"
rc=$?
# Public repo: redact licensing identifiers before the log is committed as evidence.
sed -i -E 's/(Machine Id:[[:space:]]*).*/\1[REDACTED]/; s/(License group:).*/\1 [REDACTED]/; s/^([[:space:]]+(Id|Entitlement Group Id|Serial|Ulf)[^:]*:[[:space:]]*).*/\1[REDACTED]/' "$ev"/*.log
grep -o '<test-run [^>]*>' "$ev/editmode-results.xml" 2>/dev/null | head -1
exit $rc
