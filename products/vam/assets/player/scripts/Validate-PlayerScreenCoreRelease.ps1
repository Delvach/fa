param(
    [string]$RepoRoot = "",
    [string]$Version = "",
    [string]$ReleaseRoot = "",
    [string]$RepoAssetPath = "",
    [string]$RepoPluginPath = "",
    [string]$ChangelogSourcePath = "",
    [string]$ChangelogJsonPath = "",
    [string]$ChangelogMarkdownPath = "",
    [string]$LiveAssetPath = "",
    [string]$LivePluginPath = "",
    [string]$ReceiptPath = "",
    [string]$SummaryPath = "",
    [switch]$SkipLiveDeployChecks
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
    $json = $Value | ConvertTo-Json -Depth 100
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $json, $utf8NoBom)
}

function Add-Failure {
    param(
        [System.Collections.Generic.List[object]]$Failures,
        [string]$Rule,
        [string]$Message
    )

    $Failures.Add([pscustomobject]@{
        rule = $Rule
        message = $Message
    }) | Out-Null
}

function Add-Check {
    param(
        [System.Collections.Generic.List[object]]$Checks,
        [string]$Rule,
        [string]$Message
    )

    $Checks.Add([pscustomobject]@{
        rule = $Rule
        message = $Message
    }) | Out-Null
}

function Get-MatchingFiles {
    param(
        [string]$DirectoryPath,
        [string]$Filter
    )

    if (-not (Test-Path -LiteralPath $DirectoryPath)) {
        return @()
    }

    return @(Get-ChildItem -LiteralPath $DirectoryPath -Filter $Filter -File -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)
}

function Read-JsonFile {
    param([string]$PathValue)

    return Get-Content -LiteralPath $PathValue -Raw | ConvertFrom-Json
}

$laneRoots = Get-FrameAngelPlayerLaneRoots -RepoRoot $RepoRoot -CallerScriptRoot $PSScriptRoot -EnsureAssetLaneScaffold
$RepoRoot = $laneRoots.RepoRoot
$versionState = Read-FrameAngelPlayerVersionState -RepoRoot $RepoRoot
$resolvedVersion = if ([string]::IsNullOrWhiteSpace($Version)) {
    $versionState.Version
}
else {
    $Version.Trim()
}

if ([string]::IsNullOrWhiteSpace($ReleaseRoot)) {
    $ReleaseRoot = Join-Path $laneRoots.AssetsPlayerBuildRoot (Join-Path "releases" $resolvedVersion)
}

if ([string]::IsNullOrWhiteSpace($RepoAssetPath)) {
    $RepoAssetPath = Join-Path $ReleaseRoot ("fa_player_asset.{0}.assetbundle" -f $resolvedVersion)
}

if ([string]::IsNullOrWhiteSpace($RepoPluginPath)) {
    $RepoPluginPath = Join-Path $ReleaseRoot ("fa_cua_player.{0}.dll" -f $resolvedVersion)
}

if ([string]::IsNullOrWhiteSpace($ChangelogSourcePath)) {
    $ChangelogSourcePath = if (($resolvedVersion -eq $versionState.Version) -and -not [string]::IsNullOrWhiteSpace($versionState.ChangelogPath)) {
        $versionState.ChangelogPath
    }
    else {
        Resolve-FrameAngelPlayerVersionChangelogPath -RepoRoot $RepoRoot -Version $resolvedVersion
    }
}

if ([string]::IsNullOrWhiteSpace($ChangelogJsonPath)) {
    $ChangelogJsonPath = Join-Path $ReleaseRoot "foundation_release_changelog.json"
}

if ([string]::IsNullOrWhiteSpace($ChangelogMarkdownPath)) {
    $ChangelogMarkdownPath = Join-Path $ReleaseRoot "foundation_release_changelog.md"
}

if ([string]::IsNullOrWhiteSpace($LiveAssetPath)) {
    $LiveAssetPath = Join-Path "F:\sim\vam\Custom\Assets\FrameAngel\Player" ("fa_player_asset.{0}.assetbundle" -f $resolvedVersion)
}

