# Configure this clone so the tracked guards actually run (Windows twin of
# scripts/install-git-hooks.sh): git config core.hooksPath .githooks
$ErrorActionPreference = "Stop"
$root = (& git rev-parse --show-toplevel)
if ($LASTEXITCODE -ne 0) { throw "not inside a Git work tree" }
& git -C $root config core.hooksPath .githooks
if ($LASTEXITCODE -ne 0) { throw "failed to set core.hooksPath" }
Write-Host ("hooks installed: core.hooksPath=" + (& git -C $root config core.hooksPath))
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "scripts\assert-repo-identity.ps1")
exit $LASTEXITCODE
