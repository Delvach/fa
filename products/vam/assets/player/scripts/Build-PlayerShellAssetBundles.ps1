param(
    [string]$RepoRoot = "",
    [string]$UnityEditorPath = "C:\Program Files\Unity\Hub\Editor\2018.1.9f2\Editor\Unity.exe",
    [string]$ShellExportSummaryPath = "",
    [string[]]$ShellKeys = @(),
    [string]$OutputRoot = "",
    [string]$DeployAssetsRoot = "F:\sim\vam\Custom\Assets\FrameAngel\Player",
    [string]$DeployPresetRoot = "F:\sim\vam\Custom\Atom\CustomUnityAsset",
    [string]$DeployPluginsRoot = "F:\sim\vam\Custom\Plugins",
    [string]$PlayerMediaPath = "",
    [switch]$SkipDeploy
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

function Normalize-ShellKeys {
    param([string[]]$RequestedShellKeys)

    $normalized = New-Object System.Collections.Generic.List[string]
    foreach ($rawValue in @($RequestedShellKeys)) {
        if ([string]::IsNullOrWhiteSpace($rawValue)) {
            continue
        }

        foreach ($token in ($rawValue -split ",")) {
            $trimmed = $token.Trim()
            if (-not [string]::IsNullOrWhiteSpace($trimmed)) {
                [void]$normalized.Add($trimmed)
            }
        }
    }

    return $normalized.ToArray()
}

function Resolve-DirectShellSpec {
    param([string]$ShellKey)

    switch ($ShellKey.ToLowerInvariant()) {
        "player_host" {
            return [pscustomobject]@{
                ShellKey = "player_host"
                ResourceId = "fa_cua_player_host"
                DisplayName = "FA CUA Player Host"
                BundleFileName = "fa_cua_player_host.assetbundle"
                PresetFileName = "Preset_FA CUA Player Host Raw.vap"
                LegacyPresetFileNames = @("Preset_FA CUA Player Host.vap")
            }
        }
        "mcbrooke_laptop" {
            return [pscustomobject]@{
                ShellKey = "mcbrooke_laptop"
                ResourceId = "fa_cua_player_laptop"
                DisplayName = "FA CUA Player Laptop"
                BundleFileName = "fa_cua_player_laptop.assetbundle"
                PresetFileName = "Preset_FA CUA Player Laptop Raw.vap"
                LegacyPresetFileNames = @("Preset_FA CUA Player Laptop.vap")
            }
        }
        "ivone_phone" {
            return [pscustomobject]@{
                ShellKey = "ivone_phone"
                ResourceId = "fa_cua_player_phone"
                DisplayName = "FA CUA Player Phone"
                BundleFileName = "fa_cua_player_phone.assetbundle"
                PresetFileName = "Preset_FA CUA Player Phone Raw.vap"
                LegacyPresetFileNames = @("Preset_FA CUA Player Phone.vap")
            }
        }
        "ivad_tablet" {
            return [pscustomobject]@{
                ShellKey = "ivad_tablet"
                ResourceId = "fa_cua_player_tablet"
                DisplayName = "FA CUA Player Tablet"
                BundleFileName = "fa_cua_player_tablet.assetbundle"
                PresetFileName = "Preset_FA CUA Player Tablet Raw.vap"
                LegacyPresetFileNames = @("Preset_FA CUA Player Tablet.vap")
            }
        }
        "modern_tv" {
            return [pscustomobject]@{
                ShellKey = "modern_tv"
                ResourceId = "fa_cua_player_modern_tv"
                DisplayName = "FA CUA Player Modern TV"
                BundleFileName = "fa_cua_player_modern_tv.assetbundle"
                PresetFileName = "Preset_FA CUA Player Modern TV Raw.vap"
                LegacyPresetFileNames = @("Preset_FA CUA Player Modern TV.vap")
            }
        }
        "retro_tv" {
            return [pscustomobject]@{
                ShellKey = "retro_tv"
                ResourceId = "fa_cua_player_retro_tv"
                DisplayName = "FA CUA Player Retro TV"
                BundleFileName = "fa_cua_player_retro_tv.assetbundle"
                PresetFileName = "Preset_FA CUA Player Retro TV Raw.vap"
                LegacyPresetFileNames = @("Preset_FA CUA Player Retro TV.vap")
            }
        }
        default {
            throw "Unknown shell key: $ShellKey"
        }
    }
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

function Resolve-LatestReleaseState {
    param([pscustomobject]$LaneRoots)

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
        ManifestPath = $manifestPath
        Manifest = $manifest
    }
}

$laneRoots = Get-FrameAngelPlayerLaneRoots -RepoRoot $RepoRoot -CallerScriptRoot $PSScriptRoot -EnsureAssetLaneScaffold
$RepoRoot = $laneRoots.RepoRoot
$release = Resolve-LatestReleaseState -LaneRoots $laneRoots

$resolvedShellSummaryPath = if ([string]::IsNullOrWhiteSpace($ShellExportSummaryPath)) {
    Join-Path $laneRoots.AssetsPlayerBuildRoot "host_shell_exports\ghost_player_host_shell_export_summary.json"
}
else {
    Resolve-PathFromBase -PathValue $ShellExportSummaryPath -BasePath $RepoRoot -Label "Shell export summary"
}

$resolvedOutputRoot = if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    Join-Path $laneRoots.AssetsPlayerBuildRoot "shell_assetbundle_exports_2018"
}
else {
    Resolve-PathFromBase -PathValue $OutputRoot -BasePath $RepoRoot -Label "Shell direct output root" -MustExist $false
}