if ([string]::IsNullOrWhiteSpace($LivePluginPath)) {
    $LivePluginPath = Join-Path "F:\sim\vam\Custom\Plugins" ("fa_cua_player.{0}.dll" -f $resolvedVersion)
}

if ([string]::IsNullOrWhiteSpace($ReceiptPath)) {
    $ReceiptPath = Join-Path $ReleaseRoot "foundation_release_validation.json"
}

if ([string]::IsNullOrWhiteSpace($SummaryPath)) {
    $SummaryPath = Join-Path $ReleaseRoot "foundation_release_validation.md"
}

$legacyRepoPresetPath = Join-Path $ReleaseRoot ("Preset_FA Player Asset {0}.vap" -f $resolvedVersion)
$legacyLivePresetPath = Join-Path "F:\sim\vam\Custom\Atom\CustomUnityAsset" ("Preset_FA Player Asset {0}.vap" -f $resolvedVersion)
$legacyLiveAssetDllPath = Join-Path "F:\sim\vam\Custom\Assets\FrameAngel\Player" ("fa_cua_player.{0}.dll" -f $resolvedVersion)
$legacyLivePluginAliasPath = Join-Path "F:\sim\vam\Custom\Plugins" ("fa_player_plugin.{0}.dll" -f $resolvedVersion)
$livePresetMatches = Get-MatchingFiles -DirectoryPath "F:\sim\vam\Custom\Atom\CustomUnityAsset" -Filter "Preset_FA Player Asset *.vap"
$liveAssetDllMatches = Get-MatchingFiles -DirectoryPath "F:\sim\vam\Custom\Assets\FrameAngel\Player" -Filter "fa_cua_player.*.dll"
$livePluginAliasMatches = Get-MatchingFiles -DirectoryPath "F:\sim\vam\Custom\Plugins" -Filter "fa_player_plugin.*.dll"

$failures = New-Object System.Collections.Generic.List[object]
$checks = New-Object System.Collections.Generic.List[object]
$warnings = New-Object System.Collections.Generic.List[string]

$requiredPaths = @(
    $RepoAssetPath,
    $RepoPluginPath,
    $ChangelogSourcePath,
    $ChangelogJsonPath,
    $ChangelogMarkdownPath
)

if (-not $SkipLiveDeployChecks.IsPresent) {
    $requiredPaths += @(
        $LiveAssetPath,
        $LivePluginPath
    )
}

foreach ($requiredPath in $requiredPaths) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        Add-Failure -Failures $failures -Rule "required_path" -Message ("Missing required release artifact: " + $requiredPath)
    }
    else {
        Add-Check -Checks $checks -Rule "required_path" -Message ("Found required artifact: " + $requiredPath)
    }
}

$sourceChangelog = $null
if (Test-Path -LiteralPath $ChangelogSourcePath) {
    $sourceChangelog = Read-JsonFile -PathValue $ChangelogSourcePath
    $sourceChangelogVersion = [string]$sourceChangelog.version
    if ([string]::IsNullOrWhiteSpace($sourceChangelogVersion) -or $sourceChangelogVersion.Trim() -ne $resolvedVersion) {
        Add-Failure -Failures $failures -Rule "changelog_source_version" -Message ("Changelog source version does not match release version at " + $ChangelogSourcePath)
    }
    else {
        Add-Check -Checks $checks -Rule "changelog_source_version" -Message "Changelog source version matches the release version."
    }
}

$releaseChangelog = $null
if (Test-Path -LiteralPath $ChangelogJsonPath) {
    $releaseChangelog = Read-JsonFile -PathValue $ChangelogJsonPath
    $releaseChangelogVersion = [string]$releaseChangelog.version
    if ([string]::IsNullOrWhiteSpace($releaseChangelogVersion) -or $releaseChangelogVersion.Trim() -ne $resolvedVersion) {
        Add-Failure -Failures $failures -Rule "changelog_release_version" -Message ("Release changelog artifact version does not match release version at " + $ChangelogJsonPath)
    }
    else {
        Add-Check -Checks $checks -Rule "changelog_release_version" -Message "Release changelog artifact version matches the release version."
    }
}

