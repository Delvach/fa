using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MVR.FileManagementSecure;
using UnityEngine;

public static class FAInnerPieceStorage
{
    private struct ControlSurfaceImportDiagnostics
    {
        public bool hasTargetDisplay;
        public bool hasSurfaceNodeId;
        public int actionableElementCount;
        public int pointerReadyElementCount;
    }

    public static string ResolveRoot()
    {
        string gameDataPath = NormalizePath(Application.dataPath);
        if (string.IsNullOrEmpty(gameDataPath))
            return CombinePath(CombinePath("Custom", "PluginData"), "FrameAngelInnerPiece");

        int slash = gameDataPath.LastIndexOf('\\');
        if (slash <= 0)
            return CombinePath(CombinePath("Custom", "PluginData"), "FrameAngelInnerPiece");

        string gameRoot = gameDataPath.Substring(0, slash);
        return CombinePath(CombinePath(CombinePath(gameRoot, "Custom"), "PluginData"), "FrameAngelInnerPiece");
    }

    public static string ResolveCatalogPath()
    {
        return CombinePath(ResolveRoot(), "catalog.json");
    }

    public static string ResolveResourceDirectory(string resourceId)
    {
        return CombinePath(CombinePath(ResolveRoot(), "resources"), SanitizePathSegment(resourceId));
    }

    public static string ResolveResourcePath(string resourceId)
    {
        return CombinePath(ResolveResourceDirectory(resourceId), "resource.json");
    }

    public static string ResolveImportReceiptPath(string resourceId)
    {
        return CombinePath(ResolveResourceDirectory(resourceId), "import_receipt.json");
    }

    public static FAInnerPieceCatalogState LoadCatalog()
    {
        return DeserializeCatalog(ReadAllTextSafe(ResolveCatalogPath()));
    }

    public static FAInnerPieceStoredResource LoadResource(string resourceId)
    {
        FAInnerPieceStoredResource resource = DeserializeResource(ReadAllTextSafe(ResolveResourcePath(resourceId)));
        if (resource != null && resource.screenContract == null)
            resource.screenContract = FAInnerPiecePackageSupport.LoadStoredScreenContract(resourceId);
        if (resource != null && resource.controlSurface == null)
            resource.controlSurface = FAInnerPiecePackageSupport.LoadStoredControlSurface(resourceId);
        return resource;
    }

    public static FAInnerPieceImportReceipt LoadImportReceipt(string resourceId)
    {
        return DeserializeImportReceipt(ReadAllTextSafe(ResolveImportReceiptPath(resourceId)));
    }

    public static void SaveCatalog(FAInnerPieceCatalogState catalog)
    {
        WriteAllTextSafe(ResolveCatalogPath(), SerializeCatalog(catalog, true));
    }

    public static void SaveResource(FAInnerPieceStoredResource resource)
    {
        WriteAllTextSafe(ResolveResourcePath(resource != null ? resource.resourceId : ""), SerializeResource(resource, true));
    }

    public static void SaveImportReceipt(string resourceId, FAInnerPieceImportReceipt receipt)
    {
        WriteAllTextSafe(ResolveImportReceiptPath(resourceId), SerializeImportReceipt(receipt, true));
    }

    public static bool TrySetArchived(string resourceId, bool archived, out FAInnerPieceStoredResource resource, out string errorMessage)
    {
        resource = null;
        errorMessage = "";

        if (string.IsNullOrEmpty(resourceId))
        {
            errorMessage = "resourceId is required";
            return false;
        }

        FAInnerPieceCatalogState catalog = LoadCatalog();
        FAInnerPieceCatalogEntry entry = FindEntryByResourceId(catalog, resourceId);
        if (entry == null)
        {
            errorMessage = "resource not found";
            return false;
        }

        resource = LoadResource(resourceId);
        if (resource == null)
        {
            errorMessage = "resource payload missing";
            return false;
        }

        entry.archived = archived;
        resource.archived = archived;
        SaveCatalog(catalog);
        SaveResource(resource);
        return true;
    }

