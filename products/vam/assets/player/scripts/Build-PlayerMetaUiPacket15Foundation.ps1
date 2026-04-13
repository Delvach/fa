param(
    [string]$RepoRoot = "",
    [string]$ProjectPath = "",
    [string]$UnityEditorPath = "",
    [string]$DefaultsPath = "",
    [int]$ThemeIndex = -1,
    [string]$SurfaceFilter = "",
    [string]$ControlSurfaceId = "",
    [string[]]$ShellKeys = @(),
    [string]$ToolkitOutputRoot = "",
    [string]$HostCatalogOutputRoot = "",
    [switch]$NoPreview,
    [switch]$DeployToolkitCatalog,
    [switch]$DeployHostCatalog,
    [string]$ToolkitDeployRoot = "",
    [string]$HostCatalogDeployRoot = ""
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
    $json = $Value | ConvertTo-Json -Depth 100
    Set-Content -LiteralPath $Path -Value $json -Encoding utf8
}

function Resolve-Defaults {
    param(
        [string]$ResolvedDefaultsPath
    )

    if ([string]::IsNullOrWhiteSpace($ResolvedDefaultsPath)) {
        return [pscustomobject]@{}
    }

    if (-not (Test-Path -LiteralPath $ResolvedDefaultsPath)) {
        throw "Meta UI Packet 1.5 defaults file not found: $ResolvedDefaultsPath"
    }

    return Get-Content -LiteralPath $ResolvedDefaultsPath -Raw | ConvertFrom-Json
}

function Resolve-SettingValue {
    param(
        [object]$ExplicitValue,
        [object]$DefaultValue,
        [object]$FallbackValue
    )

    if ($null -ne $ExplicitValue) {
        if ($ExplicitValue -is [string]) {
            if (-not [string]::IsNullOrWhiteSpace($ExplicitValue)) {
                return $ExplicitValue
            }
        }
        elseif ($ExplicitValue -is [int]) {
            if ($ExplicitValue -ge 0) {
                return $ExplicitValue
            }
        }
        elseif ($ExplicitValue -is [array]) {
            if ($ExplicitValue.Count -gt 0) {
                return $ExplicitValue
            }
        }
        else {
            return $ExplicitValue
        }
    }

    if ($null -ne $DefaultValue) {
        if ($DefaultValue -is [string]) {
            if (-not [string]::IsNullOrWhiteSpace($DefaultValue)) {
                return $DefaultValue
            }
        }
        elseif ($DefaultValue -is [array]) {
            if ($DefaultValue.Count -gt 0) {
                return $DefaultValue
            }
        }
        else {
            return $DefaultValue
        }
    }

    return $FallbackValue
}

function Normalize-ShellKeys {
    param([object]$RawShellKeys)

    $normalized = New-Object System.Collections.Generic.List[string]
    foreach ($rawValue in @($RawShellKeys)) {
        if ([string]::IsNullOrWhiteSpace([string]$rawValue)) {
            continue
        }

        foreach ($token in ([string]$rawValue -split ",")) {
            $trimmed = $token.Trim()
            if (-not [string]::IsNullOrWhiteSpace($trimmed)) {
                $normalized.Add($trimmed)
            }
        }
    }

    return $normalized.ToArray()
}

$laneRoots = Get-FrameAngelPlayerLaneRoots -RepoRoot $RepoRoot -CallerScriptRoot $PSScriptRoot -EnsureAssetLaneScaffold
$resolvedRepoRoot = $laneRoots.RepoRoot
$resolvedDefaultsPath = if ([string]::IsNullOrWhiteSpace($DefaultsPath)) {
    Join-Path $laneRoots.AssetsPlayerRoot "config\meta_ui_packet_1_5.defaults.json"
}
else {
    $DefaultsPath
}
$defaults = Resolve-Defaults -ResolvedDefaultsPath $resolvedDefaultsPath

