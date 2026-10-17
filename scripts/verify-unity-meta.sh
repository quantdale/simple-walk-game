#!/usr/bin/env bash
# D-184 guard: Unity .meta integrity for everything tracked under src/WalkGame.Unity/Assets.
#
# Unity identifies every asset by the GUID in its .meta file. A committed asset WITHOUT a
# committed .meta gets a fresh random GUID on every clone, so scene/prefab/asmdef references
# to it break differently on each machine; an orphan .meta (asset removed) or a copy-pasted
# .meta (duplicate GUID) makes Unity silently re-assign GUIDs on import. None of this is
# visible to `dotnet test`, so it is checked here, without Unity.
#
# Rules (paths are git-tracked files at HEAD / in the index):
#   1. every tracked asset file, and every folder that contains one, has a tracked .meta;
#   2. every tracked .meta belongs to a tracked file or folder, or to a git-ignored generated
#      asset (the synced plugin DLLs);
#   3. every tracked .meta declares exactly one 32-hex-digit guid, and guids are unique.
# Unity does not import (and so gives no .meta to): names starting with '.', names ending
# in '~', and the CONTENTS of *.androidlib / *.bundle / *.plugin / *.framework folders (the
# folder itself is one asset and still needs its .meta).
#
# Usage: scripts/verify-unity-meta.sh [--self-test]
# Exit: 0 clean, 1 violation found, 2 usage/setup error.
set -u
ASSETS_PREFIX="src/WalkGame.Unity/Assets"

die() { echo "verify-unity-meta: $*" >&2; exit 2; }

# The checker reads the NUL-separated path list on stdin, so its program is passed with -c
# (a heredoc program would itself occupy stdin and silently check nothing).
read -r -d '' CHECKER <<'PY'
import sys, os, re, subprocess, collections
root, prefix = sys.argv[1], sys.argv[2].rstrip('/')
paths = [p for p in sys.stdin.buffer.read().decode('utf-8').split('\0') if p]
OPAQUE = ('.androidlib', '.bundle', '.plugin', '.framework')

def rel_parts(p):
    return p[len(prefix) + 1:].split('/')

def skipped(p):
    """True when Unity would not import p as its own asset."""
    parts = rel_parts(p)
    for i, part in enumerate(parts):
        if part.startswith('.') or part.endswith('~'):
            return True
        if i < len(parts) - 1 and part.endswith(OPAQUE):
            return True  # inside an opaque plugin folder
    return False

tracked = set(paths)
metas = [p for p in paths if p.endswith('.meta')]
assets = [p for p in paths if not p.endswith('.meta') and not skipped(p)]
folders = set()
for p in paths:  # every folder Unity imports that holds anything tracked (incl. an opaque plugin folder)
    d = os.path.dirname(p[:-5] if p.endswith('.meta') else p)
    while d.startswith(prefix + '/'):
        if not skipped(d):
            folders.add(d)
        d = os.path.dirname(d)

out = []
for a in sorted(assets):
    if a + '.meta' not in tracked:
        out.append('MISSING META: ' + a)
for d in sorted(folders):
    if d + '.meta' not in tracked:
        out.append('MISSING FOLDER META: ' + d)

orphans = [m for m in metas if m[:-5] not in tracked and m[:-5] not in folders]
if orphans:
    ignored = set(subprocess.run(['git', '-C', root, 'check-ignore', '--no-index', '--stdin'],
                  input='\n'.join(m[:-5] for m in orphans), capture_output=True, text=True).stdout.split('\n'))
    for m in sorted(orphans):
        if m[:-5] not in ignored:
            out.append('ORPHAN META: ' + m)

guids = collections.defaultdict(list)
for m in metas:
    try:
        text = open(os.path.join(root, m), encoding='utf-8', errors='replace').read()
    except OSError as e:
        out.append('UNREADABLE META: %s (%s)' % (m, e.strerror)); continue
    found = re.findall(r'^guid:\s*(\S*)\s*$', text, re.M)
    if len(found) != 1 or not re.fullmatch(r'[0-9a-f]{32}', found[0]):
        out.append('BAD GUID: %s (%s)' % (m, ', '.join(found) or 'none')); continue
    guids[found[0]].append(m)
for g, ms in sorted(guids.items()):
    if len(ms) > 1:
        out.append('DUPLICATE GUID %s: %s' % (g, ', '.join(sorted(ms))))
print('\n'.join(out))
PY

check() { # $1 = repo root; prints violations
    ( cd "$1" && git ls-files -z -- "$ASSETS_PREFIX" ) | python3 -c "$CHECKER" "$1" "$ASSETS_PREFIX"
}

