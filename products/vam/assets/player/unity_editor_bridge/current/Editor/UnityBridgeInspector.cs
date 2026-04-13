using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FrameAngel.UnityEditorBridge
{
    internal static class UnityBridgeInspector
    {
        public static string ProjectPath
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); }
        }

        public static string TimestampUtc()
        {
            return DateTime.UtcNow.ToString("o");
        }

        public static UnityBridgeHealthData GetHealth()
        {
            return new UnityBridgeHealthData
            {
                BridgeVersion = UnityBridgeController.BridgeVersion,
                UnityVersion = Application.unityVersion,
                ProjectPath = ProjectPath,
                IsPlaying = EditorApplication.isPlaying,
                IsCompiling = EditorApplication.isCompiling,
                IsUpdating = EditorApplication.isUpdating,
                TimestampUtc = TimestampUtc()
            };
        }

        public static UnityBridgeCapabilitiesData GetCapabilities()
        {
            return new UnityBridgeCapabilitiesData
            {
                HostMode = "unity_editor_http",
                AllowUnsafeApiInvoke = UnityBridgeController.AllowUnsafeApiInvoke,
                SupportsSceneViewCapture = true,
                SupportsGameViewCapture = true,
                SupportsNamedCameraCapture = true,
                SupportsMulticamRigCapture = true,
                SupportsSectionCapture = true,
                SupportsPrimitiveWorkspaceMutation = true,
                SupportsRoundedRectPrism = true,
                SupportsObjectDuplicate = true,
                SupportsGroupRoots = true,
                SupportsCrtGlass = true,
                SupportsCrtCabinet = true,
                SupportsSeatShell = true,
                SupportsArmrest = true,
                SupportsTextureImportLocal = true,
                SupportsMaterialStyle = true,
                SupportsParticleSystem = true,
                SupportsParticleTexturedMaterials = true,
                SupportsParticleReadback = true,
                SupportsSpektrLightning = true,
                SupportsInnerPieceExport = true,
                SupportsPackageRefresh = true,
                SupportsObjectTransformObserve = true,
                CommandGroups = new List<string> { "observe", "capture", "scene", "asset", "bridge" },
                Commands = new List<string>
                {
                    "observe.selection",
                    "observe.scene_context",
                    "observe.prefab_context",
                    "observe.object_children",
                    "observe.object_bounds",
                    "observe.object_transform",
                    "observe.workspace_state",
                    "capture.scene_view",
                    "capture.game_view",
                    "capture.camera",
                    "capture.orbit_view",
                    "capture.section_view",
                    "capture.multicam_rig",
                    "scene.workspace_reset",
                    "scene.group_root_upsert",
                    "scene.primitive_upsert",
                    "scene.rounded_rect_prism_upsert",
                    "scene.player_screen_authoring_upsert",
                    "scene.particle_system_upsert",
                    "scene.spektr_lightning_upsert",
                    "scene.crt_glass_upsert",
                    "scene.crt_cabinet_upsert",
                    "scene.seat_shell_upsert",
                    "scene.armrest_upsert",
                    "scene.object_duplicate",
                    "scene.object_delete",
                    "scene.object_get_state",
                    "asset.texture_import_local",
                    "scene.material_style_upsert",
                    "asset.innerpiece.inspect_selection",
                    "asset.innerpiece.export_selection",
                    "asset.innerpiece.export_project_asset",
                    "asset.innerpiece.capture_preview",
                    "asset.innerpiece.get_last_export",
                    "bridge.refresh_package"
                }
            };
        }

        public static UnityBridgeStateData GetState()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            return new UnityBridgeStateData
            {
                ActiveScene = activeScene.name ?? "",
                SelectionSummary = UnitySelectionSummary.FromGameObject(Selection.activeGameObject),
                PrefabStage = GetPrefabStageSummary(prefabStage),
                LastCapture = UnityBridgeController.LastCapture,
                WorkspaceState = UnityBridgeWorkspaceService.GetWorkspaceState(),
                LastInnerPieceExport = UnityBridgeController.LastInnerPieceExport,
                TimestampUtc = TimestampUtc()
            };
        }

        public static UnitySceneContextData GetSceneContext()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            GameObject[] roots = activeScene.IsValid() ? activeScene.GetRootGameObjects() : Array.Empty<GameObject>();
            return new UnitySceneContextData
            {
                SceneName = activeScene.name ?? "",
                ScenePath = activeScene.path ?? "",
                RootObjectCount = roots.Length,
                RootObjectNames = roots.Select(root => root.name).ToList(),
                CameraNames = GetCameraNames(activeScene),
                HasPrefabStageOpen = PrefabStageUtility.GetCurrentPrefabStage() != null,
                SelectedObjectPath = BuildPath(Selection.activeTransform)
            };
        }

        public static UnityPrefabContextData GetPrefabContext()
        {
            GameObject selection = Selection.activeGameObject;
            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            UnityPrefabContextData data = new UnityPrefabContextData
            {
                HasSelection = selection != null,
                ContextType = "none",
                AssetPath = "",
                RootName = "",
                InstanceStatus = "",
                IsPartOfPrefabContents = false
            };

            if (prefabStage != null)
            {
                data.RootName = prefabStage.prefabContentsRoot != null ? prefabStage.prefabContentsRoot.name : "";
                data.AssetPath = prefabStage.assetPath ?? "";
            }

            if (selection == null)
            {
                if (prefabStage != null)
                {
                    data.ContextType = "prefab_stage";
                }

                return data;
            }

            data.IsPartOfPrefabContents = IsPartOfPrefabContents(selection);
            data.InstanceStatus = PrefabUtility.GetPrefabInstanceStatus(selection).ToString();

            if (data.IsPartOfPrefabContents)
            {
                data.ContextType = "prefab_stage";
                if (string.IsNullOrEmpty(data.RootName))
                {
                    data.RootName = selection.scene.name ?? "";
                }

                return data;
            }

            if (IsPrefabAsset(selection))
            {
                data.ContextType = "prefab_asset";
                data.AssetPath = AssetDatabase.GetAssetPath(selection) ?? "";
                data.RootName = selection.name;
                return data;
            }

            if (IsPrefabInstance(selection))
            {
                data.ContextType = "prefab_instance";
                data.AssetPath = ResolvePrefabAssetPath(selection);
                data.RootName = PrefabUtility.GetNearestPrefabInstanceRoot(selection) != null
                    ? PrefabUtility.GetNearestPrefabInstanceRoot(selection).name
                    : selection.name;
                return data;
            }

            data.ContextType = "scene_object";
            data.RootName = selection.name;
            return data;
        }

        public static List<UnityObjectChildSummary> GetObjectChildren(UnityObjectChildrenArgs args)
        {
            GameObject target = ResolveObject(args);
            if (target == null)
            {
                return new List<UnityObjectChildSummary>();
            }

            List<UnityObjectChildSummary> children = new List<UnityObjectChildSummary>();
            for (int i = 0; i < target.transform.childCount; i++)
            {
                Transform child = target.transform.GetChild(i);
                children.Add(new UnityObjectChildSummary
                {
                    InstanceId = child.gameObject.GetInstanceID(),
                    Name = child.gameObject.name,
                    Path = BuildPath(child),
                    ActiveSelf = child.gameObject.activeSelf,
                    ActiveInHierarchy = child.gameObject.activeInHierarchy,
                    ComponentTypes = GetComponentTypes(child.gameObject)
                });
            }

            return children;
        }

        public static UnityObjectTransformData GetObjectTransform(UnityObjectReferenceArgs args)
        {
            GameObject target = ResolveObject(args);
            if (target == null)
            {
                return null;
            }

            Transform transform = target.transform;
            return new UnityObjectTransformData
            {
                ObjectId = UnityBridgeWorkspaceService.TryGetWorkspaceObjectId(target),
                Path = BuildPath(transform),
                InstanceId = target.GetInstanceID(),
                LocalPosition = UnityVector3.FromVector3(transform.localPosition),
                LocalRotationEuler = UnityVector3.FromVector3(transform.localEulerAngles),
                LocalScale = UnityVector3.FromVector3(transform.localScale),
                WorldPosition = UnityVector3.FromVector3(transform.position),
                WorldRotationEuler = UnityVector3.FromVector3(transform.eulerAngles)
            };
        }

        public static string BuildPath(Transform transform)
        {
            if (transform == null)
            {
                return "";
            }

            List<string> segments = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                segments.Add(current.name);
                current = current.parent;
            }

            segments.Reverse();
            return string.Join("/", segments.ToArray());
        }

        public static List<string> GetComponentTypes(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return new List<string>();
            }

            return gameObject
                .GetComponents<Component>()
                .Select(component => component == null ? "MissingComponent" : component.GetType().Name)
                .ToList();
        }

        public static bool IsPrefabAsset(GameObject gameObject)
        {
            return gameObject != null && PrefabUtility.IsPartOfPrefabAsset(gameObject);
        }

        public static bool IsPrefabInstance(GameObject gameObject)
        {
            return gameObject != null && PrefabUtility.IsPartOfPrefabInstance(gameObject);
        }

        public static bool IsPartOfPrefabContents(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            return prefabStage != null && gameObject.scene == prefabStage.scene;
        }

        public static GameObject ResolveObject(UnityObjectChildrenArgs args)
        {
            if (args == null)
            {
                return Selection.activeGameObject;
            }

            return ResolveObject(new UnityObjectReferenceArgs
            {
                Path = args.Path,
                InstanceId = args.InstanceId
            });
        }

        public static GameObject ResolveObject(UnityObjectReferenceArgs args)
        {
            if (args == null)
            {
                return Selection.activeGameObject;
            }

            if (args.InstanceId.HasValue)
            {
                return EditorUtility.InstanceIDToObject(args.InstanceId.Value) as GameObject;
            }

            if (!string.IsNullOrEmpty(args.Path))
            {
                return ResolveByPath(args.Path);
            }

            return Selection.activeGameObject;
        }

        public static Camera GetCameraByName(string cameraName)
        {
            if (string.IsNullOrWhiteSpace(cameraName))
            {
                return null;
            }

            Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null || camera.gameObject == null)
                {
                    continue;
                }

                if ((camera.hideFlags & HideFlags.HideAndDontSave) != 0)
                {
                    continue;
                }

                if (string.Equals(camera.name, cameraName, StringComparison.OrdinalIgnoreCase))
                {
                    return camera;
                }
            }

            return null;
        }

        private static UnityPrefabStageSummary GetPrefabStageSummary(PrefabStage prefabStage)
        {
            if (prefabStage == null)
            {
                return new UnityPrefabStageSummary
                {
                    IsOpen = false,
                    AssetPath = "",
                    RootName = ""
                };
            }

            return new UnityPrefabStageSummary
            {
                IsOpen = true,
                AssetPath = prefabStage.assetPath ?? "",
                RootName = prefabStage.prefabContentsRoot != null ? prefabStage.prefabContentsRoot.name : ""
            };
        }

        private static List<string> GetCameraNames(Scene activeScene)
        {
            List<string> names = new List<string>();
            Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null || camera.gameObject == null)
                {
                    continue;
                }

                if ((camera.hideFlags & HideFlags.HideAndDontSave) != 0)
                {
                    continue;
                }

                PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage != null)
                {
                    if (camera.gameObject.scene != prefabStage.scene)
                    {
                        continue;
                    }
                }
                else if (camera.gameObject.scene != activeScene)
                {
                    continue;
                }

                names.Add(camera.name);
            }

            return names.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string ResolvePrefabAssetPath(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return "";
            }

            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
            if (source != null)
            {
                return AssetDatabase.GetAssetPath(source) ?? "";
            }

            return "";
        }

        private static GameObject ResolveByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot != null)
            {
                GameObject stageMatch = ResolveByPath(stage.prefabContentsRoot, path);
                if (stageMatch != null)
                {
                    return stageMatch;
                }
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                return null;
            }

            GameObject[] roots = activeScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject match = ResolveByPath(roots[i], path);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static GameObject ResolveByPath(GameObject root, string path)
        {
            if (root == null)
            {
                return null;
            }

            string rootPath = BuildPath(root.transform);
            if (string.Equals(rootPath, path, StringComparison.Ordinal))
            {
                return root;
            }

            for (int i = 0; i < root.transform.childCount; i++)
            {
                GameObject match = ResolveByPath(root.transform.GetChild(i).gameObject, path);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
