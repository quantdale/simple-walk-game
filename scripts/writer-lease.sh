#!/bin/sh
# Writer lease for quantdale/simple-walk-game — single-writer protection inside ONE
# work tree. The lease lives under .git/ so it is never committed and is naturally
# per-worktree. See AGENTS.md ("Single-writer lease") for the contract.
#
# Usage:
#   scripts/writer-lease.sh acquire  [--force]   # --force ALSO requires env
#                                                # SWG_ACKNOWLEDGE_LOCK_OVERRIDE=yes
#   scripts/writer-lease.sh release  [--force]   # --force releases a foreign lease
#   scripts/writer-lease.sh status
#
# Exit codes: 0 ok | 86 identity failure | 87 lease busy / refused | 1 usage|unexpected.
set -u

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
# shellcheck disable=SC1091
. "$SCRIPT_DIR/repo-identity-lib.sh"

LEASE_EXIT_BUSY=87
LOCK_RELATIVE_PATH=".git/simple-walk-game.writer-lock.json"
OVERRIDE_ENV="SWG_ACKNOWLEDGE_LOCK_OVERRIDE=yes"

usage() {
    echo "usage: writer-lease.sh {acquire [--force] | release [--force] | status}" >&2
    exit 1
}

json_escape() {
    printf '%s' "$1" | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g'
}

utc_now_iso() {
    date -u +"%Y-%m-%dT%H:%M:%SZ" 2>/dev/null || date -u +%Y-%m-%dT%H:%M:%SZ
}

lease_age_minutes() {
    _la_acquired="$1"
    _la_now="$(date -u +%s)"
    _la_then="$(date -u -d "$_la_acquired" +%s 2>/dev/null || date -ju -f "%Y-%m-%dT%H:%M:%SZ" "$_la_acquired" +%s 2>/dev/null || echo "$_la_now")"
    echo $(( (_la_now - _la_then) / 60 ))
}

pid_appears_alive() {
    _pa_pid="$1"
    [ -n "$_pa_pid" ] && kill -0 "$_pa_pid" 2>/dev/null
}

print_lock_diagnostics() {
    _pld_lock="$1"
    _pld_repo="$(identity_field "$_pld_lock" repository)"
    _pld_session="$(identity_field "$_pld_lock" sessionId)"
    _pld_host="$(identity_field "$_pld_lock" hostname)"
    _pld_pid="$(identity_field "$_pld_lock" pid)"
    _pld_branch="$(identity_field "$_pld_lock" branch)"
    _pld_sha="$(identity_field "$_pld_lock" startSha)"
    _pld_at="$(identity_field "$_pld_lock" acquiredAtUtc)"
    echo "    holder repository : $_pld_repo"
    echo "    holder session    : $_pld_session"
    echo "    holder host/pid   : $_pld_host / ${_pld_pid:-unknown}"
    echo "    holder branch     : $_pld_branch"
    echo "    holder start SHA  : $_pld_sha"
    echo "    acquired at       : $_pld_at ($(lease_age_minutes "$_pld_at") min ago)"
    if [ "$(identity_field "$_pld_lock" hostname)" = "$(hostname 2>/dev/null || echo '?')" ] \
        && ! pid_appears_alive "${_pld_pid:-}"; then
        echo "    NOTE: holder PID does not appear alive on this host — STALE CANDIDATE."
        echo "          Stale locks are NEVER removed automatically; a human decides"
        echo "          after verifying the session is truly gone, then runs:"
        echo "            scripts/writer-lease.sh release --force"
        echo "          ($OVERRIDE_ENV scripts/writer-lease.sh acquire --force also works)"
    fi
}

require_identity_or_die() {
    _rid_root="$(git rev-parse --show-toplevel 2>/dev/null)" ||
        fail_identity "not inside a Git work tree."
    assert_repo_identity "$_rid_root" >/dev/null   # exits 86 on mismatch
    # Per-worktree resolution: in linked worktrees '.git' is a pointer file, so the
    # lease must land in that worktree's own gitdir (.git/worktrees/<name>/).
    LOCK_PATH="$(git rev-parse --path-format=absolute --git-path simple-walk-game.writer-lock.json 2>/dev/null)" \
        || LOCK_PATH="$_rid_root/.git/simple-walk-game.writer-lock.json"
}

cmd_status() {
    require_identity_or_die
    if [ -f "$LOCK_PATH" ]; then
        echo "WRITER LEASE: BUSY"
        print_lock_diagnostics "$LOCK_PATH"
        exit "$LEASE_EXIT_BUSY"
    fi
    echo "WRITER LEASE: FREE"
    exit 0
}

