#!/usr/bin/env bash
# Release evidence assembler for quantdale/simple-walk-game.
#
# TESTING_AND_RELEASE.md section 18 lists what a release evidence package must
# contain: commit SHA; build/version; test suite results; known issues by
# severity; privacy review; clean-clone/build result; remaining unverified
# claims. Every one of those already exists somewhere in this repository -- the
# problem was that assembling the package was a manual act, and a manual step
# under release pressure is a step that gets skipped.
#
# This script assembles the package from the documents the repository already
# maintains, VERIFYING the mechanical claims as it goes rather than copying
# prose. What can be checked mechanically is checked; what cannot is labelled
# UNVERIFIED with the reason, which is the repository's standing practice.
#
# Every gate must live inside .github/workflows/*.yml as a `run:` step so that
# scripts/verify-ci-steps.sh executes it locally (D-049).
#
# Usage:
#   scripts/assemble-release-evidence.sh --output <dir> [--tag <tag>] [--commit <sha>] [--clean-clone <dir>]
#
#   --clean-clone <dir>  a scripts/validate-clean-clone.sh output for THIS commit; its
#                        CLEAN_CLONE.md (and Unity results) are copied in and its verdict
#                        is checked. Without it the clean-clone claim is listed UNVERIFIED.
#
# Exit codes:
#   0  the package was assembled (UNVERIFIED items are reported, not fatal)
#   1  a mechanical claim in the package is FALSE (a release candidate cannot be assembled)
set -eu

REPO_ROOT="$(git rev-parse --show-toplevel)"
cd "$REPO_ROOT"

OUTPUT=""
TAG=""
COMMIT=""
CLEAN_CLONE=""
while [ $# -gt 0 ]; do
    case "$1" in
        --output) OUTPUT="$2"; shift 2 ;;
        --tag) TAG="$2"; shift 2 ;;
        --commit) COMMIT="$2"; shift 2 ;;
        --clean-clone) CLEAN_CLONE="$2"; shift 2 ;;
        *) echo "ERROR: unknown argument '$1'" >&2; exit 1 ;;
    esac
done

if [ -z "$OUTPUT" ]; then
    echo "ERROR: --output is required" >&2
    exit 1
fi

mkdir -p "$OUTPUT"
COMMIT="${COMMIT:-$(git rev-parse HEAD)}"
TAG="${TAG:-$(git describe --tags --exact-match HEAD 2>/dev/null || echo "(untagged)")}"

echo "== assembling release evidence for ${TAG} @ ${COMMIT:0:12} =="

{
    echo "# Release evidence package"
    echo ""
    echo "| field | value |"
    echo "|---|---|"
    echo "| tag | ${TAG} |"
    echo "| commit | ${COMMIT} |"
    echo "| assembled at | $(date -u +%Y-%m-%dT%H:%M:%SZ) |"
    echo "| repository | $(tr -d '\n' <.repo-identity.json | sed 's/.*"repository"[^"]*"\([^"]*\)".*/\1/') |"
    echo ""
    echo "## Mechanical claims (re-verified here, not copied)"
    echo ""
} >"$OUTPUT/README.md"

check() { # name, command...
    local name="$1"; shift
    if "$@" >"$OUTPUT/${name}.log" 2>&1; then
        echo "- **PASS** \`${name}\` — see \`${name}.log\`" >>"$OUTPUT/README.md"
        echo "  PASS: ${name}"
    else
        echo "- **FAIL** \`${name}\` — the release gate is not green" >>"$OUTPUT/README.md"
        echo "  FAIL: ${name}"
        exit 1
    fi
}

# The claims that can be checked mechanically, checked with the repository's own gates.
check build dotnet build SimpleWalkGame.sln
check test dotnet test SimpleWalkGame.sln --no-build
check architecture scripts/verify-architecture.sh
check pacing-evidence scripts/verify-pacing-evidence.sh
check save-growth scripts/verify-save-growth.sh
check content-compatibility scripts/verify-content-compatibility.sh
check guard-suite tests/guards/run-guard-tests.sh
check dependency-health scripts/verify-dependency-health.sh
check coverage-floor scripts/verify-coverage-floor.sh

