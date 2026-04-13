param(
    [string]$RepoRoot = "",
    [string]$VamManagedDir = "F:\sim\vam\VaM_Data\Managed",
    [string]$Version = "",
    [switch]$Obfuscate,
    [switch]$SkipObfuscation,
    [switch]$SkipDeploy,
    [string]$DeployDir = "F:\sim\vam\Custom\Plugins",
    [string]$LegacyCleanupDir = "F:\sim\vam\Custom\Scripts",
    [switch]$AllowExistingVersion
)

$ErrorActionPreference = "Stop"

function Resolve-PathFromBase {
    param(
        [string]$PathValue,
        [string]$BasePath,
        [string]$Label,
        [bool]$MustExist = $true
    )

    if ([string]::IsNullOrWhiteSpace($PathValue)) {
        throw "$Label cannot be empty."
    }

    $candidate = if ([System.IO.Path]::IsPathRooted($PathValue)) {
        $PathValue
    }
    else {
        Join-Path $BasePath $PathValue
    }

    if ($MustExist) {
        if (-not (Test-Path -LiteralPath $candidate)) {
            throw "$Label not found: $candidate"
        }

        return (Resolve-Path -LiteralPath $candidate).Path
    }

    return [System.IO.Path]::GetFullPath($candidate)
}

function Get-PluginVersionState {
    param([string]$Path)

    $state = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    return $state
}

function Get-UiScrollerCatalogState {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Ui scroller catalog not found: $Path"
    }

    return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json)
}

$pluginRoot = Split-Path -Parent $PSScriptRoot
$repoRootResolved = if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    Resolve-Path -LiteralPath (Join-Path $pluginRoot "..\..\..\..") | Select-Object -ExpandProperty Path
}
else {
    Resolve-PathFromBase -PathValue $RepoRoot -BasePath (Get-Location).Path -Label "Repo root"
}

$pluginVersionPath = Join-Path $pluginRoot "plugin.version.json"
$pluginVersionState = Get-PluginVersionState -Path $pluginVersionPath
$catalogPath = Join-Path $pluginRoot "config\ui_scroller.catalog.json"
$catalogState = Get-UiScrollerCatalogState -Path $catalogPath
$resolvedVersion = if ([string]::IsNullOrWhiteSpace($Version)) { [string]$pluginVersionState.version } else { $Version }
$artifactName = "fa_ui_scroller.$resolvedVersion.dll"

if ((-not $AllowExistingVersion) -and ([string]$pluginVersionState.version -ne $resolvedVersion)) {
    throw "Requested version '$resolvedVersion' does not match plugin.version.json ('$($pluginVersionState.version)'). Use -AllowExistingVersion only if you are intentionally rebuilding an existing version."
}

$projectPath = Join-Path $pluginRoot "vs\fa_ui_scroller\fa_ui_scroller.csproj"
$releaseDir = Join-Path $pluginRoot ("build\releases\" + $resolvedVersion)
$rawOutputPath = Join-Path $releaseDir "fa_ui_scroller.raw.dll"
$finalOutputPath = Join-Path $releaseDir $artifactName
$obfuscationConfigPath = Join-Path $pluginRoot "config\obfuscation.defaults.json"
$obfuscationScriptPath = Join-Path $pluginRoot "scripts\Obfuscate-Plugin.ps1"
$buildOutputPath = Join-Path $pluginRoot "vs\fa_ui_scroller\bin\Release\fa_ui_scroller.dll"

New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null

$msbuildArgs = @(
    $projectPath,
    "/p:Configuration=Release",
    "/p:VamManagedDir=$VamManagedDir"
)

& msbuild @msbuildArgs
if ($LASTEXITCODE -ne 0) {
    throw "ui_scroller build failed."
}

Copy-Item -LiteralPath $buildOutputPath -Destination $rawOutputPath -Force

$shouldObfuscate = if ($SkipObfuscation.IsPresent) {
    $false
}
elseif ($PSBoundParameters.ContainsKey("Obfuscate")) {
    $Obfuscate.IsPresent
}
else {
    $true
}

if ($shouldObfuscate) {
    & $obfuscationScriptPath `
        -RepoRoot $pluginRoot `
        -PluginKey "fa_ui_scroller" `
        -InputAssemblyPath $rawOutputPath `
        -OutputAssemblyPath $finalOutputPath `
        -ConfigPath $obfuscationConfigPath `
        -ReferenceSearchPath @($VamManagedDir, (Split-Path -Parent $buildOutputPath))

    if ($LASTEXITCODE -ne 0) {
        throw "ui_scroller obfuscation failed."
    }
}
else {
    Copy-Item -LiteralPath $rawOutputPath -Destination $finalOutputPath -Force
}

$reportPath = $finalOutputPath + ".build-report.txt"
$identityPath = Join-Path $releaseDir "ui_scroller.package-identities.json"
$report = @(
    "timestamp=" + (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssK"),
    "version=" + $resolvedVersion,
    "artifact=" + $artifactName,
    "raw=" + $rawOutputPath,
    "final=" + $finalOutputPath,
    "obfuscated=" + $shouldObfuscate.ToString().ToLowerInvariant(),
    "pluginTitle=" + [string]$catalogState.ui.pluginTitle,
    "releasePackageIdentity=" + [string]$catalogState.package.releaseIdentity,
    "devTestPackageIdentity=" + [string]$catalogState.package.devIdentity
)
Set-Content -LiteralPath $reportPath -Value $report -Encoding ASCII

$identityReport = [pscustomobject]@{
    pluginTitle = [string]$catalogState.ui.pluginTitle
    releasePackageIdentity = [string]$catalogState.package.releaseIdentity
    devTestPackageIdentity = [string]$catalogState.package.devIdentity
}
Set-Content -LiteralPath $identityPath -Value ($identityReport | ConvertTo-Json -Depth 4) -Encoding ASCII

if (-not $SkipDeploy) {
    if (-not (Test-Path -LiteralPath $DeployDir)) {
        New-Item -ItemType Directory -Path $DeployDir -Force | Out-Null
    }

    Get-ChildItem -LiteralPath $DeployDir -Filter 'fa_ui_scroller*.dll' -ErrorAction SilentlyContinue | Remove-Item -Force
    if (Test-Path -LiteralPath $LegacyCleanupDir) {
        Get-ChildItem -LiteralPath $LegacyCleanupDir -Filter 'fa_ui_scroller*.dll' -ErrorAction SilentlyContinue | Remove-Item -Force
    }
    Copy-Item -LiteralPath $finalOutputPath -Destination (Join-Path $DeployDir $artifactName) -Force
}

[pscustomobject]@{
    version = $resolvedVersion
    artifactName = $artifactName
    obfuscated = $shouldObfuscate
    rawOutputPath = $rawOutputPath
    finalOutputPath = $finalOutputPath
    releasePackageIdentity = [string]$catalogState.package.releaseIdentity
    devTestPackageIdentity = [string]$catalogState.package.devIdentity
    deployedPluginPath = if ($SkipDeploy) { "" } else { Join-Path $DeployDir $artifactName }
}