    public static bool TryImportPackage(
        string packagePath,
        string displayNameOverride,
        string[] additionalTags,
        bool archiveExisting,
        out FAInnerPieceStoredResource resource,
        out FAInnerPieceImportReceipt receipt,
        out string errorMessage
    )
    {
        resource = null;
        receipt = null;
        errorMessage = "";

        string normalizedPath = NormalizeExternalPath(packagePath);
        if (string.IsNullOrEmpty(normalizedPath))
        {
            errorMessage = "packagePath is required";
            return false;
        }

        FAInnerPieceResolvedImportSource importSource;
        if (!FAInnerPiecePackageSupport.TryResolveImportSource(normalizedPath, out importSource, out errorMessage))
            return false;

        FAInnerPieceExportPackage package = DeserializeExportPackage(importSource.geometryJson);

        if (package == null)
        {
            errorMessage = "package parse failed";
            return false;
        }

        if (!string.Equals(package.schemaVersion, FAInnerPieceSchemas.ExportV1, StringComparison.Ordinal))
        {
            errorMessage = "unsupported package schema";
            return false;
        }

        List<string> warnings;
        if (!TryNormalizePackage(package, importSource.screens, importSource.controls, importSource.geometryPath, displayNameOverride, additionalTags, out resource, out warnings, out errorMessage))
            return false;

        FAInnerPieceCatalogState catalog = LoadCatalog();
        string nowUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        FAInnerPieceCatalogEntry existingByFingerprint = FindEntryByFingerprint(catalog, resource.fingerprint);
        bool created = existingByFingerprint == null;
        bool updated = !created;
        string resourceId = created
            ? BuildResourceId(resource.displayName, resource.fingerprint, catalog)
            : existingByFingerprint.resourceId;
        resource.resourceId = resourceId;
        resource.archived = false;

        List<string> archivedExistingIds = new List<string>();
        if (archiveExisting && catalog.entries != null)
        {
            for (int i = 0; i < catalog.entries.Length; i++)
            {
                FAInnerPieceCatalogEntry candidate = catalog.entries[i];
                if (candidate == null)
                    continue;
                if (string.Equals(candidate.resourceId, resourceId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (candidate.archived)
                    continue;
                if (!string.Equals(candidate.displayName, resource.displayName, StringComparison.OrdinalIgnoreCase))
                    continue;

                candidate.archived = true;
                archivedExistingIds.Add(candidate.resourceId);

                FAInnerPieceStoredResource archivedResource = LoadResource(candidate.resourceId);
                if (archivedResource != null)
                {
                    archivedResource.archived = true;
                    SaveResource(archivedResource);
                }
            }
        }

        UpsertCatalogEntry(catalog, resource, nowUtc);
        SaveCatalog(catalog);
        SaveResource(resource);

        FAInnerPiecePackageSupport.SaveStoredMaterialPackage(
            resource.resourceId,
            importSource != null ? importSource.materials : new FAInnerPieceMaterialPackage());
        FAInnerPiecePackageSupport.SaveStoredScreenContract(
            resource.resourceId,
            importSource != null ? importSource.screens : null);
        FAInnerPiecePackageSupport.SaveStoredControlSurface(
            resource.resourceId,
            importSource != null ? importSource.controls : null);
        FAInnerPiecePackageSupport.SaveStoredManifestSnapshot(
            resource.resourceId,
            importSource != null ? importSource.manifestJson : "");
        FAInnerPiecePackageSupport.SaveStoredExportReceiptSnapshot(
            resource.resourceId,
            importSource != null ? importSource.exportReceiptJson : "");

        receipt = new FAInnerPieceImportReceipt();
        receipt.resourceId = resource.resourceId;
        receipt.fingerprint = resource.fingerprint;
        receipt.packagePath = normalizedPath;
        receipt.packageKind = importSource != null ? importSource.packageKind : "raw_geometry_json";
        receipt.manifestPath = importSource != null ? importSource.manifestPath : "";
        receipt.geometryPath = importSource != null ? importSource.geometryPath : normalizedPath;
        receipt.materialsPath = importSource != null ? importSource.materialsPath : "";
        receipt.screensPath = importSource != null ? importSource.screensPath : "";
        receipt.controlsPath = importSource != null ? importSource.controlsPath : "";
        receipt.previewPath = importSource != null ? importSource.previewPath : "";
        receipt.importedAtUtc = nowUtc;
        receipt.created = created;
        receipt.updated = updated;
        receipt.archivedExistingResourceIds = archivedExistingIds.ToArray();
        receipt.warnings = warnings.ToArray();
        SaveImportReceipt(resource.resourceId, receipt);

        return true;
    }

    public static string SerializeCatalog(FAInnerPieceCatalogState catalog, bool pretty)
    {
        FAInnerPieceCatalogState value = NormalizeCatalog(catalog);
        StringBuilder sb = StartObject();
        WriteString(sb, "schemaVersion", value.schemaVersion);
        WriteArrayStart(sb, "entries");
        for (int i = 0; i < value.entries.Length; i++)
        {
            if (i > 0)
                sb.Append(',');
            WriteInlineObjectStart(sb);
            WriteCatalogEntryFields(sb, value.entries[i] ?? new FAInnerPieceCatalogEntry());
            EndObject(sb);
        }
        EndArray(sb);
        return FinishObject(sb);
    }

    public static string SerializeResource(FAInnerPieceStoredResource resource, bool pretty)
    {
        FAInnerPieceStoredResource value = NormalizeStoredResource(resource) ?? new FAInnerPieceStoredResource();
        StringBuilder sb = StartObject();
        WriteString(sb, "schemaVersion", value.schemaVersion);
        WriteString(sb, "resourceId", value.resourceId);
        WriteString(sb, "displayName", value.displayName);
        WriteString(sb, "sourceKind", value.sourceKind);
        WriteString(sb, "sourcePath", value.sourcePath);
        WriteString(sb, "units", value.units);
        WriteString(sb, "fingerprint", value.fingerprint);
        WriteBool(sb, "archived", value.archived);
        WriteStringArray(sb, "tags", value.tags);
        WriteBounds(sb, "bounds", value.bounds);
        WriteInt(sb, "nodeCount", value.nodeCount);
        WriteInt(sb, "meshCount", value.meshCount);
        WriteArrayStart(sb, "nodes");
        for (int i = 0; i < value.nodes.Length; i++)
        {
            if (i > 0)
                sb.Append(',');
            WriteInlineObjectStart(sb);
            WriteNodeFields(sb, value.nodes[i] ?? new FAInnerPieceNodeData());
            EndObject(sb);
        }
        EndArray(sb);
        WriteArrayStart(sb, "meshes");
        for (int i = 0; i < value.meshes.Length; i++)
        {
            if (i > 0)
                sb.Append(',');
            WriteInlineObjectStart(sb);
            WriteMeshFields(sb, value.meshes[i] ?? new FAInnerPieceMeshData());
            EndObject(sb);
        }
        EndArray(sb);
        if (value.screenContract != null)
            WriteRawJson(sb, "screenContract", FAInnerPiecePackageSupport.SerializeScreenContract(value.screenContract, pretty));
        if (value.controlSurface != null)
            WriteRawJson(sb, "controlSurface", FAInnerPiecePackageSupport.SerializeControlSurface(value.controlSurface, pretty));
        return FinishObject(sb);
    }

    public static string SerializeImportReceipt(FAInnerPieceImportReceipt receipt, bool pretty)
    {
        if (receipt == null)
            receipt = new FAInnerPieceImportReceipt();
        if (receipt.archivedExistingResourceIds == null)
            receipt.archivedExistingResourceIds = new string[0];
        if (receipt.warnings == null)
            receipt.warnings = new string[0];
        StringBuilder sb = StartObject();
        WriteString(sb, "schemaVersion", receipt.schemaVersion);
        WriteString(sb, "resourceId", receipt.resourceId);
        WriteString(sb, "fingerprint", receipt.fingerprint);
        WriteString(sb, "packagePath", receipt.packagePath);
        WriteString(sb, "packageKind", receipt.packageKind);
        WriteString(sb, "manifestPath", receipt.manifestPath);
        WriteString(sb, "geometryPath", receipt.geometryPath);
        WriteString(sb, "materialsPath", receipt.materialsPath);
        WriteString(sb, "screensPath", receipt.screensPath);
        WriteString(sb, "controlsPath", receipt.controlsPath);
        WriteString(sb, "previewPath", receipt.previewPath);
        WriteString(sb, "importedAtUtc", receipt.importedAtUtc);
        WriteBool(sb, "created", receipt.created);
        WriteBool(sb, "updated", receipt.updated);
        WriteStringArray(sb, "archivedExistingResourceIds", receipt.archivedExistingResourceIds);
        WriteStringArray(sb, "warnings", receipt.warnings);
        return FinishObject(sb);
    }

    public static string SerializeInstanceState(FAInnerPieceInstanceStateData state, bool pretty)
    {
        if (state == null)
            state = new FAInnerPieceInstanceStateData();
        if (state.spawnedNodeIds == null)
            state.spawnedNodeIds = new string[0];
        if (state.rootTransform == null)
            state.rootTransform = new FAInnerPieceTransformData();
        StringBuilder sb = StartObject();
        WriteString(sb, "schemaVersion", state.schemaVersion);
        WriteString(sb, "instanceId", state.instanceId);
        WriteString(sb, "resourceId", state.resourceId);
        WriteString(sb, "consumerId", state.consumerId);
        WriteString(sb, "targetType", state.targetType);
        WriteString(sb, "groupId", state.groupId);
        WriteString(sb, "rootObjectId", state.rootObjectId);
        WriteStringArray(sb, "spawnedNodeIds", state.spawnedNodeIds);
        WriteObjectStart(sb, "rootTransform");
        WriteVector3(sb, "position", state.rootTransform.position);
        WriteQuaternion(sb, "rotation", state.rootTransform.rotation);
        WriteVector3(sb, "scale", state.rootTransform.scale);
        EndObject(sb);
        sb.Append(',');
        WriteString(sb, "screenContractVersion", state.screenContractVersion);
        WriteString(sb, "shellId", state.shellId);
        WriteString(sb, "deviceClass", state.deviceClass);
        WriteString(sb, "orientationSupport", state.orientationSupport);
        WriteString(sb, "defaultAspectMode", state.defaultAspectMode);
        WriteFloat(sb, "safeCornerRadius", state.safeCornerRadius);
        WriteString(sb, "inputStyle", state.inputStyle);
        WriteBool(sb, "autoOrientToGround", state.autoOrientToGround);
        WriteArrayStart(sb, "screenSlots");
        FAInnerPieceScreenSlotRuntimeState[] screenSlots = state.screenSlots ?? new FAInnerPieceScreenSlotRuntimeState[0];
        for (int i = 0; i < screenSlots.Length; i++)
        {
            if (i > 0)
                sb.Append(',');
            FAInnerPieceScreenSlotRuntimeState slot = screenSlots[i] ?? new FAInnerPieceScreenSlotRuntimeState();
            WriteInlineObjectStart(sb);
            WriteString(sb, "slotId", slot.slotId);
            WriteString(sb, "displayId", slot.displayId);
            WriteString(sb, "surfaceTargetId", slot.surfaceTargetId);
            WriteString(sb, "disconnectStateId", slot.disconnectStateId);
            WriteString(sb, "screenSurfaceNodeId", slot.screenSurfaceNodeId);
            WriteString(sb, "screenGlassNodeId", slot.screenGlassNodeId);
            WriteString(sb, "screenApertureNodeId", slot.screenApertureNodeId);
            WriteString(sb, "disconnectSurfaceNodeId", slot.disconnectSurfaceNodeId);
            WriteBool(sb, "disconnectSurfaceVisible", slot.disconnectSurfaceVisible);
            WriteString(sb, "boundState", slot.boundState);
            WriteObjectStart(sb, "plane");
            FAInnerPiecePlaneData plane = slot.plane ?? new FAInnerPiecePlaneData();
            WriteVector3(sb, "center", plane.center);
            WriteVector3(sb, "right", plane.right);
            WriteVector3(sb, "up", plane.up);
            WriteVector3(sb, "forward", plane.forward);
            WriteFloat(sb, "widthMeters", plane.widthMeters);
            WriteFloat(sb, "heightMeters", plane.heightMeters);
            WriteFloat(sb, "depthMeters", plane.depthMeters);
            EndObject(sb);
            EndObject(sb);
        }
        EndArray(sb);
        if (state.controlSurface != null)
            WriteRawJson(sb, "controlSurface", FAInnerPiecePackageSupport.SerializeControlSurface(state.controlSurface, pretty));
        WriteString(sb, "lastError", state.lastError);
        return FinishObject(sb);
    }

    public static string SerializeActionReceipt(FAInnerPieceActionReceipt receipt, bool pretty)
    {
        if (receipt == null)
            receipt = new FAInnerPieceActionReceipt();
        StringBuilder sb = StartObject();
        WriteString(sb, "schemaVersion", receipt.schemaVersion);
        WriteString(sb, "actionId", receipt.actionId);
        WriteString(sb, "summary", receipt.summary);
        WriteString(sb, "resourceId", receipt.resourceId);
        WriteString(sb, "instanceId", receipt.instanceId);
        WriteString(sb, "consumerId", receipt.consumerId);
        WriteString(sb, "targetType", receipt.targetType);
        WriteString(sb, "lastError", receipt.lastError);
        if (receipt.importReceipt != null)
            WriteRawJson(sb, "importReceipt", SerializeImportReceipt(receipt.importReceipt, pretty));
        if (receipt.resource != null)
            WriteRawJson(sb, "resource", SerializeResource(receipt.resource, pretty));
        if (receipt.instanceState != null)
            WriteRawJson(sb, "instanceState", SerializeInstanceState(receipt.instanceState, pretty));
        return FinishObject(sb);
    }

    private static FAInnerPieceCatalogState DeserializeCatalog(string json)
    {
        if (string.IsNullOrEmpty(json))
            return new FAInnerPieceCatalogState();

        FAInnerPieceCatalogState catalog = new FAInnerPieceCatalogState();
        catalog.schemaVersion = ReadString(json, "schemaVersion", catalog.schemaVersion);
        string arrayJson;
        if (TryReadArray(json, "entries", out arrayJson))
        {
            List<string> entriesJson = ExtractObjectsFromArray(arrayJson);
            catalog.entries = new FAInnerPieceCatalogEntry[entriesJson.Count];
            for (int i = 0; i < entriesJson.Count; i++)
                catalog.entries[i] = DeserializeCatalogEntry(entriesJson[i]);
        }
        return NormalizeCatalog(catalog);
    }

    private static FAInnerPieceStoredResource DeserializeResource(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;
        return NormalizeStoredResource(DeserializeStoredResource(json));
    }

    private static FAInnerPieceImportReceipt DeserializeImportReceipt(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;
        FAInnerPieceImportReceipt receipt = new FAInnerPieceImportReceipt();
        receipt.schemaVersion = ReadString(json, "schemaVersion", receipt.schemaVersion);
        receipt.resourceId = ReadString(json, "resourceId", receipt.resourceId);
        receipt.fingerprint = ReadString(json, "fingerprint", receipt.fingerprint);
        receipt.packagePath = ReadString(json, "packagePath", receipt.packagePath);
        receipt.packageKind = ReadString(json, "packageKind", receipt.packageKind);
        receipt.manifestPath = ReadString(json, "manifestPath", receipt.manifestPath);
        receipt.geometryPath = ReadString(json, "geometryPath", receipt.geometryPath);
        receipt.materialsPath = ReadString(json, "materialsPath", receipt.materialsPath);
        receipt.screensPath = ReadString(json, "screensPath", receipt.screensPath);
        receipt.controlsPath = ReadString(json, "controlsPath", receipt.controlsPath);
        receipt.previewPath = ReadString(json, "previewPath", receipt.previewPath);
        receipt.importedAtUtc = ReadString(json, "importedAtUtc", receipt.importedAtUtc);
        receipt.created = ReadBool(json, "created", receipt.created);
        receipt.updated = ReadBool(json, "updated", receipt.updated);
        receipt.archivedExistingResourceIds = ReadStringArray(json, "archivedExistingResourceIds");
        receipt.warnings = ReadStringArray(json, "warnings");
        return receipt;
    }

    private static FAInnerPieceExportPackage DeserializeExportPackage(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        FAInnerPieceExportPackage package = new FAInnerPieceExportPackage();
        package.schemaVersion = ReadString(json, "schemaVersion", package.schemaVersion);
        package.displayName = ReadString(json, "displayName", package.displayName);
        package.sourceKind = ReadString(json, "sourceKind", package.sourceKind);
        package.sourcePath = ReadString(json, "sourcePath", package.sourcePath);
        package.units = ReadString(json, "units", package.units);
        package.tags = ReadStringArray(json, "tags");
        package.nodes = ReadNodes(json, "nodes");
        package.meshes = ReadMeshes(json, "meshes");
        return package;
    }

    private static FAInnerPieceStoredResource DeserializeStoredResource(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        FAInnerPieceStoredResource resource = new FAInnerPieceStoredResource();
        resource.schemaVersion = ReadString(json, "schemaVersion", resource.schemaVersion);
        resource.resourceId = ReadString(json, "resourceId", resource.resourceId);
        resource.displayName = ReadString(json, "displayName", resource.displayName);
        resource.sourceKind = ReadString(json, "sourceKind", resource.sourceKind);
        resource.sourcePath = ReadString(json, "sourcePath", resource.sourcePath);
        resource.units = ReadString(json, "units", resource.units);
        resource.fingerprint = ReadString(json, "fingerprint", resource.fingerprint);
        resource.archived = ReadBool(json, "archived", resource.archived);
        resource.tags = ReadStringArray(json, "tags");
        resource.bounds = ReadBounds(json, "bounds");
        resource.nodeCount = ReadInt(json, "nodeCount", resource.nodeCount);
        resource.meshCount = ReadInt(json, "meshCount", resource.meshCount);
        resource.nodes = ReadNodes(json, "nodes");
        resource.meshes = ReadMeshes(json, "meshes");
        string screenContractJson;
        if (TryReadObject(json, "screenContract", out screenContractJson))
            resource.screenContract = FAInnerPiecePackageSupport.DeserializeScreenContract(screenContractJson);
        string controlSurfaceJson;
        if (TryReadObject(json, "controlSurface", out controlSurfaceJson))
            resource.controlSurface = FAInnerPiecePackageSupport.DeserializeControlSurface(controlSurfaceJson);
        return resource;
    }

    private static FAInnerPieceCatalogEntry DeserializeCatalogEntry(string json)
    {
        FAInnerPieceCatalogEntry entry = new FAInnerPieceCatalogEntry();
        entry.resourceId = ReadString(json, "resourceId", entry.resourceId);
        entry.displayName = ReadString(json, "displayName", entry.displayName);
        entry.fingerprint = ReadString(json, "fingerprint", entry.fingerprint);
        entry.sourceKind = ReadString(json, "sourceKind", entry.sourceKind);
        entry.sourcePath = ReadString(json, "sourcePath", entry.sourcePath);
        entry.archived = ReadBool(json, "archived", entry.archived);
        entry.tags = ReadStringArray(json, "tags");
        entry.bounds = ReadBounds(json, "bounds");
        entry.nodeCount = ReadInt(json, "nodeCount", entry.nodeCount);
        entry.meshCount = ReadInt(json, "meshCount", entry.meshCount);
        entry.lastImportedUtc = ReadString(json, "lastImportedUtc", entry.lastImportedUtc);
        return entry;
    }

    private static FAInnerPieceNodeData[] ReadNodes(string json, string key)
    {
        string arrayJson;
        if (!TryReadArray(json, key, out arrayJson))
            return new FAInnerPieceNodeData[0];

        List<string> objects = ExtractObjectsFromArray(arrayJson);
        FAInnerPieceNodeData[] nodes = new FAInnerPieceNodeData[objects.Count];
        for (int i = 0; i < objects.Count; i++)
            nodes[i] = DeserializeNode(objects[i]);
        return nodes;
    }

    private static FAInnerPieceMeshData[] ReadMeshes(string json, string key)
    {
        string arrayJson;
        if (!TryReadArray(json, key, out arrayJson))
            return new FAInnerPieceMeshData[0];

        List<string> objects = ExtractObjectsFromArray(arrayJson);
        FAInnerPieceMeshData[] meshes = new FAInnerPieceMeshData[objects.Count];
        for (int i = 0; i < objects.Count; i++)
            meshes[i] = DeserializeMesh(objects[i]);
        return meshes;
    }

    private static FAInnerPieceNodeData DeserializeNode(string json)
    {
        FAInnerPieceNodeData node = new FAInnerPieceNodeData();
        node.nodeId = ReadString(json, "nodeId", node.nodeId);
        node.parentNodeId = ReadString(json, "parentNodeId", node.parentNodeId);
        node.displayName = ReadString(json, "displayName", node.displayName);
        node.localPosition = ReadVector3(json, "localPosition", node.localPosition);
        node.localRotation = ReadQuaternion(json, "localRotation", node.localRotation);
        node.localScale = ReadVector3(json, "localScale", node.localScale);
        node.meshRefIds = ReadStringArray(json, "meshRefIds");
        return node;
    }

    private static FAInnerPieceMeshData DeserializeMesh(string json)
    {
        FAInnerPieceMeshData mesh = new FAInnerPieceMeshData();
        mesh.meshId = ReadString(json, "meshId", mesh.meshId);
        mesh.materialRefId = ReadString(json, "materialRefId", mesh.materialRefId);
        mesh.submeshIndex = ReadInt(json, "submeshIndex", mesh.submeshIndex);
        mesh.vertices = ReadVector3Array(json, "vertices");
        mesh.triangleIndices = ReadIntArray(json, "triangleIndices");
        mesh.normals = ReadVector3Array(json, "normals");
        mesh.uv0 = ReadVector2Array(json, "uv0");
        mesh.localBounds = ReadBounds(json, "localBounds");
        return mesh;
    }

    private static FAInnerPieceBoundsData ReadBounds(string json, string key)
    {
        string boundsJson;
        if (!TryReadObject(json, key, out boundsJson))
            return new FAInnerPieceBoundsData();

        FAInnerPieceBoundsData bounds = new FAInnerPieceBoundsData();
        bounds.center = ReadVector3(boundsJson, "center", bounds.center);
        bounds.size = ReadVector3(boundsJson, "size", bounds.size);
        return bounds;
    }

    private static FAInnerPieceCatalogState NormalizeCatalog(FAInnerPieceCatalogState catalog)
    {
        if (catalog == null)
            catalog = new FAInnerPieceCatalogState();
        if (string.IsNullOrEmpty(catalog.schemaVersion))
            catalog.schemaVersion = FAInnerPieceSchemas.CatalogV1;
        if (catalog.entries == null)
            catalog.entries = new FAInnerPieceCatalogEntry[0];

        for (int i = 0; i < catalog.entries.Length; i++)
        {
            FAInnerPieceCatalogEntry entry = catalog.entries[i];
            if (entry == null)
            {
                catalog.entries[i] = new FAInnerPieceCatalogEntry();
                entry = catalog.entries[i];
            }

            if (entry.tags == null)
                entry.tags = new string[0];
            if (entry.bounds == null)
                entry.bounds = new FAInnerPieceBoundsData();
        }

        return catalog;
    }

    private static FAInnerPieceStoredResource NormalizeStoredResource(FAInnerPieceStoredResource resource)
    {
        if (resource == null)
            return null;

        if (string.IsNullOrEmpty(resource.schemaVersion))
            resource.schemaVersion = FAInnerPieceSchemas.ResourceV1;
        if (resource.tags == null)
            resource.tags = new string[0];
        if (resource.bounds == null)
            resource.bounds = new FAInnerPieceBoundsData();
        if (resource.nodes == null)
            resource.nodes = new FAInnerPieceNodeData[0];
        if (resource.meshes == null)
            resource.meshes = new FAInnerPieceMeshData[0];
        resource.screenContract = FAInnerPiecePackageSupport.NormalizeScreenContract(resource.screenContract);
        resource.controlSurface = FAInnerPiecePackageSupport.NormalizeControlSurface(resource.controlSurface);

        for (int i = 0; i < resource.nodes.Length; i++)
        {
            FAInnerPieceNodeData node = resource.nodes[i];
            if (node == null)
            {
                resource.nodes[i] = new FAInnerPieceNodeData();
                node = resource.nodes[i];
            }

            if (node.meshRefIds == null)
                node.meshRefIds = new string[0];
            if (node.localScale == Vector3.zero)
                node.localScale = Vector3.one;
            if (node.localRotation.x == 0f && node.localRotation.y == 0f && node.localRotation.z == 0f && node.localRotation.w == 0f)
                node.localRotation = Quaternion.identity;
        }

        for (int i = 0; i < resource.meshes.Length; i++)
        {
            FAInnerPieceMeshData mesh = resource.meshes[i];
            if (mesh == null)
            {
                resource.meshes[i] = new FAInnerPieceMeshData();
                mesh = resource.meshes[i];
            }

            if (mesh.vertices == null)
                mesh.vertices = new Vector3[0];
            if (mesh.triangleIndices == null)
                mesh.triangleIndices = new int[0];
            if (mesh.normals == null)
                mesh.normals = new Vector3[0];
            if (mesh.uv0 == null)
                mesh.uv0 = new Vector2[0];
            if (mesh.localBounds == null)
                mesh.localBounds = new FAInnerPieceBoundsData();
            if (string.IsNullOrEmpty(mesh.materialRefId))
                mesh.materialRefId = "";
        }

        resource.nodeCount = resource.nodes.Length;
        resource.meshCount = resource.meshes.Length;
        return resource;
    }

    private static bool TryNormalizePackage(
        FAInnerPieceExportPackage package,
        FAInnerPieceScreenContractData screenContract,
        FAInnerPieceControlSurfaceData controlSurface,
        string normalizedPath,
        string displayNameOverride,
        string[] additionalTags,
        out FAInnerPieceStoredResource resource,
        out List<string> warnings,
        out string errorMessage
    )
    {
        resource = null;
        warnings = new List<string>();
        errorMessage = "";

        List<FAInnerPieceMeshData> meshes = new List<FAInnerPieceMeshData>();
        Dictionary<string, string> meshIdMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> usedMeshIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        FAInnerPieceControlSurfaceData normalizedControlSurface = FAInnerPiecePackageSupport.NormalizeControlSurface(controlSurface);
        ControlSurfaceImportDiagnostics controlSurfaceDiagnostics = AnalyzeControlSurfaceForImport(normalizedControlSurface, warnings);
        FAInnerPieceMeshData[] sourceMeshes = package.meshes ?? new FAInnerPieceMeshData[0];
        for (int i = 0; i < sourceMeshes.Length; i++)
        {
            FAInnerPieceMeshData normalizedMesh;
            if (!TryNormalizeMesh(sourceMeshes[i], i, usedMeshIds, out normalizedMesh, out errorMessage))
                return false;
            meshes.Add(normalizedMesh);
            string originalId = sourceMeshes[i] != null ? sourceMeshes[i].meshId ?? "" : "";
            meshIdMap[originalId] = normalizedMesh.meshId;
        }

        if (meshes.Count <= 0 && normalizedControlSurface == null)
        {
            errorMessage = "package contains no meshes";
            return false;
        }

        if (meshes.Count <= 0 && controlSurfaceDiagnostics.actionableElementCount <= 0)
        {
            errorMessage = "package contains no meshes and no actionable control surface elements";
            return false;
        }

        List<FAInnerPieceNodeData> nodes = new List<FAInnerPieceNodeData>();
        HashSet<string> usedNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        FAInnerPieceNodeData[] sourceNodes = package.nodes ?? new FAInnerPieceNodeData[0];
        for (int i = 0; i < sourceNodes.Length; i++)
        {
            FAInnerPieceNodeData normalizedNode = NormalizeNode(sourceNodes[i], i, usedNodeIds, meshIdMap);
            nodes.Add(normalizedNode);
        }

        if (nodes.Count <= 0)
            nodes.Add(CreateDefaultRootNode());

        Dictionary<string, FAInnerPieceNodeData> nodeLookup = new Dictionary<string, FAInnerPieceNodeData>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < nodes.Count; i++)
        {
            FAInnerPieceNodeData node = nodes[i];
            if (string.Equals(node.parentNodeId, node.nodeId, StringComparison.OrdinalIgnoreCase))
                node.parentNodeId = "";
            if (!string.IsNullOrEmpty(node.parentNodeId) && !nodeLookup.ContainsKey(node.parentNodeId))
                node.parentNodeId = "";
            nodeLookup[node.nodeId] = node;
        }

        HashSet<string> referencedMeshes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < nodes.Count; i++)
        {
            FAInnerPieceNodeData node = nodes[i];
            if (node.meshRefIds == null)
                node.meshRefIds = new string[0];

            List<string> validRefs = new List<string>();
            for (int j = 0; j < node.meshRefIds.Length; j++)
            {
                string meshId = node.meshRefIds[j];
                if (string.IsNullOrEmpty(meshId))
                    continue;
                if (!ContainsMeshId(meshes, meshId))
                {
                    warnings.Add("mesh_ref_missing:" + meshId);
                    continue;
                }
                if (referencedMeshes.Contains(meshId))
                    continue;
                referencedMeshes.Add(meshId);
                validRefs.Add(meshId);
            }
            node.meshRefIds = validRefs.ToArray();
        }

        FAInnerPieceNodeData defaultNode = FindFirstRootNode(nodes);
        for (int i = 0; i < meshes.Count; i++)
        {
            string meshId = meshes[i].meshId;
            if (referencedMeshes.Contains(meshId))
                continue;

            List<string> refs = new List<string>(defaultNode.meshRefIds ?? new string[0]);
            refs.Add(meshId);
            defaultNode.meshRefIds = refs.ToArray();
            warnings.Add("mesh_attached_to_default_root:" + meshId);
        }

        FAInnerPieceBoundsData bounds = ComputeResourceBounds(nodes, meshes);
        resource = new FAInnerPieceStoredResource();
        resource.displayName = string.IsNullOrEmpty(displayNameOverride)
            ? ResolveDisplayName(package.displayName, normalizedPath)
            : displayNameOverride.Trim();
        resource.sourceKind = string.IsNullOrEmpty(package.sourceKind) ? "unity_export_json" : package.sourceKind.Trim();
        resource.sourcePath = string.IsNullOrEmpty(package.sourcePath) ? normalizedPath : NormalizeExternalPath(package.sourcePath);
        resource.units = string.IsNullOrEmpty(package.units) ? "meters" : package.units.Trim();
        resource.tags = MergeTags(package.tags, additionalTags);
        resource.bounds = bounds;
        resource.nodes = nodes.ToArray();
        resource.meshes = meshes.ToArray();
        resource.screenContract = FAInnerPiecePackageSupport.NormalizeScreenContract(screenContract);
        resource.controlSurface = normalizedControlSurface;
        resource.nodeCount = resource.nodes.Length;
        resource.meshCount = resource.meshes.Length;
        resource.fingerprint = BuildFingerprint(resource);
        return true;
    }