if (($null -ne $sourceChangelog) -and ($null -ne $releaseChangelog)) {
    if (
        ([string]$sourceChangelog.title -ne [string]$releaseChangelog.title) -or
        ([string]$sourceChangelog.summary -ne [string]$releaseChangelog.summary) -or
        ([string]$sourceChangelog.reasoning -ne [string]$releaseChangelog.reasoning)
    ) {
        Add-Failure -Failures $failures -Rule "changelog_release_sync" -Message "Release changelog artifact does not match the tracked changelog source."
    }
    else {
        Add-Check -Checks $checks -Rule "changelog_release_sync" -Message "Release changelog artifact matches the tracked changelog source."
    }
}

$fileHashes = [ordered]@{}
foreach ($pair in @(
    @{ key = "repoAssetPath"; path = $RepoAssetPath },
    @{ key = "repoPluginPath"; path = $RepoPluginPath },
    @{ key = "liveAssetPath"; path = $LiveAssetPath },
    @{ key = "livePluginPath"; path = $LivePluginPath }
)) {
    if (Test-Path -LiteralPath $pair.path) {
        $fileHashes[$pair.key] = (Get-FileHash -LiteralPath $pair.path -Algorithm SHA256).Hash
    }
}

if (-not $SkipLiveDeployChecks.IsPresent -and $fileHashes.Contains("repoAssetPath") -and $fileHashes.Contains("liveAssetPath")) {
    if ($fileHashes["repoAssetPath"] -ne $fileHashes["liveAssetPath"]) {
        Add-Failure -Failures $failures -Rule "live_asset_hash" -Message "Live assetbundle does not match the repo release assetbundle."
    }
    else {
        Add-Check -Checks $checks -Rule "live_asset_hash" -Message "Live assetbundle matches the repo release assetbundle."
    }
}

if (-not $SkipLiveDeployChecks.IsPresent -and $fileHashes.Contains("repoPluginPath") -and $fileHashes.Contains("livePluginPath")) {
    if ($fileHashes["repoPluginPath"] -ne $fileHashes["livePluginPath"]) {
        Add-Failure -Failures $failures -Rule "live_plugin_hash" -Message "Live Custom/Plugins DLL does not match the repo release plugin DLL."
    }
    else {
        Add-Check -Checks $checks -Rule "live_plugin_hash" -Message "Live Custom/Plugins DLL matches the repo release plugin DLL."
    }
}

foreach ($legacyPath in @(
    @{ rule = "legacy_repo_preset"; path = $legacyRepoPresetPath; label = "Repo release preset" }
)) {
    if (Test-Path -LiteralPath $legacyPath.path) {
        Add-Failure -Failures $failures -Rule $legacyPath.rule -Message ($legacyPath.label + " is still present for this raw-CUA release seam: " + $legacyPath.path)
    }
    else {
        Add-Check -Checks $checks -Rule $legacyPath.rule -Message ($legacyPath.label + " is absent for this raw-CUA release seam.")
    }
}

if (-not $SkipLiveDeployChecks.IsPresent) {
    foreach ($legacyGroup in @(
        @{ rule = "legacy_live_preset"; matches = $livePresetMatches; label = "Live player presets" },
        @{ rule = "legacy_live_asset_dll"; matches = $liveAssetDllMatches; label = "Live asset-side player DLLs" },
        @{ rule = "legacy_plugin_alias"; matches = $livePluginAliasMatches; label = "Live fa_player_plugin aliases" }
    )) {
        if ($legacyGroup.matches.Count -gt 0) {
            Add-Failure -Failures $failures -Rule $legacyGroup.rule -Message ($legacyGroup.label + " are still present for this raw-CUA release seam: " + ($legacyGroup.matches -join "; "))
        }
        else {
            Add-Check -Checks $checks -Rule $legacyGroup.rule -Message ($legacyGroup.label + " are absent for this raw-CUA release seam.")
        }
    }
}

$warnings.Add("This validator now treats raw CustomUnityAsset loading plus one manual fa_cua_player attach as the authority seam for the screen-core lane.") | Out-Null
$warnings.Add("Phase 1 authority is the authored screen plus VaM controls that call exposed player methods directly; Meta UI components are not first-release proof.") | Out-Null
$warnings.Add("Visual correctness still requires live VaM closure and Volodeck comparator proof; this validator only keeps the release shape deterministic.") | Out-Null
$warnings.Add("If a future slice intentionally reintroduces asset.assetDllUrl or preset bootstrap, it should ship as a separate seam instead of silently mutating this one.") | Out-Null

