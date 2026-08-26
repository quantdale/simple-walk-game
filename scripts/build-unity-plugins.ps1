# Builds the deterministic core (netstandard2.1) and stages managed-plugin DLLs
# into the Unity project so the Editor imports real compiled assemblies rather
# than duplicated source. Run after any change to src/WalkGame.* and before
# opening/verifying the Unity project.
#
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build-unity-plugins.ps1
# Optional: -Configuration Debug|Release (default Release)

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$pluginsDir = Join-Path $repoRoot "unity\Assets\WalkGame\Plugins\Core"

$projects = @(
    "src\WalkGame.Domain\WalkGame.Domain.csproj",
    "src\WalkGame.Application\WalkGame.Application.csproj",
    "src\WalkGame.Infrastructure\WalkGame.Infrastructure.csproj"
)

foreach ($project in $projects) {
    $fullPath = Join-Path $repoRoot $project
    Write-Host "Publishing $project ($Configuration)..."
    dotnet publish $fullPath -c $Configuration --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $project" }
}

New-Item -ItemType Directory -Force $pluginsDir | Out-Null

# The publish output of Infrastructure contains the full managed dependency
# closure with versions resolved by NuGet. Stage only what Unity needs.
$publishDir = Join-Path $repoRoot "src\WalkGame.Infrastructure\bin\$Configuration\netstandard2.1\publish"

$wanted = @(
    "WalkGame.Domain.dll",
    "WalkGame.Application.dll",
    "WalkGame.Infrastructure.dll",
    "System.Text.Json.dll",
    "System.Text.Encodings.Web.dll",
    "Microsoft.Bcl.AsyncInterfaces.dll",
    "System.Buffers.dll",
    "System.Memory.dll",
    "System.Runtime.CompilerServices.Unsafe.dll",
    "System.Threading.Tasks.Extensions.dll",
    "Microsoft.Extensions.Primitives.dll"
)

$copied = @()
foreach ($file in $wanted) {
    $src = Join-Path $publishDir $file
    if (-not (Test-Path $src)) {
        Write-Warning "Not produced by publish (skipping): $file"
        continue
    }
    Copy-Item $src $pluginsDir -Force
    $copied += $file
}

Write-Host ""
Write-Host "Staged $($copied.Count) assemblies into $pluginsDir :"
$copied | ForEach-Object { Write-Host "  $_" }
