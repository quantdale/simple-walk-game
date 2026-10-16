#!/usr/bin/env bash
# Guard proof suite for quantdale/simple-walk-game.
#
# Proves, against THROWAWAY local repositories only (the real remote is never
# contacted or mutated):
#   1. correct repo passes the identity guard
#   2. identity file declaring quantdale/walk-game fails
#   3. wrong 'origin' slug fails
#   4. wrong GITHUB_REPOSITORY value fails
#   5. second writer lease acquisition fails
#   6. released lease permits a new writer
#   7. stale lease is NOT silently stolen (explicit operator override required)
#   8. pre-push detects unexpected remote advancement (lost-update protection)
#   9. guard behaves correctly from a nested working directory
#  10. both HTTPS and SSH forms of the correct remote normalize correctly
#  11. architecture rule 7 self-test (Unity shell never mutates Domain state)
#  12. new-agent-worktree.sh honours SWG_SESSION_ID (KI-008)
#  13. install-git-hooks.sh --allow-no-origin works only in remote-less fixtures (KI-007)
#  14. new-agent-worktree.sh branches from a freshly fetched <remote>/main, refuses on fetch failure
set -u

REPO_ROOT="$(git rev-parse --show-toplevel)"
GUARD_IDENTITY_EXIT=86
LEASE_BUSY_EXIT=87
PASS=0; FAIL=0
unset GITHUB_REPOSITORY || true

ok()  { PASS=$((PASS+1)); echo "  PASS: $1"; }
bad() { FAIL=$((FAIL+1)); echo "  FAIL: $1" >&2; }

record() { # desc want_exit got_exit logfile
    local desc="$1" want="$2" got="$3" log="$4"
    if [ "$got" -eq "$want" ]; then ok "$desc (exit $got)"
    else bad "$desc — expected exit $want, got $got"; sed 's/^/      | /' "$log" >&2; fi
}

expect_exit() { # desc want_exit cmd...
    local desc="$1" want="$2"; shift 2
    "$@" >/tmp/swg-guard-out.$$ 2>&1
    record "$desc" "$want" $? /tmp/swg-guard-out.$$
    rm -f /tmp/swg-guard-out.$$
}

expect_in_exit() { # desc want_exit dir cmd...
    local desc="$1" want="$2" dir="$3"; shift 3
    ( cd "$dir" && "$@" ) >/tmp/swg-guard-out.$$ 2>&1
    record "$desc" "$want" $? /tmp/swg-guard-out.$$
    rm -f /tmp/swg-guard-out.$$
}

new_clone() { # name -> echoes path; origin set to the FAKE canonical https URL (never fetched)
    local d="$SANDBOX/$1"
    git clone -q --no-hardlinks "$GOLDEN" "$d"
    git -C "$d" remote set-url origin "https://github.com/quantdale/simple-walk-game.git"
    git -C "$d" config core.hooksPath .githooks
    printf '%s\n' "$d"
}

echo "== building isolated fixture repository =="
SANDBOX="$(mktemp -d)"
trap 'rm -rf "$SANDBOX"' EXIT
GOLDEN="$SANDBOX/golden"
mkdir -p "$GOLDEN/.githooks"
cp -r "$REPO_ROOT/scripts" "$GOLDEN/scripts"
cp "$REPO_ROOT"/.githooks/pre-commit "$REPO_ROOT"/.githooks/pre-push "$GOLDEN/.githooks/"
# Minimal identity manifest for the fixture (same slug; sentinels that exist there).
cat > "$GOLDEN/.repo-identity.json" <<'JSON'
{
  "schemaVersion": 1,
  "repository": "quantdale/simple-walk-game",
  "project": "Simple Walk Game",
  "sentinels": ["scripts", ".repo-identity.json"]
}
JSON
git -C "$GOLDEN" -c init.defaultBranch=main init -q
git -C "$GOLDEN" add -A
git -C "$GOLDEN" -c user.name=fixture -c user.email=fixture@local commit -qm "fixture: guard fixture"

