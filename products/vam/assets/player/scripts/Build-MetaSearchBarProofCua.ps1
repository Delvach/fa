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

function Crop-PngToVisibleBounds {
    param(
        [string]$PngPath,
        [object]$CaptureMeta
    )

    Add-Type -AssemblyName System.Drawing

    $bitmap = [System.Drawing.Bitmap]::FromFile($PngPath)
    $replacementPath = ""
    $resultMeta = $CaptureMeta
    try {
        $minX = $bitmap.Width
        $minY = $bitmap.Height
        $maxX = -1
        $maxY = -1

        for ($y = 0; $y -lt $bitmap.Height; $y++) {
            for ($x = 0; $x -lt $bitmap.Width; $x++) {
                $pixel = $bitmap.GetPixel($x, $y)
                if ($pixel.A -le 0) {
                    continue
                }

                if ($x -lt $minX) { $minX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }

        if ($maxX -lt $minX -or $maxY -lt $minY) {
            return $resultMeta
        }

        $cropWidth = $maxX - $minX + 1
        $cropHeight = $maxY - $minY + 1
        if ($cropWidth -eq $bitmap.Width -and $cropHeight -eq $bitmap.Height) {
            return $resultMeta
        }

        $cropped = New-Object System.Drawing.Bitmap($cropWidth, $cropHeight, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($cropped)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.DrawImage(
                    $bitmap,
                    (New-Object System.Drawing.Rectangle(0, 0, $cropWidth, $cropHeight)),
                    (New-Object System.Drawing.Rectangle($minX, $minY, $cropWidth, $cropHeight)),
                    [System.Drawing.GraphicsUnit]::Pixel
                )
            }
            finally {
                $graphics.Dispose()
            }

            $tempCroppedPath = $PngPath + ".cropped"
            if (Test-Path -LiteralPath $tempCroppedPath) {
                Remove-Item -LiteralPath $tempCroppedPath -Force
            }

            $cropped.Save($tempCroppedPath, [System.Drawing.Imaging.ImageFormat]::Png)
            $replacementPath = $tempCroppedPath
        }
        finally {
            $cropped.Dispose()
        }

        if ($null -ne $resultMeta -and $resultMeta.textureWidth -gt 0 -and $resultMeta.textureHeight -gt 0) {
            $resultMeta.surfaceWidthMeters = [double]$resultMeta.surfaceWidthMeters * ($cropWidth / [double]$resultMeta.textureWidth)
            $resultMeta.surfaceHeightMeters = [double]$resultMeta.surfaceHeightMeters * ($cropHeight / [double]$resultMeta.textureHeight)
            $resultMeta.textureWidth = $cropWidth
            $resultMeta.textureHeight = $cropHeight
        }
    }
    finally {
        $bitmap.Dispose()
    }

    if (-not [string]::IsNullOrWhiteSpace($replacementPath)) {
        if (Test-Path -LiteralPath $PngPath) {
            Remove-Item -LiteralPath $PngPath -Force
        }

        Move-Item -LiteralPath $replacementPath -Destination $PngPath -Force
    }

    return $resultMeta
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

function Assert-NoUnityProcess {
    param([string]$Label)

    $unityProcesses = @(Get-Process Unity -ErrorAction SilentlyContinue)
    if ($unityProcesses.Count -le 0) {
        return
    }

    $processList = ($unityProcesses | ForEach-Object {
        if ($_.Path) { "{0} ({1})" -f $_.Id, $_.Path } else { [string]$_.Id }
    }) -join ", "
    throw "Unity is already running. Close it before $Label. Active process(es): $processList"
}

function Resolve-MetaSurfacePackageRoot {
    param(
        [string]$SurfaceRoot,
        [string]$SummaryPath,
        [string]$ExpectedControlSurfaceId,
        [string]$ExpectedControlFamilyId
    )

    if (-not [string]::IsNullOrWhiteSpace($SummaryPath) -and (Test-Path -LiteralPath $SummaryPath)) {
        $summary = Get-Content -LiteralPath $SummaryPath -Raw | ConvertFrom-Json
        $match = @($summary.surfaces | Where-Object {
            $_ -and (
                [string]::Equals([string]$_.controlSurfaceId, $ExpectedControlSurfaceId, [System.StringComparison]::OrdinalIgnoreCase) -or
                [string]::Equals([string]$_.controlFamilyId, $ExpectedControlFamilyId, [System.StringComparison]::OrdinalIgnoreCase)
            )
        } | Select-Object -First 1)
        if ($match.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace([string]$match[0].packageRootPath)) {
            return [System.IO.Path]::GetFullPath([string]$match[0].packageRootPath)
        }
    }

    $candidates = @()
    Get-ChildItem -LiteralPath $SurfaceRoot -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        $controlsPath = Join-Path $_.FullName "controls.innerpiece.json"
        $receiptPath = Join-Path $_.FullName "export_receipt.json"
        if (-not (Test-Path -LiteralPath $controlsPath)) {
            return
        }

        $controls = Get-Content -LiteralPath $controlsPath -Raw | ConvertFrom-Json
        $matches = [string]::Equals([string]$controls.controlSurfaceId, $ExpectedControlSurfaceId, [System.StringComparison]::OrdinalIgnoreCase) -or
            [string]::Equals([string]$controls.controlFamilyId, $ExpectedControlFamilyId, [System.StringComparison]::OrdinalIgnoreCase)
        if (-not $matches) {
            return
        }

        $exportedAtUtc = ""
        if (Test-Path -LiteralPath $receiptPath) {
            $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
            $exportedAtUtc = [string]$receipt.exportedAtUtc
        }

        $candidates += [pscustomobject]@{
            packageRootPath = $_.FullName
            exportedAtUtc = $exportedAtUtc
        }
    }

    $resolved = @($candidates |
        Sort-Object -Property @{
            Expression = {
                if ([string]::IsNullOrWhiteSpace($_.exportedAtUtc)) { [datetime]::MinValue }
                else { [datetime]::Parse($_.exportedAtUtc, [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::RoundtripKind) }
            }
        }, @{
            Expression = { $_.packageRootPath }
        } -Descending |
        Select-Object -First 1)

    if ($resolved.Count -gt 0) {
        return [System.IO.Path]::GetFullPath([string]$resolved[0].packageRootPath)
    }

    return ""
}

$laneRoots = Get-FrameAngelPlayerLaneRoots -RepoRoot $RepoRoot -CallerScriptRoot $PSScriptRoot -EnsureAssetLaneScaffold
$resolvedOutputRoot = if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    Join-Path $laneRoots.AssetsPlayerBuildRoot "meta_search_bar_cua"
}
else {
    [System.IO.Path]::GetFullPath($OutputRoot)
}

$toolkitProjectPath = Join-Path $laneRoots.AssetsPlayerUnityRoot "ghost_training_export_clone"
$toolkitUnityEditorPath = Resolve-UnityEditorPathForProject -ProjectPath $toolkitProjectPath -RequestedUnityEditorPath ""
$surfaceRoot = Join-Path $laneRoots.AssetsPlayerBuildRoot "meta_toolkit_catalog\theme_00\meta_textinputfield_searchbar_721072e4"
$summaryPath = Join-Path $laneRoots.AssetsPlayerBuildRoot "meta_toolkit_catalog\theme_00\ghost_meta_ui_toolkit_export_summary_theme_00.json"
$templatePackageRootPath = Resolve-MetaSurfacePackageRoot `
    -SurfaceRoot $surfaceRoot `
    -SummaryPath $summaryPath `
    -ExpectedControlSurfaceId "meta_textinputfield_searchbar_721072e4" `
    -ExpectedControlFamilyId "meta_ui_search_bar"
if ([string]::IsNullOrWhiteSpace($templatePackageRootPath)) {
    throw "Search bar surface package was not found under: $surfaceRoot"
}

$curatedPackageRoot = Join-Path $laneRoots.AssetsPlayerBuildRoot "meta_search_bar_curated_surface\fa_meta_searchbar_curated"
$capturePath = Join-Path $curatedPackageRoot "search_bar_capture.png"
$captureMetaPath = Join-Path $curatedPackageRoot "search_bar_capture_meta.json"
$captureLogPath = Join-Path $curatedPackageRoot "unity_capture.log"
$curatedSummaryPath = Join-Path $curatedPackageRoot "curated_surface_summary.json"
$controlsTemplatePath = Join-Path $templatePackageRootPath "controls.innerpiece.json"
$materialsTemplatePath = Join-Path $templatePackageRootPath "materials.innerpiece.json"

if (-not (Test-Path -LiteralPath $controlsTemplatePath)) {
    throw "Search bar controls template not found: $controlsTemplatePath"
}
if (-not (Test-Path -LiteralPath $materialsTemplatePath)) {
    throw "Search bar materials template not found: $materialsTemplatePath"
}

if (Test-Path -LiteralPath $curatedPackageRoot) {
    Remove-Item -LiteralPath $curatedPackageRoot -Recurse -Force
}
Ensure-Directory -PathValue $curatedPackageRoot

Assert-NoUnityProcess -Label "capturing the curated Meta search-bar witness"
$captureArgs = @(
    "-batchmode",
    "-quit",
    "-projectPath", $toolkitProjectPath,
    "-logFile", $captureLogPath,
    "-executeMethod", "GhostMetaUiSetBootstrap.CaptureFlatGalleryElementBatch",
    "-metaUiSetElementName", "SearchBar",
    "-metaUiSetCapturePath", $capturePath,
    "-metaUiSetCaptureMetaPath", $captureMetaPath
)

$captureProcess = Start-Process -FilePath $toolkitUnityEditorPath -ArgumentList $captureArgs -PassThru -Wait
$captureExitCode = $captureProcess.ExitCode
try {
    Wait-ForPath -PathValue $capturePath -Label "Curated search-bar capture image"
    Wait-ForPath -PathValue $captureMetaPath -Label "Curated search-bar capture metadata"
}
catch {
    $logTail = Get-LogTail -PathValue $captureLogPath
    if ([string]::IsNullOrWhiteSpace($logTail)) {
        throw
    }

    throw ("Unity Meta search-bar capture failed before artifacts appeared. Log tail:`n" + $logTail)
}

if ($captureExitCode -ne 0) {
    Write-Warning "Unity returned exit code $captureExitCode while capturing the search-bar witness, but the expected capture artifacts were written. Continuing."
}

$captureMeta = Get-Content -LiteralPath $captureMetaPath -Raw | ConvertFrom-Json
$captureMeta = Crop-PngToVisibleBounds -PngPath $capturePath -CaptureMeta $captureMeta
Write-JsonFile -Path $captureMetaPath -Value $captureMeta
$controlsTemplate = Get-Content -LiteralPath $controlsTemplatePath -Raw | ConvertFrom-Json
$materialsTemplate = Get-Content -LiteralPath $materialsTemplatePath -Raw | ConvertFrom-Json
$capturePngBase64 = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($capturePath))

