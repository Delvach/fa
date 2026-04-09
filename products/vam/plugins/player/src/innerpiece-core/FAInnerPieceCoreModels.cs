using System;
using UnityEngine;

public static class FAInnerPieceSchemas
{
    public const string ExportV1 = "innerpiece_export_v1";
    public const string CatalogV1 = "frameangel_innerpiece_catalog_v1";
    public const string ResourceV1 = "frameangel_innerpiece_resource_v1";
    public const string InstanceStateV1 = "session_innerpiece_instance_state_v1";
    public const string ReceiptV1 = "session_innerpiece_receipt_v1";
    public const string ImportReceiptV1 = "innerpiece_import_receipt_v1";
    public const string PackageManifestV1 = "faipe_package_v1";
    public const string MaterialsV1 = "innerpiece_materials_v1";
    public const string ScreenContractV1 = "frameangel_screen_contract_v1";
    public const string ControlSurfaceV1 = "frameangel_control_surface_contract_v1";
}

[Serializable]
public sealed class FAInnerPieceCatalogState
{
    public string schemaVersion = FAInnerPieceSchemas.CatalogV1;
    public FAInnerPieceCatalogEntry[] entries = new FAInnerPieceCatalogEntry[0];
}

[Serializable]
public sealed class FAInnerPieceCatalogEntry
{
    public string resourceId = "";
    public string displayName = "";
    public string fingerprint = "";
    public string sourceKind = "";
    public string sourcePath = "";
    public bool archived = false;
    public string[] tags = new string[0];
    public FAInnerPieceBoundsData bounds = new FAInnerPieceBoundsData();
    public int nodeCount = 0;
    public int meshCount = 0;
    public string lastImportedUtc = "";
}

[Serializable]
public sealed class FAInnerPieceExportPackage
{
    public string schemaVersion = FAInnerPieceSchemas.ExportV1;
    public string displayName = "";
    public string sourceKind = "";
    public string sourcePath = "";
    public string units = "meters";
    public string[] tags = new string[0];
    public FAInnerPieceNodeData[] nodes = new FAInnerPieceNodeData[0];
    public FAInnerPieceMeshData[] meshes = new FAInnerPieceMeshData[0];
}

[Serializable]
public sealed class FAInnerPieceStoredResource
{
    public string schemaVersion = FAInnerPieceSchemas.ResourceV1;
    public string resourceId = "";
    public string displayName = "";
    public string sourceKind = "";
    public string sourcePath = "";
    public string units = "meters";
    public string fingerprint = "";
    public bool archived = false;
    public string[] tags = new string[0];
    public FAInnerPieceBoundsData bounds = new FAInnerPieceBoundsData();
    public int nodeCount = 0;
    public int meshCount = 0;
    public FAInnerPieceNodeData[] nodes = new FAInnerPieceNodeData[0];
    public FAInnerPieceMeshData[] meshes = new FAInnerPieceMeshData[0];
    public FAInnerPieceScreenContractData screenContract;
    public FAInnerPieceControlSurfaceData controlSurface;
}

[Serializable]
public sealed class FAInnerPieceImportReceipt
{
    public string schemaVersion = FAInnerPieceSchemas.ImportReceiptV1;
    public string resourceId = "";
    public string fingerprint = "";
    public string packagePath = "";
    public string packageKind = "";
    public string manifestPath = "";
    public string geometryPath = "";
    public string materialsPath = "";
    public string screensPath = "";
    public string controlsPath = "";
    public string previewPath = "";
    public string importedAtUtc = "";
    public bool created = false;
    public bool updated = false;
    public string[] archivedExistingResourceIds = new string[0];
    public string[] warnings = new string[0];
}

[Serializable]
public sealed class FAInnerPieceActionReceipt
{
    public string schemaVersion = FAInnerPieceSchemas.ReceiptV1;
    public string actionId = "";
    public string summary = "";
    public string resourceId = "";
    public string instanceId = "";
    public string consumerId = "";
    public string targetType = "";
    public string lastError = "";
    public FAInnerPieceImportReceipt importReceipt;
    public FAInnerPieceStoredResource resource;
    public FAInnerPieceInstanceStateData instanceState;
}

[Serializable]
public sealed class FAInnerPieceInstanceStateData
{
    public string schemaVersion = FAInnerPieceSchemas.InstanceStateV1;
    public string instanceId = "";
    public string resourceId = "";
    public string consumerId = "";
    public string targetType = "";
    public string groupId = "";
    public string rootObjectId = "";
    public string[] spawnedNodeIds = new string[0];
    public FAInnerPieceTransformData rootTransform = new FAInnerPieceTransformData();
    public string screenContractVersion = "";
    public string shellId = "";
    public string deviceClass = "monitor";
    public string orientationSupport = "landscape";
    public string defaultAspectMode = "fit";
    public float safeCornerRadius = 0f;
    public string inputStyle = "fixed";
    public bool autoOrientToGround = false;
    public FAInnerPieceFollowBindingData followBinding = new FAInnerPieceFollowBindingData();
    public FAInnerPieceScreenSlotRuntimeState[] screenSlots = new FAInnerPieceScreenSlotRuntimeState[0];
    public FAInnerPieceControlSurfaceData controlSurface;
    public string lastError = "";
}

