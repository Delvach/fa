function Get-FrameAngelPlayerVersionManifestPath {
    param(
        [string]$RepoRoot
    )

    if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
        throw "RepoRoot is required."
    }

    return Join-Path $RepoRoot "products\vam\assets\player\player.version.json"
}

function Resolve-FrameAngelPlayerVersionChangelogPath {
    param(
        [string]$RepoRoot,
        [string]$Version,
        [string]$ManifestRelativePath = ""
    )

    if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
        throw "RepoRoot is required."
    }

    if (-not [string]::IsNullOrWhiteSpace($ManifestRelativePath)) {
        if ([System.IO.Path]::IsPathRooted($ManifestRelativePath)) {
            return [System.IO.Path]::GetFullPath($ManifestRelativePath)
        }

        return [System.IO.Path]::GetFullPath((Join-Path $RepoRoot $ManifestRelativePath))
    }

    if ([string]::IsNullOrWhiteSpace($Version)) {
        throw "Version is required when changelogPath is not present in player.version.json."
    }

    return Join-Path $RepoRoot ("products\vam\assets\player\changelog\{0}.json" -f $Version.Trim())
}

function Read-FrameAngelPlayerVersionState {
    param(
        [string]$RepoRoot
    )

    $manifestPath = Get-FrameAngelPlayerVersionManifestPath -RepoRoot $RepoRoot
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        throw "Player version manifest not found: $manifestPath"
    }

    $state = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $version = [string]$state.version
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "Player version manifest is missing version: $manifestPath"
    }

    $resolvedVersion = $version.Trim()
    $changelogPath = Resolve-FrameAngelPlayerVersionChangelogPath `
        -RepoRoot $RepoRoot `
        -Version $resolvedVersion `
        -ManifestRelativePath ([string]$state.changelogPath)

    return [pscustomobject]@{
        ManifestPath = $manifestPath
        Version = $resolvedVersion
        Notes = [string]$state.notes
        ChangelogPath = $changelogPath
    }
}

function Test-FrameAngelPlayerVersionCollisions {
    param(
        [string]$RepoRoot,
        [string]$Version,
        [string[]]$ArtifactPaths
    )

    $paths = New-Object System.Collections.Generic.List[string]
    foreach ($artifactPath in @($ArtifactPaths)) {
        if ([string]::IsNullOrWhiteSpace($artifactPath)) {
            continue
        }

        $paths.Add([System.IO.Path]::GetFullPath($artifactPath)) | Out-Null
    }

    $existing = New-Object System.Collections.Generic.List[string]
    foreach ($pathValue in $paths) {
        if (Test-Path -LiteralPath $pathValue) {
            $existing.Add($pathValue) | Out-Null
        }
    }

    return [pscustomobject]@{
        Version = $Version
        CheckedPaths = $paths.ToArray()
        ExistingPaths = $existing.ToArray()
        HasCollisions = ($existing.Count -gt 0)
    }
}

function Assert-FrameAngelPlayerVersionAvailable {
    param(
        [string]$RepoRoot,
        [string]$Version,
        [string[]]$ArtifactPaths,
        [string]$ContextLabel = "player build"
    )

    $result = Test-FrameAngelPlayerVersionCollisions -RepoRoot $RepoRoot -Version $Version -ArtifactPaths $ArtifactPaths
    if (-not $result.HasCollisions) {
        return $result
    }

    $existingList = ($result.ExistingPaths | ForEach-Object { " - $_" }) -join [Environment]::NewLine
    throw "Version $Version is already present for $ContextLabel. Bump player.version.json or explicitly opt out before regenerating.`n$existingList"
}
