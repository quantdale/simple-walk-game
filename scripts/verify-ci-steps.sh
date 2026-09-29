#!/usr/bin/env bash
# Local CI-step reproduction gate for quantdale/simple-walk-game.
#
# Why this exists: hosted GitHub Actions runs for this repository are blocked by
# an account-level billing blocker (TESTING_AND_RELEASE.md section 16, "Hosted CI
# availability"). Every run since 2026-08-30 allocates no runner (steps: 0). The
# CI body itself is therefore the only executable definition of the release gate,
# and until now no tool could execute that body as-written outside GitHub. The
# steps had to be retyped by hand, which is exactly the drift this script exists
# to prevent: what CI runs and what local verification runs were two different
# things.
#
# This script parses .github/workflows/ci.yml, extracts EVERY `run:` step body
# verbatim, and executes them in order unde
#     bash --noprofile --norc -eo pipefail
# which is GitHub Actions' own default shell for Linux runner jobs. The workflow
# file is the single source of truth: a step added to ci.yml is executed here
# without this script changing at all.
#
# Platform adaptations (each printed as an ADAPTED/SKIP line, never silent):
#   * `uses:` steps are hosted actions and cannot execute locally. Their local
#     prerequisites are ASSERTED instead (checkout -> this is a git worktree with
#     a committed HEAD; setup-dotnet -> dotnet CLI major version 9 present) and
#     reported.
#   * a `pwsh` step whose interpreter is not installed locally is reported as
#     SKIP when the workflow declares it `continue-on-error: true` (future-proof;
#     the current ci.yml has no pwsh step); failures are otherwise surfaced.
#   * RUNNER_TEMP is exported as a temporary directory (its hosted meaning).
#   * GITHUB_REPOSITORY is exported from .repo-identity.json when the origin
#     matches it, so the identity guard runs with the same cross-check CI gives.
#
# Usage:
#   scripts/verify-ci-steps.sh [name-substring]   # only steps whose name contains it
#
# Exit codes:
#   0  every locally-executable step passed (hosted-only steps reported SKIP)
#   1  at least one executed step failed
#   2  setup/extraction failure (wrong repo, ci.yml missing or unparseable)
set -u

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null)" || true
if [ -z "$REPO_ROOT" ] || [ ! -d "$REPO_ROOT/src" ]; then
    echo "ERROR: run this from inside the simple-walk-game checkout" >&2
    exit 2
fi
cd "$REPO_ROOT" || exit 2

WORKFLOW=".github/workflows/ci.yml"
if [ ! -f "$WORKFLOW" ]; then
    echo "ERROR: $WORKFLOW not found" >&2
    exit 2
fi

FILTER="${1:-}"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
mkdir -p "$WORK/steps" "$WORK/out" "$WORK/runner"

# ---- environment that hosted CI provides ----
RUNNER_TEMP="$WORK/runner"
export RUNNER_TEMP
if [ -f .repo-identity.json ]; then
    DECLARED="$(sed -n 's/.*"repository"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' .repo-identity.json | head -1)"
    ORIGIN="$(git remote get-url origin 2>/dev/null | sed -e 's#.*[:/]\([^/]*/[^/]*\)\.git$#\1#' -e 's#.*[:/]\([^/]*/[^/]*\)$#\1#')"
    if [ -n "$DECLARED" ] && [ "$DECLARED" = "$ORIGIN" ]; then
        GITHUB_REPOSITORY="$DECLARED"
        export GITHUB_REPOSITORY
    fi
fi

# ---- extract every step from ci.yml ----
awk -v dir="$WORK/steps" '
BEGIN { idx = 0; inrun = 0 }
function emit() {
    if (idx == 0) return
    f = sprintf("%s/%02d.kind", dir, idx); print prevkind > f; close(f)
    f = sprintf("%s/%02d.coe",  dir, idx); print prevcoe  > f; close(f)
    f = sprintf("%s/%02d.uses", dir, idx); print prevuses > f; close(f)
    f = sprintf("%s/%02d.body", dir, idx); printf "%s", body > f; close(f)
}
/^      - name: / {
    emit()
    idx += 1; inrun = 0; body = ""
    prevkind = "unknown"; prevcoe = "false"; prevuses = ""
    name = $0; sub(/^      - name: /, "", name); gsub(/^"|"$/, "", name)
    f = sprintf("%s/%02d.name", dir, idx); print name > f; close(f)
    next
}
/^        uses: / {
    prevkind = "uses"; inrun = 0
    prevuses = $0; sub(/^        uses: /, "", prevuses); gsub(/^"|"$/, "", prevuses)
    next
}
/^        continue-on-error: / { if ($0 ~ /true/) prevcoe = "true"; inrun = 0; next }
/^        run: \|/             { prevkind = "run"; inrun = 1; next }
/^        run: / {
    prevkind = "run"; inrun = 0
    line = $0; sub(/^        run: /, "", line); body = body line "\n"
    next
}
/^          / {
    if (inrun == 1) { line = $0; sub(/^          /, "", line); body = body line "\n" }
    next
}
/^$/ { if (inrun == 1) { body = body "\n" }; next }
/^        [a-zA-Z-]+:/ { inrun = 0; next }
{ inrun = 0 }
END { emit() }
' "$WORKFLOW"

