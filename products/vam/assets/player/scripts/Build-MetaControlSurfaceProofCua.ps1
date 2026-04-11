param(
    [string]$RepoRoot = "",
    [string]$UnityEditorPath = "",
    [string]$PackageRootPath = "",
    [string]$SummaryPath = "",
    [string]$OutputRoot = "",
    [string]$DeployAssetsRoot = "F:\sim\vam\Custom\Assets\FrameAngel\Meta",
    [string]$DeployPresetRoot = "F:\sim\vam\Custom\Atom\CustomUnityAsset",
    [string]$ResourceId = "",
    [string]$BundleFileName = "",
    [string]$PresetFileName = "",
    [string]$SummaryFileName = "",
    [ValidateSet("current", "current_stripver", "lz4", "lz4_stripver", "lz4_notypetree", "lz4_notypetree_stripver")]
    [string]$BuildProfile = "current",
    [switch]$SkipDeploy
)

$ErrorActionPreference = "Stop"

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
    $json = $Value | ConvertTo-Json -Depth 50
    [System.IO.File]::WriteAllText($Path, $json, (New-Object System.Text.UTF8Encoding($false)))
}

function Wait-ForPath {
    param(
        [string]$PathValue,
        [string]$Label,
        [int]$MaxAttempts = 80,
        [int]$DelayMilliseconds = 250
    )

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        if (Test-Path -LiteralPath $PathValue) {
            return
        }

        Start-Sleep -Milliseconds $DelayMilliseconds
    }

    throw "$Label not found: $PathValue"
}

function Get-LogTail {
    param([string]$PathValue)

    if (-not (Test-Path -LiteralPath $PathValue)) {
        return ""
    }

    return ((Get-Content -LiteralPath $PathValue -Tail 120) -join [Environment]::NewLine)
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

if ([string]::IsNullOrWhiteSpace($PackageRootPath) -and [string]::IsNullOrWhiteSpace($SummaryPath)) {
    throw "Build-MetaControlSurfaceProofCua.ps1 requires either -PackageRootPath or -SummaryPath."
}
if ([string]::IsNullOrWhiteSpace($ResourceId)) {
    throw "Build-MetaControlSurfaceProofCua.ps1 requires -ResourceId."
}
if ([string]::IsNullOrWhiteSpace($BundleFileName)) {
    throw "Build-MetaControlSurfaceProofCua.ps1 requires -BundleFileName."
}
if ([string]::IsNullOrWhiteSpace($PresetFileName)) {
    throw "Build-MetaControlSurfaceProofCua.ps1 requires -PresetFileName."
}
if ([string]::IsNullOrWhiteSpace($SummaryFileName)) {
    throw "Build-MetaControlSurfaceProofCua.ps1 requires -SummaryFileName."
}

$laneRoots = Get-FrameAngelPlayerLaneRoots -RepoRoot $RepoRoot -CallerScriptRoot $PSScriptRoot -EnsureAssetLaneScaffold
$RepoRoot = $laneRoots.RepoRoot
$unityProjectPath = $laneRoots.PlayerScreenUnityProjectRoot
$UnityEditorPath = Resolve-UnityEditorPathForProject -ProjectPath $unityProjectPath -RequestedUnityEditorPath $UnityEditorPath
$exportMethod = "FrameAngelMetaVideoPlayer2018Exporter.BuildAndDeployBatch"

$resolvedPackageRootPath = if ([string]::IsNullOrWhiteSpace($PackageRootPath)) { "" } else { [System.IO.Path]::GetFullPath($PackageRootPath) }
$resolvedSummaryPath = if ([string]::IsNullOrWhiteSpace($SummaryPath)) { "" } else { [System.IO.Path]::GetFullPath($SummaryPath) }
$resolvedOutputRoot = if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    Join-Path $laneRoots.AssetsPlayerBuildRoot $ResourceId
}
else {
    [System.IO.Path]::GetFullPath($OutputRoot)
}

