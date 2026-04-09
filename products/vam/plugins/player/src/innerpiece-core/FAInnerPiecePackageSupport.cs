using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MVR.FileManagementSecure;
using UnityEngine;

public static class FAInnerPiecePackageSupport
{
    public static string ResolveStoredMaterialPackagePath(string resourceId)
    {
        return FAInnerPieceStorage.CombinePath(
            FAInnerPieceStorage.ResolveResourceDirectory(resourceId),
            "materials.innerpiece.json");
    }

    public static string ResolveStoredManifestSnapshotPath(string resourceId)
    {
        return FAInnerPieceStorage.CombinePath(
            FAInnerPieceStorage.ResolveResourceDirectory(resourceId),
            "manifest.snapshot.json");
    }

    public static string ResolveStoredScreenContractPath(string resourceId)
    {
        return FAInnerPieceStorage.CombinePath(
            FAInnerPieceStorage.ResolveResourceDirectory(resourceId),
            "screens.innerpiece.json");
    }

    public static string ResolveStoredControlSurfacePath(string resourceId)
    {
        return FAInnerPieceStorage.CombinePath(
            FAInnerPieceStorage.ResolveResourceDirectory(resourceId),
            "controls.innerpiece.json");
    }

    public static string ResolveStoredExportReceiptSnapshotPath(string resourceId)
    {
        return FAInnerPieceStorage.CombinePath(
            FAInnerPieceStorage.ResolveResourceDirectory(resourceId),
            "export_receipt.snapshot.json");
    }

    public static FAInnerPieceMaterialPackage LoadStoredMaterialPackage(string resourceId)
    {
        return DeserializeMaterialPackage(ReadAllTextSafe(ResolveStoredMaterialPackagePath(resourceId)));
    }

    public static FAInnerPieceScreenContractData LoadStoredScreenContract(string resourceId)
    {
        return DeserializeScreenContract(ReadAllTextSafe(ResolveStoredScreenContractPath(resourceId)));
    }

    public static FAInnerPieceControlSurfaceData LoadStoredControlSurface(string resourceId)
    {
        return DeserializeControlSurface(ReadAllTextSafe(ResolveStoredControlSurfacePath(resourceId)));
    }

    public static void SaveStoredMaterialPackage(string resourceId, FAInnerPieceMaterialPackage materialPackage)
    {
        WriteAllTextSafe(
            ResolveStoredMaterialPackagePath(resourceId),
            SerializeMaterialPackage(materialPackage, true));
    }

    public static void SaveStoredScreenContract(string resourceId, FAInnerPieceScreenContractData screenContract)
    {
        WriteAllTextSafe(
            ResolveStoredScreenContractPath(resourceId),
            SerializeScreenContract(screenContract, true));
    }

    public static void SaveStoredControlSurface(string resourceId, FAInnerPieceControlSurfaceData controlSurface)
    {
        WriteAllTextSafe(
            ResolveStoredControlSurfacePath(resourceId),
            SerializeControlSurface(controlSurface, true));
    }

    public static void SaveStoredManifestSnapshot(string resourceId, string manifestJson)
    {
        WriteAllTextSafe(ResolveStoredManifestSnapshotPath(resourceId), manifestJson ?? "");
    }

    public static void SaveStoredExportReceiptSnapshot(string resourceId, string exportReceiptJson)
    {
        WriteAllTextSafe(ResolveStoredExportReceiptSnapshotPath(resourceId), exportReceiptJson ?? "");
    }