step_count="$(find "$WORK/steps" -name '*.name' | wc -l | tr -d ' ')"
if [ "$step_count" -eq 0 ]; then
    echo "ERROR: extracted 0 steps from $WORKFLOW; the extraction pattern is broken" >&2
    exit 2
fi

# Hollow-pass protection: an indentation change in ci.yml must fail LOUDLY here,
# never silently execute empty bodies and report them as passing.
exp_run="$(grep -c '^        run:' "$WORKFLOW")"
exp_uses="$(grep -c '^        uses:' "$WORKFLOW")"
got_run="$(cat "$WORK/steps"/*.kind | grep -c '^run$' || true)"
got_uses="$(cat "$WORK/steps"/*.kind | grep -c '^uses$' || true)"
if [ "$got_run" != "$exp_run" ] || [ "$got_uses" != "$exp_uses" ]; then
    echo "ERROR: extracted $got_run run-steps + $got_uses uses-steps, but $WORKFLOW declares $exp_run + $exp_uses." >&2
    echo "ERROR: the ci.yml shape changed in a way this parser did not follow; fix the parser, do not trust any earlier line." >&2
    exit 2
fi


# ---- local prerequisite assertions for the hosted-only checkout/setup steps ----
if ! git rev-parse HEAD >/dev/null 2>&1; then
    echo "ERROR: HEAD is not a commit; actions/checkout cannot be simulated" >&2
    exit 2
fi
if ! command -v dotnet >/dev/null 2>&1; then
    echo "ERROR: no dotnet CLI on PATH; actions/setup-dotnet cannot be simulated" >&2
    exit 2
fi
DOTNET_VERSION="$(dotnet --version 2>/dev/null || echo unknown)"
case "$DOTNET_VERSION" in
    9.*) : ;;
    *) echo "ERROR: dotnet $DOTNET_VERSION does not satisfy workflow dotnet-version 9.0.x" >&2; exit 2 ;;
esac

echo "reproducing $step_count ci.yml steps locally (dotnet $DOTNET_VERSION, RUNNER_TEMP=$RUNNER_TEMP)"
echo "shell: bash --noprofile --norc -eo pipefail  (GitHub Actions Linux default)"
echo ""

passed=0; failed=0; skipped=0; warned=0; n=0
for namefile in $(find "$WORK/steps" -name '*.name' | sort); do
    n=$((n + 1))
    stem="${namefile%.name}"
    idx="$(basename "$stem")"
    name="$(cat "$namefile")"
    kind="$(cat "$stem.kind")"
    coe="$(cat "$stem.coe")"
    uses="$(cat "$stem.uses")"
    label="[$n/$step_count] $name"

    if [ -n "$FILTER" ]; then
        case "$name" in
            *$FILTER*) : ;;
            *) continue ;;
        esac
    fi

    if [ "$kind" = "uses" ]; then
        case "$uses" in
            *checkout*)
                echo "$label ... SKIP (hosted checkout; asserted locally: git worktree at $(git rev-parse --short HEAD))" ;;
            *setup-dotnet*)
                echo "$label ... SKIP (hosted setup-dotnet; asserted locally: dotnet $DOTNET_VERSION)" ;;
            *upload-artifact*)
                echo "$label ... SKIP (artifact upload is hosted-only; results remain under $RUNNER_TEMP)" ;;
            *)
                echo "$label ... SKIP (hosted action cannot run locally)" ;;
        esac
        skipped=$((skipped + 1))
        continue
    fi

    body="$stem.body"
    if [ ! -s "$body" ]; then
        echo "$label ... FAILED (empty body extracted; parser drift — do not trust this run)"
        failed=$((failed + 1))
        continue
    fi
    first_cmd="$(sed -n '/^[[:space:]]*$/!{p;q}' "$body" | awk '{print $1}')"
    if [ "$first_cmd" = "pwsh" ] && ! command -v pwsh >/dev/null 2>&1; then
        if [ "$coe" = "true" ]; then
            echo "$label ... SKIP (pwsh not installed locally; step is continue-on-error in CI)"
            skipped=$((skipped + 1))
            continue
        fi
    fi

    printf '%s ... ' "$label"
    if bash --noprofile --norc -eo pipefail "$body" > "$WORK/out/$idx.log" 2>&1; then
        echo "ok"
        passed=$((passed + 1))
    else
        rc=$?
        if [ "$coe" = "true" ]; then
            echo "WARN (failed with $rc, but continue-on-error is set in CI)"
            tail -5 "$WORK/out/$idx.log" | sed 's/^/      | /'
            warned=$((warned + 1))
        else
            echo "FAILED (exit $rc)"
            tail -20 "$WORK/out/$idx.log" | sed 's/^/      | /'
            failed=$((failed + 1))
        fi
    fi
done

echo ""
echo "======================================"
echo "CI-STEP REPRODUCTION: $passed ok, $warned warned, $failed failed, $skipped skipped"
echo "======================================"
[ "$failed" -eq 0 ]
