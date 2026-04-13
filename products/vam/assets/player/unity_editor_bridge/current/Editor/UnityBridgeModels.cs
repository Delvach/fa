using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FrameAngel.UnityEditorBridge
{
    [Serializable]
    public sealed class UnityBridgeCommandRequest
    {
        [JsonProperty("requestId")]
        public string RequestId { get; set; } = "";

        [JsonProperty("sessionId")]
        public string SessionId { get; set; } = "";

        [JsonProperty("action")]
        public string Action { get; set; } = "";

        [JsonProperty("args")]
        public JObject Args { get; set; } = new JObject();

        [JsonProperty("executionPolicy")]
        public UnityBridgeExecutionPolicy ExecutionPolicy { get; set; } = new UnityBridgeExecutionPolicy();

        [JsonProperty("metadata")]
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        [JsonProperty("bridgeOptions")]
        public UnityBridgeOptions BridgeOptions { get; set; } = new UnityBridgeOptions();
    }

    [Serializable]
    public sealed class UnityBridgeExecutionPolicy
    {
        [JsonProperty("confirmed")]
        public bool Confirmed { get; set; } = true;
    }

    [Serializable]
    public sealed class UnityBridgeOptions
    {
        [JsonProperty("allowUnsafeApiInvoke")]
        public bool AllowUnsafeApiInvoke { get; set; }
    }

    [Serializable]
    public sealed class UnityBridgeResponse
    {
        [JsonProperty("ok")]
        public bool Ok { get; set; } = true;

        [JsonProperty("code")]
        public string Code { get; set; } = "OK";

        [JsonProperty("message")]
        public string Message { get; set; } = "";

        [JsonProperty("requestId")]
        public string RequestId { get; set; } = "";

        [JsonProperty("data")]
        public object Data { get; set; }

        [JsonProperty("artifacts")]
        public List<UnityBridgeArtifact> Artifacts { get; set; } = new List<UnityBridgeArtifact>();

        [JsonProperty("warnings")]
        public List<string> Warnings { get; set; } = new List<string>();

        public static UnityBridgeResponse Error(string code, string message, string requestId)
        {
            return new UnityBridgeResponse
            {
                Ok = false,
                Code = code,
                Message = message,
                RequestId = requestId
            };
        }
    }

    [Serializable]
    public sealed class UnityBridgeArtifact
    {
        [JsonProperty("kind")]
        public string Kind { get; set; } = "image";

        [JsonProperty("label")]
        public string Label { get; set; } = "";

        [JsonProperty("path")]
        public string Path { get; set; } = "";

        [JsonProperty("contentType")]
        public string ContentType { get; set; } = "image/png";

        [JsonProperty("metadata")]
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    [Serializable]
    public sealed class UnityBridgeHealthData
    {
        [JsonProperty("bridgeVersion")]
        public string BridgeVersion { get; set; } = UnityBridgeController.BridgeVersion;

        [JsonProperty("unityVersion")]
        public string UnityVersion { get; set; } = "";

        [JsonProperty("projectPath")]
        public string ProjectPath { get; set; } = "";

        [JsonProperty("isPlaying")]
        public bool IsPlaying { get; set; }

        [JsonProperty("isCompiling")]
        public bool IsCompiling { get; set; }

        [JsonProperty("isUpdating")]
        public bool IsUpdating { get; set; }

        [JsonProperty("timestampUtc")]
        public string TimestampUtc { get; set; } = "";
    }

    [Serializable]
    public sealed class UnityBridgeCapabilitiesData
    {
        [JsonProperty("hostMode")]
        public string HostMode { get; set; } = "unity_editor_http";

        [JsonProperty("allowUnsafeApiInvoke")]
        public bool AllowUnsafeApiInvoke { get; set; }

        [JsonProperty("supportsSceneViewCapture")]
        public bool SupportsSceneViewCapture { get; set; } = true;

        [JsonProperty("supportsGameViewCapture")]
        public bool SupportsGameViewCapture { get; set; } = true;

        [JsonProperty("supportsNamedCameraCapture")]
        public bool SupportsNamedCameraCapture { get; set; }

        [JsonProperty("supportsMulticamRigCapture")]
        public bool SupportsMulticamRigCapture { get; set; }

        [JsonProperty("supportsSectionCapture")]
        public bool SupportsSectionCapture { get; set; }

        [JsonProperty("supportsPrimitiveWorkspaceMutation")]
        public bool SupportsPrimitiveWorkspaceMutation { get; set; }

        [JsonProperty("supportsRoundedRectPrism")]
        public bool SupportsRoundedRectPrism { get; set; }

        [JsonProperty("supportsObjectDuplicate")]
        public bool SupportsObjectDuplicate { get; set; }

        [JsonProperty("supportsGroupRoots")]
        public bool SupportsGroupRoots { get; set; }

        [JsonProperty("supportsCrtGlass")]
        public bool SupportsCrtGlass { get; set; }

        [JsonProperty("supportsCrtCabinet")]
        public bool SupportsCrtCabinet { get; set; }

        [JsonProperty("supportsSeatShell")]
        public bool SupportsSeatShell { get; set; }

        [JsonProperty("supportsArmrest")]
        public bool SupportsArmrest { get; set; }

        [JsonProperty("supportsTextureImportLocal")]
        public bool SupportsTextureImportLocal { get; set; }

        [JsonProperty("supportsMaterialStyle")]
        public bool SupportsMaterialStyle { get; set; }

        [JsonProperty("supportsParticleSystem")]
        public bool SupportsParticleSystem { get; set; }

        [JsonProperty("supportsParticleTexturedMaterials")]
        public bool SupportsParticleTexturedMaterials { get; set; }

        [JsonProperty("supportsParticleReadback")]
        public bool SupportsParticleReadback { get; set; }

        [JsonProperty("supportsSpektrLightning")]
        public bool SupportsSpektrLightning { get; set; }

        [JsonProperty("supportsInnerPieceExport")]
        public bool SupportsInnerPieceExport { get; set; }

        [JsonProperty("supportsPackageRefresh")]
        public bool SupportsPackageRefresh { get; set; }

        [JsonProperty("supportsObjectTransformObserve")]
        public bool SupportsObjectTransformObserve { get; set; }

        [JsonProperty("commandGroups")]
        public List<string> CommandGroups { get; set; } = new List<string>();

        [JsonProperty("commands")]
        public List<string> Commands { get; set; } = new List<string>();
    }

    [Serializable]
    public sealed class UnityBridgeStateData
    {
        [JsonProperty("activeScene")]
        public string ActiveScene { get; set; } = "";

        [JsonProperty("selectionSummary")]
        public UnitySelectionSummary SelectionSummary { get; set; }

        [JsonProperty("prefabStage")]
        public UnityPrefabStageSummary PrefabStage { get; set; }

        [JsonProperty("lastCapture")]
        public UnityLastCaptureSummary LastCapture { get; set; }

        [JsonProperty("workspaceState")]
        public UnityWorkspaceStateData WorkspaceState { get; set; }

        [JsonProperty("lastInnerPieceExport")]
        public UnityInnerPieceLastExportSummary LastInnerPieceExport { get; set; }

        [JsonProperty("timestampUtc")]
        public string TimestampUtc { get; set; } = "";
    }

    [Serializable]
    public sealed class UnitySelectionSummary
    {
        [JsonProperty("instanceId")]
        public int InstanceId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("path")]
        public string Path { get; set; } = "";

        [JsonProperty("tag")]
        public string Tag { get; set; } = "";

        [JsonProperty("layer")]
        public int Layer { get; set; }

        [JsonProperty("activeSelf")]
        public bool ActiveSelf { get; set; }

        [JsonProperty("activeInHierarchy")]
        public bool ActiveInHierarchy { get; set; }

        [JsonProperty("isPrefabInstance")]
        public bool IsPrefabInstance { get; set; }

        [JsonProperty("isPrefabAsset")]
        public bool IsPrefabAsset { get; set; }

        [JsonProperty("position")]
        public UnityVector3 Position { get; set; } = new UnityVector3();

        [JsonProperty("rotationEuler")]
        public UnityVector3 RotationEuler { get; set; } = new UnityVector3();

        [JsonProperty("scale")]
        public UnityVector3 Scale { get; set; } = new UnityVector3();

        [JsonProperty("componentTypes")]
        public List<string> ComponentTypes { get; set; } = new List<string>();

        public static UnitySelectionSummary FromGameObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return null;
            }

            Transform transform = gameObject.transform;
            return new UnitySelectionSummary
            {
                InstanceId = gameObject.GetInstanceID(),
                Name = gameObject.name,
                Path = UnityBridgeInspector.BuildPath(transform),
                Tag = gameObject.tag,
                Layer = gameObject.layer,
                ActiveSelf = gameObject.activeSelf,
                ActiveInHierarchy = gameObject.activeInHierarchy,
                IsPrefabInstance = UnityBridgeInspector.IsPrefabInstance(gameObject),
                IsPrefabAsset = UnityBridgeInspector.IsPrefabAsset(gameObject),
                Position = UnityVector3.FromVector3(transform.position),
                RotationEuler = UnityVector3.FromVector3(transform.eulerAngles),
                Scale = UnityVector3.FromVector3(transform.localScale),
                ComponentTypes = UnityBridgeInspector.GetComponentTypes(gameObject)
            };
        }
    }

    [Serializable]
    public sealed class UnitySceneContextData
    {
        [JsonProperty("sceneName")]
        public string SceneName { get; set; } = "";

        [JsonProperty("scenePath")]
        public string ScenePath { get; set; } = "";

        [JsonProperty("rootObjectCount")]
        public int RootObjectCount { get; set; }

        [JsonProperty("rootObjectNames")]
        public List<string> RootObjectNames { get; set; } = new List<string>();

        [JsonProperty("cameraNames")]
        public List<string> CameraNames { get; set; } = new List<string>();

        [JsonProperty("hasPrefabStageOpen")]
        public bool HasPrefabStageOpen { get; set; }

        [JsonProperty("selectedObjectPath")]
        public string SelectedObjectPath { get; set; } = "";
    }

    [Serializable]
    public sealed class UnityPrefabContextData
    {
        [JsonProperty("hasSelection")]
        public bool HasSelection { get; set; }

        [JsonProperty("contextType")]
        public string ContextType { get; set; } = "none";

        [JsonProperty("assetPath")]
        public string AssetPath { get; set; } = "";

        [JsonProperty("rootName")]
        public string RootName { get; set; } = "";

        [JsonProperty("instanceStatus")]
        public string InstanceStatus { get; set; } = "";

        [JsonProperty("isPartOfPrefabContents")]
        public bool IsPartOfPrefabContents { get; set; }
    }

    [Serializable]
    public sealed class UnityPrefabStageSummary
    {
        [JsonProperty("isOpen")]
        public bool IsOpen { get; set; }

        [JsonProperty("assetPath")]
        public string AssetPath { get; set; } = "";

        [JsonProperty("rootName")]
        public string RootName { get; set; } = "";
    }

    [Serializable]
    public sealed class UnityLastCaptureSummary
    {
        [JsonProperty("kind")]
        public string Kind { get; set; } = "";

        [JsonProperty("label")]
        public string Label { get; set; } = "";

        [JsonProperty("path")]
        public string Path { get; set; } = "";

        [JsonProperty("sceneName")]
        public string SceneName { get; set; } = "";

        [JsonProperty("cameraName")]
        public string CameraName { get; set; } = "";

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("capturedUtc")]
        public string CapturedUtc { get; set; } = "";
    }

    [Serializable]
    public sealed class UnityVector3
    {
        [JsonProperty("x")]
        public float X { get; set; }

        [JsonProperty("y")]
        public float Y { get; set; }

        [JsonProperty("z")]
        public float Z { get; set; }

        public static UnityVector3 FromVector3(Vector3 source)
        {
            return new UnityVector3
            {
                X = source.x,
                Y = source.y,
                Z = source.z
            };
        }

        public Vector3 ToVector3()
        {
            return new Vector3(X, Y, Z);
        }
    }

    [Serializable]
    public sealed class UnityVector2
    {
        [JsonProperty("x")]
        public float X { get; set; }

        [JsonProperty("y")]
        public float Y { get; set; }

        public static UnityVector2 FromVector2(Vector2 source)
        {
            return new UnityVector2
            {
                X = source.x,
                Y = source.y
            };
        }

        public Vector2 ToVector2()
        {
            return new Vector2(X, Y);
        }
    }

    [Serializable]
    public sealed class UnityCaptureArgs
    {
        [JsonProperty("outputPath")]
        public string OutputPath { get; set; } = "";

        [JsonProperty("label")]
        public string Label { get; set; } = "";

        [JsonProperty("width")]
        public int Width { get; set; } = 1024;

        [JsonProperty("height")]
        public int Height { get; set; } = 1024;
    }

    [Serializable]
    public sealed class UnityCameraCaptureArgs
    {
        [JsonProperty("cameraName")]
        public string CameraName { get; set; } = "";

        [JsonProperty("outputPath")]
        public string OutputPath { get; set; } = "";

        [JsonProperty("label")]
        public string Label { get; set; } = "";

        [JsonProperty("width")]
        public int Width { get; set; } = 1024;

        [JsonProperty("height")]
        public int Height { get; set; } = 1024;
    }

    [Serializable]
    public sealed class UnityOrbitCaptureArgs
    {
        [JsonProperty("objectId")]
        public string ObjectId { get; set; } = "";

        [JsonProperty("path")]
        public string Path { get; set; } = "";

        [JsonProperty("instanceId")]
        public int? InstanceId { get; set; }

        [JsonProperty("outputPath")]
        public string OutputPath { get; set; } = "";

        [JsonProperty("label")]
        public string Label { get; set; } = "";

        [JsonProperty("width")]
        public int Width { get; set; } = 1024;

        [JsonProperty("height")]
        public int Height { get; set; } = 1024;

        [JsonProperty("yawDegrees")]
        public float YawDegrees { get; set; } = 20f;

        [JsonProperty("pitchDegrees")]
        public float PitchDegrees { get; set; } = 12f;

        [JsonProperty("rollDegrees")]
        public float RollDegrees { get; set; }

        [JsonProperty("distanceScale")]
        public float DistanceScale { get; set; } = 1.15f;

        [JsonProperty("fieldOfView")]
        public float FieldOfView { get; set; } = 32f;

        [JsonProperty("lookAtOffset")]
        public UnityVector3 LookAtOffset { get; set; } = new UnityVector3();

        [JsonProperty("backgroundColorHex")]
        public string BackgroundColorHex { get; set; } = "#FFFFFF";
    }

    [Serializable]
    public sealed class UnitySectionCaptureArgs
    {
        [JsonProperty("objectId")]
        public string ObjectId { get; set; } = "";

        [JsonProperty("path")]
        public string Path { get; set; } = "";

        [JsonProperty("instanceId")]
        public int? InstanceId { get; set; }

        [JsonProperty("outputPath")]
        public string OutputPath { get; set; } = "";

        [JsonProperty("label")]
        public string Label { get; set; } = "";

        [JsonProperty("width")]
        public int Width { get; set; } = 1024;

        [JsonProperty("height")]
        public int Height { get; set; } = 1024;

        [JsonProperty("axis")]
        public string Axis { get; set; } = "left";

        [JsonProperty("sliceOffsetNormalized")]
        public float SliceOffsetNormalized { get; set; }

        [JsonProperty("orthographicPadding")]
        public float OrthographicPadding { get; set; } = 1.08f;

        [JsonProperty("backgroundColorHex")]
        public string BackgroundColorHex { get; set; } = "#00FF66";
    }

    [Serializable]
    public sealed class UnityMulticamCaptureArgs
    {
        [JsonProperty("captureBundleId")]
        public string CaptureBundleId { get; set; } = "";

        [JsonProperty("sessionId")]
        public string SessionId { get; set; } = "";

        [JsonProperty("targetId")]
        public string TargetId { get; set; } = "";

        [JsonProperty("iteration")]
        public int Iteration { get; set; }

        [JsonProperty("objectId")]
        public string ObjectId { get; set; } = "";

        [JsonProperty("path")]
        public string Path { get; set; } = "";

        [JsonProperty("instanceId")]
        public int? InstanceId { get; set; }

        [JsonProperty("outputPath")]
        public string OutputPath { get; set; } = "";

        [JsonProperty("width")]
        public int Width { get; set; } = 1024;

        [JsonProperty("height")]
        public int Height { get; set; } = 1024;
    }

    [Serializable]
    public sealed class UnityObjectReferenceArgs
    {
        [JsonProperty("objectId")]
        public string ObjectId { get; set; } = "";

        [JsonProperty("path")]
        public string Path { get; set; } = "";

        [JsonProperty("instanceId")]
        public int? InstanceId { get; set; }
    }

    [Serializable]
    public sealed class UnityObjectChildrenArgs
    {
        [JsonProperty("path")]
        public string Path { get; set; } = "";

        [JsonProperty("instanceId")]
        public int? InstanceId { get; set; }
    }

    [Serializable]
    public sealed class UnityObjectChildSummary
    {
        [JsonProperty("instanceId")]
        public int InstanceId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("path")]
        public string Path { get; set; } = "";

        [JsonProperty("activeSelf")]
        public bool ActiveSelf { get; set; }

        [JsonProperty("activeInHierarchy")]
        public bool ActiveInHierarchy { get; set; }

        [JsonProperty("componentTypes")]
        public List<string> ComponentTypes { get; set; } = new List<string>();
    }

    [Serializable]
    public sealed class UnityBounds3
    {
        [JsonProperty("center")]
        public UnityVector3 Center { get; set; } = new UnityVector3();

        [JsonProperty("size")]
        public UnityVector3 Size { get; set; } = new UnityVector3();

        [JsonProperty("extents")]
        public UnityVector3 Extents { get; set; } = new UnityVector3();

        public static UnityBounds3 FromBounds(Bounds bounds)
        {
            return new UnityBounds3
            {
                Center = UnityVector3.FromVector3(bounds.center),
                Size = UnityVector3.FromVector3(bounds.size),
                Extents = UnityVector3.FromVector3(bounds.extents)
            };
        }
    }

    [Serializable]
    public sealed class UnityObjectBoundsData
    {
        [JsonProperty("hasBounds")]
        public bool HasBounds { get; set; }

        [JsonProperty("objectId")]
        public string ObjectId { get; set; } = "";

        [JsonProperty("path")]
        public string Path { get; set; } = "";

        [JsonProperty("bounds")]
        public UnityBounds3 Bounds { get; set; }
    }

    [Serializable]
    public sealed class UnityObjectTransformData
    {
        [JsonProperty("objectId")]
        public string ObjectId { get; set; } = "";

        [JsonProperty("path")]
        public string Path { get; set; } = "";

        [JsonProperty("instanceId")]
        public int InstanceId { get; set; }

        [JsonProperty("localPosition")]
        public UnityVector3 LocalPosition { get; set; } = new UnityVector3();

        [JsonProperty("localRotationEuler")]
        public UnityVector3 LocalRotationEuler { get; set; } = new UnityVector3();

        [JsonProperty("localScale")]
        public UnityVector3 LocalScale { get; set; } = new UnityVector3();

        [JsonProperty("worldPosition")]
        public UnityVector3 WorldPosition { get; set; } = new UnityVector3();

        [JsonProperty("worldRotationEuler")]
        public UnityVector3 WorldRotationEuler { get; set; } = new UnityVector3();
    }

    [Serializable]
    public sealed class UnityWorkspaceStateData
    {
        [JsonProperty("workspaceExists")]
        public bool WorkspaceExists { get; set; }

        [JsonProperty("workspaceRootPath")]
        public string WorkspaceRootPath { get; set; } = "";

        [JsonProperty("targetRootPath")]
        public string TargetRootPath { get; set; } = "";

        [JsonProperty("managedObjectCount")]
        public int ManagedObjectCount { get; set; }

        [JsonProperty("selectedObjectId")]
        public string SelectedObjectId { get; set; } = "";

        [JsonProperty("managedObjects")]
        public List<UnityWorkspaceObjectState> ManagedObjects { get; set; } = new List<UnityWorkspaceObjectState>();

        [JsonProperty("canonicalRig")]
        public UnityCanonicalRigSummary CanonicalRig { get; set; } = new UnityCanonicalRigSummary();
    }

    [Serializable]
    public sealed class UnityWorkspaceObjectState
    {
        [JsonProperty("objectId")]
        public string ObjectId { get; set; } = "";

        [JsonProperty("kind")]
        public string Kind { get; set; } = "";

        [JsonProperty("instanceId")]
        public int InstanceId { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; } = "";

        [JsonProperty("parentObjectId")]
        public string ParentObjectId { get; set; } = "";

        [JsonProperty("activeSelf")]
        public bool ActiveSelf { get; set; }

        [JsonProperty("activeInHierarchy")]
        public bool ActiveInHierarchy { get; set; }

        [JsonProperty("localPosition")]
        public UnityVector3 LocalPosition { get; set; } = new UnityVector3();

        [JsonProperty("localRotationEuler")]
        public UnityVector3 LocalRotationEuler { get; set; } = new UnityVector3();

        [JsonProperty("localScale")]
        public UnityVector3 LocalScale { get; set; } = new UnityVector3();

        [JsonProperty("bounds")]
        public UnityBounds3 Bounds { get; set; }

        [JsonProperty("componentTypes")]
        public List<string> ComponentTypes { get; set; } = new List<string>();

        [JsonProperty("particleSystem")]
        public UnityParticleSystemState ParticleSystem { get; set; }
    }

    [Serializable]
    public sealed class UnityCanonicalRigSummary
    {
        [JsonProperty("rigId")]
        public string RigId { get; set; } = "canonical_six_v1";

        [JsonProperty("views")]
        public List<string> Views { get; set; } = new List<string>
        {
            "front",
            "back",
            "left",
            "right",
            "top",
            "bottom"
        };

        [JsonProperty("background")]
        public string Background { get; set; } = "#ffffff";

        [JsonProperty("normalizeToTargetBounds")]
        public bool NormalizeToTargetBounds { get; set; } = true;
    }

    [Serializable]
    public sealed class UnityCaptureBundleData
    {
        [JsonProperty("schemaVersion")]
        public string SchemaVersion { get; set; } = "unity_capture_bundle_v1";

        [JsonProperty("captureBundleId")]
        public string CaptureBundleId { get; set; } = "";

        [JsonProperty("sessionId")]
        public string SessionId { get; set; } = "";

        [JsonProperty("targetId")]
        public string TargetId { get; set; } = "";

        [JsonProperty("iteration")]
        public int Iteration { get; set; }

        [JsonProperty("workspaceStateRef")]
        public string WorkspaceStateRef { get; set; } = "";

        [JsonProperty("rigId")]
        public string RigId { get; set; } = "canonical_six_v1";

        [JsonProperty("targetRootId")]
        public string TargetRootId { get; set; } = "";

        [JsonProperty("targetRootPath")]
        public string TargetRootPath { get; set; } = "";

        [JsonProperty("targetBounds")]
        public UnityBounds3 TargetBounds { get; set; }

        [JsonProperty("views")]
        public List<UnityCaptureBundleView> Views { get; set; } = new List<UnityCaptureBundleView>();

        [JsonProperty("capturedUtc")]
        public string CapturedUtc { get; set; } = "";
    }

    [Serializable]
    public sealed class UnityCaptureBundleView
    {
        [JsonProperty("label")]
        public string Label { get; set; } = "";

        [JsonProperty("path")]
        public string Path { get; set; } = "";

        [JsonProperty("contentType")]
        public string ContentType { get; set; } = "image/png";

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("capturedUtc")]
        public string CapturedUtc { get; set; } = "";
    }

    [Serializable]
    public sealed class UnityWorkspaceResetArgs
    {
        [JsonProperty("workspaceName")]
        public string WorkspaceName { get; set; } = "";
    }

    [Serializable]
    public sealed class UnityPrimitiveUpsertArgs
    {
        [JsonProperty("objectId")]
        public string ObjectId { get; set; } = "";

        [JsonProperty("kind")]
        public string Kind { get; set; } = "cube";

        [JsonProperty("position")]
        public UnityVector3 Position { get; set; } = new UnityVector3();

        [JsonProperty("rotationEuler")]
        public UnityVector3 RotationEuler { get; set; } = new UnityVector3();

        [JsonProperty("scale")]
        public UnityVector3 Scale { get; set; } = new UnityVector3
        {
            X = 1f,
            Y = 1f,
            Z = 1f
        };

        [JsonProperty("active")]
        public bool Active { get; set; } = true;

        [JsonProperty("parentObjectId")]
        public string ParentObjectId { get; set; } = "";

        [JsonProperty("colorHex")]
        public string ColorHex { get; set; } = "";
    }

    [Serializable]
    public sealed class UnityRoundedRectPrismUpsertArgs
    {
        [JsonProperty("objectId")]
        public string ObjectId { get; set; } = "";

        [JsonProperty("position")]
        public UnityVector3 Position { get; set; } = new UnityVector3();

        [JsonProperty("rotationEuler")]
        public UnityVector3 RotationEuler { get; set; } = new UnityVector3();

        [JsonProperty("width")]
        public float Width { get; set; } = 1f;

        [JsonProperty("height")]
        public float Height { get; set; } = 1f;

        [JsonProperty("depth")]
        public float Depth { get; set; } = 0.1f;

        [JsonProperty("cornerRadius")]
        public float CornerRadius { get; set; } = 0.1f;

        [JsonProperty("cornerSegments")]
        public int CornerSegments { get; set; } = 6;

        [JsonProperty("active")]
        public bool Active { get; set; } = true;

        [JsonProperty("parentObjectId")]
        public string ParentObjectId { get; set; } = "";

        [JsonProperty("colorHex")]
        public string ColorHex { get; set; } = "";
    }

    [Serializable]
    public sealed class UnityPlayerScreenAuthoringUpsertArgs
    {
        [JsonProperty("rootObjectId")]
        public string RootObjectId { get; set; } = "";

        [JsonProperty("shellId")]
        public string ShellId { get; set; } = "player";

        [JsonProperty("screenContractVersion")]
        public string ScreenContractVersion { get; set; } = "frameangel_screen_contract_v1";

        [JsonProperty("defaultDisconnectStateId")]
        public string DefaultDisconnectStateId { get; set; } = "media_controls";

        [JsonProperty("surfaceTargetId")]
        public string SurfaceTargetId { get; set; } = "player:screen";

        [JsonProperty("slotId")]
        public string SlotId { get; set; } = "main";

        [JsonProperty("slotSurfaceTargetId")]
        public string SlotSurfaceTargetId { get; set; } = "";

        [JsonProperty("slotDisconnectStateId")]
        public string SlotDisconnectStateId { get; set; } = "";

        [JsonProperty("screenSurfaceObjectId")]
        public string ScreenSurfaceObjectId { get; set; } = "";

        [JsonProperty("screenGlassObjectId")]
        public string ScreenGlassObjectId { get; set; } = "";

        [JsonProperty("screenApertureObjectId")]
        public string ScreenApertureObjectId { get; set; } = "";

        [JsonProperty("disconnectSurfaceObjectId")]
        public string DisconnectSurfaceObjectId { get; set; } = "";
    }

    [Serializable]
    public sealed class UnityCrtGlassUpsertArgs
    {
        [JsonProperty("objectId")]
        public string ObjectId { get; set; } = "";

        [JsonProperty("parentObjectId")]
        public string ParentObjectId { get; set; } = "";

        [JsonProperty("position")]
        public UnityVector3 Position { get; set; } = new UnityVector3();

        [JsonProperty("rotationEuler")]
        public UnityVector3 RotationEuler { get; set; } = new UnityVector3();

        [JsonProperty("width")]
        public float Width { get; set; } = 1f;

        [JsonProperty("height")]
        public float Height { get; set; } = 1f;

        [JsonProperty("depth")]
        public float Depth { get; set; } = 0.1f;

        [JsonProperty("curveDepth")]
        public float CurveDepth { get; set; } = 0.04f;

        [JsonProperty("cornerRadius")]
        public float CornerRadius { get; set; } = 0.1f;

        [JsonProperty("cornerSegments")]
        public int CornerSegments { get; set; } = 8;

        [JsonProperty("active")]
        public bool Active { get; set; } = true;

        [JsonProperty("colorHex")]
        public string ColorHex { get; set; } = "";
    }

    [Serializable]
    public sealed class UnityCrtCabinetUpsertArgs
    {
        [JsonProperty("objectId")]
        public string ObjectId { get; set; } = "";

        [JsonProperty("parentObjectId")]
        public string ParentObjectId { get; set; } = "";

        [JsonProperty("position")]
        public UnityVector3 Position { get; set; } = new UnityVector3();

        [JsonProperty("rotationEuler")]
        public UnityVector3 RotationEuler { get; set; } = new UnityVector3();

        [JsonProperty("width")]
        public float Width { get; set; } = 1f;

        [JsonProperty("height")]
        public float Height { get; set; } = 1f;

        [JsonProperty("frontDepth")]
        public float FrontDepth { get; set; } = 0.12f;

        [JsonProperty("rearDepth")]
        public float RearDepth { get; set; } = 0.28f;

        [JsonProperty("backInsetX")]
        public float BackInsetX { get; set; } = 0.08f;

        [JsonProperty("backInsetTop")]
        public float BackInsetTop { get; set; } = 0.12f;

        [JsonProperty("backInsetBottom")]
        public float BackInsetBottom { get; set; } = 0.02f;

        [JsonProperty("frontCutoutWidth")]
        public float FrontCutoutWidth { get; set; }

        [JsonProperty("frontCutoutHeight")]
        public float FrontCutoutHeight { get; set; }

        [JsonProperty("frontCutoutDepth")]
        public float FrontCutoutDepth { get; set; }

        [JsonProperty("frontCutoutLipDepth")]
        public float FrontCutoutLipDepth { get; set; }

        [JsonProperty("frontCutoutX")]
        public float FrontCutoutX { get; set; }

        [JsonProperty("frontCutoutY")]
        public float FrontCutoutY { get; set; }

        [JsonProperty("frontCutoutCornerRadius")]
        public float FrontCutoutCornerRadius { get; set; } = 0.04f;

        [JsonProperty("frontCutoutBackInsetX")]
        public float FrontCutoutBackInsetX { get; set; }

        [JsonProperty("frontCutoutBackInsetTop")]
        public float FrontCutoutBackInsetTop { get; set; }

        [JsonProperty("frontCutoutBackInsetBottom")]
        public float FrontCutoutBackInsetBottom { get; set; }

        [JsonProperty("frontCutoutBackCornerRadiusOffset")]
        public float FrontCutoutBackCornerRadiusOffset { get; set; }

        [JsonProperty("cornerRadius")]
        public float CornerRadius { get; set; } = 0.08f;

        [JsonProperty("cornerSegments")]
        public int CornerSegments { get; set; } = 8;

        [JsonProperty("active")]
        public bool Active { get; set; } = true;

        [JsonProperty("colorHex")]
        public string ColorHex { get; set; } = "";
    }

    [Serializable]
    public sealed class UnitySeatShellUpsertArgs
    {
        [JsonProperty("objectId")]
        public string ObjectId { get; set; } = "";

        [JsonProperty("parentObjectId")]
        public string ParentObjectId { get; set; } = "";

        [JsonProperty("position")]
        public UnityVector3 Position { get; set; } = new UnityVector3();

        [JsonProperty("rotationEuler")]
        public UnityVector3 RotationEuler { get; set; } = new UnityVector3();

        [JsonProperty("seatWidth")]
        public float SeatWidth { get; set; } = 0.38f;

        [JsonProperty("seatBackHeight")]
        public float SeatBackHeight { get; set; } = 0.74f;

        [JsonProperty("seatBackDepth")]
        public float SeatBackDepth { get; set; } = 0.18f;

        [JsonProperty("seatPanDepth")]
        public float SeatPanDepth { get; set; } = 0.30f;

        [JsonProperty("seatPanThickness")]
        public float SeatPanThickness { get; set; } = 0.10f;

        [JsonProperty("headrestHeight")]
        public float HeadrestHeight { get; set; } = 0.16f;

        [JsonProperty("headrestInset")]
        public float HeadrestInset { get; set; } = 0.03f;

        [JsonProperty("shoulderWidth")]
        public float ShoulderWidth { get; set; } = 0.34f;

        [JsonProperty("lumbarDepth")]
        public float LumbarDepth { get; set; } = 0.05f;

        [JsonProperty("sideBolsterDepth")]
        public float SideBolsterDepth { get; set; } = 0.03f;

        [JsonProperty("seatPanAngle")]
        public float SeatPanAngle { get; set; } = 7f;

        [JsonProperty("seatBackAngle")]
        public float SeatBackAngle { get; set; } = 13f;

        [JsonProperty("screenBayWidth")]
        public float ScreenBayWidth { get; set; } = 0.22f;

        [JsonProperty("screenBayHeight")]
        public float ScreenBayHeight { get; set; } = 0.16f;

        [JsonProperty("screenBayDepth")]
        public float ScreenBayDepth { get; set; } = 0.024f;

        [JsonProperty("screenBayOffsetY")]
        public float ScreenBayOffsetY { get; set; } = 0.16f;

        [JsonProperty("screenBayCornerRadius")]
        public float ScreenBayCornerRadius { get; set; } = 0.02f;

        [JsonProperty("cornerRadius")]
        public float CornerRadius { get; set; } = 0.06f;

        [JsonProperty("segments")]
        public int Segments { get; set; } = 8;

        [JsonProperty("active")]
        public bool Active { get; set; } = true;

        [JsonProperty("colorHex")]
        public string ColorHex { get; set; } = "";
    }

    [Serializable]
    public sealed class UnityArmrestUpsertArgs
    {
        [JsonProperty("objectId")]
        public string ObjectId { get; set; } = "";

        [JsonProperty("parentObjectId")]
        public string ParentObjectId { get; set; } = "";

        [JsonProperty("position")]
        public UnityVector3 Position { get; set; } = new UnityVector3();

        [JsonProperty("rotationEuler")]
        public UnityVector3 RotationEuler { get; set; } = new UnityVector3();

        [JsonProperty("length")]
        public float Length { get; set; } = 0.34f;

        [JsonProperty("thickness")]
        public float Thickness { get; set; } = 0.065f;

        [JsonProperty("bodyHeight")]
        public float BodyHeight { get; set; } = 0.08f;

        [JsonProperty("frontNoseRadius")]
        public float FrontNoseRadius { get; set; } = 0.045f;

        [JsonProperty("rearPivotRadius")]
        public float RearPivotRadius { get; set; } = 0.05f;

        [JsonProperty("undersideSag")]
        public float UndersideSag { get; set; } = 0.018f;

        [JsonProperty("segments")]
        public int Segments { get; set; } = 12;

        [JsonProperty("active")]
        public bool Active { get; set; } = true;

        [JsonProperty("colorHex")]
        public string ColorHex { get; set; } = "";
    }

    [Serializable]
    public sealed class UnityGroupRootUpsertArgs
    {
        [JsonProperty("objectId")]
        public string ObjectId { get; set; } = "";

        [JsonProperty("parentObjectId")]
        public string ParentObjectId { get; set; } = "";

        [JsonProperty("position")]
        public UnityVector3 Position { get; set; } = new UnityVector3();

        [JsonProperty("rotationEuler")]
        public UnityVector3 RotationEuler { get; set; } = new UnityVector3();

        [JsonProperty("scale")]
        public UnityVector3 Scale { get; set; } = new UnityVector3
        {
            X = 1f,
            Y = 1f,
            Z = 1f
        };

        [JsonProperty("active")]
        public bool Active { get; set; } = true;
    }

    [Serializable]
    public sealed class UnityObjectDuplicateArgs
    {
        [JsonProperty("sourceObjectId")]
        public string SourceObjectId { get; set; } = "";

        [JsonProperty("targetObjectId")]
        public string TargetObjectId { get; set; } = "";
    }

    [Serializable]
    public sealed class UnityObjectDeleteArgs
    {
        [JsonProperty("objectId")]
        public string ObjectId { get; set; } = "";
    }

    [Serializable]
    public sealed class UnityTextureImportLocalArgs
    {
        [JsonProperty("textureId")]
        public string TextureId { get; set; } = "";

        [JsonProperty("sourcePath")]
        public string SourcePath { get; set; } = "";

        [JsonProperty("destinationFolder")]
        public string DestinationFolder { get; set; } = "";
    }

    [Serializable]
    public sealed class UnityMaterialStyleUpsertArgs
    {
        [JsonProperty("styleId")]
        public string StyleId { get; set; } = "";

        [JsonProperty("targetObjectId")]
        public string TargetObjectId { get; set; } = "";

        [JsonProperty("scope")]
        public string Scope { get; set; } = "object";

        [JsonProperty("shaderPreset")]
        public string ShaderPreset { get; set; } = "standard_opaque";

        [JsonProperty("sourceMode")]
        public string SourceMode { get; set; } = "solid_color";

        [JsonProperty("baseColorHex")]
        public string BaseColorHex { get; set; } = "";

        [JsonProperty("textureAssetPath")]
        public string TextureAssetPath { get; set; } = "";

        [JsonProperty("proceduralPreset")]
        public string ProceduralPreset { get; set; } = "";

        [JsonProperty("metallic")]
        public float Metallic { get; set; }

        [JsonProperty("smoothness")]
        public float Smoothness { get; set; } = 0.3f;

        [JsonProperty("tilingX")]
        public float TilingX { get; set; } = 1f;

        [JsonProperty("tilingY")]
        public float TilingY { get; set; } = 1f;

        [JsonProperty("offsetX")]
        public float OffsetX { get; set; }

        [JsonProperty("offsetY")]
        public float OffsetY { get; set; }
    }

    [Serializable]
    public sealed class UnityImportedTextureData
    {
        [JsonProperty("textureId")]
        public string TextureId { get; set; } = "";

        [JsonProperty("assetPath")]
        public string AssetPath { get; set; } = "";

        [JsonProperty("absolutePath")]
        public string AbsolutePath { get; set; } = "";

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("contentType")]
        public string ContentType { get; set; } = "image/png";
    }

    [Serializable]
    public sealed class UnityMaterialStyleData
    {
        [JsonProperty("styleId")]
        public string StyleId { get; set; } = "";

        [JsonProperty("materialAssetPath")]
        public string MaterialAssetPath { get; set; } = "";

        [JsonProperty("generatedTextureAssetPath")]
        public string GeneratedTextureAssetPath { get; set; } = "";

        [JsonProperty("targetObjectId")]
        public string TargetObjectId { get; set; } = "";

        [JsonProperty("appliedRendererCount")]
        public int AppliedRendererCount { get; set; }
    }

    [Serializable]
    public sealed class UnityParticleSystemUpsertArgs
    {
        [JsonProperty("objectId")]
        public string ObjectId { get; set; } = "";

        [JsonProperty("parentObjectId")]
        public string ParentObjectId { get; set; } = "";

        [JsonProperty("position")]
        public UnityVector3 Position { get; set; } = new UnityVector3();

        [JsonProperty("rotationEuler")]
        public UnityVector3 RotationEuler { get; set; } = new UnityVector3();

        [JsonProperty("scale")]
        public UnityVector3 Scale { get; set; } = new UnityVector3
        {
            X = 1f,
            Y = 1f,
            Z = 1f
        };

        [JsonProperty("active")]
        public bool Active { get; set; } = true;

        [JsonProperty("duration")]
        public float Duration { get; set; } = 1f;

        [JsonProperty("looping")]
        public bool Looping { get; set; } = true;

        [JsonProperty("maxParticles")]
        public int MaxParticles { get; set; } = 128;

        [JsonProperty("startLifetime")]
        public float StartLifetime { get; set; } = 0.8f;

        [JsonProperty("startLifetimeRandomness")]
        public float StartLifetimeRandomness { get; set; } = 0.2f;

        [JsonProperty("startSpeed")]
        public float StartSpeed { get; set; } = 0.05f;

        [JsonProperty("startSpeedRandomness")]
        public float StartSpeedRandomness { get; set; } = 0.2f;

        [JsonProperty("startSize")]
        public float StartSize { get; set; } = 0.02f;

        [JsonProperty("startSizeRandomness")]
        public float StartSizeRandomness { get; set; } = 0.2f;

        [JsonProperty("startColorHex")]
        public string StartColorHex { get; set; } = "#FFFFFFFF";

        [JsonProperty("emissionRate")]
        public float EmissionRate { get; set; } = 10f;

        [JsonProperty("burstCount")]
        public int BurstCount { get; set; }

        [JsonProperty("burstTime")]
        public float BurstTime { get; set; }

        [JsonProperty("simulationSpace")]
        public string SimulationSpace { get; set; } = "local";

        [JsonProperty("shape")]
        public string Shape { get; set; } = "cone";

        [JsonProperty("shapeRadius")]
        public float ShapeRadius { get; set; } = 0.02f;

        [JsonProperty("shapeAngle")]
        public float ShapeAngle { get; set; } = 15f;

        [JsonProperty("shapeLength")]
        public float ShapeLength { get; set; } = 0.05f;

        [JsonProperty("renderMode")]
        public string RenderMode { get; set; } = "billboard";

        [JsonProperty("materialPreset")]
        public string MaterialPreset { get; set; } = "default_particle";

        [JsonProperty("materialAssetPath")]
        public string MaterialAssetPath { get; set; } = "";

        [JsonProperty("textureAssetPath")]
        public string TextureAssetPath { get; set; } = "";

        [JsonProperty("materialBlendMode")]
        public string MaterialBlendMode { get; set; } = "";

        [JsonProperty("materialColorHex")]
        public string MaterialColorHex { get; set; } = "#FFFFFFFF";

        [JsonProperty("gravityModifier")]
        public float GravityModifier { get; set; }

        [JsonProperty("noiseStrength")]
        public float NoiseStrength { get; set; }

        [JsonProperty("colorOverLifetimeEnabled")]
        public bool ColorOverLifetimeEnabled { get; set; }

        [JsonProperty("colorOverLifetimeStartHex")]
        public string ColorOverLifetimeStartHex { get; set; } = "#FFFFFFFF";

        [JsonProperty("colorOverLifetimeEndHex")]
        public string ColorOverLifetimeEndHex { get; set; } = "#FFFFFF00";

        [JsonProperty("sizeOverLifetimeEnabled")]
        public bool SizeOverLifetimeEnabled { get; set; }

        [JsonProperty("sizeOverLifetimeEnd")]
        public float SizeOverLifetimeEnd { get; set; } = 1f;

        [JsonProperty("velocityOverLifetimeEnabled")]
        public bool VelocityOverLifetimeEnabled { get; set; }

        [JsonProperty("velocityOverLifetime")]
        public UnityVector3 VelocityOverLifetime { get; set; } = new UnityVector3();

        [JsonProperty("velocityOverLifetimeRandomness")]
        public float VelocityOverLifetimeRandomness { get; set; }

        [JsonProperty("trailsEnabled")]
        public bool TrailsEnabled { get; set; }

        [JsonProperty("trailLifetime")]
        public float TrailLifetime { get; set; } = 0.1f;

        [JsonProperty("trailMinVertexDistance")]
        public float TrailMinVertexDistance { get; set; } = 0.01f;

        [JsonProperty("trailMaterialAssetPath")]
        public string TrailMaterialAssetPath { get; set; } = "";

        [JsonProperty("trailTextureAssetPath")]
        public string TrailTextureAssetPath { get; set; } = "";

        [JsonProperty("trailMaterialBlendMode")]
        public string TrailMaterialBlendMode { get; set; } = "";

        [JsonProperty("trailMaterialColorHex")]
        public string TrailMaterialColorHex { get; set; } = "#FFFFFFFF";
    }

    [Serializable]
    public sealed class UnityParticleSystemState
    {
        [JsonProperty("duration")]
        public float Duration { get; set; }

        [JsonProperty("looping")]
        public bool Looping { get; set; }

        [JsonProperty("maxParticles")]
        public int MaxParticles { get; set; }

        [JsonProperty("startLifetime")]
        public float StartLifetime { get; set; }

        [JsonProperty("startSpeed")]
        public float StartSpeed { get; set; }

        [JsonProperty("startSize")]
        public float StartSize { get; set; }

        [JsonProperty("startColorHex")]
        public string StartColorHex { get; set; } = "#FFFFFFFF";

        [JsonProperty("emissionRate")]
        public float EmissionRate { get; set; }

        [JsonProperty("simulationSpace")]
        public string SimulationSpace { get; set; } = "local";

        [JsonProperty("shape")]
        public string Shape { get; set; } = "";

        [JsonProperty("renderMode")]
        public string RenderMode { get; set; } = "";

        [JsonProperty("materialName")]
        public string MaterialName { get; set; } = "";

        [JsonProperty("materialShader")]
        public string MaterialShader { get; set; } = "";

        [JsonProperty("materialAssetPath")]
        public string MaterialAssetPath { get; set; } = "";

        [JsonProperty("textureAssetPath")]
        public string TextureAssetPath { get; set; } = "";

        [JsonProperty("materialBlendMode")]
        public string MaterialBlendMode { get; set; } = "";

        [JsonProperty("trailsEnabled")]
        public bool TrailsEnabled { get; set; }

        [JsonProperty("trailMaterialName")]
        public string TrailMaterialName { get; set; } = "";

        [JsonProperty("trailMaterialShader")]
        public string TrailMaterialShader { get; set; } = "";

        [JsonProperty("trailMaterialAssetPath")]
        public string TrailMaterialAssetPath { get; set; } = "";

        [JsonProperty("trailTextureAssetPath")]
        public string TrailTextureAssetPath { get; set; } = "";

        [JsonProperty("trailMaterialBlendMode")]
        public string TrailMaterialBlendMode { get; set; } = "";
    }

    [Serializable]
    public sealed class UnitySpektrLightningUpsertArgs
    {
        [JsonProperty("objectId")]
        public string ObjectId { get; set; } = "";

        [JsonProperty("parentObjectId")]
        public string ParentObjectId { get; set; } = "";

        [JsonProperty("position")]
        public UnityVector3 Position { get; set; } = new UnityVector3();

        [JsonProperty("rotationEuler")]
        public UnityVector3 RotationEuler { get; set; } = new UnityVector3();

        [JsonProperty("scale")]
        public UnityVector3 Scale { get; set; } = new UnityVector3
        {
            X = 1f,
            Y = 1f,
            Z = 1f
        };

        [JsonProperty("active")]
        public bool Active { get; set; } = true;

        [JsonProperty("emitterObjectId")]
        public string EmitterObjectId { get; set; } = "";

        [JsonProperty("receiverObjectId")]
        public string ReceiverObjectId { get; set; } = "";

        [JsonProperty("emitterPosition")]
        public UnityVector3 EmitterPosition { get; set; } = new UnityVector3
        {
            X = -0.5f,
            Y = 0f,
            Z = 0f
        };

        [JsonProperty("receiverPosition")]
        public UnityVector3 ReceiverPosition { get; set; } = new UnityVector3
        {
            X = 0.5f,
            Y = 0f,
            Z = 0f
        };

        [JsonProperty("throttle")]
        public float Throttle { get; set; } = 0.9f;

        [JsonProperty("pulseInterval")]
        public float PulseInterval { get; set; } = 0.28f;

        [JsonProperty("boltLength")]
        public float BoltLength { get; set; } = 0.92f;

        [JsonProperty("lengthRandomness")]
        public float LengthRandomness { get; set; } = 0.55f;

        [JsonProperty("noiseAmplitude")]
        public float NoiseAmplitude { get; set; } = 0.08f;

        [JsonProperty("noiseFrequency")]
        public float NoiseFrequency { get; set; } = 0.18f;

        [JsonProperty("noiseMotion")]
        public float NoiseMotion { get; set; } = 0.35f;

        [JsonProperty("colorHex")]
        public string ColorHex { get; set; } = "#8ED7FFFF";

        [JsonProperty("colorIntensity")]
        public float ColorIntensity { get; set; } = 3f;

        [JsonProperty("randomSeed")]
        public int RandomSeed { get; set; } = 7;

        [JsonProperty("lineCount")]
        public int LineCount { get; set; } = 20;

        [JsonProperty("vertexCount")]
        public int VertexCount { get; set; } = 30;

        [JsonProperty("meshAssetPath")]
        public string MeshAssetPath { get; set; } = "Assets/ThirdParty/OpenSource/SpektrLightning/Spektr/Lightning/LightningMesh.asset";

        [JsonProperty("shaderAssetPath")]
        public string ShaderAssetPath { get; set; } = "Assets/ThirdParty/OpenSource/SpektrLightning/Spektr/Lightning/Shader/Lightning.shader";
    }
}