$logPath = Join-Path $resolvedOutputRoot "unity_batch.log"
$summaryPath = Join-Path $resolvedOutputRoot $SummaryFileName
$builtBundlePath = Join-Path $resolvedOutputRoot ("assetbundles\" + $BundleFileName)
$receiptPath = Join-Path $resolvedOutputRoot ($ResourceId + "_build_receipt.json")
$liveBundlePath = Join-Path $DeployAssetsRoot $BundleFileName
$livePresetPath = Join-Path $DeployPresetRoot $PresetFileName

if (-not (Test-Path -LiteralPath $UnityEditorPath)) {
    throw "Unity editor not found: $UnityEditorPath"
}

if (-not (Test-Path -LiteralPath $unityProjectPath)) {
    throw "Unity project not found: $unityProjectPath"
}

if (-not [string]::IsNullOrWhiteSpace($resolvedPackageRootPath) -and -not (Test-Path -LiteralPath $resolvedPackageRootPath)) {
    throw "Toolkit surface package root not found: $resolvedPackageRootPath"
}

if (-not [string]::IsNullOrWhiteSpace($resolvedSummaryPath) -and -not (Test-Path -LiteralPath $resolvedSummaryPath)) {
    throw "Toolkit export summary not found: $resolvedSummaryPath"
}

if (Test-Path -LiteralPath $resolvedOutputRoot) {
    Remove-Item -LiteralPath $resolvedOutputRoot -Recurse -Force
}

Ensure-Directory -PathValue $resolvedOutputRoot

$unityProcesses = @(Get-Process Unity -ErrorAction SilentlyContinue)
if ($unityProcesses.Count -gt 0) {
    $processList = ($unityProcesses | ForEach-Object {
        if ($_.Path) { "{0} ({1})" -f $_.Id, $_.Path } else { [string]$_.Id }
    }) -join ", "
    throw "Unity is already running. Close it before building the Meta control-surface proof CUA. Active process(es): $processList"
}

$deployFlag = if ($SkipDeploy.IsPresent) { "false" } else { "true" }
$unityArgs = @(
    "-batchmode",
    "-quit",
    "-projectPath", $unityProjectPath,
    "-logFile", $logPath,
    "-executeMethod", $exportMethod,
    "-faOutputRoot", $resolvedOutputRoot,
    "-faResourceId", $ResourceId,
    "-faBundleFileName", $BundleFileName,
    "-faPresetFileName", ('"' + $PresetFileName + '"'),
    "-faSummaryFileName", $SummaryFileName,
    "-faDeployAssetsRoot", $DeployAssetsRoot,
    "-faDeployPresetRoot", $DeployPresetRoot,
    "-faDeploy", $deployFlag
)

if (-not [string]::IsNullOrWhiteSpace($resolvedSummaryPath)) {
    $unityArgs += @("-faSummaryPath", $resolvedSummaryPath)
}

if (-not [string]::IsNullOrWhiteSpace($resolvedPackageRootPath)) {
    $unityArgs += @("-faPackageRootPath", $resolvedPackageRootPath)
}

$process = Start-Process -FilePath $UnityEditorPath -ArgumentList $unityArgs -PassThru -Wait
$unityExitCode = $process.ExitCode

try {
    Wait-ForPath -PathValue $builtBundlePath -Label "Built Meta control-surface proof assetbundle"
    Wait-ForPath -PathValue $summaryPath -Label "Meta control-surface proof export summary"
}
catch {
    $logTail = Get-LogTail -PathValue $logPath
    if ([string]::IsNullOrWhiteSpace($logTail)) {
        throw
    }

    throw ("Unity Meta control-surface proof export failed before artifacts appeared. Log tail:`n" + $logTail)
}

if ($unityExitCode -ne 0) {
    Write-Warning "Unity returned exit code $unityExitCode, but the expected Meta control-surface proof artifacts were written. Continuing."
}

if (-not $SkipDeploy.IsPresent) {
    if (-not (Test-Path -LiteralPath $liveBundlePath)) {
        throw "Expected deployed Meta control-surface proof assetbundle not found: $liveBundlePath"
    }
    if (-not (Test-Path -LiteralPath $livePresetPath)) {
        throw "Expected deployed Meta control-surface proof preset not found: $livePresetPath"
    }
}

$summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
$receipt = [ordered]@{
    schemaVersion = "frameangel_meta_control_surface_cua_build_v1"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    unityEditorPath = $UnityEditorPath
    unityProjectPath = $unityProjectPath
    sourceSummaryPath = $resolvedSummaryPath
    sourcePackageRootPath = $resolvedPackageRootPath
    exportMethod = $exportMethod
    outputRoot = $resolvedOutputRoot
    unityLogPath = $logPath
    buildProfile = $BuildProfile
    resourceId = $ResourceId
    deployedAssetPath = if ($SkipDeploy.IsPresent) { "" } else { $liveBundlePath }
    deployedPresetPath = if ($SkipDeploy.IsPresent) { "" } else { $livePresetPath }
    exportSummary = $summary
}
Write-JsonFile -Path $receiptPath -Value $receipt

[pscustomobject]@{
    outputRoot = $resolvedOutputRoot
    summaryPath = $summaryPath
    receiptPath = $receiptPath
    deployedAssetPath = if ($SkipDeploy.IsPresent) { "" } else { $liveBundlePath }
    deployedPresetPath = if ($SkipDeploy.IsPresent) { "" } else { $livePresetPath }
}
