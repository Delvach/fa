param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$ConfigPath = (Join-Path $PSScriptRoot "vam-forbidden-terms.json"),
    [string]$GeneratedSourcePath = "",
    [string[]]$TargetFiles = @()
)

$ErrorActionPreference = "Stop"

function Resolve-ExistingPath([string]$path, [string]$label) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "$label not found: $path"
    }
    return (Resolve-Path -LiteralPath $path).Path
}

function Resolve-PathFromRepo([string]$repoRootPath, [string]$candidatePath, [string]$label) {
    if ([string]::IsNullOrWhiteSpace($candidatePath)) {
        throw "$label cannot be empty."
    }

    if ([System.IO.Path]::IsPathRooted($candidatePath)) {
        return Resolve-ExistingPath $candidatePath $label
    }

    $combined = Join-Path $repoRootPath $candidatePath
    return Resolve-ExistingPath $combined $label
}

function Validate-RuntimeProfiles {
    param([object]$runtimeProfilesRoot)

    if ($null -eq $runtimeProfilesRoot) {
        throw "Forbidden terms config is missing required 'runtimeProfiles' section."
    }

    $profiles = $runtimeProfilesRoot.profiles
    if ($null -eq $profiles -or $profiles.Count -eq 0) {
        throw "Forbidden terms config runtimeProfiles must include at least one profile."
    }

    $ids = New-Object System.Collections.Generic.List[string]
    $idSet = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($profile in $profiles) {
        $id = [string]$profile.id
        if ([string]::IsNullOrWhiteSpace($id)) {
            throw "Each runtime profile must include a non-empty 'id'."
        }

        if (-not $idSet.Add($id)) {
            throw "Duplicate runtime profile id: '$id'."
        }

        $knownDifferences = $profile.knownDifferences
        if ($null -eq $knownDifferences -or $knownDifferences.Count -eq 0) {
            throw "Runtime profile '$id' must include at least one knownDifferences entry."
        }

        $ids.Add($id)
    }

    return @{
        Count = $ids.Count
        Ids = $ids.ToArray()
    }
}

$repoRootResolved = Resolve-ExistingPath $RepoRoot "Repo root"
$configPathResolved = Resolve-ExistingPath $ConfigPath "Forbidden terms config"

$configRaw = Get-Content -LiteralPath $configPathResolved -Raw
$config = $configRaw | ConvertFrom-Json

if ($null -eq $config.rules -or $config.rules.Count -eq 0) {
    throw "Forbidden terms config has no rules: $configPathResolved"
}

$runtimeProfileSummary = Validate-RuntimeProfiles -runtimeProfilesRoot $config.runtimeProfiles
Write-Host ("VaM runtime profiles: " + $runtimeProfileSummary.Count + " (" + [string]::Join(", ", $runtimeProfileSummary.Ids) + ")")

$filesToScan = New-Object System.Collections.Generic.List[string]
$targetPathSet = New-Object System.Collections.Generic.HashSet[string]

if ($null -ne $TargetFiles -and $TargetFiles.Count -gt 0) {
    foreach ($target in $TargetFiles) {
        if ([string]::IsNullOrWhiteSpace($target)) {
            continue
        }

        $resolvedTarget = Resolve-PathFromRepo $repoRootResolved $target "Target file"
        if ($targetPathSet.Add($resolvedTarget)) {
            $filesToScan.Add($resolvedTarget)
        }
    }
}
else {
    $sourceFiles = Get-ChildItem -LiteralPath $repoRootResolved -Filter "*.cs" -File -Recurse
    foreach ($f in $sourceFiles) {
        if ($f.FullName -like "*\generated\*") {
            continue
        }
        if ($f.Name -like "*_COMBINED.cs") {
            continue
        }

        if ($targetPathSet.Add($f.FullName)) {
            $filesToScan.Add($f.FullName)
        }
    }
}

if (-not [string]::IsNullOrWhiteSpace($GeneratedSourcePath)) {
    $generatedResolved = if ([System.IO.Path]::IsPathRooted($GeneratedSourcePath)) {
        [System.IO.Path]::GetFullPath($GeneratedSourcePath)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $repoRootResolved $GeneratedSourcePath))
    }

    if (Test-Path -LiteralPath $generatedResolved) {
        $resolvedGenerated = (Resolve-Path -LiteralPath $generatedResolved).Path
        if ($targetPathSet.Add($resolvedGenerated)) {
            $filesToScan.Add($resolvedGenerated)
        }
    }
}

if ($filesToScan.Count -eq 0) {
    Write-Host "VaM guard: no C# files to validate."
    exit 0
}

$violations = New-Object System.Collections.Generic.List[object]

foreach ($filePath in $filesToScan) {
    $lines = Get-Content -LiteralPath $filePath
    for ($lineIndex = 0; $lineIndex -lt $lines.Count; $lineIndex++) {
        $line = $lines[$lineIndex]
        foreach ($rule in $config.rules) {
            $pattern = [string]$rule.pattern
            if ([string]::IsNullOrWhiteSpace($pattern)) {
                continue
            }

            if ([regex]::IsMatch($line, $pattern)) {
                $violations.Add([pscustomobject]@{
                    RuleId = [string]$rule.id
                    File = $filePath
                    Line = $lineIndex + 1
                    Text = $line.Trim()
                    Reason = [string]$rule.reason
                })
            }
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Host "VaM guard FAILED: forbidden symbol usage found." -ForegroundColor Red
    $grouped = $violations | Group-Object RuleId
    foreach ($group in $grouped) {
        Write-Host ""
        Write-Host ("Rule: " + $group.Name) -ForegroundColor Yellow
        foreach ($v in $group.Group) {
            Write-Host ("  " + $v.File + ":" + $v.Line + " | " + $v.Text)
            if (-not [string]::IsNullOrWhiteSpace($v.Reason)) {
                Write-Host ("    Reason: " + $v.Reason)
            }
        }
    }
    throw "VaM forbidden symbol validation failed with $($violations.Count) violation(s)."
}

Write-Host ("VaM guard OK: " + $filesToScan.Count + " file(s) scanned, 0 violations.")
