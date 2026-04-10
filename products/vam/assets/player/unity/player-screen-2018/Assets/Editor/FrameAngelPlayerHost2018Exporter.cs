using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class FrameAngelPlayerHost2018Exporter
{
    private const string AssetRoot = "Assets/FrameAngel/PlayerScreen";
    private const string PrefabPath = AssetRoot + "/fa_player_screen.prefab";
    private const string MaterialsRoot = AssetRoot + "/Materials";
    private const string TexturesRoot = AssetRoot + "/Textures";
    private const string DefaultBundleFileName = "fa_player_screen_core_current.assetbundle";
    private const string DefaultOutputRoot = "C:/projects/fa/products/vam/assets/player/build/player_screen_current";
    private const string DefaultDeployAssetsRoot = "F:/sim/vam/Custom/Assets/FrameAngel/Player";
    private const string DefaultDeployPresetRoot = "F:/sim/vam/Custom/Atom/CustomUnityAsset";
    private const string DefaultPresetFileName = "Preset_FA Player Screen Core Current.vap";
    private const string SummaryFileName = "player_screen_summary.json";

    private sealed class ExportOptions
    {
        public string OutputRoot = DefaultOutputRoot;
        public string DeployAssetsRoot = DefaultDeployAssetsRoot;
        public string DeployPresetRoot = DefaultDeployPresetRoot;
        public string BundleFileName = DefaultBundleFileName;
        public string PresetFileName = DefaultPresetFileName;
        public bool Deploy = true;
        public bool WritePreset = true;
    }

    [Serializable]
    private class ExportSummary
    {
        public string schemaVersion = "frameangel_player_host_2018_export_summary_v1";
        public string generatedAtUtc;
        public string unityVersion;
        public string prefabPath;
        public string bundlePath;
        public string assetName;
        public string presetPath;
        public string deployBundlePath;
        public string deployPresetPath;
        public string assetUrl;
    }

    [MenuItem("FrameAngel/Build FA Player Screen Core CUA (VaM 2018)")]
    public static void BuildMenu()
    {
        BuildAndDeploy(CreateDefaultOptions());
    }

    public static void BuildAndDeployBatch()
    {
        BuildAndDeploy(ParseBatchOptions());
    }

    private static ExportOptions CreateDefaultOptions()
    {
        return new ExportOptions();
    }

    private static ExportOptions ParseBatchOptions()
    {
        string[] args = Environment.GetCommandLineArgs();
        ExportOptions options = CreateDefaultOptions();
        options.OutputRoot = GetArg(args, "-faOutputRoot", options.OutputRoot);
        options.DeployAssetsRoot = GetArg(args, "-faDeployAssetsRoot", options.DeployAssetsRoot);
        options.DeployPresetRoot = GetArg(args, "-faDeployPresetRoot", options.DeployPresetRoot);
        options.BundleFileName = GetArg(args, "-faBundleFileName", options.BundleFileName);
        options.PresetFileName = GetArg(args, "-faPresetFileName", options.PresetFileName);
        options.Deploy = GetBoolArg(args, "-faDeploy", true);
        options.WritePreset = GetBoolArg(args, "-faWritePreset", true);
        return options;
    }

    private static void BuildAndDeploy(ExportOptions options)
    {
        EnsureFolder("Assets/FrameAngel");
        EnsureFolder(AssetRoot);
        EnsureFolder(MaterialsRoot);
        EnsureFolder(TexturesRoot);

        Material bodyMaterial = CreateOpaqueStandardMaterial(
            MaterialsRoot + "/g007.player_host.body.style.mat",
            new Color(0.09019608f, 0.10980392f, 0.14117648f, 1f));
        Material disconnectMaterial = CreateOpaqueStandardMaterial(
            MaterialsRoot + "/g007.player_host.disconnect_mask.style.mat",
            new Color(0.02f, 0.02f, 0.02f, 1f));
        Material screenMaterial = CreateUnlitTextureMaterial(
            MaterialsRoot + "/g007.player_host.screen.style.mat",
            TexturesRoot + "/g007.player_host.screen.idle.png");
        Material controlPanelMaterial = CreateTransparentStandardMaterial(
            MaterialsRoot + "/g007.player_host.control_panel.style.mat",
            new Color(0.08235294f, 0.101960786f, 0.13333334f, 0.94f));
        Material controlRailMaterial = CreateOpaqueStandardMaterial(
            MaterialsRoot + "/g007.player_host.control_rail.style.mat",
            new Color(0.18039216f, 0.21176471f, 0.25882354f, 1f));
        Material controlButtonMaterial = CreateOpaqueStandardMaterial(
            MaterialsRoot + "/g007.player_host.control_button.style.mat",
            new Color(0.72156864f, 0.75686276f, 0.8117647f, 1f));
        Material controlAccentMaterial = CreateOpaqueStandardMaterial(
            MaterialsRoot + "/g007.player_host.control_accent.style.mat",
            new Color(0.12156863f, 0.78039217f, 0.40784314f, 1f));

        GameObject root = BuildHierarchy(
            bodyMaterial,
            disconnectMaterial,
            screenMaterial,
            controlPanelMaterial,
            controlRailMaterial,
            controlButtonMaterial,
            controlAccentMaterial);

        try
        {
            string prefabAbsolutePath = Path.Combine(Directory.GetCurrentDirectory(), PrefabPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(prefabAbsolutePath))
                AssetDatabase.DeleteAsset(PrefabPath);

            PrefabUtility.CreatePrefab(PrefabPath, root, ReplacePrefabOptions.ReplaceNameBased);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        string bundleOutputRoot = Path.Combine(options.OutputRoot, "assetbundles");
        string presetOutputRoot = Path.Combine(options.OutputRoot, "presets");
        Directory.CreateDirectory(bundleOutputRoot);
        if (options.WritePreset)
            Directory.CreateDirectory(presetOutputRoot);
        if (options.Deploy && !string.IsNullOrEmpty(options.DeployAssetsRoot))
            Directory.CreateDirectory(options.DeployAssetsRoot);
        if (options.Deploy && options.WritePreset && !string.IsNullOrEmpty(options.DeployPresetRoot))
            Directory.CreateDirectory(options.DeployPresetRoot);

        AssetBundleBuild build = new AssetBundleBuild();
        build.assetBundleName = options.BundleFileName;
        build.assetNames = new[] { PrefabPath };

        BuildPipeline.BuildAssetBundles(
            bundleOutputRoot,
            new[] { build },
            BuildAssetBundleOptions.None,
            BuildTarget.StandaloneWindows64);

        string bundlePath = Path.Combine(bundleOutputRoot, options.BundleFileName);
        if (!File.Exists(bundlePath))
            throw new InvalidOperationException("Bundle was not written: " + bundlePath);

        string assetName = ResolveFirstAssetName(bundlePath);
        string deployedBundlePath = string.Empty;
        string outputPresetPath = string.Empty;
        string deployedPresetPath = string.Empty;
        string assetUrl = "Custom/Assets/FrameAngel/Player/" + options.BundleFileName;

        if (options.WritePreset)
        {
            string presetJson = BuildCustomUnityAssetPresetJson(assetUrl, assetName);
            outputPresetPath = Path.Combine(presetOutputRoot, options.PresetFileName);
            WriteTextFile(outputPresetPath, presetJson);

            if (options.Deploy && !string.IsNullOrEmpty(options.DeployPresetRoot))
            {
                deployedPresetPath = Path.Combine(options.DeployPresetRoot, options.PresetFileName);
                WriteTextFile(deployedPresetPath, presetJson);
            }
        }

        if (options.Deploy && !string.IsNullOrEmpty(options.DeployAssetsRoot))
        {
            deployedBundlePath = Path.Combine(options.DeployAssetsRoot, options.BundleFileName);
            File.Copy(bundlePath, deployedBundlePath, true);
        }

        ExportSummary summary = new ExportSummary();
        summary.generatedAtUtc = DateTime.UtcNow.ToString("o");
        summary.unityVersion = Application.unityVersion;
        summary.prefabPath = PrefabPath;
        summary.bundlePath = bundlePath;
        summary.assetName = assetName;
        summary.presetPath = outputPresetPath;
        summary.deployBundlePath = deployedBundlePath;
        summary.deployPresetPath = deployedPresetPath;
        summary.assetUrl = assetUrl;

        WriteTextFile(Path.Combine(options.OutputRoot, SummaryFileName), JsonUtility.ToJson(summary, true));
        Debug.Log("FrameAngelPlayerHost2018Exporter: built " + bundlePath);
    }

    private static string GetArg(string[] args, string name, string defaultValue)
    {
        if (args == null)
            return defaultValue;

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return defaultValue;
    }

    private static bool GetBoolArg(string[] args, string name, bool defaultValue)
    {
        string value = GetArg(args, name, defaultValue ? "true" : "false");
        bool parsed;
        if (bool.TryParse(value, out parsed))
            return parsed;

        return defaultValue;
    }

    private static GameObject BuildHierarchy(
        Material bodyMaterial,
        Material disconnectMaterial,
        Material screenMaterial,
        Material controlPanelMaterial,
        Material controlRailMaterial,
        Material controlButtonMaterial,
        Material controlAccentMaterial)
    {
        GameObject root = new GameObject("fa_player_screen_core");

        // The bare screen-core asset should read like content with a backing, not
        // like a framed TV. Author the visible screen around a bottom-center root so
        // the CUA pivot and resize anchor feel attached to the actual display instead
        // of floating well below it.
        CreateCube(root.transform, "screen_body",
            new Vector3(0f, 0.45f, -0.0012f), new Vector3(1.6f, 0.9f, 0.008f), bodyMaterial);
        CreateQuad(root.transform, "screen_surface",
            new Vector3(0f, 0.45f, 0.0068f), new Vector2(1.6f, 0.9f), screenMaterial);
        CreateQuad(root.transform, "disconnect_surface",
            new Vector3(0f, 0.45f, 0.0060f), new Vector2(1.6f, 0.9f), disconnectMaterial);
        CreatePlayerControlSurface(
            root.transform,
            new Vector3(0f, -0.205f, 0.0105f),
            controlPanelMaterial,
            controlRailMaterial,
            controlButtonMaterial,
            controlAccentMaterial);

        CreateAnchor(root.transform, "bottom_anchor", new Vector3(0f, 0f, 0.008f));
        CreateAnchor(root.transform, "controls_anchor", new Vector3(0f, -0.205f, 0.0105f));
        return root;
    }

    private static void CreatePlayerControlSurface(
        Transform parent,
        Vector3 localPosition,
        Material panelMaterial,
        Material railMaterial,
        Material buttonMaterial,
        Material accentMaterial)
    {
        const float panelWidth = 0.460800022f;
        const float panelHeight = 0.309600025f;
        const float surfaceDepth = 0.0006f;
        Vector2 panelSize = new Vector2(panelWidth, panelHeight);

        GameObject controlSurfaceRoot = CreateGroup(parent, "control_surface", localPosition);
        CreateQuad(controlSurfaceRoot.transform, "control_panel_background", Vector3.zero, panelSize, panelMaterial);
        CreateQuad(controlSurfaceRoot.transform, "control_scrub_normalized", BuildRectLocalPosition(panelSize, 0.16f, 0.68f, 0.78f, 0.06f, surfaceDepth), BuildRectLocalSize(panelSize, 0.78f, 0.06f), railMaterial);
        CreateQuad(controlSurfaceRoot.transform, "control_volume_normalized", BuildRectLocalPosition(panelSize, 0.05f, 0.24f, 0.05f, 0.48f, surfaceDepth), BuildRectLocalSize(panelSize, 0.05f, 0.48f), railMaterial);
        CreateQuad(controlSurfaceRoot.transform, "control_mute_toggle", BuildRectLocalPosition(panelSize, 0.03f, 0.08f, 0.09f, 0.10f, surfaceDepth), BuildRectLocalSize(panelSize, 0.09f, 0.10f), buttonMaterial);
        CreateQuad(controlSurfaceRoot.transform, "control_skip_backward", BuildRectLocalPosition(panelSize, 0.20f, 0.20f, 0.09f, 0.14f, surfaceDepth), BuildRectLocalSize(panelSize, 0.09f, 0.14f), buttonMaterial);
        CreateQuad(controlSurfaceRoot.transform, "control_previous", BuildRectLocalPosition(panelSize, 0.32f, 0.20f, 0.09f, 0.14f, surfaceDepth), BuildRectLocalSize(panelSize, 0.09f, 0.14f), buttonMaterial);
        CreateQuad(controlSurfaceRoot.transform, "control_play_pause", BuildRectLocalPosition(panelSize, 0.44f, 0.18f, 0.12f, 0.18f, surfaceDepth), BuildRectLocalSize(panelSize, 0.12f, 0.18f), accentMaterial);
        CreateQuad(controlSurfaceRoot.transform, "control_next", BuildRectLocalPosition(panelSize, 0.59f, 0.20f, 0.09f, 0.14f, surfaceDepth), BuildRectLocalSize(panelSize, 0.09f, 0.14f), buttonMaterial);
        CreateQuad(controlSurfaceRoot.transform, "control_skip_forward", BuildRectLocalPosition(panelSize, 0.71f, 0.20f, 0.09f, 0.14f, surfaceDepth), BuildRectLocalSize(panelSize, 0.09f, 0.14f), buttonMaterial);
    }

    private static GameObject CreateGroup(Transform parent, string name, Vector3 localPosition)
    {
        GameObject group = new GameObject(name);
        group.transform.SetParent(parent, false);
        group.transform.localPosition = localPosition;
        group.transform.localRotation = Quaternion.identity;
        group.transform.localScale = Vector3.one;
        return group;
    }

    private static Vector3 BuildRectLocalPosition(Vector2 panelSize, float x, float y, float width, float height, float z)
    {
        float centerX = ((x + (width * 0.5f)) - 0.5f) * panelSize.x;
        float centerY = ((y + (height * 0.5f)) - 0.5f) * panelSize.y;
        return new Vector3(centerX, centerY, z);
    }

    private static Vector2 BuildRectLocalSize(Vector2 panelSize, float width, float height)
    {
        return new Vector2(panelSize.x * width, panelSize.y * height);
    }

    private static void CreateCube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localScale = localScale;

        Collider collider = cube.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.DestroyImmediate(collider, true);

        MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.receiveShadows = true;
            renderer.shadowCastingMode = ShadowCastingMode.On;
        }
    }

    private static void CreateQuad(Transform parent, string name, Vector3 localPosition, Vector2 localSize, Material material)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = localPosition;
        quad.transform.localRotation = Quaternion.identity;
        quad.transform.localScale = new Vector3(localSize.x, localSize.y, 1f);

        Collider collider = quad.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.DestroyImmediate(collider, true);

        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.receiveShadows = false;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    private static void CreateAnchor(Transform parent, string name, Vector3 localPosition)
    {
        GameObject anchor = new GameObject(name);
        anchor.transform.SetParent(parent, false);
        anchor.transform.localPosition = localPosition;
        anchor.transform.localRotation = Quaternion.identity;
        anchor.transform.localScale = Vector3.one;
    }

    private static Material CreateOpaqueStandardMaterial(string path, Color color)
    {
        Material material = LoadOrCreateMaterial(path, Shader.Find("Standard"));
        material.color = color;
        material.SetFloat("_Mode", 0f);
        material.SetInt("_SrcBlend", (int)BlendMode.One);
        material.SetInt("_DstBlend", (int)BlendMode.Zero);
        material.SetInt("_ZWrite", 1);
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = -1;
        material.SetOverrideTag("RenderType", string.Empty);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateTransparentStandardMaterial(string path, Color color)
    {
        Material material = LoadOrCreateMaterial(path, Shader.Find("Standard"));
        material.color = color;
        material.SetFloat("_Mode", 3f);
        material.SetInt("_SrcBlend", (int)BlendMode.One);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHABLEND_ON");
        material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
        material.SetOverrideTag("RenderType", "Transparent");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateUnlitTextureMaterial(string path, string idleTexturePath)
    {
        Shader shader = Shader.Find("Unlit/Texture");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = LoadOrCreateMaterial(path, shader);
        material.color = Color.white;
        Texture2D idleTexture = LoadOrCreateIdleScreenTexture(idleTexturePath);
        if (idleTexture != null)
            material.mainTexture = idleTexture;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D LoadOrCreateIdleScreenTexture(string path)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture != null)
            return texture;

        string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), path.Replace('/', Path.DirectorySeparatorChar));
        byte[] png = CreateSolidColorPng(8, 8, new Color32(5, 5, 5, 255));
        File.WriteAllBytes(absolutePath, png);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static byte[] CreateSolidColorPng(int width, int height, Color32 color)
    {
        return Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAYAAADED76LAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAATSURBVChTY2BlZf2PD48IBaz/AXNGQ4GWJaKyAAAAAElFTkSuQmCC");
    }

    private static Material LoadOrCreateMaterial(string path, Shader shader)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
        }

        return material;
    }

    private static string ResolveFirstAssetName(string bundlePath)
    {
        AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
        if (bundle == null)
            throw new InvalidOperationException("Unable to load built bundle: " + bundlePath);

        try
        {
            string[] assetNames = bundle.GetAllAssetNames();
            if (assetNames == null || assetNames.Length == 0)
                throw new InvalidOperationException("Built bundle contains no asset names: " + bundlePath);

            return assetNames[0];
        }
        finally
        {
            bundle.Unload(true);
        }
    }

    private static string BuildCustomUnityAssetPresetJson(string assetUrl, string assetName)
    {
        return "{\n"
            + "  \"setUnlistedParamsToDefault\" : \"true\",\n"
            + "  \"storables\" : [\n"
            + "    {\n"
            + "      \"id\" : \"PhysicsMaterialControl\",\n"
            + "      \"dynamicFriction\" : \"0.6\",\n"
            + "      \"staticFriction\" : \"0.6\",\n"
            + "      \"bounciness\" : \"0\",\n"
            + "      \"frictionCombine\" : \"Average\",\n"
            + "      \"bounceCombine\" : \"Average\"\n"
            + "    },\n"
            + "    {\n"
            + "      \"id\" : \"CollisionTrigger\",\n"
            + "      \"triggerEnabled\" : \"false\",\n"
            + "      \"invertAtomFilter\" : \"false\",\n"
            + "      \"useRelativeVelocityFilter\" : \"false\",\n"
            + "      \"invertRelativeVelocityFilter\" : \"false\",\n"
            + "      \"relativeVelocityFilter\" : \"1\",\n"
            + "      \"trigger\" : {\n"
            + "        \"startActions\" : [ ],\n"
            + "        \"transitionActions\" : [ ],\n"
            + "        \"endActions\" : [ ]\n"
            + "      }\n"
            + "    },\n"
            + "    {\n"
            + "      \"id\" : \"scale\",\n"
            + "      \"scale\" : \"1\"\n"
            + "    },\n"
            + "    {\n"
            + "      \"id\" : \"asset\",\n"
            + "      \"importLightmaps\" : \"true\",\n"
            + "      \"importLightProbes\" : \"true\",\n"
            + "      \"registerCanvases\" : \"false\",\n"
            + "      \"showCanvases\" : \"true\",\n"
            + "      \"loadDll\" : \"true\",\n"
            + "      \"assetName\" : \"" + EscapeJson(assetName) + "\",\n"
            + "      \"assetUrl\" : \"" + EscapeJson(assetUrl) + "\",\n"
            + "      \"assetDllUrl\" : \"\"\n"
            + "    },\n"
            + "    {\n"
            + "      \"id\" : \"PluginManager\",\n"
            + "      \"plugins\" : { }\n"
            + "    }\n"
            + "  ]\n"
            + "}\n";
    }

    private static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
            return;

        string normalized = assetPath.Replace('\\', '/');
        string parent = Path.GetDirectoryName(normalized).Replace('\\', '/');
        string name = Path.GetFileName(normalized);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        if (!string.IsNullOrEmpty(parent))
            AssetDatabase.CreateFolder(parent, name);
    }

    private static void WriteTextFile(string path, string contents)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, contents);
    }

    private static string EscapeJson(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