    private static ControlSurfaceImportDiagnostics AnalyzeControlSurfaceForImport(
        FAInnerPieceControlSurfaceData controlSurface,
        List<string> warnings)
    {
        ControlSurfaceImportDiagnostics diagnostics = new ControlSurfaceImportDiagnostics();
        if (controlSurface == null)
            return diagnostics;

        string[] targetDisplayIds = controlSurface.targetDisplayIds ?? new string[0];
        diagnostics.hasTargetDisplay =
            !string.IsNullOrEmpty(controlSurface.defaultTargetDisplayId)
            || targetDisplayIds.Length > 0;
        diagnostics.hasSurfaceNodeId = !string.IsNullOrEmpty(controlSurface.surfaceNodeId);

        if (!diagnostics.hasTargetDisplay)
            warnings.Add("control_surface_target_display_missing");
        if (!diagnostics.hasSurfaceNodeId)
            warnings.Add("control_surface_surface_node_missing");

        HashSet<string> seenElementIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        FAInnerPieceControlElementData[] elements = controlSurface.elements ?? new FAInnerPieceControlElementData[0];
        for (int i = 0; i < elements.Length; i++)
        {
            FAInnerPieceControlElementData element = elements[i];
            string elementToken = BuildControlSurfaceElementWarningToken(element, i);
            if (element == null)
            {
                warnings.Add("control_surface_element_missing:" + elementToken);
                continue;
            }

            string elementId = element.elementId ?? "";
            string actionId = element.actionId ?? "";
            bool hasNormalizedRect = element.normalizedRect != null
                && element.normalizedRect.width > 0.0001f
                && element.normalizedRect.height > 0.0001f;

            if (!string.IsNullOrEmpty(actionId))
                diagnostics.actionableElementCount++;
            else
                warnings.Add("control_surface_element_action_missing:" + elementToken);

            if (string.IsNullOrEmpty(elementId))
            {
                warnings.Add("control_surface_pointer_element_id_missing:" + elementToken);
            }
            else if (!seenElementIds.Add(elementId))
            {
                warnings.Add("control_surface_element_id_duplicate:" + elementId);
            }

            if (!hasNormalizedRect)
                warnings.Add("control_surface_pointer_rect_missing:" + elementToken);

            if (!element.readOnly
                && !string.IsNullOrEmpty(actionId)
                && !string.IsNullOrEmpty(elementId)
                && hasNormalizedRect
                && diagnostics.hasSurfaceNodeId)
            {
                diagnostics.pointerReadyElementCount++;
            }
        }

        if (diagnostics.actionableElementCount <= 0)
            warnings.Add("control_surface_no_actionable_elements");
        else if (diagnostics.pointerReadyElementCount <= 0)
            warnings.Add("control_surface_pointer_not_ready");

        return diagnostics;
    }

