#!/bin/sh
# Repository identity guard — quantdale/simple-walk-game.
# Fails with exit code 86 when this checkout is anything other than
# quantdale/simple-walk-game. Safe to run from any nested directory.
# Usage: scripts/assert-repo-identity.sh [repo_root]
set -u
SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
# shellcheck disable=SC1091
. "$SCRIPT_DIR/repo-identity-lib.sh"
assert_repo_identity "${1:-}"
