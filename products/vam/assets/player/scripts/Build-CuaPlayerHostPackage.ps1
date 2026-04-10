param(
    [string]$ScreenPackageRoot = "F:\sim\vam\Custom\PluginData\FrameAngel\GhostScreenVariants\rect\package",
    [string]$ControlsPackageRoot = "F:\sim\vam\Custom\PluginData\FrameAngel\meta_toolkit_demo\theme_00\faipe_meta_contentuiexample_videoplayer_3c2b98cf4fd1",
    [string]$OutputRoot = "",
    [switch]$Deploy,
    [string]$DeployRoot = "F:\sim\vam\Custom\PluginData\FrameAngel\cua_player_host_v1",
    [string]$HostProfilePath = "",
    [string]$HostDisplayName = "",
    [string]$HostResourceId = "",
    [string]$HostShellKey = "",
    [string]$DeviceClass = "",
    [string]$OrientationSupport = "",
    [string]$DefaultAspectMode = "",
    [string]$InputStyle = "",
    [double]$SafeCornerRadius = -1,
    [string]$ControlsAnchorNodeId = "",
    [switch]$AutoOrientToGround
)

$ErrorActionPreference = "Stop"

$resolvedScriptRepoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))))
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $resolvedScriptRepoRoot "products\vam\assets\player\build\host_packages\cua_player_host_v1"
}

function Assert-Path {
    param(
        [string]$Path,
        [string]$Label
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Label not found: $Path"
    }
}

function Read-JsonFile {
    param([string]$Path)
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Write-JsonFile {
    param(
        [string]$Path,
        [object]$Value
    )

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $json = $Value | ConvertTo-Json -Depth 100
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $json, $utf8NoBom)
}

function Convert-ToVectorRecord {
    param($Vector)
    return [ordered]@{
        x = [double]$Vector.x
        y = [double]$Vector.y
        z = [double]$Vector.z
    }
}

function Convert-ToQuaternionRecord {
    param($Quaternion)
    return [ordered]@{
        x = [double]$Quaternion.x
        y = [double]$Quaternion.y
        z = [double]$Quaternion.z
        w = [double]$Quaternion.w
    }
}

function New-GeometryNode {
    param(
        [string]$NodeId,
        [string]$ParentNodeId,
        [string]$DisplayName,
        $LocalPosition,
        $LocalRotation,
        $LocalScale,
        [string[]]$MeshRefIds
    )

    return [ordered]@{
        nodeId = $NodeId
        parentNodeId = $ParentNodeId
        displayName = $DisplayName
        localPosition = Convert-ToVectorRecord $LocalPosition
        localRotation = Convert-ToQuaternionRecord $LocalRotation
        localScale = Convert-ToVectorRecord $LocalScale
        meshRefIds = @($MeshRefIds)
    }
}

function Get-NodeMap {
    param($Nodes)

    $map = @{}
    foreach ($node in @($Nodes)) {
        if ($null -eq $node -or [string]::IsNullOrWhiteSpace([string]$node.nodeId)) {
            continue
        }

        $map[[string]$node.nodeId] = $node
    }

    return $map
}

function Get-ChildMap {
    param($Nodes)

    $map = @{}
    foreach ($node in @($Nodes)) {
        if ($null -eq $node) {
            continue
        }

        $parentId = [string]$node.parentNodeId
        if (-not $map.ContainsKey($parentId)) {
            $map[$parentId] = New-Object System.Collections.Generic.List[object]
        }

        $map[$parentId].Add($node)
    }

    return $map
}

function Get-SubtreeNodes {
    param(
        $NodeMap,
        $ChildMap,
        [string]$RootNodeId
    )

    $result = New-Object System.Collections.Generic.List[object]
    if (-not $NodeMap.ContainsKey($RootNodeId)) {
        return @()
    }

    $pending = New-Object System.Collections.Generic.Queue[string]
    $pending.Enqueue($RootNodeId)

    while ($pending.Count -gt 0) {
        $nextId = $pending.Dequeue()
        if (-not $NodeMap.ContainsKey($nextId)) {
            continue
        }

        $result.Add($NodeMap[$nextId])
        if ($ChildMap.ContainsKey($nextId)) {
            foreach ($child in $ChildMap[$nextId]) {
                $pending.Enqueue([string]$child.nodeId)
            }
        }
    }

    return $result.ToArray()
}

