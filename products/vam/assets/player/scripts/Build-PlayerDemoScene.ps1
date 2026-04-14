param(
    [string]$RepoRoot = "",
    [string]$Version = "",
    [string]$SceneTemplatePath = "",
    [string]$OutputDirectory = "F:\sim\vam\Saves\scene",
    [string]$OutputSceneBaseName = "fa_scene",
    [string]$ReceiptLabel = "player_demo_scene_build",
    [string]$AssetUrl = "",
    [string]$PluginPath = "",
    [string]$PrimaryMediaPath = "",
    [switch]$EnablePlayerDiagnostics,
    [string]$PlayerDiagnosticsFilter = "",
    [ValidateSet("single_display_fit", "multi_aspect")]
    [string]$DisplayPolicy = "multi_aspect",
    [int]$IncludeManagedControls = 1,
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

function Test-PlayerDemoCurrentControlLayout {
    param([object[]]$Atoms)

    $requiredIds = @(
        "button_toggle_play",
        "button_previous",
        "button_load",
        "button_next",
        "slider_progress",
        "checkbox_shuffle"
    )

    foreach ($requiredId in $requiredIds) {
        $matched = $Atoms | Where-Object { $null -ne $_ -and [string]$_.id -eq $requiredId } | Select-Object -First 1
        if ($null -eq $matched) {
            return $false
        }
    }

    return $true
}

function Test-PlayerDemoThreeScreenLayout {
    param([object[]]$Atoms)

    $requiredScreenIds = @(
        "screen_middle",
        "screen_left",
        "screen_right"
    )

    foreach ($requiredScreenId in $requiredScreenIds) {
        $matchedScreen = $Atoms | Where-Object { $null -ne $_ -and [string]$_.id -eq $requiredScreenId } | Select-Object -First 1
        if ($null -eq $matchedScreen) {
            return $false
        }
    }

    $requiredControlIdGroups = @(
        @("middle_button_toggle_play", "button_toggle_play"),
        @("middle_button_previous", "button_previous"),
        @("middle_button_load", "button_load"),
        @("middle_button_next", "button_next"),
        @("middle_slider_progress", "slider_progress"),
        @("middle_slider_volume", "slider_volume"),
        @("middle_checkbox_shuffle", "checkbox_shuffle"),
        @("middle_display_curr", "display_curr"),
        @("middle_display_total", "display_total")
    )

    foreach ($candidateGroup in $requiredControlIdGroups) {
        $matched = $null
        foreach ($candidateId in @($candidateGroup)) {
            $matched = $Atoms | Where-Object { $null -ne $_ -and [string]$_.id -eq $candidateId } | Select-Object -First 1
            if ($null -ne $matched) {
                break
            }
        }

        if ($null -eq $matched) {
            return $false
        }
    }

    return $true
}

function Resolve-PlayerDemoManagedAtomId {
    param(
        [string]$LocalId,
        [string]$ParentAtomId = "",
        [ValidateSet("legacy", "current_example", "three_screen_demo3")]
        [string]$ControlLayout = "legacy"
    )

    if ($ControlLayout -ne "legacy") {
        return $LocalId
    }

    return Get-SceneManagedAtomId -LocalId $LocalId -ParentAtomId $ParentAtomId
}

function Get-PlayerDemoManagedControlLocalIds {
    param(
        [ValidateSet("legacy", "current_example", "three_screen_demo3")]
        [string]$ControlLayout = "legacy"
    )

    if ($ControlLayout -eq "three_screen_demo3") {
        return @()
    }

    if ($ControlLayout -eq "current_example") {
        return @(
            "button_load",
            "button_previous",
            "button_toggle_play",
            "button_next",
            "checkbox_shuffle"
        )
    }

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
    param(
        [ValidateSet("legacy", "current_example", "three_screen_demo3")]
        [string]$ControlLayout = "legacy"
    )

    if ($ControlLayout -eq "three_screen_demo3") {
        return @()
    }

    if ($ControlLayout -eq "current_example") {
        return @(
            "slider_progress"
        )
    }

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
    param(
        [string]$ParentAtomId = "",
        [ValidateSet("legacy", "current_example", "three_screen_demo3")]
        [string]$ControlLayout = "legacy"
    )

    $ids = New-Object System.Collections.Generic.List[string]
    foreach ($localId in @(Get-PlayerDemoManagedControlLocalIds -ControlLayout $ControlLayout)) {
        foreach ($candidateId in @(Get-SceneManagedAtomCandidateIds -LocalId $localId -ParentAtomId $ParentAtomId)) {
            if (-not $ids.Contains($candidateId)) {
                [void]$ids.Add($candidateId)
            }
        }
    }

    return $ids.ToArray()
}

function Get-PlayerDemoManagedControlAtomIds {
    param(
        [string]$ParentAtomId = "",
        [ValidateSet("legacy", "current_example", "three_screen_demo3")]
        [string]$ControlLayout = "legacy"
    )

    $ids = New-Object System.Collections.Generic.List[string]
    foreach ($localId in @((Get-PlayerDemoManagedControlLocalIds -ControlLayout $ControlLayout) + (Get-PlayerDemoManagedSliderLocalIds -ControlLayout $ControlLayout))) {
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
        [string]$ParentAtomId = "",
        [ValidateSet("legacy", "current_example", "three_screen_demo3")]
        [string]$ControlLayout = "legacy"
    )

    $specs = New-Object System.Collections.Generic.List[object]

    if ($ControlLayout -eq "three_screen_demo3") {
        return $specs.ToArray()
    }

    if ($ControlLayout -eq "current_example") {
        [void]$specs.Add([pscustomobject]@{
            localId = "button_previous"
            id = (Resolve-PlayerDemoManagedAtomId -LocalId "button_previous" -ParentAtomId $ParentAtomId -ControlLayout $ControlLayout)
            text = "Previous"
            x = 1.3
            y = 0.8
            z = 0.0
            action = "Player Previous"
            parentAtom = $ParentAtomId
            preserveExistingLayout = $true
            preserveExistingText = $true
        })
        [void]$specs.Add([pscustomobject]@{
            localId = "button_toggle_play"
            id = (Resolve-PlayerDemoManagedAtomId -LocalId "button_toggle_play" -ParentAtomId $ParentAtomId -ControlLayout $ControlLayout)
            text = "Play/pause"
            x = 1.0
            y = 0.8
            z = 0.0
            action = "Player Play Pause"
            parentAtom = $ParentAtomId
            preserveExistingLayout = $true
            preserveExistingText = $true
        })
        [void]$specs.Add([pscustomobject]@{
            localId = "button_next"
            id = (Resolve-PlayerDemoManagedAtomId -LocalId "button_next" -ParentAtomId $ParentAtomId -ControlLayout $ControlLayout)
            text = "Next"
            x = 0.7
            y = 0.8
            z = 0.0
            action = "Player Next"
            parentAtom = $ParentAtomId
            preserveExistingLayout = $true
            preserveExistingText = $true
        })
        [void]$specs.Add([pscustomobject]@{
            localId = "button_load"
            id = (Resolve-PlayerDemoManagedAtomId -LocalId "button_load" -ParentAtomId $ParentAtomId -ControlLayout $ControlLayout)
            text = "Load"
            x = 1.0
            y = 0.7
            z = 0.0
            action = "Player Load Media"
            parentAtom = $ParentAtomId
            preserveExistingLayout = $true
            preserveExistingText = $true
        })
        [void]$specs.Add([pscustomobject]@{
            localId = "checkbox_shuffle"
            id = (Resolve-PlayerDemoManagedAtomId -LocalId "checkbox_shuffle" -ParentAtomId $ParentAtomId -ControlLayout $ControlLayout)
            text = "Shuffle"
            x = 0.71
            y = 0.7
            z = 0.0
            action = "Player Random On"
            parentAtom = $ParentAtomId
            preserveExistingLayout = $true
            preserveExistingText = $true
        })

        return $specs.ToArray()
    }

    if ($Policy -eq "multi_aspect") {
        [void]$specs.Add([pscustomobject]@{ localId = "fit_button"; id = (Resolve-PlayerDemoManagedAtomId -LocalId "fit_button" -ParentAtomId $ParentAtomId -ControlLayout $ControlLayout); text = "fit"; x = 1.9; y = 0.75; z = 0.0; action = "Player Aspect Fit"; parentAtom = $ParentAtomId; preserveExistingLayout = $false; preserveExistingText = $false })
        [void]$specs.Add([pscustomobject]@{ localId = "full_button"; id = (Resolve-PlayerDemoManagedAtomId -LocalId "full_button" -ParentAtomId $ParentAtomId -ControlLayout $ControlLayout); text = "full"; x = 1.4; y = 0.75; z = 0.0; action = "Player Aspect Full Width"; parentAtom = $ParentAtomId; preserveExistingLayout = $false; preserveExistingText = $false })
        [void]$specs.Add([pscustomobject]@{ localId = "crop_button"; id = (Resolve-PlayerDemoManagedAtomId -LocalId "crop_button" -ParentAtomId $ParentAtomId -ControlLayout $ControlLayout); text = "crop"; x = 0.9; y = 0.75; z = 0.0; action = "Player Aspect Crop"; parentAtom = $ParentAtomId; preserveExistingLayout = $false; preserveExistingText = $false })
    }

    [void]$specs.Add([pscustomobject]@{ localId = "reload_button"; id = (Resolve-PlayerDemoManagedAtomId -LocalId "reload_button" -ParentAtomId $ParentAtomId -ControlLayout $ControlLayout); text = "load"; x = 0.4; y = 0.75; z = 0.0; action = "Player Load Media"; parentAtom = $ParentAtomId; preserveExistingLayout = $false; preserveExistingText = $false })
    [void]$specs.Add([pscustomobject]@{ localId = "previous_button"; id = (Resolve-PlayerDemoManagedAtomId -LocalId "previous_button" -ParentAtomId $ParentAtomId -ControlLayout $ControlLayout); text = "prev"; x = 1.9; y = 0.25; z = 0.0; action = "Player Previous"; parentAtom = $ParentAtomId; preserveExistingLayout = $false; preserveExistingText = $false })
    [void]$specs.Add([pscustomobject]@{ localId = "play_pause_button"; id = (Resolve-PlayerDemoManagedAtomId -LocalId "play_pause_button" -ParentAtomId $ParentAtomId -ControlLayout $ControlLayout); text = "play/pause"; x = 1.4; y = 0.25; z = 0.0; action = "Player Play Pause"; parentAtom = $ParentAtomId; preserveExistingLayout = $false; preserveExistingText = $false })
    [void]$specs.Add([pscustomobject]@{ localId = "next_button"; id = (Resolve-PlayerDemoManagedAtomId -LocalId "next_button" -ParentAtomId $ParentAtomId -ControlLayout $ControlLayout); text = "next"; x = 0.9; y = 0.25; z = 0.0; action = "Player Next"; parentAtom = $ParentAtomId; preserveExistingLayout = $false; preserveExistingText = $false })
    [void]$specs.Add([pscustomobject]@{ localId = "skip_back_button"; id = (Resolve-PlayerDemoManagedAtomId -LocalId "skip_back_button" -ParentAtomId $ParentAtomId -ControlLayout $ControlLayout); text = "-10s"; x = 1.9; y = -0.25; z = 0.0; action = "Player Skip Backward"; parentAtom = $ParentAtomId; preserveExistingLayout = $false; preserveExistingText = $false })
    [void]$specs.Add([pscustomobject]@{ localId = "seek_reference_button"; id = (Resolve-PlayerDemoManagedAtomId -LocalId "seek_reference_button" -ParentAtomId $ParentAtomId -ControlLayout $ControlLayout); text = "ref"; x = 1.4; y = -0.25; z = 0.0; action = "Player Seek Reference"; parentAtom = $ParentAtomId; preserveExistingLayout = $false; preserveExistingText = $false })
    [void]$specs.Add([pscustomobject]@{ localId = "skip_forward_button"; id = (Resolve-PlayerDemoManagedAtomId -LocalId "skip_forward_button" -ParentAtomId $ParentAtomId -ControlLayout $ControlLayout); text = "+10s"; x = 0.9; y = -0.25; z = 0.0; action = "Player Skip Forward"; parentAtom = $ParentAtomId; preserveExistingLayout = $false; preserveExistingText = $false })
    [void]$specs.Add([pscustomobject]@{ localId = "volume_low_button"; id = (Resolve-PlayerDemoManagedAtomId -LocalId "volume_low_button" -ParentAtomId $ParentAtomId -ControlLayout $ControlLayout); text = "vol25"; x = 0.4; y = -0.25; z = 0.0; action = "Player Volume 25"; parentAtom = $ParentAtomId; preserveExistingLayout = $false; preserveExistingText = $false })
    [void]$specs.Add([pscustomobject]@{ localId = "volume_high_button"; id = (Resolve-PlayerDemoManagedAtomId -LocalId "volume_high_button" -ParentAtomId $ParentAtomId -ControlLayout $ControlLayout); text = "vol75"; x = 1.9; y = -0.75; z = 0.0; action = "Player Volume 75"; parentAtom = $ParentAtomId; preserveExistingLayout = $false; preserveExistingText = $false })
    [void]$specs.Add([pscustomobject]@{ localId = "resize_down_button"; id = (Resolve-PlayerDemoManagedAtomId -LocalId "resize_down_button" -ParentAtomId $ParentAtomId -ControlLayout $ControlLayout); text = "size-"; x = 1.4; y = -0.75; z = 0.0; action = "Player Resize Down"; parentAtom = $ParentAtomId; preserveExistingLayout = $false; preserveExistingText = $false })
    [void]$specs.Add([pscustomobject]@{ localId = "resize_up_button"; id = (Resolve-PlayerDemoManagedAtomId -LocalId "resize_up_button" -ParentAtomId $ParentAtomId -ControlLayout $ControlLayout); text = "size+"; x = 0.9; y = -0.75; z = 0.0; action = "Player Resize Up"; parentAtom = $ParentAtomId; preserveExistingLayout = $false; preserveExistingText = $false })

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

function Get-PlayerDemoThreeScreenControlSpecs {
    $specs = New-Object System.Collections.Generic.List[object]

    [void]$specs.Add([pscustomobject]@{ localId = "button_previous"; candidateIds = @("middle_button_previous", "button_previous"); kind = "button"; required = $true; sourceParentAtomIds = @("screen_middle") })
    [void]$specs.Add([pscustomobject]@{ localId = "button_toggle_play"; candidateIds = @("middle_button_toggle_play", "button_toggle_play"); kind = "button"; required = $true; sourceParentAtomIds = @("screen_middle") })
    [void]$specs.Add([pscustomobject]@{ localId = "button_next"; candidateIds = @("middle_button_next", "button_next"); kind = "button"; required = $true; sourceParentAtomIds = @("screen_middle") })
    [void]$specs.Add([pscustomobject]@{ localId = "button_load"; candidateIds = @("middle_button_load", "button_load"); kind = "button"; required = $true; sourceParentAtomIds = @("screen_middle") })
    [void]$specs.Add([pscustomobject]@{ localId = "checkbox_shuffle"; candidateIds = @("middle_checkbox_shuffle", "checkbox_shuffle"); kind = "toggle"; required = $true; sourceParentAtomIds = @("screen_middle") })
    [void]$specs.Add([pscustomobject]@{ localId = "checkbox_ab"; candidateIds = @("middle_checkbox_ab", "checkbox_ab"); kind = "toggle_ab"; required = $false; sourceParentAtomIds = @("screen_middle"); fallbackType = "UIToggle"; fallbackText = "A B Loop" })
    [void]$specs.Add([pscustomobject]@{ localId = "slider_progress"; candidateIds = @("middle_slider_progress", "slider_progress"); kind = "slider"; required = $true; sourceParentAtomIds = @("screen_middle") })
    [void]$specs.Add([pscustomobject]@{ localId = "slider_volume"; candidateIds = @("middle_slider_volume", "slider_volume"); kind = "slider"; required = $true; sourceParentAtomIds = @("screen_middle") })
    [void]$specs.Add([pscustomobject]@{ localId = "display_curr"; candidateIds = @("middle_display_curr", "display_curr"); kind = "display"; required = $true; sourceParentAtomIds = @("screen_middle") })
    [void]$specs.Add([pscustomobject]@{ localId = "display_total"; candidateIds = @("middle_display_total", "display_total"); kind = "display"; required = $true; sourceParentAtomIds = @("screen_middle") })
    [void]$specs.Add([pscustomobject]@{ localId = "scale_25"; candidateIds = @("middle_scale_25", "25%"); kind = "scale"; required = $false; sourceParentAtomIds = @("screen_middle") })
    [void]$specs.Add([pscustomobject]@{ localId = "scale_50"; candidateIds = @("middle_scale_50", "50%"); kind = "scale"; required = $false; sourceParentAtomIds = @("screen_middle") })
    [void]$specs.Add([pscustomobject]@{ localId = "scale_100"; candidateIds = @("middle_scale_100", "100%"); kind = "scale"; required = $false; sourceParentAtomIds = @("screen_middle") })
    [void]$specs.Add([pscustomobject]@{ localId = "scale_150"; candidateIds = @("middle_scale_150", "150%"); kind = "scale"; required = $false; sourceParentAtomIds = @("screen_middle") })
    [void]$specs.Add([pscustomobject]@{ localId = "scale_200"; candidateIds = @("middle_scale_200", "200%"); kind = "scale"; required = $false; sourceParentAtomIds = @("screen_middle", "spotlight_middle") })
    [void]$specs.Add([pscustomobject]@{ localId = "button_ab_start"; candidateIds = @("middle_button_ab_start", "button_a"); kind = "button"; required = $false; sourceParentAtomIds = @("screen_middle") })
    [void]$specs.Add([pscustomobject]@{ localId = "button_ab_end"; candidateIds = @("middle_button_ab_end", "button_b"); kind = "button"; required = $false; sourceParentAtomIds = @("screen_middle") })
    [void]$specs.Add([pscustomobject]@{ localId = "button_clear"; candidateIds = @("middle_button_clear", "middle_button_ab_clear", "button_clear", "clear"); kind = "button"; required = $false; sourceParentAtomIds = @("screen_middle", "spotlight_middle") })

    return $specs.ToArray()
}

function Resolve-PlayerDemoThreeScreenControlId {
    param(
        [ValidateSet("middle", "left", "right")]
        [string]$Role,
        [string]$LocalId
    )

    return "{0}_{1}" -f $Role, $LocalId
}

function Find-SceneAtomByIdsAndParent {
    param(
        [System.Collections.ArrayList]$Atoms,
        [string[]]$CandidateIds,
        [string]$ParentAtomId = ""
    )

    foreach ($candidateId in @($CandidateIds)) {
        if ([string]::IsNullOrWhiteSpace($candidateId)) {
            continue
        }

        $match = $Atoms | Where-Object {
            if ($null -eq $_ -or [string]$_.id -ne $candidateId) {
                return $false
            }

            if ([string]::IsNullOrWhiteSpace($ParentAtomId)) {
                return $true
            }

            return [string]$_.parentAtom -eq $ParentAtomId
        } | Select-Object -First 1

        if ($null -ne $match) {
            return $match
        }
    }

    return $null
}

function Find-SceneAtomByTextTypeAndParents {
    param(
        [System.Collections.ArrayList]$Atoms,
        [string]$Type = "",
        [string]$Text = "",
        [string[]]$ParentAtomIds = @()
    )

    foreach ($atom in @($Atoms)) {
        if ($null -eq $atom) {
            continue
        }

        if (-not [string]::IsNullOrWhiteSpace($Type) -and [string]$atom.type -ne $Type) {
            continue
        }

        if ($ParentAtomIds.Count -gt 0 -and -not ($ParentAtomIds -contains [string]$atom.parentAtom)) {
            continue
        }

        if (-not [string]::IsNullOrWhiteSpace($Text)) {
            $textStorable = Get-StorableById -Storables @($atom.storables) -Id "Text"
            if ($null -eq $textStorable -or [string]$textStorable.text -ne $Text) {
                continue
            }
        }

        return $atom
    }

    return $null
}

function Resolve-PlayerDemoThreeScreenSourceAtom {
    param(
        [System.Collections.ArrayList]$Atoms,
        [object]$Spec
    )

    $sourceParentAtomIds = @($Spec.sourceParentAtomIds)
    if ($sourceParentAtomIds.Count -le 0) {
        $sourceParentAtomIds = @("screen_middle")
    }

    foreach ($parentAtomId in $sourceParentAtomIds) {
        $match = Find-SceneAtomByIdsAndParent -Atoms $Atoms -CandidateIds @($Spec.candidateIds) -ParentAtomId $parentAtomId
        if ($null -ne $match) {
            return $match
        }
    }

    if (-not [string]::IsNullOrWhiteSpace([string]$Spec.fallbackText) -or -not [string]::IsNullOrWhiteSpace([string]$Spec.fallbackType)) {
        return Find-SceneAtomByTextTypeAndParents -Atoms $Atoms -Type ([string]$Spec.fallbackType) -Text ([string]$Spec.fallbackText) -ParentAtomIds $sourceParentAtomIds
    }

    return $null
}

function Clone-SceneAtomDeep {
    param([object]$Atom)

    if ($null -eq $Atom) {
        throw "Scene atom cannot be cloned from null."
    }

    return ($Atom | ConvertTo-Json -Depth 50 | ConvertFrom-Json)
}

function Set-PlayerDemoSceneAtomLinkToParent {
    param(
        [object]$Atom,
        [string]$ParentAtomId
    )

    Set-SceneAtomParent -Atom $Atom -ParentAtomId $ParentAtomId
    $controlStorable = Get-StorableById -Storables @($Atom.storables) -Id "control"
    if ($null -eq $controlStorable) {
        return
    }

    $controlStorable | Add-Member -NotePropertyName positionState -NotePropertyValue "ParentLink" -Force
    $controlStorable | Add-Member -NotePropertyName rotationState -NotePropertyValue "ParentLink" -Force
    $controlStorable | Add-Member -NotePropertyName linkTo -NotePropertyValue ("{0}:object" -f $ParentAtomId) -Force
}

function Set-PlayerDemoSceneAtomRelativeTransform {
    param(
        [object]$Atom,
        [object]$PositionSource,
        [object]$RotationSource
    )

    if ($null -eq $Atom) {
        return
    }

    if ($null -ne $PositionSource) {
        $positionX = [string]$PositionSource.x
        $positionY = [string]$PositionSource.y
        $positionZ = [string]$PositionSource.z

        if ($null -ne $Atom.position) {
            $Atom.position.x = $positionX
            $Atom.position.y = $positionY
            $Atom.position.z = $positionZ
        }

        if ($null -ne $Atom.containerPosition) {
            $Atom.containerPosition.x = $positionX
            $Atom.containerPosition.y = $positionY
            $Atom.containerPosition.z = $positionZ
        }
    }

    if ($null -ne $RotationSource) {
        $rotationX = [string]$RotationSource.x
        $rotationY = [string]$RotationSource.y
        $rotationZ = [string]$RotationSource.z

        if ($null -ne $Atom.rotation) {
            $Atom.rotation.x = $rotationX
            $Atom.rotation.y = $rotationY
            $Atom.rotation.z = $rotationZ
        }

        if ($null -ne $Atom.containerRotation) {
            $Atom.containerRotation.x = $rotationX
            $Atom.containerRotation.y = $rotationY
            $Atom.containerRotation.z = $rotationZ
        }
    }

    $controlStorable = Get-StorableById -Storables @($Atom.storables) -Id "control"
    if ($null -eq $controlStorable) {
        return
    }

    if ($null -ne $PositionSource -and $null -ne $controlStorable.position) {
        $controlStorable.position.x = [string]$PositionSource.x
        $controlStorable.position.y = [string]$PositionSource.y
        $controlStorable.position.z = [string]$PositionSource.z
    }

    if ($null -ne $RotationSource -and $null -ne $controlStorable.rotation) {
        $controlStorable.rotation.x = [string]$RotationSource.x
        $controlStorable.rotation.y = [string]$RotationSource.y
        $controlStorable.rotation.z = [string]$RotationSource.z
    }
}

function Copy-PlayerDemoSceneAtomRelativeTransformFromSource {
    param(
        [object]$SourceAtom,
        [object]$TargetAtom
    )

    if ($null -eq $SourceAtom -or $null -eq $TargetAtom) {
        return
    }

    $sourceControl = Get-StorableById -Storables @($SourceAtom.storables) -Id "control"
    $sourcePosition = if ($null -ne $sourceControl -and $null -ne $sourceControl.position) {
        $sourceControl.position
    }
    elseif ($null -ne $SourceAtom.containerPosition) {
        $SourceAtom.containerPosition
    }
    else {
        $SourceAtom.position
    }

    $sourceRotation = if ($null -ne $sourceControl -and $null -ne $sourceControl.rotation) {
        $sourceControl.rotation
    }
    elseif ($null -ne $SourceAtom.containerRotation) {
        $SourceAtom.containerRotation
    }
    else {
        $SourceAtom.rotation
    }

    Set-PlayerDemoSceneAtomRelativeTransform -Atom $TargetAtom -PositionSource $sourcePosition -RotationSource $sourceRotation
}

function Resolve-PlayerDemoSceneAtomTransformSources {
    param([object]$Atom)

    if ($null -eq $Atom) {
        return $null
    }

    $controlStorable = Get-StorableById -Storables @($Atom.storables) -Id "control"
    $positionSource = if ($null -ne $controlStorable -and $null -ne $controlStorable.position) {
        $controlStorable.position
    }
    elseif ($null -ne $Atom.containerPosition) {
        $Atom.containerPosition
    }
    else {
        $Atom.position
    }

    $rotationSource = if ($null -ne $controlStorable -and $null -ne $controlStorable.rotation) {
        $controlStorable.rotation
    }
    elseif ($null -ne $Atom.containerRotation) {
        $Atom.containerRotation
    }
    else {
        $Atom.rotation
    }

    return [pscustomobject]@{
        position = $positionSource
        rotation = $rotationSource
    }
}

function Rotate-PlayerDemoSceneOffsetAroundY {
    param(
        [double]$X,
        [double]$Z,
        [double]$Degrees
    )

    $radians = $Degrees * [Math]::PI / 180.0
    $cosine = [Math]::Cos($radians)
    $sine = [Math]::Sin($radians)

    return [pscustomobject]@{
        x = (($X * $cosine) + ($Z * $sine))
        z = ((-$X * $sine) + ($Z * $cosine))
    }
}

function Copy-PlayerDemoSceneAtomRelativeTransformWithScreenDelta {
    param(
        [object]$SourceAtom,
        [object]$TargetAtom,
        [object]$SourceScreenAtom,
        [object]$TargetScreenAtom
    )

    if ($null -eq $SourceAtom -or $null -eq $TargetAtom -or $null -eq $SourceScreenAtom -or $null -eq $TargetScreenAtom) {
        return
    }

    $sourceTransform = Resolve-PlayerDemoSceneAtomTransformSources -Atom $SourceAtom
    $sourceScreenTransform = Resolve-PlayerDemoSceneAtomTransformSources -Atom $SourceScreenAtom
    $targetScreenTransform = Resolve-PlayerDemoSceneAtomTransformSources -Atom $TargetScreenAtom
    if ($null -eq $sourceTransform -or $null -eq $sourceScreenTransform -or $null -eq $targetScreenTransform) {
        Copy-PlayerDemoSceneAtomRelativeTransformFromSource -SourceAtom $SourceAtom -TargetAtom $TargetAtom
        return
    }

    $rotationDeltaX = (([double]$targetScreenTransform.rotation.x) - ([double]$sourceScreenTransform.rotation.x))
    $rotationDeltaY = (([double]$targetScreenTransform.rotation.y) - ([double]$sourceScreenTransform.rotation.y))
    $rotationDeltaZ = (([double]$targetScreenTransform.rotation.z) - ([double]$sourceScreenTransform.rotation.z))

    $localOffsetX = (([double]$sourceTransform.position.x) - ([double]$sourceScreenTransform.position.x))
    $localOffsetY = (([double]$sourceTransform.position.y) - ([double]$sourceScreenTransform.position.y))
    $localOffsetZ = (([double]$sourceTransform.position.z) - ([double]$sourceScreenTransform.position.z))
    $rotatedLocalOffset = Rotate-PlayerDemoSceneOffsetAroundY -X $localOffsetX -Z $localOffsetZ -Degrees $rotationDeltaY

    $position = [pscustomobject]@{
        x = (([double]$targetScreenTransform.position.x) + [double]$rotatedLocalOffset.x)
        y = (([double]$targetScreenTransform.position.y) + $localOffsetY)
        z = (([double]$targetScreenTransform.position.z) + [double]$rotatedLocalOffset.z)
    }

    $rotation = [pscustomobject]@{
        x = ([double]$sourceTransform.rotation.x + $rotationDeltaX)
        y = ([double]$sourceTransform.rotation.y + $rotationDeltaY)
        z = ([double]$sourceTransform.rotation.z + $rotationDeltaZ)
    }

    Set-PlayerDemoSceneAtomRelativeTransform -Atom $TargetAtom -PositionSource $position -RotationSource $rotation
}

function New-PlayerDemoPluginActionEntry {
    param(
        [string]$ScreenAtomId,
        [string]$ActionName
    )

    return [pscustomobject]@{
        name = "A_$ActionName"
        receiverAtom = $ScreenAtomId
        receiver = "plugin#0_FASyncRuntime"
        receiverTargetName = $ActionName
    }
}

function New-PlayerDemoThreeScreenTriggerStorable {
    param(
        [object]$Spec,
        [string]$ScreenAtomId,
        [object]$ExistingTriggerStorable
    )

    switch ([string]$Spec.kind) {
        "button" {
            $startActions = @(switch ([string]$Spec.localId) {
                "button_previous" { @(New-PlayerDemoPluginActionEntry -ScreenAtomId $ScreenAtomId -ActionName "Player Previous") }
                "button_toggle_play" { @(New-PlayerDemoPluginActionEntry -ScreenAtomId $ScreenAtomId -ActionName "Player Play Pause") }
                "button_next" { @(New-PlayerDemoPluginActionEntry -ScreenAtomId $ScreenAtomId -ActionName "Player Next") }
                "button_load" { @(New-PlayerDemoPluginActionEntry -ScreenAtomId $ScreenAtomId -ActionName "Player Load Media") }
                "button_ab_start" { @(New-PlayerDemoPluginActionEntry -ScreenAtomId $ScreenAtomId -ActionName "Player A-B Set Start") }
                "button_ab_end" {
                    @(
                        (New-PlayerDemoPluginActionEntry -ScreenAtomId $ScreenAtomId -ActionName "Player A-B Set End"),
                        (New-PlayerDemoPluginActionEntry -ScreenAtomId $ScreenAtomId -ActionName "Player A-B Enable")
                    )
                }
                "button_clear" { @(New-PlayerDemoPluginActionEntry -ScreenAtomId $ScreenAtomId -ActionName "Player A-B Clear") }
                "button_ab_clear" { @(New-PlayerDemoPluginActionEntry -ScreenAtomId $ScreenAtomId -ActionName "Player A-B Clear") }
                default { @() }
            })

            if ($startActions.Count -le 0) {
                return $null
            }

            return [pscustomobject]@{
                id = "Trigger"
                trigger = [ordered]@{
                    displayName = ("A_{0}" -f $Spec.localId)
                    startActions = $startActions
                    transitionActions = @()
                    endActions = @()
                }
            }
        }
        "scale" {
            $scaleValue = switch ([string]$Spec.localId) {
                "scale_25" { 0.25 }
                "scale_50" { 0.50 }
                "scale_100" { 1.00 }
                "scale_150" { 1.50 }
                "scale_200" { 2.00 }
                default { $null }
            }

            if ($null -eq $scaleValue) {
                return $null
            }

            return [pscustomobject]@{
                id = "Trigger"
                trigger = [ordered]@{
                    displayName = ("A_scale:{0}" -f (Format-SceneNumber -Value $scaleValue))
                    startActions = @(
                        [pscustomobject]@{
                            name = ("A_scale:{0}" -f (Format-SceneNumber -Value $scaleValue))
                            receiverAtom = $ScreenAtomId
                            receiver = "scale"
                            receiverTargetName = "scale"
                            floatValue = (Format-SceneNumber -Value $scaleValue)
                            useTimer = "true"
                            timerLength = "0.5"
                            timerType = "EaseInOut"
                        }
                    )
                    transitionActions = @()
                    endActions = @()
                }
            }
        }
        "toggle" {
            return [pscustomobject]@{
                id = "Trigger"
                trigger = [ordered]@{
                    displayName = "A_Player Random Toggle"
                    startActions = @(
                        (New-PlayerDemoPluginActionEntry -ScreenAtomId $ScreenAtomId -ActionName "Player Random On")
                    )
                    transitionActions = @()
                    endActions = @(
                        (New-PlayerDemoPluginActionEntry -ScreenAtomId $ScreenAtomId -ActionName "Player Random Off")
                    )
                }
            }
        }
        "toggle_ab" {
            return [pscustomobject]@{
                id = "Trigger"
                trigger = [ordered]@{
                    displayName = "A_Player A-B Toggle"
                    startActions = @()
                    transitionActions = @()
                    endActions = @()
                }
            }
        }
        "slider" {
            $receiverTargetName = switch ([string]$Spec.localId) {
                "slider_progress" { "scrub_normalized" }
                "slider_volume" { "volume_normalized" }
                default { "" }
            }

            if ([string]::IsNullOrWhiteSpace($receiverTargetName)) {
                return $null
            }

            $existingValue = ""
            if ($receiverTargetName -eq "volume_normalized" -and $null -ne $ExistingTriggerStorable -and $ExistingTriggerStorable.PSObject.Properties.Name -contains "value") {
                $existingValue = [string]$ExistingTriggerStorable.value
            }
            if ([string]::IsNullOrWhiteSpace($existingValue)) {
                $existingValue = if ($receiverTargetName -eq "volume_normalized") { "1" } else { "0" }
            }

            return [pscustomobject]@{
                id = "Trigger"
                value = $existingValue
                trigger = [ordered]@{
                    displayName = ("A_{0}:0.00_1.00" -f $receiverTargetName)
                    startActions = @()
                    transitionActions = @(
                        [pscustomobject]@{
                            name = ("A_{0}:0.00_1.00" -f $receiverTargetName)
                            receiverAtom = $ScreenAtomId
                            receiver = "plugin#0_FASyncRuntime"
                            receiverTargetName = $receiverTargetName
                            startValue = "0"
                            endValue = "1"
                            startWithCurrentVal = "false"
                        }
                    )
                    endActions = @()
                }
            }
        }
    }

    return $null
}

function Set-PlayerDemoThreeScreenControlWiring {
    param(
        [object]$Atom,
        [object]$Spec,
        [string]$ScreenAtomId
    )

    if ($null -eq $Atom -or $null -eq $Spec) {
        return
    }

    if ([string]$Spec.kind -eq "display") {
        $textStorable = Get-StorableById -Storables @($Atom.storables) -Id "Text"
        if ($null -ne $textStorable) {
            $defaultDisplayText = switch ([string]$Spec.localId) {
                "display_curr" { "00:00" }
                "display_total" { "00:00" }
                default { "" }
            }

            if (-not [string]::IsNullOrWhiteSpace($defaultDisplayText)) {
                $textStorable | Add-Member -NotePropertyName text -NotePropertyValue $defaultDisplayText -Force
            }
        }

        return
    }

    $existingTrigger = Get-StorableById -Storables @($Atom.storables) -Id "Trigger"
    $storables = New-OrderedStorableList -ExistingStorables @($Atom.storables)
    Remove-StorableByPredicate -Storables $storables -Predicate {
        param($entry)
        return ([string]$entry.id -eq "Trigger")
    }

    $triggerStorable = New-PlayerDemoThreeScreenTriggerStorable -Spec $Spec -ScreenAtomId $ScreenAtomId -ExistingTriggerStorable $existingTrigger
    if ($null -ne $triggerStorable) {
        [void]$storables.Add($triggerStorable)
    }

    $Atom.storables = $storables
}

function Apply-PlayerDemoThreeScreenControls {
    param(
        [System.Collections.ArrayList]$Atoms,
        [bool]$IncludeManagedControlsEnabled
    )

    $screenIdsByRole = [ordered]@{
        middle = "screen_middle"
        left = "screen_left"
        right = "screen_right"
    }
    $screenAtomsByRole = @{}
    foreach ($roleKey in @($screenIdsByRole.Keys)) {
        $screenAtomsByRole[$roleKey] = Find-SceneAtomByIdsAndParent -Atoms $Atoms -CandidateIds @([string]$screenIdsByRole[$roleKey])
    }

    $controlSpecs = @(Get-PlayerDemoThreeScreenControlSpecs)
    if (-not $IncludeManagedControlsEnabled) {
        $candidateIdsToRemove = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::Ordinal)
        foreach ($spec in $controlSpecs) {
            foreach ($candidateId in @($spec.candidateIds)) {
                if (-not [string]::IsNullOrWhiteSpace($candidateId)) {
                    [void]$candidateIdsToRemove.Add($candidateId)
                }
            }

            foreach ($role in @($screenIdsByRole.Keys)) {
                [void]$candidateIdsToRemove.Add((Resolve-PlayerDemoThreeScreenControlId -Role $role -LocalId $spec.localId))
            }
        }

        for ($atomIndex = $Atoms.Count - 1; $atomIndex -ge 0; $atomIndex--) {
            $atom = $Atoms[$atomIndex]
            if ($null -eq $atom) {
                continue
            }

            if ($candidateIdsToRemove.Contains([string]$atom.id)) {
                $Atoms.RemoveAt($atomIndex)
            }
        }

        return
    }

    foreach ($spec in $controlSpecs) {
        $middleAtom = Resolve-PlayerDemoThreeScreenSourceAtom -Atoms $Atoms -Spec $spec
        if ($null -eq $middleAtom) {
            if ($spec.required) {
                throw "Three-screen template is missing required middle control atom for '$($spec.localId)'."
            }

            continue
        }

        foreach ($role in @($screenIdsByRole.Keys)) {
            $screenAtomId = [string]$screenIdsByRole[$role]
            $desiredId = Resolve-PlayerDemoThreeScreenControlId -Role $role -LocalId $spec.localId
            $targetAtom = if ($role -eq "middle") {
                $middleAtom
            }
            else {
                $existingRoleAtom = Find-SceneAtomByIdsAndParent -Atoms $Atoms -CandidateIds @($desiredId) -ParentAtomId $screenAtomId
                if ($null -ne $existingRoleAtom) {
                    $existingRoleAtom
                }
                else {
                    $clone = Clone-SceneAtomDeep -Atom $middleAtom
                    [void]$Atoms.Add($clone)
                    $clone
                }
            }

            Set-SceneAtomId -Atom $targetAtom -Id $desiredId
            if ($role -ne "middle") {
                Copy-PlayerDemoSceneAtomRelativeTransformFromSource -SourceAtom $middleAtom -TargetAtom $targetAtom
            }
            Set-PlayerDemoSceneAtomLinkToParent -Atom $targetAtom -ParentAtomId $screenAtomId
            Set-PlayerDemoThreeScreenControlWiring -Atom $targetAtom -Spec $spec -ScreenAtomId $screenAtomId
        }
    }
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

$defaultSceneTemplatePath = Join-Path $laneRoots.AssetsPlayerRoot "scene_templates\demo3.json"
$resolvedTemplatePath = Normalize-PathValue -PathValue $(if ([string]::IsNullOrWhiteSpace($SceneTemplatePath)) { $defaultSceneTemplatePath } else { $SceneTemplatePath })
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

$resolvedOutputSceneBaseName = if ([string]::IsNullOrWhiteSpace($OutputSceneBaseName)) {
    "fa_scene"
}
else {
    $OutputSceneBaseName.Trim()
}
$resolvedReceiptLabel = if ([string]::IsNullOrWhiteSpace($ReceiptLabel)) {
    "player_demo_scene_build"
}
else {
    $ReceiptLabel.Trim()
}
$sceneOutputPath = Join-Path $resolvedOutputDirectory ("{0}.{1}.json" -f $resolvedOutputSceneBaseName, $resolvedVersion)
$previewSourcePath = [System.IO.Path]::ChangeExtension($resolvedTemplatePath, ".jpg")
$previewOutputPath = [System.IO.Path]::ChangeExtension($sceneOutputPath, ".jpg")

if ((Test-Path -LiteralPath $sceneOutputPath) -and -not $AllowExistingVersion.IsPresent) {
    throw ("Scene output already exists for version {0}: {1}" -f $resolvedVersion, $sceneOutputPath)
}

$liveAssetPath = Join-Path "F:\sim\vam\Custom\Assets\FrameAngel\Player" ("dev_cua_player.{0}.assetbundle" -f $resolvedVersion)
$livePluginPath = Join-Path "F:\sim\vam\Custom\Plugins" ("dev_plugin_player.{0}.dll" -f $resolvedVersion)

if ([string]::IsNullOrWhiteSpace($AssetUrl)) {
    if (-not (Test-Path -LiteralPath $liveAssetPath)) {
        throw "Required live player asset not found: $liveAssetPath"
    }
}

if ([string]::IsNullOrWhiteSpace($PluginPath)) {
    if (-not (Test-Path -LiteralPath $livePluginPath)) {
        throw "Required live player plugin not found: $livePluginPath"
    }
}

$assetUrl = if ([string]::IsNullOrWhiteSpace($AssetUrl)) {
    "Custom/Assets/FrameAngel/Player/dev_cua_player.{0}.assetbundle" -f $resolvedVersion
}
else {
    $AssetUrl.Trim()
}

$pluginPath = if ([string]::IsNullOrWhiteSpace($PluginPath)) {
    "Custom/Plugins/dev_plugin_player.{0}.dll" -f $resolvedVersion
}
else {
    $PluginPath.Trim()
}

$assetName = Resolve-PlayerAssetName -LaneRoots $laneRoots -ResolvedVersion $resolvedVersion

$scene = Get-Content -LiteralPath $resolvedTemplatePath -Raw | ConvertFrom-Json
$atoms = New-Object System.Collections.ArrayList
foreach ($atom in @($scene.atoms)) {
    if ($null -ne $atom) {
        [void]$atoms.Add($atom)
    }
}
$scene.atoms = $atoms
$threeScreenLayoutDetected = Test-PlayerDemoThreeScreenLayout -Atoms @($atoms)
$screenAtoms = New-Object System.Collections.Generic.List[object]

if ($threeScreenLayoutDetected) {
    foreach ($threeScreenId in @("screen_middle", "screen_left", "screen_right")) {
        $threeScreenAtom = $atoms | Where-Object { $null -ne $_ -and [string]$_.id -eq $threeScreenId } | Select-Object -First 1
        if ($null -eq $threeScreenAtom) {
            throw "Three-screen template is missing expected screen atom '$threeScreenId': $resolvedTemplatePath"
        }

        [void]$screenAtoms.Add($threeScreenAtom)
    }

    $screenAtom = $screenAtoms | Where-Object { [string]$_.id -eq "screen_middle" } | Select-Object -First 1
}
else {
    $screenAtom = $atoms | Where-Object { [string]$_.id -eq "screen_cua" } | Select-Object -First 1
    if ($null -eq $screenAtom) {
        $screenAtom = $atoms | Where-Object {
            if ($null -eq $_ -or -not [string]::Equals([string]$_.type, "CustomUnityAsset", [System.StringComparison]::OrdinalIgnoreCase)) {
                return $false
            }

            foreach ($storable in @($_.storables)) {
                if ($null -ne $storable -and [string]$storable.id -eq "plugin#0_FASyncRuntime") {
                    return $true
                }
            }

            return $false
        } | Select-Object -First 1
    }
    if ($null -eq $screenAtom) {
        throw "Scene template does not contain a player screen atom: $resolvedTemplatePath"
    }

    [void]$screenAtoms.Add($screenAtom)
}

$screenAtomId = [string]$screenAtom.id

foreach ($currentScreenAtom in $screenAtoms) {
    $screenStorables = New-OrderedStorableList -ExistingStorables @($currentScreenAtom.storables)
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
    $currentScreenAtom.storables = $screenStorables
}

$controlLayout = if ($threeScreenLayoutDetected) { "three_screen_demo3" } elseif (Test-PlayerDemoCurrentControlLayout -Atoms @($atoms)) { "current_example" } else { "legacy" }
$controlRootAtom = if ($controlLayout -eq "legacy") {
    $atoms | Where-Object { [string]$_.id -eq "controls" } | Select-Object -First 1
}
else {
    $null
}
$controlParentId = if ($controlLayout -eq "three_screen_demo3") {
    "screen_middle"
}
elseif ($controlLayout -eq "current_example") {
    "fap"
}
elseif ($null -ne $controlRootAtom) {
    "controls"
}
else {
    ""
}
$controlSubSceneStorePath = "Custom/SubScene/FrameAngel/controls/player_controls.json"
$useControlSubScene = $controlLayout -eq "legacy" -and $null -ne $controlRootAtom -and [string]::Equals([string]$controlRootAtom.type, "SubScene", [System.StringComparison]::OrdinalIgnoreCase)
$managedControlAtomIds = Get-PlayerDemoManagedControlAtomIds -ParentAtomId $controlParentId -ControlLayout $controlLayout
$includeManagedControlsEnabled = $IncludeManagedControls -ne 0
$buttonSpecs = @()
$sliderSpecs = @()

if ($controlLayout -eq "three_screen_demo3") {
    Apply-PlayerDemoThreeScreenControls -Atoms $atoms -IncludeManagedControlsEnabled $includeManagedControlsEnabled
}
else {
if ($useControlSubScene -and $includeManagedControlsEnabled) {
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

    for ($atomIndex = $atoms.Count - 1; $atomIndex -ge 0; $atomIndex--) {
        $atom = $atoms[$atomIndex]
        $atomId = if ($null -eq $atom) { "" } else { [string]$atom.id }
        if ($managedControlAtomIds -contains $atomId) {
            $atoms.RemoveAt($atomIndex)
        }
    }
}

if (-not $includeManagedControlsEnabled) {
    for ($atomIndex = $atoms.Count - 1; $atomIndex -ge 0; $atomIndex--) {
        $atom = $atoms[$atomIndex]
        $atomId = if ($null -eq $atom) { "" } else { [string]$atom.id }
        if (($managedControlAtomIds -contains $atomId) -or $atomId -eq "controls") {
            $atoms.RemoveAt($atomIndex)
        }
    }

    $controlRootAtom = $null
    $controlParentId = ""
    $useControlSubScene = $false
}

if (-not $IncludeDebugConsole.IsPresent) {
    for ($atomIndex = $atoms.Count - 1; $atomIndex -ge 0; $atomIndex--) {
        $atom = $atoms[$atomIndex]
        if ($null -ne $atom -and [string]$atom.id -eq "debug") {
            $atoms.RemoveAt($atomIndex)
        }
    }
}

if ($includeManagedControlsEnabled) {
    $buttonSpecs = Get-PlayerDemoButtonSpecs -Policy $DisplayPolicy -ParentAtomId $controlParentId -ControlLayout $controlLayout

    if ($controlLayout -eq "current_example") {
        $sliderSpecs = @(
            [pscustomobject]@{
                localId = "slider_progress"
                id = (Resolve-PlayerDemoManagedAtomId -LocalId "slider_progress" -ParentAtomId $controlParentId -ControlLayout $controlLayout)
                text = " "
                x = 1.0
                y = 0.9
                z = 0.0
                parentAtom = $controlParentId
                receiverTargetName = "scrub_normalized"
                startValue = 0.0
                endValue = 1.0
                defaultValue = 0.0
                preserveExistingLayout = $true
                preserveExistingText = $true
            }
        )
    }
    else {
        $sliderSpecs = @(
            [pscustomobject]@{
                localId = "scrub_slider"
                id = (Resolve-PlayerDemoManagedAtomId -LocalId "scrub_slider" -ParentAtomId $controlParentId -ControlLayout $controlLayout)
                text = "scrub"
                x = 1.15
                y = -1.30
                z = 0.0
                parentAtom = $controlParentId
                receiverTargetName = "scrub_normalized"
                startValue = 0.0
                endValue = 1.0
                defaultValue = 0.0
                preserveExistingLayout = $false
                preserveExistingText = $false
            },
            [pscustomobject]@{
                localId = "volume_slider"
                id = (Resolve-PlayerDemoManagedAtomId -LocalId "volume_slider" -ParentAtomId $controlParentId -ControlLayout $controlLayout)
                text = "volume"
                x = 1.15
                y = -1.80
                z = 0.0
                parentAtom = $controlParentId
                receiverTargetName = "volume_normalized"
                startValue = 0.0
                endValue = 1.0
                defaultValue = 1.0
                preserveExistingLayout = $false
                preserveExistingText = $false
            }
        )
    }

    $buttonSpecs = @($buttonSpecs | ForEach-Object {
        New-OffsetSceneSpec -Spec $_ -OffsetX $ControlOffsetX -OffsetY $ControlOffsetY -OffsetZ $ControlOffsetZ
    })

    $sliderSpecs = @($sliderSpecs | ForEach-Object {
        New-OffsetSceneSpec -Spec $_ -OffsetX $ControlOffsetX -OffsetY $ControlOffsetY -OffsetZ $ControlOffsetZ
    })
}

$managedButtonIds = Get-PlayerDemoManagedButtonIds -ParentAtomId $controlParentId -ControlLayout $controlLayout
if ($includeManagedControlsEnabled -and -not $useControlSubScene) {
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

        if (-not $buttonSpec.preserveExistingLayout -or -not $buttonSpec.preserveExistingText) {
            foreach ($storable in @($buttonAtom.storables)) {
                if ($null -eq $storable) {
                    continue
                }

                if (([string]$storable.id -eq "Text") -and -not $buttonSpec.preserveExistingText) {
                    $storable.text = $buttonSpec.text
                }
                elseif (([string]$storable.id -eq "control") -and -not $buttonSpec.preserveExistingLayout) {
                    $storable.position.x = [string]$buttonSpec.x
                    $storable.position.y = [string]$buttonSpec.y
                    $storable.position.z = [string]$buttonSpec.z
                }
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
                        receiverAtom = $screenAtomId
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
            -ReceiverAtomId $screenAtomId `
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
        if ($null -ne $textStorable -and -not $sliderSpec.preserveExistingText) {
            $textStorable.text = $sliderSpec.text
        }
        elseif ($null -eq $textStorable) {
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
    includeManagedControls = $includeManagedControlsEnabled
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

$receiptPath = Join-Path $receiptRoot ("{0}_receipt.json" -f $resolvedReceiptLabel)
$summaryPath = Join-Path $receiptRoot ("{0}_receipt.md" -f $resolvedReceiptLabel)
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
$markdownLines.Add("- Include Managed Controls: $($receipt.includeManagedControls)")
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
