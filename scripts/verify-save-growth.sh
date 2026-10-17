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
#   scripts/verify-save-growth.sh --self-test   # seeded violations must fail (D-187)
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

# The gate's judgement over two SCENARIO-SIZES files; shared with --self-test (D-187).
check_series() { # run1.sizes run2.sizes
    S1="$1"; S2="$2"
    if cmp -s "$S1" "$S2"; then
        echo "  PASS: scenario sizes reproduce exactly across two independent runs"
        echo ""
        echo "  save-size growth series (PERFORMANCE_BUDGETS 15):"
        while read -r _ line; do
            day="$(printf '%s' "$line" | sed -n 's/.*day=\([0-9]*\).*/\1/p')"
            bytes="$(printf '%s' "$line" | sed -n 's/.*saveBytes=\([0-9]*\).*/\1/p')"
            echo "    day ${day}: ${bytes} bytes"
        done < "$S1"
    else
        echo "  FAIL: scenario sizes drifted between runs" >&2
        diff -u "$S1" "$S2" | sed 's/^/      | /' >&2 || true
        return 1
    fi

    # A region that never completes, or a save that fails to grow, would both silently
    # make this gate pass. Assert the series is actually doing work.
    if ! grep -q 'regionCompleted=True' "$S1"; then
        echo "  FAIL: no checkpoint reached a completed region; the growth series is not exercising maturity" >&2
        return 1
    fi
    if ! grep -q 'completedProjects=19 ' "$S1"; then
        echo "  FAIL: no checkpoint reached all 19 projects completed" >&2
        return 1
    fi
    echo "  PASS: growth series reaches a completed region with all projects unlocked"

    # Convergence (F-07 / D-066): a growth-MODEL check, not a byte count. Growth over the
    # second year (day 365 -> 730) must be at most CONVERGENCE_PCT % of growth over the first
    # (day 7 -> 365). Before ledger retirement the ratio was ~100 % (linear, no plateau).
    CONVERGENCE_PCT=60
    bytes_at() { sed -n "s/.*day=$1 .*saveBytes=\([0-9]*\).*/\1/p" "$S1" | head -n1; }
    b7="$(bytes_at 7)"; b365="$(bytes_at 365)"; b730="$(bytes_at 730)"
    if [ -z "$b7" ] || [ -z "$b365" ] || [ -z "$b730" ]; then
        echo "  FAIL: growth series is missing the day 7/365/730 checkpoints needed for the convergence check" >&2
        return 1
    fi
    first=$((b365 - b7)); second=$((b730 - b365))
    ratio=$(( first > 0 ? second * 100 / first : 100 ))
    if [ "$ratio" -gt "$CONVERGENCE_PCT" ]; then
        echo "  FAIL: save growth does not converge: second-year growth ${second} B is ${ratio}% of first-year growth ${first} B (bound ${CONVERGENCE_PCT}%)" >&2
        return 1
    fi
    echo "  PASS: save growth converges: second-year growth ${second} B is ${ratio}% of first-year ${first} B (bound ${CONVERGENCE_PCT}%)"
}

if [ "${1:-}" = "--self-test" ]; then
    # D-187: prove each judgement fails on a seeded violation, without the simulation.
    t="$(mktemp -d)"; trap 'rm -rf "$t"' EXIT
    rc=0
    line() { # day bytes projects completed
        echo "SCENARIO-SIZES day=$1 saveBytes=$2 processedLedger=1 ledgerRecords=1 ledgerVitality=1 materials=1 knowledge=1 completedProjects=$3 regionCompleted=$4"
    }
    series() { # file b7 b365 b730 projects completed
        { line 7 "$2" 3 False; line 365 "$3" "$5" "$6"; line 730 "$4" "$5" "$6"; } > "$1"
    }
    expect() { # want label run1 run2
        local want="$1" label="$2" got=0
        check_series "$3" "$4" >/dev/null 2>&1 || got=1
        if [ "$got" = "$want" ]; then echo "  PASS: $label"; else echo "  FAIL: $label (got $got, want $want)" >&2; rc=1; fi
    }
    series "$t/good" 1000 11000 14000 19 True
    series "$t/drift" 1000 11000 14001 19 True
    series "$t/incomplete" 1000 11000 14000 19 False
    series "$t/few" 1000 11000 14000 18 True
    series "$t/linear" 1000 11000 21000 19 True
    { line 7 1000 3 False; line 730 14000 19 True; } > "$t/gap"
    expect 0 "a converging, completed, reproducible series passes" "$t/good" "$t/good"
    expect 1 "a one-byte drift between runs fails" "$t/good" "$t/drift"
    expect 1 "a series that never completes the region fails" "$t/incomplete" "$t/incomplete"
    expect 1 "a series that stops short of 19 projects fails" "$t/few" "$t/few"
    expect 1 "linear (non-converging) growth fails" "$t/linear" "$t/linear"
    expect 1 "a missing day-365 checkpoint fails" "$t/gap" "$t/gap"
    [ "$rc" -eq 0 ] && echo "SAVE GROWTH SELF-TEST: PASS" || echo "SAVE GROWTH SELF-TEST: FAIL" >&2
    exit "$rc"
fi

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

check_series "$WORK/run1.sizes" "$WORK/run2.sizes" || exit 1

echo ""
echo "======================================"
echo "SAVE GROWTH: verified (timings not gated)"
echo "======================================"
