param(
    [string]$RepoRoot = "",
    [string]$ProjectPath = "",
    [string]$UnityEditorPath = "",
    [int]$ThemeIndex = 0,
    [string]$SurfaceFilter = "",
    [string]$OutputRoot = "",
    [switch]$NoPreview,
    [switch]$Deploy,
    [string]$DeployRoot = ""
)

$ErrorActionPreference = "Stop"

$laneResolverPath = Join-Path (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))))) "shared\scripts\player-assets\Resolve-FrameAngelPlayerRoots.ps1"
. $laneResolverPath

function Ensure-Directory {
    param([string]$PathValue)

    if (-not (Test-Path -LiteralPath $PathValue)) {
        New-Item -ItemType Directory -Path $PathValue -Force | Out-Null
    }
}

function Resolve-UnityEditorPathForProject {
    param(
        [string]$ResolvedProjectPath,
        [string]$RequestedUnityEditorPath
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedUnityEditorPath)) {
        return $RequestedUnityEditorPath
    }

    $projectVersionPath = Join-Path $ResolvedProjectPath "ProjectSettings\ProjectVersion.txt"
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
        throw "Unity editor $projectVersion required by $ResolvedProjectPath was not found at: $candidate"
    }

    return $candidate
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

    return ((Get-Content -LiteralPath $PathValue -Tail 160) -join [Environment]::NewLine)
}

$laneRoots = Get-FrameAngelPlayerLaneRoots -RepoRoot $RepoRoot -CallerScriptRoot $PSScriptRoot -EnsureAssetLaneScaffold
$resolvedRepoRoot = $laneRoots.RepoRoot
$resolvedProjectPath = if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    Join-Path $laneRoots.AssetsPlayerUnityRoot "ghost_training_export_clone"
}
else {
    [System.IO.Path]::GetFullPath($ProjectPath)
}
$resolvedUnityEditorPath = Resolve-UnityEditorPathForProject -ResolvedProjectPath $resolvedProjectPath -RequestedUnityEditorPath $UnityEditorPath
$themeFolderName = "theme_{0:D2}" -f $ThemeIndex
$resolvedOutputRoot = if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    Join-Path $laneRoots.AssetsPlayerBuildRoot ("meta_toolkit_catalog\{0}" -f $themeFolderName)
}
else {
    [System.IO.Path]::GetFullPath($OutputRoot)
}
$summaryPath = Join-Path $resolvedOutputRoot ("ghost_meta_ui_toolkit_export_summary_{0}.json" -f $themeFolderName)
$logPath = Join-Path $resolvedOutputRoot "unity_toolkit_export.log"
$capturePreview = if ($NoPreview.IsPresent) { "false" } else { "true" }

Ensure-Directory -PathValue $resolvedOutputRoot

$unityProcesses = @(Get-Process Unity -ErrorAction SilentlyContinue)
if ($unityProcesses.Count -gt 0) {
    $processList = ($unityProcesses | ForEach-Object {
        if ($_.Path) { "{0} ({1})" -f $_.Id, $_.Path } else { [string]$_.Id }
    }) -join ", "
    throw "Unity is already running. Close it before building the Meta toolkit catalog. Active process(es): $processList"
}

$proc = Start-Process -FilePath $resolvedUnityEditorPath -ArgumentList @(
    '-batchmode',
    '-quit',
    '-projectPath', $resolvedProjectPath,
    '-logFile', $logPath,
    '-executeMethod', 'GhostMetaUiSetToolkitExporter.ExportToolkitCatalogBatch',
    '-metaUiThemeIndex', $ThemeIndex,
    '-metaUiOutputRoot', $resolvedOutputRoot,
    '-metaUiCapturePreview', $capturePreview,
    '-metaUiSurfaceFilter', ('"' + $SurfaceFilter + '"')
) -PassThru -Wait

try {
    Wait-ForPath -PathValue $summaryPath -Label "Meta toolkit export summary"
}
catch {
    $logTail = Get-LogTail -PathValue $logPath
    if ([string]::IsNullOrWhiteSpace($logTail)) {
        throw
    }

    throw ("Unity Meta toolkit catalog export failed before the summary appeared. Log tail:`n" + $logTail)
}

if ($proc.ExitCode -ne 0) {
    $logTail = Get-LogTail -PathValue $logPath
    throw ("Unity Meta toolkit catalog export failed with exit code $($proc.ExitCode). Log tail:`n" + $logTail)
}

$syncArgs = @{
    ProjectPath = $resolvedProjectPath
    ThemeIndex = $ThemeIndex
    SummaryPath = $summaryPath
    OutputRoot = $resolvedOutputRoot
}
if ($Deploy.IsPresent) {
    $syncArgs.Deploy = $true
}
if (-not [string]::IsNullOrWhiteSpace($DeployRoot)) {
    $syncArgs.DeployRoot = $DeployRoot
}

$syncResult = & (Join-Path $PSScriptRoot "Sync-MetaToolkitThemeCatalog.ps1") @syncArgs

[pscustomobject]@{
    repoRoot = $resolvedRepoRoot
    projectPath = $resolvedProjectPath
    unityEditorPath = $resolvedUnityEditorPath
    themeIndex = $ThemeIndex
    surfaceFilter = $SurfaceFilter
    capturePreview = (-not $NoPreview.IsPresent)
    outputRoot = $resolvedOutputRoot
    summaryPath = $summaryPath
    unityLogPath = $logPath
    deploy = [bool]$Deploy
    deployRoot = if ($Deploy.IsPresent) { $syncResult.deployRoot } else { "" }
    surfaceCount = $syncResult.surfaceCount
}
