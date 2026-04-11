param(
    [string]$RepoRoot = "",
    [ValidateSet("current", "current_stripver", "lz4", "lz4_stripver", "lz4_notypetree", "lz4_notypetree_stripver")]
    [string]$BuildProfile = "current",
    [switch]$SkipDeploy
)

$ErrorActionPreference = "Stop"

$scriptNames = @(
    "Build-MetaVideoPlayerProofCua.ps1",
    "Build-MetaVideoPlayerSnapshotCua.ps1",
    "Build-MetaSearchBarProofCua.ps1",
    "Build-MetaGridMenu2x4ProofCua.ps1"
)

$commonArgs = @{
    RepoRoot = $RepoRoot
    BuildProfile = $BuildProfile
}

if ($SkipDeploy.IsPresent) {
    $commonArgs.SkipDeploy = $true
}

$results = @()
foreach ($scriptName in $scriptNames) {
    $scriptPath = Join-Path $PSScriptRoot $scriptName
    if (-not (Test-Path -LiteralPath $scriptPath)) {
        throw "Standalone Meta proof script not found: $scriptPath"
    }

    $results += & $scriptPath @commonArgs
}

$results
