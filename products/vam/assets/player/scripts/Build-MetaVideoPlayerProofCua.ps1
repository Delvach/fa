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

$laneRoots = Get-FrameAngelPlayerLaneRoots -RepoRoot $RepoRoot -CallerScriptRoot $PSScriptRoot -EnsureAssetLaneScaffold
$RepoRoot = $laneRoots.RepoRoot
$unityProjectPath = Join-Path $laneRoots.AssetsPlayerUnityRoot "ghost_training_export_clone"
$UnityEditorPath = Resolve-UnityEditorPathForProject -ProjectPath $unityProjectPath -RequestedUnityEditorPath $UnityEditorPath
$exportMethod = "GhostMetaVideoPlayerProofCustomUnityAssetExporter.ExportMetaVideoPlayerProofBatch"
$resolvedOutputRoot = if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    Join-Path $laneRoots.AssetsPlayerBuildRoot "meta_proof_cua"
}
else {
    $OutputRoot
}

$resolvedOutputRoot = [System.IO.Path]::GetFullPath($resolvedOutputRoot)
$logPath = Join-Path $resolvedOutputRoot "unity_batch.log"
$summaryPath = Join-Path $resolvedOutputRoot "meta_video_player_proof_cua_export_summary.json"
$builtBundlePath = Join-Path $resolvedOutputRoot "assetbundles\fa_meta_video_player_proof.assetbundle"
$receiptPath = Join-Path $resolvedOutputRoot "meta_video_player_proof_cua_build_receipt.json"
$liveBundlePath = Join-Path $DeployAssetsRoot "fa_meta_video_player_proof.assetbundle"
$livePresetPath = Join-Path $DeployPresetRoot "Preset_FA Meta Video Player Proof.vap"

if (-not (Test-Path -LiteralPath $UnityEditorPath)) {
    throw "Unity editor not found: $UnityEditorPath"
}

if (-not (Test-Path -LiteralPath $unityProjectPath)) {
    throw "Unity project not found: $unityProjectPath"
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
    throw "Unity is already running. Close it before building the Meta proof CUA. Active process(es): $processList"
}

$deployFlag = if ($SkipDeploy.IsPresent) { "false" } else { "true" }
$unityArgs = @(
    "-batchmode",
    "-quit",
    "-projectPath", $unityProjectPath,
    "-logFile", $logPath,
    "-executeMethod", $exportMethod,
    "-faOutputRoot", $resolvedOutputRoot,
    "-faBuildProfile", $BuildProfile,
    "-faResourceId", "fa_meta_video_player_proof",
    "-faBundleFileName", "fa_meta_video_player_proof.assetbundle",
    "-faPresetFileName", '"Preset_FA Meta Video Player Proof.vap"',
    "-faSummaryFileName", "meta_video_player_proof_cua_export_summary.json",
    "-faDeployAssetsRoot", $DeployAssetsRoot,
    "-faDeployPresetRoot", $DeployPresetRoot,
    "-faDeploy", $deployFlag
)

$process = Start-Process -FilePath $UnityEditorPath -ArgumentList $unityArgs -PassThru -Wait
$unityExitCode = $process.ExitCode

try {
    Wait-ForPath -PathValue $builtBundlePath -Label "Built Meta proof assetbundle"
    Wait-ForPath -PathValue $summaryPath -Label "Meta proof export summary"
}
catch {
    $logTail = Get-LogTail -PathValue $logPath
    if ([string]::IsNullOrWhiteSpace($logTail)) {
        throw
    }

    throw ("Unity Meta proof export failed before artifacts appeared. Log tail:`n" + $logTail)
}

if ($unityExitCode -ne 0) {
    Write-Warning "Unity returned exit code $unityExitCode, but the expected Meta proof artifacts were written. Continuing."
}

if (-not $SkipDeploy.IsPresent) {
    if (-not (Test-Path -LiteralPath $liveBundlePath)) {
        throw "Expected deployed Meta proof assetbundle not found: $liveBundlePath"
    }
    if (-not (Test-Path -LiteralPath $livePresetPath)) {
        throw "Expected deployed Meta proof preset not found: $livePresetPath"
    }
}

$summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
$receipt = [ordered]@{
    schemaVersion = "frameangel_meta_video_player_proof_cua_build_v1"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    unityEditorPath = $UnityEditorPath
    unityProjectPath = $unityProjectPath
    exportMethod = $exportMethod
    outputRoot = $resolvedOutputRoot
    unityLogPath = $logPath
    buildProfile = $BuildProfile
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
