#!/usr/bin/env bash
# Dependency health gate for quantdale/simple-walk-game.
#
# Two properties this repository claims about its dependencies are currently
# observed once by hand: the runtime surface is ONE first-party package, and no
# package carries a known advisory. Both are stated in PRIVACY_REVIEW.md section 2
# and both are load-bearing for the offline-first, zero-network thesis -- so both
# are enforced here rather than remembered.
#
# The gate is deliberately conservative about what "enforced" means:
#   * `dotnet list package --vulnerable` fails the build on a KNOWN advisory;
#   * the resolved package inventory is printed, so "one first-party runtime
#     package" is a reviewable fact on every run rather than a claim in a document;
#   * the inventory is emitted as a machine-readable artifact, which is the
#     smallest honest substitute for a full SBOM: generating a CycloneDX document
#     requires a new third-party tool, and this change's non-goals forbid adding
#     dependencies for tooling's sake.
#
# Every gate must live inside .github/workflows/ci.yml as a `run:` step so that
# scripts/verify-ci-steps.sh executes it locally (D-049).
#
# Usage:
#   scripts/verify-dependency-health.sh [inventory-output.json]
#   scripts/verify-dependency-health.sh --self-test   # seeded violations must fail (D-187)
#
# Exit codes:
#   0  no known advisories; inventory emitted
#   1  a known advisory exists, or the inventory could not be produced
set -eu

REPO_ROOT="$(git rev-parse --show-toplevel)"
cd "$REPO_ROOT"

# --- gate logic, shared by the real run and --self-test ------------------------------

# Prints one "project|package|requested|resolved" row per top-level package of a
# shipped project in a `dotnet list package --format json` inventory.
runtime_rows() { # inventory.json
    python3 - "$1" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as handle:
    data = json.load(handle)

shipped = [
    "WalkGame.Domain",
    "WalkGame.Application",
    "WalkGame.Infrastructure",
]

rows = []
for project in data.get("projects", []):
    # The inventory keys projects by PATH, not by name.
    path = project.get("path", "")
    if not any(shipped in path for shipped in shipped):
        continue
    if "/tests/" in path.replace("\\", "/"):
        continue  # test-side packages are listed separately below, not part of the runtime claim
    name = path.split("/")[-1]
    for framework in project.get("frameworks", []):
        for package in framework.get("topLevelPackages", []):
            rows.append(
                f"{name}|{package['id']}|"
                f"{package.get('requestedVersion')}|{package.get('resolvedVersion')}"
            )

if not rows:
    # WalkGame.Domain and WalkGame.Application declare no PackageReference at all;
    # that is the claim being measured, so say it rather than print nothing.
    print("WalkGame.Domain|(no package references)|-|-")
    print("WalkGame.Application|(no package references)|-|-")

print("\n".join(rows))
PY
}

# Counts real package rows. D-187: this used to count only rows matching
# '|System' or 'resolved', so a non-System third-party package was invisible and
# the "exactly one runtime package" claim could pass with two.
count_runtime() {
    grep '|' | grep -vc '(no package references)' || true
}

# 0 when `dotnet list package --vulnerable` text says nothing is affected.
vulnerable_text_clean() { # vulnerable.txt
    grep -qi 'no vulnerable packages' "$1"
}

EXPECTED_RUNTIME_PACKAGES=1