    private static string BuildControlSurfaceElementWarningToken(FAInnerPieceControlElementData element, int index)
    {
        if (element != null)
        {
            if (!string.IsNullOrEmpty(element.elementId))
                return element.elementId;
            if (!string.IsNullOrEmpty(element.actionId))
                return element.actionId;
        }

        return "index_" + index.ToString(CultureInfo.InvariantCulture);
    }

    private static bool TryNormalizeMesh(
        FAInnerPieceMeshData source,
        int index,
        HashSet<string> usedMeshIds,
        out FAInnerPieceMeshData mesh,
        out string errorMessage
    )
    {
        mesh = null;
        errorMessage = "";

        if (source == null)
        {
            errorMessage = "mesh entry missing";
            return false;
        }

        mesh = new FAInnerPieceMeshData();
        mesh.meshId = BuildUniqueId(source.meshId, "mesh", index, usedMeshIds);
        mesh.materialRefId = source.materialRefId ?? "";
        mesh.submeshIndex = source.submeshIndex;
        mesh.vertices = CloneVector3Array(source.vertices);
        mesh.triangleIndices = CloneIntArray(source.triangleIndices);
        mesh.normals = CloneVector3Array(source.normals);
        mesh.uv0 = CloneVector2Array(source.uv0);

        if (mesh.vertices.Length <= 0)
        {
            errorMessage = "mesh vertices missing";
            return false;
        }

        if (mesh.triangleIndices.Length < 3 || mesh.triangleIndices.Length % 3 != 0)
        {
            errorMessage = "triangle indices missing";
            return false;
        }

        for (int i = 0; i < mesh.triangleIndices.Length; i++)
        {
            int vertexIndex = mesh.triangleIndices[i];
            if (vertexIndex < 0 || vertexIndex >= mesh.vertices.Length)
            {
                errorMessage = "triangle index out of range";
                return false;
            }
        }

        if (mesh.normals.Length != mesh.vertices.Length)
            mesh.normals = new Vector3[0];
        if (mesh.uv0.Length != mesh.vertices.Length)
            mesh.uv0 = new Vector2[0];

        mesh.localBounds = ComputeBounds(mesh.vertices);
        return true;
    }

