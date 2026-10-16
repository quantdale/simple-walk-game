#!/usr/bin/env bash
# Package src/WalkGame.Unity/Build/iOS/xcode as <45 MB xz chunks on a throwaway ios-export/<tag>
# branch (HEAD + one payload commit) and push it; ios-xcode-export-verify.yml then runs on it.
# Never targets main. Usage: tools/unity/push-ios-export.sh <tag>
set -euo pipefail
tag="${1:?usage: push-ios-export.sh <tag>}"
root="$(git rev-parse --show-toplevel)"; sh "$root/scripts/assert-repo-identity.sh" "$root" >/dev/null
src="$root/src/WalkGame.Unity/Build/iOS/xcode"; test -d "$src"
base="$(git -C "$root" rev-parse HEAD)"; tmp="$(mktemp -d)"
git -C "$root" worktree add --detach "$tmp/wt" "$base" >/dev/null
mkdir "$tmp/wt/ios-export"
tar -C "$(dirname "$src")" -cf - xcode | xz -T0 -6 | split -b 45m - "$tmp/wt/ios-export/xcode.tar.xz.part-"
(cd "$tmp/wt/ios-export" && sha256sum xcode.tar.xz.part-* > SHA256SUMS)
printf 'source_sha=%s\nunity=6000.3.26f1 (Linux editor, iOS Build Support)\nexported=%s\n' "$base" "$(date -Is)" > "$tmp/wt/ios-export/MANIFEST.txt"
git -C "$tmp/wt" add ios-export
git -C "$tmp/wt" commit -q -m "ios-export payload for $base (throwaway branch, not for main)"
git -C "$tmp/wt" push origin "HEAD:refs/heads/ios-export/$tag"
git -C "$root" worktree remove "$tmp/wt"
