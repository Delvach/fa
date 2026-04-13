param(
    [string]$RepoRoot = "",
    [string]$Version = "",
    [string]$ShellKey = "modern_tv",
    [string]$HostCatalogSummaryPath = "",
    [string]$UnityEditorPath = "",
    [int]$ThemeIndex = -1,
    [switch]$RefreshPacket15Foundation,
    [string]$OutputRoot = "",
    [string]$DeployAssetsRoot = "F:\sim\vam\Custom\Assets\FrameAngel\Player",
    [string]$DeployPluginRoot = "F:\sim\vam\Custom\Plugins",
    [string]$DeployPresetRoot = "F:\sim\vam\Custom\Atom\CustomUnityAsset",
    [ValidateSet("raw", "packaged")]
    [string]$PlayerPluginMode = "raw",
    [string]$PrimaryMediaPath = ""
)

Set-StrictMode -Version Latest
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
    Set-Content -LiteralPath $Path -Value $json -Encoding utf8
}

function Resolve-UnityEditorPathForProject {
    param(
        [string]$ProjectPath,
        [string]$RequestedUnityEditorPath
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedUnityEditorPath)) {
        return $RequestedUnityEditorPath
    }

    $projectVersionPath = Join-Path $ProjectPath "ProjectSettings\ProjectVersion.txt"
    if (-not (Test-Path -LiteralPath $projectVersionPath)) {
        throw "Unity project version file not found: $projectVersionPath"
    }

    $projectVersionLine = Get-Content -LiteralPath $projectVersionPath | Where-Object { $_ -match '^m_EditorVersion:\s+' } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($projectVersionLine)) {
        throw "Could not resolve Unity editor version from: $projectVersionPath"
    }

    $projectVersion = ($projectVersionLine -replace '^m_EditorVersion:\s+', '').Trim()
    if ([string]::IsNullOrWhiteSpace($projectVersion)) {
        throw "Unity project editor version was blank in: $projectVersionPath"
    }

    $candidate = Join-Path (Join-Path "C:\Program Files\Unity\Hub\Editor" $projectVersion) "Editor\Unity.exe"
    if (-not (Test-Path -LiteralPath $candidate)) {
        throw "Unity editor $projectVersion required by $ProjectPath was not found at: $candidate"
    }

    return $candidate
}

function Read-JsonFile {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "JSON file not found: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Resolve-ThemeIndex {
    param(
        [int]$ExplicitThemeIndex,
        [string]$DefaultsPath
    )

    if ($ExplicitThemeIndex -ge 0) {
        return $ExplicitThemeIndex
    }

    if (-not (Test-Path -LiteralPath $DefaultsPath)) {
        return 0
    }

    $defaults = Read-JsonFile -Path $DefaultsPath
    if ($null -ne $defaults -and $null -ne $defaults.themeIndex) {
        return [int]$defaults.themeIndex
    }

    return 0
}

function Resolve-HostCatalogSummaryPath {
    param(
        [string]$RequestedPath,
        [string]$RepoRootValue,
        [int]$ResolvedThemeIndex
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        return $RequestedPath
    }

    return Join-Path $RepoRootValue ("products\vam\assets\player\build\host_catalog\theme_{0:D2}\ghost_player_host_catalog_summary.json" -f $ResolvedThemeIndex)
}

function Resolve-InteractivePresetFileName {
    param([string]$HostDisplayName)

    $baseName = if ([string]::IsNullOrWhiteSpace($HostDisplayName)) {
        "FA Meta Interactive Player Host"
    }
    else {
        $HostDisplayName.Trim()
    }

    $baseName = [regex]::Replace($baseName, '[<>:"/\\|?*]', "_")
    $baseName = $baseName.TrimEnd('.', ' ')
    if ([string]::IsNullOrWhiteSpace($baseName)) {
        $baseName = "FA Meta Interactive Player Host"
    }

    return ("Preset_{0} Interactive Proof.vap" -f $baseName)
}