    public static bool TryResolveImportSource(string path, out FAInnerPieceResolvedImportSource source, out string errorMessage)
    {
        source = null;
        errorMessage = "";

        string normalizedPath = FAInnerPieceStorage.NormalizePath(path);
        if (string.IsNullOrEmpty(normalizedPath))
        {
            errorMessage = "packagePath is required";
            return false;
        }

        string directJson = ReadAllTextSafe(normalizedPath);
        if (!string.IsNullOrEmpty(directJson))
        {
            if (string.Equals(GetLeafName(normalizedPath), "manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                return TryBuildHybridSource(GetDirectoryNameSafe(normalizedPath), normalizedPath, directJson, out source, out errorMessage);
            }

            FAInnerPiecePackageManifest directManifest = DeserializeManifest(directJson);
            if (directManifest != null && string.Equals(directManifest.schemaVersion, FAInnerPieceSchemas.PackageManifestV1, StringComparison.Ordinal))
            {
                return TryBuildHybridSource(GetDirectoryNameSafe(normalizedPath), normalizedPath, directJson, out source, out errorMessage);
            }

            FAInnerPieceResolvedImportSource rawSource = new FAInnerPieceResolvedImportSource();
            rawSource.packageKind = "raw_geometry_json";
            rawSource.packagePath = normalizedPath;
            rawSource.geometryPath = normalizedPath;
            rawSource.geometryJson = directJson;
            rawSource.packageRootPath = GetDirectoryNameSafe(normalizedPath);
            source = rawSource;
            return true;
        }

        string manifestPath = FAInnerPieceStorage.CombinePath(normalizedPath, "manifest.json");
        string manifestJson = ReadAllTextSafe(manifestPath);
        if (string.IsNullOrEmpty(manifestJson))
        {
            errorMessage = "package file or manifest not found";
            return false;
        }

        return TryBuildHybridSource(normalizedPath, manifestPath, manifestJson, out source, out errorMessage);
    }

    public static string SerializeMaterialPackage(FAInnerPieceMaterialPackage materialPackage, bool pretty)
    {
        FAInnerPieceMaterialPackage value = NormalizeMaterialPackage(materialPackage);
        StringBuilder sb = new StringBuilder();
        sb.Append('{');
        WriteString(sb, "schemaVersion", value.schemaVersion);
        WriteArrayStart(sb, "materials");
        for (int i = 0; i < value.materials.Length; i++)
        {
            if (i > 0)
                sb.Append(',');
            FAInnerPieceMaterialEntry entry = value.materials[i] ?? new FAInnerPieceMaterialEntry();
            sb.Append('{');
            WriteString(sb, "materialRefId", entry.materialRefId);
            WriteString(sb, "displayName", entry.displayName);
            WriteString(sb, "shaderName", entry.shaderName);
            WriteString(sb, "baseColorHex", entry.baseColorHex);
            WriteString(sb, "textureAssetPath", entry.textureAssetPath);
            WriteString(sb, "texturePngBase64", entry.texturePngBase64);
            WriteStringArray(sb, "featureFlags", entry.featureFlags);
            WriteStringArray(sb, "warnings", entry.warnings);
            EndObject(sb);
        }
        EndArray(sb);
        return FinishObject(sb);
    }

    public static string SerializeScreenContract(FAInnerPieceScreenContractData screenContract, bool pretty)
    {
        FAInnerPieceScreenContractData value = NormalizeScreenContract(screenContract);
        if (value == null)
            return "";

        StringBuilder sb = new StringBuilder();
        sb.Append('{');
        WriteString(sb, "schemaVersion", value.schemaVersion);
        WriteString(sb, "shellId", value.shellId);
        WriteString(sb, "defaultDisconnectStateId", value.defaultDisconnectStateId);
        WriteString(sb, "surfaceTargetId", value.surfaceTargetId);
        WriteString(sb, "deviceClass", value.deviceClass);
        WriteString(sb, "orientationSupport", value.orientationSupport);
        WriteString(sb, "defaultAspectMode", value.defaultAspectMode);
        WriteFloat(sb, "safeCornerRadius", value.safeCornerRadius);
        WriteString(sb, "inputStyle", value.inputStyle);
        WriteBool(sb, "autoOrientToGround", value.autoOrientToGround);
        WriteArrayStart(sb, "slots");
        for (int i = 0; i < value.slots.Length; i++)
        {
            if (i > 0)
                sb.Append(',');
            FAInnerPieceScreenSlotData slot = value.slots[i] ?? new FAInnerPieceScreenSlotData();
            sb.Append('{');
            WriteString(sb, "slotId", slot.slotId);
            WriteString(sb, "displayId", slot.displayId);
            WriteString(sb, "surfaceTargetId", slot.surfaceTargetId);
            WriteString(sb, "disconnectStateId", slot.disconnectStateId);
            WriteString(sb, "screenSurfaceNodeId", slot.screenSurfaceNodeId);
            WriteString(sb, "screenGlassNodeId", slot.screenGlassNodeId);
            WriteString(sb, "screenApertureNodeId", slot.screenApertureNodeId);
            WriteString(sb, "disconnectSurfaceNodeId", slot.disconnectSurfaceNodeId);
            EndObject(sb);
        }
        EndArray(sb);
        return FinishObject(sb);
    }

    public static string SerializeControlSurface(FAInnerPieceControlSurfaceData controlSurface, bool pretty)
    {
        FAInnerPieceControlSurfaceData value = NormalizeControlSurface(controlSurface);
        if (value == null)
            return "";

        StringBuilder sb = new StringBuilder();
        sb.Append('{');
        WriteString(sb, "schemaVersion", value.schemaVersion);
        WriteString(sb, "controlSurfaceId", value.controlSurfaceId);
        WriteString(sb, "controlSurfaceLabel", value.controlSurfaceLabel);
        WriteString(sb, "controlFamilyId", value.controlFamilyId);
        WriteString(sb, "controlThemeId", value.controlThemeId);
        WriteString(sb, "controlThemeLabel", value.controlThemeLabel);
        WriteString(sb, "controlThemeVariantId", value.controlThemeVariantId);
        WriteString(sb, "controlThemeAssetPath", value.controlThemeAssetPath);
        WriteString(sb, "controlThemeAssetGuid", value.controlThemeAssetGuid);
        WriteString(sb, "toolkitCategory", value.toolkitCategory);
        WriteString(sb, "sourcePrefabAssetPath", value.sourcePrefabAssetPath);
        WriteString(sb, "layoutSource", value.layoutSource);
        WriteStringArray(sb, "targetDisplayIds", value.targetDisplayIds);
        WriteString(sb, "defaultTargetDisplayId", value.defaultTargetDisplayId);
        WriteString(sb, "surfaceNodeId", value.surfaceNodeId);
        WriteString(sb, "colliderNodeId", value.colliderNodeId);
        WriteFloat(sb, "surfaceWidthMeters", value.surfaceWidthMeters);
        WriteFloat(sb, "surfaceHeightMeters", value.surfaceHeightMeters);
        WriteArrayStart(sb, "elements");
        for (int i = 0; i < value.elements.Length; i++)
        {
            if (i > 0)
                sb.Append(',');
            FAInnerPieceControlElementData element = value.elements[i] ?? new FAInnerPieceControlElementData();
            sb.Append('{');
            WriteString(sb, "elementId", element.elementId);
            WriteString(sb, "elementLabel", element.elementLabel);
            WriteString(sb, "actionId", element.actionId);
            WriteString(sb, "nodeId", element.nodeId);
            WriteString(sb, "colliderNodeId", element.colliderNodeId);
            WriteString(sb, "elementKind", element.elementKind);
            WriteString(sb, "valueKind", element.valueKind);
            sb.Append("\"normalizedRect\":{");
            FAInnerPieceNormalizedRectData rect = element.normalizedRect ?? new FAInnerPieceNormalizedRectData();
            WriteFloat(sb, "x", rect.x);
            WriteFloat(sb, "y", rect.y);
            WriteFloat(sb, "width", rect.width);
            WriteFloat(sb, "height", rect.height);
            EndObject(sb);
            WriteBool(sb, "readOnly", element.readOnly);
            EndObject(sb);
        }
        EndArray(sb);
        return FinishObject(sb);
    }

    private static bool TryBuildHybridSource(
        string packageRootPath,
        string manifestPath,
        string manifestJson,
        out FAInnerPieceResolvedImportSource source,
        out string errorMessage)
    {
        source = null;
        errorMessage = "";

        FAInnerPiecePackageManifest manifest = DeserializeManifest(manifestJson);
        if (manifest == null)
        {
            errorMessage = "manifest parse failed";
            return false;
        }

        if (!string.Equals(manifest.schemaVersion, FAInnerPieceSchemas.PackageManifestV1, StringComparison.Ordinal))
        {
            errorMessage = "unsupported manifest schema";
            return false;
        }

        string geometryPath = ResolvePackagePath(packageRootPath, string.IsNullOrEmpty(manifest.geometryPath) ? "geometry.innerpiece.json" : manifest.geometryPath);
        string geometryJson = ReadAllTextSafe(geometryPath);
        if (string.IsNullOrEmpty(geometryJson))
        {
            errorMessage = "geometry payload missing";
            return false;
        }

        string materialsPath = ResolvePackagePath(packageRootPath, manifest.materialsPath);
        string materialsJson = ReadAllTextSafe(materialsPath);
        string screensPath = ResolvePackagePath(packageRootPath, manifest.screensPath);
        string screensJson = ReadAllTextSafe(screensPath);
        if (!string.IsNullOrEmpty(manifest.screensPath) && string.IsNullOrEmpty(screensJson))
        {
            errorMessage = "screen payload missing";
            return false;
        }
        string controlsPath = ResolvePackagePath(packageRootPath, manifest.controlsPath);
        string controlsJson = ReadAllTextSafe(controlsPath);
        if (!string.IsNullOrEmpty(manifest.controlsPath) && string.IsNullOrEmpty(controlsJson))
        {
            errorMessage = "controls payload missing";
            return false;
        }
        string previewPath = ResolvePackagePath(packageRootPath, manifest.previewPath);
        string exportReceiptPath = FAInnerPieceStorage.CombinePath(packageRootPath, "export_receipt.json");
        string exportReceiptJson = ReadAllTextSafe(exportReceiptPath);

        FAInnerPieceControlSurfaceData controls = null;
        if (!TryDeserializeControlSurface(controlsJson, out controls, out errorMessage))
        {
            errorMessage = "controls payload malformed";
            return false;
        }

        source = new FAInnerPieceResolvedImportSource();
        source.packageKind = "hybrid_directory";
        source.packageRootPath = packageRootPath;
        source.packagePath = packageRootPath;
        source.manifestPath = manifestPath;
        source.manifestJson = manifestJson;
        source.manifest = manifest;
        source.geometryPath = geometryPath;
        source.geometryJson = geometryJson;
        source.materialsPath = !string.IsNullOrEmpty(materialsJson) ? materialsPath : "";
        source.materialsJson = materialsJson;
        source.materials = DeserializeMaterialPackage(materialsJson);
        source.screensPath = !string.IsNullOrEmpty(screensJson) ? screensPath : "";
        source.screensJson = screensJson;
        source.screens = DeserializeScreenContract(screensJson);
        source.controlsPath = !string.IsNullOrEmpty(controlsJson) ? controlsPath : "";
        source.controlsJson = controlsJson;
        source.controls = controls;
        source.previewPath = !string.IsNullOrEmpty(manifest.previewPath) ? previewPath : "";
        source.exportReceiptPath = !string.IsNullOrEmpty(exportReceiptJson) ? exportReceiptPath : "";
        source.exportReceiptJson = exportReceiptJson;
        return true;
    }

    private static string ResolvePackagePath(string root, string relativeOrAbsolutePath)
    {
        if (string.IsNullOrEmpty(relativeOrAbsolutePath))
            return "";

        string normalized = FAInnerPieceStorage.NormalizePath(relativeOrAbsolutePath);
        if (normalized.Contains(":\\"))
            return normalized;
        return FAInnerPieceStorage.CombinePath(root, normalized);
    }

    private static FAInnerPiecePackageManifest DeserializeManifest(string json)
    {
        try
        {
            if (string.IsNullOrEmpty(json))
                return null;

            FAInnerPiecePackageManifest manifest = new FAInnerPiecePackageManifest();
            manifest.schemaVersion = ReadString(json, "schemaVersion", manifest.schemaVersion);
            manifest.packageId = ReadString(json, "packageId", manifest.packageId);
            manifest.resourceId = ReadString(json, "resourceId", manifest.resourceId);
            manifest.displayName = ReadString(json, "displayName", manifest.displayName);
            manifest.controlSurfaceId = ReadString(json, "controlSurfaceId", manifest.controlSurfaceId);
            manifest.controlFamilyId = ReadString(json, "controlFamilyId", manifest.controlFamilyId);
            manifest.controlThemeId = ReadString(json, "controlThemeId", manifest.controlThemeId);
            manifest.controlThemeLabel = ReadString(json, "controlThemeLabel", manifest.controlThemeLabel);
            manifest.controlThemeVariantId = ReadString(json, "controlThemeVariantId", manifest.controlThemeVariantId);
            manifest.controlThemeAssetPath = ReadString(json, "controlThemeAssetPath", manifest.controlThemeAssetPath);
            manifest.controlThemeAssetGuid = ReadString(json, "controlThemeAssetGuid", manifest.controlThemeAssetGuid);
            manifest.toolkitCategory = ReadString(json, "toolkitCategory", manifest.toolkitCategory);
            manifest.sourcePrefabAssetPath = ReadString(json, "sourcePrefabAssetPath", manifest.sourcePrefabAssetPath);
            manifest.exporterVersion = ReadString(json, "exporterVersion", manifest.exporterVersion);
            manifest.exportedAtUtc = ReadString(json, "exportedAtUtc", manifest.exportedAtUtc);
            manifest.sourceKind = ReadString(json, "sourceKind", manifest.sourceKind);
            manifest.sourcePath = ReadString(json, "sourcePath", manifest.sourcePath);
            manifest.geometryPath = ReadString(json, "geometryPath", manifest.geometryPath);
            manifest.materialsPath = ReadString(json, "materialsPath", manifest.materialsPath);
            manifest.screensPath = ReadString(json, "screensPath", manifest.screensPath);
            manifest.controlsPath = ReadString(json, "controlsPath", manifest.controlsPath);
            manifest.previewPath = ReadString(json, "previewPath", manifest.previewPath);
            manifest.featureFlags = ReadStringArray(json, "featureFlags");
            manifest.warnings = ReadStringArray(json, "warnings");
            if (manifest == null)
                return null;
            if (manifest.featureFlags == null)
                manifest.featureFlags = new string[0];
            if (manifest.warnings == null)
                manifest.warnings = new string[0];
            if (string.IsNullOrEmpty(manifest.schemaVersion))
                manifest.schemaVersion = FAInnerPieceSchemas.PackageManifestV1;
            return manifest;
        }
        catch
        {
            return null;
        }
    }

    private static FAInnerPieceMaterialPackage DeserializeMaterialPackage(string json)
    {
        try
        {
            if (string.IsNullOrEmpty(json))
                return new FAInnerPieceMaterialPackage();
            FAInnerPieceMaterialPackage materialPackage = new FAInnerPieceMaterialPackage();
            materialPackage.schemaVersion = ReadString(json, "schemaVersion", materialPackage.schemaVersion);
            string arrayJson;
            if (TryReadArray(json, "materials", out arrayJson))
            {
                System.Collections.Generic.List<string> objects = ExtractObjectsFromArray(arrayJson);
                materialPackage.materials = new FAInnerPieceMaterialEntry[objects.Count];
                for (int i = 0; i < objects.Count; i++)
                {
                    string objectJson = objects[i];
                    FAInnerPieceMaterialEntry entry = new FAInnerPieceMaterialEntry();
                    entry.materialRefId = ReadString(objectJson, "materialRefId", entry.materialRefId);
                    entry.displayName = ReadString(objectJson, "displayName", entry.displayName);
                    entry.shaderName = ReadString(objectJson, "shaderName", entry.shaderName);
                    entry.baseColorHex = ReadString(objectJson, "baseColorHex", entry.baseColorHex);
                    entry.textureAssetPath = ReadString(objectJson, "textureAssetPath", entry.textureAssetPath);
                    entry.texturePngBase64 = ReadString(objectJson, "texturePngBase64", entry.texturePngBase64);
                    entry.featureFlags = ReadStringArray(objectJson, "featureFlags");
                    entry.warnings = ReadStringArray(objectJson, "warnings");
                    materialPackage.materials[i] = entry;
                }
            }
            return NormalizeMaterialPackage(materialPackage);
        }
        catch
        {
            return new FAInnerPieceMaterialPackage();
        }
    }

    public static FAInnerPieceScreenContractData DeserializeScreenContract(string json)
    {
        try
        {
            if (string.IsNullOrEmpty(json))
                return null;

            FAInnerPieceScreenContractData screenContract = new FAInnerPieceScreenContractData();
            screenContract.schemaVersion = ReadString(json, "schemaVersion", screenContract.schemaVersion);
            screenContract.shellId = ReadString(json, "shellId", screenContract.shellId);
            screenContract.defaultDisconnectStateId = ReadString(json, "defaultDisconnectStateId", screenContract.defaultDisconnectStateId);
            screenContract.surfaceTargetId = ReadString(json, "surfaceTargetId", screenContract.surfaceTargetId);
            screenContract.deviceClass = ReadString(json, "deviceClass", screenContract.deviceClass);
            screenContract.orientationSupport = ReadString(json, "orientationSupport", screenContract.orientationSupport);
            screenContract.defaultAspectMode = ReadString(json, "defaultAspectMode", screenContract.defaultAspectMode);
            screenContract.safeCornerRadius = ReadFloat(json, "safeCornerRadius", screenContract.safeCornerRadius);
            screenContract.inputStyle = ReadString(json, "inputStyle", screenContract.inputStyle);
            screenContract.autoOrientToGround = ReadBool(json, "autoOrientToGround", screenContract.autoOrientToGround);

            string arrayJson;
            if (TryReadArray(json, "slots", out arrayJson))
            {
                System.Collections.Generic.List<string> objects = ExtractObjectsFromArray(arrayJson);
                screenContract.slots = new FAInnerPieceScreenSlotData[objects.Count];
                for (int i = 0; i < objects.Count; i++)
                {
                    string objectJson = objects[i];
                    FAInnerPieceScreenSlotData slot = new FAInnerPieceScreenSlotData();
                    slot.slotId = ReadString(objectJson, "slotId", slot.slotId);
                    slot.displayId = ReadString(objectJson, "displayId", slot.displayId);
                    slot.surfaceTargetId = ReadString(objectJson, "surfaceTargetId", slot.surfaceTargetId);
                    slot.disconnectStateId = ReadString(objectJson, "disconnectStateId", slot.disconnectStateId);
                    slot.screenSurfaceNodeId = ReadString(objectJson, "screenSurfaceNodeId", slot.screenSurfaceNodeId);
                    slot.screenGlassNodeId = ReadString(objectJson, "screenGlassNodeId", slot.screenGlassNodeId);
                    slot.screenApertureNodeId = ReadString(objectJson, "screenApertureNodeId", slot.screenApertureNodeId);
                    slot.disconnectSurfaceNodeId = ReadString(objectJson, "disconnectSurfaceNodeId", slot.disconnectSurfaceNodeId);
                    screenContract.slots[i] = slot;
                }
            }

            return NormalizeScreenContract(screenContract);
        }
        catch
        {
            return null;
        }
    }

    public static FAInnerPieceControlSurfaceData DeserializeControlSurface(string json)
    {
        try
        {
            return ParseControlSurfaceOrThrow(json);
        }
        catch
        {
            return null;
        }
    }

    public static bool TryDeserializeControlSurface(string json, out FAInnerPieceControlSurfaceData controlSurface, out string errorMessage)
    {
        controlSurface = null;
        errorMessage = "";

        try
        {
            controlSurface = ParseControlSurfaceOrThrow(json);
            return true;
        }
        catch
        {
            errorMessage = "controls payload malformed";
            return false;
        }
    }

    private static FAInnerPieceMaterialPackage NormalizeMaterialPackage(FAInnerPieceMaterialPackage materialPackage)
    {
        if (materialPackage == null)
            materialPackage = new FAInnerPieceMaterialPackage();
        if (string.IsNullOrEmpty(materialPackage.schemaVersion))
            materialPackage.schemaVersion = FAInnerPieceSchemas.MaterialsV1;
        if (materialPackage.materials == null)
            materialPackage.materials = new FAInnerPieceMaterialEntry[0];

        for (int i = 0; i < materialPackage.materials.Length; i++)
        {
            FAInnerPieceMaterialEntry entry = materialPackage.materials[i];
            if (entry == null)
            {
                materialPackage.materials[i] = new FAInnerPieceMaterialEntry();
                entry = materialPackage.materials[i];
            }

            if (entry.featureFlags == null)
                entry.featureFlags = new string[0];
            if (entry.warnings == null)
                entry.warnings = new string[0];
            if (string.IsNullOrEmpty(entry.baseColorHex))
                entry.baseColorHex = "#FFFFFF";
            if (entry.texturePngBase64 == null)
                entry.texturePngBase64 = "";
        }

        return materialPackage;
    }

    public static FAInnerPieceScreenContractData NormalizeScreenContract(FAInnerPieceScreenContractData screenContract)
    {
        if (screenContract == null)
            return null;

        if (string.IsNullOrEmpty(screenContract.schemaVersion))
            screenContract.schemaVersion = FAInnerPieceSchemas.ScreenContractV1;
        if (string.IsNullOrEmpty(screenContract.surfaceTargetId))
            screenContract.surfaceTargetId = "player:screen";
        if (string.IsNullOrEmpty(screenContract.deviceClass))
            screenContract.deviceClass = "monitor";
        if (string.IsNullOrEmpty(screenContract.orientationSupport))
            screenContract.orientationSupport = "landscape";
        if (string.IsNullOrEmpty(screenContract.defaultAspectMode))
            screenContract.defaultAspectMode = "fit";
        if (screenContract.safeCornerRadius < 0f)
            screenContract.safeCornerRadius = 0f;
        if (string.IsNullOrEmpty(screenContract.inputStyle))
            screenContract.inputStyle = "fixed";
        if (screenContract.slots == null)
            screenContract.slots = new FAInnerPieceScreenSlotData[0];

        for (int i = 0; i < screenContract.slots.Length; i++)
        {
            FAInnerPieceScreenSlotData slot = screenContract.slots[i];
            if (slot == null)
            {
                screenContract.slots[i] = new FAInnerPieceScreenSlotData();
                slot = screenContract.slots[i];
            }

            if (string.IsNullOrEmpty(slot.slotId))
                slot.slotId = "main";
            if (string.IsNullOrEmpty(slot.displayId))
            {
                string effectiveSurfaceTargetId = string.IsNullOrEmpty(slot.surfaceTargetId)
                    ? screenContract.surfaceTargetId
                    : slot.surfaceTargetId;
                if (string.Equals(slot.slotId, "main", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(effectiveSurfaceTargetId, "player:screen", StringComparison.OrdinalIgnoreCase))
                {
                    slot.displayId = "player_main";
                }
                else
                {
                    slot.displayId = slot.slotId;
                }
            }
            if (string.IsNullOrEmpty(slot.surfaceTargetId))
                slot.surfaceTargetId = string.IsNullOrEmpty(screenContract.surfaceTargetId)
                    ? "player:screen"
                    : screenContract.surfaceTargetId;
            if (slot.disconnectStateId == null)
                slot.disconnectStateId = "";
            if (slot.screenSurfaceNodeId == null)
                slot.screenSurfaceNodeId = "";
            if (slot.screenGlassNodeId == null)
                slot.screenGlassNodeId = "";
            if (slot.screenApertureNodeId == null)
                slot.screenApertureNodeId = "";
            if (slot.disconnectSurfaceNodeId == null)
                slot.disconnectSurfaceNodeId = "";
        }

        return screenContract;
    }

    public static FAInnerPieceControlSurfaceData NormalizeControlSurface(FAInnerPieceControlSurfaceData controlSurface)
    {
        if (controlSurface == null)
            return null;

        controlSurface.controlSurfaceId = NormalizeControlString(controlSurface.controlSurfaceId);
        controlSurface.controlSurfaceLabel = NormalizeControlLabel(controlSurface.controlSurfaceLabel);
        controlSurface.controlFamilyId = NormalizeControlString(controlSurface.controlFamilyId);
        controlSurface.controlThemeId = NormalizeControlString(controlSurface.controlThemeId);
        controlSurface.controlThemeLabel = NormalizeControlLabel(controlSurface.controlThemeLabel);
        controlSurface.controlThemeVariantId = NormalizeControlString(controlSurface.controlThemeVariantId);
        controlSurface.controlThemeAssetPath = NormalizeControlString(controlSurface.controlThemeAssetPath);
        controlSurface.controlThemeAssetGuid = NormalizeControlString(controlSurface.controlThemeAssetGuid);
        controlSurface.toolkitCategory = NormalizeControlString(controlSurface.toolkitCategory);
        controlSurface.sourcePrefabAssetPath = NormalizeControlString(controlSurface.sourcePrefabAssetPath);
        controlSurface.layoutSource = NormalizeControlString(controlSurface.layoutSource);
        controlSurface.defaultTargetDisplayId = NormalizeControlString(controlSurface.defaultTargetDisplayId);
        controlSurface.surfaceNodeId = NormalizeControlString(controlSurface.surfaceNodeId);
        controlSurface.colliderNodeId = NormalizeControlString(controlSurface.colliderNodeId);
        controlSurface.targetDisplayIds = NormalizeControlStringArray(controlSurface.targetDisplayIds);
        if (string.IsNullOrEmpty(controlSurface.schemaVersion))
            controlSurface.schemaVersion = FAInnerPieceSchemas.ControlSurfaceV1;
        if (string.IsNullOrEmpty(controlSurface.layoutSource))
            controlSurface.layoutSource = "canvas_export_v1";
        if (controlSurface.elements == null)
            controlSurface.elements = new FAInnerPieceControlElementData[0];
        if (controlSurface.surfaceWidthMeters < 0f)
            controlSurface.surfaceWidthMeters = 0f;
        if (controlSurface.surfaceHeightMeters < 0f)
            controlSurface.surfaceHeightMeters = 0f;
        if (string.IsNullOrEmpty(controlSurface.defaultTargetDisplayId) && controlSurface.targetDisplayIds.Length > 0)
            controlSurface.defaultTargetDisplayId = controlSurface.targetDisplayIds[0];

        for (int i = 0; i < controlSurface.elements.Length; i++)
        {
            FAInnerPieceControlElementData element = controlSurface.elements[i];
            if (element == null)
            {
                controlSurface.elements[i] = new FAInnerPieceControlElementData();
                element = controlSurface.elements[i];
            }

            element.elementId = NormalizeControlString(element.elementId);
            element.elementLabel = NormalizeControlLabel(element.elementLabel);
            element.actionId = NormalizeControlString(element.actionId);
            element.nodeId = NormalizeControlString(element.nodeId);
            element.colliderNodeId = NormalizeControlString(element.colliderNodeId);
            element.elementKind = NormalizeControlString(element.elementKind);
            element.valueKind = NormalizeControlString(element.valueKind);
            if (string.IsNullOrEmpty(element.elementKind))
                element.elementKind = "button";
            if (string.IsNullOrEmpty(element.valueKind))
                element.valueKind = "none";
            if (string.IsNullOrEmpty(element.colliderNodeId))
                element.colliderNodeId = element.nodeId ?? "";
            if (element.normalizedRect == null)
                element.normalizedRect = new FAInnerPieceNormalizedRectData();

            element.normalizedRect.x = Mathf.Clamp01(element.normalizedRect.x);
            element.normalizedRect.y = Mathf.Clamp01(element.normalizedRect.y);
            element.normalizedRect.width = Mathf.Clamp01(element.normalizedRect.width);
            element.normalizedRect.height = Mathf.Clamp01(element.normalizedRect.height);
        }

        return controlSurface;
    }

    private static FAInnerPieceControlSurfaceData ParseControlSurfaceOrThrow(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        FAInnerPieceControlSurfaceData controlSurface = new FAInnerPieceControlSurfaceData();
        controlSurface.schemaVersion = ReadString(json, "schemaVersion", controlSurface.schemaVersion);
        controlSurface.controlSurfaceId = ReadString(json, "controlSurfaceId", controlSurface.controlSurfaceId);
        controlSurface.controlSurfaceLabel = ReadString(json, "controlSurfaceLabel", controlSurface.controlSurfaceLabel);
        controlSurface.controlFamilyId = ReadString(json, "controlFamilyId", controlSurface.controlFamilyId);
        controlSurface.controlThemeId = ReadString(json, "controlThemeId", controlSurface.controlThemeId);
        controlSurface.controlThemeLabel = ReadString(json, "controlThemeLabel", controlSurface.controlThemeLabel);
        controlSurface.controlThemeVariantId = ReadString(json, "controlThemeVariantId", controlSurface.controlThemeVariantId);
        controlSurface.controlThemeAssetPath = ReadString(json, "controlThemeAssetPath", controlSurface.controlThemeAssetPath);
        controlSurface.controlThemeAssetGuid = ReadString(json, "controlThemeAssetGuid", controlSurface.controlThemeAssetGuid);
        controlSurface.toolkitCategory = ReadString(json, "toolkitCategory", controlSurface.toolkitCategory);
        controlSurface.sourcePrefabAssetPath = ReadString(json, "sourcePrefabAssetPath", controlSurface.sourcePrefabAssetPath);
        controlSurface.layoutSource = ReadString(json, "layoutSource", controlSurface.layoutSource);
        controlSurface.targetDisplayIds = ReadStringArray(json, "targetDisplayIds");
        controlSurface.defaultTargetDisplayId = ReadString(json, "defaultTargetDisplayId", controlSurface.defaultTargetDisplayId);
        controlSurface.surfaceNodeId = ReadString(json, "surfaceNodeId", controlSurface.surfaceNodeId);
        controlSurface.colliderNodeId = ReadString(json, "colliderNodeId", controlSurface.colliderNodeId);
        controlSurface.surfaceWidthMeters = ReadFloat(json, "surfaceWidthMeters", controlSurface.surfaceWidthMeters);
        controlSurface.surfaceHeightMeters = ReadFloat(json, "surfaceHeightMeters", controlSurface.surfaceHeightMeters);

        string arrayJson;
        if (TryReadArray(json, "elements", out arrayJson))
        {
            System.Collections.Generic.List<string> objects = ExtractObjectsFromArray(arrayJson);
            controlSurface.elements = new FAInnerPieceControlElementData[objects.Count];
            for (int i = 0; i < objects.Count; i++)
            {
                string objectJson = objects[i];
                FAInnerPieceControlElementData element = new FAInnerPieceControlElementData();
                element.elementId = ReadString(objectJson, "elementId", element.elementId);
                element.elementLabel = ReadString(objectJson, "elementLabel", element.elementLabel);
                element.actionId = ReadString(objectJson, "actionId", element.actionId);
                element.nodeId = ReadString(objectJson, "nodeId", element.nodeId);
                element.colliderNodeId = ReadString(objectJson, "colliderNodeId", element.colliderNodeId);
                element.elementKind = ReadString(objectJson, "elementKind", element.elementKind);
                element.valueKind = ReadString(objectJson, "valueKind", element.valueKind);
                element.readOnly = ReadBool(objectJson, "readOnly", element.readOnly);

                string rectJson;
                if (TryReadObject(objectJson, "normalizedRect", out rectJson))
                {
                    element.normalizedRect.x = ReadFloat(rectJson, "x", element.normalizedRect.x);
                    element.normalizedRect.y = ReadFloat(rectJson, "y", element.normalizedRect.y);
                    element.normalizedRect.width = ReadFloat(rectJson, "width", element.normalizedRect.width);
                    element.normalizedRect.height = ReadFloat(rectJson, "height", element.normalizedRect.height);
                }

                controlSurface.elements[i] = element;
            }
        }

        return NormalizeControlSurface(controlSurface);
    }

    private static string NormalizeControlString(string value)
    {
        return string.IsNullOrEmpty(value) ? "" : value.Trim();
    }

    private static string NormalizeControlLabel(string value)
    {
        return string.IsNullOrEmpty(value) ? "" : value.Trim();
    }

    private static string[] NormalizeControlStringArray(string[] values)
    {
        if (values == null || values.Length <= 0)
            return new string[0];

        System.Collections.Generic.List<string> results = new System.Collections.Generic.List<string>(values.Length);
        System.Collections.Generic.HashSet<string> seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < values.Length; i++)
        {
            string candidate = NormalizeControlString(values[i]);
            if (string.IsNullOrEmpty(candidate) || !seen.Add(candidate))
                continue;
            results.Add(candidate);
        }

        return results.ToArray();
    }

    private static string ReadAllTextSafe(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || SuperController.singleton == null)
                return "";
            return SuperController.singleton.ReadFileIntoString(path) ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static void WriteAllTextSafe(string path, string text)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || SuperController.singleton == null)
                return;

