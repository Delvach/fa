param(
    [string]$RepoRoot = "",
    [string]$Version = "",
    [string]$ReleaseManifestPath = "",
    [string]$CreatorName = "FrameAngel",
    [string]$PackageName = "Player",
    [int]$PublicRelease = 1,
    [string]$OutputRoot = "",
    [switch]$IncludeScene,
    [string]$SceneTemplatePath = "F:\sim\vam\Saves\scene\buttons_setup_scene.json",
    [string]$ScenePrimaryMediaPath = "",
    [ValidateSet("single_display_fit", "multi_aspect")]
    [string]$SceneDisplayPolicy = "multi_aspect",
    [string]$DestinationAddonPackages = "F:\sim\vam\AddonPackages",
    [switch]$SkipDistribute
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$laneResolverPath = Join-Path (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))))) "shared\scripts\player-assets\Resolve-FrameAngelPlayerRoots.ps1"
. $laneResolverPath

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
    $json = $Value | ConvertTo-Json -Depth 100
    [System.IO.File]::WriteAllText($Path, $json, (New-Object System.Text.UTF8Encoding($false)))
}

function Resolve-PathFromBase {
    param(
        [string]$PathValue,
        [string]$BasePath,
        [string]$Label,
        [bool]$MustExist = $true
    )

    if ([string]::IsNullOrWhiteSpace($PathValue)) {
        throw "$Label cannot be empty."
    }

    $candidate = if ([System.IO.Path]::IsPathRooted($PathValue)) {
        $PathValue
    }
    else {
        Join-Path $BasePath $PathValue
    }

    if ($MustExist -and -not (Test-Path -LiteralPath $candidate)) {
        throw "$Label not found: $candidate"
    }

    return [System.IO.Path]::GetFullPath($candidate)
}

function Copy-FileIntoStage {
    param(
        [string]$SourcePath,
        [string]$StageRoot,
        [string]$RelativePath
    )

    $targetPath = Join-Path $StageRoot $RelativePath
    $targetDirectory = Split-Path -Parent $targetPath
    Ensure-Directory -PathValue $targetDirectory
    Copy-Item -LiteralPath $SourcePath -Destination $targetPath -Force
    return $targetPath
}

