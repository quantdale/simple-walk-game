#!/bin/sh
# Configure this clone so the tracked guards actually run:
#   git config core.hooksPath .githooks
# Idempotent; safe to re-run after every fresh checkout or worktree add.
set -eu
root="$(git rev-parse --show-toplevel)"
git -C "$root" config core.hooksPath .githooks
echo "hooks installed: core.hooksPath=$(git -C "$root" config core.hooksPath)"
sh "$root/scripts/assert-repo-identity.sh" "$root"
