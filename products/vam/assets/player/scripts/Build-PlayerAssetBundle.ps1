param(
    [string]$RepoRoot = "",
    [string]$Version = "",
    [string]$UnityEditorPath = "C:\Program Files\Unity\Hub\Editor\2018.1.9f2\Editor\Unity.exe",
    [string]$DeployAssetsRoot = "F:\sim\vam\Custom\Assets\FrameAngel\Player",
    [switch]$IncludeControlSurface,
    [switch]$SkipDeploy,
    [switch]$AllowExistingVersion
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

$laneRoots = Get-FrameAngelPlayerLaneRoots -RepoRoot $RepoRoot -CallerScriptRoot $PSScriptRoot -EnsureAssetLaneScaffold
$RepoRoot = $laneRoots.RepoRoot
$resolvedVersion = if ([string]::IsNullOrWhiteSpace($Version)) {
    (Read-FrameAngelPlayerVersionState -RepoRoot $RepoRoot).Version
}
else {
    $Version.Trim()
}

$unityProjectPath = $laneRoots.PlayerScreenUnityProjectRoot
$exportMethod = "FrameAngelPlayerHost2018Exporter.BuildAndDeployBatch"
$bundleFileName = "fa_player_asset.{0}.assetbundle" -f $resolvedVersion
$presetFileName = "Preset_FA Player Asset {0}.vap" -f $resolvedVersion
$exportRoot = Join-Path $laneRoots.AssetsPlayerBuildRoot (Join-Path "assetbundle_exports" $resolvedVersion)
$logPath = Join-Path $exportRoot "unity_batch.log"
$summaryPath = Join-Path $exportRoot "player_screen_summary.json"
$builtBundlePath = Join-Path $exportRoot (Join-Path "assetbundles" $bundleFileName)
$releaseRoot = Join-Path $laneRoots.AssetsPlayerBuildRoot (Join-Path "releases" $resolvedVersion)
$repoArtifactPath = Join-Path $releaseRoot $bundleFileName
$liveArtifactPath = Join-Path $DeployAssetsRoot $bundleFileName
$receiptPath = Join-Path $exportRoot "player_assetbundle_build_receipt.json"

if (-not (Test-Path -LiteralPath $UnityEditorPath)) {
    throw "Unity editor not found: $UnityEditorPath"
}

if (-not (Test-Path -LiteralPath $unityProjectPath)) {
    throw "Unity project not found: $unityProjectPath"
}

if (-not $AllowExistingVersion.IsPresent) {
    $collisionPaths = @($repoArtifactPath)
    if (-not $SkipDeploy.IsPresent) {
        $collisionPaths += $liveArtifactPath
    }

    Assert-FrameAngelPlayerVersionAvailable `
        -RepoRoot $RepoRoot `
        -Version $resolvedVersion `
        -ArtifactPaths $collisionPaths `
        -ContextLabel "player assetbundle build" | Out-Null
}

if (Test-Path -LiteralPath $exportRoot) {
    Remove-Item -LiteralPath $exportRoot -Recurse -Force
}

Ensure-Directory -PathValue $exportRoot

$unityProcesses = @(Get-Process Unity -ErrorAction SilentlyContinue)
if ($unityProcesses.Count -gt 0) {
    $processList = ($unityProcesses | ForEach-Object {
        if ($_.Path) { "{0} ({1})" -f $_.Id, $_.Path } else { [string]$_.Id }
    }) -join ", "
    throw "Unity is already running. Close it before building the player assetbundle. Active process(es): $processList"
}

$deployFlag = if ($SkipDeploy.IsPresent) { "false" } else { "true" }

$unityArgs = @(
    "-batchmode",
    "-quit",
    "-projectPath", $unityProjectPath,
    "-logFile", $logPath,
    "-executeMethod", $exportMethod,
    "-faOutputRoot", $exportRoot,
    "-faBundleFileName", $bundleFileName,
    "-faPresetFileName", $presetFileName,
    "-faDeployAssetsRoot", $DeployAssetsRoot,
    "-faDeployPresetRoot", "F:\sim\vam\Custom\Atom\CustomUnityAsset",
    "-faDeploy", $deployFlag,
    "-faWritePreset", "false",
    "-faIncludeControlSurface", $(if ($IncludeControlSurface.IsPresent) { "true" } else { "false" })
)

$process = Start-Process -FilePath $UnityEditorPath -ArgumentList $unityArgs -PassThru -Wait
$unityExitCode = $process.ExitCode

try {
    Wait-ForPath -PathValue $builtBundlePath -Label "Built assetbundle"
    Wait-ForPath -PathValue $summaryPath -Label "Export summary"
}
catch {
    $logTail = Get-LogTail -PathValue $logPath
    if ([string]::IsNullOrWhiteSpace($logTail)) {
        throw
    }

    throw ("Unity export failed before artifacts appeared. Log tail:`n" + $logTail)
}

if ($unityExitCode -ne 0) {
    Write-Warning "Unity returned exit code $unityExitCode, but the expected assetbundle and summary were written. Continuing."
}

Ensure-Directory -PathValue $releaseRoot
Copy-Item -LiteralPath $builtBundlePath -Destination $repoArtifactPath -Force

if (-not $SkipDeploy.IsPresent) {
    if (-not (Test-Path -LiteralPath $liveArtifactPath)) {
        throw "Expected deployed assetbundle not found: $liveArtifactPath"
    }
}

$summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
$receipt = [ordered]@{
    schemaVersion = "frameangel_player_assetbundle_build_v1"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    version = $resolvedVersion
    unityEditorPath = $UnityEditorPath
    unityProjectPath = $unityProjectPath
    exportMethod = $exportMethod
    repoArtifactPath = $repoArtifactPath
    deployedAssetPath = if ($SkipDeploy.IsPresent) { "" } else { $liveArtifactPath }
    exportRoot = $exportRoot
    unityLogPath = $logPath
    exportSummary = $summary
}
Write-JsonFile -Path $receiptPath -Value $receipt

[pscustomobject]@{
    version = $resolvedVersion
    repoArtifactPath = $repoArtifactPath
    deployedAssetPath = if ($SkipDeploy.IsPresent) { "" } else { $liveArtifactPath }
    summaryPath = $summaryPath
    receiptPath = $receiptPath
}
