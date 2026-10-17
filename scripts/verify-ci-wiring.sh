#!/usr/bin/env bash
# CI wiring gate for quantdale/simple-walk-game (D-187).
#
# "A gate in a script CI never calls is not a gate" (D-049). This makes that rule
# mechanical:
#   * every scripts/verify-*.sh is invoked from a `run:` line of .github/workflows/ci.yml;
#   * every self-test flag a gate declares (`--self-test`, `--selftest-<name>`) is also
#     invoked there, so each gate proves in CI that it can fail on a seeded violation.
# scripts/verify-ci-steps.sh is exempt: it is the local runner that replays ci.yml.
#
# Usage:
#   scripts/verify-ci-wiring.sh                                  # check this repository
#   scripts/verify-ci-wiring.sh --ci FILE --scripts DIR          # check another layout
#   scripts/verify-ci-wiring.sh --self-test                      # seeded violations must fail
#
# Exit codes:
#   0  every gate and every declared self-test is wired into CI
#   1  at least one gate or self-test is not invoked by CI
#   2  bad usage or files missing
set -eu

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null)" || true
if [ -z "$REPO_ROOT" ]; then echo "ERROR: run this from inside the checkout" >&2; exit 2; fi
cd "$REPO_ROOT"

CHECKER='
import os, re, sys
ci_path, scripts_dir = sys.argv[1], sys.argv[2]
EXEMPT = {"verify-ci-steps.sh"}
try:
    ci_lines = open(ci_path, encoding="utf-8").read().splitlines()
except OSError as e:
    print(f"ERROR: cannot read {ci_path}: {e}", file=sys.stderr); sys.exit(2)
# Only live YAML lines count: a commented-out step is not a gate.
live = [l.split(" #", 1)[0] for l in ci_lines if not l.lstrip().startswith("#")]
invocations = []
for l in live:
    for m in re.finditer(r"scripts/(verify-[A-Za-z0-9_.-]+\.sh)((?:[ \t]+[^ \t;&|]+)*)", l):
        invocations.append((m.group(1), m.group(2).split()))
# A declared flag is an `= "--self-test"` comparison or a `--self-test)` case label.
flag_re = re.compile(r"(?:=[ \t]*\"|^[ \t]*)(--self-?test(?:-[A-Za-z0-9]+)?)(?:\"|\))", re.M)
gates = sorted(f for f in os.listdir(scripts_dir) if re.fullmatch(r"verify-.+\.sh", f) and f not in EXEMPT)
if not gates:
    print(f"ERROR: no verify-*.sh gates found in {scripts_dir}", file=sys.stderr); sys.exit(2)
fail = 0
for g in gates:
    src = open(os.path.join(scripts_dir, g), encoding="utf-8").read()
    calls = [args for name, args in invocations if name == g]
    if not calls:
        print(f"  FAIL: {g} is never invoked by {ci_path}", file=sys.stderr); fail += 1; continue
    flags = sorted(set(flag_re.findall(src)))
    missing = [f for f in flags if not any(f in args for args in calls)]
    if missing:
        print(f"  FAIL: {g} declares {missing} but CI never runs it that way", file=sys.stderr); fail += 1; continue
    if not any(not args or not any(a.startswith("--self") for a in args) for args in calls):
        print(f"  FAIL: {g} only runs as a self-test in CI; the real gate never runs", file=sys.stderr); fail += 1; continue
    extra = " (+ " + ", ".join(flags) + ")" if flags else ""
    print(f"  PASS: {g} runs in CI{extra}")
print("")
print(f"CI WIRING: {len(gates) - fail} wired, {fail} not wired")
sys.exit(1 if fail else 0)
'

check() { python3 -c "$CHECKER" "$1" "$2"; }

if [ "${1:-}" = "--self-test" ]; then
    t="$(mktemp -d)"; trap 'rm -rf "$t"' EXIT
    mkdir -p "$t/s"
    printf '#!/bin/sh\nif [ "${1:-}" = "--self-test" ]; then exit 0; fi\n' > "$t/s/verify-a.sh"
    printf '#!/bin/sh\ncase "$1" in\n    --self-test) exit 0 ;;\nesac\n' > "$t/s/verify-b.sh"
    printf '#!/bin/sh\nif [ "$1" = "%s" ]; then exit 0; fi\n' "--selftest-rule7" > "$t/s/verify-c.sh"
    printf '#!/bin/sh\nexit 0\n' > "$t/s/verify-ci-steps.sh"
    good='      - run: |
          scripts/verify-a.sh --self-test
          scripts/verify-a.sh
      - run: scripts/verify-b.sh 55
      - run: scripts/verify-b.sh --self-test
      - run: scripts/verify-c.sh && scripts/verify-c.sh --selftest-rule7'
    printf '%s\n' "$good" > "$t/good.yml"
    printf '%s\n' "$good" | grep -v 'verify-b' > "$t/no-b.yml"
    printf '%s\n' "$good" | grep -v 'verify-b.sh --self-test' > "$t/no-case-selftest.yml"
    printf '%s\n' "$good" | grep -v 'verify-a.sh --self-test' > "$t/no-selftest.yml"
    printf '%s\n' "$good" | sed 's/scripts\/verify-c.sh && //' > "$t/selftest-only.yml"
    printf '%s\n' "$good" | sed 's/- run: scripts\/verify-b.sh 55/# - run: scripts\/verify-b.sh 55/' > "$t/commented.yml"
    rc=0
    expect() { # want label yml
        local got=0
        check "$t/$3" "$t/s" >/dev/null 2>&1 || got=$?
        if [ "$got" = "$1" ]; then echo "  PASS: $2"; else echo "  FAIL: $2 (got $got, want $1)" >&2; rc=1; fi
    }
    expect 0 "fully wired fixture passes (verify-ci-steps.sh exempt)" good.yml
    expect 1 "a gate CI never calls fails" no-b.yml
    expect 1 "a declared --self-test CI never runs fails" no-selftest.yml
    expect 1 "a case-label --self-test CI never runs fails" no-case-selftest.yml
    expect 1 "a gate that only runs as a self-test fails" selftest-only.yml
    expect 1 "a commented-out step does not count" commented.yml
    [ "$rc" -eq 0 ] && echo "CI WIRING SELF-TEST: PASS" || echo "CI WIRING SELF-TEST: FAIL" >&2
    exit "$rc"
fi

CI=".github/workflows/ci.yml"; DIR="scripts"
while [ $# -gt 0 ]; do
    case "$1" in
        --ci) CI="$2"; shift 2 ;;
        --scripts) DIR="$2"; shift 2 ;;
        *) echo "usage: $0 [--self-test | --ci FILE --scripts DIR]" >&2; exit 2 ;;
    esac
done
check "$CI" "$DIR"