if (-not (Test-Path -LiteralPath $UnityEditorPath)) {
    throw "Unity editor not found: $UnityEditorPath"
}

$unityProjectPath = $laneRoots.PlayerScreenUnityProjectRoot
if (-not (Test-Path -LiteralPath $unityProjectPath)) {
    throw "Unity project not found: $unityProjectPath"
}

$pluginDllPath = Resolve-PathFromBase -PathValue ([string]$release.Manifest.repoPluginPath) -BasePath $RepoRoot -Label "Latest release plugin DLL"
$pluginFileName = [System.IO.Path]::GetFileName($pluginDllPath)
$pluginUrl = "Custom/Plugins/{0}" -f $pluginFileName

$summary = Get-Content -LiteralPath $resolvedShellSummaryPath -Raw | ConvertFrom-Json
$shellProfiles = @($summary.shells)
if (@($shellProfiles).Count -le 0) {
    throw "Shell export summary does not contain shells: $resolvedShellSummaryPath"
}

$requestedShellKeys = Normalize-ShellKeys -RequestedShellKeys $ShellKeys
if (@($requestedShellKeys).Count -le 0) {
    $requestedShellKeys = @("mcbrooke_laptop", "ivone_phone", "ivad_tablet", "modern_tv", "retro_tv")
}

Ensure-Directory -PathValue $resolvedOutputRoot
if (-not $SkipDeploy.IsPresent) {
    Ensure-Directory -PathValue $DeployAssetsRoot
    Ensure-Directory -PathValue $DeployPresetRoot
    Ensure-Directory -PathValue $DeployPluginsRoot
    Copy-Item -LiteralPath $pluginDllPath -Destination (Join-Path $DeployPluginsRoot $pluginFileName) -Force
}

$results = New-Object System.Collections.Generic.List[object]

