using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GhostPlayerHostCustomUnityAssetExporter
{
    private const string ScenePath = "Assets/Scenes/GhostMetaUiSetVideoPlayerProof.unity";
    private const string DefaultOutputRoot = "C:/projects/frameangel/products/vam/plugins/player/build/cua_customunityassets";
    private const string DefaultDeployAssetsRoot = "F:/sim/vam/Custom/Assets/FrameAngel/Player";
    private const string DefaultDeployPresetRoot = "F:/sim/vam/Custom/Atom/CustomUnityAsset";
    private const string TempPrefabFolder = "Assets/__FrameAngelTemp/PlayerHostCua";
    private const string SummaryFileName = "ghost_player_host_cua_export_summary.json";

    [Serializable]
    private sealed class ShellEntry
    {
        public string shellKey = "";
        public string rootObjectSuffix = "";
        public string displayName = "";
        public string resourceId = "";
        public string presetFileName = "";
        public string bundleFileName = "";
        public bool generatedBaselineControl;
    }

    [Serializable]
    private sealed class ExportSummary
    {
        public string schemaVersion = "ghost_player_host_customunityasset_export_summary_v1";
        public string generatedAtUtc = "";
        public string scenePath = "";
        public string outputRoot = "";
        public string deployAssetsRoot = "";
        public string deployPresetRoot = "";
        public List<ExportEntry> exports = new List<ExportEntry>();
    }

    [Serializable]
    private sealed class ExportEntry
    {
        public string shellKey = "";
        public string displayName = "";
        public string resourceId = "";
        public string bundlePath = "";
        public string assetName = "";
        public string presetPath = "";
        public string deployBundlePath = "";
        public string deployPresetPath = "";
        public string assetUrl = "";
    }

    private static readonly ShellEntry[] ShellEntries =
    {
        new ShellEntry
        {
            shellKey = "player_host",
            rootObjectSuffix = "g007.player_host",
            displayName = "FA CUA Player Host",
            resourceId = "fa_cua_player_host",
            presetFileName = "Preset_FA CUA Player Host.vap",
            bundleFileName = "fa_cua_player_host.assetbundle"
        },
        new ShellEntry
        {
            shellKey = "baseline_control",
            rootObjectSuffix = "",
            displayName = "FA CUA Baseline Control",
            resourceId = "fa_cua_baseline_control",
            presetFileName = "Preset_FA CUA Baseline Control.vap",
            bundleFileName = "fa_cua_baseline_control.assetbundle",
            generatedBaselineControl = true
        },
        new ShellEntry
        {
            shellKey = "mcbrooke_laptop",
            rootObjectSuffix = "g007.mcbrooke_laptop",
            displayName = "FA CUA Player Laptop",
            resourceId = "fa_cua_player_laptop",
            presetFileName = "Preset_FA CUA Player Laptop.vap",
            bundleFileName = "fa_cua_player_laptop.assetbundle"
        },
        new ShellEntry
        {
            shellKey = "ivone_phone",
            rootObjectSuffix = "g007.ivone_phone",
            displayName = "FA CUA Player Phone",
            resourceId = "fa_cua_player_phone",
            presetFileName = "Preset_FA CUA Player Phone.vap",
            bundleFileName = "fa_cua_player_phone.assetbundle"
        },
        new ShellEntry
        {
            shellKey = "ivad_tablet",
            rootObjectSuffix = "g007.ivad_tablet",
            displayName = "FA CUA Player Tablet",
            resourceId = "fa_cua_player_tablet",
            presetFileName = "Preset_FA CUA Player Tablet.vap",
            bundleFileName = "fa_cua_player_tablet.assetbundle"
        },
        new ShellEntry
        {
            shellKey = "modern_tv",
            rootObjectSuffix = "g007.modern_tv",
            displayName = "FA CUA Player Modern TV",
            resourceId = "fa_cua_player_modern_tv",
            presetFileName = "Preset_FA CUA Player Modern TV.vap",
            bundleFileName = "fa_cua_player_modern_tv.assetbundle"
        },
        new ShellEntry
        {
            shellKey = "retro_tv",
            rootObjectSuffix = "g007.retro_tv",
            displayName = "FA CUA Player Retro TV",
            resourceId = "fa_cua_player_retro_tv",
            presetFileName = "Preset_FA CUA Player Retro TV.vap",
            bundleFileName = "fa_cua_player_retro_tv.assetbundle"
        }
    };

    [MenuItem("FrameAngel/Ghost/Meta UI Set/Export Player Host CUA Family")]
    public static void ExportPlayerHostCuaFamily()
    {
        ExportInternal(DefaultOutputRoot, DefaultDeployAssetsRoot, DefaultDeployPresetRoot, true, Array.Empty<string>());
    }

    public static void ExportPlayerHostCuaFamilyBatch()
    {
        string[] args = Environment.GetCommandLineArgs();
        string outputRoot = GetArg(args, "-faOutputRoot", DefaultOutputRoot);
        string deployAssetsRoot = GetArg(args, "-faDeployAssetsRoot", DefaultDeployAssetsRoot);
        string deployPresetRoot = GetArg(args, "-faDeployPresetRoot", DefaultDeployPresetRoot);
        bool deploy = GetBoolArg(args, "-faDeploy", true);
        bool includeBaselineControl = GetBoolArg(args, "-faIncludeBaselineControl", false);
        string shellKeysCsv = GetArg(args, "-faShellKeys", "");
        string[] shellKeys = string.IsNullOrWhiteSpace(shellKeysCsv)
            ? Array.Empty<string>()
            : shellKeysCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .ToArray();

        ExportInternal(outputRoot, deployAssetsRoot, deployPresetRoot, deploy, shellKeys, includeBaselineControl);
    }

    private static void ExportInternal(
        string outputRoot,
        string deployAssetsRoot,
        string deployPresetRoot,
        bool deploy,
        string[] requestedShellKeys,
        bool includeBaselineControl = false)
    {
        string resolvedOutputRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(outputRoot) ? DefaultOutputRoot : outputRoot);
        string resolvedDeployAssetsRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(deployAssetsRoot) ? DefaultDeployAssetsRoot : deployAssetsRoot);
        string resolvedDeployPresetRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(deployPresetRoot) ? DefaultDeployPresetRoot : deployPresetRoot);

        Directory.CreateDirectory(resolvedOutputRoot);
        Directory.CreateDirectory(resolvedDeployAssetsRoot);
        Directory.CreateDirectory(resolvedDeployPresetRoot);

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        List<ShellEntry> selectedEntries = SelectEntries(requestedShellKeys, includeBaselineControl);
        ExportSummary summary = new ExportSummary
        {
            generatedAtUtc = DateTime.UtcNow.ToString("o"),
            scenePath = Path.GetFullPath(ScenePath),
            outputRoot = resolvedOutputRoot,
            deployAssetsRoot = resolvedDeployAssetsRoot,
            deployPresetRoot = resolvedDeployPresetRoot
        };

        for (int i = 0; i < selectedEntries.Count; i++)
        {
            summary.exports.Add(ExportEntryToCustomUnityAsset(selectedEntries[i], resolvedOutputRoot, resolvedDeployAssetsRoot, resolvedDeployPresetRoot, deploy));
        }

        string summaryPath = Path.Combine(resolvedOutputRoot, SummaryFileName);
        WriteTextFile(summaryPath, JsonUtility.ToJson(summary, true));
        Debug.Log("GhostPlayerHostCustomUnityAssetExporter: exported " + summary.exports.Count.ToString() + " CUA host(s) to " + resolvedOutputRoot);
    }

    private static ExportEntry ExportEntryToCustomUnityAsset(
        ShellEntry entry,
        string outputRoot,
        string deployAssetsRoot,
        string deployPresetRoot,
        bool deploy)
    {
        GameObject clone = CreateExportClone(entry);

        try
        {
            NormalizeCloneForCustomUnityAsset(clone.transform, entry.resourceId);
            StripAuthoringComponents(clone);
            StripToRuntimeSafeComponents(clone);

            string prefabFolder = TempPrefabFolder;
            EnsureAssetFolder(prefabFolder);

            string prefabAssetPath = prefabFolder + "/" + entry.resourceId + ".prefab";
            string prefabAbsolutePath = Path.Combine(Directory.GetCurrentDirectory(), prefabAssetPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(prefabAbsolutePath))
                AssetDatabase.DeleteAsset(prefabAssetPath);

            PrefabUtility.SaveAsPrefabAsset(clone, prefabAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string bundleOutputRoot = Path.Combine(outputRoot, "assetbundles");
            Directory.CreateDirectory(bundleOutputRoot);

            AssetBundleBuild build = new AssetBundleBuild
            {
                assetBundleName = entry.bundleFileName,
                assetNames = new[] { prefabAssetPath }
            };

            BuildPipeline.BuildAssetBundles(
                bundleOutputRoot,
                new[] { build },
                ResolveBuildOptions(GetArg(Environment.GetCommandLineArgs(), "-faBuildProfile", "current")),
                BuildTarget.StandaloneWindows64);

            string builtBundlePath = Path.Combine(bundleOutputRoot, entry.bundleFileName);
            if (!File.Exists(builtBundlePath))
                throw new InvalidOperationException("GhostPlayerHostCustomUnityAssetExporter: bundle was not written: " + builtBundlePath);

            string assetName = ResolveFirstPrefabAssetName(builtBundlePath, prefabAssetPath);
            if (string.IsNullOrWhiteSpace(assetName))
                throw new InvalidOperationException("GhostPlayerHostCustomUnityAssetExporter: prefab asset name not found in bundle: " + builtBundlePath);

            string deployedBundlePath = Path.Combine(deployAssetsRoot, entry.bundleFileName);
            string deployedPresetPath = Path.Combine(deployPresetRoot, entry.presetFileName);
            string assetUrl = ConvertToVamCustomAssetUrl(deployedBundlePath);
            string presetJson = BuildCustomUnityAssetPresetJson(assetUrl, assetName);

            string outputPresetRoot = Path.Combine(outputRoot, "presets");
            Directory.CreateDirectory(outputPresetRoot);
            string outputPresetPath = Path.Combine(outputPresetRoot, entry.presetFileName);
            WriteTextFile(outputPresetPath, presetJson);

            if (deploy)
            {
                File.Copy(builtBundlePath, deployedBundlePath, true);
                WriteTextFile(deployedPresetPath, presetJson);
            }

            return new ExportEntry
            {
                shellKey = entry.shellKey,
                displayName = entry.displayName,
                resourceId = entry.resourceId,
                bundlePath = builtBundlePath,
                assetName = assetName,
                presetPath = outputPresetPath,
                deployBundlePath = deploy ? deployedBundlePath : "",
                deployPresetPath = deploy ? deployedPresetPath : "",
                assetUrl = assetUrl
            };
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(clone);
            CleanupTempPrefab(entry.resourceId);
        }
    }

    private static void NormalizeCloneForCustomUnityAsset(Transform root, string resourceId)
    {
        if (root == null)
            return;

        root.name = resourceId;
        RenameBySuffix(root, ".screen", "screen_surface");
        RenameBySuffix(root, ".glass", "screen_glass");
        RenameBySuffix(root, ".disconnect_mask", "disconnect_surface");
        RenameBySuffix(root, ".controls_anchor", "controls_anchor");
        RenameBySuffix(root, ".bottom_anchor", "bottom_anchor");
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
            string fullName = type.FullName ?? type.Name;
            string typeName = type.Name ?? "";
            if (fullName.StartsWith("FrameAngel.UnityEditorBridge.", StringComparison.Ordinal)
                || typeName.StartsWith("GhostMeta", StringComparison.Ordinal))
            {
                UnityEngine.Object.DestroyImmediate(behaviour, true);
            }
        }
    }

    private static List<ShellEntry> SelectEntries(string[] requestedShellKeys, bool includeBaselineControl)
    {
        if (requestedShellKeys == null || requestedShellKeys.Length <= 0)
        {
            List<ShellEntry> defaults = new List<ShellEntry>();
            ShellEntry playerHost = ShellEntries.FirstOrDefault(entry => string.Equals(entry.shellKey, "player_host", StringComparison.OrdinalIgnoreCase));
            if (playerHost == null)
                throw new InvalidOperationException("GhostPlayerHostCustomUnityAssetExporter: player_host entry is missing.");

            defaults.Add(playerHost);
            if (includeBaselineControl)
            {
                ShellEntry baseline = ShellEntries.FirstOrDefault(entry => string.Equals(entry.shellKey, "baseline_control", StringComparison.OrdinalIgnoreCase));
                if (baseline != null)
                    defaults.Add(baseline);
            }

            return defaults;
        }

        List<ShellEntry> entries = new List<ShellEntry>();
        for (int i = 0; i < requestedShellKeys.Length; i++)
        {
            string requestedKey = requestedShellKeys[i];
            ShellEntry match = ShellEntries.FirstOrDefault(entry => string.Equals(entry.shellKey, requestedKey, StringComparison.OrdinalIgnoreCase));
            if (match == null)
                throw new InvalidOperationException("GhostPlayerHostCustomUnityAssetExporter: unknown shell key " + requestedKey);

            entries.Add(match);
        }

        return entries;
    }

    private static void CleanupTempPrefab(string resourceId)
    {
        string prefabAssetPath = TempPrefabFolder + "/" + resourceId + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(prefabAssetPath) != null)
            AssetDatabase.DeleteAsset(prefabAssetPath);

        string materialAssetPath = TempPrefabFolder + "/" + resourceId + "_mat.mat";
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(materialAssetPath) != null)
            AssetDatabase.DeleteAsset(materialAssetPath);

        AssetDatabase.Refresh();
    }

    private static GameObject CreateExportClone(ShellEntry entry)
    {
        if (entry.generatedBaselineControl)
            return CreateBaselineControlRoot(entry.resourceId);

        GameObject sourceRoot = FindObjectBySuffix(entry.rootObjectSuffix);
        if (sourceRoot == null)
            throw new InvalidOperationException("GhostPlayerHostCustomUnityAssetExporter: root object not found for " + entry.shellKey);

        GameObject clone = UnityEngine.Object.Instantiate(sourceRoot);
        clone.name = entry.resourceId;
        clone.transform.SetParent(null, false);
        clone.transform.localPosition = Vector3.zero;
        clone.transform.localRotation = Quaternion.identity;
        clone.transform.localScale = Vector3.one;
        return clone;
    }

    private static GameObject CreateBaselineControlRoot(string resourceId)
    {
        EnsureAssetFolder(TempPrefabFolder);

        string materialAssetPath = TempPrefabFolder + "/" + resourceId + "_mat.mat";
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(materialAssetPath) != null)
            AssetDatabase.DeleteAsset(materialAssetPath);

        Shader shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Legacy Shaders/Diffuse");
        if (shader == null)
            throw new InvalidOperationException("GhostPlayerHostCustomUnityAssetExporter: baseline control shader could not be resolved.");

        Material material = new Material(shader);
        material.name = resourceId + "_mat";
        material.color = new Color(0.22f, 0.28f, 0.36f, 1f);
        AssetDatabase.CreateAsset(material, materialAssetPath);
        AssetDatabase.SaveAssets();

        GameObject root = new GameObject(resourceId);
        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "baseline_surface";
        panel.transform.SetParent(root.transform, false);
        panel.transform.localPosition = Vector3.zero;
        panel.transform.localRotation = Quaternion.identity;
        panel.transform.localScale = new Vector3(0.85f, 0.5f, 0.04f);

        Collider collider = panel.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.DestroyImmediate(collider);

        MeshRenderer renderer = panel.GetComponent<MeshRenderer>();
        Material savedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
        if (renderer != null && savedMaterial != null)
            renderer.sharedMaterial = savedMaterial;

        return root;
    }

    private static void StripToRuntimeSafeComponents(GameObject rootObject)
    {
        if (rootObject == null)
            return;

        Component[] components = rootObject.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null || component is Transform)
                continue;

            if (component is MeshFilter || component is MeshRenderer || component is SkinnedMeshRenderer)
                continue;

            UnityEngine.Object.DestroyImmediate(component, true);
        }
    }

    private static void EnsureAssetFolder(string assetFolderPath)
    {
        string[] segments = assetFolderPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length <= 0 || !string.Equals(segments[0], "Assets", StringComparison.Ordinal))
            throw new InvalidOperationException("GhostPlayerHostCustomUnityAssetExporter: temp asset folder must start with Assets/");

        string current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string next = current + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[i]);

            current = next;
        }
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
                throw new InvalidOperationException("GhostPlayerHostCustomUnityAssetExporter: unknown build profile " + buildProfile);
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
            bundle.Unload(unloadAllLoadedObjects: false);
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
            throw new InvalidOperationException("GhostPlayerHostCustomUnityAssetExporter: deploy bundle path must live under Custom/: " + absoluteBundlePath);

        return normalized.Substring(customIndex + 1);
    }

    private static void RenameBySuffix(Transform root, string suffix, string targetName)
    {
        Transform match = FindChildBySuffix(root, suffix);
        if (match != null)
            match.name = targetName;
    }

    private static GameObject FindObjectBySuffix(string suffix)
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] == null)
                continue;

            if (roots[i].name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return roots[i];

            Transform nested = FindChildBySuffix(roots[i].transform, suffix);
            if (nested != null)
                return nested.gameObject;
        }

        return null;
    }

    private static Transform FindChildBySuffix(Transform root, string suffix)
    {
        if (root == null || string.IsNullOrEmpty(suffix))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return child;

            Transform nested = FindChildBySuffix(child, suffix);
            if (nested != null)
                return nested;
        }

        return null;
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
