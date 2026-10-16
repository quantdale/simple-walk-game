#!/usr/bin/env bash
# Run a Unity test platform headless (default EditMode). NUnit XML + log land in
# ${EVIDENCE_DIR:-docs/evidence/m5-u1}/<platform>-*. Set EVIDENCE_DIR per campaign so a rerun
# does not overwrite committed evidence of an earlier campaign (KI-015).
set -uo pipefail
root="$(git rev-parse --show-toplevel)"
unity="${UNITY:-$HOME/Unity/Hub/Editor/6000.3.26f1/Editor/Unity}"
platform="${TEST_PLATFORM:-EditMode}"; lc="$(echo "$platform" | tr A-Z a-z)"
ev="$root/${EVIDENCE_DIR:-docs/evidence/m5-u1}"; mkdir -p "$ev"
"$root/tools/unity/sync-plugins.sh" >/dev/null
"$unity" -batchmode -nographics -projectPath "$root/src/WalkGame.Unity" \
  -runTests -testPlatform "$platform" -testResults "$ev/$lc-results.xml" \
  -logFile "$ev/$lc-run.log"
rc=$?
# Public repo: redact licensing identifiers before the log is committed as evidence.
sed -i -E 's/(Machine Id:[[:space:]]*).*/\1[REDACTED]/; s/(License group:).*/\1 [REDACTED]/; s/^([[:space:]]+(Id|Entitlement Group Id|Serial|Ulf)[^:]*:[[:space:]]*).*/\1[REDACTED]/' "$ev"/*.log
grep -o '<test-run [^>]*>' "$ev/$lc-results.xml" 2>/dev/null | head -1
exit $rc
