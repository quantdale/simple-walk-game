#!/bin/sh
# Create an isolated writer environment for an autonomous agent session:
#   one writer = one worktree = one branch (+ its own writer lease).
#
# Usage: scripts/new-agent-worktree.sh <campaign> <session-id>
# Creates branch  agent/simple-walk-game/<campaign>-<session-id>
# at worktree     <repo-sibling>/simple-walk-game-wt-<campaign>-<session-id>
# and ACQUIRES that worktree's writer lease before printing next steps.
# Lease session id: an exported SWG_SESSION_ID is honoured, so the calling session can
# later release/resume its own lease (KI-008); otherwise it defaults to wt-<campaign>-<session-id>.
# Base: the branch is ALWAYS cut from a freshly fetched <remote>/main (never the local, possibly
# stale, checkout HEAD). A failed fetch refuses rather than silently using a stale base.
# SWG_WORKTREE_BASE_REMOTE (default: origin) exists only so the guard suite can fetch from a
# throwaway fixture remote; real sessions must leave it unset.
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

BASE_REMOTE="${SWG_WORKTREE_BASE_REMOTE:-origin}"
if ! git -C "$root" fetch --quiet "$BASE_REMOTE" "+refs/heads/main:refs/remotes/$BASE_REMOTE/main"; then
    echo "fetch of $BASE_REMOTE/main failed; refusing to branch from a possibly stale base." >&2
    exit 1
fi
BASE_SHA="$(git -C "$root" rev-parse --verify "refs/remotes/$BASE_REMOTE/main^{commit}")"
echo "Base: freshly fetched $BASE_REMOTE/main at $BASE_SHA"
git -C "$root" worktree add --no-track -b "$BRANCH" "$WT" "$BASE_SHA"
echo ""
echo "WORKTREE READY: $WT (branch $BRANCH)"
echo "Configuring guards inside the new worktree..."
git -C "$WT" config core.hooksPath .githooks
LEASE_ID="${SWG_SESSION_ID:-wt-$CAMPAIGN-$SESSION}"
( cd "$WT" && SWG_SESSION_ID="$LEASE_ID" sh "$WT/scripts/writer-lease.sh" acquire ) || {
    echo "WARNING: lease acquisition failed inside the new worktree — resolve before writing." >&2
    exit 87
}
echo ""
echo "Session contract:"
echo "  * record your starting SHA: $(git -C "$WT" rev-parse HEAD)"
echo "  * do all writes ONLY inside $WT"
echo "  * lease session id: $LEASE_ID (export SWG_SESSION_ID=$LEASE_ID to release/resume it)"
echo "  * on completion: commit, then release the lease (SWG_SESSION_ID=$LEASE_ID scripts/writer-lease.sh release)"
echo "  * before integrating to main: fetch origin and reconcile deliberately (see AGENTS.md)"