    private static FAInnerPieceNodeData NormalizeNode(
        FAInnerPieceNodeData source,
        int index,
        HashSet<string> usedNodeIds,
        Dictionary<string, string> meshIdMap
    )
    {
        FAInnerPieceNodeData node = new FAInnerPieceNodeData();
        node.nodeId = BuildUniqueId(source != null ? source.nodeId : "", "node", index, usedNodeIds);
        node.parentNodeId = source != null ? source.parentNodeId ?? "" : "";
        node.displayName = source != null && !string.IsNullOrEmpty(source.displayName)
            ? source.displayName
            : "Node " + (index + 1).ToString(CultureInfo.InvariantCulture);
        node.localPosition = source != null ? source.localPosition : Vector3.zero;
        node.localRotation = source != null ? source.localRotation : Quaternion.identity;
        if (node.localRotation.x == 0f && node.localRotation.y == 0f && node.localRotation.z == 0f && node.localRotation.w == 0f)
            node.localRotation = Quaternion.identity;
        node.localScale = source != null ? source.localScale : Vector3.one;
        if (node.localScale == Vector3.zero)
            node.localScale = Vector3.one;

        List<string> meshRefs = new List<string>();
        string[] sourceRefs = source != null && source.meshRefIds != null ? source.meshRefIds : new string[0];
        for (int i = 0; i < sourceRefs.Length; i++)
        {
            string sourceId = sourceRefs[i];
            if (string.IsNullOrEmpty(sourceId))
                continue;
            string normalizedMeshId;
            if (meshIdMap.TryGetValue(sourceId, out normalizedMeshId) && !string.IsNullOrEmpty(normalizedMeshId))
                meshRefs.Add(normalizedMeshId);
            else
                meshRefs.Add(sourceId);
        }
        node.meshRefIds = meshRefs.ToArray();
        return node;
    }

    private static FAInnerPieceNodeData CreateDefaultRootNode()
    {
        FAInnerPieceNodeData node = new FAInnerPieceNodeData();
        node.nodeId = "root";
        node.displayName = "Root";
        return node;
    }

