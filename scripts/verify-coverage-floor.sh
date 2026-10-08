#!/usr/bin/env bash
# Coverage floor gate for quantdale/simple-walk-game.
#
# Why a FLOOR and not a target: this repository does not chase a coverage
# percentage. It chases one specific failure -- an entire behaviour class losing
# its tests silently. `coverlet.collector` was referenced by all three test
# projects and never invoked by ANYTHING, so the test suite that found two P0
# data-loss defects had no aggregate measurement behind it at all, and a gap like
# "two public use cases with zero test references" was discoverable only by a
# targeted grep.
#
# The floor is therefore deliberately LOW: comfortably under the measured
# aggregate, so instrumentation or tooling drift does not fail the gate, and far
# above "the suites stopped running", which is the thing it exists to catch. A
# quality target belongs in a reviewed change, not in a CI constant.
#
# Every gate must live inside .github/workflows/ci.yml as a `run:` step so that
# scripts/verify-ci-steps.sh executes it locally (D-049). A gate in a script CI
# never calls is not a gate.
#
# Usage:
#   scripts/verify-coverage-floor.sh [floor]     # floor as a percentage, default 55
#
# Exit codes:
#   0  aggregate line coverage is at or above the floor
#   1  aggregate line coverage fell below the floor, or coverage could not be measured
set -eu

REPO_ROOT="$(git rev-parse --show-toplevel)"
cd "$REPO_ROOT"

FLOOR="${1:-55}"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

echo "== collecting coverage (XPlat Code Coverage) =="
if ! dotnet test SimpleWalkGame.sln --no-build \
        --collect:"XPlat Code Coverage" \
        --results-directory "$WORK/coverage" >"$WORK/test.log" 2>&1; then
    echo "ERROR: the test suite failed while collecting coverage" >&2
    sed 's/^/      | /' "$WORK/test.log" >&2
    exit 1
fi

# coverlet writes one cobertura report per test project.
REPORTS="$(find "$WORK/coverage" -name 'coverage.cobertura.xml' | sort)"
if [ -z "$REPORTS" ]; then
    echo "ERROR: coverage was collected but no cobertura report was produced" >&2
    exit 1
fi

python3 - "$FLOOR" $REPORTS <<'PY'
import sys
import xml.etree.ElementTree as ET

floor = float(sys.argv[1])
reports = sys.argv[2:]

covered_total = 0
valid_total = 0
print("  per-assembly line coverage:")
for path in reports:
    root = ET.parse(path).getroot()
    covered = int(root.get("lines-covered", "0"))
    valid = int(root.get("lines-valid", "0"))
    rate = (covered / valid * 100.0) if valid else 0.0
    covered_total += covered
    valid_total += valid
    print(f"    {rate:6.2f}%  ({covered}/{valid})  {path.split('/')[-2][:8]}")

aggregate = (covered_total / valid_total * 100.0) if valid_total else 0.0
print("")
print(f"  aggregate line coverage: {aggregate:.2f}%  ({covered_total}/{valid_total})")
print(f"  floor: {floor:.2f}%")

if aggregate + 1e-9 < floor:
    print(f"  FAIL: coverage fell below the floor by {floor - aggregate:.2f} points", file=sys.stderr)
    sys.exit(1)

print("  PASS: coverage is at or above the floor")
PY

echo ""
echo "======================================"
echo "COVERAGE FLOOR: satisfied"
echo "======================================"
