param(
    [string]$RepoRoot = "",
    [ValidateSet("current", "current_stripver", "lz4", "lz4_stripver", "lz4_notypetree", "lz4_notypetree_stripver")]
    [string]$BuildProfile = "current",
    [switch]$SkipDeploy
)

$ErrorActionPreference = "Stop"

$proofScriptPath = Join-Path $PSScriptRoot "Build-MetaVideoPlayerProofCua.ps1"
$snapshotScriptPath = Join-Path $PSScriptRoot "Build-MetaVideoPlayerSnapshotCua.ps1"

if (-not (Test-Path -LiteralPath $proofScriptPath)) {
    throw "Meta proof build script not found: $proofScriptPath"
}

if (-not (Test-Path -LiteralPath $snapshotScriptPath)) {
    throw "Meta snapshot build script not found: $snapshotScriptPath"
}

$commonArgs = @{
    RepoRoot = $RepoRoot
    BuildProfile = $BuildProfile
}

if ($SkipDeploy.IsPresent) {
    $commonArgs.SkipDeploy = $true
}

$liveProofResult = & $proofScriptPath @commonArgs
$snapshotResult = & $snapshotScriptPath @commonArgs

[pscustomobject]@{
    liveProof = $liveProofResult
    snapshotProof = $snapshotResult
}
