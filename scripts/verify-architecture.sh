#!/usr/bin/env bash
# Architecture fitness checks for quantdale/simple-walk-game.
#
# Implements the mechanically-verifiable subset of TECHNICAL_ARCHITECTURE.md
# section 23 "Architectural fitness checks" and the TESTING_AND_RELEASE.md
# section 16 CI gate "architecture dependency checks", neither of which existed.
#
# This is the countermeasure for RISK_REGISTER R-017 ("Architecture collapses
# into Unity scene scripts", High / Open), which had no evidence paragraph: the
# risk could not be observed because nothing checked the dependency direction.
#
# Rules enforced:
#   1. Domain references nothing at all (no project, no package, no framework
#      beyond netstandard2.1) and speaks no engine namespace.
#   2. Application references Domain and nothing else. It may name the BCL
#      exception IOException as a documented port-failure contract, but it must
#      not touch the filesystem.
#   3. Infrastructure references only Application and Domain.
#   4. No engine namespace (UnityEngine/UnityEditor/...) appears anywhere in src/.
#   5. Domain and Application perform no filesystem access.
#
# Exit codes:
#   0  every rule holds
#   1  at least one architectural rule was violated
#   2  not run from inside the repository checkout
set -u

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null)" || true
if [ -z "$REPO_ROOT" ] || [ ! -d "$REPO_ROOT/src" ]; then
    echo "ERROR: run this from inside the simple-walk-game checkout (git rev-parse failed)" >&2
    exit 2
fi
cd "$REPO_ROOT" || exit 2

PASS=0
FAIL=0
ok()  { PASS=$((PASS+1)); echo "  PASS: $1"; }
bad() { FAIL=$((FAIL+1)); echo "  FAIL: $1" >&2; }

# Reference names a csproj may legitimately declare, per layer.
allowed_refs() {
    case "$1" in
        WalkGame.Domain)        echo "" ;;
        WalkGame.Application)   echo "WalkGame.Domain" ;;
        WalkGame.Infrastructure) echo "WalkGame.Application WalkGame.Domain" ;;
        *)                      echo "" ;;
    esac
}

# csproj files for a project, by project name.
proj_file() { echo "src/$1/$1.csproj"; }

# Strips // and /// comments so prose like "no separate code path." cannot be
# mistaken for a Path. call site.
strip_comments() {
    sed -e 's://.*$::' "$1"
}

echo "== rule 1-3: project reference direction =="
for layer in WalkGame.Domain WalkGame.Application WalkGame.Infrastructure; do
    file="$(proj_file "$layer")"
    if [ ! -f "$file" ]; then
        bad "$layer: project file not found at $file"
        continue
    fi

    refs="$(grep -o 'ProjectReference Include="[^"]*"' "$file" \
            | sed -e 's/.*Include="//' -e 's/"$//' -e 's#.*[\\/]##' -e 's/\.csproj$//' \
            | sort -u | tr '\n' ' ' | sed -e 's/ *$//')"

    actual="$refs"
    expected="$(allowed_refs "$layer")"

    # Compare as sorted token sets so ordering never matters.
    norm() { echo "$1" | tr ' ' '\n' | grep -v '^$' | sort | tr '\n' ' '; }
    if [ "$(norm "$actual")" = "$(norm "$expected")" ]; then
        ok "$layer references exactly: ${expected:-(nothing)}"
    else
        bad "$layer references [${actual:-(nothing)}] but must reference [${expected:-(nothing)}]"
    fi

    # Domain must not pull in any NuGet package either.
    if [ "$layer" = "WalkGame.Domain" ] && grep -q 'PackageReference' "$file"; then
        bad "WalkGame.Domain declares a PackageReference; the domain must stay dependency-free"
    fi

    # Every core project must stay engine-free at the framework level too.
    if ! grep -q '<TargetFramework>netstandard2.1</TargetFramework>' "$file"; then
        bad "$layer is not targeting netstandard2.1"
    fi
done

echo "== rule 4: no engine namespace anywhere in src/ =="
engine_hits="$(grep -rn --include=*.cs -E '(^|[^A-Za-z0-9_.])(using[[:space:]]+)?UnityEngine|UnityEditor' src/ || true)"
if [ -z "$engine_hits" ]; then
    ok "no UnityEngine/UnityEditor reference in src/"
else
    bad "engine namespace leaked into src/:"
    printf '%s\n' "$engine_hits" | sed 's/^/      | /' >&2
fi

echo "== rule 5: no filesystem access in Domain or Application =="
for layer in WalkGame.Domain WalkGame.Application; do
    fs_hits=""
    while IFS= read -r f; do
        [ -z "$f" ] && continue
        hit="$(strip_comments "$f" \
            | grep -nE '\b(File|Directory|Path|FileInfo|FileStream|StreamWriter|StreamReader|DriveInfo)\b[[:space:]]*[.(]' \
            || true)"
        if [ -n "$hit" ]; then
            fs_hits="$fs_hits
$f: $hit"
        fi
    done <<EOF
$(find "src/$layer" -name '*.cs' -type f)
EOF

    if [ -z "$fs_hits" ]; then
        ok "$layer performs no filesystem access"
    else
        bad "$layer performs filesystem access:$fs_hits"
    fi
done

echo ""
echo "=============================="
echo "ARCHITECTURE CHECKS: $PASS passed, $FAIL failed"
echo "=============================="
[ "$FAIL" -eq 0 ]
