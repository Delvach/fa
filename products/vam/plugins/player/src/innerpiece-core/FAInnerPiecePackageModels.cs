using System;

[Serializable]
public sealed class FAInnerPiecePackageManifest
{
    public string schemaVersion = FAInnerPieceSchemas.PackageManifestV1;
    public string packageId = "";
    public string resourceId = "";
    public string displayName = "";
    public string controlSurfaceId = "";
    public string controlFamilyId = "";
    public string controlThemeId = "";
    public string controlThemeLabel = "";
    public string controlThemeVariantId = "";
    public string controlThemeAssetPath = "";
    public string controlThemeAssetGuid = "";
    public string toolkitCategory = "";
    public string sourcePrefabAssetPath = "";
    public string exporterVersion = "";
    public string exportedAtUtc = "";
    public string sourceKind = "";
    public string sourcePath = "";
    public string geometryPath = "geometry.innerpiece.json";
    public string materialsPath = "";
    public string screensPath = "";
    public string controlsPath = "";
    public string previewPath = "";
    public string[] featureFlags = new string[0];
    public string[] warnings = new string[0];
}

[Serializable]
public sealed class FAInnerPieceMaterialPackage
{
    public string schemaVersion = FAInnerPieceSchemas.MaterialsV1;
    public FAInnerPieceMaterialEntry[] materials = new FAInnerPieceMaterialEntry[0];
}

[Serializable]
public sealed class FAInnerPieceMaterialEntry
{
    public string materialRefId = "";
    public string displayName = "";
    public string shaderName = "";
    public string baseColorHex = "#FFFFFF";
    public string textureAssetPath = "";
    public string texturePngBase64 = "";
    public string[] featureFlags = new string[0];
    public string[] warnings = new string[0];
}

[Serializable]
public sealed class FAInnerPieceResolvedImportSource
{
    public string packageKind = "raw_geometry_json";
    public string packageRootPath = "";
    public string packagePath = "";
    public string manifestPath = "";
    public string manifestJson = "";
    public FAInnerPiecePackageManifest manifest;
    public string geometryPath = "";
    public string geometryJson = "";
    public string materialsPath = "";
    public string materialsJson = "";
    public FAInnerPieceMaterialPackage materials;
    public string screensPath = "";
    public string screensJson = "";
    public FAInnerPieceScreenContractData screens;
    public string controlsPath = "";
    public string controlsJson = "";
    public FAInnerPieceControlSurfaceData controls;
    public string previewPath = "";
    public string exportReceiptPath = "";
    public string exportReceiptJson = "";
}
