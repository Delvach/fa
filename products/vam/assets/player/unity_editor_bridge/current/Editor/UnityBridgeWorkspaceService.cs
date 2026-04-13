using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FrameAngel.UnityEditorBridge
{
    internal static class UnityBridgeWorkspaceService
    {
        public const string WorkspaceRootName = "FrameAngelPrimitiveWorkspace";
        public const string TargetRootName = "WorkspaceRoot";
        public const string CanonicalRigId = "canonical_six_v1";

        private const string ManagedObjectPrefix = "FAPrimitive__";
        private const string GeneratedAssetsRoot = "Assets/FrameAngelGenerated";
        private const string GeneratedParticleMaterialsRoot = GeneratedAssetsRoot + "/ParticleMaterials";
        private const float CanonicalDistanceScale = 2.5f;
        private static readonly string[] CanonicalViews = { "front", "back", "left", "right", "top", "bottom" };

        public static UnityWorkspaceStateData GetWorkspaceState()
        {
            GameObject workspaceRoot = FindWorkspaceRoot();
            GameObject targetRoot = GetTargetRoot(workspaceRoot, false);
            List<UnityWorkspaceObjectState> managedObjects = new List<UnityWorkspaceObjectState>();
            string selectedObjectId = "";

            if (targetRoot != null)
            {
                CollectManagedObjects(targetRoot.transform, managedObjects);
            }

            if (Selection.activeGameObject != null && IsManagedWorkspaceObject(Selection.activeGameObject, out string objectId, out _))
            {
                selectedObjectId = objectId;
            }

            return new UnityWorkspaceStateData
            {
                WorkspaceExists = workspaceRoot != null && targetRoot != null,
                WorkspaceRootPath = workspaceRoot != null ? UnityBridgeInspector.BuildPath(workspaceRoot.transform) : "",
                TargetRootPath = targetRoot != null ? UnityBridgeInspector.BuildPath(targetRoot.transform) : "",
                ManagedObjectCount = managedObjects.Count,
                SelectedObjectId = selectedObjectId,
                ManagedObjects = managedObjects,
                CanonicalRig = BuildCanonicalRigSummary()
            };
        }

        public static UnityObjectBoundsData GetObjectBounds(UnityObjectReferenceArgs args)
        {
            GameObject target = ResolveObject(args);
            if (target == null)
            {
                return null;
            }

            UnityObjectBoundsData data = new UnityObjectBoundsData
            {
                ObjectId = TryGetWorkspaceObjectId(target),
                Path = UnityBridgeInspector.BuildPath(target.transform),
                HasBounds = TryGetWorldBounds(target, out Bounds bounds)
            };

            if (data.HasBounds)
            {
                data.Bounds = UnityBounds3.FromBounds(bounds);
            }

            return data;
        }

        public static UnityBridgeResponse WorkspaceReset(UnityBridgeCommandRequest request)
        {
            if (!CanMutateWorkspace(request, out UnityBridgeResponse rejection))
            {
                return rejection;
            }

            GameObject existing = FindWorkspaceRoot();
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            GameObject workspaceRoot = new GameObject(WorkspaceRootName);
            Undo.RegisterCreatedObjectUndo(workspaceRoot, "FrameAngel workspace root");

            GameObject targetRoot = new GameObject(TargetRootName);
            Undo.RegisterCreatedObjectUndo(targetRoot, "FrameAngel target root");
            Undo.SetTransformParent(targetRoot.transform, workspaceRoot.transform, "FrameAngel target root parent");
            targetRoot.transform.localPosition = Vector3.zero;
            targetRoot.transform.localRotation = Quaternion.identity;
            targetRoot.transform.localScale = Vector3.one;

            Selection.activeGameObject = targetRoot;
            MarkActiveSceneDirty();

            return new UnityBridgeResponse
            {
                RequestId = request.RequestId,
                Data = new Dictionary<string, object>
                {
                    { "workspaceState", GetWorkspaceState() }
                }
            };
        }

        public static UnityBridgeResponse GroupRootUpsert(UnityBridgeCommandRequest request)
        {
            if (!CanMutateWorkspace(request, out UnityBridgeResponse rejection))
            {
                return rejection;
            }

            UnityGroupRootUpsertArgs args = request.Args != null
                ? request.Args.ToObject<UnityGroupRootUpsertArgs>()
                : new UnityGroupRootUpsertArgs();

            if (!TryValidateObjectId(args.ObjectId, out string objectIdError))
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", objectIdError, request.RequestId);
            }

            GameObject targetRoot = GetTargetRoot(EnsureWorkspaceRoot(), true);
            GameObject target = FindManagedObject(targetRoot, args.ObjectId);
            if (target != null && !KindMatches(target, "group_root"))
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "Managed workspace object '" + args.ObjectId + "' already exists with kind '" + GetManagedKind(target) + "'.", request.RequestId);
            }

            Transform parentTransform = ResolveParentTransform(targetRoot, args.ParentObjectId, args.ObjectId, target, request.RequestId, out UnityBridgeResponse parentError);
            if (parentError != null)
            {
                return parentError;
            }

            if (target == null)
            {
                target = CreateManagedGroupRoot(parentTransform, args.ObjectId);
            }
            else if (target.transform.parent != parentTransform)
            {
                Undo.SetTransformParent(target.transform, parentTransform, "FrameAngel group root parent");
            }

            Undo.RecordObject(target.transform, "FrameAngel group root transform");
            target.SetActive(args.Active);
            target.transform.localPosition = args.Position != null ? args.Position.ToVector3() : Vector3.zero;
            target.transform.localRotation = Quaternion.Euler(args.RotationEuler != null ? args.RotationEuler.ToVector3() : Vector3.zero);
            target.transform.localScale = args.Scale != null ? args.Scale.ToVector3() : Vector3.one;

            Selection.activeGameObject = target;
            MarkActiveSceneDirty();

            return new UnityBridgeResponse
            {
                RequestId = request.RequestId,
                Data = new Dictionary<string, object>
                {
                    { "objectState", BuildWorkspaceObjectState(target) },
                    { "workspaceState", GetWorkspaceState() }
                }
            };
        }

        public static UnityBridgeResponse PrimitiveUpsert(UnityBridgeCommandRequest request)
        {
            if (!CanMutateWorkspace(request, out UnityBridgeResponse rejection))
            {
                return rejection;
            }

            UnityPrimitiveUpsertArgs args = request.Args != null
                ? request.Args.ToObject<UnityPrimitiveUpsertArgs>()
                : new UnityPrimitiveUpsertArgs();

            if (!TryValidateObjectId(args.ObjectId, out string objectIdError))
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", objectIdError, request.RequestId);
            }

            if (!TryMapPrimitiveType(args.Kind, out PrimitiveType primitiveType))
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "Primitive kind must be one of: cube, sphere, capsule, cylinder, plane, quad.", request.RequestId);
            }

            List<string> warnings = new List<string>();
            GameObject targetRoot = GetTargetRoot(EnsureWorkspaceRoot(), true);
            GameObject target = FindManagedObject(targetRoot, args.ObjectId);
            if (target != null && !KindMatches(target, args.Kind) && target.transform.childCount > 0)
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "Managed workspace object '" + args.ObjectId + "' cannot change kind while it still owns child objects.", request.RequestId);
            }

            Transform parentTransform = ResolveParentTransform(targetRoot, args.ParentObjectId, args.ObjectId, target, request.RequestId, out UnityBridgeResponse parentError);
            if (parentError != null)
            {
                return parentError;
            }

            if (target != null && !KindMatches(target, args.Kind))
            {
                int siblingIndex = target.transform.GetSiblingIndex();
                Undo.DestroyObjectImmediate(target);
                target = CreateManagedPrimitive(parentTransform, primitiveType, args.ObjectId, args.Kind);
                target.transform.SetSiblingIndex(siblingIndex);
            }

            if (target == null)
            {
                target = CreateManagedPrimitive(parentTransform, primitiveType, args.ObjectId, args.Kind);
            }
            else if (target.transform.parent != parentTransform)
            {
                Undo.SetTransformParent(target.transform, parentTransform, "FrameAngel primitive parent");
            }

            Undo.RecordObject(target.transform, "FrameAngel primitive transform");
            target.SetActive(args.Active);
            target.transform.localPosition = args.Position != null ? args.Position.ToVector3() : Vector3.zero;
            target.transform.localRotation = Quaternion.Euler(args.RotationEuler != null ? args.RotationEuler.ToVector3() : Vector3.zero);
            target.transform.localScale = args.Scale != null ? args.Scale.ToVector3() : Vector3.one;

            if (!string.IsNullOrWhiteSpace(args.ColorHex))
            {
                warnings.Add("colorHex is accepted for future compatibility but not applied in V1; primitive form only is the current scoring target.");
            }

            Selection.activeGameObject = target;
            MarkActiveSceneDirty();

            return new UnityBridgeResponse
            {
                RequestId = request.RequestId,
                Data = new Dictionary<string, object>
                {
                    { "objectState", BuildWorkspaceObjectState(target) },
                    { "workspaceState", GetWorkspaceState() }
                },
                Warnings = warnings
            };
        }

        public static UnityBridgeResponse RoundedRectPrismUpsert(UnityBridgeCommandRequest request)
        {
            if (!CanMutateWorkspace(request, out UnityBridgeResponse rejection))
            {
                return rejection;
            }

            UnityRoundedRectPrismUpsertArgs args = request.Args != null
                ? request.Args.ToObject<UnityRoundedRectPrismUpsertArgs>()
                : new UnityRoundedRectPrismUpsertArgs();

            if (!TryValidateObjectId(args.ObjectId, out string objectIdError))
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", objectIdError, request.RequestId);
            }

            if (args.Width <= 0f || args.Height <= 0f || args.Depth <= 0f)
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "width, height, and depth must all be greater than zero.", request.RequestId);
            }

            GameObject targetRoot = GetTargetRoot(EnsureWorkspaceRoot(), true);
            GameObject target = FindManagedObject(targetRoot, args.ObjectId);
            if (target != null && !KindMatches(target, "rounded_rect_prism") && target.transform.childCount > 0)
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "Managed workspace object '" + args.ObjectId + "' cannot change kind while it still owns child objects.", request.RequestId);
            }

            Transform parentTransform = ResolveParentTransform(targetRoot, args.ParentObjectId, args.ObjectId, target, request.RequestId, out UnityBridgeResponse parentError);
            if (parentError != null)
            {
                return parentError;
            }

            if (target != null && !KindMatches(target, "rounded_rect_prism"))
            {
                int siblingIndex = target.transform.GetSiblingIndex();
                Undo.DestroyObjectImmediate(target);
                target = CreateManagedMeshObject(parentTransform, args.ObjectId, "rounded_rect_prism");
                target.transform.SetSiblingIndex(siblingIndex);
            }

            if (target == null)
            {
                target = CreateManagedMeshObject(parentTransform, args.ObjectId, "rounded_rect_prism");
            }
            else if (target.transform.parent != parentTransform)
            {
                Undo.SetTransformParent(target.transform, parentTransform, "FrameAngel mesh object parent");
            }

            List<string> warnings = new List<string>();
            float maxCornerRadius = Mathf.Min(args.Width, args.Height) * 0.5f;
            float cornerRadius = Mathf.Clamp(args.CornerRadius, 0f, maxCornerRadius);
            int cornerSegments = Mathf.Clamp(args.CornerSegments, 1, 32);
            if (args.CornerRadius > maxCornerRadius)
            {
                warnings.Add("cornerRadius was clamped to half of the smaller face dimension.");
            }

            Mesh mesh = BuildRoundedRectPrismMesh(args.Width, args.Height, args.Depth, cornerRadius, cornerSegments);
            AssignManagedMesh(target, mesh);

            Undo.RecordObject(target.transform, "FrameAngel rounded rect prism transform");
            target.SetActive(args.Active);
            target.transform.localPosition = args.Position != null ? args.Position.ToVector3() : Vector3.zero;
            target.transform.localRotation = Quaternion.Euler(args.RotationEuler != null ? args.RotationEuler.ToVector3() : Vector3.zero);
            target.transform.localScale = Vector3.one;

            if (!string.IsNullOrWhiteSpace(args.ColorHex))
            {
                warnings.Add("colorHex is accepted for future compatibility but not applied in V1; primitive form only is the current scoring target.");
            }

            Selection.activeGameObject = target;
            MarkActiveSceneDirty();

            return new UnityBridgeResponse
            {
                RequestId = request.RequestId,
                Data = new Dictionary<string, object>
                {
                    { "objectState", BuildWorkspaceObjectState(target) },
                    { "workspaceState", GetWorkspaceState() }
                },
                Warnings = warnings
            };
        }

        public static UnityBridgeResponse PlayerScreenAuthoringUpsert(UnityBridgeCommandRequest request)
        {
            if (!CanMutateWorkspace(request, out UnityBridgeResponse rejection))
            {
                return rejection;
            }

            UnityPlayerScreenAuthoringUpsertArgs args = request.Args != null
                ? request.Args.ToObject<UnityPlayerScreenAuthoringUpsertArgs>()
                : new UnityPlayerScreenAuthoringUpsertArgs();

            if (!TryValidateObjectId(args.RootObjectId, out string objectIdError))
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", objectIdError, request.RequestId);
            }

            GameObject targetRoot = GetTargetRoot(EnsureWorkspaceRoot(), true);
            GameObject rootObject = FindManagedObject(targetRoot, args.RootObjectId);
            if (rootObject == null)
            {
                return UnityBridgeResponse.Error("OBJECT_NOT_FOUND", "Managed workspace object '" + args.RootObjectId + "' was not found.", request.RequestId);
            }

            GameObject screenSurface = ResolveManagedAuthoringTarget(targetRoot, args.ScreenSurfaceObjectId, true, "screenSurfaceObjectId", request.RequestId, out UnityBridgeResponse screenSurfaceError);
            if (screenSurfaceError != null)
            {
                return screenSurfaceError;
            }

            GameObject disconnectSurface = ResolveManagedAuthoringTarget(targetRoot, args.DisconnectSurfaceObjectId, true, "disconnectSurfaceObjectId", request.RequestId, out UnityBridgeResponse disconnectSurfaceError);
            if (disconnectSurfaceError != null)
            {
                return disconnectSurfaceError;
            }

            GameObject screenGlass = ResolveManagedAuthoringTarget(targetRoot, args.ScreenGlassObjectId, false, "screenGlassObjectId", request.RequestId, out UnityBridgeResponse screenGlassError);
            if (screenGlassError != null)
            {
                return screenGlassError;
            }

            GameObject screenAperture = ResolveManagedAuthoringTarget(targetRoot, args.ScreenApertureObjectId, false, "screenApertureObjectId", request.RequestId, out UnityBridgeResponse screenApertureError);
            if (screenApertureError != null)
            {
                return screenApertureError;
            }

            Undo.RecordObject(rootObject, "FrameAngel player shell authoring");
            FAPlayerShellAuthoring shellAuthoring = rootObject.GetComponent<FAPlayerShellAuthoring>();
            if (shellAuthoring == null)
            {
                shellAuthoring = Undo.AddComponent<FAPlayerShellAuthoring>(rootObject);
            }

            shellAuthoring.shellId = string.IsNullOrWhiteSpace(args.ShellId) ? "player" : args.ShellId.Trim();
            shellAuthoring.screenContractVersion = string.IsNullOrWhiteSpace(args.ScreenContractVersion) ? "frameangel_screen_contract_v1" : args.ScreenContractVersion.Trim();
            shellAuthoring.defaultDisconnectStateId = string.IsNullOrWhiteSpace(args.DefaultDisconnectStateId) ? "media_controls" : args.DefaultDisconnectStateId.Trim();
            shellAuthoring.surfaceTargetId = string.IsNullOrWhiteSpace(args.SurfaceTargetId) ? "player:screen" : args.SurfaceTargetId.Trim();

            Undo.RecordObject(rootObject, "FrameAngel player screen slot authoring");
            FAPlayerScreenSlotAuthoring slotAuthoring = rootObject.GetComponent<FAPlayerScreenSlotAuthoring>();
            if (slotAuthoring == null)
            {
                slotAuthoring = Undo.AddComponent<FAPlayerScreenSlotAuthoring>(rootObject);
            }

            slotAuthoring.slotId = string.IsNullOrWhiteSpace(args.SlotId) ? "main" : args.SlotId.Trim();
            slotAuthoring.surfaceTargetId = string.IsNullOrWhiteSpace(args.SlotSurfaceTargetId)
                ? shellAuthoring.surfaceTargetId
                : args.SlotSurfaceTargetId.Trim();
            slotAuthoring.disconnectStateId = string.IsNullOrWhiteSpace(args.SlotDisconnectStateId)
                ? shellAuthoring.defaultDisconnectStateId
                : args.SlotDisconnectStateId.Trim();
            slotAuthoring.screenSurface = screenSurface != null ? screenSurface.transform : null;
            slotAuthoring.screenGlass = screenGlass != null ? screenGlass.transform : null;
            slotAuthoring.screenAperture = screenAperture != null ? screenAperture.transform : null;
            slotAuthoring.disconnectSurface = disconnectSurface != null ? disconnectSurface.transform : null;

            Selection.activeGameObject = rootObject;
            MarkActiveSceneDirty();

            List<string> warnings = new List<string>();
            if (screenGlass == null)
            {
                warnings.Add("screenGlassObjectId not set; export will proceed without a screen glass node.");
            }

            if (screenAperture == null)
            {
                warnings.Add("screenApertureObjectId not set; export will proceed without a screen aperture node.");
            }

            return new UnityBridgeResponse
            {
                RequestId = request.RequestId,
                Data = new Dictionary<string, object>
                {
                    { "objectState", BuildWorkspaceObjectState(rootObject) },
                    { "workspaceState", GetWorkspaceState() }
                },
                Warnings = warnings
            };
        }

        public static UnityBridgeResponse ParticleSystemUpsert(UnityBridgeCommandRequest request)
        {
            if (!CanMutateWorkspace(request, out UnityBridgeResponse rejection))
            {
                return rejection;
            }

            UnityParticleSystemUpsertArgs args = request.Args != null
                ? request.Args.ToObject<UnityParticleSystemUpsertArgs>()
                : new UnityParticleSystemUpsertArgs();

            if (!TryValidateObjectId(args.ObjectId, out string objectIdError))
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", objectIdError, request.RequestId);
            }

            GameObject targetRoot = GetTargetRoot(EnsureWorkspaceRoot(), true);
            GameObject target = FindManagedObject(targetRoot, args.ObjectId);
            if (target != null && !KindMatches(target, "particle_system") && target.transform.childCount > 0)
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "Managed workspace object '" + args.ObjectId + "' cannot change kind while it still owns child objects.", request.RequestId);
            }

            Transform parentTransform = ResolveParentTransform(targetRoot, args.ParentObjectId, args.ObjectId, target, request.RequestId, out UnityBridgeResponse parentError);
            if (parentError != null)
            {
                return parentError;
            }

            if (target != null && !KindMatches(target, "particle_system"))
            {
                int siblingIndex = target.transform.GetSiblingIndex();
                Undo.DestroyObjectImmediate(target);
                target = CreateManagedParticleSystemObject(parentTransform, args.ObjectId);
                target.transform.SetSiblingIndex(siblingIndex);
            }

            if (target == null)
            {
                target = CreateManagedParticleSystemObject(parentTransform, args.ObjectId);
            }
            else if (target.transform.parent != parentTransform)
            {
                Undo.SetTransformParent(target.transform, parentTransform, "FrameAngel particle system parent");
            }

            Undo.RecordObject(target.transform, "FrameAngel particle system transform");
            target.SetActive(args.Active);
            target.transform.localPosition = args.Position != null ? args.Position.ToVector3() : Vector3.zero;
            target.transform.localRotation = Quaternion.Euler(args.RotationEuler != null ? args.RotationEuler.ToVector3() : Vector3.zero);
            target.transform.localScale = args.Scale != null ? args.Scale.ToVector3() : Vector3.one;

            List<string> warnings = new List<string>();
            ApplyParticleSystemSettings(target, args, warnings);

            Selection.activeGameObject = target;
            MarkActiveSceneDirty();

            return new UnityBridgeResponse
            {
                RequestId = request.RequestId,
                Data = new Dictionary<string, object>
                {
                    { "objectState", BuildWorkspaceObjectState(target) },
                    { "workspaceState", GetWorkspaceState() }
                },
                Warnings = warnings
            };
        }

        public static UnityBridgeResponse SpektrLightningUpsert(UnityBridgeCommandRequest request)
        {
            if (!CanMutateWorkspace(request, out UnityBridgeResponse rejection))
            {
                return rejection;
            }

            UnitySpektrLightningUpsertArgs args = request.Args != null
                ? request.Args.ToObject<UnitySpektrLightningUpsertArgs>()
                : new UnitySpektrLightningUpsertArgs();

            if (!TryValidateObjectId(args.ObjectId, out string objectIdError))
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", objectIdError, request.RequestId);
            }

            GameObject targetRoot = GetTargetRoot(EnsureWorkspaceRoot(), true);
            GameObject target = FindManagedObject(targetRoot, args.ObjectId);
            if (target != null && !KindMatches(target, "spektr_lightning") && target.transform.childCount > 0)
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "Managed workspace object '" + args.ObjectId + "' cannot change kind while it still owns child objects.", request.RequestId);
            }

            Transform parentTransform = ResolveParentTransform(targetRoot, args.ParentObjectId, args.ObjectId, target, request.RequestId, out UnityBridgeResponse parentError);
            if (parentError != null)
            {
                return parentError;
            }

            if (target != null && !KindMatches(target, "spektr_lightning"))
            {
                int siblingIndex = target.transform.GetSiblingIndex();
                Undo.DestroyObjectImmediate(target);
                target = CreateManagedSpektrLightningObject(parentTransform, args.ObjectId);
                target.transform.SetSiblingIndex(siblingIndex);
            }

            if (target == null)
            {
                target = CreateManagedSpektrLightningObject(parentTransform, args.ObjectId);
            }
            else if (target.transform.parent != parentTransform)
            {
                Undo.SetTransformParent(target.transform, parentTransform, "FrameAngel Spektr lightning parent");
            }

            Undo.RecordObject(target.transform, "FrameAngel Spektr lightning transform");
            target.SetActive(args.Active);
            target.transform.localPosition = args.Position != null ? args.Position.ToVector3() : Vector3.zero;
            target.transform.localRotation = Quaternion.Euler(args.RotationEuler != null ? args.RotationEuler.ToVector3() : Vector3.zero);
            target.transform.localScale = args.Scale != null ? args.Scale.ToVector3() : Vector3.one;

            List<string> warnings = new List<string>();
            if (!TryApplySpektrLightningSettings(targetRoot, target, args, warnings, out string applyError))
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", applyError, request.RequestId);
            }

            Selection.activeGameObject = target;
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
            MarkActiveSceneDirty();

            return new UnityBridgeResponse
            {
                RequestId = request.RequestId,
                Data = new Dictionary<string, object>
                {
                    { "objectState", BuildWorkspaceObjectState(target) },
                    { "workspaceState", GetWorkspaceState() }
                },
                Warnings = warnings
            };
        }

        public static UnityBridgeResponse CrtGlassUpsert(UnityBridgeCommandRequest request)
        {
            if (!CanMutateWorkspace(request, out UnityBridgeResponse rejection))
            {
                return rejection;
            }

            UnityCrtGlassUpsertArgs args = request.Args != null
                ? request.Args.ToObject<UnityCrtGlassUpsertArgs>()
                : new UnityCrtGlassUpsertArgs();

            if (!TryValidateObjectId(args.ObjectId, out string objectIdError))
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", objectIdError, request.RequestId);
            }

            if (args.Width <= 0f || args.Height <= 0f || args.Depth <= 0f)
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "width, height, and depth must all be greater than zero.", request.RequestId);
            }

            GameObject targetRoot = GetTargetRoot(EnsureWorkspaceRoot(), true);
            GameObject target = FindManagedObject(targetRoot, args.ObjectId);
            if (target != null && !KindMatches(target, "crt_glass") && target.transform.childCount > 0)
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "Managed workspace object '" + args.ObjectId + "' cannot change kind while it still owns child objects.", request.RequestId);
            }

            Transform parentTransform = ResolveParentTransform(targetRoot, args.ParentObjectId, args.ObjectId, target, request.RequestId, out UnityBridgeResponse parentError);
            if (parentError != null)
            {
                return parentError;
            }

            if (target != null && !KindMatches(target, "crt_glass"))
            {
                int siblingIndex = target.transform.GetSiblingIndex();
                Undo.DestroyObjectImmediate(target);
                target = CreateManagedMeshObject(parentTransform, args.ObjectId, "crt_glass");
                target.transform.SetSiblingIndex(siblingIndex);
            }

            if (target == null)
            {
                target = CreateManagedMeshObject(parentTransform, args.ObjectId, "crt_glass");
            }
            else if (target.transform.parent != parentTransform)
            {
                Undo.SetTransformParent(target.transform, parentTransform, "FrameAngel CRT glass parent");
            }

            List<string> warnings = new List<string>();
            float maxCornerRadius = Mathf.Min(args.Width, args.Height) * 0.5f;
            float cornerRadius = Mathf.Clamp(args.CornerRadius, 0f, maxCornerRadius);
            float curveDepth = Mathf.Clamp(args.CurveDepth, 0f, Mathf.Max(0.001f, args.Depth * 1.5f));
            int cornerSegments = Mathf.Clamp(args.CornerSegments, 1, 32);
            if (args.CornerRadius > maxCornerRadius)
            {
                warnings.Add("cornerRadius was clamped to half of the smaller face dimension.");
            }

            Mesh mesh = BuildCrtGlassMesh(args.Width, args.Height, args.Depth, curveDepth, cornerRadius, cornerSegments);
            AssignManagedMesh(target, mesh);

            Undo.RecordObject(target.transform, "FrameAngel CRT glass transform");
            target.SetActive(args.Active);
            target.transform.localPosition = args.Position != null ? args.Position.ToVector3() : Vector3.zero;
            target.transform.localRotation = Quaternion.Euler(args.RotationEuler != null ? args.RotationEuler.ToVector3() : Vector3.zero);
            target.transform.localScale = Vector3.one;

            if (!string.IsNullOrWhiteSpace(args.ColorHex))
            {
                warnings.Add("colorHex is accepted for future compatibility; use scene.material_style_upsert for the visible CRT glass finish.");
            }

            Selection.activeGameObject = target;
            MarkActiveSceneDirty();

            return new UnityBridgeResponse
            {
                RequestId = request.RequestId,
                Data = new Dictionary<string, object>
                {
                    { "objectState", BuildWorkspaceObjectState(target) },
                    { "workspaceState", GetWorkspaceState() }
                },
                Warnings = warnings
            };
        }

        public static UnityBridgeResponse CrtCabinetUpsert(UnityBridgeCommandRequest request)
        {
            if (!CanMutateWorkspace(request, out UnityBridgeResponse rejection))
            {
                return rejection;
            }

            UnityCrtCabinetUpsertArgs args = request.Args != null
                ? request.Args.ToObject<UnityCrtCabinetUpsertArgs>()
                : new UnityCrtCabinetUpsertArgs();

            if (!TryValidateObjectId(args.ObjectId, out string objectIdError))
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", objectIdError, request.RequestId);
            }

            if (args.Width <= 0f || args.Height <= 0f || args.FrontDepth <= 0f || args.RearDepth <= 0f)
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "width, height, frontDepth, and rearDepth must all be greater than zero.", request.RequestId);
            }

            if (args.BackInsetX < 0f || args.BackInsetTop < 0f || args.BackInsetBottom < 0f)
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "backInsetX, backInsetTop, and backInsetBottom must be zero or greater.", request.RequestId);
            }

            if (args.FrontCutoutLipDepth < 0f)
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "frontCutoutLipDepth must be zero or greater.", request.RequestId);
            }

            if (args.FrontCutoutBackInsetX < 0f || args.FrontCutoutBackInsetTop < 0f || args.FrontCutoutBackInsetBottom < 0f)
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "frontCutoutBackInsetX, frontCutoutBackInsetTop, and frontCutoutBackInsetBottom must be zero or greater.", request.RequestId);
            }

            float rearWidth = args.Width - (args.BackInsetX * 2f);
            float rearHeight = args.Height - args.BackInsetTop - args.BackInsetBottom;
            if (rearWidth <= 0.001f || rearHeight <= 0.001f)
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "Rear cabinet profile must remain positive after applying back insets.", request.RequestId);
            }

            bool hasFrontCutout = args.FrontCutoutWidth > 0f || args.FrontCutoutHeight > 0f || args.FrontCutoutDepth > 0f;
            if (hasFrontCutout)
            {
                if (args.FrontCutoutWidth <= 0f || args.FrontCutoutHeight <= 0f || args.FrontCutoutDepth <= 0f)
                {
                    return UnityBridgeResponse.Error("BAD_REQUEST", "frontCutoutWidth, frontCutoutHeight, and frontCutoutDepth must all be greater than zero when a front cutout is used.", request.RequestId);
                }

                const float frameMargin = 0.01f;
                if (Mathf.Abs(args.FrontCutoutX) + (args.FrontCutoutWidth * 0.5f) >= (args.Width * 0.5f) - frameMargin ||
                    Mathf.Abs(args.FrontCutoutY) + (args.FrontCutoutHeight * 0.5f) >= (args.Height * 0.5f) - frameMargin)
                {
                    return UnityBridgeResponse.Error("BAD_REQUEST", "Front cutout must remain inside the cabinet face with a visible frame margin.", request.RequestId);
                }

                float cutoutRearWidth = args.FrontCutoutWidth - (args.FrontCutoutBackInsetX * 2f);
                float cutoutRearHeight = args.FrontCutoutHeight - args.FrontCutoutBackInsetTop - args.FrontCutoutBackInsetBottom;
                if (cutoutRearWidth <= 0.001f || cutoutRearHeight <= 0.001f)
                {
                    return UnityBridgeResponse.Error("BAD_REQUEST", "Front cutout back profile must remain positive after applying cutout back insets.", request.RequestId);
                }
            }

            GameObject targetRoot = GetTargetRoot(EnsureWorkspaceRoot(), true);
            GameObject target = FindManagedObject(targetRoot, args.ObjectId);
            if (target != null && !KindMatches(target, "crt_cabinet") && target.transform.childCount > 0)
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "Managed workspace object '" + args.ObjectId + "' cannot change kind while it still owns child objects.", request.RequestId);
            }

            Transform parentTransform = ResolveParentTransform(targetRoot, args.ParentObjectId, args.ObjectId, target, request.RequestId, out UnityBridgeResponse parentError);
            if (parentError != null)
            {
                return parentError;
            }

            if (target != null && !KindMatches(target, "crt_cabinet"))
            {
                int siblingIndex = target.transform.GetSiblingIndex();
                Undo.DestroyObjectImmediate(target);
                target = CreateManagedMeshObject(parentTransform, args.ObjectId, "crt_cabinet");
                target.transform.SetSiblingIndex(siblingIndex);
            }

            if (target == null)
            {
                target = CreateManagedMeshObject(parentTransform, args.ObjectId, "crt_cabinet");
            }
            else if (target.transform.parent != parentTransform)
            {
                Undo.SetTransformParent(target.transform, parentTransform, "FrameAngel CRT cabinet parent");
            }

            List<string> warnings = new List<string>();
            float maxCornerRadius = Mathf.Min(args.Width, args.Height) * 0.5f;
            float cornerRadius = Mathf.Clamp(args.CornerRadius, 0f, maxCornerRadius);
            int cornerSegments = Mathf.Clamp(args.CornerSegments, 1, 32);
            if (args.CornerRadius > maxCornerRadius)
            {
                warnings.Add("cornerRadius was clamped to half of the smaller face dimension.");
            }

            float cutoutCornerRadius = Mathf.Max(0f, args.FrontCutoutCornerRadius);
            if (hasFrontCutout)
            {
                float maxCutoutRadius = Mathf.Min(args.FrontCutoutWidth, args.FrontCutoutHeight) * 0.5f;
                if (cutoutCornerRadius > maxCutoutRadius)
                {
                    cutoutCornerRadius = maxCutoutRadius;
                    warnings.Add("frontCutoutCornerRadius was clamped to half of the smaller front cutout dimension.");
                }

                if (args.FrontCutoutDepth > args.FrontDepth)
                {
                    warnings.Add("frontCutoutDepth was clamped to frontDepth.");
                }
            }

            Mesh mesh = BuildCrtCabinetMesh(
                args.Width,
                args.Height,
                args.FrontDepth,
                args.RearDepth,
                args.BackInsetX,
                args.BackInsetTop,
                args.BackInsetBottom,
                args.FrontCutoutWidth,
                args.FrontCutoutHeight,
                args.FrontCutoutDepth,
                args.FrontCutoutLipDepth,
                args.FrontCutoutX,
                args.FrontCutoutY,
                cutoutCornerRadius,
                args.FrontCutoutBackInsetX,
                args.FrontCutoutBackInsetTop,
                args.FrontCutoutBackInsetBottom,
                args.FrontCutoutBackCornerRadiusOffset,
                cornerRadius,
                cornerSegments);
            AssignManagedMesh(target, mesh);

            Undo.RecordObject(target.transform, "FrameAngel CRT cabinet transform");
            target.SetActive(args.Active);
            target.transform.localPosition = args.Position != null ? args.Position.ToVector3() : Vector3.zero;
            target.transform.localRotation = Quaternion.Euler(args.RotationEuler != null ? args.RotationEuler.ToVector3() : Vector3.zero);
            target.transform.localScale = Vector3.one;

            if (!string.IsNullOrWhiteSpace(args.ColorHex))
            {
                warnings.Add("colorHex is accepted for future compatibility; use scene.material_style_upsert for the visible cabinet finish.");
            }

            Selection.activeGameObject = target;
            MarkActiveSceneDirty();

            return new UnityBridgeResponse
            {
                RequestId = request.RequestId,
                Data = new Dictionary<string, object>
                {
                    { "objectState", BuildWorkspaceObjectState(target) },
                    { "workspaceState", GetWorkspaceState() }
                },
                Warnings = warnings
            };
        }

        public static UnityBridgeResponse SeatShellUpsert(UnityBridgeCommandRequest request)
        {
            if (!CanMutateWorkspace(request, out UnityBridgeResponse rejection))
            {
                return rejection;
            }

            UnitySeatShellUpsertArgs args = request.Args != null
                ? request.Args.ToObject<UnitySeatShellUpsertArgs>()
                : new UnitySeatShellUpsertArgs();

            if (!TryValidateObjectId(args.ObjectId, out string objectIdError))
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", objectIdError, request.RequestId);
            }

            if (args.SeatWidth <= 0f || args.SeatBackHeight <= 0f || args.SeatBackDepth <= 0f || args.SeatPanDepth <= 0f || args.SeatPanThickness <= 0f)
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "seatWidth, seatBackHeight, seatBackDepth, seatPanDepth, and seatPanThickness must all be greater than zero.", request.RequestId);
            }

            if (args.HeadrestHeight < 0f || args.HeadrestInset < 0f || args.LumbarDepth < 0f || args.SideBolsterDepth < 0f || args.ScreenBayDepth < 0f)
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "headrestHeight, headrestInset, lumbarDepth, sideBolsterDepth, and screenBayDepth must be zero or greater.", request.RequestId);
            }

            GameObject targetRoot = GetTargetRoot(EnsureWorkspaceRoot(), true);
            GameObject target = FindManagedObject(targetRoot, args.ObjectId);
            if (target != null && !KindMatches(target, "seat_shell") && target.transform.childCount > 0)
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "Managed workspace object '" + args.ObjectId + "' cannot change kind while it still owns child objects.", request.RequestId);
            }

            Transform parentTransform = ResolveParentTransform(targetRoot, args.ParentObjectId, args.ObjectId, target, request.RequestId, out UnityBridgeResponse parentError);
            if (parentError != null)
            {
                return parentError;
            }

            if (target != null && !KindMatches(target, "seat_shell"))
            {
                int siblingIndex = target.transform.GetSiblingIndex();
                Undo.DestroyObjectImmediate(target);
                target = CreateManagedMeshObject(parentTransform, args.ObjectId, "seat_shell");
                target.transform.SetSiblingIndex(siblingIndex);
            }

            if (target == null)
            {
                target = CreateManagedMeshObject(parentTransform, args.ObjectId, "seat_shell");
            }
            else if (target.transform.parent != parentTransform)
            {
                Undo.SetTransformParent(target.transform, parentTransform, "FrameAngel seat shell parent");
            }

            List<string> warnings = new List<string>();
            int segments = Mathf.Clamp(args.Segments, 2, 32);
            float maxCornerRadius = Mathf.Min(args.SeatWidth, args.SeatBackHeight) * 0.5f;
            float cornerRadius = Mathf.Clamp(args.CornerRadius, 0f, maxCornerRadius);
            if (args.CornerRadius > maxCornerRadius)
            {
                warnings.Add("cornerRadius was clamped to half of the smaller seat shell dimension.");
            }

            Mesh mesh = BuildSeatShellMesh(
                args.SeatWidth,
                args.SeatBackHeight,
                args.SeatBackDepth,
                args.SeatPanDepth,
                args.SeatPanThickness,
                args.HeadrestHeight,
                args.HeadrestInset,
                args.ShoulderWidth,
                args.LumbarDepth,
                args.SideBolsterDepth,
                args.SeatPanAngle,
                args.SeatBackAngle,
                args.ScreenBayWidth,
                args.ScreenBayHeight,
                args.ScreenBayDepth,
                args.ScreenBayOffsetY,
                args.ScreenBayCornerRadius,
                cornerRadius,
                segments);
            AssignManagedMesh(target, mesh);

            Undo.RecordObject(target.transform, "FrameAngel seat shell transform");
            target.SetActive(args.Active);
            target.transform.localPosition = args.Position != null ? args.Position.ToVector3() : Vector3.zero;
            target.transform.localRotation = Quaternion.Euler(args.RotationEuler != null ? args.RotationEuler.ToVector3() : Vector3.zero);
            target.transform.localScale = Vector3.one;

            if (!string.IsNullOrWhiteSpace(args.ColorHex))
            {
                warnings.Add("colorHex is accepted for future compatibility; use scene.material_style_upsert for visible seat-shell finishes.");
            }

            Selection.activeGameObject = target;
            MarkActiveSceneDirty();

            return new UnityBridgeResponse
            {
                RequestId = request.RequestId,
                Data = new Dictionary<string, object>
                {
                    { "objectState", BuildWorkspaceObjectState(target) },
                    { "workspaceState", GetWorkspaceState() }
                },
                Warnings = warnings
            };
        }

        public static UnityBridgeResponse ArmrestUpsert(UnityBridgeCommandRequest request)
        {
            if (!CanMutateWorkspace(request, out UnityBridgeResponse rejection))
            {
                return rejection;
            }

            UnityArmrestUpsertArgs args = request.Args != null
                ? request.Args.ToObject<UnityArmrestUpsertArgs>()
                : new UnityArmrestUpsertArgs();

            if (!TryValidateObjectId(args.ObjectId, out string objectIdError))
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", objectIdError, request.RequestId);
            }

            if (args.Length <= 0f || args.Thickness <= 0f || args.BodyHeight <= 0f)
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "length, thickness, and bodyHeight must all be greater than zero.", request.RequestId);
            }

            if (args.FrontNoseRadius <= 0f || args.RearPivotRadius <= 0f)
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "frontNoseRadius and rearPivotRadius must both be greater than zero.", request.RequestId);
            }

            if (args.UndersideSag < 0f)
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "undersideSag must be zero or greater.", request.RequestId);
            }

            GameObject targetRoot = GetTargetRoot(EnsureWorkspaceRoot(), true);
            GameObject target = FindManagedObject(targetRoot, args.ObjectId);
            if (target != null && !KindMatches(target, "armrest") && target.transform.childCount > 0)
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "Managed workspace object '" + args.ObjectId + "' cannot change kind while it still owns child objects.", request.RequestId);
            }

            Transform parentTransform = ResolveParentTransform(targetRoot, args.ParentObjectId, args.ObjectId, target, request.RequestId, out UnityBridgeResponse parentError);
            if (parentError != null)
            {
                return parentError;
            }

            if (target != null && !KindMatches(target, "armrest"))
            {
                int siblingIndex = target.transform.GetSiblingIndex();
                Undo.DestroyObjectImmediate(target);
                target = CreateManagedMeshObject(parentTransform, args.ObjectId, "armrest");
                target.transform.SetSiblingIndex(siblingIndex);
            }

            if (target == null)
            {
                target = CreateManagedMeshObject(parentTransform, args.ObjectId, "armrest");
            }
            else if (target.transform.parent != parentTransform)
            {
                Undo.SetTransformParent(target.transform, parentTransform, "FrameAngel armrest parent");
            }

            List<string> warnings = new List<string>();
            int segments = Mathf.Clamp(args.Segments, 4, 32);
            float maxFrontNoseRadius = Mathf.Min(args.BodyHeight * 0.49f, args.Length * 0.22f);
            float frontNoseRadius = Mathf.Clamp(args.FrontNoseRadius, args.BodyHeight * 0.16f, maxFrontNoseRadius);
            float maxRearPivotRadius = Mathf.Min(args.BodyHeight * 1.10f, args.Length * 0.22f);
            float rearPivotRadius = Mathf.Clamp(args.RearPivotRadius, args.BodyHeight * 0.30f, maxRearPivotRadius);
            float undersideSag = Mathf.Clamp(args.UndersideSag, 0f, args.BodyHeight * 0.45f);
            if (!Mathf.Approximately(frontNoseRadius, args.FrontNoseRadius))
            {
                warnings.Add("frontNoseRadius was clamped to fit the armrest body.");
            }

            if (!Mathf.Approximately(rearPivotRadius, args.RearPivotRadius))
            {
                warnings.Add("rearPivotRadius was clamped to fit the armrest body.");
            }

            Mesh mesh = BuildArmrestMesh(
                args.Length,
                args.Thickness,
                args.BodyHeight,
                frontNoseRadius,
                rearPivotRadius,
                undersideSag,
                segments);
            AssignManagedMesh(target, mesh);

            Undo.RecordObject(target.transform, "FrameAngel armrest transform");
            target.SetActive(args.Active);
            target.transform.localPosition = args.Position != null ? args.Position.ToVector3() : Vector3.zero;
            target.transform.localRotation = Quaternion.Euler(args.RotationEuler != null ? args.RotationEuler.ToVector3() : Vector3.zero);
            target.transform.localScale = Vector3.one;

            if (!string.IsNullOrWhiteSpace(args.ColorHex))
            {
                warnings.Add("colorHex is accepted for future compatibility; use scene.material_style_upsert for visible armrest finishes.");
            }

            Selection.activeGameObject = target;
            MarkActiveSceneDirty();

            return new UnityBridgeResponse
            {
                RequestId = request.RequestId,
                Data = new Dictionary<string, object>
                {
                    { "objectState", BuildWorkspaceObjectState(target) },
                    { "workspaceState", GetWorkspaceState() },
                    { "pivotCenterLocal", UnityVector3.FromVector3(Vector3.zero) }
                },
                Warnings = warnings
            };
        }

        public static UnityBridgeResponse DuplicateObject(UnityBridgeCommandRequest request)
        {
            if (!CanMutateWorkspace(request, out UnityBridgeResponse rejection))
            {
                return rejection;
            }

            UnityObjectDuplicateArgs args = request.Args != null
                ? request.Args.ToObject<UnityObjectDuplicateArgs>()
                : new UnityObjectDuplicateArgs();

            if (!TryValidateObjectId(args.SourceObjectId, out string sourceError))
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "sourceObjectId: " + sourceError, request.RequestId);
            }

            if (!TryValidateObjectId(args.TargetObjectId, out string targetError))
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "targetObjectId: " + targetError, request.RequestId);
            }

            if (string.Equals(args.SourceObjectId, args.TargetObjectId, StringComparison.Ordinal))
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "sourceObjectId and targetObjectId must be different.", request.RequestId);
            }

            GameObject targetRoot = GetTargetRoot(EnsureWorkspaceRoot(), true);
            GameObject source = FindManagedObject(targetRoot, args.SourceObjectId);
            if (source == null)
            {
                return UnityBridgeResponse.Error("OBJECT_NOT_FOUND", "Managed workspace object '" + args.SourceObjectId + "' was not found.", request.RequestId);
            }

            if (FindManagedObject(targetRoot, args.TargetObjectId) != null)
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "Managed workspace object '" + args.TargetObjectId + "' already exists.", request.RequestId);
            }

            string sourceKind = "";
            IsManagedWorkspaceObject(source, out _, out sourceKind);

            GameObject clone = UnityEngine.Object.Instantiate(source, source.transform.parent);
            Undo.RegisterCreatedObjectUndo(clone, "FrameAngel object duplicate");
            RetagManagedCloneHierarchy(clone, args.SourceObjectId, args.TargetObjectId);
            CloneManagedMeshesRecursive(clone);

            Selection.activeGameObject = clone;
            MarkActiveSceneDirty();

            return new UnityBridgeResponse
            {
                RequestId = request.RequestId,
                Data = new Dictionary<string, object>
                {
                    { "objectState", BuildWorkspaceObjectState(clone) },
                    { "workspaceState", GetWorkspaceState() }
                }
            };
        }

        public static UnityBridgeResponse DeleteObject(UnityBridgeCommandRequest request)
        {
            if (!CanMutateWorkspace(request, out UnityBridgeResponse rejection))
            {
                return rejection;
            }

            UnityObjectDeleteArgs args = request.Args != null
                ? request.Args.ToObject<UnityObjectDeleteArgs>()
                : new UnityObjectDeleteArgs();

            if (!TryValidateObjectId(args.ObjectId, out string objectIdError))
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", objectIdError, request.RequestId);
            }

            GameObject workspaceRoot = FindWorkspaceRoot();
            GameObject targetRoot = GetTargetRoot(workspaceRoot, false);
            GameObject target = FindManagedObject(targetRoot, args.ObjectId);
            if (target == null)
            {
                return UnityBridgeResponse.Error("OBJECT_NOT_FOUND", "Managed workspace object '" + args.ObjectId + "' was not found.", request.RequestId);
            }

            Undo.DestroyObjectImmediate(target);
            MarkActiveSceneDirty();

            return new UnityBridgeResponse
            {
                RequestId = request.RequestId,
                Data = new Dictionary<string, object>
                {
                    { "deletedObjectId", args.ObjectId },
                    { "workspaceState", GetWorkspaceState() }
                }
            };
        }

        public static UnityBridgeResponse GetObjectState(UnityBridgeCommandRequest request)
        {
            UnityObjectReferenceArgs args = request.Args != null
                ? request.Args.ToObject<UnityObjectReferenceArgs>()
                : new UnityObjectReferenceArgs();
            GameObject target = ResolveObject(args);
            if (target == null)
            {
                return UnityBridgeResponse.Error("OBJECT_NOT_FOUND", "Requested object was not found.", request.RequestId);
            }

            UnityWorkspaceObjectState state = BuildWorkspaceObjectState(target);
            if (state == null)
            {
                state = new UnityWorkspaceObjectState
                {
                    ObjectId = TryGetWorkspaceObjectId(target),
                    Kind = "",
                    InstanceId = target.GetInstanceID(),
                    Path = UnityBridgeInspector.BuildPath(target.transform),
                    ParentObjectId = TryGetWorkspaceObjectId(target.transform.parent != null ? target.transform.parent.gameObject : null),
                    ActiveSelf = target.activeSelf,
                    ActiveInHierarchy = target.activeInHierarchy,
                    LocalPosition = UnityVector3.FromVector3(target.transform.localPosition),
                    LocalRotationEuler = UnityVector3.FromVector3(target.transform.localEulerAngles),
                    LocalScale = UnityVector3.FromVector3(target.transform.localScale),
                    ComponentTypes = UnityBridgeInspector.GetComponentTypes(target)
                };

                if (TryGetWorldBounds(target, out Bounds bounds))
                {
                    state.Bounds = UnityBounds3.FromBounds(bounds);
                }
            }

            return new UnityBridgeResponse
            {
                RequestId = request.RequestId,
                Data = new Dictionary<string, object>
                {
                    { "objectState", state }
                }
            };
        }

        public static GameObject ResolveObject(UnityObjectReferenceArgs args)
        {
            if (args == null)
            {
                return Selection.activeGameObject;
            }

            if (!string.IsNullOrWhiteSpace(args.ObjectId))
            {
                GameObject targetRoot = GetTargetRoot(FindWorkspaceRoot(), false);
                GameObject managed = FindManagedObject(targetRoot, args.ObjectId);
                if (managed != null)
                {
                    return managed;
                }

                if (string.Equals(args.ObjectId, "workspace_root", StringComparison.OrdinalIgnoreCase))
                {
                    return GetTargetRoot(FindWorkspaceRoot(), false);
                }
            }

            return UnityBridgeInspector.ResolveObject(args);
        }

        public static GameObject ResolveCaptureTarget(UnityMulticamCaptureArgs args)
        {
            GameObject target = ResolveObject(new UnityObjectReferenceArgs
            {
                ObjectId = args != null ? args.ObjectId : "",
                Path = args != null ? args.Path : "",
                InstanceId = args != null ? args.InstanceId : null
            });

            if (target != null)
            {
                return target;
            }

            GameObject workspaceTargetRoot = GetTargetRoot(FindWorkspaceRoot(), false);
            if (workspaceTargetRoot != null)
            {
                return workspaceTargetRoot;
            }

            return Selection.activeGameObject;
        }

        public static bool TryGetWorldBounds(GameObject target, out Bounds bounds)
        {
            bounds = new Bounds();
            if (target == null)
            {
                return false;
            }

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return false;
            }

            bool initialized = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return initialized;
        }

        public static string TryGetWorkspaceObjectId(GameObject gameObject)
        {
            return IsManagedWorkspaceObject(gameObject, out string objectId, out _) ? objectId : "";
        }

        public static UnityCanonicalRigSummary BuildCanonicalRigSummary()
        {
            return new UnityCanonicalRigSummary
            {
                RigId = CanonicalRigId,
                Views = new List<string>(CanonicalViews),
                Background = "#ffffff",
                NormalizeToTargetBounds = true
            };
        }

        public static float ResolveCanonicalDistance(Bounds bounds)
        {
            float longestAxis = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            return Mathf.Max(2f, longestAxis * CanonicalDistanceScale);
        }

        public static bool IsManagedWorkspaceObject(GameObject gameObject, out string objectId, out string kind)
        {
            objectId = "";
            kind = "";
            if (gameObject == null || string.IsNullOrEmpty(gameObject.name))
            {
                return false;
            }

            if (!gameObject.name.StartsWith(ManagedObjectPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            string remainder = gameObject.name.Substring(ManagedObjectPrefix.Length);
            string[] parts = remainder.Split(new[] { "__" }, StringSplitOptions.None);
            if (parts.Length != 2)
            {
                return false;
            }

            kind = parts[0];
            objectId = parts[1];
            return !string.IsNullOrEmpty(kind) && !string.IsNullOrEmpty(objectId);
        }

        private static bool CanMutateWorkspace(UnityBridgeCommandRequest request, out UnityBridgeResponse rejection)
        {
            rejection = null;
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                rejection = UnityBridgeResponse.Error(
                    "WORKSPACE_MUTATION_REJECTED",
                    "Workspace mutation commands are disabled while a prefab stage is open. Close prefab mode and use a regular scene workspace.",
                    request.RequestId);
                return false;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                rejection = UnityBridgeResponse.Error("WORKSPACE_MUTATION_REJECTED", "No valid active scene is available for the primitive workspace.", request.RequestId);
                return false;
            }

            return true;
        }

        private static GameObject EnsureWorkspaceRoot()
        {
            GameObject existing = FindWorkspaceRoot();
            if (existing != null)
            {
                return existing;
            }

            GameObject workspaceRoot = new GameObject(WorkspaceRootName);
            Undo.RegisterCreatedObjectUndo(workspaceRoot, "FrameAngel workspace root");

            GameObject targetRoot = new GameObject(TargetRootName);
            Undo.RegisterCreatedObjectUndo(targetRoot, "FrameAngel target root");
            Undo.SetTransformParent(targetRoot.transform, workspaceRoot.transform, "FrameAngel target root parent");
            targetRoot.transform.localPosition = Vector3.zero;
            targetRoot.transform.localRotation = Quaternion.identity;
            targetRoot.transform.localScale = Vector3.one;

            MarkActiveSceneDirty();
            return workspaceRoot;
        }

        private static GameObject FindWorkspaceRoot()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                return null;
            }

            GameObject[] roots = activeScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (string.Equals(roots[i].name, WorkspaceRootName, StringComparison.Ordinal))
                {
                    return roots[i];
                }
            }

            return null;
        }

        private static GameObject GetTargetRoot(GameObject workspaceRoot, bool createIfMissing)
        {
            if (workspaceRoot == null)
            {
                return null;
            }

            Transform existing = workspaceRoot.transform.Find(TargetRootName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            if (!createIfMissing)
            {
                return null;
            }

            GameObject targetRoot = new GameObject(TargetRootName);
            Undo.RegisterCreatedObjectUndo(targetRoot, "FrameAngel target root");
            Undo.SetTransformParent(targetRoot.transform, workspaceRoot.transform, "FrameAngel target root parent");
            targetRoot.transform.localPosition = Vector3.zero;
            targetRoot.transform.localRotation = Quaternion.identity;
            targetRoot.transform.localScale = Vector3.one;
            return targetRoot;
        }

        private static GameObject FindManagedObject(GameObject targetRoot, string objectId)
        {
            if (targetRoot == null || string.IsNullOrWhiteSpace(objectId))
            {
                return null;
            }

            for (int i = 0; i < targetRoot.transform.childCount; i++)
            {
                GameObject child = targetRoot.transform.GetChild(i).gameObject;
                if (IsManagedWorkspaceObject(child, out string childObjectId, out _) &&
                    string.Equals(childObjectId, objectId, StringComparison.Ordinal))
                {
                    return child;
                }

                GameObject nested = FindManagedObject(child, objectId);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static GameObject ResolveManagedAuthoringTarget(
            GameObject targetRoot,
            string objectId,
            bool required,
            string fieldName,
            string requestId,
            out UnityBridgeResponse error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(objectId))
            {
                if (required)
                {
                    error = UnityBridgeResponse.Error("BAD_REQUEST", fieldName + " is required.", requestId);
                }

                return null;
            }

            GameObject target = FindManagedObject(targetRoot, objectId.Trim());
            if (target == null)
            {
                error = UnityBridgeResponse.Error("OBJECT_NOT_FOUND", "Managed workspace object '" + objectId.Trim() + "' was not found for " + fieldName + ".", requestId);
            }

            return target;
        }

        private static UnityWorkspaceObjectState BuildWorkspaceObjectState(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return null;
            }

            string kind = "";
            string objectId = "";
            if (!IsManagedWorkspaceObject(gameObject, out objectId, out kind))
            {
                return null;
            }

            UnityWorkspaceObjectState state = new UnityWorkspaceObjectState
            {
                ObjectId = objectId,
                Kind = kind,
                InstanceId = gameObject.GetInstanceID(),
                Path = UnityBridgeInspector.BuildPath(gameObject.transform),
                ParentObjectId = TryGetWorkspaceObjectId(gameObject.transform.parent != null ? gameObject.transform.parent.gameObject : null),
                ActiveSelf = gameObject.activeSelf,
                ActiveInHierarchy = gameObject.activeInHierarchy,
                LocalPosition = UnityVector3.FromVector3(gameObject.transform.localPosition),
                LocalRotationEuler = UnityVector3.FromVector3(gameObject.transform.localEulerAngles),
                LocalScale = UnityVector3.FromVector3(gameObject.transform.localScale),
                ComponentTypes = UnityBridgeInspector.GetComponentTypes(gameObject)
            };

            if (TryGetWorldBounds(gameObject, out Bounds bounds))
            {
                state.Bounds = UnityBounds3.FromBounds(bounds);
            }

            ParticleSystem particleSystem = gameObject.GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                state.ParticleSystem = BuildParticleSystemState(gameObject, particleSystem);
            }

            return state;
        }

        private static bool KindMatches(GameObject target, string expectedKind)
        {
            return IsManagedWorkspaceObject(target, out _, out string actualKind) &&
                string.Equals(actualKind, expectedKind, StringComparison.OrdinalIgnoreCase);
        }

        private static GameObject CreateManagedPrimitive(Transform parent, PrimitiveType primitiveType, string objectId, string kind)
        {
            GameObject gameObject = GameObject.CreatePrimitive(primitiveType);
            gameObject.name = BuildManagedObjectName(objectId, kind);
            Undo.RegisterCreatedObjectUndo(gameObject, "FrameAngel primitive create");
            Undo.SetTransformParent(gameObject.transform, parent, "FrameAngel primitive parent");
            gameObject.transform.localPosition = Vector3.zero;
            gameObject.transform.localRotation = Quaternion.identity;
            gameObject.transform.localScale = Vector3.one;
            return gameObject;
        }

        private static GameObject CreateManagedParticleSystemObject(Transform parent, string objectId)
        {
            GameObject gameObject = new GameObject(BuildManagedObjectName(objectId, "particle_system"));
            Undo.RegisterCreatedObjectUndo(gameObject, "FrameAngel particle system create");
            Undo.SetTransformParent(gameObject.transform, parent, "FrameAngel particle system parent");
            gameObject.transform.localPosition = Vector3.zero;
            gameObject.transform.localRotation = Quaternion.identity;
            gameObject.transform.localScale = Vector3.one;
            EnsureManagedParticleSystemComponents(gameObject);
            return gameObject;
        }

        private static GameObject CreateManagedSpektrLightningObject(Transform parent, string objectId)
        {
            GameObject gameObject = new GameObject(BuildManagedObjectName(objectId, "spektr_lightning"));
            Undo.RegisterCreatedObjectUndo(gameObject, "FrameAngel Spektr lightning create");
            Undo.SetTransformParent(gameObject.transform, parent, "FrameAngel Spektr lightning parent");
            gameObject.transform.localPosition = Vector3.zero;
            gameObject.transform.localRotation = Quaternion.identity;
            gameObject.transform.localScale = Vector3.one;
            return gameObject;
        }

        private static GameObject CreateManagedMeshObject(Transform parent, string objectId, string kind)
        {
            GameObject gameObject = new GameObject(BuildManagedObjectName(objectId, kind));
            Undo.RegisterCreatedObjectUndo(gameObject, "FrameAngel mesh object create");
            Undo.SetTransformParent(gameObject.transform, parent, "FrameAngel mesh object parent");
            gameObject.transform.localPosition = Vector3.zero;
            gameObject.transform.localRotation = Quaternion.identity;
            gameObject.transform.localScale = Vector3.one;
            EnsureManagedMeshComponents(gameObject);
            return gameObject;
        }

        private static GameObject CreateManagedGroupRoot(Transform parent, string objectId)
        {
            GameObject gameObject = new GameObject(BuildManagedObjectName(objectId, "group_root"));
            Undo.RegisterCreatedObjectUndo(gameObject, "FrameAngel group root create");
            Undo.SetTransformParent(gameObject.transform, parent, "FrameAngel group root parent");
            gameObject.transform.localPosition = Vector3.zero;
            gameObject.transform.localRotation = Quaternion.identity;
            gameObject.transform.localScale = Vector3.one;
            return gameObject;
        }

        private static string BuildManagedObjectName(string objectId, string kind)
        {
            return ManagedObjectPrefix + kind.ToLowerInvariant() + "__" + objectId;
        }

        private static bool TryValidateObjectId(string objectId, out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(objectId))
            {
                error = "objectId is required.";
                return false;
            }

            if (objectId.Contains("/") || objectId.Contains("\\") || objectId.Contains("__") || objectId.Contains(" "))
            {
                error = "objectId must not contain slashes, spaces, or '__'.";
                return false;
            }

            for (int i = 0; i < objectId.Length; i++)
            {
                char value = objectId[i];
                bool allowed =
                    (value >= 'a' && value <= 'z') ||
                    (value >= 'A' && value <= 'Z') ||
                    (value >= '0' && value <= '9') ||
                    value == '_' ||
                    value == '-' ||
                    value == '.';

                if (!allowed)
                {
                    error = "objectId may only contain letters, digits, '_', '-', or '.'.";
                    return false;
                }
            }

            return true;
        }

        private static Transform ResolveParentTransform(GameObject targetRoot, string parentObjectId, string objectId, GameObject existingObject, string requestId, out UnityBridgeResponse error)
        {
            error = null;
            if (targetRoot == null)
            {
                error = UnityBridgeResponse.Error("WORKSPACE_MUTATION_REJECTED", "Workspace target root is not available.", requestId);
                return null;
            }

            if (string.IsNullOrWhiteSpace(parentObjectId) || string.Equals(parentObjectId, "workspace_root", StringComparison.OrdinalIgnoreCase))
            {
                return targetRoot.transform;
            }

            if (string.Equals(parentObjectId, objectId, StringComparison.Ordinal))
            {
                error = UnityBridgeResponse.Error("BAD_REQUEST", "parentObjectId cannot be the same as objectId.", requestId);
                return null;
            }

            GameObject parent = FindManagedObject(targetRoot, parentObjectId);
            if (parent == null)
            {
                error = UnityBridgeResponse.Error("OBJECT_NOT_FOUND", "Managed workspace parent '" + parentObjectId + "' was not found.", requestId);
                return null;
            }

            if (existingObject != null && parent.transform.IsChildOf(existingObject.transform))
            {
                error = UnityBridgeResponse.Error("BAD_REQUEST", "parentObjectId cannot point at a descendant of the target object.", requestId);
                return null;
            }

            return parent.transform;
        }

        private static void CollectManagedObjects(Transform root, List<UnityWorkspaceObjectState> managedObjects)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                UnityWorkspaceObjectState state = BuildWorkspaceObjectState(child.gameObject);
                if (state != null)
                {
                    managedObjects.Add(state);
                }

                CollectManagedObjects(child, managedObjects);
            }
        }

        private static string GetManagedKind(GameObject gameObject)
        {
            return IsManagedWorkspaceObject(gameObject, out _, out string kind) ? kind : "";
        }

        private static bool TryMapPrimitiveType(string kind, out PrimitiveType primitiveType)
        {
            primitiveType = PrimitiveType.Cube;
            switch ((kind ?? "").Trim().ToLowerInvariant())
            {
                case "cube":
                    primitiveType = PrimitiveType.Cube;
                    return true;
                case "sphere":
                    primitiveType = PrimitiveType.Sphere;
                    return true;
                case "capsule":
                    primitiveType = PrimitiveType.Capsule;
                    return true;
                case "cylinder":
                    primitiveType = PrimitiveType.Cylinder;
                    return true;
                case "plane":
                    primitiveType = PrimitiveType.Plane;
                    return true;
                case "quad":
                    primitiveType = PrimitiveType.Quad;
                    return true;
                default:
                    return false;
            }
        }

        private static void EnsureManagedMeshComponents(GameObject gameObject)
        {
            if (gameObject.GetComponent<MeshFilter>() == null)
            {
                gameObject.AddComponent<MeshFilter>();
            }

            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = gameObject.AddComponent<MeshRenderer>();
            }

            if (renderer.sharedMaterial == null)
            {
                renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
            }

            if (gameObject.GetComponent<BoxCollider>() == null)
            {
                gameObject.AddComponent<BoxCollider>();
            }
        }

        private static void EnsureManagedParticleSystemComponents(GameObject gameObject)
        {
            if (gameObject.GetComponent<ParticleSystem>() == null)
            {
                gameObject.AddComponent<ParticleSystem>();
            }

            if (gameObject.GetComponent<ParticleSystemRenderer>() == null)
            {
                gameObject.AddComponent<ParticleSystemRenderer>();
            }
        }

        private static UnityParticleSystemState BuildParticleSystemState(GameObject gameObject, ParticleSystem particleSystem)
        {
            if (gameObject == null || particleSystem == null)
            {
                return null;
            }

            ParticleSystemRenderer renderer = gameObject.GetComponent<ParticleSystemRenderer>();
            Material material = renderer != null ? renderer.sharedMaterial : null;
            Material trailMaterial = renderer != null ? renderer.trailMaterial : null;
            Texture mainTexture = ResolveMaterialMainTexture(material);
            Texture trailTexture = ResolveMaterialMainTexture(trailMaterial);
            ParticleSystem.MainModule main = particleSystem.main;
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            ParticleSystem.TrailModule trails = particleSystem.trails;

            return new UnityParticleSystemState
            {
                Duration = main.duration,
                Looping = main.loop,
                MaxParticles = main.maxParticles,
                StartLifetime = main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants
                    ? Mathf.Max(main.startLifetime.constantMin, main.startLifetime.constantMax)
                    : main.startLifetime.constant,
                StartSpeed = main.startSpeed.mode == ParticleSystemCurveMode.TwoConstants
                    ? Mathf.Max(main.startSpeed.constantMin, main.startSpeed.constantMax)
                    : main.startSpeed.constant,
                StartSize = main.startSize.mode == ParticleSystemCurveMode.TwoConstants
                    ? Mathf.Max(main.startSize.constantMin, main.startSize.constantMax)
                    : main.startSize.constant,
                StartColorHex = "#" + ColorUtility.ToHtmlStringRGBA(main.startColor.color),
                EmissionRate = emission.rateOverTime.constant,
                SimulationSpace = main.simulationSpace == ParticleSystemSimulationSpace.World ? "world" : "local",
                Shape = MapShapeTypeName(shape.shapeType),
                RenderMode = MapRenderModeName(renderer != null ? renderer.renderMode : ParticleSystemRenderMode.Billboard),
                MaterialName = material != null ? material.name : "",
                MaterialShader = material != null && material.shader != null ? material.shader.name : "",
                MaterialAssetPath = material != null ? AssetDatabase.GetAssetPath(material) ?? "" : "",
                TextureAssetPath = mainTexture != null ? AssetDatabase.GetAssetPath(mainTexture) ?? "" : "",
                MaterialBlendMode = InferParticleBlendMode(material),
                TrailsEnabled = trails.enabled,
                TrailMaterialName = trailMaterial != null ? trailMaterial.name : "",
                TrailMaterialShader = trailMaterial != null && trailMaterial.shader != null ? trailMaterial.shader.name : "",
                TrailMaterialAssetPath = trailMaterial != null ? AssetDatabase.GetAssetPath(trailMaterial) ?? "" : "",
                TrailTextureAssetPath = trailTexture != null ? AssetDatabase.GetAssetPath(trailTexture) ?? "" : "",
                TrailMaterialBlendMode = InferParticleBlendMode(trailMaterial)
            };
        }

        private static void ApplyParticleSystemSettings(GameObject target, UnityParticleSystemUpsertArgs args, List<string> warnings)
        {
            EnsureManagedParticleSystemComponents(target);
            ParticleSystem particleSystem = target.GetComponent<ParticleSystem>();
            ParticleSystemRenderer renderer = target.GetComponent<ParticleSystemRenderer>();

            ParticleSystem.MainModule main = particleSystem.main;
            main.duration = Mathf.Max(0.01f, args.Duration);
            main.loop = args.Looping;
            main.maxParticles = Mathf.Max(1, args.MaxParticles);
            main.startLifetime = BuildRandomizedCurve(args.StartLifetime, args.StartLifetimeRandomness);
            main.startSpeed = BuildRandomizedCurve(args.StartSpeed, args.StartSpeedRandomness);
            main.startSize = BuildRandomizedCurve(args.StartSize, args.StartSizeRandomness);
            main.startColor = ParseColorOrDefault(args.StartColorHex, Color.white);
            main.gravityModifier = Mathf.Max(0f, args.GravityModifier);
            main.simulationSpace = MapSimulationSpace(args.SimulationSpace);
            main.playOnAwake = true;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = Mathf.Max(0f, args.EmissionRate);
            if (args.BurstCount > 0)
            {
                ParticleSystem.Burst burst = new ParticleSystem.Burst(
                    Mathf.Max(0f, args.BurstTime),
                    (short)Mathf.Clamp(args.BurstCount, 1, short.MaxValue));
                emission.SetBursts(new[] { burst }, 1);
            }
            else
            {
                emission.SetBursts(Array.Empty<ParticleSystem.Burst>(), 0);
            }

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = MapShapeType(args.Shape);
            shape.radius = Mathf.Max(0f, args.ShapeRadius);
            shape.angle = Mathf.Max(0f, args.ShapeAngle);
            shape.length = Mathf.Max(0f, args.ShapeLength);

            ParticleSystem.NoiseModule noise = particleSystem.noise;
            noise.enabled = args.NoiseStrength > 0.0001f;
            if (noise.enabled)
            {
                noise.strength = Mathf.Max(0f, args.NoiseStrength);
                noise.separateAxes = false;
            }

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = args.ColorOverLifetimeEnabled;
            if (colorOverLifetime.enabled)
            {
                Gradient gradient = new Gradient();
                Color startColor = ParseColorOrDefault(args.ColorOverLifetimeStartHex, ParseColorOrDefault(args.StartColorHex, Color.white));
                Color endColor = ParseColorOrDefault(args.ColorOverLifetimeEndHex, new Color(startColor.r, startColor.g, startColor.b, 0f));
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(startColor, 0f),
                        new GradientColorKey(endColor, 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(startColor.a, 0f),
                        new GradientAlphaKey(endColor.a, 1f)
                    });
                colorOverLifetime.color = gradient;
            }

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = args.SizeOverLifetimeEnabled;
            if (sizeOverLifetime.enabled)
            {
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                    1f,
                    AnimationCurve.Linear(0f, 1f, 1f, Mathf.Max(0f, args.SizeOverLifetimeEnd)));
            }

            ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = particleSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = args.VelocityOverLifetimeEnabled;
            if (velocityOverLifetime.enabled)
            {
                Vector3 velocity = args.VelocityOverLifetime != null ? args.VelocityOverLifetime.ToVector3() : Vector3.zero;
                velocityOverLifetime.space = MapSimulationSpace(args.SimulationSpace);
                velocityOverLifetime.x = BuildRandomizedCurve(velocity.x, args.VelocityOverLifetimeRandomness);
                velocityOverLifetime.y = BuildRandomizedCurve(velocity.y, args.VelocityOverLifetimeRandomness);
                velocityOverLifetime.z = BuildRandomizedCurve(velocity.z, args.VelocityOverLifetimeRandomness);
            }

            renderer.renderMode = MapRenderMode(args.RenderMode);
            if (renderer.renderMode == ParticleSystemRenderMode.Stretch)
            {
                renderer.lengthScale = 2f;
                renderer.velocityScale = 0.35f;
            }

            ParticleSystem.TrailModule trails = particleSystem.trails;
            trails.enabled = args.TrailsEnabled;
            if (trails.enabled)
            {
                trails.mode = ParticleSystemTrailMode.PerParticle;
                trails.lifetime = Mathf.Max(0.01f, args.TrailLifetime);
                trails.minVertexDistance = Mathf.Max(0.001f, args.TrailMinVertexDistance);
                trails.dieWithParticles = true;
            }

            renderer.sharedMaterial = ResolveParticleMaterial(
                args.ObjectId,
                "main",
                args.MaterialAssetPath,
                args.TextureAssetPath,
                args.MaterialBlendMode,
                args.MaterialPreset,
                args.MaterialColorHex,
                warnings);

            if (trails.enabled)
            {
                renderer.trailMaterial = ResolveParticleMaterial(
                    args.ObjectId,
                    "trail",
                    args.TrailMaterialAssetPath,
                    args.TrailTextureAssetPath,
                    string.IsNullOrWhiteSpace(args.TrailMaterialBlendMode) ? args.MaterialBlendMode : args.TrailMaterialBlendMode,
                    args.MaterialPreset,
                    args.TrailMaterialColorHex,
                    warnings);
            }
            else
            {
                renderer.trailMaterial = null;
            }

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (target.activeInHierarchy)
            {
                particleSystem.Play(true);
            }
        }

        private static bool TryApplySpektrLightningSettings(
            GameObject targetRoot,
            GameObject target,
            UnitySpektrLightningUpsertArgs args,
            List<string> warnings,
            out string error)
        {
            error = "";
            Type rendererType = FindTypeByFullName("Spektr.LightningRenderer");
            if (rendererType == null)
            {
                error = "Spektr.LightningRenderer type was not found in the current Unity project.";
                return false;
            }

            Type meshType = FindTypeByFullName("Spektr.LightningMesh");
            if (meshType == null)
            {
                error = "Spektr.LightningMesh type was not found in the current Unity project.";
                return false;
            }

            Component component = target.GetComponent(rendererType);
            if (component == null)
            {
                component = Undo.AddComponent(target, rendererType);
            }

            UnityEngine.Object meshAsset = AssetDatabase.LoadAssetAtPath(args.MeshAssetPath ?? "", meshType);
            if (meshAsset == null)
            {
                error = "Spektr mesh asset '" + args.MeshAssetPath + "' could not be loaded.";
                return false;
            }

            Shader shaderAsset = AssetDatabase.LoadAssetAtPath<Shader>(args.ShaderAssetPath ?? "");
            if (shaderAsset == null)
            {
                error = "Spektr shader asset '" + args.ShaderAssetPath + "' could not be loaded.";
                return false;
            }

            ApplySpektrMeshShape(meshAsset, args.LineCount, args.VertexCount, warnings);
            TryRebuildSpektrMesh(meshAsset, meshType, warnings);

            SerializedObject serializedObject = new SerializedObject(component);

            Transform emitterTransform = ResolveManagedTransformReference(targetRoot, args.EmitterObjectId);
            Transform receiverTransform = ResolveManagedTransformReference(targetRoot, args.ReceiverObjectId);
            if (!string.IsNullOrWhiteSpace(args.EmitterObjectId) && emitterTransform == null)
            {
                warnings.Add("Emitter object '" + args.EmitterObjectId + "' was not found; using emitterPosition instead.");
            }

            if (!string.IsNullOrWhiteSpace(args.ReceiverObjectId) && receiverTransform == null)
            {
                warnings.Add("Receiver object '" + args.ReceiverObjectId + "' was not found; using receiverPosition instead.");
            }

            SetSerializedObjectReference(serializedObject, "_emitterTransform", emitterTransform);
            SetSerializedVector3(serializedObject, "_emitterPosition", args.EmitterPosition != null ? args.EmitterPosition.ToVector3() : Vector3.left * 0.5f);
            SetSerializedObjectReference(serializedObject, "_receiverTransform", receiverTransform);
            SetSerializedVector3(serializedObject, "_receiverPosition", args.ReceiverPosition != null ? args.ReceiverPosition.ToVector3() : Vector3.right * 0.5f);
            SetSerializedFloat(serializedObject, "_throttle", Mathf.Clamp01(args.Throttle));
            SetSerializedFloat(serializedObject, "_pulseInterval", Mathf.Max(0.02f, args.PulseInterval));
            SetSerializedFloat(serializedObject, "_boltLength", Mathf.Clamp01(args.BoltLength));
            SetSerializedFloat(serializedObject, "_lengthRandomness", Mathf.Clamp01(args.LengthRandomness));
            SetSerializedFloat(serializedObject, "_noiseAmplitude", Mathf.Max(0f, args.NoiseAmplitude));
            SetSerializedFloat(serializedObject, "_noiseFrequency", Mathf.Max(0f, args.NoiseFrequency));
            SetSerializedFloat(serializedObject, "_noiseMotion", Mathf.Max(0f, args.NoiseMotion));
            SetSerializedColor(serializedObject, "_color", ParseColorOrDefault(args.ColorHex, Color.white) * Mathf.Max(0f, args.ColorIntensity));
            SetSerializedObjectReference(serializedObject, "_mesh", meshAsset);
            SetSerializedObjectReference(serializedObject, "_shader", shaderAsset);
            SetSerializedInt(serializedObject, "_randomSeed", args.RandomSeed);

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(target);
            return true;
        }

        private static Material ResolveParticleMaterial(
            string objectId,
            string slot,
            string materialAssetPath,
            string textureAssetPath,
            string blendMode,
            string materialPreset,
            string materialColorHex,
            List<string> warnings)
        {
            if (!string.IsNullOrWhiteSpace(materialAssetPath))
            {
                Material existingMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
                if (existingMaterial != null)
                {
                    return existingMaterial;
                }

                warnings.Add("particle material asset '" + materialAssetPath + "' could not be loaded; using generated material instead.");
            }

            EnsureParticleAssetFolders();
            string assetPath = GeneratedParticleMaterialsRoot + "/" + SanitizeForAssetPath(objectId) + "__" + slot + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            Shader shader = ResolveParticleShader(blendMode, materialPreset);
            if (shader == null)
            {
                shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended") ?? Shader.Find("Particles/Standard Unlit");
            }

            if (material == null)
            {
                Material seed = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
                material = seed != null ? new Material(seed) : new Material(shader);
                material.name = Path.GetFileNameWithoutExtension(assetPath);
                AssetDatabase.CreateAsset(material, assetPath);
            }

            material.shader = shader;
            Texture texture = string.IsNullOrWhiteSpace(textureAssetPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture>(textureAssetPath);
            if (!string.IsNullOrWhiteSpace(textureAssetPath) && texture == null)
            {
                warnings.Add("particle texture asset '" + textureAssetPath + "' could not be loaded.");
            }

            ApplyParticleMaterialProperties(material, texture, blendMode, materialColorHex);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static void ApplyParticleMaterialProperties(Material material, Texture texture, string blendMode, string colorHex)
        {
            if (material == null)
            {
                return;
            }

            Color tint = ParseColorOrDefault(colorHex, Color.white);
            if (material.HasProperty("_TintColor"))
            {
                material.SetColor("_TintColor", tint);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", tint);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", tint);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            string normalizedBlendMode = NormalizeParticleBlendMode(blendMode);

            if (material.HasProperty("_Cutoff"))
            {
                material.SetFloat("_Cutoff", string.Equals(normalizedBlendMode, "alpha_blend", StringComparison.OrdinalIgnoreCase) ? 0.03f : 0f);
            }

            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");

            if (string.Equals(material.shader != null ? material.shader.name : "", "Particles/Standard Unlit", StringComparison.OrdinalIgnoreCase))
            {
                float mode = 2f;
                switch (normalizedBlendMode)
                {
                    case "additive":
                        mode = 4f;
                        break;
                    case "alpha_premultiply":
                        mode = 3f;
                        break;
                    default:
                        mode = 2f;
                        break;
                }

                if (material.HasProperty("_Mode"))
                {
                    material.SetFloat("_Mode", mode);
                }

                if (material.HasProperty("_Surface"))
                {
                    material.SetFloat("_Surface", 1f);
                }

                if (material.HasProperty("_Blend"))
                {
                    material.SetFloat("_Blend", string.Equals(normalizedBlendMode, "additive", StringComparison.OrdinalIgnoreCase) ? 1f : 0f);
                }
            }
        }

        private static ParticleSystem.MinMaxCurve BuildRandomizedCurve(float value, float randomness)
        {
            float magnitude = Mathf.Abs(value);
            float spread = magnitude * Mathf.Max(0f, randomness);
            float min = value >= 0f
                ? Mathf.Max(0f, value - spread)
                : value - spread;
            float max = value >= 0f
                ? Mathf.Max(0f, value + spread)
                : value + spread;
            return Mathf.Approximately(min, max)
                ? new ParticleSystem.MinMaxCurve(value)
                : new ParticleSystem.MinMaxCurve(min, max);
        }

        private static Type FindTypeByFullName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return null;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static Transform ResolveManagedTransformReference(GameObject targetRoot, string objectId)
        {
            if (targetRoot == null || string.IsNullOrWhiteSpace(objectId))
            {
                return null;
            }

            GameObject target = FindManagedObject(targetRoot, objectId);
            return target != null ? target.transform : null;
        }

        private static void TryRebuildSpektrMesh(UnityEngine.Object meshAsset, Type meshType, List<string> warnings)
        {
            if (meshAsset == null || meshType == null)
            {
                return;
            }

            try
            {
                PropertyInfo sharedMeshProperty = meshType.GetProperty("sharedMesh", BindingFlags.Instance | BindingFlags.Public);
                Mesh sharedMesh = sharedMeshProperty != null ? sharedMeshProperty.GetValue(meshAsset, null) as Mesh : null;
                if (sharedMesh != null)
                {
                    return;
                }

                MethodInfo rebuildMethod = meshType.GetMethod("RebuildMesh", BindingFlags.Instance | BindingFlags.Public);
                if (rebuildMethod != null)
                {
                    rebuildMethod.Invoke(meshAsset, null);
                    EditorUtility.SetDirty(meshAsset);
                    AssetDatabase.SaveAssets();
                }
            }
            catch (Exception ex)
            {
                warnings.Add("Spektr mesh rebuild check failed: " + ex.Message);
            }
        }

        private static void ApplySpektrMeshShape(UnityEngine.Object meshAsset, int lineCount, int vertexCount, List<string> warnings)
        {
            if (meshAsset == null)
            {
                return;
            }

            int clampedLineCount = Mathf.Clamp(lineCount, 1, 256);
            int clampedVertexCount = Mathf.Clamp(vertexCount, 2, 256);

            try
            {
                SerializedObject serializedObject = new SerializedObject(meshAsset);
                bool changed = false;

                SerializedProperty lineCountProperty = serializedObject.FindProperty("_lineCount");
                if (lineCountProperty != null && lineCountProperty.intValue != clampedLineCount)
                {
                    lineCountProperty.intValue = clampedLineCount;
                    changed = true;
                }

                SerializedProperty vertexCountProperty = serializedObject.FindProperty("_vertexCount");
                if (vertexCountProperty != null && vertexCountProperty.intValue != clampedVertexCount)
                {
                    vertexCountProperty.intValue = clampedVertexCount;
                    changed = true;
                }

                if (changed)
                {
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(meshAsset);
                    AssetDatabase.SaveAssets();
                }
            }
            catch (Exception ex)
            {
                warnings.Add("Spektr mesh shape update failed: " + ex.Message);
            }
        }

        private static void SetSerializedObjectReference(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void SetSerializedVector3(SerializedObject serializedObject, string propertyName, Vector3 value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.vector3Value = value;
            }
        }

        private static void SetSerializedFloat(SerializedObject serializedObject, string propertyName, float value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static void SetSerializedInt(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetSerializedColor(SerializedObject serializedObject, string propertyName, Color value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.colorValue = value;
            }
        }

        private static ParticleSystemSimulationSpace MapSimulationSpace(string simulationSpace)
        {
            return string.Equals((simulationSpace ?? "").Trim(), "world", StringComparison.OrdinalIgnoreCase)
                ? ParticleSystemSimulationSpace.World
                : ParticleSystemSimulationSpace.Local;
        }

        private static ParticleSystemShapeType MapShapeType(string shape)
        {
            switch ((shape ?? "").Trim().ToLowerInvariant())
            {
                case "circle":
                    return ParticleSystemShapeType.Circle;
                case "sphere":
                    return ParticleSystemShapeType.Sphere;
                case "hemisphere":
                    return ParticleSystemShapeType.Hemisphere;
                default:
                    return ParticleSystemShapeType.Cone;
            }
        }

        private static string MapShapeTypeName(ParticleSystemShapeType shapeType)
        {
            switch (shapeType)
            {
                case ParticleSystemShapeType.Circle:
                    return "circle";
                case ParticleSystemShapeType.Sphere:
                    return "sphere";
                case ParticleSystemShapeType.Hemisphere:
                    return "hemisphere";
                default:
                    return "cone";
            }
        }

        private static ParticleSystemRenderMode MapRenderMode(string renderMode)
        {
            switch ((renderMode ?? "").Trim().ToLowerInvariant())
            {
                case "stretched_billboard":
                    return ParticleSystemRenderMode.Stretch;
                case "horizontal_billboard":
                    return ParticleSystemRenderMode.HorizontalBillboard;
                case "vertical_billboard":
                    return ParticleSystemRenderMode.VerticalBillboard;
                default:
                    return ParticleSystemRenderMode.Billboard;
            }
        }

        private static string MapRenderModeName(ParticleSystemRenderMode renderMode)
        {
            switch (renderMode)
            {
                case ParticleSystemRenderMode.Stretch:
                    return "stretched_billboard";
                case ParticleSystemRenderMode.HorizontalBillboard:
                    return "horizontal_billboard";
                case ParticleSystemRenderMode.VerticalBillboard:
                    return "vertical_billboard";
                default:
                    return "billboard";
            }
        }

        private static Texture ResolveMaterialMainTexture(Material material)
        {
            if (material == null)
            {
                return null;
            }

            if (material.HasProperty("_MainTex"))
            {
                Texture texture = material.GetTexture("_MainTex");
                if (texture != null)
                {
                    return texture;
                }
            }

            return material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
        }

        private static string InferParticleBlendMode(Material material)
        {
            if (material == null || material.shader == null)
            {
                return "";
            }

            string shaderName = material.shader.name ?? "";
            if (shaderName.IndexOf("Additive", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "additive";
            }

            if (shaderName.IndexOf("Premultiply", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "alpha_premultiply";
            }

            if (shaderName.IndexOf("Alpha", StringComparison.OrdinalIgnoreCase) >= 0 ||
                shaderName.IndexOf("Fade", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "alpha_blend";
            }

            return NormalizeParticleBlendMode(material.HasProperty("_Mode") ? material.GetFloat("_Mode").ToString() : "");
        }

        private static string NormalizeParticleBlendMode(string blendMode)
        {
            string normalized = (blendMode ?? "").Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "4":
                case "additive":
                    return "additive";
                case "3":
                case "alpha_premultiply":
                    return "alpha_premultiply";
                case "2":
                case "1":
                case "fade":
                case "alpha":
                case "alpha_blend":
                default:
                    return "alpha_blend";
            }
        }

        private static Shader ResolveParticleShader(string blendMode, string materialPreset)
        {
            string normalizedBlendMode = NormalizeParticleBlendMode(blendMode);
            switch (normalizedBlendMode)
            {
                case "additive":
                    return AssetDatabase.LoadAssetAtPath<Shader>("Assets/FrameAngelGenerated/Shaders/FrameAngelParticlesUnlitAdditive.shader")
                        ?? Shader.Find("FrameAngel/Particles/UnlitAdditive")
                        ?? Shader.Find("Particles/Additive")
                        ?? Shader.Find("Legacy Shaders/Particles/Additive")
                        ?? Shader.Find("Particles/Standard Unlit");
                case "alpha_premultiply":
                    return AssetDatabase.LoadAssetAtPath<Shader>("Assets/FrameAngelGenerated/Shaders/FrameAngelParticlesUnlitAlpha.shader")
                        ?? Shader.Find("FrameAngel/Particles/UnlitAlpha")
                        ?? Shader.Find("Particles/Standard Unlit")
                        ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended");
                default:
                    return AssetDatabase.LoadAssetAtPath<Shader>("Assets/FrameAngelGenerated/Shaders/FrameAngelParticlesUnlitAlpha.shader")
                        ?? Shader.Find("FrameAngel/Particles/UnlitAlpha")
                        ?? Shader.Find("Particles/Alpha Blended")
                        ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended")
                        ?? Shader.Find("Particles/Standard Unlit");
            }
        }

        private static void EnsureParticleAssetFolders()
        {
            EnsureAssetFolder("Assets", "FrameAngelGenerated");
            EnsureAssetFolder(GeneratedAssetsRoot, "ParticleMaterials");
        }

        private static void EnsureAssetFolder(string parent, string child)
        {
            string assetPath = parent.TrimEnd('/') + "/" + child;
            if (!AssetDatabase.IsValidFolder(assetPath))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static string SanitizeForAssetPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "particle";
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] characters = value.ToCharArray();
            for (int i = 0; i < characters.Length; i++)
            {
                if (invalid.Contains(characters[i]))
                {
                    characters[i] = '_';
                }
            }

            return new string(characters).Replace(" ", "_");
        }

        private static Color ParseColorOrDefault(string colorHex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(colorHex))
            {
                return fallback;
            }

            return ColorUtility.TryParseHtmlString(colorHex, out Color parsed) ? parsed : fallback;
        }

        private static void AssignManagedMesh(GameObject target, Mesh mesh)
        {
            EnsureManagedMeshComponents(target);

            MeshFilter filter = target.GetComponent<MeshFilter>();
            if (filter.sharedMesh != null)
            {
                UnityEngine.Object.DestroyImmediate(filter.sharedMesh);
            }

            mesh.name = target.name + "_Mesh";
            filter.sharedMesh = mesh;

            BoxCollider collider = target.GetComponent<BoxCollider>();
            if (collider != null)
            {
                collider.center = mesh.bounds.center;
                collider.size = mesh.bounds.size;
            }
        }

        private static void CloneManagedMeshIfPresent(GameObject target)
        {
            MeshFilter filter = target.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                Mesh clonedMesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
                clonedMesh.name = target.name + "_Mesh";
                filter.sharedMesh = clonedMesh;

                BoxCollider collider = target.GetComponent<BoxCollider>();
                if (collider != null)
                {
                    collider.center = clonedMesh.bounds.center;
                    collider.size = clonedMesh.bounds.size;
                }
            }
        }

        private static void CloneManagedMeshesRecursive(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            CloneManagedMeshIfPresent(target);
            for (int i = 0; i < target.transform.childCount; i++)
            {
                CloneManagedMeshesRecursive(target.transform.GetChild(i).gameObject);
            }
        }

        private static void RetagManagedCloneHierarchy(GameObject root, string sourceObjectId, string targetObjectId)
        {
            if (root == null)
            {
                return;
            }

            if (IsManagedWorkspaceObject(root, out string currentObjectId, out string currentKind))
            {
                string nextObjectId = currentObjectId;
                if (string.Equals(currentObjectId, sourceObjectId, StringComparison.Ordinal))
                {
                    nextObjectId = targetObjectId;
                }
                else if (currentObjectId.StartsWith(sourceObjectId + ".", StringComparison.Ordinal))
                {
                    nextObjectId = targetObjectId + currentObjectId.Substring(sourceObjectId.Length);
                }

                root.name = BuildManagedObjectName(nextObjectId, currentKind);
            }

            for (int i = 0; i < root.transform.childCount; i++)
            {
                RetagManagedCloneHierarchy(root.transform.GetChild(i).gameObject, sourceObjectId, targetObjectId);
            }
        }

        private static Mesh BuildRoundedRectPrismMesh(float width, float height, float depth, float cornerRadius, int cornerSegments)
        {
            float clampedWidth = Mathf.Max(0.001f, width);
            float clampedHeight = Mathf.Max(0.001f, height);
            float clampedDepth = Mathf.Max(0.001f, depth);
            float clampedCornerRadius = Mathf.Clamp(cornerRadius, 0f, Mathf.Min(clampedWidth, clampedHeight) * 0.5f);
            int segments = Mathf.Clamp(cornerSegments, 1, 32);

            List<Vector2> outline = BuildRoundedRectOutline(clampedWidth, clampedHeight, clampedCornerRadius, segments);
            Mesh mesh = new Mesh();
            if (outline.Count >= 3)
            {
                mesh.indexFormat = outline.Count > 250 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            }

            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();
            float halfDepth = clampedDepth * 0.5f;

            int frontStart = vertices.Count;
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 point = outline[i];
                vertices.Add(new Vector3(point.x, point.y, halfDepth));
                normals.Add(Vector3.forward);
                uvs.Add(new Vector2((point.x / clampedWidth) + 0.5f, (point.y / clampedHeight) + 0.5f));
            }

            for (int i = 1; i < outline.Count - 1; i++)
            {
                triangles.Add(frontStart);
                triangles.Add(frontStart + i);
                triangles.Add(frontStart + i + 1);
            }

            int backStart = vertices.Count;
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 point = outline[i];
                vertices.Add(new Vector3(point.x, point.y, -halfDepth));
                normals.Add(Vector3.back);
                uvs.Add(new Vector2(1f - ((point.x / clampedWidth) + 0.5f), (point.y / clampedHeight) + 0.5f));
            }

            for (int i = 1; i < outline.Count - 1; i++)
            {
                triangles.Add(backStart);
                triangles.Add(backStart + i + 1);
                triangles.Add(backStart + i);
            }

            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 current = outline[i];
                Vector2 next = outline[(i + 1) % outline.Count];
                Vector2 edge = next - current;
                Vector3 normal = new Vector3(edge.y, -edge.x, 0f).normalized;
                int sideStart = vertices.Count;

                vertices.Add(new Vector3(current.x, current.y, halfDepth));
                vertices.Add(new Vector3(next.x, next.y, halfDepth));
                vertices.Add(new Vector3(next.x, next.y, -halfDepth));
                vertices.Add(new Vector3(current.x, current.y, -halfDepth));

                normals.Add(normal);
                normals.Add(normal);
                normals.Add(normal);
                normals.Add(normal);

                uvs.Add(new Vector2(0f, 1f));
                uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(0f, 0f));

                // Keep the side-band winding aligned with the outward-facing normals.
                triangles.Add(sideStart);
                triangles.Add(sideStart + 2);
                triangles.Add(sideStart + 1);
                triangles.Add(sideStart);
                triangles.Add(sideStart + 3);
                triangles.Add(sideStart + 2);
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildCrtGlassMesh(float width, float height, float depth, float curveDepth, float cornerRadius, int cornerSegments)
        {
            float clampedDepth = Mathf.Max(0.001f, depth);
            float frontHalfDepth = clampedDepth * 0.5f;
            Mesh mesh = BuildRoundedRectPrismMesh(width, height, clampedDepth, cornerRadius, cornerSegments);
            Vector3[] vertices = mesh.vertices;
            float halfWidth = Mathf.Max(0.001f, width * 0.5f);
            float halfHeight = Mathf.Max(0.001f, height * 0.5f);
            float maxCurveDepth = Mathf.Clamp(curveDepth, 0f, clampedDepth * 1.5f);

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 vertex = vertices[i];
                if (vertex.z < frontHalfDepth - 0.0001f)
                {
                    continue;
                }

                float nx = Mathf.Abs(vertex.x) / halfWidth;
                float ny = Mathf.Abs(vertex.y) / halfHeight;
                float radial = Mathf.Clamp01(Mathf.Sqrt((nx * nx) + (ny * ny)) / Mathf.Sqrt(2f));
                float bulge = (1f - (radial * radial)) * maxCurveDepth;
                vertex.z += bulge;
                vertices[i] = vertex;
            }

            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildCrtCabinetMesh(
            float width,
            float height,
            float frontDepth,
            float rearDepth,
            float backInsetX,
            float backInsetTop,
            float backInsetBottom,
            float frontCutoutWidth,
            float frontCutoutHeight,
            float frontCutoutDepth,
            float frontCutoutLipDepth,
            float frontCutoutX,
            float frontCutoutY,
            float frontCutoutCornerRadius,
            float frontCutoutBackInsetX,
            float frontCutoutBackInsetTop,
            float frontCutoutBackInsetBottom,
            float frontCutoutBackCornerRadiusOffset,
            float cornerRadius,
            int cornerSegments)
        {
            float clampedWidth = Mathf.Max(0.001f, width);
            float clampedHeight = Mathf.Max(0.001f, height);
            float clampedFrontDepth = Mathf.Max(0.001f, frontDepth);
            float clampedRearDepth = Mathf.Max(0.001f, rearDepth);
            float totalDepth = clampedFrontDepth + clampedRearDepth;
            float rearWidth = Mathf.Max(0.001f, clampedWidth - (Mathf.Max(0f, backInsetX) * 2f));
            float rearHeight = Mathf.Max(0.001f, clampedHeight - Mathf.Max(0f, backInsetTop) - Mathf.Max(0f, backInsetBottom));
            float rearCenterY = (Mathf.Max(0f, backInsetBottom) - Mathf.Max(0f, backInsetTop)) * 0.5f;
            float frontCornerRadius = Mathf.Clamp(cornerRadius, 0f, Mathf.Min(clampedWidth, clampedHeight) * 0.5f);
            float rearCornerRadius = Mathf.Clamp(cornerRadius, 0f, Mathf.Min(rearWidth, rearHeight) * 0.5f);
            int segments = Mathf.Clamp(cornerSegments, 1, 32);

            List<Vector2> frontOutline = BuildRoundedRectOutline(clampedWidth, clampedHeight, frontCornerRadius, segments);
            List<Vector2> rearOutline = BuildRoundedRectOutline(rearWidth, rearHeight, rearCornerRadius, segments);
            for (int i = 0; i < rearOutline.Count; i++)
            {
                Vector2 point = rearOutline[i];
                rearOutline[i] = new Vector2(point.x, point.y + rearCenterY);
            }

            float frontZ = totalDepth * 0.5f;
            float taperStartZ = frontZ - clampedFrontDepth;
            float backZ = -totalDepth * 0.5f;

            Mesh mesh = new Mesh();
            if (frontOutline.Count >= 3)
            {
                mesh.indexFormat = frontOutline.Count > 250 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            }

            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            bool hasFrontCutout = frontCutoutWidth > 0.0001f && frontCutoutHeight > 0.0001f && frontCutoutDepth > 0.0001f;
            if (hasFrontCutout)
            {
                float cutoutWidth = Mathf.Min(frontCutoutWidth, clampedWidth - 0.01f);
                float cutoutHeight = Mathf.Min(frontCutoutHeight, clampedHeight - 0.01f);
                float cutoutDepth = Mathf.Clamp(frontCutoutDepth, 0.001f, clampedFrontDepth);
                float cutoutLipDepth = Mathf.Clamp(frontCutoutLipDepth, 0f, Mathf.Max(0f, cutoutDepth - 0.001f));
                float cutoutCorner = Mathf.Clamp(frontCutoutCornerRadius, 0f, Mathf.Min(cutoutWidth, cutoutHeight) * 0.5f);
                float cutoutCenterX = Mathf.Clamp(frontCutoutX, -(clampedWidth - cutoutWidth) * 0.5f, (clampedWidth - cutoutWidth) * 0.5f);
                float cutoutCenterY = Mathf.Clamp(frontCutoutY, -(clampedHeight - cutoutHeight) * 0.5f, (clampedHeight - cutoutHeight) * 0.5f);
                float cavityBackZ = frontZ - cutoutDepth;
                float lipZ = frontZ - cutoutLipDepth;
                List<Vector2> cutoutOutline = BuildRoundedRectOutline(cutoutWidth, cutoutHeight, cutoutCorner, segments);
                for (int i = 0; i < cutoutOutline.Count; i++)
                {
                    Vector2 point = cutoutOutline[i];
                    cutoutOutline[i] = new Vector2(point.x + cutoutCenterX, point.y + cutoutCenterY);
                }

                float cutoutBackWidth = Mathf.Max(0.001f, cutoutWidth - (Mathf.Max(0f, frontCutoutBackInsetX) * 2f));
                float cutoutBackHeight = Mathf.Max(0.001f, cutoutHeight - Mathf.Max(0f, frontCutoutBackInsetTop) - Mathf.Max(0f, frontCutoutBackInsetBottom));
                float cutoutBackCenterY = cutoutCenterY + ((Mathf.Max(0f, frontCutoutBackInsetBottom) - Mathf.Max(0f, frontCutoutBackInsetTop)) * 0.5f);
                float cutoutBackCorner = Mathf.Clamp(
                    cutoutCorner + frontCutoutBackCornerRadiusOffset,
                    0f,
                    Mathf.Min(cutoutBackWidth, cutoutBackHeight) * 0.5f);
                List<Vector2> cutoutBackOutline = BuildRoundedRectOutline(cutoutBackWidth, cutoutBackHeight, cutoutBackCorner, segments);
                for (int i = 0; i < cutoutBackOutline.Count; i++)
                {
                    Vector2 point = cutoutBackOutline[i];
                    cutoutBackOutline[i] = new Vector2(point.x + cutoutCenterX, point.y + cutoutBackCenterY);
                }

                AddRingCap(vertices, normals, uvs, triangles, frontOutline, cutoutOutline, frontZ, true, clampedWidth, clampedHeight);
                AddBand(vertices, normals, uvs, triangles, frontOutline, frontZ, frontOutline, cavityBackZ);
                if (cutoutLipDepth > 0.0001f)
                {
                    float lipInsetFactor = Mathf.Clamp01(cutoutLipDepth / cutoutDepth);
                    float lipInsetScale = 0.35f + (lipInsetFactor * 0.30f);
                    List<Vector2> BuildInsetOutline(float insetScale)
                    {
                        float outlineWidth = Mathf.Max(0.001f, cutoutWidth - ((Mathf.Max(0f, frontCutoutBackInsetX) * 2f) * insetScale));
                        float outlineHeight = Mathf.Max(0.001f, cutoutHeight - (Mathf.Max(0f, frontCutoutBackInsetTop) + Mathf.Max(0f, frontCutoutBackInsetBottom)) * insetScale);
                        float outlineCenterY = cutoutCenterY + (((Mathf.Max(0f, frontCutoutBackInsetBottom) - Mathf.Max(0f, frontCutoutBackInsetTop)) * 0.5f) * insetScale);
                        float outlineCorner = Mathf.Clamp(
                            cutoutCorner + (frontCutoutBackCornerRadiusOffset * insetScale),
                            0f,
                            Mathf.Min(outlineWidth, outlineHeight) * 0.5f);
                        List<Vector2> outline = BuildRoundedRectOutline(outlineWidth, outlineHeight, outlineCorner, segments);
                        for (int i = 0; i < outline.Count; i++)
                        {
                            Vector2 point = outline[i];
                            outline[i] = new Vector2(point.x + cutoutCenterX, point.y + outlineCenterY);
                        }

                        return outline;
                    }

                    float outerLipZ = Mathf.Lerp(frontZ, lipZ, 0.45f);
                    float outerLipScale = lipInsetScale * 0.45f;
                    List<Vector2> outerLipOutline = BuildInsetOutline(outerLipScale);
                    List<Vector2> innerLipOutline = BuildInsetOutline(lipInsetScale);

                    AddBand(vertices, normals, uvs, triangles, cutoutOutline, frontZ, outerLipOutline, outerLipZ, invertWinding: true);
                    AddBand(vertices, normals, uvs, triangles, outerLipOutline, outerLipZ, innerLipOutline, lipZ, invertWinding: true);
                    AddBand(vertices, normals, uvs, triangles, innerLipOutline, lipZ, cutoutBackOutline, cavityBackZ, invertWinding: true);
                }
                else
                {
                    AddBand(vertices, normals, uvs, triangles, cutoutOutline, frontZ, cutoutBackOutline, cavityBackZ, invertWinding: true);
                }

                AddCap(vertices, normals, uvs, triangles, cutoutBackOutline, cavityBackZ, true, cutoutBackWidth, cutoutBackHeight);

                if (cavityBackZ > taperStartZ + 0.0001f)
                {
                    AddBand(vertices, normals, uvs, triangles, frontOutline, cavityBackZ, frontOutline, taperStartZ);
                }
            }
            else
            {
                AddCap(vertices, normals, uvs, triangles, frontOutline, frontZ, true, clampedWidth, clampedHeight);
                AddBand(vertices, normals, uvs, triangles, frontOutline, frontZ, frontOutline, taperStartZ);
            }

            AddCap(vertices, normals, uvs, triangles, rearOutline, backZ, false, clampedWidth, clampedHeight);
            AddBand(vertices, normals, uvs, triangles, frontOutline, taperStartZ, rearOutline, backZ);

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildSeatShellMesh(
            float seatWidth,
            float seatBackHeight,
            float seatBackDepth,
            float seatPanDepth,
            float seatPanThickness,
            float headrestHeight,
            float headrestInset,
            float shoulderWidth,
            float lumbarDepth,
            float sideBolsterDepth,
            float seatPanAngle,
            float seatBackAngle,
            float screenBayWidth,
            float screenBayHeight,
            float screenBayDepth,
            float screenBayOffsetY,
            float screenBayCornerRadius,
            float cornerRadius,
            int cornerSegments)
        {
            float clampedSeatWidth = Mathf.Max(0.05f, seatWidth);
            float clampedSeatBackHeight = Mathf.Max(0.10f, seatBackHeight);
            float clampedSeatBackDepth = Mathf.Max(0.02f, seatBackDepth);
            float clampedSeatPanDepth = Mathf.Max(0.04f, seatPanDepth);
            float clampedSeatPanThickness = Mathf.Max(0.02f, seatPanThickness);
            float clampedHeadrestHeight = Mathf.Clamp(headrestHeight, 0.04f, clampedSeatBackHeight * 0.45f);
            float clampedShoulderWidth = Mathf.Clamp(shoulderWidth, clampedSeatWidth * 0.58f, clampedSeatWidth);
            float clampedLumbarDepth = Mathf.Clamp(lumbarDepth, 0f, clampedSeatBackDepth * 0.8f);
            float clampedSideBolsterDepth = Mathf.Clamp(sideBolsterDepth, 0f, clampedSeatWidth * 0.16f);
            float clampedScreenBayWidth = Mathf.Clamp(screenBayWidth, clampedSeatWidth * 0.32f, clampedSeatWidth * 0.84f);
            float clampedScreenBayHeight = Mathf.Clamp(screenBayHeight, clampedSeatBackHeight * 0.20f, clampedSeatBackHeight * 0.68f);
            float clampedScreenBayDepth = Mathf.Clamp(screenBayDepth, 0f, clampedSeatBackDepth * 0.75f);
            float clampedScreenBayOffsetY = Mathf.Clamp(screenBayOffsetY, -clampedSeatBackHeight * 0.2f, clampedSeatBackHeight * 0.24f);
            float clampedCornerRadius = Mathf.Clamp(cornerRadius, 0f, Mathf.Min(clampedSeatWidth, clampedSeatBackHeight) * 0.25f);
            float bayCornerRadius = Mathf.Clamp(screenBayCornerRadius, 0f, Mathf.Min(clampedScreenBayWidth, clampedScreenBayHeight) * 0.25f);
            int segments = Mathf.Clamp(cornerSegments, 2, 32);

            float seatPanAngleRadians = Mathf.Deg2Rad * Mathf.Clamp(seatPanAngle, -18f, 18f);
            float seatBackAngleRadians = Mathf.Deg2Rad * Mathf.Clamp(seatBackAngle, -24f, 24f);
            float seatBackBaseY = -0.08f;
            float backTopY = seatBackBaseY + clampedSeatBackHeight;
            float headrestCenterY = backTopY - (clampedHeadrestHeight * 0.5f);
            float upperBackCenterY = seatBackBaseY + (clampedSeatBackHeight * 0.72f);
            float lowerBackCenterY = seatBackBaseY + (clampedSeatBackHeight * 0.36f);
            float panCenterY = seatBackBaseY + (clampedSeatPanThickness * 0.18f);
            float panRise = Mathf.Tan(seatPanAngleRadians) * clampedSeatPanDepth * 0.35f;
            float backLean = Mathf.Tan(seatBackAngleRadians) * clampedSeatBackHeight * 0.18f;
            float rearZ = -(clampedSeatBackDepth * 0.5f) - Mathf.Clamp(headrestInset, 0f, clampedSeatBackDepth * 0.4f) * 0.35f;
            float screenBayZ = rearZ + clampedScreenBayDepth;
            float upperBackZ = -(clampedSeatBackDepth * 0.28f) - backLean;
            float lowerBackZ = -(clampedSeatBackDepth * 0.05f) + (clampedLumbarDepth * 0.35f);
            float panTransitionZ = clampedSeatPanDepth * 0.18f;
            float panMidZ = clampedSeatPanDepth * 0.62f;
            float panFrontZ = clampedSeatPanDepth;

            float backCenterY = seatBackBaseY + (clampedSeatBackHeight * 0.5f);
            float screenBayCenterY = seatBackBaseY + (clampedSeatBackHeight * 0.5f) + clampedScreenBayOffsetY;
            float shellCornerRadius = Mathf.Min(clampedCornerRadius, Mathf.Min(clampedSeatWidth, clampedSeatBackHeight) * 0.2f);
            float bayHeight = Mathf.Min(clampedScreenBayHeight + 0.16f, clampedSeatBackHeight * 0.74f);
            float lowerBackWidth = Mathf.Max(clampedSeatWidth - (clampedSideBolsterDepth * 0.8f), clampedSeatWidth * 0.78f);
            float panWidth = Mathf.Max(clampedSeatWidth - (clampedSideBolsterDepth * 1.2f), clampedSeatWidth * 0.74f);

            List<(List<Vector2> Outline, float Z, float UvWidth, float UvHeight)> sections = new List<(List<Vector2>, float, float, float)>
            {
                (BuildOffsetRoundedRectOutline(clampedShoulderWidth, clampedHeadrestHeight, shellCornerRadius * 0.9f, segments, headrestCenterY), rearZ, clampedSeatWidth, clampedSeatBackHeight),
                (BuildOffsetRoundedRectOutline(Mathf.Max(clampedScreenBayWidth + 0.14f, clampedSeatWidth * 0.72f), bayHeight, Mathf.Max(bayCornerRadius, shellCornerRadius * 0.45f), segments, screenBayCenterY), screenBayZ, clampedSeatWidth, clampedSeatBackHeight),
                (BuildOffsetRoundedRectOutline(clampedShoulderWidth, Mathf.Max(clampedHeadrestHeight + 0.18f, clampedSeatBackHeight * 0.34f), shellCornerRadius, segments, upperBackCenterY), upperBackZ, clampedSeatWidth, clampedSeatBackHeight),
                (BuildOffsetRoundedRectOutline(lowerBackWidth, clampedSeatBackHeight * 0.44f, shellCornerRadius, segments, lowerBackCenterY), lowerBackZ, clampedSeatWidth, clampedSeatBackHeight),
                (BuildOffsetRoundedRectOutline(Mathf.Lerp(lowerBackWidth, panWidth, 0.7f), clampedSeatPanThickness + 0.12f, shellCornerRadius * 0.85f, segments, seatBackBaseY + 0.08f), panTransitionZ, clampedSeatWidth, clampedSeatBackHeight),
                (BuildOffsetRoundedRectOutline(panWidth, clampedSeatPanThickness + 0.02f, shellCornerRadius * 0.75f, segments, panCenterY + (panRise * 0.35f)), panMidZ, clampedSeatWidth, clampedSeatBackHeight),
                (BuildOffsetRoundedRectOutline(Mathf.Max(panWidth - 0.04f, clampedSeatWidth * 0.68f), clampedSeatPanThickness * 0.82f, shellCornerRadius * 0.6f, segments, panCenterY + panRise), panFrontZ, clampedSeatWidth, clampedSeatBackHeight)
            };

            Mesh mesh = new Mesh();
            mesh.indexFormat = sections[0].Outline.Count > 250 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;

            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            AddCap(vertices, normals, uvs, triangles, sections[0].Outline, sections[0].Z, false, sections[0].UvWidth, sections[0].UvHeight);
            for (int i = 0; i < sections.Count - 1; i++)
            {
                AddBand(
                    vertices,
                    normals,
                    uvs,
                    triangles,
                    sections[i].Outline,
                    sections[i].Z,
                    sections[i + 1].Outline,
                    sections[i + 1].Z);
            }

            int lastIndex = sections.Count - 1;
            AddCap(vertices, normals, uvs, triangles, sections[lastIndex].Outline, sections[lastIndex].Z, true, sections[lastIndex].UvWidth, sections[lastIndex].UvHeight);

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildArmrestMesh(
            float length,
            float thickness,
            float bodyHeight,
            float frontNoseRadius,
            float rearPivotRadius,
            float undersideSag,
            int segments)
        {
            float clampedLength = Mathf.Max(0.08f, length);
            float clampedThickness = Mathf.Max(0.01f, thickness);
            float clampedBodyHeight = Mathf.Max(0.03f, bodyHeight);
            float clampedFrontNoseRadius = Mathf.Clamp(frontNoseRadius, clampedBodyHeight * 0.18f, Mathf.Min(clampedBodyHeight * 0.52f, clampedLength * 0.26f));
            float clampedRearPivotRadius = Mathf.Clamp(rearPivotRadius, clampedBodyHeight * 0.42f, Mathf.Min(clampedBodyHeight * 0.95f, clampedLength * 0.24f));
            float clampedUndersideSag = Mathf.Clamp(undersideSag, 0f, clampedBodyHeight * 0.42f);
            int clampedSegments = Mathf.Clamp(segments, 4, 32);

            List<Vector2> profile = BuildArmrestProfile(
                clampedLength,
                clampedBodyHeight,
                clampedFrontNoseRadius,
                clampedRearPivotRadius,
                clampedUndersideSag,
                clampedSegments);

            float halfThickness = clampedThickness * 0.5f;
            float uvWidth = Mathf.Max(0.001f, clampedLength);
            float uvHeight = Mathf.Max(0.001f, Mathf.Max(clampedBodyHeight, clampedRearPivotRadius * 2f));

            Mesh mesh = new Mesh();
            mesh.indexFormat = profile.Count > 200 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;

            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            AddCap(vertices, normals, uvs, triangles, profile, -halfThickness, false, uvWidth, uvHeight);
            AddBand(vertices, normals, uvs, triangles, profile, -halfThickness, profile, halfThickness);
            AddCap(vertices, normals, uvs, triangles, profile, halfThickness, true, uvWidth, uvHeight);

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static List<Vector2> BuildArmrestProfile(
            float length,
            float bodyHeight,
            float frontNoseRadius,
            float rearPivotRadius,
            float undersideSag,
            int segments)
        {
            float frontCenterX = length - frontNoseRadius;
            float frontCenterY = Mathf.Lerp(-bodyHeight * 0.06f, bodyHeight * 0.04f, 0.55f);
            float topLineY = Mathf.Max(frontCenterY + (frontNoseRadius * 0.78f), rearPivotRadius * 0.60f);
            float frontTopAngle = 78f;
            float frontBottomAngle = -70f;
            float rearTopAngle = 74f;
            float rearBottomAngle = 250f;

            Vector2 frontTop = new Vector2(
                frontCenterX + (Mathf.Cos(Mathf.Deg2Rad * frontTopAngle) * frontNoseRadius),
                frontCenterY + (Mathf.Sin(Mathf.Deg2Rad * frontTopAngle) * frontNoseRadius));
            Vector2 frontBottom = new Vector2(
                frontCenterX + (Mathf.Cos(Mathf.Deg2Rad * frontBottomAngle) * frontNoseRadius),
                frontCenterY + (Mathf.Sin(Mathf.Deg2Rad * frontBottomAngle) * frontNoseRadius));
            Vector2 rearTop = new Vector2(
                Mathf.Cos(Mathf.Deg2Rad * rearTopAngle) * rearPivotRadius,
                Mathf.Sin(Mathf.Deg2Rad * rearTopAngle) * rearPivotRadius);
            Vector2 rearBottom = new Vector2(
                Mathf.Cos(Mathf.Deg2Rad * rearBottomAngle) * rearPivotRadius,
                Mathf.Sin(Mathf.Deg2Rad * rearBottomAngle) * rearPivotRadius);

            Vector2 topControl1 = new Vector2(length * 0.58f, topLineY + (bodyHeight * 0.06f));
            Vector2 topControl2 = new Vector2(length * 0.24f, topLineY + (bodyHeight * 0.01f));
            Vector2 undersideControl1 = new Vector2(length * 0.22f, rearBottom.y - undersideSag);
            Vector2 undersideControl2 = new Vector2(length * 0.72f, Mathf.Min(frontBottom.y - (undersideSag * 0.68f), -bodyHeight * 0.34f));

            List<Vector2> profile = new List<Vector2>();
            AppendArc(profile, new Vector2(frontCenterX, frontCenterY), frontNoseRadius, frontBottomAngle, frontTopAngle, segments, true);
            AppendCubicBezier(profile, frontTop, topControl1, topControl2, rearTop, Mathf.Max(3, segments / 2), false);
            AppendArc(profile, Vector2.zero, rearPivotRadius, rearTopAngle, rearBottomAngle, segments, false);
            AppendCubicBezier(profile, rearBottom, undersideControl1, undersideControl2, frontBottom, Mathf.Max(4, segments / 2), false);
            return profile;
        }

        private static void AppendCubicBezier(
            List<Vector2> points,
            Vector2 start,
            Vector2 control1,
            Vector2 control2,
            Vector2 end,
            int segments,
            bool includeStart)
        {
            int segmentCount = Mathf.Max(1, segments);
            for (int i = 0; i <= segmentCount; i++)
            {
                if (!includeStart && i == 0)
                {
                    continue;
                }

                float t = (float)i / segmentCount;
                float oneMinusT = 1f - t;
                Vector2 point =
                    (oneMinusT * oneMinusT * oneMinusT * start) +
                    (3f * oneMinusT * oneMinusT * t * control1) +
                    (3f * oneMinusT * t * t * control2) +
                    (t * t * t * end);
                points.Add(point);
            }
        }

        private static List<Vector2> BuildOffsetRoundedRectOutline(float width, float height, float cornerRadius, int cornerSegments, float centerY)
        {
            List<Vector2> outline = BuildRoundedRectOutline(width, height, cornerRadius, cornerSegments);
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 point = outline[i];
                outline[i] = new Vector2(point.x, point.y + centerY);
            }

            return outline;
        }

        private static List<Vector2> BuildRoundedRectOutline(float width, float height, float cornerRadius, int cornerSegments)
        {
            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;
            float radius = Mathf.Clamp(cornerRadius, 0f, Mathf.Min(width, height) * 0.5f);

            if (radius <= 0.0001f)
            {
                return new List<Vector2>
                {
                    new Vector2(halfWidth, -halfHeight),
                    new Vector2(halfWidth, halfHeight),
                    new Vector2(-halfWidth, halfHeight),
                    new Vector2(-halfWidth, -halfHeight)
                };
            }

            List<Vector2> points = new List<Vector2>();
            AppendArc(points, new Vector2(halfWidth - radius, halfHeight - radius), radius, 0f, 90f, cornerSegments, true);
            AppendArc(points, new Vector2(-halfWidth + radius, halfHeight - radius), radius, 90f, 180f, cornerSegments, false);
            AppendArc(points, new Vector2(-halfWidth + radius, -halfHeight + radius), radius, 180f, 270f, cornerSegments, false);
            AppendArc(points, new Vector2(halfWidth - radius, -halfHeight + radius), radius, 270f, 360f, cornerSegments, false);
            return points;
        }

        private static void AppendArc(List<Vector2> points, Vector2 center, float radius, float startDegrees, float endDegrees, int cornerSegments, bool includeStart)
        {
            int segmentCount = Mathf.Max(1, cornerSegments);
            for (int i = 0; i <= segmentCount; i++)
            {
                if (!includeStart && i == 0)
                {
                    continue;
                }

                float t = segmentCount == 0 ? 0f : (float)i / segmentCount;
                float radians = Mathf.Deg2Rad * Mathf.Lerp(startDegrees, endDegrees, t);
                points.Add(new Vector2(
                    center.x + (Mathf.Cos(radians) * radius),
                    center.y + (Mathf.Sin(radians) * radius)));
            }
        }

        private static void AddCap(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            List<Vector2> outline,
            float z,
            bool frontFacing,
            float uvWidth,
            float uvHeight)
        {
            int start = vertices.Count;
            Vector3 normal = frontFacing ? Vector3.forward : Vector3.back;
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 point = outline[i];
                vertices.Add(new Vector3(point.x, point.y, z));
                normals.Add(normal);
                uvs.Add(new Vector2((point.x / Mathf.Max(0.001f, uvWidth)) + 0.5f, (point.y / Mathf.Max(0.001f, uvHeight)) + 0.5f));
            }

            for (int i = 1; i < outline.Count - 1; i++)
            {
                triangles.Add(start);
                if (frontFacing)
                {
                    triangles.Add(start + i);
                    triangles.Add(start + i + 1);
                }
                else
                {
                    triangles.Add(start + i + 1);
                    triangles.Add(start + i);
                }
            }
        }

        private static void AddRingCap(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            List<Vector2> outerOutline,
            List<Vector2> innerOutline,
            float z,
            bool frontFacing,
            float uvWidth,
            float uvHeight)
        {
            int pointCount = Mathf.Min(outerOutline.Count, innerOutline.Count);
            Vector3 normal = frontFacing ? Vector3.forward : Vector3.back;
            for (int i = 0; i < pointCount; i++)
            {
                Vector2 outerA = outerOutline[i];
                Vector2 outerB = outerOutline[(i + 1) % pointCount];
                Vector2 innerA = innerOutline[i];
                Vector2 innerB = innerOutline[(i + 1) % pointCount];

                int start = vertices.Count;
                vertices.Add(new Vector3(outerA.x, outerA.y, z));
                vertices.Add(new Vector3(outerB.x, outerB.y, z));
                vertices.Add(new Vector3(innerB.x, innerB.y, z));
                vertices.Add(new Vector3(innerA.x, innerA.y, z));

                normals.Add(normal);
                normals.Add(normal);
                normals.Add(normal);
                normals.Add(normal);

                uvs.Add(new Vector2((outerA.x / Mathf.Max(0.001f, uvWidth)) + 0.5f, (outerA.y / Mathf.Max(0.001f, uvHeight)) + 0.5f));
                uvs.Add(new Vector2((outerB.x / Mathf.Max(0.001f, uvWidth)) + 0.5f, (outerB.y / Mathf.Max(0.001f, uvHeight)) + 0.5f));
                uvs.Add(new Vector2((innerB.x / Mathf.Max(0.001f, uvWidth)) + 0.5f, (innerB.y / Mathf.Max(0.001f, uvHeight)) + 0.5f));
                uvs.Add(new Vector2((innerA.x / Mathf.Max(0.001f, uvWidth)) + 0.5f, (innerA.y / Mathf.Max(0.001f, uvHeight)) + 0.5f));

                if (frontFacing)
                {
                    triangles.Add(start);
                    triangles.Add(start + 1);
                    triangles.Add(start + 2);
                    triangles.Add(start);
                    triangles.Add(start + 2);
                    triangles.Add(start + 3);
                }
                else
                {
                    triangles.Add(start);
                    triangles.Add(start + 2);
                    triangles.Add(start + 1);
                    triangles.Add(start);
                    triangles.Add(start + 3);
                    triangles.Add(start + 2);
                }
            }
        }

        private static void AddBand(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            List<Vector2> startOutline,
            float startZ,
            List<Vector2> endOutline,
            float endZ,
            bool invertWinding = false)
        {
            float perimeter = 0f;
            for (int i = 0; i < startOutline.Count; i++)
            {
                perimeter += Vector2.Distance(startOutline[i], startOutline[(i + 1) % startOutline.Count]);
            }

            float accumulated = 0f;
            for (int i = 0; i < startOutline.Count; i++)
            {
                Vector2 startA = startOutline[i];
                Vector2 startB = startOutline[(i + 1) % startOutline.Count];
                Vector2 endA = endOutline[i];
                Vector2 endB = endOutline[(i + 1) % endOutline.Count];
                Vector3 a = new Vector3(startA.x, startA.y, startZ);
                Vector3 b = new Vector3(startB.x, startB.y, startZ);
                Vector3 c = new Vector3(endB.x, endB.y, endZ);
                Vector3 d = new Vector3(endA.x, endA.y, endZ);

                // The rounded outlines are authored counter-clockwise in XY.
                // For the outer cabinet shell, the outward-facing side normal comes
                // from the back-edge vector crossed with the outline tangent.
                Vector3 normal = Vector3.Cross(d - a, b - a).normalized;
                if (invertWinding)
                {
                    normal = -normal;
                }

                if (normal.sqrMagnitude <= 0.000001f)
                {
                    normal = invertWinding ? Vector3.back : Vector3.forward;
                }

                float segmentLength = Vector2.Distance(startA, startB);
                float u0 = perimeter <= 0.0001f ? 0f : accumulated / perimeter;
                float u1 = perimeter <= 0.0001f ? 1f : (accumulated + segmentLength) / perimeter;
                accumulated += segmentLength;

                int sideStart = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                vertices.Add(d);

                normals.Add(normal);
                normals.Add(normal);
                normals.Add(normal);
                normals.Add(normal);

                uvs.Add(new Vector2(u0, 1f));
                uvs.Add(new Vector2(u1, 1f));
                uvs.Add(new Vector2(u1, 0f));
                uvs.Add(new Vector2(u0, 0f));

                if (invertWinding)
                {
                    triangles.Add(sideStart);
                    triangles.Add(sideStart + 1);
                    triangles.Add(sideStart + 2);
                    triangles.Add(sideStart + 2);
                    triangles.Add(sideStart + 3);
                    triangles.Add(sideStart);
                }
                else
                {
                    triangles.Add(sideStart);
                    triangles.Add(sideStart + 2);
                    triangles.Add(sideStart + 1);
                    triangles.Add(sideStart);
                    triangles.Add(sideStart + 3);
                    triangles.Add(sideStart + 2);
                }
            }
        }

        private static void MarkActiveSceneDirty()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
            }
        }
    }
}
