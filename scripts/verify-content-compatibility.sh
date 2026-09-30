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
#   scripts/verify-content-compatibility.sh --with-fixtures -- <save files...>
#                                                        # also validate each named save
#   scripts/verify-content-compatibility.sh --self-test    # prove the fixture
#                                                        # path REJECTS a bad save
#
# --self-test exists because a gate nobody has watched fail is not a gate. This
# repository has already shipped one unproven gate (verify-architecture.sh rule 6
# exists for that reason). It plants a deliberately corrupt save BESIDE a valid one --
# the exact arrangement that made the fixture path a false pass before 2026-09-30 -- and
# requires the gate to REJECT it. A regression to the old behaviour fails the self-test.
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

if [ "${1:-}" = "--self-test" ]; then
    echo "== fixture-path self-test: the gate MUST reject a corrupt save =="
    # A valid save and a corrupt one in the SAME directory. Before the isolation fix the
    # corrupt one reported "violations=0 ... EXACT-MATCH" because the validator was handed
    # the directory and read the valid save.json instead.
    st_dir="$WORK/selftest-fixtures"
    mkdir -p "$st_dir"
    st_clock="$st_dir/clock"
    dotnet run --project tools/simulation --no-build -- new --save "$st_clock" \
        --seed 7 --at 2026-08-20T08:00:00Z >/dev/null 2>&1 || true
    printf 'not-a-save-at-all' > "$st_dir/corrupt.json"

    if bash "$0" --with-fixtures "$st_dir/corrupt.json" >"$WORK/selftest.log" 2>&1; then
        echo "SELF-TEST FAIL: the gate accepted a corrupt save fixture." >&2
        sed 's/^/      | /' "$WORK/selftest.log" >&2
        echo "SELF-TEST FAIL: a gate that cannot fail is not a gate." >&2
        exit 1
    fi
    echo "  PASS: a corrupt save fixture is REJECTED (the gate can fail)"

    # The converse half: a real save must still be accepted, so the self-test cannot be
    # satisfied by simply rejecting everything.
    if bash "$0" --with-fixtures "$st_clock/save.json" >"$WORK/selftest-good.log" 2>&1; then
        echo "  PASS: a valid save fixture is still accepted (no over-rejection)"
    else
        echo "SELF-TEST FAIL: the gate rejected a VALID save fixture." >&2
        sed 's/^/      | /' "$WORK/selftest-good.log" >&2
        exit 1
    fi

    echo ""
    echo "CONTENT COMPATIBILITY SELF-TEST: passed"
    exit 0
fi

# ---------------------------------------------------------------------------
# 3. Optional: any extra save files the caller wants checked.
# ---------------------------------------------------------------------------
if [ "${1:-}" = "--with-fixtures" ] && [ "${#}" -gt 1 ]; then
    echo "== caller-supplied save fixtures =="
    shift
    # Each fixture is staged into its OWN isolated directory as save.json.
    #
    # This must not be `dirname "$save"`. The validator reads save.json (then falls back
    # to save.backup.json) FROM THE DIRECTORY IT IS GIVEN, so validating a fixture's
    # parent directory validates whatever unrelated save happens to sit there. A corrupt
    # fixture sitting next to any valid save then reported
    # "violations=0 ... EXACT-MATCH" and exited 0 -- a false PASS on precisely the
    # unresolvable-save condition this gate exists to detect. Proven during review on
    # 2026-09-30; isolation is the fix.
    fixture_index=0
    for save in "$@"; do
        if [ ! -f "$save" ]; then
            note_fail "$save: file not found — the gate cannot check a fixture it cannot read"
            continue
        fi
        fixture_index=$((fixture_index + 1))
        isolate="$WORK/fixture-$fixture_index"
        mkdir -p "$isolate"
        cp "$save" "$isolate/save.json"
        output="$(dotnet run --project tools/simulation --no-build -- \
            validate --save "$isolate" 2>&1 || true)"
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
