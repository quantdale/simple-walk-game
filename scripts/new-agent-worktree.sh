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

# Self-update (D-150): a stale checkout runs ITS OWN copy of this script, which may predate
# the fetch-and-base fix (that is how a session was branched from stale e44b282 after #61).
# If the copy being executed differs from <remote>/main's copy, re-exec the fetched copy so
# the newest contract always applies regardless of which checkout invoked it.
if [ -z "${SWG_WORKTREE_REEXEC:-}" ] && [ -f "$0" ]; then
    SELF_BLOB="$(git hash-object "$0")"
    MAIN_BLOB="$(git -C "$root" rev-parse --verify --quiet "$BASE_SHA:scripts/new-agent-worktree.sh" || true)"
    if [ -n "$MAIN_BLOB" ] && [ "$SELF_BLOB" != "$MAIN_BLOB" ]; then
        TMP_SCRIPT="$(mktemp)"
        git -C "$root" cat-file blob "$MAIN_BLOB" > "$TMP_SCRIPT"
        echo "This copy of new-agent-worktree.sh differs from $BASE_REMOTE/main; re-running the fetched copy." >&2
        SWG_WORKTREE_REEXEC="$TMP_SCRIPT"; export SWG_WORKTREE_REEXEC
        exec sh "$TMP_SCRIPT" "$CAMPAIGN" "$SESSION"
    fi
fi
[ -n "${SWG_WORKTREE_REEXEC:-}" ] && [ -f "$SWG_WORKTREE_REEXEC" ] && rm -f "$SWG_WORKTREE_REEXEC"
echo "Base: freshly fetched $BASE_REMOTE/main at $BASE_SHA"
git -C "$root" worktree add --no-track -b "$BRANCH" "$WT" "$BASE_SHA"
# Self-check: the new worktree must sit exactly on the fetched base, never the invoking HEAD.
GOT_SHA="$(git -C "$WT" rev-parse HEAD)"
if [ "$GOT_SHA" != "$BASE_SHA" ]; then
    echo "SELF-CHECK FAILED: new worktree HEAD $GOT_SHA != fetched base $BASE_SHA" >&2
    exit 1
fi
echo "Self-check OK: worktree HEAD == $BASE_REMOTE/main ($BASE_SHA)"
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
