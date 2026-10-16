#!/bin/sh
# Configure this clone so the tracked guards actually run:
#   git config core.hooksPath .githooks
# Idempotent; safe to re-run after every fresh checkout or worktree add.
# Usage: scripts/install-git-hooks.sh [--allow-no-origin]
#   --allow-no-origin  for throwaway fixture repos with NO remotes (KI-007): the identity check
#                      still enforces the declared slug and sentinels, and any origin that does
#                      exist is still verified. Never needed in a real clone.
set -eu
mode=""
for arg in "$@"; do
    case "$arg" in
        --allow-no-origin) mode="allow-no-origin" ;;
        *) echo "usage: $0 [--allow-no-origin]" >&2; exit 2 ;;
    esac
done
root="$(git rev-parse --show-toplevel)"
git -C "$root" config core.hooksPath .githooks
echo "hooks installed: core.hooksPath=$(git -C "$root" config core.hooksPath)"
SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
# shellcheck disable=SC1091
. "$SCRIPT_DIR/repo-identity-lib.sh"
assert_repo_identity "$root" "$mode"
