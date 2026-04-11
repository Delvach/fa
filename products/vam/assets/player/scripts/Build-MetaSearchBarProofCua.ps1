param(
    [string]$RepoRoot = "",
    [string]$UnityEditorPath = "",
    [string]$OutputRoot = "",
    [string]$DeployAssetsRoot = "F:\sim\vam\Custom\Assets\FrameAngel\Meta",
    [string]$DeployPresetRoot = "F:\sim\vam\Custom\Atom\CustomUnityAsset",
    [ValidateSet("current", "current_stripver", "lz4", "lz4_stripver", "lz4_notypetree", "lz4_notypetree_stripver")]
    [string]$BuildProfile = "current",
    [switch]$SkipDeploy
)

$ErrorActionPreference = "Stop"

$laneResolverPath = Join-Path (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))))) "shared\scripts\player-assets\Resolve-FrameAngelPlayerRoots.ps1"
. $laneResolverPath

$laneRoots = Get-FrameAngelPlayerLaneRoots -RepoRoot $RepoRoot -CallerScriptRoot $PSScriptRoot -EnsureAssetLaneScaffold
$resolvedOutputRoot = if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    Join-Path $laneRoots.AssetsPlayerBuildRoot "meta_search_bar_cua"
}
else {
    [System.IO.Path]::GetFullPath($OutputRoot)
}

$surfaceRoot = Join-Path $laneRoots.AssetsPlayerBuildRoot "meta_toolkit_catalog\theme_00\meta_textinputfield_searchbar_721072e4"
$packageRootPath = Get-ChildItem -Path $surfaceRoot -Directory | Select-Object -First 1 -ExpandProperty FullName
if ([string]::IsNullOrWhiteSpace($packageRootPath)) {
    throw "Search bar surface package was not found under: $surfaceRoot"
}

$commonArgs = @{
    RepoRoot = $RepoRoot
    UnityEditorPath = $UnityEditorPath
    PackageRootPath = $packageRootPath
    OutputRoot = $resolvedOutputRoot
    DeployAssetsRoot = $DeployAssetsRoot
    DeployPresetRoot = $DeployPresetRoot
    ResourceId = "fa_meta_search_bar_proof"
    BundleFileName = "fa_meta_search_bar_proof.assetbundle"
    PresetFileName = "Preset_FA Meta Search Bar Proof.vap"
    SummaryFileName = "meta_search_bar_proof_cua_export_summary.json"
    BuildProfile = $BuildProfile
}

if ($SkipDeploy.IsPresent) {
    $commonArgs.SkipDeploy = $true
}

& (Join-Path $PSScriptRoot "Build-MetaControlSurfaceProofCua.ps1") @commonArgs