echo "== scenario 1: correct repo passes =="
C1="$(new_clone s1)"
expect_exit "guard accepts faithful clone" 0 sh "$C1/scripts/assert-repo-identity.sh" "$C1"

echo "== scenario 2: identity file claiming the SIBLING repo fails =="
C2="$(new_clone s2)"
sed -i 's#"repository": "quantdale/simple-walk-game"#"repository": "quantdale/walk-game"#' "$C2/.repo-identity.json"
expect_exit "guard rejects sibling identity file" "$GUARD_IDENTITY_EXIT" \
    sh "$C2/scripts/assert-repo-identity.sh" "$C2"

echo "== scenario 3: wrong origin slug fails =="
C3="$(new_clone s3)"
git -C "$C3" remote set-url origin "https://github.com/quantdale/walk-game.git"
expect_exit "guard rejects sibling origin (https)" "$GUARD_IDENTITY_EXIT" \
    sh "$C3/scripts/assert-repo-identity.sh" "$C3"
git -C "$C3" remote set-url origin "git@github.com:quantdale/walk-game"
expect_exit "guard rejects sibling origin (ssh)" "$GUARD_IDENTITY_EXIT" \
    sh "$C3/scripts/assert-repo-identity.sh" "$C3"

echo "== scenario 4: wrong CI repository value fails =="
C4="$(new_clone s4)"
expect_exit "guard rejects mismatched GITHUB_REPOSITORY" "$GUARD_IDENTITY_EXIT" \
    env GITHUB_REPOSITORY=quantdale/walk-game sh "$C4/scripts/assert-repo-identity.sh" "$C4"
expect_exit "matching GITHUB_REPOSITORY passes" 0 \
    env GITHUB_REPOSITORY=quantdale/simple-walk-game sh "$C4/scripts/assert-repo-identity.sh" "$C4"

echo "== scenario 5: second writer lease acquisition fails =="
C5="$(new_clone s5)"
expect_in_exit "first lease acquires" 0 "$C5" env SWG_SESSION_ID=w1 sh scripts/writer-lease.sh acquire
expect_in_exit "second lease refused" "$LEASE_BUSY_EXIT" "$C5" env SWG_SESSION_ID=w2 sh scripts/writer-lease.sh acquire
# KI-008 same-session resume: the owner may re-acquire; the lock file is untouched;
# an anonymous (PID-derived) acquirer and a different session are still refused.
_lock5="$(cd "$C5" && git rev-parse --path-format=absolute --git-path simple-walk-game.writer-lock.json)"
_before5="$(cat "$_lock5")"
expect_in_exit "same session re-acquire resumes" 0 "$C5" env SWG_SESSION_ID=w1 sh scripts/writer-lease.sh acquire
if [ "$(cat "$_lock5")" = "$_before5" ]; then ok "same-session resume left the lease file unchanged"
else bad "same-session resume modified the lease file"; fi
expect_in_exit "anonymous acquirer still refused" "$LEASE_BUSY_EXIT" "$C5" env -u SWG_SESSION_ID sh scripts/writer-lease.sh acquire
expect_in_exit "other session still refused after resume" "$LEASE_BUSY_EXIT" "$C5" env SWG_SESSION_ID=w2 sh scripts/writer-lease.sh acquire

echo "== scenario 6: released lease permits a new writer =="
expect_in_exit "foreign release without --force refused" "$LEASE_BUSY_EXIT" "$C5" env SWG_SESSION_ID=w2 sh scripts/writer-lease.sh release
expect_in_exit "owner releases" 0 "$C5" env SWG_SESSION_ID=w1 sh scripts/writer-lease.sh release
expect_in_exit "new writer acquires after release" 0 "$C5" env SWG_SESSION_ID=w2 sh scripts/writer-lease.sh acquire
expect_in_exit "cleanup release" 0 "$C5" env SWG_SESSION_ID=w2 sh scripts/writer-lease.sh release

