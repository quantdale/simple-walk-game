#!/bin/sh
# Create an isolated writer environment for an autonomous agent session:
#   one writer = one worktree = one branch (+ its own writer lease).
#
# Usage: scripts/new-agent-worktree.sh <campaign> <session-id>
# Creates branch  agent/simple-walk-game/<campaign>-<session-id>
# at worktree     <repo-sibling>/simple-walk-game-wt-<campaign>-<session-id>
# and ACQUIRES that worktree's writer lease before printing next steps.
set -eu

[ $# -eq 2 ] || { echo "usage: new-agent-worktree.sh <campaign> <session-id>" >&2; exit 1; }
CAMPAIGN="$1"; SESSION="$2"

root="$(git rev-parse --show-toplevel)"
sh "$root/scripts/assert-repo-identity.sh" "$root" >/dev/null

BRANCH="agent/simple-walk-game/$CAMPAIGN-$SESSION"
WT="$root/../simple-walk-game-wt-$CAMPAIGN-$SESSION"

if git -C "$root" show-ref --verify --quiet "refs/heads/$BRANCH"; then
    echo "branch '$BRANCH' already exists; refusing to reuse campaign branch names." >&2
    exit 1
fi
if [ -e "$WT" ]; then
    echo "worktree path already exists: $WT" >&2
    exit 1
fi

git -C "$root" worktree add -b "$BRANCH" "$WT"
echo ""
echo "WORKTREE READY: $WT (branch $BRANCH)"
echo "Configuring guards inside the new worktree..."
git -C "$WT" config core.hooksPath .githooks
( cd "$WT" && SWG_SESSION_ID="wt-$CAMPAIGN-$SESSION" sh "$WT/scripts/writer-lease.sh" acquire ) || {
    echo "WARNING: lease acquisition failed inside the new worktree — resolve before writing." >&2
    exit 87
}
echo ""
echo "Session contract:"
echo "  * record your starting SHA: $(git -C "$WT" rev-parse HEAD)"
echo "  * do all writes ONLY inside $WT"
echo "  * on completion: commit, then release the lease (scripts/writer-lease.sh release)"
echo "  * before integrating to main: fetch origin and reconcile deliberately (see AGENTS.md)"
