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
#   scripts/assemble-release-evidence.sh --output <dir> [--tag <tag>] [--commit <sha>]
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
while [ $# -gt 0 ]; do
    case "$1" in
        --output) OUTPUT="$2"; shift 2 ;;
        --tag) TAG="$2"; shift 2 ;;
        --commit) COMMIT="$2"; shift 2 ;;
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
    echo "| repository | $(cat .repo-identity.json | tr -d '\n' | sed 's/.*"slug"[^"]*"\([^"]*\)".*/\1/') |"
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
    echo "- hosted CI is green (KI-001: the account's runners execute zero steps);"
    echo "  the local gate reproduction is the only executable evidence that exists;"
    echo "- runtime/device behaviour including screens, accessibility notifications,"
    echo "  platform permission dialogs and performance budgets (KI-002: no Unity"
    echo "  editor exists in the environment)."
    echo ""
    echo "These are stated as unverified rather than omitted: a release package that"
    echo "quietly drops its open blockers is worse than one that lists them."
} >>"$OUTPUT/README.md"

cp docs/KNOWN_ISSUES.md "$OUTPUT/KNOWN_ISSUES.md"
cp docs/PRIVACY_REVIEW.md "$OUTPUT/PRIVACY_REVIEW.md"
cp docs/TESTING_AND_RELEASE.md "$OUTPUT/TESTING_AND_RELEASE.md"

echo ""
echo "======================================"
echo "RELEASE EVIDENCE: assembled at ${OUTPUT}"
echo "======================================"