function Get-UniformScale {
    param($Nodes, [string]$NodeId)

    $nodeMap = Get-NodeMap $Nodes
    if (-not $nodeMap.ContainsKey($NodeId)) {
        throw "Node not found for scale resolution: $NodeId"
    }

    $scaleX = 1.0
    $scaleY = 1.0
    $scaleZ = 1.0
    $current = $nodeMap[$NodeId]
    while ($null -ne $current) {
        $scaleX *= [double]$current.localScale.x
        $scaleY *= [double]$current.localScale.y
        $scaleZ *= [double]$current.localScale.z

        $parentId = [string]$current.parentNodeId
        if ([string]::IsNullOrWhiteSpace($parentId) -or -not $nodeMap.ContainsKey($parentId)) {
            break
        }

        $current = $nodeMap[$parentId]
    }

    return [ordered]@{
        x = $scaleX
        y = $scaleY
        z = $scaleZ
    }
}

function Copy-Materials {
    param(
        $TargetList,
        $Entries,
        $SeenMaterialIds
    )

    foreach ($entry in @($Entries)) {
        $materialRefId = [string]$entry.materialRefId
        if ([string]::IsNullOrWhiteSpace($materialRefId)) {
            continue
        }

        if ($SeenMaterialIds.Contains($materialRefId)) {
            throw "Duplicate materialRefId detected during CUA host composition: $materialRefId"
        }

        [void]$SeenMaterialIds.Add($materialRefId)
        $TargetList.Add($entry)
    }
}

function Copy-Meshes {
    param(
        $TargetList,
        $Entries,
        $SeenMeshIds
    )

    foreach ($entry in @($Entries)) {
        $meshId = [string]$entry.meshId
        if ([string]::IsNullOrWhiteSpace($meshId)) {
            continue
        }

        if ($SeenMeshIds.Contains($meshId)) {
            throw "Duplicate meshId detected during CUA host composition: $meshId"
        }

        [void]$SeenMeshIds.Add($meshId)
        $TargetList.Add($entry)
    }
}

function Get-OptionalPropertyValue {
    param(
        $Object,
        [string]$Name,
        $DefaultValue
    )

    if ($null -eq $Object) {
        return $DefaultValue
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $DefaultValue
    }

    $value = $property.Value
    if ($null -eq $value) {
        return $DefaultValue
    }

    return $value
}

function Resolve-ExistingControlsPackageRoot {
    param(
        [string]$RequestedRoot
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedRoot) -and (Test-Path -LiteralPath $RequestedRoot)) {
        return $RequestedRoot
    }

    $fallbackRoot = if ([string]::IsNullOrWhiteSpace($RequestedRoot)) {
        "F:\sim\vam\Custom\PluginData\FrameAngel\meta_toolkit_demo\theme_00"
    }
    else {
        Split-Path -Parent $RequestedRoot
    }

    if ([string]::IsNullOrWhiteSpace($fallbackRoot) -or -not (Test-Path -LiteralPath $fallbackRoot)) {
        return $RequestedRoot
    }

    $matches = Get-ChildItem -LiteralPath $fallbackRoot -Directory -Filter "faipe_meta_contentuiexample_videoplayer_*" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending

    if ($matches -and $matches.Count -gt 0) {
        return $matches[0].FullName
    }

    return $RequestedRoot
}

function Get-FirstRootNodeId {
    param($Nodes)

    foreach ($node in @($Nodes)) {
        if ($null -eq $node) {
            continue
        }

        if ([string]::IsNullOrWhiteSpace([string]$node.parentNodeId)) {
            return [string]$node.nodeId
        }
    }

    return if (@($Nodes).Count -gt 0) { [string]$Nodes[0].nodeId } else { "" }
}

function Resolve-NodeIdByProfileOrSuffix {
    param(
        $Nodes,
        $NodeMap,
        [string]$PreferredNodeId,
        [string[]]$Suffixes
    )

    if (-not [string]::IsNullOrWhiteSpace($PreferredNodeId) -and $NodeMap.ContainsKey($PreferredNodeId)) {
        return $PreferredNodeId
    }

    foreach ($node in @($Nodes)) {
        if ($null -eq $node) {
            continue
        }

        $displayName = [string]$node.displayName
        $nodeId = [string]$node.nodeId
        foreach ($suffix in @($Suffixes)) {
            if ([string]::IsNullOrWhiteSpace($suffix)) {
                continue
            }

            if ($displayName.EndsWith($suffix, [System.StringComparison]::OrdinalIgnoreCase) -or
                $nodeId.EndsWith($suffix, [System.StringComparison]::OrdinalIgnoreCase)) {
                return $nodeId
            }
        }
    }

    return ""
}

