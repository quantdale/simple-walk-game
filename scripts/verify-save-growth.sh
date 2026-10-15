#!/usr/bin/env bash
# Save-growth / performance-scenario determinism gate for quantdale/simple-walk-game.
#
# PERFORMANCE_BUDGETS section 15 states: "CI simulation should report save-size growth
# across long-running synthetic profiles." Nothing did that. This gate makes the
# deterministic half of the section 19 scenario harness reportable and regression-proof.
#
# It runs `tools/simulation scenarios` twice and requires the SCENARIO-SIZES lines to
# be identical. Those lines carry save bytes, ledger sizes, credited totals and resource
# balances - all deterministic for a fixed seed and content. Wall-clock figures are
# deliberately EXCLUDED: they are machine-dependent, and TESTING_AND_RELEASE section 17
# treats flaky timing assertions as defects rather than as evidence.
#
# Usage:
#   scripts/verify-save-growth.sh
#
# Exit codes:
#   0  scenario sizes reproduce exactly across two independent runs
#   1  a size/count value drifted between runs
#   2  the scenarios harness could not be run
set -eu

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null)" || true
if [ -z "$REPO_ROOT" ] || [ ! -d "$REPO_ROOT/src" ]; then
    echo "ERROR: run this from inside the simple-walk-game checkout" >&2
    exit 2
fi
cd "$REPO_ROOT" || exit 2

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

echo "== building simulation CLI =="
if ! dotnet build tools/simulation -v quiet --nologo >"$WORK/build.log" 2>&1; then
    echo "ERROR: could not build tools/simulation" >&2
    sed 's/^/      | /' "$WORK/build.log" >&2
    exit 2
fi

run_sizes() { # out_file save_dir
    dotnet run --project tools/simulation --no-build -- \
        scenarios --save "$2" --seed 42 2>&1 \
        | grep '^SCENARIO-SIZES' > "$1"
}

run_sizes "$WORK/run1.sizes" "$WORK/run1"
run_sizes "$WORK/run2.sizes" "$WORK/run2"

if [ ! -s "$WORK/run1.sizes" ]; then
    echo "ERROR: scenarios produced no SCENARIO-SIZES lines" >&2
    exit 2
fi

if cmp -s "$WORK/run1.sizes" "$WORK/run2.sizes"; then
    echo "  PASS: scenario sizes reproduce exactly across two independent runs"
    echo ""
    echo "  save-size growth series (PERFORMANCE_BUDGETS 15):"
    while read -r _ line; do
        day="$(printf '%s' "$line" | sed -n 's/.*day=\([0-9]*\).*/\1/p')"
        bytes="$(printf '%s' "$line" | sed -n 's/.*saveBytes=\([0-9]*\).*/\1/p')"
        echo "    day ${day}: ${bytes} bytes"
    done < "$WORK/run1.sizes"
else
    echo "  FAIL: scenario sizes drifted between runs" >&2
    diff -u "$WORK/run1.sizes" "$WORK/run2.sizes" | sed 's/^/      | /' >&2 || true
    exit 1
fi

# A region that never completes, or a save that fails to grow, would both silently
# make this gate pass. Assert the series is actually doing work.
if ! grep -q 'regionCompleted=True' "$WORK/run1.sizes"; then
    echo "  FAIL: no checkpoint reached a completed region; the growth series is not exercising maturity" >&2
    exit 1
fi
if ! grep -q 'completedProjects=19' "$WORK/run1.sizes"; then
    echo "  FAIL: no checkpoint reached all 19 projects completed" >&2
    exit 1
fi
echo "  PASS: growth series reaches a completed region with all projects unlocked"

# Convergence (F-07 / D-066): a growth-MODEL check, not a byte count. Growth over the
# second year (day 365 -> 730) must be at most CONVERGENCE_PCT % of growth over the first
# (day 7 -> 365). Before ledger retirement the ratio was ~100 % (linear, no plateau).
CONVERGENCE_PCT=60
bytes_at() { sed -n "s/.*day=$1 .*saveBytes=\([0-9]*\).*/\1/p" "$WORK/run1.sizes" | head -n1; }
b7="$(bytes_at 7)"; b365="$(bytes_at 365)"; b730="$(bytes_at 730)"
if [ -z "$b7" ] || [ -z "$b365" ] || [ -z "$b730" ]; then
    echo "  FAIL: growth series is missing the day 7/365/730 checkpoints needed for the convergence check" >&2
    exit 1
fi
first=$((b365 - b7)); second=$((b730 - b365))
ratio=$(( first > 0 ? second * 100 / first : 100 ))
if [ "$ratio" -gt "$CONVERGENCE_PCT" ]; then
    echo "  FAIL: save growth does not converge: second-year growth ${second} B is ${ratio}% of first-year growth ${first} B (bound ${CONVERGENCE_PCT}%)" >&2
    exit 1
fi
echo "  PASS: save growth converges: second-year growth ${second} B is ${ratio}% of first-year ${first} B (bound ${CONVERGENCE_PCT}%)"

echo ""
echo "======================================"
echo "SAVE GROWTH: verified (timings not gated)"
echo "======================================"