    private static FAInnerPieceNodeData FindFirstRootNode(List<FAInnerPieceNodeData> nodes)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (string.IsNullOrEmpty(nodes[i].parentNodeId))
                return nodes[i];
        }

        return nodes.Count > 0 ? nodes[0] : CreateDefaultRootNode();
    }

    private static bool ContainsMeshId(List<FAInnerPieceMeshData> meshes, string meshId)
    {
        for (int i = 0; i < meshes.Count; i++)
        {
            if (string.Equals(meshes[i].meshId, meshId, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static FAInnerPieceBoundsData ComputeBounds(Vector3[] vertices)
    {
        FAInnerPieceBoundsData bounds = new FAInnerPieceBoundsData();
        if (vertices == null || vertices.Length <= 0)
            return bounds;

        Vector3 min = vertices[0];
        Vector3 max = vertices[0];
        for (int i = 1; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }

        bounds.center = (min + max) * 0.5f;
        bounds.size = max - min;
        return bounds;
    }

    private static FAInnerPieceBoundsData ComputeResourceBounds(List<FAInnerPieceNodeData> nodes, List<FAInnerPieceMeshData> meshes)
    {
        FAInnerPieceBoundsData bounds = new FAInnerPieceBoundsData();
        if (nodes == null || nodes.Count <= 0 || meshes == null || meshes.Count <= 0)
            return bounds;

        Dictionary<string, FAInnerPieceNodeData> nodeLookup = new Dictionary<string, FAInnerPieceNodeData>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < nodes.Count; i++)
            nodeLookup[nodes[i].nodeId] = nodes[i];

        Dictionary<string, Matrix4x4> worldMatrices = new Dictionary<string, Matrix4x4>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, FAInnerPieceMeshData> meshLookup = new Dictionary<string, FAInnerPieceMeshData>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < meshes.Count; i++)
            meshLookup[meshes[i].meshId] = meshes[i];

        bool hasBounds = false;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;

        for (int i = 0; i < nodes.Count; i++)
        {
            FAInnerPieceNodeData node = nodes[i];
            Matrix4x4 matrix = ResolveWorldMatrix(node, nodeLookup, worldMatrices);
            string[] meshRefs = node.meshRefIds ?? new string[0];
            for (int j = 0; j < meshRefs.Length; j++)
            {
                FAInnerPieceMeshData mesh;
                if (!meshLookup.TryGetValue(meshRefs[j], out mesh) || mesh == null || mesh.vertices == null)
                    continue;

                for (int k = 0; k < mesh.vertices.Length; k++)
                {
                    Vector3 point = matrix.MultiplyPoint3x4(mesh.vertices[k]);
                    if (!hasBounds)
                    {
                        min = point;
                        max = point;
                        hasBounds = true;
                    }
                    else
                    {
                        min = Vector3.Min(min, point);
                        max = Vector3.Max(max, point);
                    }
                }
            }
        }

        if (!hasBounds)
            return bounds;

        bounds.center = (min + max) * 0.5f;
        bounds.size = max - min;
        return bounds;
    }

    private static Matrix4x4 ResolveWorldMatrix(
        FAInnerPieceNodeData node,
        Dictionary<string, FAInnerPieceNodeData> nodeLookup,
        Dictionary<string, Matrix4x4> worldMatrices
    )
    {
        Matrix4x4 cached;
        if (worldMatrices.TryGetValue(node.nodeId, out cached))
            return cached;

        Matrix4x4 local = Matrix4x4.TRS(node.localPosition, node.localRotation, node.localScale == Vector3.zero ? Vector3.one : node.localScale);
        if (string.IsNullOrEmpty(node.parentNodeId))
        {
            worldMatrices[node.nodeId] = local;
            return local;
        }

        FAInnerPieceNodeData parent;
        if (!nodeLookup.TryGetValue(node.parentNodeId, out parent) || parent == null)
        {
            worldMatrices[node.nodeId] = local;
            return local;
        }

        Matrix4x4 world = ResolveWorldMatrix(parent, nodeLookup, worldMatrices) * local;
        worldMatrices[node.nodeId] = world;
        return world;
    }

    private static void UpsertCatalogEntry(FAInnerPieceCatalogState catalog, FAInnerPieceStoredResource resource, string lastImportedUtc)
    {
        catalog = NormalizeCatalog(catalog);
        List<FAInnerPieceCatalogEntry> entries = new List<FAInnerPieceCatalogEntry>(catalog.entries);
        bool replaced = false;
        for (int i = 0; i < entries.Count; i++)
        {
            FAInnerPieceCatalogEntry entry = entries[i];
            if (entry == null)
                continue;
            if (!string.Equals(entry.resourceId, resource.resourceId, StringComparison.OrdinalIgnoreCase))
                continue;

            ApplyCatalogEntry(entry, resource, lastImportedUtc);
            replaced = true;
            break;
        }

        if (!replaced)
        {
            FAInnerPieceCatalogEntry entry = new FAInnerPieceCatalogEntry();
            ApplyCatalogEntry(entry, resource, lastImportedUtc);
            entries.Add(entry);
        }

        catalog.entries = entries.ToArray();
    }

    private static void ApplyCatalogEntry(FAInnerPieceCatalogEntry entry, FAInnerPieceStoredResource resource, string lastImportedUtc)
    {
        entry.resourceId = resource.resourceId;
        entry.displayName = resource.displayName;
        entry.fingerprint = resource.fingerprint;
        entry.sourceKind = resource.sourceKind;
        entry.sourcePath = resource.sourcePath;
        entry.archived = resource.archived;
        entry.tags = CloneStringArray(resource.tags);
        entry.bounds = resource.bounds != null
            ? new FAInnerPieceBoundsData { center = resource.bounds.center, size = resource.bounds.size }
            : new FAInnerPieceBoundsData();
        entry.nodeCount = resource.nodeCount;
        entry.meshCount = resource.meshCount;
        entry.lastImportedUtc = lastImportedUtc;
    }

    private static FAInnerPieceCatalogEntry FindEntryByFingerprint(FAInnerPieceCatalogState catalog, string fingerprint)
    {
        if (catalog == null || catalog.entries == null || string.IsNullOrEmpty(fingerprint))
            return null;

        for (int i = 0; i < catalog.entries.Length; i++)
        {
            FAInnerPieceCatalogEntry entry = catalog.entries[i];
            if (entry != null && string.Equals(entry.fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        return null;
    }

    private static FAInnerPieceCatalogEntry FindEntryByResourceId(FAInnerPieceCatalogState catalog, string resourceId)
    {
        if (catalog == null || catalog.entries == null || string.IsNullOrEmpty(resourceId))
            return null;

        for (int i = 0; i < catalog.entries.Length; i++)
        {
            FAInnerPieceCatalogEntry entry = catalog.entries[i];
            if (entry != null && string.Equals(entry.resourceId, resourceId, StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        return null;
    }

    private static string BuildResourceId(string displayName, string fingerprint, FAInnerPieceCatalogState catalog)
    {
        string prefix = SanitizePathSegment(string.IsNullOrEmpty(displayName) ? "innerpiece" : displayName).ToLowerInvariant();
        if (string.IsNullOrEmpty(prefix))
            prefix = "innerpiece";

        string suffix = (fingerprint ?? "").Length >= 12 ? fingerprint.Substring(0, 12).ToLowerInvariant() : "resource";
        string candidate = prefix + "_" + suffix;
        if (FindEntryByResourceId(catalog, candidate) == null)
            return candidate;

        int counter = 2;
        while (FindEntryByResourceId(catalog, candidate + "_" + counter.ToString(CultureInfo.InvariantCulture)) != null)
            counter++;
        return candidate + "_" + counter.ToString(CultureInfo.InvariantCulture);
    }

    private static string ResolveDisplayName(string displayName, string normalizedPath)
    {
        if (!string.IsNullOrEmpty(displayName))
            return displayName.Trim();

        try
        {
            return GetFileNameWithoutExtension(normalizedPath);
        }
        catch
        {
            return "InnerPiece Resource";
        }
    }

    private static string BuildFingerprint(FAInnerPieceStoredResource resource)
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.Append(resource.units ?? "").Append('|');
        AppendBoundsFingerprint(sb, resource.bounds);

        FAInnerPieceNodeData[] nodes = resource.nodes ?? new FAInnerPieceNodeData[0];
        for (int i = 0; i < nodes.Length; i++)
        {
            FAInnerPieceNodeData node = nodes[i];
            sb.Append("|node|").Append(node.nodeId).Append('|').Append(node.parentNodeId).Append('|');
            AppendVector3Fingerprint(sb, node.localPosition);
            AppendQuaternionFingerprint(sb, node.localRotation);
            AppendVector3Fingerprint(sb, node.localScale);
            string[] meshRefs = node.meshRefIds ?? new string[0];
            for (int j = 0; j < meshRefs.Length; j++)
                sb.Append(meshRefs[j]).Append(',');
        }

        FAInnerPieceMeshData[] meshes = resource.meshes ?? new FAInnerPieceMeshData[0];
        for (int i = 0; i < meshes.Length; i++)
        {
            FAInnerPieceMeshData mesh = meshes[i];
            sb.Append("|mesh|").Append(mesh.meshId).Append('|');
            sb.Append(mesh.materialRefId ?? "").Append('|');
            sb.Append(mesh.submeshIndex.ToString(CultureInfo.InvariantCulture)).Append('|');
            AppendBoundsFingerprint(sb, mesh.localBounds);

            Vector3[] vertices = mesh.vertices ?? new Vector3[0];
            for (int j = 0; j < vertices.Length; j++)
                AppendVector3Fingerprint(sb, vertices[j]);

            int[] triangles = mesh.triangleIndices ?? new int[0];
            for (int j = 0; j < triangles.Length; j++)
                sb.Append(triangles[j].ToString(CultureInfo.InvariantCulture)).Append(',');

            Vector3[] normals = mesh.normals ?? new Vector3[0];
            for (int j = 0; j < normals.Length; j++)
                AppendVector3Fingerprint(sb, normals[j]);

            Vector2[] uv0 = mesh.uv0 ?? new Vector2[0];
            for (int j = 0; j < uv0.Length; j++)
            {
                sb.Append(uv0[j].x.ToString("R", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(uv0[j].y.ToString("R", CultureInfo.InvariantCulture)).Append(';');
            }
        }

        FAInnerPieceScreenContractData screenContract = resource.screenContract;
        if (screenContract != null)
        {
            sb.Append("|screen_contract|").Append(screenContract.schemaVersion ?? "").Append('|');
            sb.Append(screenContract.shellId ?? "").Append('|');
            sb.Append(screenContract.defaultDisconnectStateId ?? "").Append('|');
            sb.Append(screenContract.surfaceTargetId ?? "").Append('|');
            FAInnerPieceScreenSlotData[] slots = screenContract.slots ?? new FAInnerPieceScreenSlotData[0];
            for (int i = 0; i < slots.Length; i++)
            {
                FAInnerPieceScreenSlotData slot = slots[i] ?? new FAInnerPieceScreenSlotData();
                sb.Append("|slot|").Append(slot.slotId ?? "").Append('|');
                sb.Append(slot.surfaceTargetId ?? "").Append('|');
                sb.Append(slot.disconnectStateId ?? "").Append('|');
                sb.Append(slot.screenSurfaceNodeId ?? "").Append('|');
                sb.Append(slot.screenGlassNodeId ?? "").Append('|');
                sb.Append(slot.screenApertureNodeId ?? "").Append('|');
                sb.Append(slot.disconnectSurfaceNodeId ?? "").Append('|');
            }
        }

        FAInnerPieceControlSurfaceData controlSurface = resource.controlSurface;
        if (controlSurface != null)
        {
            sb.Append("|control_surface|").Append(controlSurface.schemaVersion ?? "").Append('|');
            sb.Append(controlSurface.controlSurfaceId ?? "").Append('|');
            sb.Append(controlSurface.controlFamilyId ?? "").Append('|');
            sb.Append(controlSurface.controlThemeId ?? "").Append('|');
            sb.Append(controlSurface.controlThemeLabel ?? "").Append('|');
            sb.Append(controlSurface.controlThemeVariantId ?? "").Append('|');
            sb.Append(controlSurface.controlThemeAssetPath ?? "").Append('|');
            sb.Append(controlSurface.controlThemeAssetGuid ?? "").Append('|');
            sb.Append(controlSurface.toolkitCategory ?? "").Append('|');
            sb.Append(controlSurface.sourcePrefabAssetPath ?? "").Append('|');
            sb.Append(controlSurface.layoutSource ?? "").Append('|');
            sb.Append(controlSurface.defaultTargetDisplayId ?? "").Append('|');
            sb.Append(controlSurface.surfaceNodeId ?? "").Append('|');
            sb.Append(controlSurface.colliderNodeId ?? "").Append('|');
            sb.Append(controlSurface.surfaceWidthMeters.ToString("R", CultureInfo.InvariantCulture)).Append('|');
            sb.Append(controlSurface.surfaceHeightMeters.ToString("R", CultureInfo.InvariantCulture)).Append('|');

            string[] targets = controlSurface.targetDisplayIds ?? new string[0];
            for (int i = 0; i < targets.Length; i++)
                sb.Append("|target|").Append(targets[i] ?? "").Append('|');

            FAInnerPieceControlElementData[] elements = controlSurface.elements ?? new FAInnerPieceControlElementData[0];
            for (int i = 0; i < elements.Length; i++)
            {
                FAInnerPieceControlElementData element = elements[i] ?? new FAInnerPieceControlElementData();
                sb.Append("|element|").Append(element.elementId ?? "").Append('|');
                sb.Append(element.actionId ?? "").Append('|');
                sb.Append(element.nodeId ?? "").Append('|');
                sb.Append(element.colliderNodeId ?? "").Append('|');
                sb.Append(element.elementKind ?? "").Append('|');
                sb.Append(element.valueKind ?? "").Append('|');
                sb.Append(element.readOnly ? "1" : "0").Append('|');
                FAInnerPieceNormalizedRectData rect = element.normalizedRect ?? new FAInnerPieceNormalizedRectData();
                sb.Append(rect.x.ToString("R", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(rect.y.ToString("R", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(rect.width.ToString("R", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(rect.height.ToString("R", CultureInfo.InvariantCulture)).Append('|');
            }
        }

        ulong hash = 1469598103934665603UL;
        string text = sb.ToString();
        for (int i = 0; i < text.Length; i++)
        {
            hash ^= text[i];
            hash *= 1099511628211UL;
        }

        return hash.ToString("x16", CultureInfo.InvariantCulture);
    }

    private static void AppendBoundsFingerprint(StringBuilder sb, FAInnerPieceBoundsData bounds)
    {
        if (bounds == null)
        {
            sb.Append("bounds:null|");
            return;
        }

        AppendVector3Fingerprint(sb, bounds.center);
        AppendVector3Fingerprint(sb, bounds.size);
    }

    private static void AppendVector3Fingerprint(StringBuilder sb, Vector3 value)
    {
        sb.Append(value.x.ToString("R", CultureInfo.InvariantCulture)).Append(',');
        sb.Append(value.y.ToString("R", CultureInfo.InvariantCulture)).Append(',');
        sb.Append(value.z.ToString("R", CultureInfo.InvariantCulture)).Append(';');
    }

    private static void AppendQuaternionFingerprint(StringBuilder sb, Quaternion value)
    {
        sb.Append(value.x.ToString("R", CultureInfo.InvariantCulture)).Append(',');
        sb.Append(value.y.ToString("R", CultureInfo.InvariantCulture)).Append(',');
        sb.Append(value.z.ToString("R", CultureInfo.InvariantCulture)).Append(',');
        sb.Append(value.w.ToString("R", CultureInfo.InvariantCulture)).Append(';');
    }

    private static string[] MergeTags(string[] left, string[] right)
    {
        HashSet<string> tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddTags(tags, left);
        AddTags(tags, right);
        List<string> ordered = new List<string>(tags);
        ordered.Sort(StringComparer.OrdinalIgnoreCase);
        return ordered.ToArray();
    }

    private static void AddTags(HashSet<string> tags, string[] values)
    {
        if (tags == null || values == null)
            return;

        for (int i = 0; i < values.Length; i++)
        {
            string tag = values[i];
            if (string.IsNullOrEmpty(tag))
                continue;
            string trimmed = tag.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                tags.Add(trimmed);
        }
    }

    private static string BuildUniqueId(string requestedId, string prefix, int index, HashSet<string> usedIds)
    {
        string baseId = SanitizePathSegment(string.IsNullOrEmpty(requestedId) ? prefix + "_" + index.ToString(CultureInfo.InvariantCulture) : requestedId);
        if (string.IsNullOrEmpty(baseId))
            baseId = prefix + "_" + index.ToString(CultureInfo.InvariantCulture);

        string candidate = baseId;
        int counter = 2;
        while (usedIds.Contains(candidate))
        {
            candidate = baseId + "_" + counter.ToString(CultureInfo.InvariantCulture);
            counter++;
        }

        usedIds.Add(candidate);
        return candidate;
    }

    private static string[] CloneStringArray(string[] source)
    {
        if (source == null || source.Length <= 0)
            return new string[0];
        string[] clone = new string[source.Length];
        for (int i = 0; i < source.Length; i++)
            clone[i] = source[i] ?? "";
        return clone;
    }

    private static Vector3[] CloneVector3Array(Vector3[] source)
    {
        if (source == null || source.Length <= 0)
            return new Vector3[0];
        Vector3[] clone = new Vector3[source.Length];
        Array.Copy(source, clone, source.Length);
        return clone;
    }

    private static Vector2[] CloneVector2Array(Vector2[] source)
    {
        if (source == null || source.Length <= 0)
            return new Vector2[0];
        Vector2[] clone = new Vector2[source.Length];
        Array.Copy(source, clone, source.Length);
        return clone;
    }

    private static int[] CloneIntArray(int[] source)
    {
        if (source == null || source.Length <= 0)
            return new int[0];
        int[] clone = new int[source.Length];
        Array.Copy(source, clone, source.Length);
        return clone;
    }

    private static void WriteCatalogEntryFields(StringBuilder sb, FAInnerPieceCatalogEntry entry)
    {
        WriteString(sb, "resourceId", entry.resourceId);
        WriteString(sb, "displayName", entry.displayName);
        WriteString(sb, "fingerprint", entry.fingerprint);
        WriteString(sb, "sourceKind", entry.sourceKind);
        WriteString(sb, "sourcePath", entry.sourcePath);
        WriteBool(sb, "archived", entry.archived);
        WriteStringArray(sb, "tags", entry.tags);
        WriteBounds(sb, "bounds", entry.bounds);
        WriteInt(sb, "nodeCount", entry.nodeCount);
        WriteInt(sb, "meshCount", entry.meshCount);
        WriteString(sb, "lastImportedUtc", entry.lastImportedUtc);
    }

    private static void WriteNodeFields(StringBuilder sb, FAInnerPieceNodeData node)
    {
        WriteString(sb, "nodeId", node.nodeId);
        WriteString(sb, "parentNodeId", node.parentNodeId);
        WriteString(sb, "displayName", node.displayName);
        WriteVector3(sb, "localPosition", node.localPosition);
        WriteQuaternion(sb, "localRotation", node.localRotation);
        WriteVector3(sb, "localScale", node.localScale);
        WriteStringArray(sb, "meshRefIds", node.meshRefIds);
    }

    private static void WriteMeshFields(StringBuilder sb, FAInnerPieceMeshData mesh)
    {
        WriteString(sb, "meshId", mesh.meshId);
        WriteString(sb, "materialRefId", mesh.materialRefId);
        WriteInt(sb, "submeshIndex", mesh.submeshIndex);
        WriteVector3Array(sb, "vertices", mesh.vertices);
        WriteIntArray(sb, "triangleIndices", mesh.triangleIndices);
        WriteVector3Array(sb, "normals", mesh.normals);
        WriteVector2Array(sb, "uv0", mesh.uv0);
        WriteBounds(sb, "localBounds", mesh.localBounds);
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

    private static int ReadInt(string json, string key, int fallback)
    {
        int value;
        return TryReadInt(json, key, out value) ? value : fallback;
    }

    private static bool ReadBool(string json, string key, bool fallback)
    {
        bool value;
        return TryReadBool(json, key, out value) ? value : fallback;
    }

    private static Vector3 ReadVector3(string json, string key, Vector3 fallback)
    {
        string objectJson;
        if (!TryReadObject(json, key, out objectJson))
            return fallback;
        return new Vector3(
            ReadFloat(objectJson, "x", fallback.x),
            ReadFloat(objectJson, "y", fallback.y),
            ReadFloat(objectJson, "z", fallback.z)
        );
    }

    private static Quaternion ReadQuaternion(string json, string key, Quaternion fallback)
    {
        string objectJson;
        if (!TryReadObject(json, key, out objectJson))
            return fallback;
        return new Quaternion(
            ReadFloat(objectJson, "x", fallback.x),
            ReadFloat(objectJson, "y", fallback.y),
            ReadFloat(objectJson, "z", fallback.z),
            ReadFloat(objectJson, "w", fallback.w)
        );
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

    private static int[] ReadIntArray(string json, string key)
    {
        string arrayJson;
        if (!TryReadArray(json, key, out arrayJson))
            return new int[0];

        MatchCollection matches = Regex.Matches(arrayJson, "-?\\d+", RegexOptions.Singleline);
        int[] values = new int[matches.Count];
        for (int i = 0; i < matches.Count; i++)
            int.TryParse(matches[i].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out values[i]);
        return values;
    }

    private static Vector3[] ReadVector3Array(string json, string key)
    {
        string arrayJson;
        if (!TryReadArray(json, key, out arrayJson))
            return new Vector3[0];

        List<string> objects = ExtractObjectsFromArray(arrayJson);
        Vector3[] values = new Vector3[objects.Count];
        for (int i = 0; i < objects.Count; i++)
            values[i] = ReadVector3(objects[i], "", Vector3.zero);
        return values;
    }

    private static Vector2[] ReadVector2Array(string json, string key)
    {
        string arrayJson;
        if (!TryReadArray(json, key, out arrayJson))
            return new Vector2[0];

        List<string> objects = ExtractObjectsFromArray(arrayJson);
        Vector2[] values = new Vector2[objects.Count];
        for (int i = 0; i < objects.Count; i++)
        {
            string objectJson = objects[i];
            values[i] = new Vector2(
                ReadFloat(objectJson, "x", 0f),
                ReadFloat(objectJson, "y", 0f)
            );
        }
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

    private static bool TryReadInt(string json, string key, out int value)
    {
        value = 0;
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            return false;
        Match match = Regex.Match(json, "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*(-?\\d+)", RegexOptions.Singleline);
        if (!match.Success)
            return false;
        return int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
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

    private static bool TryReadObject(string json, string key, out string objectJson)
    {
        return TryReadDelimited(json, key, '{', '}', out objectJson);
    }

    private static bool TryReadArray(string json, string key, out string arrayJson)
    {
        return TryReadDelimited(json, key, '[', ']', out arrayJson);
    }

    private static bool TryReadDelimited(string json, string key, char openChar, char closeChar, out string value)
    {
        value = "";
        if (string.IsNullOrEmpty(json))
            return false;

        if (string.IsNullOrEmpty(key))
        {
            string trimmed = json.Trim();
            if (!string.IsNullOrEmpty(trimmed) && trimmed[0] == openChar)
            {
                value = trimmed;
                return true;
            }
            return false;
        }

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

    private static List<string> ExtractObjectsFromArray(string arrayJson)
    {
        List<string> results = new List<string>();
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

    private static StringBuilder StartObject()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append('{');
        return sb;
    }

    private static string FinishObject(StringBuilder sb)
    {
        EndObject(sb);
        return sb.ToString();
    }

    private static void WriteObjectStart(StringBuilder sb, string key)
    {
        WritePrefix(sb, key);
        sb.Append('{');
    }

    private static void WriteInlineObjectStart(StringBuilder sb)
    {
        sb.Append('{');
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

    private static void WriteInt(StringBuilder sb, string key, int value)
    {
        WritePrefix(sb, key);
        sb.Append(value.ToString(CultureInfo.InvariantCulture)).Append(',');
    }

    private static void WriteBool(StringBuilder sb, string key, bool value)
    {
        WritePrefix(sb, key);
        sb.Append(value ? "true" : "false").Append(',');
    }

    private static void WriteRawJson(StringBuilder sb, string key, string json)
    {
        WritePrefix(sb, key);
        sb.Append(string.IsNullOrEmpty(json) ? "{}" : json);
        sb.Append(',');
    }

    private static void WriteVector3(StringBuilder sb, string key, Vector3 value)
    {
        WriteObjectStart(sb, key);
        WriteFloat(sb, "x", value.x);
        WriteFloat(sb, "y", value.y);
        WriteFloat(sb, "z", value.z);
        EndObject(sb);
        sb.Append(',');
    }

    private static void WriteVector2(StringBuilder sb, Vector2 value)
    {
        WriteInlineObjectStart(sb);
        WriteFloat(sb, "x", value.x);
        WriteFloat(sb, "y", value.y);
        EndObject(sb);
    }

    private static void WriteQuaternion(StringBuilder sb, string key, Quaternion value)
    {
        WriteObjectStart(sb, key);
        WriteFloat(sb, "x", value.x);
        WriteFloat(sb, "y", value.y);
        WriteFloat(sb, "z", value.z);
        WriteFloat(sb, "w", value.w);
        EndObject(sb);
        sb.Append(',');
    }

    private static void WriteBounds(StringBuilder sb, string key, FAInnerPieceBoundsData bounds)
    {
        FAInnerPieceBoundsData value = bounds ?? new FAInnerPieceBoundsData();
        WriteObjectStart(sb, key);
        WriteVector3(sb, "center", value.center);
        WriteVector3(sb, "size", value.size);
        EndObject(sb);
        sb.Append(',');
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

    private static void WriteIntArray(StringBuilder sb, string key, int[] values)
    {
        WriteArrayStart(sb, key);
        int[] safeValues = values ?? new int[0];
        for (int i = 0; i < safeValues.Length; i++)
        {
            if (i > 0)
                sb.Append(',');
            sb.Append(safeValues[i].ToString(CultureInfo.InvariantCulture));
        }
        EndArray(sb);
    }

    private static void WriteVector3Array(StringBuilder sb, string key, Vector3[] values)
    {
        WriteArrayStart(sb, key);
        Vector3[] safeValues = values ?? new Vector3[0];
        for (int i = 0; i < safeValues.Length; i++)
        {
            if (i > 0)
                sb.Append(',');
            WriteInlineObjectStart(sb);
            WriteFloat(sb, "x", safeValues[i].x);
            WriteFloat(sb, "y", safeValues[i].y);
            WriteFloat(sb, "z", safeValues[i].z);
            EndObject(sb);
        }
        EndArray(sb);
    }

    private static void WriteVector2Array(StringBuilder sb, string key, Vector2[] values)
    {
        WriteArrayStart(sb, key);
        Vector2[] safeValues = values ?? new Vector2[0];
        for (int i = 0; i < safeValues.Length; i++)
        {
            if (i > 0)
                sb.Append(',');
            WriteVector2(sb, safeValues[i]);
        }
        EndArray(sb);
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

    private static string ReadAllTextSafe(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path))
                return "";
            return SuperController.singleton != null ? (SuperController.singleton.ReadFileIntoString(path) ?? "") : "";
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

    private static string NormalizeExternalPath(string path)
    {
        return NormalizePath(path);
    }

    public static string NormalizePath(string path)
    {
        return (path ?? string.Empty).Replace('/', '\\');
    }

    public static string CombinePath(string left, string right)
    {
        if (string.IsNullOrEmpty(left))
            return NormalizePath(right);
        if (string.IsNullOrEmpty(right))
            return NormalizePath(left);
        return NormalizePath(left).TrimEnd('\\') + "\\" + NormalizePath(right).TrimStart('\\');
    }

    public static string SanitizePathSegment(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        StringBuilder sb = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.')
                sb.Append(c);
            else if (char.IsWhiteSpace(c))
                sb.Append('_');
        }

        return sb.ToString().Trim('_', '.');
    }

    private static bool FileExistsSafe(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        return !string.IsNullOrEmpty(ReadAllTextSafe(path));
    }

    private static string GetDirectoryNameSafe(string path)
    {
        string normalized = NormalizePath(path).TrimEnd('\\');
        if (string.IsNullOrEmpty(normalized))
            return "";

        int slash = normalized.LastIndexOf('\\');
        if (slash <= 0)
            return "";
        return normalized.Substring(0, slash);
    }

    private static string GetLeafName(string path)
    {
        string normalized = NormalizePath(path).TrimEnd('\\');
        if (string.IsNullOrEmpty(normalized))
            return "";

        int slash = normalized.LastIndexOf('\\');
        if (slash < 0 || slash >= (normalized.Length - 1))
            return normalized;
        return normalized.Substring(slash + 1);
    }

    private static string GetFileNameWithoutExtension(string path)
    {
        string leaf = GetLeafName(path);
        if (string.IsNullOrEmpty(leaf))
            return "";

        int dot = leaf.LastIndexOf('.');
        if (dot <= 0)
            return leaf;
        return leaf.Substring(0, dot);
    }
}