$receipt = [ordered]@{
    schemaVersion = "frameangel_player_screen_core_release_validation_v4"
    validatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    version = $resolvedVersion
    repoAssetPath = $RepoAssetPath
    repoPluginPath = $RepoPluginPath
    changelogSourcePath = $ChangelogSourcePath
    changelogJsonPath = $ChangelogJsonPath
    changelogMarkdownPath = $ChangelogMarkdownPath
    liveAssetPath = $LiveAssetPath
    livePluginPath = $LivePluginPath
    releaseBoundary = [ordered]@{
        phase = "phase_1_screen_with_vam_controls"
        expectedControlSurface = "VaM buttons and sliders bound to exposed player methods"
        includesMetaUi = $false
        deterministicSceneWitness = "useful as a witness seam, but outside this validator's authority"
    }
    legacyRepoPresetPath = $legacyRepoPresetPath
    legacyLivePresetPath = $legacyLivePresetPath
    legacyLiveAssetDllPath = $legacyLiveAssetDllPath
    legacyLivePluginAliasPath = $legacyLivePluginAliasPath
    legacyLivePresetMatches = $livePresetMatches
    legacyLiveAssetDllMatches = $liveAssetDllMatches
    legacyLivePluginAliasMatches = $livePluginAliasMatches
    mirroredVamRules = @(
        "Load the raw versioned assetbundle directly in the CustomUnityAsset atom.",
        "Attach the matching fa_cua_player.<version>.dll manually from Custom/Plugins.",
        "Do not treat a same-version preset as part of the authority seam for this lane.",
        "Do not treat an asset-side DLL copy as part of the authority seam for this lane.",
        "The canonical player runtime DLL name stays fa_cua_player.<version>.dll."
    )
    checks = $checks.ToArray()
    warnings = $warnings.ToArray()
    failures = $failures.ToArray()
    passed = ($failures.Count -eq 0)
    hashes = $fileHashes
}

Write-JsonFile -Path $ReceiptPath -Value $receipt

$summaryLines = New-Object System.Collections.Generic.List[string]
$summaryLines.Add("# Foundation Release Validation") | Out-Null
$summaryLines.Add("") | Out-Null
$summaryLines.Add(("Version: ``{0}``" -f $resolvedVersion)) | Out-Null
$summaryLines.Add("") | Out-Null
$summaryLines.Add(("Passed: ``{0}``" -f $receipt.passed.ToString().ToLowerInvariant())) | Out-Null
$summaryLines.Add("") | Out-Null
$summaryLines.Add("## Failures") | Out-Null
if ($failures.Count -eq 0) {
    $summaryLines.Add("- none") | Out-Null
}
else {
    foreach ($failure in $failures) {
        $summaryLines.Add("- [" + $failure.rule + "] " + $failure.message) | Out-Null
    }
}
$summaryLines.Add("") | Out-Null
$summaryLines.Add("## Warnings") | Out-Null
foreach ($warning in $warnings) {
    $summaryLines.Add("- " + $warning) | Out-Null
}
$summaryLines.Add("") | Out-Null
$summaryLines.Add("## Passed Checks") | Out-Null
foreach ($check in $checks) {
    $summaryLines.Add("- [" + $check.rule + "] " + $check.message) | Out-Null
}
[System.IO.File]::WriteAllLines($SummaryPath, $summaryLines, (New-Object System.Text.UTF8Encoding($false)))

if ($failures.Count -gt 0) {
    $messages = $failures | ForEach-Object { " - [{0}] {1}" -f $_.rule, $_.message }
    throw ("Player screen-core release validation failed for {0}:`n{1}" -f $resolvedVersion, ($messages -join [Environment]::NewLine))
}

[pscustomobject]@{
    version = $resolvedVersion
    receiptPath = $ReceiptPath
    summaryPath = $SummaryPath
    passed = $true
}
