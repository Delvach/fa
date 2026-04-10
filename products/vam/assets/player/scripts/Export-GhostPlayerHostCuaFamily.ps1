param(
    [string]$UnityExe = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe",
    [string]$ProjectPath = "",
    [string]$UnityEditorBridgePackagePath = "",
    [string]$OutputRoot = "",
    [string]$DeployAssetsRoot = "F:\sim\vam\Custom\Assets\FrameAngel\Player",
    [string]$DeployPresetRoot = "F:\sim\vam\Custom\Atom\CustomUnityAsset",
    [string[]]$ShellKeys = @(),
    [switch]$NoDeploy
)

$ErrorActionPreference = "Stop"
$resolvedAssetLaneRoot = Split-Path -Parent $PSScriptRoot
$defaultProjectPath = Join-Path $resolvedAssetLaneRoot "unity\ghost_training_export_clone"
$defaultUnityEditorBridgePackagePath = "C:\projects\frameangel_tools\tools\unity_editor_bridge\current"
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
Repair-UnityEditorBridgeDependency -TargetProjectPath $ProjectPath -LiveBridgePackagePath $UnityEditorBridgePackagePath

if ($null -eq $ShellKeys -or $ShellKeys.Count -le 0) {
    $ShellKeys = @($defaultFamilyShellKeys)
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $resolvedAssetLaneRoot "build\cua_shell_family"
}

$timestamp = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ")
$receiptRoot = Join-Path $resolvedAssetLaneRoot "build\cua_shell_family_runs"
if (-not (Test-Path -LiteralPath $receiptRoot)) {
    New-Item -ItemType Directory -Path $receiptRoot -Force | Out-Null
}

$unityLogPath = Join-Path $receiptRoot ("ghost_player_host_cua_export_" + $timestamp + ".log")
$receiptPath = Join-Path $receiptRoot ("ghost_player_host_cua_export_" + $timestamp + ".json")

$deployValue = if ($NoDeploy.IsPresent) { "false" } else { "true" }
$shellKeysCsv = if ($null -ne $ShellKeys -and $ShellKeys.Count -gt 0) {
    ($ShellKeys | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join ","
}
else {
    ""
}

$unityArgs = @(
    "-batchmode",
    "-quit",
    "-projectPath", $ProjectPath,
    "-executeMethod", "GhostPlayerHostCustomUnityAssetExporter.ExportPlayerHostCuaFamilyBatch",
    "-faOutputRoot", $OutputRoot,
    "-faDeployAssetsRoot", $DeployAssetsRoot,
    "-faDeployPresetRoot", $DeployPresetRoot,
    "-faDeploy", $deployValue,
    "-logFile", $unityLogPath
)

if (-not [string]::IsNullOrWhiteSpace($shellKeysCsv)) {
    $unityArgs += @("-faShellKeys", $shellKeysCsv)
}

$null = & $UnityExe @unityArgs
$unityExitCode = $LASTEXITCODE
$summaryPath = Join-Path $OutputRoot "ghost_player_host_cua_export_summary.json"
Assert-Path -Path $summaryPath -Label "CUA export summary"
$summary = Get-Content -Raw -LiteralPath $summaryPath | ConvertFrom-Json

$requestedShellKeySet = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
foreach ($shellKey in @($ShellKeys)) {
    if (-not [string]::IsNullOrWhiteSpace($shellKey)) {
        [void]$requestedShellKeySet.Add($shellKey)
    }
}

$exportedShellKeySet = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
foreach ($exportEntry in @($summary.exports)) {
    if (-not [string]::IsNullOrWhiteSpace([string]$exportEntry.shellKey)) {
        [void]$exportedShellKeySet.Add([string]$exportEntry.shellKey)
    }
}

if ($unityExitCode -ne 0) {
    $allRequestedExportsPresent = $true
    foreach ($requestedShellKey in $requestedShellKeySet) {
        if (-not $exportedShellKeySet.Contains($requestedShellKey)) {
            $allRequestedExportsPresent = $false
            break
        }
    }

    if (-not $allRequestedExportsPresent) {
        throw "Unity CUA export failed before all requested shell exports were written. See log: $unityLogPath"
    }

    Write-Warning "Unity exited with code $unityExitCode after writing the requested CUA shell family. Accepting summary-backed success. Log: $unityLogPath"
}

$receipt = [ordered]@{
    schemaVersion = "ghost_player_host_cua_export_run_v1"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    unityExe = $UnityExe
    projectPath = $ProjectPath
    outputRoot = $OutputRoot
    deployAssetsRoot = $DeployAssetsRoot
    deployPresetRoot = $DeployPresetRoot
    deployed = (-not $NoDeploy.IsPresent)
    shellKeys = @($ShellKeys)
    unityExitCode = $unityExitCode
    unityLogPath = $unityLogPath
    summaryPath = $summaryPath
    summary = $summary
}

Write-JsonFile -Path $receiptPath -Value $receipt

[pscustomobject]@{
    receiptPath = $receiptPath
    summaryPath = $summaryPath
    unityLogPath = $unityLogPath
    outputRoot = $OutputRoot
    deployed = (-not $NoDeploy.IsPresent)
}
