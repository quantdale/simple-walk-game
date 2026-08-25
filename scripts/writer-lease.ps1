# Writer lease for quantdale/simple-walk-game (Windows/PowerShell twin of
# scripts/writer-lease.sh). Single-writer protection inside ONE work tree; the lease
# file lives under .git/ so it is never committed and is naturally per-worktree.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File scripts\writer-lease.ps1 -Acquire [-Force]
#   powershell -NoProfile -ExecutionPolicy Bypass -File scripts\writer-lease.ps1 -Release [-Force]
#   powershell -NoProfile -ExecutionPolicy Bypass -File scripts\writer-lease.ps1 -Status
#
# Override requires BOTH -Force AND environment SWG_ACKNOWLEDGE_LOCK_OVERRIDE=yes.
# Exit codes: 0 ok | 86 identity failure | 87 lease busy / refused | 1 usage.
param(
    [switch]$Acquire,
    [switch]$Release,
    [switch]$Status,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$LEASE_EXIT_BUSY = 87
$GUARD_EXIT_IDENTITY = 86
$OverrideEnv = "SWG_ACKNOWLEDGE_LOCK_OVERRIDE=yes"

function Fail-Busy([string]$Reason) {
    Write-Host "WRITER LEASE: REFUSED -- $Reason"
    exit $LEASE_EXIT_BUSY
}
function Fail-Identity([string]$Reason) {
    Write-Output "REPOSITORY IDENTITY GUARD: FAIL -- $Reason"
    exit $GUARD_EXIT_IDENTITY
}

$verbs = @(@($Acquire.IsPresent, $Release.IsPresent, $Status.IsPresent) | Where-Object { $_ })
if ($verbs.Count -ne 1) {
    Write-Host "usage: writer-lease.ps1 -Acquire|-Release|-Status [-Force]" -ForegroundColor Yellow
    exit 1
}

# --- identity preflight (mirrors assert-repo-identity.ps1) ---
$RepoRoot = (& git rev-parse --show-toplevel 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($RepoRoot)) { Fail-Identity "not inside a Git work tree." }
$IdFile = Join-Path $RepoRoot ".repo-identity.json"
if (-not (Test-Path -LiteralPath $IdFile)) { Fail-Identity "missing $IdFile." }
try { $Identity = Get-Content -LiteralPath $IdFile -Raw | ConvertFrom-Json } catch { Fail-Identity "invalid .repo-identity.json" }
$ExpectedRepo = [string]$Identity.repository
$Origin = (& git -C $RepoRoot remote get-url origin 2>$null).Trim()
if ([string]::IsNullOrWhiteSpace($Origin)) { Fail-Identity "no 'origin' remote configured." }
switch -Regex ($Origin) {
    '^[^/]+@github\.com:(?<slug>.+?)(?:\.git)?$' { $Slug = $Matches.slug.TrimStart('/'); break }
    '^ssh://[^/]+@github\.com/(?<slug>.+?)(?:\.git)?$' { $Slug = $Matches.slug.TrimStart('/'); break }
    '^https?://github\.com/(?<slug>.+?)(?:\.git)?$' { $Slug = $Matches.slug.TrimStart('/'); break }
    default { Fail-Identity "origin '$Origin' is not a recognizable GitHub remote." }
}
if ($Slug -ne $ExpectedRepo) { Fail-Identity "origin resolves to '$Slug' but checkout declares '$ExpectedRepo'." }

$LockPath = (& git -C $RepoRoot rev-parse --path-format=absolute --git-path simple-walk-game.writer-lock.json 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($LockPath)) {
    $LockPath = Join-Path $RepoRoot ".git\simple-walk-game.writer-lock.json"
}

function Read-Lock {
    try { Get-Content -LiteralPath $LockPath -Raw | ConvertFrom-Json } catch { $null }
}

function Show-Lock([object]$Lock) {
    $ageMin = 0
    if ($Lock.acquiredAtUtc) {
        try { $ageMin = [int][Math]::Floor(((Get-Date).ToUniversalTime() - [datetimeoffset]::Parse($Lock.acquiredAtUtc)).TotalMinutes) } catch {}
    }
    Write-Host "    holder repository : $($Lock.repository)"
    Write-Host "    holder session    : $($Lock.sessionId)"
    Write-Host "    holder host/pid   : $($Lock.hostname) / $($Lock.pid)"
    Write-Host "    holder branch     : $($Lock.branch)"
    Write-Host "    holder start SHA  : $($Lock.startSha)"
    Write-Host "    acquired at       : $($Lock.acquiredAtUtc) ($ageMin min ago)"
    if ($Lock.hostname -eq [System.Environment]::MachineName -and $Lock.pid) {
        $alive = $false
        try { $null = Get-Process -Id ([int]$Lock.pid) -ErrorAction Stop; $alive = $true } catch {}
        if (-not $alive) {
            Write-Host "    NOTE: holder PID does not appear alive on this host -- STALE CANDIDATE."
            Write-Host "          Stale locks are NEVER removed automatically; a human decides after"
            Write-Host "          verifying the session is truly gone: scripts\writer-lease.ps1 -Release -Force"
        }
    }
}

if ($Status) {
    if (Test-Path -LiteralPath $LockPath) {
        Write-Host "WRITER LEASE: BUSY"; Show-Lock (Read-Lock); exit $LEASE_EXIT_BUSY
    }
    Write-Host "WRITER LEASE: FREE"; exit 0
}

if ($Acquire) {
    if (Test-Path -LiteralPath $LockPath) {
        Write-Host "WRITER LEASE: another writer holds the lease for this work tree."
        Show-Lock (Read-Lock)
        if ($Force) {
            if ($env:SWG_ACKNOWLEDGE_LOCK_OVERRIDE -ne "yes") {
                Write-Host ""
                Write-Host "Override refused -- '-Force' additionally requires env SWG_ACKNOWLEDGE_LOCK_OVERRIDE=yes"
                exit $LEASE_EXIT_BUSY
            }
            Write-Host ""
            Write-Host "WARNING: operator-acknowledged override; replacing the existing lease."
            Remove-Item -LiteralPath $LockPath -Force
        } else {
            Write-Host ""
            Write-Host "A second autonomous writer MUST STOP here. Options:"
            Write-Host "  * work in your own worktree instead: scripts/new-agent-worktree.sh <campaign> <session-id>"
            Write-Host "  * human operator, holder confirmed dead: env SWG_ACKNOWLEDGE_LOCK_OVERRIDE=yes + scripts\writer-lease.ps1 -Acquire -Force"
            exit $LEASE_EXIT_BUSY
        }
    }
    $SessionId = if ($env:SWG_SESSION_ID) { $env:SWG_SESSION_ID } else { "pid-$PID-$(Get-Date -UFormat %s)" }
    $Branch = (& git rev-parse --abbrev-ref HEAD)
    $StartSha = (& git rev-parse HEAD)
    $Payload = [ordered]@{
        schemaVersion  = 1
        repository     = $ExpectedRepo
        sessionId      = $SessionId
        hostname       = [System.Environment]::MachineName
        pid            = "$PID"
        branch         = $Branch
        startSha       = $StartSha
        acquiredAtUtc  = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")
    } | ConvertTo-Json -Compress
    # Atomic create-or-fail: 'CreateNew' throws if the file already exists (no TOCTOU).
    try {
        $fs = [System.IO.File]::Open($LockPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        try {
            $bytes = [System.Text.Encoding]::UTF8.GetBytes($Payload + "`n")
            $fs.Write($bytes, 0, $bytes.Length)
        } finally { $fs.Dispose() }
    } catch [System.IO.IOException] {
        Write-Host "WRITER LEASE: REFUSED -- lost atomic creation race; another writer holds the lease."
        if (Test-Path -LiteralPath $LockPath) { Show-Lock (Read-Lock) }
        exit $LEASE_EXIT_BUSY
    }
    Write-Host "WRITER LEASE: ACQUIRED ($SessionId)"
    Write-Host "    repository : $ExpectedRepo"
    Write-Host "    branch     : $($Branch)"
    Write-Host "    start SHA  : $($StartSha)"
    Write-Host "Release on normal completion: scripts\writer-lease.ps1 -Release"
    exit 0
}

if ($Release) {
    if (-not (Test-Path -LiteralPath $LockPath)) { Write-Host "WRITER LEASE: FREE (nothing to release)"; exit 0 }
    $Lock = Read-Lock
    $mine = ($env:SWG_SESSION_ID -and $Lock.sessionId -eq $env:SWG_SESSION_ID) -or ("$($Lock.pid)" -eq "$PID")
    if ($mine) {
        Remove-Item -LiteralPath $LockPath -Force
        Write-Host "WRITER LEASE: RELEASED ($($Lock.sessionId))"
        exit 0
    }
    if ($Force) {
        Write-Host "WARNING: releasing a lease owned by another session ($($Lock.sessionId))."
        Show-Lock $Lock
        Remove-Item -LiteralPath $LockPath -Force
        Write-Host "WRITER LEASE: FORCE-RELEASED"
        exit 0
    }
    Write-Host "WRITER LEASE: REFUSED -- lease belongs to another session ($($Lock.sessionId))."
    Show-Lock $Lock
    Write-Host "Human operator, session confirmed dead: scripts\writer-lease.ps1 -Release -Force"
    exit $LEASE_EXIT_BUSY
}