function Resolve-PlayerPluginBinding {
    param(
        [string]$PluginMode,
        [string]$ResolvedVersionValue,
        [string]$PackagedPluginUrl,
        [string]$ReleasePluginPath,
        [string]$DeployPluginDirectory
    )

    $resolvedMode = if ([string]::IsNullOrWhiteSpace($PluginMode)) { "raw" } else { $PluginMode.Trim().ToLowerInvariant() }
    if ($resolvedMode -eq "packaged") {
        if ([string]::IsNullOrWhiteSpace($PackagedPluginUrl)) {
            throw "Packaged player plugin url was blank."
        }

        return [pscustomobject]@{
            mode = "packaged"
            pluginUrl = $PackagedPluginUrl
            deployedPluginPath = ""
            sourcePluginPath = ""
        }
    }

    if (-not (Test-Path -LiteralPath $ReleasePluginPath)) {
        throw "Raw player plugin release artifact not found: $ReleasePluginPath"
    }

    Ensure-Directory -PathValue $DeployPluginDirectory
    $pluginFileName = Split-Path -Path $ReleasePluginPath -Leaf
    $deployedPluginPath = Join-Path $DeployPluginDirectory $pluginFileName
    Copy-Item -LiteralPath $ReleasePluginPath -Destination $deployedPluginPath -Force

    return [pscustomobject]@{
        mode = "raw"
        pluginUrl = "Custom/Plugins/" + $pluginFileName
        deployedPluginPath = $deployedPluginPath
        sourcePluginPath = $ReleasePluginPath
    }
}

function Build-InteractiveHostedPlayerPreset {
    param(
        [string]$AssetUrl,
        [string]$AssetName,
        [string]$PluginUrl,
        [string]$ResolvedPrimaryMediaPath
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
                plugins = [ordered]@{
                    "plugin#0" = $PluginUrl
                }
            },
            [ordered]@{
                id = "plugin#0_FASyncRuntime"
                "Player Media Path" = $ResolvedPrimaryMediaPath
            }
        )
    }
}

function Write-MarkdownReceipt {
    param(
        [string]$Path,
        [object]$Receipt
    )

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("# Meta Interactive Hosted Player Proof")
    $lines.Add("")
    $lines.Add("- Generated At (UTC): $($Receipt.generatedAtUtc)")
    $lines.Add("- Shell Key: $($Receipt.shellKey)")
    $lines.Add("- Host Display Name: $($Receipt.hostDisplayName)")
    $lines.Add("- Host Package Root: $($Receipt.hostPackageRoot)")
    $lines.Add("- Raw Shell Report Path: $($Receipt.rawShellReportPath)")
    $lines.Add("- Host Shell Export Summary Path: $($Receipt.hostShellExportSummaryPath)")
    $lines.Add("- Host Shell Preview Path: $($Receipt.hostShellPreviewPath)")
    $lines.Add("- Host Resource Id: $($Receipt.hostResourceId)")
    $lines.Add("- Host Bundle Path: $($Receipt.hostBundlePath)")
    $lines.Add("- Deployed Host Bundle Path: $($Receipt.deployedHostBundlePath)")
    $lines.Add("- Interactive Preset Path: $($Receipt.deployedInteractivePresetPath)")
    $lines.Add("- Player Version: $($Receipt.playerVersion)")
    $lines.Add("- Player Package File: $($Receipt.playerPackageFileName)")
    $lines.Add("- Player Plugin Mode: $($Receipt.playerPluginMode)")
    $lines.Add("- Player Plugin Url: $($Receipt.playerPluginUrl)")
    $lines.Add("- Player Plugin Source Path: $($Receipt.playerPluginSourcePath)")
    $lines.Add("- Deployed Player Plugin Path: $($Receipt.deployedPlayerPluginPath)")
    $lines.Add("- Player Asset Url: $($Receipt.playerAssetUrl)")
    $lines.Add("- Host Catalog Summary Path: $($Receipt.hostCatalogSummaryPath)")
    $lines.Add("- Packet 1.5 Receipt Path: $($Receipt.packet15ReceiptPath)")
    $lines.Add("- Unity Log Path: $($Receipt.unityLogPath)")
    $lines.Add("")
    $lines.Add("## Current proof boundary")
    $lines.Add("")
    $lines.Add("1. This artifact is player-backed and no longer ships an empty PluginManager.")
    $lines.Add("2. Current confidence is contract/build confidence, not live Halo-backed proof, because Halo is offline.")
    $lines.Add("3. Use Volodeck + live VaM session proof before promoting this beyond interactive proof status.")
    Set-Content -LiteralPath $Path -Value ($lines -join [Environment]::NewLine) -Encoding utf8
}

