#!/usr/bin/env bash
# Pacing-evidence reproducibility gate for quantdale/simple-walk-game.
#
# docs/ROADMAP.md, docs/DECISIONS.md (D-046) and docs/TESTING_AND_RELEASE.md all
# claim that every committed pacing report under docs/evidence/m4/ reproduces
# byte-for-byte. This script proves that claim mechanically instead of leaving it
# as documentation fiction (the exact failure mode RISK_REGISTER R-020 describes).
#
# For each profile it runs the real `tools/simulation profile` command into a
# throwaway save directory and compares the output to the committed report.
#
# Usage:
#   scripts/verify-pacing-evidence.sh              # verify all profiles
#   scripts/verify-pacing-evidence.sh irregular    # verify one profile
#
# Exit codes:
#   0  every requested profile reproduced its committed report byte-for-byte
#   1  at least one profile drifted (or its report is missing)
#   2  the simulation CLI could not be run at all
set -eu

REPO_ROOT="$(git rev-parse --show-toplevel)"
cd "$REPO_ROOT"

# Kept in the same order as the GAME_SYSTEMS 13 profile list and as D-046.
ALL_PROFILES="low moderate high irregular weekend-heavy inactivity pathological"
DAYS=400

PROFILES="${*:-$ALL_PROFILES}"

# The CLI is built once; every profile then reuses the same binary so a compile
# failure is reported once instead of seven times.
BUILD_LOG="$(mktemp)"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"; rm -f "$BUILD_LOG"' EXIT

echo "== building simulation CLI =="
if ! dotnet build tools/simulation -v quiet --nologo >"$BUILD_LOG" 2>&1; then
    echo "ERROR: could not build tools/simulation" >&2
    sed 's/^/      | /' "$BUILD_LOG" >&2
    exit 2
fi

PASS=0
FAIL=0

for profile in $PROFILES; do
    committed="docs/evidence/m4/pacing-${profile}.txt"
    if [ ! -f "$committed" ]; then
        echo "  FAIL: ${profile}: no committed report at ${committed}" >&2
        FAIL=$((FAIL + 1))
        continue
    fi

    save_dir="$WORK/${profile}"
    actual="$WORK/${profile}.actual"
    mkdir -p "$save_dir"

    if ! dotnet run --project tools/simulation --no-build -- \
        profile --save "$save_dir" --profile "$profile" --days "$DAYS" \
        >"$actual" 2>&1; then
        echo "  FAIL: ${profile}: simulation exited non-zero" >&2
        sed 's/^/      | /' "$actual" >&2
        FAIL=$((FAIL + 1))
        continue
    fi

    if cmp -s "$actual" "$committed"; then
        echo "  PASS: ${profile} reproduces docs/evidence/m4/pacing-${profile}.txt"
        PASS=$((PASS + 1))
    else
        echo "  FAIL: ${profile} drifted from its committed report" >&2
        diff -u "$committed" "$actual" | sed 's/^/      | /' >&2 || true
        FAIL=$((FAIL + 1))
    fi
done

echo ""
echo "=============================================="
echo "PACING EVIDENCE: $PASS reproduced, $FAIL drifted"
echo "=============================================="
[ "$FAIL" -eq 0 ]