echo "== scenario 7: stale lease is NOT silently stolen =="
C7="$(new_clone s7)"
printf '%s\n' '{"schemaVersion":1,"repository":"quantdale/simple-walk-game","sessionId":"ghost","hostname":"'"$(hostname 2>/dev/null || echo localhost)"'","pid":"2147483646","branch":"main","startSha":"dead","acquiredAtUtc":"2020-01-01T00:00:00Z"}' \
    > "$C7/.git/simple-walk-game.writer-lock.json"
out="$(cd "$C7" && sh scripts/writer-lease.sh status 2>&1)"; rc=$?
if [ "$rc" -eq "$LEASE_BUSY_EXIT" ] && printf '%s' "$out" | grep -q "STALE CANDIDATE"; then
    ok "stale lease reported BUSY with STALE CANDIDATE diagnostics"
else bad "stale lease diagnostics missing (exit $rc)"; fi
expect_in_exit "plain acquire does NOT steal stale lease" "$LEASE_BUSY_EXIT" "$C7" sh scripts/writer-lease.sh acquire
expect_in_exit "--force alone does NOT steal (env ack required)" "$LEASE_BUSY_EXIT" "$C7" sh scripts/writer-lease.sh acquire --force
if [ -f "$C7/.git/simple-walk-game.writer-lock.json" ] \
    && grep -q '"sessionId":"ghost"' "$C7/.git/simple-walk-game.writer-lock.json"; then
    ok "stale lease file survived all refusal paths, still owned by 'ghost'"
else bad "stale lease file was removed or replaced by a refused acquire"; fi
expect_in_exit "operator override (flag + env ack) takes over explicitly" 0 "$C7" \
    env SWG_ACKNOWLEDGE_LOCK_OVERRIDE=yes SWG_SESSION_ID=override sh scripts/writer-lease.sh acquire --force
expect_in_exit "override holder releases cleanly" 0 "$C7" env SWG_SESSION_ID=override sh scripts/writer-lease.sh release

echo "== scenario 8: pre-push detects unexpected remote advancement =="
BARE="$SANDBOX/race-remote.git"
git clone -q --bare "$GOLDEN" "$BARE"
CA="$(new_clone s8a)"; CB="$(new_clone s8b)"
git -C "$CA" remote add race "$BARE"
git -C "$CB" remote add race "$BARE"
git -C "$CA" -c user.name=a -c user.email=a@local commit -q --allow-empty -m "session A lands work"
git -C "$CA" push -q race main
# Session B started from the older tip and produced DIVERGING work without seeing A's commit.
git -C "$CB" -c user.name=b -c user.email=b@local commit -q --allow-empty -m "session B concurrent work"
push_out="$(git -C "$CB" push race main 2>&1)"; push_rc=$?
if [ "$push_rc" -ne 0 ] && printf '%s' "$push_out" | grep -q "PRE-PUSH REFUSED"; then
    ok "divergent push refused by pre-push lost-update guard"
else bad "divergent push was NOT refused (rc=$push_rc)"; echo "$push_out" | sed 's/^/      | /' >&2; fi
remote_tip="$(git -C "$CB" ls-remote race refs/heads/main | cut -f1)"
local_tip="$(git -C "$CB" rev-parse HEAD)"
if [ "$remote_tip" != "$local_tip" ]; then
    ok "session B's divergent commit did NOT land on the remote"
else bad "refusal failed — remote now holds session B's divergent commit"; fi
# Deliberate reconciliation: fetch, rebase onto incoming work, then integrate.
git -C "$CB" fetch -q race
git -C "$CB" -c user.name=b -c user.email=b@local rebase -q race/main
push_out="$(git -C "$CB" push race main 2>&1)"; push_rc=$?
if [ "$push_rc" -eq 0 ]; then ok "reconciled (rebased) push accepted"
else bad "reconciled push rejected (rc=$push_rc)"; echo "$push_out" | sed 's/^/      | /' >&2; fi

