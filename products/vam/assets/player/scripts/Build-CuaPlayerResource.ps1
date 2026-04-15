param(
    [string]$RepoRoot = "",
    [string]$VamManagedDir = "F:\sim\vam\VaM_Data\Managed",
    [string]$Version = "",
    [string]$DeployIteration = "",
    [string]$ForbiddenTermsConfig = "",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$SkipGuard,
    [switch]$SkipDeploy,
    [string]$DeployDir = "F:\sim\vam\Custom\Plugins",
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

function Resolve-DeployIterationToken {
    param([string]$RequestedIteration)

    $value = if ([string]::IsNullOrWhiteSpace($RequestedIteration)) {
        "alpha"
    }
    else {
        $RequestedIteration.Trim()
    }

    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Deploy iteration cannot be blank."
    }

    $value = $value.ToLowerInvariant()
    $value = ($value -replace '[^a-z0-9_]+', '_').Trim('_')
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Deploy iteration token resolved blank."
    }

    return $value
}

function Get-BuildRuntimeInfoVersion {
    param([string]$Path)

    $content = Get-Content -LiteralPath $Path -Raw
    $match = [regex]::Match($content, 'internal static readonly string BuildVersion = "(.*?)";')
    if ($match.Success) {
        return $match.Groups[1].Value
    }

    throw "Could not resolve BuildVersion from $Path"
}

function Set-BuildRuntimeInfoVersion {
    param(
        [string]$Path,
        [string]$BuildVersion
    )

    $content = Get-Content -LiteralPath $Path -Raw
    $updated = [regex]::Replace(
        $content,
        'internal static readonly string BuildVersion = ".*?";',
        ('internal static readonly string BuildVersion = "{0}";' -f $BuildVersion),
        1)

    if ($updated -eq $content) {
        throw "Could not stamp BuildVersion into $Path"
    }

    [System.IO.File]::WriteAllText($Path, $updated, (New-Object System.Text.UTF8Encoding($false)))
}

function Resolve-PlayerVersion {
    param(
        [string]$RepoRootValue,
        [string]$ExplicitVersion,
        [string]$RuntimeInfoPath
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitVersion)) {
        return $ExplicitVersion.Trim()
    }

    $manifestPath = Get-FrameAngelPlayerVersionManifestPath -RepoRoot $RepoRootValue
    if (Test-Path -LiteralPath $manifestPath) {
        return (Read-FrameAngelPlayerVersionState -RepoRoot $RepoRootValue).Version
    }

    return Get-BuildRuntimeInfoVersion -Path $RuntimeInfoPath
}

$laneRoots = Get-FrameAngelPlayerLaneRoots -RepoRoot $RepoRoot -CallerScriptRoot $PSScriptRoot -EnsureAssetLaneScaffold
$RepoRoot = $laneRoots.RepoRoot
$runtimeInfoPath = Join-Path $laneRoots.PluginPlayerRoot "src\shared-runtime\BuildRuntimeInfo.cs"
$projectPath = Join-Path $laneRoots.PluginPlayerRoot "vs\fa_cua_player\fa_cua_player.csproj"
$guardScriptPath = Join-Path $laneRoots.AssetsPlayerRoot "scripts\Validate-VamForbiddenUsage.ps1"
$configPathResolved = if ([string]::IsNullOrWhiteSpace($ForbiddenTermsConfig)) {
    Join-Path $laneRoots.AssetsPlayerRoot "scripts\vam-forbidden-terms.json"
}
else {
    $ForbiddenTermsConfig
}

if (-not (Test-Path -LiteralPath $runtimeInfoPath)) {
    throw "BuildRuntimeInfo not found: $runtimeInfoPath"
}

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "CUA player project not found: $projectPath"
}

if (-not (Test-Path -LiteralPath $guardScriptPath)) {
    throw "Forbidden usage validator not found: $guardScriptPath"
}

$resolvedVersion = Resolve-PlayerVersion -RepoRootValue $RepoRoot -ExplicitVersion $Version -RuntimeInfoPath $runtimeInfoPath
$deployIterationToken = Resolve-DeployIterationToken -RequestedIteration $DeployIteration
$repoArtifactDir = Join-Path $RepoRoot "deployed\plugins"
$repoArtifactPath = Join-Path $repoArtifactDir ("plugin_player_dev.{0}.{1}.dll" -f $resolvedVersion, $deployIterationToken)
$liveArtifactPath = Join-Path $DeployDir ("plugin_player_dev.{0}.{1}.dll" -f $resolvedVersion, $deployIterationToken)

if (-not $AllowExistingVersion.IsPresent) {
    $collisionPaths = @($repoArtifactPath)
    if (-not $SkipDeploy.IsPresent) {
        $collisionPaths += $liveArtifactPath
    }

    Assert-FrameAngelPlayerVersionAvailable `
        -RepoRoot $RepoRoot `
        -Version $resolvedVersion `
        -ArtifactPaths $collisionPaths `
        -ContextLabel "fa_cua_player build" | Out-Null
}

if ((Get-BuildRuntimeInfoVersion -Path $runtimeInfoPath) -ne $resolvedVersion) {
    Set-BuildRuntimeInfoVersion -Path $runtimeInfoPath -BuildVersion $resolvedVersion
}

if (-not $SkipGuard.IsPresent) {
    $targetFiles = Get-ChildItem -LiteralPath (Join-Path $laneRoots.PluginPlayerRoot "src") -Filter "*.cs" -File -Recurse |
        Select-Object -ExpandProperty FullName

    & $guardScriptPath `
        -RepoRoot $laneRoots.PluginPlayerRoot `
        -ConfigPath $configPathResolved `
        -TargetFiles $targetFiles
}

$buildArgs = @(
    $projectPath,
    "/t:Restore,Build",
    "/p:Configuration=$Configuration",
    "/p:VamManagedDir=$VamManagedDir"
)

& msbuild.cmd @buildArgs
if ($LASTEXITCODE -ne 0) {
    throw "msbuild failed for fa_cua_player."
}

$builtPluginPath = Join-Path $laneRoots.PluginPlayerRoot ("vs\fa_cua_player\bin\{0}\fa_cua_player.dll" -f $Configuration)
if (-not (Test-Path -LiteralPath $builtPluginPath)) {
    throw "Built plugin artifact not found: $builtPluginPath"
}

Ensure-Directory -PathValue $repoArtifactDir
Copy-Item -LiteralPath $builtPluginPath -Destination $repoArtifactPath -Force

if (-not $SkipDeploy.IsPresent) {
    Ensure-Directory -PathValue $DeployDir
    Copy-Item -LiteralPath $repoArtifactPath -Destination $liveArtifactPath -Force
}

[pscustomobject]@{
    buildVersion = $resolvedVersion
    deployIteration = $deployIterationToken
    configuration = $Configuration
    builtPluginPath = $builtPluginPath
    repoArtifactPath = $repoArtifactPath
    deployedPluginPath = if ($SkipDeploy.IsPresent) { "" } else { $liveArtifactPath }
}
