using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class FrameAngelTutorialBaselineCuaExporter
{
    private const string DefaultOutputRoot = "C:/projects/frameangel/products/vam/plugins/player/build/tutorial_baseline_cua";
    private const string DefaultDeployAssetsRoot = "F:/sim/vam/Custom/Assets/FrameAngel";
    private const string DefaultDeployPresetRoot = "F:/sim/vam/Custom/Atom/CustomUnityAsset";
    private const string TempAssetFolder = "Assets/__FrameAngelTemp/TutorialBaselineCua";
    private const string ResourceId = "fa_cua_tutorial_baseline";
    private const string BundleName = "fa_cua_tutorial_baseline";
    private const string BundleFileName = "fa_cua_tutorial_baseline.assetbundle";
    private const string PresetFileName = "Preset_FA CUA Tutorial Baseline.vap";
    private const string SummaryFileName = "frameangel_tutorial_baseline_cua_export_summary.json";

    [Serializable]
    private sealed class ExportSummary
    {
        public string schemaVersion = "frameangel_tutorial_baseline_cua_export_summary_v1";
        public string generatedAtUtc = "";
        public string tempPrefabPath = "";
        public string bundleBuildRoot = "";
        public string bundleSourcePath = "";
        public string bundlePath = "";
        public string presetPath = "";
        public string deployBundlePath = "";
        public string deployPresetPath = "";
        public string assetName = "";
        public string assetUrl = "";
    }

    [MenuItem("FrameAngel/Tutorial/Export Minimal CUA Baseline")]
    public static void ExportTutorialBaselineCua()
    {
        ExportInternal(DefaultOutputRoot, DefaultDeployAssetsRoot, DefaultDeployPresetRoot, true);
    }

    public static void ExportTutorialBaselineCuaBatch()
    {
        string[] args = Environment.GetCommandLineArgs();
        string outputRoot = GetArg(args, "-faOutputRoot", DefaultOutputRoot);
        string deployAssetsRoot = GetArg(args, "-faDeployAssetsRoot", DefaultDeployAssetsRoot);
        string deployPresetRoot = GetArg(args, "-faDeployPresetRoot", DefaultDeployPresetRoot);
        bool deploy = GetBoolArg(args, "-faDeploy", true);

        ExportInternal(outputRoot, deployAssetsRoot, deployPresetRoot, deploy);
    }

    private static void ExportInternal(
        string outputRoot,
        string deployAssetsRoot,
        string deployPresetRoot,
        bool deploy)
    {
        string resolvedOutputRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(outputRoot) ? DefaultOutputRoot : outputRoot);
        string resolvedDeployAssetsRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(deployAssetsRoot) ? DefaultDeployAssetsRoot : deployAssetsRoot);
        string resolvedDeployPresetRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(deployPresetRoot) ? DefaultDeployPresetRoot : deployPresetRoot);

        Directory.CreateDirectory(resolvedOutputRoot);
        Directory.CreateDirectory(resolvedDeployAssetsRoot);
        Directory.CreateDirectory(resolvedDeployPresetRoot);
        EnsureAssetFolder(TempAssetFolder);

        string prefabAssetPath = TempAssetFolder + "/" + ResourceId + ".prefab";
        string materialAssetPath = TempAssetFolder + "/" + ResourceId + "_mat.mat";
        DeleteAssetIfExists(prefabAssetPath);
        DeleteAssetIfExists(materialAssetPath);

        Material material = CreateBaselineMaterial(materialAssetPath);
        GameObject root = CreateBaselineRoot(material);
        string builtSourcePath = "";
        string outputBundlePath = "";
        string outputPresetPath = "";
        string deployedBundlePath = Path.Combine(resolvedDeployAssetsRoot, BundleFileName);
        string deployedPresetPath = Path.Combine(resolvedDeployPresetRoot, PresetFileName);

        try
        {
            PrefabUtility.SaveAsPrefabAsset(root, prefabAssetPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        AssetImporter importer = AssetImporter.GetAtPath(prefabAssetPath);
        if (importer == null)
            throw new InvalidOperationException("FrameAngelTutorialBaselineCuaExporter: prefab importer not found for " + prefabAssetPath);

        importer.assetBundleName = BundleName;
        importer.assetBundleVariant = string.Empty;
        importer.SaveAndReimport();

        string bundleBuildRoot = Path.Combine(resolvedOutputRoot, "AssetBundles", "StandaloneWindows");
        Directory.CreateDirectory(bundleBuildRoot);
        BuildPipeline.BuildAssetBundles(bundleBuildRoot, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows);

        builtSourcePath = Path.Combine(bundleBuildRoot, BundleName);
        if (!File.Exists(builtSourcePath))
            throw new InvalidOperationException("FrameAngelTutorialBaselineCuaExporter: bundle was not written: " + builtSourcePath);

        string outputBundleRoot = Path.Combine(resolvedOutputRoot, "assetbundles");
        Directory.CreateDirectory(outputBundleRoot);
        outputBundlePath = Path.Combine(outputBundleRoot, BundleFileName);
        File.Copy(builtSourcePath, outputBundlePath, true);

        string assetName = ResolveFirstPrefabAssetName(outputBundlePath, prefabAssetPath);
        if (string.IsNullOrWhiteSpace(assetName))
            throw new InvalidOperationException("FrameAngelTutorialBaselineCuaExporter: prefab asset name not found in bundle: " + outputBundlePath);

        string assetUrl = ConvertToVamCustomAssetUrl(deployedBundlePath);
        string presetJson = BuildCustomUnityAssetPresetJson(assetUrl, assetName);

        string outputPresetRoot = Path.Combine(resolvedOutputRoot, "presets");
        Directory.CreateDirectory(outputPresetRoot);
        outputPresetPath = Path.Combine(outputPresetRoot, PresetFileName);
        WriteTextFile(outputPresetPath, presetJson);

        if (deploy)
        {
            File.Copy(outputBundlePath, deployedBundlePath, true);
            WriteTextFile(deployedPresetPath, presetJson);
        }

        ExportSummary summary = new ExportSummary
        {
            generatedAtUtc = DateTime.UtcNow.ToString("o"),
            tempPrefabPath = prefabAssetPath,
            bundleBuildRoot = bundleBuildRoot,
            bundleSourcePath = builtSourcePath,
            bundlePath = outputBundlePath,
            presetPath = outputPresetPath,
            deployBundlePath = deploy ? deployedBundlePath : "",
            deployPresetPath = deploy ? deployedPresetPath : "",
            assetName = assetName,
            assetUrl = assetUrl
        };

        WriteTextFile(Path.Combine(resolvedOutputRoot, SummaryFileName), JsonUtility.ToJson(summary, true));
        Debug.Log("FrameAngelTutorialBaselineCuaExporter: exported baseline CUA to " + resolvedOutputRoot);
    }

    private static Material CreateBaselineMaterial(string materialAssetPath)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Legacy Shaders/Diffuse");
        if (shader == null)
            throw new InvalidOperationException("FrameAngelTutorialBaselineCuaExporter: no compatible shader found for baseline material.");

        Material material = new Material(shader);
        material.name = ResourceId + "_mat";
        material.color = new Color(0.18f, 0.28f, 0.38f, 1f);
        AssetDatabase.CreateAsset(material, materialAssetPath);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath) ?? material;
    }

    private static GameObject CreateBaselineRoot(Material material)
    {
        GameObject root = new GameObject(ResourceId);
        GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
        surface.name = "baseline_surface";
        surface.transform.SetParent(root.transform, false);
        surface.transform.localPosition = Vector3.zero;
        surface.transform.localRotation = Quaternion.identity;
        surface.transform.localScale = new Vector3(0.75f, 0.42f, 0.03f);

        Collider collider = surface.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.DestroyImmediate(collider);

        MeshRenderer renderer = surface.GetComponent<MeshRenderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;

        return root;
    }

    private static string ResolveFirstPrefabAssetName(string bundlePath, string fallbackPrefabAssetPath)
    {
        AssetBundle assetBundle = AssetBundle.LoadFromFile(bundlePath);
        try
        {
            if (assetBundle != null)
            {
                string assetName = assetBundle.GetAllAssetNames().FirstOrDefault(name =>
                    name.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(assetName))
                    return assetName;
            }
        }
        finally
        {
            if (assetBundle != null)
                assetBundle.Unload(true);
        }

        return fallbackPrefabAssetPath.Replace('\\', '/').ToLowerInvariant();
    }

    private static string ConvertToVamCustomAssetUrl(string absolutePath)
    {
        string normalized = absolutePath.Replace('\\', '/');
        int customIndex = normalized.IndexOf("/Custom/", StringComparison.OrdinalIgnoreCase);
        if (customIndex >= 0)
            return normalized.Substring(customIndex + 1);

        customIndex = normalized.IndexOf("Custom/", StringComparison.OrdinalIgnoreCase);
        if (customIndex >= 0)
            return normalized.Substring(customIndex);

        throw new InvalidOperationException("FrameAngelTutorialBaselineCuaExporter: path is not under a VaM Custom root: " + absolutePath);
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
            "      \"currentPhysicsMaterial\" : \"\"\n" +
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
        if (value == null)
            return string.Empty;

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }

    private static string GetArg(string[] args, string name, string defaultValue)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return defaultValue;
    }

    private static bool GetBoolArg(string[] args, string name, bool defaultValue)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                continue;

            if (bool.TryParse(args[i + 1], out bool parsed))
                return parsed;

            return defaultValue;
        }

        return defaultValue;
    }

    private static void EnsureAssetFolder(string assetFolderPath)
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

    private static void WriteTextFile(string path, string content)
    {
        string directory = Path.GetDirectoryName(path) ?? "";
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, content, new UTF8Encoding(false));
    }
}
