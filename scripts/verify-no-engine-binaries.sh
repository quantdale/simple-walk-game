#!/usr/bin/env bash
# D-116 guard: Unity engine binaries (libiPhone-lib.a, UnityFramework, libil2cpp, any exported
# player/Xcode tree, editor Library/ caches, chunked export payloads) must never be committed to
# this PUBLIC repository. Redistributing them is a licence risk.
#
# Usage:
#   scripts/verify-no-engine-binaries.sh            # every tracked file at HEAD (name + content)
#   scripts/verify-no-engine-binaries.sh --staged   # files staged for commit (pre-commit hook)
#   scripts/verify-no-engine-binaries.sh --history  # every path ever committed on HEAD's history,
#                                                   # engine-payload rules only (see below)
#   scripts/verify-no-engine-binaries.sh --self-test
# Exit: 0 clean, 1 violation found, 2 usage/setup error.
set -u

# Path rules (extended regex, matched against the repo-relative path).
ENGINE_PATHS='(^|/)libiPhone-lib\.a$|(^|/)UnityFramework(\.framework)?(/|$)|(^|/)libil2cpp[^/]*$|(^|/)ios-export/|\.tar\.xz\.part-|(^|/)Build/iOS/|(^|/)Unity-iPhone(\.xcodeproj)?(/|$)|(^|/)Il2CppOutputProject/'
NATIVE_EXT='\.(a|dylib|so|o|dll|pdb|framework|xcframework|bundle|apk|aab|ipa)$'
EDITOR_CACHE='^(src/WalkGame\.Unity/)?(Library|Temp|Logs|obj)/'
# Allow-list for native-extension files that are legitimately ours (none today).
ALLOW='^$'

die() { echo "verify-no-engine-binaries: $*" >&2; exit 2; }

check_names() { # reads paths on stdin, mode in $1 (full|history); prints violations
    local mode="$1" p
    while IFS= read -r p; do
        [ -n "$p" ] || continue
        if printf '%s\n' "$p" | grep -Eq "$ENGINE_PATHS"; then echo "ENGINE PAYLOAD: $p"; continue; fi
        [ "$mode" = history ] && continue
        if printf '%s\n' "$p" | grep -Eq "$EDITOR_CACHE"; then echo "EDITOR CACHE: $p"; continue; fi
        if printf '%s\n' "$p" | grep -Eiq "$NATIVE_EXT" && ! printf '%s\n' "$p" | grep -Eq "$ALLOW"; then
            echo "NATIVE BINARY: $p"; fi
    done
}

check_content() { # paths on stdin, files read from the work tree (or index blob with $1=staged)
    local src="$1" p magic
    while IFS= read -r p; do
        [ -n "$p" ] || continue
        if [ "$src" = staged ]; then
            magic="$(git cat-file blob ":$p" 2>/dev/null | head -c 8 | od -An -tx1 | tr -d ' \n')"
        else
            [ -f "$p" ] || continue
            magic="$(head -c 8 "$p" | od -An -tx1 | tr -d ' \n')"
        fi
        case "$magic" in
            213c617263683e0a*) echo "AR ARCHIVE (static library): $p" ;;   # "!<arch>\n"
            cffaedfe*|cefaedfe*|feedfacf*|feedface*) echo "MACH-O BINARY: $p" ;;
            7f454c46*) echo "ELF BINARY: $p" ;;
            fd377a585a00*) echo "XZ ARCHIVE: $p" ;;
        esac
    done
}

run() { # mode -> prints violations
    case "$1" in
        full)    local l; l="$(git ls-files)"
                 printf '%s\n' "$l" | check_names full; printf '%s\n' "$l" | check_content tree ;;
        staged)  local l; l="$(git diff --cached --name-only --diff-filter=ACMR)"
                 printf '%s\n' "$l" | check_names full; printf '%s\n' "$l" | check_content staged ;;
        history) git log --format= --name-only HEAD | sort -u | check_names history ;;
    esac
}

self_test() {
    local d; d="$(mktemp -d)"; trap 'rm -rf "$d"' RETURN
    git -C "$d" init -q && git -C "$d" config user.email t@t && git -C "$d" config user.name t
    echo ok > "$d/README.md"; git -C "$d" add . && git -C "$d" commit -qm base
    local fails=0
    (cd "$d" && [ -z "$(run full)" ]) || { echo "SELF-TEST FAIL: clean repo flagged"; fails=1; }
    for f in "Libraries/libiPhone-lib.a" "ios-export/xcode.tar.xz.part-aa" "Library/ScriptAssemblies/X.dll" "plugin/foo.so"; do
        mkdir -p "$d/$(dirname "$f")"; echo x > "$d/$f"
        git -C "$d" add -f "$f"
        (cd "$d" && [ -n "$(run staged)" ]) || { echo "SELF-TEST FAIL: staged $f not flagged"; fails=1; }
        git -C "$d" rm -q --cached "$f"; rm -f "$d/$f"
    done
    printf '!<arch>\nxx' > "$d/innocent.txt"; git -C "$d" add innocent.txt
    (cd "$d" && [ -n "$(run full)" ]) || { echo "SELF-TEST FAIL: ar magic not flagged"; fails=1; }
    git -C "$d" rm -qf innocent.txt
    mkdir -p "$d/ios-export"; echo x > "$d/ios-export/MANIFEST.txt"; git -C "$d" add . && git -C "$d" commit -qm payload
    git -C "$d" rm -rq ios-export && git -C "$d" commit -qm removed
    (cd "$d" && [ -n "$(run history)" ]) || { echo "SELF-TEST FAIL: history payload not flagged"; fails=1; }
    [ "$fails" -eq 0 ] && echo "verify-no-engine-binaries self-test: PASS (7 cases)"
    return "$fails"
}

root="$(git rev-parse --show-toplevel 2>/dev/null)" || die "not inside a git work tree"
cd "$root" || die "cannot cd to $root"
case "${1:-}" in
    "") mode=full ;; --staged) mode=staged ;; --history) mode=history ;;
    --self-test) self_test; exit $? ;;
    *) die "usage: $0 [--staged|--history|--self-test]" ;;
esac
out="$(run "$mode" | sort -u)"
if [ -n "$out" ]; then
    printf '%s\n' "$out" >&2
    echo "FAIL ($mode): Unity engine binaries / build payloads must never be committed to this public repo (D-116)." >&2
    exit 1
fi
echo "verify-no-engine-binaries ($mode): PASS — no Unity engine binaries or export payloads."
