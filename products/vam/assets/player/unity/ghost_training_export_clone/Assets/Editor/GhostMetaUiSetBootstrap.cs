using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FrameAngel.UnityEditorBridge;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public static class GhostMetaUiSetBootstrap
{
    private const string SceneDirectory = "Assets/Scenes";
    private const string ScenePath = SceneDirectory + "/GhostMetaUiSetStudy.unity";
    private const string FlatGalleryScenePath = SceneDirectory + "/GhostMetaUiSetFlatGallery.unity";
    private const string VideoPlayerProofScenePath = SceneDirectory + "/GhostMetaUiSetVideoPlayerProof.unity";
    private const string ImportedPackageSceneDirectory = SceneDirectory + "/MetaUiSetSamples";
    private const string ImportedUiSetScenePath = ImportedPackageSceneDirectory + "/UISet.unity";
    private const string ImportedUiSetPatternsScenePath = ImportedPackageSceneDirectory + "/UISetPatterns.unity";
    private const string PackageUiSetScenePath = "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Objects/UISet/Scenes/UISet.unity";
    private const string PackageUiSetPatternsScenePath = "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Objects/UISet/Scenes/UISetPatterns.unity";
    private const string PackageVideoPlayerPrefabPath = "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Objects/UISet/Prefabs/Patterns/ContentUIExample-VideoPlayer.prefab";
    private const string PackageUiSetInstructionVideoPath = "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Objects/Environment/Tooltips/DialogVideos/Instruction_UISet.mp4";

    [Serializable]
    private sealed class FlatGalleryElementCaptureMetadata
    {
        public string schemaVersion = "ghost_meta_ui_set_flat_gallery_element_capture_v1";
        public string scenePath = "";
        public string elementName = "";
        public int textureWidth = 0;
        public int textureHeight = 0;
        public float surfaceWidthMeters = 0f;
        public float surfaceHeightMeters = 0f;
    }

    [MenuItem("FrameAngel/Ghost/Meta UI Set/Open Study Scene")]
    public static void OpenStudyScene()
    {
        OpenOrCreateSceneInternal(ScenePath, CreateStudySceneInternal, rebuildScene: false);
    }

    [MenuItem("FrameAngel/Ghost/Meta UI Set/Refresh Study Scene")]
    public static void RefreshStudyScene()
    {
        OpenOrCreateSceneInternal(ScenePath, CreateStudySceneInternal, rebuildScene: true);
    }

    [MenuItem("FrameAngel/Ghost/Meta UI Set/Create Study Scene Asset")]
    public static void CreateStudyScene()
    {
        CreateStudySceneInternal();
    }

    [MenuItem("FrameAngel/Ghost/Meta UI Set/Open Flat Gallery Scene")]
    public static void OpenFlatGalleryScene()
    {
        OpenOrCreateSceneInternal(FlatGalleryScenePath, CreateFlatGallerySceneInternal, rebuildScene: false);
    }

    [MenuItem("FrameAngel/Ghost/Meta UI Set/Refresh Flat Gallery Scene")]
    public static void RefreshFlatGalleryScene()
    {
        OpenOrCreateSceneInternal(FlatGalleryScenePath, CreateFlatGallerySceneInternal, rebuildScene: true);
    }

    [MenuItem("FrameAngel/Ghost/Meta UI Set/Open Video Player Proof Scene")]
    public static void OpenVideoPlayerProofScene()
    {
        OpenOrCreateSceneInternal(VideoPlayerProofScenePath, CreateVideoPlayerProofSceneInternal, rebuildScene: false);
    }

    [MenuItem("FrameAngel/Ghost/Meta UI Set/Refresh Video Player Proof Scene")]
    public static void RefreshVideoPlayerProofScene()
    {
        OpenOrCreateSceneInternal(VideoPlayerProofScenePath, CreateVideoPlayerProofSceneInternal, rebuildScene: true);
    }

    [MenuItem("FrameAngel/Ghost/Meta UI Set/Export Video Player Proof InnerPiece")]
    public static void ExportVideoPlayerProofInnerPiece()
    {
        ExportVideoPlayerProofInnerPieceInternal(rebuildScene: true);
    }

    [MenuItem("FrameAngel/Ghost/Meta UI Set/Import Packaged UISet Scenes")]
    public static void ImportPackagedUiSetScenes()
    {
        ImportPackagedUiSetScenesInternal(overwriteExisting: true);
    }

    [MenuItem("FrameAngel/Ghost/Meta UI Set/Open Imported UISet Scene")]
    public static void OpenImportedUiSetScene()
    {
        OpenImportedPackageScene(ImportedUiSetScenePath);
    }

    [MenuItem("FrameAngel/Ghost/Meta UI Set/Open Imported UISet Patterns Scene")]
    public static void OpenImportedUiSetPatternsScene()
    {
        OpenImportedPackageScene(ImportedUiSetPatternsScenePath);
    }

    [MenuItem("FrameAngel/Ghost/Meta UI Set/Apply Standalone D3D11 Project Fix")]
    public static void ApplyStandaloneDirect3D11ProjectFix()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        bool continueWithRestart = EditorUtility.DisplayDialog(
            "Apply Standalone D3D11 Project Fix",
            "This sets Standalone graphics APIs to Direct3D11 and restarts the Unity Editor so the Meta project fix is persisted.\n\nDo you want to continue?",
            "Apply and Restart",
            "Cancel");

        if (!continueWithRestart)
        {
            return;
        }

        var buildTarget = EditorUserBuildSettings.activeBuildTarget;
        if (BuildPipeline.GetBuildTargetGroup(buildTarget) != BuildTargetGroup.Standalone)
        {
            buildTarget = BuildTarget.StandaloneWindows64;
        }

        PlayerSettings.SetUseDefaultGraphicsAPIs(buildTarget, false);
        PlayerSettings.SetGraphicsAPIs(buildTarget, new[] { GraphicsDeviceType.Direct3D11 });
        AssetDatabase.SaveAssets();

        EditorApplication.delayCall += () =>
        {
            EditorApplication.OpenProject(Directory.GetCurrentDirectory());
        };
    }

    public static void CreateStudySceneBatch()
    {
        CreateStudySceneInternal();
    }

    public static void CreateFlatGallerySceneBatch()
    {
        CreateFlatGallerySceneInternal();
    }

    public static void CreateVideoPlayerProofSceneBatch()
    {
        CreateVideoPlayerProofSceneInternal();
    }

    public static void ExportVideoPlayerProofInnerPieceBatch()
    {
        ExportVideoPlayerProofInnerPieceInternal(rebuildScene: true);
    }

    public static void CaptureStudyScenePreviewBatch()
    {
        string capturePath = GetCommandLineArgument("-metaUiSetCapturePath");
        if (string.IsNullOrWhiteSpace(capturePath))
        {
            throw new InvalidOperationException("Missing -metaUiSetCapturePath for GhostMetaUiSetBootstrap.CaptureStudyScenePreviewBatch");
        }

        if (!File.Exists(GetAbsoluteScenePath(ScenePath)))
        {
            throw new FileNotFoundException("Meta UI Set study scene was not found.", ScenePath);
        }

        string captureDirectory = Path.GetDirectoryName(capturePath) ?? string.Empty;
        Directory.CreateDirectory(captureDirectory);
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Camera camera = Camera.main ?? UnityEngine.Object.FindObjectOfType<Camera>();
        if (camera == null)
        {
            var cameraObject = new GameObject("MetaUiSetPreviewCamera");
            camera = cameraObject.AddComponent<Camera>();
        }

        camera.transform.position = new Vector3(0f, 1.25f, -2.25f);
        camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.02f, 1f));
        camera.fieldOfView = 46f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.93f, 0.95f, 0.97f, 1f);

        RenderTexture renderTexture = new RenderTexture(1600, 900, 24);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        Texture2D texture = null;
        try
        {
            camera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            camera.Render();

            texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture.Apply();

            byte[] png = texture.EncodeToPNG();
            File.WriteAllBytes(capturePath, png);
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;

            if (texture != null)
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(renderTexture);
        }

        Debug.Log($"GhostMetaUiSetBootstrap: captured preview for {scene.path} to {capturePath}");
    }

    public static void CaptureFlatGalleryElementBatch()
    {
        string capturePath = GetCommandLineArgument("-metaUiSetCapturePath");
        if (string.IsNullOrWhiteSpace(capturePath))
        {
            throw new InvalidOperationException("Missing -metaUiSetCapturePath for GhostMetaUiSetBootstrap.CaptureFlatGalleryElementBatch");
        }

        string elementName = GetCommandLineArgument("-metaUiSetElementName");
        if (string.IsNullOrWhiteSpace(elementName))
        {
            throw new InvalidOperationException("Missing -metaUiSetElementName for GhostMetaUiSetBootstrap.CaptureFlatGalleryElementBatch");
        }

        string captureMetaPath = GetCommandLineArgument("-metaUiSetCaptureMetaPath");
        string absoluteFlatGalleryScenePath = GetAbsoluteScenePath(FlatGalleryScenePath);
        if (!File.Exists(absoluteFlatGalleryScenePath))
        {
            CreateFlatGallerySceneInternal();
        }

        string captureDirectory = Path.GetDirectoryName(capturePath) ?? string.Empty;
        Directory.CreateDirectory(captureDirectory);
        if (!string.IsNullOrWhiteSpace(captureMetaPath))
        {
            string captureMetaDirectory = Path.GetDirectoryName(captureMetaPath) ?? string.Empty;
            Directory.CreateDirectory(captureMetaDirectory);
        }

        Scene scene = EditorSceneManager.OpenScene(FlatGalleryScenePath, OpenSceneMode.Single);
        Canvas.ForceUpdateCanvases();

        GameObject elementObject = FindSceneObjectByName(scene, elementName);
        if (elementObject == null)
        {
            throw new InvalidOperationException(
                "GhostMetaUiSetBootstrap: flat gallery element '" + elementName + "' was not found in " + scene.path);
        }

        RectTransform rectTransform = elementObject.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            rectTransform = elementObject.GetComponentInChildren<RectTransform>(true);
        }

        if (rectTransform == null)
        {
            throw new InvalidOperationException(
                "GhostMetaUiSetBootstrap: flat gallery element '" + elementName + "' did not expose a RectTransform.");
        }

        CaptureRectTransformToPng(scene.path, elementName, rectTransform, capturePath, captureMetaPath);
        Debug.Log($"GhostMetaUiSetBootstrap: captured flat gallery element '{elementName}' to {capturePath}");
    }

    private static void OpenOrCreateSceneInternal(string scenePath, Action createSceneAction, bool rebuildScene)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        if (rebuildScene || !File.Exists(GetAbsoluteScenePath(scenePath)))
        {
            createSceneAction();
        }

        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
    }

    private static void CreateStudySceneInternal()
    {
        SceneSetup();
        SaveSceneAsset(ScenePath);
    }

    private static void CreateFlatGallerySceneInternal()
    {
        FlatGallerySceneSetup();
        SaveSceneAsset(FlatGalleryScenePath);
    }

    private static void CreateVideoPlayerProofSceneInternal()
    {
        VideoPlayerProofSceneSetup();
        SaveSceneAsset(VideoPlayerProofScenePath);
    }

    private static void ExportVideoPlayerProofInnerPieceInternal(bool rebuildScene)
    {
        OpenOrCreateSceneInternal(VideoPlayerProofScenePath, CreateVideoPlayerProofSceneInternal, rebuildScene);

        GameObject proofRoot = GameObject.Find("GhostMetaVideoPlayerProof");
        if (proofRoot == null)
        {
            throw new InvalidOperationException("GhostMetaUiSetBootstrap: GhostMetaVideoPlayerProof was not found after opening the proof scene.");
        }

        Selection.activeGameObject = proofRoot;
        EditorGUIUtility.PingObject(proofRoot);

        UnityBridgeResponse response = UnityBridgeInnerPieceFacade.ExportSelectionFromWindow(
            displayNameOverride: "Ghost Meta Video Player Proof",
            outputPath: UnityBridgeInnerPieceFacade.InnerPieceExportRoot,
            capturePreview: true,
            tagsCsv: "ghost,meta_ui,player,controls");

        if (response == null || !response.Ok)
        {
            string message = response != null
                ? response.Code + ": " + response.Message
                : "unknown export failure";
            throw new InvalidOperationException("GhostMetaUiSetBootstrap: video player proof export failed. " + message);
        }

        UnityInnerPieceLastExportSummary lastExport = UnityBridgeInnerPieceFacade.LastInnerPieceExport;
        if (lastExport == null)
        {
            throw new InvalidOperationException("GhostMetaUiSetBootstrap: export completed but no last export summary was recorded.");
        }

        Debug.Log(
            "GhostMetaUiSetBootstrap: exported Meta video player proof to " + lastExport.PackageRootPath +
            " | controls=" + lastExport.ControlsPath +
            " | screens=" + lastExport.ScreensPath);
    }

    private static void OpenImportedPackageScene(string importedScenePath)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        ImportPackagedUiSetScenesInternal(overwriteExisting: false);
        EditorSceneManager.OpenScene(importedScenePath, OpenSceneMode.Single);
    }

    private static void ImportPackagedUiSetScenesInternal(bool overwriteExisting)
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Scenes", "MetaUiSetSamples"));

        CopySceneFromPackage(PackageUiSetScenePath, ImportedUiSetScenePath, overwriteExisting);
        CopySceneFromPackage(PackageUiSetPatternsScenePath, ImportedUiSetPatternsScenePath, overwriteExisting);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            $"GhostMetaUiSetBootstrap: imported packaged Meta UI Set scenes to {ImportedPackageSceneDirectory}");
    }

    private static string GetAbsoluteScenePath(string scenePath)
    {
        string relativePath = scenePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(Directory.GetCurrentDirectory(), relativePath);
    }

    private static void SaveSceneAsset(string scenePath)
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Scenes"));
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"GhostMetaUiSetBootstrap: wrote {scenePath}");
    }

    private static void CaptureRectTransformToPng(
        string scenePath,
        string elementName,
        RectTransform rectTransform,
        string capturePath,
        string captureMetaPath)
    {
        if (rectTransform == null)
        {
            throw new ArgumentNullException(nameof(rectTransform));
        }

        Canvas.ForceUpdateCanvases();

        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        float surfaceWidthMeters = Vector3.Distance(corners[0], corners[3]);
        float surfaceHeightMeters = Vector3.Distance(corners[0], corners[1]);
        if (surfaceWidthMeters <= 0f || surfaceHeightMeters <= 0f)
        {
            throw new InvalidOperationException(
                "GhostMetaUiSetBootstrap: capture element '" + elementName + "' had zero-sized bounds.");
        }

        Vector3 center = (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;
        Vector3 forward = rectTransform.forward;
        Vector3 up = rectTransform.up;
        float aspect = Mathf.Max(0.01f, surfaceWidthMeters / surfaceHeightMeters);
        int longEdgePixels = 2048;
        int textureWidth = aspect >= 1f
            ? longEdgePixels
            : Mathf.Clamp(Mathf.RoundToInt(longEdgePixels * aspect), 256, longEdgePixels);
        int textureHeight = aspect >= 1f
            ? Mathf.Clamp(Mathf.RoundToInt(longEdgePixels / aspect), 256, longEdgePixels)
            : longEdgePixels;

        float paddingMeters = Mathf.Max(surfaceWidthMeters, surfaceHeightMeters) * 0.08f;

        GameObject cameraObject = new GameObject("MetaUiSetElementCaptureCamera");
        Camera captureCamera = cameraObject.AddComponent<Camera>();
        captureCamera.transform.position = center - (forward * 1.5f);
        captureCamera.transform.rotation = Quaternion.LookRotation(forward, up);
        captureCamera.orthographic = true;
        captureCamera.orthographicSize = Mathf.Max(
            (surfaceHeightMeters + (paddingMeters * 2f)) * 0.5f,
            ((surfaceWidthMeters + (paddingMeters * 2f)) * 0.5f) / aspect);
        captureCamera.clearFlags = CameraClearFlags.SolidColor;
        captureCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        captureCamera.nearClipPlane = 0.01f;
        captureCamera.farClipPlane = 10f;
        captureCamera.allowHDR = false;
        captureCamera.allowMSAA = false;

        RenderTexture renderTexture = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.ARGB32);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = captureCamera.targetTexture;
        Texture2D captureTexture = null;
        try
        {
            renderTexture.antiAliasing = 1;
            captureCamera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            captureCamera.Render();

            captureTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            captureTexture.ReadPixels(new Rect(0, 0, textureWidth, textureHeight), 0, 0);
            captureTexture.Apply();
            File.WriteAllBytes(capturePath, captureTexture.EncodeToPNG());

            if (!string.IsNullOrWhiteSpace(captureMetaPath))
            {
                FlatGalleryElementCaptureMetadata metadata = new FlatGalleryElementCaptureMetadata();
                metadata.scenePath = scenePath ?? "";
                metadata.elementName = elementName ?? "";
                metadata.textureWidth = textureWidth;
                metadata.textureHeight = textureHeight;
                metadata.surfaceWidthMeters = surfaceWidthMeters;
                metadata.surfaceHeightMeters = surfaceHeightMeters;
                File.WriteAllText(captureMetaPath, JsonUtility.ToJson(metadata, true));
            }
        }
        finally
        {
            captureCamera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;

            if (captureTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(captureTexture);
            }

            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(renderTexture);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
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
            GameObject root = roots[i];
            if (root == null)
            {
                continue;
            }

            if (string.Equals(root.name, objectName, StringComparison.Ordinal))
            {
                return root;
            }

            Transform nested = FindChildRecursive(root.transform, objectName);
            if (nested != null)
            {
                return nested.gameObject;
            }
        }

        return null;
    }

    private static void SceneSetup()
    {
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var root = new GameObject("GhostMetaUiSetStudyRoot");

        EnsurePointableCanvasModule(root.transform);

        GameObject flatCanvas = InstantiateNamedPrefab(
            root.transform,
            new[] { "FlatUnityCanvas" },
            new Vector3(-0.95f, 1.3f, 1.8f),
            new Vector3(0f, 20f, 0f),
            "MetaFlatCanvas");

        GameObject curvedCanvas = InstantiateNamedPrefab(
            root.transform,
            new[] { "CurvedUnityCanvas" },
            new Vector3(0.95f, 1.3f, 1.8f),
            new Vector3(0f, -20f, 0f),
            "MetaCurvedCanvas");

        GameObject customPanel = InstantiateNamedPrefab(
            root.transform,
            new[] { "EmptyUIBackplateWithCanvas" },
            new Vector3(0f, 1.2f, 1.4f),
            Vector3.zero,
            "GhostCustomPanel");

        if (customPanel != null)
        {
            PopulateCustomPanel(customPanel.transform);
        }

        if (flatCanvas == null && curvedCanvas == null && customPanel == null)
        {
            throw new InvalidOperationException(
                "Meta UI Set prefabs were not found. Confirm com.meta.xr.sdk.interaction.ovr is installed and available in the project.");
        }
    }

    private static void FlatGallerySceneSetup()
    {
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var root = new GameObject("GhostMetaUiSetFlatGalleryRoot");

        EnsurePointableCanvasModule(root.transform);
        ConfigurePreviewCamera(new Vector3(0f, 1.55f, -4.8f), new Vector3(0f, 1.35f, 2.5f), 30f);
        ConfigureDirectionalLight();

        CreateGalleryPanel(root.transform, "ButtonsPanel", new Vector3(-2.55f, 1.95f, 2.5f), PopulateButtonsPanel, 0.95f);
        CreateGalleryPanel(root.transform, "ControlsPanel", new Vector3(-0.85f, 1.95f, 2.5f), PopulateControlsPanel, 0.95f);
        CreateGalleryPanel(root.transform, "SlidersPanel", new Vector3(0.85f, 1.95f, 2.5f), PopulateSlidersPanel, 0.95f);
        CreateGalleryPanel(root.transform, "DialogsPanel", new Vector3(2.55f, 1.95f, 2.5f), PopulateDialogsPanel, 0.95f);

        CreateGalleryPanel(root.transform, "DropDownPanel", new Vector3(-2.55f, 0.72f, 2.5f), PopulateDropDownPanel, 0.95f);
        CreateGalleryPanel(root.transform, "TextInputPanel", new Vector3(-0.85f, 0.72f, 2.5f), PopulateTextInputPanel, 0.95f);
        CreateGalleryPanel(root.transform, "TooltipPanel", new Vector3(0.85f, 0.72f, 2.5f), PopulateTooltipPanel, 0.95f);
        CreateGalleryPanel(root.transform, "PatternsPanel", new Vector3(2.55f, 0.72f, 2.5f), PopulatePatternsPanel, 0.95f);
    }

    private static void VideoPlayerProofSceneSetup()
    {
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var root = new GameObject("GhostMetaUiSetVideoPlayerProofRoot");

        EnsurePointableCanvasModule(root.transform);
        ConfigurePreviewCamera(new Vector3(0f, 1.42f, -2.1f), new Vector3(0f, 1.32f, 1.95f), 38f);
        ConfigureDirectionalLight();

        GameObject videoPlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackageVideoPlayerPrefabPath);
        if (videoPlayerPrefab == null)
        {
            throw new FileNotFoundException("Meta UI Set video player prefab was not found.", PackageVideoPlayerPrefabPath);
        }

        GameObject videoPlayerInstance = PrefabUtility.InstantiatePrefab(videoPlayerPrefab) as GameObject;
        if (videoPlayerInstance == null)
        {
            throw new InvalidOperationException("Failed to instantiate the Meta UI Set video player prefab.");
        }

        videoPlayerInstance.name = "GhostMetaVideoPlayerProof";
        videoPlayerInstance.transform.SetParent(root.transform, false);
        videoPlayerInstance.transform.localPosition = new Vector3(0f, 1.3f, 1.9f);
        videoPlayerInstance.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        videoPlayerInstance.transform.localScale = Vector3.one;

        ConfigureVideoPlayerProof(videoPlayerInstance);
    }

    private static void PopulateCustomPanel(Transform panelRoot)
    {
        Transform canvasRoot = FindChildRecursive(panelRoot, "CanvasRoot") ?? FindChildRecursive(panelRoot, "Canvas");
        if (canvasRoot == null)
        {
            Debug.LogWarning("GhostMetaUiSetBootstrap: custom panel prefab did not expose CanvasRoot/Canvas; leaving it empty.");
            return;
        }

        GameObject primaryButton = InstantiateNamedPrefab(
            canvasRoot,
            new[] { "PrimaryButton_IconAndLabel_UnityUIButton", "PrimaryButton_UnityUIButton", "PrimaryButton" },
            Vector3.zero,
            Vector3.zero,
            "PrimaryButton");
        SetAnchoredPosition(primaryButton, new Vector2(0f, 28f));

        GameObject slider = InstantiateNamedPrefab(
            canvasRoot,
            new[] { "Slider", "DefaultSlider", "UISlider" },
            Vector3.zero,
            Vector3.zero,
            "Slider");
        SetAnchoredPosition(slider, new Vector2(0f, -24f));

        GameObject toggle = InstantiateNamedPrefab(
            canvasRoot,
            new[] { "Switch", "Toggle", "UIToggle" },
            Vector3.zero,
            Vector3.zero,
            "Toggle");
        SetAnchoredPosition(toggle, new Vector2(0f, -78f));
    }

    private static void PopulateButtonsPanel(Transform panelRoot)
    {
        Transform canvasRoot = ResolveCanvasRoot(panelRoot);
        if (canvasRoot == null)
        {
            return;
        }

        CreateSectionTitle(canvasRoot, "Buttons");
        InstantiateUiPrefab(canvasRoot, new[] { "PrimaryButton_IconAndLabel_UnityUIButton", "PrimaryButton_IconAndLabel" }, new Vector2(0f, 36f), "PrimaryButton", 1f);
        InstantiateUiPrefab(canvasRoot, new[] { "SecondaryButton_IconAndLabel_UnityUIButton", "SecondaryButton_IconAndLabel" }, new Vector2(0f, -6f), "SecondaryButton", 1f);
        InstantiateUiPrefab(canvasRoot, new[] { "DestructiveButton_IconAndLabel_UnityUIButton", "DestructiveButton_IconAndLabel" }, new Vector2(0f, -48f), "DestructiveButton", 1f);
        InstantiateUiPrefab(canvasRoot, new[] { "ButtonShelf_IconAndLabel_Toggle" }, new Vector2(0f, -96f), "ButtonShelf", 0.92f);
    }

    private static void PopulateControlsPanel(Transform panelRoot)
    {
        Transform canvasRoot = ResolveCanvasRoot(panelRoot);
        if (canvasRoot == null)
        {
            return;
        }

        CreateSectionTitle(canvasRoot, "Controls");
        InstantiateUiPrefab(canvasRoot, new[] { "ToggleButton_Switch" }, new Vector2(0f, 42f), "ToggleSwitch", 0.95f);
        InstantiateUiPrefab(canvasRoot, new[] { "ToggleButton_Checkbox" }, new Vector2(0f, -2f), "ToggleCheckbox", 0.95f);
        InstantiateUiPrefab(canvasRoot, new[] { "ToggleButton_Radio" }, new Vector2(0f, -46f), "ToggleRadio", 0.95f);
        InstantiateUiPrefab(canvasRoot, new[] { "TextTileButton_IconAndLabel_Toggle" }, new Vector2(0f, -96f), "ToggleTile", 0.85f);
    }

    private static void PopulateSlidersPanel(Transform panelRoot)
    {
        Transform canvasRoot = ResolveCanvasRoot(panelRoot);
        if (canvasRoot == null)
        {
            return;
        }

        CreateSectionTitle(canvasRoot, "Sliders");
        InstantiateUiPrefab(canvasRoot, new[] { "SmallSlider" }, new Vector2(0f, 52f), "SmallSlider", 0.95f);
        InstantiateUiPrefab(canvasRoot, new[] { "MediumSlider" }, new Vector2(0f, 4f), "MediumSlider", 0.95f);
        InstantiateUiPrefab(canvasRoot, new[] { "LargeSlider" }, new Vector2(0f, -54f), "LargeSlider", 0.9f);
    }

    private static void PopulateDialogsPanel(Transform panelRoot)
    {
        Transform canvasRoot = ResolveCanvasRoot(panelRoot);
        if (canvasRoot == null)
        {
            return;
        }

        CreateSectionTitle(canvasRoot, "Dialogs");
        InstantiateUiPrefab(canvasRoot, new[] { "Dialog1Button_TextOnly" }, new Vector2(0f, 32f), "DialogSingle", 0.62f);
        InstantiateUiPrefab(canvasRoot, new[] { "Dialog2Button_TextOnly" }, new Vector2(0f, -56f), "DialogDouble", 0.62f);
    }

    private static void PopulateDropDownPanel(Transform panelRoot)
    {
        Transform canvasRoot = ResolveCanvasRoot(panelRoot);
        if (canvasRoot == null)
        {
            return;
        }

        CreateSectionTitle(canvasRoot, "Drop Down");
        InstantiateUiPrefab(canvasRoot, new[] { "DropDown1LineTextOnly" }, new Vector2(0f, 34f), "DropDownTextOnly", 0.92f);
        InstantiateUiPrefab(canvasRoot, new[] { "DropDownIconAnd1LineText" }, new Vector2(0f, -16f), "DropDownIconText", 0.92f);
        InstantiateUiPrefab(canvasRoot, new[] { "DropDownIconAnd2LineText" }, new Vector2(0f, -78f), "DropDownTwoLine", 0.86f);
    }

    private static void PopulateTextInputPanel(Transform panelRoot)
    {
        Transform canvasRoot = ResolveCanvasRoot(panelRoot);
        if (canvasRoot == null)
        {
            return;
        }

        CreateSectionTitle(canvasRoot, "Text Input");
        InstantiateUiPrefab(canvasRoot, new[] { "SearchBar" }, new Vector2(0f, 28f), "SearchBar", 0.92f);
        InstantiateUiPrefab(canvasRoot, new[] { "TextInputField" }, new Vector2(0f, -36f), "TextInputField", 0.92f);
    }

    private static void PopulateTooltipPanel(Transform panelRoot)
    {
        Transform canvasRoot = ResolveCanvasRoot(panelRoot);
        if (canvasRoot == null)
        {
            return;
        }

        CreateSectionTitle(canvasRoot, "Tooltip / Menu");
        InstantiateUiPrefab(canvasRoot, new[] { "Tooltip" }, new Vector2(0f, 40f), "Tooltip", 0.9f);
        InstantiateUiPrefab(canvasRoot, new[] { "ContextMenu1LineTextOnly" }, new Vector2(0f, -18f), "ContextMenuText", 0.82f);
        InstantiateUiPrefab(canvasRoot, new[] { "ContextMenuIconAnd1LineText" }, new Vector2(0f, -80f), "ContextMenuIcon", 0.82f);
    }

    private static void PopulatePatternsPanel(Transform panelRoot)
    {
        Transform canvasRoot = ResolveCanvasRoot(panelRoot);
        if (canvasRoot == null)
        {
            return;
        }

        CreateSectionTitle(canvasRoot, "Patterns");
        InstantiateUiPrefab(canvasRoot, new[] { "GridMenuExample2x4" }, new Vector2(0f, 8f), "GridMenu", 0.52f);
        InstantiateUiPrefab(canvasRoot, new[] { "ContentUIExample-HorizonOS1", "ContentUIExample1" }, new Vector2(0f, -86f), "ContentExample", 0.42f);
    }

    private static void EnsurePointableCanvasModule(Transform root)
    {
        Type pointableCanvasModuleType = FindTypeByName("PointableCanvasModule");
        if (pointableCanvasModuleType == null)
        {
            Debug.LogWarning("GhostMetaUiSetBootstrap: PointableCanvasModule type was not found; scene will still be created.");
            return;
        }

        var canvasModule = new GameObject("CanvasModule");
        canvasModule.transform.SetParent(root, false);
        canvasModule.AddComponent(pointableCanvasModuleType);
    }

    private static void ConfigurePreviewCamera(Vector3 position, Vector3 lookAtPoint, float fieldOfView)
    {
        Camera mainCamera = Camera.main ?? UnityEngine.Object.FindObjectOfType<Camera>();
        if (mainCamera == null)
        {
            return;
        }

        mainCamera.transform.position = position;
        mainCamera.transform.rotation = Quaternion.LookRotation(lookAtPoint - position, Vector3.up);
        mainCamera.fieldOfView = fieldOfView;
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color(0.93f, 0.95f, 0.97f, 1f);
    }

    private static void ConfigureDirectionalLight()
    {
        Light mainLight = UnityEngine.Object.FindObjectOfType<Light>();
        if (mainLight == null)
        {
            return;
        }

        mainLight.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
        mainLight.intensity = 1.1f;
    }

    private static void CreateGalleryPanel(
        Transform parent,
        string panelName,
        Vector3 localPosition,
        Action<Transform> populateAction,
        float uniformScale)
    {
        GameObject panel = InstantiateNamedPrefab(
            parent,
            new[] { "EmptyUIBackplateWithCanvas" },
            localPosition,
            Vector3.zero,
            panelName);

        if (panel == null)
        {
            return;
        }

        panel.transform.localScale = Vector3.one * uniformScale;
        populateAction(panel.transform);
    }

    private static GameObject InstantiateNamedPrefab(
        Transform parent,
        IEnumerable<string> candidateNames,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        string instanceName)
    {
        foreach (string candidateName in candidateNames)
        {
            GameObject prefab = FindPrefabByName(candidateName);
            if (prefab == null)
            {
                continue;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                continue;
            }

            instance.name = instanceName;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPosition;
            instance.transform.localEulerAngles = localEulerAngles;
            return instance;
        }

        Debug.LogWarning(
            $"GhostMetaUiSetBootstrap: none of the candidate prefabs were found for {instanceName}: {string.Join(", ", candidateNames)}");
        return null;
    }

    private static GameObject FindPrefabByName(string prefabName)
    {
        string[] guids = AssetDatabase.FindAssets(prefabName + " t:Prefab");
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (assetPath.IndexOf("UISet", StringComparison.OrdinalIgnoreCase) < 0 &&
                assetPath.IndexOf("Canvas", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        }

        return null;
    }

    private static void ConfigureVideoPlayerProof(GameObject videoPlayerInstance)
    {
        if (videoPlayerInstance == null)
        {
            return;
        }

        RoundedBoxVideoController legacyController = videoPlayerInstance.GetComponent<RoundedBoxVideoController>();
        GhostMetaVideoPlayerProof proofBinder = videoPlayerInstance.GetComponent<GhostMetaVideoPlayerProof>();
        if (proofBinder == null)
        {
            proofBinder = videoPlayerInstance.AddComponent<GhostMetaVideoPlayerProof>();
        }

        if (legacyController != null)
        {
            proofBinder.legacyController = legacyController;
            proofBinder.timeSlider = legacyController.timeSlider;
            proofBinder.playPauseImage = legacyController.playPauseImg;
            proofBinder.playIcon = legacyController.playIcon;
            proofBinder.pauseIcon = legacyController.pauseIcon;
            proofBinder.leftLabel = legacyController.leftLabel;
            proofBinder.rightLabel = legacyController.rightLabel;
            proofBinder.backgroundImage = legacyController.backgroundImage;
        }

        Transform demoVideoContent = FindChildRecursive(videoPlayerInstance.transform, "DemoVideoContent");
        if (demoVideoContent != null)
        {
            proofBinder.demoVideoContent = demoVideoContent as RectTransform;
        }

        if (proofBinder.playPauseImage != null)
        {
            proofBinder.playPauseToggle = proofBinder.playPauseImage.GetComponentInParent<Toggle>(true);
        }

        proofBinder.initialVideoClip = AssetDatabase.LoadAssetAtPath<VideoClip>(PackageUiSetInstructionVideoPath);
        proofBinder.playOnStart = true;
        proofBinder.loop = true;

        GhostMetaControlSurfaceProofMetadata controlSurfaceMetadata =
            videoPlayerInstance.GetComponent<GhostMetaControlSurfaceProofMetadata>();
        if (controlSurfaceMetadata == null)
        {
            controlSurfaceMetadata = videoPlayerInstance.AddComponent<GhostMetaControlSurfaceProofMetadata>();
        }

        controlSurfaceMetadata.ConfigureForVideoPlayerProof(proofBinder);

        if (legacyController != null)
        {
            UnityEngine.Object.DestroyImmediate(legacyController);
        }
    }

    private static Transform ResolveCanvasRoot(Transform panelRoot)
    {
        Transform canvasRoot = FindChildRecursive(panelRoot, "CanvasRoot") ?? FindChildRecursive(panelRoot, "Canvas");
        if (canvasRoot == null)
        {
            Debug.LogWarning($"GhostMetaUiSetBootstrap: panel '{panelRoot.name}' did not expose CanvasRoot/Canvas.");
        }

        return canvasRoot;
    }

    private static void CreateSectionTitle(Transform canvasRoot, string title)
    {
        var titleObject = new GameObject("SectionTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObject.transform.SetParent(canvasRoot, false);
        var rectTransform = titleObject.transform as RectTransform;
        if (rectTransform != null)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(0f, 106f);
            rectTransform.sizeDelta = new Vector2(260f, 28f);
        }

        var label = titleObject.GetComponent<TextMeshProUGUI>();
        label.text = title;
        label.fontSize = 18f;
        label.color = new Color(0.92f, 0.94f, 0.97f, 1f);
        label.alignment = TextAlignmentOptions.Center;
    }

    private static GameObject InstantiateUiPrefab(
        Transform canvasRoot,
        IEnumerable<string> candidateNames,
        Vector2 anchoredPosition,
        string instanceName,
        float uniformScale)
    {
        GameObject instance = InstantiateNamedPrefab(canvasRoot, candidateNames, Vector3.zero, Vector3.zero, instanceName);
        if (instance == null)
        {
            return null;
        }

        instance.transform.localScale = Vector3.one * uniformScale;
        SetAnchoredPosition(instance, anchoredPosition);
        return instance;
    }

    private static void SetAnchoredPosition(GameObject instance, Vector2 anchoredPosition)
    {
        if (instance == null)
        {
            return;
        }

        if (instance.transform is RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
        }
    }

    private static Transform FindChildRecursive(Transform root, string name)
    {
        foreach (Transform child in root)
        {
            if (string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, name);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static Type FindTypeByName(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).Cast<Type>().ToArray();
            }

            Type match = types.FirstOrDefault(t => string.Equals(t.Name, typeName, StringComparison.Ordinal));
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static string GetCommandLineArgument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return string.Empty;
    }

    private static void CopySceneFromPackage(string packageScenePath, string destinationScenePath, bool overwriteExisting)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(packageScenePath) == null)
        {
            throw new FileNotFoundException(
                "Meta UI Set package scene was not found. Confirm com.meta.xr.sdk.interaction is installed.",
                packageScenePath);
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(destinationScenePath) != null)
        {
            if (!overwriteExisting)
            {
                return;
            }

            AssetDatabase.DeleteAsset(destinationScenePath);
        }

        bool copied = AssetDatabase.CopyAsset(packageScenePath, destinationScenePath);
        if (!copied)
        {
            throw new InvalidOperationException(
                $"GhostMetaUiSetBootstrap: failed to copy package scene '{packageScenePath}' to '{destinationScenePath}'.");
        }
    }
}