echo "== scenario 9: guard behaves correctly from a nested working directory =="
C9="$(new_clone s9)"
mkdir -p "$C9/src/WalkGame.Domain/Activity/deeper"
NESTED="$C9/src/WalkGame.Domain/Activity/deeper"
expect_in_exit "guard resolves nested cwd to repo root" 0 "$NESTED" sh "$C9/scripts/assert-repo-identity.sh"
expect_in_exit "lease also resolves nested cwd" 0 "$NESTED" env SWG_SESSION_ID=nested sh "$C9/scripts/writer-lease.sh" acquire
expect_in_exit "nested cleanup release" 0 "$NESTED" env SWG_SESSION_ID=nested sh "$C9/scripts/writer-lease.sh" release

echo "== scenario 10: HTTPS and SSH forms of the correct remote normalize identically =="
norm() { ( . "$REPO_ROOT/scripts/repo-identity-lib.sh"; normalize_github_slug "$1" ); }
n1="$(norm "https://github.com/quantdale/simple-walk-game")"
n2="$(norm "https://github.com/quantdale/simple-walk-game.git")"
n3="$(norm "git@github.com:quantdale/simple-walk-game")"
n4="$(norm "git@github.com:quantdale/simple-walk-game.git")"
n5="$(norm "ssh://git@github.com/quantdale/simple-walk-game.git")"
if [ "$n1" = "$n2" ] && [ "$n2" = "$n3" ] && [ "$n3" = "$n4" ] && [ "$n4" = "$n5" ] \
    && [ "$n1" = "quantdale/simple-walk-game" ]; then
    ok "https / scp-ssh / ssh:// forms all normalize to quantdale/simple-walk-game"
else bad "normalization mismatch: [$n1] [$n2] [$n3] [$n4] [$n5]"; fi

echo ""
echo "== scenario 11: architecture rule 7 (Unity shell) rejects violating fixtures =="
expect_exit "verify-architecture.sh --selftest-rule7" 0 bash "$REPO_ROOT/scripts/verify-architecture.sh" --selftest-rule7

echo "== scenario 12: new-agent-worktree.sh honours an exported SWG_SESSION_ID (KI-008) =="
C12="$(new_clone s12)"
git -C "$C12" remote add fixture-base "$GOLDEN"
export SWG_WORKTREE_BASE_REMOTE=fixture-base
expect_in_exit "helper with exported SWG_SESSION_ID creates worktree" 0 "$C12" env SWG_SESSION_ID=exported-sid sh "$C12/scripts/new-agent-worktree.sh" camp a
WT12A="$C12/../simple-walk-game-wt-camp-a"
expect_in_exit "same exported id resumes its own lease" 0 "$WT12A" env SWG_SESSION_ID=exported-sid sh "$C12/scripts/writer-lease.sh" acquire
expect_in_exit "a different id is still refused" 87 "$WT12A" env SWG_SESSION_ID=someone-else sh "$C12/scripts/writer-lease.sh" acquire
expect_in_exit "same exported id releases its own lease" 0 "$WT12A" env SWG_SESSION_ID=exported-sid sh "$C12/scripts/writer-lease.sh" release
expect_in_exit "helper without SWG_SESSION_ID defaults to wt-<campaign>-<session>" 0 "$C12" env -u SWG_SESSION_ID sh "$C12/scripts/new-agent-worktree.sh" camp b
WT12B="$C12/../simple-walk-game-wt-camp-b"
expect_in_exit "default id releases the default lease" 0 "$WT12B" env SWG_SESSION_ID=wt-camp-b sh "$C12/scripts/writer-lease.sh" release

unset SWG_WORKTREE_BASE_REMOTE

