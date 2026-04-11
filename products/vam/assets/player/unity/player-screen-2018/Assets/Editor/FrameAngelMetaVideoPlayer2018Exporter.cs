using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class FrameAngelMetaVideoPlayer2018Exporter
{
    private const string AssetRoot = "Assets/FrameAngel/Meta";
    private const string MaterialsRoot = AssetRoot + "/Materials";
    private const string TexturesRoot = AssetRoot + "/Textures";
    private const string DefaultSummaryPath = "C:/projects/fa/products/vam/assets/player/build/meta_toolkit_catalog/theme_00/ghost_meta_ui_toolkit_export_summary_theme_00.json";
    private const string DefaultOutputRoot = "C:/projects/fa/products/vam/assets/player/build/meta_snapshot_cua";
    private const string DefaultDeployAssetsRoot = "F:/sim/vam/Custom/Assets/FrameAngel/Meta";
    private const string DefaultDeployPresetRoot = "F:/sim/vam/Custom/Atom/CustomUnityAsset";
    private const string DefaultResourceId = "fa_meta_video_player_snapshot";
    private const string DefaultBundleFileName = "fa_meta_video_player_snapshot.assetbundle";
    private const string DefaultPresetFileName = "Preset_FA Meta Video Player Snapshot.vap";
    private const string DefaultSummaryFileName = "meta_video_player_snapshot_cua_export_summary.json";

    private sealed class ExportOptions
    {
        public string SummaryPath = DefaultSummaryPath;
        public string PackageRootPath = "";
        public string OutputRoot = DefaultOutputRoot;
        public string DeployAssetsRoot = DefaultDeployAssetsRoot;
        public string DeployPresetRoot = DefaultDeployPresetRoot;
        public string ResourceId = DefaultResourceId;
        public string BundleFileName = DefaultBundleFileName;
        public string PresetFileName = DefaultPresetFileName;
        public string SummaryFileName = DefaultSummaryFileName;
        public bool Deploy = true;
    }

    [Serializable]
    private sealed class ToolkitSummary
    {
        public SurfaceSummary[] surfaces;
    }

    [Serializable]
    private sealed class SurfaceSummary
    {
        public string controlSurfaceId = "";
        public string exportDisplayName = "";
        public string controlFamilyId = "";
        public string packageRootPath = "";
        public string materialsPath = "";
        public string controlsPath = "";
    }

    [Serializable]
    private sealed class MaterialsDoc
    {
        public MaterialDoc[] materials;
    }

    [Serializable]
    private sealed class MaterialDoc
    {
        public string materialRefId = "";
        public string displayName = "";
        public string texturePngBase64 = "";
        public string[] featureFlags;
    }

    [Serializable]
    private sealed class ControlsDoc
    {
        public string controlSurfaceId = "";
        public string controlSurfaceLabel = "";
        public string controlFamilyId = "";
        public string controlThemeId = "";
        public string controlThemeLabel = "";
        public string controlThemeVariantId = "";
        public float surfaceWidthMeters = 0.4608f;
        public float surfaceHeightMeters = 0.3096f;
    }

    [Serializable]
    private sealed class ExportSummary
    {
        public string schemaVersion = "frameangel_meta_control_surface_2018_export_summary_v1";
        public string generatedAtUtc = "";
        public string unityVersion = "";
        public string sourceSummaryPath = "";
        public string packageRootPath = "";
        public string materialsPath = "";
        public string controlsPath = "";
        public string prefabPath = "";
        public string bundlePath = "";
        public string assetName = "";
        public string presetPath = "";
        public string deployBundlePath = "";
        public string deployPresetPath = "";
        public string assetUrl = "";
        public string controlSurfaceId = "";
        public string controlSurfaceLabel = "";
        public string controlFamilyId = "";
        public string controlThemeId = "";
        public string controlThemeLabel = "";
        public string controlThemeVariantId = "";
        public string sourceSnapshotMaterialRefId = "";
        public string sourceSnapshotDisplayName = "";
        public int snapshotTextureWidth = 0;
        public int snapshotTextureHeight = 0;
        public float surfaceWidthMeters = 0f;
        public float surfaceHeightMeters = 0f;
        public string resourceId = "";
    }

    [MenuItem("FrameAngel/Build Meta Video Player Proof CUA (VaM 2018)")]
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
        options.SummaryPath = GetArg(args, "-faSummaryPath", options.SummaryPath);
        options.PackageRootPath = GetArg(args, "-faPackageRootPath", options.PackageRootPath);
        options.OutputRoot = GetArg(args, "-faOutputRoot", options.OutputRoot);
        options.DeployAssetsRoot = GetArg(args, "-faDeployAssetsRoot", options.DeployAssetsRoot);
        options.DeployPresetRoot = GetArg(args, "-faDeployPresetRoot", options.DeployPresetRoot);
        options.ResourceId = GetArg(args, "-faResourceId", options.ResourceId);
        options.BundleFileName = GetArg(args, "-faBundleFileName", options.BundleFileName);
        options.PresetFileName = GetArg(args, "-faPresetFileName", options.PresetFileName);
        options.SummaryFileName = GetArg(args, "-faSummaryFileName", options.SummaryFileName);
        options.Deploy = GetBoolArg(args, "-faDeploy", true);
        return options;
    }

    private static void BuildAndDeploy(ExportOptions options)
    {
        string resolvedSummaryPath = IsNullOrWhiteSpaceCompat(options.SummaryPath)
            ? string.Empty
            : Path.GetFullPath(options.SummaryPath);
        string resolvedPackageRootPath = IsNullOrWhiteSpaceCompat(options.PackageRootPath)
            ? string.Empty
            : Path.GetFullPath(options.PackageRootPath);
        SurfaceSummary surface = ResolveSurface(resolvedSummaryPath, resolvedPackageRootPath);
        if (surface == null)
        {
            throw new InvalidOperationException(
                "FrameAngelMetaVideoPlayer2018Exporter: no eligible surface package could be resolved.");
        }

        string resolvedMaterialsPath = Path.GetFullPath(surface.materialsPath);
        string resolvedControlsPath = Path.GetFullPath(surface.controlsPath);
        MaterialsDoc materialsDoc = ReadJson<MaterialsDoc>(resolvedMaterialsPath, "materials doc");
        ControlsDoc controlsDoc = ReadJson<ControlsDoc>(resolvedControlsPath, "controls doc");
        MaterialDoc snapshotMaterialDoc = FindSnapshotMaterial(materialsDoc);
        if (snapshotMaterialDoc == null)
            throw new InvalidOperationException("FrameAngelMetaVideoPlayer2018Exporter: snapshot material was not found in the materials doc.");

        EnsureFolder("Assets/FrameAngel");
        EnsureFolder(AssetRoot);
        EnsureFolder(MaterialsRoot);
        EnsureFolder(TexturesRoot);

        string prefabPath = AssetRoot + "/" + options.ResourceId + ".prefab";
        string materialPath = MaterialsRoot + "/" + options.ResourceId + ".mat";
        string texturePath = TexturesRoot + "/" + options.ResourceId + ".png";
        DeleteAssetIfExists(prefabPath);
        DeleteAssetIfExists(materialPath);
        DeleteAssetIfExists(texturePath);

        try
        {
            Texture2D texture = ImportSnapshotTexture(texturePath, snapshotMaterialDoc.texturePngBase64);
            Material material = CreateSnapshotMaterial(materialPath, texture);
            GameObject root = CreateSnapshotRoot(options.ResourceId, material, controlsDoc.surfaceWidthMeters, controlsDoc.surfaceHeightMeters);

            try
            {
                PrefabUtility.CreatePrefab(prefabPath, root, ReplacePrefabOptions.ReplaceNameBased);
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
            Directory.CreateDirectory(presetOutputRoot);
            if (options.Deploy && !string.IsNullOrEmpty(options.DeployAssetsRoot))
                Directory.CreateDirectory(options.DeployAssetsRoot);
            if (options.Deploy && !string.IsNullOrEmpty(options.DeployPresetRoot))
                Directory.CreateDirectory(options.DeployPresetRoot);

            AssetBundleBuild build = new AssetBundleBuild();
            build.assetBundleName = options.BundleFileName;
            build.assetNames = new[] { prefabPath };

            BuildPipeline.BuildAssetBundles(
                bundleOutputRoot,
                new[] { build },
                BuildAssetBundleOptions.None,
                BuildTarget.StandaloneWindows64);

            string bundlePath = Path.Combine(bundleOutputRoot, options.BundleFileName);
            if (!File.Exists(bundlePath))
                throw new InvalidOperationException("FrameAngelMetaVideoPlayer2018Exporter: bundle was not written: " + bundlePath);

            string assetName = ResolveFirstAssetName(bundlePath, prefabPath);
            string assetUrl = "Custom/Assets/FrameAngel/Meta/" + options.BundleFileName;
            string presetJson = BuildCustomUnityAssetPresetJson(assetUrl, assetName);
            string outputPresetPath = Path.Combine(presetOutputRoot, options.PresetFileName);
            WriteTextFile(outputPresetPath, presetJson);

            string deployedBundlePath = string.Empty;
            string deployedPresetPath = string.Empty;
            if (options.Deploy && !string.IsNullOrEmpty(options.DeployAssetsRoot))
            {
                deployedBundlePath = Path.Combine(options.DeployAssetsRoot, options.BundleFileName);
                File.Copy(bundlePath, deployedBundlePath, true);
            }
            if (options.Deploy && !string.IsNullOrEmpty(options.DeployPresetRoot))
            {
                deployedPresetPath = Path.Combine(options.DeployPresetRoot, options.PresetFileName);
                WriteTextFile(deployedPresetPath, presetJson);
            }

            ExportSummary summary = new ExportSummary();
            summary.generatedAtUtc = DateTime.UtcNow.ToString("o");
            summary.unityVersion = Application.unityVersion;
            summary.sourceSummaryPath = resolvedSummaryPath;
            summary.packageRootPath = surface.packageRootPath ?? "";
            summary.materialsPath = resolvedMaterialsPath;
            summary.controlsPath = resolvedControlsPath;
            summary.prefabPath = prefabPath;
            summary.bundlePath = bundlePath;
            summary.assetName = assetName;
            summary.presetPath = outputPresetPath;
            summary.deployBundlePath = deployedBundlePath;
            summary.deployPresetPath = deployedPresetPath;
            summary.assetUrl = assetUrl;
            summary.controlSurfaceId = controlsDoc.controlSurfaceId ?? surface.controlSurfaceId ?? "";
            summary.controlSurfaceLabel = controlsDoc.controlSurfaceLabel ?? surface.exportDisplayName ?? "";
            summary.controlFamilyId = !string.IsNullOrEmpty(surface.controlFamilyId)
                ? surface.controlFamilyId
                : (controlsDoc.controlFamilyId ?? "");
            summary.controlThemeId = controlsDoc.controlThemeId ?? "";
            summary.controlThemeLabel = controlsDoc.controlThemeLabel ?? "";
            summary.controlThemeVariantId = controlsDoc.controlThemeVariantId ?? "";
            summary.sourceSnapshotMaterialRefId = snapshotMaterialDoc.materialRefId ?? "";
            summary.sourceSnapshotDisplayName = snapshotMaterialDoc.displayName ?? "";
            summary.snapshotTextureWidth = texture != null ? texture.width : 0;
            summary.snapshotTextureHeight = texture != null ? texture.height : 0;
            summary.surfaceWidthMeters = controlsDoc.surfaceWidthMeters;
            summary.surfaceHeightMeters = controlsDoc.surfaceHeightMeters;
            summary.resourceId = options.ResourceId;
            WriteTextFile(Path.Combine(options.OutputRoot, options.SummaryFileName), JsonUtility.ToJson(summary, true));
            Debug.Log("FrameAngelMetaVideoPlayer2018Exporter: built " + bundlePath);
        }
        finally
        {
            DeletePathIfExists(prefabPath);
            DeletePathIfExists(materialPath);
            DeletePathIfExists(texturePath);
            DeletePathIfExists(TexturesRoot);
            DeletePathIfExists(MaterialsRoot);
            DeletePathIfExists(AssetRoot);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    private static SurfaceSummary ResolveSurface(string resolvedSummaryPath, string resolvedPackageRootPath)
    {
        if (!string.IsNullOrEmpty(resolvedPackageRootPath))
        {
            return BuildSurfaceSummaryFromPackageRoot(resolvedPackageRootPath);
        }

        ToolkitSummary toolkitSummary = ReadJson<ToolkitSummary>(resolvedSummaryPath, "toolkit summary");
        SurfaceSummary surface = FindVideoPlayerSurface(toolkitSummary);
        if (surface == null)
        {
            throw new InvalidOperationException(
                "FrameAngelMetaVideoPlayer2018Exporter: no meta_ui_video_player surface was found in the toolkit summary.");
        }

        return surface;
    }

    private static SurfaceSummary BuildSurfaceSummaryFromPackageRoot(string packageRootPath)
    {
        if (string.IsNullOrEmpty(packageRootPath) || !Directory.Exists(packageRootPath))
        {
            throw new DirectoryNotFoundException(
                "FrameAngelMetaVideoPlayer2018Exporter: surface package root was not found: " + packageRootPath);
        }

        string controlsPath = Path.Combine(packageRootPath, "controls.innerpiece.json");
        string materialsPath = Path.Combine(packageRootPath, "materials.innerpiece.json");
        ControlsDoc controlsDoc = ReadJson<ControlsDoc>(controlsPath, "controls doc");

        return new SurfaceSummary
        {
            controlSurfaceId = controlsDoc.controlSurfaceId ?? "",
            exportDisplayName = controlsDoc.controlSurfaceLabel ?? "",
            controlFamilyId = controlsDoc.controlFamilyId ?? "",
            packageRootPath = packageRootPath,
            materialsPath = materialsPath,
            controlsPath = controlsPath
        };
    }

    private static SurfaceSummary FindVideoPlayerSurface(ToolkitSummary toolkitSummary)
    {
        if (toolkitSummary == null || toolkitSummary.surfaces == null)
            return null;

        for (int i = 0; i < toolkitSummary.surfaces.Length; i++)
        {
            SurfaceSummary candidate = toolkitSummary.surfaces[i];
            if (candidate == null)
                continue;

            if (string.Equals(candidate.controlFamilyId ?? "", "meta_ui_video_player", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(candidate.materialsPath)
                && !string.IsNullOrEmpty(candidate.controlsPath))
            {
                return candidate;
            }
        }

        return null;
    }

    private static MaterialDoc FindSnapshotMaterial(MaterialsDoc materialsDoc)
    {
        if (materialsDoc == null || materialsDoc.materials == null)
            return null;

        for (int i = 0; i < materialsDoc.materials.Length; i++)
        {
            MaterialDoc candidate = materialsDoc.materials[i];
            if (candidate == null || string.IsNullOrEmpty(candidate.texturePngBase64))
                continue;

            if (candidate.featureFlags != null)
            {
                for (int j = 0; j < candidate.featureFlags.Length; j++)
                {
                    if (string.Equals(candidate.featureFlags[j] ?? "", "control_surface_canvas_snapshot", StringComparison.OrdinalIgnoreCase))
                        return candidate;
                }
            }

            if ((candidate.displayName ?? "").IndexOf("snapshot", StringComparison.OrdinalIgnoreCase) >= 0)
                return candidate;
        }

        return null;
    }

    private static T ReadJson<T>(string path, string label) where T : class
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("FrameAngelMetaVideoPlayer2018Exporter: " + label + " was not found.", path);

        T value = JsonUtility.FromJson<T>(File.ReadAllText(path));
        if (value == null)
            throw new InvalidOperationException("FrameAngelMetaVideoPlayer2018Exporter: " + label + " could not be parsed.");

        return value;
    }

    private static Texture2D ImportSnapshotTexture(string texturePath, string pngBase64)
    {
        if (string.IsNullOrEmpty(pngBase64))
            throw new InvalidOperationException("FrameAngelMetaVideoPlayer2018Exporter: snapshot texture payload was empty.");

        byte[] pngBytes = Convert.FromBase64String(pngBase64);
        string absoluteTexturePath = Path.Combine(Directory.GetCurrentDirectory(), texturePath.Replace('/', Path.DirectorySeparatorChar));
        string textureDirectory = Path.GetDirectoryName(absoluteTexturePath) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(textureDirectory);
        File.WriteAllBytes(absoluteTexturePath, pngBytes);
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null)
            throw new InvalidOperationException("FrameAngelMetaVideoPlayer2018Exporter: texture import failed for " + texturePath);

        return texture;
    }

    private static Material CreateSnapshotMaterial(string materialPath, Texture2D texture)
    {
        Shader shader = Shader.Find("Unlit/Texture");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Legacy Shaders/Diffuse");
        if (shader == null)
            throw new InvalidOperationException("FrameAngelMetaVideoPlayer2018Exporter: no compatible shader found.");

        Material material = new Material(shader);
        material.name = Path.GetFileNameWithoutExtension(materialPath);
        material.color = Color.white;
        material.mainTexture = texture;
        if (material.HasProperty("_Cull"))
            material.SetInt("_Cull", (int)CullMode.Off);

        AssetDatabase.CreateAsset(material, materialPath);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<Material>(materialPath) ?? material;
    }

    private static GameObject CreateSnapshotRoot(string resourceId, Material material, float widthMeters, float heightMeters)
    {
        float width = Mathf.Max(0.001f, widthMeters > 0f ? widthMeters : 0.4608f);
        float height = Mathf.Max(0.001f, heightMeters > 0f ? heightMeters : 0.3096f);

        GameObject root = new GameObject(resourceId);
        GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
        surface.name = "meta_video_player_snapshot_surface";
        surface.transform.SetParent(root.transform, false);
        surface.transform.localPosition = Vector3.zero;
        surface.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        surface.transform.localScale = new Vector3(width, height, 1f);

        Collider collider = surface.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.DestroyImmediate(collider);

        MeshRenderer renderer = surface.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        return root;
    }

    private static string ResolveFirstAssetName(string bundlePath, string fallbackPrefabPath)
    {
        AssetBundle assetBundle = AssetBundle.LoadFromFile(bundlePath);
        try
        {
            if (assetBundle != null)
            {
                string[] assetNames = assetBundle.GetAllAssetNames();
                if (assetNames != null)
                {
                    for (int i = 0; i < assetNames.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(assetNames[i]) && assetNames[i].EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                            return assetNames[i];
                    }
                }
            }
        }
        finally
        {
            if (assetBundle != null)
                assetBundle.Unload(true);
        }

        return fallbackPrefabPath.Replace('\\', '/').ToLowerInvariant();
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

    private static bool IsNullOrWhiteSpaceCompat(string value)
    {
        return string.IsNullOrEmpty(value) || value.Trim().Length == 0;
    }

    private static bool GetBoolArg(string[] args, string name, bool defaultValue)
    {
        string value = GetArg(args, name, defaultValue ? "true" : "false");
        bool parsed;
        if (bool.TryParse(value, out parsed))
            return parsed;

        return defaultValue;
    }

    private static void EnsureFolder(string assetFolderPath)
    {
        string[] segments = assetFolderPath.Split('/');
        if (segments.Length <= 1)
            return;

        string current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string next = current + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[i]);

            current = next;
        }
    }

    private static void DeleteAssetIfExists(string assetPath)
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
            AssetDatabase.DeleteAsset(assetPath);
    }

    private static void DeletePathIfExists(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            AssetDatabase.DeleteAsset(assetPath);
            return;
        }

        DeleteAssetIfExists(assetPath);
    }

    private static string BuildCustomUnityAssetPresetJson(string assetUrl, string assetName)
    {
        string escapedAssetUrl = EscapeJson(assetUrl);
        string escapedAssetName = EscapeJson(assetName);
        return
            "{\n" +
            "  \"setUnlistedParamsToDefault\" : \"true\",\n" +
            "  \"storables\" : [\n" +
            "    {\n" +
            "      \"id\" : \"PhysicsMaterialControl\",\n" +
            "      \"dynamicFriction\" : \"0.6\",\n" +
            "      \"staticFriction\" : \"0.6\",\n" +
            "      \"bounciness\" : \"0\",\n" +
            "      \"frictionCombine\" : \"Average\",\n" +
            "      \"bounceCombine\" : \"Average\"\n" +
            "    },\n" +
            "    {\n" +
            "      \"id\" : \"CollisionTrigger\",\n" +
            "      \"triggerEnabled\" : \"false\",\n" +
            "      \"invertAtomFilter\" : \"false\",\n" +
            "      \"useRelativeVelocityFilter\" : \"false\",\n" +
            "      \"invertRelativeVelocityFilter\" : \"false\",\n" +
            "      \"relativeVelocityFilter\" : \"1\",\n" +
            "      \"trigger\" : {\n" +
            "        \"startActions\" : [ ],\n" +
            "        \"transitionActions\" : [ ],\n" +
            "        \"endActions\" : [ ]\n" +
            "      }\n" +
            "    },\n" +
            "    {\n" +
            "      \"id\" : \"scale\",\n" +
            "      \"scale\" : \"1\"\n" +
            "    },\n" +
            "    {\n" +
            "      \"id\" : \"asset\",\n" +
            "      \"importLightmaps\" : \"true\",\n" +
            "      \"importLightProbes\" : \"true\",\n" +
            "      \"registerCanvases\" : \"false\",\n" +
            "      \"showCanvases\" : \"true\",\n" +
            "      \"loadDll\" : \"false\",\n" +
            "      \"assetName\" : \"" + escapedAssetName + "\",\n" +
            "      \"assetUrl\" : \"" + escapedAssetUrl + "\",\n" +
            "      \"assetDllUrl\" : \"\"\n" +
            "    },\n" +
            "    {\n" +
            "      \"id\" : \"PluginManager\",\n" +
            "      \"plugins\" : { }\n" +
            "    }\n" +
            "  ]\n" +
            "}\n";
    }

    private static string EscapeJson(string value)
    {
        if (value == null)
            return string.Empty;

        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static void WriteTextFile(string path, string content)
    {
        string directory = Path.GetDirectoryName(path) ?? "";
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, content, new System.Text.UTF8Encoding(false));
    }
}
