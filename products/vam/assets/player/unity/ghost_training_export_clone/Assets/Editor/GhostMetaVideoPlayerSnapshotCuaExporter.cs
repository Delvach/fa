using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class GhostMetaVideoPlayerSnapshotCuaExporter
{
    private const string DefaultSummaryPath = "C:/projects/fa/products/vam/assets/player/build/meta_toolkit_catalog/theme_00/ghost_meta_ui_toolkit_export_summary_theme_00.json";
    private const string DefaultOutputRoot = "C:/projects/fa/products/vam/assets/player/build/meta_snapshot_cua";
    private const string DefaultDeployAssetsRoot = "F:/sim/vam/Custom/Assets/FrameAngel/Meta";
    private const string DefaultDeployPresetRoot = "F:/sim/vam/Custom/Atom/CustomUnityAsset";
    private const string TempAssetFolder = "Assets/FrameAngel/Meta";
    private const string ResourceId = "fa_meta_video_player_snapshot";
    private const string BundleFileName = "fa_meta_video_player_snapshot.assetbundle";
    private const string PresetFileName = "Preset_FA Meta Video Player Snapshot.vap";
    private const string SummaryFileName = "meta_video_player_snapshot_cua_export_summary.json";

    [Serializable] private sealed class ToolkitSummary { public SurfaceSummary[] surfaces; }
    [Serializable] private sealed class SurfaceSummary
    {
        public string controlSurfaceId = "";
        public string exportDisplayName = "";
        public string controlFamilyId = "";
        public string packageRootPath = "";
        public string materialsPath = "";
        public string controlsPath = "";
    }
    [Serializable] private sealed class MaterialsDoc { public MaterialDoc[] materials; }
    [Serializable] private sealed class MaterialDoc
    {
        public string materialRefId = "";
        public string displayName = "";
        public string texturePngBase64 = "";
        public string[] featureFlags;
    }
    [Serializable] private sealed class ControlsDoc
    {
        public string controlSurfaceId = "";
        public string controlSurfaceLabel = "";
        public string controlThemeId = "";
        public string controlThemeLabel = "";
        public string controlThemeVariantId = "";
        public float surfaceWidthMeters = 0.4608f;
        public float surfaceHeightMeters = 0.3096f;
    }
    [Serializable] private sealed class ExportSummary
    {
        public string schemaVersion = "frameangel_meta_video_player_snapshot_cua_export_v1";
        public string generatedAtUtc = "";
        public string sourceSummaryPath = "";
        public string packageRootPath = "";
        public string materialsPath = "";
        public string controlsPath = "";
        public string outputRoot = "";
        public string deployAssetsRoot = "";
        public string deployPresetRoot = "";
        public string bundlePath = "";
        public string assetName = "";
        public string presetPath = "";
        public string deployBundlePath = "";
        public string deployPresetPath = "";
        public string assetUrl = "";
        public string buildProfile = "";
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
    }

    [MenuItem("FrameAngel/Ghost/Meta UI Set/Export Meta Video Player Snapshot CUA")]
    public static void ExportMetaVideoPlayerSnapshotCua()
    {
        ExportInternal(DefaultSummaryPath, DefaultOutputRoot, DefaultDeployAssetsRoot, DefaultDeployPresetRoot, true, "current");
    }

    public static void ExportMetaVideoPlayerSnapshotCuaBatch()
    {
        string[] args = Environment.GetCommandLineArgs();
        ExportInternal(
            GetArg(args, "-faSummaryPath", DefaultSummaryPath),
            GetArg(args, "-faOutputRoot", DefaultOutputRoot),
            GetArg(args, "-faDeployAssetsRoot", DefaultDeployAssetsRoot),
            GetArg(args, "-faDeployPresetRoot", DefaultDeployPresetRoot),
            GetBoolArg(args, "-faDeploy", true),
            GetArg(args, "-faBuildProfile", "current"));
    }

    private static void ExportInternal(string summaryPath, string outputRoot, string deployAssetsRoot, string deployPresetRoot, bool deploy, string buildProfile)
    {
        string resolvedSummaryPath = Path.GetFullPath(string.IsNullOrWhiteSpace(summaryPath) ? DefaultSummaryPath : summaryPath);
        ToolkitSummary toolkitSummary = ReadJson<ToolkitSummary>(resolvedSummaryPath, "toolkit summary");
        SurfaceSummary surface = toolkitSummary.surfaces.FirstOrDefault(item =>
            item != null
            && string.Equals(item.controlFamilyId ?? "", "meta_ui_video_player", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(item.materialsPath)
            && !string.IsNullOrWhiteSpace(item.controlsPath));
        if (surface == null)
            throw new InvalidOperationException("GhostMetaVideoPlayerSnapshotCuaExporter: no meta_ui_video_player surface was found in the toolkit summary.");

        string resolvedMaterialsPath = Path.GetFullPath(surface.materialsPath);
        string resolvedControlsPath = Path.GetFullPath(surface.controlsPath);
        MaterialsDoc materialsDoc = ReadJson<MaterialsDoc>(resolvedMaterialsPath, "materials doc");
        ControlsDoc controlsDoc = ReadJson<ControlsDoc>(resolvedControlsPath, "controls doc");

        MaterialDoc snapshotMaterialDoc = materialsDoc.materials.FirstOrDefault(item =>
            item != null && !string.IsNullOrWhiteSpace(item.texturePngBase64)
            && item.featureFlags != null
            && item.featureFlags.Any(flag => string.Equals(flag, "control_surface_canvas_snapshot", StringComparison.OrdinalIgnoreCase)))
            ?? materialsDoc.materials.FirstOrDefault(item =>
                item != null && !string.IsNullOrWhiteSpace(item.texturePngBase64)
                && (item.displayName ?? "").IndexOf("snapshot", StringComparison.OrdinalIgnoreCase) >= 0);
        if (snapshotMaterialDoc == null)
            throw new InvalidOperationException("GhostMetaVideoPlayerSnapshotCuaExporter: snapshot material was not found in the materials doc.");

        byte[] pngBytes = Convert.FromBase64String(snapshotMaterialDoc.texturePngBase64);
        string resolvedOutputRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(outputRoot) ? DefaultOutputRoot : outputRoot);
        string resolvedDeployAssetsRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(deployAssetsRoot) ? DefaultDeployAssetsRoot : deployAssetsRoot);
        string resolvedDeployPresetRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(deployPresetRoot) ? DefaultDeployPresetRoot : deployPresetRoot);
        Directory.CreateDirectory(resolvedOutputRoot);
        Directory.CreateDirectory(resolvedDeployAssetsRoot);
        Directory.CreateDirectory(resolvedDeployPresetRoot);

        string textureAssetPath = TempAssetFolder + "/" + ResourceId + "_tex.png";
        string materialAssetPath = TempAssetFolder + "/" + ResourceId + "_mat.mat";
        string prefabAssetPath = TempAssetFolder + "/" + ResourceId + ".prefab";
        string absoluteTextureAssetPath = Path.Combine(Application.dataPath, "FrameAngel/Meta/" + ResourceId + "_tex.png");
        string bundleOutputRoot = Path.Combine(resolvedOutputRoot, "assetbundles");
        string outputBundlePath = Path.Combine(bundleOutputRoot, BundleFileName);
        string outputPresetPath = Path.Combine(Path.Combine(resolvedOutputRoot, "presets"), PresetFileName);
        string deployedBundlePath = Path.Combine(resolvedDeployAssetsRoot, BundleFileName);
        string deployedPresetPath = Path.Combine(resolvedDeployPresetRoot, PresetFileName);

        Texture2D textureAsset = null;
        GameObject root = null;
        try
        {
            EnsureAssetFolder(TempAssetFolder);
            DeleteAssetIfPresent(prefabAssetPath);
            DeleteAssetIfPresent(materialAssetPath);
            DeleteAssetIfPresent(textureAssetPath);

            Directory.CreateDirectory(Path.GetDirectoryName(absoluteTextureAssetPath) ?? Application.dataPath);
            File.WriteAllBytes(absoluteTextureAssetPath, pngBytes);
            AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceSynchronousImport);
            textureAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
            if (textureAsset == null)
                throw new InvalidOperationException("GhostMetaVideoPlayerSnapshotCuaExporter: texture asset failed to import: " + textureAssetPath);

            Material materialAsset = CreateSnapshotMaterial(materialAssetPath, textureAsset);
            root = CreateSnapshotRoot(materialAsset, controlsDoc.surfaceWidthMeters, controlsDoc.surfaceHeightMeters);
            PrefabUtility.SaveAsPrefabAsset(root, prefabAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Directory.CreateDirectory(bundleOutputRoot);
            AssetBundleBuild build = new AssetBundleBuild { assetBundleName = BundleFileName, assetNames = new[] { prefabAssetPath } };
            BuildPipeline.BuildAssetBundles(bundleOutputRoot, new[] { build }, ResolveBuildOptions(buildProfile), BuildTarget.StandaloneWindows64);
            if (!File.Exists(outputBundlePath))
                throw new InvalidOperationException("GhostMetaVideoPlayerSnapshotCuaExporter: bundle was not written: " + outputBundlePath);

            string assetName = ResolveFirstPrefabAssetName(outputBundlePath, prefabAssetPath);
            string assetUrl = ConvertToVamCustomAssetUrl(deployedBundlePath);
            string presetJson = BuildPresetJson(assetUrl, assetName);
            WriteTextFile(outputPresetPath, presetJson);
            if (deploy)
            {
                File.Copy(outputBundlePath, deployedBundlePath, true);
                WriteTextFile(deployedPresetPath, presetJson);
            }

            ExportSummary summary = new ExportSummary();
            summary.generatedAtUtc = DateTime.UtcNow.ToString("o");
            summary.sourceSummaryPath = resolvedSummaryPath;
            summary.packageRootPath = surface.packageRootPath ?? "";
            summary.materialsPath = resolvedMaterialsPath;
            summary.controlsPath = resolvedControlsPath;
            summary.outputRoot = resolvedOutputRoot;
            summary.deployAssetsRoot = resolvedDeployAssetsRoot;
            summary.deployPresetRoot = resolvedDeployPresetRoot;
            summary.bundlePath = outputBundlePath;
            summary.assetName = assetName;
            summary.presetPath = outputPresetPath;
            summary.deployBundlePath = deploy ? deployedBundlePath : "";
            summary.deployPresetPath = deploy ? deployedPresetPath : "";
            summary.assetUrl = assetUrl;
            summary.buildProfile = buildProfile ?? "";
            summary.controlSurfaceId = controlsDoc.controlSurfaceId ?? surface.controlSurfaceId ?? "";
            summary.controlSurfaceLabel = controlsDoc.controlSurfaceLabel ?? surface.exportDisplayName ?? "";
            summary.controlFamilyId = surface.controlFamilyId ?? "";
            summary.controlThemeId = controlsDoc.controlThemeId ?? "";
            summary.controlThemeLabel = controlsDoc.controlThemeLabel ?? "";
            summary.controlThemeVariantId = controlsDoc.controlThemeVariantId ?? "";
            summary.sourceSnapshotMaterialRefId = snapshotMaterialDoc.materialRefId ?? "";
            summary.sourceSnapshotDisplayName = snapshotMaterialDoc.displayName ?? "";
            summary.snapshotTextureWidth = textureAsset.width;
            summary.snapshotTextureHeight = textureAsset.height;
            summary.surfaceWidthMeters = controlsDoc.surfaceWidthMeters;
            summary.surfaceHeightMeters = controlsDoc.surfaceHeightMeters;
            WriteTextFile(Path.Combine(resolvedOutputRoot, SummaryFileName), JsonUtility.ToJson(summary, true));
        }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            DeleteAssetIfPresent(prefabAssetPath);
            DeleteAssetIfPresent(materialAssetPath);
            DeleteAssetIfPresent(textureAssetPath);
            AssetDatabase.Refresh();
        }
    }

    private static T ReadJson<T>(string path, string label) where T : class
    {
        string resolved = Path.GetFullPath(path);
        if (!File.Exists(resolved))
            throw new FileNotFoundException("GhostMetaVideoPlayerSnapshotCuaExporter: " + label + " was not found.", resolved);
        T value = JsonUtility.FromJson<T>(File.ReadAllText(resolved));
        if (value == null)
            throw new InvalidOperationException("GhostMetaVideoPlayerSnapshotCuaExporter: " + label + " could not be parsed.");
        return value;
    }

    private static Material CreateSnapshotMaterial(string materialAssetPath, Texture2D texture)
    {
        Shader shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
        if (shader == null)
            throw new InvalidOperationException("GhostMetaVideoPlayerSnapshotCuaExporter: no compatible shader was found for the snapshot material.");
        Material material = new Material(shader);
        material.name = ResourceId + "_mat";
        material.color = Color.white;
        material.mainTexture = texture;
        if (material.HasProperty("_Cull")) material.SetInt("_Cull", (int)CullMode.Off);
        AssetDatabase.CreateAsset(material, materialAssetPath);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath) ?? material;
    }

    private static GameObject CreateSnapshotRoot(Material material, float widthMeters, float heightMeters)
    {
        float width = Mathf.Max(0.001f, widthMeters > 0f ? widthMeters : 0.4608f);
        float height = Mathf.Max(0.001f, heightMeters > 0f ? heightMeters : 0.3096f);
        GameObject root = new GameObject(ResourceId);
        GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
        surface.name = "meta_video_player_snapshot_surface";
        surface.transform.SetParent(root.transform, false);
        surface.transform.localScale = new Vector3(width, height, 1f);
        Collider collider = surface.GetComponent<Collider>();
        if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
        MeshRenderer renderer = surface.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        return root;
    }

    private static BuildAssetBundleOptions ResolveBuildOptions(string buildProfile)
    {
        string normalized = string.IsNullOrWhiteSpace(buildProfile) ? "current" : buildProfile.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "current":
            case "uncompressed": return BuildAssetBundleOptions.UncompressedAssetBundle;
            case "current_stripver":
            case "uncompressed_stripver": return BuildAssetBundleOptions.UncompressedAssetBundle | BuildAssetBundleOptions.AssetBundleStripUnityVersion;
            case "lz4":
            case "chunked": return BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle;
            case "lz4_stripver":
            case "chunked_stripver": return BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle | BuildAssetBundleOptions.AssetBundleStripUnityVersion;
            case "lz4_notypetree":
            case "chunked_notypetree": return BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.DisableWriteTypeTree | BuildAssetBundleOptions.ForceRebuildAssetBundle;
            case "lz4_notypetree_stripver":
            case "chunked_notypetree_stripver": return BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.DisableWriteTypeTree | BuildAssetBundleOptions.ForceRebuildAssetBundle | BuildAssetBundleOptions.AssetBundleStripUnityVersion;
            default: throw new InvalidOperationException("GhostMetaVideoPlayerSnapshotCuaExporter: unknown build profile " + buildProfile);
        }
    }

    private static string ResolveFirstPrefabAssetName(string bundlePath, string fallbackPrefabAssetPath)
    {
        AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
        if (bundle == null) return NormalizeBundleAssetName(fallbackPrefabAssetPath);
        try { return bundle.GetAllAssetNames().FirstOrDefault(name => name.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) ?? NormalizeBundleAssetName(fallbackPrefabAssetPath); }
        finally { bundle.Unload(false); }
    }

    private static string NormalizeBundleAssetName(string assetPath) => string.IsNullOrWhiteSpace(assetPath) ? "" : assetPath.Replace('\\', '/').ToLowerInvariant();
    private static string ConvertToVamCustomAssetUrl(string absoluteBundlePath)
    {
        string normalized = absoluteBundlePath.Replace('\\', '/');
        int customIndex = normalized.IndexOf("/Custom/", StringComparison.OrdinalIgnoreCase);
        if (customIndex < 0) throw new InvalidOperationException("GhostMetaVideoPlayerSnapshotCuaExporter: deploy bundle path must live under Custom/: " + absoluteBundlePath);
        return normalized.Substring(customIndex + 1);
    }

    private static string BuildPresetJson(string assetUrl, string assetName)
    {
        string escapedAssetUrl = EscapeJson(assetUrl);
        string escapedAssetName = EscapeJson(assetName);
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
            + "      \"trigger\" : { \"startActions\" : [ ], \"transitionActions\" : [ ], \"endActions\" : [ ] }\n"
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
            + "      \"loadDll\" : \"false\",\n"
            + "      \"assetName\" : \"" + escapedAssetName + "\",\n"
            + "      \"assetUrl\" : \"" + escapedAssetUrl + "\",\n"
            + "      \"assetDllUrl\" : \"\"\n"
            + "    },\n"
            + "    {\n"
            + "      \"id\" : \"PluginManager\",\n"
            + "      \"plugins\" : { }\n"
            + "    }\n"
            + "  ]\n"
            + "}\n";
    }

    private static string EscapeJson(string value) => value == null ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    private static string GetArg(string[] args, string name, string defaultValue)
    {
        for (int i = 0; i < args.Length - 1; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        return defaultValue;
    }
    private static bool GetBoolArg(string[] args, string name, bool defaultValue)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) continue;
            if (bool.TryParse(args[i + 1], out bool parsed)) return parsed;
            return defaultValue;
        }
        return defaultValue;
    }
    private static void EnsureAssetFolder(string assetFolderPath)
    {
        string[] segments = assetFolderPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        string current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string next = current + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[i]);
            current = next;
        }
    }
    private static void DeleteAssetIfPresent(string assetPath)
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);
    }
    private static void WriteTextFile(string path, string content)
    {
        string directory = Path.GetDirectoryName(path) ?? "";
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }
}