# Clean-clone validation (D-075): verified from the validator's own report, for this commit.
if [ -n "$CLEAN_CLONE" ]; then
    if [ -f "$CLEAN_CLONE/CLEAN_CLONE.md" ] \
        && grep -q "CLEAN CLONE: PASS" "$CLEAN_CLONE/CLEAN_CLONE.md" \
        && grep -q "\`${COMMIT}\`" "$CLEAN_CLONE/CLEAN_CLONE.md"; then
        mkdir -p "$OUTPUT/clean-clone"
        cp "$CLEAN_CLONE"/CLEAN_CLONE.md "$OUTPUT/clean-clone/"
        if [ -d "$CLEAN_CLONE/unity" ]; then
            mkdir -p "$OUTPUT/clean-clone/unity"
            find "$CLEAN_CLONE/unity" -name '*results.xml' -exec cp {} "$OUTPUT/clean-clone/unity/" \;
        fi
        echo "- **PASS** \`clean-clone\` — fresh clone of this commit; see \`clean-clone/CLEAN_CLONE.md\`" >>"$OUTPUT/README.md"
        echo "  PASS: clean-clone"
    else
        echo "- **FAIL** \`clean-clone\` — report missing, failed, or for another commit" >>"$OUTPUT/README.md"
        echo "  FAIL: clean-clone"
        exit 1
    fi
else
    echo "- **UNVERIFIED** \`clean-clone\` — run scripts/validate-clean-clone.sh and pass --clean-clone" >>"$OUTPUT/README.md"
fi

# Hosted CI for this commit, read from GitHub when the CLI is available (never assumed).
if command -v gh >/dev/null 2>&1 && HOSTED="$(gh run list --commit "$COMMIT" --workflow ci --json conclusion,databaseId --jq '[.[] | select(.conclusion=="success")] | length' 2>/dev/null)" && [ "${HOSTED:-0}" -gt 0 ]; then
    echo "- **PASS** \`hosted-ci\` — ${HOSTED} successful hosted \`ci\` run(s) for this commit (\`gh run list --commit\`)" >>"$OUTPUT/README.md"
else
    echo "- **UNVERIFIED** \`hosted-ci\` — no successful hosted \`ci\` run found for this commit from here" >>"$OUTPUT/README.md"
fi

{
    echo ""
    echo "## Documented evidence (copied verbatim from the maintained documents)"
    echo ""
    echo "- known issues by severity: \`docs/KNOWN_ISSUES.md\`"
    echo "- privacy review: \`docs/PRIVACY_REVIEW.md\`"
    echo "- decisions affecting this release: \`docs/DECISIONS.md\`"
    echo "- release qualification model: \`docs/TESTING_AND_RELEASE.md\`"
    echo ""
    echo "## Claims that CANNOT be established here"
    echo ""
    echo "- runtime/DEVICE behaviour: launch, lifecycle (OS kill/reboot), TalkBack,"
    echo "  OS font scaling, Health Connect permission/read/deletions, on-device"
    echo "  performance/memory/battery, GPU rendering (KI-002: no device and no KVM)."
    echo "  Unity-editor-tested evidence exists for the shell (EditMode/PlayMode) and the"
    echo "  arm64 debug APK builds; that is not device evidence."
    echo "- iOS / HealthKit: not started."
    echo ""
    echo "The full device list is the Final Device and Release Qualification Checklist in"
    echo "\`RELEASE_CHECKLIST.md\` (copied into this package)."
    echo ""
    echo "These are stated as unverified rather than omitted: a release package that"
    echo "quietly drops its open blockers is worse than one that lists them."
} >>"$OUTPUT/README.md"

cp docs/KNOWN_ISSUES.md "$OUTPUT/KNOWN_ISSUES.md"
cp docs/PRIVACY_REVIEW.md "$OUTPUT/PRIVACY_REVIEW.md"
cp docs/TESTING_AND_RELEASE.md "$OUTPUT/TESTING_AND_RELEASE.md"
cp docs/RELEASE_CHECKLIST.md "$OUTPUT/RELEASE_CHECKLIST.md"

echo ""
echo "======================================"
echo "RELEASE EVIDENCE: assembled at ${OUTPUT}"
echo "======================================"
