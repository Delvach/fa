using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class GhostMinimalCustomUnityAssetExporter
{
    private const string DefaultOutputRoot = "C:/projects/staging/frameangel-player-split/products/vam/plugins/player/build/minimal_cua_probe";
    private const string DefaultDeployAssetsRoot = "F:/sim/vam/Custom/Assets/FrameAngel/Player";
    private const string DefaultDeployPresetRoot = "F:/sim/vam/Custom/Atom/CustomUnityAsset";
    private const string TempPrefabFolder = "Assets/__FrameAngelTemp/MinimalCuaProbe";
    private const string ResourceId = "fa_cua_probe_cube";
    private const string BundleFileName = "fa_cua_probe_cube.assetbundle";
    private const string PresetFileName = "Preset_FA CUA Probe Cube.vap";
    private const string SummaryFileName = "minimal_cua_probe_summary.json";

    [Serializable]
    private sealed class ExportSummary
    {
        public string schemaVersion = "frameangel_minimal_cua_probe_summary_v1";
        public string generatedAtUtc = "";
        public string outputRoot = "";
        public string deployAssetsRoot = "";
        public string deployPresetRoot = "";
        public string resourceId = ResourceId;
        public string bundlePath = "";
        public string assetName = "";
        public string presetPath = "";
        public string deployBundlePath = "";
        public string deployPresetPath = "";
        public string assetUrl = "";
        public string buildProfile = "";
    }

    [MenuItem("FrameAngel/Ghost/Meta UI Set/Export Minimal CUA Probe")]
    public static void ExportMinimalCuaProbe()
    {
        ExportInternal(DefaultOutputRoot, DefaultDeployAssetsRoot, DefaultDeployPresetRoot, true, "lz4_notypetree");
    }

    public static void ExportMinimalCuaProbeBatch()
    {
        string[] args = Environment.GetCommandLineArgs();
        string outputRoot = GetArg(args, "-faOutputRoot", DefaultOutputRoot);
        string deployAssetsRoot = GetArg(args, "-faDeployAssetsRoot", DefaultDeployAssetsRoot);
        string deployPresetRoot = GetArg(args, "-faDeployPresetRoot", DefaultDeployPresetRoot);
        string buildProfile = GetArg(args, "-faBuildProfile", "lz4_notypetree");
        bool deploy = GetBoolArg(args, "-faDeploy", true);
        ExportInternal(outputRoot, deployAssetsRoot, deployPresetRoot, deploy, buildProfile);
    }

    private static void ExportInternal(
        string outputRoot,
        string deployAssetsRoot,
        string deployPresetRoot,
        bool deploy,
        string buildProfile)
    {
        string resolvedOutputRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(outputRoot) ? DefaultOutputRoot : outputRoot);
        string resolvedDeployAssetsRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(deployAssetsRoot) ? DefaultDeployAssetsRoot : deployAssetsRoot);
        string resolvedDeployPresetRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(deployPresetRoot) ? DefaultDeployPresetRoot : deployPresetRoot);

        Directory.CreateDirectory(resolvedOutputRoot);
        Directory.CreateDirectory(resolvedDeployAssetsRoot);
        Directory.CreateDirectory(resolvedDeployPresetRoot);

        GameObject probeRoot = CreateProbeRoot();
        try
        {
            string prefabFolder = TempPrefabFolder;
            EnsureAssetFolder(prefabFolder);

            string prefabAssetPath = prefabFolder + "/" + ResourceId + ".prefab";
            DeleteAssetIfPresent(prefabAssetPath);

            PrefabUtility.SaveAsPrefabAsset(probeRoot, prefabAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string bundleOutputRoot = Path.Combine(resolvedOutputRoot, "assetbundles");
            Directory.CreateDirectory(bundleOutputRoot);

            AssetBundleBuild build = new AssetBundleBuild
            {
                assetBundleName = BundleFileName,
                assetNames = new[] { prefabAssetPath }
            };

            BuildPipeline.BuildAssetBundles(
                bundleOutputRoot,
                new[] { build },
                ResolveBuildOptions(buildProfile),
                BuildTarget.StandaloneWindows64);

            string builtBundlePath = Path.Combine(bundleOutputRoot, BundleFileName);
            if (!File.Exists(builtBundlePath))
                throw new InvalidOperationException("GhostMinimalCustomUnityAssetExporter: bundle was not written: " + builtBundlePath);

            string assetName = ResolveFirstPrefabAssetName(builtBundlePath, prefabAssetPath);
            string deployBundlePath = Path.Combine(resolvedDeployAssetsRoot, BundleFileName);
            string deployPresetPath = Path.Combine(resolvedDeployPresetRoot, PresetFileName);
            string assetUrl = ConvertToVamCustomAssetUrl(deployBundlePath);
            string presetJson = BuildCustomUnityAssetPresetJson(assetUrl, assetName);

            string outputPresetRoot = Path.Combine(resolvedOutputRoot, "presets");
            Directory.CreateDirectory(outputPresetRoot);
            string outputPresetPath = Path.Combine(outputPresetRoot, PresetFileName);
            WriteTextFile(outputPresetPath, presetJson);

            if (deploy)
            {
                File.Copy(builtBundlePath, deployBundlePath, true);
                WriteTextFile(deployPresetPath, presetJson);
            }

            ExportSummary summary = new ExportSummary
            {
                generatedAtUtc = DateTime.UtcNow.ToString("o"),
                outputRoot = resolvedOutputRoot,
                deployAssetsRoot = resolvedDeployAssetsRoot,
                deployPresetRoot = resolvedDeployPresetRoot,
                bundlePath = builtBundlePath,
                assetName = assetName,
                presetPath = outputPresetPath,
                deployBundlePath = deploy ? deployBundlePath : "",
                deployPresetPath = deploy ? deployPresetPath : "",
                assetUrl = assetUrl,
                buildProfile = buildProfile
            };

            string summaryPath = Path.Combine(resolvedOutputRoot, SummaryFileName);
            WriteTextFile(summaryPath, JsonUtility.ToJson(summary, true));
            Debug.Log("GhostMinimalCustomUnityAssetExporter: exported minimal direct CUA probe to " + resolvedOutputRoot);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(probeRoot);
            CleanupTempAssets();
        }
    }

    private static GameObject CreateProbeRoot()
    {
        EnsureAssetFolder(TempPrefabFolder);

        string materialAssetPath = TempPrefabFolder + "/" + ResourceId + "_mat.mat";
        DeleteAssetIfPresent(materialAssetPath);

        Shader shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Legacy Shaders/Diffuse");
        if (shader == null)
            throw new InvalidOperationException("GhostMinimalCustomUnityAssetExporter: no compatible shader found.");

        Material material = new Material(shader);
        material.name = ResourceId + "_mat";
        material.color = new Color(0.1f, 0.85f, 0.35f, 1f);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", 0.25f);
        AssetDatabase.CreateAsset(material, materialAssetPath);
        AssetDatabase.SaveAssets();

        GameObject root = new GameObject(ResourceId);
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "probe_cube";
        cube.transform.SetParent(root.transform, false);
        cube.transform.localPosition = Vector3.zero;
        cube.transform.localRotation = Quaternion.identity;
        cube.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

        Collider collider = cube.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.DestroyImmediate(collider);

        MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
        Material savedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
        if (renderer != null && savedMaterial != null)
            renderer.sharedMaterial = savedMaterial;

        return root;
    }

    private static BuildAssetBundleOptions ResolveBuildOptions(string buildProfile)
    {
        string normalized = string.IsNullOrWhiteSpace(buildProfile)
            ? "lz4_notypetree"
            : buildProfile.Trim().ToLowerInvariant();

        switch (normalized)
        {
            case "current":
                return BuildAssetBundleOptions.UncompressedAssetBundle;
            case "current_stripver":
            case "uncompressed_stripver":
                return BuildAssetBundleOptions.UncompressedAssetBundle | BuildAssetBundleOptions.AssetBundleStripUnityVersion;
            case "lz4":
            case "chunked":
                return BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle;
            case "lz4_stripver":
            case "chunked_stripver":
                return BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle | BuildAssetBundleOptions.AssetBundleStripUnityVersion;
            case "lz4_notypetree":
            case "chunked_notypetree":
                return BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.DisableWriteTypeTree | BuildAssetBundleOptions.ForceRebuildAssetBundle;
            case "lz4_notypetree_stripver":
            case "chunked_notypetree_stripver":
                return BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.DisableWriteTypeTree | BuildAssetBundleOptions.ForceRebuildAssetBundle | BuildAssetBundleOptions.AssetBundleStripUnityVersion;
            default:
                throw new InvalidOperationException("GhostMinimalCustomUnityAssetExporter: unknown build profile " + buildProfile);
        }
    }

    private static string ResolveFirstPrefabAssetName(string bundlePath, string fallbackPrefabAssetPath)
    {
        AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
        if (bundle == null)
            return NormalizeBundleAssetName(fallbackPrefabAssetPath);

        try
        {
            return bundle.GetAllAssetNames()
                .FirstOrDefault(name => name.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                ?? NormalizeBundleAssetName(fallbackPrefabAssetPath);
        }
        finally
        {
            bundle.Unload(false);
        }
    }

    private static string NormalizeBundleAssetName(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return "";

        return assetPath.Replace('\\', '/').ToLowerInvariant();
    }

    private static string ConvertToVamCustomAssetUrl(string absoluteBundlePath)
    {
        string normalized = absoluteBundlePath.Replace('\\', '/');
        int customIndex = normalized.IndexOf("/Custom/", StringComparison.OrdinalIgnoreCase);
        if (customIndex < 0)
            throw new InvalidOperationException("GhostMinimalCustomUnityAssetExporter: deploy bundle path must live under Custom/: " + absoluteBundlePath);

        return normalized.Substring(customIndex + 1);
    }

    private static void EnsureAssetFolder(string assetFolderPath)
    {
        string[] segments = assetFolderPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 1 || !string.Equals(segments[0], "Assets", StringComparison.Ordinal))
            throw new InvalidOperationException("GhostMinimalCustomUnityAssetExporter: asset folder must start with Assets/: " + assetFolderPath);

        string currentPath = "Assets";
        for (int i = 1; i < segments.Length; i++)
        {
            string childName = segments[i];
            string childPath = currentPath + "/" + childName;
            if (!AssetDatabase.IsValidFolder(childPath))
                AssetDatabase.CreateFolder(currentPath, childName);

            currentPath = childPath;
        }
    }

    private static void DeleteAssetIfPresent(string assetPath)
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
            AssetDatabase.DeleteAsset(assetPath);
    }

    private static void CleanupTempAssets()
    {
        DeleteAssetIfPresent(TempPrefabFolder + "/" + ResourceId + ".prefab");
        DeleteAssetIfPresent(TempPrefabFolder + "/" + ResourceId + "_mat.mat");
        AssetDatabase.Refresh();
    }

    private static void WriteTextFile(string path, string content)
    {
        string directory = Path.GetDirectoryName(path) ?? "";
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, content, new UTF8Encoding(false));
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
            "      \"loadDll\" : \"true\",\n" +
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
        if (string.IsNullOrEmpty(value))
            return "";

        StringBuilder builder = new StringBuilder(value.Length + 8);
        for (int i = 0; i < value.Length; i++)
        {
            char ch = value[i];
            switch (ch)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    builder.Append(ch);
                    break;
            }
        }

        return builder.ToString();
    }

    private static string GetArg(string[] args, string name, string fallback)
    {
        if (args == null)
            return fallback;

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return fallback;
    }

    private static bool GetBoolArg(string[] args, string name, bool fallback)
    {
        string value = GetArg(args, name, fallback ? "true" : "false");
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase))
            return false;
        if (bool.TryParse(value, out bool parsed))
            return parsed;

        return fallback;
    }
}
