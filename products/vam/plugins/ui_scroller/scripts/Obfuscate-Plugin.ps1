param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$PluginKey = "",
    [string]$InputAssemblyPath,
    [string]$OutputAssemblyPath,
    [string]$ConfigPath = "config/obfuscation.defaults.json",
    [string]$Enabled = "",
    [string]$Profile = "",
    [string[]]$KeepType = @(),
    [string[]]$ReferenceSearchPath = @()
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

function ConvertTo-Hashtable {
    param([object]$InputObject)

    if ($null -eq $InputObject) {
        return $null
    }

    if ($InputObject -is [System.Collections.IDictionary]) {
        $table = @{}
        foreach ($key in $InputObject.Keys) {
            $table[$key] = ConvertTo-Hashtable -InputObject $InputObject[$key]
        }
        return $table
    }

    if ($InputObject -is [System.Management.Automation.PSCustomObject]) {
        $table = @{}
        foreach ($prop in $InputObject.PSObject.Properties) {
            $table[$prop.Name] = ConvertTo-Hashtable -InputObject $prop.Value
        }
        return $table
    }

    if (
        ($InputObject -is [System.Collections.IEnumerable]) -and
        (-not ($InputObject -is [string]))
    ) {
        $list = New-Object System.Collections.ArrayList
        foreach ($item in $InputObject) {
            [void]$list.Add((ConvertTo-Hashtable -InputObject $item))
        }
        return , $list.ToArray()
    }

    return $InputObject
}

function Load-JsonFileAsHashtable {
    param([string]$PathValue)

    $raw = Get-Content -LiteralPath $PathValue -Raw
    $obj = $raw | ConvertFrom-Json
    $table = ConvertTo-Hashtable -InputObject $obj
    if (-not ($table -is [hashtable])) {
        throw "JSON file did not parse into an object: $PathValue"
    }
    return $table
}

function ConvertTo-Bool {
    param(
        [object]$RawValue,
        [bool]$DefaultValue = $false
    )

    if ($null -eq $RawValue) {
        return $DefaultValue
    }

    if ($RawValue -is [bool]) {
        return $RawValue
    }

    if ($RawValue -is [int] -or $RawValue -is [long]) {
        return ($RawValue -ne 0)
    }

    $asString = ([string]$RawValue).Trim()
    switch -Regex ($asString) {
        '^(1|true|yes|on)$' { return $true }
        '^(0|false|no|off)$' { return $false }
    }

    $parsed = $false
    if ([bool]::TryParse($asString, [ref]$parsed)) {
        return $parsed
    }

    return [System.Convert]::ToBoolean($asString)
}

function ConvertTo-StringArray {
    param([object]$RawValue)

    if ($null -eq $RawValue) {
        return @()
    }

    if (-not ($RawValue -is [System.Array])) {
        throw "Expected an array value."
    }

    $list = New-Object System.Collections.Generic.List[string]
    foreach ($item in $RawValue) {
        $value = [string]$item
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            $list.Add($value.Trim())
        }
    }

    return $list.ToArray()
}

function Add-UniqueStrings {
    param(
        [System.Collections.Generic.List[string]]$List,
        [System.Collections.Generic.HashSet[string]]$Seen,
        [string[]]$Values
    )

    if ($null -eq $Values) {
        return
    }

    foreach ($value in $Values) {
        if ([string]::IsNullOrWhiteSpace($value)) {
            continue
        }

        if ($Seen.Add($value)) {
            $List.Add($value)
        }
    }
}

function Escape-Xml {
    param([string]$Value)

    if ($null -eq $Value) {
        return ""
    }

    return [System.Security.SecurityElement]::Escape($Value)
}

function Ensure-ObfuscarTool {
    param(
        [string]$ToolPath,
        [string]$PackageName,
        [string]$PackageVersion
    )

    if (-not (Test-Path -LiteralPath $ToolPath)) {
        New-Item -ItemType Directory -Path $ToolPath -Force | Out-Null
    }

    $toolExe = Join-Path $ToolPath "obfuscar.console.exe"
    if (Test-Path -LiteralPath $toolExe) {
        return $toolExe
    }

    Write-Host "Installing obfuscation tool '$PackageName' ($PackageVersion) to $ToolPath"
    $installArgs = @(
        "tool",
        "install",
        "--tool-path",
        $ToolPath,
        $PackageName
    )

    if (-not [string]::IsNullOrWhiteSpace($PackageVersion)) {
        $installArgs += "--version"
        $installArgs += $PackageVersion
    }

    $installOutput = & dotnet @installArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to install obfuscation tool '$PackageName' (exit code $LASTEXITCODE)."
    }

    if ($null -ne $installOutput) {
        foreach ($line in $installOutput) {
            if (-not [string]::IsNullOrWhiteSpace([string]$line)) {
                Write-Host $line
            }
        }
    }

    if (-not (Test-Path -LiteralPath $toolExe)) {
        throw "Obfuscation tool executable not found after install: $toolExe"
    }

    return $toolExe
}