[Serializable]
public sealed class FAInnerPieceFollowBindingData
{
    public string anchorAtomUid = "";
    public bool followPosition = false;
    public bool followRotation = false;
    public Vector3 localPositionOffset = Vector3.zero;
    public Quaternion localRotationOffset = Quaternion.identity;
}

[Serializable]
public sealed class FAInnerPieceTransformData
{
    public Vector3 position = Vector3.zero;
    public Quaternion rotation = Quaternion.identity;
    public Vector3 scale = Vector3.one;
}

[Serializable]
public sealed class FAInnerPieceNodeData
{
    public string nodeId = "";
    public string parentNodeId = "";
    public string displayName = "";
    public Vector3 localPosition = Vector3.zero;
    public Quaternion localRotation = Quaternion.identity;
    public Vector3 localScale = Vector3.one;
    public string[] meshRefIds = new string[0];
}

[Serializable]
public sealed class FAInnerPieceMeshData
{
    public string meshId = "";
    public string materialRefId = "";
    public int submeshIndex = 0;
    public Vector3[] vertices = new Vector3[0];
    public int[] triangleIndices = new int[0];
    public Vector3[] normals = new Vector3[0];
    public Vector2[] uv0 = new Vector2[0];
    public FAInnerPieceBoundsData localBounds = new FAInnerPieceBoundsData();
}

[Serializable]
public sealed class FAInnerPieceBoundsData
{
    public Vector3 center = Vector3.zero;
    public Vector3 size = Vector3.zero;
}

[Serializable]
public sealed class FAInnerPieceScreenContractData
{
    public string schemaVersion = FAInnerPieceSchemas.ScreenContractV1;
    public string shellId = "";
    public string defaultDisconnectStateId = "";
    public string surfaceTargetId = "player:screen";
    public string deviceClass = "monitor";
    public string orientationSupport = "landscape";
    public string defaultAspectMode = "fit";
    public float safeCornerRadius = 0f;
    public string inputStyle = "fixed";
    public bool autoOrientToGround = false;
    public FAInnerPieceScreenSlotData[] slots = new FAInnerPieceScreenSlotData[0];
}

[Serializable]
public sealed class FAInnerPieceScreenSlotData
{
    public string slotId = "main";
    public string displayId = "main";
    public string surfaceTargetId = "player:screen";
    public string disconnectStateId = "";
    public string screenSurfaceNodeId = "";
    public string screenGlassNodeId = "";
    public string screenApertureNodeId = "";
    public string disconnectSurfaceNodeId = "";
}

[Serializable]
public sealed class FAInnerPiecePlaneData
{
    public Vector3 center = Vector3.zero;
    public Vector3 right = Vector3.right;
    public Vector3 up = Vector3.up;
    public Vector3 forward = Vector3.forward;
    public float widthMeters = 0f;
    public float heightMeters = 0f;
    public float depthMeters = 0f;
}

[Serializable]
public sealed class FAInnerPieceScreenSlotRuntimeState
{
    public string slotId = "main";
    public string displayId = "main";
    public string surfaceTargetId = "player:screen";
    public string disconnectStateId = "";
    public string screenSurfaceNodeId = "";
    public string screenGlassNodeId = "";
    public string screenApertureNodeId = "";
    public string disconnectSurfaceNodeId = "";
    public bool disconnectSurfaceVisible = true;
    public string boundState = "unbound";
    public FAInnerPiecePlaneData plane = new FAInnerPiecePlaneData();
}

[Serializable]
public sealed class FAInnerPieceControlSurfaceData
{
    public string schemaVersion = FAInnerPieceSchemas.ControlSurfaceV1;
    public string controlSurfaceId = "";
    public string controlSurfaceLabel = "";
    public string controlFamilyId = "";
    public string controlThemeId = "";
    public string controlThemeLabel = "";
    public string controlThemeVariantId = "";
    public string controlThemeAssetPath = "";
    public string controlThemeAssetGuid = "";
    public string toolkitCategory = "";
    public string sourcePrefabAssetPath = "";
    public string layoutSource = "canvas_export_v1";
    public string[] targetDisplayIds = new string[0];
    public string defaultTargetDisplayId = "";
    public string surfaceNodeId = "";
    public string colliderNodeId = "";
    public float surfaceWidthMeters = 0f;
    public float surfaceHeightMeters = 0f;
    public FAInnerPieceControlElementData[] elements = new FAInnerPieceControlElementData[0];
}

[Serializable]
public sealed class FAInnerPieceControlElementData
{
    public string elementId = "";
    public string elementLabel = "";
    public string actionId = "";
    public string nodeId = "";
    public string colliderNodeId = "";
    public string elementKind = "button";
    public string valueKind = "none";
    public FAInnerPieceNormalizedRectData normalizedRect = new FAInnerPieceNormalizedRectData();
    public bool readOnly = false;
}

[Serializable]
public sealed class FAInnerPieceNormalizedRectData
{
    public float x = 0f;
    public float y = 0f;
    public float width = 0f;
    public float height = 0f;
}