$laneRoots = Get-FrameAngelPlayerLaneRoots -RepoRoot $RepoRoot -CallerScriptRoot $PSScriptRoot -EnsureAssetLaneScaffold
$resolvedRepoRoot = $laneRoots.RepoRoot
$resolvedVersion = if ([string]::IsNullOrWhiteSpace($Version)) {
    (Read-FrameAngelPlayerVersionState -RepoRoot $resolvedRepoRoot).Version
}
else {
    $Version.Trim()
}

$defaultsPath = Join-Path $laneRoots.AssetsPlayerRoot "config\meta_ui_packet_1_5.defaults.json"
$resolvedThemeIndex = Resolve-ThemeIndex -ExplicitThemeIndex $ThemeIndex -DefaultsPath $defaultsPath
$resolvedHostCatalogSummaryPath = Resolve-HostCatalogSummaryPath -RequestedPath $HostCatalogSummaryPath -RepoRootValue $resolvedRepoRoot -ResolvedThemeIndex $resolvedThemeIndex
$resolvedOutputRoot = if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    Join-Path $laneRoots.AssetsPlayerBuildRoot (Join-Path "meta_interactive_host_proof" $ShellKey)
}
else {
    $OutputRoot
}

$packet15ReceiptPath = ""
if ($RefreshPacket15Foundation.IsPresent -or -not (Test-Path -LiteralPath $resolvedHostCatalogSummaryPath)) {
    $packet15ScriptPath = Join-Path $PSScriptRoot "Build-PlayerMetaUiPacket15Foundation.ps1"
    $packet15Result = & $packet15ScriptPath `
        -RepoRoot $resolvedRepoRoot `
        -ThemeIndex $resolvedThemeIndex `
        -ShellKeys $ShellKey `
        -NoPreview
    $packet15ReceiptPath = [string]$packet15Result.receiptPath
    $resolvedHostCatalogSummaryPath = [string]$packet15Result.hostCatalogSummaryPath
}

$hostCatalogSummary = Read-JsonFile -Path $resolvedHostCatalogSummaryPath
$hostEntry = @($hostCatalogSummary.hosts) | Where-Object { [string]$_.shellKey -eq $ShellKey } | Select-Object -First 1
if ($null -eq $hostEntry) {
    throw "Host catalog did not contain shell '$ShellKey': $resolvedHostCatalogSummaryPath"
}

