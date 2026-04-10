using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GhostMetaVideoPlayerProofCustomUnityAssetExporter
{
    private const string ScenePath = "Assets/Scenes/GhostMetaUiSetVideoPlayerProof.unity";
    private const string ProofObjectName = "GhostMetaVideoPlayerProof";
    private const string DefaultOutputRoot = "C:/projects/fa/products/vam/assets/player/build/meta_proof_cua";
    private const string DefaultDeployAssetsRoot = "F:/sim/vam/Custom/Assets/FrameAngel/Meta";
    private const string DefaultDeployPresetRoot = "F:/sim/vam/Custom/Atom/CustomUnityAsset";
    private const string TempPrefabFolder = "Assets/FrameAngel/Meta";
    private const string ResourceId = "fa_meta_video_player_proof";
    private const string BundleFileName = "fa_meta_video_player_proof.assetbundle";
    private const string PresetFileName = "Preset_FA Meta Video Player Proof.vap";
    private const string SummaryFileName = "meta_video_player_proof_cua_export_summary.json";

    [Serializable]
    private sealed class ExportSummary
    {
        public string schemaVersion = "frameangel_meta_video_player_proof_cua_export_v1";
        public string generatedAtUtc = "";
        public string scenePath = "";
        public string proofObjectName = ProofObjectName;
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
    }

    [MenuItem("FrameAngel/Ghost/Meta UI Set/Export Meta Video Player Proof CUA")]
    public static void ExportMetaVideoPlayerProof()
    {
        ExportInternal(DefaultOutputRoot, DefaultDeployAssetsRoot, DefaultDeployPresetRoot, true, "current");
    }

    public static void ExportMetaVideoPlayerProofBatch()
    {
        string[] args = Environment.GetCommandLineArgs();
        string outputRoot = GetArg(args, "-faOutputRoot", DefaultOutputRoot);
        string deployAssetsRoot = GetArg(args, "-faDeployAssetsRoot", DefaultDeployAssetsRoot);
        string deployPresetRoot = GetArg(args, "-faDeployPresetRoot", DefaultDeployPresetRoot);
        string buildProfile = GetArg(args, "-faBuildProfile", "current");
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

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject sourceRoot = FindSceneObjectByName(ProofObjectName);
        if (sourceRoot == null)
            throw new InvalidOperationException("GhostMetaVideoPlayerProofCustomUnityAssetExporter: proof root not found: " + ProofObjectName);

        GameObject clone = UnityEngine.Object.Instantiate(sourceRoot);
        clone.name = ResourceId;
        clone.transform.SetParent(null, false);
        clone.transform.localPosition = Vector3.zero;
        clone.transform.localRotation = Quaternion.identity;
        clone.transform.localScale = Vector3.one;

        string prefabAssetPath = TempPrefabFolder + "/" + ResourceId + ".prefab";
        string bundleOutputRoot = Path.Combine(resolvedOutputRoot, "assetbundles");
        string outputBundlePath = Path.Combine(bundleOutputRoot, BundleFileName);
        string outputPresetRoot = Path.Combine(resolvedOutputRoot, "presets");
        string outputPresetPath = Path.Combine(outputPresetRoot, PresetFileName);
        string deployedBundlePath = Path.Combine(resolvedDeployAssetsRoot, BundleFileName);
        string deployedPresetPath = Path.Combine(resolvedDeployPresetRoot, PresetFileName);

        try
        {
            EnsureAssetFolder(TempPrefabFolder);
            DeleteAssetIfPresent(prefabAssetPath);

            StripAuthoringComponents(clone);
            StripUnsupportedComponents(clone);

            PrefabUtility.SaveAsPrefabAsset(clone, prefabAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

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

            if (!File.Exists(outputBundlePath))
                throw new InvalidOperationException("GhostMetaVideoPlayerProofCustomUnityAssetExporter: bundle was not written: " + outputBundlePath);

            string assetName = ResolveFirstPrefabAssetName(outputBundlePath, prefabAssetPath);
            if (string.IsNullOrWhiteSpace(assetName))
                throw new InvalidOperationException("GhostMetaVideoPlayerProofCustomUnityAssetExporter: prefab asset name not found in bundle: " + outputBundlePath);

            string assetUrl = ConvertToVamCustomAssetUrl(deployedBundlePath);
            string presetJson = BuildCustomUnityAssetPresetJson(assetUrl, assetName);

            Directory.CreateDirectory(outputPresetRoot);
            WriteTextFile(outputPresetPath, presetJson);

            if (deploy)
            {
                File.Copy(outputBundlePath, deployedBundlePath, true);
                WriteTextFile(deployedPresetPath, presetJson);
            }

            ExportSummary summary = new ExportSummary
            {
                generatedAtUtc = DateTime.UtcNow.ToString("o"),
                scenePath = Path.GetFullPath(ScenePath),
                outputRoot = resolvedOutputRoot,
                deployAssetsRoot = resolvedDeployAssetsRoot,
                deployPresetRoot = resolvedDeployPresetRoot,
                bundlePath = outputBundlePath,
                assetName = assetName,
                presetPath = outputPresetPath,
                deployBundlePath = deploy ? deployedBundlePath : "",
                deployPresetPath = deploy ? deployedPresetPath : "",
                assetUrl = assetUrl,
                buildProfile = buildProfile ?? ""
            };

            WriteTextFile(Path.Combine(resolvedOutputRoot, SummaryFileName), JsonUtility.ToJson(summary, true));
            Debug.Log("GhostMetaVideoPlayerProofCustomUnityAssetExporter: exported Meta proof CUA to " + resolvedOutputRoot);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(clone);
            CleanupTempAssets(prefabAssetPath);
        }
    }

    private static void StripAuthoringComponents(GameObject rootObject)
    {
        if (rootObject == null)
            return;

        MonoBehaviour[] monoBehaviours = rootObject.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < monoBehaviours.Length; i++)
        {
            MonoBehaviour behaviour = monoBehaviours[i];
            if (behaviour == null)
                continue;

            Type type = behaviour.GetType();
            string fullName = type.FullName ?? type.Name ?? "";
            string typeName = type.Name ?? "";
            string nameSpace = type.Namespace ?? "";

            if (fullName.StartsWith("FrameAngel.UnityEditorBridge.", StringComparison.Ordinal)
                || typeName.StartsWith("GhostMeta", StringComparison.Ordinal)
                || string.Equals(typeName, "RoundedBoxVideoController", StringComparison.Ordinal)
                || nameSpace.StartsWith("Oculus", StringComparison.Ordinal)
                || nameSpace.StartsWith("Meta", StringComparison.Ordinal))
            {
                UnityEngine.Object.DestroyImmediate(behaviour, true);
            }
        }
    }

    private static void StripUnsupportedComponents(GameObject rootObject)
    {
        if (rootObject == null)
            return;

        Component[] components = rootObject.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null || component is Transform)
                continue;

            if (IsSupportedRuntimeComponent(component))
                continue;

            UnityEngine.Object.DestroyImmediate(component, true);
        }
    }

    private static bool IsSupportedRuntimeComponent(Component component)
    {
        if (component == null)
            return false;

        Type type = component.GetType();
        string nameSpace = type.Namespace ?? "";

        if (component is MeshFilter
            || component is MeshRenderer
            || component is SkinnedMeshRenderer
            || component is Canvas
            || component is CanvasRenderer
            || component is RectTransform)
        {
            return true;
        }

        if (nameSpace.StartsWith("UnityEngine.UI", StringComparison.Ordinal)
            || nameSpace.StartsWith("UnityEngine.EventSystems", StringComparison.Ordinal)
            || nameSpace.StartsWith("TMPro", StringComparison.Ordinal)
            || nameSpace.StartsWith("UnityEngine.Video", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null)
                continue;

            if (string.Equals(root.name, objectName, StringComparison.Ordinal))
                return root;

            Transform nested = FindChildRecursive(root.transform, objectName);
            if (nested != null)
                return nested.gameObject;
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, objectName, StringComparison.Ordinal))
                return child;

            Transform nested = FindChildRecursive(child, objectName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static void EnsureAssetFolder(string assetFolderPath)
    {
        string[] segments = assetFolderPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length <= 0 || !string.Equals(segments[0], "Assets", StringComparison.Ordinal))
            throw new InvalidOperationException("GhostMetaVideoPlayerProofCustomUnityAssetExporter: temp asset folder must start with Assets/.");

        string current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string next = current + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[i]);

            current = next;
        }
    }

    private static void DeleteAssetIfPresent(string assetPath)
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
            AssetDatabase.DeleteAsset(assetPath);
    }

    private static void CleanupTempAssets(string prefabAssetPath)
    {
        DeleteAssetIfPresent(prefabAssetPath);
        AssetDatabase.Refresh();
    }

    private static BuildAssetBundleOptions ResolveBuildOptions(string buildProfile)
    {
        string normalized = string.IsNullOrWhiteSpace(buildProfile)
            ? "current"
            : buildProfile.Trim().ToLowerInvariant();

        switch (normalized)
        {
            case "current":
            case "uncompressed":
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
                throw new InvalidOperationException("GhostMetaVideoPlayerProofCustomUnityAssetExporter: unknown build profile " + buildProfile);
        }
    }

    private static string ResolveFirstPrefabAssetName(string bundlePath, string fallbackPrefabAssetPath)
    {
        AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
        if (bundle == null)
            return NormalizeBundleAssetName(fallbackPrefabAssetPath);

        try
        {
            return bundle.GetAllAssetNames().FirstOrDefault(name => name.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
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
            throw new InvalidOperationException("GhostMetaVideoPlayerProofCustomUnityAssetExporter: deploy bundle path must live under Custom/: " + absoluteBundlePath);

        return normalized.Substring(customIndex + 1);
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
            "      \"registerCanvases\" : \"true\",\n" +
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

    private static void WriteTextFile(string path, string content)
    {
        string directory = Path.GetDirectoryName(path) ?? "";
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, content, new UTF8Encoding(false));
    }
}
