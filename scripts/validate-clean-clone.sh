#!/usr/bin/env bash
# Clean-clone release validation for quantdale/simple-walk-game (M9, D-075).
#
# Proves a release candidate builds and passes from NOTHING but the remote:
# a fresh `git clone --no-local` of one exact commit into a new temp directory,
# no reuse of any worktree, build output, Unity Library or local hook. Then:
#   1. repository identity guard;
#   2. every CI `run:` step, verbatim (scripts/verify-ci-steps.sh, D-049);
#   3. with --with-unity: Unity EditMode + PlayMode in batchmode and the arm64
#      Android debug APK build, from a cold Library (Unity-editor-tested only;
#      device runtime stays UNVERIFIED, KI-002).
# Writes <output>/CLEAN_CLONE.md (one PASS/FAIL row per step, durations, counts)
# plus one log per step. Long: run it detached and read the logs.
#
# Usage:
#   scripts/validate-clean-clone.sh --output <dir> [--ref <sha|branch>] [--source <url|path>] [--with-unity] [--keep]
# Exit codes: 0 all steps passed; 1 a step failed; 2 usage error.
set -eu

REPO_ROOT="$(git rev-parse --show-toplevel)"
OUTPUT=""; REF="origin/main"; SOURCE=""; WITH_UNITY=0; KEEP=0
while [ $# -gt 0 ]; do
    case "$1" in
        --output) OUTPUT="$2"; shift 2 ;;
        --ref) REF="$2"; shift 2 ;;
        --source) SOURCE="$2"; shift 2 ;;
        --with-unity) WITH_UNITY=1; shift ;;
        --keep) KEEP=1; shift ;;
        *) echo "ERROR: unknown argument '$1'" >&2; exit 2 ;;
    esac
done
[ -n "$OUTPUT" ] || { echo "ERROR: --output is required" >&2; exit 2; }
SOURCE="${SOURCE:-$(git -C "$REPO_ROOT" remote get-url origin)}"
mkdir -p "$OUTPUT"
OUTPUT="$(cd "$OUTPUT" && pwd)"

# Resolve the ref to an exact SHA against the source, so the report names one commit.
git -C "$REPO_ROOT" fetch -q origin 2>/dev/null || true
SHA="$(git -C "$REPO_ROOT" rev-parse --verify "${REF}^{commit}")"

CLONE="$(mktemp -d "${TMPDIR:-/tmp}/swg-clean-clone.XXXXXX")"
cleanup() { if [ "$KEEP" -eq 0 ]; then rm -rf "$CLONE"; fi; }
trap cleanup EXIT

REPORT="$OUTPUT/CLEAN_CLONE.md"
{
    echo "# Clean-clone validation"
    echo ""
    echo "| field | value |"
    echo "|---|---|"
    echo "| source | \`${SOURCE}\` |"
    echo "| commit | \`${SHA}\` |"
    echo "| started (UTC) | $(date -u +%Y-%m-%dT%H:%M:%SZ) |"
    echo "| host | $(uname -sm), .NET $(dotnet --version 2>/dev/null || echo none) |"
    echo "| Unity lane | $([ "$WITH_UNITY" -eq 1 ] && echo "yes (Unity-editor-tested; device UNVERIFIED, KI-002)" || echo "not requested") |"
    echo ""
    echo "| step | result | seconds | log |"
    echo "|---|---|---|---|"
} >"$REPORT"

FAILED=0
step() { # name, command... (runs inside the clone)
    local name="$1"; shift
    local t0; t0=$(date +%s)
    echo "== ${name}"
    if (cd "$CLONE" && "$@") >"$OUTPUT/${name}.log" 2>&1; then r=PASS; else r=FAIL; FAILED=1; fi
    echo "| ${name} | **${r}** | $(( $(date +%s) - t0 )) | \`${name}.log\` |" >>"$REPORT"
    echo "   ${r}"
}

step clone bash -c "git clone -q --no-local '$SOURCE' . && git checkout -q --detach '$SHA' && test \"\$(git rev-parse HEAD)\" = '$SHA' && git status --porcelain | wc -l | grep -qx 0"
if [ "$FAILED" -eq 0 ]; then
    step identity scripts/assert-repo-identity.sh
    step ci-steps scripts/verify-ci-steps.sh
    # verify-ci-steps prints one line per step; re-run the suite once more, visibly, for counts.
    step dotnet-tests dotnet test SimpleWalkGame.sln --no-build
    if [ "$WITH_UNITY" -eq 1 ]; then
        step unity-editmode env EVIDENCE_DIR=.clean-clone-evidence/editmode tools/unity/run-editmode-tests.sh
        step unity-playmode env EVIDENCE_DIR=.clean-clone-evidence/playmode TEST_PLATFORM=PlayMode tools/unity/run-playmode-tests.sh
        step android-apk env EVIDENCE_DIR=.clean-clone-evidence/android tools/unity/build-android-debug.sh
        if [ -d "$CLONE/.clean-clone-evidence" ]; then cp -r "$CLONE/.clean-clone-evidence/." "$OUTPUT/unity/" 2>/dev/null || { mkdir -p "$OUTPUT/unity"; cp -r "$CLONE/.clean-clone-evidence/." "$OUTPUT/unity/"; }; fi
    fi
fi

# Counts, read from the logs rather than asserted from memory.
{
    echo ""
    echo "## Counts (parsed from the logs)"
    echo ""
    if [ -f "$OUTPUT/ci-steps.log" ]; then
        echo "- CI steps: $(grep -Ec '^\[[0-9]+/[0-9]+\].* ok$' "$OUTPUT/ci-steps.log") ok of $(grep -Eo '^\[[0-9]+/[0-9]+\]' "$OUTPUT/ci-steps.log" | tail -1 | grep -Eo '/[0-9]+' | tr -d /)"
    fi
    if [ -f "$OUTPUT/dotnet-tests.log" ]; then
        grep -Eo '(Passed|Failed)! +- Failed: +[0-9]+, Passed: +[0-9]+, Skipped: +[0-9]+, Total: +[0-9]+' "$OUTPUT/dotnet-tests.log" | sed 's/ \+/ /g; s/^/- .NET: /' || true
    fi
    for x in editmode playmode; do
        f="$(ls "$OUTPUT"/unity/"$x"/*results.xml 2>/dev/null | head -1 || true)"
        if [ -n "$f" ]; then
            grep -Eo 'test-run id="2"[^>]*' "$f" | grep -Eo '(result|total|passed|failed|skipped)="[^"]*"' | tr '\n' ' ' | sed "s/^/- Unity ${x}: /"; echo ""
        fi
    done
    if [ -f "$OUTPUT/android-apk.log" ]; then grep -Eo 'WG_BUILD_RESULT [A-Za-z]+' "$OUTPUT/android-apk.log" | head -1 | sed 's/^/- Android: /'; fi
    echo ""
    echo "finished (UTC): $(date -u +%Y-%m-%dT%H:%M:%SZ)"
    echo ""
    if [ "$FAILED" -eq 0 ]; then echo "**CLEAN CLONE: PASS**"; else echo "**CLEAN CLONE: FAIL**"; fi
} >>"$REPORT"

echo "report: $REPORT"
[ "$FAILED" -eq 0 ]