self_test() {
    local t fails=0 out
    t="$(mktemp -d)" || die "mktemp failed"
    trap 'rm -rf "$t"' RETURN
    git -C "$t" init -q || die "git init failed"
    local A="$t/$ASSETS_PREFIX"
    meta() { printf 'fileFormatVersion: 2\nguid: %s\n' "$2" > "$1.meta"; }
    mkdir -p "$A/Shell" "$A/Plugins/Android/X.androidlib/src" "$A/Plugins/Gen"
    echo 'class A {}' > "$A/Shell/A.cs"; meta "$A/Shell/A.cs" 0123456789abcdef0123456789abcdef
    meta "$A/Shell" 00000000000000000000000000000001
    meta "$A/Plugins" 00000000000000000000000000000002
    meta "$A/Plugins/Android" 00000000000000000000000000000003
    echo x > "$A/Plugins/Android/X.androidlib/src/B.java"; meta "$A/Plugins/Android/X.androidlib" 00000000000000000000000000000004
    meta "$A/Plugins/Gen" 00000000000000000000000000000006
    meta "$A/Plugins/Gen/Gen.dll" 00000000000000000000000000000005; echo '/src/WalkGame.Unity/Assets/Plugins/Gen/Gen.dll' > "$t/.gitignore"
    echo x > "$A/.hidden"
    git -C "$t" add -A >/dev/null
    expect() { # $1 label, $2 expected substring ('' = clean)
        out="$(check "$t")"
        if [ -z "$2" ]; then [ -z "$out" ] || { echo "SELF-TEST FAIL ($1): expected clean, got: $out"; fails=$((fails+1)); }
        else printf '%s' "$out" | grep -qF "$2" || { echo "SELF-TEST FAIL ($1): expected '$2', got: $out"; fails=$((fails+1)); }; fi
    }
    expect "clean baseline (androidlib contents, ignored DLL meta, dotfile)" ''
    echo 'class C {}' > "$A/Shell/C.cs"; git -C "$t" add -A >/dev/null
    expect "asset without meta" "MISSING META: $ASSETS_PREFIX/Shell/C.cs"
    git -C "$t" rm -q --cached "$A/Shell/C.cs"; rm "$A/Shell/C.cs"
    mkdir -p "$A/New"; echo x > "$A/New/D.txt"; meta "$A/New/D.txt" 00000000000000000000000000000007; git -C "$t" add -A >/dev/null
    expect "folder without meta" "MISSING FOLDER META: $ASSETS_PREFIX/New"
    git -C "$t" rm -rq --cached "$A/New"; rm -rf "$A/New"
    meta "$A/Shell/Gone.cs" 00000000000000000000000000000008; git -C "$t" add -A >/dev/null
    expect "orphan meta" "ORPHAN META: $ASSETS_PREFIX/Shell/Gone.cs.meta"
    git -C "$t" rm -q --cached "$A/Shell/Gone.cs.meta"; rm "$A/Shell/Gone.cs.meta"
    echo 'class E {}' > "$A/Shell/E.cs"; meta "$A/Shell/E.cs" 0123456789abcdef0123456789abcdef; git -C "$t" add -A >/dev/null
    expect "duplicate guid" "DUPLICATE GUID 0123456789abcdef0123456789abcdef"
    meta "$A/Shell/E.cs" NOT-A-GUID; git -C "$t" add -A >/dev/null
    expect "malformed guid" "BAD GUID: $ASSETS_PREFIX/Shell/E.cs.meta"
    [ "$fails" -eq 0 ] && echo "verify-unity-meta self-test: PASS (6 cases)"
    return "$fails"
}

command -v python3 >/dev/null 2>&1 || die "python3 is required"
root="$(git rev-parse --show-toplevel 2>/dev/null)" || die "not inside a git work tree"
case "${1:-}" in
    "") ;;
    --self-test) self_test; exit $? ;;
    *) die "usage: $0 [--self-test]" ;;
esac
[ -d "$root/$ASSETS_PREFIX" ] || die "$ASSETS_PREFIX not found"
out="$(check "$root")"
if [ -n "$out" ]; then
    printf '%s\n' "$out"
    echo "UNITY META CHECK: FAIL ($(printf '%s\n' "$out" | wc -l) violation(s))"
    exit 1
fi
echo "UNITY META CHECK: PASS ($(cd "$root" && git ls-files -- "$ASSETS_PREFIX" | grep -c '\.meta$') metas)"
