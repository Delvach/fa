param(
    [string]$UnityExe = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe",
    [string]$ProjectPath = "",
    [string]$UnityEditorBridgePackagePath = "",
    [int]$ThemeIndex = 0,
    [string]$ControlsSummaryPath = "",
    [string]$ShellExportRoot = "",
    [string]$OutputRoot = "",
    [string]$ControlSurfaceId = "meta_patterns_contentuiexample_videoplayer_e7cfc411",
    [string[]]$ShellKeys = @(),
    [switch]$Deploy,
    [string]$DeployRoot = "F:\sim\vam\Custom\PluginData\FrameAngel\cua_player_host_catalog"
)

$ErrorActionPreference = "Stop"
$resolvedAssetLaneRoot = Split-Path -Parent $PSScriptRoot
$defaultProjectPath = Join-Path $resolvedAssetLaneRoot "unity\ghost_training_export_clone"
$defaultUnityEditorBridgePackagePath = Join-Path $resolvedAssetLaneRoot "unity_editor_bridge\current"
$defaultBuildThemeFolderName = "theme_{0:D2}" -f $ThemeIndex
$defaultBuildControlsSummaryPath = Join-Path $resolvedAssetLaneRoot ("build\meta_toolkit_catalog\{0}\ghost_meta_ui_toolkit_export_summary_{0}.json" -f $defaultBuildThemeFolderName)
$defaultFamilyShellKeys = @(
    "mcbrooke_laptop",
    "ivone_phone",
    "ivad_tablet",
    "modern_tv",
    "retro_tv"
)

function Assert-Path {
    param(
        [string]$Path,
        [string]$Label
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Label not found: $Path"
    }
}

function Write-JsonFile {
    param(
        [string]$Path,
        [object]$Value
    )

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $json = $Value | ConvertTo-Json -Depth 100
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $json, $utf8NoBom)
}

function Repair-UnityEditorBridgeDependency {
    param(
        [string]$TargetProjectPath,
        [string]$LiveBridgePackagePath
    )

    $manifestPath = Join-Path $TargetProjectPath "Packages\manifest.json"
    $lockPath = Join-Path $TargetProjectPath "Packages\packages-lock.json"
    $liveDependencyValue = "file:" + ($LiveBridgePackagePath -replace "\\", "/")
    $legacyDependencyValues = @(
        "file:C:/projects/frameangel_tools/tools/unity_editor_bridge/current",
        "file:C:/projects/10-products/vam/vam-plugin-suite/external/unity-editor-bridge",
        "file:C:/projects/frameangel/tools/unity/packages/unity-editor-bridge"
    )

    if (-not (Test-Path -LiteralPath $manifestPath)) {
        return
    }

    if (-not (Test-Path -LiteralPath $LiveBridgePackagePath)) {
        throw "Live unity-editor-bridge package not found: $LiveBridgePackagePath"
    }

    $manifestContent = Get-Content -LiteralPath $manifestPath -Raw
    $updatedManifest = $manifestContent
    foreach ($dependencyValue in $legacyDependencyValues) {
        if (-not [string]::Equals($dependencyValue, $liveDependencyValue, [System.StringComparison]::OrdinalIgnoreCase)) {
            $updatedManifest = $updatedManifest.Replace($dependencyValue, $liveDependencyValue)
        }
    }
    if ($updatedManifest -ne $manifestContent) {
        [System.IO.File]::WriteAllText($manifestPath, $updatedManifest, (New-Object System.Text.UTF8Encoding($false)))
    }

    if (Test-Path -LiteralPath $lockPath) {
        $lockContent = Get-Content -LiteralPath $lockPath -Raw
        $updatedLock = $lockContent
        foreach ($dependencyValue in $legacyDependencyValues) {
            if (-not [string]::Equals($dependencyValue, $liveDependencyValue, [System.StringComparison]::OrdinalIgnoreCase)) {
                $updatedLock = $updatedLock.Replace($dependencyValue, $liveDependencyValue)
            }
        }
        if ($updatedLock -ne $lockContent) {
            [System.IO.File]::WriteAllText($lockPath, $updatedLock, (New-Object System.Text.UTF8Encoding($false)))
        }
    }
}

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = $defaultProjectPath
}

if ([string]::IsNullOrWhiteSpace($UnityEditorBridgePackagePath)) {
    $UnityEditorBridgePackagePath = $defaultUnityEditorBridgePackagePath
}

Assert-Path -Path $UnityExe -Label "Unity editor"
Assert-Path -Path $ProjectPath -Label "Ghost training project"
Assert-Path -Path $resolvedAssetLaneRoot -Label "Player asset lane root"
Repair-UnityEditorBridgeDependency -TargetProjectPath $ProjectPath -LiveBridgePackagePath $UnityEditorBridgePackagePath

if ($null -eq $ShellKeys -or $ShellKeys.Count -le 0) {
    $ShellKeys = @($defaultFamilyShellKeys)
}

$resolvedShellExportRoot = if ([string]::IsNullOrWhiteSpace($ShellExportRoot)) {
    Join-Path $resolvedAssetLaneRoot "build\host_shell_exports"
}
else {
    $ShellExportRoot
}

