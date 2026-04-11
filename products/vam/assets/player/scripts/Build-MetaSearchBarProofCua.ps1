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

function Resolve-MetaSurfacePackageRoot {
    param(
        [string]$SurfaceRoot,
        [string]$SummaryPath,
        [string]$ExpectedControlSurfaceId,
        [string]$ExpectedControlFamilyId
    )

    if (-not [string]::IsNullOrWhiteSpace($SummaryPath) -and (Test-Path -LiteralPath $SummaryPath)) {
        $summary = Get-Content -LiteralPath $SummaryPath -Raw | ConvertFrom-Json
        $match = @($summary.surfaces | Where-Object {
            $_ -and (
                [string]::Equals([string]$_.controlSurfaceId, $ExpectedControlSurfaceId, [System.StringComparison]::OrdinalIgnoreCase) -or
                [string]::Equals([string]$_.controlFamilyId, $ExpectedControlFamilyId, [System.StringComparison]::OrdinalIgnoreCase)
            )
        } | Select-Object -First 1)
        if ($match.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace([string]$match[0].packageRootPath)) {
            return [System.IO.Path]::GetFullPath([string]$match[0].packageRootPath)
        }
    }

    $candidates = @()
    Get-ChildItem -LiteralPath $SurfaceRoot -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        $controlsPath = Join-Path $_.FullName "controls.innerpiece.json"
        $receiptPath = Join-Path $_.FullName "export_receipt.json"
        if (-not (Test-Path -LiteralPath $controlsPath)) {
            return
        }

        $controls = Get-Content -LiteralPath $controlsPath -Raw | ConvertFrom-Json
        $matches = [string]::Equals([string]$controls.controlSurfaceId, $ExpectedControlSurfaceId, [System.StringComparison]::OrdinalIgnoreCase) -or
            [string]::Equals([string]$controls.controlFamilyId, $ExpectedControlFamilyId, [System.StringComparison]::OrdinalIgnoreCase)
        if (-not $matches) {
            return
        }

        $exportedAtUtc = ""
        if (Test-Path -LiteralPath $receiptPath) {
            $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
            $exportedAtUtc = [string]$receipt.exportedAtUtc
        }

        $candidates += [pscustomobject]@{
            packageRootPath = $_.FullName
            exportedAtUtc = $exportedAtUtc
        }
    }

    $resolved = @($candidates |
        Sort-Object -Property @{
            Expression = {
                if ([string]::IsNullOrWhiteSpace($_.exportedAtUtc)) { [datetime]::MinValue }
                else { [datetime]::Parse($_.exportedAtUtc, [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::RoundtripKind) }
            }
        }, @{
            Expression = { $_.packageRootPath }
        } -Descending |
        Select-Object -First 1)

    if ($resolved.Count -gt 0) {
        return [System.IO.Path]::GetFullPath([string]$resolved[0].packageRootPath)
    }

    return ""
}

$laneRoots = Get-FrameAngelPlayerLaneRoots -RepoRoot $RepoRoot -CallerScriptRoot $PSScriptRoot -EnsureAssetLaneScaffold
$resolvedOutputRoot = if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    Join-Path $laneRoots.AssetsPlayerBuildRoot "meta_search_bar_cua"
}
else {
    [System.IO.Path]::GetFullPath($OutputRoot)
}

$surfaceRoot = Join-Path $laneRoots.AssetsPlayerBuildRoot "meta_toolkit_catalog\theme_00\meta_textinputfield_searchbar_721072e4"
$summaryPath = Join-Path $laneRoots.AssetsPlayerBuildRoot "meta_toolkit_catalog\theme_00\ghost_meta_ui_toolkit_export_summary_theme_00.json"
$packageRootPath = Resolve-MetaSurfacePackageRoot `
    -SurfaceRoot $surfaceRoot `
    -SummaryPath $summaryPath `
    -ExpectedControlSurfaceId "meta_textinputfield_searchbar_721072e4" `
    -ExpectedControlFamilyId "meta_ui_search_bar"
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