function Write-ZipArchiveFromStageRoot {
    param(
        [string]$SourceRoot,
        [string]$DestinationPath
    )

    if (Test-Path -LiteralPath $DestinationPath) {
        Remove-Item -LiteralPath $DestinationPath -Force
    }

    $archive = [System.IO.Compression.ZipFile]::Open($DestinationPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem -LiteralPath $SourceRoot -Recurse -File | Sort-Object FullName | ForEach-Object {
            $relative = $_.FullName.Substring($SourceRoot.Length).TrimStart('\', '/')
            $entryName = $relative -replace '\\', '/'
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive,
                $_.FullName,
                $entryName,
                [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Resolve-ReleaseManifest {
    param(
        [pscustomobject]$LaneRoots,
        [string]$ExplicitVersion,
        [string]$ExplicitReleaseManifestPath
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitReleaseManifestPath)) {
        $manifestPath = Resolve-PathFromBase -PathValue $ExplicitReleaseManifestPath -BasePath $LaneRoots.RepoRoot -Label "Release manifest"
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        return [pscustomobject]@{
            Version = [string]$manifest.version
            LatestStatePath = ""
            ManifestPath = $manifestPath
            Manifest = $manifest
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($ExplicitVersion)) {
        $versionValue = $ExplicitVersion.Trim()
        $manifestPath = Join-Path $LaneRoots.AssetsPlayerBuildRoot (Join-Path ("releases\" + $versionValue) "foundation_release_manifest.json")
        if (-not (Test-Path -LiteralPath $manifestPath)) {
            throw ("Release manifest not found for version {0}: {1}" -f $versionValue, $manifestPath)
        }

        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        return [pscustomobject]@{
            Version = $versionValue
            LatestStatePath = ""
            ManifestPath = $manifestPath
            Manifest = $manifest
        }
    }

    $latestStatePath = Join-Path $LaneRoots.AssetsPlayerBuildRoot "releases\player_screen_core_release_latest.json"
    if (-not (Test-Path -LiteralPath $latestStatePath)) {
        throw "Latest release state not found: $latestStatePath"
    }

    $latestState = Get-Content -LiteralPath $latestStatePath -Raw | ConvertFrom-Json
    if ($null -eq $latestState -or [string]::IsNullOrWhiteSpace([string]$latestState.manifestPath)) {
        throw "Latest release state is missing manifestPath: $latestStatePath"
    }

    $manifestPath = Resolve-PathFromBase -PathValue ([string]$latestState.manifestPath) -BasePath $LaneRoots.RepoRoot -Label "Latest release manifest"
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    return [pscustomobject]@{
        Version = [string]$latestState.version
        LatestStatePath = $latestStatePath
        ManifestPath = $manifestPath
        Manifest = $manifest
    }
}

function Resolve-PlayerAssetName {
    param(
        [pscustomobject]$LaneRoots,
        [string]$ReleaseVersion
    )

    $summaryPath = Join-Path $LaneRoots.AssetsPlayerBuildRoot (Join-Path ("assetbundle_exports\" + $ReleaseVersion) "player_screen_summary.json")
    if (Test-Path -LiteralPath $summaryPath) {
        $summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
        if ($null -ne $summary -and -not [string]::IsNullOrWhiteSpace([string]$summary.assetName)) {
            return [string]$summary.assetName
        }
    }

    $receiptPath = Join-Path $LaneRoots.AssetsPlayerBuildRoot (Join-Path ("assetbundle_exports\" + $ReleaseVersion) "player_assetbundle_build_receipt.json")
    if (Test-Path -LiteralPath $receiptPath) {
        $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
        if ($null -ne $receipt.exportSummary -and -not [string]::IsNullOrWhiteSpace([string]$receipt.exportSummary.assetName)) {
            return [string]$receipt.exportSummary.assetName
        }
    }

    return "assets/frameangel/playerscreen/fa_player_screen.prefab"
}

function New-CustomUnityAssetPreset {
    param(
        [string]$AssetUrl,
        [string]$AssetName,
        [string]$PluginUrl = "",
        [string]$PlayerMediaPath = ""
    )

    $plugins = [ordered]@{}
    if (-not [string]::IsNullOrWhiteSpace($PluginUrl)) {
        $plugins["plugin#0"] = $PluginUrl
    }

    $storables = New-Object System.Collections.Generic.List[object]
    [void]$storables.Add([ordered]@{
        id = "PhysicsMaterialControl"
        dynamicFriction = "0.6"
        staticFriction = "0.6"
        bounciness = "0"
        frictionCombine = "Average"
        bounceCombine = "Average"
    })
    [void]$storables.Add([ordered]@{
        id = "CollisionTrigger"
        triggerEnabled = "false"
        invertAtomFilter = "false"
        useRelativeVelocityFilter = "false"
        invertRelativeVelocityFilter = "false"
        relativeVelocityFilter = "1"
        trigger = [ordered]@{
            startActions = @()
            transitionActions = @()
            endActions = @()
        }
    })
    [void]$storables.Add([ordered]@{
        id = "scale"
        scale = "1"
    })
    [void]$storables.Add([ordered]@{
        id = "asset"
        importLightmaps = "true"
        importLightProbes = "true"
        registerCanvases = "false"
        showCanvases = "true"
        loadDll = "true"
        assetName = $AssetName
        assetUrl = $AssetUrl
        assetDllUrl = ""
    })
    [void]$storables.Add([ordered]@{
        id = "PluginManager"
        plugins = $plugins
    })

    if (-not [string]::IsNullOrWhiteSpace($PluginUrl)) {
        [void]$storables.Add([ordered]@{
            id = "plugin#0_FASyncRuntime"
            "Player Media Path" = $PlayerMediaPath
        })
    }

    return [ordered]@{
        setUnlistedParamsToDefault = "true"
        storables = $storables
    }
}

$laneRoots = Get-FrameAngelPlayerLaneRoots -RepoRoot $RepoRoot -CallerScriptRoot $PSScriptRoot -EnsureAssetLaneScaffold
$RepoRoot = $laneRoots.RepoRoot
$sceneBuildScript = Join-Path $laneRoots.AssetsPlayerRoot "scripts\Build-PlayerDemoScene.ps1"

$release = Resolve-ReleaseManifest -LaneRoots $laneRoots -ExplicitVersion $Version -ExplicitReleaseManifestPath $ReleaseManifestPath
$resolvedVersion = $release.Version
$releaseManifestPath = $release.ManifestPath
$releaseManifest = $release.Manifest

if ($null -eq $releaseManifest) {
    throw "Release manifest could not be read: $releaseManifestPath"
}

$assetBundlePath = Resolve-PathFromBase -PathValue ([string]$releaseManifest.repoAssetPath) -BasePath $RepoRoot -Label "Release assetbundle"
$pluginDllPath = Resolve-PathFromBase -PathValue ([string]$releaseManifest.repoPluginPath) -BasePath $RepoRoot -Label "Release plugin DLL"
$assetName = Resolve-PlayerAssetName -LaneRoots $laneRoots -ReleaseVersion $resolvedVersion

$resolvedOutputRoot = if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    Join-Path $laneRoots.AssetsPlayerBuildRoot (Join-Path ("var_packages\" + $resolvedVersion) "direct_cua")
}
else {
    Resolve-PathFromBase -PathValue $OutputRoot -BasePath $RepoRoot -Label "Output root" -MustExist $false
}

$packageFileName = "{0}.{1}.{2}.var" -f $CreatorName, $PackageName, $PublicRelease
$packageZipName = "{0}.{1}.{2}.zip" -f $CreatorName, $PackageName, $PublicRelease
$resourceRoot = $resolvedOutputRoot
$stageRoot = Join-Path $resourceRoot "source"
$packagesRoot = Join-Path $resourceRoot "packages"
$zipPath = Join-Path $packagesRoot $packageZipName
$varPath = Join-Path $packagesRoot $packageFileName
$metaPath = Join-Path $stageRoot "meta.json"
$stageManifestPath = Join-Path $stageRoot "frameangel_player_var_manifest.json"
$reportPath = Join-Path $resourceRoot "player_var_package_report_latest.json"

if (Test-Path -LiteralPath $stageRoot) {
    Remove-Item -LiteralPath $stageRoot -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
if (Test-Path -LiteralPath $varPath) {
    Remove-Item -LiteralPath $varPath -Force
}

Ensure-Directory -PathValue $stageRoot
Ensure-Directory -PathValue $packagesRoot

$assetBundleFileName = [System.IO.Path]::GetFileName($assetBundlePath)
$pluginFileName = [System.IO.Path]::GetFileName($pluginDllPath)
$presetFileName = "Preset_FA Player Asset {0}.vap" -f $resolvedVersion

$assetBundleRelativePath = Join-Path "Custom\Assets\FrameAngel\Player" $assetBundleFileName
$pluginRelativePath = Join-Path "Custom\Scripts" $pluginFileName
$presetRelativePath = Join-Path "Custom\Atom\CustomUnityAsset" $presetFileName
$packagedAssetPath = ($assetBundleRelativePath -replace '\\', '/')
$packagedPluginPath = ($pluginRelativePath -replace '\\', '/')
$packagedPresetPath = ($presetRelativePath -replace '\\', '/')
$packagedAssetUrl = "{0}.{1}.{2}:/{3}" -f $CreatorName, $PackageName, $PublicRelease, $packagedAssetPath
$packagedPluginUrl = "{0}.{1}.{2}:/{3}" -f $CreatorName, $PackageName, $PublicRelease, $packagedPluginPath

[void](Copy-FileIntoStage -SourcePath $assetBundlePath -StageRoot $stageRoot -RelativePath $assetBundleRelativePath)
[void](Copy-FileIntoStage -SourcePath $pluginDllPath -StageRoot $stageRoot -RelativePath $pluginRelativePath)

$presetObject = New-CustomUnityAssetPreset -AssetUrl $packagedAssetUrl -AssetName $assetName -PluginUrl $packagedPluginUrl -PlayerMediaPath ""
$presetStagePath = Join-Path $stageRoot $presetRelativePath
Write-JsonFile -Path $presetStagePath -Value $presetObject

$generatedScenePath = ""
$generatedScenePreviewPath = ""
$packagedScenePath = ""
$packagedScenePreviewPath = ""
if ($IncludeScene.IsPresent) {
    if (-not (Test-Path -LiteralPath $sceneBuildScript)) {
        throw "Scene build script not found: $sceneBuildScript"
    }

    $sceneSourceOutputRoot = Join-Path $resourceRoot "scene_source"
    if (Test-Path -LiteralPath $sceneSourceOutputRoot) {
        Remove-Item -LiteralPath $sceneSourceOutputRoot -Recurse -Force
    }

    $sceneArgs = @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $sceneBuildScript,
        "-RepoRoot",
        $RepoRoot,
        "-Version",
        $resolvedVersion,
        "-SceneTemplatePath",
        $SceneTemplatePath,
        "-OutputDirectory",
        $sceneSourceOutputRoot,
        "-AssetUrl",
        $packagedAssetUrl,
        "-PluginPath",
        $packagedPluginUrl,
        "-DisplayPolicy",
        $SceneDisplayPolicy,
        "-AllowExistingVersion"
    )
    if (-not [string]::IsNullOrWhiteSpace($ScenePrimaryMediaPath)) {
        $sceneArgs += @(
            "-PrimaryMediaPath",
            $ScenePrimaryMediaPath
        )
    }

    & powershell @sceneArgs | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Build-PlayerDemoScene.ps1 failed while staging the packaged scene."
    }

    $generatedScenePath = Join-Path $sceneSourceOutputRoot ("fa_scene.{0}.json" -f $resolvedVersion)
    if (-not (Test-Path -LiteralPath $generatedScenePath)) {
        throw "Packaged scene output not found: $generatedScenePath"
    }

    $generatedScenePreviewPath = [System.IO.Path]::ChangeExtension($generatedScenePath, ".jpg")
    $sceneRelativePath = Join-Path "Saves\scene\FrameAngel\Player" ("fa_player_demo_scene.{0}.json" -f $resolvedVersion)
    $packagedScenePath = ($sceneRelativePath -replace '\\', '/')
    [void](Copy-FileIntoStage -SourcePath $generatedScenePath -StageRoot $stageRoot -RelativePath $sceneRelativePath)

    if (Test-Path -LiteralPath $generatedScenePreviewPath) {
        $scenePreviewRelativePath = [System.IO.Path]::ChangeExtension($sceneRelativePath, ".jpg")
        $packagedScenePreviewPath = ($scenePreviewRelativePath -replace '\\', '/')
        [void](Copy-FileIntoStage -SourcePath $generatedScenePreviewPath -StageRoot $stageRoot -RelativePath $scenePreviewRelativePath)
    }
}

$meta = [ordered]@{
    licenseType = "FC"
    creatorName = $CreatorName
    packageName = $PackageName
    description = "FrameAngel player direct CUA package"
}
Write-JsonFile -Path $metaPath -Value $meta

$stagedFiles = New-Object System.Collections.Generic.List[object]
[void]$stagedFiles.Add([ordered]@{
    kind = "assetbundle"
    sourcePath = $assetBundlePath
    packagedPath = $packagedAssetPath
})
[void]$stagedFiles.Add([ordered]@{
    kind = "plugin_dll"
    sourcePath = $pluginDllPath
    packagedPath = $packagedPluginPath
})
[void]$stagedFiles.Add([ordered]@{
    kind = "custom_unity_asset_preset"
    sourcePath = ""
    packagedPath = $packagedPresetPath
})
if (-not [string]::IsNullOrWhiteSpace($packagedScenePath)) {
    [void]$stagedFiles.Add([ordered]@{
        kind = "scene_json"
        sourcePath = $generatedScenePath
        packagedPath = $packagedScenePath
    })
}
if (-not [string]::IsNullOrWhiteSpace($packagedScenePreviewPath)) {
    [void]$stagedFiles.Add([ordered]@{
        kind = "scene_preview"
        sourcePath = $generatedScenePreviewPath
        packagedPath = $packagedScenePreviewPath
    })
}
[void]$stagedFiles.Add([ordered]@{
    kind = "meta_json"
    sourcePath = ""
    packagedPath = "meta.json"
})
[void]$stagedFiles.Add([ordered]@{
    kind = "frameangel_player_var_manifest"
    sourcePath = ""
    packagedPath = "frameangel_player_var_manifest.json"
})

$stageManifest = [ordered]@{
    schemaVersion = "frameangel_player_var_manifest_v1"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    repoRoot = $RepoRoot
    packageMode = "direct_cua"
    creatorName = $CreatorName
    packageName = $PackageName
    publicRelease = $PublicRelease
    packageFileName = $packageFileName
    playerVersion = $resolvedVersion
    releaseManifestPath = $releaseManifestPath
    authoritySeam = if ($IncludeScene.IsPresent) { "phase_2_package_first_direct_cua_with_packaged_scene" } else { "phase_2_direct_cua_with_packaged_plugin_attach" }
    note = if ($IncludeScene.IsPresent) {
        "This package stages the validated player assetbundle, the matching player plugin under Custom/Scripts, a package-contained CustomUnityAsset preset that targets the packaged asset and plugin URLs, and a packaged scene that references those same in-package resources."
    }
    else {
        "This package stages the validated player assetbundle, the matching player plugin under Custom/Scripts, and a package-contained CustomUnityAsset preset that targets the packaged asset and plugin URLs."
    }
    directCua = [ordered]@{
        assetBundlePath = $assetBundlePath
        assetBundleAssetName = $assetName
        pluginDllPath = $pluginDllPath
        presetFileName = $presetFileName
        packagedAssetUrl = $packagedAssetUrl
        packagedPluginUrl = $packagedPluginUrl
        packagedPluginPath = $packagedPluginPath
    }
    packagedScene = if ($IncludeScene.IsPresent) {
        [ordered]@{
            sceneTemplatePath = $SceneTemplatePath
            scenePrimaryMediaPath = $ScenePrimaryMediaPath
            sceneDisplayPolicy = $SceneDisplayPolicy
            sourceScenePath = $generatedScenePath
            sourceScenePreviewPath = if (Test-Path -LiteralPath $generatedScenePreviewPath) { $generatedScenePreviewPath } else { "" }
            packagedScenePath = $packagedScenePath
            packagedScenePreviewPath = $packagedScenePreviewPath
        }
    }
    else {
        $null
    }
    stagedFiles = $stagedFiles
}
Write-JsonFile -Path $stageManifestPath -Value $stageManifest

Write-ZipArchiveFromStageRoot -SourceRoot $stageRoot -DestinationPath $zipPath
Move-Item -LiteralPath $zipPath -Destination $varPath -Force

$distribution = [ordered]@{
    distributed = $false
    destinationAddonPackages = ""
    distributedPackagePath = ""
}

if (-not $SkipDistribute.IsPresent) {
    if (-not (Test-Path -LiteralPath $DestinationAddonPackages)) {
        throw "AddonPackages destination not found: $DestinationAddonPackages"
    }

    $distributedPackagePath = Join-Path $DestinationAddonPackages $packageFileName
    Copy-Item -LiteralPath $varPath -Destination $distributedPackagePath -Force
    $distribution = [ordered]@{
        distributed = $true
        destinationAddonPackages = $DestinationAddonPackages
        distributedPackagePath = $distributedPackagePath
    }
}

$report = [ordered]@{
    schemaVersion = "frameangel_player_var_package_report_v1"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    repoRoot = $RepoRoot
    version = $resolvedVersion
    creatorName = $CreatorName
    packageName = $PackageName
    publicRelease = $PublicRelease
    packageMode = "direct_cua"
    packageFileName = $packageFileName
    packagePath = $varPath
    stageRoot = $stageRoot
    sourceMetaPath = $metaPath
    sourceManifestPath = $stageManifestPath
    releaseManifestPath = $releaseManifestPath
    assetBundlePath = $assetBundlePath
    pluginDllPath = $pluginDllPath
    presetStagePath = $presetStagePath
    assetBundleAssetName = $assetName
    packagedAssetUrl = $packagedAssetUrl
    packagedPluginUrl = $packagedPluginUrl
    packagedScenePath = $packagedScenePath
    packagedScenePreviewPath = $packagedScenePreviewPath
    distribution = $distribution
}
Write-JsonFile -Path $reportPath -Value $report

[pscustomobject]@{
    version = $resolvedVersion
    creatorName = $CreatorName
    packageName = $PackageName
    publicRelease = $PublicRelease
    packageMode = "direct_cua"
    packagePath = $varPath
    reportPath = $reportPath
    distributed = $distribution.distributed
    distributedPackagePath = $distribution.distributedPackagePath
    packagedAssetUrl = $packagedAssetUrl
    packagedPluginUrl = $packagedPluginUrl
    packagedScenePath = $packagedScenePath
    presetStagePath = $presetStagePath
}
