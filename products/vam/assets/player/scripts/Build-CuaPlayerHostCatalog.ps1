param(
    [string]$ShellExportSummaryPath = "",
    [string]$ControlsSummaryPath = "",
    [string]$ControlsPackageRoot = "",
    [string]$ControlSurfaceId = "meta_patterns_contentuiexample_videoplayer_e7cfc411",
    [string]$ControlFamilyId = "meta_ui_video_player",
    [string[]]$ShellKeys = @(),
    [string]$OutputRoot = "",
    [switch]$Deploy,
    [string]$DeployRoot = "F:\sim\vam\Custom\PluginData\FrameAngel\cua_player_host_catalog"
)

$ErrorActionPreference = "Stop"

$resolvedScriptRepoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))))
if ([string]::IsNullOrWhiteSpace($ShellExportSummaryPath)) {
    $ShellExportSummaryPath = Join-Path $resolvedScriptRepoRoot "products\vam\assets\player\build\host_shell_exports\ghost_player_host_shell_export_summary.json"
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $resolvedScriptRepoRoot "products\vam\assets\player\build\host_catalog"
}
if ([string]::IsNullOrWhiteSpace($ControlsSummaryPath)) {
    $ControlsSummaryPath = Join-Path $resolvedScriptRepoRoot "products\vam\assets\player\build\meta_toolkit_catalog\theme_00\ghost_meta_ui_toolkit_export_summary_theme_00.json"
}

function Assert-Path {
    param(
        [string]$Path,
        [string]$Label
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Label not found: $Path"
    }
}