if ($null -eq $controlsTemplate) {
    throw "Curated search-bar controls template could not be parsed: $controlsTemplatePath"
}
if ($null -eq $materialsTemplate) {
    throw "Curated search-bar materials template could not be parsed: $materialsTemplatePath"
}

if ($captureMeta -and $captureMeta.surfaceWidthMeters -gt 0) {
    $controlsTemplate.surfaceWidthMeters = [double]$captureMeta.surfaceWidthMeters
}
if ($captureMeta -and $captureMeta.surfaceHeightMeters -gt 0) {
    $controlsTemplate.surfaceHeightMeters = [double]$captureMeta.surfaceHeightMeters
}

$materialRefId = "fa_meta_search_bar_curated_snapshot"
if ($materialsTemplate.materials -and $materialsTemplate.materials.Count -gt 0 -and $materialsTemplate.materials[0].materialRefId) {
    $materialRefId = [string]$materialsTemplate.materials[0].materialRefId
}

$curatedMaterials = [ordered]@{
    schemaVersion = "innerpiece_materials_v1"
    materials = @(
        [ordered]@{
            materialRefId = $materialRefId
            displayName = "SearchBar Curated Snapshot"
            shaderName = "Unlit/Texture"
            baseColorHex = "#FFFFFF"
            textureAssetPath = ""
            texturePngBase64 = $capturePngBase64
            featureFlags = @(
                "base_color",
                "texture_ref_present",
                "inline_texture_png_base64",
                "control_surface_canvas_snapshot"
            )
            warnings = @()
        }
    )
}

