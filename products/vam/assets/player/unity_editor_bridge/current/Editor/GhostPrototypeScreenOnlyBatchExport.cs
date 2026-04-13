using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace FrameAngel.UnityEditorBridge
{
    public static class GhostPrototypeScreenOnlyBatchExport
    {
        private const string DefaultScenePath = "Assets/Scenes/SampleScene.unity";
        private const string RoundedDisplayName = "Ghost Prototype Screen Only";
        private const string RectDisplayName = "Ghost Prototype Screen Rect";

        [Serializable]
        private sealed class BatchSummary
        {
            public string schemaVersion = "ghost_prototype_screen_batch_export_v1";
            public string projectPath = "";
            public string scenePath = "";
            public string generatedAtUtc = "";
            public bool capturePreview;
            public List<VariantSummary> variants = new List<VariantSummary>();
        }

        [Serializable]
        private sealed class VariantSummary
        {
            public string variantId = "";
            public string rootObjectId = "";
            public string variantOutputRoot = "";
            public string inspectJsonPath = "";
            public string exportJsonPath = "";
            public string lastExportJsonPath = "";
            public string packageId = "";
            public string resourceId = "";
            public string packageRootPath = "";
            public string geometryPath = "";
            public string materialsPath = "";
            public string screensPath = "";
            public string previewPath = "";
            public string exportReceiptPath = "";
            public string fingerprint = "";
        }

        public static void RunFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            string variant = GetArg(args, "-faVariant", "both").Trim().ToLowerInvariant();
            string scenePath = GetArg(args, "-faScenePath", DefaultScenePath);
            string outputRoot = GetArg(args, "-faOutputRoot", "");
            string summaryPath = GetArg(args, "-faSummaryPath", "");
            bool capturePreview = GetBoolArg(args, "-faCapturePreview", false);

            if (string.IsNullOrWhiteSpace(outputRoot))
            {
                throw new InvalidOperationException("Missing required -faOutputRoot argument.");
            }

            string absoluteOutputRoot = Path.GetFullPath(outputRoot);
            string absoluteSummaryPath = string.IsNullOrWhiteSpace(summaryPath)
                ? Path.Combine(absoluteOutputRoot, "headless_export_summary.json")
                : Path.GetFullPath(summaryPath);
            string absoluteScenePath = Path.IsPathRooted(scenePath)
                ? scenePath
                : Path.GetFullPath(Path.Combine(UnityBridgeInspector.ProjectPath, scenePath));

            if (!File.Exists(absoluteScenePath))
            {
                throw new FileNotFoundException("Unity scene was not found for headless export.", absoluteScenePath);
            }

            Directory.CreateDirectory(absoluteOutputRoot);
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            List<VariantSummary> variants = new List<VariantSummary>();
            if (variant == "both" || variant == "rounded")
            {
                variants.Add(ExportVariant(rectangular: false, absoluteOutputRoot, capturePreview));
            }

            if (variant == "both" || variant == "rect")
            {
                variants.Add(ExportVariant(rectangular: true, absoluteOutputRoot, capturePreview));
            }

            if (variants.Count == 0)
            {
                throw new InvalidOperationException("Variant must be one of: both, rounded, rect.");
            }

            BatchSummary summary = new BatchSummary
            {
                projectPath = UnityBridgeInspector.ProjectPath,
                scenePath = absoluteScenePath,
                generatedAtUtc = UnityBridgeInspector.TimestampUtc(),
                capturePreview = capturePreview,
                variants = variants
            };

            string summaryDirectory = Path.GetDirectoryName(absoluteSummaryPath);
            if (!string.IsNullOrWhiteSpace(summaryDirectory))
            {
                Directory.CreateDirectory(summaryDirectory);
            }

            File.WriteAllText(absoluteSummaryPath, JsonConvert.SerializeObject(summary, Formatting.Indented));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        private static VariantSummary ExportVariant(bool rectangular, string outputRoot, bool capturePreview)
        {
            string resourceStem = rectangular ? "ghost_prototype_screen_rect" : "ghost_prototype_screen_only";
            string variantId = rectangular ? "rect" : "rounded";
            string variantOutputRoot = Path.Combine(outputRoot, variantId);
            string displayName = rectangular ? RectDisplayName : RoundedDisplayName;
            string rootId = resourceStem + ".g001";
            const float screenWidth = 1.24f;
            const float screenHeight = 0.76f;
            const float screenCornerRadius = 0.075f;
            const float screenSurfaceDepth = 0.010f;
            const float disconnectDepth = 0.008f;

            Directory.CreateDirectory(variantOutputRoot);

            EnsureSuccess(
                UnityBridgeWorkspaceService.WorkspaceReset(CreateRequest("scene.workspace_reset", new UnityWorkspaceResetArgs())),
                "scene.workspace_reset");

            EnsureSuccess(
                UnityBridgeWorkspaceService.GroupRootUpsert(CreateRequest("scene.group_root_upsert", new UnityGroupRootUpsertArgs
                {
                    ObjectId = rootId,
                    Position = new UnityVector3(),
                    RotationEuler = new UnityVector3(),
                    Scale = new UnityVector3 { X = 1f, Y = 1f, Z = 1f },
                    Active = true
                })),
                "scene.group_root_upsert[root]");

            if (rectangular)
            {
                EnsureSuccess(
                    UnityBridgeWorkspaceService.PrimitiveUpsert(CreateRequest("scene.primitive_upsert", new UnityPrimitiveUpsertArgs
                    {
                        ObjectId = rootId + ".screen_surface",
                        ParentObjectId = rootId,
                        Kind = "cube",
                        Position = new UnityVector3(),
                        RotationEuler = new UnityVector3(),
                        Scale = new UnityVector3 { X = screenWidth, Y = screenHeight, Z = screenSurfaceDepth },
                        Active = true
                    })),
                    "scene.primitive_upsert[screen_surface]");

                EnsureSuccess(
                    UnityBridgeWorkspaceService.PrimitiveUpsert(CreateRequest("scene.primitive_upsert", new UnityPrimitiveUpsertArgs
                    {
                        ObjectId = rootId + ".disconnect_surface",
                        ParentObjectId = rootId,
                        Kind = "cube",
                        Position = new UnityVector3 { Z = -0.011f },
                        RotationEuler = new UnityVector3(),
                        Scale = new UnityVector3 { X = screenWidth, Y = screenHeight, Z = disconnectDepth },
                        Active = true
                    })),
                    "scene.primitive_upsert[disconnect_surface]");
            }
            else
            {
                EnsureSuccess(
                    UnityBridgeWorkspaceService.RoundedRectPrismUpsert(CreateRequest("scene.rounded_rect_prism_upsert", new UnityRoundedRectPrismUpsertArgs
                    {
                        ObjectId = rootId + ".screen_surface",
                        ParentObjectId = rootId,
                        Position = new UnityVector3(),
                        RotationEuler = new UnityVector3(),
                        Width = screenWidth,
                        Height = screenHeight,
                        Depth = screenSurfaceDepth,
                        CornerRadius = screenCornerRadius,
                        CornerSegments = 14,
                        Active = true
                    })),
                    "scene.rounded_rect_prism_upsert[screen_surface]");

                EnsureSuccess(
                    UnityBridgeWorkspaceService.RoundedRectPrismUpsert(CreateRequest("scene.rounded_rect_prism_upsert", new UnityRoundedRectPrismUpsertArgs
                    {
                        ObjectId = rootId + ".disconnect_surface",
                        ParentObjectId = rootId,
                        Position = new UnityVector3 { Z = -0.011f },
                        RotationEuler = new UnityVector3(),
                        Width = screenWidth,
                        Height = screenHeight,
                        Depth = disconnectDepth,
                        CornerRadius = screenCornerRadius,
                        CornerSegments = 14,
                        Active = true
                    })),
                    "scene.rounded_rect_prism_upsert[disconnect_surface]");
            }

            EnsureSuccess(
                UnityBridgeMaterialStyleService.MaterialStyleUpsert(CreateRequest("scene.material_style_upsert", new UnityMaterialStyleUpsertArgs
                {
                    StyleId = rootId + ".screen_surface.style",
                    TargetObjectId = rootId + ".screen_surface",
                    Scope = "object",
                    ShaderPreset = "unlit_screen",
                    SourceMode = "solid_color",
                    BaseColorHex = "#0F1318FF",
                    Metallic = 0f,
                    Smoothness = 0f
                })),
                "scene.material_style_upsert[screen_surface]");

            EnsureSuccess(
                UnityBridgeMaterialStyleService.MaterialStyleUpsert(CreateRequest("scene.material_style_upsert", new UnityMaterialStyleUpsertArgs
                {
                    StyleId = rootId + ".disconnect_surface.style",
                    TargetObjectId = rootId + ".disconnect_surface",
                    Scope = "object",
                    ShaderPreset = "standard_opaque",
                    SourceMode = "solid_color",
                    BaseColorHex = "#2A3440FF",
                    Metallic = 0.03f,
                    Smoothness = 0.10f
                })),
                "scene.material_style_upsert[disconnect_surface]");

            EnsureSuccess(
                UnityBridgeWorkspaceService.PlayerScreenAuthoringUpsert(CreateRequest("scene.player_screen_authoring_upsert", new UnityPlayerScreenAuthoringUpsertArgs
                {
                    RootObjectId = rootId,
                    ShellId = resourceStem,
                    ScreenContractVersion = "frameangel_screen_contract_v1",
                    DefaultDisconnectStateId = "media_controls",
                    SurfaceTargetId = "player:screen",
                    SlotId = "main",
                    SlotSurfaceTargetId = "player:screen",
                    SlotDisconnectStateId = "media_controls",
                    ScreenSurfaceObjectId = rootId + ".screen_surface",
                    DisconnectSurfaceObjectId = rootId + ".disconnect_surface"
                })),
                "scene.player_screen_authoring_upsert");

            EnsureSuccess(
                UnityBridgeWorkspaceService.GroupRootUpsert(CreateRequest("scene.group_root_upsert", new UnityGroupRootUpsertArgs
                {
                    ObjectId = rootId,
                    Position = new UnityVector3(),
                    RotationEuler = new UnityVector3(),
                    Scale = new UnityVector3 { X = 1f, Y = 1f, Z = 1f },
                    Active = true
                })),
                "scene.group_root_upsert[reselect]");

            string inspectPath = Path.Combine(variantOutputRoot, "inspect.json");
            UnityInnerPieceInspectionData inspection = UnityBridgeInnerPieceService.InspectSelectionFromWindow(out string inspectError);
            if (inspection == null)
            {
                throw new InvalidOperationException("asset.innerpiece.inspect_selection failed: " + inspectError);
            }

            WriteJson(
                inspectPath,
                new UnityBridgeResponse
                {
                    RequestId = "batch_inspect_selection",
                    Data = new Dictionary<string, object>
                    {
                        { "inspection", inspection },
                        { "policyProfile", UnityBridgeController.InnerPiecePolicyProfile }
                    }
                });

            if (!inspection.ExportReady)
            {
                throw new InvalidOperationException("Selection is not export-ready for variant '" + variantId + "'.");
            }

            string exportPath = Path.Combine(variantOutputRoot, "export.json");
            UnityBridgeResponse exportResponse = UnityBridgeInnerPieceService.ExportSelection(CreateRequest(
                "asset.innerpiece.export_selection",
                new UnityInnerPieceExportSelectionArgs
                {
                    DisplayNameOverride = displayName,
                    OutputPath = variantOutputRoot,
                    CapturePreview = capturePreview,
                    Tags = new List<string>
                    {
                        "ghost",
                        "prototype",
                        "screen",
                        "screen-only",
                        "integration",
                        rectangular ? "rect" : "rounded"
                    }
                }));
            EnsureSuccess(exportResponse, "asset.innerpiece.export_selection");
            WriteJson(exportPath, exportResponse);

            UnityInnerPieceLastExportSummary lastExport = UnityBridgeController.LastInnerPieceExport;
            if (lastExport == null)
            {
                throw new InvalidOperationException("asset.innerpiece.get_last_export returned no data.");
            }

            string lastExportPath = Path.Combine(variantOutputRoot, "last_export.json");
            WriteJson(
                lastExportPath,
                new UnityBridgeResponse
                {
                    RequestId = "batch_get_last_export",
                    Data = new Dictionary<string, object>
                    {
                        { "lastExport", lastExport },
                        { "policyProfile", UnityBridgeController.InnerPiecePolicyProfile }
                    }
                });

            return new VariantSummary
            {
                variantId = variantId,
                rootObjectId = rootId,
                variantOutputRoot = variantOutputRoot,
                inspectJsonPath = inspectPath,
                exportJsonPath = exportPath,
                lastExportJsonPath = lastExportPath,
                packageId = lastExport.PackageId,
                resourceId = lastExport.ResourceId,
                packageRootPath = lastExport.PackageRootPath,
                geometryPath = lastExport.GeometryPath,
                materialsPath = lastExport.MaterialsPath,
                screensPath = lastExport.ScreensPath,
                previewPath = lastExport.PreviewPath,
                exportReceiptPath = lastExport.ExportReceiptPath,
                fingerprint = lastExport.Fingerprint
            };
        }

        private static UnityBridgeCommandRequest CreateRequest(string action, object args)
        {
            return new UnityBridgeCommandRequest
            {
                RequestId = "batch_" + action.Replace('.', '_'),
                Action = action,
                Args = args != null ? JObject.FromObject(args) : new JObject()
            };
        }

        private static void EnsureSuccess(UnityBridgeResponse response, string stage)
        {
            if (response == null)
            {
                throw new InvalidOperationException(stage + " returned no response.");
            }

            if (!response.Ok)
            {
                throw new InvalidOperationException(stage + " failed: " + response.Code + " " + response.Message);
            }
        }

        private static void WriteJson(string path, object value)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonConvert.SerializeObject(value, Formatting.Indented));
        }

        private static string GetArg(string[] args, string key, string fallback)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return fallback;
        }

        private static bool GetBoolArg(string[] args, string key, bool fallback)
        {
            string value = GetArg(args, key, null);
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            if (bool.TryParse(value, out bool parsed))
            {
                return parsed;
            }

            return fallback;
        }
    }
}
