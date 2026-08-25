# Repository identity guard -- quantdale/simple-walk-game (Windows/PowerShell twin of
# scripts/assert-repo-identity.sh). Fails with exit code 86 on any identity mismatch.
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File scripts\assert-repo-identity.ps1 [-RepoRoot <path>]
param(
    [string]$RepoRoot = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$GUARD_EXIT_IDENTITY = 86

function Fail-Identity([string]$Reason) {
    Write-Error @"
REPOSITORY IDENTITY GUARD: FAIL -- $Reason
This session must STOP modifying anything.
If you are an autonomous agent: you are probably in (or pointed at) the wrong repository.
Do not reset, clean, commit, or push here. Re-checkout the correct repository and re-run
the preflight. Human operators may override only after manually confirming the checkout.
"@ -ErrorAction Continue
    exit $GUARD_EXIT_IDENTITY
}

if ($RepoRoot -eq "") {
    $RepoRoot = (& git rev-parse --show-toplevel 2>$null)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($RepoRoot)) {
        Fail-Identity "not inside a Git work tree."
    }
}

$IdFile = Join-Path $RepoRoot ".repo-identity.json"
if (-not (Test-Path -LiteralPath $IdFile)) { Fail-Identity "missing $IdFile." }

try { $Identity = Get-Content -LiteralPath $IdFile -Raw | ConvertFrom-Json }
catch { Fail-Identity ".repo-identity.json is not valid JSON: $($_.Exception.Message)" }

$ExpectedRepo = [string]$Identity.repository
$ExpectedProject = [string]$Identity.project
if ([string]::IsNullOrWhiteSpace($ExpectedRepo)) {
    Fail-Identity ".repo-identity.json has no 'repository' slug."
}

$Origin = (& git -C $RepoRoot remote get-url origin 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($Origin)) {
    Fail-Identity "no 'origin' remote configured."
}
$Origin = $Origin.Trim()

# Normalize https / scp-like ssh / ssh:// forms to owner/repo.
switch -Regex ($Origin) {
    '^[^/]+@github\.com:(?<slug>.+?)(?:\.git)?$' { $Slug = $Matches.slug; break }
    '^ssh://[^/]+@github\.com/(?<slug>.+?)(?:\.git)?$' { $Slug = $Matches.slug; break }
    '^https?://github\.com/(?<slug>.+?)(?:\.git)?$' { $Slug = $Matches.slug; break }
    default { Fail-Identity "origin '$Origin' is not a recognizable GitHub remote." }
}
$Slug = $Slug.TrimStart('/')

if ($Slug -ne $ExpectedRepo) {
    Fail-Identity "origin resolves to '$Slug' but this checkout declares '$ExpectedRepo'."
}

foreach ($Sentinel in @($Identity.sentinels)) {
    if (-not (Test-Path -LiteralPath (Join-Path $RepoRoot $Sentinel))) {
        Fail-Identity "sentinel '$Sentinel' is missing from this tree."
    }
}

if (-not [string]::IsNullOrEmpty($env:GITHUB_REPOSITORY) -and $env:GITHUB_REPOSITORY -ne $ExpectedRepo) {
    Fail-Identity "GITHUB_REPOSITORY='$($env:GITHUB_REPOSITORY)' does not match declared '$ExpectedRepo'."
}

Write-Output "REPOSITORY IDENTITY OK: $Slug ($ExpectedProject)"
exit 0
