using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class GhostPlayerHostPackageCustomUnityAssetExporter
{
    private const string DefaultPackageRoot = "C:/projects/staging/frameangel-player-split/products/vam/plugins/player/build/cua_player_host_v1";
    private const string DefaultOutputRoot = "C:/projects/staging/frameangel-player-split/products/vam/plugins/player/build/locked_cua_exports";
    private const string DefaultDeployAssetsRoot = "F:/sim/vam/Custom/Assets/FrameAngel/Player";
    private const string DefaultDeployPresetRoot = "F:/sim/vam/Custom/Atom/CustomUnityAsset";
    private const string TempAssetRoot = "Assets/__FrameAngelTemp/PlayerHostCua";
    private const string MeshesRoot = TempAssetRoot + "/Meshes";
    private const string MaterialsRoot = TempAssetRoot + "/Materials";
    private const string TexturesRoot = TempAssetRoot + "/Textures";
    private const string TempPrefabPath = TempAssetRoot + "/fa_cua_player_host.prefab";
    private const string BundleFileName = "fa_cua_player_host.assetbundle";
    private const string PresetFileName = "Preset_FA CUA Player Host.vap";
    private const string SummaryFileName = "ghost_player_host_cua_export_summary.json";
    private const string QuadMeshPath = MeshesRoot + "/frameangel_unit_quad.asset";
    private const string ControlSnapshotMaterialPath = MaterialsRoot + "/fa_cua_player_host.control_snapshot.mat";

    [Serializable]
    private sealed class PackageManifest
    {
        public string schemaVersion = "";
        public string packageId = "";
        public string resourceId = "";
        public string displayName = "";
    }

    [Serializable]
    private sealed class ExportSummary
    {
        public string schemaVersion = "ghost_player_host_customunityasset_export_summary_v2";
        public string generatedAtUtc = "";
        public string packageRoot = "";
        public string outputRoot = "";
        public string deployAssetsRoot = "";
        public string deployPresetRoot = "";
        public List<ExportEntry> exports = new List<ExportEntry>();
    }

    [Serializable]
    private sealed class ExportEntry
    {
        public string shellKey = "player_host";
        public string displayName = "FA CUA Player Host";
        public string resourceId = "fa_cua_player_host";
        public string bundlePath = "";
        public string assetName = "";
        public string presetPath = "";
        public string deployBundlePath = "";
        public string deployPresetPath = "";
        public string assetUrl = "";
        public string sourcePackageId = "";
    }

    [Serializable]
    private sealed class GeometryDoc
    {
        public Node[] nodes;
        public MeshDoc[] meshes;
    }

    [Serializable]
    private sealed class Node
    {
        public string nodeId;
        public string parentNodeId;
        public string displayName;
        public SerializableVector3 localPosition;
        public SerializableQuaternion localRotation;
        public SerializableVector3 localScale;
        public string[] meshRefIds;
    }

    [Serializable]
    private sealed class MeshDoc
    {
        public string meshId;
        public string materialRefId;
        public SerializableVector3[] vertices;
        public int[] triangleIndices;
        public SerializableVector3[] normals;
        public SerializableVector2[] uv0;
    }

    [Serializable]
    private sealed class MaterialsDoc
    {
        public MaterialDoc[] materials;
    }

    [Serializable]
    private sealed class MaterialDoc
    {
        public string materialRefId;
        public string shaderName;
        public string baseColorHex;
        public string texturePngBase64;
    }

    [Serializable]
    private sealed class ScreensDoc
    {
        public ScreenSlot[] slots;
    }

    [Serializable]
    private sealed class ScreenSlot
    {
        public string screenSurfaceNodeId;
        public string disconnectSurfaceNodeId;
        public string screenGlassNodeId;
    }

    [Serializable]
    private sealed class ControlsDoc
    {
        public string surfaceNodeId;
        public string colliderNodeId;
        public float surfaceWidthMeters;
        public float surfaceHeightMeters;
    }

    [Serializable]
    private struct SerializableVector2
    {
        public float x;
        public float y;

        public Vector2 ToUnity()
        {
            return new Vector2(x, y);
        }
    }

    [Serializable]
    private struct SerializableVector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3 ToUnity()
        {
            return new Vector3(x, y, z);
        }
    }

    [Serializable]
    private struct SerializableQuaternion
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public Quaternion ToUnity()
        {
            return new Quaternion(x, y, z, w);
        }
    }

    public static void ExportPlayerHostPackageCuaBatch()
    {
        string[] args = Environment.GetCommandLineArgs();
        string outputRoot = GetArg(args, "-faOutputRoot", DefaultOutputRoot);
        string deployAssetsRoot = GetArg(args, "-faDeployAssetsRoot", DefaultDeployAssetsRoot);
        string deployPresetRoot = GetArg(args, "-faDeployPresetRoot", DefaultDeployPresetRoot);
        string packageRoot = GetArg(args, "-faPackageRoot", DefaultPackageRoot);
        string shellKeysCsv = GetArg(args, "-faShellKeys", "player_host");
        string[] shellKeys = shellKeysCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .ToArray();

        if (shellKeys.Length > 1 || (shellKeys.Length == 1 && !string.Equals(shellKeys[0], "player_host", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("GhostPlayerHostPackageCustomUnityAssetExporter only supports -faShellKeys player_host.");

        bool deploy = GetBoolArg(args, "-faDeploy", true);
        string buildProfile = GetArg(args, "-faBuildProfile", "current");
        ExportInternal(outputRoot, deployAssetsRoot, deployPresetRoot, packageRoot, deploy, buildProfile);
    }

    private static void ExportInternal(string outputRoot, string deployAssetsRoot, string deployPresetRoot, string packageRoot, bool deploy, string buildProfile)
    {
        string resolvedPackageRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(packageRoot) ? DefaultPackageRoot : packageRoot);
        string resolvedOutputRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(outputRoot) ? DefaultOutputRoot : outputRoot);
        string resolvedDeployAssetsRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(deployAssetsRoot) ? DefaultDeployAssetsRoot : deployAssetsRoot);
        string resolvedDeployPresetRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(deployPresetRoot) ? DefaultDeployPresetRoot : deployPresetRoot);

        AssertFile(Path.Combine(resolvedPackageRoot, "manifest.json"));
        AssertFile(Path.Combine(resolvedPackageRoot, "geometry.innerpiece.json"));
        AssertFile(Path.Combine(resolvedPackageRoot, "materials.innerpiece.json"));
        AssertFile(Path.Combine(resolvedPackageRoot, "screens.innerpiece.json"));
        AssertFile(Path.Combine(resolvedPackageRoot, "controls.innerpiece.json"));

        Directory.CreateDirectory(resolvedOutputRoot);
        Directory.CreateDirectory(resolvedDeployAssetsRoot);
        Directory.CreateDirectory(resolvedDeployPresetRoot);

        EnsureAssetFolder(TempAssetRoot);
        EnsureAssetFolder(MeshesRoot);
        EnsureAssetFolder(MaterialsRoot);
        EnsureAssetFolder(TexturesRoot);

        PackageManifest packageManifest = LoadJson<PackageManifest>(Path.Combine(resolvedPackageRoot, "manifest.json"));
        GeometryDoc geometry = LoadJson<GeometryDoc>(Path.Combine(resolvedPackageRoot, "geometry.innerpiece.json"));
        MaterialsDoc materials = LoadJson<MaterialsDoc>(Path.Combine(resolvedPackageRoot, "materials.innerpiece.json"));
        ScreensDoc screens = LoadJson<ScreensDoc>(Path.Combine(resolvedPackageRoot, "screens.innerpiece.json"));
        ControlsDoc controls = LoadJson<ControlsDoc>(Path.Combine(resolvedPackageRoot, "controls.innerpiece.json"));

        Dictionary<string, Material> materialMap = BuildMaterialMap(materials);
        Dictionary<string, MeshDoc> meshDocs = BuildMeshDocMap(geometry.meshes);
        Dictionary<string, Mesh> meshMap = BuildMeshMap(geometry.meshes);

        string screenSurfaceNodeId = (screens != null && screens.slots != null && screens.slots.Length > 0) ? screens.slots[0].screenSurfaceNodeId : "screen_surface";
        string disconnectSurfaceNodeId = (screens != null && screens.slots != null && screens.slots.Length > 0) ? screens.slots[0].disconnectSurfaceNodeId : "disconnect_surface";
        string controlSurfaceNodeId = (controls != null && !string.IsNullOrEmpty(controls.surfaceNodeId)) ? controls.surfaceNodeId : "control_surface";

        GameObject root = BuildHierarchy(geometry, meshDocs, meshMap, materialMap);
        try
        {
            root.name = "fa_cua_player_host";

            GameObject screenSurface = FindNode(root, screenSurfaceNodeId);
            GameObject disconnectSurface = FindNode(root, disconnectSurfaceNodeId);
            GameObject controlSurface = FindNode(root, controlSurfaceNodeId);
            if (screenSurface == null)
                throw new InvalidOperationException("screen_surface node was not found in composed host package.");

            NormalizeContractSurfaces(screenSurface, disconnectSurface, controlSurface, controls);
            ApplyPackageMaterial(screenSurface, screenSurfaceNodeId, geometry, meshDocs, materialMap);
            ApplyPackageMaterial(disconnectSurface, disconnectSurfaceNodeId, geometry, meshDocs, materialMap);
            ApplySnapshotMaterial(controlSurface, materialMap, meshDocs);
            DisableKnownPlaceholderNodes(root);
            EnsureAnchor(root.transform, "bottom_anchor", new Vector3(0f, -0.55f, 0.08f));
            if (controlSurface != null)
                EnsureAnchor(root.transform, "controls_anchor", controlSurface.transform.localPosition);

            string prefabAbsolutePath = Path.Combine(Directory.GetCurrentDirectory(), TempPrefabPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(prefabAbsolutePath))
                AssetDatabase.DeleteAsset(TempPrefabPath);

            PrefabUtility.SaveAsPrefabAsset(root, TempPrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        string bundleOutputRoot = Path.Combine(resolvedOutputRoot, "assetbundles");
        Directory.CreateDirectory(bundleOutputRoot);

        AssetBundleBuild build = new AssetBundleBuild
        {
            assetBundleName = BundleFileName,
            assetNames = new[] { TempPrefabPath }
        };

        BuildPipeline.BuildAssetBundles(
            bundleOutputRoot,
            new[] { build },
            ResolveBuildOptions(buildProfile),
            BuildTarget.StandaloneWindows64);

        string builtBundlePath = Path.Combine(bundleOutputRoot, BundleFileName);
        if (!File.Exists(builtBundlePath))
            throw new InvalidOperationException("Package-driven direct bundle was not written: " + builtBundlePath);

        string assetName = ResolveFirstPrefabAssetName(builtBundlePath, TempPrefabPath);
        if (string.IsNullOrWhiteSpace(assetName))
            throw new InvalidOperationException("Could not resolve prefab asset name from bundle: " + builtBundlePath);

        string outputPresetRoot = Path.Combine(resolvedOutputRoot, "presets");
        Directory.CreateDirectory(outputPresetRoot);
        string outputPresetPath = Path.Combine(outputPresetRoot, PresetFileName);
        string deployedBundlePath = Path.Combine(resolvedDeployAssetsRoot, BundleFileName);
        string deployedPresetPath = Path.Combine(resolvedDeployPresetRoot, PresetFileName);
        string assetUrl = ConvertToVamCustomAssetUrl(deployedBundlePath);
        string presetJson = BuildCustomUnityAssetPresetJson(assetUrl, assetName);

        WriteTextFile(outputPresetPath, presetJson);
        if (deploy)
        {
            File.Copy(builtBundlePath, deployedBundlePath, true);
            WriteTextFile(deployedPresetPath, presetJson);
        }

        ExportSummary summary = new ExportSummary
        {
            generatedAtUtc = DateTime.UtcNow.ToString("o"),
            packageRoot = resolvedPackageRoot,
            outputRoot = resolvedOutputRoot,
            deployAssetsRoot = resolvedDeployAssetsRoot,
            deployPresetRoot = resolvedDeployPresetRoot,
            exports = new List<ExportEntry>
            {
                new ExportEntry
                {
                    bundlePath = builtBundlePath,
                    assetName = assetName,
                    presetPath = outputPresetPath,
                    deployBundlePath = deploy ? deployedBundlePath : "",
                    deployPresetPath = deploy ? deployedPresetPath : "",
                    assetUrl = assetUrl,
                    sourcePackageId = packageManifest != null ? packageManifest.packageId : ""
                }
            }
        };

        WriteTextFile(Path.Combine(resolvedOutputRoot, SummaryFileName), JsonUtility.ToJson(summary, true));
    }

    private static T LoadJson<T>(string path) where T : class
    {
        T value = JsonUtility.FromJson<T>(File.ReadAllText(path));
        if (value == null)
            throw new InvalidOperationException("Unable to parse JSON: " + path);
        return value;
    }

    private static Dictionary<string, Material> BuildMaterialMap(MaterialsDoc materials)
    {
        Dictionary<string, Material> map = new Dictionary<string, Material>(StringComparer.Ordinal);
        if (materials == null || materials.materials == null)
            return map;

        for (int i = 0; i < materials.materials.Length; i++)
        {
            MaterialDoc source = materials.materials[i];
            if (source == null || string.IsNullOrEmpty(source.materialRefId))
                continue;

            string path = MaterialsRoot + "/" + Sanitize(source.materialRefId) + ".mat";
            Shader shader = ResolveShader(source);
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

            material.color = ParseColor(source.baseColorHex);
            material.mainTexture = ResolveTexture(source);
            ConfigureMaterial(material, source);
            EditorUtility.SetDirty(material);
            map[source.materialRefId] = material;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return map;
    }

    private static Dictionary<string, MeshDoc> BuildMeshDocMap(MeshDoc[] meshes)
    {
        Dictionary<string, MeshDoc> map = new Dictionary<string, MeshDoc>(StringComparer.Ordinal);
        if (meshes == null)
            return map;

        for (int i = 0; i < meshes.Length; i++)
        {
            MeshDoc mesh = meshes[i];
            if (mesh != null && !string.IsNullOrEmpty(mesh.meshId))
                map[mesh.meshId] = mesh;
        }

        return map;
    }

    private static Dictionary<string, Mesh> BuildMeshMap(MeshDoc[] meshes)
    {
        Dictionary<string, Mesh> map = new Dictionary<string, Mesh>(StringComparer.Ordinal);
        if (meshes == null)
            return map;

        for (int i = 0; i < meshes.Length; i++)
        {
            MeshDoc source = meshes[i];
            if (source == null || string.IsNullOrEmpty(source.meshId))
                continue;

            string path = MeshesRoot + "/" + Sanitize(source.meshId) + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, path);
            }

            mesh.name = source.meshId;
            mesh.Clear();
            mesh.vertices = (source.vertices ?? Array.Empty<SerializableVector3>()).Select(item => item.ToUnity()).ToArray();
            mesh.triangles = source.triangleIndices ?? Array.Empty<int>();
            SerializableVector3[] normals = source.normals ?? Array.Empty<SerializableVector3>();
            if (normals.Length == mesh.vertexCount)
                mesh.normals = normals.Select(item => item.ToUnity()).ToArray();
            else
                mesh.RecalculateNormals();

            SerializableVector2[] uv0 = source.uv0 ?? Array.Empty<SerializableVector2>();
            if (uv0.Length == mesh.vertexCount)
                mesh.uv = uv0.Select(item => item.ToUnity()).ToArray();

            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            map[source.meshId] = mesh;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return map;
    }

    private static GameObject BuildHierarchy(GeometryDoc geometry, Dictionary<string, MeshDoc> meshDocs, Dictionary<string, Mesh> meshMap, Dictionary<string, Material> materialMap)
    {
        if (geometry == null || geometry.nodes == null || geometry.nodes.Length == 0)
            throw new InvalidOperationException("Composed host package did not contain geometry nodes.");

        Dictionary<string, GameObject> objectMap = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        GameObject root = null;

        for (int i = 0; i < geometry.nodes.Length; i++)
        {
            Node node = geometry.nodes[i];
            if (node == null || string.IsNullOrEmpty(node.nodeId))
                continue;

            GameObject go = new GameObject(node.nodeId);
            objectMap[node.nodeId] = go;
            if (string.IsNullOrEmpty(node.parentNodeId))
                root = go;
        }

        if (root == null)
            throw new InvalidOperationException("Composed host package root was not found.");

        for (int i = 0; i < geometry.nodes.Length; i++)
        {
            Node node = geometry.nodes[i];
            if (node == null || string.IsNullOrEmpty(node.nodeId))
                continue;

            GameObject go = objectMap[node.nodeId];
            if (!string.IsNullOrEmpty(node.parentNodeId) && objectMap.ContainsKey(node.parentNodeId))
                go.transform.SetParent(objectMap[node.parentNodeId].transform, false);
            else
                go.transform.SetParent(null, false);

            go.transform.localPosition = node.localPosition.ToUnity();
            go.transform.localRotation = node.localRotation.ToUnity();
            go.transform.localScale = IsZero(node.localScale) ? Vector3.one : node.localScale.ToUnity();

            if (node.meshRefIds == null)
                continue;

            for (int meshIndex = 0; meshIndex < node.meshRefIds.Length; meshIndex++)
            {
                string meshId = node.meshRefIds[meshIndex];
                if (string.IsNullOrEmpty(meshId) || !meshMap.ContainsKey(meshId))
                    continue;

                GameObject target = node.meshRefIds.Length == 1 ? go : new GameObject("mesh_" + meshIndex.ToString(CultureInfo.InvariantCulture));
                if (target != go)
                    target.transform.SetParent(go.transform, false);

                MeshFilter meshFilter = target.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = meshMap[meshId];
                MeshRenderer meshRenderer = target.AddComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode = ShadowCastingMode.On;
                meshRenderer.receiveShadows = true;

                MeshDoc meshDoc = meshDocs.ContainsKey(meshId) ? meshDocs[meshId] : null;
                if (meshDoc != null && !string.IsNullOrEmpty(meshDoc.materialRefId) && materialMap.ContainsKey(meshDoc.materialRefId))
                    meshRenderer.sharedMaterial = materialMap[meshDoc.materialRefId];
            }
        }

        return root;
    }

    private static void NormalizeContractSurfaces(GameObject screenSurface, GameObject disconnectSurface, GameObject controlSurface, ControlsDoc controls)
    {
        Vector3 screenScale = screenSurface.transform.localScale;
        Vector3 disconnectScale = disconnectSurface != null ? disconnectSurface.transform.localScale : screenScale;
        float controlWidth = controls != null && controls.surfaceWidthMeters > 0f ? controls.surfaceWidthMeters : 0.4608f;
        float controlHeight = controls != null && controls.surfaceHeightMeters > 0f ? controls.surfaceHeightMeters : 0.3096f;

        RebuildSurfaceAsQuad(screenSurface, screenScale.x, screenScale.y);
        if (disconnectSurface != null)
            RebuildSurfaceAsQuad(disconnectSurface, disconnectScale.x, disconnectScale.y);
        if (controlSurface != null)
            RebuildControlSurface(controlSurface, controlWidth, controlHeight);
    }

    private static void RebuildSurfaceAsQuad(GameObject node, float width, float height)
    {
        if (node == null)
            return;

        ClearVisualComponents(node);
        node.transform.localScale = Vector3.one;

        MeshFilter meshFilter = node.GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = node.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = LoadOrCreateQuadMesh();

        MeshRenderer meshRenderer = node.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = node.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        node.transform.localScale = new Vector3(width, height, 1f);
    }

    private static void RebuildControlSurface(GameObject controlSurface, float width, float height)
    {
        DisableChildren(controlSurface.transform);
        RebuildSurfaceAsQuad(controlSurface, width, height);
    }

    private static void ApplyPackageMaterial(GameObject node, string nodeId, GeometryDoc geometry, Dictionary<string, MeshDoc> meshDocs, Dictionary<string, Material> materialMap)
    {
        if (node == null || geometry == null || geometry.nodes == null)
            return;

        Node sourceNode = geometry.nodes.FirstOrDefault(item => item != null && string.Equals(item.nodeId, nodeId, StringComparison.Ordinal));
        if (sourceNode == null || sourceNode.meshRefIds == null || sourceNode.meshRefIds.Length <= 0)
            return;

        string meshId = sourceNode.meshRefIds[0];
        if (!meshDocs.ContainsKey(meshId))
            return;

        MeshDoc meshDoc = meshDocs[meshId];
        if (meshDoc == null || string.IsNullOrEmpty(meshDoc.materialRefId) || !materialMap.ContainsKey(meshDoc.materialRefId))
            return;

        MeshRenderer renderer = node.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.sharedMaterial = materialMap[meshDoc.materialRefId];
    }

    private static void ApplySnapshotMaterial(GameObject controlSurface, Dictionary<string, Material> materialMap, Dictionary<string, MeshDoc> meshDocs)
    {
        if (controlSurface == null)
            return;

        const string snapshotMeshId = "toolkitexport_meta_patterns_contentuiexample_videoplayer_e7cfc411_contentuiexample_videoplayer_canvasroot_controls_snapshot_mesh";
        if (!meshDocs.ContainsKey(snapshotMeshId))
            return;

        MeshDoc meshDoc = meshDocs[snapshotMeshId];
        if (meshDoc == null || string.IsNullOrEmpty(meshDoc.materialRefId) || !materialMap.ContainsKey(meshDoc.materialRefId))
            return;

        MeshRenderer renderer = controlSurface.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.sharedMaterial = CreateCompatibleSnapshotMaterial(materialMap[meshDoc.materialRefId]);
    }

    private static void DisableKnownPlaceholderNodes(GameObject root)
    {
        string[] nodeIds =
        {
            "control__toolkitexport_meta_patterns_contentuiexample_videoplayer_e7cfc411_contentuiexample_videoplayer_canvasroot_controls_demovideocontent_box1",
            "control__toolkitexport_meta_patterns_contentuiexample_videoplayer_e7cfc411_contentuiexample_videoplayer_canvasroot_controls_demovideocontent_box2",
            "control__toolkitexport_meta_patterns_contentuiexample_videoplayer_e7cfc411_contentuiexample_videoplayer_canvasroot_controls_demovideocontent_box3",
            "control__toolkitexport_meta_patterns_contentuiexample_videoplayer_e7cfc411_contentuiexample_videoplayer_canvasroot_controls_demovideocontent_box4"
        };

        foreach (string nodeId in nodeIds)
        {
            GameObject go = FindNode(root, nodeId);
            if (go != null)
                go.SetActive(false);
        }
    }

    private static void EnsureAnchor(Transform parent, string nodeId, Vector3 localPosition)
    {
        if (parent.Find(nodeId) != null)
            return;

        GameObject go = new GameObject(nodeId);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
    }

    private static GameObject FindNode(GameObject root, string nodeId)
    {
        if (root == null || string.IsNullOrEmpty(nodeId))
            return null;

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (string.Equals(all[i].name, nodeId, StringComparison.Ordinal))
                return all[i].gameObject;
        }

        return null;
    }

    private static Material CreateCompatibleSnapshotMaterial(Material sourceMaterial)
    {
        Shader shader = Shader.Find("Unlit/Texture");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(ControlSnapshotMaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, ControlSnapshotMaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        material.color = Color.white;
        material.mainTexture = sourceMaterial != null ? sourceMaterial.mainTexture : null;
        if (material.HasProperty("_Cull"))
            material.SetInt("_Cull", (int)CullMode.Off);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Shader ResolveShader(MaterialDoc material)
    {
        Shader shader = !string.IsNullOrEmpty(material.shaderName) ? Shader.Find(material.shaderName) : null;
        if (shader == null && !string.IsNullOrEmpty(material.texturePngBase64))
            shader = Shader.Find("Unlit/Texture");
        if (shader == null)
            shader = Shader.Find("Standard");
        return shader;
    }

    private static void ConfigureMaterial(Material material, MaterialDoc source)
    {
        bool transparent = material.color.a < 0.999f;
        if (material.shader != null && string.Equals(material.shader.name, "Standard", StringComparison.Ordinal))
        {
            if (transparent)
            {
                material.SetFloat("_Mode", 3f);
                material.SetInt("_SrcBlend", (int)BlendMode.One);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = 3000;
                material.SetOverrideTag("RenderType", "Transparent");
            }
            else
            {
                material.SetFloat("_Mode", 0f);
                material.SetInt("_SrcBlend", (int)BlendMode.One);
                material.SetInt("_DstBlend", (int)BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = -1;
                material.SetOverrideTag("RenderType", string.Empty);
            }
        }

        if (!string.IsNullOrEmpty(source.texturePngBase64) && material.HasProperty("_Cull"))
            material.SetInt("_Cull", (int)CullMode.Off);
    }

    private static Texture2D ResolveTexture(MaterialDoc source)
    {
        // Keep the package-driven 2022 witness structurally closer to the older
        // direct-CUA family that previously produced the valid wrong-resource TV
        // witness. We are currently chasing validity first, not presentation.
        // Pulling embedded textures into the bundle introduces an extra
        // Texture2D class delta that the older witness family did not have.
        if (string.IsNullOrEmpty(source.texturePngBase64))
            return null;

        return null;
    }

    private static Color ParseColor(string value)
    {
        if (string.IsNullOrEmpty(value))
            return Color.white;

        string hex = value.Trim().TrimStart('#');
        if (hex.Length == 6)
            hex += "FF";
        if (hex.Length != 8)
            return Color.white;

        byte r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        byte a = byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new Color32(r, g, b, a);
    }

    private static string Sanitize(string value)
    {
        char[] chars = (value ?? "unnamed").ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.'))
                chars[i] = '_';
        }

        return new string(chars);
    }

    private static Mesh LoadOrCreateQuadMesh()
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(QuadMeshPath);
        if (mesh != null)
            return mesh;

        mesh = new Mesh();
        mesh.name = "frameangel_unit_quad";
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };
        mesh.normals = new[]
        {
            Vector3.forward,
            Vector3.forward,
            Vector3.forward,
            Vector3.forward
        };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, QuadMeshPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return mesh;
    }

    private static void ClearVisualComponents(GameObject node)
    {
        MeshFilter meshFilter = node.GetComponent<MeshFilter>();
        if (meshFilter != null)
            UnityEngine.Object.DestroyImmediate(meshFilter, true);

        MeshRenderer meshRenderer = node.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            UnityEngine.Object.DestroyImmediate(meshRenderer, true);
    }

    private static void DisableChildren(Transform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
            parent.GetChild(i).gameObject.SetActive(false);
    }

    private static void EnsureAssetFolder(string assetFolderPath)
    {
        string[] segments = assetFolderPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length <= 0 || !string.Equals(segments[0], "Assets", StringComparison.Ordinal))
            throw new InvalidOperationException("Temp asset folder must start with Assets/.");

        string current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string next = current + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[i]);
            current = next;
        }
    }

    private static bool IsZero(SerializableVector3 value)
    {
        return Mathf.Approximately(value.x, 0f) && Mathf.Approximately(value.y, 0f) && Mathf.Approximately(value.z, 0f);
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
            default:
                throw new InvalidOperationException("Unknown build profile " + buildProfile);
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
        return string.IsNullOrWhiteSpace(assetPath) ? "" : assetPath.Replace('\\', '/').ToLowerInvariant();
    }

    private static string ConvertToVamCustomAssetUrl(string absoluteBundlePath)
    {
        string normalized = absoluteBundlePath.Replace('\\', '/');
        int customIndex = normalized.IndexOf("/Custom/", StringComparison.OrdinalIgnoreCase);
        if (customIndex < 0)
            throw new InvalidOperationException("Deploy bundle path must live under Custom/: " + absoluteBundlePath);

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
                case '\\': builder.Append("\\\\"); break;
                case '"': builder.Append("\\\""); break;
                case '\r': builder.Append("\\r"); break;
                case '\n': builder.Append("\\n"); break;
                case '\t': builder.Append("\\t"); break;
                default: builder.Append(ch); break;
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

        bool parsed;
        return bool.TryParse(value, out parsed) ? parsed : fallback;
    }

    private static void WriteTextFile(string path, string content)
    {
        string directory = Path.GetDirectoryName(path) ?? "";
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    private static void AssertFile(string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException("Required file not found: " + path);
    }
}
