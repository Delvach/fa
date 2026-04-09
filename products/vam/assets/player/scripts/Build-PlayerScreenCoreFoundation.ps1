param(
    [string]$RepoRoot = "",
    [string]$VamManagedDir = "F:\sim\vam\VaM_Data\Managed",
    [string]$Version = "",
    [switch]$AllowExistingVersion,
    [switch]$SkipLiveDeploy
)

$ErrorActionPreference = "Stop"

$laneResolverPath = Join-Path (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))))) "shared\scripts\player-assets\Resolve-FrameAngelPlayerRoots.ps1"
. $laneResolverPath
$versionResolverPath = Join-Path (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))))) "shared\scripts\player-assets\Resolve-FrameAngelPlayerVersion.ps1"
. $versionResolverPath

function Ensure-Directory {
    param([string]$PathValue)

    if (-not (Test-Path -LiteralPath $PathValue)) {
        New-Item -ItemType Directory -Path $PathValue -Force | Out-Null
    }
}

function Write-JsonFile {
    param(
        [string]$Path,
        [object]$Value
    )

    $directory = Split-Path -Parent $Path
    Ensure-Directory -PathValue $directory
    $json = $Value | ConvertTo-Json -Depth 50
    [System.IO.File]::WriteAllText($Path, $json, (New-Object System.Text.UTF8Encoding($false)))
}

function Remove-FilesByPattern {
    param(
        [string]$DirectoryPath,
        [string]$Filter
    )

    if (-not (Test-Path -LiteralPath $DirectoryPath)) {
        return
    }

    Get-ChildItem -LiteralPath $DirectoryPath -Filter $Filter -File -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-Item -LiteralPath $_.FullName -Force
    }
}

$laneRoots = Get-FrameAngelPlayerLaneRoots -RepoRoot $RepoRoot -CallerScriptRoot $PSScriptRoot -EnsureAssetLaneScaffold
$RepoRoot = $laneRoots.RepoRoot
$resolvedVersion = if ([string]::IsNullOrWhiteSpace($Version)) {
    (Read-FrameAngelPlayerVersionState -RepoRoot $RepoRoot).Version
}
else {
    $Version.Trim()
}

$releaseRoot = Join-Path $laneRoots.AssetsPlayerBuildRoot (Join-Path "releases" $resolvedVersion)
$latestManifestPath = Join-Path $laneRoots.AssetsPlayerBuildRoot "releases\player_screen_core_release_latest.json"
$repoAssetPath = Join-Path $releaseRoot ("fa_player_asset.{0}.assetbundle" -f $resolvedVersion)
$repoPluginPath = Join-Path $releaseRoot ("fa_cua_player.{0}.dll" -f $resolvedVersion)
$liveAssetPath = Join-Path "F:\sim\vam\Custom\Assets\FrameAngel\Player" ("fa_player_asset.{0}.assetbundle" -f $resolvedVersion)
$livePluginPath = Join-Path "F:\sim\vam\Custom\Plugins" ("fa_cua_player.{0}.dll" -f $resolvedVersion)
$manifestPath = Join-Path $releaseRoot "foundation_release_manifest.json"
$validatorScript = Join-Path $laneRoots.AssetsPlayerRoot "scripts\Validate-PlayerScreenCoreRelease.ps1"
$pluginBuildScript = Join-Path $laneRoots.AssetsPlayerRoot "scripts\Build-CuaPlayerResource.ps1"
$assetBuildScript = Join-Path $laneRoots.AssetsPlayerRoot "scripts\Build-PlayerAssetBundle.ps1"

if (-not $AllowExistingVersion.IsPresent) {
    $collisionPaths = @($repoAssetPath, $repoPluginPath)
    if (-not $SkipLiveDeploy.IsPresent) {
        $collisionPaths += @($liveAssetPath, $livePluginPath)
    }

    Assert-FrameAngelPlayerVersionAvailable `
        -RepoRoot $RepoRoot `
        -Version $resolvedVersion `
        -ArtifactPaths $collisionPaths `
        -ContextLabel "player screen-core release" | Out-Null
}

$pluginArgs = @(
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-File",
    $pluginBuildScript,
    "-RepoRoot",
    $RepoRoot,
    "-VamManagedDir",
    $VamManagedDir,
    "-Version",
    $resolvedVersion,
    "-Configuration",
    "Release"
)
if ($AllowExistingVersion.IsPresent) {
    $pluginArgs += "-AllowExistingVersion"
}
if ($SkipLiveDeploy.IsPresent) {
    $pluginArgs += "-SkipDeploy"
}

& powershell @pluginArgs
if ($LASTEXITCODE -ne 0) {
    throw "Build-CuaPlayerResource.ps1 failed."
}