function Clone-NodeWithMapping {
    param(
        $SourceNode,
        $NodeIdMap,
        [string]$RootDisplayName
    )

    $sourceNodeId = [string]$SourceNode.nodeId
    $newNodeId = $NodeIdMap[$sourceNodeId]
    $sourceParentId = [string]$SourceNode.parentNodeId
    $newParentId = ""

    if (-not [string]::IsNullOrWhiteSpace($sourceParentId) -and $NodeIdMap.ContainsKey($sourceParentId)) {
        $newParentId = $NodeIdMap[$sourceParentId]
    }

    $displayName = [string]$SourceNode.displayName
    if ($newNodeId -eq "fa_cua_player_host") {
        $displayName = $RootDisplayName
    }
    elseif ($newNodeId -eq "screen_surface") {
        $displayName = "screen_surface"
    }
    elseif ($newNodeId -eq "disconnect_surface") {
        $displayName = "disconnect_surface"
    }

    return (New-GeometryNode `
        -NodeId $newNodeId `
        -ParentNodeId $newParentId `
        -DisplayName $displayName `
        -LocalPosition $SourceNode.localPosition `
        -LocalRotation $SourceNode.localRotation `
        -LocalScale $SourceNode.localScale `
        -MeshRefIds @($SourceNode.meshRefIds))
}

$screenManifestPath = Join-Path $ScreenPackageRoot "manifest.json"
$screenGeometryPath = Join-Path $ScreenPackageRoot "geometry.innerpiece.json"
$screenMaterialsPath = Join-Path $ScreenPackageRoot "materials.innerpiece.json"
$screenContractPath = Join-Path $ScreenPackageRoot "screens.innerpiece.json"
$defaultHostProfilePath = Join-Path $ScreenPackageRoot "host_profile.json"

$resolvedControlsPackageRoot = Resolve-ExistingControlsPackageRoot -RequestedRoot $ControlsPackageRoot

$controlsManifestPath = Join-Path $resolvedControlsPackageRoot "manifest.json"
$controlsGeometryPath = Join-Path $resolvedControlsPackageRoot "geometry.innerpiece.json"
$controlsMaterialsPath = Join-Path $resolvedControlsPackageRoot "materials.innerpiece.json"
$controlsContractPath = Join-Path $resolvedControlsPackageRoot "controls.innerpiece.json"

Assert-Path -Path $screenManifestPath -Label "Screen manifest"
Assert-Path -Path $screenGeometryPath -Label "Screen geometry"
Assert-Path -Path $screenMaterialsPath -Label "Screen materials"
Assert-Path -Path $screenContractPath -Label "Screen contract"
Assert-Path -Path $controlsManifestPath -Label "Controls manifest"
Assert-Path -Path $controlsGeometryPath -Label "Controls geometry"
Assert-Path -Path $controlsMaterialsPath -Label "Controls materials"
Assert-Path -Path $controlsContractPath -Label "Controls contract"

$resolvedHostProfilePath = if ([string]::IsNullOrWhiteSpace($HostProfilePath)) { $defaultHostProfilePath } else { $HostProfilePath }
$hostProfile = if (Test-Path -LiteralPath $resolvedHostProfilePath) { Read-JsonFile -Path $resolvedHostProfilePath } else { $null }

$screenManifest = Read-JsonFile -Path $screenManifestPath
$screenGeometry = Read-JsonFile -Path $screenGeometryPath
$screenMaterials = Read-JsonFile -Path $screenMaterialsPath
$screenContract = Read-JsonFile -Path $screenContractPath

$controlsManifest = Read-JsonFile -Path $controlsManifestPath
$controlsGeometry = Read-JsonFile -Path $controlsGeometryPath
$controlsMaterials = Read-JsonFile -Path $controlsMaterialsPath
$controlsContract = Read-JsonFile -Path $controlsContractPath

$screenSlot = @($screenContract.slots) | Select-Object -First 1
if ($null -eq $screenSlot) {
    throw "Screen package does not contain a usable slot definition."
}

$screenSurfaceSourceId = [string]$screenSlot.screenSurfaceNodeId
$disconnectSurfaceSourceId = [string]$screenSlot.disconnectSurfaceNodeId
$controlsSurfaceSourceId = [string]$controlsContract.surfaceNodeId

if ([string]::IsNullOrWhiteSpace($screenSurfaceSourceId)) {
    throw "Screen package is missing screenSurfaceNodeId."
}

if ([string]::IsNullOrWhiteSpace($controlsSurfaceSourceId)) {
    throw "Controls package is missing surfaceNodeId."
}

$screenNodeMap = Get-NodeMap $screenGeometry.nodes
if (-not $screenNodeMap.ContainsKey($screenSurfaceSourceId)) {
    throw "Screen surface node not found in screen geometry: $screenSurfaceSourceId"
}

$controlsNodeMap = Get-NodeMap $controlsGeometry.nodes
$controlsChildMap = Get-ChildMap $controlsGeometry.nodes
if (-not $controlsNodeMap.ContainsKey($controlsSurfaceSourceId)) {
    throw "Control surface node not found in controls geometry: $controlsSurfaceSourceId"
}

$resolvedShellId = if (-not [string]::IsNullOrWhiteSpace($HostShellKey)) {
    $HostShellKey
}
else {
    [string](Get-OptionalPropertyValue -Object $hostProfile -Name "shellKey" -DefaultValue ([string]$screenContract.shellId))
}

if ([string]::IsNullOrWhiteSpace($resolvedShellId)) {
    $resolvedShellId = "player_host"
}

$resolvedHostResourceId = if (-not [string]::IsNullOrWhiteSpace($HostResourceId)) {
    $HostResourceId
}
else {
    [string](Get-OptionalPropertyValue -Object $hostProfile -Name "hostResourceId" -DefaultValue "fa_cua_player_host_v1")
}

$resolvedHostPackageId = [string](Get-OptionalPropertyValue -Object $hostProfile -Name "hostPackageId" -DefaultValue "")
if ([string]::IsNullOrWhiteSpace($resolvedHostPackageId)) {
    $resolvedHostPackageId = "faipe_" + $resolvedHostResourceId
}

$resolvedHostDisplayName = if (-not [string]::IsNullOrWhiteSpace($HostDisplayName)) {
    $HostDisplayName
}
else {
    [string](Get-OptionalPropertyValue -Object $hostProfile -Name "hostDisplayName" -DefaultValue "FA CUA Player Host")
}

$resolvedDeviceClass = if (-not [string]::IsNullOrWhiteSpace($DeviceClass)) {
    $DeviceClass
}
else {
    [string](Get-OptionalPropertyValue -Object $hostProfile -Name "deviceClass" -DefaultValue "monitor")
}

$resolvedOrientationSupport = if (-not [string]::IsNullOrWhiteSpace($OrientationSupport)) {
    $OrientationSupport
}
else {
    [string](Get-OptionalPropertyValue -Object $hostProfile -Name "orientationSupport" -DefaultValue "landscape")
}

$resolvedDefaultAspectMode = if (-not [string]::IsNullOrWhiteSpace($DefaultAspectMode)) {
    $DefaultAspectMode
}
else {
    [string](Get-OptionalPropertyValue -Object $hostProfile -Name "defaultAspectMode" -DefaultValue "fit")
}

$resolvedInputStyle = if (-not [string]::IsNullOrWhiteSpace($InputStyle)) {
    $InputStyle
}
else {
    [string](Get-OptionalPropertyValue -Object $hostProfile -Name "inputStyle" -DefaultValue "fixed")
}

$resolvedSafeCornerRadius = if ($SafeCornerRadius -ge 0) {
    $SafeCornerRadius
}
else {
    [double](Get-OptionalPropertyValue -Object $hostProfile -Name "safeCornerRadius" -DefaultValue 0.0)
}

$resolvedAutoOrientToGround = if ($PSBoundParameters.ContainsKey('AutoOrientToGround')) {
    [bool]$AutoOrientToGround
}
else {
    [bool](Get-OptionalPropertyValue -Object $hostProfile -Name "autoOrientToGround" -DefaultValue $false)
}

$preferredControlsAnchorNodeId = if (-not [string]::IsNullOrWhiteSpace($ControlsAnchorNodeId)) {
    $ControlsAnchorNodeId
}
else {
    [string](Get-OptionalPropertyValue -Object $hostProfile -Name "controlsAnchorNodeId" -DefaultValue "")
}

$preferredBottomAnchorNodeId = [string](Get-OptionalPropertyValue -Object $hostProfile -Name "bottomAnchorNodeId" -DefaultValue "")

$screenRootSourceId = Get-FirstRootNodeId -Nodes $screenGeometry.nodes
if ([string]::IsNullOrWhiteSpace($screenRootSourceId)) {
    throw "Screen geometry does not contain a root node."
}

$screenNodeIdMap = @{}
$usedNodeIds = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
foreach ($sourceNode in @($screenGeometry.nodes)) {
    $sourceNodeId = [string]$sourceNode.nodeId
    $newNodeId = if ($sourceNodeId -eq $screenRootSourceId) {
        "fa_cua_player_host"
    }
    elseif ($sourceNodeId -eq $screenSurfaceSourceId) {
        "screen_surface"
    }
    elseif (-not [string]::IsNullOrWhiteSpace($disconnectSurfaceSourceId) -and $sourceNodeId -eq $disconnectSurfaceSourceId) {
        "disconnect_surface"
    }
    else {
        $sourceNodeId
    }

    if ($usedNodeIds.Contains($newNodeId)) {
        throw "Duplicate nodeId detected while mapping shell geometry: $newNodeId"
    }

    [void]$usedNodeIds.Add($newNodeId)
    $screenNodeIdMap[$sourceNodeId] = $newNodeId
}

$newNodes = New-Object System.Collections.Generic.List[object]
foreach ($sourceNode in @($screenGeometry.nodes)) {
    $newNodes.Add((Clone-NodeWithMapping -SourceNode $sourceNode -NodeIdMap $screenNodeIdMap -RootDisplayName $resolvedHostDisplayName))
}

$controlsScale = Get-UniformScale -Nodes $controlsGeometry.nodes -NodeId $controlsSurfaceSourceId
$controlsAnchorSourceId = Resolve-NodeIdByProfileOrSuffix `
    -Nodes $screenGeometry.nodes `
    -NodeMap $screenNodeMap `
    -PreferredNodeId $preferredControlsAnchorNodeId `
    -Suffixes @(".controls_anchor", "controls_anchor")
$bottomAnchorSourceId = Resolve-NodeIdByProfileOrSuffix `
    -Nodes $screenGeometry.nodes `
    -NodeMap $screenNodeMap `
    -PreferredNodeId $preferredBottomAnchorNodeId `
    -Suffixes @(".bottom_anchor", "bottom_anchor")

$controlsParentNodeId = if (-not [string]::IsNullOrWhiteSpace($controlsAnchorSourceId) -and $screenNodeIdMap.ContainsKey($controlsAnchorSourceId)) {
    $screenNodeIdMap[$controlsAnchorSourceId]
}
else {
    "fa_cua_player_host"
}

$controlsRootLocalPosition = if ($controlsParentNodeId -eq "fa_cua_player_host") {
    [ordered]@{ x = 0.0; y = -0.48; z = 0.0 }
}
else {
    [ordered]@{ x = 0.0; y = 0.0; z = 0.0 }
}

$controlsRootLocalRotation = [ordered]@{ x = 0.0; y = 0.0; z = 0.0; w = 1.0 }
$controlsRootLocalScale = $controlsScale

$controlSubtreeNodes = Get-SubtreeNodes -NodeMap $controlsNodeMap -ChildMap $controlsChildMap -RootNodeId $controlsSurfaceSourceId
$controlNodeIdMap = @{}
foreach ($sourceNode in $controlSubtreeNodes) {
    $sourceNodeId = [string]$sourceNode.nodeId
    $mappedNodeId = if ($sourceNodeId -eq $controlsSurfaceSourceId) { "control_surface" } else { "control__" + $sourceNodeId }
    if ($usedNodeIds.Contains($mappedNodeId)) {
        throw "Duplicate nodeId detected while mapping control surface geometry: $mappedNodeId"
    }

    [void]$usedNodeIds.Add($mappedNodeId)
    $controlNodeIdMap[$sourceNodeId] = $mappedNodeId
}

foreach ($sourceNode in $controlSubtreeNodes) {
    $sourceNodeId = [string]$sourceNode.nodeId
    $newNodeId = $controlNodeIdMap[$sourceNodeId]
    $newParentId = if ($sourceNodeId -eq $controlsSurfaceSourceId) {
        $controlsParentNodeId
    }
    elseif ($controlNodeIdMap.ContainsKey([string]$sourceNode.parentNodeId)) {
        $controlNodeIdMap[[string]$sourceNode.parentNodeId]
    }
    else {
        "control_surface"
    }

    $localPosition = $sourceNode.localPosition
    $localRotation = $sourceNode.localRotation
    $localScale = $sourceNode.localScale
    $displayName = [string]$sourceNode.displayName

    if ($sourceNodeId -eq $controlsSurfaceSourceId) {
        $localPosition = $controlsRootLocalPosition
        $localRotation = $controlsRootLocalRotation
        $localScale = $controlsRootLocalScale
        $displayName = "control_surface"
    }

    $newNodes.Add((New-GeometryNode `
        -NodeId $newNodeId `
        -ParentNodeId $newParentId `
        -DisplayName $displayName `
        -LocalPosition $localPosition `
        -LocalRotation $localRotation `
        -LocalScale $localScale `
        -MeshRefIds @($sourceNode.meshRefIds)
    ))
}