Write-JsonFile -Path (Join-Path $curatedPackageRoot "controls.innerpiece.json") -Value $controlsTemplate
Write-JsonFile -Path (Join-Path $curatedPackageRoot "materials.innerpiece.json") -Value $curatedMaterials
Write-JsonFile -Path $curatedSummaryPath -Value ([ordered]@{
    schemaVersion = "frameangel_meta_search_bar_curated_surface_v1"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    templatePackageRootPath = $templatePackageRootPath
    templateControlsPath = $controlsTemplatePath
    templateMaterialsPath = $materialsTemplatePath
    capturePath = $capturePath
    captureMetaPath = $captureMetaPath
    captureLogPath = $captureLogPath
    curatedPackageRootPath = $curatedPackageRoot
    elementName = "SearchBar"
    textureWidth = if ($captureMeta) { [int]$captureMeta.textureWidth } else { 0 }
    textureHeight = if ($captureMeta) { [int]$captureMeta.textureHeight } else { 0 }
    surfaceWidthMeters = if ($captureMeta) { [double]$captureMeta.surfaceWidthMeters } else { 0.0 }
    surfaceHeightMeters = if ($captureMeta) { [double]$captureMeta.surfaceHeightMeters } else { 0.0 }
})

$commonArgs = @{
    RepoRoot = $RepoRoot
    UnityEditorPath = $UnityEditorPath
    PackageRootPath = $curatedPackageRoot
    OutputRoot = $resolvedOutputRoot
    DeployAssetsRoot = $DeployAssetsRoot
    DeployPresetRoot = $DeployPresetRoot
    ResourceId = "fa_meta_search_bar_proof"
    BundleFileName = "fa_meta_search_bar_proof.assetbundle"
    PresetFileName = "Preset_FA Meta Search Bar Proof.vap"
    SummaryFileName = "meta_search_bar_proof_cua_export_summary.json"
    BuildProfile = $BuildProfile
}

if ($SkipDeploy.IsPresent) {
    $commonArgs.SkipDeploy = $true
}

& (Join-Path $PSScriptRoot "Build-MetaControlSurfaceProofCua.ps1") @commonArgs
