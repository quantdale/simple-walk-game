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
#   6. Every script CI invokes directly is committed executable and LF-only.
#
# Rule 6 exists because a gate script committed without the executable bit passes
# every local run on Windows (which ignores the bit) and then fails in CI with
# "Permission denied". That exact latent breakage was introduced and shipped once
# already; this rule makes it fail locally instead.
#
# The netstandard2.1 half of rules 1-3 was reworked to read the EFFECTIVE MSBuild
# property instead of grepping a csproj, when the shared settings moved into
# Directory.Build.props. It was re-proved against a broken fixture exactly the way
# rule 6 was: a scratch copy of WalkGame.Domain.csproj with the target moved to
# net9.0 makes the rule fail, and a probe of a project that is legitimately
# net9.0 proves the reader is not returning a constant.
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

# Reads a property as MSBuild actually computes it for a project, rather than
# grepping one file for a literal.
#
# Why this is not a grep: build settings are declared once in
# Directory.Build.props / src/Directory.Build.props, so the file a property is
# written in is an implementation detail, not the contract. The contract is the
# EFFECTIVE value. (scripts/verify-architecture.sh previously grepped
# <TargetFramework>netstandard2.1</TargetFramework> straight out of each csproj,
# which would have silently stopped checking anything the moment the property
# moved — a gate that silently stops checking is worse than no gate.)
#
# Fails closed: if the SDK is absent, the project cannot be evaluated, or the
# answer is empty, it prints nothing and returns non-zero, and the caller
# records a FAIL. It never prints a value it did not read from MSBuild.
effective_prop() {
    prop="$2"
    if ! command -v dotnet >/dev/null 2>&1; then
        return 1
    fi
    out="$(dotnet msbuild "$1" "-getProperty:$prop" 2>/dev/null | tr -d '\r' | sed -n '1p')"
    [ -n "$out" ] || return 1
    echo "$out"
}

# The effective-property reader is itself a rule: if it cannot evaluate, or it
# reports netstandard2.1 for a project that is legitimately net9.0, then the
# netstandard2.1 rule below would pass for the wrong reason and a production
# layer moved to net9.0 would sail through. Proved to fail by pointing the
# reader at tests/WalkGame.Domain.Tests (net9.0 by design) and by moving
# src/WalkGame.Domain to net9.0 in a scratch copy.
PROBE="tests/WalkGame.Domain.Tests/WalkGame.Domain.Tests.csproj"
if [ ! -f "$PROBE" ]; then
    bad "self-check probe $PROBE is missing; the effective-property rule cannot be trusted"
elif ! probe_tfm="$(effective_prop "$PROBE" TargetFramework)"; then
    bad "self-check: 'dotnet msbuild -getProperty' is unavailable or failed; the netstandard2.1 rule would pass vacuously"
elif [ "$probe_tfm" = "netstandard2.1" ]; then
    bad "self-check is vacuous: $PROBE reports netstandard2.1, so the reader is not reading real values"
fi

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
    # Read as the EFFECTIVE target framework: it may be declared in the csproj or
    # inherited from src/Directory.Build.props, and both are legitimate. What is
    # not legitimate is a core layer that is not netstandard2.1 — proved against a
    # deliberately broken fixture (see "rule 1-3" note at the end of this file).
    if ! tfm="$(effective_prop "$file" TargetFramework)"; then
        bad "$layer: could not read the effective TargetFramework via 'dotnet msbuild -getProperty' (no dotnet CLI, or the project failed to evaluate); the netstandard2.1 rule was NOT checked and must not be assumed to hold"
    elif [ "$tfm" != "netstandard2.1" ]; then
        bad "$layer is targeting ${tfm}, not netstandard2.1"
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

echo "== rule 6: CI-invoked scripts are executable and LF-only =="
ci_scripts="$(sed -n 's/^[[:space:]]*run:[[:space:]]*\(scripts\/[A-Za-z0-9._-]*\.sh\).*/\1/p' \
             .github/workflows/ci.yml 2>/dev/null | sort -u)"
ci_scripts="$ci_scripts
$(sed -n 's/^[[:space:]]*run:[[:space:]]*\(tests\/guards\/[A-Za-z0-9._/-]*\.sh\).*/\1/p' \
             .github/workflows/ci.yml 2>/dev/null | sort -u)"

if [ -z "$(printf '%s' "$ci_scripts" | tr -d '[:space:]')" ]; then
    bad "no CI-invoked shell scripts were discovered; the extraction pattern is broken"
else
    for s in $ci_scripts; do
        if [ ! -f "$s" ]; then
            bad "CI invokes $s but it does not exist"
            continue
        fi
        mode="$(git ls-files -s -- "$s" | awk '{print $1}')"
        if [ "$mode" != "100755" ]; then
            bad "CI invokes $s directly but it is committed with mode ${mode:-unknown}, not 100755"
        elif grep -qU $'\r' "$s"; then
            bad "$s contains CRLF; a POSIX shebang script with CRLF breaks under sh"
        else
            ok "$s is committed executable and LF-only"
        fi
    done
fi

echo ""
echo "=============================="
echo "ARCHITECTURE CHECKS: $PASS passed, $FAIL failed"
echo "=============================="
[ "$FAIL" -eq 0 ]