$hostShellExportSummaryPath = Join-Path $laneRoots.AssetsPlayerBuildRoot "host_shell_exports\ghost_player_host_shell_export_summary.json"
$hostShellPreviewPath = ""
if (Test-Path -LiteralPath $hostShellExportSummaryPath) {
    $hostShellExportSummary = Read-JsonFile -Path $hostShellExportSummaryPath
    $hostShellEntry = @($hostShellExportSummary.shells) | Where-Object { [string]$_.shellKey -eq $ShellKey } | Select-Object -First 1
    if ($null -ne $hostShellEntry -and -not [string]::IsNullOrWhiteSpace([string]$hostShellEntry.packageRootPath)) {
        $hostShellManifestPath = Join-Path ([string]$hostShellEntry.packageRootPath) "manifest.json"
        if (Test-Path -LiteralPath $hostShellManifestPath) {
            $hostShellManifest = Read-JsonFile -Path $hostShellManifestPath
            $hostShellPreviewRelativePath = [string]$hostShellManifest.previewPath
            if (-not [string]::IsNullOrWhiteSpace($hostShellPreviewRelativePath)) {
                $hostShellPreviewPath = Join-Path ([string]$hostShellEntry.packageRootPath) ($hostShellPreviewRelativePath -replace '/', '\')
            }
        }
    }
}

$hostPackageRoot = [string]$hostEntry.outputRoot
$hostManifestPath = Join-Path $hostPackageRoot "manifest.json"
$hostProfilePath = Join-Path $hostPackageRoot "host_profile.json"
$hostManifest = Read-JsonFile -Path $hostManifestPath
$hostProfile = Read-JsonFile -Path $hostProfilePath

$varPackageReportPath = Join-Path $laneRoots.AssetsPlayerBuildRoot ("var_packages\{0}\direct_cua\player_var_package_report_latest.json" -f $resolvedVersion)
$varPackageReport = Read-JsonFile -Path $varPackageReportPath
$playerPluginUrl = [string]$varPackageReport.packagedPluginUrl
$playerAssetUrl = [string]$varPackageReport.packagedAssetUrl
$playerPackageFileName = [string]$varPackageReport.packageFileName
$playerReleaseRoot = Join-Path $laneRoots.AssetsPlayerBuildRoot (Join-Path "releases" $resolvedVersion)
$playerReleasePluginPath = Join-Path $playerReleaseRoot ("dev_plugin_player.{0}.dll" -f $resolvedVersion)

if ([string]::IsNullOrWhiteSpace($playerPluginUrl)) {
    throw "Player var package report did not provide packagedPluginUrl: $varPackageReportPath"
}

$temporaryExportRoot = Join-Path $resolvedOutputRoot "package_cua_export"
$receiptRoot = Join-Path $resolvedOutputRoot "receipts"
$unityLogPath = Join-Path $receiptRoot "unity_batch.log"
$interactiveReceiptPath = Join-Path $receiptRoot "meta_interactive_hosted_player_proof_receipt.json"
$interactiveReceiptMarkdownPath = Join-Path $receiptRoot "meta_interactive_hosted_player_proof_receipt.md"
 $resolvedPrimaryMediaPath = if ([string]::IsNullOrWhiteSpace($PrimaryMediaPath)) { "" } else { $PrimaryMediaPath.Trim() }

if (Test-Path -LiteralPath $resolvedOutputRoot) {
    Remove-Item -LiteralPath $resolvedOutputRoot -Recurse -Force
}

Ensure-Directory -PathValue $resolvedOutputRoot
Ensure-Directory -PathValue $receiptRoot
Ensure-Directory -PathValue $DeployAssetsRoot
Ensure-Directory -PathValue $DeployPresetRoot

$playerPluginBinding = Resolve-PlayerPluginBinding `
    -PluginMode $PlayerPluginMode `
    -ResolvedVersionValue $resolvedVersion `
    -PackagedPluginUrl $playerPluginUrl `
    -ReleasePluginPath $playerReleasePluginPath `
    -DeployPluginDirectory $DeployPluginRoot

$builtHostBundlePath = ""
$builtHostAssetName = ""
$hostResourceId = ""
$hostDisplayName = ""
$rawShellReportPath = ""
$proofExportAuthority = ""
$unityProjectPath = ""
$resolvedUnityEditorPath = ""
$unityExitCode = 0

if ([string]::Equals([string]$playerPluginBinding.mode, "raw", [System.StringComparison]::OrdinalIgnoreCase)) {
    $rawShellScriptPath = Join-Path $PSScriptRoot "Build-PlayerShellAssetBundles.ps1"
    $rawShellOutputRoot = Join-Path $resolvedOutputRoot "raw_shell_export"
    $rawShellResult = & $rawShellScriptPath `
        -RepoRoot $resolvedRepoRoot `
        -ShellKeys $ShellKey `
        -OutputRoot $rawShellOutputRoot `
        -DeployAssetsRoot $DeployAssetsRoot `
        -DeployPresetRoot $DeployPresetRoot `
        -DeployPluginsRoot $DeployPluginRoot `
        -PlayerMediaPath $resolvedPrimaryMediaPath `
        -SkipDeploy
    $rawShellReportPath = [string]$rawShellResult.reportPath
    $rawShellReport = Read-JsonFile -Path $rawShellReportPath
    $rawShellEntry = @($rawShellReport.shells) | Where-Object { [string]$_.shellKey -eq $ShellKey } | Select-Object -First 1
    if ($null -eq $rawShellEntry) {
        throw "Raw shell report did not contain shell '$ShellKey': $rawShellReportPath"
    }

    $builtHostBundlePath = [string]$rawShellEntry.builtBundlePath
    $builtHostAssetName = [string]$rawShellEntry.builtAssetName
    $hostResourceId = [string]$rawShellEntry.resourceId
    $hostDisplayName = [string]$rawShellEntry.displayName
    $hostPackageRoot = [string]$rawShellEntry.packageRoot
    $unityLogPath = [string]$rawShellEntry.unityLogPath
    $unityProjectPath = [string]$rawShellReport.unityProjectPath
    $resolvedUnityEditorPath = [string]$rawShellReport.unityEditorPath
    $proofExportAuthority = "raw_shell_2018"
}
else {
    $unityProjectPath = Join-Path $laneRoots.AssetsPlayerUnityRoot "ghost_training_export_clone"
    $resolvedUnityEditorPath = Resolve-UnityEditorPathForProject -ProjectPath $unityProjectPath -RequestedUnityEditorPath $UnityEditorPath

    $unityArgs = @(
        "-batchmode",
        "-quit",
        "-projectPath", $unityProjectPath,
        "-executeMethod", "GhostPlayerHostPackageCustomUnityAssetExporter.ExportPlayerHostPackageCuaBatch",
        "-faOutputRoot", $temporaryExportRoot,
        "-faPackageRoot", $hostPackageRoot,
        "-faShellKeys", "player_host",
        "-faDeploy", "false",
        "-logFile", $unityLogPath
    )

    $unityProcess = Start-Process -FilePath $resolvedUnityEditorPath -ArgumentList $unityArgs -PassThru -Wait
    $unityExitCode = $unityProcess.ExitCode

    $genericSummaryPath = Join-Path $temporaryExportRoot "ghost_player_host_cua_export_summary.json"
    if (-not (Test-Path -LiteralPath $genericSummaryPath)) {
        throw "Interactive host export summary not found: $genericSummaryPath"
    }

    $genericSummary = Read-JsonFile -Path $genericSummaryPath
    $genericExportEntry = @($genericSummary.exports) | Select-Object -First 1
    if ($null -eq $genericExportEntry) {
        throw "Interactive host export summary did not contain any exports: $genericSummaryPath"
    }

    $builtHostBundlePath = [string]$genericExportEntry.bundlePath
    $builtHostAssetName = [string]$genericExportEntry.assetName
    $hostResourceId = if (-not [string]::IsNullOrWhiteSpace([string]$hostProfile.resourceId)) {
        [string]$hostProfile.resourceId
    }
    else {
        [string]$hostManifest.resourceId
    }
    $hostDisplayName = if (-not [string]::IsNullOrWhiteSpace([string]$hostProfile.hostDisplayName)) {
        [string]$hostProfile.hostDisplayName
    }
    else {
        [string]$hostManifest.displayName
    }
    $proofExportAuthority = "host_package_2022"
}

if (-not (Test-Path -LiteralPath $builtHostBundlePath)) {
    throw "Interactive host bundle not found: $builtHostBundlePath"
}

if ([string]::IsNullOrWhiteSpace($hostResourceId)) {
    throw "Unable to resolve host resource id for shell '$ShellKey'."
}

$deployedHostBundlePath = Join-Path $DeployAssetsRoot ($hostResourceId + ".assetbundle")
$interactivePresetFileName = Resolve-InteractivePresetFileName -HostDisplayName $hostDisplayName
$deployedInteractivePresetPath = Join-Path $DeployPresetRoot $interactivePresetFileName

Copy-Item -LiteralPath $builtHostBundlePath -Destination $deployedHostBundlePath -Force

$interactivePreset = Build-InteractiveHostedPlayerPreset `
    -AssetUrl ("Custom/Assets/FrameAngel/Player/" + (Split-Path -Path $deployedHostBundlePath -Leaf)) `
    -AssetName $builtHostAssetName `
    -PluginUrl ([string]$playerPluginBinding.pluginUrl) `
    -ResolvedPrimaryMediaPath $resolvedPrimaryMediaPath

$interactivePresetStagePath = Join-Path $resolvedOutputRoot $interactivePresetFileName
Write-JsonFile -Path $interactivePresetStagePath -Value $interactivePreset
Write-JsonFile -Path $deployedInteractivePresetPath -Value $interactivePreset

$receipt = [ordered]@{
    schemaVersion = "frameangel_meta_interactive_hosted_player_proof_v1"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    shellKey = $ShellKey
    hostDisplayName = $hostDisplayName
    hostShellExportSummaryPath = $hostShellExportSummaryPath
    hostShellPreviewPath = $hostShellPreviewPath
    hostResourceId = $hostResourceId
    hostPackageRoot = $hostPackageRoot
    rawShellReportPath = $rawShellReportPath
    hostCatalogSummaryPath = $resolvedHostCatalogSummaryPath
    hostCatalogControlsControlFamilyId = [string]$hostCatalogSummary.controlsControlFamilyId
    packet15ReceiptPath = $packet15ReceiptPath
    playerVersion = $resolvedVersion
    playerVarPackageReportPath = $varPackageReportPath
    playerPackageFileName = $playerPackageFileName
    playerAssetUrl = $playerAssetUrl
    playerPluginMode = [string]$playerPluginBinding.mode
    playerPluginUrl = [string]$playerPluginBinding.pluginUrl
    playerPluginSourcePath = [string]$playerPluginBinding.sourcePluginPath
    deployedPlayerPluginPath = [string]$playerPluginBinding.deployedPluginPath
    hostBundlePath = $builtHostBundlePath
    hostAssetName = $builtHostAssetName
    deployedHostBundlePath = $deployedHostBundlePath
    interactivePresetStagePath = $interactivePresetStagePath
    deployedInteractivePresetPath = $deployedInteractivePresetPath
    unityProjectPath = $unityProjectPath
    unityEditorPath = $resolvedUnityEditorPath
    unityLogPath = $unityLogPath
    unityExitCode = $unityExitCode
    proofExportAuthority = $proofExportAuthority
    proofBoundary = [ordered]@{
        interactionArtifactReady = $true
        vamBundleCompatibilityAuthority = if ([string]::Equals($proofExportAuthority, "raw_shell_2018", [System.StringComparison]::OrdinalIgnoreCase)) { "2018.1.9f2 raw shell export" } else { "not yet VaM-valid; 2022 host package witness only" }
        liveHaloVerified = $false
        liveHaloFailure = "Halo MCP unavailable at http://localhost:5031/mcp during this slice."
        operatorTestRequested = $false
    }
}

Write-JsonFile -Path $interactiveReceiptPath -Value $receipt
Write-MarkdownReceipt -Path $interactiveReceiptMarkdownPath -Receipt $receipt

[pscustomobject]@{
    receiptPath = $interactiveReceiptPath
    receiptMarkdownPath = $interactiveReceiptMarkdownPath
    hostCatalogSummaryPath = $resolvedHostCatalogSummaryPath
    deployedHostBundlePath = $deployedHostBundlePath
    deployedInteractivePresetPath = $deployedInteractivePresetPath
    deployedPlayerPluginPath = [string]$playerPluginBinding.deployedPluginPath
    playerPluginMode = [string]$playerPluginBinding.mode
    playerPluginUrl = [string]$playerPluginBinding.pluginUrl
    unityLogPath = $unityLogPath
}
