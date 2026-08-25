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
echo "=============================="
echo "GUARD PROOF SUITE: $PASS passed, $FAIL failed"
echo "=============================="
[ "$FAIL" -eq 0 ]
