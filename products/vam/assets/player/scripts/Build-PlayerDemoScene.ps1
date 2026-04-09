param(
    [string]$RepoRoot = "",
    [string]$Version = "",
    [string]$SceneTemplatePath = "F:\sim\vam\Saves\scene\buttons_setup_scene.json",
    [string]$OutputDirectory = "F:\sim\vam\Saves\scene",
    [string]$PrimaryMediaPath = "",
    [ValidateSet("single_display_fit", "multi_aspect")]
    [string]$DisplayPolicy = "multi_aspect",
    [double]$ControlOffsetX = 0.0,
    [double]$ControlOffsetY = 0.0,
    [double]$ControlOffsetZ = 0.0,
    [switch]$IncludeDebugConsole,
    [switch]$AllowExistingVersion
)

Set-StrictMode -Version Latest
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

function Normalize-PathValue {
    param([string]$PathValue)

    if ([string]::IsNullOrWhiteSpace($PathValue)) {
        return ""
    }

    # Build-host only: scene generation runs in PowerShell and this path handling
    # never enters the VaM plugin assembly.
    return [System.IO.Path]::GetFullPath($PathValue.Replace('/', '\'))
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
    # Build-host only: writing generated scene/receipt files here is safe because
    # this script is not part of the runtime plugin surface.
    [System.IO.File]::WriteAllText($Path, $json, $utf8NoBom)
}

function Get-FrameAngelGitCurrentBranchSafe {
    param([string]$RepoRootValue)

    if ([string]::IsNullOrWhiteSpace($RepoRootValue)) {
        return ""
    }

    try {
        $branch = git -C $RepoRootValue branch --show-current 2>$null
        if ($LASTEXITCODE -ne 0) {
            return ""
        }

        return ([string]$branch).Trim()
    }
    catch {
        return ""
    }
}

function Format-SceneNumber {
    param([double]$Value)

    return $Value.ToString("0.#######", [System.Globalization.CultureInfo]::InvariantCulture)
}

function Get-PlayerDemoManagedControlLocalIds {
    return @(
        "fit_button",
        "full_button",
        "crop_button",
        "reload_button",
        "previous_button",
        "play_pause_button",
        "next_button",
        "seek_start_button",
        "skip_back_button",
        "seek_reference_button",
        "skip_forward_button",
        "volume_low_button",
        "volume_high_button",
        "resize_down_button",
        "resize_up_button"
    )
}

function Get-PlayerDemoManagedSliderLocalIds {
    return @(
        "scrub_slider",
        "volume_slider"
    )
}

function Get-SceneManagedAtomId {
    param(
        [string]$LocalId,
        [string]$ParentAtomId = ""
    )

    if ([string]::IsNullOrWhiteSpace($ParentAtomId)) {
        return $LocalId
    }

    return "$ParentAtomId/$LocalId"
}

function Get-SceneManagedAtomCandidateIds {
    param(
        [string]$LocalId,
        [string]$ParentAtomId = ""
    )

    $ids = New-Object System.Collections.Generic.List[string]
    if (-not [string]::IsNullOrWhiteSpace($ParentAtomId)) {
        [void]$ids.Add((Get-SceneManagedAtomId -LocalId $LocalId -ParentAtomId $ParentAtomId))
    }

    [void]$ids.Add($LocalId)
    return $ids.ToArray()
}

function Get-PlayerDemoManagedButtonIds {
    param([string]$ParentAtomId = "")

    $ids = New-Object System.Collections.Generic.List[string]
    foreach ($localId in @(Get-PlayerDemoManagedControlLocalIds)) {
        foreach ($candidateId in @(Get-SceneManagedAtomCandidateIds -LocalId $localId -ParentAtomId $ParentAtomId)) {
            if (-not $ids.Contains($candidateId)) {
                [void]$ids.Add($candidateId)
            }
        }
    }

    return $ids.ToArray()
}

function Get-PlayerDemoManagedControlAtomIds {
    param([string]$ParentAtomId = "")

    $ids = New-Object System.Collections.Generic.List[string]
    foreach ($localId in @((Get-PlayerDemoManagedControlLocalIds) + (Get-PlayerDemoManagedSliderLocalIds))) {
        foreach ($candidateId in @(Get-SceneManagedAtomCandidateIds -LocalId $localId -ParentAtomId $ParentAtomId)) {
            if (-not $ids.Contains($candidateId)) {
                [void]$ids.Add($candidateId)
            }
        }
    }

    return $ids.ToArray()
}

function Get-PlayerDemoButtonSpecs {
    param(
        [string]$Policy,
        [string]$ParentAtomId = ""
    )

    $specs = New-Object System.Collections.Generic.List[object]

    if ($Policy -eq "multi_aspect") {
        [void]$specs.Add([pscustomobject]@{ localId = "fit_button"; id = (Get-SceneManagedAtomId -LocalId "fit_button" -ParentAtomId $ParentAtomId); text = "fit"; x = 1.9; y = 0.75; z = 0.0; action = "Player Aspect Fit"; parentAtom = $ParentAtomId })
        [void]$specs.Add([pscustomobject]@{ localId = "full_button"; id = (Get-SceneManagedAtomId -LocalId "full_button" -ParentAtomId $ParentAtomId); text = "full"; x = 1.4; y = 0.75; z = 0.0; action = "Player Aspect Full Width"; parentAtom = $ParentAtomId })
        [void]$specs.Add([pscustomobject]@{ localId = "crop_button"; id = (Get-SceneManagedAtomId -LocalId "crop_button" -ParentAtomId $ParentAtomId); text = "crop"; x = 0.9; y = 0.75; z = 0.0; action = "Player Aspect Crop"; parentAtom = $ParentAtomId })
    }

    [void]$specs.Add([pscustomobject]@{ localId = "reload_button"; id = (Get-SceneManagedAtomId -LocalId "reload_button" -ParentAtomId $ParentAtomId); text = "load"; x = 0.4; y = 0.75; z = 0.0; action = "Player Load Media"; parentAtom = $ParentAtomId })
    [void]$specs.Add([pscustomobject]@{ localId = "previous_button"; id = (Get-SceneManagedAtomId -LocalId "previous_button" -ParentAtomId $ParentAtomId); text = "prev"; x = 1.9; y = 0.25; z = 0.0; action = "Player Previous"; parentAtom = $ParentAtomId })
    [void]$specs.Add([pscustomobject]@{ localId = "play_pause_button"; id = (Get-SceneManagedAtomId -LocalId "play_pause_button" -ParentAtomId $ParentAtomId); text = "play/pause"; x = 1.4; y = 0.25; z = 0.0; action = "Player Play Pause"; parentAtom = $ParentAtomId })
    [void]$specs.Add([pscustomobject]@{ localId = "next_button"; id = (Get-SceneManagedAtomId -LocalId "next_button" -ParentAtomId $ParentAtomId); text = "next"; x = 0.9; y = 0.25; z = 0.0; action = "Player Next"; parentAtom = $ParentAtomId })
    [void]$specs.Add([pscustomobject]@{ localId = "skip_back_button"; id = (Get-SceneManagedAtomId -LocalId "skip_back_button" -ParentAtomId $ParentAtomId); text = "-10s"; x = 1.9; y = -0.25; z = 0.0; action = "Player Skip Backward"; parentAtom = $ParentAtomId })
    [void]$specs.Add([pscustomobject]@{ localId = "seek_reference_button"; id = (Get-SceneManagedAtomId -LocalId "seek_reference_button" -ParentAtomId $ParentAtomId); text = "ref"; x = 1.4; y = -0.25; z = 0.0; action = "Player Seek Reference"; parentAtom = $ParentAtomId })
    [void]$specs.Add([pscustomobject]@{ localId = "skip_forward_button"; id = (Get-SceneManagedAtomId -LocalId "skip_forward_button" -ParentAtomId $ParentAtomId); text = "+10s"; x = 0.9; y = -0.25; z = 0.0; action = "Player Skip Forward"; parentAtom = $ParentAtomId })
    [void]$specs.Add([pscustomobject]@{ localId = "volume_low_button"; id = (Get-SceneManagedAtomId -LocalId "volume_low_button" -ParentAtomId $ParentAtomId); text = "vol25"; x = 0.4; y = -0.25; z = 0.0; action = "Player Volume 25"; parentAtom = $ParentAtomId })
    [void]$specs.Add([pscustomobject]@{ localId = "volume_high_button"; id = (Get-SceneManagedAtomId -LocalId "volume_high_button" -ParentAtomId $ParentAtomId); text = "vol75"; x = 1.9; y = -0.75; z = 0.0; action = "Player Volume 75"; parentAtom = $ParentAtomId })
    [void]$specs.Add([pscustomobject]@{ localId = "resize_down_button"; id = (Get-SceneManagedAtomId -LocalId "resize_down_button" -ParentAtomId $ParentAtomId); text = "size-"; x = 1.4; y = -0.75; z = 0.0; action = "Player Resize Down"; parentAtom = $ParentAtomId })
    [void]$specs.Add([pscustomobject]@{ localId = "resize_up_button"; id = (Get-SceneManagedAtomId -LocalId "resize_up_button" -ParentAtomId $ParentAtomId); text = "size+"; x = 0.9; y = -0.75; z = 0.0; action = "Player Resize Up"; parentAtom = $ParentAtomId })

    return $specs.ToArray()
}

function New-SceneButtonAtom {
    param(
        [object]$TemplateAtom,
        [string]$NewId,
        [string]$ButtonText,
        [double]$ContainerX,
        [double]$ContainerY,
        [double]$ContainerZ,
        [string]$ParentAtomId = ""
    )

    if ($null -eq $TemplateAtom) {
        throw "Cannot clone scene button without a template atom."
    }

    $clone = ($TemplateAtom | ConvertTo-Json -Depth 50 | ConvertFrom-Json)
    $clone.id = $NewId
    if ([string]::IsNullOrWhiteSpace($ParentAtomId)) {
        [void]$clone.PSObject.Properties.Remove('parentAtom')
    }
    else {
        $clone | Add-Member -NotePropertyName parentAtom -NotePropertyValue $ParentAtomId -Force
    }
    $positionX = [string]$ContainerX
    $positionY = [string]$ContainerY
    $positionZ = [string]$ContainerZ

    $clone.position.x = $positionX
    $clone.position.y = $positionY
    $clone.position.z = $positionZ
    $clone.containerPosition.x = $positionX
    $clone.containerPosition.y = $positionY
    $clone.containerPosition.z = $positionZ

    foreach ($storable in @($clone.storables)) {
        if ($null -eq $storable) {
            continue
        }

        if ([string]$storable.id -eq "Text") {
            $storable.text = $ButtonText
        }
        elseif ([string]$storable.id -eq "control") {
            $storable.position.x = $positionX
            $storable.position.y = $positionY
            $storable.position.z = $positionZ
        }
    }

    return $clone
}

function New-SceneSliderAtom {
    param(
        [string]$NewId,
        [string]$Label,
        [double]$ContainerX,
        [double]$ContainerY,
        [double]$ContainerZ,
        [string]$ParentAtomId,
        [string]$ReceiverAtomId,
        [string]$Receiver,
        [string]$ReceiverTargetName,
        [double]$StartValue,
        [double]$EndValue,
        [double]$DefaultValue
    )

    $positionX = Format-SceneNumber -Value $ContainerX
    $positionY = Format-SceneNumber -Value $ContainerY
    $positionZ = Format-SceneNumber -Value $ContainerZ
    $startValueText = Format-SceneNumber -Value $StartValue
    $endValueText = Format-SceneNumber -Value $EndValue
    $defaultValueText = Format-SceneNumber -Value $DefaultValue

    $atom = [pscustomobject]@{
        id = $NewId
        on = "true"
        type = "UISlider"
        parentAtom = $ParentAtomId
        position = [ordered]@{
            x = $positionX
            y = $positionY
            z = $positionZ
        }
        rotation = [ordered]@{
            x = "0"
            y = "0"
            z = "0"
        }
        containerPosition = [ordered]@{
            x = $positionX
            y = $positionY
            z = $positionZ
        }
        containerRotation = [ordered]@{
            x = "0"
            y = "0"
            z = "0"
        }
        storables = @(
            [pscustomobject]@{
                id = "CollisionTrigger"
                trigger = [ordered]@{
                    startActions = @()
                    transitionActions = @()
                    endActions = @()
                }
            },
            [pscustomobject]@{
                id = "Text"
                text = $Label
            },
            [pscustomobject]@{
                id = "Canvas"
                xSize = "2000"
                ySize = "305"
            },
            [pscustomobject]@{
                id = "TextColor"
                color = [ordered]@{
                    h = "0"
                    s = "0"
                    v = "0.1985294"
                }
            },
            [pscustomobject]@{
                id = "Trigger"
                value = $defaultValueText
                trigger = [ordered]@{
                    startActions = @()
                    transitionActions = @(
                        [pscustomobject]@{
                            receiverAtom = $ReceiverAtomId
                            receiver = $Receiver
                            receiverTargetName = $ReceiverTargetName
                            startValue = $startValueText
                            endValue = $endValueText
                            startWithCurrentVal = "false"
                        }
                    )
                    endActions = @()
                }
            },
            [pscustomobject]@{
                id = "PluginManager"
                plugins = [ordered]@{}
            },
            [pscustomobject]@{
                id = "control"
                xRotationLock = "true"
                yRotationLock = "true"
                zRotationLock = "true"
                position = [ordered]@{
                    x = $positionX
                    y = $positionY
                    z = $positionZ
                }
                rotation = [ordered]@{
                    x = "0"
                    y = "0"
                    z = "0"
                }
            }
        )
    }

    if ([string]::IsNullOrWhiteSpace($ParentAtomId)) {
        [void]$atom.PSObject.Properties.Remove('parentAtom')
    }

    return $atom
}

function New-OffsetSceneSpec {
    param(
        [object]$Spec,
        [double]$OffsetX,
        [double]$OffsetY,
        [double]$OffsetZ
    )

    if ($null -eq $Spec) {
        throw "Scene spec cannot be null."
    }

    $clone = ($Spec | ConvertTo-Json -Depth 20 | ConvertFrom-Json)
    $clone.x = [double]$Spec.x + $OffsetX
    $clone.y = [double]$Spec.y + $OffsetY
    $clone.z = [double]$Spec.z + $OffsetZ
    return $clone
}

function Get-StorableById {
    param(
        [object[]]$Storables,
        [string]$Id
    )

    foreach ($storable in @($Storables)) {
        if ($null -ne $storable -and [string]::Equals([string]$storable.id, $Id, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $storable
        }
    }

    return $null
}

function New-OrderedStorableList {
    param([object[]]$ExistingStorables)

    $list = New-Object System.Collections.ArrayList
    foreach ($entry in @($ExistingStorables)) {
        if ($null -ne $entry) {
            [void]$list.Add($entry)
        }
    }

    return ,$list
}

function Remove-StorableByPredicate {
    param(
        [System.Collections.ArrayList]$Storables,
        [scriptblock]$Predicate
    )

    for ($index = $Storables.Count - 1; $index -ge 0; $index--) {
        $current = $Storables[$index]
        if ($null -ne $current -and (& $Predicate $current)) {
            $Storables.RemoveAt($index)
        }
    }
}

function Find-SceneAtomByIds {
    param(
        [System.Collections.ArrayList]$Atoms,
        [string[]]$CandidateIds
    )

    foreach ($candidateId in @($CandidateIds)) {
        if ([string]::IsNullOrWhiteSpace($candidateId)) {
            continue
        }

        $match = $Atoms | Where-Object { [string]$_.id -eq $candidateId } | Select-Object -First 1
        if ($null -ne $match) {
            return $match
        }
    }

    return $null
}

function Set-SceneAtomParent {
    param(
        [object]$Atom,
        [string]$ParentAtomId = ""
    )

    if ($null -eq $Atom) {
        return
    }

    if ([string]::IsNullOrWhiteSpace($ParentAtomId)) {
        [void]$Atom.PSObject.Properties.Remove('parentAtom')
        return
    }

    $Atom | Add-Member -NotePropertyName parentAtom -NotePropertyValue $ParentAtomId -Force
}

function Set-SceneAtomId {
    param(
        [object]$Atom,
        [string]$Id
    )

    if ($null -eq $Atom -or [string]::IsNullOrWhiteSpace($Id)) {
        return
    }

    $Atom.id = $Id
}

function Resolve-SceneVersion {
    param(
        [string]$RepoRootValue,
        [string]$ExplicitVersion
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitVersion)) {
        return $ExplicitVersion.Trim()
    }

    return (Read-FrameAngelPlayerVersionState -RepoRoot $RepoRootValue).Version
}

function Resolve-PlayerAssetName {
    param(
        [object]$LaneRoots,
        [string]$ResolvedVersion
    )

    $candidateSummaryPaths = @(
        (Join-Path $LaneRoots.AssetsPlayerBuildRoot (Join-Path ("assetbundle_exports\{0}" -f $ResolvedVersion) "player_screen_summary.json"))
    )

    foreach ($summaryPath in $candidateSummaryPaths) {
        if (Test-Path -LiteralPath $summaryPath) {
            $summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
            if ($null -ne $summary -and -not [string]::IsNullOrWhiteSpace([string]$summary.assetName)) {
                return ([string]$summary.assetName).Trim()
            }
        }
    }

    return "assets/frameangel/playerscreen/fa_player_screen.prefab"
}

$laneRoots = Get-FrameAngelPlayerLaneRoots -RepoRoot $RepoRoot -CallerScriptRoot $PSScriptRoot -EnsureAssetLaneScaffold
$RepoRoot = $laneRoots.RepoRoot
$resolvedVersion = Resolve-SceneVersion -RepoRootValue $RepoRoot -ExplicitVersion $Version

$resolvedTemplatePath = Normalize-PathValue -PathValue $SceneTemplatePath
if (-not (Test-Path -LiteralPath $resolvedTemplatePath)) {
    throw "Scene template path not found: $resolvedTemplatePath"
}

$resolvedPrimaryMediaPath = if ([string]::IsNullOrWhiteSpace($PrimaryMediaPath)) {
    ""
}
else {
    $PrimaryMediaPath.Trim().Replace('/', '\')
}

$resolvedOutputDirectory = Normalize-PathValue -PathValue $OutputDirectory
Ensure-Directory -PathValue $resolvedOutputDirectory

$sceneOutputPath = Join-Path $resolvedOutputDirectory ("fa_scene.{0}.json" -f $resolvedVersion)
$previewSourcePath = [System.IO.Path]::ChangeExtension($resolvedTemplatePath, ".jpg")
$previewOutputPath = [System.IO.Path]::ChangeExtension($sceneOutputPath, ".jpg")

if ((Test-Path -LiteralPath $sceneOutputPath) -and -not $AllowExistingVersion.IsPresent) {
    throw ("Scene output already exists for version {0}: {1}" -f $resolvedVersion, $sceneOutputPath)
}

$liveAssetPath = Join-Path "F:\sim\vam\Custom\Assets\FrameAngel\Player" ("fa_player_asset.{0}.assetbundle" -f $resolvedVersion)
$livePluginPath = Join-Path "F:\sim\vam\Custom\Plugins" ("fa_cua_player.{0}.dll" -f $resolvedVersion)

foreach ($requiredPath in @($liveAssetPath, $livePluginPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required live player artifact not found: $requiredPath"
    }
}

$assetUrl = "Custom/Assets/FrameAngel/Player/fa_player_asset.{0}.assetbundle" -f $resolvedVersion
$pluginPath = "Custom/Plugins/fa_cua_player.{0}.dll" -f $resolvedVersion
$assetName = Resolve-PlayerAssetName -LaneRoots $laneRoots -ResolvedVersion $resolvedVersion

$scene = Get-Content -LiteralPath $resolvedTemplatePath -Raw | ConvertFrom-Json
$atoms = New-Object System.Collections.ArrayList
foreach ($atom in @($scene.atoms)) {
    if ($null -ne $atom) {
        [void]$atoms.Add($atom)
    }
}
$scene.atoms = $atoms

$screenAtom = $atoms | Where-Object { [string]$_.id -eq "screen_cua" } | Select-Object -First 1
if ($null -eq $screenAtom) {
    throw "Scene template does not contain atom 'screen_cua': $resolvedTemplatePath"
}

$screenStorables = New-OrderedStorableList -ExistingStorables @($screenAtom.storables)
Remove-StorableByPredicate -Storables $screenStorables -Predicate {
    param($entry)
    $id = [string]$entry.id
    return (
        $id -eq "asset" -or
        $id -eq "PluginManager" -or
        $id -match '^plugin#\d+_FASyncRuntime$'
    )
}

[void]$screenStorables.Add([pscustomobject]@{
    id = "asset"
    assetName = $assetName
    assetUrl = $assetUrl
})
[void]$screenStorables.Add([pscustomobject]@{
    id = "PluginManager"
    plugins = [ordered]@{
        "plugin#0" = $pluginPath
    }
})
[void]$screenStorables.Add([pscustomobject]@{
    id = "plugin#0_FASyncRuntime"
    "Player Media Path" = $resolvedPrimaryMediaPath
})
$screenAtom.storables = $screenStorables

$controlRootAtom = $atoms | Where-Object { [string]$_.id -eq "controls" } | Select-Object -First 1
$controlParentId = if ($null -ne $controlRootAtom) { "controls" } else { "" }
$controlSubSceneStorePath = "Custom/SubScene/FrameAngel/controls/player_controls.json"
$useControlSubScene = $null -ne $controlRootAtom -and [string]::Equals([string]$controlRootAtom.type, "SubScene", [System.StringComparison]::OrdinalIgnoreCase)

if ($useControlSubScene) {
    $controlStorables = New-OrderedStorableList -ExistingStorables @($controlRootAtom.storables)
    $subSceneStorable = Get-StorableById -Storables @($controlStorables) -Id "SubScene"
    if ($null -eq $subSceneStorable) {
        [void]$controlStorables.Insert(0, [pscustomobject]@{
            id = "SubScene"
            storePath = $controlSubSceneStorePath
        })
    }
    else {
        $subSceneStorable.storePath = $controlSubSceneStorePath
    }
    $controlRootAtom.storables = $controlStorables

    $managedControlAtomIds = Get-PlayerDemoManagedControlAtomIds -ParentAtomId $controlParentId
    for ($atomIndex = $atoms.Count - 1; $atomIndex -ge 0; $atomIndex--) {
        $atom = $atoms[$atomIndex]
        $atomId = if ($null -eq $atom) { "" } else { [string]$atom.id }
        if ($managedControlAtomIds -contains $atomId) {
            $atoms.RemoveAt($atomIndex)
        }
    }
}

if (-not $IncludeDebugConsole.IsPresent) {
    for ($atomIndex = $atoms.Count - 1; $atomIndex -ge 0; $atomIndex--) {
        $atom = $atoms[$atomIndex]
        if ($null -ne $atom -and [string]$atom.id -eq "debug") {
            $atoms.RemoveAt($atomIndex)
        }
    }
}

$buttonSpecs = Get-PlayerDemoButtonSpecs -Policy $DisplayPolicy -ParentAtomId $controlParentId

$sliderSpecs = @(
    [pscustomobject]@{
        localId = "scrub_slider"
        id = (Get-SceneManagedAtomId -LocalId "scrub_slider" -ParentAtomId $controlParentId)
        text = "scrub"
        x = 1.15
        y = -1.30
        z = 0.0
        parentAtom = $controlParentId
        receiverTargetName = "scrub_normalized"
        startValue = 0.0
        endValue = 1.0
        defaultValue = 0.0
    },
    [pscustomobject]@{
        localId = "volume_slider"
        id = (Get-SceneManagedAtomId -LocalId "volume_slider" -ParentAtomId $controlParentId)
        text = "volume"
        x = 1.15
        y = -1.80
        z = 0.0
        parentAtom = $controlParentId
        receiverTargetName = "volume_normalized"
        startValue = 0.0
        endValue = 1.0
        defaultValue = 1.0
    }
)

$buttonSpecs = @($buttonSpecs | ForEach-Object {
    New-OffsetSceneSpec -Spec $_ -OffsetX $ControlOffsetX -OffsetY $ControlOffsetY -OffsetZ $ControlOffsetZ
})

$sliderSpecs = @($sliderSpecs | ForEach-Object {
    New-OffsetSceneSpec -Spec $_ -OffsetX $ControlOffsetX -OffsetY $ControlOffsetY -OffsetZ $ControlOffsetZ
})

$managedButtonIds = Get-PlayerDemoManagedButtonIds -ParentAtomId $controlParentId
if (-not $useControlSubScene) {
    $buttonTemplate = Find-SceneAtomByIds -Atoms $atoms -CandidateIds $managedButtonIds
    if ($null -eq $buttonTemplate) {
        throw "Scene template is missing a managed button template atom."
    }

    $desiredButtonIds = @($buttonSpecs | ForEach-Object { [string]$_.id })
    for ($atomIndex = $atoms.Count - 1; $atomIndex -ge 0; $atomIndex--) {
        $atom = $atoms[$atomIndex]
        $atomId = if ($null -eq $atom) { "" } else { [string]$atom.id }
        if (($managedButtonIds -contains $atomId) -and ($desiredButtonIds -notcontains $atomId)) {
            $atoms.RemoveAt($atomIndex)
        }
    }

    foreach ($buttonSpec in $buttonSpecs) {
        $buttonAtom = Find-SceneAtomByIds -Atoms $atoms -CandidateIds @(Get-SceneManagedAtomCandidateIds -LocalId $buttonSpec.localId -ParentAtomId $controlParentId)
        if ($null -eq $buttonAtom) {
            $buttonAtom = New-SceneButtonAtom `
                -TemplateAtom $buttonTemplate `
                -NewId $buttonSpec.id `
                -ButtonText $buttonSpec.text `
                -ContainerX $buttonSpec.x `
                -ContainerY $buttonSpec.y `
                -ContainerZ $buttonSpec.z `
                -ParentAtomId $buttonSpec.parentAtom
            [void]$atoms.Add($buttonAtom)
        }
        else {
            Set-SceneAtomId -Atom $buttonAtom -Id $buttonSpec.id
            Set-SceneAtomParent -Atom $buttonAtom -ParentAtomId $buttonSpec.parentAtom
        }

        foreach ($storable in @($buttonAtom.storables)) {
            if ($null -eq $storable) {
                continue
            }

            if ([string]$storable.id -eq "Text") {
                $storable.text = $buttonSpec.text
            }
            elseif ([string]$storable.id -eq "control") {
                $storable.position.x = [string]$buttonSpec.x
                $storable.position.y = [string]$buttonSpec.y
                $storable.position.z = [string]$buttonSpec.z
            }
        }

        if ($null -eq $buttonAtom) {
            throw "Scene template is missing expected button atom '$($buttonSpec.id)'."
        }

        $buttonStorables = New-OrderedStorableList -ExistingStorables @($buttonAtom.storables)
        Remove-StorableByPredicate -Storables $buttonStorables -Predicate {
            param($entry)
            return ([string]$entry.id -eq "Trigger")
        }

        [void]$buttonStorables.Add([pscustomobject]@{
            id = "Trigger"
            trigger = [ordered]@{
                displayName = $buttonSpec.id
                startActions = @(
                    [pscustomobject]@{
                        name = $buttonSpec.id
                        receiverAtom = "screen_cua"
                        receiver = "plugin#0_FASyncRuntime"
                        receiverTargetName = $buttonSpec.action
                    }
                )
                transitionActions = @()
                endActions = @()
            }
        })

        $buttonAtom.storables = $buttonStorables
    }

    foreach ($sliderSpec in $sliderSpecs) {
        $sliderAtomModel = New-SceneSliderAtom `
            -NewId $sliderSpec.id `
            -Label $sliderSpec.text `
            -ContainerX $sliderSpec.x `
            -ContainerY $sliderSpec.y `
            -ContainerZ $sliderSpec.z `
            -ParentAtomId $sliderSpec.parentAtom `
            -ReceiverAtomId "screen_cua" `
            -Receiver "plugin#0_FASyncRuntime" `
            -ReceiverTargetName $sliderSpec.receiverTargetName `
            -StartValue $sliderSpec.startValue `
            -EndValue $sliderSpec.endValue `
            -DefaultValue $sliderSpec.defaultValue

        $sliderAtom = Find-SceneAtomByIds -Atoms $atoms -CandidateIds @(Get-SceneManagedAtomCandidateIds -LocalId $sliderSpec.localId -ParentAtomId $controlParentId)
        if ($null -eq $sliderAtom) {
            $sliderAtom = $sliderAtomModel
            [void]$atoms.Add($sliderAtom)
            continue
        }

        Set-SceneAtomId -Atom $sliderAtom -Id $sliderSpec.id
        Set-SceneAtomParent -Atom $sliderAtom -ParentAtomId $sliderSpec.parentAtom
        $sliderAtom.on = $sliderAtomModel.on
        $sliderAtom.type = $sliderAtomModel.type
        $sliderStorables = New-OrderedStorableList -ExistingStorables @($sliderAtom.storables)
        Remove-StorableByPredicate -Storables $sliderStorables -Predicate {
            param($entry)
            return ([string]$entry.id -eq "Trigger")
        }

        $textStorable = Get-StorableById -Storables @($sliderStorables) -Id "Text"
        if ($null -ne $textStorable) {
            $textStorable.text = $sliderSpec.text
        }
        else {
            [void]$sliderStorables.Insert(0, [pscustomobject]@{
                id = "Text"
                text = $sliderSpec.text
            })
        }

        $modelTrigger = Get-StorableById -Storables @($sliderAtomModel.storables) -Id "Trigger"
        if ($null -ne $modelTrigger) {
            [void]$sliderStorables.Add($modelTrigger)
        }

        $sliderAtom.storables = $sliderStorables
    }
}

Write-JsonFile -Path $sceneOutputPath -Value $scene

if (Test-Path -LiteralPath $previewSourcePath) {
    Copy-Item -LiteralPath $previewSourcePath -Destination $previewOutputPath -Force
}

$receiptRoot = Join-Path $laneRoots.AssetsPlayerBuildRoot (Join-Path "scene_builds" $resolvedVersion)
Ensure-Directory -PathValue $receiptRoot

$receipt = [pscustomobject]@{
    schemaVersion = "frameangel_player_demo_scene_build_v1"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    version = $resolvedVersion
    branch = (Get-FrameAngelGitCurrentBranchSafe -RepoRootValue $RepoRoot)
    templateScenePath = $resolvedTemplatePath
    outputScenePath = $sceneOutputPath
    outputPreviewPath = if (Test-Path -LiteralPath $previewOutputPath) { $previewOutputPath } else { "" }
    assetUrl = $assetUrl
    assetName = $assetName
    pluginPath = $pluginPath
    primaryMediaPath = $resolvedPrimaryMediaPath
    displayPolicy = $DisplayPolicy
    includeDebugConsole = $IncludeDebugConsole.IsPresent
    controlParentAtom = $controlParentId
    controlSubScenePath = if ($useControlSubScene) { $controlSubSceneStorePath } else { "" }
    controlOffset = [pscustomobject]@{
        x = $ControlOffsetX
        y = $ControlOffsetY
        z = $ControlOffsetZ
    }
    buttonMappings = @(
        $buttonSpecs | ForEach-Object {
            [pscustomobject]@{
                buttonId = $_.id
                text = $_.text
                action = $_.action
            }
        }
    )
    sliderMappings = @(
        $sliderSpecs | ForEach-Object {
            [pscustomobject]@{
                sliderId = $_.id
                text = $_.text
                receiverTargetName = $_.receiverTargetName
                startValue = $_.startValue
                endValue = $_.endValue
                defaultValue = $_.defaultValue
            }
        }
    )
}

$receiptPath = Join-Path $receiptRoot "player_demo_scene_build_receipt.json"
$summaryPath = Join-Path $receiptRoot "player_demo_scene_build_receipt.md"
Write-JsonFile -Path $receiptPath -Value $receipt

$markdownLines = [System.Collections.Generic.List[string]]::new()
$markdownLines.Add("# Player Demo Scene Build Receipt")
$markdownLines.Add("")
$markdownLines.Add("- Version: $($receipt.version)")
$markdownLines.Add("- Branch: $($receipt.branch)")
$markdownLines.Add("- Template: $($receipt.templateScenePath)")
$markdownLines.Add("- Output Scene: $($receipt.outputScenePath)")
$markdownLines.Add("- Output Preview: $($receipt.outputPreviewPath)")
$markdownLines.Add("- Raw Asset: $($receipt.assetUrl)")
$markdownLines.Add("- Plugin: $($receipt.pluginPath)")
$markdownLines.Add("- Asset Name: $($receipt.assetName)")
$markdownLines.Add("- Primary Media Path: $($receipt.primaryMediaPath)")
$markdownLines.Add("- Display Policy: $($receipt.displayPolicy)")
$markdownLines.Add("- Control Offset: ($($receipt.controlOffset.x), $($receipt.controlOffset.y), $($receipt.controlOffset.z))")
$markdownLines.Add("")
$markdownLines.Add("## Button Mappings")
$markdownLines.Add("")
foreach ($buttonSpec in $buttonSpecs) {
    $markdownLines.Add("- $($buttonSpec.id) -> $($buttonSpec.action)")
}
$markdownLines.Add("")
$markdownLines.Add("## Slider Mappings")
$markdownLines.Add("")
foreach ($sliderSpec in $sliderSpecs) {
    $markdownLines.Add("- $($sliderSpec.id) -> $($sliderSpec.receiverTargetName) [$($sliderSpec.startValue), $($sliderSpec.endValue)]")
}
$markdown = [string]::Join([Environment]::NewLine, $markdownLines)
[System.IO.File]::WriteAllText($summaryPath, $markdown, (New-Object System.Text.UTF8Encoding($false)))

Write-Output $sceneOutputPath
