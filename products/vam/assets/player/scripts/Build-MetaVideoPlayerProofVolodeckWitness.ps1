param(
    [string]$RepoRoot = "",
    [string]$UnityEditorPath = "",
    [string]$OutputRoot = ""
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

function Invoke-UnityCapture {
    param(
        [string]$UnityEditor,
        [string]$ProjectPath,
        [string]$ExecuteMethod,
        [string]$CapturePath,
        [string]$CaptureMetaPath,
        [string]$LogPath
    )

    $args = @(
        "-batchmode",
        "-quit",
        "-projectPath", $ProjectPath,
        "-logFile", $LogPath,
        "-executeMethod", $ExecuteMethod,
        "-metaUiSetCapturePath", $CapturePath
    )

    if (-not [string]::IsNullOrWhiteSpace($CaptureMetaPath)) {
        $args += @("-metaUiSetCaptureMetaPath", $CaptureMetaPath)
    }

    $process = Start-Process -FilePath $UnityEditor -ArgumentList $args -PassThru -Wait
    $exitCode = $process.ExitCode

    try {
        Wait-ForPath -PathValue $CapturePath -Label "$ExecuteMethod capture image"
        if (-not [string]::IsNullOrWhiteSpace($CaptureMetaPath)) {
            Wait-ForPath -PathValue $CaptureMetaPath -Label "$ExecuteMethod capture metadata"
        }
    }
    catch {
        $logTail = Get-LogTail -PathValue $LogPath
        if ([string]::IsNullOrWhiteSpace($logTail)) {
            throw
        }

        throw ("Unity capture failed before artifacts appeared for $ExecuteMethod. Log tail:`n" + $logTail)
    }

    if ($exitCode -ne 0) {
        Write-Warning "Unity returned exit code $exitCode for $ExecuteMethod, but the expected capture artifacts were written. Continuing."
    }
}

$laneRoots = Get-FrameAngelPlayerLaneRoots -RepoRoot $RepoRoot -CallerScriptRoot $PSScriptRoot -EnsureAssetLaneScaffold
$toolkitProjectPath = Join-Path $laneRoots.AssetsPlayerUnityRoot "ghost_training_export_clone"
$UnityEditorPath = Resolve-UnityEditorPathForProject -ProjectPath $toolkitProjectPath -RequestedUnityEditorPath $UnityEditorPath
$resolvedOutputRoot = if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    Join-Path $laneRoots.AssetsPlayerBuildRoot "meta_video_player_proof_volodeck_witness"
}
else {
    [System.IO.Path]::GetFullPath($OutputRoot)
}

$scenePreviewPath = Join-Path $resolvedOutputRoot "video_player_proof_scene_preview.png"
$scenePreviewLogPath = Join-Path $resolvedOutputRoot "unity_scene_preview.log"
$surfacePreviewPath = Join-Path $resolvedOutputRoot "video_player_proof_surface_preview.png"
$surfacePreviewMetaPath = Join-Path $resolvedOutputRoot "video_player_proof_surface_preview.meta.json"
$surfacePreviewLogPath = Join-Path $resolvedOutputRoot "unity_surface_preview.log"
$receiptPath = Join-Path $resolvedOutputRoot "meta_video_player_proof_volodeck_witness_receipt.json"

if (Test-Path -LiteralPath $resolvedOutputRoot) {
    Remove-Item -LiteralPath $resolvedOutputRoot -Recurse -Force
}

Ensure-Directory -PathValue $resolvedOutputRoot
Assert-NoUnityProcess -Label "capturing the Meta video player proof Volodeck witness"

Invoke-UnityCapture `
    -UnityEditor $UnityEditorPath `
    -ProjectPath $toolkitProjectPath `
    -ExecuteMethod "GhostMetaUiSetBootstrap.CaptureVideoPlayerProofScenePreviewBatch" `
    -CapturePath $scenePreviewPath `
    -CaptureMetaPath "" `
    -LogPath $scenePreviewLogPath

Invoke-UnityCapture `
    -UnityEditor $UnityEditorPath `
    -ProjectPath $toolkitProjectPath `
    -ExecuteMethod "GhostMetaUiSetBootstrap.CaptureVideoPlayerProofSurfaceBatch" `
    -CapturePath $surfacePreviewPath `
    -CaptureMetaPath $surfacePreviewMetaPath `
    -LogPath $surfacePreviewLogPath

$surfaceMeta = Get-Content -LiteralPath $surfacePreviewMetaPath -Raw | ConvertFrom-Json
$surfaceMeta = Crop-PngToVisibleBounds -PngPath $surfacePreviewPath -CaptureMeta $surfaceMeta
Write-JsonFile -Path $surfacePreviewMetaPath -Value $surfaceMeta

$receipt = [ordered]@{
    schemaVersion = "frameangel_meta_video_player_proof_volodeck_witness_v1"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    unityEditorPath = $UnityEditorPath
    unityProjectPath = $toolkitProjectPath
    scenePreviewPath = $scenePreviewPath
    scenePreviewLogPath = $scenePreviewLogPath
    surfacePreviewPath = $surfacePreviewPath
    surfacePreviewMetaPath = $surfacePreviewMetaPath
    surfacePreviewLogPath = $surfacePreviewLogPath
    surfaceWidthMeters = if ($surfaceMeta) { [double]$surfaceMeta.surfaceWidthMeters } else { 0.0 }
    surfaceHeightMeters = if ($surfaceMeta) { [double]$surfaceMeta.surfaceHeightMeters } else { 0.0 }
    textureWidth = if ($surfaceMeta) { [int]$surfaceMeta.textureWidth } else { 0 }
    textureHeight = if ($surfaceMeta) { [int]$surfaceMeta.textureHeight } else { 0 }
}

Write-JsonFile -Path $receiptPath -Value $receipt

[pscustomobject]@{
    outputRoot = $resolvedOutputRoot
    scenePreviewPath = $scenePreviewPath
    surfacePreviewPath = $surfacePreviewPath
    surfacePreviewMetaPath = $surfacePreviewMetaPath
    receiptPath = $receiptPath
}
