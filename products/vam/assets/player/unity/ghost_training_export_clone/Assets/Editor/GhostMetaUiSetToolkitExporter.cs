using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FrameAngel.UnityEditorBridge;
using Oculus.Interaction;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GhostMetaUiSetToolkitExporter
{
    private const string SceneDirectory = "Assets/Scenes";
    private const string ToolkitScenePath = SceneDirectory + "/GhostMetaUiSetToolkitExport.unity";
    private const string VideoPlayerProofScenePath = SceneDirectory + "/GhostMetaUiSetVideoPlayerProof.unity";
    private const string TempSceneSourcesRoot = "Assets/__FrameAngelTemp/MetaToolkitSceneSources";
    private const string PackagePrefabsRoot = "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Objects/UISet/Prefabs";
    private const string PackageThemesRoot = "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Objects/UISet/Themes";
    private const string PackageScenesRoot = "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Objects/UISet/Scenes";
    private const string ToolkitRootName = "GhostMetaUiSetToolkitExportRoot";
    private static readonly string[] UnsupportedSnapshotShaderNames =
    {
        "UI/Prerendered",
        "UI/Prerendered Opaque",
        "URP/UI/Prerendered",
        "URP/UI/Prerendered Opaque"
    };
    private const BindingFlags InstanceMethodFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Serializable]
    private sealed class SurfaceOverride
    {
        public string preferredSurfacePath;
        public float scale = 1f;
        public string tagsSuffix = "";
        public string sceneSourcePath = "";
        public string sceneObjectName = "";
    }

    [Serializable]
    private sealed class ThemeContext
    {
        public int themeIndex;
        public string themeId = "";
        public string themeLabel = "";
        public string themeVariantId = "";
        public string themeAssetPath = "";
        public string themeAssetGuid = "";
    }

    [Serializable]
    private sealed class ToolkitExportRunSummary
    {
        public string schemaVersion = "ghost_meta_ui_toolkit_export_v1";
        public string catalogId = "ghost_meta_ui_toolkit";
        public string generatedAtUtc = "";
        public string projectPath = "";
        public string scenePath = "";
        public int themeIndex;
        public string themeId = "";
        public string themeLabel = "";
        public string themeVariantId = "";
        public string themeAssetPath = "";
        public string themeAssetGuid = "";
        public string surfaceFilter = "";
        public string outputRoot = "";
        public List<ToolkitSurfaceSummary> surfaces = new List<ToolkitSurfaceSummary>();
    }

    [Serializable]
    private sealed class ToolkitSurfaceSummary
    {
        public string controlSurfaceId = "";
        public string exportDisplayName = "";
        public string prefabAssetPath = "";
        public string relativePrefabPath = "";
        public string category = "";
        public string controlFamilyId = "";
        public string exportTagsCsv = "";
        public string defaultTargetDisplayId = "";
        public string themeId = "";
        public string themeLabel = "";
        public string themeVariantId = "";
        public string themeAssetPath = "";
        public string themeAssetGuid = "";
        public int controlElementCount;
        public string packageId = "";
        public string resourceId = "";
        public string packageRootPath = "";
        public string geometryPath = "";
        public string materialsPath = "";
        public string controlsPath = "";
        public string previewPath = "";
        public string exportReceiptPath = "";
        public string fingerprint = "";
    }

    private sealed class ProfileIsolationState
    {
        public GameObject gameObject;
        public bool wasActiveSelf;
    }

    private static readonly string[] ThemeAssetPaths =
    {
        PackageThemesRoot + "/UIThemeQuest_Dark.asset",
        PackageThemesRoot + "/UIThemeQuest_Light.asset",
        PackageThemesRoot + "/UIThemeCustomBrandExample1.asset",
        PackageThemesRoot + "/UIThemeCustomBrandExample2.asset"
    };

    private static readonly Dictionary<string, SurfaceOverride> Overrides =
        new Dictionary<string, SurfaceOverride>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "Patterns/ContentUIExample-VideoPlayer.prefab",
                new SurfaceOverride
                {
                    preferredSurfacePath = "CanvasRoot/Controls",
                    scale = 0.90f,
                    tagsSuffix = "video_player",
                    sceneSourcePath = VideoPlayerProofScenePath,
                    sceneObjectName = "GhostMetaVideoPlayerProof"
                }
            },
            { "Patterns/ContentUIExample-HorizonOS1.prefab", new SurfaceOverride { preferredSurfacePath = "CanvasRoot", scale = 0.90f, tagsSuffix = "content_ui" } },
            { "Patterns/ContentUIExample-HorizonOS2.prefab", new SurfaceOverride { preferredSurfacePath = "CanvasRoot", scale = 0.90f, tagsSuffix = "content_ui" } },
            { "Patterns/ContentUIExample-HorizonOS3.prefab", new SurfaceOverride { preferredSurfacePath = "CanvasRoot", scale = 0.90f, tagsSuffix = "content_ui" } },
            { "Patterns/ContentUIExample1.prefab", new SurfaceOverride { preferredSurfacePath = "CanvasRoot", scale = 0.90f, tagsSuffix = "content_ui" } },
            { "Patterns/ContentUIExample2.prefab", new SurfaceOverride { preferredSurfacePath = "CanvasRoot", scale = 0.90f, tagsSuffix = "content_ui" } },
            { "Patterns/GridMenuExample2x4.prefab", new SurfaceOverride { preferredSurfacePath = "CanvasRoot", scale = 0.95f, tagsSuffix = "grid_menu" } },
            { "Patterns/GridMenuExample3x3.prefab", new SurfaceOverride { preferredSurfacePath = "CanvasRoot", scale = 0.95f, tagsSuffix = "grid_menu" } }
        };

    [MenuItem("FrameAngel/Ghost/Meta UI Set/Open Toolkit Export Scene")]
    public static void OpenToolkitExportScene()
    {
        OpenOrCreateSceneInternal(ToolkitScenePath, CreateToolkitSceneInternal, false);
    }

    [MenuItem("FrameAngel/Ghost/Meta UI Set/Refresh Toolkit Export Scene")]
    public static void RefreshToolkitExportScene()
    {
        OpenOrCreateSceneInternal(ToolkitScenePath, CreateToolkitSceneInternal, true);
    }

    [MenuItem("FrameAngel/Ghost/Meta UI Set/Export Toolkit Catalog")]
    public static void ExportToolkitCatalog()
    {
        ExportToolkitCatalogInternal(true, 0, "", "", true);
    }

    [MenuItem("FrameAngel/Ghost/Meta UI Set/Export Selected Toolkit Surface")]
    public static void ExportSelectedToolkitSurface()
    {
        GhostMetaControlSurfaceExportProfile profile = ResolveProfileFromSelection();
        if (profile == null)
        {
            EditorUtility.DisplayDialog(
                "Export Selected Toolkit Surface",
                "Select an exported toolkit wrapper or a child beneath one first.",
                "OK");
            return;
        }

        GameObject root = GameObject.Find(ToolkitRootName);
        UIThemeManager themeManager = root != null ? root.GetComponent<UIThemeManager>() : null;
        ApplyThemeMetadataToProfile(profile, ResolveThemeContext(themeManager, GetCurrentThemeIndex()));
        ExportProfile(profile, ResolveDefaultOutputRoot(GetCurrentThemeIndex()), true);
    }

    [MenuItem("FrameAngel/Ghost/Meta UI Set/Attach Export Profile To Selection")]
    public static void AttachExportProfileToSelection()
    {
        GameObject selection = Selection.activeGameObject;
        if (selection == null)
        {
            EditorUtility.DisplayDialog(
                "Attach Export Profile",
                "Select a UI root first.",
                "OK");
            return;
        }

        GhostMetaControlSurfaceExportProfile profile = selection.GetComponent<GhostMetaControlSurfaceExportProfile>();
        if (profile == null)
        {
            profile = selection.AddComponent<GhostMetaControlSurfaceExportProfile>();
        }

        RectTransform root = ResolveLargestRectTransform(selection.transform);
        string surfaceId = "meta_custom_" + SanitizeId(selection.name);
        string label = selection.name;
        profile.Configure(
            surfaceId,
            label,
            "meta_ui_custom",
            root,
            "Meta Custom " + label,
            "ghost,meta_ui,custom",
            "player_main",
            new[] { "player_main" });
        profile.surfaceUnitsToMetersMultiplier = 0f;
        profile.AutoConfigureElements();
        Selection.activeGameObject = selection;
    }

    [MenuItem("FrameAngel/Ghost/Meta UI Set/Refresh Export Profile On Selection")]
    public static void RefreshExportProfileOnSelection()
    {
        GhostMetaControlSurfaceExportProfile profile = ResolveProfileFromSelection();
        if (profile == null)
        {
            EditorUtility.DisplayDialog(
                "Refresh Export Profile",
                "Select an object with a GhostMetaControlSurfaceExportProfile first.",
                "OK");
            return;
        }

        profile.AutoConfigureElements();
        Selection.activeGameObject = profile.gameObject;
    }

    public static void CreateToolkitExportSceneBatch()
    {
        int themeIndex = GetIntArg(Environment.GetCommandLineArgs(), "-metaUiThemeIndex", 0);
        CreateToolkitSceneInternal(themeIndex);
    }

    public static void ExportToolkitCatalogBatch()
    {
        string[] args = Environment.GetCommandLineArgs();
        int themeIndex = GetIntArg(args, "-metaUiThemeIndex", 0);
        string surfaceFilter = GetArg(args, "-metaUiSurfaceFilter", "");
        string outputRoot = GetArg(args, "-metaUiOutputRoot", "");
        bool capturePreview = GetBoolArg(args, "-metaUiCapturePreview", true);
        ExportToolkitCatalogInternal(true, themeIndex, surfaceFilter, outputRoot, capturePreview);
    }

    private static void OpenOrCreateSceneInternal(string scenePath, Action createSceneAction, bool rebuildScene)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        if (rebuildScene || !File.Exists(GetAbsoluteProjectPath(scenePath)))
        {
            createSceneAction();
        }

        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
    }

    private static void CreateToolkitSceneInternal()
    {
        CreateToolkitSceneInternal(0);
    }

    private static void CreateToolkitSceneInternal(int themeIndex)
    {
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        Scene scene = EditorSceneManager.GetActiveScene();

        GameObject root = new GameObject(ToolkitRootName);
        ConfigureCameraForGallery();
        UIThemeManager themeManager = EnsureThemeManager(root);

        List<ToolkitCatalogEntry> catalog = BuildCatalogEntries();
        Dictionary<string, Transform> categoryRoots = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> categoryCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        string[] categoryOrder = catalog.Select(entry => entry.category).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        for (int categoryIndex = 0; categoryIndex < categoryOrder.Length; categoryIndex++)
        {
            string category = categoryOrder[categoryIndex];
            GameObject categoryRoot = new GameObject(category);
            categoryRoot.transform.SetParent(root.transform, false);
            categoryRoot.transform.localPosition = new Vector3(categoryIndex * 4.8f, 0f, 0f);
            categoryRoots[category] = categoryRoot.transform;
            categoryCounts[category] = 0;
        }

        for (int i = 0; i < catalog.Count; i++)
        {
            ToolkitCatalogEntry entry = catalog[i];
            Transform categoryRoot = categoryRoots[entry.category];
            int indexWithinCategory = categoryCounts[entry.category];
            categoryCounts[entry.category] = indexWithinCategory + 1;

            GameObject wrapper = new GameObject("ToolkitExport_" + entry.controlSurfaceId);
            wrapper.transform.SetParent(categoryRoot, false);
            wrapper.transform.localPosition = new Vector3((indexWithinCategory % 2) * 1.9f, -(indexWithinCategory / 2) * 1.35f, 0f);

            GameObject instance = InstantiateSourceForEntry(entry, scene);
            if (instance == null)
            {
                Debug.LogWarning("GhostMetaUiSetToolkitExporter: failed to instantiate source " + entry.prefabAssetPath);
                continue;
            }

            instance.name = Path.GetFileNameWithoutExtension(entry.prefabAssetPath);
            instance.transform.SetParent(wrapper.transform, false);
            instance.transform.localScale = Vector3.one * entry.scale;

            RectTransform surfaceRoot = ResolvePreferredSurfaceRoot(instance.transform, entry.preferredSurfacePath);
            GhostMetaControlSurfaceExportProfile profile = wrapper.AddComponent<GhostMetaControlSurfaceExportProfile>();
            profile.Configure(
                entry.controlSurfaceId,
                entry.controlSurfaceLabel,
                entry.controlFamilyId,
                surfaceRoot,
                entry.exportDisplayName,
                entry.exportTagsCsv,
                entry.defaultTargetDisplayId,
                entry.targetDisplayIds);
            profile.sourcePrefabAssetPath = entry.prefabAssetPath;
            ConfigureProfileForEntry(profile, entry);
            if (!TryApplyProofMetadataToProfile(profile, instance))
            {
                profile.AutoConfigureElements();
            }
        }

        ApplyTheme(themeManager, themeIndex);
        PrepareToolkitSceneForSnapshot(root);
        SaveScene(scene, ToolkitScenePath);
    }

    private static void ExportToolkitCatalogInternal(
        bool rebuildScene,
        int themeIndex,
        string surfaceFilter,
        string outputRoot,
        bool capturePreview)
    {
        OpenOrCreateSceneInternal(ToolkitScenePath, () => CreateToolkitSceneInternal(themeIndex), rebuildScene);
        Scene scene = EditorSceneManager.OpenScene(ToolkitScenePath, OpenSceneMode.Single);

        GameObject root = GameObject.Find(ToolkitRootName);
        UIThemeManager themeManager = root != null ? root.GetComponent<UIThemeManager>() : null;
        ApplyTheme(themeManager, themeIndex);
        ThemeContext themeContext = ResolveThemeContext(themeManager, themeIndex);

        GhostMetaControlSurfaceExportProfile[] profiles = UnityEngine.Object.FindObjectsOfType<GhostMetaControlSurfaceExportProfile>(true)
            .Where(profile => profile != null && profile.gameObject.scene == scene)
            .OrderBy(profile => profile.controlSurfaceId ?? "", StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string resolvedOutputRoot = string.IsNullOrWhiteSpace(outputRoot)
            ? ResolveDefaultOutputRoot(themeIndex)
            : Path.GetFullPath(outputRoot);
        Directory.CreateDirectory(resolvedOutputRoot);

        ToolkitExportRunSummary summary = new ToolkitExportRunSummary
        {
            generatedAtUtc = DateTime.UtcNow.ToString("o"),
            projectPath = Directory.GetCurrentDirectory(),
            scenePath = GetAbsoluteProjectPath(ToolkitScenePath),
            themeIndex = themeIndex,
            themeId = themeContext.themeId,
            themeLabel = themeContext.themeLabel,
            themeVariantId = themeContext.themeVariantId,
            themeAssetPath = themeContext.themeAssetPath,
            themeAssetGuid = themeContext.themeAssetGuid,
            surfaceFilter = surfaceFilter ?? "",
            outputRoot = resolvedOutputRoot
        };

        for (int i = 0; i < profiles.Length; i++)
        {
            GhostMetaControlSurfaceExportProfile profile = profiles[i];
            if (!ShouldExportProfile(profile, surfaceFilter))
            {
                continue;
            }

            ApplyThemeMetadataToProfile(profile, themeContext);
            ToolkitSurfaceSummary surface = ExportProfile(profile, resolvedOutputRoot, capturePreview);
            if (surface != null)
            {
                summary.surfaces.Add(surface);
            }
        }

        string summaryPath = Path.Combine(
            resolvedOutputRoot,
            "ghost_meta_ui_toolkit_export_summary_theme_" + themeIndex.ToString("00") + ".json");
        File.WriteAllText(summaryPath, JsonUtility.ToJson(summary, true));
        Debug.Log(
            "GhostMetaUiSetToolkitExporter: exported toolkit catalog to " + resolvedOutputRoot +
            " | summary=" + summaryPath +
            " | surfaces=" + summary.surfaces.Count.ToString());
    }

    private static ToolkitSurfaceSummary ExportProfile(
        GhostMetaControlSurfaceExportProfile profile,
        string outputRoot,
        bool capturePreview)
    {
        if (profile == null)
        {
            return null;
        }

        List<ProfileIsolationState> isolationStates = null;
        try
        {
            isolationStates = IsolateProfileForSnapshot(profile);
            PrepareProfileForSnapshot(profile);
            if (!HasExplicitProofBindings(profile))
            {
                profile.AutoConfigureElements();
            }
            Selection.activeGameObject = profile.gameObject;

            string surfaceRoot = Path.Combine(outputRoot, profile.controlSurfaceId ?? "meta_ui_surface");
            UnityBridgeResponse response = UnityBridgeInnerPieceFacade.ExportSelectionFromWindow(
                string.IsNullOrWhiteSpace(profile.exportDisplayName)
                    ? profile.controlSurfaceLabel
                    : profile.exportDisplayName,
                surfaceRoot,
                capturePreview,
                string.IsNullOrWhiteSpace(profile.exportTagsCsv)
                    ? "ghost,meta_ui,toolkit"
                    : profile.exportTagsCsv);
            EnsureSuccess(response, "asset.innerpiece.export_selection[" + (profile.controlSurfaceId ?? "unknown") + "]");

            UnityInnerPieceLastExportSummary lastExport = UnityBridgeInnerPieceFacade.LastInnerPieceExport;
            if (lastExport == null)
            {
                throw new InvalidOperationException("GhostMetaUiSetToolkitExporter: export completed but no last export summary was recorded.");
            }

            return new ToolkitSurfaceSummary
            {
                controlSurfaceId = profile.controlSurfaceId ?? "",
                exportDisplayName = string.IsNullOrWhiteSpace(profile.exportDisplayName) ? (profile.controlSurfaceLabel ?? "") : profile.exportDisplayName,
                relativePrefabPath = ResolveRelativePrefabPath(profile.sourcePrefabAssetPath),
                category = ResolveCategoryFromTags(profile.exportTagsCsv),
                controlFamilyId = profile.controlFamilyId ?? "",
                exportTagsCsv = profile.exportTagsCsv ?? "",
                defaultTargetDisplayId = profile.defaultTargetDisplayId ?? "",
                themeId = profile.controlThemeId ?? "",
                themeLabel = profile.controlThemeLabel ?? "",
                themeVariantId = profile.controlThemeVariantId ?? "",
                themeAssetPath = profile.controlThemeAssetPath ?? "",
                themeAssetGuid = profile.controlThemeAssetGuid ?? "",
                controlElementCount = profile.elements != null ? profile.elements.Count : 0,
                packageId = lastExport.PackageId,
                resourceId = lastExport.ResourceId,
                packageRootPath = lastExport.PackageRootPath,
                geometryPath = lastExport.GeometryPath,
                materialsPath = lastExport.MaterialsPath,
                controlsPath = lastExport.ControlsPath,
                previewPath = lastExport.PreviewPath,
                exportReceiptPath = lastExport.ExportReceiptPath,
                fingerprint = lastExport.Fingerprint,
                prefabAssetPath = profile.sourcePrefabAssetPath ?? ""
            };
        }
        finally
        {
            RestoreProfileIsolation(isolationStates);
        }
    }

    private static List<ProfileIsolationState> IsolateProfileForSnapshot(GhostMetaControlSurfaceExportProfile activeProfile)
    {
        List<ProfileIsolationState> states = new List<ProfileIsolationState>();
        if (activeProfile == null || activeProfile.gameObject == null || activeProfile.gameObject.scene.IsValid() == false)
        {
            return states;
        }

        GhostMetaControlSurfaceExportProfile[] profiles = UnityEngine.Object.FindObjectsOfType<GhostMetaControlSurfaceExportProfile>(true);
        for (int i = 0; i < profiles.Length; i++)
        {
            GhostMetaControlSurfaceExportProfile profile = profiles[i];
            if (profile == null || profile.gameObject == null)
            {
                continue;
            }

            if (profile.gameObject.scene != activeProfile.gameObject.scene)
            {
                continue;
            }

            if (profile == activeProfile)
            {
                continue;
            }

            states.Add(new ProfileIsolationState
            {
                gameObject = profile.gameObject,
                wasActiveSelf = profile.gameObject.activeSelf
            });

            if (profile.gameObject.activeSelf)
            {
                profile.gameObject.SetActive(false);
            }
        }

        Canvas.ForceUpdateCanvases();
        return states;
    }

    private static void RestoreProfileIsolation(List<ProfileIsolationState> states)
    {
        if (states == null)
        {
            return;
        }

        for (int i = 0; i < states.Count; i++)
        {
            ProfileIsolationState state = states[i];
            if (state == null || state.gameObject == null)
            {
                continue;
            }

            if (state.gameObject.activeSelf != state.wasActiveSelf)
            {
                state.gameObject.SetActive(state.wasActiveSelf);
            }
        }

        Canvas.ForceUpdateCanvases();
    }

    private static UIThemeManager EnsureThemeManager(GameObject root)
    {
        UIThemeManager manager = root.GetComponent<UIThemeManager>();
        if (manager == null)
        {
            manager = root.AddComponent<UIThemeManager>();
        }

        UITheme[] themes = ThemeAssetPaths
            .Select(path => AssetDatabase.LoadAssetAtPath<UITheme>(path))
            .Where(asset => asset != null)
            .ToArray();

        SerializedObject serialized = new SerializedObject(manager);
        SerializedProperty themesProperty = serialized.FindProperty("_themes");
        SerializedProperty currentThemeProperty = serialized.FindProperty("_currentThemeIndex");
        if (themesProperty != null)
        {
            themesProperty.arraySize = themes.Length;
            for (int i = 0; i < themes.Length; i++)
            {
                themesProperty.GetArrayElementAtIndex(i).objectReferenceValue = themes[i];
            }
        }

        if (currentThemeProperty != null)
        {
            currentThemeProperty.intValue = 0;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manager);
        return manager;
    }

    private static void ApplyTheme(UIThemeManager themeManager, int themeIndex)
    {
        if (themeManager == null || themeManager.Themes == null || themeManager.Themes.Length <= 0)
        {
            return;
        }

        int clampedThemeIndex = Mathf.Clamp(themeIndex, 0, themeManager.Themes.Length - 1);
        SerializedObject serialized = new SerializedObject(themeManager);
        SerializedProperty currentThemeProperty = serialized.FindProperty("_currentThemeIndex");
        if (currentThemeProperty != null)
        {
            currentThemeProperty.intValue = clampedThemeIndex;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        themeManager.ApplyTheme(clampedThemeIndex);
        EditorUtility.SetDirty(themeManager);
    }

    private static void ConfigureCameraForGallery()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            Camera[] cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
            camera = cameras != null && cameras.Length > 0 ? cameras[0] : null;
        }

        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
        }

        camera.transform.position = new Vector3(4.2f, 0.35f, -8.5f);
        camera.transform.rotation = Quaternion.Euler(4f, 0f, 0f);
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = 40f;
    }

    private static void SaveScene(Scene scene, string scenePath)
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Scenes"));
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("GhostMetaUiSetToolkitExporter: wrote " + scenePath);
    }

    private static List<ToolkitCatalogEntry> BuildCatalogEntries()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PackagePrefabsRoot });
        List<ToolkitCatalogEntry> entries = new List<ToolkitCatalogEntry>();
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string relativePath = assetPath.StartsWith(PackagePrefabsRoot + "/", StringComparison.OrdinalIgnoreCase)
                ? assetPath.Substring(PackagePrefabsRoot.Length + 1)
                : assetPath;
            string category = relativePath.Contains("/") ? relativePath.Substring(0, relativePath.IndexOf('/')) : "Misc";
            string fileStem = Path.GetFileNameWithoutExtension(assetPath);
            SurfaceOverride surfaceOverride;
            Overrides.TryGetValue(relativePath, out surfaceOverride);

            ToolkitCatalogEntry entry = new ToolkitCatalogEntry
            {
                prefabAssetPath = assetPath,
                relativePrefabPath = relativePath,
                category = category,
                controlSurfaceId = BuildShortSurfaceId(category, fileStem, relativePath),
                controlSurfaceLabel = BuildDisplayName(fileStem),
                controlFamilyId = ResolveControlFamilyId(relativePath, category),
                exportDisplayName = "Meta " + BuildDisplayName(fileStem),
                exportTagsCsv = BuildTagsCsv(category, fileStem, surfaceOverride != null ? surfaceOverride.tagsSuffix : ""),
                preferredSurfacePath = surfaceOverride != null ? surfaceOverride.preferredSurfacePath : "",
                sceneSourcePath = surfaceOverride != null ? surfaceOverride.sceneSourcePath : "",
                sceneObjectName = surfaceOverride != null ? surfaceOverride.sceneObjectName : "",
                scale = surfaceOverride != null && surfaceOverride.scale > 0f ? surfaceOverride.scale : 1f,
                defaultTargetDisplayId = "player_main",
                targetDisplayIds = new[] { "player_main" }
            };

            entries.Add(entry);
        }

        return entries
            .OrderBy(entry => ResolveCategorySortKey(entry.category))
            .ThenBy(entry => entry.relativePrefabPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void ConfigureProfileForEntry(
        GhostMetaControlSurfaceExportProfile profile,
        ToolkitCatalogEntry entry)
    {
        if (profile == null || entry == null)
        {
            return;
        }

        profile.controlFamilyId = entry.controlFamilyId ?? profile.controlFamilyId;
        profile.toolkitCategory = entry.category ?? profile.toolkitCategory;
        profile.surfaceUnitsToMetersMultiplier = 0f;
        profile.SetActionMappings(BuildActionMappings(entry));
    }

    private static bool TryApplyProofMetadataToProfile(
        GhostMetaControlSurfaceExportProfile profile,
        GameObject instance)
    {
        if (profile == null || instance == null)
        {
            return false;
        }

        GhostMetaVideoPlayerProof proofBinder = instance.GetComponentInChildren<GhostMetaVideoPlayerProof>(true);
        GhostMetaControlSurfaceProofMetadata proofMetadata = instance.GetComponentInChildren<GhostMetaControlSurfaceProofMetadata>(true);
        if (proofBinder == null || proofMetadata == null)
        {
            return false;
        }

        PrimeGhostMetaVideoPlayerProof(proofBinder);
        InvokeHiddenInstanceMethod(proofMetadata, "RepairBindingsFromProofBinder");

        if (proofMetadata.surfaceRoot != null)
        {
            profile.surfaceRoot = proofMetadata.surfaceRoot;
        }

        if (!string.IsNullOrWhiteSpace(proofMetadata.layoutSource))
        {
            profile.layoutSource = proofMetadata.layoutSource.Trim();
        }

        if (!string.IsNullOrWhiteSpace(proofMetadata.defaultTargetDisplayId))
        {
            profile.defaultTargetDisplayId = proofMetadata.defaultTargetDisplayId.Trim();
        }

        string[] targetDisplayIds = NormalizeTargetDisplayIds(proofMetadata.targetDisplayIds, profile.defaultTargetDisplayId);
        if (targetDisplayIds.Length > 0)
        {
            profile.targetDisplayIds = targetDisplayIds;
        }

        profile.surfaceUnitsToMetersMultiplier = 0f;
        profile.elements = CloneProofBindings(proofMetadata.elements);
        return profile.surfaceRoot != null && profile.elements != null && profile.elements.Count > 0;
    }

    private static bool HasExplicitProofBindings(GhostMetaControlSurfaceExportProfile profile)
    {
        if (profile == null || profile.elements == null || profile.elements.Count <= 0)
        {
            return false;
        }

        return profile.elements.Any(binding =>
            binding != null &&
            (
                string.Equals(binding.elementId, "play_pause_button", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(binding.elementId, "scrub_slider", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(binding.elementId, "volume_slider", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(binding.elementId, "time_current_label", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(binding.elementId, "time_remaining_label", StringComparison.OrdinalIgnoreCase)
            ));
    }

    private static List<GhostMetaControlSurfaceExportProfile.ControlElementBinding> CloneProofBindings(
        IEnumerable<GhostMetaControlSurfaceProofMetadata.ControlElementBinding> source)
    {
        List<GhostMetaControlSurfaceExportProfile.ControlElementBinding> cloned =
            new List<GhostMetaControlSurfaceExportProfile.ControlElementBinding>();
        if (source == null)
        {
            return cloned;
        }

        foreach (GhostMetaControlSurfaceProofMetadata.ControlElementBinding binding in source)
        {
            if (binding == null || binding.rectTransform == null || string.IsNullOrWhiteSpace(binding.elementId))
            {
                continue;
            }

            cloned.Add(new GhostMetaControlSurfaceExportProfile.ControlElementBinding
            {
                elementId = binding.elementId ?? "",
                elementLabel = binding.elementLabel ?? "",
                actionId = binding.actionId ?? "",
                elementKind = binding.elementKind ?? "",
                valueKind = binding.valueKind ?? "",
                rectTransform = binding.rectTransform,
                readOnly = binding.readOnly
            });
        }

        return cloned;
    }

    private static string[] NormalizeTargetDisplayIds(IEnumerable<string> source, string fallback)
    {
        List<string> normalized = source != null
            ? source
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new List<string>();

        if (normalized.Count <= 0 && !string.IsNullOrWhiteSpace(fallback))
        {
            normalized.Add(fallback.Trim());
        }

        return normalized.ToArray();
    }

    private static ThemeContext ResolveThemeContext(UIThemeManager themeManager, int themeIndex)
    {
        ThemeContext context = new ThemeContext();
        context.themeIndex = Mathf.Max(0, themeIndex);
        context.themeVariantId = "theme_" + context.themeIndex.ToString("00");

        UITheme theme = null;
        if (themeManager != null && themeManager.Themes != null && themeManager.Themes.Length > 0)
        {
            int clamped = Mathf.Clamp(themeIndex, 0, themeManager.Themes.Length - 1);
            context.themeIndex = clamped;
            context.themeVariantId = "theme_" + clamped.ToString("00");
            theme = themeManager.Themes[clamped];
        }

        if (theme == null && ThemeAssetPaths.Length > 0)
        {
            int clamped = Mathf.Clamp(context.themeIndex, 0, ThemeAssetPaths.Length - 1);
            context.themeIndex = clamped;
            context.themeVariantId = "theme_" + clamped.ToString("00");
            theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemeAssetPaths[clamped]);
        }

        context.themeLabel = theme != null ? theme.name : "Meta UI Theme " + context.themeIndex.ToString("00");
        context.themeAssetPath = theme != null ? (AssetDatabase.GetAssetPath(theme) ?? "") : "";
        context.themeAssetGuid = string.IsNullOrWhiteSpace(context.themeAssetPath)
            ? ""
            : AssetDatabase.AssetPathToGUID(context.themeAssetPath);
        context.themeId = BuildThemeId(context.themeLabel, context.themeAssetGuid);
        return context;
    }

    private static void ApplyThemeMetadataToProfile(
        GhostMetaControlSurfaceExportProfile profile,
        ThemeContext themeContext)
    {
        if (profile == null || themeContext == null)
        {
            return;
        }

        profile.controlThemeId = themeContext.themeId ?? "";
        profile.controlThemeLabel = themeContext.themeLabel ?? "";
        profile.controlThemeVariantId = themeContext.themeVariantId ?? "";
        profile.controlThemeAssetPath = themeContext.themeAssetPath ?? "";
        profile.controlThemeAssetGuid = themeContext.themeAssetGuid ?? "";
    }

    private static string BuildThemeId(string themeLabel, string themeAssetGuid)
    {
        string labelToken = SanitizeId(themeLabel);
        if (string.IsNullOrWhiteSpace(labelToken))
        {
            labelToken = "meta_ui_theme";
        }

        if (!string.IsNullOrWhiteSpace(themeAssetGuid))
        {
            string shortGuid = themeAssetGuid.Length > 8 ? themeAssetGuid.Substring(0, 8).ToLowerInvariant() : themeAssetGuid.ToLowerInvariant();
            return labelToken + "_" + shortGuid;
        }

        return labelToken;
    }

    private static List<GhostMetaControlSurfaceExportProfile.ActionMappingRule> BuildActionMappings(
        ToolkitCatalogEntry entry)
    {
        List<GhostMetaControlSurfaceExportProfile.ActionMappingRule> mappings =
            new List<GhostMetaControlSurfaceExportProfile.ActionMappingRule>();
        if (entry == null)
        {
            return mappings;
        }

        if (string.Equals(entry.relativePrefabPath, "Patterns/ContentUIExample-VideoPlayer.prefab", StringComparison.OrdinalIgnoreCase))
        {
            mappings.Add(new GhostMetaControlSurfaceExportProfile.ActionMappingRule
            {
                pathContains = "PlayerControls/Control/QuickControls/SecondaryButton_IconAndLabel",
                elementKind = "toggle",
                actionId = "play_pause",
                elementKindOverride = "button",
                valueKindOverride = "bool"
            });
            mappings.Add(new GhostMetaControlSurfaceExportProfile.ActionMappingRule
            {
                pathContains = "PlayerControls/SmallSlider_LabelsAndIcons/SmallSlider",
                elementKind = "slider",
                actionId = "scrub_normalized",
                elementKindOverride = "slider",
                valueKindOverride = "normalized_float"
            });
            mappings.Add(new GhostMetaControlSurfaceExportProfile.ActionMappingRule
            {
                pathContains = "PlayerControls/Control/Sound/VolumeSlider",
                elementKind = "slider",
                actionId = "volume_normalized",
                elementKindOverride = "slider",
                valueKindOverride = "normalized_float"
            });
        }

        return mappings;
    }

    private static string ResolveControlFamilyId(string relativePath, string category)
    {
        if (string.Equals(relativePath, "Patterns/ContentUIExample-VideoPlayer.prefab", StringComparison.OrdinalIgnoreCase))
        {
            return "meta_ui_video_player";
        }

        if (string.Equals(relativePath, "Patterns/GridMenuExample2x4.prefab", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(relativePath, "Patterns/GridMenuExample3x3.prefab", StringComparison.OrdinalIgnoreCase))
        {
            return "meta_ui_grid_menu";
        }

        if (string.Equals(relativePath, "TextInputField/SearchBar.prefab", StringComparison.OrdinalIgnoreCase))
        {
            return "meta_ui_search_bar";
        }

        if (string.Equals(relativePath, "TextInputField/TextInputField.prefab", StringComparison.OrdinalIgnoreCase))
        {
            return "meta_ui_text_input";
        }

        return string.Equals(category, "Patterns", StringComparison.OrdinalIgnoreCase)
            ? "meta_ui_pattern"
            : "meta_ui_toolkit";
    }

    private static string BuildTagsCsv(string category, string fileStem, string extraTag)
    {
        List<string> tags = new List<string>
        {
            "ghost",
            "meta_ui",
            SanitizeId(category),
            SanitizeId(fileStem)
        };

        if (!string.IsNullOrWhiteSpace(extraTag))
        {
            tags.Add(SanitizeId(extraTag));
        }

        return string.Join(",", tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string ResolveCategorySortKey(string category)
    {
        string[] order =
        {
            "Patterns",
            "Backplate",
            "Button",
            "ContextMenu",
            "Dialog",
            "DropDown",
            "Slider",
            "TextInputField",
            "Tooltip"
        };

        int index = Array.FindIndex(order, value => string.Equals(value, category, StringComparison.OrdinalIgnoreCase));
        return (index < 0 ? 99 : index).ToString("D2") + "_" + category;
    }

    private static string BuildDisplayName(string fileStem)
    {
        string[] tokens = fileStem
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return fileStem;
        }

        return string.Join(" ", tokens);
    }

    private static RectTransform ResolvePreferredSurfaceRoot(Transform root, string preferredPath)
    {
        if (root == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(preferredPath))
        {
            Transform current = root;
            string[] parts = preferredPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                current = current != null ? current.Find(parts[i]) : null;
            }

            RectTransform preferred = current as RectTransform;
            if (preferred != null)
            {
                return preferred;
            }
        }

        return ResolveLargestRectTransform(root);
    }

    private static GameObject InstantiateSourceForEntry(ToolkitCatalogEntry entry, Scene exportScene)
    {
        if (entry == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(entry.sceneSourcePath) &&
            !string.IsNullOrWhiteSpace(entry.sceneObjectName))
        {
            GameObject sceneInstance = InstantiateSceneSourceObject(
                entry.sceneSourcePath,
                entry.sceneObjectName,
                exportScene);
            if (sceneInstance != null)
            {
                return sceneInstance;
            }
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefabAssetPath);
        if (prefab == null)
        {
            Debug.LogWarning("GhostMetaUiSetToolkitExporter: missing prefab " + entry.prefabAssetPath);
            return null;
        }

        return PrefabUtility.InstantiatePrefab(prefab) as GameObject;
    }

    private static GameObject InstantiateSceneSourceObject(string scenePath, string objectName, Scene exportScene)
    {
        if (string.IsNullOrWhiteSpace(scenePath) || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        string absoluteSourcePath = GetAbsoluteProjectPath(scenePath);
        if (!File.Exists(absoluteSourcePath))
        {
            Debug.LogWarning("GhostMetaUiSetToolkitExporter: missing scene source " + scenePath);
            return null;
        }

        string editableScenePath = ResolveEditableSceneSourcePath(scenePath);
        if (string.IsNullOrWhiteSpace(editableScenePath))
        {
            return null;
        }

        Scene sourceScene = default(Scene);
        bool opened = false;
        try
        {
            sourceScene = EditorSceneManager.OpenScene(editableScenePath, OpenSceneMode.Additive);
            opened = sourceScene.IsValid();
            if (!opened)
            {
                return null;
            }

            GameObject sourceObject = FindSceneObjectByName(sourceScene, objectName);
            if (sourceObject == null)
            {
                Debug.LogWarning(
                    "GhostMetaUiSetToolkitExporter: scene source object '" + objectName +
                    "' was not found in " + scenePath);
                return null;
            }

            GameObject clone = UnityEngine.Object.Instantiate(sourceObject);
            if (clone == null)
            {
                return null;
            }

            if (exportScene.IsValid())
            {
                SceneManager.MoveGameObjectToScene(clone, exportScene);
            }

            return clone;
        }
        finally
        {
            if (opened && sourceScene.IsValid())
            {
                EditorSceneManager.CloseScene(sourceScene, true);
            }
        }
    }

    private static string ResolveEditableSceneSourcePath(string scenePath)
    {
        if (string.IsNullOrWhiteSpace(scenePath))
        {
            return null;
        }

        if (!scenePath.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
        {
            return scenePath;
        }

        string tempScenePath = TempSceneSourcesRoot + "/" + Path.GetFileName(scenePath);
        string absoluteSourcePath = GetAbsoluteProjectPath(scenePath);
        string absoluteTempScenePath = GetAbsoluteProjectPath(tempScenePath);
        string absoluteTempDirectory = Path.GetDirectoryName(absoluteTempScenePath) ?? GetAbsoluteProjectPath(TempSceneSourcesRoot);
        Directory.CreateDirectory(absoluteTempDirectory);

        bool needsCopy = !File.Exists(absoluteTempScenePath);
        if (!needsCopy)
        {
            DateTime sourceWriteTimeUtc = File.GetLastWriteTimeUtc(absoluteSourcePath);
            DateTime tempWriteTimeUtc = File.GetLastWriteTimeUtc(absoluteTempScenePath);
            needsCopy = sourceWriteTimeUtc > tempWriteTimeUtc;
        }

        if (needsCopy)
        {
            AssetDatabase.Refresh();
            AssetDatabase.DeleteAsset(tempScenePath);
            if (!AssetDatabase.CopyAsset(scenePath, tempScenePath))
            {
                Debug.LogWarning(
                    "GhostMetaUiSetToolkitExporter: failed to copy scene source '" +
                    scenePath + "' to '" + tempScenePath + "'.");
                return null;
            }

            AssetDatabase.ImportAsset(tempScenePath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh();
        }

        return tempScenePath;
    }

    private static GameObject FindSceneObjectByName(Scene scene, string objectName)
    {
        if (!scene.IsValid() || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject match = FindChildByName(roots[i].transform, objectName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static GameObject FindChildByName(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (string.Equals(root.name, objectName, StringComparison.Ordinal))
        {
            return root.gameObject;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            GameObject match = FindChildByName(root.GetChild(i), objectName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static void PrepareProfileForSnapshot(GhostMetaControlSurfaceExportProfile profile)
    {
        if (profile == null)
        {
            return;
        }

        PrepareToolkitSceneForSnapshot(profile.gameObject);
        RectTransform surfaceRoot = profile.surfaceRoot != null ? profile.surfaceRoot : ResolveLargestRectTransform(profile.transform);
        if (surfaceRoot != null)
        {
            ForceLayoutAndGraphicRefresh(surfaceRoot);
        }

        Canvas.ForceUpdateCanvases();
    }

    private static void PrepareToolkitSceneForSnapshot(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        PrimeGhostMetaVideoPlayerProofs(root);
        NormalizeUnsupportedGraphicMaterials(root);
        PrimeRoundedBoxVideoControllers(root);
        ForceLayoutAndGraphicRefresh(root.transform as RectTransform);
        ForceTextRefresh(root);
        Canvas.ForceUpdateCanvases();
    }

    private static void NormalizeUnsupportedGraphicMaterials(GameObject root)
    {
        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
            {
                continue;
            }

            Material material = graphic.material;
            Shader shader = material != null ? material.shader : null;
            string shaderName = shader != null ? shader.name : "";
            if (string.IsNullOrWhiteSpace(shaderName))
            {
                continue;
            }

            bool unsupported = UnsupportedSnapshotShaderNames.Any(
                candidate => string.Equals(candidate, shaderName, StringComparison.OrdinalIgnoreCase));
            if (!unsupported)
            {
                continue;
            }

            graphic.material = null;
            graphic.SetMaterialDirty();
            graphic.SetVerticesDirty();
        }
    }

    private static void PrimeRoundedBoxVideoControllers(GameObject root)
    {
        RoundedBoxVideoController[] controllers = root.GetComponentsInChildren<RoundedBoxVideoController>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            RoundedBoxVideoController controller = controllers[i];
            if (controller == null)
            {
                continue;
            }

            FieldInfo animationsField = typeof(RoundedBoxVideoController).GetField("animations", InstanceMethodFlags);
            object animationsValue = animationsField != null ? animationsField.GetValue(controller) : null;
            if (animationsValue == null)
            {
                InvokeHiddenInstanceMethod(controller, "Start");
            }

            controller.UpdateBackgroundMaterialProperties();
            InvokeHiddenInstanceMethod(controller, "LateUpdate");
        }
    }

    private static void PrimeGhostMetaVideoPlayerProofs(GameObject root)
    {
        GhostMetaVideoPlayerProof[] proofs = root.GetComponentsInChildren<GhostMetaVideoPlayerProof>(true);
        for (int i = 0; i < proofs.Length; i++)
        {
            PrimeGhostMetaVideoPlayerProof(proofs[i]);
        }
    }

    private static void PrimeGhostMetaVideoPlayerProof(GhostMetaVideoPlayerProof proof)
    {
        if (proof == null)
        {
            return;
        }

        InvokeHiddenInstanceMethod(proof, "Awake");
        InvokeHiddenInstanceMethod(proof, "Start");

        GhostMetaControlSurfaceProofMetadata metadata = proof.GetComponent<GhostMetaControlSurfaceProofMetadata>();
        if (metadata != null)
        {
            InvokeHiddenInstanceMethod(metadata, "RepairBindingsFromProofBinder");
        }

        InvokeHiddenInstanceMethod(proof, "Update");
    }

    private static void ForceLayoutAndGraphicRefresh(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();

        RectTransform[] rectTransforms = root.GetComponentsInChildren<RectTransform>(true);
        for (int i = rectTransforms.Length - 1; i >= 0; i--)
        {
            RectTransform rectTransform = rectTransforms[i];
            if (rectTransform == null)
            {
                continue;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }

        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
            {
                continue;
            }

            graphic.SetAllDirty();
            graphic.SetMaterialDirty();
            graphic.SetVerticesDirty();
        }

        Canvas.ForceUpdateCanvases();
    }

    private static void ForceTextRefresh(GameObject root)
    {
        TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TextMeshProUGUI text = texts[i];
            if (text == null)
            {
                continue;
            }

            text.ForceMeshUpdate(true, true);
        }
    }

    private static void InvokeHiddenInstanceMethod(object target, string methodName)
    {
        if (target == null || string.IsNullOrWhiteSpace(methodName))
        {
            return;
        }

        MethodInfo method = target.GetType().GetMethod(methodName, InstanceMethodFlags);
        if (method == null || method.GetParameters().Length > 0)
        {
            return;
        }

        method.Invoke(target, null);
    }

    private static RectTransform ResolveLargestRectTransform(Transform root)
    {
        RectTransform[] rectTransforms = root.GetComponentsInChildren<RectTransform>(true);
        RectTransform best = null;
        float bestArea = 0f;
        for (int i = 0; i < rectTransforms.Length; i++)
        {
            RectTransform candidate = rectTransforms[i];
            if (candidate == null)
            {
                continue;
            }

            float area = Mathf.Abs(candidate.rect.width * candidate.rect.height);
            if (area <= bestArea)
            {
                continue;
            }

            best = candidate;
            bestArea = area;
        }

        return best;
    }

    private static GhostMetaControlSurfaceExportProfile ResolveProfileFromSelection()
    {
        GameObject selection = Selection.activeGameObject;
        if (selection == null)
        {
            return null;
        }

        GhostMetaControlSurfaceExportProfile profile = selection.GetComponent<GhostMetaControlSurfaceExportProfile>();
        if (profile != null)
        {
            return profile;
        }

        profile = selection.GetComponentInParent<GhostMetaControlSurfaceExportProfile>(true);
        if (profile != null)
        {
            return profile;
        }

        return selection.GetComponentInChildren<GhostMetaControlSurfaceExportProfile>(true);
    }

    private static bool ShouldExportProfile(GhostMetaControlSurfaceExportProfile profile, string filter)
    {
        if (profile == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        string[] tokens = filter.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return true;
        }

        string haystack = string.Join(
            " ",
            new[]
            {
                profile.controlSurfaceId ?? "",
                profile.controlSurfaceLabel ?? "",
                profile.exportDisplayName ?? "",
                profile.exportTagsCsv ?? ""
            }).ToLowerInvariant();

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i].Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (haystack.Contains(token))
            {
                return true;
            }
        }

        return false;
    }

    private static string ResolveDefaultOutputRoot(int themeIndex)
    {
        string assetLaneRoot = Path.GetFullPath(
            Path.Combine(
                Directory.GetCurrentDirectory(),
                "..",
                ".."));
        return Path.Combine(
            assetLaneRoot,
            "build",
            "meta_toolkit_catalog",
            "theme_" + themeIndex.ToString("00"));
    }

    private static int GetCurrentThemeIndex()
    {
        GameObject root = GameObject.Find(ToolkitRootName);
        UIThemeManager manager = root != null ? root.GetComponent<UIThemeManager>() : null;
        return manager != null ? manager.CurrentThemeIndex : 0;
    }

    private static string ResolveCategoryFromTags(string tagsCsv)
    {
        if (string.IsNullOrWhiteSpace(tagsCsv))
        {
            return "";
        }

        string[] tokens = tagsCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length >= 3 ? tokens[2].Trim() : tokens[0].Trim();
    }

    private static string ResolveRelativePrefabPath(string prefabAssetPath)
    {
        if (string.IsNullOrWhiteSpace(prefabAssetPath))
        {
            return "";
        }

        return prefabAssetPath.StartsWith(PackagePrefabsRoot + "/", StringComparison.OrdinalIgnoreCase)
            ? prefabAssetPath.Substring(PackagePrefabsRoot.Length + 1)
            : prefabAssetPath;
    }

    private static string GetAbsoluteProjectPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string GetArg(string[] args, string key, string fallback)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return fallback;
    }

    private static int GetIntArg(string[] args, string key, int fallback)
    {
        string value = GetArg(args, key, fallback.ToString());
        int parsed;
        return int.TryParse(value, out parsed) ? parsed : fallback;
    }

    private static bool GetBoolArg(string[] args, string key, bool fallback)
    {
        string value = GetArg(args, key, fallback ? "true" : "false");
        bool parsed;
        return bool.TryParse(value, out parsed) ? parsed : fallback;
    }

    private static string SanitizeId(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "";
        }

        char[] chars = input.ToLowerInvariant().ToCharArray();
        List<char> output = new List<char>(chars.Length);
        bool previousUnderscore = false;
        for (int i = 0; i < chars.Length; i++)
        {
            char current = chars[i];
            if ((current >= 'a' && current <= 'z') || (current >= '0' && current <= '9'))
            {
                output.Add(current);
                previousUnderscore = false;
                continue;
            }

            if (previousUnderscore)
            {
                continue;
            }

            output.Add('_');
            previousUnderscore = true;
        }

        string sanitized = new string(output.ToArray()).Trim('_');
        return sanitized;
    }

    private static string BuildShortSurfaceId(string category, string fileStem, string relativePath)
    {
        string categoryToken = SanitizeId(category);
        string fileToken = SanitizeId(fileStem);
        string hash = ComputeStableShortHash(relativePath);
        string combined = "meta_" + categoryToken + "_" + fileToken;
        if (combined.Length > 48)
        {
            combined = combined.Substring(0, 48).TrimEnd('_');
        }

        return combined + "_" + hash;
    }

    private static string ComputeStableShortHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261u;
            string normalized = string.IsNullOrWhiteSpace(value) ? "" : value;
            for (int i = 0; i < normalized.Length; i++)
            {
                hash ^= normalized[i];
                hash *= 16777619u;
            }

            return hash.ToString("x8");
        }
    }

    private static void EnsureSuccess(UnityBridgeResponse response, string stage)
    {
        if (response == null)
        {
            throw new InvalidOperationException(stage + " returned no response.");
        }

        if (!response.Ok)
        {
            throw new InvalidOperationException(stage + " failed: " + response.Code + ": " + response.Message);
        }
    }

    private sealed class ToolkitCatalogEntry
    {
        public string prefabAssetPath = "";
        public string relativePrefabPath = "";
        public string category = "";
        public string controlSurfaceId = "";
        public string controlSurfaceLabel = "";
        public string controlFamilyId = "";
        public string exportDisplayName = "";
        public string exportTagsCsv = "";
        public string preferredSurfacePath = "";
        public string sceneSourcePath = "";
        public string sceneObjectName = "";
        public float scale = 1f;
        public string defaultTargetDisplayId = "player_main";
        public string[] targetDisplayIds = { "player_main" };
    }
}
