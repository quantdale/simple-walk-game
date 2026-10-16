#!/usr/bin/env bash
# D-117 guard (KI-015): src/WalkGame.Unity/ProjectSettings/ProjectSettings.asset is rewritten by
# Unity tooling (platform switch, WG_ANDROID_ARCH, iOS export) and must never be committed by
# accident. A deliberate change is allowed only when it names an existing decision:
#   --staged         : refuse a staged change unless SWG_PROJECT_SETTINGS_DECISION=D-NNN is set
#                      and docs/DECISIONS.md has that "## D-NNN" heading (pre-commit hook).
#   --range <A>..<B> : refuse if the range changes the file unless a commit in the range that
#                      touches it carries the trailer "Project-Settings-Decision: D-NNN" naming
#                      an existing decision (CI).
#   --self-test      : prove both modes can fail and can pass.
# Exit: 0 ok, 1 refused, 2 usage error.
set -u
FILE="src/WalkGame.Unity/ProjectSettings/ProjectSettings.asset"

decision_exists() { # D-NNN -> 0 if docs/DECISIONS.md (work tree) has the heading
    printf '%s' "$1" | grep -Eq '^D-[0-9]{3}$' && grep -Eq "^## $1([^0-9]|$)" docs/DECISIONS.md
}

staged() {
    git diff --cached --name-only | grep -Fxq "$FILE" || return 0
    local d="${SWG_PROJECT_SETTINGS_DECISION:-}"
    if [ -n "$d" ] && decision_exists "$d"; then
        echo "verify-project-settings: staged $FILE allowed by $d"; return 0; fi
    echo "verify-project-settings: refusing to commit $FILE (KI-015/D-117)." >&2
    echo "  Restore it: git restore --staged --worktree $FILE" >&2
    echo "  Deliberate change: record a D-entry, then SWG_PROJECT_SETTINGS_DECISION=D-NNN git commit ..." >&2
    return 1
}

range() {
    local r="$1" c d ok=1
    git diff --name-only "$r" -- "$FILE" | grep -q . || { echo "verify-project-settings: $FILE unchanged in $r"; return 0; }
    for c in $(git rev-list "$r" -- "$FILE"); do
        d="$(git log -1 --format='%(trailers:key=Project-Settings-Decision,valueonly)' "$c" | tr -d ' \n')"
        if [ -n "$d" ] && decision_exists "$d"; then echo "verify-project-settings: $c changes $FILE under $d"
        else echo "verify-project-settings: commit $c changes $FILE without a valid Project-Settings-Decision trailer (KI-015/D-117)" >&2; ok=0; fi
    done
    [ "$ok" -eq 1 ]
}

self_test() {
    local t; t="$(mktemp -d)"; trap 'rm -rf "$t"' RETURN
    local f=0 me; me="$(cd "$(dirname "$0")" && pwd)/$(basename "$0")"
    git -C "$t" init -q && git -C "$t" config user.email t@t && git -C "$t" config user.name t
    mkdir -p "$t/docs" "$t/$(dirname "$FILE")"
    printf '## D-001 — x\n' > "$t/docs/DECISIONS.md"; echo a > "$t/$FILE"
    git -C "$t" add . && git -C "$t" commit -qm base; local base; base="$(git -C "$t" rev-parse HEAD)"
    echo b > "$t/$FILE"; git -C "$t" add "$FILE"
    (cd "$t" && bash "$me" --staged >/dev/null 2>&1) && { echo "SELF-TEST FAIL: unannounced staged change passed"; f=1; }
    (cd "$t" && SWG_PROJECT_SETTINGS_DECISION=D-999 bash "$me" --staged >/dev/null 2>&1) && { echo "SELF-TEST FAIL: unknown decision passed"; f=1; }
    (cd "$t" && SWG_PROJECT_SETTINGS_DECISION=D-001 bash "$me" --staged >/dev/null 2>&1) || { echo "SELF-TEST FAIL: valid decision refused"; f=1; }
    git -C "$t" commit -qm "no trailer"
    (cd "$t" && bash "$me" --range "$base..HEAD" >/dev/null 2>&1) && { echo "SELF-TEST FAIL: range without trailer passed"; f=1; }
    git -C "$t" reset -q --soft "$base"; git -C "$t" commit -qm "settings" -m "Project-Settings-Decision: D-001"
    (cd "$t" && bash "$me" --range "$base..HEAD" >/dev/null 2>&1) || { echo "SELF-TEST FAIL: range with trailer refused"; f=1; }
    echo z > "$t/other.txt"; git -C "$t" add other.txt; git -C "$t" commit -qm other
    (cd "$t" && bash "$me" --range "HEAD~1..HEAD" >/dev/null 2>&1) || { echo "SELF-TEST FAIL: unrelated range refused"; f=1; }
    [ "$f" -eq 0 ] && echo "verify-project-settings self-test: PASS (6 cases)"
    return "$f"
}

root="$(git rev-parse --show-toplevel 2>/dev/null)" || { echo "not in a git work tree" >&2; exit 2; }
case "${1:-}" in
    --self-test) self_test; exit $? ;;
esac
cd "$root" || exit 2
case "${1:-}" in
    --staged) staged ;;
    --range) [ $# -eq 2 ] || { echo "usage: $0 --range A..B" >&2; exit 2; }; range "$2" ;;
    *) echo "usage: $0 --staged | --range A..B | --self-test" >&2; exit 2 ;;
esac