if ($usedNodeIds.Contains("control_collider")) {
    throw "Duplicate nodeId detected while adding control collider."
}
[void]$usedNodeIds.Add("control_collider")
$newNodes.Add((New-GeometryNode `
    -NodeId "control_collider" `
    -ParentNodeId "control_surface" `
    -DisplayName "control_collider" `
    -LocalPosition ([ordered]@{ x = 0.0; y = 0.0; z = 0.0 }) `
    -LocalRotation ([ordered]@{ x = 0.0; y = 0.0; z = 0.0; w = 1.0 }) `
    -LocalScale ([ordered]@{ x = 1.0; y = 1.0; z = 1.0 }) `
    -MeshRefIds @()
))

$materialEntries = New-Object System.Collections.Generic.List[object]
$seenMaterialIds = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
Copy-Materials -TargetList $materialEntries -Entries $screenMaterials.materials -SeenMaterialIds $seenMaterialIds
Copy-Materials -TargetList $materialEntries -Entries $controlsMaterials.materials -SeenMaterialIds $seenMaterialIds

$meshEntries = New-Object System.Collections.Generic.List[object]
$seenMeshIds = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
Copy-Meshes -TargetList $meshEntries -Entries $screenGeometry.meshes -SeenMeshIds $seenMeshIds
Copy-Meshes -TargetList $meshEntries -Entries $controlsGeometry.meshes -SeenMeshIds $seenMeshIds

$resolvedScreenGlassNodeId = ""
if (-not [string]::IsNullOrWhiteSpace([string]$screenSlot.screenGlassNodeId)) {
    $screenGlassSourceId = [string]$screenSlot.screenGlassNodeId
    if ($screenNodeIdMap.ContainsKey($screenGlassSourceId)) {
        $resolvedScreenGlassNodeId = $screenNodeIdMap[$screenGlassSourceId]
    }
    else {
        $resolvedScreenGlassNodeId = $screenGlassSourceId
    }
}

$resolvedControlsAnchorNodeId = if (-not [string]::IsNullOrWhiteSpace($controlsAnchorSourceId) -and $screenNodeIdMap.ContainsKey($controlsAnchorSourceId)) {
    $screenNodeIdMap[$controlsAnchorSourceId]
}
else {
    ""
}

$resolvedBottomAnchorNodeId = if (-not [string]::IsNullOrWhiteSpace($bottomAnchorSourceId) -and $screenNodeIdMap.ContainsKey($bottomAnchorSourceId)) {
    $screenNodeIdMap[$bottomAnchorSourceId]
}
else {
    ""
}

$mergedTags = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
foreach ($tag in @("frameangel", "cua_player", "host", "screen", "meta_ui", "video_player", $resolvedShellId, $resolvedDeviceClass)) {
    if (-not [string]::IsNullOrWhiteSpace($tag)) {
        [void]$mergedTags.Add($tag)
    }
}

$composedGeometry = [ordered]@{
    schemaVersion = "innerpiece_export_v1"
    displayName = $resolvedHostDisplayName
    sourceKind = "composed_package_v1"
    sourcePath = ($screenManifest.sourcePath + " + " + $controlsManifest.sourcePath)
    units = "meters"
    tags = @($mergedTags | Sort-Object)
    nodes = $newNodes.ToArray()
    meshes = $meshEntries.ToArray()
}

$composedMaterials = [ordered]@{
    schemaVersion = "innerpiece_materials_v1"
    materials = $materialEntries.ToArray()
}

$resolvedDefaultDisconnectStateId = [string]$screenContract.defaultDisconnectStateId
if ([string]::IsNullOrWhiteSpace($resolvedDefaultDisconnectStateId)) {
    $resolvedDefaultDisconnectStateId = "media_controls"
}

$composedScreens = [ordered]@{
    schemaVersion = "frameangel_screen_contract_v1"
    shellId = $resolvedShellId
    defaultDisconnectStateId = $resolvedDefaultDisconnectStateId
    surfaceTargetId = "player:screen"
    deviceClass = $resolvedDeviceClass
    orientationSupport = $resolvedOrientationSupport
    defaultAspectMode = $resolvedDefaultAspectMode
    safeCornerRadius = [double]$resolvedSafeCornerRadius
    inputStyle = $resolvedInputStyle
    autoOrientToGround = [bool]$resolvedAutoOrientToGround
    slots = @(
        [ordered]@{
            slotId = "main"
            displayId = "player_main"
            surfaceTargetId = "player:screen"
            disconnectStateId = [string]$screenSlot.disconnectStateId
            screenSurfaceNodeId = "screen_surface"
            screenGlassNodeId = $resolvedScreenGlassNodeId
            screenApertureNodeId = ""
            disconnectSurfaceNodeId = if ([string]::IsNullOrWhiteSpace($disconnectSurfaceSourceId)) { "" } else { "disconnect_surface" }
        }
    )
}

$composedElements = foreach ($element in @($controlsContract.elements)) {
    $sourceElementNodeId = [string]$element.nodeId
    $sourceElementColliderNodeId = [string]$element.colliderNodeId
    [ordered]@{
        elementId = [string]$element.elementId
        elementLabel = [string]$element.elementLabel
        actionId = [string]$element.actionId
        nodeId = if ($controlNodeIdMap.ContainsKey($sourceElementNodeId)) { $controlNodeIdMap[$sourceElementNodeId] } elseif ($sourceElementNodeId -eq $controlsSurfaceSourceId) { "control_surface" } else { "control_surface" }
        colliderNodeId = if ($controlNodeIdMap.ContainsKey($sourceElementColliderNodeId)) { $controlNodeIdMap[$sourceElementColliderNodeId] } elseif ($sourceElementColliderNodeId -eq $controlsSurfaceSourceId) { "control_surface" } elseif (-not [string]::IsNullOrWhiteSpace($sourceElementColliderNodeId)) { "control_surface" } else { "control_collider" }
        elementKind = [string]$element.elementKind
        valueKind = [string]$element.valueKind
        normalizedRect = $element.normalizedRect
        readOnly = [bool]$element.readOnly
    }
}

$composedControls = [ordered]@{
    schemaVersion = "frameangel_control_surface_contract_v1"
    controlSurfaceId = $resolvedHostResourceId + "_controls"
    controlSurfaceLabel = $resolvedHostDisplayName + " Controls"
    controlFamilyId = [string]$controlsContract.controlFamilyId
    controlThemeId = [string]$controlsContract.controlThemeId
    controlThemeLabel = [string]$controlsContract.controlThemeLabel
    controlThemeVariantId = [string]$controlsContract.controlThemeVariantId
    controlThemeAssetPath = [string]$controlsContract.controlThemeAssetPath
    controlThemeAssetGuid = [string]$controlsContract.controlThemeAssetGuid
    toolkitCategory = [string]$controlsContract.toolkitCategory
    sourcePrefabAssetPath = [string]$controlsContract.sourcePrefabAssetPath
    layoutSource = [string]$controlsContract.layoutSource
    targetDisplayIds = @("player_main")
    defaultTargetDisplayId = "player_main"
    surfaceNodeId = "control_surface"
    colliderNodeId = "control_collider"
    surfaceWidthMeters = [double]$controlsContract.surfaceWidthMeters
    surfaceHeightMeters = [double]$controlsContract.surfaceHeightMeters
    elements = @($composedElements)
}

$composedManifest = [ordered]@{
    schemaVersion = "faipe_package_v1"
    packageId = $resolvedHostPackageId
    resourceId = $resolvedHostResourceId
    displayName = $resolvedHostDisplayName
    exporterVersion = "frameangel.compose.v2"
    exportedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    sourceKind = "composed_package_v1"
    sourcePath = ($screenManifest.packageId + " + " + $controlsManifest.packageId)
    geometryPath = "geometry.innerpiece.json"
    materialsPath = "materials.innerpiece.json"
    screensPath = "screens.innerpiece.json"
    controlsPath = "controls.innerpiece.json"
    previewPath = ""
    featureFlags = @(
        "screen_contract_present",
        "control_surface_present",
        "host_object_contract",
        "composed_from_existing_exports",
        "full_shell_geometry_present"
    )
    warnings = @(
        ("composed from shell export " + $screenManifest.packageId + " and controls export " + $controlsManifest.packageId),
        ("controls anchored via " + $(if ([string]::IsNullOrWhiteSpace($controlsAnchorSourceId)) { "fallback_monitor_layout" } else { $controlsAnchorSourceId })),
        "intended as canonical authored host package basis for fa_cua_player"
    )
}

$compositionReceipt = [ordered]@{
    schemaVersion = "frameangel_cua_player_host_composition_receipt_v2"
    createdAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    screenPackageRoot = $ScreenPackageRoot
    controlsPackageRoot = $resolvedControlsPackageRoot
    hostProfilePath = if ($null -ne $hostProfile) { $resolvedHostProfilePath } else { "" }
    outputRoot = $OutputRoot
    packageId = $composedManifest.packageId
    resourceId = $composedManifest.resourceId
    shellId = $resolvedShellId
    deviceClass = $resolvedDeviceClass
    orientationSupport = $resolvedOrientationSupport
    controlsAnchorSourceNodeId = $controlsAnchorSourceId
    bottomAnchorSourceNodeId = $bottomAnchorSourceId
    canonicalNodes = [ordered]@{
        root = "fa_cua_player_host"
        screenSurface = "screen_surface"
        disconnectSurface = if ([string]::IsNullOrWhiteSpace($disconnectSurfaceSourceId)) { "" } else { "disconnect_surface" }
        controlSurface = "control_surface"
        controlCollider = "control_collider"
    }
}

$outputHostProfile = [ordered]@{
    schemaVersion = "frameangel_player_host_shell_profile_v1"
    shellKey = $resolvedShellId
    hostDisplayName = $resolvedHostDisplayName
    hostResourceId = $resolvedHostResourceId
    hostPackageId = $resolvedHostPackageId
    packageRootPath = $OutputRoot
    packageId = $resolvedHostPackageId
    resourceId = $resolvedHostResourceId
    scenePath = [string](Get-OptionalPropertyValue -Object $hostProfile -Name "scenePath" -DefaultValue "")
    rootObjectName = [string](Get-OptionalPropertyValue -Object $hostProfile -Name "rootObjectName" -DefaultValue "fa_cua_player_host")
    screenSurfaceNodeId = "screen_surface"
    disconnectSurfaceNodeId = if ([string]::IsNullOrWhiteSpace($disconnectSurfaceSourceId)) { "" } else { "disconnect_surface" }
    screenGlassNodeId = $resolvedScreenGlassNodeId
    controlsAnchorNodeId = $resolvedControlsAnchorNodeId
    bottomAnchorNodeId = $resolvedBottomAnchorNodeId
    deviceClass = $resolvedDeviceClass
    orientationSupport = $resolvedOrientationSupport
    defaultAspectMode = $resolvedDefaultAspectMode
    inputStyle = $resolvedInputStyle
    autoOrientToGround = [bool]$resolvedAutoOrientToGround
    safeCornerRadius = [double]$resolvedSafeCornerRadius
}

if (Test-Path -LiteralPath $OutputRoot) {
    Remove-Item -LiteralPath $OutputRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
Write-JsonFile -Path (Join-Path $OutputRoot "manifest.json") -Value $composedManifest
Write-JsonFile -Path (Join-Path $OutputRoot "geometry.innerpiece.json") -Value $composedGeometry
Write-JsonFile -Path (Join-Path $OutputRoot "materials.innerpiece.json") -Value $composedMaterials
Write-JsonFile -Path (Join-Path $OutputRoot "screens.innerpiece.json") -Value $composedScreens
Write-JsonFile -Path (Join-Path $OutputRoot "controls.innerpiece.json") -Value $composedControls
Write-JsonFile -Path (Join-Path $OutputRoot "host_profile.json") -Value $outputHostProfile
Write-JsonFile -Path (Join-Path $OutputRoot "composition_receipt.json") -Value $compositionReceipt

if ($Deploy) {
    if (Test-Path -LiteralPath $DeployRoot) {
        Remove-Item -LiteralPath $DeployRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Path (Split-Path -Parent $DeployRoot) -Force | Out-Null
    Copy-Item -LiteralPath $OutputRoot -Destination $DeployRoot -Recurse -Force
}

[pscustomobject]@{
    packageId = $composedManifest.packageId
    resourceId = $composedManifest.resourceId
    shellId = $resolvedShellId
    outputRoot = $OutputRoot
    deployed = [bool]$Deploy
    deployRoot = if ($Deploy) { $DeployRoot } else { "" }
}