$resolvedThemeIndex = [int](Resolve-SettingValue -ExplicitValue $ThemeIndex -DefaultValue $defaults.themeIndex -FallbackValue 0)
$resolvedControlSurfaceId = [string](Resolve-SettingValue -ExplicitValue $ControlSurfaceId -DefaultValue $defaults.controlSurfaceId -FallbackValue "meta_patterns_contentuiexample_videoplayer_e7cfc411")
$resolvedShellKeys = Normalize-ShellKeys -RawShellKeys (Resolve-SettingValue -ExplicitValue $ShellKeys -DefaultValue $defaults.shellKeys -FallbackValue @("modern_tv"))
$resolvedProjectPath = if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    Join-Path $laneRoots.AssetsPlayerUnityRoot "ghost_training_export_clone"
}
else {
    $ProjectPath
}
$themeFolderName = "theme_{0:D2}" -f $resolvedThemeIndex
$resolvedToolkitOutputRoot = if ([string]::IsNullOrWhiteSpace($ToolkitOutputRoot)) {
    Join-Path $laneRoots.AssetsPlayerBuildRoot ("meta_toolkit_catalog\" + $themeFolderName)
}
else {
    $ToolkitOutputRoot
}
$resolvedHostCatalogOutputRoot = if ([string]::IsNullOrWhiteSpace($HostCatalogOutputRoot)) {
    Join-Path $laneRoots.AssetsPlayerBuildRoot ("host_catalog\" + $themeFolderName)
}
else {
    $HostCatalogOutputRoot
}
$resolvedToolkitDeployRoot = [string](Resolve-SettingValue -ExplicitValue $ToolkitDeployRoot -DefaultValue $defaults.toolkitDeployRoot -FallbackValue "")
$resolvedHostCatalogDeployRoot = [string](Resolve-SettingValue -ExplicitValue $HostCatalogDeployRoot -DefaultValue $defaults.hostCatalogDeployRoot -FallbackValue "")

$toolkitScriptPath = Join-Path $PSScriptRoot "meta-toolkit\Build-MetaToolkitThemeCatalog.ps1"
$hostCatalogScriptPath = Join-Path $PSScriptRoot "Export-GhostPlayerHostCatalog.ps1"
$receiptRoot = Join-Path $laneRoots.AssetsPlayerBuildRoot "meta_ui_packet_1_5_runs"
$timestamp = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ")
$receiptPath = Join-Path $receiptRoot ("meta_ui_packet_1_5_foundation_" + $timestamp + ".json")

if (-not (Test-Path -LiteralPath $toolkitScriptPath)) {
    throw "Meta toolkit build script not found: $toolkitScriptPath"
}

if (-not (Test-Path -LiteralPath $hostCatalogScriptPath)) {
    throw "Host catalog export script not found: $hostCatalogScriptPath"
}

Ensure-Directory -PathValue $receiptRoot

$toolkitArgs = @{
    RepoRoot = $resolvedRepoRoot
    ProjectPath = $resolvedProjectPath
    ThemeIndex = $resolvedThemeIndex
    OutputRoot = $resolvedToolkitOutputRoot
}
if (-not [string]::IsNullOrWhiteSpace($UnityEditorPath)) {
    $toolkitArgs.UnityEditorPath = $UnityEditorPath
}
if (-not [string]::IsNullOrWhiteSpace($SurfaceFilter)) {
    $toolkitArgs.SurfaceFilter = $SurfaceFilter
}
if ($NoPreview.IsPresent) {
    $toolkitArgs.NoPreview = $true
}
if ($DeployToolkitCatalog.IsPresent) {
    $toolkitArgs.Deploy = $true
}
if ($DeployToolkitCatalog.IsPresent -and -not [string]::IsNullOrWhiteSpace($resolvedToolkitDeployRoot)) {
    $toolkitArgs.DeployRoot = (Join-Path $resolvedToolkitDeployRoot $themeFolderName)
}

$toolkitResult = & $toolkitScriptPath @toolkitArgs

$hostCatalogArgs = @{
    ProjectPath = $resolvedProjectPath
    ThemeIndex = $resolvedThemeIndex
    ControlsSummaryPath = [string]$toolkitResult.summaryPath
    OutputRoot = $resolvedHostCatalogOutputRoot
    ControlSurfaceId = $resolvedControlSurfaceId
}
if (-not [string]::IsNullOrWhiteSpace($UnityEditorPath)) {
    $hostCatalogArgs.UnityExe = $UnityEditorPath
}
if ($null -ne $resolvedShellKeys -and $resolvedShellKeys.Count -gt 0) {
    $hostCatalogArgs.ShellKeys = $resolvedShellKeys
}
if ($DeployHostCatalog.IsPresent) {
    $hostCatalogArgs.Deploy = $true
}
if ($DeployHostCatalog.IsPresent -and -not [string]::IsNullOrWhiteSpace($resolvedHostCatalogDeployRoot)) {
    $hostCatalogArgs.DeployRoot = $resolvedHostCatalogDeployRoot
}

$hostCatalogResult = & $hostCatalogScriptPath @hostCatalogArgs

$receipt = [ordered]@{
    schemaVersion = "frameangel_meta_ui_packet_1_5_foundation_run_v1"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    repoRoot = $resolvedRepoRoot
    defaultsPath = $resolvedDefaultsPath
    projectPath = $resolvedProjectPath
    themeIndex = $resolvedThemeIndex
    themeFolderName = $themeFolderName
    controlSurfaceId = $resolvedControlSurfaceId
    shellKeys = @($resolvedShellKeys)
    surfaceFilter = $SurfaceFilter
    noPreview = [bool]$NoPreview
    deployToolkitCatalog = [bool]$DeployToolkitCatalog
    deployHostCatalog = [bool]$DeployHostCatalog
    toolkit = $toolkitResult
    hostCatalog = $hostCatalogResult
}

Write-JsonFile -Path $receiptPath -Value $receipt

[pscustomobject]@{
    receiptPath = $receiptPath
    toolkitSummaryPath = [string]$toolkitResult.summaryPath
    hostCatalogSummaryPath = [string](Join-Path $hostCatalogResult.outputRoot "ghost_player_host_catalog_summary.json")
    hostCatalogReceiptPath = [string]$hostCatalogResult.receiptPath
    toolkitOutputRoot = [string]$toolkitResult.outputRoot
    hostCatalogOutputRoot = [string]$hostCatalogResult.outputRoot
    controlSurfaceId = $resolvedControlSurfaceId
    themeIndex = $resolvedThemeIndex
    shellKeys = @($resolvedShellKeys)
}