function Read-JsonFile {
    param([string]$Path)
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
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

function Normalize-ShellKeys {
    param(
        [string[]]$RequestedShellKeys
    )

    $normalized = New-Object System.Collections.Generic.List[string]
    foreach ($rawValue in @($RequestedShellKeys)) {
        if ([string]::IsNullOrWhiteSpace($rawValue)) {
            continue
        }

        foreach ($token in ($rawValue -split ",")) {
            $trimmed = $token.Trim()
            if (-not [string]::IsNullOrWhiteSpace($trimmed)) {
                $normalized.Add($trimmed)
            }
        }
    }

    return $normalized.ToArray()
}

function Resolve-RelocatedPackageRoot {
    param(
        [string]$RecordedPackageRoot,
        [string]$RecordedSummaryOutputRoot,
        [string]$ActualSummaryPath
    )

    if ([string]::IsNullOrWhiteSpace($RecordedPackageRoot)) {
        return $RecordedPackageRoot
    }

    if (Test-Path -LiteralPath $RecordedPackageRoot) {
        return $RecordedPackageRoot
    }

    if ([string]::IsNullOrWhiteSpace($RecordedSummaryOutputRoot)) {
        return $RecordedPackageRoot
    }

    $normalizedRecordedPackageRoot = [System.IO.Path]::GetFullPath($RecordedPackageRoot)
    $normalizedRecordedSummaryOutputRoot = [System.IO.Path]::GetFullPath($RecordedSummaryOutputRoot)
    if (-not $normalizedRecordedPackageRoot.StartsWith($normalizedRecordedSummaryOutputRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $RecordedPackageRoot
    }

    $relativePackagePath = $normalizedRecordedPackageRoot.Substring($normalizedRecordedSummaryOutputRoot.Length).TrimStart('\', '/')
    $actualSummaryOutputRoot = Split-Path -Parent $ActualSummaryPath
    if ([string]::IsNullOrWhiteSpace($relativePackagePath) -or [string]::IsNullOrWhiteSpace($actualSummaryOutputRoot)) {
        return $RecordedPackageRoot
    }

    $candidatePackageRoot = Join-Path $actualSummaryOutputRoot $relativePackagePath
    if (Test-Path -LiteralPath $candidatePackageRoot) {
        return $candidatePackageRoot
    }

    return $RecordedPackageRoot
}

function Resolve-ControlsPackageRoot {
    param(
        [string]$RequestedPackageRoot,
        [string]$SummaryPath,
        [string]$RequestedControlSurfaceId,
        [string]$RequestedControlFamilyId
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedPackageRoot)) {
        Assert-Path -Path $RequestedPackageRoot -Label "Controls package root"
        return @{
            packageRootPath = $RequestedPackageRoot
            packageId = [string]((Read-JsonFile -Path (Join-Path $RequestedPackageRoot "manifest.json")).packageId)
            controlSurfaceId = ""
            controlFamilyId = ""
        }
    }

    Assert-Path -Path $SummaryPath -Label "Controls summary"
    $summary = Read-JsonFile -Path $SummaryPath
    $surfaces = @($summary.surfaces)
    if ($surfaces.Count -le 0) {
        throw "No surfaces were found in controls summary: $SummaryPath"
    }

    $selected = $null
    if (-not [string]::IsNullOrWhiteSpace($RequestedControlSurfaceId)) {
        $selected = $surfaces | Where-Object { [string]$_.controlSurfaceId -eq $RequestedControlSurfaceId } | Select-Object -First 1
    }

    if ($null -eq $selected -and -not [string]::IsNullOrWhiteSpace($RequestedControlFamilyId)) {
        $selected = $surfaces | Where-Object { [string]$_.controlFamilyId -eq $RequestedControlFamilyId } | Select-Object -First 1
    }

    if ($null -eq $selected) {
        $selected = $surfaces | Where-Object {
            ([string]$_.exportDisplayName) -like "*VideoPlayer*" -or
            ([string]$_.packageId) -like "*videoplayer*"
        } | Select-Object -First 1
    }

    if ($null -eq $selected) {
        throw "Unable to resolve a controls package from summary '$SummaryPath'."
    }

    $packageRootPath = Resolve-RelocatedPackageRoot `
        -RecordedPackageRoot ([string]$selected.packageRootPath) `
        -RecordedSummaryOutputRoot ([string]$summary.outputRoot) `
        -ActualSummaryPath $SummaryPath
    Assert-Path -Path $packageRootPath -Label "Resolved controls package root"
    return @{
        packageRootPath = $packageRootPath
        packageId = [string]$selected.packageId
        controlSurfaceId = [string]$selected.controlSurfaceId
        controlFamilyId = [string]$selected.controlFamilyId
    }
}

$composeScriptPath = Join-Path $PSScriptRoot "Build-CuaPlayerHostPackage.ps1"
Assert-Path -Path $composeScriptPath -Label "Build-CuaPlayerHostPackage script"
Assert-Path -Path $ShellExportSummaryPath -Label "Shell export summary"

$shellSummary = Read-JsonFile -Path $ShellExportSummaryPath
$shellProfiles = @($shellSummary.shells)
if ($shellProfiles.Count -le 0) {
    throw "No shell profiles were found in shell export summary: $ShellExportSummaryPath"
}

$normalizedShellKeys = Normalize-ShellKeys -RequestedShellKeys $ShellKeys

$selectedShells = New-Object System.Collections.Generic.List[object]
if ($null -ne $normalizedShellKeys -and $normalizedShellKeys.Count -gt 0) {
    $requested = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($shellKey in $normalizedShellKeys) {
        if (-not [string]::IsNullOrWhiteSpace($shellKey)) {
            [void]$requested.Add($shellKey.Trim())
        }
    }

    foreach ($shellProfile in @($shellProfiles)) {
        $resolvedShellKey = [string]$shellProfile.shellKey
        if ([string]::IsNullOrWhiteSpace($resolvedShellKey)) {
            continue
        }

        if ($requested.Contains($resolvedShellKey.Trim())) {
            [void]$selectedShells.Add($shellProfile)
        }
    }
}
else {
    foreach ($shellProfile in @($shellProfiles)) {
        [void]$selectedShells.Add($shellProfile)
    }
}

if ($selectedShells.Count -le 0) {
    throw "No shell profiles matched the requested shell keys."
}

$resolvedControls = Resolve-ControlsPackageRoot `
    -RequestedPackageRoot $ControlsPackageRoot `
    -SummaryPath $ControlsSummaryPath `
    -RequestedControlSurfaceId $ControlSurfaceId `
    -RequestedControlFamilyId $ControlFamilyId

if (Test-Path -LiteralPath $OutputRoot) {
    Remove-Item -LiteralPath $OutputRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null

$results = New-Object System.Collections.Generic.List[object]

foreach ($shellProfile in $selectedShells) {
    $screenPackageRoot = [string]$shellProfile.packageRootPath
    $shellKey = [string]$shellProfile.shellKey
    $hostPackageId = [string]$shellProfile.hostPackageId
    if ([string]::IsNullOrWhiteSpace($hostPackageId)) {
        $hostPackageId = "faipe_" + [string]$shellProfile.hostResourceId
    }

    if ([string]::IsNullOrWhiteSpace($screenPackageRoot)) {
        throw "Shell profile '$shellKey' does not define packageRootPath."
    }

    Assert-Path -Path $screenPackageRoot -Label ("Shell package root for " + $shellKey)
    $hostProfilePath = Join-Path $screenPackageRoot "host_profile.json"
    Assert-Path -Path $hostProfilePath -Label ("Host profile for " + $shellKey)

    $shellOutputRoot = Join-Path (Join-Path $OutputRoot $shellKey) $hostPackageId
    $composeArgs = @{
        ScreenPackageRoot = $screenPackageRoot
        ControlsPackageRoot = [string]$resolvedControls.packageRootPath
        OutputRoot = $shellOutputRoot
        HostProfilePath = $hostProfilePath
    }

    if ($Deploy.IsPresent) {
        $composeArgs.Deploy = $true
        $composeArgs.DeployRoot = Join-Path $DeployRoot $hostPackageId
    }

    $composeResult = & $composeScriptPath @composeArgs
    $results.Add([ordered]@{
        shellKey = $shellKey
        hostDisplayName = [string]$shellProfile.hostDisplayName
        packageId = [string]$composeResult.packageId
        resourceId = [string]$composeResult.resourceId
        outputRoot = [string]$composeResult.outputRoot
        deployed = [bool]$composeResult.deployed
        deployRoot = [string]$composeResult.deployRoot
    })
}

$summaryPath = Join-Path $OutputRoot "ghost_player_host_catalog_summary.json"
$summary = [ordered]@{
    schemaVersion = "frameangel_player_host_catalog_v1"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    shellExportSummaryPath = $ShellExportSummaryPath
    controlsSummaryPath = if ([string]::IsNullOrWhiteSpace($ControlsPackageRoot)) { $ControlsSummaryPath } else { "" }
    controlsPackageRoot = [string]$resolvedControls.packageRootPath
    controlsPackageId = [string]$resolvedControls.packageId
    controlsControlSurfaceId = [string]$resolvedControls.controlSurfaceId
    controlsControlFamilyId = [string]$resolvedControls.controlFamilyId
    outputRoot = $OutputRoot
    deployRoot = if ($Deploy.IsPresent) { $DeployRoot } else { "" }
    hosts = $results.ToArray()
}

Write-JsonFile -Path $summaryPath -Value $summary

[pscustomobject]@{
    shellCount = $results.Count
    controlsPackageId = [string]$resolvedControls.packageId
    outputRoot = $OutputRoot
    summaryPath = $summaryPath
    deployed = [bool]$Deploy
}
