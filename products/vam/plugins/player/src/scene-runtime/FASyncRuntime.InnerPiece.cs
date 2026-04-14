using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

internal sealed class FAInnerPieceControlSurfacePointerRelay : MonoBehaviour
{
    public FASyncRuntime runtime;
    public string controlSurfaceInstanceId = "";
    public string colliderNodeId = "";

    private Collider cachedCollider;
    private int lastDispatchFrame = -1;
    private int lastHoverFrame = -1;

    private void Awake()
    {
        cachedCollider = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        if (cachedCollider == null)
            cachedCollider = GetComponent<Collider>();
    }

    private void OnMouseDown()
    {
        TryDispatchPointer(false);
    }

    private void OnMouseEnter()
    {
        TryDispatchHover();
    }

    private void OnMouseDrag()
    {
        TryDispatchPointer(true);
    }

    private void OnMouseOver()
    {
        TryDispatchHover();
    }

    private void OnMouseUp()
    {
        if (runtime == null || string.IsNullOrEmpty(controlSurfaceInstanceId))
            return;

        runtime.HandleInnerPieceControlSurfacePointerUp(controlSurfaceInstanceId, colliderNodeId);
    }

    private void OnMouseExit()
    {
        if (runtime == null || string.IsNullOrEmpty(controlSurfaceInstanceId))
            return;

        runtime.HandleInnerPieceControlSurfaceHoverExit(controlSurfaceInstanceId);
    }

    private void TryDispatchPointer(bool continuous)
    {
        if (runtime == null || string.IsNullOrEmpty(controlSurfaceInstanceId))
            return;

        if (lastDispatchFrame == Time.frameCount)
            return;

        if (cachedCollider == null)
            cachedCollider = GetComponent<Collider>();

        if (cachedCollider == null)
            return;

        lastDispatchFrame = Time.frameCount;
        runtime.HandleInnerPieceControlSurfacePointer(
            controlSurfaceInstanceId,
            cachedCollider,
            colliderNodeId,
            continuous);
    }

    private void TryDispatchHover()
    {
        if (runtime == null || string.IsNullOrEmpty(controlSurfaceInstanceId))
            return;

        if (lastHoverFrame == Time.frameCount)
            return;

        if (cachedCollider == null)
            cachedCollider = GetComponent<Collider>();

        if (cachedCollider == null)
            return;

        lastHoverFrame = Time.frameCount;
        runtime.HandleInnerPieceControlSurfaceHover(
            controlSurfaceInstanceId,
            cachedCollider,
            colliderNodeId);
    }
}

public partial class FASyncRuntime : MVRScript
{
    private const string InnerPieceAllowedConsumerId = "scene_runtime";
    private const string InnerPieceAllowedTargetType = "session_scene";
    private const string InnerPieceObjectKind = "innerpiece";
    private const string InnerPieceDefaultMaterialMode = "opaque";
    private const string InnerPiecePrimaryPlayerDisplayId = "player_main";
    private const string HostPackageImportActionId = "Session.InnerPiece.ImportPackage";
    private const string HostResourceSpawnActionId = "Session.InnerPiece.Spawn";
    private const string HostInstanceTransformActionId = "Session.InnerPiece.TransformInstance";
    private const string HostInstanceSetFollowActionId = "Session.InnerPiece.SetFollowBinding";
    private const string HostAnchorSpawnAtomActionId = "Session.InnerPiece.SpawnAnchorAtom";
    private const string HostAnchorSpawnMovementActionId = "Session.InnerPiece.SpawnMovementAnchor";
    private const string HostAnchorSpawnGrabPointActionId = "Session.InnerPiece.SpawnGrabPoint";
    private const string HostAnchorSpawnGripPointActionId = "Session.InnerPiece.SpawnGripPoint";
    private const float InnerPieceGrabHandleDiameterMeters = 0.08f;
    private const float InnerPieceGrabHandleFrontOffsetMeters = 0.04f;
    private const float InnerPieceGrabHandleEdgeFactor = 0.55f;

    private sealed class InnerPieceScreenSlotRuntimeRecord
    {
        public string slotId = "main";
        public string displayId = "main";
        public string surfaceTargetId = "player:screen";
        public string disconnectStateId = "";
        public string screenSurfaceNodeId = "";
        public string screenGlassNodeId = "";
        public string screenApertureNodeId = "";
        public string disconnectSurfaceNodeId = "";
        public GameObject screenSurfaceObject;
        public GameObject screenGlassObject;
        public GameObject screenApertureObject;
        public GameObject disconnectSurfaceObject;
        public GameObject mediaTargetObject;
        public bool mediaTargetUsesNormalizedRect = false;
        public Rect mediaTargetNormalizedRect = new Rect(0f, 0f, 1f, 1f);
        public bool forceOperatorFacingFrontFace = false;
        public GameObject runtimeMediaSurfaceObject;
        public Renderer runtimeMediaSurfaceRenderer;
        public bool disconnectSurfaceVisible = true;
    }

