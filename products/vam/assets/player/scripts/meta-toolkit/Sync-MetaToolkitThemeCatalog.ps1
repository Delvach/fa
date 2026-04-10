param(
    [string]$ProjectPath = "",
    [int]$ThemeIndex = 0,
    [string]$SummaryPath = "",
    [string]$OutputRoot = "",
    [switch]$Deploy,
    [string]$DeployRoot = ""
)

$ErrorActionPreference = "Stop"

$resolvedAssetLaneRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $resolvedAssetLaneRoot "unity\ghost_training_export_clone"
}

$themeFolderName = "theme_{0:D2}" -f $ThemeIndex
if ([string]::IsNullOrWhiteSpace($SummaryPath)) {
    $SummaryPath = Join-Path (Join-Path (Join-Path $ProjectPath "Library\MetaUiToolkitExports") $themeFolderName) ("ghost_meta_ui_toolkit_export_summary_{0}.json" -f $themeFolderName)
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $resolvedAssetLaneRoot ("build\meta_toolkit_catalog\{0}" -f $themeFolderName)
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

function Sync-DirectoryTree {
    param(
        [string]$SourcePath,
        [string]$TargetPath,
        [string]$Label
    )

    Assert-Path -Path $SourcePath -Label ($Label + " source")

    if (Test-Path -LiteralPath $TargetPath) {
        Remove-Item -LiteralPath $TargetPath -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $TargetPath | Out-Null

    & robocopy $SourcePath $TargetPath /E /NFL /NDL /NJH /NJS /NC /NS /NP | Out-Null
    $copyExitCode = $LASTEXITCODE
    if ($copyExitCode -gt 3) {
        throw "robocopy failed ($copyExitCode) while syncing $Label."
    }
}

function Prune-StaleSurfacePackages {
    param(
        [string]$CatalogRoot,
        [object]$Summary
    )

    if ([string]::IsNullOrWhiteSpace($CatalogRoot) -or $null -eq $Summary) {
        return
    }

    foreach ($surface in @($Summary.surfaces)) {
        if ($null -eq $surface) {
            continue
        }

        $recordedPackageRoot = [string]$surface.packageRootPath
        if ([string]::IsNullOrWhiteSpace($recordedPackageRoot)) {
            continue
        }

        $surfaceFolderName = Split-Path -Parent $recordedPackageRoot | Split-Path -Leaf
        $currentPackageFolderName = Split-Path -Leaf $recordedPackageRoot
        if ([string]::IsNullOrWhiteSpace($surfaceFolderName) -or [string]::IsNullOrWhiteSpace($currentPackageFolderName)) {
            continue
        }

        $surfaceRoot = Join-Path $CatalogRoot $surfaceFolderName
        if (-not (Test-Path -LiteralPath $surfaceRoot)) {
            continue
        }

        Get-ChildItem -LiteralPath $surfaceRoot -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            if (-not [string]::Equals($_.Name, $currentPackageFolderName, [System.StringComparison]::OrdinalIgnoreCase)) {
                Remove-Item -LiteralPath $_.FullName -Recurse -Force
            }
        }
    }
}

Assert-Path -Path $ProjectPath -Label "Ghost training project"
Assert-Path -Path $SummaryPath -Label "Meta toolkit export summary"

$summaryRoot = Split-Path -Parent $SummaryPath
$normalizedSummaryRoot = [System.IO.Path]::GetFullPath($summaryRoot)
$normalizedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not [string]::Equals($normalizedSummaryRoot, $normalizedOutputRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    Sync-DirectoryTree -SourcePath $summaryRoot -TargetPath $OutputRoot -Label "meta toolkit catalog"
}
else {
    Assert-Path -Path $OutputRoot -Label "Meta toolkit catalog output root"
}

$summary = Get-Content -LiteralPath $SummaryPath -Raw | ConvertFrom-Json
Prune-StaleSurfacePackages -CatalogRoot $OutputRoot -Summary $summary

$resolvedDeployRoot = ""
if ($Deploy.IsPresent) {
    if ([string]::IsNullOrWhiteSpace($DeployRoot)) {
        $resolvedDeployRoot = Join-Path "F:\sim\vam\Custom\PluginData\FrameAngel\meta_toolkit_demo" $themeFolderName
    }
    else {
        $resolvedDeployRoot = $DeployRoot
    }

    Sync-DirectoryTree -SourcePath $OutputRoot -TargetPath $resolvedDeployRoot -Label "meta toolkit deploy root"
    Prune-StaleSurfacePackages -CatalogRoot $resolvedDeployRoot -Summary $summary
}

[pscustomobject]@{
    projectPath = $ProjectPath
    summaryPath = $SummaryPath
    outputRoot = $OutputRoot
    deployed = [bool]$Deploy
    deployRoot = $resolvedDeployRoot
    themeIndex = $ThemeIndex
    surfaceCount = @($summary.surfaces).Count
}
