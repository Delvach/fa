using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FrameAngel.UnityEditorBridge
{
    internal static class UnityBridgeMaterialStyleService
    {
        private const string GeneratedAssetsRoot = "Assets/FrameAngelGenerated";
        private const string GeneratedTexturesRoot = GeneratedAssetsRoot + "/Textures";
        private const string GeneratedMaterialsRoot = GeneratedAssetsRoot + "/Materials";

        public static UnityBridgeResponse ImportLocalTexture(UnityBridgeCommandRequest request)
        {
            UnityTextureImportLocalArgs args = request.Args != null
                ? request.Args.ToObject<UnityTextureImportLocalArgs>()
                : new UnityTextureImportLocalArgs();

            if (!TryValidateAssetId(args.TextureId, out string textureIdError))
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", textureIdError, request.RequestId);
            }

            if (string.IsNullOrWhiteSpace(args.SourcePath))
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "sourcePath is required.", request.RequestId);
            }

            string sourcePath = NormalizeAbsolutePath(args.SourcePath);
            if (!File.Exists(sourcePath))
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "Texture source file was not found at '" + sourcePath + "'.", request.RequestId);
            }

            string extension = Path.GetExtension(sourcePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".png";
            }

            string destinationFolder = NormalizeAssetFolder(args.DestinationFolder, GeneratedTexturesRoot);
            string assetPath = destinationFolder.TrimEnd('/') + "/" + args.TextureId + extension.ToLowerInvariant();
            string absolutePath = ToAbsoluteAssetPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? UnityBridgeInspector.ProjectPath);
            File.Copy(sourcePath, absolutePath, true);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
            {
                return UnityBridgeResponse.Error("TEXTURE_IMPORT_FAILED", "Unity could not load the imported texture asset.", request.RequestId);
            }

            return new UnityBridgeResponse
            {
                RequestId = request.RequestId,
                Data = new UnityImportedTextureData
                {
                    TextureId = args.TextureId,
                    AssetPath = assetPath,
                    AbsolutePath = absolutePath,
                    Width = texture.width,
                    Height = texture.height,
                    ContentType = ResolveContentType(extension)
                }
            };
        }

        public static UnityBridgeResponse MaterialStyleUpsert(UnityBridgeCommandRequest request)
        {
            UnityMaterialStyleUpsertArgs args = request.Args != null
                ? request.Args.ToObject<UnityMaterialStyleUpsertArgs>()
                : new UnityMaterialStyleUpsertArgs();

            if (!TryValidateAssetId(args.StyleId, out string styleIdError))
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", styleIdError, request.RequestId);
            }

            if (string.IsNullOrWhiteSpace(args.TargetObjectId))
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "targetObjectId is required.", request.RequestId);
            }

            string scope = (args.Scope ?? "object").Trim().ToLowerInvariant();
            if (scope != "object" && scope != "subtree")
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "scope must be 'object' or 'subtree'.", request.RequestId);
            }

            GameObject target = UnityBridgeWorkspaceService.ResolveObject(new UnityObjectReferenceArgs { ObjectId = args.TargetObjectId });
            if (target == null)
            {
                return UnityBridgeResponse.Error("OBJECT_NOT_FOUND", "Target object '" + args.TargetObjectId + "' was not found.", request.RequestId);
            }

            Shader shader = ResolveShader(args.ShaderPreset);
            if (shader == null)
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "shaderPreset must be 'standard_opaque' or 'unlit_screen'.", request.RequestId);
            }

            List<Renderer> renderers = ResolveTargetRenderers(target, scope);
            if (renderers.Count == 0)
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "The target object has no renderer targets for the requested scope.", request.RequestId);
            }

            string generatedTextureAssetPath = "";
            Texture texture = null;
            Material material = LoadOrCreateMaterial(args.StyleId, shader);
            material.shader = shader;

            if (!TryApplyStyleSource(material, args, out generatedTextureAssetPath, out string sourceError))
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", sourceError, request.RequestId);
            }

            if (!string.IsNullOrWhiteSpace(args.TextureAssetPath))
            {
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(args.TextureAssetPath);
            }

            if (!string.IsNullOrWhiteSpace(generatedTextureAssetPath))
            {
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(generatedTextureAssetPath);
            }

            ApplyShaderProperties(material, args, texture);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            foreach (Renderer renderer in renderers)
            {
                Undo.RecordObject(renderer, "FrameAngel material style apply");
                renderer.sharedMaterial = material;
                EditorUtility.SetDirty(renderer);
            }

            return new UnityBridgeResponse
            {
                RequestId = request.RequestId,
                Data = new UnityMaterialStyleData
                {
                    StyleId = args.StyleId,
                    MaterialAssetPath = AssetDatabase.GetAssetPath(material),
                    GeneratedTextureAssetPath = generatedTextureAssetPath,
                    TargetObjectId = args.TargetObjectId,
                    AppliedRendererCount = renderers.Count
                }
            };
        }

        private static bool TryApplyStyleSource(Material material, UnityMaterialStyleUpsertArgs args, out string generatedTextureAssetPath, out string error)
        {
            generatedTextureAssetPath = "";
            error = "";
            string sourceMode = (args.SourceMode ?? "solid_color").Trim().ToLowerInvariant();
            switch (sourceMode)
            {
                case "solid_color":
                    {
                        material.mainTexture = null;
                        Color baseColor = ParseColorOrDefault(args.BaseColorHex, Color.white);
                        material.color = baseColor;
                        SetMaterialColor(material, baseColor);
                        return true;
                    }
                case "imported_texture":
                    {
                        if (string.IsNullOrWhiteSpace(args.TextureAssetPath))
                        {
                            error = "textureAssetPath is required when sourceMode is 'imported_texture'.";
                            return false;
                        }

                        Texture2D importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(args.TextureAssetPath);
                        if (importedTexture == null)
                        {
                            error = "textureAssetPath '" + args.TextureAssetPath + "' could not be loaded.";
                            return false;
                        }

                        material.mainTexture = importedTexture;
                        SetTextureOnMaterial(material, importedTexture);
                        Color tint = ParseColorOrDefault(args.BaseColorHex, Color.white);
                        material.color = tint;
                        SetMaterialColor(material, tint);
                        return true;
                    }
                case "procedural_placeholder":
                    {
                        string preset = (args.ProceduralPreset ?? "").Trim().ToLowerInvariant();
                        if (string.IsNullOrWhiteSpace(preset))
                        {
                            error = "proceduralPreset is required when sourceMode is 'procedural_placeholder'.";
                            return false;
                        }

                        generatedTextureAssetPath = UpsertProceduralTextureAsset(args.StyleId, preset);
                        Texture2D proceduralTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(generatedTextureAssetPath);
                        if (proceduralTexture == null)
                        {
                            error = "Procedural placeholder texture for preset '" + preset + "' could not be loaded.";
                            return false;
                        }

                        material.mainTexture = proceduralTexture;
                        SetTextureOnMaterial(material, proceduralTexture);
                        Color baseColor = ParseColorOrDefault(args.BaseColorHex, Color.white);
                        material.color = baseColor;
                        SetMaterialColor(material, baseColor);
                        return true;
                    }
                default:
                    error = "sourceMode must be 'solid_color', 'imported_texture', or 'procedural_placeholder'.";
                    return false;
            }
        }

        private static void ApplyShaderProperties(Material material, UnityMaterialStyleUpsertArgs args, Texture texture)
        {
            float tilingX = Mathf.Approximately(args.TilingX, 0f) ? 1f : args.TilingX;
            float tilingY = Mathf.Approximately(args.TilingY, 0f) ? 1f : args.TilingY;
            material.mainTextureScale = new Vector2(tilingX, tilingY);
            material.mainTextureOffset = new Vector2(args.OffsetX, args.OffsetY);

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
                material.SetTextureScale("_MainTex", material.mainTextureScale);
                material.SetTextureOffset("_MainTex", material.mainTextureOffset);
            }

            if (material.shader != null && string.Equals(material.shader.name, "Standard", StringComparison.OrdinalIgnoreCase))
            {
                Color effectiveColor = material.HasProperty("_Color") ? material.GetColor("_Color") : material.color;
                ConfigureStandardBlendMode(material, effectiveColor.a < 0.999f);

                if (material.HasProperty("_Metallic"))
                {
                    material.SetFloat("_Metallic", Mathf.Clamp01(args.Metallic));
                }

                if (material.HasProperty("_Glossiness"))
                {
                    material.SetFloat("_Glossiness", Mathf.Clamp01(args.Smoothness));
                }
            }
        }

        private static Material LoadOrCreateMaterial(string styleId, Shader shader)
        {
            EnsureAssetFolders();
            string assetPath = GeneratedMaterialsRoot + "/" + styleId + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material != null)
            {
                return material;
            }

            material = new Material(shader) { name = styleId };
            AssetDatabase.CreateAsset(material, assetPath);
            return material;
        }

        private static string UpsertProceduralTextureAsset(string styleId, string preset)
        {
            EnsureAssetFolders();
            string fileName = styleId + "__" + preset + ".png";
            string assetPath = GeneratedTexturesRoot + "/" + fileName;
            string absolutePath = ToAbsoluteAssetPath(assetPath);
            Texture2D texture = BuildProceduralTexture(styleId, preset, 512, 512);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? UnityBridgeInspector.ProjectPath);
                File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = false;
                importer.SaveAndReimport();
            }

            return assetPath;
        }

        private static Texture2D BuildProceduralTexture(string styleId, string preset, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            int seed = ComputeSeed(styleId + "|" + preset);
            switch (preset)
            {
                case "tv_static":
                    FillTvStatic(texture, seed);
                    break;
                case "no_internet_connection":
                    FillNoInternet(texture, seed);
                    break;
                case "dead_battery":
                    FillDeadBattery(texture, seed);
                    break;
                default:
                    FillSolid(texture, new Color(0.1f, 0.1f, 0.1f, 1f));
                    break;
            }

            texture.Apply(false, false);
            return texture;
        }

        private static void FillTvStatic(Texture2D texture, int seed)
        {
            for (int y = 0; y < texture.height; y++)
            {
                float scanline = 0.82f + (((y % 6) == 0) ? -0.16f : 0f);
                for (int x = 0; x < texture.width; x++)
                {
                    float nx = (float)x / Mathf.Max(1, texture.width - 1);
                    float ny = (float)y / Mathf.Max(1, texture.height - 1);
                    float vignette = Mathf.Clamp01(1f - (Mathf.Abs((nx * 2f) - 1f) * 0.28f) - (Mathf.Abs((ny * 2f) - 1f) * 0.28f));
                    float noise = 0.25f + (Hash01(seed, x, y) * 0.75f);
                    float value = Mathf.Clamp01(noise * scanline * vignette);
                    texture.SetPixel(x, y, new Color(value, value, value, 1f));
                }
            }
        }

        private static void FillNoInternet(Texture2D texture, int seed)
        {
            Color background = new Color(0.03f, 0.03f, 0.04f, 1f);
            Color accent = new Color(0.88f, 0.25f, 0.18f, 1f);
            Color secondary = new Color(0.75f, 0.78f, 0.82f, 1f);
            float minSide = Mathf.Min(texture.width, texture.height);
            float boxHalfWidth = texture.width * 0.28f;
            float boxHalfHeight = texture.height * 0.16f;
            float centerX = texture.width * 0.5f;
            float centerY = texture.height * 0.56f;
            float border = Mathf.Max(3f, minSide * 0.012f);
            float barHeight = Mathf.Max(6f, minSide * 0.018f);

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    Color pixel = background;
                    bool inHeader = y > texture.height * 0.80f && y < texture.height * 0.88f;
                    if (inHeader)
                    {
                        float bandNoise = 0.9f + (Hash01(seed, x, y) * 0.1f);
                        pixel = secondary * bandNoise;
                    }

                    bool inBox = Mathf.Abs(x - centerX) <= boxHalfWidth && Mathf.Abs(y - centerY) <= boxHalfHeight;
                    bool onBorder = inBox &&
                        (Mathf.Abs(Mathf.Abs(x - centerX) - boxHalfWidth) <= border || Mathf.Abs(Mathf.Abs(y - centerY) - boxHalfHeight) <= border);
                    if (onBorder)
                    {
                        pixel = secondary;
                    }

                    bool onDiagonalA = Mathf.Abs((y - centerY) - ((x - centerX) * (boxHalfHeight / Mathf.Max(1f, boxHalfWidth)))) <= border * 1.5f;
                    bool onDiagonalB = Mathf.Abs((y - centerY) + ((x - centerX) * (boxHalfHeight / Mathf.Max(1f, boxHalfWidth)))) <= border * 1.5f;
                    if (inBox && (onDiagonalA || onDiagonalB))
                    {
                        pixel = accent;
                    }

                    bool inStatusBar = y > texture.height * 0.16f && y < (texture.height * 0.16f) + barHeight && Mathf.Abs(x - centerX) < texture.width * 0.25f;
                    if (inStatusBar)
                    {
                        pixel = accent * (0.85f + (Hash01(seed, x, y) * 0.15f));
                    }

                    texture.SetPixel(x, y, pixel);
                }
            }
        }

        private static void FillDeadBattery(Texture2D texture, int seed)
        {
            Color background = new Color(0.02f, 0.03f, 0.04f, 1f);
            Color outline = new Color(0.80f, 0.82f, 0.84f, 1f);
            Color warning = new Color(0.82f, 0.12f, 0.14f, 1f);
            float centerX = texture.width * 0.5f;
            float centerY = texture.height * 0.52f;
            float bodyHalfWidth = texture.width * 0.16f;
            float bodyHalfHeight = texture.height * 0.08f;
            float border = Mathf.Max(3f, Mathf.Min(texture.width, texture.height) * 0.01f);

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    float glow = 0.02f + (Hash01(seed, x, y) * 0.02f);
                    Color pixel = background + new Color(glow, glow, glow, 0f);

                    bool inBody = Mathf.Abs(x - centerX) <= bodyHalfWidth && Mathf.Abs(y - centerY) <= bodyHalfHeight;
                    bool onBorder = inBody &&
                        (Mathf.Abs(Mathf.Abs(x - centerX) - bodyHalfWidth) <= border || Mathf.Abs(Mathf.Abs(y - centerY) - bodyHalfHeight) <= border);
                    if (onBorder)
                    {
                        pixel = outline;
                    }

                    bool inTerminal = Mathf.Abs(x - centerX) <= bodyHalfWidth * 0.24f &&
                        y > centerY + bodyHalfHeight &&
                        y < centerY + bodyHalfHeight + (border * 2.5f);
                    if (inTerminal)
                    {
                        pixel = outline;
                    }

                    bool inCharge = x > centerX - bodyHalfWidth + border * 2f &&
                        x < centerX - bodyHalfWidth + border * 2f + (bodyHalfWidth * 0.65f) &&
                        y > centerY - bodyHalfHeight + border * 2f &&
                        y < centerY + bodyHalfHeight - border * 2f;
                    if (inCharge)
                    {
                        pixel = warning;
                    }

                    texture.SetPixel(x, y, pixel);
                }
            }
        }

        private static void FillSolid(Texture2D texture, Color color)
        {
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    texture.SetPixel(x, y, color);
                }
            }
        }

        private static int ComputeSeed(string value)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < value.Length; i++)
                {
                    hash = (hash * 31) + value[i];
                }

                return hash;
            }
        }

        private static float Hash01(int seed, int x, int y)
        {
            unchecked
            {
                int value = seed;
                value = (value * 397) ^ x;
                value = (value * 397) ^ y;
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                uint positive = (uint)value;
                return (positive % 10000u) / 9999f;
            }
        }

        private static List<Renderer> ResolveTargetRenderers(GameObject target, string scope)
        {
            if (target == null)
            {
                return new List<Renderer>();
            }

            if (string.Equals(scope, "subtree", StringComparison.OrdinalIgnoreCase))
            {
                return target.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer != null)
                    .Distinct()
                    .ToList();
            }

            Renderer rendererComponent = target.GetComponent<Renderer>();
            return rendererComponent == null ? new List<Renderer>() : new List<Renderer> { rendererComponent };
        }

        private static void EnsureAssetFolders()
        {
            EnsureAssetFolder("Assets", "FrameAngelGenerated");
            EnsureAssetFolder(GeneratedAssetsRoot, "Textures");
            EnsureAssetFolder(GeneratedAssetsRoot, "Materials");
        }

        private static void EnsureAssetFolder(string parent, string child)
        {
            string assetPath = parent.TrimEnd('/') + "/" + child;
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            AssetDatabase.CreateFolder(parent, child);
        }

        private static string NormalizeAssetFolder(string requestedFolder, string fallbackFolder)
        {
            if (string.IsNullOrWhiteSpace(requestedFolder))
            {
                EnsureAssetFolders();
                return fallbackFolder;
            }

            string normalized = requestedFolder.Replace('\\', '/').Trim();
            if (normalized.StartsWith(UnityBridgeInspector.ProjectPath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(UnityBridgeInspector.ProjectPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/');
            }

            if (!normalized.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                normalized = fallbackFolder;
            }

            string[] segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                EnsureAssetFolder(current, segments[i]);
                current += "/" + segments[i];
            }

            return normalized;
        }

        private static string NormalizeAbsolutePath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return Path.GetFullPath(path);
            }

            return Path.GetFullPath(Path.Combine(UnityBridgeInspector.ProjectPath, path));
        }

        private static string ToAbsoluteAssetPath(string assetPath)
        {
            string relative = assetPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(UnityBridgeInspector.ProjectPath, relative);
        }

        private static string ResolveContentType(string extension)
        {
            switch ((extension ?? "").Trim().ToLowerInvariant())
            {
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".tga":
                    return "image/x-tga";
                case ".psd":
                    return "image/vnd.adobe.photoshop";
                default:
                    return "image/png";
            }
        }

        private static Shader ResolveShader(string shaderPreset)
        {
            switch ((shaderPreset ?? "").Trim().ToLowerInvariant())
            {
                case "standard_opaque":
                    return Shader.Find("Standard");
                case "unlit_screen":
                    return Shader.Find("Unlit/Texture") ?? Shader.Find("Unlit/Color");
                default:
                    return null;
            }
        }

        private static bool TryValidateAssetId(string value, out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(value))
            {
                error = "A stable id is required.";
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                bool allowed =
                    (current >= 'a' && current <= 'z') ||
                    (current >= 'A' && current <= 'Z') ||
                    (current >= '0' && current <= '9') ||
                    current == '_' ||
                    current == '-' ||
                    current == '.';
                if (!allowed)
                {
                    error = "Ids may only contain letters, digits, '_', '-', or '.'.";
                    return false;
                }
            }

            return true;
        }

        private static Color ParseColorOrDefault(string colorHex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(colorHex))
            {
                return fallback;
            }

            return ColorUtility.TryParseHtmlString(colorHex, out Color parsed) ? parsed : fallback;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private static void SetTextureOnMaterial(Material material, Texture texture)
        {
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }
        }

        private static void ConfigureStandardBlendMode(Material material, bool transparent)
        {
            if (!transparent)
            {
                material.SetFloat("_Mode", 0f);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = -1;
                return;
            }

            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}