$assetArgs = @(
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-File",
    $assetBuildScript,
    "-RepoRoot",
    $RepoRoot,
    "-Version",
    $resolvedVersion
)
if ($AllowExistingVersion.IsPresent) {
    $assetArgs += "-AllowExistingVersion"
}
if ($SkipLiveDeploy.IsPresent) {
    $assetArgs += "-SkipDeploy"
}

& powershell @assetArgs
if ($LASTEXITCODE -ne 0) {
    throw "Build-PlayerAssetBundle.ps1 failed."
}

$repoBuiltPluginPath = Join-Path $RepoRoot "deployed\plugins\fa_cua_player.$resolvedVersion.dll"
if (-not (Test-Path -LiteralPath $repoBuiltPluginPath)) {
    throw "Built plugin artifact not found: $repoBuiltPluginPath"
}

Ensure-Directory -PathValue $releaseRoot
Copy-Item -LiteralPath $repoBuiltPluginPath -Destination $repoPluginPath -Force

if (-not $SkipLiveDeploy.IsPresent) {
    Remove-FilesByPattern -DirectoryPath "F:\sim\vam\Custom\Atom\CustomUnityAsset" -Filter "Preset_FA Player Asset *.vap"
    Remove-FilesByPattern -DirectoryPath "F:\sim\vam\Custom\Assets\FrameAngel\Player" -Filter "fa_cua_player.*.dll"
    Remove-FilesByPattern -DirectoryPath "F:\sim\vam\Custom\Plugins" -Filter "fa_player_plugin.*.dll"
}

$validationArgs = @(
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-File",
    $validatorScript,
    "-RepoRoot",
    $RepoRoot,
    "-Version",
    $resolvedVersion,
    "-ReleaseRoot",
    $releaseRoot,
    "-RepoAssetPath",
    $repoAssetPath,
    "-RepoPluginPath",
    $repoPluginPath,
    "-LiveAssetPath",
    $liveAssetPath,
    "-LivePluginPath",
    $livePluginPath
)
if ($SkipLiveDeploy.IsPresent) {
    $validationArgs += "-SkipLiveDeployChecks"
}

& powershell @validationArgs
if ($LASTEXITCODE -ne 0) {
    throw "Validate-PlayerScreenCoreRelease.ps1 failed."
}

$manifest = [ordered]@{
    schemaVersion = "frameangel_player_screen_core_release_v3"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    version = $resolvedVersion
    repoAssetPath = $repoAssetPath
    repoPluginPath = $repoPluginPath
    liveAssetPath = if ($SkipLiveDeploy.IsPresent) { "" } else { $liveAssetPath }
    livePluginPath = if ($SkipLiveDeploy.IsPresent) { "" } else { $livePluginPath }
    releaseSurface = [ordered]@{
        phase = "phase_1_screen_with_vam_controls"
        authoritySeam = "versioned assetbundle plus matching manually attached plugin"
        expectedControlSurface = "VaM buttons and sliders bound to exposed player methods"
        deterministicSceneWitness = "allowed as a later witness seam, but not required by this build wrapper"
        includesMetaUi = $false
    }
    process = [ordered]@{
        pluginBuildScript = $pluginBuildScript
        assetBuildScript = $assetBuildScript
        validatorScript = $validatorScript
        deploysVersionedPlugin = $true
        deploysVersionedAssetbundle = $true
        removesLegacyPresetDrift = $true
        removesAssetSideDllDrift = $true
    }
}
Write-JsonFile -Path $manifestPath -Value $manifest
Write-JsonFile -Path $latestManifestPath -Value ([ordered]@{
    schemaVersion = "frameangel_player_screen_core_release_latest_v1"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    version = $resolvedVersion
    releaseRoot = $releaseRoot
    manifestPath = $manifestPath
    repoAssetPath = $repoAssetPath
    repoPluginPath = $repoPluginPath
    liveAssetPath = if ($SkipLiveDeploy.IsPresent) { "" } else { $liveAssetPath }
    livePluginPath = if ($SkipLiveDeploy.IsPresent) { "" } else { $livePluginPath }
})

[pscustomobject]@{
    version = $resolvedVersion
    releaseRoot = $releaseRoot
    repoAssetPath = $repoAssetPath
    repoPluginPath = $repoPluginPath
    liveAssetPath = if ($SkipLiveDeploy.IsPresent) { "" } else { $liveAssetPath }
    livePluginPath = if ($SkipLiveDeploy.IsPresent) { "" } else { $livePluginPath }
    manifestPath = $manifestPath
    latestManifestPath = $latestManifestPath
}
