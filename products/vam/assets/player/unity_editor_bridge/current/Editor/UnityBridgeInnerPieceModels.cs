using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace FrameAngel.UnityEditorBridge
{
    [Serializable]
    internal class UnityInnerPieceExportSelectionArgs
    {
        [JsonProperty("displayNameOverride")]
        public string DisplayNameOverride { get; set; } = "";

        [JsonProperty("outputPath")]
        public string OutputPath { get; set; } = "";

        [JsonProperty("capturePreview")]
        public bool CapturePreview { get; set; } = true;

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new List<string>();

        [JsonProperty("tagsCsv")]
        public string TagsCsv { get; set; } = "";
    }

    [Serializable]
    internal sealed class UnityInnerPieceExportProjectAssetArgs : UnityInnerPieceExportSelectionArgs
    {
        [JsonProperty("assetPath")]
        public string AssetPath { get; set; } = "";

        [JsonProperty("assetGuid")]
        public string AssetGuid { get; set; } = "";
    }

    [Serializable]
    internal sealed class UnityInnerPieceCapturePreviewArgs
    {
        [JsonProperty("assetPath")]
        public string AssetPath { get; set; } = "";

        [JsonProperty("assetGuid")]
        public string AssetGuid { get; set; } = "";

        [JsonProperty("outputPath")]
        public string OutputPath { get; set; } = "";

        [JsonProperty("label")]
        public string Label { get; set; } = "";

        [JsonProperty("width")]
        public int Width { get; set; } = 512;

        [JsonProperty("height")]
        public int Height { get; set; } = 512;
    }

    [Serializable]
    internal sealed class UnityInnerPiecePolicyProfile
    {
        [JsonProperty("schemaVersion")]
        public string SchemaVersion { get; set; } = "faipe_policy_profile_v1";

        [JsonProperty("profileId")]
        public string ProfileId { get; set; } = "internal_permissive_v1";

        [JsonProperty("displayName")]
        public string DisplayName { get; set; } = "Internal Permissive";

        [JsonProperty("maxNodeCount")]
        public int MaxNodeCount { get; set; } = 10000;

        [JsonProperty("maxMeshCount")]
        public int MaxMeshCount { get; set; } = 10000;

        [JsonProperty("maxTotalVertexCount")]
        public int MaxTotalVertexCount { get; set; } = 4000000;

        [JsonProperty("maxTotalTriangleCount")]
        public int MaxTotalTriangleCount { get; set; } = 4000000;

        [JsonProperty("maxPackageBytes")]
        public long MaxPackageBytes { get; set; } = 2147483648L;

        [JsonProperty("maxTextureCount")]
        public int MaxTextureCount { get; set; } = 128;

        [JsonProperty("maxTextureResolution")]
        public int MaxTextureResolution { get; set; } = 8192;

        [JsonProperty("allowedFeatureFlags")]
        public List<string> AllowedFeatureFlags { get; set; } = new List<string>();

        [JsonProperty("allowedSourceClasses")]
        public List<string> AllowedSourceClasses { get; set; } = new List<string>();

        [JsonProperty("batchExportEnabled")]
        public bool BatchExportEnabled { get; set; } = true;

        public static UnityInnerPiecePolicyProfile CreateDefault()
        {
            UnityInnerPiecePolicyProfile profile = new UnityInnerPiecePolicyProfile();
            profile.AllowedFeatureFlags.Add("static_geometry");
            profile.AllowedFeatureFlags.Add("material_base_color");
            profile.AllowedFeatureFlags.Add("submesh_material_slots");
            profile.AllowedFeatureFlags.Add("vertex_colors_present");
            profile.AllowedFeatureFlags.Add("texture_refs_present");
            profile.AllowedFeatureFlags.Add("control_surface_present");
            profile.AllowedFeatureFlags.Add("control_slider_present");
            profile.AllowedFeatureFlags.Add("control_readonly_present");
            profile.AllowedSourceClasses.Add("single_static_mesh");
            profile.AllowedSourceClasses.Add("static_multi_mesh_prop");
            profile.AllowedSourceClasses.Add("static_prefab_hierarchy");
            profile.AllowedSourceClasses.Add("large_static_set_piece");
            profile.AllowedSourceClasses.Add("control_surface_canvas");
            return profile;
        }
    }

    [Serializable]
    internal sealed class UnityInnerPieceInspectionData
    {
        [JsonProperty("exportReady")]
        public bool ExportReady { get; set; }

        [JsonProperty("assetClass")]
        public string AssetClass { get; set; } = "";

        [JsonProperty("recommendedExportStage")]
        public string RecommendedExportStage { get; set; } = "";

        [JsonProperty("sourceKind")]
        public string SourceKind { get; set; } = "";

        [JsonProperty("sourcePath")]
        public string SourcePath { get; set; } = "";

        [JsonProperty("displayName")]
        public string DisplayName { get; set; } = "";

        [JsonProperty("hasScreenContract")]
        public bool HasScreenContract { get; set; }

        [JsonProperty("screenContractVersion")]
        public string ScreenContractVersion { get; set; } = "";

        [JsonProperty("shellId")]
        public string ShellId { get; set; } = "";

        [JsonProperty("screenSlotCount")]
        public int ScreenSlotCount { get; set; }

        [JsonProperty("hasControlSurfaceContract")]
        public bool HasControlSurfaceContract { get; set; }

        [JsonProperty("controlSurfaceContractVersion")]
        public string ControlSurfaceContractVersion { get; set; } = "";

        [JsonProperty("controlSurfaceId")]
        public string ControlSurfaceId { get; set; } = "";

        [JsonProperty("controlElementCount")]
        public int ControlElementCount { get; set; }

        [JsonProperty("nodeCount")]
        public int NodeCount { get; set; }

        [JsonProperty("meshCount")]
        public int MeshCount { get; set; }

        [JsonProperty("materialCount")]
        public int MaterialCount { get; set; }

        [JsonProperty("rendererCount")]
        public int RendererCount { get; set; }

        [JsonProperty("totalVertexCount")]
        public int TotalVertexCount { get; set; }

        [JsonProperty("totalTriangleCount")]
        public int TotalTriangleCount { get; set; }

        [JsonProperty("bounds")]
        public UnityBounds3 Bounds { get; set; } = new UnityBounds3();

        [JsonProperty("featureFlags")]
        public List<string> FeatureFlags { get; set; } = new List<string>();

        [JsonProperty("warnings")]
        public List<string> Warnings { get; set; } = new List<string>();

        [JsonProperty("materials")]
        public List<UnityInnerPieceMaterialSummary> Materials { get; set; } = new List<UnityInnerPieceMaterialSummary>();

        [JsonProperty("policyProfile")]
        public UnityInnerPiecePolicyProfile PolicyProfile { get; set; } = UnityInnerPiecePolicyProfile.CreateDefault();
    }

    [Serializable]
    internal sealed class UnityInnerPieceMaterialSummary
    {
        [JsonProperty("materialRefId")]
        public string MaterialRefId { get; set; } = "";

        [JsonProperty("displayName")]
        public string DisplayName { get; set; } = "";

        [JsonProperty("shaderName")]
        public string ShaderName { get; set; } = "";

        [JsonProperty("baseColorHex")]
        public string BaseColorHex { get; set; } = "#FFFFFF";

        [JsonProperty("hasTexture")]
        public bool HasTexture { get; set; }

        [JsonProperty("textureAssetPath")]
        public string TextureAssetPath { get; set; } = "";

        [JsonProperty("texturePngBase64")]
        public string TexturePngBase64 { get; set; } = "";

        [JsonProperty("hasVertexColors")]
        public bool HasVertexColors { get; set; }

        [JsonProperty("featureFlags")]
        public List<string> FeatureFlags { get; set; } = new List<string>();

        [JsonProperty("warnings")]
        public List<string> Warnings { get; set; } = new List<string>();
    }

    [Serializable]
    public sealed class UnityInnerPieceLastExportSummary
    {
        [JsonProperty("packageId")]
        public string PackageId { get; set; } = "";

        [JsonProperty("resourceId")]
        public string ResourceId { get; set; } = "";

        [JsonProperty("displayName")]
        public string DisplayName { get; set; } = "";

        [JsonProperty("controlSurfaceId")]
        public string ControlSurfaceId { get; set; } = "";

        [JsonProperty("controlFamilyId")]
        public string ControlFamilyId { get; set; } = "";

        [JsonProperty("controlThemeId")]
        public string ControlThemeId { get; set; } = "";

        [JsonProperty("controlThemeLabel")]
        public string ControlThemeLabel { get; set; } = "";

        [JsonProperty("controlThemeVariantId")]
        public string ControlThemeVariantId { get; set; } = "";

        [JsonProperty("controlThemeAssetPath")]
        public string ControlThemeAssetPath { get; set; } = "";

        [JsonProperty("controlThemeAssetGuid")]
        public string ControlThemeAssetGuid { get; set; } = "";

        [JsonProperty("toolkitCategory")]
        public string ToolkitCategory { get; set; } = "";

        [JsonProperty("sourcePrefabAssetPath")]
        public string SourcePrefabAssetPath { get; set; } = "";

        [JsonProperty("sourceKind")]
        public string SourceKind { get; set; } = "";

        [JsonProperty("sourcePath")]
        public string SourcePath { get; set; } = "";

        [JsonProperty("packageRootPath")]
        public string PackageRootPath { get; set; } = "";

        [JsonProperty("geometryPath")]
        public string GeometryPath { get; set; } = "";

        [JsonProperty("materialsPath")]
        public string MaterialsPath { get; set; } = "";

        [JsonProperty("screensPath")]
        public string ScreensPath { get; set; } = "";

        [JsonProperty("controlsPath")]
        public string ControlsPath { get; set; } = "";

        [JsonProperty("previewPath")]
        public string PreviewPath { get; set; } = "";

        [JsonProperty("exportReceiptPath")]
        public string ExportReceiptPath { get; set; } = "";

        [JsonProperty("fingerprint")]
        public string Fingerprint { get; set; } = "";

        [JsonProperty("featureFlags")]
        public List<string> FeatureFlags { get; set; } = new List<string>();

        [JsonProperty("warnings")]
        public List<string> Warnings { get; set; } = new List<string>();

        [JsonProperty("nodeCount")]
        public int NodeCount { get; set; }

        [JsonProperty("meshCount")]
        public int MeshCount { get; set; }

        [JsonProperty("materialCount")]
        public int MaterialCount { get; set; }

        [JsonProperty("totalVertexCount")]
        public int TotalVertexCount { get; set; }

        [JsonProperty("totalTriangleCount")]
        public int TotalTriangleCount { get; set; }

        [JsonProperty("packageBytes")]
        public long PackageBytes { get; set; }

        [JsonProperty("exportDurationMs")]
        public long ExportDurationMs { get; set; }

        [JsonProperty("exportedAtUtc")]
        public string ExportedAtUtc { get; set; } = "";
    }

    [Serializable]
    internal sealed class UnityInnerPieceExportData
    {
        [JsonProperty("inspection")]
        public UnityInnerPieceInspectionData Inspection { get; set; } = new UnityInnerPieceInspectionData();

        [JsonProperty("lastExport")]
        public UnityInnerPieceLastExportSummary LastExport { get; set; } = new UnityInnerPieceLastExportSummary();
    }

    [Serializable]
    internal sealed class FAIPEPackageManifest
    {
        [JsonProperty("schemaVersion")]
        public string SchemaVersion { get; set; } = "faipe_package_v1";

        [JsonProperty("packageId")]
        public string PackageId { get; set; } = "";

        [JsonProperty("resourceId")]
        public string ResourceId { get; set; } = "";

        [JsonProperty("displayName")]
        public string DisplayName { get; set; } = "";

        [JsonProperty("controlSurfaceId")]
        public string ControlSurfaceId { get; set; } = "";

        [JsonProperty("controlFamilyId")]
        public string ControlFamilyId { get; set; } = "";

        [JsonProperty("controlThemeId")]
        public string ControlThemeId { get; set; } = "";

        [JsonProperty("controlThemeLabel")]
        public string ControlThemeLabel { get; set; } = "";

        [JsonProperty("controlThemeVariantId")]
        public string ControlThemeVariantId { get; set; } = "";

        [JsonProperty("controlThemeAssetPath")]
        public string ControlThemeAssetPath { get; set; } = "";

        [JsonProperty("controlThemeAssetGuid")]
        public string ControlThemeAssetGuid { get; set; } = "";

        [JsonProperty("toolkitCategory")]
        public string ToolkitCategory { get; set; } = "";

        [JsonProperty("sourcePrefabAssetPath")]
        public string SourcePrefabAssetPath { get; set; } = "";

        [JsonProperty("exporterVersion")]
        public string ExporterVersion { get; set; } = "";

        [JsonProperty("exportedAtUtc")]
        public string ExportedAtUtc { get; set; } = "";

        [JsonProperty("sourceKind")]
        public string SourceKind { get; set; } = "";

        [JsonProperty("sourcePath")]
        public string SourcePath { get; set; } = "";

        [JsonProperty("geometryPath")]
        public string GeometryPath { get; set; } = "geometry.innerpiece.json";

        [JsonProperty("materialsPath")]
        public string MaterialsPath { get; set; } = "";

        [JsonProperty("screensPath")]
        public string ScreensPath { get; set; } = "";

        [JsonProperty("controlsPath")]
        public string ControlsPath { get; set; } = "";

        [JsonProperty("previewPath")]
        public string PreviewPath { get; set; } = "";

        [JsonProperty("featureFlags")]
        public List<string> FeatureFlags { get; set; } = new List<string>();

        [JsonProperty("warnings")]
        public List<string> Warnings { get; set; } = new List<string>();
    }

    [Serializable]
    internal sealed class FAIPEGeometryPackage
    {
        [JsonProperty("schemaVersion")]
        public string SchemaVersion { get; set; } = "innerpiece_export_v1";

        [JsonProperty("displayName")]
        public string DisplayName { get; set; } = "";

        [JsonProperty("sourceKind")]
        public string SourceKind { get; set; } = "";

        [JsonProperty("sourcePath")]
        public string SourcePath { get; set; } = "";

        [JsonProperty("units")]
        public string Units { get; set; } = "meters";

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new List<string>();

        [JsonProperty("nodes")]
        public List<FAIPEExportNode> Nodes { get; set; } = new List<FAIPEExportNode>();

        [JsonProperty("meshes")]
        public List<FAIPEExportMesh> Meshes { get; set; } = new List<FAIPEExportMesh>();
    }

    [Serializable]
    internal sealed class FAIPEExportNode
    {
        [JsonProperty("nodeId")]
        public string NodeId { get; set; } = "";

        [JsonProperty("parentNodeId")]
        public string ParentNodeId { get; set; } = "";

        [JsonProperty("displayName")]
        public string DisplayName { get; set; } = "";

        [JsonProperty("localPosition")]
        public UnityVector3 LocalPosition { get; set; } = new UnityVector3();

        [JsonProperty("localRotation")]
        public UnityQuaternion LocalRotation { get; set; } = new UnityQuaternion();

        [JsonProperty("localScale")]
        public UnityVector3 LocalScale { get; set; } = new UnityVector3 { X = 1f, Y = 1f, Z = 1f };

        [JsonProperty("meshRefIds")]
        public List<string> MeshRefIds { get; set; } = new List<string>();
    }

    [Serializable]
    internal sealed class FAIPEExportMesh
    {
        [JsonProperty("meshId")]
        public string MeshId { get; set; } = "";

        [JsonProperty("materialRefId")]
        public string MaterialRefId { get; set; } = "";

        [JsonProperty("submeshIndex")]
        public int SubmeshIndex { get; set; }

        [JsonProperty("vertices")]
        public List<UnityVector3> Vertices { get; set; } = new List<UnityVector3>();

        [JsonProperty("triangleIndices")]
        public List<int> TriangleIndices { get; set; } = new List<int>();

        [JsonProperty("normals")]
        public List<UnityVector3> Normals { get; set; } = new List<UnityVector3>();

        [JsonProperty("uv0")]
        public List<UnityVector2> Uv0 { get; set; } = new List<UnityVector2>();

        [JsonProperty("localBounds")]
        public UnityBounds3 LocalBounds { get; set; } = new UnityBounds3();
    }

    [Serializable]
    internal sealed class FAIPEMaterialsPackage
    {
        [JsonProperty("schemaVersion")]
        public string SchemaVersion { get; set; } = "innerpiece_materials_v1";

        [JsonProperty("materials")]
        public List<FAIPEMaterialEntry> Materials { get; set; } = new List<FAIPEMaterialEntry>();
    }

    [Serializable]
    internal sealed class FAIPEScreenContractPackage
    {
        [JsonProperty("schemaVersion")]
        public string SchemaVersion { get; set; } = "frameangel_screen_contract_v1";

        [JsonProperty("shellId")]
        public string ShellId { get; set; } = "";

        [JsonProperty("defaultDisconnectStateId")]
        public string DefaultDisconnectStateId { get; set; } = "";

        [JsonProperty("surfaceTargetId")]
        public string SurfaceTargetId { get; set; } = "player:screen";

        [JsonProperty("slots")]
        public List<FAIPEScreenSlotEntry> Slots { get; set; } = new List<FAIPEScreenSlotEntry>();
    }

    [Serializable]
    internal sealed class FAIPEScreenSlotEntry
    {
        [JsonProperty("slotId")]
        public string SlotId { get; set; } = "main";

        [JsonProperty("surfaceTargetId")]
        public string SurfaceTargetId { get; set; } = "player:screen";

        [JsonProperty("disconnectStateId")]
        public string DisconnectStateId { get; set; } = "";

        [JsonProperty("screenSurfaceNodeId")]
        public string ScreenSurfaceNodeId { get; set; } = "";

        [JsonProperty("screenGlassNodeId")]
        public string ScreenGlassNodeId { get; set; } = "";

        [JsonProperty("screenApertureNodeId")]
        public string ScreenApertureNodeId { get; set; } = "";

        [JsonProperty("disconnectSurfaceNodeId")]
        public string DisconnectSurfaceNodeId { get; set; } = "";
    }

    [Serializable]
    internal sealed class FAIPEControlSurfacePackage
    {
        [JsonProperty("schemaVersion")]
        public string SchemaVersion { get; set; } = "frameangel_control_surface_contract_v1";

        [JsonProperty("controlSurfaceId")]
        public string ControlSurfaceId { get; set; } = "";

        [JsonProperty("controlSurfaceLabel")]
        public string ControlSurfaceLabel { get; set; } = "";

        [JsonProperty("controlFamilyId")]
        public string ControlFamilyId { get; set; } = "";

        [JsonProperty("controlThemeId")]
        public string ControlThemeId { get; set; } = "";

        [JsonProperty("controlThemeLabel")]
        public string ControlThemeLabel { get; set; } = "";

        [JsonProperty("controlThemeVariantId")]
        public string ControlThemeVariantId { get; set; } = "";

        [JsonProperty("controlThemeAssetPath")]
        public string ControlThemeAssetPath { get; set; } = "";

        [JsonProperty("controlThemeAssetGuid")]
        public string ControlThemeAssetGuid { get; set; } = "";

        [JsonProperty("toolkitCategory")]
        public string ToolkitCategory { get; set; } = "";

        [JsonProperty("sourcePrefabAssetPath")]
        public string SourcePrefabAssetPath { get; set; } = "";

        [JsonProperty("layoutSource")]
        public string LayoutSource { get; set; } = "canvas_export_v1";

        [JsonProperty("targetDisplayIds")]
        public List<string> TargetDisplayIds { get; set; } = new List<string>();

        [JsonProperty("defaultTargetDisplayId")]
        public string DefaultTargetDisplayId { get; set; } = "";

        [JsonProperty("surfaceNodeId")]
        public string SurfaceNodeId { get; set; } = "";

        [JsonProperty("colliderNodeId")]
        public string ColliderNodeId { get; set; } = "";

        [JsonProperty("surfaceWidthMeters")]
        public float SurfaceWidthMeters { get; set; }

        [JsonProperty("surfaceHeightMeters")]
        public float SurfaceHeightMeters { get; set; }

        [JsonProperty("elements")]
        public List<FAIPEControlSurfaceElementEntry> Elements { get; set; } = new List<FAIPEControlSurfaceElementEntry>();
    }

    [Serializable]
    internal sealed class FAIPEControlSurfaceElementEntry
    {
        [JsonProperty("elementId")]
        public string ElementId { get; set; } = "";

        [JsonProperty("elementLabel")]
        public string ElementLabel { get; set; } = "";

        [JsonProperty("actionId")]
        public string ActionId { get; set; } = "";

        [JsonProperty("nodeId")]
        public string NodeId { get; set; } = "";

        [JsonProperty("colliderNodeId")]
        public string ColliderNodeId { get; set; } = "";

        [JsonProperty("elementKind")]
        public string ElementKind { get; set; } = "button";

        [JsonProperty("valueKind")]
        public string ValueKind { get; set; } = "none";

        [JsonProperty("normalizedRect")]
        public UnityNormalizedRect NormalizedRect { get; set; } = new UnityNormalizedRect();

        [JsonProperty("readOnly")]
        public bool ReadOnly { get; set; }
    }

    [Serializable]
    internal sealed class FAIPEMaterialEntry
    {
        [JsonProperty("materialRefId")]
        public string MaterialRefId { get; set; } = "";

        [JsonProperty("displayName")]
        public string DisplayName { get; set; } = "";

        [JsonProperty("shaderName")]
        public string ShaderName { get; set; } = "";

        [JsonProperty("baseColorHex")]
        public string BaseColorHex { get; set; } = "#FFFFFF";

        [JsonProperty("textureAssetPath")]
        public string TextureAssetPath { get; set; } = "";

        [JsonProperty("texturePngBase64")]
        public string TexturePngBase64 { get; set; } = "";

        [JsonProperty("featureFlags")]
        public List<string> FeatureFlags { get; set; } = new List<string>();

        [JsonProperty("warnings")]
        public List<string> Warnings { get; set; } = new List<string>();
    }

    [Serializable]
    internal sealed class FAIPEExportReceipt
    {
        [JsonProperty("schemaVersion")]
        public string SchemaVersion { get; set; } = "faipe_export_receipt_v1";

        [JsonProperty("packageId")]
        public string PackageId { get; set; } = "";

        [JsonProperty("resourceId")]
        public string ResourceId { get; set; } = "";

        [JsonProperty("controlSurfaceId")]
        public string ControlSurfaceId { get; set; } = "";

        [JsonProperty("controlFamilyId")]
        public string ControlFamilyId { get; set; } = "";

        [JsonProperty("controlThemeId")]
        public string ControlThemeId { get; set; } = "";

        [JsonProperty("controlThemeLabel")]
        public string ControlThemeLabel { get; set; } = "";

        [JsonProperty("controlThemeVariantId")]
        public string ControlThemeVariantId { get; set; } = "";

        [JsonProperty("controlThemeAssetPath")]
        public string ControlThemeAssetPath { get; set; } = "";

        [JsonProperty("controlThemeAssetGuid")]
        public string ControlThemeAssetGuid { get; set; } = "";

        [JsonProperty("toolkitCategory")]
        public string ToolkitCategory { get; set; } = "";

        [JsonProperty("sourcePrefabAssetPath")]
        public string SourcePrefabAssetPath { get; set; } = "";

        [JsonProperty("fingerprint")]
        public string Fingerprint { get; set; } = "";

        [JsonProperty("sourceKind")]
        public string SourceKind { get; set; } = "";

        [JsonProperty("sourcePath")]
        public string SourcePath { get; set; } = "";

        [JsonProperty("exportedAtUtc")]
        public string ExportedAtUtc { get; set; } = "";

        [JsonProperty("exportDurationMs")]
        public long ExportDurationMs { get; set; }

        [JsonProperty("nodeCount")]
        public int NodeCount { get; set; }

        [JsonProperty("meshCount")]
        public int MeshCount { get; set; }

        [JsonProperty("materialCount")]
        public int MaterialCount { get; set; }

        [JsonProperty("totalVertexCount")]
        public int TotalVertexCount { get; set; }

        [JsonProperty("totalTriangleCount")]
        public int TotalTriangleCount { get; set; }

        [JsonProperty("packageBytes")]
        public long PackageBytes { get; set; }

        [JsonProperty("featureFlags")]
        public List<string> FeatureFlags { get; set; } = new List<string>();

        [JsonProperty("warnings")]
        public List<string> Warnings { get; set; } = new List<string>();
    }

    [Serializable]
    internal sealed class UnityInnerPieceExportSource
    {
        public GameObject RootObject { get; set; }
        public string SourceKind { get; set; } = "";
        public string SourcePath { get; set; } = "";
        public string DefaultDisplayName { get; set; } = "";
    }

    [Serializable]
    public sealed class UnityQuaternion
    {
        [JsonProperty("x")]
        public float X { get; set; }

        [JsonProperty("y")]
        public float Y { get; set; }

        [JsonProperty("z")]
        public float Z { get; set; }

        [JsonProperty("w")]
        public float W { get; set; } = 1f;

        public static UnityQuaternion FromQuaternion(UnityEngine.Quaternion value)
        {
            return new UnityQuaternion
            {
                X = value.x,
                Y = value.y,
                Z = value.z,
                W = value.w
            };
        }
    }

    [Serializable]
    public sealed class UnityNormalizedRect
    {
        [JsonProperty("x")]
        public float X { get; set; }

        [JsonProperty("y")]
        public float Y { get; set; }

        [JsonProperty("width")]
        public float Width { get; set; }

        [JsonProperty("height")]
        public float Height { get; set; }
    }
}