            string directory = FileManagerSecure.GetDirectoryName(path, false);
            if (!string.IsNullOrEmpty(directory))
                FileManagerSecure.CreateDirectory(directory);

            SuperController.singleton.SaveStringIntoFile(path, text ?? "");
        }
        catch
        {
        }
    }

    private static string GetDirectoryNameSafe(string path)
    {
        string normalized = FAInnerPieceStorage.NormalizePath(path).TrimEnd('\\');
        int slash = normalized.LastIndexOf('\\');
        if (slash <= 0)
            return "";
        return normalized.Substring(0, slash);
    }

    private static string GetLeafName(string path)
    {
        string normalized = FAInnerPieceStorage.NormalizePath(path).TrimEnd('\\');
        int slash = normalized.LastIndexOf('\\');
        if (slash < 0 || slash >= normalized.Length - 1)
            return normalized;
        return normalized.Substring(slash + 1);
    }

    private static string ReadString(string json, string key, string fallback)
    {
        string value;
        return TryReadString(json, key, out value) ? value : fallback;
    }

    private static float ReadFloat(string json, string key, float fallback)
    {
        float value;
        return TryReadFloat(json, key, out value) ? value : fallback;
    }

    private static bool ReadBool(string json, string key, bool fallback)
    {
        bool value;
        return TryReadBool(json, key, out value) ? value : fallback;
    }

    private static string[] ReadStringArray(string json, string key)
    {
        string arrayJson;
        if (!TryReadArray(json, key, out arrayJson))
            return new string[0];

        MatchCollection matches = Regex.Matches(arrayJson, "\\\"((?:\\\\.|[^\\\\\\\"])*)\\\"", RegexOptions.Singleline);
        string[] values = new string[matches.Count];
        for (int i = 0; i < matches.Count; i++)
            values[i] = UnescapeJson(matches[i].Groups[1].Value);
        return values;
    }

    private static bool TryReadString(string json, string key, out string value)
    {
        value = "";
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            return false;

        Match match = Regex.Match(json, "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*\\\"((?:\\\\.|[^\\\\\\\"])*)\\\"", RegexOptions.Singleline);
        if (!match.Success)
            return false;
        value = UnescapeJson(match.Groups[1].Value);
        return true;
    }

    private static bool TryReadFloat(string json, string key, out float value)
    {
        value = 0f;
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            return false;

        Match match = Regex.Match(json, "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?(?:[eE][+-]?\\d+)?)", RegexOptions.Singleline);
        if (!match.Success)
            return false;
        return float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadBool(string json, string key, out bool value)
    {
        value = false;
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            return false;

        Match match = Regex.Match(json, "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success)
            return false;
        return bool.TryParse(match.Groups[1].Value, out value);
    }

    private static bool TryReadArray(string json, string key, out string arrayJson)
    {
        return TryReadDelimited(json, key, '[', ']', out arrayJson);
    }

    private static bool TryReadObject(string json, string key, out string objectJson)
    {
        return TryReadDelimited(json, key, '{', '}', out objectJson);
    }

    private static bool TryReadDelimited(string json, string key, char openChar, char closeChar, out string value)
    {
        value = "";
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            return false;

        string needle = "\"" + key + "\"";
        int keyIndex = json.IndexOf(needle, StringComparison.Ordinal);
        if (keyIndex < 0)
            return false;
        int colonIndex = json.IndexOf(':', keyIndex + needle.Length);
        if (colonIndex < 0)
            return false;
        int start = colonIndex + 1;
        while (start < json.Length && char.IsWhiteSpace(json[start]))
            start++;
        if (start >= json.Length || json[start] != openChar)
            return false;

        int depth = 0;
        bool inString = false;
        bool escaped = false;
        for (int i = start; i < json.Length; i++)
        {
            char c = json[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }
            if (c == '\\')
            {
                escaped = true;
                continue;
            }
            if (c == '"')
            {
                inString = !inString;
                continue;
            }
            if (inString)
                continue;
            if (c == openChar)
                depth++;
            else if (c == closeChar)
            {
                depth--;
                if (depth == 0)
                {
                    value = json.Substring(start, i - start + 1);
                    return true;
                }
            }
        }
        return false;
    }

    private static System.Collections.Generic.List<string> ExtractObjectsFromArray(string arrayJson)
    {
        System.Collections.Generic.List<string> results = new System.Collections.Generic.List<string>();
        if (string.IsNullOrEmpty(arrayJson))
            return results;

        bool inString = false;
        bool escaped = false;
        int depth = 0;
        int objectStart = -1;
        for (int i = 0; i < arrayJson.Length; i++)
        {
            char c = arrayJson[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }
            if (c == '\\')
            {
                escaped = true;
                continue;
            }
            if (c == '"')
            {
                inString = !inString;
                continue;
            }
            if (inString)
                continue;
            if (c == '{')
            {
                if (depth == 0)
                    objectStart = i;
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0 && objectStart >= 0)
                {
                    results.Add(arrayJson.Substring(objectStart, i - objectStart + 1));
                    objectStart = -1;
                }
            }
        }
        return results;
    }

    private static void WriteString(StringBuilder sb, string key, string value)
    {
        WritePrefix(sb, key);
        sb.Append('"').Append(EscapeJson(value ?? "")).Append('"').Append(',');
    }

    private static void WriteFloat(StringBuilder sb, string key, float value)
    {
        WritePrefix(sb, key);
        sb.Append(value.ToString("0.######", CultureInfo.InvariantCulture)).Append(',');
    }

    private static void WriteBool(StringBuilder sb, string key, bool value)
    {
        WritePrefix(sb, key);
        sb.Append(value ? "true" : "false").Append(',');
    }

    private static void WriteStringArray(StringBuilder sb, string key, string[] values)
    {
        WriteArrayStart(sb, key);
        string[] safeValues = values ?? new string[0];
        for (int i = 0; i < safeValues.Length; i++)
        {
            if (i > 0)
                sb.Append(',');
            sb.Append('"').Append(EscapeJson(safeValues[i] ?? "")).Append('"');
        }
        EndArray(sb);
    }

    private static void WriteArrayStart(StringBuilder sb, string key)
    {
        WritePrefix(sb, key);
        sb.Append('[');
    }

    private static void EndArray(StringBuilder sb)
    {
        TrimTrailingComma(sb);
        sb.Append(']');
        sb.Append(',');
    }

    private static void EndObject(StringBuilder sb)
    {
        TrimTrailingComma(sb);
        sb.Append('}');
    }

    private static string FinishObject(StringBuilder sb)
    {
        EndObject(sb);
        return sb.ToString();
    }

    private static void WritePrefix(StringBuilder sb, string key)
    {
        if (sb.Length > 0 && sb[sb.Length - 1] != '{' && sb[sb.Length - 1] != '[' && sb[sb.Length - 1] != ',')
            sb.Append(',');
        sb.Append('"').Append(EscapeJson(key)).Append("\":");
    }

    private static void TrimTrailingComma(StringBuilder sb)
    {
        if (sb.Length > 0 && sb[sb.Length - 1] == ',')
            sb.Length--;
    }

    private static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        StringBuilder sb = new StringBuilder(value.Length + 8);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            switch (c)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    if (c < 32)
                    {
                        sb.Append("\\u");
                        sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }

        return sb.ToString();
    }

    private static string UnescapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        StringBuilder sb = new StringBuilder(value.Length);
        bool escaped = false;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (!escaped)
            {
                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }
                sb.Append(c);
                continue;
            }

            escaped = false;
            switch (c)
            {
                case '\\':
                    sb.Append('\\');
                    break;
                case '"':
                    sb.Append('"');
                    break;
                case 'n':
                    sb.Append('\n');
                    break;
                case 'r':
                    sb.Append('\r');
                    break;
                case 't':
                    sb.Append('\t');
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }
}