$resolvedControlsSummaryPath = if ([string]::IsNullOrWhiteSpace($ControlsSummaryPath)) {
    if (Test-Path -LiteralPath $defaultBuildControlsSummaryPath) {
        $defaultBuildControlsSummaryPath
    }
    else {
        $themeFolderName = "theme_{0:D2}" -f $ThemeIndex
        $summaryFileName = "ghost_meta_ui_toolkit_export_summary_{0}.json" -f $themeFolderName
        Join-Path (Join-Path (Join-Path $ProjectPath "Library\MetaUiToolkitExports") $themeFolderName) $summaryFileName
    }
}
else {
    $ControlsSummaryPath
}

$resolvedOutputRoot = if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    Join-Path $resolvedAssetLaneRoot ("build\host_catalog\theme_{0:D2}" -f $ThemeIndex)
}
else {
    $OutputRoot
}

$buildCatalogScript = Join-Path $PSScriptRoot "Build-CuaPlayerHostCatalog.ps1"
Assert-Path -Path $buildCatalogScript -Label "Build-CuaPlayerHostCatalog script"

$timestamp = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ")
$receiptRoot = Join-Path $resolvedAssetLaneRoot "build\host_catalog_runs"
$unityLogPath = Join-Path $receiptRoot ("ghost_player_host_shell_export_{0}.log" -f $timestamp)
$receiptPath = Join-Path $receiptRoot ("ghost_player_host_catalog_run_{0}.json" -f $timestamp)

if (-not (Test-Path -LiteralPath $receiptRoot)) {
    New-Item -ItemType Directory -Path $receiptRoot -Force | Out-Null
}

$unityArgs = @(
    "-batchmode",
    "-quit",
    "-projectPath", $ProjectPath,
    "-executeMethod", "GhostPlayerHostShellExporter.ExportPlayerHostShellCatalogBatch",
    "-playerHostShellOutputRoot", $resolvedShellExportRoot,
    "-playerHostShellCapturePreview", "true",
    "-logFile", $unityLogPath
)

$null = & $UnityExe @unityArgs
$unityExitCode = $LASTEXITCODE

$shellSummaryPath = Join-Path $resolvedShellExportRoot "ghost_player_host_shell_export_summary.json"
Assert-Path -Path $shellSummaryPath -Label "Shell export summary"
Assert-Path -Path $resolvedControlsSummaryPath -Label "Meta toolkit controls summary"

$shellSummary = Get-Content -Raw -LiteralPath $shellSummaryPath | ConvertFrom-Json
$requestedShellKeySet = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
foreach ($shellKey in @($ShellKeys)) {
    if (-not [string]::IsNullOrWhiteSpace($shellKey)) {
        [void]$requestedShellKeySet.Add($shellKey)
    }
}

$exportedShellKeySet = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
foreach ($shellProfile in @($shellSummary.shells)) {
    if (-not [string]::IsNullOrWhiteSpace([string]$shellProfile.shellKey)) {
        [void]$exportedShellKeySet.Add([string]$shellProfile.shellKey)
    }
}

if ($unityExitCode -ne 0) {
    $allRequestedShellsPresent = $true
    foreach ($requestedShellKey in $requestedShellKeySet) {
        if (-not $exportedShellKeySet.Contains($requestedShellKey)) {
            $allRequestedShellsPresent = $false
            break
        }
    }

    if (-not $allRequestedShellsPresent) {
        throw "Unity shell export failed before all requested shells were written. See log: $unityLogPath"
    }

    Write-Warning "Unity exited with code $unityExitCode after writing the requested shell export family. Accepting summary-backed success. Log: $unityLogPath"
}

$catalogArgs = @{
    ShellExportSummaryPath = $shellSummaryPath
    ControlsSummaryPath = $resolvedControlsSummaryPath
    ControlSurfaceId = $ControlSurfaceId
    OutputRoot = $resolvedOutputRoot
}

if ($null -ne $ShellKeys -and $ShellKeys.Count -gt 0) {
    $catalogArgs.ShellKeys = $ShellKeys
}

if ($Deploy.IsPresent) {
    $catalogArgs.Deploy = $true
    $catalogArgs.DeployRoot = $DeployRoot
}

$catalogResult = & $buildCatalogScript @catalogArgs

$receipt = [ordered]@{
    schemaVersion = "ghost_player_host_catalog_run_v1"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    unityExe = $UnityExe
    projectPath = $ProjectPath
    playerAssetLaneRoot = $resolvedAssetLaneRoot
    shellExportRoot = $resolvedShellExportRoot
    shellExportSummaryPath = $shellSummaryPath
    controlsSummaryPath = $resolvedControlsSummaryPath
    outputRoot = $resolvedOutputRoot
    unityLogPath = $unityLogPath
    unityExitCode = $unityExitCode
    controlSurfaceId = $ControlSurfaceId
    shellKeys = @($ShellKeys)
    deployRoot = if ($Deploy.IsPresent) { $DeployRoot } else { "" }
    catalog = $catalogResult
}

Write-JsonFile -Path $receiptPath -Value $receipt

[pscustomobject]@{
    receiptPath = $receiptPath
    unityLogPath = $unityLogPath
    outputRoot = $resolvedOutputRoot
    shellExportRoot = $resolvedShellExportRoot
    deployed = [bool]$Deploy
}
