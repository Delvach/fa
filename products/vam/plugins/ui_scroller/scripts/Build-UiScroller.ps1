param(
    [string]$RepoRoot = "",
    [string]$VamManagedDir = "F:\sim\vam\VaM_Data\Managed",
    [string]$Version = "",
    [switch]$Obfuscate,
    [switch]$SkipObfuscation,
    [switch]$SkipDeploy,
    [ValidateSet("package", "raw", "both")]
    [string]$DeployMode = "package",
    [ValidateSet("dev", "release")]
    [string]$PackageChannel = "dev",
    [string]$DeployDir = "F:\sim\vam\Custom\Plugins",
    [string]$LegacyCleanupDir = "F:\sim\vam\Custom\Scripts",
    [string]$DestinationAddonPackages = "F:\sim\vam\AddonPackages",
    [switch]$SkipVarPackage,
    [switch]$SkipVarDistribute,
    [switch]$AllowExistingVersion
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

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

function Write-ZipArchiveFromStageRoot {
    param(
        [string]$SourceRoot,
        [string]$DestinationPath
    )

    if (Test-Path -LiteralPath $DestinationPath) {
        Remove-Item -LiteralPath $DestinationPath -Force
    }

    $archive = [System.IO.Compression.ZipFile]::Open($DestinationPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem -LiteralPath $SourceRoot -Recurse -File | Sort-Object FullName | ForEach-Object {
            $relative = $_.FullName.Substring($SourceRoot.Length).TrimStart('\', '/')
            $entryName = $relative -replace '\\', '/'
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive,
                $_.FullName,
                $entryName,
                [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Get-PluginVersionState {
    param([string]$Path)

    return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json)
}

function Get-UiScrollerCatalogState {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Ui scroller catalog not found: $Path"
    }

    return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json)
}

function Resolve-PackageIdentityState {
    param([string]$Identity)

    if ([string]::IsNullOrWhiteSpace($Identity)) {
        throw "Package identity cannot be empty."
    }

    $packageFileName = [System.IO.Path]::GetFileName($Identity.Trim())
    if (-not $packageFileName.EndsWith(".var", [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Package identity must end with .var: $Identity"
    }

    $stem = $packageFileName.Substring(0, $packageFileName.Length - 4)
    $parts = $stem.Split('.')
    if ($parts.Length -lt 2) {
        throw "Package identity must contain at least creator and package name: $packageFileName"
    }

    $creatorName = $parts[0]
    $packageName = ""
    $packageVersionTag = ""

    if ($parts.Length -ge 3) {
        $packageName = ($parts[1..($parts.Length - 2)] -join '.')
        $packageVersionTag = $parts[$parts.Length - 1]
    }
    else {
        $packageName = $parts[1]
    }

    return [pscustomobject]@{
        packageIdentity = $Identity.Trim()
        packageFileName = $packageFileName
        creatorName = $creatorName
        packageName = $packageName
        packageVersionTag = $packageVersionTag
    }
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

Ensure-Directory -PathValue $releaseDir

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

$selectedPackageIdentity = if ($PackageChannel -eq "release") {
    [string]$catalogState.package.releaseIdentity
}
else {
    [string]$catalogState.package.devIdentity
}
$packageState = Resolve-PackageIdentityState -Identity $selectedPackageIdentity

$shouldBuildVarPackage = -not $SkipVarPackage.IsPresent
$shouldDeployRaw = (-not $SkipDeploy.IsPresent) -and ($DeployMode -eq "raw" -or $DeployMode -eq "both")
$shouldDistributeVar = $shouldBuildVarPackage -and (-not $SkipDeploy.IsPresent) -and (-not $SkipVarDistribute.IsPresent) -and ($DeployMode -eq "package" -or $DeployMode -eq "both")

$reportPath = $finalOutputPath + ".build-report.txt"
$identityPath = Join-Path $releaseDir "ui_scroller.package-identities.json"

$packageRoot = Join-Path $pluginRoot ("build\var_packages\" + $resolvedVersion + "\" + $PackageChannel)
$stageRoot = Join-Path $packageRoot "source"
$packagesDir = Join-Path $packageRoot "packages"
$packageManifestPath = Join-Path $stageRoot "frameangel_joystick_scroller_var_manifest.json"
$metaPath = Join-Path $stageRoot "meta.json"
$packageReportPath = Join-Path $packageRoot "ui_scroller_var_package_report.json"
$packagePath = Join-Path $packagesDir $packageState.packageFileName
$distributedPackagePath = ""
$packagedPluginRelativePath = Join-Path "Custom\Scripts" $artifactName

if ($shouldBuildVarPackage) {
    if (Test-Path -LiteralPath $stageRoot) {
        Remove-Item -LiteralPath $stageRoot -Recurse -Force
    }

    Ensure-Directory -PathValue $stageRoot
    Ensure-Directory -PathValue $packagesDir

    $stagedPluginPath = Join-Path $stageRoot $packagedPluginRelativePath
    Ensure-Directory -PathValue (Split-Path -Parent $stagedPluginPath)
    Copy-Item -LiteralPath $finalOutputPath -Destination $stagedPluginPath -Force

    $meta = [ordered]@{
        licenseType = "FC"
        creatorName = $packageState.creatorName
        packageName = $packageState.packageName
        description = "FrameAngel Joystick Scroll plugin"
    }
    Write-JsonFile -Path $metaPath -Value $meta

    $stageManifest = [ordered]@{
        schemaVersion = "frameangel_joystick_scroll_var_manifest_v1"
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        repoRoot = $repoRootResolved
        version = $resolvedVersion
        pluginTitle = [string]$catalogState.ui.pluginTitle
        artifactName = $artifactName
        packageChannel = $PackageChannel
        packageIdentity = $packageState.packageIdentity
        packageFileName = $packageState.packageFileName
        creatorName = $packageState.creatorName
        packageName = $packageState.packageName
        packageVersionTag = $packageState.packageVersionTag
        obfuscated = $shouldObfuscate
        packagedPluginPath = ($packagedPluginRelativePath -replace '\\', '/')
    }
    Write-JsonFile -Path $packageManifestPath -Value $stageManifest

    Write-ZipArchiveFromStageRoot -SourceRoot $stageRoot -DestinationPath $packagePath

    if ($shouldDistributeVar) {
        Ensure-Directory -PathValue $DestinationAddonPackages
        $distributedPackagePath = Join-Path $DestinationAddonPackages $packageState.packageFileName
        Copy-Item -LiteralPath $packagePath -Destination $distributedPackagePath -Force
    }

    $packageReport = [ordered]@{
        schemaVersion = "frameangel_joystick_scroll_var_package_report_v1"
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        repoRoot = $repoRootResolved
        version = $resolvedVersion
        pluginTitle = [string]$catalogState.ui.pluginTitle
        packageChannel = $PackageChannel
        packageIdentity = $packageState.packageIdentity
        creatorName = $packageState.creatorName
        packageName = $packageState.packageName
        packageVersionTag = $packageState.packageVersionTag
        packageFileName = $packageState.packageFileName
        packagePath = $packagePath
        stageRoot = $stageRoot
        sourceMetaPath = $metaPath
        sourceManifestPath = $packageManifestPath
        artifactName = $artifactName
        pluginDllPath = $finalOutputPath
        packagedPluginPath = ($packagedPluginRelativePath -replace '\\', '/')
        distribution = [ordered]@{
            distributed = [bool]$shouldDistributeVar
            destinationAddonPackages = $(if ($shouldDistributeVar) { $DestinationAddonPackages } else { "" })
            distributedPackagePath = $distributedPackagePath
        }
    }
    Write-JsonFile -Path $packageReportPath -Value $packageReport
}

$report = @(
    "timestamp=" + (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssK"),
    "version=" + $resolvedVersion,
    "artifact=" + $artifactName,
    "raw=" + $rawOutputPath,
    "final=" + $finalOutputPath,
    "obfuscated=" + $shouldObfuscate.ToString().ToLowerInvariant(),
    "pluginTitle=" + [string]$catalogState.ui.pluginTitle,
    "releasePackageIdentity=" + [string]$catalogState.package.releaseIdentity,
    "devTestPackageIdentity=" + [string]$catalogState.package.devIdentity,
    "deployMode=" + $DeployMode,
    "packageChannel=" + $PackageChannel,
    "buildsVarPackage=" + $shouldBuildVarPackage.ToString().ToLowerInvariant()
)
[System.IO.File]::WriteAllLines($reportPath, $report, (New-Object System.Text.UTF8Encoding($false)))

$identityReport = [pscustomobject]@{
    pluginTitle = [string]$catalogState.ui.pluginTitle
    releasePackageIdentity = [string]$catalogState.package.releaseIdentity
    devTestPackageIdentity = [string]$catalogState.package.devIdentity
    packageChannel = $PackageChannel
}
Write-JsonFile -Path $identityPath -Value $identityReport

if ($shouldDeployRaw) {
    Ensure-Directory -PathValue $DeployDir
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
    deployMode = $DeployMode
    packageChannel = $PackageChannel
    releasePackageIdentity = [string]$catalogState.package.releaseIdentity
    devTestPackageIdentity = [string]$catalogState.package.devIdentity
    packagePath = $(if ($shouldBuildVarPackage) { $packagePath } else { "" })
    distributedPackagePath = $distributedPackagePath
    packageReportPath = $(if ($shouldBuildVarPackage) { $packageReportPath } else { "" })
    deployedPluginPath = $(if ($shouldDeployRaw) { Join-Path $DeployDir $artifactName } else { "" })
}