foreach ($shellKey in @($requestedShellKeys)) {
    $shellProfile = @($shellProfiles | Where-Object { [string]$_.shellKey -eq $shellKey }) | Select-Object -First 1
    if ($null -eq $shellProfile) {
        throw "Shell profile was not found for key '$shellKey' in $resolvedShellSummaryPath"
    }

    $spec = Resolve-DirectShellSpec -ShellKey $shellKey
    $packageRoot = Resolve-PathFromBase -PathValue ([string]$shellProfile.packageRootPath) -BasePath $RepoRoot -Label ("Package root for " + $shellKey)
    $hostProfilePath = Join-Path $packageRoot "host_profile.json"
    if (-not (Test-Path -LiteralPath $hostProfilePath)) {
        throw ("Host profile not found for shell '{0}': {1}" -f $shellKey, $hostProfilePath)
    }

    $shellOutputRoot = Join-Path $resolvedOutputRoot $shellKey
    if (Test-Path -LiteralPath $shellOutputRoot) {
        Remove-Item -LiteralPath $shellOutputRoot -Recurse -Force
    }
    Ensure-Directory -PathValue $shellOutputRoot

    $unityLogPath = Join-Path $shellOutputRoot "unity_batch.log"
    $unityArgs = @(
        "-batchmode",
        "-quit",
        "-projectPath", $unityProjectPath,
        "-logFile", $unityLogPath,
        "-executeMethod", "FrameAngelPlayerShell2018Exporter.BuildAndDeployBatch",
        "-faOutputRoot", $shellOutputRoot,
        "-faPackageRoot", $packageRoot,
        "-faHostProfilePath", $hostProfilePath,
        "-faBundleFileName", $spec.BundleFileName,
        "-faResourceId", $spec.ResourceId,
        "-faShellKey", $spec.ShellKey
    )

    $process = Start-Process -FilePath $UnityEditorPath -ArgumentList $unityArgs -PassThru -Wait
    if ($process.ExitCode -ne 0) {
        $logTail = if (Test-Path -LiteralPath $unityLogPath) { (Get-Content -LiteralPath $unityLogPath -Tail 120) -join [Environment]::NewLine } else { "" }
        if ([string]::IsNullOrWhiteSpace($logTail)) {
            throw "Unity shell export failed for $shellKey with exit code $($process.ExitCode)."
        }

        throw ("Unity shell export failed for {0}. Log tail:`n{1}" -f $shellKey, $logTail)
    }

    $shellSummaryPath = Join-Path $shellOutputRoot "player_shell_summary.json"
    if (-not (Test-Path -LiteralPath $shellSummaryPath)) {
        throw ("Shell build summary not found for {0}: {1}" -f $shellKey, $shellSummaryPath)
    }

    $shellBuildSummary = Get-Content -LiteralPath $shellSummaryPath -Raw | ConvertFrom-Json
    $builtBundlePath = Resolve-PathFromBase -PathValue ([string]$shellBuildSummary.bundlePath) -BasePath $RepoRoot -Label ("Built shell bundle for " + $shellKey)

    $presetPath = Join-Path $shellOutputRoot $spec.PresetFileName
    $presetObject = New-CustomUnityAssetPreset `
        -AssetUrl ("Custom/Assets/FrameAngel/Player/{0}" -f $spec.BundleFileName) `
        -AssetName ([string]$shellBuildSummary.assetName) `
        -PluginUrl $pluginUrl `
        -PlayerMediaPath $PlayerMediaPath
    Write-JsonFile -Path $presetPath -Value $presetObject

    $deployedBundlePath = ""
    $deployedPresetPath = ""
    if (-not $SkipDeploy.IsPresent) {
        $deployedBundlePath = Join-Path $DeployAssetsRoot $spec.BundleFileName
        $deployedPresetPath = Join-Path $DeployPresetRoot $spec.PresetFileName
        Copy-Item -LiteralPath $builtBundlePath -Destination $deployedBundlePath -Force

        foreach ($legacyPresetFileName in @($spec.LegacyPresetFileNames)) {
            if ([string]::IsNullOrWhiteSpace($legacyPresetFileName)) {
                continue
            }

            $legacyPresetPath = Join-Path $DeployPresetRoot $legacyPresetFileName
            if (Test-Path -LiteralPath $legacyPresetPath) {
                Remove-Item -LiteralPath $legacyPresetPath -Force
            }
        }

        Write-JsonFile -Path $deployedPresetPath -Value $presetObject
    }

    [void]$results.Add([ordered]@{
        shellKey = $shellKey
        displayName = $spec.DisplayName
        resourceId = $spec.ResourceId
        packageRoot = $packageRoot
        unityLogPath = $unityLogPath
        summaryPath = $shellSummaryPath
        builtBundlePath = $builtBundlePath
        builtAssetName = [string]$shellBuildSummary.assetName
        presetPath = $presetPath
        deployedBundlePath = $deployedBundlePath
        deployedPresetPath = $deployedPresetPath
        pluginUrl = $pluginUrl
    })
}

$reportPath = Join-Path $resolvedOutputRoot "player_shell_assetbundle_report_latest.json"
$report = [ordered]@{
    schemaVersion = "frameangel_player_shell_assetbundle_report_v1"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    version = $release.Version
    unityEditorPath = $UnityEditorPath
    unityProjectPath = $unityProjectPath
    shellExportSummaryPath = $resolvedShellSummaryPath
    outputRoot = $resolvedOutputRoot
    deployed = (-not $SkipDeploy.IsPresent)
    deployAssetsRoot = if ($SkipDeploy.IsPresent) { "" } else { $DeployAssetsRoot }
    deployPresetRoot = if ($SkipDeploy.IsPresent) { "" } else { $DeployPresetRoot }
    deployPluginsRoot = if ($SkipDeploy.IsPresent) { "" } else { $DeployPluginsRoot }
    pluginDllPath = $pluginDllPath
    pluginUrl = $pluginUrl
    shells = $results.ToArray()
}
Write-JsonFile -Path $reportPath -Value $report

[pscustomobject]@{
    version = $release.Version
    outputRoot = $resolvedOutputRoot
    reportPath = $reportPath
    shellCount = $results.Count
    deployed = (-not $SkipDeploy.IsPresent)
}
