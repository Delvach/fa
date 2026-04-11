param(
    [string]$RepoRoot = "",
    [string]$Version = "",
    [string]$ReleaseManifestPath = "",
    [string]$CreatorName = "FrameAngel",
    [string]$PackageName = "Player",
    [int]$PublicRelease = 1,
    [string]$OutputRoot = "",
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
        [string]$AssetName
    )

    return [ordered]@{
        setUnlistedParamsToDefault = "true"
        storables = @(
            [ordered]@{
                id = "PhysicsMaterialControl"
                dynamicFriction = "0.6"
                staticFriction = "0.6"
                bounciness = "0"
                frictionCombine = "Average"
                bounceCombine = "Average"
            },
            [ordered]@{
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
            },
            [ordered]@{
                id = "scale"
                scale = "1"
            },
            [ordered]@{
                id = "asset"
                importLightmaps = "true"
                importLightProbes = "true"
                registerCanvases = "false"
                showCanvases = "true"
                loadDll = "true"
                assetName = $AssetName
                assetUrl = $AssetUrl
                assetDllUrl = ""
            },
            [ordered]@{
                id = "PluginManager"
                plugins = [ordered]@{}
            }
        )
    }
}

$laneRoots = Get-FrameAngelPlayerLaneRoots -RepoRoot $RepoRoot -CallerScriptRoot $PSScriptRoot -EnsureAssetLaneScaffold
$RepoRoot = $laneRoots.RepoRoot

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

[void](Copy-FileIntoStage -SourcePath $assetBundlePath -StageRoot $stageRoot -RelativePath $assetBundleRelativePath)
[void](Copy-FileIntoStage -SourcePath $pluginDllPath -StageRoot $stageRoot -RelativePath $pluginRelativePath)

$presetObject = New-CustomUnityAssetPreset -AssetUrl $packagedAssetUrl -AssetName $assetName
$presetStagePath = Join-Path $stageRoot $presetRelativePath
Write-JsonFile -Path $presetStagePath -Value $presetObject

$meta = [ordered]@{
    licenseType = "FC"
    creatorName = $CreatorName
    packageName = $PackageName
    description = "FrameAngel player direct CUA package"
}
Write-JsonFile -Path $metaPath -Value $meta

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
    authoritySeam = "phase_1_direct_cua_plus_manual_packaged_plugin_attach"
    note = "This package stages the validated player assetbundle, the matching player plugin under Custom/Scripts, and a package-contained CustomUnityAsset preset that targets the packaged asset URL."
    directCua = [ordered]@{
        assetBundlePath = $assetBundlePath
        assetBundleAssetName = $assetName
        pluginDllPath = $pluginDllPath
        presetFileName = $presetFileName
        packagedAssetUrl = $packagedAssetUrl
        packagedPluginPath = $packagedPluginPath
    }
    stagedFiles = @(
        [ordered]@{
            kind = "assetbundle"
            sourcePath = $assetBundlePath
            packagedPath = $packagedAssetPath
        },
        [ordered]@{
            kind = "plugin_dll"
            sourcePath = $pluginDllPath
            packagedPath = $packagedPluginPath
        },
        [ordered]@{
            kind = "custom_unity_asset_preset"
            sourcePath = ""
            packagedPath = $packagedPresetPath
        },
        [ordered]@{
            kind = "meta_json"
            sourcePath = ""
            packagedPath = "meta.json"
        },
        [ordered]@{
            kind = "frameangel_player_var_manifest"
            sourcePath = ""
            packagedPath = "frameangel_player_var_manifest.json"
        }
    )
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
    presetStagePath = $presetStagePath
}
