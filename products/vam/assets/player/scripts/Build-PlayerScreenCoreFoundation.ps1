param(
    [string]$RepoRoot = "",
    [string]$VamManagedDir = "F:\sim\vam\VaM_Data\Managed",
    [string]$Version = "",
    [switch]$AllowExistingVersion,
    [switch]$SkipLiveDeploy,
    [switch]$BuildVarPackage,
    [switch]$PackageOnlyDeploy,
    [switch]$IncludeVarScene,
    [switch]$IncludeVarDiagnosticsScene,
    [string]$VarSceneTemplatePath = "F:\sim\vam\Saves\scene\buttons_setup_scene.json",
    [string]$VarScenePrimaryMediaPath = "",
    [string]$VarSceneDiagnosticsFilter = "",
    [ValidateSet("single_display_fit", "multi_aspect")]
    [string]$VarSceneDisplayPolicy = "multi_aspect",
    [int]$VarSceneIncludeManagedControls = 0,
    [string]$VarDemoMediaSourceRoot = "",
    [string]$VarDemoMediaPackageRelativeRoot = "Custom\Images\FrameAngel\Player\demo_media",
    [string]$VarCreatorName = "FrameAngel",
    [string]$VarPackageName = "Player",
    [int]$VarPublicRelease = 0,
    [string]$VarDestinationAddonPackages = "F:\sim\vam\AddonPackages",
    [switch]$SkipVarDistribute
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

function Write-TextFile {
    param(
        [string]$Path,
        [string]$Value
    )

    $directory = Split-Path -Parent $Path
    Ensure-Directory -PathValue $directory
    [System.IO.File]::WriteAllText($Path, $Value, (New-Object System.Text.UTF8Encoding($false)))
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

function Read-PlayerReleaseChangelog {
    param(
        [string]$Path,
        [string]$ExpectedVersion
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Release changelog source not found: $Path"
    }

    $changelog = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ($null -eq $changelog) {
        throw "Release changelog could not be read: $Path"
    }

    $resolvedChangelogVersion = [string]$changelog.version
    if ([string]::IsNullOrWhiteSpace($resolvedChangelogVersion)) {
        throw "Release changelog is missing version: $Path"
    }

    if ($resolvedChangelogVersion.Trim() -ne $ExpectedVersion) {
        throw "Release changelog version mismatch. Expected $ExpectedVersion but found $resolvedChangelogVersion in $Path"
    }

    return $changelog
}

function Convert-PlayerReleaseChangelogToMarkdown {
    param([object]$Changelog)

    $title = [string]$Changelog.title
    if ([string]::IsNullOrWhiteSpace($title)) {
        $title = "FrameAngel Player Release $($Changelog.version)"
    }

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("# $title") | Out-Null
    $lines.Add("") | Out-Null
    $lines.Add(("Version: ``{0}``" -f $Changelog.version)) | Out-Null
    $lines.Add("") | Out-Null

    if (-not [string]::IsNullOrWhiteSpace([string]$Changelog.summary)) {
        $lines.Add("## Summary") | Out-Null
        $lines.Add([string]$Changelog.summary) | Out-Null
        $lines.Add("") | Out-Null
    }

    if (-not [string]::IsNullOrWhiteSpace([string]$Changelog.reasoning)) {
        $lines.Add("## Reasoning") | Out-Null
        $lines.Add([string]$Changelog.reasoning) | Out-Null
        $lines.Add("") | Out-Null
    }

    $lines.Add("## Changes") | Out-Null
    if (@($Changelog.changes).Count -eq 0) {
        $lines.Add("- none") | Out-Null
    }
    else {
        foreach ($change in @($Changelog.changes)) {
            $area = [string]$change.area
            if ([string]::IsNullOrWhiteSpace($area)) {
                $area = "general"
            }

            $summary = [string]$change.summary
            if ([string]::IsNullOrWhiteSpace($summary)) {
                $summary = "No summary provided."
            }

            $lines.Add(("### {0}" -f $area)) | Out-Null
            $lines.Add($summary) | Out-Null

            $changeReasoning = [string]$change.reasoning
            if (-not [string]::IsNullOrWhiteSpace($changeReasoning)) {
                $lines.Add("") | Out-Null
                $lines.Add(("Reasoning: {0}" -f $changeReasoning)) | Out-Null
            }

            $lines.Add("") | Out-Null
        }
    }

    $lines.Add("## Verification") | Out-Null
    if (@($Changelog.verification).Count -eq 0) {
        $lines.Add("- none") | Out-Null
    }
    else {
        foreach ($item in @($Changelog.verification)) {
            $lines.Add("- " + [string]$item) | Out-Null
        }
    }

    $lines.Add("") | Out-Null
    $lines.Add("## Known Issues") | Out-Null
    if (@($Changelog.knownIssues).Count -eq 0) {
        $lines.Add("- none") | Out-Null
    }
    else {
        foreach ($item in @($Changelog.knownIssues)) {
            $lines.Add("- " + [string]$item) | Out-Null
        }
    }

    return ($lines -join [Environment]::NewLine)
}

$laneRoots = Get-FrameAngelPlayerLaneRoots -RepoRoot $RepoRoot -CallerScriptRoot $PSScriptRoot -EnsureAssetLaneScaffold
$RepoRoot = $laneRoots.RepoRoot
$versionState = Read-FrameAngelPlayerVersionState -RepoRoot $RepoRoot

if ($PackageOnlyDeploy.IsPresent) {
    $SkipLiveDeploy = $true
    $BuildVarPackage = $true
}

$effectiveIncludeVarScene = $IncludeVarScene.IsPresent -or $PackageOnlyDeploy.IsPresent
$resolvedVersion = if ([string]::IsNullOrWhiteSpace($Version)) {
    $versionState.Version
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
$varPackagerScript = Join-Path $laneRoots.AssetsPlayerRoot "scripts\Build-CuaPlayerVarPackage.ps1"
$pluginBuildScript = Join-Path $laneRoots.AssetsPlayerRoot "scripts\Build-CuaPlayerResource.ps1"
$assetBuildScript = Join-Path $laneRoots.AssetsPlayerRoot "scripts\Build-PlayerAssetBundle.ps1"
$changelogSourcePath = if (($resolvedVersion -eq $versionState.Version) -and -not [string]::IsNullOrWhiteSpace($versionState.ChangelogPath)) {
    $versionState.ChangelogPath
}
else {
    Resolve-FrameAngelPlayerVersionChangelogPath -RepoRoot $RepoRoot -Version $resolvedVersion
}
$changelog = Read-PlayerReleaseChangelog -Path $changelogSourcePath -ExpectedVersion $resolvedVersion
$releaseChangelogJsonPath = Join-Path $releaseRoot "foundation_release_changelog.json"
$releaseChangelogMarkdownPath = Join-Path $releaseRoot "foundation_release_changelog.md"

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

$releaseChangelog = [ordered]@{
    schemaVersion = "frameangel_player_release_changelog_v1"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    version = $resolvedVersion
    sourcePath = $changelogSourcePath
    title = [string]$changelog.title
    summary = [string]$changelog.summary
    reasoning = [string]$changelog.reasoning
    changes = @($changelog.changes)
    verification = @($changelog.verification)
    knownIssues = @($changelog.knownIssues)
}
Write-JsonFile -Path $releaseChangelogJsonPath -Value $releaseChangelog
Write-TextFile -Path $releaseChangelogMarkdownPath -Value (Convert-PlayerReleaseChangelogToMarkdown -Changelog $releaseChangelog)

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
    "-ChangelogSourcePath",
    $changelogSourcePath,
    "-ChangelogJsonPath",
    $releaseChangelogJsonPath,
    "-ChangelogMarkdownPath",
    $releaseChangelogMarkdownPath,
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
    schemaVersion = "frameangel_player_screen_core_release_v4"
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
        deploysVersionedPlugin = -not $SkipLiveDeploy.IsPresent
        deploysVersionedAssetbundle = -not $SkipLiveDeploy.IsPresent
        removesLegacyPresetDrift = -not $SkipLiveDeploy.IsPresent
        removesAssetSideDllDrift = -not $SkipLiveDeploy.IsPresent
        packageOnlyDeploy = $PackageOnlyDeploy.IsPresent
        buildsVarPackage = $BuildVarPackage.IsPresent
        includesPackagedScene = $effectiveIncludeVarScene
    }
    changelog = [ordered]@{
        sourcePath = $changelogSourcePath
        releaseJsonPath = $releaseChangelogJsonPath
        releaseMarkdownPath = $releaseChangelogMarkdownPath
        title = [string]$releaseChangelog.title
        summary = [string]$releaseChangelog.summary
        reasoning = [string]$releaseChangelog.reasoning
    }
}
Write-JsonFile -Path $manifestPath -Value $manifest
Write-JsonFile -Path $latestManifestPath -Value ([ordered]@{
    schemaVersion = "frameangel_player_screen_core_release_latest_v1"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    version = $resolvedVersion
    releaseRoot = $releaseRoot
    manifestPath = $manifestPath
    changelogJsonPath = $releaseChangelogJsonPath
    changelogMarkdownPath = $releaseChangelogMarkdownPath
    repoAssetPath = $repoAssetPath
    repoPluginPath = $repoPluginPath
    liveAssetPath = if ($SkipLiveDeploy.IsPresent) { "" } else { $liveAssetPath }
    livePluginPath = if ($SkipLiveDeploy.IsPresent) { "" } else { $livePluginPath }
})

$varPackageReport = $null
if ($BuildVarPackage.IsPresent) {
    $varArgs = @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $varPackagerScript,
        "-RepoRoot",
        $RepoRoot,
        "-ReleaseManifestPath",
        $manifestPath,
        "-CreatorName",
        $VarCreatorName,
        "-PackageName",
        $VarPackageName,
        "-PublicRelease",
        $VarPublicRelease,
        "-DestinationAddonPackages",
        $VarDestinationAddonPackages
    )
    if ($effectiveIncludeVarScene) {
        $varArgs += @(
            "-IncludeScene",
            "-SceneTemplatePath",
            $VarSceneTemplatePath,
            "-SceneDisplayPolicy",
            $VarSceneDisplayPolicy,
            "-SceneIncludeManagedControls",
            $VarSceneIncludeManagedControls
        )
        if (-not [string]::IsNullOrWhiteSpace($VarScenePrimaryMediaPath)) {
            $varArgs += @(
                "-ScenePrimaryMediaPath",
                $VarScenePrimaryMediaPath
            )
        }
        if ($IncludeVarDiagnosticsScene.IsPresent) {
            $varArgs += "-IncludeDiagnosticsScene"
        }
        if (-not [string]::IsNullOrWhiteSpace($VarSceneDiagnosticsFilter)) {
            $varArgs += @(
                "-DiagnosticsSceneFilter",
                $VarSceneDiagnosticsFilter
            )
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($VarDemoMediaSourceRoot)) {
        $varArgs += @(
            "-DemoMediaSourceRoot",
            $VarDemoMediaSourceRoot,
            "-DemoMediaPackageRelativeRoot",
            $VarDemoMediaPackageRelativeRoot
        )
    }
    if ($SkipVarDistribute.IsPresent) {
        $varArgs += "-SkipDistribute"
    }

    $varPackageReport = & powershell @varArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Build-CuaPlayerVarPackage.ps1 failed."
    }
}

[pscustomobject]@{
    version = $resolvedVersion
    releaseRoot = $releaseRoot
    repoAssetPath = $repoAssetPath
    repoPluginPath = $repoPluginPath
    liveAssetPath = if ($SkipLiveDeploy.IsPresent) { "" } else { $liveAssetPath }
    livePluginPath = if ($SkipLiveDeploy.IsPresent) { "" } else { $livePluginPath }
    manifestPath = $manifestPath
    changelogJsonPath = $releaseChangelogJsonPath
    changelogMarkdownPath = $releaseChangelogMarkdownPath
    latestManifestPath = $latestManifestPath
    varPackage = $varPackageReport
}