function Write-ObfuscationReport {
    param(
        [string]$ReportPath,
        [string]$PluginKeyValue,
        [string]$ProfileName,
        [bool]$EnabledValue,
        [string]$InputPath,
        [string]$OutputPath,
        [string]$ToolVersion,
        [string[]]$KeepTypes
    )

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("timestamp=" + (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssK"))
    $lines.Add("plugin=" + $PluginKeyValue)
    $lines.Add("profile=" + $ProfileName)
    $lines.Add("enabled=" + $EnabledValue.ToString().ToLowerInvariant())
    $lines.Add("toolVersion=" + $ToolVersion)
    $lines.Add("input=" + $InputPath)
    $lines.Add("output=" + $OutputPath)
    $lines.Add("keepTypes=" + [string]::Join(",", $KeepTypes))
    Set-Content -LiteralPath $ReportPath -Value $lines.ToArray() -Encoding ASCII
}

$repoRootResolved = Resolve-PathFromBase -PathValue $RepoRoot -BasePath (Get-Location).Path -Label "Repo root"
$inputAssemblyResolved = Resolve-PathFromBase -PathValue $InputAssemblyPath -BasePath $repoRootResolved -Label "Input assembly"
$outputAssemblyResolved = Resolve-PathFromBase -PathValue $OutputAssemblyPath -BasePath $repoRootResolved -Label "Output assembly" -MustExist $false
$configResolved = Resolve-PathFromBase -PathValue $ConfigPath -BasePath $repoRootResolved -Label "Obfuscation config"

$config = Load-JsonFileAsHashtable -PathValue $configResolved

$toolConfig = @{}
if ($config.ContainsKey("tool")) {
    if (-not ($config["tool"] -is [hashtable])) {
        throw "'tool' section in obfuscation config must be an object."
    }
    $toolConfig = $config["tool"]
}

$defaultsConfig = @{}
if ($config.ContainsKey("defaults")) {
    if (-not ($config["defaults"] -is [hashtable])) {
        throw "'defaults' section in obfuscation config must be an object."
    }
    $defaultsConfig = $config["defaults"]
}

$profilesConfig = @{}
if ($config.ContainsKey("profiles")) {
    if (-not ($config["profiles"] -is [hashtable])) {
        throw "'profiles' section in obfuscation config must be an object."
    }
    $profilesConfig = $config["profiles"]
}

$pluginsConfig = @{}
if ($config.ContainsKey("plugins")) {
    if (-not ($config["plugins"] -is [hashtable])) {
        throw "'plugins' section in obfuscation config must be an object."
    }
    $pluginsConfig = $config["plugins"]
}

$packageName = "Obfuscar.GlobalTool"
if ($toolConfig.ContainsKey("package")) {
    $packageName = [string]$toolConfig["package"]
}

$packageVersion = ""
if ($toolConfig.ContainsKey("version")) {
    $packageVersion = [string]$toolConfig["version"]
}

$toolPathValue = "tools/obfuscar"
if ($toolConfig.ContainsKey("toolPath")) {
    $toolPathValue = [string]$toolConfig["toolPath"]
}
$toolPathResolved = Resolve-PathFromBase -PathValue $toolPathValue -BasePath $repoRootResolved -Label "Obfuscation tool path" -MustExist $false

$pluginConfig = @{}
if (-not [string]::IsNullOrWhiteSpace($PluginKey)) {
    if ($pluginsConfig.ContainsKey($PluginKey)) {
        if (-not ($pluginsConfig[$PluginKey] -is [hashtable])) {
            throw "Obfuscation plugin config for '$PluginKey' must be an object."
        }
        $pluginConfig = $pluginsConfig[$PluginKey]
    }
}

$effectiveEnabled = ConvertTo-Bool -RawValue $defaultsConfig["enabled"] -DefaultValue $true
if ($pluginConfig.ContainsKey("enabled")) {
    $effectiveEnabled = ConvertTo-Bool -RawValue $pluginConfig["enabled"] -DefaultValue $effectiveEnabled
}
if (-not [string]::IsNullOrWhiteSpace($Enabled)) {
    $effectiveEnabled = ConvertTo-Bool -RawValue $Enabled -DefaultValue $effectiveEnabled
}

$effectiveProfile = "safe"
if ($defaultsConfig.ContainsKey("profile")) {
    $effectiveProfile = [string]$defaultsConfig["profile"]
}
if ($pluginConfig.ContainsKey("profile")) {
    $effectiveProfile = [string]$pluginConfig["profile"]
}
if (-not [string]::IsNullOrWhiteSpace($Profile)) {
    $effectiveProfile = $Profile
}
if ([string]::IsNullOrWhiteSpace($effectiveProfile)) {
    $effectiveProfile = "safe"
}

if (-not $profilesConfig.ContainsKey($effectiveProfile)) {
    throw "Obfuscation profile '$effectiveProfile' is not defined in $configResolved."
}

$profileConfig = $profilesConfig[$effectiveProfile]
if (-not ($profileConfig -is [hashtable])) {
    throw "Obfuscation profile '$effectiveProfile' must be an object."
}

$keepTypesList = New-Object System.Collections.Generic.List[string]
$keepTypesSeen = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::Ordinal)
if ($profileConfig.ContainsKey("keepTypes")) {
    Add-UniqueStrings -List $keepTypesList -Seen $keepTypesSeen -Values (ConvertTo-StringArray -RawValue $profileConfig["keepTypes"])
}
if ($pluginConfig.ContainsKey("keepTypes")) {
    Add-UniqueStrings -List $keepTypesList -Seen $keepTypesSeen -Values (ConvertTo-StringArray -RawValue $pluginConfig["keepTypes"])
}
if ($null -ne $KeepType -and $KeepType.Count -gt 0) {
    Add-UniqueStrings -List $keepTypesList -Seen $keepTypesSeen -Values $KeepType
}
$effectiveKeepTypes = $keepTypesList.ToArray()

$outputDir = Split-Path -Parent $outputAssemblyResolved
if (-not (Test-Path -LiteralPath $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$reportPath = $outputAssemblyResolved + ".obf-report.txt"

if (-not $effectiveEnabled) {
    Copy-Item -LiteralPath $inputAssemblyResolved -Destination $outputAssemblyResolved -Force
    Write-ObfuscationReport -ReportPath $reportPath -PluginKeyValue $PluginKey -ProfileName $effectiveProfile -EnabledValue $false -InputPath $inputAssemblyResolved -OutputPath $outputAssemblyResolved -ToolVersion $packageVersion -KeepTypes $effectiveKeepTypes
    Write-Host "Obfuscation disabled for '$PluginKey'; copied input assembly."
    return
}

$toolExe = Ensure-ObfuscarTool -ToolPath $toolPathResolved -PackageName $packageName -PackageVersion $packageVersion

$workRoot = Join-Path $env:TEMP ("vam-obfuscator-" + [System.Guid]::NewGuid().ToString("N"))
$workInDir = Join-Path $workRoot "in"
$workOutDir = Join-Path $workRoot "out"
New-Item -ItemType Directory -Path $workInDir -Force | Out-Null
New-Item -ItemType Directory -Path $workOutDir -Force | Out-Null

$inputFileName = [System.IO.Path]::GetFileName($inputAssemblyResolved)
$stagedInputAssembly = Join-Path $workInDir $inputFileName
Copy-Item -LiteralPath $inputAssemblyResolved -Destination $stagedInputAssembly -Force

$referenceFilesToStage = New-Object System.Collections.Generic.List[string]
$referenceSeen = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
if ($null -ne $ReferenceSearchPath) {
    foreach ($rawRefPath in $ReferenceSearchPath) {
        if ([string]::IsNullOrWhiteSpace($rawRefPath)) {
            continue
        }

        $candidateRefPath = Resolve-PathFromBase -PathValue $rawRefPath -BasePath $repoRootResolved -Label "Reference search path" -MustExist $false
        if (-not (Test-Path -LiteralPath $candidateRefPath)) {
            continue
        }

        $item = Get-Item -LiteralPath $candidateRefPath
        if ($item.PSIsContainer) {
            $candidateDlls = @(Get-ChildItem -LiteralPath $candidateRefPath -File -Filter *.dll -ErrorAction SilentlyContinue)
            foreach ($dll in $candidateDlls) {
                if ($referenceSeen.Add($dll.FullName)) {
                    $referenceFilesToStage.Add($dll.FullName)
                }
            }
        }
        else {
            if (
                [string]::Equals($item.Extension, ".dll", [System.StringComparison]::OrdinalIgnoreCase) -and
                $referenceSeen.Add($item.FullName)
            ) {
                $referenceFilesToStage.Add($item.FullName)
            }
        }
    }
}

foreach ($referenceFile in $referenceFilesToStage) {
    $referenceName = [System.IO.Path]::GetFileName($referenceFile)
    if ([string]::Equals($referenceName, $inputFileName, [System.StringComparison]::OrdinalIgnoreCase)) {
        continue
    }

    $stageTarget = Join-Path $workInDir $referenceName
    if (-not (Test-Path -LiteralPath $stageTarget)) {
        Copy-Item -LiteralPath $referenceFile -Destination $stageTarget -Force
    }
}

$keepPublicApi = ConvertTo-Bool -RawValue $profileConfig["keepPublicApi"] -DefaultValue $true
$hidePrivateApi = ConvertTo-Bool -RawValue $profileConfig["hidePrivateApi"] -DefaultValue $true
$renameFields = ConvertTo-Bool -RawValue $profileConfig["renameFields"] -DefaultValue $true
$renameProperties = ConvertTo-Bool -RawValue $profileConfig["renameProperties"] -DefaultValue $true
$renameEvents = ConvertTo-Bool -RawValue $profileConfig["renameEvents"] -DefaultValue $true
$hideStrings = ConvertTo-Bool -RawValue $profileConfig["hideStrings"] -DefaultValue $true
$reuseNames = ConvertTo-Bool -RawValue $profileConfig["reuseNames"] -DefaultValue $true
$useUnicodeNames = ConvertTo-Bool -RawValue $profileConfig["useUnicodeNames"] -DefaultValue $false
$suppressIldasm = ConvertTo-Bool -RawValue $profileConfig["suppressIldasm"] -DefaultValue $true

$obfuscarProjectPath = Join-Path $workRoot "obfuscar.xml"
$xml = New-Object System.Collections.Generic.List[string]
$xml.Add('<?xml version="1.0" encoding="utf-8"?>')
$xml.Add('<Obfuscator>')
$xml.Add('  <Var name="InPath" value="' + (Escape-Xml $workInDir) + '" />')
$xml.Add('  <Var name="OutPath" value="' + (Escape-Xml $workOutDir) + '" />')
$xml.Add('  <Var name="KeepPublicApi" value="' + $keepPublicApi.ToString().ToLowerInvariant() + '" />')
$xml.Add('  <Var name="HidePrivateApi" value="' + $hidePrivateApi.ToString().ToLowerInvariant() + '" />')
$xml.Add('  <Var name="RenameFields" value="' + $renameFields.ToString().ToLowerInvariant() + '" />')
$xml.Add('  <Var name="RenameProperties" value="' + $renameProperties.ToString().ToLowerInvariant() + '" />')
$xml.Add('  <Var name="RenameEvents" value="' + $renameEvents.ToString().ToLowerInvariant() + '" />')
$xml.Add('  <Var name="HideStrings" value="' + $hideStrings.ToString().ToLowerInvariant() + '" />')
$xml.Add('  <Var name="ReuseNames" value="' + $reuseNames.ToString().ToLowerInvariant() + '" />')
$xml.Add('  <Var name="UseUnicodeNames" value="' + $useUnicodeNames.ToString().ToLowerInvariant() + '" />')
$xml.Add('  <Var name="SuppressIldasm" value="' + $suppressIldasm.ToString().ToLowerInvariant() + '" />')
$xml.Add('  <Module file="' + (Escape-Xml $stagedInputAssembly) + '" />')
foreach ($keepTypeValue in $effectiveKeepTypes) {
    $xml.Add('  <SkipType name="' + (Escape-Xml $keepTypeValue) + '" />')
}
$xml.Add('</Obfuscator>')
Set-Content -LiteralPath $obfuscarProjectPath -Value $xml.ToArray() -Encoding ASCII

& $toolExe --verbosity:m $obfuscarProjectPath
if ($LASTEXITCODE -ne 0) {
    throw "Obfuscar failed for plugin '$PluginKey' with exit code $LASTEXITCODE."
}

$obfuscatedOutput = Join-Path $workOutDir $inputFileName
if (-not (Test-Path -LiteralPath $obfuscatedOutput)) {
    $outDlls = @(Get-ChildItem -LiteralPath $workOutDir -File -Filter *.dll -ErrorAction SilentlyContinue)
    if ($outDlls.Count -eq 0) {
        throw "Obfuscation completed but no DLL was produced in $workOutDir."
    }
    $obfuscatedOutput = $outDlls[0].FullName
}

Copy-Item -LiteralPath $obfuscatedOutput -Destination $outputAssemblyResolved -Force
Write-ObfuscationReport -ReportPath $reportPath -PluginKeyValue $PluginKey -ProfileName $effectiveProfile -EnabledValue $true -InputPath $inputAssemblyResolved -OutputPath $outputAssemblyResolved -ToolVersion $packageVersion -KeepTypes $effectiveKeepTypes

try {
    Remove-Item -LiteralPath $workRoot -Recurse -Force
}
catch {
    Write-Host "Obfuscation temp cleanup warning: $($workRoot)"
}

Write-Host "Obfuscated assembly written to $outputAssemblyResolved"
