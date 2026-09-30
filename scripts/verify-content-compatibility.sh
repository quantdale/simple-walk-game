#!/usr/bin/env bash
# Content-drift gate for quantdale/simple-walk-game.
#
# RISK_REGISTER R-018 ("Content IDs/schema drift break mature saves", High) listed
# four intended mitigations. Immutable stable IDs and the content validator were
# enforced; the tombstone policy did not exist and NOTHING detected drift before it
# destroyed a profile.
#
# The failure this prevents is specific and unrecoverable. Canonical state fails
# CLOSED on unknown content -- which is correct, and is exactly what makes a content
# rename dangerous. If a campaign renames proj.clear-trailhead, or drops a discovery an
# existing save already unlocked, every existing save fails validation on boot, BOTH
# generations are unreadable, and the one-generation backup holds the same invalid
# content so it fails too. The player has no recovery path.
#
# This gate proves the SHIPPED content still resolves saves across their whole life:
#   1. the shipped content validates with zero violations;
#   2. mature saves generated at several ages all decode and validate against it;
#   3. the recorded AUTHORED content version on each save is reported, so drift is
#      visible even when it happens to be compatible (D-059).
#
# It is wired into .github/workflows/ci.yml deliberately: scripts/verify-ci-steps.sh
# (D-049) executes the workflow verbatim, so a script CI does not call is not a gate.
#
# Usage:
#   scripts/verify-content-compatibility.sh                 # full gate
#   scripts/verify-content-compatibility.sh --with-fixtures # also validate every
#                                                        # save file passed as an
#                                                        # extra argument
#
# Exit codes:
#   0  shipped content and every mature save resolve cleanly
#   1  at least one content or save validation violation, or an unresolved identifier
#   2  setup failure (wrong repo, CLI could not be built)
set -eu

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null)" || true
if [ -z "$REPO_ROOT" ] || [ ! -d "$REPO_ROOT/src" ]; then
    echo "ERROR: run this from inside the simple-walk-game checkout" >&2
    exit 2
fi
cd "$REPO_ROOT" || exit 2

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

BUILD_LOG="$WORK/build.log"
echo "== building simulation CLI =="
if ! dotnet build tools/simulation -v quiet --nologo >"$BUILD_LOG" 2>&1; then
    echo "ERROR: could not build tools/simulation" >&2
    sed 's/^/      | /' "$BUILD_LOG" >&2
    exit 2
fi

FAIL=0
note_fail() { echo "  FAIL: $1" >&2; FAIL=$((FAIL + 1)); }
note_pass() { echo "  PASS: $1"; }

# ---------------------------------------------------------------------------
# 1. The shipped content itself.
# ---------------------------------------------------------------------------
echo "== shipped content validates =="
CONTENT_DIR="$WORK/content-check"
mkdir -p "$CONTENT_DIR"
# A fresh save from the shipped catalog exercises ContentValidator at construction
# time: GameSession refuses to build against invalid content.
if ! dotnet run --project tools/simulation --no-build -- \
        new --save "$CONTENT_DIR" --seed 7 --at 2026-08-20T08:00:00Z >"$WORK/new.log" 2>&1; then
    note_fail "the shipped Region 1 content does not construct (see below)"
    sed 's/^/      | /' "$WORK/new.log" >&2
else
    note_pass "the shipped Region 1 content constructs and validates"
fi

# ---------------------------------------------------------------------------
# 2. Mature saves across their whole life.
# ---------------------------------------------------------------------------
echo "== mature saves resolve against the shipped content =="
# A save is only meaningful if the CONTENT IT REFERENCES still resolves. Each profile
# below drives the real pipeline to a different maturity, then the resulting save is
# validated on its own -- which decodes it, runs GameStateValidator against the shipped
# Region 1 catalog, and reports the authored content version.
for spec in "low 12" "moderate 40" "high 80" "irregular 60" "weekend-heavy 50"; do
    profile="${spec% *}"
    days="${spec#* }"
    dir="$WORK/mature-$profile"
    mkdir -p "$dir"

    if ! dotnet run --project tools/simulation --no-build -- \
            profile --save "$dir" --profile "$profile" --days "$days" \
            >"$WORK/profile-$profile.log" 2>&1; then
        note_fail "profile '$profile' could not be driven to maturity (see below)"
        sed 's/^/      | /' "$WORK/profile-$profile.log" >&2
        continue
    fi

    verdict="$(dotnet run --project tools/simulation --no-build -- \
        validate --save "$dir" 2>&1 | grep '^validate:' || true)"
        if [ -z "$verdict" ]; then
            note_fail "profile '$profile': validate produced no verdict (see below)"
            dotnet run --project tools/simulation --no-build -- \
                validate --save "$dir" 2>&1 | sed 's/^/      | /' >&2
            continue
        fi
    case "$verdict" in
        *"violations=0"*)
            if echo "$verdict" | grep -q 'content=NEWER-THAN-BUILD'; then
                note_fail "profile '$profile': save is NEWER than the build's content — $verdict"
            else
                note_pass "profile '$profile' (${days}d): $verdict"
            fi
            ;;
        *)
            note_fail "profile '$profile': $verdict"
            dotnet run --project tools/simulation --no-build -- \
                validate --save "$dir" 2>&1 | sed 's/^/      | /' >&2
            ;;
    esac
done

# ---------------------------------------------------------------------------
# 3. Optional: any extra save files the caller wants checked.
# ---------------------------------------------------------------------------
if [ "${1:-}" = "--with-fixtures" ] && [ "${#}" -gt 1 ]; then
    echo "== caller-supplied save fixtures =="
    shift
    for save in "$@"; do
        if [ ! -f "$save" ]; then
            note_fail "$save: file not found — the gate cannot check a fixture it cannot read"
            continue
        fi
        dir="$(dirname "$save")"
        output="$(dotnet run --project tools/simulation --no-build -- \
            validate --save "$dir" 2>&1 || true)"
        verdict="$(printf '%s\n' "$output" | grep '^validate:' || true)"
        if [ -z "$verdict" ]; then
            # Boot refused the save, which is exactly the content-drift failure this gate
            # exists to catch. Report WHY, not just that it failed: "no verdict" alone
            # would send a reader looking for a harness bug instead of a content break.
            note_fail "$save: save did not validate (see below)"
            printf '%s\n' "$output" | sed 's/^/      | /' >&2
            continue
        fi
        case "$verdict" in
            *"violations=0"*)
                if printf '%s' "$verdict" | grep -q 'content=NEWER-THAN-BUILD'; then
                    note_fail "$save: save is NEWER than the build's content — $verdict"
                else
                    note_pass "$save: $verdict"
                fi
                ;;
            *)
                note_fail "$save: $verdict"
                printf '%s\n' "$output" | sed 's/^/      | /' >&2
                ;;
        esac
    done
fi

echo ""
echo "======================================"
if [ "$FAIL" -eq 0 ]; then
    echo "CONTENT COMPATIBILITY: all checks passed"
else
    echo "CONTENT COMPATIBILITY: $FAIL check(s) failed"
fi
echo "======================================"
[ "$FAIL" -eq 0 ]