if [ "${1:-}" = "--self-test" ]; then
    t="$(mktemp -d)"; trap 'rm -rf "$t"' EXIT
    rc=0
    expect() { # want(0|1) label command...
        local want="$1" label="$2"; shift 2
        if "$@"; then got=0; else got=1; fi
        if [ "$got" = "$want" ]; then echo "  PASS: $label"; else echo "  FAIL: $label (got $got, want $want)" >&2; rc=1; fi
    }
    inventory() { # file package-ids...
        local f="$1"; shift
        python3 - "$f" "$@" <<'PY'
import json, sys
pkgs = [{"id": p, "requestedVersion": "1.0.0", "resolvedVersion": "1.0.0"} for p in sys.argv[2:]]
json.dump({"projects": [
    {"path": "/r/src/WalkGame.Domain/WalkGame.Domain.csproj", "frameworks": [{"framework": "netstandard2.1"}]},
    {"path": "/r/src/WalkGame.Infrastructure/WalkGame.Infrastructure.csproj",
     "frameworks": [{"framework": "netstandard2.1", "topLevelPackages": pkgs}]},
    {"path": "/r/tests/WalkGame.Infrastructure.Tests/WalkGame.Infrastructure.Tests.csproj",
     "frameworks": [{"framework": "net9.0", "topLevelPackages": [{"id": "NUnit", "requestedVersion": "1", "resolvedVersion": "1"}]}]},
]}, open(sys.argv[1], "w"))
PY
    }
    count_is() { [ "$(runtime_rows "$1" | count_runtime)" -eq "$2" ]; }
    inventory "$t/one.json" System.Text.Json
    inventory "$t/two.json" System.Text.Json Newtonsoft.Json
    inventory "$t/other.json" Newtonsoft.Json
    inventory "$t/none.json"
    expect 0 "one shipped package counts as 1" count_is "$t/one.json" 1
    expect 0 "a second, non-System package is counted (2)" count_is "$t/two.json" 2
    expect 0 "a lone non-System package is counted (1)" count_is "$t/other.json" 1
    expect 0 "no packages counts as 0" count_is "$t/none.json" 0
    expect 1 "two packages break the documented runtime surface" count_is "$t/two.json" "$EXPECTED_RUNTIME_PACKAGES"
    echo "No vulnerable packages given current sources." > "$t/clean.txt"
    printf "Project 'X' has the following vulnerable packages\n   > Foo 1.0.0  High  https://example.invalid\n" > "$t/dirty.txt"
    expect 0 "clean advisory text passes" vulnerable_text_clean "$t/clean.txt"
    expect 1 "an advisory listing fails" vulnerable_text_clean "$t/dirty.txt"
    [ "$rc" -eq 0 ] && echo "DEPENDENCY HEALTH SELF-TEST: PASS" || echo "DEPENDENCY HEALTH SELF-TEST: FAIL" >&2
    exit "$rc"
fi

INVENTORY="${1:-}"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

echo "== restoring (dependency resolution is part of this gate) =="
dotnet restore SimpleWalkGame.sln >"$WORK/restore.log" 2>&1 || {
    echo "ERROR: restore failed" >&2
    sed 's/^/      | /' "$WORK/restore.log" >&2
    exit 1
}

echo "== known advisories =="
if dotnet list package --vulnerable --include-transitive >"$WORK/vulnerable.txt" 2>&1; then
    # A clean run prints a "no vulnerable packages" line; an affected run lists them
    # and, since .NET 8, can still exit 0, so the TEXT is what decides.
    if vulnerable_text_clean "$WORK/vulnerable.txt"; then
        echo "  PASS: no package has a known vulnerability advisory"
    else
        echo "  FAIL: `dotnet list package --vulnerable` reported affected packages:" >&2
        sed 's/^/      | /' "$WORK/vulnerable.txt" >&2
        exit 1
    fi
else
    echo "  FAIL: the vulnerability check itself failed to run:" >&2
    sed 's/^/      | /' "$WORK/vulnerable.txt" >&2
    exit 1
fi

echo "== resolved package inventory =="
dotnet list package --include-transitive --format json >"$WORK/inventory.json" 2>/dev/null || {
    echo "ERROR: could not produce the package inventory" >&2
    exit 1
}

# The runtime surface claim, measured rather than asserted: the projects that
# ship (Domain, Application, Infrastructure) must resolve exactly one package.
RUNTIME_JSON="$(runtime_rows "$WORK/inventory.json")"

RUNTIME_PACKAGES="$(printf '%s\n' "$RUNTIME_JSON" | count_runtime)"
printf '%s\n' "$RUNTIME_JSON" | grep '|' | sed 's/^/    /' || true

# The measured claim: exactly one third-party runtime package (EXPECTED_RUNTIME_PACKAGES above).
if [ "$RUNTIME_PACKAGES" -ne "$EXPECTED_RUNTIME_PACKAGES" ]; then
    echo "  FAIL: the runtime surface is $RUNTIME_PACKAGES packages, expected $EXPECTED_RUNTIME_PACKAGES." >&2
    echo "        PRIVACY_REVIEW.md section 2 documents one first-party runtime dependency;" >&2
    echo "        either the dependency set changed (update the review) or the gate is wrong." >&2
    exit 1
fi
echo "  PASS: the runtime surface is exactly one third-party package, as documented"

if [ -n "$INVENTORY" ]; then
    cp "$WORK/inventory.json" "$INVENTORY"
    echo "  inventory written to: $INVENTORY"
fi

echo ""
echo "======================================"
echo "DEPENDENCY HEALTH: verified"
echo "======================================"
