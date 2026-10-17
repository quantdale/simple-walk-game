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
#   scripts/verify-pacing-evidence.sh --self-test  # seeded drift must fail (D-187)
#   scripts/verify-pacing-evidence.sh --noise      # same reports under provider noise (D-190)
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

# 0 = byte-identical, 1 = drifted, 3 = no committed report. Shared with --self-test.
compare_report() { # committed actual
    [ -f "$1" ] || return 3
    cmp -s "$2" "$1"
}

# D-190: a --noise run must show the noise reached the pipeline (duplicates ignored, no-op
# corrections applied, unseen deletions tombstoned) and that none of it was rejected or
# deleted real credit. Prints the counts line; 0 = plausible, 1 = noise missing or harmful.
check_noise_counts() { # actual
    local line
    line="$(grep '^NOISE-COUNTS ' "$1" || true)"
    [ -n "$line" ] || { echo "no NOISE-COUNTS line"; return 1; }
    echo "$line"
    count() { printf '%s\n' "$line" | sed -n "s/.* $1=\([0-9]*\).*/\1/p"; }
    [ "$(count duplicatesIgnored)" -gt 0 ] && [ "$(count correctionsApplied)" -gt 0 ] \
        && [ "$(count deletionsIgnored)" -gt 0 ] && [ "$(count deletionsApplied)" -eq 0 ] \
        && [ "$(count rejected)" -eq 0 ]
}

if [ "${1:-}" = "--self-test" ]; then
    # D-187: prove the comparison rejects a drifted or missing report, without the simulation.
    t="$(mktemp -d)"; trap 'rm -rf "$t"' EXIT
    rc=0
    check() { # want label committed actual
        local want="$1" label="$2" got=0
        compare_report "$3" "$4" || got=$?
        if [ "$got" = "$want" ]; then echo "  PASS: $label"; else echo "  FAIL: $label (got $got, want $want)" >&2; rc=1; fi
    }
    for profile in $ALL_PROFILES; do
        [ -s "docs/evidence/m4/pacing-${profile}.txt" ] || { echo "  FAIL: no committed report for ${profile}" >&2; rc=1; }
    done
    src="docs/evidence/m4/pacing-low.txt"
    cp "$src" "$t/same.txt"
    python3 - "$src" "$t/flipped.txt" <<'PY'
import sys
b = bytearray(open(sys.argv[1], "rb").read())
i = len(b) // 2
b[i] = ord("0") if b[i] != ord("0") else ord("1")
open(sys.argv[2], "wb").write(bytes(b))
PY
    printf '\n' | cat "$src" - > "$t/extra-newline.txt"
    check 0 "an identical report reproduces" "$src" "$t/same.txt"
    check 1 "a one-byte drift is rejected" "$src" "$t/flipped.txt"
    check 1 "a trailing-newline drift is rejected" "$src" "$t/extra-newline.txt"
    check 3 "a missing committed report is rejected" "$t/absent.txt" "$t/same.txt"
    noise() { # want label line
        printf '%s\n' "$3" > "$t/noise.txt"; local got=0
        check_noise_counts "$t/noise.txt" >/dev/null || got=1
        if [ "$got" = "$1" ]; then echo "  PASS: $2"; else echo "  FAIL: $2 (got $got, want $1)" >&2; rc=1; fi
    }
    ok_line="NOISE-COUNTS received=9 duplicatesIgnored=3 staleRevisionsIgnored=0 correctionsApplied=2 deletionsIgnored=2 deletionsApplied=0 rejected=0"
    noise 0 "noise that reached the pipeline harmlessly passes" "$ok_line"
    noise 1 "a run with no noise line fails" "--- profile report: low ---"
    noise 1 "noise that produced no duplicates fails" "${ok_line/duplicatesIgnored=3/duplicatesIgnored=0}"
    noise 1 "noise that deleted real credit fails" "${ok_line/deletionsApplied=0/deletionsApplied=1}"
    noise 1 "noise the pipeline rejected fails" "${ok_line/rejected=0/rejected=4}"
    [ "$rc" -eq 0 ] && echo "PACING EVIDENCE SELF-TEST: PASS" || echo "PACING EVIDENCE SELF-TEST: FAIL" >&2
    exit "$rc"
fi

NOISE=0
if [ "${1:-}" = "--noise" ]; then NOISE=1; shift; fi
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

    noise_flag=""; [ "$NOISE" -eq 1 ] && noise_flag="--noise"
    if ! dotnet run --project tools/simulation --no-build -- \
        profile --save "$save_dir" --profile "$profile" --days "$DAYS" $noise_flag \
        >"$actual" 2>&1; then
        echo "  FAIL: ${profile}: simulation exited non-zero" >&2
        sed 's/^/      | /' "$actual" >&2
        FAIL=$((FAIL + 1))
        continue
    fi

    if [ "$NOISE" -eq 1 ]; then
        if ! counts="$(check_noise_counts "$actual")"; then
            echo "  FAIL: ${profile}: provider noise did not reach the pipeline harmlessly: ${counts}" >&2
            FAIL=$((FAIL + 1))
            continue
        fi
        echo "    ${profile} ${counts}"
        grep -v '^NOISE-COUNTS ' "$actual" > "$actual.report"
        actual="$actual.report"
    fi

    if compare_report "$committed" "$actual"; then
        echo "  PASS: ${profile} reproduces docs/evidence/m4/pacing-${profile}.txt$([ "$NOISE" -eq 1 ] && echo ' under provider noise')"
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
