using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class FrameAngelPlayerShell2018Exporter
{
    private const string DefaultOutputRoot = "C:/projects/fa/products/vam/assets/player/build/shell_assetbundle_exports_2018/current";
    private const string DefaultPackageRoot = "C:/projects/fa/products/vam/assets/player/build/host_shell_exports/mcbrooke_laptop";
    private const string DefaultBundleFileName = "fa_cua_player_shell_current.assetbundle";

    private sealed class ExportOptions
    {
        public string OutputRoot = DefaultOutputRoot;
        public string PackageRoot = DefaultPackageRoot;
        public string HostProfilePath = "";
        public string BundleFileName = DefaultBundleFileName;
        public string ResourceId = "";
        public string DisplayName = "";
        public string ShellKey = "";
    }

    [Serializable]
    private sealed class ExportSummary
    {
        public string schemaVersion = "frameangel_player_shell_2018_export_summary_v1";
        public string generatedAtUtc = "";
        public string unityVersion = "";
        public string shellKey = "";
        public string displayName = "";
        public string resourceId = "";
        public string packageRoot = "";
        public string hostProfilePath = "";
        public string prefabPath = "";
        public string bundlePath = "";
        public string assetName = "";
    }

    [Serializable]
    private sealed class HostProfile
    {
        public string shellKey = "";
        public string hostDisplayName = "";
        public string hostResourceId = "";
        public string screenSurfaceNodeId = "";
        public string disconnectSurfaceNodeId = "";
        public string screenGlassNodeId = "";
        public string controlsAnchorNodeId = "";
        public string bottomAnchorNodeId = "";
    }

    [Serializable]
    private sealed class PackageManifest
    {
        public string packageId = "";
        public string resourceId = "";
        public string displayName = "";
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

    public static void BuildAndDeployBatch()
    {
        Build(ParseBatchOptions());
    }

    private static ExportOptions ParseBatchOptions()
    {
        string[] args = Environment.GetCommandLineArgs();
        ExportOptions options = new ExportOptions();
        options.OutputRoot = GetArg(args, "-faOutputRoot", options.OutputRoot);
        options.PackageRoot = GetArg(args, "-faPackageRoot", options.PackageRoot);
        options.HostProfilePath = GetArg(args, "-faHostProfilePath", options.HostProfilePath);
        options.BundleFileName = GetArg(args, "-faBundleFileName", options.BundleFileName);
        options.ResourceId = GetArg(args, "-faResourceId", options.ResourceId);
        options.DisplayName = GetArg(args, "-faDisplayName", options.DisplayName);
        options.ShellKey = GetArg(args, "-faShellKey", options.ShellKey);
        return options;
    }

    private static void Build(ExportOptions options)
    {
        string resolvedOutputRoot = Path.GetFullPath(options.OutputRoot);
        string resolvedPackageRoot = Path.GetFullPath(options.PackageRoot);
        string resolvedHostProfilePath = IsBlank(options.HostProfilePath)
            ? Path.Combine(resolvedPackageRoot, "host_profile.json")
            : Path.GetFullPath(options.HostProfilePath);

        AssertFile(Path.Combine(resolvedPackageRoot, "manifest.json"));
        AssertFile(Path.Combine(resolvedPackageRoot, "geometry.innerpiece.json"));
        AssertFile(Path.Combine(resolvedPackageRoot, "materials.innerpiece.json"));
        AssertFile(resolvedHostProfilePath);

        Directory.CreateDirectory(resolvedOutputRoot);

        HostProfile hostProfile = LoadJson<HostProfile>(resolvedHostProfilePath);
        PackageManifest packageManifest = LoadJson<PackageManifest>(Path.Combine(resolvedPackageRoot, "manifest.json"));
        GeometryDoc geometry = LoadJson<GeometryDoc>(Path.Combine(resolvedPackageRoot, "geometry.innerpiece.json"));
        MaterialsDoc materials = LoadJson<MaterialsDoc>(Path.Combine(resolvedPackageRoot, "materials.innerpiece.json"));

        string resourceId = ResolveResourceId(options, hostProfile, packageManifest);
        string displayName = ResolveDisplayName(options, hostProfile, packageManifest, resourceId);
        string shellKey = ResolveShellKey(options, hostProfile, resourceId);

        string tempRoot = "Assets/FrameAngel/PlayerShell2018Temp/" + Sanitize(resourceId);
        string meshesRoot = tempRoot + "/Meshes";
        string materialsRoot = tempRoot + "/Materials";
        string prefabPath = tempRoot + "/" + resourceId + ".prefab";
        string quadMeshPath = meshesRoot + "/frameangel_unit_quad.asset";

        if (AssetDatabase.IsValidFolder(tempRoot))
            AssetDatabase.DeleteAsset(tempRoot);

        EnsureAssetFolder(tempRoot);
        EnsureAssetFolder(meshesRoot);
        EnsureAssetFolder(materialsRoot);

        Dictionary<string, Material> materialMap = BuildMaterialMap(materials, materialsRoot);
        Dictionary<string, MeshDoc> meshDocs = BuildMeshDocMap(geometry.meshes);
        Dictionary<string, Mesh> meshMap = BuildMeshMap(geometry.meshes, meshesRoot);

        GameObject root = BuildHierarchy(geometry, meshDocs, meshMap, materialMap);
        try
        {
            root.name = resourceId;

            RenameNode(root, hostProfile.screenSurfaceNodeId, "screen_surface");
            RenameNode(root, hostProfile.disconnectSurfaceNodeId, "disconnect_surface");
            RenameNode(root, hostProfile.screenGlassNodeId, "screen_glass");
            RenameNode(root, hostProfile.controlsAnchorNodeId, "controls_anchor");
            RenameNode(root, hostProfile.bottomAnchorNodeId, "bottom_anchor");

            GameObject screenSurface = FindNode(root, "screen_surface");
            GameObject disconnectSurface = FindNode(root, "disconnect_surface");
            GameObject screenGlass = FindNode(root, "screen_glass");
            GameObject controlsAnchor = FindNode(root, "controls_anchor");
            GameObject bottomAnchor = FindNode(root, "bottom_anchor");

            if (screenSurface == null)
                throw new InvalidOperationException("FrameAngelPlayerShell2018Exporter: screen_surface node was not found.");

            NormalizeSurfaceAsQuad(screenSurface, quadMeshPath);
            if (disconnectSurface != null)
                NormalizeSurfaceAsQuad(disconnectSurface, quadMeshPath);

            ApplyPackageMaterial(screenSurface, hostProfile.screenSurfaceNodeId, geometry, meshDocs, materialMap);
            ApplyPackageMaterial(disconnectSurface, hostProfile.disconnectSurfaceNodeId, geometry, meshDocs, materialMap);
            ApplyPackageMaterial(screenGlass, hostProfile.screenGlassNodeId, geometry, meshDocs, materialMap);
            PromoteDisplaySurfaceToVisibleFront(root.transform, screenSurface, disconnectSurface, screenGlass, controlsAnchor);

            if (bottomAnchor == null)
            {
                Vector3 defaultBottomAnchor = new Vector3(
                    screenSurface.transform.localPosition.x,
                    screenSurface.transform.localPosition.y - (screenSurface.transform.localScale.y * 0.5f),
                    screenSurface.transform.localPosition.z);
                bottomAnchor = EnsureAnchor(root.transform, "bottom_anchor", defaultBottomAnchor);
            }

            if (controlsAnchor == null)
            {
                Vector3 defaultControlsAnchor = new Vector3(
                    screenSurface.transform.localPosition.x,
                    bottomAnchor.transform.localPosition.y - 0.18f,
                    screenSurface.transform.localPosition.z + 0.01f);
                controlsAnchor = EnsureAnchor(root.transform, "controls_anchor", defaultControlsAnchor);
            }

            string prefabAbsolutePath = Path.Combine(Directory.GetCurrentDirectory(), prefabPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(prefabAbsolutePath))
                AssetDatabase.DeleteAsset(prefabPath);

            PrefabUtility.CreatePrefab(prefabPath, root, ReplacePrefabOptions.ReplaceNameBased);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        string bundleOutputRoot = Path.Combine(resolvedOutputRoot, "assetbundles");
        Directory.CreateDirectory(bundleOutputRoot);

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
            throw new InvalidOperationException("FrameAngelPlayerShell2018Exporter: bundle was not written: " + bundlePath);

        string assetName = ResolveFirstAssetName(bundlePath, prefabPath);
        ExportSummary summary = new ExportSummary();
        summary.generatedAtUtc = DateTime.UtcNow.ToString("o");
        summary.unityVersion = Application.unityVersion;
        summary.shellKey = shellKey;
        summary.displayName = displayName;
        summary.resourceId = resourceId;
        summary.packageRoot = resolvedPackageRoot;
        summary.hostProfilePath = resolvedHostProfilePath;
        summary.prefabPath = prefabPath;
        summary.bundlePath = bundlePath;
        summary.assetName = assetName;

        WriteTextFile(Path.Combine(resolvedOutputRoot, "player_shell_summary.json"), JsonUtility.ToJson(summary, true));
        Debug.Log("FrameAngelPlayerShell2018Exporter: built " + bundlePath);

        if (AssetDatabase.IsValidFolder(tempRoot))
        {
            AssetDatabase.DeleteAsset(tempRoot);
            AssetDatabase.Refresh();
        }
    }

    private static string ResolveResourceId(ExportOptions options, HostProfile hostProfile, PackageManifest packageManifest)
    {
        if (!IsBlank(options.ResourceId))
            return options.ResourceId.Trim();
        if (hostProfile != null && !IsBlank(hostProfile.hostResourceId))
            return StripTrailingVersionTag(hostProfile.hostResourceId.Trim());
        if (packageManifest != null && !IsBlank(packageManifest.resourceId))
            return StripTrailingHashTag(packageManifest.resourceId.Trim());
        return "fa_cua_player_shell";
    }

    private static string ResolveDisplayName(ExportOptions options, HostProfile hostProfile, PackageManifest packageManifest, string resourceId)
    {
        if (!IsBlank(options.DisplayName))
            return options.DisplayName.Trim();
        if (hostProfile != null && !IsBlank(hostProfile.hostDisplayName))
            return hostProfile.hostDisplayName.Trim();
        if (packageManifest != null && !IsBlank(packageManifest.displayName))
            return packageManifest.displayName.Trim();
        return resourceId;
    }

    private static string ResolveShellKey(ExportOptions options, HostProfile hostProfile, string resourceId)
    {
        if (!IsBlank(options.ShellKey))
            return options.ShellKey.Trim();
        if (hostProfile != null && !IsBlank(hostProfile.shellKey))
            return hostProfile.shellKey.Trim();
        return resourceId;
    }

    private static string StripTrailingVersionTag(string value)
    {
        if (IsBlank(value))
            return value;
        if (value.EndsWith("_v1", StringComparison.OrdinalIgnoreCase))
            return value.Substring(0, value.Length - 3);
        return value;
    }

    private static string StripTrailingHashTag(string value)
    {
        if (IsBlank(value))
            return value;
        int separator = value.LastIndexOf('_');
        if (separator <= 0 || separator >= (value.Length - 1))
            return value;
        string suffix = value.Substring(separator + 1);
        if (suffix.Length == 12 && suffix.All(IsLowerHex))
            return value.Substring(0, separator);
        return value;
    }

    private static bool IsLowerHex(char value)
    {
        return (value >= '0' && value <= '9') || (value >= 'a' && value <= 'f');
    }

    private static T LoadJson<T>(string path) where T : class
    {
        T value = JsonUtility.FromJson<T>(File.ReadAllText(path));
        if (value == null)
            throw new InvalidOperationException("FrameAngelPlayerShell2018Exporter: unable to parse JSON: " + path);
        return value;
    }

    private static Dictionary<string, Material> BuildMaterialMap(MaterialsDoc materials, string materialsRoot)
    {
        Dictionary<string, Material> map = new Dictionary<string, Material>(StringComparer.Ordinal);
        if (materials == null || materials.materials == null)
            return map;

        for (int i = 0; i < materials.materials.Length; i++)
        {
            MaterialDoc source = materials.materials[i];
            if (source == null || string.IsNullOrEmpty(source.materialRefId))
                continue;

            string path = materialsRoot + "/" + Sanitize(source.materialRefId) + ".mat";
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
            material.mainTexture = null;
            ConfigureMaterial(material);
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

    private static Dictionary<string, Mesh> BuildMeshMap(MeshDoc[] meshes, string meshesRoot)
    {
        Dictionary<string, Mesh> map = new Dictionary<string, Mesh>(StringComparer.Ordinal);
        if (meshes == null)
            return map;

        for (int i = 0; i < meshes.Length; i++)
        {
            MeshDoc source = meshes[i];
            if (source == null || string.IsNullOrEmpty(source.meshId))
                continue;

            string path = meshesRoot + "/" + Sanitize(source.meshId) + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, path);
            }

            mesh.name = source.meshId;
            mesh.Clear();
            mesh.vertices = ConvertVertices(source.vertices);
            mesh.triangles = source.triangleIndices ?? new int[0];

            Vector3[] normals = ConvertVertices(source.normals);
            if (normals.Length == mesh.vertexCount)
                mesh.normals = normals;
            else
                mesh.RecalculateNormals();

            Vector2[] uv0 = ConvertUv(source.uv0);
            if (uv0.Length == mesh.vertexCount)
                mesh.uv = uv0;

            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            map[source.meshId] = mesh;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return map;
    }

    private static GameObject BuildHierarchy(
        GeometryDoc geometry,
        Dictionary<string, MeshDoc> meshDocs,
        Dictionary<string, Mesh> meshMap,
        Dictionary<string, Material> materialMap)
    {
        if (geometry == null || geometry.nodes == null || geometry.nodes.Length == 0)
            throw new InvalidOperationException("FrameAngelPlayerShell2018Exporter: composed shell package did not contain geometry nodes.");

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
            throw new InvalidOperationException("FrameAngelPlayerShell2018Exporter: composed shell root was not found.");

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

            go.transform.localPosition = string.IsNullOrEmpty(node.parentNodeId)
                ? Vector3.zero
                : node.localPosition.ToUnity();
            go.transform.localRotation = node.localRotation.ToUnity();
            go.transform.localScale = IsZero(node.localScale) ? Vector3.one : node.localScale.ToUnity();

            if (node.meshRefIds == null)
                continue;

            for (int meshIndex = 0; meshIndex < node.meshRefIds.Length; meshIndex++)
            {
                string meshId = node.meshRefIds[meshIndex];
                if (string.IsNullOrEmpty(meshId) || !meshMap.ContainsKey(meshId))
                    continue;

                GameObject target = node.meshRefIds.Length == 1
                    ? go
                    : new GameObject("mesh_" + meshIndex.ToString(CultureInfo.InvariantCulture));
                if (target != go)
                    target.transform.SetParent(go.transform, false);

                MeshFilter meshFilter = target.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = meshMap[meshId];
                MeshRenderer meshRenderer = target.AddComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode = ShadowCastingMode.On;
                meshRenderer.receiveShadows = true;

                MeshDoc meshDoc;
                if (meshDocs.TryGetValue(meshId, out meshDoc) &&
                    meshDoc != null &&
                    !string.IsNullOrEmpty(meshDoc.materialRefId) &&
                    materialMap.ContainsKey(meshDoc.materialRefId))
                {
                    meshRenderer.sharedMaterial = materialMap[meshDoc.materialRefId];
                }
            }
        }

        return root;
    }

    private static void RenameNode(GameObject root, string sourceNodeId, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return;

        if (FindNode(root, targetName) != null)
            return;

        if (string.IsNullOrEmpty(sourceNodeId))
            return;

        GameObject node = FindNode(root, sourceNodeId);
        if (node != null)
            node.name = targetName;
    }

    private static void NormalizeSurfaceAsQuad(GameObject node, string quadMeshPath)
    {
        if (node == null)
            return;

        Vector3 originalScale = node.transform.localScale;
        DisableChildren(node.transform);
        ClearVisualComponents(node);
        node.transform.localScale = Vector3.one;

        MeshFilter meshFilter = node.GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = node.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = LoadOrCreateQuadMesh(quadMeshPath);

        MeshRenderer meshRenderer = node.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = node.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        node.transform.localScale = new Vector3(originalScale.x, originalScale.y, 1f);
    }

    private static void ApplyPackageMaterial(
        GameObject node,
        string nodeId,
        GeometryDoc geometry,
        Dictionary<string, MeshDoc> meshDocs,
        Dictionary<string, Material> materialMap)
    {
        if (node == null || geometry == null || geometry.nodes == null || string.IsNullOrEmpty(nodeId))
            return;

        Node sourceNode = geometry.nodes.FirstOrDefault(item => item != null && string.Equals(item.nodeId, nodeId, StringComparison.Ordinal));
        if (sourceNode == null || sourceNode.meshRefIds == null || sourceNode.meshRefIds.Length <= 0)
            return;

        string meshId = sourceNode.meshRefIds[0];
        MeshDoc meshDoc;
        if (!meshDocs.TryGetValue(meshId, out meshDoc) || meshDoc == null || string.IsNullOrEmpty(meshDoc.materialRefId))
            return;

        Material material;
        if (!materialMap.TryGetValue(meshDoc.materialRefId, out material) || material == null)
            return;

        MeshRenderer renderer = node.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
    }

    private static void PromoteDisplaySurfaceToVisibleFront(
        Transform root,
        GameObject screenSurface,
        GameObject disconnectSurface,
        GameObject screenGlass,
        GameObject controlsAnchor)
    {
        if (root == null || screenSurface == null)
            return;

        float shellFrontZ;
        if (!TryResolveShellFrontZ(root, screenSurface, disconnectSurface, screenGlass, out shellFrontZ))
            return;

        const float screenLift = 0.002f;
        const float disconnectGap = 0.002f;
        const float glassLift = 0.001f;

        Vector3 screenPosition = screenSurface.transform.localPosition;
        screenPosition.z = Mathf.Max(screenPosition.z, shellFrontZ + screenLift);
        screenSurface.transform.localPosition = screenPosition;

        if (disconnectSurface != null)
        {
            Vector3 disconnectPosition = disconnectSurface.transform.localPosition;
            disconnectPosition.z = Mathf.Min(disconnectPosition.z, screenPosition.z - disconnectGap);
            disconnectSurface.transform.localPosition = disconnectPosition;
        }

        if (screenGlass != null)
        {
            Vector3 glassPosition = screenGlass.transform.localPosition;
            glassPosition.z = Mathf.Max(glassPosition.z, screenPosition.z + glassLift);
            screenGlass.transform.localPosition = glassPosition;
        }

        if (controlsAnchor != null)
        {
            Vector3 controlsPosition = controlsAnchor.transform.localPosition;
            controlsPosition.z = Mathf.Max(controlsPosition.z, screenPosition.z + 0.01f);
            controlsAnchor.transform.localPosition = controlsPosition;
        }
    }

    private static bool TryResolveShellFrontZ(
        Transform root,
        GameObject screenSurface,
        GameObject disconnectSurface,
        GameObject screenGlass,
        out float frontZ)
    {
        frontZ = 0f;
        if (root == null)
            return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return false;

        bool found = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Transform rendererTransform = renderer.transform;
            if (rendererTransform == null)
                continue;

            if (screenSurface != null && rendererTransform.IsChildOf(screenSurface.transform))
                continue;

            if (disconnectSurface != null && rendererTransform.IsChildOf(disconnectSurface.transform))
                continue;

            if (screenGlass != null && rendererTransform.IsChildOf(screenGlass.transform))
                continue;

            Bounds bounds = renderer.bounds;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = new Vector3(
                            center.x + (extents.x * x),
                            center.y + (extents.y * y),
                            center.z + (extents.z * z));
                        Vector3 localCorner = root.InverseTransformPoint(corner);
                        if (!found || localCorner.z > frontZ)
                            frontZ = localCorner.z;
                        found = true;
                    }
                }
            }
        }

        return found;
    }

    private static GameObject EnsureAnchor(Transform parent, string nodeId, Vector3 localPosition)
    {
        Transform existing = parent.Find(nodeId);
        if (existing != null)
            return existing.gameObject;

        GameObject go = new GameObject(nodeId);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go;
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

    private static Mesh LoadOrCreateQuadMesh(string quadMeshPath)
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(quadMeshPath);
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
        AssetDatabase.CreateAsset(mesh, quadMeshPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return mesh;
    }

    private static Shader ResolveShader(MaterialDoc material)
    {
        Shader shader = null;
        if (material != null && !string.IsNullOrEmpty(material.shaderName))
            shader = Shader.Find(material.shaderName);
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        return shader;
    }

    private static void ConfigureMaterial(Material material)
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
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
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
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = -1;
                material.SetOverrideTag("RenderType", string.Empty);
            }
        }
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

    private static Vector3[] ConvertVertices(SerializableVector3[] values)
    {
        if (values == null || values.Length == 0)
            return new Vector3[0];

        Vector3[] output = new Vector3[values.Length];
        for (int i = 0; i < values.Length; i++)
            output[i] = values[i].ToUnity();
        return output;
    }

    private static Vector2[] ConvertUv(SerializableVector2[] values)
    {
        if (values == null || values.Length == 0)
            return new Vector2[0];

        Vector2[] output = new Vector2[values.Length];
        for (int i = 0; i < values.Length; i++)
            output[i] = values[i].ToUnity();
        return output;
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

    private static bool IsZero(SerializableVector3 value)
    {
        return Mathf.Approximately(value.x, 0f) &&
               Mathf.Approximately(value.y, 0f) &&
               Mathf.Approximately(value.z, 0f);
    }

    private static string ResolveFirstAssetName(string bundlePath, string fallbackPrefabPath)
    {
        AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
        if (bundle == null)
            return NormalizeAssetName(fallbackPrefabPath);

        try
        {
            string assetName = bundle.GetAllAssetNames().FirstOrDefault(name => name.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase));
            return string.IsNullOrEmpty(assetName) ? NormalizeAssetName(fallbackPrefabPath) : assetName;
        }
        finally
        {
            bundle.Unload(false);
        }
    }

    private static string NormalizeAssetName(string value)
    {
        return IsBlank(value) ? "" : value.Replace('\\', '/').ToLowerInvariant();
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

    private static void EnsureAssetFolder(string assetFolderPath)
    {
        string[] segments = assetFolderPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length <= 0 || !string.Equals(segments[0], "Assets", StringComparison.Ordinal))
            throw new InvalidOperationException("FrameAngelPlayerShell2018Exporter: temp asset folder must start with Assets/.");

        string current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string next = current + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[i]);
            current = next;
        }
    }

    private static void AssertFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("FrameAngelPlayerShell2018Exporter: required file was not found.", path);
    }

    private static void WriteTextFile(string path, string value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, value);
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

    private static bool IsBlank(string value)
    {
        return value == null || value.Trim().Length == 0;
    }
}
