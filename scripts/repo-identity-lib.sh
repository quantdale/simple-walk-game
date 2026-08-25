#!/bin/sh
# Shared repository-identity logic for quantdale/simple-walk-game.
# Sourced by the identity guard, git hooks, and the guard proof tests.
#
# Identity failure exit code: 86 (distinctive; see AGENTS.md "Exit codes").
GUARD_EXIT_IDENTITY=86

# Normalize a GitHub remote URL to "owner/repo".
# Accepts: https://github.com/o/r(.git), ssh git@github.com:o/r(.git), ssh://git@github.com/o/r(.git).
normalize_github_slug() {
    _ngs_url="$1"
    case "$_ngs_url" in
        git@github.com:* )
            _ngs_url="${_ngs_url#git@github.com:}" ;;
        ssh://git@github.com/* )
            _ngs_url="${_ngs_url#ssh://git@github.com/}" ;;
        https://github.com/*|http://github.com/* )
            _ngs_url="${_ngs_url#*github.com/}" ;;
        * )
            return 1 ;;
    esac
    _ngs_url="${_ngs_url%.git}"
    _ngs_url="${_ngs_url#/}"
    case "$_ngs_url" in
        */*|"" ) printf '%s\n' "$_ngs_url";;
        * ) return 1;;
    esac
}

# Read one string value from .repo-identity.json without requiring jq.
# Only trusted for this repository-owned, flat, double-keyed schema.
identity_field() {
    _if_file="$1"; _if_key="$2"
    sed -n 's/.*"'"$_if_key"'"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$_if_file" | head -n 1
}

fail_identity() {
    echo "REPOSITORY IDENTITY GUARD: FAIL — $1" >&2
    echo "This session must STOP modifying anything." >&2
    echo "If you are an autonomous agent: you are probably in (or pointed at) the wrong" >&2
    echo "repository. Do not reset, clean, commit, or push here. Re-checkout the correct" >&2
    echo "repository and re-run the preflight. Human operators may override only after" >&2
    echo "manually confirming the checkout they intended." >&2
    exit "$GUARD_EXIT_IDENTITY"
}

# Full assertion. Usage: assert_repo_identity [repo_root]
# Resolves the root itself when unset, so it behaves correctly from nested dirs.
assert_repo_identity() {
    _ari_root="$1"
    if [ -z "$_ari_root" ]; then
        _ari_root="$(git rev-parse --show-toplevel 2>/dev/null)" ||
            fail_identity "not inside a Git work tree."
    fi

    _ari_id_file="$_ari_root/.repo-identity.json"
    [ -f "$_ari_id_file" ] || fail_identity "missing $_ari_id_file."

    _ari_expected_repo="$(identity_field "$_ari_id_file" repository)"
    _ari_expected_project="$(identity_field "$_ari_id_file" project)"
    [ -n "$_ari_expected_repo" ] || fail_identity ".repo-identity.json has no 'repository' slug."

    # 1. Origin slug.
    _ari_origin="$(git -C "$_ari_root" remote get-url origin 2>/dev/null)" ||
        fail_identity "no 'origin' remote configured."
    _ari_slug="$(normalize_github_slug "$_ari_origin")" ||
        fail_identity "origin '$(_ari_origin)' is not a recognizable GitHub remote."
    if [ "$_ari_slug" != "$_ari_expected_repo" ]; then
        fail_identity "origin resolves to '$_ari_slug' but this checkout declares '$_ari_expected_repo'."
    fi

    # 2. Sentinel files from the identity manifest.
    _ari_sentinels="$(sed -n '/"sentinels"/,/^[[:space:]]*\]/p' "$_ari_id_file" | grep -o '"[^"]*"' | grep -v sentinels | tr -d '"')"
    if [ -z "$_ari_sentinels" ]; then
        fail_identity "identity file declares no sentinel files."
    fi
    for _ari_sentinel in $_ari_sentinels; do
        [ -e "$_ari_root/$_ari_sentinel" ] ||
            fail_identity "sentinel '$_ari_sentinel' is missing from this tree."
    done

    # 3. CI-provided repository, whenever present (set by GitHub Actions or by tests).
    if [ -n "${GITHUB_REPOSITORY:-}" ] && [ "$GITHUB_REPOSITORY" != "$_ari_expected_repo" ]; then
        fail_identity "GITHUB_REPOSITORY='$GITHUB_REPOSITORY' does not match declared '$_ari_expected_repo'."
    fi

    echo "REPOSITORY IDENTITY OK: $_ari_slug ($_ari_expected_project)"
}