echo "== scenario 14: new-agent-worktree.sh branches from a freshly fetched remote main =="
C14="$(new_clone s14)"
STALE14="$(git -C "$C14" rev-parse HEAD)"
git -C "$GOLDEN" -c user.name=fixture -c user.email=fixture@local commit -q --allow-empty -m "fixture: remote advanced"
ADV14="$(git -C "$GOLDEN" rev-parse HEAD)"
git -C "$C14" remote add fixture-base "$GOLDEN"
expect_in_exit "helper creates worktree from fetched base" 0 "$C14" env SWG_WORKTREE_BASE_REMOTE=fixture-base SWG_SESSION_ID=s14 sh "$C14/scripts/new-agent-worktree.sh" camp c
WT14="$C14/../simple-walk-game-wt-camp-c"
GOT14="$(git -C "$WT14" rev-parse HEAD 2>/dev/null || echo none)"
if [ "$GOT14" = "$ADV14" ] && [ "$GOT14" != "$STALE14" ]; then ok "worktree HEAD is the fetched remote main, not the stale local HEAD"
else bad "worktree HEAD $GOT14; want fetched $ADV14 (stale local $STALE14)"; fi
git -C "$C14" remote add fixture-dead "$SANDBOX/does-not-exist"
expect_in_exit "fetch failure refuses (no stale fallback)" 1 "$C14" env SWG_WORKTREE_BASE_REMOTE=fixture-dead SWG_SESSION_ID=s14 sh "$C14/scripts/new-agent-worktree.sh" camp d
if [ ! -e "$C14/../simple-walk-game-wt-camp-d" ]; then ok "no worktree created after fetch failure"; else bad "worktree created despite fetch failure"; fi

echo "== scenario 13: hook installer in a remote-less fixture (KI-007) =="
C13="$SANDBOX/s13"; git clone -q --no-hardlinks "$GOLDEN" "$C13"; git -C "$C13" remote remove origin
expect_in_exit "no origin, no flag: refused" 86 "$C13" sh "$C13/scripts/install-git-hooks.sh"
expect_in_exit "no origin, --allow-no-origin: passes without GITHUB_REPOSITORY" 0 "$C13" env -u GITHUB_REPOSITORY sh "$C13/scripts/install-git-hooks.sh" --allow-no-origin
if [ "$(git -C "$C13" config core.hooksPath)" = ".githooks" ]; then ok "hooksPath configured in the fixture"; else bad "hooksPath not configured"; fi
expect_in_exit "flag still enforces GITHUB_REPOSITORY" 86 "$C13" env GITHUB_REPOSITORY=quantdale/walk-game sh "$C13/scripts/install-git-hooks.sh" --allow-no-origin
expect_in_exit "the plain guard stays strict without origin" 86 "$C13" sh "$C13/scripts/assert-repo-identity.sh"
git -C "$C13" remote add upstream "https://github.com/quantdale/simple-walk-game.git"
expect_in_exit "flag refused when other remotes exist" 86 "$C13" sh "$C13/scripts/install-git-hooks.sh" --allow-no-origin
git -C "$C13" remote remove upstream
git -C "$C13" remote add origin "https://github.com/quantdale/walk-game.git"
expect_in_exit "flag never excuses a WRONG origin" 86 "$C13" sh "$C13/scripts/install-git-hooks.sh" --allow-no-origin
sed -i 's#quantdale/simple-walk-game#quantdale/walk-game#' "$C13/.repo-identity.json"; git -C "$C13" remote remove origin
expect_in_exit "flag refuses a remote-less fixture declaring walk-game" 86 "$C13" env -u GITHUB_REPOSITORY sh "$C13/scripts/install-git-hooks.sh" --allow-no-origin
expect_in_exit "unknown argument is a usage error" 2 "$C13" sh "$C13/scripts/install-git-hooks.sh" --force

echo "=============================="
echo "GUARD PROOF SUITE: $PASS passed, $FAIL failed"
echo "=============================="
[ "$FAIL" -eq 0 ]