cmd_acquire() {
    _ca_force=0
    for _ca_arg in "$@"; do
        case "$_ca_arg" in
            --force) _ca_force=1 ;;
            *) usage ;;
        esac
    done

    require_identity_or_die
    _ca_repo="$(identity_field "$(git rev-parse --show-toplevel)/.repo-identity.json" repository)"
    _ca_root="$(git rev-parse --show-toplevel)"

    if [ -f "$LOCK_PATH" ]; then
        echo "WRITER LEASE: another writer holds the lease for this work tree." >&2
        print_lock_diagnostics "$LOCK_PATH" >&2
        if [ "$_ca_force" -eq 1 ]; then
            if [ "${SWG_ACKNOWLEDGE_LOCK_OVERRIDE:-}" != "yes" ]; then
                echo "" >&2
                echo "Override refused — '--force' additionally requires env $OVERRIDE_ENV." >&2
                exit "$LEASE_EXIT_BUSY"
            fi
            echo "" >&2
            echo "WARNING: operator-acknowledged override; replacing the existing lease." >&2
            rm -f "$LOCK_PATH"
        else
            echo "" >&2
            echo "A second autonomous writer MUST STOP here. Options:" >&2
            echo "  * work in your own worktree instead:  scripts/new-agent-worktree.sh <campaign> <session-id>" >&2
            echo "  * if you are the human operator and the holder is confirmed dead:" >&2
            echo "        $OVERRIDE_ENV scripts/writer-lease.sh acquire --force" >&2
            echo "    (never steal silently; never 'because it was inconvenient')" >&2
            exit "$LEASE_EXIT_BUSY"
        fi
    fi

    _ca_session="${SWG_SESSION_ID:-pid-$$-$(date +%s)}"
    _ca_branch="$(git rev-parse --abbrev-ref HEAD)"
    _ca_sha="$(git rev-parse HEAD)"
    _ca_host="$(hostname 2>/dev/null || echo unknown)"
    _ca_now="$(utc_now_iso)"

    # Single atomic create-or-fail write (noclobber): no TOCTOU window between
    # checking and taking the lease.
    _ca_payload="{\"schemaVersion\":1,\"repository\":\"$(json_escape "$_ca_repo")\",\"sessionId\":\"$(json_escape "$_ca_session")\",\"hostname\":\"$(json_escape "$_ca_host")\",\"pid\":\"$$\",\"branch\":\"$(json_escape "$_ca_branch")\",\"startSha\":\"$_ca_sha\",\"acquiredAtUtc\":\"$_ca_now\"}"
    if ! (set -o noclobber; printf '%s\n' "$_ca_payload" > "$LOCK_PATH") 2>/dev/null; then
        echo "WRITER LEASE: REFUSED — lost atomic creation race; another writer holds the lease." >&2
        print_lock_diagnostics "$LOCK_PATH" >&2
        exit "$LEASE_EXIT_BUSY"
    fi

    echo "WRITER LEASE: ACQUIRED ($_ca_session)"
    echo "    repository : $_ca_repo"
    echo "    branch     : $_ca_branch"
    echo "    start SHA  : $_ca_sha"
    echo "Release it on normal completion: scripts/writer-lease.sh release"
    exit 0
}

cmd_release() {
    _cr_force=0
    for _cr_arg in "$@"; do
        case "$_cr_arg" in
            --force) _cr_force=1 ;;
            *) usage ;;
        esac
    done

    require_identity_or_die
    if [ ! -f "$LOCK_PATH" ]; then
        echo "WRITER LEASE: FREE (nothing to release)"
        exit 0
    fi

    _cr_lock_session="$(identity_field "$LOCK_PATH" sessionId)"
    _cr_lock_pid="$(identity_field "$LOCK_PATH" pid)"
    _cr_mine=0
    { [ -n "${SWG_SESSION_ID:-}" ] && [ "$_cr_lock_session" = "$SWG_SESSION_ID" ]; } && _cr_mine=1
    [ "${_cr_lock_pid:-}" = "$$" ] && _cr_mine=1

    if [ "$_cr_mine" -eq 1 ]; then
        rm -f "$LOCK_PATH"
        echo "WRITER LEASE: RELEASED ($_cr_lock_session)"
        exit 0
    fi

    if [ "$_cr_force" -eq 1 ]; then
        echo "WARNING: releasing a lease owned by another session ($_cr_lock_session)." >&2
        print_lock_diagnostics "$LOCK_PATH" >&2
        rm -f "$LOCK_PATH"
        echo "WRITER LEASE: FORCE-RELEASED"
        exit 0
    fi

    echo "WRITER LEASE: REFUSED — lease belongs to another session ($_cr_lock_session)." >&2
    print_lock_diagnostics "$LOCK_PATH" >&2
    echo "If you are the human operator and that session is confirmed dead:" >&2
    echo "    scripts/writer-lease.sh release --force" >&2
    exit "$LEASE_EXIT_BUSY"
}

[ $# -ge 1 ] || usage
CMD="$1"; shift
case "$CMD" in
    acquire) cmd_acquire "$@" ;;
    release) cmd_release "$@" ;;
    status)  [ $# -eq 0 ] || usage; cmd_status ;;
    *) usage ;;
esac