    private sealed class InnerPieceInstanceRecord
    {
        public string instanceId = "";
        public string resourceId = "";
        public string consumerId = InnerPieceAllowedConsumerId;
        public string targetType = InnerPieceAllowedTargetType;
        public string groupId = "";
        public string rootObjectId = "";
        public string screenContractVersion = "";
        public string shellId = "";
        public string deviceClass = "monitor";
        public string orientationSupport = "landscape";
        public string defaultAspectMode = "fit";
        public float safeCornerRadius = 0f;
        public string inputStyle = "fixed";
        public bool autoOrientToGround = false;
        public FAInnerPieceControlSurfaceData controlSurface;
        public string anchorAtomUid = "";
        public Atom anchorAtomRef;
        public bool followPosition = false;
        public bool followRotation = false;
        public Vector3 localPositionOffset = Vector3.zero;
        public Quaternion localRotationOffset = Quaternion.identity;
        public string lastError = "";
        public bool pendingAnchorDiscovery = false;
        public float pendingAnchorDiscoveryDeadline = 0f;
        public string pendingAnchorActionId = "";
        public Atom pendingSelectedBeforeSpawn;
        public HashSet<int> pendingBaselineAtomIds;
        public string pendingRequestedAnchorUid = "";
        public Vector3 pendingAnchorPosition = Vector3.zero;
        public Quaternion pendingAnchorRotation = Quaternion.identity;
        public float pendingAnchorScaleFactor = 1f;
        public bool pendingBindFollow = false;
        public bool pendingFollowPosition = false;
        public bool pendingFollowRotation = false;
        public Vector3 pendingLocalPositionOffset = Vector3.zero;
        public Quaternion pendingLocalRotationOffset = Quaternion.identity;
        public GameObject grabHandleObject;
        public Renderer grabHandleRenderer;
        public readonly List<string> spawnedNodeIds = new List<string>();
        public readonly List<Renderer> renderers = new List<Renderer>();
        public readonly Dictionary<string, GameObject> nodeObjects =
            new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, InnerPieceScreenSlotRuntimeRecord> screenSlots =
            new Dictionary<string, InnerPieceScreenSlotRuntimeRecord>(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ControlSurfaceDragState
    {
        public string controlSurfaceInstanceId = "";
        public string targetInstanceId = "";
        public string anchorAtomUid = "";
        public Atom anchorAtomRef;
        public Vector3 anchorStartPosition = Vector3.zero;
        public Quaternion anchorStartRotation = Quaternion.identity;
        public Vector3 dragStartHitPoint = Vector3.zero;
        public float anchorScaleFactor = 1f;
    }

    private sealed class LocalControlSurfaceElementStateRecord
    {
        public string elementId = "";
        public string actionId = "";
        public string elementKind = "";
        public string valueKind = "";
        public int activationCount = 0;
        public bool hasBoolValue = false;
        public bool boolValue = false;
        public bool hasNormalizedValue = false;
        public float normalizedValue = 0f;
        public bool hasVector2Value = false;
        public Vector2 vector2Value = Vector2.zero;
        public bool hasStringValue = false;
        public string stringValue = "";
        public bool focused = false;
        public bool hovered = false;
        public int submitCount = 0;
        public string lastSubmittedText = "";
        public string lastSubmittedAtUtc = "";
        public string lastInteractionAtUtc = "";
        public string lastInteractionSource = "";
    }

    private sealed class LocalControlSurfaceStateRecord
    {
        public string controlSurfaceInstanceId = "";
        public string controlSurfaceId = "";
        public string controlFamilyId = "";
        public string controlThemeId = "";
        public string controlThemeVariantId = "";
        public string toolkitCategory = "";
        public string lastInteractionAtUtc = "";
        public string lastElementId = "";
        public bool expanded = true;
        public string selectedElementId = "";
        public string selectedElementLabel = "";
        public int selectedElementIndex = -1;
        public string hoveredElementId = "";
        public int submitCount = 0;
        public string lastSubmittedText = "";
        public string lastSubmittedAtUtc = "";
        public string lastChoiceElementId = "";
        public string lastChoiceElementLabel = "";
        public int choiceCount = 0;
        public readonly Dictionary<string, LocalControlSurfaceElementStateRecord> elements =
            new Dictionary<string, LocalControlSurfaceElementStateRecord>(StringComparer.OrdinalIgnoreCase);
    }

    private readonly Dictionary<string, InnerPieceInstanceRecord> innerPieceInstances =
        new Dictionary<string, InnerPieceInstanceRecord>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> innerPieceRootObjectToInstance =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ControlSurfaceDragState> activeControlSurfaceDrags =
        new Dictionary<string, ControlSurfaceDragState>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Mesh> innerPieceMeshCache =
        new Dictionary<string, Mesh>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Texture2D> innerPieceTextureCache =
        new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LocalControlSurfaceStateRecord> localControlSurfaceStates =
        new Dictionary<string, LocalControlSurfaceStateRecord>(StringComparer.OrdinalIgnoreCase);

    private bool TryImportInnerPiecePackage(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        string packagePath = ExtractJsonArgString(argsJson, "packagePath", "path", "filePath");
        if (string.IsNullOrEmpty(packagePath))
        {
            errorMessage = "packagePath is required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        bool archiveExisting;
        if (!TryReadBoolArg(argsJson, out archiveExisting, "archiveExisting"))
            archiveExisting = false;

        List<string> tags = ExtractJsonStringList(argsJson, "tags");
        string tagsCsv = ExtractJsonArgString(argsJson, "tagsCsv");
        if (!string.IsNullOrEmpty(tagsCsv))
        {
            string[] split = tagsCsv.Split(',');
            for (int i = 0; i < split.Length; i++)
            {
                string tag = split[i] != null ? split[i].Trim() : "";
                if (!string.IsNullOrEmpty(tag))
                    tags.Add(tag);
            }
        }

        FAInnerPieceStoredResource resource;
        FAInnerPieceImportReceipt receipt;
        if (!FAInnerPieceStorage.TryImportPackage(
            packagePath,
            ExtractJsonArgString(argsJson, "displayNameOverride"),
            tags.ToArray(),
            archiveExisting,
            out resource,
            out receipt,
            out errorMessage
        ))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string payload = BuildInnerPieceReceiptJson(actionId, "innerpiece_imported", resource, null, receipt, "");
        resultJson = BuildBrokerResult(true, "innerpiece_imported", payload);
        EmitRuntimeEvent(
            "innerpiece_imported",
            actionId,
            "ok",
            "",
            resource != null ? resource.resourceId : "",
            resource != null ? resource.resourceId : "",
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            payload
        );
        return true;
    }

    private bool TryListInnerPieceResources(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        errorMessage = "";
        FAInnerPieceCatalogState catalog = FAInnerPieceStorage.LoadCatalog();
        FAInnerPieceCatalogState filtered = new FAInnerPieceCatalogState();
        List<FAInnerPieceCatalogEntry> entries = new List<FAInnerPieceCatalogEntry>();

        string query = ExtractJsonArgString(argsJson, "query", "resourceId", "name");
        string tag = ExtractJsonArgString(argsJson, "tag");
        bool includeArchived;
        if (!TryReadBoolArg(argsJson, out includeArchived, "includeArchived"))
            includeArchived = false;

        FAInnerPieceCatalogEntry[] sourceEntries = catalog != null && catalog.entries != null
            ? catalog.entries
            : new FAInnerPieceCatalogEntry[0];
        for (int i = 0; i < sourceEntries.Length; i++)
        {
            FAInnerPieceCatalogEntry entry = sourceEntries[i];
            if (entry == null)
                continue;
            if (entry.archived && !includeArchived)
                continue;
            if (!MatchesInnerPieceQuery(entry, query))
                continue;
            if (!MatchesInnerPieceTag(entry, tag))
                continue;
            entries.Add(entry);
        }

        filtered.entries = entries.ToArray();
        string payload = FAInnerPieceStorage.SerializeCatalog(filtered, false);
        resultJson = BuildBrokerResult(true, "innerpiece_catalog ok", payload);
        EmitRuntimeEvent(
            "innerpiece_catalog",
            actionId,
            "ok",
            "",
            "innerpiece_catalog ok",
            "",
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            payload
        );
        return true;
    }

    private bool TryGetInnerPieceResource(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        string resourceId = ExtractJsonArgString(argsJson, "resourceId");
        if (string.IsNullOrEmpty(resourceId))
        {
            errorMessage = "resourceId is required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        FAInnerPieceStoredResource resource = FAInnerPieceStorage.LoadResource(resourceId);
        if (resource == null)
        {
            errorMessage = "resource not found";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string payload = FAInnerPieceStorage.SerializeResource(resource, false);
        resultJson = BuildBrokerResult(true, "innerpiece_resource ok", payload);
        EmitRuntimeEvent(
            "innerpiece_resource",
            actionId,
            "ok",
            "",
            resource.resourceId,
            resource.resourceId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            payload
        );
        return true;
    }

    private bool TryArchiveInnerPieceResource(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        string resourceId = ExtractJsonArgString(argsJson, "resourceId");
        if (string.IsNullOrEmpty(resourceId))
        {
            errorMessage = "resourceId is required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        bool archived;
        if (!TryReadBoolArg(argsJson, out archived, "archived"))
            archived = true;

        FAInnerPieceStoredResource resource;
        if (!FAInnerPieceStorage.TrySetArchived(resourceId, archived, out resource, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string payload = BuildInnerPieceReceiptJson(
            actionId,
            archived ? "innerpiece_archived" : "innerpiece_unarchived",
            resource,
            null,
            null,
            ""
        );
        resultJson = BuildBrokerResult(true, archived ? "innerpiece_archived" : "innerpiece_unarchived", payload);
        EmitRuntimeEvent(
            archived ? "innerpiece_archived" : "innerpiece_unarchived",
            actionId,
            "ok",
            "",
            resource.resourceId,
            resource.resourceId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            payload
        );
        return true;
    }

    private bool TrySpawnInnerPiece(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        string resourceId = ExtractJsonArgString(argsJson, "resourceId");
        if (string.IsNullOrEmpty(resourceId))
        {
            errorMessage = "resourceId is required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string consumerId = ExtractJsonArgString(argsJson, "consumerId");
        if (string.IsNullOrEmpty(consumerId))
            consumerId = InnerPieceAllowedConsumerId;

        string targetType = ExtractJsonArgString(argsJson, "targetType");
        if (string.IsNullOrEmpty(targetType))
            targetType = InnerPieceAllowedTargetType;

        if (!string.Equals(consumerId, InnerPieceAllowedConsumerId, StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "unsupported consumerId";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        if (!string.Equals(targetType, InnerPieceAllowedTargetType, StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "unsupported targetType";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        FAInnerPieceStoredResource resource = FAInnerPieceStorage.LoadResource(resourceId);
        if (resource == null)
        {
            errorMessage = "resource not found";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string instanceId = ExtractJsonArgString(argsJson, "instanceId", "id");
        if (string.IsNullOrEmpty(instanceId))
            instanceId = "innerpiece_" + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
        if (innerPieceInstances.ContainsKey(instanceId))
        {
            errorMessage = "instance already exists";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string rootObjectId = instanceId + "_root";
        if (syncObjects.ContainsKey(rootObjectId))
        {
            errorMessage = "root object already exists";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string groupId = instanceId + "_group";
        InnerPieceInstanceRecord instance = new InnerPieceInstanceRecord();
        instance.instanceId = instanceId;
        instance.resourceId = resource.resourceId;
        instance.consumerId = InnerPieceAllowedConsumerId;
        instance.targetType = InnerPieceAllowedTargetType;
        instance.groupId = groupId;
        instance.rootObjectId = rootObjectId;

        EnsureRuntimeRoot();

        GameObject rootObject = new GameObject("FAInnerPiece_" + resource.displayName);
        rootObject.transform.SetParent(runtimeRoot.transform, false);

        SyncObjectRecord rootRecord = GetOrCreateObjectRecord(rootObjectId, InnerPieceObjectKind, resource.resourceId, false);
        rootRecord.kind = InnerPieceObjectKind;
        rootRecord.resourceType = resource.resourceId;
        rootRecord.gameObject = rootObject;
        rootRecord.visible = true;
        rootRecord.materialMode = InnerPieceDefaultMaterialMode;
        rootRecord.tagsCsv = string.Join(",", resource.tags ?? new string[0]);
        rootRecord.parentGroupId = groupId;
        rootRecord.scale = Vector3.one;

        string transformJson;
        if (TryExtractJsonObjectField(argsJson, "transform", out transformJson) && !string.IsNullOrEmpty(transformJson))
        {
            ApplyPositionRotationScaleFromArgs(rootRecord, transformJson, 1f);
        }
        else
        {
            Vector3 position;
            if (TryReadVectorArg(argsJson, "position", out position) || TryReadVectorArg(argsJson, "pos", out position))
                rootRecord.position = position;

            Quaternion rotation;
            if (TryReadQuaternionComponents(argsJson, out rotation))
                rootRecord.rotation = rotation;

            Vector3 scale;
            if (TryReadScaleComponents(argsJson, 1f, out scale))
                rootRecord.scale = scale;
        }
        if (rootRecord.scale == Vector3.zero)
            rootRecord.scale = Vector3.one;

        if (!TryBuildInnerPieceHierarchy(resource, rootObject, instance, out errorMessage))
        {
            Destroy(rootObject);
            rootRecord.gameObject = null;
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        BuildInnerPieceScreenRuntime(resource, instance);
        BuildInnerPieceControlSurfaceRuntime(resource, instance);

        ApplyRecordVisuals(rootRecord);
        EnsureInnerPieceGrabHandle(instance);
        SyncGroupRecord group = EnsureGroupRecord(groupId);
        group.tagsCsv = "innerpiece";
        group.updatedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        group.memberIds.Add(rootObjectId);

        innerPieceInstances[instanceId] = instance;
        innerPieceRootObjectToInstance[rootObjectId] = instanceId;

        FAInnerPieceInstanceStateData state = BuildInnerPieceInstanceState(instance, "");
        string payload = BuildInnerPieceReceiptJson(actionId, "innerpiece_spawned", resource, state, null, "");
        resultJson = BuildBrokerResult(true, "innerpiece_spawned", payload);
        EmitRuntimeEvent(
            "innerpiece_spawned",
            actionId,
            "ok",
            "",
            instance.instanceId,
            rootObjectId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            payload
        );
        return true;
    }

    private bool TryGetInnerPieceInstanceState(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        InnerPieceInstanceRecord instance;
        if (!TryResolveInnerPieceInstance(argsJson, out instance, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string payload = FAInnerPieceStorage.SerializeInstanceState(BuildInnerPieceInstanceState(instance, ""), false);
        resultJson = BuildBrokerResult(true, "innerpiece_instance_state ok", payload);
        EmitRuntimeEvent(
            "innerpiece_instance_state",
            actionId,
            "ok",
            "",
            instance.instanceId,
            instance.rootObjectId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            payload
        );
        return true;
    }

    private bool TryTransformInnerPieceInstance(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        InnerPieceInstanceRecord instance;
        if (!TryResolveInnerPieceInstance(argsJson, out instance, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        SyncObjectRecord rootRecord;
        if (!syncObjects.TryGetValue(instance.rootObjectId, out rootRecord) || rootRecord == null)
        {
            errorMessage = "instance root not found";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        if (rootRecord.tweenCoroutine != null)
            StopTween(rootRecord);

        string transformJson;
        if (TryExtractJsonObjectField(argsJson, "transform", out transformJson) && !string.IsNullOrEmpty(transformJson))
        {
            ApplyPositionRotationScaleFromArgs(rootRecord, transformJson, 1f);
        }
        else
        {
            Vector3 position;
            if (TryReadVectorArg(argsJson, "position", out position) || TryReadVectorArg(argsJson, "pos", out position))
                rootRecord.position = position;

            Quaternion rotation;
            if (TryReadQuaternionComponents(argsJson, out rotation))
                rootRecord.rotation = rotation;

            Vector3 scale;
            if (TryReadScaleComponents(argsJson, 1f, out scale))
                rootRecord.scale = scale;
        }

        Vector3 translate;
        if (TryReadVectorArg(argsJson, "translate", out translate) || TryReadVectorArg(argsJson, "positionDelta", out translate))
            rootRecord.position += translate;

        Vector3 rotateEuler;
        if (TryReadVectorArg(argsJson, "rotateEuler", out rotateEuler) || TryReadVectorArg(argsJson, "euler", out rotateEuler))
            rootRecord.rotation = Quaternion.Euler(rotateEuler) * rootRecord.rotation;

        Vector3 scaleMultiplier;
        if (TryReadVectorArg(argsJson, "scaleMultiplier", out scaleMultiplier))
            rootRecord.scale = Vector3.Scale(rootRecord.scale, scaleMultiplier);
        else
        {
            float uniformScaleMultiplier;
            if (TryExtractJsonFloatField(argsJson, "uniformScaleMultiplier", out uniformScaleMultiplier))
                rootRecord.scale = rootRecord.scale * uniformScaleMultiplier;
        }

        if (rootRecord.scale == Vector3.zero)
            rootRecord.scale = Vector3.one;

        ApplyRecordVisuals(rootRecord);
        RefreshPlayerScreenBindingsForInnerPieceInstance(instance.instanceId);
        SyncGroupRecord group;
        if (syncGroups.TryGetValue(instance.groupId, out group) && group != null)
            group.updatedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        FAInnerPieceInstanceStateData state = BuildInnerPieceInstanceState(instance, "");
        string payload = BuildInnerPieceReceiptJson(actionId, "innerpiece_instance_transformed", null, state, null, "");
        resultJson = BuildBrokerResult(true, "innerpiece_instance_transformed", payload);
        EmitRuntimeEvent(
            "innerpiece_instance_transformed",
            actionId,
            "ok",
            "",
            instance.instanceId,
            instance.rootObjectId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            payload
        );
        return true;
    }

    private bool TrySetInnerPieceFollowBinding(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        InnerPieceInstanceRecord instance;
        if (!TryResolveInnerPieceInstance(argsJson, out instance, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        SyncObjectRecord rootRecord;
        if (!syncObjects.TryGetValue(instance.rootObjectId, out rootRecord) || rootRecord == null || rootRecord.gameObject == null)
        {
            errorMessage = "instance root not found";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        bool enabled = true;
        bool clear = false;
        TryReadBoolArg(argsJson, out enabled, "enabled");
        TryReadBoolArg(argsJson, out clear, "clear");
        string anchorAtomUid = ExtractJsonArgString(argsJson, "anchorAtomUid", "atomUid", "anchorUid", "uid");
        if (clear || !enabled || string.IsNullOrEmpty(anchorAtomUid))
        {
            ClearInnerPieceFollowBinding(instance);
            FAInnerPieceInstanceStateData clearedState = BuildInnerPieceInstanceState(instance, "");
            string clearedPayload = BuildInnerPieceReceiptJson(actionId, "innerpiece_follow_binding_cleared", null, clearedState, null, "");
            resultJson = BuildBrokerResult(true, "innerpiece_follow_binding_cleared", clearedPayload);
            EmitRuntimeEvent(
                "innerpiece_follow_binding_cleared",
                actionId,
                "ok",
                "",
                instance.instanceId,
                instance.rootObjectId,
                ExtractJsonArgString(argsJson, "correlationId"),
                ExtractJsonArgString(argsJson, "messageId"),
                "",
                clearedPayload
            );
            return true;
        }

        Atom resolvedAnchorAtom;
        Transform anchorTransform;
        if (!TryResolveInnerPieceAnchorTransform(anchorAtomUid, null, out resolvedAnchorAtom, out anchorTransform, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        if (rootRecord.tweenCoroutine != null)
            StopTween(rootRecord);

        bool followPosition = true;
        bool followRotation = true;
        TryReadBoolArg(argsJson, out followPosition, "followPosition");
        TryReadBoolArg(argsJson, out followRotation, "followRotation");

        Vector3 localPositionOffset = instance.localPositionOffset;
        bool hasExplicitLocalPosition =
            TryReadVectorArg(argsJson, "localPositionOffset", out localPositionOffset)
            || TryReadVectorArg(argsJson, "localOffset", out localPositionOffset)
            || TryReadVectorArg(argsJson, "offset", out localPositionOffset);

        Vector3 localRotationEuler;
        Quaternion localRotationOffset = instance.localRotationOffset;
        bool hasExplicitLocalRotation =
            TryReadVectorArg(argsJson, "localRotationEuler", out localRotationEuler)
            || TryReadVectorArg(argsJson, "localRotateEuler", out localRotationEuler)
            || TryReadVectorArg(argsJson, "localEuler", out localRotationEuler);
        if (hasExplicitLocalRotation)
            localRotationOffset = Quaternion.Euler(localRotationEuler);

        bool snapUnderAnchor = false;
        TryReadBoolArg(argsJson, out snapUnderAnchor, "snapUnderAnchor", "placeUnderAnchor", "belowAnchor");

        float anchorClearanceMeters = 0f;
        TryExtractJsonFloatField(argsJson, "anchorClearanceMeters", out anchorClearanceMeters);
        if (anchorClearanceMeters <= 0f)
            TryExtractJsonFloatField(argsJson, "verticalOffsetMeters", out anchorClearanceMeters);
        anchorClearanceMeters = Mathf.Max(0f, anchorClearanceMeters);

        if (followPosition && !hasExplicitLocalPosition)
        {
            if (snapUnderAnchor)
            {
                localPositionOffset = BuildInnerPieceUnderAnchorOffset(instance, anchorClearanceMeters);
            }
            else
            {
                localPositionOffset = anchorTransform.InverseTransformPoint(rootRecord.position);
            }
        }

        if (followRotation && !hasExplicitLocalRotation)
            localRotationOffset = Quaternion.Inverse(anchorTransform.rotation) * rootRecord.rotation;

        instance.anchorAtomUid = anchorAtomUid;
        instance.anchorAtomRef = resolvedAnchorAtom;
        instance.followPosition = followPosition;
        instance.followRotation = followRotation;
        instance.localPositionOffset = localPositionOffset;
        instance.localRotationOffset = localRotationOffset;
        instance.lastError = "";

        ApplyInnerPieceFollowBinding(instance, rootRecord, anchorTransform);

        FAInnerPieceInstanceStateData state = BuildInnerPieceInstanceState(instance, "");
        string payload = BuildInnerPieceReceiptJson(actionId, "innerpiece_follow_binding_set", null, state, null, "");
        resultJson = BuildBrokerResult(true, "innerpiece_follow_binding_set", payload);
        EmitRuntimeEvent(
            "innerpiece_follow_binding_set",
            actionId,
            "ok",
            "",
            instance.instanceId,
            instance.rootObjectId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            anchorAtomUid,
            payload
        );
        return true;
    }

    private bool TrySpawnInnerPieceAnchorAtom(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";
        string resolvedActionId = NormalizeInnerPieceAnchorActionId(actionId);

        InnerPieceInstanceRecord instance;
        if (!TryResolveInnerPieceInstance(argsJson, out instance, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        SyncObjectRecord rootRecord;
        if (!syncObjects.TryGetValue(instance.rootObjectId, out rootRecord) || rootRecord == null || rootRecord.gameObject == null)
        {
            errorMessage = "instance root not found";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        SuperController sc = SuperController.singleton;
        if (sc == null)
        {
            errorMessage = "SuperController unavailable";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string atomType = ExtractJsonArgString(argsJson, "anchorAtomType", "atomType", "spawnAtomType");
        if (string.IsNullOrEmpty(atomType))
            atomType = ResolveDefaultInnerPieceAnchorAtomType(resolvedActionId);

        bool bindFollow = true;
        TryReadBoolArg(argsJson, out bindFollow, "bindFollow", "follow");

        bool followPosition = true;
        bool followRotation = true;
        TryReadBoolArg(argsJson, out followPosition, "followPosition");
        TryReadBoolArg(argsJson, out followRotation, "followRotation");

        Vector3 anchorPosition = rootRecord.position;
        bool hasExplicitAnchorPosition =
            TryReadVectorArg(argsJson, "anchorPosition", out anchorPosition)
            || TryReadVectorArg(argsJson, "position", out anchorPosition)
            || TryReadVectorArg(argsJson, "pos", out anchorPosition);

        Quaternion anchorRotation = rootRecord.rotation;
        Vector3 anchorRotationEuler;
        bool hasExplicitAnchorRotation =
            TryReadVectorArg(argsJson, "anchorRotationEuler", out anchorRotationEuler)
            || TryReadVectorArg(argsJson, "rotationEuler", out anchorRotationEuler)
            || TryReadVectorArg(argsJson, "rotateEuler", out anchorRotationEuler)
            || TryReadVectorArg(argsJson, "euler", out anchorRotationEuler);
        if (hasExplicitAnchorRotation)
            anchorRotation = Quaternion.Euler(anchorRotationEuler);

        float anchorScaleFactor = 1f;
        if (!TryExtractJsonFloatField(argsJson, "anchorScaleFactor", out anchorScaleFactor)
            && !TryExtractJsonFloatField(argsJson, "anchorScale", out anchorScaleFactor)
            && !TryExtractJsonFloatField(argsJson, "atomScale", out anchorScaleFactor))
        {
            anchorScaleFactor = 1f;
        }

        Vector3 localPositionOffset = instance.localPositionOffset;
        bool hasExplicitLocalPosition =
            TryReadVectorArg(argsJson, "localPositionOffset", out localPositionOffset)
            || TryReadVectorArg(argsJson, "localOffset", out localPositionOffset)
            || TryReadVectorArg(argsJson, "offset", out localPositionOffset);

        Quaternion localRotationOffset = instance.localRotationOffset;
        Vector3 localRotationEuler;
        bool hasExplicitLocalRotation =
            TryReadVectorArg(argsJson, "localRotationEuler", out localRotationEuler)
            || TryReadVectorArg(argsJson, "localRotateEuler", out localRotationEuler)
            || TryReadVectorArg(argsJson, "localEuler", out localRotationEuler);
        if (hasExplicitLocalRotation)
            localRotationOffset = Quaternion.Euler(localRotationEuler);

        bool snapUnderAnchor = true;
        TryReadBoolArg(argsJson, out snapUnderAnchor, "snapUnderAnchor", "placeUnderAnchor", "belowAnchor");

        float anchorClearanceMeters = 0f;
        TryExtractJsonFloatField(argsJson, "anchorClearanceMeters", out anchorClearanceMeters);
        if (anchorClearanceMeters <= 0f)
            TryExtractJsonFloatField(argsJson, "verticalOffsetMeters", out anchorClearanceMeters);
        anchorClearanceMeters = Mathf.Max(0f, anchorClearanceMeters);

        if (bindFollow && followPosition && !hasExplicitLocalPosition && !hasExplicitAnchorPosition && snapUnderAnchor)
        {
            localPositionOffset = BuildInnerPieceUnderAnchorOffset(instance, anchorClearanceMeters);
            anchorPosition = rootRecord.position - (anchorRotation * localPositionOffset);
        }

        string requestedAnchorUid = ExtractJsonArgString(argsJson, "anchorAtomUid", "atomUid", "anchorUid", "uid");
        if (string.IsNullOrEmpty(requestedAnchorUid))
            requestedAnchorUid = BuildDefaultInnerPieceAnchorAtomUid(instance, atomType);
        requestedAnchorUid = BuildUniqueInnerPieceAnchorAtomUid(sc, requestedAnchorUid);

        HashSet<int> baselineAtomIds = SnapshotInnerPieceAnchorAtoms(sc);
        Atom selectedBeforeSpawn = sc.GetSelectedAtom();

        string requestDiagnostics;
        if (!TryRequestInnerPieceAnchorAtom(sc, atomType, out requestDiagnostics))
        {
            errorMessage = string.IsNullOrEmpty(requestDiagnostics)
                ? "anchor atom spawn failed"
                : "anchor atom spawn failed: " + requestDiagnostics;
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        Atom spawnedAtom = TryDiscoverSpawnedInnerPieceAnchorAtom(sc, selectedBeforeSpawn, baselineAtomIds);

        if (spawnedAtom == null)
        {
            instance.pendingAnchorDiscovery = true;
            instance.pendingAnchorDiscoveryDeadline = Time.unscaledTime + 2.0f;
            instance.pendingAnchorActionId = resolvedActionId;
            instance.pendingSelectedBeforeSpawn = selectedBeforeSpawn;
            instance.pendingBaselineAtomIds = baselineAtomIds;
            instance.pendingRequestedAnchorUid = requestedAnchorUid;
            instance.pendingAnchorPosition = anchorPosition;
            instance.pendingAnchorRotation = anchorRotation;
            instance.pendingAnchorScaleFactor = anchorScaleFactor;
            instance.pendingBindFollow = bindFollow;
            instance.pendingFollowPosition = followPosition;
            instance.pendingFollowRotation = followRotation;
            instance.pendingLocalPositionOffset = localPositionOffset;
            instance.pendingLocalRotationOffset = localRotationOffset;
            instance.lastError = "";

            FAInnerPieceInstanceStateData pendingState = BuildInnerPieceInstanceState(instance, "");
            string pendingPayload = BuildInnerPieceReceiptJson(resolvedActionId, "innerpiece_anchor_atom_spawn_pending", null, pendingState, null, "");
            resultJson = BuildBrokerResult(true, "innerpiece_anchor_atom_spawn_pending", pendingPayload);
            EmitRuntimeEvent(
                "innerpiece_anchor_atom_spawn_pending",
                resolvedActionId,
                "ok",
                "",
                instance.instanceId,
                instance.rootObjectId,
                ExtractJsonArgString(argsJson, "correlationId"),
                ExtractJsonArgString(argsJson, "messageId"),
                "",
                pendingPayload
            );
            return true;
        }

        string anchorAtomUid;
        if (!TryFinalizeInnerPieceAnchorAtomBinding(
            resolvedActionId,
            instance,
            rootRecord,
            spawnedAtom,
            requestedAnchorUid,
            anchorPosition,
            anchorRotation,
            anchorScaleFactor,
            bindFollow,
            followPosition,
            followRotation,
            localPositionOffset,
            localRotationOffset,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            out resultJson,
            out errorMessage,
            out anchorAtomUid))
        {
            return false;
        }

        return true;
    }

    private bool TryDeleteInnerPieceInstance(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        InnerPieceInstanceRecord instance;
        if (!TryResolveInnerPieceInstance(argsJson, out instance, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        FAInnerPieceInstanceStateData state = BuildInnerPieceInstanceState(instance, "");
        DeleteInnerPieceInstanceInternal(instance, true);
        string payload = BuildInnerPieceReceiptJson(actionId, "innerpiece_instance_deleted", null, state, null, "");
        resultJson = BuildBrokerResult(true, "innerpiece_instance_deleted", payload);
        EmitRuntimeEvent(
            "innerpiece_instance_deleted",
            actionId,
            "ok",
            "",
            instance.instanceId,
            instance.rootObjectId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            payload
        );
        return true;
    }

    private bool TryResolveInnerPieceInstance(string argsJson, out InnerPieceInstanceRecord instance, out string errorMessage)
    {
        instance = null;
        errorMessage = "";

        string instanceId = ExtractJsonArgString(argsJson, "instanceId", "id");
        if (!string.IsNullOrEmpty(instanceId))
        {
            if (innerPieceInstances.TryGetValue(instanceId, out instance) && instance != null)
                return true;
            errorMessage = "instance not found";
            return false;
        }

        string rootObjectId = ExtractJsonArgString(argsJson, "rootObjectId", "objectId");
        if (!string.IsNullOrEmpty(rootObjectId))
        {
            string mappedInstanceId;
            if (innerPieceRootObjectToInstance.TryGetValue(rootObjectId, out mappedInstanceId)
                && innerPieceInstances.TryGetValue(mappedInstanceId, out instance)
                && instance != null)
            {
                return true;
            }
        }

        errorMessage = "instanceId is required";
        return false;
    }

    private bool TryBuildInnerPieceHierarchy(
        FAInnerPieceStoredResource resource,
        GameObject rootObject,
        InnerPieceInstanceRecord instance,
        out string errorMessage
    )
    {
        errorMessage = "";
        if (resource == null || rootObject == null || instance == null)
        {
            errorMessage = "innerpiece hierarchy input missing";
            return false;
        }

        Dictionary<string, GameObject> nodeObjects = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        FAInnerPieceNodeData[] nodes = resource.nodes ?? new FAInnerPieceNodeData[0];
        for (int i = 0; i < nodes.Length; i++)
        {
            FAInnerPieceNodeData node = nodes[i];
            GameObject nodeObject = new GameObject("Node_" + node.nodeId);
            nodeObject.transform.localPosition = node.localPosition;
            nodeObject.transform.localRotation = node.localRotation;
            nodeObject.transform.localScale = node.localScale == Vector3.zero ? Vector3.one : node.localScale;
            nodeObjects[node.nodeId] = nodeObject;
            instance.nodeObjects[node.nodeId] = nodeObject;
            instance.spawnedNodeIds.Add(instance.instanceId + "::" + node.nodeId);
        }

        for (int i = 0; i < nodes.Length; i++)
        {
            FAInnerPieceNodeData node = nodes[i];
            GameObject nodeObject = nodeObjects[node.nodeId];
            GameObject parentObject = rootObject;
            if (!string.IsNullOrEmpty(node.parentNodeId))
            {
                GameObject candidateParent;
                if (nodeObjects.TryGetValue(node.parentNodeId, out candidateParent) && candidateParent != null)
                    parentObject = candidateParent;
            }
            nodeObject.transform.SetParent(parentObject.transform, false);
        }

        Dictionary<string, FAInnerPieceMeshData> meshLookup = new Dictionary<string, FAInnerPieceMeshData>(StringComparer.OrdinalIgnoreCase);
        FAInnerPieceMeshData[] meshes = resource.meshes ?? new FAInnerPieceMeshData[0];
        for (int i = 0; i < meshes.Length; i++)
            meshLookup[meshes[i].meshId] = meshes[i];
        Dictionary<string, FAInnerPieceMaterialEntry> materialLookup = BuildInnerPieceMaterialLookup(resource != null ? resource.resourceId : "");

        for (int i = 0; i < nodes.Length; i++)
        {
            FAInnerPieceNodeData node = nodes[i];
            GameObject nodeObject = nodeObjects[node.nodeId];
            string[] meshRefs = node.meshRefIds ?? new string[0];
            for (int j = 0; j < meshRefs.Length; j++)
            {
                FAInnerPieceMeshData meshData;
                if (!meshLookup.TryGetValue(meshRefs[j], out meshData) || meshData == null)
                    continue;

                Mesh sharedMesh;
                if (!TryGetOrCreateInnerPieceMesh(resource.resourceId, meshData, out sharedMesh, out errorMessage))
                    return false;

                GameObject meshObject = new GameObject("Mesh_" + meshData.meshId);
                meshObject.transform.SetParent(nodeObject.transform, false);
                MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
                MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();
                meshFilter.sharedMesh = sharedMesh;
                meshRenderer.material = CreateInnerPieceMaterial(
                    resource != null ? resource.resourceId : "",
                    LookupInnerPieceMaterial(materialLookup, meshData.materialRefId));
                instance.renderers.Add(meshRenderer);
            }
        }

        return true;
    }

    private void BuildInnerPieceScreenRuntime(
        FAInnerPieceStoredResource resource,
        InnerPieceInstanceRecord instance)
    {
        if (resource == null || instance == null)
            return;

        instance.screenContractVersion = "";
        instance.shellId = "";
        instance.deviceClass = "monitor";
        instance.orientationSupport = "landscape";
        instance.defaultAspectMode = "fit";
        instance.safeCornerRadius = 0f;
        instance.inputStyle = "fixed";
        instance.autoOrientToGround = false;
        instance.screenSlots.Clear();

        FAInnerPieceScreenContractData screenContract = resource.screenContract;
        if (screenContract == null)
            return;

        instance.screenContractVersion = string.IsNullOrEmpty(screenContract.schemaVersion)
            ? FAInnerPieceSchemas.ScreenContractV1
            : screenContract.schemaVersion;
        instance.shellId = screenContract.shellId ?? "";
        instance.deviceClass = string.IsNullOrEmpty(screenContract.deviceClass) ? "monitor" : screenContract.deviceClass;
        instance.orientationSupport = string.IsNullOrEmpty(screenContract.orientationSupport) ? "landscape" : screenContract.orientationSupport;
        instance.defaultAspectMode = string.IsNullOrEmpty(screenContract.defaultAspectMode) ? "fit" : screenContract.defaultAspectMode;
        instance.safeCornerRadius = Mathf.Max(0f, screenContract.safeCornerRadius);
        instance.inputStyle = string.IsNullOrEmpty(screenContract.inputStyle) ? "fixed" : screenContract.inputStyle;
        instance.autoOrientToGround = screenContract.autoOrientToGround;

        FAInnerPieceScreenSlotData[] slots = screenContract.slots ?? new FAInnerPieceScreenSlotData[0];
        for (int i = 0; i < slots.Length; i++)
        {
            FAInnerPieceScreenSlotData slot = slots[i];
            if (slot == null || string.IsNullOrEmpty(slot.slotId))
                continue;

            InnerPieceScreenSlotRuntimeRecord runtimeSlot = new InnerPieceScreenSlotRuntimeRecord();
            runtimeSlot.slotId = slot.slotId;
            runtimeSlot.displayId = ResolveDefaultInnerPieceDisplayId(
                slot.slotId,
                slot.displayId,
                string.IsNullOrEmpty(slot.surfaceTargetId)
                    ? screenContract.surfaceTargetId
                    : slot.surfaceTargetId
            );
            runtimeSlot.surfaceTargetId = string.IsNullOrEmpty(slot.surfaceTargetId)
                ? (string.IsNullOrEmpty(screenContract.surfaceTargetId) ? "player:screen" : screenContract.surfaceTargetId)
                : slot.surfaceTargetId;
            runtimeSlot.disconnectStateId = string.IsNullOrEmpty(slot.disconnectStateId)
                ? (screenContract.defaultDisconnectStateId ?? "")
                : slot.disconnectStateId;
            runtimeSlot.screenSurfaceNodeId = slot.screenSurfaceNodeId ?? "";
            runtimeSlot.screenGlassNodeId = slot.screenGlassNodeId ?? "";
            runtimeSlot.screenApertureNodeId = slot.screenApertureNodeId ?? "";
            runtimeSlot.disconnectSurfaceNodeId = slot.disconnectSurfaceNodeId ?? "";
            runtimeSlot.screenSurfaceObject = ResolveInnerPieceNodeObject(instance, runtimeSlot.screenSurfaceNodeId);
            runtimeSlot.screenGlassObject = ResolveInnerPieceNodeObject(instance, runtimeSlot.screenGlassNodeId);
            runtimeSlot.screenApertureObject = ResolveInnerPieceNodeObject(instance, runtimeSlot.screenApertureNodeId);
            runtimeSlot.disconnectSurfaceObject = ResolveInnerPieceNodeObject(instance, runtimeSlot.disconnectSurfaceNodeId);
            // The old rect Ghost screen needed a forced front-face correction when playback
            // actually targeted the disconnect slab. The direct-CUA screen-core lane now
            // binds to the authored screen surface, so inheriting that shell-wide flag makes
            // the runtime quad readable only by mirroring it.
            runtimeSlot.forceOperatorFacingFrontFace =
                string.Equals(instance.shellId ?? "", "ghost_prototype_screen_rect", StringComparison.OrdinalIgnoreCase)
                && ShouldUseDisconnectSurfaceAsMediaTarget(runtimeSlot);
            runtimeSlot.disconnectSurfaceVisible =
                !IsAuthoredScreenSurfacePresentationTarget(instance, runtimeSlot, runtimeSlot.screenSurfaceObject);
            instance.screenSlots[runtimeSlot.slotId] = runtimeSlot;
            SetInnerPieceNodeRenderersVisible(runtimeSlot.disconnectSurfaceObject, runtimeSlot.disconnectSurfaceVisible);
        }
    }

    private void BuildInnerPieceControlSurfaceRuntime(
        FAInnerPieceStoredResource resource,
        InnerPieceInstanceRecord instance)
    {
        if (instance == null)
            return;

        instance.controlSurface = CloneInnerPieceControlSurface(resource != null ? resource.controlSurface : null);
        EnsureLocalControlSurfaceState(instance, instance.controlSurface);
        AttachInnerPieceControlSurfacePointerRuntime(instance);
    }

    private void AttachInnerPieceControlSurfacePointerRuntime(InnerPieceInstanceRecord instance)
    {
        if (instance == null || instance.controlSurface == null)
            return;

        string surfaceNodeId = instance.controlSurface.surfaceNodeId ?? "";
        string colliderNodeId = string.IsNullOrEmpty(instance.controlSurface.colliderNodeId)
            ? surfaceNodeId
            : instance.controlSurface.colliderNodeId;

        if (string.IsNullOrEmpty(surfaceNodeId))
            return;

        GameObject colliderRoot = ResolveInnerPieceNodeObject(instance, colliderNodeId);
        if (colliderRoot == null)
            colliderRoot = ResolveInnerPieceNodeObject(instance, surfaceNodeId);
        if (colliderRoot == null)
            return;

        MeshFilter[] meshFilters = colliderRoot.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter meshFilter = meshFilters[i];
            if (meshFilter == null || meshFilter.sharedMesh == null)
                continue;

            GameObject meshObject = meshFilter.gameObject;
            MeshCollider meshCollider = meshObject.GetComponent<MeshCollider>();
            if (meshCollider == null)
                meshCollider = meshObject.AddComponent<MeshCollider>();

            meshCollider.sharedMesh = meshFilter.sharedMesh;
            meshCollider.convex = false;
            meshCollider.isTrigger = false;

            FAInnerPieceControlSurfacePointerRelay relay =
                meshObject.GetComponent<FAInnerPieceControlSurfacePointerRelay>();
            if (relay == null)
                relay = meshObject.AddComponent<FAInnerPieceControlSurfacePointerRelay>();

            relay.runtime = this;
            relay.controlSurfaceInstanceId = instance.instanceId ?? "";
            relay.colliderNodeId = colliderNodeId;
        }
    }

    internal void HandleInnerPieceControlSurfacePointer(
        string controlSurfaceInstanceId,
        Collider targetCollider,
        string colliderNodeId,
        bool continuous)
    {
        if (string.IsNullOrEmpty(controlSurfaceInstanceId) || targetCollider == null)
            return;

        InnerPieceInstanceRecord instance;
        FAInnerPieceControlSurfaceData controlSurface;
        string errorMessage;
        if (!TryResolveInnerPieceControlSurfaceInstance(
                "{\"controlSurfaceInstanceId\":\"" + EscapeJsonString(controlSurfaceInstanceId) + "\"}",
                out instance,
                out controlSurface,
                out errorMessage)
            || controlSurface == null)
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
            return;

        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (!targetCollider.Raycast(ray, out hit, 100f))
            return;

        Vector2 uv = hit.textureCoord;

        FAInnerPieceControlElementData element;
        float normalizedValue;
        bool includeNormalizedValue;
        if (!TryResolveControlSurfaceElementByPointer(
                instance,
                controlSurface,
                colliderNodeId,
                uv,
                continuous,
                out element,
                out normalizedValue,
                out includeNormalizedValue))
        {
            UpdateControlSurfaceHoverState(instance, controlSurface, null, "pointer_hover");
            TryHandleControlSurfaceBackgroundDrag(controlSurfaceInstanceId, hit.point, continuous);
            return;
        }

        UpdateControlSurfaceHoverState(instance, controlSurface, element, "pointer_hover");

        if (TryHandleControlSurfaceDragElement(controlSurfaceInstanceId, element, hit.point, continuous))
            return;

        System.Text.StringBuilder args = new System.Text.StringBuilder(160);
        args.Append('{');
        args.Append("\"controlSurfaceInstanceId\":\"").Append(EscapeJsonString(controlSurfaceInstanceId)).Append("\",");
        args.Append("\"elementId\":\"").Append(EscapeJsonString(element.elementId ?? "")).Append("\",");
        args.Append("\"interactionSource\":\"pointer\"");
        if (includeNormalizedValue)
            args.Append(",\"normalized\":").Append(FormatFloat(normalizedValue));
        if (TryComputeControlSurfaceElementPointerVector(element, uv, out Vector2 pointerVector))
        {
            args.Append(",\"normalizedX\":").Append(FormatFloat(pointerVector.x));
            args.Append(",\"normalizedY\":").Append(FormatFloat(pointerVector.y));
        }
        args.Append('}');

        string ignoredResultJson;
        TryTriggerToolkitControlSurfaceElement(
            "Runtime.ControlSurface.Pointer",
            args.ToString(),
            out ignoredResultJson,
            out errorMessage);
    }

    internal void HandleInnerPieceControlSurfaceHover(
        string controlSurfaceInstanceId,
        Collider targetCollider,
        string colliderNodeId)
    {
        if (string.IsNullOrEmpty(controlSurfaceInstanceId) || targetCollider == null)
            return;

        InnerPieceInstanceRecord instance;
        FAInnerPieceControlSurfaceData controlSurface;
        string errorMessage;
        if (!TryResolveInnerPieceControlSurfaceInstance(
                "{\"controlSurfaceInstanceId\":\"" + EscapeJsonString(controlSurfaceInstanceId) + "\"}",
                out instance,
                out controlSurface,
                out errorMessage)
            || controlSurface == null)
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
            return;

        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (!targetCollider.Raycast(ray, out hit, 100f))
        {
            UpdateControlSurfaceHoverState(instance, controlSurface, null, "pointer_hover");
            return;
        }

        Vector2 uv = hit.textureCoord;
        if (TryResolveControlSurfaceElementByPointer(
                instance,
                controlSurface,
                colliderNodeId,
                uv,
                false,
                out FAInnerPieceControlElementData element,
                out float _,
                out bool _))
        {
            UpdateControlSurfaceHoverState(instance, controlSurface, element, "pointer_hover");
            return;
        }

        UpdateControlSurfaceHoverState(instance, controlSurface, null, "pointer_hover");
    }

    internal void HandleInnerPieceControlSurfaceHoverExit(string controlSurfaceInstanceId)
    {
        if (string.IsNullOrEmpty(controlSurfaceInstanceId))
            return;

        if (!innerPieceInstances.TryGetValue(controlSurfaceInstanceId, out InnerPieceInstanceRecord instance)
            || instance == null
            || instance.controlSurface == null)
        {
            return;
        }

        UpdateControlSurfaceHoverState(instance, instance.controlSurface, null, "pointer_hover");
    }

    internal void HandleInnerPieceControlSurfacePointerUp(string controlSurfaceInstanceId, string colliderNodeId)
    {
        if (string.IsNullOrEmpty(controlSurfaceInstanceId))
            return;

        activeControlSurfaceDrags.Remove(controlSurfaceInstanceId);
    }

    private void UpdateControlSurfaceHoverState(
        InnerPieceInstanceRecord instance,
        FAInnerPieceControlSurfaceData controlSurface,
        FAInnerPieceControlElementData hoveredElement,
        string interactionSource)
    {
        if (instance == null || controlSurface == null)
            return;

        LocalControlSurfaceStateRecord surfaceState = EnsureLocalControlSurfaceState(instance, controlSurface);
        if (surfaceState == null)
            return;

        string nowUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        string hoveredElementId = hoveredElement != null ? (hoveredElement.elementId ?? "") : "";
        surfaceState.hoveredElementId = hoveredElementId;

        FAInnerPieceControlElementData[] elements = controlSurface.elements ?? new FAInnerPieceControlElementData[0];
        for (int i = 0; i < elements.Length; i++)
        {
            FAInnerPieceControlElementData candidate = elements[i];
            if (candidate == null)
                continue;

            LocalControlSurfaceElementStateRecord elementState = EnsureLocalControlSurfaceElementState(surfaceState, candidate);
            if (elementState == null)
                continue;

            bool isHovered =
                !string.IsNullOrEmpty(hoveredElementId)
                && string.Equals(candidate.elementId ?? "", hoveredElementId, StringComparison.OrdinalIgnoreCase);
            elementState.hovered = isHovered;
            if (isHovered)
            {
                elementState.lastInteractionAtUtc = nowUtc;
                elementState.lastInteractionSource = interactionSource ?? "";
            }
        }
    }

    private bool TryHandleControlSurfaceBackgroundDrag(
        string controlSurfaceInstanceId,
        Vector3 hitPoint,
        bool continuous)
    {
        if (ShouldDisableHostedMetaProofPanelDrag(controlSurfaceInstanceId))
            return false;

        if (continuous)
            return TryUpdateControlSurfaceDrag(controlSurfaceInstanceId, hitPoint);

        return TryBeginControlSurfaceDrag(controlSurfaceInstanceId, hitPoint);
    }

    private bool TryHandleControlSurfaceDragElement(
        string controlSurfaceInstanceId,
        FAInnerPieceControlElementData element,
        Vector3 hitPoint,
        bool continuous)
    {
        if (ShouldDisableHostedMetaProofPanelDrag(controlSurfaceInstanceId))
            return false;

        if (!IsDragControlSurfaceElement(element))
            return false;

        if (continuous)
            return TryUpdateControlSurfaceDrag(controlSurfaceInstanceId, hitPoint);

        return TryBeginControlSurfaceDrag(controlSurfaceInstanceId, hitPoint);
    }

    private bool TryResolveControlSurfaceElementByPointer(
        InnerPieceInstanceRecord instance,
        FAInnerPieceControlSurfaceData controlSurface,
        string colliderNodeId,
        Vector2 uv,
        bool continuous,
        out FAInnerPieceControlElementData element,
        out float normalizedValue,
        out bool includeNormalizedValue)
    {
        element = null;
        normalizedValue = 0f;
        includeNormalizedValue = false;

        if (controlSurface == null)
            return false;

        LocalControlSurfaceStateRecord surfaceState = EnsureLocalControlSurfaceState(instance, controlSurface);
        FAInnerPieceControlElementData[] elements = controlSurface.elements ?? new FAInnerPieceControlElementData[0];
        float bestArea = float.MaxValue;
        int bestPriority = int.MinValue;

        for (int i = 0; i < elements.Length; i++)
        {
            FAInnerPieceControlElementData candidate = elements[i];
            if (candidate == null || candidate.readOnly)
                continue;
            if (IsSelectorToolkitSurface(controlSurface)
                && surfaceState != null
                && !surfaceState.expanded
                && IsSelectorOptionElement(controlSurface, candidate))
            {
                continue;
            }

            FAInnerPieceNormalizedRectData rect = candidate.normalizedRect ?? new FAInnerPieceNormalizedRectData();
            float xMin = rect.x;
            float yMin = rect.y;
            float xMax = rect.x + rect.width;
            float yMax = rect.y + rect.height;
            if (rect.width <= 0.0001f || rect.height <= 0.0001f)
                continue;

            if (uv.x < xMin || uv.x > xMax || uv.y < yMin || uv.y > yMax)
                continue;

            bool isContinuous = IsContinuousControlSurfaceElement(candidate);
            if (continuous && !isContinuous)
                continue;

            int priority = 0;
            if (!string.IsNullOrEmpty(colliderNodeId))
            {
                if (string.Equals(candidate.colliderNodeId, colliderNodeId, StringComparison.OrdinalIgnoreCase))
                    priority += 4;
                else if (string.Equals(candidate.nodeId, colliderNodeId, StringComparison.OrdinalIgnoreCase))
                    priority += 2;
            }

            float area = rect.width * rect.height;
            if (priority < bestPriority)
                continue;
            if (priority == bestPriority && area >= bestArea)
                continue;

            bestPriority = priority;
            bestArea = area;
            element = candidate;
        }

        if (element == null)
            return false;

        if (IsContinuousControlSurfaceElement(element))
        {
            FAInnerPieceNormalizedRectData rect = element.normalizedRect ?? new FAInnerPieceNormalizedRectData();
            bool vertical = rect.height > rect.width;
            normalizedValue = vertical
                ? Mathf.InverseLerp(rect.y, rect.y + Mathf.Max(0.0001f, rect.height), uv.y)
                : Mathf.InverseLerp(rect.x, rect.x + Mathf.Max(0.0001f, rect.width), uv.x);
            normalizedValue = Mathf.Clamp01(normalizedValue);
            includeNormalizedValue = true;
        }

        return true;
    }

    private bool IsContinuousControlSurfaceElement(FAInnerPieceControlElementData element)
    {
        if (element == null)
            return false;

        if (string.Equals(element.elementKind, "slider", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(element.valueKind, "normalized", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(element.valueKind, "float", StringComparison.OrdinalIgnoreCase))
            return true;

        string actionId = NormalizeControlSurfaceActionId(element.actionId ?? "");
        return string.Equals(actionId, "scrub_normalized", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionId, "volume", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionId, "volume_normalized", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsDragControlSurfaceElement(FAInnerPieceControlElementData element)
    {
        if (element == null)
            return false;

        string elementKind = element.elementKind ?? "";
        if (string.Equals(elementKind, "drag", StringComparison.OrdinalIgnoreCase)
            || string.Equals(elementKind, "drag_handle", StringComparison.OrdinalIgnoreCase))
            return true;

        string actionId = element.actionId ?? "";
        return string.Equals(actionId, "drag_move", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionId, "panel_drag", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionId, "move_surface", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryBeginControlSurfaceDrag(string controlSurfaceInstanceId, Vector3 hitPoint)
    {
        if (string.IsNullOrEmpty(controlSurfaceInstanceId))
            return false;

        if (activeControlSurfaceDrags.ContainsKey(controlSurfaceInstanceId))
            return true;

        if (!TryCreateControlSurfaceDragState(controlSurfaceInstanceId, hitPoint, out ControlSurfaceDragState dragState))
            return false;

        activeControlSurfaceDrags[controlSurfaceInstanceId] = dragState;
        return true;
    }

    private bool TryUpdateControlSurfaceDrag(string controlSurfaceInstanceId, Vector3 hitPoint)
    {
        if (string.IsNullOrEmpty(controlSurfaceInstanceId))
            return false;

        if (!activeControlSurfaceDrags.TryGetValue(controlSurfaceInstanceId, out ControlSurfaceDragState dragState)
            || dragState == null)
            return false;

        Atom resolvedAnchorAtom;
        Transform anchorTransform;
        string ignoredError;
        if (!TryResolveInnerPieceAnchorTransform(
                dragState.anchorAtomUid,
                dragState.anchorAtomRef,
                out resolvedAnchorAtom,
                out anchorTransform,
                out ignoredError)
            || anchorTransform == null)
        {
            activeControlSurfaceDrags.Remove(controlSurfaceInstanceId);
            return false;
        }

        dragState.anchorAtomRef = resolvedAnchorAtom;
        Vector3 delta = hitPoint - dragState.dragStartHitPoint;
        ApplyInnerPieceAnchorAtomTransform(
            resolvedAnchorAtom,
            dragState.anchorStartPosition + delta,
            dragState.anchorStartRotation,
            dragState.anchorScaleFactor);
        return true;
    }

    private void ClearActiveControlSurfaceDragsForInstance(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId) || activeControlSurfaceDrags.Count == 0)
            return;

        List<string> dragKeysToRemove = null;
        foreach (KeyValuePair<string, ControlSurfaceDragState> kvp in activeControlSurfaceDrags)
        {
            ControlSurfaceDragState dragState = kvp.Value;
            if (dragState == null)
                continue;

            if (!string.Equals(dragState.controlSurfaceInstanceId, instanceId, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(dragState.targetInstanceId, instanceId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (dragKeysToRemove == null)
                dragKeysToRemove = new List<string>();
            dragKeysToRemove.Add(kvp.Key);
        }

        if (dragKeysToRemove == null)
            return;

        for (int i = 0; i < dragKeysToRemove.Count; i++)
            activeControlSurfaceDrags.Remove(dragKeysToRemove[i]);
    }

    private bool TryCreateControlSurfaceDragState(
        string controlSurfaceInstanceId,
        Vector3 hitPoint,
        out ControlSurfaceDragState dragState)
    {
        dragState = null;

        if (!playerControlSurfaceBindings.TryGetValue(controlSurfaceInstanceId, out PlayerControlSurfaceBindingRecord binding)
            || binding == null
            || string.IsNullOrEmpty(binding.targetInstanceId))
        {
            return false;
        }

        if (!innerPieceInstances.TryGetValue(binding.targetInstanceId, out InnerPieceInstanceRecord targetInstance)
            || targetInstance == null)
        {
            return false;
        }

        string anchorAtomUid = targetInstance.anchorAtomUid ?? "";
        if (string.IsNullOrEmpty(anchorAtomUid))
            return false;

        Atom resolvedAnchorAtom;
        Transform anchorTransform;
        string ignoredError;
        if (!TryResolveInnerPieceAnchorTransform(
                anchorAtomUid,
                targetInstance.anchorAtomRef,
                out resolvedAnchorAtom,
                out anchorTransform,
                out ignoredError)
            || anchorTransform == null)
        {
            return false;
        }

        targetInstance.anchorAtomRef = resolvedAnchorAtom;
        dragState = new ControlSurfaceDragState();
        dragState.controlSurfaceInstanceId = controlSurfaceInstanceId;
        dragState.targetInstanceId = binding.targetInstanceId;
        dragState.anchorAtomUid = anchorAtomUid;
        dragState.anchorAtomRef = resolvedAnchorAtom;
        dragState.anchorStartPosition = anchorTransform.position;
        dragState.anchorStartRotation = anchorTransform.rotation;
        dragState.dragStartHitPoint = hitPoint;
        dragState.anchorScaleFactor = Mathf.Clamp(targetInstance.pendingAnchorScaleFactor > 0f ? targetInstance.pendingAnchorScaleFactor : 1f, 0.25f, 3.0f);
        return true;
    }

    private bool TryComputeControlSurfaceElementPointerVector(
        FAInnerPieceControlElementData element,
        Vector2 uv,
        out Vector2 pointerVector)
    {
        pointerVector = Vector2.zero;
        if (element == null)
            return false;

        FAInnerPieceNormalizedRectData rect = element.normalizedRect ?? new FAInnerPieceNormalizedRectData();
        if (rect.width <= 0.0001f || rect.height <= 0.0001f)
            return false;

        pointerVector = new Vector2(
            Mathf.Clamp01(Mathf.InverseLerp(rect.x, rect.x + Mathf.Max(0.0001f, rect.width), uv.x)),
            Mathf.Clamp01(Mathf.InverseLerp(rect.y, rect.y + Mathf.Max(0.0001f, rect.height), uv.y)));
        return true;
    }

    private LocalControlSurfaceStateRecord EnsureLocalControlSurfaceState(
        InnerPieceInstanceRecord instance,
        FAInnerPieceControlSurfaceData controlSurface)
    {
        if (instance == null || controlSurface == null || string.IsNullOrEmpty(instance.instanceId))
            return null;

        LocalControlSurfaceStateRecord state;
        if (!localControlSurfaceStates.TryGetValue(instance.instanceId, out state) || state == null)
        {
            state = new LocalControlSurfaceStateRecord();
            state.controlSurfaceInstanceId = instance.instanceId ?? "";
            localControlSurfaceStates[instance.instanceId] = state;
        }

        state.controlSurfaceId = controlSurface.controlSurfaceId ?? "";
        state.controlFamilyId = controlSurface.controlFamilyId ?? "";
        state.controlThemeId = controlSurface.controlThemeId ?? "";
        state.controlThemeVariantId = controlSurface.controlThemeVariantId ?? "";
        state.toolkitCategory = controlSurface.toolkitCategory ?? "";

        FAInnerPieceControlElementData[] elements = controlSurface.elements ?? new FAInnerPieceControlElementData[0];
        for (int i = 0; i < elements.Length; i++)
        {
            FAInnerPieceControlElementData element = elements[i];
            if (element == null || string.IsNullOrEmpty(element.elementId))
                continue;

            EnsureLocalControlSurfaceElementState(state, element);
        }

        return state;
    }

    private LocalControlSurfaceElementStateRecord EnsureLocalControlSurfaceElementState(
        LocalControlSurfaceStateRecord surfaceState,
        FAInnerPieceControlElementData element)
    {
        if (surfaceState == null || element == null || string.IsNullOrEmpty(element.elementId))
            return null;

        LocalControlSurfaceElementStateRecord state;
        if (!surfaceState.elements.TryGetValue(element.elementId, out state) || state == null)
        {
            state = new LocalControlSurfaceElementStateRecord();
            state.elementId = element.elementId ?? "";
            surfaceState.elements[element.elementId] = state;
        }

        state.actionId = element.actionId ?? "";
        state.elementKind = element.elementKind ?? "";
        state.valueKind = element.valueKind ?? "";
        return state;
    }

    private void ResetLocalControlSurfaceElementValueState(LocalControlSurfaceElementStateRecord elementState)
    {
        if (elementState == null)
            return;

        elementState.hasBoolValue = false;
        elementState.boolValue = false;
        elementState.hasNormalizedValue = false;
        elementState.normalizedValue = 0f;
        elementState.hasVector2Value = false;
        elementState.vector2Value = Vector2.zero;
        elementState.hasStringValue = false;
        elementState.stringValue = "";
    }

    private void RecordBoundPlayerControlSurfaceInteraction(
        InnerPieceInstanceRecord instance,
        FAInnerPieceControlSurfaceData controlSurface,
        FAInnerPieceControlElementData element,
        string argsJson)
    {
        if (instance == null || controlSurface == null || element == null)
            return;

        LocalControlSurfaceStateRecord surfaceState = EnsureLocalControlSurfaceState(instance, controlSurface);
        LocalControlSurfaceElementStateRecord elementState = EnsureLocalControlSurfaceElementState(surfaceState, element);
        if (surfaceState == null || elementState == null)
            return;

        string nowUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        surfaceState.lastInteractionAtUtc = nowUtc;
        surfaceState.lastElementId = element.elementId ?? "";
        elementState.lastInteractionAtUtc = nowUtc;
        elementState.lastInteractionSource = ExtractJsonArgString(argsJson, "interactionSource");
        if (string.IsNullOrEmpty(elementState.lastInteractionSource))
            elementState.lastInteractionSource = "player";
        elementState.activationCount++;

        SyncBoundPlayerControlSurfaceState(instance, controlSurface, surfaceState);
    }

    private void SyncBoundPlayerControlSurfaceState(
        InnerPieceInstanceRecord instance,
        FAInnerPieceControlSurfaceData controlSurface,
        LocalControlSurfaceStateRecord surfaceState)
    {
        if (instance == null || controlSurface == null || surfaceState == null)
            return;

        if (!playerControlSurfaceBindings.TryGetValue(instance.instanceId ?? "", out PlayerControlSurfaceBindingRecord binding)
            || binding == null
            || !IsStandalonePlayerTargetKind(binding.targetKind))
        {
            return;
        }

        StandalonePlayerRecord record;
        string ignoredError;
        if (!TryResolveBoundStandalonePlayerRecord(binding, out record, out ignoredError) || record == null)
            return;

        bool isPlaying = record.desiredPlaying;
        double currentTimeSeconds = 0d;
        double currentDurationSeconds = 0d;
        try
        {
            if (record.videoPlayer != null)
            {
                isPlaying = record.videoPlayer.isPlaying;
                currentTimeSeconds = Math.Max(0d, record.videoPlayer.time);
                currentDurationSeconds = GetStandalonePlayerDurationSeconds(record);
            }
        }
        catch
        {
            isPlaying = record.desiredPlaying;
            currentTimeSeconds = 0d;
            currentDurationSeconds = 0d;
        }

        double currentTimeNormalized = 0d;
        if (!double.IsNaN(currentTimeSeconds)
            && !double.IsInfinity(currentTimeSeconds)
            && !double.IsNaN(currentDurationSeconds)
            && !double.IsInfinity(currentDurationSeconds)
            && currentDurationSeconds > 0.0001d)
        {
            currentTimeNormalized = Math.Max(0d, Math.Min(1d, currentTimeSeconds / currentDurationSeconds));
        }

        float volumeNormalized = Mathf.Clamp01(record.muted ? record.storedVolume : record.volume);
        bool loopEnabled = !string.Equals(record.loopMode, PlayerLoopModeNone, StringComparison.OrdinalIgnoreCase);
        bool fitAspectEnabled = ShouldUseFitBlackAspectMode(record.aspectMode);
        int currentPlaylistIndex = record.currentIndex;
        if (currentPlaylistIndex < 0 || currentPlaylistIndex >= record.playlistPaths.Count)
        {
            currentPlaylistIndex = FindStandalonePlayerPlaylistIndex(record.playlistPaths, GetStandalonePlayerCurrentPlaylistPath(record));
            if (currentPlaylistIndex < 0 && record.playlistPaths.Count > 0)
                currentPlaylistIndex = 0;
        }

        FAInnerPieceControlElementData[] elements = controlSurface.elements ?? new FAInnerPieceControlElementData[0];
        for (int i = 0; i < elements.Length; i++)
        {
            FAInnerPieceControlElementData element = elements[i];
            if (element == null || string.IsNullOrEmpty(element.elementId))
                continue;

            LocalControlSurfaceElementStateRecord elementState = EnsureLocalControlSurfaceElementState(surfaceState, element);
            if (elementState == null)
                continue;

            ResetLocalControlSurfaceElementValueState(elementState);

            string elementKind = (element.elementKind ?? "").Trim();
            string valueKind = (element.valueKind ?? "").Trim();
            if (string.Equals(elementKind, "toggle", StringComparison.OrdinalIgnoreCase)
                || string.Equals(elementKind, "button", StringComparison.OrdinalIgnoreCase)
                || string.Equals(valueKind, "bool", StringComparison.OrdinalIgnoreCase))
            {
                elementState.hasBoolValue = true;
                elementState.boolValue = false;
            }

            string actionId = ResolveStandalonePlayerControlSurfaceActionId(binding, element);
            if (string.Equals(actionId, "play_pause", StringComparison.OrdinalIgnoreCase))
            {
                elementState.hasBoolValue = true;
                elementState.boolValue = isPlaying;
                continue;
            }

            if (string.Equals(actionId, "scrub_normalized", StringComparison.OrdinalIgnoreCase))
            {
                elementState.hasNormalizedValue = true;
                elementState.normalizedValue = Mathf.Clamp01((float)currentTimeNormalized);
                continue;
            }

            if (string.Equals(actionId, "volume_normalized", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionId, "volume", StringComparison.OrdinalIgnoreCase))
            {
                elementState.hasNormalizedValue = true;
                elementState.normalizedValue = volumeNormalized;
                continue;
            }

            if (string.Equals(actionId, "mute_toggle", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionId, "toggle_mute", StringComparison.OrdinalIgnoreCase))
            {
                elementState.hasBoolValue = true;
                elementState.boolValue = record.muted;
                continue;
            }

            if (string.Equals(actionId, "random_toggle", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionId, "toggle_random", StringComparison.OrdinalIgnoreCase))
            {
                elementState.hasBoolValue = true;
                elementState.boolValue = record.randomEnabled;
                continue;
            }

            if (string.Equals(actionId, "loop_toggle", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionId, "toggle_loop", StringComparison.OrdinalIgnoreCase))
            {
                elementState.hasBoolValue = true;
                elementState.boolValue = loopEnabled;
                continue;
            }

            if (string.Equals(actionId, "aspect_cycle", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionId, "toggle_aspect", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionId, "aspect_mode", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionId, "set_aspect_mode", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionId, "select_aspect_mode", StringComparison.OrdinalIgnoreCase))
            {
                elementState.hasBoolValue = true;
                elementState.boolValue = fitAspectEnabled;
                continue;
            }

            if (TryResolveMetaVideoPlayerPlaylistElementIndex(element, out int playlistIndex))
            {
                elementState.hasBoolValue = true;
                elementState.boolValue =
                    playlistIndex >= 0
                    && playlistIndex < record.playlistPaths.Count
                    && playlistIndex == currentPlaylistIndex;
            }
        }
    }

    private bool IsSearchToolkitSurface(FAInnerPieceControlSurfaceData controlSurface)
    {
        if (controlSurface == null)
            return false;

        if (string.Equals(controlSurface.controlFamilyId ?? "", "meta_ui_search_bar", StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(controlSurface.toolkitCategory ?? "", "textinputfield", StringComparison.OrdinalIgnoreCase))
            return true;

        FAInnerPieceControlElementData[] elements = controlSurface.elements ?? new FAInnerPieceControlElementData[0];
        for (int i = 0; i < elements.Length; i++)
        {
            if (IsTextInputControlSurfaceElement(elements[i]))
                return true;
        }

        return false;
    }

    private bool IsSelectorToolkitSurface(FAInnerPieceControlSurfaceData controlSurface)
    {
        if (controlSurface == null)
            return false;

        string category = controlSurface.toolkitCategory ?? "";
        return string.Equals(category, "dropdown", StringComparison.OrdinalIgnoreCase)
            || string.Equals(category, "contextmenu", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsDialogToolkitSurface(FAInnerPieceControlSurfaceData controlSurface)
    {
        return controlSurface != null
            && string.Equals(controlSurface.toolkitCategory ?? "", "dialog", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsTextInputControlSurfaceElement(FAInnerPieceControlElementData element)
    {
        if (element == null)
            return false;

        return string.Equals(element.elementKind ?? "", "text_input", StringComparison.OrdinalIgnoreCase)
            || string.Equals(element.valueKind ?? "", "string", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsSelectorHeaderElement(FAInnerPieceControlElementData element)
    {
        if (element == null)
            return false;

        string elementId = element.elementId ?? "";
        string actionId = element.actionId ?? "";
        return elementId.IndexOf("header", StringComparison.OrdinalIgnoreCase) >= 0
            || actionId.IndexOf("header", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool IsSelectorOptionElement(FAInnerPieceControlSurfaceData controlSurface, FAInnerPieceControlElementData element)
    {
        if (!IsSelectorToolkitSurface(controlSurface) || element == null)
            return false;

        return !IsSelectorHeaderElement(element);
    }

    private int GetSelectorOptionIndex(FAInnerPieceControlSurfaceData controlSurface, FAInnerPieceControlElementData targetElement)
    {
        if (!IsSelectorToolkitSurface(controlSurface) || targetElement == null)
            return -1;

        FAInnerPieceControlElementData[] elements = controlSurface.elements ?? new FAInnerPieceControlElementData[0];
        int optionIndex = 0;
        for (int i = 0; i < elements.Length; i++)
        {
            FAInnerPieceControlElementData candidate = elements[i];
            if (!IsSelectorOptionElement(controlSurface, candidate))
                continue;

            if (ReferenceEquals(candidate, targetElement)
                || string.Equals(candidate.elementId, targetElement.elementId, StringComparison.OrdinalIgnoreCase))
            {
                return optionIndex;
            }

            optionIndex++;
        }

        return -1;
    }

    private int CountSelectorOptions(FAInnerPieceControlSurfaceData controlSurface)
    {
        if (!IsSelectorToolkitSurface(controlSurface))
            return 0;

        int count = 0;
        FAInnerPieceControlElementData[] elements = controlSurface.elements ?? new FAInnerPieceControlElementData[0];
        for (int i = 0; i < elements.Length; i++)
        {
            if (IsSelectorOptionElement(controlSurface, elements[i]))
                count++;
        }

        return count;
    }

    private FAInnerPieceControlElementData FindSelectorHeaderElement(FAInnerPieceControlSurfaceData controlSurface)
    {
        if (!IsSelectorToolkitSurface(controlSurface))
            return null;

        FAInnerPieceControlElementData[] elements = controlSurface.elements ?? new FAInnerPieceControlElementData[0];
        for (int i = 0; i < elements.Length; i++)
        {
            if (IsSelectorHeaderElement(elements[i]))
                return elements[i];
        }

        return null;
    }

    private void SyncSelectorHeaderState(
        FAInnerPieceControlSurfaceData controlSurface,
        LocalControlSurfaceStateRecord surfaceState)
    {
        if (!IsSelectorToolkitSurface(controlSurface) || surfaceState == null)
            return;

        FAInnerPieceControlElementData headerElement = FindSelectorHeaderElement(controlSurface);
        LocalControlSurfaceElementStateRecord headerState = EnsureLocalControlSurfaceElementState(surfaceState, headerElement);
        if (headerState == null)
            return;

        headerState.hasBoolValue = true;
        headerState.boolValue = surfaceState.expanded;
    }

    private void ApplySelectorSelectionState(
        FAInnerPieceControlSurfaceData controlSurface,
        LocalControlSurfaceStateRecord surfaceState,
        FAInnerPieceControlElementData selectedElement)
    {
        if (!IsSelectorToolkitSurface(controlSurface) || surfaceState == null)
            return;

        FAInnerPieceControlElementData[] elements = controlSurface.elements ?? new FAInnerPieceControlElementData[0];
        for (int i = 0; i < elements.Length; i++)
        {
            FAInnerPieceControlElementData candidate = elements[i];
            if (!IsSelectorOptionElement(controlSurface, candidate))
                continue;

            LocalControlSurfaceElementStateRecord optionState = EnsureLocalControlSurfaceElementState(surfaceState, candidate);
            if (optionState == null)
                continue;

            bool isSelected = selectedElement != null
                && string.Equals(candidate.elementId, selectedElement.elementId, StringComparison.OrdinalIgnoreCase);
            optionState.hasBoolValue = true;
            optionState.boolValue = isSelected;
        }

        if (selectedElement == null)
        {
            surfaceState.selectedElementId = "";
            surfaceState.selectedElementLabel = "";
            surfaceState.selectedElementIndex = -1;
        }
        else
        {
            surfaceState.selectedElementId = selectedElement.elementId ?? "";
            surfaceState.selectedElementLabel = selectedElement.elementLabel ?? "";
            surfaceState.selectedElementIndex = GetSelectorOptionIndex(controlSurface, selectedElement);
        }
    }

    private void CommitTextInputInteraction(
        LocalControlSurfaceStateRecord surfaceState,
        LocalControlSurfaceElementStateRecord elementState,
        string text,
        string nowUtc)
    {
        if (surfaceState == null || elementState == null)
            return;

        string committedText = text ?? "";
        surfaceState.submitCount++;
        surfaceState.lastSubmittedText = committedText;
        surfaceState.lastSubmittedAtUtc = nowUtc ?? "";

        elementState.submitCount++;
        elementState.lastSubmittedText = committedText;
        elementState.lastSubmittedAtUtc = nowUtc ?? "";
    }

    private bool TryApplyToolkitSelectorInteraction(
        string argsJson,
        FAInnerPieceControlSurfaceData controlSurface,
        FAInnerPieceControlElementData element,
        LocalControlSurfaceStateRecord surfaceState,
        LocalControlSurfaceElementStateRecord elementState,
        out string operation)
    {
        operation = "";
        if (!IsSelectorToolkitSurface(controlSurface) || element == null || surfaceState == null || elementState == null)
            return false;

        if (IsSelectorHeaderElement(element))
        {
            bool expanded;
            if (!TryReadBoolArg(argsJson, out expanded, "expanded", "open", "selected", "checked", "value", "boolValue"))
                expanded = !surfaceState.expanded;

            surfaceState.expanded = expanded;
            elementState.hasBoolValue = true;
            elementState.boolValue = expanded;
            SyncSelectorHeaderState(controlSurface, surfaceState);
            operation = expanded ? "local_selector_expand" : "local_selector_collapse";
            return true;
        }

        if (!IsSelectorOptionElement(controlSurface, element))
            return false;

        bool selected;
        bool hasSelected = TryReadBoolArg(argsJson, out selected, "selected", "checked", "enabled", "value", "boolValue");
        if (!hasSelected)
            selected = true;

        ApplySelectorSelectionState(controlSurface, surfaceState, selected ? element : null);
        operation = selected ? "local_selector_select" : "local_selector_clear";
        return true;
    }

    private bool TryApplyToolkitTextInputInteraction(
        string argsJson,
        FAInnerPieceControlElementData element,
        LocalControlSurfaceStateRecord surfaceState,
        LocalControlSurfaceElementStateRecord elementState,
        string nowUtc,
        out string operation)
    {
        operation = "";
        if (!IsTextInputControlSurfaceElement(element) || surfaceState == null || elementState == null)
            return false;

        bool clearRequested = false;
        TryReadBoolArg(argsJson, out clearRequested, "clear", "reset");

        bool submitRequested = false;
        TryReadBoolArg(argsJson, out submitRequested, "submit", "commit", "enter");

        bool hasText =
            TryExtractJsonStringField(argsJson, "text", out string textValue)
            || TryExtractJsonStringField(argsJson, "value", out textValue)
            || TryExtractJsonStringField(argsJson, "stringValue", out textValue);

        if (clearRequested)
        {
            elementState.hasStringValue = true;
            elementState.stringValue = "";
            if (submitRequested)
            {
                CommitTextInputInteraction(surfaceState, elementState, "", nowUtc);
                elementState.focused = false;
                operation = "local_text_submit";
            }
            else
            {
                operation = "local_text_clear";
            }

            return true;
        }

        if (hasText)
        {
            elementState.hasStringValue = true;
            elementState.stringValue = textValue ?? "";
            elementState.focused = true;
            if (submitRequested)
            {
                CommitTextInputInteraction(surfaceState, elementState, elementState.stringValue, nowUtc);
                elementState.focused = false;
                operation = "local_text_submit";
            }
            else
            {
                operation = "local_text_set";
            }

            return true;
        }

        if (submitRequested)
        {
            string committedText = elementState.hasStringValue ? (elementState.stringValue ?? "") : "";
            CommitTextInputInteraction(surfaceState, elementState, committedText, nowUtc);
            elementState.focused = false;
            operation = "local_text_submit";
            return true;
        }

        bool focused;
        if (TryReadBoolArg(argsJson, out focused, "focused", "focus", "isFocused"))
        {
            elementState.focused = focused;
            operation = focused ? "local_text_focus" : "local_text_blur";
            return true;
        }

        elementState.focused = !elementState.focused;
        operation = elementState.focused ? "local_text_focus" : "local_text_blur";
        return true;
    }

    private bool TryApplyToolkitDialogInteraction(
        FAInnerPieceControlSurfaceData controlSurface,
        FAInnerPieceControlElementData element,
        LocalControlSurfaceStateRecord surfaceState,
        LocalControlSurfaceElementStateRecord elementState,
        out string operation)
    {
        operation = "";
        if (!IsDialogToolkitSurface(controlSurface) || element == null || surfaceState == null || elementState == null)
            return false;

        FAInnerPieceControlElementData[] elements = controlSurface.elements ?? new FAInnerPieceControlElementData[0];
        for (int i = 0; i < elements.Length; i++)
        {
            FAInnerPieceControlElementData candidate = elements[i];
            if (candidate == null)
                continue;

            LocalControlSurfaceElementStateRecord candidateState = EnsureLocalControlSurfaceElementState(surfaceState, candidate);
            if (candidateState == null)
                continue;

            bool isChosen = string.Equals(candidate.elementId, element.elementId, StringComparison.OrdinalIgnoreCase);
            candidateState.hasBoolValue = true;
            candidateState.boolValue = isChosen;
        }

        surfaceState.lastChoiceElementId = element.elementId ?? "";
        surfaceState.lastChoiceElementLabel = element.elementLabel ?? "";
        surfaceState.choiceCount++;
        operation = "local_dialog_choice";
        return true;
    }

    private string BuildMetaToolkitFamilyStateJson(
        FAInnerPieceControlSurfaceData controlSurface,
        LocalControlSurfaceStateRecord surfaceState)
    {
        if (controlSurface == null || surfaceState == null)
            return "";

        if (IsSearchToolkitSurface(controlSurface))
        {
            LocalControlSurfaceElementStateRecord textState = null;
            FAInnerPieceControlElementData[] elements = controlSurface.elements ?? new FAInnerPieceControlElementData[0];
            for (int i = 0; i < elements.Length; i++)
            {
                FAInnerPieceControlElementData element = elements[i];
                if (!IsTextInputControlSurfaceElement(element) || string.IsNullOrEmpty(element.elementId))
                    continue;

                surfaceState.elements.TryGetValue(element.elementId, out textState);
                break;
            }

            string currentText = textState != null && textState.hasStringValue ? (textState.stringValue ?? "") : "";
            bool focused = textState != null && textState.focused;
            bool keyboardVisible = false;
            string keyboardPlacementMode = "";
            string keyboardPreferredHand = "";
            string focusedFieldId = "";
            bool hasKeyboardState = TryGetHarpKeyboardBridgeState(
                out keyboardVisible,
                out keyboardPlacementMode,
                out keyboardPreferredHand,
                out focusedFieldId);
            return "{"
                + "\"family\":\"search_input\""
                + ",\"text\":\"" + EscapeJsonString(currentText) + "\""
                + ",\"focused\":" + (focused ? "true" : "false")
                + ",\"keyboardBridge\":" + (HasHarpTextInputBridge() ? "true" : "false")
                + ",\"keyboardVisible\":" + (hasKeyboardState && keyboardVisible ? "true" : "false")
                + ",\"keyboardPlacementMode\":\"" + EscapeJsonString(keyboardPlacementMode) + "\""
                + ",\"keyboardPreferredHand\":\"" + EscapeJsonString(keyboardPreferredHand) + "\""
                + ",\"focusedFieldId\":\"" + EscapeJsonString(focusedFieldId) + "\""
                + ",\"submitCount\":" + surfaceState.submitCount.ToString(CultureInfo.InvariantCulture)
                + ",\"lastSubmittedText\":\"" + EscapeJsonString(surfaceState.lastSubmittedText ?? "") + "\""
                + ",\"lastSubmittedAtUtc\":\"" + EscapeJsonString(surfaceState.lastSubmittedAtUtc ?? "") + "\""
                + "}";
        }

        if (IsSelectorToolkitSurface(controlSurface))
        {
            FAInnerPieceControlElementData headerElement = FindSelectorHeaderElement(controlSurface);
            return "{"
                + "\"family\":\"selector\""
                + ",\"expanded\":" + (surfaceState.expanded ? "true" : "false")
                + ",\"headerElementId\":\"" + EscapeJsonString(headerElement != null ? (headerElement.elementId ?? "") : "") + "\""
                + ",\"selectedElementId\":\"" + EscapeJsonString(surfaceState.selectedElementId ?? "") + "\""
                + ",\"selectedElementLabel\":\"" + EscapeJsonString(surfaceState.selectedElementLabel ?? "") + "\""
                + ",\"selectedIndex\":" + surfaceState.selectedElementIndex.ToString(CultureInfo.InvariantCulture)
                + ",\"optionCount\":" + CountSelectorOptions(controlSurface).ToString(CultureInfo.InvariantCulture)
                + "}";
        }

        if (IsDialogToolkitSurface(controlSurface))
        {
            return "{"
                + "\"family\":\"dialog\""
                + ",\"lastChoiceElementId\":\"" + EscapeJsonString(surfaceState.lastChoiceElementId ?? "") + "\""
                + ",\"lastChoiceElementLabel\":\"" + EscapeJsonString(surfaceState.lastChoiceElementLabel ?? "") + "\""
                + ",\"choiceCount\":" + surfaceState.choiceCount.ToString(CultureInfo.InvariantCulture)
                + "}";
        }

        return "";
    }

    private string BuildMetaToolkitControlSurfaceStateJson(string instanceId)
    {
        InnerPieceInstanceRecord instance;
        FAInnerPieceControlSurfaceData controlSurface;
        string errorMessage;
        if (!TryResolveInnerPieceControlSurfaceInstance(
                "{\"controlSurfaceInstanceId\":\"" + EscapeJsonString(instanceId ?? "") + "\"}",
                out instance,
                out controlSurface,
                out errorMessage))
        {
            return "{}";
        }

        return BuildMetaToolkitControlSurfaceStateJson(instance, controlSurface);
    }

    private string BuildMetaToolkitControlSurfaceStateJson(
        InnerPieceInstanceRecord instance,
        FAInnerPieceControlSurfaceData controlSurface)
    {
        if (instance == null || controlSurface == null)
            return "{}";

        LocalControlSurfaceStateRecord surfaceState = EnsureLocalControlSurfaceState(instance, controlSurface);
        SyncBoundPlayerControlSurfaceState(instance, controlSurface, surfaceState);
        bool isSearchSurface = IsSearchToolkitSurface(controlSurface);
        bool isSelectorSurface = IsSelectorToolkitSurface(controlSurface);
        bool isDialogSurface = IsDialogToolkitSurface(controlSurface);
        bool hasHarpKeyboardBridge = isSearchSurface && HasHarpTextInputBridge();
        StringBuilder sb = new StringBuilder(1024);
        sb.Append('{');
        sb.Append("\"schemaVersion\":\"").Append(EscapeJsonString(MetaToolkitControlSurfaceStateSchemaVersion)).Append("\",");
        sb.Append("\"controlSurfaceInstanceId\":\"").Append(EscapeJsonString(instance.instanceId ?? "")).Append("\",");
        sb.Append("\"controlSurfaceId\":\"").Append(EscapeJsonString(controlSurface.controlSurfaceId ?? "")).Append("\",");
        sb.Append("\"controlFamilyId\":\"").Append(EscapeJsonString(controlSurface.controlFamilyId ?? "")).Append("\",");
        sb.Append("\"controlThemeId\":\"").Append(EscapeJsonString(controlSurface.controlThemeId ?? "")).Append("\",");
        sb.Append("\"controlThemeVariantId\":\"").Append(EscapeJsonString(controlSurface.controlThemeVariantId ?? "")).Append("\",");
        sb.Append("\"toolkitCategory\":\"").Append(EscapeJsonString(controlSurface.toolkitCategory ?? "")).Append("\",");
        sb.Append("\"lastInteractionAtUtc\":\"").Append(EscapeJsonString(surfaceState != null ? (surfaceState.lastInteractionAtUtc ?? "") : "")).Append("\",");
        sb.Append("\"lastElementId\":\"").Append(EscapeJsonString(surfaceState != null ? (surfaceState.lastElementId ?? "") : "")).Append("\",");
        sb.Append("\"hoveredElementId\":\"").Append(EscapeJsonString(surfaceState != null ? (surfaceState.hoveredElementId ?? "") : "")).Append("\",");
        sb.Append("\"capabilities\":{");
        sb.Append("\"transform\":true,");
        sb.Append("\"tween\":true,");
        sb.Append("\"cancelTween\":true,");
        sb.Append("\"state\":true,");
        sb.Append("\"observeMotion\":true,");
        sb.Append("\"stableOperationId\":true,");
        sb.Append("\"follow\":true,");
        sb.Append("\"textSubmit\":").Append(isSearchSurface ? "true" : "false").Append(',');
        sb.Append("\"keyboardBridge\":").Append(hasHarpKeyboardBridge ? "true" : "false").Append(',');
        sb.Append("\"selector\":").Append(isSelectorSurface ? "true" : "false").Append(',');
        sb.Append("\"selectionByIndex\":").Append(isSelectorSurface ? "true" : "false").Append(',');
        sb.Append("\"dialogChoice\":").Append(isDialogSurface ? "true" : "false").Append(',');
        sb.Append("\"playerBinding\":").Append(
            IsPlayerBoundControlFamily(controlSurface.controlFamilyId)
                ? "true"
                : "false");
        sb.Append("},");
        sb.Append("\"rootObjectId\":\"").Append(EscapeJsonString(instance.rootObjectId ?? "")).Append("\",");

        SyncObjectRecord rootRecord = null;
        if (!string.IsNullOrEmpty(instance.rootObjectId))
            syncObjects.TryGetValue(instance.rootObjectId, out rootRecord);

        sb.Append("\"rootTransform\":{");
        sb.Append("\"posX\":").Append(FormatFloat(rootRecord != null ? rootRecord.position.x : 0f)).Append(',');
        sb.Append("\"posY\":").Append(FormatFloat(rootRecord != null ? rootRecord.position.y : 0f)).Append(',');
        sb.Append("\"posZ\":").Append(FormatFloat(rootRecord != null ? rootRecord.position.z : 0f)).Append(',');
        sb.Append("\"rotX\":").Append(FormatFloat(rootRecord != null ? rootRecord.rotation.x : 0f)).Append(',');
        sb.Append("\"rotY\":").Append(FormatFloat(rootRecord != null ? rootRecord.rotation.y : 0f)).Append(',');
        sb.Append("\"rotZ\":").Append(FormatFloat(rootRecord != null ? rootRecord.rotation.z : 0f)).Append(',');
        sb.Append("\"rotW\":").Append(FormatFloat(rootRecord != null ? rootRecord.rotation.w : 1f)).Append(',');
        sb.Append("\"scaleX\":").Append(FormatFloat(rootRecord != null ? rootRecord.scale.x : 1f)).Append(',');
        sb.Append("\"scaleY\":").Append(FormatFloat(rootRecord != null ? rootRecord.scale.y : 1f)).Append(',');
        sb.Append("\"scaleZ\":").Append(FormatFloat(rootRecord != null ? rootRecord.scale.z : 1f));
        sb.Append("},");

        sb.Append("\"followBinding\":{");
        sb.Append("\"anchorAtomUid\":\"").Append(EscapeJsonString(instance.anchorAtomUid ?? "")).Append("\",");
        sb.Append("\"followPosition\":").Append(instance.followPosition ? "true" : "false").Append(',');
        sb.Append("\"followRotation\":").Append(instance.followRotation ? "true" : "false").Append(',');
        sb.Append("\"offsetX\":").Append(FormatFloat(instance.localPositionOffset.x)).Append(',');
        sb.Append("\"offsetY\":").Append(FormatFloat(instance.localPositionOffset.y)).Append(',');
        sb.Append("\"offsetZ\":").Append(FormatFloat(instance.localPositionOffset.z)).Append(',');
        sb.Append("\"rotOffsetX\":").Append(FormatFloat(instance.localRotationOffset.x)).Append(',');
        sb.Append("\"rotOffsetY\":").Append(FormatFloat(instance.localRotationOffset.y)).Append(',');
        sb.Append("\"rotOffsetZ\":").Append(FormatFloat(instance.localRotationOffset.z));
        sb.Append("},");

        sb.Append("\"motion\":").Append(BuildSyncObjectMotionJson(rootRecord)).Append(',');
        string familyStateJson = BuildMetaToolkitFamilyStateJson(controlSurface, surfaceState);
        if (!string.IsNullOrEmpty(familyStateJson))
            sb.Append("\"familyState\":").Append(familyStateJson).Append(',');
        sb.Append("\"elements\":[");

        FAInnerPieceControlElementData[] elements = controlSurface.elements ?? new FAInnerPieceControlElementData[0];
        for (int i = 0; i < elements.Length; i++)
        {
            FAInnerPieceControlElementData element = elements[i];
            if (element == null)
                continue;

            if (i > 0)
                sb.Append(',');

            LocalControlSurfaceElementStateRecord elementState = null;
            if (surfaceState != null && !string.IsNullOrEmpty(element.elementId))
                surfaceState.elements.TryGetValue(element.elementId, out elementState);

            sb.Append('{');
            sb.Append("\"elementId\":\"").Append(EscapeJsonString(element.elementId ?? "")).Append("\",");
            sb.Append("\"elementLabel\":\"").Append(EscapeJsonString(element.elementLabel ?? "")).Append("\",");
            sb.Append("\"actionId\":\"").Append(EscapeJsonString(element.actionId ?? "")).Append("\",");
            sb.Append("\"elementKind\":\"").Append(EscapeJsonString(element.elementKind ?? "")).Append("\",");
            sb.Append("\"valueKind\":\"").Append(EscapeJsonString(element.valueKind ?? "")).Append("\",");
            sb.Append("\"readOnly\":").Append(element.readOnly ? "true" : "false").Append(',');
            sb.Append("\"activationCount\":").Append(elementState != null ? elementState.activationCount.ToString(CultureInfo.InvariantCulture) : "0").Append(',');
            sb.Append("\"focused\":").Append(elementState != null && elementState.focused ? "true" : "false").Append(',');
            sb.Append("\"hovered\":").Append(elementState != null && elementState.hovered ? "true" : "false").Append(',');
            sb.Append("\"lastInteractionAtUtc\":\"").Append(EscapeJsonString(elementState != null ? (elementState.lastInteractionAtUtc ?? "") : "")).Append("\",");
            sb.Append("\"lastInteractionSource\":\"").Append(EscapeJsonString(elementState != null ? (elementState.lastInteractionSource ?? "") : "")).Append("\"");
            if (isSelectorSurface)
            {
                string selectorRole = IsSelectorHeaderElement(element) ? "header" : (IsSelectorOptionElement(controlSurface, element) ? "option" : "");
                if (!string.IsNullOrEmpty(selectorRole))
                    sb.Append(",\"selectorRole\":\"").Append(EscapeJsonString(selectorRole)).Append('"');

                int selectorIndex = GetSelectorOptionIndex(controlSurface, element);
                if (selectorIndex >= 0)
                    sb.Append(",\"selectorIndex\":").Append(selectorIndex.ToString(CultureInfo.InvariantCulture));
            }
            if (elementState != null && elementState.hasBoolValue)
                sb.Append(",\"boolValue\":").Append(elementState.boolValue ? "true" : "false");
            if (elementState != null && elementState.hasNormalizedValue)
                sb.Append(",\"normalized\":").Append(FormatFloat(elementState.normalizedValue));
            if (elementState != null && elementState.hasVector2Value)
            {
                sb.Append(",\"normalizedVector2\":{");
                sb.Append("\"x\":").Append(FormatFloat(elementState.vector2Value.x)).Append(',');
                sb.Append("\"y\":").Append(FormatFloat(elementState.vector2Value.y));
                sb.Append('}');
            }
            if (elementState != null && elementState.hasStringValue)
                sb.Append(",\"text\":\"").Append(EscapeJsonString(elementState.stringValue ?? "")).Append('"');
            if (elementState != null && elementState.submitCount > 0)
            {
                sb.Append(",\"submitCount\":").Append(elementState.submitCount.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"lastSubmittedText\":\"").Append(EscapeJsonString(elementState.lastSubmittedText ?? "")).Append('"');
                sb.Append(",\"lastSubmittedAtUtc\":\"").Append(EscapeJsonString(elementState.lastSubmittedAtUtc ?? "")).Append('"');
            }
            sb.Append('}');
        }

        sb.Append("]}");
        return sb.ToString();
    }

    private bool TryTriggerLocalControlSurfaceElement(
        string actionId,
        string argsJson,
        InnerPieceInstanceRecord instance,
        FAInnerPieceControlSurfaceData controlSurface,
        FAInnerPieceControlElementData element,
        out string resultJson,
        out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        if (instance == null || controlSurface == null || element == null)
        {
            errorMessage = "local control surface trigger target missing";
            return false;
        }

        LocalControlSurfaceStateRecord surfaceState = EnsureLocalControlSurfaceState(instance, controlSurface);
        LocalControlSurfaceElementStateRecord elementState = EnsureLocalControlSurfaceElementState(surfaceState, element);
        if (surfaceState == null || elementState == null)
        {
            errorMessage = "local control surface state missing";
            return false;
        }

        string operation;
        if (!TryApplyLocalControlSurfaceElementInteraction(argsJson, controlSurface, element, surfaceState, elementState, out operation, out errorMessage))
            return false;

        if (IsTextInputControlSurfaceElement(element))
            NotifyHarpTextInputFocusChanged(instance.instanceId, element.elementId, operation);

        string payload = "{"
            + "\"summary\":\"" + EscapeJsonString(string.IsNullOrEmpty(operation) ? "meta_toolkit_control_surface_element_triggered" : operation) + "\""
            + ",\"controlSurfaceState\":" + BuildMetaToolkitControlSurfaceStateJson(instance, controlSurface)
            + "}";

        resultJson = BuildBrokerResult(true, "meta_toolkit_control_surface_element_triggered", payload);
        EmitRuntimeEvent(
            "meta_toolkit_control_surface_element_triggered",
            actionId,
            "ok",
            "",
            "meta_toolkit_control_surface_element_triggered",
            instance.instanceId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            instance.instanceId,
            payload
        );
        return true;
    }

    private bool TryApplyLocalControlSurfaceElementInteraction(
        string argsJson,
        FAInnerPieceControlSurfaceData controlSurface,
        FAInnerPieceControlElementData element,
        LocalControlSurfaceStateRecord surfaceState,
        LocalControlSurfaceElementStateRecord elementState,
        out string operation,
        out string errorMessage)
    {
        operation = "";
        errorMessage = "";

        if (element == null || surfaceState == null || elementState == null)
        {
            errorMessage = "local control surface element missing";
            return false;
        }

        string nowUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        surfaceState.lastInteractionAtUtc = nowUtc;
        surfaceState.lastElementId = element.elementId ?? "";
        elementState.lastInteractionAtUtc = nowUtc;
        elementState.lastInteractionSource = ExtractJsonArgString(argsJson, "interactionSource");
        if (string.IsNullOrEmpty(elementState.lastInteractionSource))
            elementState.lastInteractionSource = "session";
        elementState.activationCount++;

        string elementKind = string.IsNullOrEmpty(element.elementKind) ? "button" : element.elementKind.Trim();
        string valueKind = string.IsNullOrEmpty(element.valueKind) ? "none" : element.valueKind.Trim();

        if (TryApplyToolkitSelectorInteraction(argsJson, controlSurface, element, surfaceState, elementState, out operation))
            return true;

        if (TryApplyToolkitTextInputInteraction(argsJson, element, surfaceState, elementState, nowUtc, out operation))
            return true;

        if (TryApplyToolkitDialogInteraction(controlSurface, element, surfaceState, elementState, out operation))
            return true;

        if (string.Equals(elementKind, "slider", StringComparison.OrdinalIgnoreCase)
            || string.Equals(valueKind, "normalized", StringComparison.OrdinalIgnoreCase)
            || string.Equals(valueKind, "normalized_float", StringComparison.OrdinalIgnoreCase)
            || string.Equals(valueKind, "float", StringComparison.OrdinalIgnoreCase))
        {
            float normalized;
            if (!TryExtractJsonFloatField(argsJson, "normalized", out normalized)
                && !TryExtractJsonFloatField(argsJson, "value", out normalized))
            {
                normalized = elementState.hasNormalizedValue ? elementState.normalizedValue : 0.5f;
            }

            elementState.hasNormalizedValue = true;
            elementState.normalizedValue = Mathf.Clamp01(normalized);
            operation = "local_slider_set";
            return true;
        }

        if (string.Equals(elementKind, "scroll_view", StringComparison.OrdinalIgnoreCase)
            || string.Equals(valueKind, "normalized_vector2", StringComparison.OrdinalIgnoreCase))
        {
            float x;
            float y;
            bool hasX =
                TryExtractJsonFloatField(argsJson, "normalizedX", out x)
                || TryExtractJsonFloatField(argsJson, "x", out x);
            bool hasY =
                TryExtractJsonFloatField(argsJson, "normalizedY", out y)
                || TryExtractJsonFloatField(argsJson, "y", out y)
                || TryExtractJsonFloatField(argsJson, "normalized", out y)
                || TryExtractJsonFloatField(argsJson, "value", out y);

            Vector2 next = elementState.hasVector2Value ? elementState.vector2Value : new Vector2(0.5f, 0.5f);
            if (hasX)
                next.x = Mathf.Clamp01(x);
            if (hasY)
                next.y = Mathf.Clamp01(y);
            elementState.hasVector2Value = true;
            elementState.vector2Value = next;
            operation = "local_scroll_set";
            return true;
        }

        if (string.Equals(elementKind, "text_input", StringComparison.OrdinalIgnoreCase)
            || string.Equals(valueKind, "string", StringComparison.OrdinalIgnoreCase))
        {
            string text = ExtractJsonArgString(argsJson, "text", "value", "stringValue");
            if (!string.IsNullOrEmpty(text))
            {
                elementState.hasStringValue = true;
                elementState.stringValue = text;
                elementState.focused = true;
                operation = "local_text_set";
            }
            else
            {
                elementState.focused = !elementState.focused;
                operation = "local_text_focus";
            }

            return true;
        }

        if (string.Equals(elementKind, "toggle", StringComparison.OrdinalIgnoreCase)
            || string.Equals(valueKind, "bool", StringComparison.OrdinalIgnoreCase))
        {
            bool toggledValue;
            if (!TryReadBoolArg(argsJson, out toggledValue, "selected", "enabled", "checked", "value", "boolValue"))
                toggledValue = elementState.hasBoolValue ? !elementState.boolValue : true;

            elementState.hasBoolValue = true;
            elementState.boolValue = toggledValue;
            operation = "local_toggle_set";
            return true;
        }

        elementState.hasBoolValue = false;
        operation = "local_button_trigger";
        return true;
    }

    private FAInnerPieceControlSurfaceData CloneInnerPieceControlSurface(FAInnerPieceControlSurfaceData source)
    {
        if (source == null)
            return null;

        string serialized = FAInnerPiecePackageSupport.SerializeControlSurface(source, false);
        return FAInnerPiecePackageSupport.DeserializeControlSurface(serialized);
    }

    private GameObject ResolveInnerPieceNodeObject(InnerPieceInstanceRecord instance, string nodeId)
    {
        if (instance == null || string.IsNullOrEmpty(nodeId))
            return null;

        GameObject nodeObject;
        return instance.nodeObjects.TryGetValue(nodeId, out nodeObject) ? nodeObject : null;
    }

    private void SetInnerPieceNodeRenderersVisible(GameObject nodeObject, bool visible)
    {
        if (nodeObject == null)
            return;

        Renderer[] renderers = nodeObject.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = visible;
        }
    }

    private bool TrySetInnerPieceDisconnectSurfaceVisible(
        string instanceId,
        string slotSelector,
        bool visible,
        out string errorMessage)
    {
        errorMessage = "";

        InnerPieceInstanceRecord instance;
        if (!innerPieceInstances.TryGetValue(instanceId ?? "", out instance) || instance == null)
        {
            errorMessage = "instance not found";
            return false;
        }

        InnerPieceScreenSlotRuntimeRecord slot;
        if (!TryResolveInnerPieceScreenSlotRecord(instance, slotSelector, out slot))
        {
            errorMessage = "screen slot not found";
            return false;
        }

        slot.disconnectSurfaceVisible = visible;
        SetInnerPieceNodeRenderersVisible(slot.disconnectSurfaceObject, visible);
        return true;
    }

    private bool TryResolveInnerPieceScreenSlot(
        string instanceId,
        string slotSelector,
        out InnerPieceInstanceRecord instance,
        out InnerPieceScreenSlotRuntimeRecord slot,
        out string errorMessage)
    {
        instance = null;
        slot = null;
        errorMessage = "";

        if (string.IsNullOrEmpty(instanceId))
        {
            errorMessage = "instanceId is required";
            return false;
        }

        if (!innerPieceInstances.TryGetValue(instanceId, out instance) || instance == null)
        {
            errorMessage = "instance not found";
            return false;
        }

        if (string.IsNullOrEmpty(slotSelector))
        {
            errorMessage = "slotId or displayId is required";
            return false;
        }

        if (!TryResolveInnerPieceScreenSlotRecord(instance, slotSelector, out slot))
        {
            errorMessage = "screen slot not found";
            return false;
        }

        return true;
    }

    private bool TryResolveInnerPieceScreenSlotRecord(
        InnerPieceInstanceRecord instance,
        string slotSelector,
        out InnerPieceScreenSlotRuntimeRecord slot)
    {
        slot = null;
        if (instance == null || string.IsNullOrEmpty(slotSelector))
            return false;

        if (instance.screenSlots.TryGetValue(slotSelector, out slot) && slot != null)
            return true;

        foreach (KeyValuePair<string, InnerPieceScreenSlotRuntimeRecord> kvp in instance.screenSlots)
        {
            InnerPieceScreenSlotRuntimeRecord candidate = kvp.Value;
            if (candidate == null)
                continue;

            if (string.Equals(candidate.displayId, slotSelector, StringComparison.OrdinalIgnoreCase)
                || AreEquivalentInnerPieceDisplayIds(candidate.displayId, slotSelector)
                || AreEquivalentInnerPieceDisplayIds(candidate.slotId, slotSelector))
            {
                slot = candidate;
                return true;
            }
        }

        return false;
    }

    private static string ResolveDefaultInnerPieceDisplayId(string slotId, string displayId, string surfaceTargetId)
    {
        if (!string.IsNullOrEmpty(displayId))
            return displayId;

        string normalizedSlotId = string.IsNullOrEmpty(slotId) ? "main" : slotId.Trim();
        if (string.Equals(normalizedSlotId, "main", StringComparison.OrdinalIgnoreCase)
            && string.Equals(surfaceTargetId, "player:screen", StringComparison.OrdinalIgnoreCase))
        {
            return InnerPiecePrimaryPlayerDisplayId;
        }

        return normalizedSlotId;
    }

    private static string NormalizeInnerPieceDisplayAlias(string value)
    {
        string normalized = string.IsNullOrEmpty(value) ? "" : value.Trim();
        if (string.Equals(normalized, InnerPiecePrimaryPlayerDisplayId, StringComparison.OrdinalIgnoreCase))
            return "main";
        return normalized;
    }

    private static bool AreEquivalentInnerPieceDisplayIds(string left, string right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            return false;

        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(
            NormalizeInnerPieceDisplayAlias(left),
            NormalizeInnerPieceDisplayAlias(right),
            StringComparison.OrdinalIgnoreCase
        );
    }

    private bool TryResolveInnerPieceScreenPlane(
        string instanceId,
        string slotId,
        out FAInnerPiecePlaneData plane,
        out string errorMessage)
    {
        plane = new FAInnerPiecePlaneData();
        errorMessage = "";

        InnerPieceInstanceRecord instance;
        InnerPieceScreenSlotRuntimeRecord slot;
        if (!TryResolveInnerPieceScreenSlot(instanceId, slotId, out instance, out slot, out errorMessage))
            return false;

        if (slot.screenSurfaceObject == null)
        {
            errorMessage = "screen surface not found";
            return false;
        }

        if (!TryBuildInnerPiecePlaneData(slot.screenSurfaceObject, out plane))
        {
            errorMessage = "screen surface metrics unavailable";
            return false;
        }

        return true;
    }

    private bool TryBuildInnerPiecePlaneData(GameObject surfaceObject, out FAInnerPiecePlaneData plane)
    {
        plane = new FAInnerPiecePlaneData();
        if (surfaceObject == null)
            return false;

        Transform target = surfaceObject.transform;
        Vector3 right = target.right;
        Vector3 up = target.up;
        Vector3 forward = target.forward;
        if (right.sqrMagnitude <= 0.0001f)
            right = Vector3.right;
        if (up.sqrMagnitude <= 0.0001f)
            up = Vector3.up;
        if (forward.sqrMagnitude <= 0.0001f)
            forward = Vector3.forward;
        right.Normalize();
        up.Normalize();
        forward.Normalize();

        Renderer[] renderers = surfaceObject.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length <= 0)
            return false;

        bool hasProjection = false;
        float minRight = 0f;
        float maxRight = 0f;
        float minUp = 0f;
        float maxUp = 0f;
        float minForward = 0f;
        float maxForward = 0f;
        Vector3 origin = target.position;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Bounds worldBounds = renderer.bounds;
            Vector3 worldMin = worldBounds.min;
            Vector3 worldMax = worldBounds.max;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 worldPoint = new Vector3(
                    (corner & 1) == 0 ? worldMin.x : worldMax.x,
                    (corner & 2) == 0 ? worldMin.y : worldMax.y,
                    (corner & 4) == 0 ? worldMin.z : worldMax.z
                );
                Vector3 relative = worldPoint - origin;
                float projectedRight = Vector3.Dot(relative, right);
                float projectedUp = Vector3.Dot(relative, up);
                float projectedForward = Vector3.Dot(relative, forward);
                if (!hasProjection)
                {
                    minRight = maxRight = projectedRight;
                    minUp = maxUp = projectedUp;
                    minForward = maxForward = projectedForward;
                    hasProjection = true;
                }
                else
                {
                    minRight = Mathf.Min(minRight, projectedRight);
                    maxRight = Mathf.Max(maxRight, projectedRight);
                    minUp = Mathf.Min(minUp, projectedUp);
                    maxUp = Mathf.Max(maxUp, projectedUp);
                    minForward = Mathf.Min(minForward, projectedForward);
                    maxForward = Mathf.Max(maxForward, projectedForward);
                }
            }
        }

        if (!hasProjection)
            return false;

        plane.center =
            origin
            + (right * ((minRight + maxRight) * 0.5f))
            + (up * ((minUp + maxUp) * 0.5f))
            + (forward * ((minForward + maxForward) * 0.5f));
        plane.right = right;
        plane.up = up;
        plane.forward = forward;
        plane.widthMeters = Mathf.Max(0.001f, maxRight - minRight);
        plane.heightMeters = Mathf.Max(0.001f, maxUp - minUp);
        plane.depthMeters = Mathf.Max(0.001f, maxForward - minForward);
        return true;
    }

    private void EnsureInnerPieceGrabHandle(InnerPieceInstanceRecord instance)
    {
        if (instance == null)
            return;

        SyncObjectRecord rootRecord;
        if (!syncObjects.TryGetValue(instance.rootObjectId, out rootRecord) || rootRecord == null || rootRecord.gameObject == null)
            return;

        FAInnerPiecePlaneData plane;
        if (!TryBuildInnerPieceGrabHandlePlaneData(instance, out plane))
            return;

        // The Ghost root remains the real transform authority. This handle is only a
        // visible VR grab affordance positioned near the screen edge so grip can find
        // the screen without introducing a second transform system.
        GameObject handleObject = instance.grabHandleObject;
        Renderer handleRenderer = instance.grabHandleRenderer;
        if (handleObject == null)
        {
            handleObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            handleObject.name = "GrabHandle";
            handleObject.transform.SetParent(rootRecord.gameObject.transform, false);
            Collider handleCollider = handleObject.GetComponent<Collider>();
            if (handleCollider != null)
                Destroy(handleCollider);

            handleRenderer = handleObject.GetComponent<Renderer>();
            if (handleRenderer != null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                    shader = Shader.Find("Unlit/Color");
                if (shader != null)
                    handleRenderer.material = new Material(shader);
                handleRenderer.enabled = false;
            }

            instance.grabHandleObject = handleObject;
            instance.grabHandleRenderer = handleRenderer;
        }

        Vector3 worldPosition =
            plane.center
            + (plane.right * (plane.widthMeters * InnerPieceGrabHandleEdgeFactor))
            - (plane.up * (plane.heightMeters * InnerPieceGrabHandleEdgeFactor))
            + (plane.forward * ((plane.depthMeters * 0.5f) + InnerPieceGrabHandleFrontOffsetMeters));

        handleObject.transform.position = worldPosition;
        handleObject.transform.rotation = Quaternion.identity;
        handleObject.transform.localScale = Vector3.one * InnerPieceGrabHandleDiameterMeters;
    }

    private void TickInnerPieceFollowBindings()
    {
        if (innerPieceInstances.Count <= 0)
            return;

        foreach (KeyValuePair<string, InnerPieceInstanceRecord> kvp in innerPieceInstances)
        {
            InnerPieceInstanceRecord instance = kvp.Value;
            if (instance == null || string.IsNullOrEmpty(instance.anchorAtomUid))
                continue;

            if (!instance.followPosition && !instance.followRotation)
                continue;

            SyncObjectRecord rootRecord;
            if (!syncObjects.TryGetValue(instance.rootObjectId, out rootRecord) || rootRecord == null || rootRecord.gameObject == null)
                continue;

            Atom resolvedAnchorAtom;
            Transform anchorTransform;
            string errorMessage;
            if (!TryResolveInnerPieceAnchorTransform(instance.anchorAtomUid, instance.anchorAtomRef, out resolvedAnchorAtom, out anchorTransform, out errorMessage))
            {
                instance.lastError = errorMessage;
                continue;
            }

            instance.anchorAtomRef = resolvedAnchorAtom;
            if (ApplyInnerPieceFollowBinding(instance, rootRecord, anchorTransform))
                instance.lastError = "";
        }
    }

    private bool ApplyInnerPieceFollowBinding(
        InnerPieceInstanceRecord instance,
        SyncObjectRecord rootRecord,
        Transform anchorTransform)
    {
        if (instance == null || rootRecord == null || rootRecord.gameObject == null || anchorTransform == null)
            return false;

        Vector3 nextPosition = rootRecord.position;
        Quaternion nextRotation = rootRecord.rotation;
        if (instance.followPosition)
            nextPosition = anchorTransform.TransformPoint(instance.localPositionOffset);
        if (instance.followRotation)
            nextRotation = anchorTransform.rotation * instance.localRotationOffset;

        bool positionChanged = (nextPosition - rootRecord.position).sqrMagnitude > 0.00000025f;
        bool rotationChanged = Quaternion.Angle(nextRotation, rootRecord.rotation) > 0.01f;
        if (!positionChanged && !rotationChanged)
            return false;

        rootRecord.position = nextPosition;
        rootRecord.rotation = nextRotation;
        ApplyRecordVisuals(rootRecord);

        // Follow updates bypass the explicit TransformInstance action, so the same
        // binding refreshes need to happen here to keep any attached player surface
        // aligned with the moved root.
        RefreshPlayerScreenBindingsForInnerPieceInstance(instance.instanceId);
        MarkStandalonePlayerRecordsForInnerPieceRefresh(instance.instanceId);
        return true;
    }

    private void ClearInnerPieceFollowBinding(InnerPieceInstanceRecord instance)
    {
        if (instance == null)
            return;

        instance.anchorAtomUid = "";
        instance.anchorAtomRef = null;
        instance.followPosition = false;
        instance.followRotation = false;
        instance.localPositionOffset = Vector3.zero;
        instance.localRotationOffset = Quaternion.identity;
        instance.lastError = "";
    }

    private bool TryResolveInnerPieceAnchorTransform(
        string atomUid,
        Atom anchorAtomRef,
        out Atom resolvedAtom,
        out Transform anchorTransform,
        out string errorMessage)
    {
        resolvedAtom = null;
        anchorTransform = null;
        errorMessage = "";
        if (anchorAtomRef == null && string.IsNullOrEmpty(atomUid))
        {
            errorMessage = "anchorAtomUid is required";
            return false;
        }

        Atom atom = anchorAtomRef;
        if (atom == null)
        {
            SuperController sc = SuperController.singleton;
            if (sc == null)
            {
                errorMessage = "SuperController unavailable";
                return false;
            }

            try
            {
                atom = sc.GetAtomByUid(atomUid);
            }
            catch
            {
                atom = null;
            }
        }

        if (atom == null)
        {
            errorMessage = "anchor atom not found";
            return false;
        }

        resolvedAtom = atom;
        if ((string.Equals(atom.type ?? "", "Empty", StringComparison.OrdinalIgnoreCase)
                || string.Equals(atom.type ?? "", "CustomUnityAsset", StringComparison.OrdinalIgnoreCase))
            && atom.transform != null)
        {
            anchorTransform = atom.transform;
        }
        else
        {
            anchorTransform = atom.mainController != null ? atom.mainController.transform : atom.transform;
        }
        if (anchorTransform == null)
        {
            errorMessage = "anchor transform unavailable";
            return false;
        }

        return true;
    }

    private bool TryRequestInnerPieceAnchorAtom(SuperController sc, string atomType, out string diagnostics)
    {
        diagnostics = "";
        if (sc == null || string.IsNullOrEmpty(atomType))
            return false;

        List<string> attempts = new List<string>();
        try
        {
            sc.AddAtomByTypeForceSelect(atomType);
            diagnostics = "spawn request ok: forceSelect(" + atomType + ")";
            return true;
        }
        catch
        {
            attempts.Add("forceSelect(" + atomType + ")=fail");
        }

        try
        {
            sc.AddAtomByType(atomType, true, true, true);
            diagnostics = "spawn request ok: add(" + atomType + ")";
            return true;
        }
        catch
        {
            attempts.Add("add(" + atomType + ")=fail");
        }

        diagnostics = attempts.Count > 0 ? string.Join(", ", attempts.ToArray()) : "";
        return false;
    }

    private HashSet<int> SnapshotInnerPieceAnchorAtoms(SuperController sc)
    {
        HashSet<int> set = new HashSet<int>();
        if (sc == null)
            return set;

        List<Atom> atoms = sc.GetAtoms();
        if (atoms == null)
            return set;

        for (int i = 0; i < atoms.Count; i++)
        {
            Atom atom = atoms[i];
            if (atom == null)
                continue;
            set.Add(atom.GetInstanceID());
        }
        return set;
    }

    private bool TryFindNewInnerPieceAnchorAtomByReferenceDiff(SuperController sc, HashSet<int> baselineAtomIds, out Atom createdAtom)
    {
        createdAtom = null;
        if (sc == null || baselineAtomIds == null)
            return false;

        List<Atom> atoms = sc.GetAtoms();
        if (atoms == null)
            return false;

        for (int i = 0; i < atoms.Count; i++)
        {
            Atom atom = atoms[i];
            if (atom == null)
                continue;

            if (!baselineAtomIds.Contains(atom.GetInstanceID()))
            {
                createdAtom = atom;
                return true;
            }
        }

        return false;
    }

    private Atom TryDiscoverSpawnedInnerPieceAnchorAtom(SuperController sc, Atom selectedBeforeSpawn, HashSet<int> baselineAtomIds)
    {
        if (sc == null)
            return null;

        Atom selectedAfterSpawn = sc.GetSelectedAtom();
        if (selectedAfterSpawn != null && !ReferenceEquals(selectedAfterSpawn, selectedBeforeSpawn))
            return selectedAfterSpawn;

        Atom createdAtom;
        if (TryFindNewInnerPieceAnchorAtomByReferenceDiff(sc, baselineAtomIds, out createdAtom))
            return createdAtom;

        return null;
    }

    private Atom TryFindExistingInnerPieceAnchorAtom(
        SuperController sc,
        string requestedAnchorUid,
        string instanceId)
    {
        if (sc == null)
            return null;

        string normalizedRequestedUid = string.IsNullOrEmpty(requestedAnchorUid)
            ? ""
            : requestedAnchorUid.Trim();
        if (!string.IsNullOrEmpty(normalizedRequestedUid))
        {
            try
            {
                Atom directMatch = sc.GetAtomByUid(normalizedRequestedUid);
                if (directMatch != null)
                    return directMatch;
            }
            catch
            {
            }
        }

        List<Atom> atoms = sc.GetAtoms();
        if (atoms == null || atoms.Count <= 0)
            return null;

        string instancePrefix = string.IsNullOrEmpty(instanceId)
            ? ""
            : instanceId.Trim();
        for (int i = 0; i < atoms.Count; i++)
        {
            Atom atom = atoms[i];
            if (atom == null)
                continue;

            string uid = "";
            try
            {
                uid = atom.uid ?? "";
            }
            catch
            {
                uid = "";
            }

            if (!string.IsNullOrEmpty(normalizedRequestedUid))
            {
                if (string.Equals(uid, normalizedRequestedUid, StringComparison.OrdinalIgnoreCase))
                    return atom;
                if (uid.StartsWith(normalizedRequestedUid + "_", StringComparison.OrdinalIgnoreCase))
                    return atom;
            }

            if (!string.IsNullOrEmpty(instancePrefix))
            {
                if (uid.StartsWith(instancePrefix + "_grabpoint", StringComparison.OrdinalIgnoreCase))
                    return atom;
                if (uid.StartsWith(instancePrefix + "_anchor", StringComparison.OrdinalIgnoreCase))
                    return atom;
            }
        }

        return null;
    }

    private void TickPendingInnerPieceAnchorSpawns()
    {
        if (innerPieceInstances.Count <= 0)
            return;

        foreach (KeyValuePair<string, InnerPieceInstanceRecord> kvp in innerPieceInstances)
        {
            InnerPieceInstanceRecord instance = kvp.Value;
            if (instance == null || !instance.pendingAnchorDiscovery)
                continue;

            SyncObjectRecord rootRecord;
            if (!syncObjects.TryGetValue(instance.rootObjectId, out rootRecord) || rootRecord == null || rootRecord.gameObject == null)
                continue;

            SuperController sc = SuperController.singleton;
            if (sc == null)
                continue;

            Atom spawnedAtom = TryDiscoverSpawnedInnerPieceAnchorAtom(sc, instance.pendingSelectedBeforeSpawn, instance.pendingBaselineAtomIds);
            if (spawnedAtom == null)
            {
                spawnedAtom = TryFindExistingInnerPieceAnchorAtom(
                    sc,
                    instance.pendingRequestedAnchorUid,
                    instance.instanceId);
            }
            if (spawnedAtom == null)
            {
                if (Time.unscaledTime > instance.pendingAnchorDiscoveryDeadline)
                {
                    if (TryStartInnerPieceAnchorFallbackSpawn(sc, instance, rootRecord))
                        continue;

                    instance.pendingAnchorDiscovery = false;
                    instance.pendingAnchorActionId = "";
                    instance.pendingSelectedBeforeSpawn = null;
                    instance.pendingBaselineAtomIds = null;
                    instance.lastError = "spawned anchor atom was not discoverable after deferred discovery window";
                }
                continue;
            }

            string resultJson;
            string errorMessage;
            string anchorAtomUid;
            if (!TryFinalizeInnerPieceAnchorAtomBinding(
                string.IsNullOrEmpty(instance.pendingAnchorActionId) ? HostAnchorSpawnAtomActionId : instance.pendingAnchorActionId,
                instance,
                rootRecord,
                spawnedAtom,
                instance.pendingRequestedAnchorUid,
                instance.pendingAnchorPosition,
                instance.pendingAnchorRotation,
                instance.pendingAnchorScaleFactor,
                instance.pendingBindFollow,
                instance.pendingFollowPosition,
                instance.pendingFollowRotation,
                instance.pendingLocalPositionOffset,
                instance.pendingLocalRotationOffset,
                "",
                "",
                out resultJson,
                out errorMessage,
                out anchorAtomUid))
            {
                // Newly spawned VaM atoms can surface in GetAtoms()/selection before their
                // controller/uid lookup is fully stable. Keep the deferred bind alive until
                // the discovery window expires instead of hard-failing on the first transient
                // anchor resolution miss.
                if (IsTransientInnerPieceAnchorBindingError(errorMessage)
                    && Time.unscaledTime <= instance.pendingAnchorDiscoveryDeadline)
                {
                    instance.lastError = "";
                    continue;
                }

                instance.pendingAnchorDiscovery = false;
                instance.pendingAnchorActionId = "";
                instance.pendingSelectedBeforeSpawn = null;
                instance.pendingBaselineAtomIds = null;
                instance.lastError = errorMessage;
                continue;
            }
        }
    }

    private bool TryStartInnerPieceAnchorFallbackSpawn(
        SuperController sc,
        InnerPieceInstanceRecord instance,
        SyncObjectRecord rootRecord)
    {
        if (sc == null || instance == null || rootRecord == null || rootRecord.gameObject == null)
            return false;

        if (!string.Equals(
                instance.pendingAnchorActionId,
                HostAnchorSpawnGrabPointActionId,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string fallbackActionId = HostAnchorSpawnAtomActionId;
        string fallbackRequestedAnchorUid = BuildUniqueInnerPieceAnchorAtomUid(
            sc,
            BuildDefaultInnerPieceAnchorAtomUid(instance, "Empty"));

        HashSet<int> fallbackBaselineAtomIds = SnapshotInnerPieceAnchorAtoms(sc);
        Atom fallbackSelectedBeforeSpawn = sc.GetSelectedAtom();

        string requestDiagnostics;
        if (!TryRequestInnerPieceAnchorAtom(sc, "Empty", out requestDiagnostics))
        {
            instance.lastError = string.IsNullOrEmpty(requestDiagnostics)
                ? "fallback anchor atom spawn failed"
                : "fallback anchor atom spawn failed: " + requestDiagnostics;
            return false;
        }

        Atom fallbackSpawnedAtom = TryDiscoverSpawnedInnerPieceAnchorAtom(
            sc,
            fallbackSelectedBeforeSpawn,
            fallbackBaselineAtomIds);

        instance.pendingAnchorDiscovery = true;
        instance.pendingAnchorDiscoveryDeadline = Time.unscaledTime + 2.0f;
        instance.pendingAnchorActionId = fallbackActionId;
        instance.pendingSelectedBeforeSpawn = fallbackSelectedBeforeSpawn;
        instance.pendingBaselineAtomIds = fallbackBaselineAtomIds;
        instance.pendingRequestedAnchorUid = fallbackRequestedAnchorUid;
        instance.lastError = "";

        if (fallbackSpawnedAtom == null)
            return true;

        string resultJson;
        string errorMessage;
        string anchorAtomUid;
        if (!TryFinalizeInnerPieceAnchorAtomBinding(
            fallbackActionId,
            instance,
            rootRecord,
            fallbackSpawnedAtom,
            fallbackRequestedAnchorUid,
            instance.pendingAnchorPosition,
            instance.pendingAnchorRotation,
            instance.pendingAnchorScaleFactor,
            instance.pendingBindFollow,
            instance.pendingFollowPosition,
            instance.pendingFollowRotation,
            instance.pendingLocalPositionOffset,
            instance.pendingLocalRotationOffset,
            "",
            "",
            out resultJson,
            out errorMessage,
            out anchorAtomUid))
        {
            if (IsTransientInnerPieceAnchorBindingError(errorMessage))
                return true;

            instance.pendingAnchorDiscovery = false;
            instance.pendingAnchorActionId = "";
            instance.pendingSelectedBeforeSpawn = null;
            instance.pendingBaselineAtomIds = null;
            instance.pendingRequestedAnchorUid = "";
            instance.lastError = errorMessage;
            return false;
        }

        return true;
    }

    private bool IsTransientInnerPieceAnchorBindingError(string errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
            return false;

        return string.Equals(errorMessage, "anchor atom not found", StringComparison.OrdinalIgnoreCase)
            || string.Equals(errorMessage, "anchor transform unavailable", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryFinalizeInnerPieceAnchorAtomBinding(
        string actionId,
        InnerPieceInstanceRecord instance,
        SyncObjectRecord rootRecord,
        Atom spawnedAtom,
        string requestedAnchorUid,
        Vector3 anchorPosition,
        Quaternion anchorRotation,
        float anchorScaleFactor,
        bool bindFollow,
        bool followPosition,
        bool followRotation,
        Vector3 localPositionOffset,
        Quaternion localRotationOffset,
        string correlationId,
        string messageId,
        out string resultJson,
        out string errorMessage,
        out string anchorAtomUid)
    {
        resultJson = "{}";
        errorMessage = "";
        anchorAtomUid = "";

        try
        {
            spawnedAtom.uid = requestedAnchorUid;
        }
        catch
        {
        }

        SuperController sc = SuperController.singleton;
        Atom anchorAtom = null;
        if (sc != null)
        {
            try
            {
                anchorAtom = sc.GetAtomByUid(requestedAnchorUid);
            }
            catch
            {
                anchorAtom = null;
            }
        }

        if (anchorAtom == null)
            anchorAtom = spawnedAtom;

        anchorAtomUid = !string.IsNullOrEmpty(anchorAtom.uid) ? anchorAtom.uid : requestedAnchorUid;
        ApplyInnerPieceAnchorAtomTransform(anchorAtom, anchorPosition, anchorRotation, anchorScaleFactor);

        if (bindFollow)
        {
            Atom resolvedAnchorAtom;
            Transform anchorTransform;
            if (!TryResolveInnerPieceAnchorTransform(anchorAtomUid, anchorAtom, out resolvedAnchorAtom, out anchorTransform, out errorMessage))
            {
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            instance.anchorAtomUid = anchorAtomUid;
            instance.anchorAtomRef = resolvedAnchorAtom;
            instance.followPosition = followPosition;
            instance.followRotation = followRotation;
            instance.localPositionOffset = localPositionOffset;
            instance.localRotationOffset = localRotationOffset;
            instance.lastError = "";

            ApplyInnerPieceFollowBinding(instance, rootRecord, anchorTransform);
        }

        instance.pendingAnchorDiscovery = false;
        instance.pendingAnchorActionId = "";
        instance.pendingSelectedBeforeSpawn = null;
        instance.pendingBaselineAtomIds = null;
        instance.pendingRequestedAnchorUid = "";

        FAInnerPieceInstanceStateData state = BuildInnerPieceInstanceState(instance, "");
        string payload = BuildInnerPieceReceiptJson(actionId, "innerpiece_anchor_atom_spawned", null, state, null, "");
        resultJson = BuildBrokerResult(true, "innerpiece_anchor_atom_spawned", payload);
        EmitRuntimeEvent(
            "innerpiece_anchor_atom_spawned",
            actionId,
            "ok",
            "",
            instance.instanceId,
            instance.rootObjectId,
            correlationId,
            messageId,
            anchorAtomUid,
            payload
        );
        return true;
    }

    private string NormalizeInnerPieceAnchorActionId(string actionId)
    {
        if (string.Equals(actionId, HostAnchorSpawnGripPointActionId, StringComparison.OrdinalIgnoreCase))
            return HostAnchorSpawnGrabPointActionId;
        return actionId;
    }

    private string ResolveDefaultInnerPieceAnchorAtomType(string actionId)
    {
        if (string.Equals(actionId, HostAnchorSpawnGrabPointActionId, StringComparison.OrdinalIgnoreCase))
            return "GrabPoint";
        return "Empty";
    }

    private string BuildDefaultInnerPieceAnchorAtomUid(InnerPieceInstanceRecord instance, string atomType)
    {
        string suffix = string.Equals(atomType, "GrabPoint", StringComparison.OrdinalIgnoreCase)
            ? "_grabpoint"
            : "_anchor";
        string baseId = string.IsNullOrEmpty(instance != null ? instance.instanceId : null)
            ? "innerpiece"
            : instance.instanceId;
        if (baseId.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return baseId;
        return baseId + suffix;
    }

    private string BuildUniqueInnerPieceAnchorAtomUid(SuperController sc, string preferredUid)
    {
        string baseUid = string.IsNullOrEmpty(preferredUid)
            ? ("innerpiece_anchor_" + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture))
            : preferredUid;
        string candidate = baseUid;
        int suffix = 1;

        while (InnerPieceAnchorAtomUidExists(sc, candidate))
        {
            candidate = baseUid + "_" + suffix.ToString(CultureInfo.InvariantCulture);
            suffix++;
        }

        return candidate;
    }

    private bool InnerPieceAnchorAtomUidExists(SuperController sc, string atomUid)
    {
        if (sc == null || string.IsNullOrEmpty(atomUid))
            return false;

        try
        {
            return sc.GetAtomByUid(atomUid) != null;
        }
        catch
        {
            return false;
        }
    }

    private void ApplyInnerPieceAnchorAtomTransform(Atom atom, Vector3 position, Quaternion rotation, float scaleFactor)
    {
        if (atom == null)
            return;

        atom.transform.position = position;
        atom.transform.rotation = rotation;
        if (atom.mainController != null)
        {
            atom.mainController.transform.position = position;
            atom.mainController.transform.rotation = rotation;
        }

        JSONStorable control = atom.GetStorableByID("Control");
        if (control == null)
            control = atom.GetStorableByID("control");
        if (control != null)
        {
            JSONStorableFloat scaleParam = control.GetFloatJSONParam("Scale");
            if (scaleParam != null)
                scaleParam.val = Mathf.Clamp(scaleFactor, 0.25f, 3.0f);
        }
    }

    private Vector3 BuildInnerPieceUnderAnchorOffset(InnerPieceInstanceRecord instance, float clearanceMeters)
    {
        float halfHeight = 0.15f;
        FAInnerPiecePlaneData plane;
        if (TryBuildInnerPieceGrabHandlePlaneData(instance, out plane))
            halfHeight = Mathf.Max(0.001f, plane.heightMeters * 0.5f);

        return new Vector3(0f, -(halfHeight + Mathf.Max(0f, clearanceMeters)), 0f);
    }

    private bool TryBuildInnerPieceGrabHandlePlaneData(InnerPieceInstanceRecord instance, out FAInnerPiecePlaneData plane)
    {
        plane = new FAInnerPiecePlaneData();
        if (instance == null)
            return false;

        // Prefer the disconnect surface because that is the live playable plane on the
        // current Ghost rounded-screen path. Fall back to the authored screen surface
        // so the handle still appears on older or alternate exports.
        InnerPieceScreenSlotRuntimeRecord mainSlot;
        if (instance.screenSlots.TryGetValue("main", out mainSlot) && mainSlot != null)
        {
            if (TryBuildInnerPiecePlaneData(mainSlot.disconnectSurfaceObject, out plane))
                return true;
            if (TryBuildInnerPiecePlaneData(mainSlot.screenSurfaceObject, out plane))
                return true;
        }

        foreach (KeyValuePair<string, InnerPieceScreenSlotRuntimeRecord> kvp in instance.screenSlots)
        {
            InnerPieceScreenSlotRuntimeRecord slot = kvp.Value;
            if (slot == null)
                continue;

            if (TryBuildInnerPiecePlaneData(slot.disconnectSurfaceObject, out plane))
                return true;
            if (TryBuildInnerPiecePlaneData(slot.screenSurfaceObject, out plane))
                return true;
        }

        return false;
    }

    private bool TryGetOrCreateInnerPieceMesh(
        string resourceId,
        FAInnerPieceMeshData meshData,
        out Mesh sharedMesh,
        out string errorMessage
    )
    {
        sharedMesh = null;
        errorMessage = "";

        string cacheKey = resourceId + "::" + meshData.meshId;
        if (innerPieceMeshCache.TryGetValue(cacheKey, out sharedMesh) && sharedMesh != null)
            return true;

        try
        {
            Mesh mesh = new Mesh();
            mesh.name = "InnerPiece_" + cacheKey;
            mesh.vertices = meshData.vertices ?? new Vector3[0];
            mesh.triangles = meshData.triangleIndices ?? new int[0];
            if (meshData.normals != null && meshData.normals.Length == meshData.vertices.Length)
                mesh.normals = meshData.normals;
            else
                mesh.RecalculateNormals();
            if (meshData.uv0 != null && meshData.uv0.Length == meshData.vertices.Length)
                mesh.uv = meshData.uv0;
            mesh.RecalculateBounds();
            innerPieceMeshCache[cacheKey] = mesh;
            sharedMesh = mesh;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private Dictionary<string, FAInnerPieceMaterialEntry> BuildInnerPieceMaterialLookup(string resourceId)
    {
        Dictionary<string, FAInnerPieceMaterialEntry> lookup =
            new Dictionary<string, FAInnerPieceMaterialEntry>(StringComparer.OrdinalIgnoreCase);
        FAInnerPieceMaterialPackage materialPackage = FAInnerPiecePackageSupport.LoadStoredMaterialPackage(resourceId);
        FAInnerPieceMaterialEntry[] materials = materialPackage != null && materialPackage.materials != null
            ? materialPackage.materials
            : new FAInnerPieceMaterialEntry[0];
        for (int i = 0; i < materials.Length; i++)
        {
            FAInnerPieceMaterialEntry entry = materials[i];
            if (entry == null || string.IsNullOrEmpty(entry.materialRefId))
                continue;
            lookup[entry.materialRefId] = entry;
        }

        return lookup;
    }

    private FAInnerPieceMaterialEntry LookupInnerPieceMaterial(
        Dictionary<string, FAInnerPieceMaterialEntry> materialLookup,
        string materialRefId)
    {
        if (materialLookup == null || string.IsNullOrEmpty(materialRefId))
            return null;

        FAInnerPieceMaterialEntry entry;
        return materialLookup.TryGetValue(materialRefId, out entry) ? entry : null;
    }

    private Material CreateInnerPieceMaterial(string resourceId, FAInnerPieceMaterialEntry materialEntry)
    {
        Texture2D texture = ResolveInnerPieceMaterialTexture(resourceId, materialEntry);
        Shader shader = ResolveInnerPieceMaterialShader(materialEntry, texture);
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Texture");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Diffuse");
        Material material = shader != null ? new Material(shader) : new Material(Shader.Find("Standard"));
        material.color = texture != null ? Color.white : ResolveInnerPieceBaseColor(materialEntry);
        if (texture != null)
        {
            material.mainTexture = texture;
            material.SetTextureScale("_MainTex", Vector2.one);
            material.SetTextureOffset("_MainTex", Vector2.zero);
        }

        if (IsControlSurfaceSnapshotMaterial(materialEntry))
            ConfigureControlSurfaceSnapshotMaterial(material);
        return material;
    }

    private Shader ResolveInnerPieceMaterialShader(FAInnerPieceMaterialEntry materialEntry, Texture2D texture)
    {
        string requestedShaderName = materialEntry != null ? (materialEntry.shaderName ?? "").Trim() : "";
        bool isSnapshotMaterial = IsControlSurfaceSnapshotMaterial(materialEntry);
        if (texture != null && isSnapshotMaterial)
        {
            Shader snapshotShader = Shader.Find("Sprites/Default");
            if (snapshotShader == null)
                snapshotShader = Shader.Find("Unlit/Transparent");
            if (snapshotShader == null)
                snapshotShader = Shader.Find("Unlit/Texture");
            if (snapshotShader != null)
                return snapshotShader;
        }

        if (!string.IsNullOrEmpty(requestedShaderName))
        {
            Shader requestedShader = Shader.Find(requestedShaderName);
            if (requestedShader != null)
                return requestedShader;
        }

        return texture != null ? Shader.Find("Unlit/Texture") : null;
    }

    private bool IsControlSurfaceSnapshotMaterial(FAInnerPieceMaterialEntry materialEntry)
    {
        if (materialEntry == null || materialEntry.featureFlags == null)
            return false;

        string[] featureFlags = materialEntry.featureFlags;
        for (int i = 0; i < featureFlags.Length; i++)
        {
            if (string.Equals(featureFlags[i], "control_surface_canvas_snapshot", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void ConfigureControlSurfaceSnapshotMaterial(Material material)
    {
        if (material == null)
            return;

        material.color = Color.white;
        material.renderQueue = 3000;
        if (material.HasProperty("_Cull"))
            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        if (material.HasProperty("_ZWrite"))
            material.SetInt("_ZWrite", 0);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);
    }

    private Texture2D ResolveInnerPieceMaterialTexture(string resourceId, FAInnerPieceMaterialEntry materialEntry)
    {
        if (materialEntry == null || string.IsNullOrEmpty(materialEntry.texturePngBase64))
            return null;

        string cacheKey =
            (string.IsNullOrEmpty(resourceId) ? "inline" : resourceId)
            + "::"
            + (string.IsNullOrEmpty(materialEntry.materialRefId) ? "material" : materialEntry.materialRefId);

        Texture2D cachedTexture;
        if (innerPieceTextureCache.TryGetValue(cacheKey, out cachedTexture) && cachedTexture != null)
            return cachedTexture;

        try
        {
            byte[] pngBytes = Convert.FromBase64String(materialEntry.texturePngBase64);
            if (pngBytes == null || pngBytes.Length <= 0)
                return null;

            Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            if (!texture.LoadImage(pngBytes))
            {
                Destroy(texture);
                return null;
            }

            texture.name = "InnerPieceTexture_" + cacheKey;
            innerPieceTextureCache[cacheKey] = texture;
            return texture;
        }
        catch
        {
            return null;
        }
    }

    private Color ResolveInnerPieceBaseColor(FAInnerPieceMaterialEntry materialEntry)
    {
        if (materialEntry == null || string.IsNullOrEmpty(materialEntry.baseColorHex))
            return Color.white;

        Color parsed;
        return ColorUtility.TryParseHtmlString(materialEntry.baseColorHex, out parsed) ? parsed : Color.white;
    }

    private FAInnerPieceInstanceStateData BuildInnerPieceInstanceState(InnerPieceInstanceRecord instance, string lastError)
    {
        FAInnerPieceInstanceStateData state = new FAInnerPieceInstanceStateData();
        if (instance == null)
        {
            state.lastError = lastError ?? "";
            return state;
        }

        state.instanceId = instance.instanceId;
        state.resourceId = instance.resourceId;
        state.consumerId = instance.consumerId;
        state.targetType = instance.targetType;
        state.groupId = instance.groupId;
        state.rootObjectId = instance.rootObjectId;
        state.spawnedNodeIds = instance.spawnedNodeIds.ToArray();
        state.screenContractVersion = instance.screenContractVersion;
        state.shellId = instance.shellId;
        state.deviceClass = instance.deviceClass;
        state.orientationSupport = instance.orientationSupport;
        state.defaultAspectMode = instance.defaultAspectMode;
        state.safeCornerRadius = instance.safeCornerRadius;
        state.inputStyle = instance.inputStyle;
        state.autoOrientToGround = instance.autoOrientToGround;
        state.followBinding.anchorAtomUid = instance.anchorAtomUid;
        state.followBinding.followPosition = instance.followPosition;
        state.followBinding.followRotation = instance.followRotation;
        state.followBinding.localPositionOffset = instance.localPositionOffset;
        state.followBinding.localRotationOffset = instance.localRotationOffset;
        state.controlSurface = CloneInnerPieceControlSurface(instance.controlSurface);
        state.lastError = string.IsNullOrEmpty(lastError) ? instance.lastError : lastError;

        SyncObjectRecord rootRecord;
        if (syncObjects.TryGetValue(instance.rootObjectId, out rootRecord) && rootRecord != null)
        {
            state.rootTransform.position = rootRecord.position;
            state.rootTransform.rotation = rootRecord.rotation;
            state.rootTransform.scale = rootRecord.scale;
            if (!string.IsNullOrEmpty(rootRecord.parentGroupId))
                state.groupId = rootRecord.parentGroupId;
        }

        List<FAInnerPieceScreenSlotRuntimeState> screenSlots = new List<FAInnerPieceScreenSlotRuntimeState>();
        foreach (KeyValuePair<string, InnerPieceScreenSlotRuntimeRecord> kvp in instance.screenSlots)
        {
            InnerPieceScreenSlotRuntimeRecord slot = kvp.Value;
            if (slot == null)
                continue;

            FAInnerPieceScreenSlotRuntimeState slotState = new FAInnerPieceScreenSlotRuntimeState();
            slotState.slotId = slot.slotId;
            slotState.displayId = slot.displayId;
            slotState.surfaceTargetId = slot.surfaceTargetId;
            slotState.disconnectStateId = slot.disconnectStateId;
            slotState.screenSurfaceNodeId = slot.screenSurfaceNodeId;
            slotState.screenGlassNodeId = slot.screenGlassNodeId;
            slotState.screenApertureNodeId = slot.screenApertureNodeId;
            slotState.disconnectSurfaceNodeId = slot.disconnectSurfaceNodeId;
            slotState.disconnectSurfaceVisible = slot.disconnectSurfaceVisible;
            slotState.boundState = slot.screenSurfaceObject != null ? "slot_ready" : "slot_missing_surface";
            TryBuildInnerPiecePlaneData(slot.screenSurfaceObject, out slotState.plane);
            screenSlots.Add(slotState);
        }
        state.screenSlots = screenSlots.ToArray();

        return state;
    }

    private string BuildInnerPieceReceiptJson(
        string actionId,
        string summary,
        FAInnerPieceStoredResource resource,
        FAInnerPieceInstanceStateData instanceState,
        FAInnerPieceImportReceipt importReceipt,
        string lastError
    )
    {
        FAInnerPieceActionReceipt receipt = new FAInnerPieceActionReceipt();
        receipt.actionId = actionId ?? "";
        receipt.summary = summary ?? "";
        receipt.resourceId = resource != null ? resource.resourceId : (instanceState != null ? instanceState.resourceId : "");
        receipt.instanceId = instanceState != null ? instanceState.instanceId : "";
        receipt.consumerId = instanceState != null ? instanceState.consumerId : "";
        receipt.targetType = instanceState != null ? instanceState.targetType : "";
        receipt.lastError = lastError ?? "";
        receipt.resource = resource;
        receipt.instanceState = instanceState;
        receipt.importReceipt = importReceipt;
        return FAInnerPieceStorage.SerializeActionReceipt(receipt, false);
    }

    private bool MatchesInnerPieceQuery(FAInnerPieceCatalogEntry entry, string query)
    {
        if (entry == null)
            return false;
        if (string.IsNullOrEmpty(query))
            return true;

        string loweredQuery = query.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(loweredQuery))
            return true;

        if (!string.IsNullOrEmpty(entry.resourceId) && entry.resourceId.ToLowerInvariant().Contains(loweredQuery))
            return true;
        if (!string.IsNullOrEmpty(entry.displayName) && entry.displayName.ToLowerInvariant().Contains(loweredQuery))
            return true;
        if (!string.IsNullOrEmpty(entry.sourcePath) && entry.sourcePath.ToLowerInvariant().Contains(loweredQuery))
            return true;

        string[] tags = entry.tags ?? new string[0];
        for (int i = 0; i < tags.Length; i++)
        {
            if (!string.IsNullOrEmpty(tags[i]) && tags[i].ToLowerInvariant().Contains(loweredQuery))
                return true;
        }

        return false;
    }

    private bool MatchesInnerPieceTag(FAInnerPieceCatalogEntry entry, string tag)
    {
        if (entry == null)
            return false;
        if (string.IsNullOrEmpty(tag))
            return true;

        string[] tags = entry.tags ?? new string[0];
        for (int i = 0; i < tags.Length; i++)
        {
            if (string.Equals(tags[i], tag, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void ApplyInnerPieceRecordVisuals(SyncObjectRecord record)
    {
        if (record == null)
            return;

        InnerPieceInstanceRecord instance;
        if (!TryGetInnerPieceInstanceByRootObjectId(record.objectId, out instance))
            return;

        Color displayColor = GetDisplayColor(record);
        bool transparent = string.Equals(record.materialMode, "transparent", StringComparison.OrdinalIgnoreCase);
        displayColor.a = transparent ? 0.35f : 1f;

        for (int i = 0; i < instance.renderers.Count; i++)
        {
            Renderer renderer = instance.renderers[i];
            if (renderer == null)
                continue;

            bool visibilityOverride;
            if (TryGetPlayerScreenRendererVisibilityOverride(renderer, out visibilityOverride))
                renderer.enabled = record.visible && visibilityOverride;
            else
                renderer.enabled = record.visible;

            Material material = renderer.material;
            if (material != null)
                material.color = displayColor;
        }

        if (instance.grabHandleRenderer != null)
        {
            bool isManipulatingThisObject = IsManipulatingObject(record.objectId);
            bool handleVisible = record.visible
                && IsSceneDevModeEnabled()
                && (IsSceneInputCaptureEnabled() || IsActiveObject(record) || isManipulatingThisObject);
            instance.grabHandleRenderer.enabled = handleVisible;
            if (handleVisible)
            {
                Material handleMaterial = instance.grabHandleRenderer.material;
                if (handleMaterial != null)
                {
                    Color handleColor = isManipulatingThisObject || IsActiveObject(record)
                        ? Color.Lerp(GetDisplayColor(record), new Color(1.0f, 0.72f, 0.18f, 1.0f), 0.60f)
                        : new Color(1.0f, 0.72f, 0.18f, 0.90f);
                    handleColor.a = 0.95f;
                    handleMaterial.color = handleColor;
                }
            }
        }
    }

    private bool TryGetInnerPieceInstanceByRootObjectId(string rootObjectId, out InnerPieceInstanceRecord instance)
    {
        instance = null;
        if (string.IsNullOrEmpty(rootObjectId))
            return false;

        string instanceId;
        if (!innerPieceRootObjectToInstance.TryGetValue(rootObjectId, out instanceId))
            return false;

        return innerPieceInstances.TryGetValue(instanceId, out instance) && instance != null;
    }

    private void DeleteInnerPieceInstanceInternal(InnerPieceInstanceRecord instance, bool destroyRootRecord)
    {
        if (instance == null)
            return;

        ClearActiveControlSurfaceDragsForInstance(instance.instanceId);
        ClearPlayerScreenBindingsForInnerPieceInstance(instance.instanceId, false);
        ClearPlayerControlSurfaceBindingsForInnerPieceInstance(instance.instanceId);
        localControlSurfaceStates.Remove(instance.instanceId);
        if (instance.grabHandleObject != null)
            Destroy(instance.grabHandleObject);
        innerPieceInstances.Remove(instance.instanceId);
        innerPieceRootObjectToInstance.Remove(instance.rootObjectId);

        SyncGroupRecord group;
        if (syncGroups.TryGetValue(instance.groupId, out group))
            syncGroups.Remove(instance.groupId);
        if (string.Equals(activeGroupId, instance.groupId, StringComparison.OrdinalIgnoreCase))
            activeGroupId = "";

        SyncObjectRecord rootRecord;
        if (syncObjects.TryGetValue(instance.rootObjectId, out rootRecord) && rootRecord != null)
        {
            if (string.Equals(activeObjectId, instance.rootObjectId, StringComparison.OrdinalIgnoreCase))
            {
                CancelManipulation("innerpiece_deleted");
                activeObjectId = "";
                activeCorrelationId = "";
                activeMessageId = "";
            }

            rootRecord.parentGroupId = "";
            RemoveObjectFromAllGroups(instance.rootObjectId);

            if (destroyRootRecord)
            {
                DestroyRecord(rootRecord);
                syncObjects.Remove(instance.rootObjectId);
            }
        }
    }

    private bool IsInnerPieceRootObject(SyncObjectRecord record)
    {
        return record != null
            && string.Equals(record.kind, InnerPieceObjectKind, StringComparison.OrdinalIgnoreCase)
            && innerPieceRootObjectToInstance.ContainsKey(record.objectId);
    }

    private void ShutdownInnerPieceRuntime()
    {
        ShutdownPlayerScreenBindings();
        ShutdownPlayerControlSurfaceBindings();
        activeControlSurfaceDrags.Clear();
        foreach (KeyValuePair<string, Mesh> kvp in innerPieceMeshCache)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value);
        }
        foreach (KeyValuePair<string, Texture2D> kvp in innerPieceTextureCache)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value);
        }

        innerPieceMeshCache.Clear();
        innerPieceTextureCache.Clear();
        localControlSurfaceStates.Clear();
        innerPieceInstances.Clear();
        innerPieceRootObjectToInstance.Clear();
    }
}
